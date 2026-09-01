# ECHO PROTOCOL

Online cooperative first-person horror game — capstone project (4 months, team of 4).

## Tech stack

| Layer | Technology |
|---|---|
| Game client | Unity 6.5 (`6000.5.8f1`, `KLTN/`) |
| Multiplayer | Photon Fusion Host Mode (manual setup required) |
| Backend | ASP.NET Core Web API (`EchoProtocol.Backend/`) |
| Database | PostgreSQL for transactional data + MongoDB for telemetry |
| Auth | JWT + BCrypt |

## Repository layout

```text
d:\Bin\KLTN\
├── KLTN\                    # Unity client (EchoProtocol.Client)
├── EchoProtocol.Backend\    # ASP.NET Core solution
├── docs\                    # SRS, API, DB schema, setup guides
├── docker\                  # Local PostgreSQL + MongoDB
├── .gitignore
└── README.md
```

> **Note:** Unity project stays at `KLTN/` to avoid breaking Unity paths. Treat it as `EchoProtocol.Client`.

Canonical SRS: [`docs/SRS.md`](docs/SRS.md)

## Local setup

### 1. Databases (Docker)

```powershell
Copy-Item .env.example .env
# Replace all placeholders in .env locally before continuing.
docker compose --env-file .env -f docker/docker-compose.yml up -d
```

Local credentials and the backend connection string come from untracked environment variables;
see [`EchoProtocol.Backend/README.md`](EchoProtocol.Backend/README.md).

### 2. Backend API

```powershell
cd EchoProtocol.Backend
dotnet restore
dotnet build
dotnet run --project src/EchoProtocol.Api
```

Health check: `GET http://localhost:5042/health`

### 3. Unity client

1. Open Unity Hub → project `d:\Bin\KLTN\KLTN`
2. Unity version: **6000.5.8f1**
3. Ensure MCP bridge: Console shows `[MCP] Server started on port 6400`

See [`docs/SETUP_GUIDE.md`](docs/SETUP_GUIDE.md) for full instructions.

## Manual requirements (not automated)

- [ ] Photon Fusion package imported (Unity 6000.5 compatible)
- [ ] Photon Fusion App ID from [Photon Dashboard](https://dashboard.photonengine.com)
- [ ] Docker Desktop running for local PostgreSQL and MongoDB
- [ ] Production secrets via env / user-secrets (never commit)

## Foundation status checklist

- [x] Git + `.gitignore`
- [x] Docs (`docs/`)
- [x] Docker PostgreSQL + MongoDB compose
- [x] Backend skeleton + health endpoint
- [x] Unity folder/scene/script foundation
- [ ] Photon Fusion wired (manual)
- [ ] Auth / JWT / seed admin (next phase)

## Team conventions

- Shell: prefix with `rtk` when possible (`rtk git status`, `rtk dotnet build`)
- Cursor: use `unity-editor` MCP for Unity Editor ops; `codegraph` before structural changes
- No plain-text passwords; no production secrets in repo
