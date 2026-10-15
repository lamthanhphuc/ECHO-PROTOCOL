param(
    [Parameter(Mandatory)][string]$Executable,
    [Parameter(Mandatory)][string]$HostConfig,
    [Parameter(Mandatory)][string]$ClientConfig,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [int]$TimeoutSeconds = 600
)
$ErrorActionPreference = 'Stop'
$output = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $output -Force | Out-Null
foreach ($name in @('Host.log','Client.log','Host-readback.jsonl','Client-readback.jsonl','Host-room-ready.txt','Client-room-ready.txt','native-proof.json')) {
    if (Test-Path -LiteralPath (Join-Path $output $name)) { throw 'Evidence directory contains an earlier native run. Use a new directory.' }
}
$configs = @($HostConfig,$ClientConfig) | ForEach-Object { Get-Content -LiteralPath $_ -Raw | ConvertFrom-Json }
if ($configs[0].role -ne 'Host' -or $configs[1].role -ne 'Client' -or $configs[0].session -ne $configs[1].session -or
    $configs[0].username -eq $configs[1].username -or $configs[0].apiUrl -ne $configs[1].apiUrl) { throw 'Native configurations must describe two independent identities in one session.' }
foreach ($config in $configs) {
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
        $processes += Start-Process -FilePath $Executable -ArgumentList $quoted -PassThru -WindowStyle Hidden
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
            $hostRows = @(Get-Content -LiteralPath $hostReadback | ForEach-Object { $_ | ConvertFrom-Json })
            $clientRows = @(Get-Content -LiteralPath $clientReadback | ForEach-Object { $_ | ConvertFrom-Json })
            foreach ($row in $hostRows | Where-Object { $_.revision -gt 1 -and $_.consumerObserved -and $_.players -eq 2 }) {
                $remote = $clientRows | Where-Object { $_.matchId -eq $row.matchId -and $_.decisionId -eq $row.decisionId -and
                    $_.revision -eq $row.revision -and $_.fingerprint -eq $row.fingerprint -and $_.roster -eq $row.roster -and
                    $_.actionFingerprint -eq $row.actionFingerprint -and $_.scene -eq $row.scene -and $_.policy -eq $row.policy -and
                    $_.chaseSpeed -eq $row.chaseSpeed -and $_.players -eq 2 } | Select-Object -Last 1
                $initial = $hostRows | Where-Object { $_.matchId -eq $row.matchId -and $_.revision -eq 1 } | Select-Object -First 1
                if (!$remote -or !$initial -or $row.chaseSpeed -eq $initial.chaseSpeed) { continue }
                $receiptLine = Get-Content -LiteralPath (Join-Path $output 'Host.log') | Where-Object {
                    $_.Contains('[APPLY_RECEIPT]') -and $_.Contains($row.matchId) -and $_.Contains($row.decisionId)
                } | Select-Object -Last 1
                if (!$receiptLine) { continue }
                $record.status='PASS'; $record.host=$row; $record.remoteClient=$remote; $record.receipt=$receiptLine
                $record.initial=$initial
                break
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
