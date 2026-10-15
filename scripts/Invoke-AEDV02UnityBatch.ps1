param(
    [Parameter(Mandatory)][ValidateSet('Manifest','EditMode','PlayMode','WindowsBuild')][string]$Stage,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string]$UnityEditor = 'C:\Program Files\Unity\Hub\Editor\6000.5.8f1\Editor\Unity.exe',
    [int]$TimeoutMinutes = 45
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$project = [IO.Path]::GetFullPath((Join-Path $repo 'KLTN'))
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (!(Test-Path -LiteralPath $UnityEditor)) { throw 'Unity 6000.5.8f1 executable missing; stage NOT RUN.' }
$editors = Get-CimInstance Win32_Process -Filter "Name='Unity.exe'"
foreach ($editor in $editors) {
    if ($editor.CommandLine -and $editor.CommandLine.IndexOf($project, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
        throw "Project already owned by Unity PID $($editor.ProcessId); do not start another Editor."
    }
}
$lockPath = Join-Path $project 'Temp/UnityLockfile'
if (Test-Path -LiteralPath $lockPath) {
    # An exclusive open proves a leftover file is not currently owned; never delete an active lock.
    $lockProbe = [IO.File]::Open($lockPath, 'Open', 'ReadWrite', 'None')
    $lockProbe.Dispose()
}
New-Item -ItemType Directory -Path $output -Force | Out-Null
$candidate = (& rtk proxy git -C $repo rev-parse HEAD | Out-String).Trim()
$dirty = (& rtk proxy git -C $repo diff --name-only | Out-String).Trim()
$log = Join-Path $output "$Stage.log"
$resultFile = Join-Path $output "$Stage.xml"
$argsList = @('-batchmode','-projectPath',$project,'-logFile',$log)
switch ($Stage) {
    'Manifest' { $argsList += @('-nographics','-quit','-executeMethod','AEDV02BatchBuild.ExportManifest') }
    'EditMode' { $argsList += @('-nographics','-runTests','-testPlatform','EditMode','-testResults',$resultFile) }
    'PlayMode' { $argsList += @('-runTests','-testPlatform','PlayMode','-testResults',$resultFile) }
    'WindowsBuild' {
        $argsList += @('-quit','-buildTarget','StandaloneWindows64','-executeMethod','AEDV02BatchBuild.WindowsDevelopment',
            '-aedBuildOutput',(Join-Path $output 'Windows/EchoProtocol.exe'),'-aedCandidate',$candidate)
    }
}
$started = [DateTime]::UtcNow
$record = [ordered]@{ stage=$Stage; candidate=$candidate; dirtyPaths=$dirty; startedUtc=$started.ToString('O'); status='NOT RUN' }
try {
    $quoted = $argsList | ForEach-Object { '"' + $_.Replace('"','\"') + '"' }
    $process = Start-Process -FilePath $UnityEditor -ArgumentList $quoted -PassThru -WindowStyle Hidden
    $record.pid = $process.Id
    if (!$process.WaitForExit($TimeoutMinutes * 60000)) {
        # Only the process created by this invocation can be terminated.
        $process.Kill()
        $process.WaitForExit()
        throw "$Stage timed out. Inspect $log."
    }
    $record.exitCode = $process.ExitCode
    if ($process.ExitCode -ne 0) { throw "$Stage exited with $($process.ExitCode). Inspect $log." }
    if ($Stage -in @('EditMode','PlayMode')) {
        if (!(Test-Path -LiteralPath $resultFile)) { throw 'Unity produced no test results; NOT RUN.' }
        [xml]$xml = Get-Content -LiteralPath $resultFile -Raw
        $run = $xml.'test-run'
        $record.passed = [int]$run.passed; $record.failed = [int]$run.failed
        $record.skipped = [int]$run.skipped; $record.total = [int]$run.total
        if ($record.total -lt 1 -or $run.result -ne 'Passed' -or $record.failed -ne 0 -or $record.skipped -ne 0) {
            $record.status = 'FAIL'
            throw "$Stage did not pass completely."
        }
    }
    if ($Stage -eq 'WindowsBuild') {
        $report = Join-Path $output 'Windows/build-report.json'
        if (!(Test-Path -LiteralPath $report)) { throw 'Build report missing.' }
        $build = Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
        if ($build.result -ne 'Succeeded' -or $build.errors -ne 0 -or $build.candidate -ne $candidate) { throw 'Build report invalid.' }
        $record.build = $build
    }
    $record.status = 'PASS'
}
finally {
    $record.finishedUtc = [DateTime]::UtcNow.ToString('O')
    $record | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $output "$Stage-summary.json") -Encoding UTF8
}
