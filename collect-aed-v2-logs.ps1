param(
    [Parameter(Mandatory = $true)][string]$EditorLog,
    [Parameter(Mandatory = $true)][string]$ClientLog,
    [string]$OutputDir = (Join-Path $env:USERPROFILE 'Desktop\AEDv2_E2E_Logs')
)

$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

$entries = @(
    @{ Source = $EditorLog; Output = 'AEDv2_Host.txt' },
    @{ Source = $ClientLog; Output = 'AEDv2_Client.txt' }
)
foreach ($entry in $entries) {
    if (-not (Test-Path -LiteralPath $entry.Source -PathType Leaf)) {
        throw "Log file not found: $($entry.Source)"
    }
    $lines = @(Get-Content -LiteralPath $entry.Source -Encoding UTF8 |
        Where-Object { $_ -match '\[(AED_E2E|AED_V2)\]' })
    $file = Join-Path $OutputDir $entry.Output
    Set-Content -LiteralPath $file -Value $lines -Encoding UTF8
    Write-Host "$file - $($lines.Count) AED lines"
}
Write-Host 'Review the output for private data before sharing.'
