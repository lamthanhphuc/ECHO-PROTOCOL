# ECHO PROTOCOL — Cursor Prompts & Conventions

## MCP tools

| Tool | Use when |
|---|---|
| **CodeGraph** | Before creating/refactoring files; `codegraph init` at repo root |
| **unity-editor** | Unity scene, GameObject, component, asset ops |
| **Context7** | Latest package/API docs (.NET, EF Core, Unity) |
| **GameCodex** | Game architecture, multiplayer patterns |
| **Headroom** | Long sessions: `headroom proxy --port 8787`, profile `headroom-on` |

## Skills

- Unity: `.cursor/skills/unity/SKILL.md`

## Shell

Always prefix with **rtk** when possible:

```powershell
rtk git status
rtk dotnet build
rtk docker compose -f docker/docker-compose.yml up -d
```

## Manual reminder rule

Stop and provide checklist when:

| Condition | User confirms with |
|---|---|
| Unity not open / Play mode | *"Unity đã mở, không Play mode"* |
| Photon not imported | *"Đã import Photon Fusion và có App ID"* |
| Docker not running | *"Docker đã chạy, tiếp tục foundation"* |
| Need real secrets / cloud | Manual only |

Checklist format:
1. What to do
2. Where
3. Message to send when done
4. Which step Cursor continues from

## Project paths

- Workspace: `d:\Bin\KLTN`
- Unity: `d:\Bin\KLTN\KLTN`
- Backend: `d:\Bin\KLTN\EchoProtocol.Backend`

## Scope discipline

Foundation prompts: structure + docs + health API only.

Do **not** implement in foundation: monster AI, full shop, admin UI, anti-cheat rewards, cloud deploy.

## Suggested next prompt

> Implement Backend Auth foundation: User entity, Register/Login, BCrypt hash, JWT issue, Seed admin account, EF migrations, test health+auth.
