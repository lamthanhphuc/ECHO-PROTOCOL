param([string]$RealReleaseDirectory, [string]$RealBackupEvidenceDirectory)
$ErrorActionPreference = 'Stop'
function Get-Sha256([string]$path) {
    $sha = [Security.Cryptography.SHA256]::Create()
    $stream = [IO.File]::OpenRead($path)
    try { return [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '').ToLowerInvariant() }
    finally { $stream.Dispose(); $sha.Dispose() }
}
$temp = Join-Path ([IO.Path]::GetTempPath()) "echo-web-transport-test-$PID"
New-Item -ItemType Directory -Path $temp | Out-Null
try {
    $audit = Join-Path $PSScriptRoot 'web-audit.sh'
    if ([IO.File]::ReadAllBytes($audit) -contains 13) { throw 'Audit script has CRLF line endings' }
    $bash = if (Test-Path -LiteralPath 'D:\Git\bin\bash.exe') { 'D:\Git\bin\bash.exe' } else { 'bash' }
    $OutputEncoding = [Text.UTF8Encoding]::new($false)
    Get-Content -LiteralPath $audit -Raw -Encoding UTF8 | & $bash -n
    if ($LASTEXITCODE -ne 0) { throw 'Windows audit stdin transport failed Bash parsing' }
    $global:AuditArgs = @()
    $global:AuditInput = ''
    function ssh {
        begin { $global:AuditArgs = @($args) }
        process { $global:AuditInput += [string]$_ }
        end { $global:LASTEXITCODE = 0 }
    }
    Get-Content -LiteralPath $audit -Raw -Encoding UTF8 |
        ssh -T -F 'fake-ssm.conf' -o BatchMode=yes -o StrictHostKeyChecking=yes echo-ec2-ssm 'sudo -n bash -s'
    if ($global:AuditArgs[-1] -cne 'sudo -n bash -s' -or -not $global:AuditInput.StartsWith('#!/usr/bin/env bash')) {
        throw 'Windows SSH audit argument or stdin quoting changed'
    }
    $config = Join-Path $temp 'fake-ssm.conf'
    [IO.File]::WriteAllText($config, 'Host echo-ec2-ssm')
    $reviewed = Join-Path $temp 'reviewed.sha256'
    $names = @('docker-compose.prod.yml', 'docker-compose.mongo-kernel.yml', 'docker-compose.backend-migration.override.yml')
    [IO.File]::WriteAllLines($reviewed, @($names | ForEach-Object { ('a' * 64) + '  ' + $_ }))
    $global:Calls = @()
    function Read-Host { $global:Approval }
    function ssh {
        $call = 'ssh ' + ($args -join ' ')
        $global:Calls += $call
        if ($args -contains '-G') {
            $global:LASTEXITCODE = 0
            if ($global:BadSshConfig) { return @('hostname i-00689d79dfa51a384', 'stricthostkeychecking false') }
            return @('hostname i-00689d79dfa51a384', 'stricthostkeychecking true', 'hostkeyalias 3.1.238.6',
                'proxycommand aws.exe ssm start-session --target %h --document-name AWS-StartSSHSession')
        }
        $global:LASTEXITCODE = if ($global:FailRemote -and $call -match 'web-rollback\.sh') { 255 } else { 0 }
    }
    function scp { $global:Calls += 'scp ' + ($args -join ' '); $global:LASTEXITCODE = 0 }
    $global:Approval = 'ROLLBACK WEB 164d078'
    & (Join-Path $PSScriptRoot 'Invoke-EchoWebRollback.ps1') -ReviewedHashesFile $reviewed -ConfigPath $config
    if (@($global:Calls | Where-Object { $_ -match '^scp ' }).Count -ne 6) { throw 'SCP transfer count mismatch' }
    $remote = @($global:Calls | Where-Object { $_ -match '^ssh .*web-rollback\.sh' })
    if ($remote.Count -ne 1 -or $remote[0].Length -gt 1000 -or $remote[0] -match 'base64|authorize-security-group|0\.0\.0\.0/0|ubuntu@3\.1\.238\.6' -or
        $remote[0] -notmatch 'echo-ec2-ssm|StrictHostKeyChecking=yes') { throw 'Unsafe SSH command transport' }
    $global:Calls = @()
    $global:BadSshConfig = $true
    try {
        & (Join-Path $PSScriptRoot 'Invoke-EchoWebRollback.ps1') -ReviewedHashesFile $reviewed -ConfigPath $config
        throw 'Unsafe SSM SSH configuration was accepted'
    } catch {
        if ($_.Exception.Message -notmatch 'SSH config|SSM') { throw }
    } finally { $global:BadSshConfig = $false }
    if (@($global:Calls | Where-Object { $_ -match '^scp ' -or ($_ -match '^ssh ' -and $_ -notmatch ' -G ') }).Count -ne 0) {
        throw 'Remote transport ran despite unsafe SSH configuration'
    }

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
    $archiveHash = Get-Sha256 (Join-Path $release 'web-image.tar.gz')
    [IO.File]::WriteAllText((Join-Path $release 'SHA256SUMS'), "$archiveHash  web-image.tar.gz`n")
    $backupLines = foreach ($name in @('postgres.dump', 'mongodb.archive.gz')) {
        $hash = Get-Sha256 (Join-Path $backup $name)
        "$hash  $name"
    }
    [IO.File]::WriteAllText((Join-Path $backup 'SHA256SUMS'), (($backupLines -join "`n") + "`n"))
    $global:Approval = 'DEPLOY WEB 164d078'
    & (Join-Path $PSScriptRoot 'Invoke-EchoWebRelease.ps1') -ReviewedHashesFile $reviewed -ReleaseDirectory $release -BackupEvidenceDirectory $backup -ConfigPath $config
    if (@($global:Calls | Where-Object { $_ -match '^scp ' }).Count -ne 10) { throw 'Deploy transfer count mismatch' }
    if (-not @($global:Calls | Where-Object { $_ -match 'web-deploy\.sh' }).Count) { throw 'Deploy command missing' }
    $global:Calls = @()
    [IO.File]::WriteAllText((Join-Path $release 'SHA256SUMS'), (('0' * 64) + "  backend-image.tar.gz`n$archiveHash  web-image.tar.gz`n"))
    & (Join-Path $PSScriptRoot 'Invoke-EchoWebRelease.ps1') -ReviewedHashesFile $reviewed -ReleaseDirectory $release -BackupEvidenceDirectory $backup -ConfigPath $config
    if (@($global:Calls | Where-Object { $_ -match '^scp ' }).Count -ne 10 -or
        @($global:Calls | Where-Object { $_ -match 'backend-image\.tar\.gz' }).Count -ne 0) { throw 'Combined release transferred Backend archive' }
    $global:Calls = @()
    [IO.File]::WriteAllText((Join-Path $release 'SHA256SUMS'), (('0' * 64) + "  backend-image.tar.gz`n" + ('0' * 64) + '  web-image.tar.gz'))
    try {
        & (Join-Path $PSScriptRoot 'Invoke-EchoWebRelease.ps1') -ReviewedHashesFile $reviewed -ReleaseDirectory $release -BackupEvidenceDirectory $backup -ConfigPath $config
        throw 'Checksum mismatch was accepted'
    } catch {
        if ($_.Exception.Message -notmatch 'Checksum mismatch') { throw }
    }
    if (@($global:Calls | Where-Object { $_ -match '^scp ' -or ($_ -match '^ssh ' -and $_ -notmatch ' -G ') }).Count -ne 0) { throw 'Transport ran after checksum mismatch' }
    [IO.File]::WriteAllText((Join-Path $release 'SHA256SUMS'), (('0' * 64) + '  web-image.tar.gz'))
    try {
        & (Join-Path $PSScriptRoot 'Invoke-EchoWebRelease.ps1') -ReviewedHashesFile $reviewed -ReleaseDirectory $release -BackupEvidenceDirectory $backup -ConfigPath $config
        throw 'Web-only checksum mismatch was accepted'
    } catch {
        if ($_.Exception.Message -notmatch 'Checksum mismatch') { throw }
    }
    if (@($global:Calls | Where-Object { $_ -match '^scp ' -or ($_ -match '^ssh ' -and $_ -notmatch ' -G ') }).Count -ne 0) { throw 'Transport ran after Web-only checksum mismatch' }
    $global:Calls = @()
    $global:FailRemote = $true
    $global:Approval = 'ROLLBACK WEB 164d078'
    try {
        & (Join-Path $PSScriptRoot 'Invoke-EchoWebRollback.ps1') -ReviewedHashesFile $reviewed -ConfigPath $config
        throw 'Interrupted SSH was accepted'
    } catch {
        if ($_.Exception.Message -match 'Interrupted SSH was accepted') { throw }
    }
    if (-not @($global:Calls | Where-Object { $_ -match 'web-rollback\.sh' }).Count) { throw 'Remote failure was not exercised' }
    if (Test-Path -LiteralPath (Join-Path ([IO.Path]::GetTempPath()) "echo-web-cd-$PID")) { throw 'Temporary transport files survived failure' }
    if ($RealReleaseDirectory) {
        $global:FailRemote = $false
        $global:Approval = 'DEPLOY WEB 164d078'
        $global:Calls = @()
        & (Join-Path $PSScriptRoot 'Invoke-EchoWebRelease.ps1') -ReviewedHashesFile $reviewed -ReleaseDirectory $RealReleaseDirectory -BackupEvidenceDirectory $RealBackupEvidenceDirectory -ConfigPath $config
        if (@($global:Calls | Where-Object { $_ -match 'backend-image\.tar\.gz' }).Count -ne 0) { throw 'Real combined release transferred Backend archive' }
    }
    'TRANSPORT_AND_CHECKSUM_TESTS_PASS'
}
finally {
    $resolved = [IO.Path]::GetFullPath($temp)
    $root = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    if ($resolved.StartsWith($root, [StringComparison]::OrdinalIgnoreCase) -and (Split-Path -Leaf $resolved) -eq "echo-web-transport-test-$PID") {
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}
