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

## AED V02 source-change check (2026-11-19)
- Baseline: branch `main`, HEAD `1465d0bc60f8272c9ec5236800c52ea1dffde6ea`.
- Pre-existing dirty paths preserved: `EchoProtocol.Backend/src/EchoProtocol.Api/Controllers/PaymentCatalogController.cs`, `EchoProtocol.Web/next-env.d.ts`, `EchoProtocol.Web/src/app/(player)/wallet/page.tsx`, `EchoProtocol.Web/src/app/admin/remote-fixed/page.tsx`, `EchoProtocol.Web/src/components/auth/login-form.tsx`, `EchoProtocol.Web/src/proxy.ts`, `EchoProtocol.Web/src/app/topup/`, `KLTN/Assets/Resources/UI/Loading/Typography/SpectralLoadingTMP.asset`, `KLTN/Assets/Resources/UI/LobbyReference/LobbySerif.asset`, `KLTN/Assets/Resources/UI/MainMenuTypography/OswaldTMP.asset`, `KLTN/Assets/Scenes/Lobby.unity`, `KLTN/Assets/Scenes/MainMenu.unity`, `KLTN/Assets/Scripts/AlienHunt/AlienHuntMission.cs`, `KLTN/Assets/Scripts/MatchFlow/Zone2DebugSkipShortcut.cs`, `KLTN/Assets/Scripts/Player/PlayerGodModeDebugShortcut.cs`, `KLTN/Assets/Scripts/UI/HUD/HUD3DWorldMarker.cs`, `KLTN/Assets/Scripts/UI/LobbyUtilityBarController.cs`, `KLTN/Assets/Scripts/UI/MainMenu/MainMenuProfileController.cs`, `KLTN/Assets/Scripts/UI/MainMenu/StoreReferencePresentation.cs`, `KLTN/Assets/Settings/UniversalRenderPipelineGlobalSettings.asset`, `KLTN/Assets/_Project/Scripts/Networking/Authority/MatchAuthorityRuntime.ResultOutbox.cs`, `KLTN/Assets/_Project/Scripts/Networking/Authority/MatchAuthorityRuntime.cs`, `KLTN/Assets/_Project/Scripts/Networking/Match/NetworkMatchState.AlienHunt.cs`, `KLTN/Assets/unity/Mercer_FBX_Cape_Mesh1.asset`.
- Unity Editor readiness manually confirmed by the user; AED contract documents under `docs/aed/` were unavailable. Fusion source/dependencies are present; App ID and live gameplay verification were not checked.
