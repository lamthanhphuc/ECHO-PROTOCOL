# AGENTS.md — ECHO PROTOCOL

## Overview

| Item | Path / Value |
|---|---|
| Project | ECHO PROTOCOL |
| Workspace | `d:\Bin\KLTN` |
| Unity client | `d:\Bin\KLTN\KLTN` |
| Backend | `d:\Bin\KLTN\EchoProtocol.Backend` |
| Engine | Unity `6000.5.8f1` |
| Docs | `docs/SRS.md`, `docs/API_SPEC.md`, `docs/DB_SCHEMA.md` |

## MCP usage

| Server | When |
|---|---|
| `unityMCP` | Unity scene/object/component/console operations via `http://127.0.0.1:8080/mcp` |
| Unity CLI/Pipeline | Recompile and Unity tests via Pipeline Server port `7800` |
| `codegraph` | Structure exploration before create/refactor, when available |
| `context7` | Library/API documentation, only when configured and available |
| `gamecodex` | Game architecture patterns, only when configured and available |
| `headroom` | Long context sessions, when available |

## Manual gates (stop and ask user)

1. Unity not open or in Play mode → wait for *"Unity đã mở, không Play mode"*
2. Photon Fusion not imported → wait for *"Đã import Photon Fusion và có App ID"*
3. Docker not running → wait for *"Docker đã chạy, tiếp tục foundation"*
4. Production secrets / cloud deploy → never automate

## Shell
- Use installed `rtk` 0.48.0 prefix: `rtk git status`, `rtk dotnet build`

## Workflow
1. Read SRS/API_SPEC for scope
2. CodeGraph explore affected areas when available
3. Implement backend in `EchoProtocol.Backend/`
4. Unity changes via `unityMCP` when Editor is ready
5. Validate build + document manual steps

## Skills
- Project: `.cursor/skills/unity/`

## Security
- No secrets in git; BCrypt for passwords; JWT from config/env only
