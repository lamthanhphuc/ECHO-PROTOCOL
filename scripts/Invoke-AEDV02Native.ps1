param(
    [Parameter(Mandatory)][string]$Executable,
    [Parameter(Mandatory)][string]$HostConfig,
    [Parameter(Mandatory)][string]$ClientConfig,
    [Parameter(Mandatory)][Security.SecureString]$HostPassword,
    [Parameter(Mandatory)][Security.SecureString]$ClientPassword,
    [string[]]$AdditionalClientConfig = @(),
    [Security.SecureString[]]$AdditionalClientPassword = @(),
    [ValidateRange(0,1200)][int]$ObserveSecondsAfterProof = 0,
    [switch]$SessionOnly,
    [switch]$VisiblePlayers,
    [switch]$AllowProduction,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [int]$TimeoutSeconds = 600
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'AEDV02NativeProof.ps1')
. (Join-Path $PSScriptRoot 'AEDV02BackendTarget.ps1')
if ($HostPassword.Length -eq 0 -or $ClientPassword.Length -eq 0) { throw 'Both native credentials are required before starting any player.' }
$output = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $output -Force | Out-Null
foreach ($name in @('Host.log','Client.log','Host-readback.jsonl','Client-readback.jsonl','Host-room-ready.txt','Client-room-ready.txt','native-proof.json')) {
    if (Test-Path -LiteralPath (Join-Path $output $name)) { throw 'Evidence directory contains an earlier native run. Use a new directory.' }
}
$configPaths = @($HostConfig,$ClientConfig) + @($AdditionalClientConfig)
$credentials = @($HostPassword,$ClientPassword) + @($AdditionalClientPassword)
if ($configPaths.Count -gt 4 -or $configPaths.Count -ne $credentials.Count -or @($credentials | Where-Object { !$_.Length }).Count -gt 0) {
    throw 'Two to four independently authenticated native processes are required.'
}
$configs = @($configPaths | ForEach-Object { Get-Content -LiteralPath $_ -Raw | ConvertFrom-Json })
if ($configs[0].role -ne 'Host' -or $configs[1].role -ne 'Client' -or $configs[0].session -ne $configs[1].session -or
    $configs[0].username -eq $configs[1].username -or $configs[0].apiUrl -ne $configs[1].apiUrl) { throw 'Native configurations must describe two independent identities in one session.' }
foreach ($config in $configs) {
    if ($config.PSObject.Properties.Name -contains 'password') { throw 'Plaintext passwords are not permitted in native config artifacts.' }
    $environment = if ($config.backendEnvironment) { [string]$config.backendEnvironment } else { 'Local' }
    $null = Resolve-AEDV02BackendTarget -ApiUrl $config.apiUrl -BackendEnvironment $environment -AllowProduction:$AllowProduction
    if ((!$SessionOnly -and !$config.gameplay) -or [IO.Path]::GetFullPath($config.output) -ne $output) {
        throw 'Native proof requires explicit pilot Gameplay and the same evidence directory.'
    }
}
if (@($configs.username | ForEach-Object { $_.Trim().ToLowerInvariant() } | Select-Object -Unique).Count -ne $configs.Count) { throw 'Each native process needs a distinct backend identity.' }
for ($index=0; $index -lt $configs.Count; $index++) {
    $label=if ($index -eq 0) { 'Host' } elseif ($index -eq 1) { 'Client' } else { 'Client'+$index }
    $config=$configs[$index]
    $count=if ($config.playerCount) { [int]$config.playerCount } else { 2 }
    if (($index -gt 0 -and $config.role -ne 'Client') -or ($config.label -and $config.label -ne $label) -or
        ($index -gt 1 -and $config.label -ne $label) -or $count -ne $configs.Count -or
        $config.apiUrl -ne $configs[0].apiUrl -or $config.session -ne $configs[0].session) { throw 'Native topology/identity mismatch.' }
    if (Test-Path -LiteralPath (Join-Path $output "$label-world.jsonl")) { throw 'Native world evidence already exists.' }
}
if (!(Test-Path -LiteralPath $Executable)) { throw 'Windows executable missing; native integration NOT RUN.' }
$buildReportPath = Join-Path (Split-Path ([IO.Path]::GetFullPath($Executable)) -Parent) 'build-report.json'
if (!(Test-Path -LiteralPath $buildReportPath)) { throw 'Build provenance report missing; native integration NOT RUN.' }
$buildReport = Get-Content -LiteralPath $buildReportPath -Raw | ConvertFrom-Json
if ($buildReport.result -ne 'Succeeded' -or $buildReport.candidate -notmatch '^[a-f0-9]{40}$') { throw 'Build provenance invalid.' }
$processes = @()
$record = [ordered]@{ status='NOT RUN'; candidate=$buildReport.candidate; apiUrl=$configs[0].apiUrl; session=$configs[0].session;
    backendEnvironment= $(if ($configs[0].backendEnvironment) { $configs[0].backendEnvironment } else { 'Local' });
    startedUtc=[DateTime]::UtcNow.ToString('O'); executableHash=(Get-FileHash -LiteralPath $Executable -Algorithm SHA256).Hash }
try {
    for ($index=0; $index -lt $configs.Count; $index++) {
        $role = if ($index -eq 0) { 'Host' } elseif ($index -eq 1) { 'Client' } else { 'Client'+$index }
        $configPath = $configPaths[$index]
        $log = Join-Path $output "$role.log"
        $arguments = @('-aedHarnessConfig', [IO.Path]::GetFullPath($configPath), '-logFile', $log, '-screen-width','640','-screen-height','480','-windowed')
        $quoted = $arguments | ForEach-Object { '"' + $_.Replace('"','\"') + '"' }
        $credential = $credentials[$index]
        if ($credential.Length -eq 0) { throw 'Native credential is missing; player NOT RUN.' }
        $start = [Diagnostics.ProcessStartInfo]::new()
        $start.FileName = [IO.Path]::GetFullPath($Executable)
        $start.Arguments = $quoted -join ' '
        $start.UseShellExecute = $false; $start.CreateNoWindow = $true; $start.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
        $start.EnvironmentVariables['ECHO_AED_ALLOW_PRODUCTION'] = if ($AllowProduction) { '1' } else { '0' }
        if ($VisiblePlayers) { $start.WindowStyle = [Diagnostics.ProcessWindowStyle]::Normal }
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
        $ended=$false
        foreach ($process in $processes) {
            if ($process.HasExited) {
                if (!$SessionOnly) { throw "Native player PID $($process.Id) exited before transaction proof: $($process.ExitCode)." }
                $record.playerExit=@{pid=$process.Id;exitCode=$process.ExitCode};$ended=$true
            }
        }
        if ($SessionOnly) {
            if ($ended) { break }
            Start-Sleep -Seconds 1
            continue
        }
        $hostReadback = Join-Path $output 'Host-readback.jsonl'
        $clientReadback = Join-Path $output 'Client-readback.jsonl'
        if ((Test-Path -LiteralPath $hostReadback) -and (Test-Path -LiteralPath $clientReadback)) {
            $hostRows = @(Read-AEDV02NativeRows -Path $hostReadback)
            $clientRows = @(Read-AEDV02NativeRows -Path $clientReadback)
            $proof=Find-AEDV02NativeProof -HostRows $hostRows -ClientRows $clientRows -ReceiptLines (Get-Content -LiteralPath (Join-Path $output 'Host.log')) -ExpectedPlayers $configs.Count
            if ($proof) {
                $record.status='PASS';$record.host=$proof.host;$record.remoteClient=$proof.remoteClient;$record.receipt=$proof.receipt;$record.initial=$proof.initial
            }
        }
        if ($record.status -eq 'PASS') { break }
        Start-Sleep -Seconds 2
    } while ([DateTime]::UtcNow -lt $deadline)
    if ($SessionOnly) {
        if (Select-String -LiteralPath (Join-Path $output 'Host.log') -Pattern '[AED_P8][TOOL_SOURCE_INVALID]' -SimpleMatch -Quiet) {
            $record.status='FAIL';throw 'Native tool spawn source was rejected; inspect Host.log before claiming source coverage.'
        }
        foreach ($label in @('Host','Client')) {
            $rows=@(Read-AEDV02NativeRows -Path (Join-Path $output "$label-world.jsonl"))
            if (!@($rows | Where-Object { $_.phase -and $_.sessionState -eq 'InMatch' }).Count) {
                $record.status='FAIL';throw 'No actual gameplay world captured for a required native role.'
            }
        }
        $record.status='CAPTURED_UNVERIFIED';$record.requiresOwnerVerification=$true
    } elseif ($record.status -ne 'PASS') { $record.status='FAIL'; throw 'No complete native relief transaction observed. Startup/readback alone is not PASS.' }
    if ($ObserveSecondsAfterProof -gt 0) {
        Write-Output "Initial transaction proven; observing controls for $ObserveSecondsAfterProof seconds. Later scenario results are not inferred from this proof."
        $observeUntil = [DateTime]::UtcNow.AddSeconds($ObserveSecondsAfterProof)
        while ([DateTime]::UtcNow -lt $observeUntil) {
            foreach ($process in $processes) { if ($process.HasExited) { throw 'A controlled player exited; inspect lifecycle logs.' } }
            Start-Sleep -Seconds 1
        }
    }
}
finally {
    foreach ($process in $processes) {
        if (!$process.HasExited) { $process.Kill(); $process.WaitForExit() }
    }
    $record.finishedUtc=[DateTime]::UtcNow.ToString('O')
    $record | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $output 'native-proof.json') -Encoding UTF8
}
