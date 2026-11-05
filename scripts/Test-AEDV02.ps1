param([switch] $BackendOnly, [string] $OutputDirectory)
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'Assert-AEDV02LocalDocker.ps1')
$repo = Split-Path $PSScriptRoot -Parent
$output = if ($OutputDirectory) { [IO.Path]::GetFullPath($OutputDirectory) } else { Join-Path $repo ('KLTN/Temp/AEDV02/Runs/' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmssfff')) }
New-Item -ItemType Directory -Path $output -Force | Out-Null
$result = [ordered]@{ head = ''; startedUtc = [DateTime]::UtcNow.ToString('O'); backend = 'NOT RUN'; editor = 'NOT RUN'; playmode = 'NOT RUN' }
$previousConnection = $env:ECHO_M4_POSTGRES_CONNECTION
$previousMongo = $env:ECHO_PHASE6R_MONGO_URI
Push-Location $repo
try {
    $result.head = (& rtk proxy git rev-parse HEAD | Out-String).Trim()
    & rtk proxy powershell -NoProfile -File (Join-Path $PSScriptRoot 'Sync-AEDV02Contract.ps1') -Check
    if ($LASTEXITCODE -ne 0) { throw 'Shared contract/manifest copies differ.' }
    if ([string]::IsNullOrWhiteSpace($previousConnection)) {
        $password = & rtk proxy docker exec echo_protocol_postgres printenv POSTGRES_PASSWORD
        if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($password)) { throw 'Local PostgreSQL is unavailable; Backend NOT RUN.' }
        $connection = [System.Data.Common.DbConnectionStringBuilder]::new()
        $connection['Host'] = '127.0.0.1'; $connection['Port'] = 5433
        $connection['Database'] = 'echo_protocol_test'; $connection['Username'] = 'postgres'; $connection['Password'] = [string]$password
        $env:ECHO_M4_POSTGRES_CONNECTION = $connection.ConnectionString
    }
    $guard = [System.Data.Common.DbConnectionStringBuilder]::new()
    $guard.set_ConnectionString($env:ECHO_M4_POSTGRES_CONNECTION)
    if ($guard['Host'] -notin @('127.0.0.1', 'localhost', '::1') -or $guard['Database'] -ne 'echo_protocol_test') {
        throw 'Tests require loopback PostgreSQL and the isolated echo_protocol_test database.'
    }
    if ([string]::IsNullOrWhiteSpace($previousMongo)) {
        $mongoUser = & rtk proxy docker exec echo_protocol_mongodb printenv MONGO_INITDB_ROOT_USERNAME
        $mongoPassword = & rtk proxy docker exec echo_protocol_mongodb printenv MONGO_INITDB_ROOT_PASSWORD
        if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($mongoUser) -or [string]::IsNullOrWhiteSpace($mongoPassword)) {
            throw 'Local MongoDB credentials unavailable; integration tests NOT RUN.'
        }
        $env:ECHO_PHASE6R_MONGO_URI = 'mongodb://' + [Uri]::EscapeDataString([string]$mongoUser) + ':' +
            [Uri]::EscapeDataString([string]$mongoPassword) + '@127.0.0.1:27018/?authSource=admin&directConnection=true'
    }
    if (([Uri]$env:ECHO_PHASE6R_MONGO_URI).Host -notin @('127.0.0.1', 'localhost', '::1', '[::1]')) {
        throw 'Integration tests require loopback MongoDB.'
    }
    & rtk proxy dotnet test EchoProtocol.Backend/tests/EchoProtocol.Api.Tests/EchoProtocol.Api.Tests.csproj --verbosity minimal `
        --logger 'trx;LogFileName=backend-full.trx' --results-directory $output
    $backendExit = $LASTEXITCODE
    if (Test-Path (Join-Path $output 'backend-full.trx')) {
        [xml] $trx = Get-Content -LiteralPath (Join-Path $output 'backend-full.trx') -Raw
        $counters = $trx.TestRun.ResultSummary.Counters
        $result.backend = @{ passed = [int]$counters.passed; failed = [int]$counters.failed; total = [int]$counters.total; notExecuted = [int]$counters.notExecuted }
    }
    if ($backendExit -ne 0 -or $result.backend.total -lt 1 -or $result.backend.notExecuted -gt 0) { throw 'Backend did not pass completely; inspect backend-full.trx.' }
    if (-not $BackendOnly) {
        foreach ($mode in @('EditMode', 'PlayMode')) {
            & (Join-Path $PSScriptRoot 'Invoke-AEDV02UnityBatch.ps1') -Stage $mode -OutputDirectory $output
            $status = Get-Content -LiteralPath (Join-Path $output ($mode + '-summary.json')) -Raw | ConvertFrom-Json
            $key = if ($mode -eq 'EditMode') { 'editor' } else { 'playmode' }
            $result[$key] = $status
            if ($status.status -ne 'PASS') { throw ($mode + ' did not pass completely.') }
        }
    }
}
finally {
    $env:ECHO_M4_POSTGRES_CONNECTION = $previousConnection
    $env:ECHO_PHASE6R_MONGO_URI = $previousMongo
    $password = $null; $connection = $null; $guard = $null
    $mongoUser = $null; $mongoPassword = $null
    $result.finishedUtc = [DateTime]::UtcNow.ToString('O')
    $result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $output 'test-summary.json') -Encoding UTF8
    Write-Output "AED test artifacts: $output"
    Pop-Location
}
