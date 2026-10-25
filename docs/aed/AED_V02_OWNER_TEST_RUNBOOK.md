# AED v0.2 Rev02 — owner verification runbook

## Current checkpoint — 2026-10-25 (`3c1d0e3`)

Backend build and 707/707 tests passed on an isolated local stack. SciFi now has one authored Safe Room marker and both manifest copies match. The project Unity Editor was open during this checkpoint; Unity batch, Windows build, native Host/Client, Fixed runtime and twelve owner scenarios remain NOT RUN. Use a fresh matching build for later native acceptance. The older status and Safe Room statements below refer to an earlier source checkpoint.

## 1. Status and environment

This candidate is **IMPLEMENTATION INCOMPLETE — Backend CI 703 PASS**. The Backend validation and regression suite passed in CI; Unity/native verification still has not run. Commands below remain instructions for the owner's later verification, not multiplayer acceptance proof. The shipped manifests still require owner-authored Safe Room export for live scenes.

Requirements: Windows, licensed Unity **6000.5.8f1** with Windows build support, .NET 8 SDK, local Docker Desktop, PowerShell, Git and RTK. Existing Fusion/Voice App IDs must be configured privately. Use the same commit for Backend, Host and every Client. Do not publish credentials or change production flags.

Work from `D:\Bin\KLTN`. Keep owner RuntimeLogSettings/TMP fallback changes separate. The Editor may remain open for Backend-only verification. **Every command using `Invoke-AEDV02UnityBatch.ps1`, including Manifest, requires closing this project's Editor first, later when running verification.** The runner checks ownership and never deletes an active lock. Run Unity stages sequentially. Do not launch a second Editor on this project.

## 2. Record the candidate and inspect contracts

```powershell
Set-Location D:\Bin\KLTN
rtk proxy git rev-parse HEAD
rtk proxy powershell -NoProfile -File scripts/Sync-AEDV02Contract.ps1 -Check
```

Current source identities: `AED_V02_WORLD_SOURCE_REV02_7`, `AED_V02_SOURCE_VERIFICATION_REV02_V6`, opportunity `AED_V02_PILOT_OPPORTUNITIES_2`, policy `AED_STALKER_POLICY_V0_2_REV02_PILOT_DEFAULTS_2`. The two manifest copies must be byte identical. Re-exporting content can change its fingerprint; resynchronize and rebuild both peers after any export. Do not mix an older build with a newer manifest. The current scene/manifests have not been exported with Safe Room volumes; do not invent bounds or hand-edit either JSON file.

## 3. Start an isolated local Backend and run Backend build/tests

These commands create a new disposable Development stack, not the owner's existing API at port 5042. Default native ports are API **5072**, PostgreSQL **5544**, MongoDB **27028**. The scripts refuse occupied ports and remote Docker endpoints. No production `.env` is needed.

```powershell
rtk proxy powershell -NoProfile -File scripts/Start-AEDV02NativeBackend.ps1
# Copy the stack.json path printed above into this variable; do not print compose.json.
$pilotStackFile = "$env:LOCALAPPDATA\Temp\AEDV02\Native\<run-id>\stack.json"
$pilotStack = Get-Content -LiteralPath $pilotStackFile -Raw | ConvertFrom-Json
$pilotCompose = Get-Content -LiteralPath $pilotStack.compose -Raw | ConvertFrom-Json

# A separate database for the suite; use a fresh stack if it already exists.
rtk proxy docker compose -p $pilotStack.runId -f $pilotStack.compose exec -T postgres psql -U postgres -d postgres -v ON_ERROR_STOP=1 -c 'CREATE DATABASE echo_protocol_test;'
$pilotConnection = [System.Data.Common.DbConnectionStringBuilder]::new()
$pilotConnection['Host']='127.0.0.1'; $pilotConnection['Port']=$pilotStack.postgresPort
$pilotConnection['Database']='echo_protocol_test'; $pilotConnection['Username']='postgres'
$pilotConnection['Password']=$pilotCompose.services.postgres.environment.POSTGRES_PASSWORD
$env:ECHO_M4_POSTGRES_CONNECTION=$pilotConnection.ConnectionString
$env:ECHO_PHASE6R_MONGO_URI="mongodb://127.0.0.1:$($pilotStack.mongoPort)/?directConnection=true"

rtk proxy dotnet build EchoProtocol.Backend/EchoProtocol.sln --configuration Debug
rtk proxy powershell -NoProfile -File scripts/Test-AEDV02.ps1 -BackendOnly -OutputDirectory KLTN/Temp/AEDV02/Owner/Backend
$pilotCompose=$null; $pilotConnection=$null
```

`Test-AEDV02.ps1` runs the real PostgreSQL/MongoDB tests too; missing infrastructure is a failure, not a silent pass. Keep the existing migration roundtrip test, including `HadGameplayDisconnect`. Do not use `--no-incremental` with `dotnet test`. If using the combined campaign below, let it create its fresh suite database instead of first creating it manually.

## 4. Unity EditMode, PlayMode and Windows build

**Close this project's Editor before executing these later.** No Editor is closed by these commands. No automatic acceptance follows a zero process exit code: inspect XML counters/build reports.

```powershell
rtk proxy powershell -NoProfile -File scripts/Invoke-AEDV02UnityBatch.ps1 -Stage EditMode -OutputDirectory KLTN/Temp/AEDV02/Owner/Unity
rtk proxy powershell -NoProfile -File scripts/Invoke-AEDV02UnityBatch.ps1 -Stage PlayMode -OutputDirectory KLTN/Temp/AEDV02/Owner/Unity
rtk proxy powershell -NoProfile -File scripts/Invoke-AEDV02UnityBatch.ps1 -Stage WindowsBuild -OutputDirectory KLTN/Temp/AEDV02/Owner/Unity
```

The real build entrypoint is `AEDV02BatchBuild.WindowsDevelopment`. It adds `AED_V02_PILOT_HARNESS` to this Development build only. Executable: `KLTN/Temp/AEDV02/Owner/Unity/Windows/EchoProtocol.exe`, with all companion files. Do not copy the executable alone.

To regenerate the manifest later: use `-Stage Manifest` with the same runner, then synchronize both copies and commit/rebuild. Live asset authoring entrypoint: `ExportAEDV02Scene.AuthorPilotEntriesAndExport`; export-only entrypoint: `ExportAEDV02Scene.Run`. Both require idle/stopped Editor and a clean isolated SciFi scene. Neither is a test or a NavMesh bake. The authoring root is `AEDRev02PilotEntries`, outside the legacy Special entry registry.

## 5. Combined campaign and native transaction

On a **fresh** stack from step 3, before manually creating the suite database:

```powershell
rtk proxy powershell -NoProfile -File scripts/Invoke-AEDV02Campaign.ps1 -StackJson $pilotStackFile
```

This runs source synchronization, comparator fixtures, Backend suites, Unity suites, Windows build and separate real Host/Client processes. It registers two distinct local test accounts and passes passwords through child-process environment only; configuration/logs contain no plaintext password. Each player clears the inherited password after reading. The native comparator requires actual Host consumer reads, remote Animator movement/readback and the matching Backend receipt. Fixture success is never native proof. Campaign output deliberately does not declare owner readiness.

For a separately prepared native session:

```powershell
$pilotHostPassword=Read-Host 'Local pilot Host password' -AsSecureString
$pilotClientPassword=Read-Host 'Local pilot Client password' -AsSecureString
# Current-shell invocation keeps SecureStrings typed and passwords out of process arguments.
$pilotSession = & .\scripts\New-AEDV02NativeSession.ps1 -StackJson $pilotStackFile `
  -OutputDirectory 'D:\Bin\KLTN\KLTN\Temp\AEDV02\Owner\Native-unique' `
  -Usernames @('local_pilot_host','local_pilot_client') -Passwords @($pilotHostPassword,$pilotClientPassword) -Cabinet
# Add -UseExistingAccounts on later matches to retain these identities and their real history.
# Invoke in the current PowerShell so SecureString objects stay typed:
& .\scripts\Invoke-AEDV02Native.ps1 -Executable '<matching-build>\EchoProtocol.exe' `
  -HostConfig $pilotSession.hostConfig -ClientConfig $pilotSession.clientConfig `
  -HostPassword $pilotHostPassword -ClientPassword $pilotClientPassword `
  -OutputDirectory $pilotSession.output -TimeoutSeconds 600 -ObserveSecondsAfterProof 900
```

Create config JSON files in a **new ignored output directory**. Example Host config (replace account/session/output; no password field):

```json
{"role":"Host","label":"Host","mode":"Adaptive","playerCount":2,"apiUrl":"http://127.0.0.1:5072","username":"existing-local-pilot-host","session":"unique-pilot-session","output":"D:/Bin/KLTN/KLTN/Temp/AEDV02/Owner/Native-unique","commandsFile":"D:/Bin/KLTN/KLTN/Temp/AEDV02/Owner/Native-unique/Host-commands.jsonl","gameplay":true,"cabinet":true,"autoRelief":true,"timeoutSeconds":1800}
```

Client config uses `role/label=Client`, a different authenticated account, and `Client-commands.jsonl`; session/API/output/playerCount must match. Local test accounts can be created with the existing registration UI/API. `gameplay=false` selects Shadow in the Development harness; `mode=Fixed` retains Fixed gates, regardless of gameplay flag. For Fixed, Shadow or scenario-driven sessions, add **`-SessionOnly`** and omit `-ObserveSecondsAfterProof`; the launcher captures real world/control journals for `TimeoutSeconds` and reports `CAPTURED_UNVERIFIED`, never Adaptive PASS. The default positive comparator requires Gameplay and a complete actual transaction. Invoke the script in the current PowerShell to pass typed SecureStrings; do not serialize passwords into command arguments.

Four-player fairness: set `playerCount=4` on all four configs. Extra configs have `role=Client`, labels `Client2`/`Client3`, distinct accounts and command files. Pass `-AdditionalClientConfig @('<...Client2-private-config.json>','<...Client3-private-config.json>') -AdditionalClientPassword @($pilotClient2Password,$pilotClient3Password)` to the same native launcher. Each password must be a SecureString. No process writes another process's journal. Match start waits for the configured full roster. The comparator checks the expected topology.

## 6. Pilot flags and truthful prerequisites

Production defaults stay OFF: `AEDV02Pilot.Enabled`, `CabinetAmbushEnabled`, Unity `PacingV02GameplayEnabled`/`CabinetAmbushEnabled`. The isolated Docker script enables Backend pilot flags only in its generated Development compose. The build harness changes Unity settings **in memory** only after explicit `-aedHarnessConfig` and a loopback API. Do not edit the shipped settings asset to run these cases.

Pressure comes from real gameplay. Unknown is not PP0. Adverse decisions require eligible real ended-match history: at least three comparable sealed matches, eight resolved opportunities per required axis/family, recency/confidence and weakest-player protection. All five Tool families must independently qualify. Fresh accounts can test neutral/relief, but cannot certify Cabinet/adverse decisions. Scripts never populate forged history, set PP/TP or mark a snapshot Verified. Missing prerequisites produce a blocked scenario; this is not an acceptance PASS.

## 7. Twelve owner scenarios and controls

For human gameplay acceptance, create the session with **-ManualInput** and launch with **-SessionOnly -VisiblePlayers**. The ordinary keyboard/mouse controls remain available in the focused Player. After a recipe takes over movement, send **manual-input** to return that role to normal input. No Editor Play is required: Host and Client can both use the same Development Windows build. These options are for later owner execution only.

Launch with a long enough observation interval/config timeout. In another PowerShell, use the actual session output:

```powershell
$pilotOutput='<new-output>'
rtk proxy powershell -NoProfile -File scripts/Invoke-AEDV02AcceptanceScenario.ps1 -OutputDirectory $pilotOutput -Scenario E2E-P4-01 -Role Client
# For Backend outage cases also pass: -StackJson $pilotStackFile
```

The driver sends normal movement/interactions and records conditions. It does not automatically approve the twelve gameplay acceptance assertions. Reach each required phase by normal objectives; it cannot skip phases, teleport, inject damage, force a pressure tier or forge telemetry. Use the formal Rev02 acceptance document for visual/gameplay judgment.

| ID | Real prerequisite / driver action | Owner acceptance evidence |
|---|---|---|
| E2E-P4-01 | Fresh canonical PP4; move to registered Cabinet, hide, actual token, flashlight | Hidden throughout positive protection timer, including observed entry; zero unauthorized force-exit |
| E2E-P4-02 | Same; observe actual expiry and later inspection grace | Actual 10 s token, 3 s grace, no immediate pull or camping |
| E2E-P4-03 | Actual token, scoped Backend offline then restored | Existing token survives outage to TTL; no new unsupported token; progression continues |
| E2E-CAB-01 | PP0/1, TP3/4, approved Cabinet plan/history, real entry clue | Same encounter: ready audio/visual cues, >=2.5 s warning, JumpIn at valid 8–12 m path, physical approach, revalidation/inspection |
| E2E-CAB-02 | Same; driver voluntarily exits during warning | Same encounter abort/backoff, no forced exit/duplicate jump/quota |
| E2E-CAB-03 | Same, remote Client transport shutdown during warning | Reconnect with same identity, restored state, no double jump/force-exit/quota; no client-caused match abort |
| E2E-PP-01 | Four real players with distinct verified pressure | Physically eligible LOS targets only, bounded fairness, attack target frozen, Unknown treated separately |
| E2E-PP-02 | Driver follows actual Minion, waits for slow, then follows Stalker until Down | Real slow/Down/causal events; no duplicate Down contribution; relief matches cause |
| E2E-Z3-01 | Normal objectives to Zone3; eligible actual approval | Fixed Normal zero; Adaptive actual spawn at approved cap 1; relief cap 0 does not delete active Minion; no Cabinet stacking |
| E2E-TT-01 | Real Zone2 supply materialized; use/pickup/drop normal Tools | Five required types, preserved materialized IDs, future optional reduction only; support new instance/marker/receipt, no double spawn |
| E2E-F4-01 | Normal objectives reach FinalHunt/Escape | No new positive Stalker/Minion/Cabinet adjustment; normal completion possible |
| E2E-AED-01 | Scoped real HTTP outage/recovery; continue normal phases/windows | PreMatch, Boundary, two distinct same-phase SafeWindows, idempotent retry, matching Host/Client actual effect and receipt |

Direct controls use `Send-AEDV02NativeCommand.ps1 -OutputDirectory $pilotOutput -Role Client -Action ... -Target ...`. Actions: `move-marker` (manifest ID), `move-target`/`aim-target`/`interact` (real network object ID), `hide` (registered HideSpotId), `exit-hide`, `flashlight`, `use-tool`, `drop-tool`, `scan-core`, `scan-motion`, `stop`, `manual-input`, `transport-loss` (Client only), `join`, `leave`, `shutdown`. Reusing `-CommandId` deduplicates input. `REQUEST_SUBMITTED` is not a world effect. Normal authority/range/LOS/charge/cooldown checks still apply.

## 8. Results and correlation

- Backend: `backend-full.trx`, `test-summary.json` in selected output.
- Unity: `EditMode.xml`, `PlayMode.xml`, stage logs and `*-summary.json`.
- Build: `Windows/build-report.json` and all Player files.
- Campaign: `campaign-summary.json` under `KLTN/Temp/AEDV02/Campaigns/<timestamp>`.
- Native: `Host.log`, `Client.log` (and optional Client2/3 logs), `*-readback.jsonl`, `*-world.jsonl`, `*-controls.jsonl`, `native-proof.json` and per-scenario journals.

Compare **MatchId, DecisionId, logical/native phase, WindowId, roster, plan revision, plan fingerprint, action fingerprint, scene fingerprint and policy version**. `[FUSION_CONFIG]`, `[SOURCE_VERIFIED]`, `[HOST_COMMIT]`, `[APPLY_RECEIPT]`, Cabinet reservation/phase/world-state logs are correlated evidence. A replicated plan alone is insufficient. Check actual consumer values/presentation and later world effect. Legitimate Hold/Reject and a rejected late join into a closed room are not automatically AED failures.

## 9. Send failures to the reviewer

Include full commit SHA, command/exit code, relevant TRX/XML/build-report, sanitized logs around the first error, scenario ID and shared transaction identities. State NOT RUN if a step never executed. Keep original assertions and the three earlier CI regression cases. Do not send `.env`, compose.json, tokens, passwords, stack credentials, PlayerPrefs or unredacted HTTP Authorization headers. Test counts from older commits do not certify this candidate.

## 10. Safe reset

The native launcher stops only the Player processes it created. It does not close any Editor. Preserve logs before resetting. To stop or discard only the owned disposable stack:

```powershell
rtk proxy powershell -NoProfile -File scripts/Stop-AEDV02NativeBackend.ps1 -StackJson $pilotStackFile
# Explicitly discard only this stack's test database volumes:
rtk proxy powershell -NoProfile -File scripts/Stop-AEDV02NativeBackend.ps1 -StackJson $pilotStackFile -ResetData
```

For outage exercises only, use `-Action BackendOffline` / `-Action BackendOnline`; these operate on that stack's Backend service. No production or owner Development stack is selected. Use a new RunId/output directory for the next campaign. Never delete Library/Temp/Editor locks to reset tests. Clear test-only environment variables after verification and dispose SecureString objects; do not print their values.
