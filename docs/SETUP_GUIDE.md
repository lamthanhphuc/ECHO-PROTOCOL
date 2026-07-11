# ECHO PROTOCOL — Setup Guide

## Prerequisites

| Tool | Version / Notes |
|---|---|
| Unity Hub | Unity **6.3 LTS** (`6000.3.19f1`) |
| .NET SDK | 8.x |
| Docker Desktop | For local PostgreSQL |
| Git | Optional but recommended |
| Cursor | With `unity-editor` MCP configured |

## Unity client

1. Open Unity Hub → **Add/Open** → `d:\Bin\KLTN\KLTN`
2. Confirm version `6000.3.19f1` in Project Settings
3. Wait for package resolve (`com.unity.editor-mcp`, URP, Input System)
4. Console should show: `[MCP] Server started on port 6400`
5. **Do not** enter Play mode when using MCP for scene edits

### Scenes (foundation)

| Scene | Purpose |
|---|---|
| Bootstrap | App init, service locator |
| Login | Auth UI |
| MainMenu | Main menu |
| Lobby | Room / ready |
| Game | Gameplay |
| Result | Post-match |
| SampleScene | WASD prototype (legacy) |

Create foundation scenes in Unity (when Editor is open):

**ECHO PROTOCOL → Create Foundation Scenes**

Or via MCP `execute_menu_item` when `unity-editor` is connected.

## PostgreSQL (local Docker)

```powershell
cd d:\Bin\KLTN
docker compose -f docker/docker-compose.yml up -d
```

**Dev connection string only** (never use in production):

```
Host=localhost;Port=5433;Database=echo_protocol;Username=postgres;Password=postgres
```

Stop:

```powershell
docker compose -f docker/docker-compose.yml down
```

## Backend API

```powershell
cd d:\Bin\KLTN\EchoProtocol.Backend
dotnet restore
dotnet build
dotnet run --project src/EchoProtocol.Api
```

Swagger (Development): `https://localhost:7xxx/swagger` or `http://localhost:5xxx/swagger`

Health check:

```powershell
curl http://localhost:5000/api/health
```

Adjust port per `src/EchoProtocol.Api/Properties/launchSettings.json`.

## Photon Fusion (manual)

1. Import **Photon Fusion** compatible with Unity 6.3 LTS
2. Create app at [Photon Dashboard](https://dashboard.photonengine.com) → Fusion
3. Copy **App ID** into Fusion Network Project Config (Inspector — not in source control)
4. Report: *"Đã import Photon Fusion và có App ID"*

## Cursor / MCP

- `unity-editor`: Unity Editor automation (port 6400 bridge)
- `codegraph`: `codegraph init` at repo root
- Shell: prefix with `rtk`

## Pre-demo checklist

- [ ] Docker Postgres running on port 5433
- [ ] Backend builds and `/api/health` returns success
- [ ] Unity opens without compile errors
- [ ] Photon Fusion + App ID configured
- [ ] 2–4 laptops on same network for multiplayer test (later phase)

## Troubleshooting

| Issue | Fix |
|---|---|
| MCP ECONNREFUSED | Open Unity; check port 6400 |
| Docker fails | Start Docker Desktop |
| EF connection failed | Verify Postgres container `docker ps` |
| Unity compile error | Window → Console, fix scripts under `Assets/Scripts/` |
