param(
    [Parameter(Mandatory)][string]$ReviewedHashesFile,
    [string]$KeyPath = "$HOME\Downloads\echo-protocol-ec2-key.pem"
)
& (Join-Path $PSScriptRoot 'Invoke-EchoWebRelease.ps1') -Action Rollback -ReviewedHashesFile $ReviewedHashesFile -KeyPath $KeyPath
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
