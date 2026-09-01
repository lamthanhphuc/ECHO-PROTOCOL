# ECHO PROTOCOL Backend

## Prerequisites

- .NET SDK 8.x
- Docker Desktop
- EF Core CLI restored from the repository tool manifest

## Local configuration

Copy `.env.example` to `.env`, replace every placeholder locally, and keep `.env` untracked.
PowerShell does not automatically import `.env`; load the required values into the current
process environment before running the API or EF commands. The required configuration names are:

- `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD`, `POSTGRES_PORT`
- `MONGO_ROOT_USERNAME`, `MONGO_ROOT_PASSWORD`, `MONGO_DATABASE`, `MONGO_PORT`
- `ConnectionStrings__DefaultConnection`
- `ConnectionStrings__MongoDb`
- `JwtSettings__SecretKey`
- Optional telemetry limits: `MongoDb__MaxBatchSize`, `MongoDb__SupportedSchemaVersion`,
  `MongoDb__MaxValueJsonBytes`, `MongoDb__MaxFutureSkewMinutes`, `MongoDb__MaxEventAgeDays`
- Optional admin seed: `AdminSeed__Email`, `AdminSeed__Username`, `AdminSeed__Password`,
  `AdminSeed__DisplayName`, `AdminSeed__InitialWalletBalance`

## Start PostgreSQL and MongoDB

From the repository root, after replacing the placeholders in `.env`:

```powershell
docker compose --env-file .env -f docker/docker-compose.yml up -d
docker compose --env-file .env -f docker/docker-compose.yml ps
```

## Restore, migrate, and run

```powershell
dotnet restore EchoProtocol.Backend/EchoProtocol.sln
dotnet tool restore
dotnet build EchoProtocol.Backend/EchoProtocol.sln --no-restore
dotnet test EchoProtocol.Backend/EchoProtocol.sln --no-build
dotnet ef database update `
  --project EchoProtocol.Backend/src/EchoProtocol.Api `
  --startup-project EchoProtocol.Backend/src/EchoProtocol.Api
dotnet run --project EchoProtocol.Backend/src/EchoProtocol.Api
```

Development endpoints from the default launch profile:

- Health: `http://localhost:5042/health`
- Compatibility health route: `http://localhost:5042/api/health`
- Swagger UI: `http://localhost:5042/swagger`
- OpenAPI JSON: `http://localhost:5042/swagger/v1/swagger.json`

Both health routes check PostgreSQL and MongoDB. They return HTTP 200 only when the API can
connect to both databases, otherwise HTTP 503.

If MongoDB is temporarily unavailable, the API still starts and PostgreSQL-backed Auth remains
available. `POST /api/telemetry/batch` returns HTTP 503 with `TELEMETRY_UNAVAILABLE` until
MongoDB recovers.

MongoDB stores raw/versioned telemetry only. PostgreSQL remains authoritative for identity,
profile, wallet, inventory, transactions, match results, and aggregated AI profiles.
