# AGENTS.md — ECHO PROTOCOL

## Overview

| Item | Path / Value |
|---|---|
| Project | ECHO PROTOCOL |
| Workspace | `d:\Bin\KLTN` |
| Unity client | `d:\Bin\KLTN\KLTN` |
| Backend | `d:\Bin\KLTN\EchoProtocol.Backend` |
| Engine | Unity 6.3 LTS (`6000.3.19f1`) |
| Docs | `docs/SRS.md`, `docs/API_SPEC.md`, `docs/DB_SCHEMA.md` |

## MCP usage

| Server | When |
|---|---|
| `unity-editor` | Unity scene/object/component operations |
| `codegraph` | Structure exploration before create/refactor |
| `context7` | Library/API documentation |
| `gamecodex` | Game architecture patterns |
| `headroom` | Long context sessions |

## Manual gates (stop and ask user)

1. Unity not open or in Play mode → wait for *"Unity đã mở, không Play mode"*
2. Photon Fusion not imported → wait for *"Đã import Photon Fusion và có App ID"*
3. Docker not running → wait for *"Docker đã chạy, tiếp tục foundation"*
4. Production secrets / cloud deploy → never automate

## Shell
- Use `rtk` prefix: `rtk git status`, `rtk dotnet build`

## Workflow
1. Read SRS/API_SPEC for scope
2. CodeGraph explore affected areas
3. Implement backend in `EchoProtocol.Backend/`
4. Unity changes via `unity-editor` MCP when Editor is ready
5. Validate build + document manual steps

## Skills
- Project: `.cursor/skills/unity/`

## Security
- No secrets in git; BCrypt for passwords; JWT from config/env only
