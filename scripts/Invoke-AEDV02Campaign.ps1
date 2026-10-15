param(
    [Parameter(Mandatory)][string]$StackJson,
    [string]$UnityEditor = 'C:\Program Files\Unity\Hub\Editor\6000.5.8f1\Editor\Unity.exe',
    [int]$NativeTimeoutSeconds = 600
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$stackPath = [IO.Path]::GetFullPath($StackJson)
$nativeRoot = [IO.Path]::GetFullPath((Join-Path $repo 'KLTN/Temp/AEDV02/Native')) + [IO.Path]::DirectorySeparatorChar
if (!$stackPath.StartsWith($nativeRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Stack metadata must belong to the isolated native test workspace.' }
$stack = Get-Content -LiteralPath $stackPath -Raw | ConvertFrom-Json
$candidate = (& rtk proxy git -C $repo rev-parse HEAD | Out-String).Trim()
$branch = (& rtk proxy git -C $repo branch --show-current | Out-String).Trim()
if ($branch -ne 'feature/aed-v02-full-integration' -or !$stack.ready -or $stack.candidate -ne $candidate -or $stack.runId -notmatch '^aedv02-[a-z0-9-]{1,48}$') {
    throw 'Campaign requires the feature branch and a healthy isolated Backend built from the same candidate HEAD.'
}
$api = [Uri]$stack.apiUrl
if (!$api.IsLoopback -or $api.Scheme -ne 'http' -or $stack.database -ne 'aedv02_native_test') { throw 'Non-test Backend rejected.' }
$protected = @('KLTN/Assets/Resources/RuntimeLogSettings.asset','KLTN/Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset')
$dirty = @((& rtk proxy git -C $repo status --porcelain -z | Out-String).Split([char]0) | Where-Object { $_.Trim().Length -gt 0 })
foreach ($entry in $dirty) {
    if ($entry.Length -lt 4 -or $entry.Substring(3).TrimEnd("`r","`n") -notin $protected) { throw 'Commit the complete candidate source before the full campaign.' }
}
$output = Join-Path $repo ('KLTN/Temp/AEDV02/Campaigns/' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmssfff'))
New-Item -ItemType Directory -Path $output -Force | Out-Null
$report = [ordered]@{candidate=$candidate;branch=$branch;stack=$stack.runId;startedUtc=[DateTime]::UtcNow.ToString('O');
    backend='NOT RUN';EditMode='NOT RUN';PlayMode='NOT RUN';WindowsBuild='NOT RUN';native='NOT RUN';status='NOT READY'}
$previousConnection=$env:ECHO_M4_POSTGRES_CONNECTION
$previousMongo=$env:ECHO_PHASE6R_MONGO_URI
try {
    & rtk proxy powershell -NoProfile -File (Join-Path $PSScriptRoot 'Sync-AEDV02Contract.ps1') -Check
    if ($LASTEXITCODE -ne 0) { throw 'Source contracts or scene manifest differ; suites NOT RUN.' }
    if ([IO.Path]::GetFullPath($stack.compose) -ne (Join-Path (Split-Path $stackPath -Parent) 'compose.json')) {
        throw 'Stack compose path does not belong to this isolated run.'
    }
    $compose = Get-Content -LiteralPath $stack.compose -Raw | ConvertFrom-Json
    if ($compose.services.postgres.environment.POSTGRES_DB -ne 'aedv02_native_test' -or $compose.services.postgres.container_name -or
        $compose.services.backend.environment.ASPNETCORE_ENVIRONMENT -ne 'Development') { throw 'Stack is not the disposable Development topology.' }
    $postgresContainer=(& rtk proxy docker compose -p $stack.runId -f $stack.compose ps -q postgres | Out-String).Trim()
    if (!$postgresContainer) { throw 'Disposable PostgreSQL container missing.' }
    $labels=(& rtk proxy docker inspect --format '{{json .Config.Labels}}' $postgresContainer | Out-String) | ConvertFrom-Json
    if ($labels.'com.docker.compose.project' -ne $stack.runId -or $labels.'com.docker.compose.service' -ne 'postgres') {
        throw 'PostgreSQL container ownership mismatch; no test database created.'
    }
    & rtk proxy docker compose -p $stack.runId -f $stack.compose exec -T postgres psql -U postgres -d postgres -v ON_ERROR_STOP=1 -c 'CREATE DATABASE echo_protocol_test;'
    if ($LASTEXITCODE -ne 0) { throw 'Could not create a fresh isolated suite database. Use a new stack.' }
    $connection = [System.Data.Common.DbConnectionStringBuilder]::new()
    $connection['Host']='127.0.0.1';$connection['Port']=[int]$stack.postgresPort
    $connection['Database']='echo_protocol_test';$connection['Username']='postgres';$connection['Password']=$compose.services.postgres.environment.POSTGRES_PASSWORD
    $env:ECHO_M4_POSTGRES_CONNECTION=$connection.ConnectionString
    $env:ECHO_PHASE6R_MONGO_URI="mongodb://127.0.0.1:$($stack.mongoPort)/?directConnection=true"
    try {
        & (Join-Path $PSScriptRoot 'Test-AEDV02.ps1') -BackendOnly -OutputDirectory (Join-Path $output 'Backend')
        $report.backend=@{status='PASS';counts=(Get-Content -LiteralPath (Join-Path $output 'Backend/test-summary.json') -Raw | ConvertFrom-Json).backend}
    } catch {
        $backendSummary=Join-Path $output 'Backend/test-summary.json'
        $tests=if (Test-Path -LiteralPath $backendSummary) { (Get-Content -LiteralPath $backendSummary -Raw | ConvertFrom-Json).backend } else { 'NOT RUN' }
        $report.backend=@{status='FAIL';tests=$tests;reason=$_.Exception.Message}
    }
    foreach ($stage in @('EditMode','PlayMode','WindowsBuild')) {
        try {
            & (Join-Path $PSScriptRoot 'Invoke-AEDV02UnityBatch.ps1') -Stage $stage -OutputDirectory $output -UnityEditor $UnityEditor
            $report[$stage]=Get-Content -LiteralPath (Join-Path $output "$stage-summary.json") -Raw | ConvertFrom-Json
        } catch {
            $stageSummary=Join-Path $output "$stage-summary.json"
            $detail=if (Test-Path -LiteralPath $stageSummary) { Get-Content -LiteralPath $stageSummary -Raw | ConvertFrom-Json } else { $null }
            $report[$stage]=@{status=if ($detail) { $detail.status } else { 'NOT RUN' };details=$detail;reason=$_.Exception.Message}
        }
    }
    if ($report.WindowsBuild.status -eq 'PASS') {
        try {
            $nativeOutput=Join-Path $output 'Native'
            New-Item -ItemType Directory -Path $nativeOutput -Force | Out-Null
            $session='aed-' + [Guid]::NewGuid().ToString('N').Substring(0,20)
            $configPaths=@()
            foreach ($role in @('Host','Client')) {
                $username='aed' + $role.Substring(0,1).ToLowerInvariant() + [Guid]::NewGuid().ToString('N').Substring(0,12)
                $password='AEDpilot!' + [Guid]::NewGuid().ToString('N')
                $body=@{email=($username+'@example.invalid');username=$username;password=$password;confirmPassword=$password} | ConvertTo-Json
                $registered=Invoke-RestMethod -Uri ($stack.apiUrl+'/api/auth/register') -Method Post -ContentType 'application/json' -Body $body -TimeoutSec 30
                if (!$registered.success) { throw "Native $role registration failed." }
                $configPath=Join-Path $nativeOutput "$role-private-config.json"
                @{role=$role;apiUrl=$stack.apiUrl;username=$username;password=$password;session=$session;output=$nativeOutput;
                    gameplay=$true;cabinet=$false;autoRelief=$true;timeoutSeconds=$NativeTimeoutSeconds} |
                    ConvertTo-Json | Set-Content -LiteralPath $configPath -Encoding UTF8
                $configPaths += $configPath
                $password=$null;$body=$null
            }
            & (Join-Path $PSScriptRoot 'Invoke-AEDV02Native.ps1') -Executable (Join-Path $output 'Windows/EchoProtocol.exe') `
                -HostConfig $configPaths[0] -ClientConfig $configPaths[1] -OutputDirectory $nativeOutput -TimeoutSeconds $NativeTimeoutSeconds
            $report.native=Get-Content -LiteralPath (Join-Path $nativeOutput 'native-proof.json') -Raw | ConvertFrom-Json
        } catch { $report.native=@{status='FAIL';reason=$_.Exception.Message} }
    }
    # A native relief proof is one gate, not proof of all twelve scenarios, Fixed parity or reconnect.
    $report.status='GATES_RECORDED_NOT_OWNER_READY'
}
finally {
    $env:ECHO_M4_POSTGRES_CONNECTION=$previousConnection;$env:ECHO_PHASE6R_MONGO_URI=$previousMongo
    $password=$null;$body=$null;$connection=$null;$compose=$null
    $report.finishedUtc=[DateTime]::UtcNow.ToString('O')
    $report | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $output 'campaign-summary.json') -Encoding UTF8
    Write-Output "Campaign evidence: $output"
}
