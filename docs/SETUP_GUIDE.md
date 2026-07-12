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
rtk docker compose -f docker/docker-compose.yml up -d
```

**Dev connection string** lives in `appsettings.Development.json` only (never use in production):

```
Host=localhost;Port=5433;Database=echo_protocol;Username=postgres;Password=postgres
```

Production: set `ConnectionStrings__DefaultConnection` via environment variable or secret manager.

Stop:

```powershell
rtk docker compose -f docker/docker-compose.yml down
```

## Backend API

### Build

```powershell
cd d:\Bin\KLTN
rtk dotnet build EchoProtocol.Backend/EchoProtocol.sln
```

### EF Core migration

Install tool once if needed:

```powershell
rtk dotnet tool install --global dotnet-ef
```

**Fresh setup (clone repo):** apply committed migrations only — do **not** recreate `InitialAuthSchema`:

```powershell
rtk dotnet ef database update --project EchoProtocol.Backend/src/EchoProtocol.Api --startup-project EchoProtocol.Backend/src/EchoProtocol.Api
```

Migration `InitialAuthSchema` is already committed. New team members only need `database update`.

**When schema changes** (new tables/columns after auth foundation):

```powershell
rtk dotnet ef migrations add <NewMigrationName> --project EchoProtocol.Backend/src/EchoProtocol.Api --startup-project EchoProtocol.Backend/src/EchoProtocol.Api
rtk dotnet ef database update --project EchoProtocol.Backend/src/EchoProtocol.Api --startup-project EchoProtocol.Backend/src/EchoProtocol.Api
```

Production secrets (`ConnectionStrings__DefaultConnection`, `JwtSettings__SecretKey`, admin seed) must use environment variables, user-secrets, or a cloud secret manager — never commit real production values.

In **Development**, the API also runs `MigrateAsync()` and admin seed on startup.

### Run

```powershell
rtk dotnet run --project EchoProtocol.Backend/src/EchoProtocol.Api
```

- Swagger: `http://localhost:5042/swagger`
- Health: `http://localhost:5042/api/health`

### Configuration

| Setting | Local dev | Production |
|---|---|---|
| Connection string | `appsettings.Development.json` | `ConnectionStrings__DefaultConnection` env |
| JWT `SecretKey` | `appsettings.Development.json` (≥ 32 UTF-8 bytes) | `JwtSettings__SecretKey` env / user-secrets / secret manager |
| Admin seed | `AdminSeed` section in Development | Configure via env; no auto-seed in Production |

**Dev admin seed** (local only — change for real demos):

- Username: `admin`
- Password: see `appsettings.Development.json` (not logged by API)
- Role: `ADMIN`

### Test auth (PowerShell)

```powershell
$base = "http://localhost:5042/api"

# Register
$body = @{ username = "player01"; password = "123456"; confirmPassword = "123456" } | ConvertTo-Json -Compress
Invoke-RestMethod -Uri "$base/auth/register" -Method Post -ContentType "application/json" -Body $body

# Login
$loginBody = @{ username = "player01"; password = "123456" } | ConvertTo-Json -Compress
$login = Invoke-RestMethod -Uri "$base/auth/login" -Method Post -ContentType "application/json" -Body $loginBody
$token = $login.data.accessToken

# Me
Invoke-RestMethod -Uri "$base/auth/me" -Headers @{ Authorization = "Bearer $token" }
```

### Swagger Bearer

1. Open `http://localhost:5042/swagger`
2. Click **Authorize**
3. Paste raw JWT token (Swagger adds `Bearer` prefix)
4. Call `GET /api/auth/me`

## Unity Auth UI

Unity project root: `d:\Bin\KLTN\KLTN`

### Prerequisites

1. Backend running: `rtk dotnet run --project EchoProtocol.Backend/src/EchoProtocol.Api`
2. Base URL (Unity): `http://localhost:5042` — endpoints are `/api/auth/...` (built via `ApiConfiguration.BuildApiUrl`)
3. Open Unity **6000.3.19f1** on the KLTN project

### Wire scenes (Editor)

Run once (idempotent — safe to re-run):

**ECHO PROTOCOL → Setup Auth UI**

Or batchmode (when Unity path is known):

**Close the Unity Editor before running batchmode.** Do not run two Unity instances on the same project.

```powershell
& "<UnityEditorPath>\Unity.exe" `
  -batchmode `
  -quit `
  -projectPath "<RepoRoot>\KLTN" `
  -executeMethod AuthUiSceneSetup.SetupAuthUi `
  -logFile "<RepoRoot>\unity-auth-ui-compile.log"
```

Example with concrete paths:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.19f1\Editor\Unity.exe" `
  -batchmode `
  -quit `
  -projectPath "d:\Bin\KLTN\KLTN" `
  -executeMethod AuthUiSceneSetup.SetupAuthUi `
  -logFile "d:\Bin\KLTN\unity-auth-ui-compile.log"
```

Batchmode compile success does **not** replace Play Mode testing — always verify auth flows in the Editor after wiring changes.

This creates/wires:

- `Bootstrap` — `AuthRuntime`, `BootstrapSceneFlowController` (add-only; preserves NetworkBootstrap/LobbyManager)
- `Login` — uGUI auth UI + `InputSystemUIInputModule`
- `MainMenu` — profile placeholder + logout
- `Assets/Resources/ApiConfiguration.asset` — dev base URL

### Play Mode flow

| Entry scene | Behavior |
|---|---|
| **Bootstrap** (recommended) | Token check → `/api/auth/me` → MainMenu or Login |
| **Login** | Auth forms; `AuthRuntime.EnsureExists()` if entered directly |

**Login success flow:** Login API → save token → `/api/auth/me` → MainMenu (never skip `/me`).

**Logout:** Main Menu → Logout → clears PlayerPrefs token + session → Login.

### PlayerPrefs (MVP local only)

Keys: `echo_protocol.auth.access_token`, `echo_protocol.auth.expires_at`

Not secure production storage — replace with OS keychain/encrypted storage before release.

### Change backend URL (cloud later)

Edit `Assets/Resources/ApiConfiguration.asset` → `baseUrl` (host only, no `/api` suffix).

### Play Mode test checklist

- [ ] Register new user → success message → Login panel
- [ ] Duplicate username (case-insensitive) → error
- [ ] Login wrong password → `INVALID_CREDENTIALS`
- [ ] Login success → MainMenu shows username, role, wallet
- [ ] Logout → Login scene, token cleared
- [ ] Restart Play from Bootstrap with valid token → MainMenu
- [ ] Backend stopped on restore → network message, **token not cleared**
- [ ] Run **Setup Auth UI** twice → no duplicate Canvas/EventSystem/AuthRuntime

### Manual wiring (if Editor menu unavailable)

Manual step 1
- Lý do: Unity Editor menu `ECHO PROTOCOL/Setup Auth UI` cannot be run.
- Mở: `Assets/Scenes/Login.unity`
- Thực hiện:
  1. Create Canvas `AuthCanvas` (Screen Space Overlay)
  2. Add `EventSystem` + `InputSystemUIInputModule` (remove `StandaloneInputModule` if present)
  3. Create `AuthRoot` with `AuthScreenController`
  4. Create `LoginPanel` / `RegisterPanel` with uGUI `InputField` + `Button` children
  5. Wire all serialized fields on `AuthScreenController`
- Giá trị: `mainMenuSceneName` = `MainMenu`
- Kết quả mong đợi: Play Mode shows Login panel, Console has no errors.
- Báo lại: Inspector screenshot + Console output.

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
- [ ] Auth register/login/me work
- [ ] Unity opens without compile errors
- [ ] Photon Fusion + App ID configured
- [ ] 2–4 laptops on same network for multiplayer test (later phase)

## Troubleshooting

| Issue | Fix |
|---|---|
| MCP ECONNREFUSED | Open Unity; check port 6400 |
| Docker fails | Start Docker Desktop |
| EF connection failed | Verify Postgres container `rtk docker ps` |
| JWT startup error | Ensure `JwtSettings:SecretKey` in Development is ≥ 32 bytes |
| Unity compile error | Window → Console, fix scripts under `Assets/Scripts/` |
