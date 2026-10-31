param(
    [string]$RepositoryRoot = (Split-Path $PSScriptRoot -Parent),
    [string]$EvidenceRoot,
    [string]$Commit = 'HEAD',
    [ValidateSet('All', 'EditMode', 'PlayMode')][string]$Mode = 'All',
    [string]$TestFilter,
    [string]$UnityEditor = 'C:\Program Files\Unity\Hub\Editor\6000.5.8f1\Editor\Unity.exe'
)

$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath($RepositoryRoot)
$project = Join-Path $repository 'KLTN'
$validator = Join-Path $repository 'scripts\check_unity_editmode_results.py'

if (-not (Test-Path -LiteralPath $UnityEditor -PathType Leaf)) { throw "Unity Editor not found: $UnityEditor" }
foreach ($path in @('Assets', 'Packages\manifest.json', 'Packages\packages-lock.json', 'ProjectSettings\ProjectVersion.txt')) {
    if (-not (Test-Path -LiteralPath (Join-Path $project $path))) { throw "Unity project dependency missing: $path" }
}
if (-not (Test-Path -LiteralPath $validator -PathType Leaf)) { throw "NUnit validator missing: $validator" }
if (-not (Get-Command python -ErrorAction SilentlyContinue)) { throw 'Python 3 is required for NUnit validation' }

$versionFile = Get-Content -LiteralPath (Join-Path $project 'ProjectSettings\ProjectVersion.txt')
if ($versionFile -notcontains 'm_EditorVersion: 6000.5.8f1') { throw 'Project is not pinned to Unity 6000.5.8f1' }
$revision = ($versionFile | Where-Object { $_ -like 'm_EditorVersionWithRevision:*' }) -replace '^.*\(([0-9a-f]+)\)$', '$1'
$installedVersion = (Get-Item -LiteralPath $UnityEditor).VersionInfo.ProductVersion
if ($installedVersion -ne "6000.5.8f1_$revision") { throw "Installed Unity version is $installedVersion; expected 6000.5.8f1_$revision" }

$manifest = Get-Content -LiteralPath (Join-Path $project 'Packages\manifest.json') -Raw | ConvertFrom-Json
if ($manifest.dependencies.'com.unity.test-framework' -ne '1.7.0' -or
    -not $manifest.dependencies.'com.photonengine.voice-fusion') { throw 'Required Unity test or Photon package is missing' }

$actualCommit = (& git -C $repository rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'RepositoryRoot is not a Git worktree' }
$targetCommit = (& git -C $repository rev-parse $Commit).Trim()
if ($LASTEXITCODE -ne 0 -or $actualCommit -ne $targetCommit) { throw "Worktree is not at target commit $Commit" }
$dirty = & git -C $repository status --porcelain --untracked-files=normal
if ($LASTEXITCODE -ne 0 -or $dirty) { throw 'Worktree must be clean; use an isolated Git worktree' }
& git -C $repository lfs fsck
if ($LASTEXITCODE -ne 0) { throw 'Git LFS checkout is incomplete' }

$lockfile = Join-Path $project 'Temp\UnityLockfile'
if (Test-Path -LiteralPath $lockfile) { throw "Unity project lock exists: $lockfile" }
if (Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'") {
    throw 'Close all Unity Editor processes before local CI'
}

if (-not $EvidenceRoot) { $EvidenceRoot = Join-Path (Split-Path $repository -Parent) 'UnityCI-Evidence' }
$evidenceBase = [IO.Path]::GetFullPath($EvidenceRoot)
if ($evidenceBase.Equals($repository, [StringComparison]::OrdinalIgnoreCase) -or
    $evidenceBase.StartsWith($repository.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'EvidenceRoot must be outside the source worktree'
}
$evidence = Join-Path $evidenceBase "$actualCommit-$(Get-Date -Format 'yyyyMMdd-HHmmss-fff')"
New-Item -ItemType Directory -Path $evidence -Force | Out-Null
Write-Host "Unity: $installedVersion"
Write-Host "Commit: $actualCommit"
Write-Host "Evidence: $evidence"

$failed = $false

$cleanupPaths = @(
    'KLTN/Assets/Resources/UI/Loading/Typography/CourierPrimeLoadingTMP.asset'
    'KLTN/Assets/Resources/UI/Loading/Typography/SpectralLoadingTMP.asset'
    'KLTN/Assets/Settings/UniversalRenderPipelineGlobalSettings.asset'
    'KLTN/ProjectSettings/ProjectSettings.asset'
)

$modes = if ($Mode -eq 'All') { @('EditMode', 'PlayMode') } else { @($Mode) }

try {
    foreach ($mode in $modes) {
        $modeDir = Join-Path $evidence $mode
        New-Item -ItemType Directory -Path $modeDir -Force | Out-Null
        $runRecord = Join-Path $modeDir 'run.txt'
        @("Commit=$actualCommit", "Unity=$installedVersion", "Mode=$mode", "Filter=$TestFilter", 'Status=RUNNING') | Set-Content -LiteralPath $runRecord
        $arguments = @('-batchmode', '-projectPath', $project, '-runTests', '-testPlatform', $mode,
            '-testResults', (Join-Path $modeDir 'results.xml'), '-logFile', (Join-Path $modeDir 'Editor.log'))
        if ($TestFilter) { $arguments += @('-testFilter', $TestFilter) }
        Write-Host "Running $mode tests..."
        $commandLine = ($arguments | ForEach-Object { if ($_ -match '\s') { '"' + $_ + '"' } else { $_ } }) -join ' '
        $unityProcess = Start-Process -FilePath $UnityEditor -ArgumentList $commandLine -PassThru -Wait -WindowStyle Hidden
        $unityExit = $unityProcess.ExitCode
        $deadline = [DateTime]::UtcNow.AddMinutes(2)
        while (Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'") {
            if ([DateTime]::UtcNow -gt $deadline) { throw "$mode Unity Editor did not exit after its launcher" }
            Start-Sleep -Seconds 1
        }
        $outcome = if ($unityExit -eq 0) { 'success' } else { 'failure' }
        Write-Host "$mode Unity exit code: $unityExit"
        & python $validator $modeDir $outcome $mode
        $gateExit = $LASTEXITCODE
        @("Commit=$actualCommit", "Unity=$installedVersion", "Mode=$mode", "Filter=$TestFilter", "UnityExitCode=$unityExit", "GateExitCode=$gateExit") |
            Set-Content -LiteralPath $runRecord
        if ($unityExit -ne 0 -or $gateExit -ne 0) { $failed = $true }
    }
}
finally {
    Write-Host "`n=== UNITY POST-TEST CLEANUP ==="

    if (Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'") {
        Write-Warning "Unity is still running; cleanup skipped."
        $failed = $true
    }
    else {
        $changed = @(
            & git -C $repository diff --name-only -- $cleanupPaths
        )

        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Could not inspect Unity-generated changes."
            $failed = $true
        }
        elseif ($changed.Count -gt 0) {
            & git -C $repository restore -- $changed

            if ($LASTEXITCODE -ne 0) {
                Write-Warning "Unity asset cleanup failed."
                $failed = $true
            }
            else {
                Write-Host "Restored Unity-generated assets:"
                $changed | ForEach-Object { Write-Host "  $_" }
            }
        }
    }

    $remaining = @(
        & git -C $repository status --porcelain --untracked-files=normal
    )

    if ($LASTEXITCODE -ne 0 -or $remaining.Count -gt 0) {
        Write-Warning "Worktree still contains changes:"
        $remaining | ForEach-Object { Write-Host $_ }
        $failed = $true
    }
    else {
        Write-Host "CLEANUP PASSED - worktree clean."
    }
}

if ($failed) { exit 1 }
exit 0
