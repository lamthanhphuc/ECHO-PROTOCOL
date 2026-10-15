param(
    [string]$RunId = ('aedv02-' + [DateTime]::UtcNow.ToString('yyyyMMddHHmmss')),
    [int]$ApiPort = 5072,
    [int]$PostgresPort = 5544,
    [int]$MongoPort = 27028
)
$ErrorActionPreference = 'Stop'
if ($RunId -notmatch '^aedv02-[a-z0-9-]{1,48}$') { throw 'RunId must identify a new AED test stack.' }
if (@($ApiPort,$PostgresPort,$MongoPort | Select-Object -Unique).Count -ne 3 -or
    @($ApiPort,$PostgresPort,$MongoPort | Where-Object { $_ -lt 1024 -or $_ -gt 65535 }).Count -gt 0) { throw 'Invalid test ports.' }
foreach ($port in @($ApiPort,$PostgresPort,$MongoPort)) {
    if (Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue) { throw "Port $port already owned; choose another test port." }
}
$repo = Split-Path $PSScriptRoot -Parent
$directory = Join-Path $repo "KLTN/Temp/AEDV02/Native/$RunId"
if (Test-Path -LiteralPath $directory) { throw 'Run directory exists; preserve its evidence and use a new RunId.' }
New-Item -ItemType Directory -Path $directory -Force | Out-Null
$password = [Guid]::NewGuid().ToString('N') + [Guid]::NewGuid().ToString('N')
$jwt = [Guid]::NewGuid().ToString('N') + [Guid]::NewGuid().ToString('N')
$proof = [Guid]::NewGuid().ToString('N') + [Guid]::NewGuid().ToString('N')
$compose = @{
    services = @{
        postgres = @{
            image='postgres:16'; environment=@{POSTGRES_DB='aedv02_native_test';POSTGRES_USER='postgres';POSTGRES_PASSWORD=$password}
            ports=@("127.0.0.1:${PostgresPort}:5432"); volumes=@('pgdata:/var/lib/postgresql/data')
            healthcheck=@{test=@('CMD-SHELL','pg_isready -U postgres -d aedv02_native_test');interval='2s';timeout='3s';retries=30}
        }
        # Replica set is private to this disposable compose network and loopback host port.
        mongodb = @{
            image='mongo:8.0'; command=@('mongod','--replSet','aedpilot','--bind_ip_all')
            ports=@("127.0.0.1:${MongoPort}:27017"); volumes=@('mongodata:/data/db')
            healthcheck=@{test=@('CMD','mongosh','--quiet','--eval','quit(db.adminCommand({ping:1}).ok ? 0 : 1)');interval='2s';timeout='3s';retries=30}
        }
        backend = @{
            build=@{context=(Join-Path $repo 'EchoProtocol.Backend');dockerfile='Dockerfile'}
            depends_on=@{postgres=@{condition='service_healthy'};mongodb=@{condition='service_healthy'}}
            ports=@("127.0.0.1:${ApiPort}:5042")
            environment=@{
                ASPNETCORE_ENVIRONMENT='Development';ASPNETCORE_URLS='http://+:5042'
                ConnectionStrings__DefaultConnection="Host=postgres;Port=5432;Database=aedv02_native_test;Username=postgres;Password=$password"
                ConnectionStrings__MongoDb='mongodb://mongodb:27017/?replicaSet=aedpilot'
                MongoDb__DatabaseName='aedv02_native_telemetry_test';MongoDb__MaxValueJsonBytes='1048576'
                JwtSettings__SecretKey=$jwt;MatchAuthority__ProofSigningKey=$proof
                AEDPreMatchPolicy__Enabled='true';AEDV02Pilot__Enabled='true';AEDV02Pilot__CabinetAmbushEnabled='true'
            }
        }
    }
    volumes=@{pgdata=@{};mongodata=@{}}
}
$composePath = Join-Path $directory 'compose.json'
$compose | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $composePath -Encoding UTF8
& rtk proxy docker compose -p $RunId -f $composePath up -d --wait postgres mongodb
if ($LASTEXITCODE -ne 0) { throw 'Disposable databases failed to start. Preserve logs; native Backend NOT RUN.' }
& rtk proxy docker compose -p $RunId -f $composePath exec -T mongodb mongosh --quiet --eval 'rs.initiate({_id:"aedpilot",members:[{_id:0,host:"mongodb:27017"}]})'
if ($LASTEXITCODE -ne 0) { throw 'Disposable Mongo replica-set initialization failed.' }
& rtk proxy docker compose -p $RunId -f $composePath up -d --build backend
if ($LASTEXITCODE -ne 0) { throw 'Disposable Backend build/start failed.' }
$deadline = [DateTime]::UtcNow.AddMinutes(3)
$ready = $false
do {
    try { $health = Invoke-WebRequest -Uri "http://127.0.0.1:$ApiPort/health" -UseBasicParsing -TimeoutSec 5; $ready=$health.StatusCode -eq 200 } catch { }
    if (!$ready) { Start-Sleep -Seconds 2 }
} while (!$ready -and [DateTime]::UtcNow -lt $deadline)
@{runId=$RunId;apiUrl="http://127.0.0.1:$ApiPort";compose=$composePath;ready=$ready;database='aedv02_native_test';
    candidate=(& rtk proxy git -C $repo rev-parse HEAD | Out-String).Trim();postgresPort=$PostgresPort;mongoPort=$MongoPort} |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $directory 'stack.json') -Encoding UTF8
$password=$null;$jwt=$null;$proof=$null;$compose=$null
if (!$ready) { throw 'Disposable Backend health timed out; preserve this stack for diagnosis.' }
Write-Output "Isolated Development Backend ready: $directory/stack.json"
