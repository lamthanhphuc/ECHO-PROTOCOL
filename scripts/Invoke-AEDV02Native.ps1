param(
    [Parameter(Mandatory)][string]$Executable,
    [Parameter(Mandatory)][string]$HostConfig,
    [Parameter(Mandatory)][string]$ClientConfig,
    [Parameter(Mandatory)][Security.SecureString]$HostPassword,
    [Parameter(Mandatory)][Security.SecureString]$ClientPassword,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [int]$TimeoutSeconds = 600
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'AEDV02NativeProof.ps1')
if ($HostPassword.Length -eq 0 -or $ClientPassword.Length -eq 0) { throw 'Both native credentials are required before starting any player.' }
$output = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $output -Force | Out-Null
foreach ($name in @('Host.log','Client.log','Host-readback.jsonl','Client-readback.jsonl','Host-room-ready.txt','Client-room-ready.txt','native-proof.json')) {
    if (Test-Path -LiteralPath (Join-Path $output $name)) { throw 'Evidence directory contains an earlier native run. Use a new directory.' }
}
$configs = @($HostConfig,$ClientConfig) | ForEach-Object { Get-Content -LiteralPath $_ -Raw | ConvertFrom-Json }
if ($configs[0].role -ne 'Host' -or $configs[1].role -ne 'Client' -or $configs[0].session -ne $configs[1].session -or
    $configs[0].username -eq $configs[1].username -or $configs[0].apiUrl -ne $configs[1].apiUrl) { throw 'Native configurations must describe two independent identities in one session.' }
foreach ($config in $configs) {
    if ($config.PSObject.Properties.Name -contains 'password') { throw 'Plaintext passwords are not permitted in native config artifacts.' }
    $uri = [Uri]$config.apiUrl
    if (!$uri.IsLoopback -or $uri.Scheme -ne 'http' -or !$config.gameplay -or [IO.Path]::GetFullPath($config.output) -ne $output) {
        throw 'Native proof requires explicit pilot Gameplay, loopback Backend, and the same evidence directory.'
    }
}
if (!(Test-Path -LiteralPath $Executable)) { throw 'Windows executable missing; native integration NOT RUN.' }
$buildReportPath = Join-Path (Split-Path ([IO.Path]::GetFullPath($Executable)) -Parent) 'build-report.json'
if (!(Test-Path -LiteralPath $buildReportPath)) { throw 'Build provenance report missing; native integration NOT RUN.' }
$buildReport = Get-Content -LiteralPath $buildReportPath -Raw | ConvertFrom-Json
if ($buildReport.result -ne 'Succeeded' -or $buildReport.candidate -notmatch '^[a-f0-9]{40}$') { throw 'Build provenance invalid.' }
$processes = @()
$record = [ordered]@{ status='NOT RUN'; candidate=$buildReport.candidate; apiUrl=$configs[0].apiUrl; session=$configs[0].session;
    startedUtc=[DateTime]::UtcNow.ToString('O'); executableHash=(Get-FileHash -LiteralPath $Executable -Algorithm SHA256).Hash }
try {
    foreach ($role in @('Host','Client')) {
        $configPath = if ($role -eq 'Host') { $HostConfig } else { $ClientConfig }
        $log = Join-Path $output "$role.log"
        $arguments = @('-aedHarnessConfig', [IO.Path]::GetFullPath($configPath), '-logFile', $log, '-screen-width','640','-screen-height','480','-windowed')
        $quoted = $arguments | ForEach-Object { '"' + $_.Replace('"','\"') + '"' }
        $credential = if ($role -eq 'Host') { $HostPassword } else { $ClientPassword }
        if ($credential.Length -eq 0) { throw 'Native credential is missing; player NOT RUN.' }
        $start = [Diagnostics.ProcessStartInfo]::new()
        $start.FileName = [IO.Path]::GetFullPath($Executable)
        $start.Arguments = $quoted -join ' '
        $start.UseShellExecute = $false; $start.CreateNoWindow = $true; $start.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
        $secret = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($credential)
        try { $start.EnvironmentVariables['ECHO_AED_PILOT_PASSWORD'] = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($secret) }
        finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($secret) }
        try { $processes += [Diagnostics.Process]::Start($start) }
        finally { $start.EnvironmentVariables.Remove('ECHO_AED_PILOT_PASSWORD') }
        if ($role -eq 'Host') {
            $hostDeadline = [DateTime]::UtcNow.AddSeconds(90)
            $ready = Join-Path $output 'Host-room-ready.txt'
            while (!(Test-Path -LiteralPath $ready) -and !$processes[0].HasExited -and [DateTime]::UtcNow -lt $hostDeadline) { Start-Sleep -Seconds 1 }
            if (!(Test-Path -LiteralPath $ready) -or (Get-Content -LiteralPath $ready -Raw) -ne $configs[0].session) {
                throw 'Host did not create its room; remote Client NOT RUN.'
            }
        }
    }
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        foreach ($process in $processes) { if ($process.HasExited) { throw "Native player PID $($process.Id) exited before transaction proof: $($process.ExitCode)." } }
        $hostReadback = Join-Path $output 'Host-readback.jsonl'
        $clientReadback = Join-Path $output 'Client-readback.jsonl'
        if ((Test-Path -LiteralPath $hostReadback) -and (Test-Path -LiteralPath $clientReadback)) {
            $hostRows = @(Read-AEDV02NativeRows -Path $hostReadback)
            $clientRows = @(Read-AEDV02NativeRows -Path $clientReadback)
            $proof=Find-AEDV02NativeProof -HostRows $hostRows -ClientRows $clientRows -ReceiptLines (Get-Content -LiteralPath (Join-Path $output 'Host.log'))
            if ($proof) {
                $record.status='PASS';$record.host=$proof.host;$record.remoteClient=$proof.remoteClient;$record.receipt=$proof.receipt;$record.initial=$proof.initial
            }
        }
        if ($record.status -eq 'PASS') { break }
        Start-Sleep -Seconds 2
    } while ([DateTime]::UtcNow -lt $deadline)
    if ($record.status -ne 'PASS') { $record.status='FAIL'; throw 'No complete native relief transaction observed. Startup/readback alone is not PASS.' }
}
finally {
    foreach ($process in $processes) {
        if (!$process.HasExited) { $process.Kill(); $process.WaitForExit() }
    }
    $record.finishedUtc=[DateTime]::UtcNow.ToString('O')
    $record | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $output 'native-proof.json') -Encoding UTF8
}
