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
- `ConnectionStrings__DefaultConnection`
- `JwtSettings__SecretKey`
- Optional admin seed: `AdminSeed__Username`, `AdminSeed__Password`,
  `AdminSeed__DisplayName`, `AdminSeed__InitialWalletBalance`

## Start PostgreSQL

From the repository root, with the PostgreSQL variables set in the shell or `.env`:

```powershell
docker compose -f docker/docker-compose.yml up -d
docker compose -f docker/docker-compose.yml ps
```

## Restore, migrate, and run

```powershell
dotnet restore EchoProtocol.Backend/EchoProtocol.sln
dotnet tool restore
dotnet build EchoProtocol.Backend/EchoProtocol.sln --no-restore
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

Both health routes execute the registered PostgreSQL connectivity health check. They return
HTTP 200 only when the API can connect to the database, otherwise HTTP 503.
