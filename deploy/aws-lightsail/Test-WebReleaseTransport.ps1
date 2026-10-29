$ErrorActionPreference = 'Stop'
$temp = Join-Path ([IO.Path]::GetTempPath()) "echo-web-transport-test-$PID"
New-Item -ItemType Directory -Path $temp | Out-Null
try {
    $key = Join-Path $temp 'fake-key.pem'
    [IO.File]::WriteAllText($key, 'test-only')
    $reviewed = Join-Path $temp 'reviewed.sha256'
    $names = @('docker-compose.prod.yml', 'docker-compose.mongo-kernel.yml', 'docker-compose.backend-migration.override.yml')
    [IO.File]::WriteAllLines($reviewed, @($names | ForEach-Object { ('a' * 64) + '  ' + $_ }))
    $global:Calls = @()
    function Read-Host { $global:Approval }
    function ssh {
        $call = 'ssh ' + ($args -join ' ')
        $global:Calls += $call
        $global:LASTEXITCODE = if ($global:FailRemote -and $call -match 'web-rollback\.sh') { 255 } else { 0 }
    }
    function scp { $global:Calls += 'scp ' + ($args -join ' '); $global:LASTEXITCODE = 0 }
    $global:Approval = 'ROLLBACK WEB 164d078'
    & (Join-Path $PSScriptRoot 'Invoke-EchoWebRollback.ps1') -ReviewedHashesFile $reviewed -KeyPath $key
    if (@($global:Calls | Where-Object { $_ -match '^scp ' }).Count -ne 6) { throw 'SCP transfer count mismatch' }
    $remote = @($global:Calls | Where-Object { $_ -match '^ssh .*web-rollback\.sh' })
    if ($remote.Count -ne 1 -or $remote[0].Length -gt 1000 -or $remote[0] -match 'base64|authorize-security-group|0\.0\.0\.0/0') { throw 'Unsafe SSH command transport' }

    $global:Calls = @()
    $release = Join-Path $temp 'release'
    $backup = Join-Path $temp 'backup'
    New-Item -ItemType Directory -Path $release | Out-Null
    New-Item -ItemType Directory -Path $backup | Out-Null
    [IO.File]::WriteAllText((Join-Path $release 'COMMIT'), '164d078ee0eb1bcfa2aa8c6379067d658e7904f8')
    [IO.File]::WriteAllText((Join-Path $release 'web-image.tar.gz'), 'test-archive')
    [IO.File]::WriteAllText((Join-Path $release 'web-image-id.txt'), 'sha256:test')
    foreach ($name in @('postgres.dump', 'mongodb.archive.gz', 'production-comparison.csv')) {
        [IO.File]::WriteAllText((Join-Path $backup $name), 'test-only')
    }
    $archiveHash = (Get-FileHash -LiteralPath (Join-Path $release 'web-image.tar.gz') -Algorithm SHA256).Hash.ToLowerInvariant()
    [IO.File]::WriteAllText((Join-Path $release 'SHA256SUMS'), "$archiveHash  web-image.tar.gz`n")
    $backupLines = foreach ($name in @('postgres.dump', 'mongodb.archive.gz')) {
        $hash = (Get-FileHash -LiteralPath (Join-Path $backup $name) -Algorithm SHA256).Hash.ToLowerInvariant()
        "$hash  $name"
    }
    [IO.File]::WriteAllText((Join-Path $backup 'SHA256SUMS'), (($backupLines -join "`n") + "`n"))
    $global:Approval = 'DEPLOY WEB 164d078'
    & (Join-Path $PSScriptRoot 'Invoke-EchoWebRelease.ps1') -ReviewedHashesFile $reviewed -ReleaseDirectory $release -BackupEvidenceDirectory $backup -KeyPath $key
    if (@($global:Calls | Where-Object { $_ -match '^scp ' }).Count -ne 10) { throw 'Deploy transfer count mismatch' }
    if (-not @($global:Calls | Where-Object { $_ -match 'web-deploy\.sh' }).Count) { throw 'Deploy command missing' }
    $global:Calls = @()
    [IO.File]::WriteAllText((Join-Path $release 'SHA256SUMS'), (('0' * 64) + '  web-image.tar.gz'))
    try {
        & (Join-Path $PSScriptRoot 'Invoke-EchoWebRelease.ps1') -ReviewedHashesFile $reviewed -ReleaseDirectory $release -KeyPath $key
        throw 'Checksum mismatch was accepted'
    } catch {
        if ($_.Exception.Message -notmatch 'Checksum mismatch') { throw }
    }
    if ($global:Calls.Count -ne 0) { throw 'Transport ran after checksum mismatch' }
    $global:Calls = @()
    $global:FailRemote = $true
    $global:Approval = 'ROLLBACK WEB 164d078'
    try {
        & (Join-Path $PSScriptRoot 'Invoke-EchoWebRollback.ps1') -ReviewedHashesFile $reviewed -KeyPath $key
        throw 'Interrupted SSH was accepted'
    } catch {
        if ($_.Exception.Message -match 'Interrupted SSH was accepted') { throw }
    }
    if (-not @($global:Calls | Where-Object { $_ -match 'web-rollback\.sh' }).Count) { throw 'Remote failure was not exercised' }
    if (Test-Path -LiteralPath (Join-Path ([IO.Path]::GetTempPath()) "echo-web-cd-$PID")) { throw 'Temporary transport files survived failure' }
    'TRANSPORT_AND_CHECKSUM_TESTS_PASS'
}
finally {
    $resolved = [IO.Path]::GetFullPath($temp)
    $root = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    if ($resolved.StartsWith($root, [StringComparison]::OrdinalIgnoreCase) -and (Split-Path -Leaf $resolved) -eq "echo-web-transport-test-$PID") {
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}
