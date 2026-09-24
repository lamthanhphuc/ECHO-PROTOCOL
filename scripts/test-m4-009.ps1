[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$backendRoot = Join-Path $repoRoot "EchoProtocol.Backend"
$solution = Join-Path $backendRoot "EchoProtocol.sln"
$runId = "{0}-{1}" -f (Get-Date -Format "yyyyMMdd-HHmmss"), $PID
$resultsRoot = Join-Path $repoRoot "TestResults\M4-009\$runId"
$containerName = "echo-m4-009-pg-$PID"
$containerStarted = $false
$integrationStatus = "NOT RUN"
$hasFailure = $false

function Invoke-CheckedCommand {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][scriptblock]$Command
    )

    Write-Host "`n== $Name =="
    & $Command
    if ($LASTEXITCODE -ne 0) {
        throw "$Name failed with exit code $LASTEXITCODE."
    }
}

function Invoke-TestPhase {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$Filter,
        [Parameter(Mandatory = $true)][string]$TrxName
    )

    Write-Host "`n== $Name =="
    & dotnet test $solution --no-build --no-restore `
        --filter $Filter `
        --results-directory $resultsRoot `
        --logger "trx;LogFileName=$TrxName" | Out-Host
    $testExitCode = $LASTEXITCODE

    $trxPath = Join-Path $resultsRoot $TrxName
    if (-not (Test-Path -LiteralPath $trxPath)) {
        throw "$Name did not produce $trxPath. Tests are NOT RUN."
    }

    [xml]$trx = Get-Content -Raw -LiteralPath $trxPath
    $counters = $trx.TestRun.ResultSummary.Counters
    $total = [int]$counters.total
    $passed = [int]$counters.passed
    $failed = [int]$counters.failed + [int]$counters.error + [int]$counters.timeout + [int]$counters.aborted
    $notRun = [int]$counters.notExecuted
    Write-Host "$Name summary: Total=$total Passed=$passed Failed=$failed NotRun=$notRun"

    if ($total -eq 0) {
        throw "$Name found zero tests. Tests are NOT RUN."
    }

    if ($testExitCode -ne 0 -or $failed -ne 0) {
        throw "$Name failed."
    }

    return [pscustomobject]@{
        Name = $Name
        Total = $total
        Passed = $passed
        Failed = $failed
        NotRun = $notRun
    }
}

New-Item -ItemType Directory -Path $resultsRoot | Out-Null

$summaries = @()
try {
    Push-Location $backendRoot
    Invoke-CheckedCommand "dotnet restore" { dotnet restore $solution }
    Invoke-CheckedCommand "dotnet build" { dotnet build $solution --no-restore }

    $summaries += Invoke-TestPhase `
        -Name "M4-009 unit tests" `
        -Filter "Category=M4Unit" `
        -TrxName "m4-unit.trx"

    Write-Host "`n== PostgreSQL test environment =="
    & docker info --format "{{.ServerVersion}}" | Out-Host
    if ($LASTEXITCODE -ne 0) {
        $integrationStatus = "NOT RUN: Docker daemon is unavailable."
        $hasFailure = $true
    }
    else {
        & docker run --rm -d `
            --name $containerName `
            -e POSTGRES_PASSWORD=EchoM4_009_Test_Only `
            -e POSTGRES_DB=echo_m4_009 `
            -p "127.0.0.1::5432" `
            postgres:16-alpine | Out-Host
        if ($LASTEXITCODE -ne 0) {
            throw "Could not start the isolated PostgreSQL test container."
        }
        $containerStarted = $true

        $ready = $false
        for ($attempt = 1; $attempt -le 30; $attempt++) {
            & docker exec $containerName pg_isready -U postgres -d echo_m4_009 *> $null
            if ($LASTEXITCODE -eq 0) {
                $ready = $true
                break
            }
            Start-Sleep -Seconds 1
        }
        if (-not $ready) {
            throw "PostgreSQL test container did not become ready within 30 seconds."
        }

        $portOutput = & docker port $containerName 5432/tcp
        $portLine = $portOutput | Where-Object { $_ -match "127\.0\.0\.1:(\d+)$" } | Select-Object -First 1
        if (-not $portLine -or $portLine -notmatch ":(\d+)$") {
            throw "Could not resolve the PostgreSQL test container port."
        }

        $postgresPort = $Matches[1]
        $env:ECHO_M4_POSTGRES_CONNECTION = "Host=127.0.0.1;Port=$postgresPort;Database=echo_m4_009;Username=postgres;Password=EchoM4_009_Test_Only"
        $integrationSummary = Invoke-TestPhase `
            -Name "M4-009 PostgreSQL integration tests" `
            -Filter "Category=M4PostgreSqlIntegration" `
            -TrxName "m4-postgresql.trx"
        $summaries += $integrationSummary
        $integrationStatus = "PASS: $($integrationSummary.Passed)/$($integrationSummary.Total)"
    }

    $summaries += Invoke-TestPhase `
        -Name "M2 regression tests" `
        -Filter "Category!=M4Unit&Category!=M4PostgreSqlIntegration" `
        -TrxName "m2-regression.trx"
}
catch {
    $hasFailure = $true
    Write-Host "ERROR: $_" -ForegroundColor Red
}
finally {
    Remove-Item Env:ECHO_M4_POSTGRES_CONNECTION -ErrorAction SilentlyContinue
    if ($containerStarted) {
        & docker stop $containerName | Out-Host
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Could not stop test container $containerName."
            $hasFailure = $true
        }
    }
    Pop-Location -ErrorAction SilentlyContinue
}

Write-Host "`n== M4-009 final summary =="
foreach ($summary in $summaries) {
    Write-Host "$($summary.Name): Total=$($summary.Total) Passed=$($summary.Passed) Failed=$($summary.Failed) NotRun=$($summary.NotRun)"
}
Write-Host "PostgreSQL integration: $integrationStatus"

if ($hasFailure) {
    exit 1
}

exit 0
