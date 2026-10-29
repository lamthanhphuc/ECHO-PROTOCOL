param(
    [Parameter(Mandatory)][string]$ReviewedHashesFile,
    [string]$ConfigPath = "$HOME\.ssh\echo-ssm.conf"
)
& (Join-Path $PSScriptRoot 'Invoke-EchoWebRelease.ps1') -Action Rollback -ReviewedHashesFile $ReviewedHashesFile -ConfigPath $ConfigPath
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
