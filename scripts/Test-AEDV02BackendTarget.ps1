$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'AEDV02BackendTarget.ps1')
$production = Get-AEDV02ProductionApiUrl
$allowed = @(
    @('http://localhost:5072', 'Local', $false),
    @('http://127.0.0.1:5072/', 'Local', $false),
    @('http://[::1]:5072', 'Local', $false),
    @($production, 'Production', $true)
)
foreach ($case in $allowed) {
    $actual = Resolve-AEDV02BackendTarget -ApiUrl $case[0] -BackendEnvironment $case[1] -AllowProduction:$case[2]
    if ($actual -ne $case[0].TrimEnd('/')) { throw 'Approved endpoint did not normalize correctly.' }
}
$rejected = @(
    @($production, 'Production', $false), @($production, 'Local', $true),
    @('http://api.3.1.238.6.sslip.io', 'Production', $true),
    @('https://other.example', 'Production', $true),
    @('https://api.3.1.238.6.sslip.io.evil.example', 'Production', $true),
    @('https://api.3.1.238.6.sslip.io:444', 'Production', $true),
    @('https://api.3.1.238.6.sslip.io/api', 'Production', $true),
    @('https://user:pass@api.3.1.238.6.sslip.io', 'Production', $true),
    @('https://api.3.1.238.6.sslip.io?token=x', 'Production', $true),
    @('https://api.3.1.238.6.sslip.io#fragment', 'Production', $true),
    @('http://localhost:5072', 'Production', $true),
    @('http://localhost:5072/api', 'Local', $false),
    @('http://localhost:5072?x=1', 'Local', $false),
    @('invalid', 'Local', $false), @('', 'Local', $false)
)
foreach ($case in $rejected) {
    $blocked = $false
    try { Resolve-AEDV02BackendTarget -ApiUrl $case[0] -BackendEnvironment $case[1] -AllowProduction:$case[2] | Out-Null }
    catch { $blocked = $true }
    if (!$blocked) { throw 'Unapproved endpoint was accepted.' }
}
Write-Output "Endpoint policy: $($allowed.Count + $rejected.Count) cases PASS; no network requests made."
