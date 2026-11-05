$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'Test-AEDV02BackendTarget.ps1')
$fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ('aed-production-config-fixtures-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixtureRoot | Out-Null
$generator = Join-Path $PSScriptRoot 'New-AEDV02ProductionSession.ps1'
$launcher = Join-Path $PSScriptRoot 'Invoke-AEDV02Native.ps1'
$two = & $generator -OutputDirectory (Join-Path $fixtureRoot 'two') -Usernames @('host_fixture','client_fixture')
$hostFixture = Get-Content -LiteralPath $two.hostConfig -Raw | ConvertFrom-Json
if ($hostFixture.backendEnvironment -ne 'Production' -or $hostFixture.gameplay -or $hostFixture.cabinet -or $hostFixture.autoRelief -or
    $hostFixture.PSObject.Properties.Name -contains 'password') { throw 'Production defaults must be Shadow/manual and contain no password.' }
$four = & $generator -OutputDirectory (Join-Path $fixtureRoot 'four') -Usernames @('host_fixture','client_fixture','client2_fixture','client3_fixture') -Gameplay -AutoRelief
if ($four.additionalClientConfig.Count -ne 2 -or !$four.gameplay) { throw 'Four-player explicit Gameplay configuration failed.' }
$cases = 2
foreach ($parameters in @(
    @{ Usernames=@('host_fixture','HOST_FIXTURE'); OutputDirectory=(Join-Path $fixtureRoot 'duplicate') },
    @{ Usernames=@('host_fixture','client_fixture'); OutputDirectory=(Join-Path $fixtureRoot 'auto'); AutoRelief=$true },
    @{ Usernames=@('host_fixture','client_fixture'); OutputDirectory=(Join-Path $fixtureRoot 'url'); ApiUrl='https://other.example' },
    @{ Usernames=@('host_fixture','client_fixture'); OutputDirectory=$two.output }
)) {
    $blocked = $false
    try { & $generator @parameters | Out-Null } catch { $blocked = $true }
    if (!$blocked) { throw 'Invalid production session configuration was accepted.' }
    $cases++
}
# No real credentials and no executable: these cases must stop before creating any player or HTTP request.
$fixturePassword = ConvertTo-SecureString 'fixture-only-not-a-real-account-password' -AsPlainText -Force
$launch = @{ Executable=(Join-Path $fixtureRoot 'missing.exe'); HostConfig=$two.hostConfig; ClientConfig=$two.clientConfig
    HostPassword=$fixturePassword; ClientPassword=$fixturePassword; OutputDirectory=$two.output; SessionOnly=$true }
foreach ($allowed in @($false,$true)) {
    $reason = ''
    try { & $launcher @launch -AllowProduction:$allowed | Out-Null } catch { $reason = $_.Exception.Message }
    $expected = if ($allowed) { '*Windows executable missing*' } else { '*Backend target rejected*' }
    if ($reason -notlike $expected) { throw "Unexpected launch guard result: $reason" }
    $cases++
}
$third = Get-Content -LiteralPath $four.additionalClientConfig[0] -Raw | ConvertFrom-Json
$third.username = ' HOST_FIXTURE '
$third | ConvertTo-Json | Set-Content -LiteralPath $four.additionalClientConfig[0] -Encoding UTF8
$reason = ''
try {
    & $launcher -Executable $launch.Executable -HostConfig $four.hostConfig -ClientConfig $four.clientConfig `
        -HostPassword $fixturePassword -ClientPassword $fixturePassword -OutputDirectory $four.output -SessionOnly -AllowProduction `
        -AdditionalClientConfig $four.additionalClientConfig -AdditionalClientPassword @($fixturePassword,$fixturePassword) | Out-Null
} catch { $reason = $_.Exception.Message }
if ($reason -notlike '*distinct backend identity*') { throw 'Normalized duplicate identity was not rejected before player startup.' }
$cases++
$fixturePassword.Dispose()
Write-Output "Production configuration/launcher: $cases cases PASS; no processes started or network requests made. Fixtures: $fixtureRoot"
