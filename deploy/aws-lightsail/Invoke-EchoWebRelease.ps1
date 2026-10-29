param(
    [ValidateSet('Deploy', 'Rollback')][string]$Action = 'Deploy',
    [Parameter(Mandatory)][string]$ReviewedHashesFile,
    [string]$ReleaseDirectory,
    [string]$BackupEvidenceDirectory = 'D:\ECHO_PROTOCOL_SECURE_BACKUPS\pre-release-ltp-20261029T061910Z',
    [string]$ConfigPath = "$HOME\.ssh\echo-ssm.conf"
)

$ErrorActionPreference = 'Stop'
$sha = '164d078ee0eb1bcfa2aa8c6379067d658e7904f8'
$remoteDir = "/home/ubuntu/echo-protocol/deploy-artifacts/$sha/operator-scripts"
$remoteRelease = "/home/ubuntu/echo-protocol/deploy-artifacts/$sha"
$hostName = 'echo-ec2-ssm'
$scriptDir = $PSScriptRoot
$files = @('web-deploy.sh', 'web-rollback.sh', 'web-smoke.sh', 'web-audit.sh')

function Get-Sha256([string]$path) {
    $sha = [Security.Cryptography.SHA256]::Create()
    $stream = [IO.File]::OpenRead($path)
    try { return [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '').ToLowerInvariant() }
    finally { $stream.Dispose(); $sha.Dispose() }
}

function Assert-ChecksumFile([string]$directory, [string]$file, [string[]]$required) {
    $lines = @(Get-Content -LiteralPath (Join-Path $directory $file))
    $found = @()
    foreach ($line in $lines) {
        if ($line -cnotmatch '^([0-9a-fA-F]{64})  ([A-Za-z0-9._-]+)$') { throw "Invalid checksum manifest: $file" }
        $expected = $Matches[1]
        $name = $Matches[2]
        $found += $name
        $actual = Get-Sha256 (Join-Path $directory $name)
        if ($actual -ine $expected) { throw "Checksum mismatch: $name" }
    }
    foreach ($name in $required) { if ($name -notin $found) { throw "Missing checksum: $name" } }
}

if (-not (Test-Path -LiteralPath $ConfigPath -PathType Leaf)) { throw 'SSM SSH config unavailable' }
$effectiveSsh = (& ssh -G -T -F $ConfigPath $hostName) -join "`n"
if ($LASTEXITCODE -ne 0 -or
    $effectiveSsh -notmatch '(?m)^hostname i-00689d79dfa51a384$' -or
    $effectiveSsh -notmatch '(?m)^stricthostkeychecking (true|yes)$' -or
    $effectiveSsh -notmatch '(?m)^hostkeyalias 3\.1\.238\.6$' -or
    $effectiveSsh -notmatch '(?m)^proxycommand aws(\.exe)? ssm start-session\b') {
    throw 'SSM SSH target or host-key verification differs from the reviewed config'
}
if (-not (Test-Path -LiteralPath $ReviewedHashesFile -PathType Leaf)) { throw 'Reviewed Compose hash manifest unavailable' }
$hashLines = @(Get-Content -LiteralPath $ReviewedHashesFile)
$requiredHashes = @('docker-compose.prod.yml', 'docker-compose.mongo-kernel.yml', 'docker-compose.backend-migration.override.yml')
$hashNames = foreach ($line in $hashLines) {
    if ($line -cnotmatch '^[0-9a-fA-F]{64}  (docker-compose\.prod\.yml|docker-compose\.mongo-kernel\.yml|docker-compose\.backend-migration\.override\.yml)$') { throw 'Invalid reviewed Compose manifest' }
    $Matches[1]
}
if ($hashNames.Count -ne 3 -or (@($hashNames | Sort-Object -Unique).Count -ne 3)) { throw 'Reviewed Compose manifest needs exactly three files' }
foreach ($name in $requiredHashes) { if ($name -notin $hashNames) { throw "Missing reviewed hash: $name" } }

if ($Action -eq 'Deploy') {
    if (-not $ReleaseDirectory) { throw 'ReleaseDirectory is required' }
    if ((Get-Content -LiteralPath (Join-Path $ReleaseDirectory 'COMMIT') -Raw).Trim() -cne $sha) { throw 'Release commit mismatch' }
    $releaseEntries = @(Get-Content -LiteralPath (Join-Path $ReleaseDirectory 'SHA256SUMS'))
    $releaseNames = @()
    foreach ($line in $releaseEntries) {
        if ($line -cnotmatch '^([0-9a-fA-F]{64})  (backend-image\.tar\.gz|web-image\.tar\.gz)$') { throw 'Invalid release checksum manifest' }
        $releaseNames += $Matches[2]
        if ($Matches[2] -eq 'web-image.tar.gz') {
            $actual = Get-Sha256 (Join-Path $ReleaseDirectory 'web-image.tar.gz')
            if ($actual -ine $Matches[1]) { throw 'Checksum mismatch: web-image.tar.gz' }
        }
    }
    if ($releaseNames.Count -lt 1 -or $releaseNames.Count -gt 2 -or
        @($releaseNames | Sort-Object -Unique).Count -ne $releaseNames.Count -or
        'web-image.tar.gz' -notin $releaseNames) { throw 'Expected a Web-only or combined release manifest' }
    Assert-ChecksumFile $BackupEvidenceDirectory 'SHA256SUMS' @('postgres.dump', 'mongodb.archive.gz')
    if (-not (Test-Path -LiteralPath (Join-Path $BackupEvidenceDirectory 'production-comparison.csv'))) { throw 'Restore comparison evidence missing' }
}

$confirmation = if ($Action -eq 'Deploy') { 'DEPLOY WEB 164d078' } else { 'ROLLBACK WEB 164d078' }
if ((Read-Host "Type $confirmation to authorize the production operation") -cne $confirmation) { throw 'Operator approval not given' }

$tempDir = Join-Path ([IO.Path]::GetTempPath()) "echo-web-cd-$PID"
New-Item -ItemType Directory -Path $tempDir -ErrorAction Stop | Out-Null
try {
    [IO.File]::WriteAllText((Join-Path $tempDir 'reviewed-compose.sha256'), (($hashLines -join "`n") + "`n"), [Text.UTF8Encoding]::new($false))
    $manifest = foreach ($name in $files) {
        $digest = Get-Sha256 (Join-Path $scriptDir $name)
        "$digest  $name"
    }
    $reviewedDigest = Get-Sha256 (Join-Path $tempDir 'reviewed-compose.sha256')
    $manifest += "$reviewedDigest  reviewed-compose.sha256"
    [IO.File]::WriteAllText((Join-Path $tempDir 'scripts.sha256'), (($manifest -join "`n") + "`n"), [Text.UTF8Encoding]::new($false))
    $sshOptions = @('-F', $ConfigPath, '-o', 'BatchMode=yes', '-o', 'StrictHostKeyChecking=yes', '-o', 'ConnectTimeout=10')
    & ssh @sshOptions $hostName "install -d -m 700 '$remoteDir' '$remoteRelease'"
    if ($LASTEXITCODE -ne 0) { throw 'SSH unavailable; production state unchanged by this invocation' }
    foreach ($name in $files) {
        & scp @sshOptions (Join-Path $scriptDir $name) "${hostName}:$remoteDir/$name"
        if ($LASTEXITCODE -ne 0) { throw "SCP failed: $name" }
    }
    foreach ($name in @('scripts.sha256', 'reviewed-compose.sha256')) {
        & scp @sshOptions (Join-Path $tempDir $name) "${hostName}:$remoteDir/$name"
        if ($LASTEXITCODE -ne 0) { throw "SCP failed: $name" }
    }
    if ($Action -eq 'Deploy') {
        foreach ($name in @('COMMIT', 'SHA256SUMS', 'web-image-id.txt', 'web-image.tar.gz')) {
            & scp @sshOptions (Join-Path $ReleaseDirectory $name) "${hostName}:$remoteRelease/$name"
            if ($LASTEXITCODE -ne 0) { throw "SCP failed: $name" }
        }
    }
    $command = "cd '$remoteDir' && sha256sum -c scripts.sha256 >/dev/null && bash -n web-audit.sh web-smoke.sh web-deploy.sh web-rollback.sh && sudo -n bash '$remoteDir/web-$($Action.ToLowerInvariant()).sh' '$remoteDir'"
    & ssh @sshOptions $hostName $command
    if ($LASTEXITCODE -ne 0) { throw 'Remote operation failed or connection interrupted; inspect production state before retry' }
}
catch {
    Write-Error "MANUAL_INTERVENTION_REQUIRED: $($_.Exception.Message)"
    throw
}
finally {
    $resolved = [IO.Path]::GetFullPath($tempDir)
    $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    if ($resolved.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase) -and (Split-Path -Leaf $resolved) -eq "echo-web-cd-$PID") {
        Remove-Item -LiteralPath $resolved -Recurse -Force -ErrorAction SilentlyContinue
    }
}
