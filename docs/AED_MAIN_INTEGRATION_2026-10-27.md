# AED / main integration, 2026-10-27

Branch: `integration/aed-main-20261027`, worktree: `D:\Bin\KLTN-aed-main-integration`.
Parents targeted: feature `46fab78f`, original `origin/main` `0466abc1`. A later main commit `599076e8` (Lobby settings sync) was merged separately; the final HEAD must include both merge commits.
State: all merge paths reconciled; the test results below distinguish passing suites from unresolved PlayMode failures.

As of 2026-10-28, the integration index has **0 unmerged files** and the conflicted source/scene paths have **0 Git conflict markers**. This is a source reconciliation checkpoint, not the final acceptance result.

## Conflict accounting

The merge reported 32 conflicted paths. The table names each path, the main behavior to retain, the AED behavior to retain, the relevant caller or serialized consumer, and the needed invariant/check. “Edited” means a source-level reconciliation exists in the worktree; it does not mean runtime PASS. `Lobby.unity` originally had 132 textual conflict blocks; its main UI hierarchy is now selected after checking that the feature-only objects are superseded outfit/pet panels and the feature scene has no AED/Adaptive binding.

| Path (under repository root) | Main API / behavior | AED hook / behavior | Caller or serialized consumer | Invariant and check | State |
|---|---|---|---|---|---|
| `EchoProtocol.Backend/src/EchoProtocol.Api/Services/MatchAuthorityService.cs` | Disconnect with expected user ID and conditional update | Match lease and PreMatch lifecycle | MatchAuthorityController, Unity MatchAuthorityRuntime | No stale actor can disconnect remapped user; backend focused tests | Edited; focused PASS |
| `EchoProtocol.Backend/tests/EchoProtocol.Api.Tests/TelemetryServiceTests.cs` | Provenance test | P8 telemetry tests | TelemetryService | Both test groups execute at merge source | Edited; focused PASS |
| `KLTN/Assets/Scripts/AI/AED/ScenarioConfigAuthorityRuntime.cs` | MapId in resolution request | Extended-policy request and Fixed fallback | MatchState, LobbyManager | Map identity and fail-closed Fixed path; EditMode | Edited, not run |
| `KLTN/Assets/Scripts/AI/Minions/CreepMinionRuntime.cs` | Networked roles, Resonance, dive and migration | Spawn/despawn, noise/alert, attack, tool/core, flashlight facts, snapshot | PlayerSpawner, Scanner, Stalker, MatchAuthorityRuntime | Facts only after authoritative outcomes; EditMode and Host/Client | Edited, not run |
| `KLTN/Assets/Scripts/AI/Stalker/Networking/StalkerFusionRuntime.cs` | Purge and host-migration audit | Pursuit snapshots and V02 perception | MatchState, Stalker AI | Fixed/Purge and AED state coexist; EditMode and Host/Client | Edited, not run |
| `KLTN/Assets/Scripts/AI/Stalker/Presentation/StalkerAnimatorPresenter.cs` | Purge animation pacing | AED native presentation probe | Stalker prefab | Probe remains pilot-only; animator test | Edited, not run |
| `KLTN/Assets/Scripts/AI/Stalker/StalkerNavigationController.cs` | Last valid NavMesh position | AED path safety validator | StalkerFusionRuntime | Rejection cannot erase valid position; navigation test | Edited, not run |
| `KLTN/Assets/Scripts/Gameplay/MatchDifficulty.cs` | Map2 population requirements | Fixed Map1 Normal cap baseline | PlayerSpawner, lobby | Map1 1/2/0 and Map2 explicit caps; EditMode | Edited, not run |
| `KLTN/Assets/Scripts/Networking/FusionPlayerLifecycle.cs` | Result roster capture | Clear verified backend identity | NetworkBootstrap, MatchAuthorityRuntime | Reconnect requires fresh binding; EditMode | Edited, not run |
| `KLTN/Assets/Scripts/Tools/Scanner/NetworkFieldScanner.cs` | New scanner targeting and host-migration guard | Verified AED scan evidence producer | MatchAuthorityRuntime, player input | No fact from unverified scan; EditMode/Host-Client | Edited, not run |
| `KLTN/Assets/_Project/Scripts/Networking/Player/NetworkPlayerMovement.cs` | Resonance and input migration | P4 hide protection and V02 slow | Player input, Stalker targeting | Simultaneous Resonance and hide protection; EditMode/PlayMode | Edited, not run |
| `KLTN/Assets/Scripts/UI/MainMenu/MainMenuMatchmakingController.cs` | Game mode selection | Fixed/Adaptive selection | NetworkBootstrap.CreateRoomAsync, MainMenu scene | Both selectors serialize to distinct session properties; UI/PlayMode | Edited, not run |
| `KLTN/Assets/_Project/Scripts/Networking/Authority/MatchAuthorityRuntime.cs` | Entitlement verification, safe disconnect, nonblocking telemetry | PreMatch, pursuit/minion/objective evidence censor and receipt | LobbyManager, MatchState, player lifecycle | No stale binding; no gameplay rollback from telemetry exception; EditMode/Host-Client | Edited, not run |
| `KLTN/Assets/_Project/Scripts/Networking/Interaction/NetworkTeamToolPickup.cs` | Pickup availability | World-source availability | TeamToolWorldSpawn, interactor | Consumed pickup cannot appear available; EditMode | Edited, not run |
| `KLTN/Assets/_Project/Scripts/Networking/Match/NetworkMatchState.cs` | ResultSession, migration restore, Purge/Hunt | PreMatch commit, AED boundary receipt and cleanup | MatchAuthorityRuntime, PlayerSpawner | Approval→Host→Consumer→Receipt and result capture; EditMode/Host-Client | Edited, not run |
| `KLTN/Assets/_Project/Scripts/Networking/Player/LobbyPlayerState.cs` | Confirm backend identity RPC | Clear verified backend identity | MatchAuthorityRuntime, lifecycle | Proof cannot survive disconnect; EditMode | Edited, not run |
| `KLTN/Assets/_Project/Scripts/Networking/Player/NetworkPlayerLifeState.cs` | Forced item drop on elimination | Incomplete evidence on failed drop | Interactor, MatchAuthorityRuntime | One authoritative drop, no false receipt; EditMode | Edited, not run |
| `KLTN/Assets/_Project/Scripts/Networking/Player/PlayerSpawner.cs` | Map2/Purge population and lizard alias | Minion cap/receipt and Zone3 safe-window guard | MatchState, CreepMinionRuntime | Fixed-Normal Map1 baseline and Map2 role caps; EditMode/Host-Client | Edited, not run |
| `KLTN/Assets/_Project/Scripts/Networking/Session/LobbyManager.cs` | Committed roster wait and match-start gate | Adaptive PreMatch staging and cleanup | MatchAuthorityRuntime, MatchState | Binding before snapshot; cleanup on rejected start; EditMode | Edited, not run |
| `KLTN/Assets/_Project/Scripts/Networking/Session/NetworkBootstrap.cs` | GameMode/Map session properties, reconnect proof | AED resolution mode property | MainMenu, LobbyManager | Distinct properties and fresh proof; EditMode | Edited, not run |
| `KLTN/Assets/Resources/PF_CreepMinionNetwork.prefab` | Attack recovery value | Steal cooldown value | CreepMinionRuntime | Serialized fields both present; prefab inspection | Edited, not run |
| `KLTN/Assets/Scenes/Bootstrap.unity` | Map2 lizard/RegionGraphs | AED spawner integration | PlayerSpawner | Serialized field names match source; scene load | Edited, not run |
| `KLTN/Assets/Scenes/MainMenu.unity` | Main UI decoration object | Resolution mode row and references | MainMenuMatchmakingController | Unique file IDs and both selectors visible; scene load | Edited, not run |
| `KLTN/Assets/Scenes/Lobby.unity` | Rebuilt lobby hierarchy and selectors | AED PreMatch remains in LobbyManager; no AED scene binding on feature | Lobby UI scripts, LobbyManager | Main selectors and match controls visible; scene load/PlayMode | Edited, not run |
| `KLTN/Assets/Prefabs/StalkerNetwork_Lizard.prefab` | New lizard VFX/render children, Map2 agent type | Existing lizard network prefab identity | PlayerSpawner, Stalker presenter | All internal file IDs resolve; prefab load | Edited, not run |
| `KLTN/Assets/Animations/Stalker/AC_Stalker_Lizard.controller` | Purge layer and speed parameter | Existing clip GUID references | StalkerAnimatorPresenter, prefab | Controller references valid clip GUID; animator load | Edited, not run |
| `KLTN/Assets/Hatogame_new/Lizard/Animation/Lizard@hit.FBX.meta` | Imported hit clip settings | Existing asset GUID | Lizard controllers | Keep GUID `ec8e...`, both controllers refer to it; Unity import | Edited, not run |
| `KLTN/Assets/Tests/EditMode/Networking/CreepMinionContractTests.cs` | Role, dive, migration coverage | Fixed caps and minion behavior tests | CreepMinionRuntime, PlayerSpawner | Both test sets compile and execute | Edited, not run |
| `KLTN/Assets/Tests/EditMode/Networking/FusionPlayerLifecycleContractTests.cs` | Host migration and queued result | Rebind contract | Lifecycle, authority | All tests execute at merge source | Edited, not run |
| `KLTN/Assets/Tests/EditMode/Networking/NetworkMatchStateTests.cs` | Phase and new HUD checks | Debug skip remains separate | NetworkMatchState, HUD | Normal completion never teleports; EditMode | Edited, not run |
| `KLTN/Assets/Tests/EditMode/Player/PlayerNetworkPrefabContractTests.cs` | Tool drop assertion | Cooldown preservation assertions | Interactor, pickup prefab | Both assertions execute | Edited, not run |
| `KLTN/Assets/Tests/PlayMode/Voice/VoiceStartupTests.cs` | Current Unity find API | Disabled mic test isolation | VoiceManager | No hardware side effect; PlayMode | Edited, not run |

## Current verification

- Backend focused MatchAuthority/Telemetry tests: **PASS**, 128 tests after restore, before full Unity reconciliation.
- Backend non-database test filter: **PASS**, 701 tests in this worktree.
- Backend solution build: **PASS**, 3 projects, 0 errors, 2 xUnit analyzer warnings after the latest main commit.
- Full backend suite: **FAIL / infrastructure**, 701 passed and 57 failed because `ECHO_M4_POSTGRES_CONNECTION` and `ECHO_PHASE6R_MONGO_URI` are absent. No local Docker was started.
- Unity C# offline compile: **PASS**, `Assembly-CSharp.csproj` 7 projects, 0 errors, 31 warnings after repairing the presenter duplicate and the Minion/Scanner/Spawner merge calls. Networking EditMode and Voice PlayMode test assemblies also compile. Six new cross-path regression guards live in `AEDMainMergeRegressionTests.cs`.
- AED contract/manifest sync check: **PASS**, `scripts/Sync-AEDV02Contract.ps1 -Check`.
- Six merge regression guards: **PASS 6/6** through the worktree Unity Editor and Pipeline server.
- Full EditMode: **FAIL / incomplete**. `AlienHuntSceneTests.BaitCaptureFuelAndBurnCycleConsumesExactlyOneAlien` enters Play mode inside EditMode; the Unity Test Runner then threw a `NullReferenceException` in `EditModeRunTask` and `InvalidOperationException` while restoring the scene. Pipeline status stayed running, so the run was cancelled. No aggregate PASS is claimed.
- Targeted EditMode: **PASS** Networking 257/257 (including six merge regression guards), AED Common 288/288, Stalker 333/333, Player 91/91, UI 12/12, Telemetry 36/36. Additional LobbyGameMode 2/2, FieldScanner 38/38, MatchDifficulty 1/1. The full suite remains **FAIL / incomplete** as noted above.
- Targeted PlayMode: Networking **PASS 1/1**, Voice **PASS 4/4**, Stalker **PASS 191/191** after repairing test fixtures to use NavMesh agent type `0`, matching the generated mesh. The prior 43 Stalker failures came from the fixtures using the project's other default agent type; the off-mesh recovery test now starts outside the generated mesh. This is test-fixture repair, not a production Stalker behavior change.
- Windows Build artifact: **PRESENT**, `D:\Bin\DBinEchoProtocol-P3-NetworkTest\KLTN.exe` (owner-reported successful build, 2026-10-28 11:23 local file timestamp). Its `sharedassets0.assets` contains `https://api.3.1.238.6.sslip.io` and no `127.0.0.1:5042` string; this verifies bundled API URL. The Pipeline build invoked separately with build ID `build_73d61f799595` later returned `idle` after an Editor reload, so its completion/provenance is **NOT VERIFIED**. No online gameplay proof is claimed.
- Merge commits: initial AED/main merge `a54ae6f4`; latest main Lobby sync merge is a descendant of that commit. `git merge-base --is-ancestor origin/main HEAD` returned success after the second merge. No unmerged entries or conflict markers remained.

## Scene blocker

`Lobby.unity` changed in two incompatible UI structures. Main has 1,910 serialized objects; feature has 2,413; 57 feature objects are newly added, 174 main objects are newly added, and main removed 620 objects still present on feature. Seven parent IDs referenced by feature-added objects are absent on main. Feature-only components are superseded outfit/pet panels; main has the new selectors and the feature scene has no AED/Adaptive serialized binding. The main scene hierarchy is selected as a whole for this file. Unity scene import, visual inspection, and PlayMode are still required before calling the scene valid.

## Later EC2 rollout plan (no production action in this task)

After merge commit and current-commit validation: compare the production Compose trio and migrations; capture PostgreSQL/MongoDB backups and restore checks; tag the running backend image immutably; build the exact SHA in CI/remote builder; require the project's manual production gate; update only backend unless Web diff requires it; run health, auth, match authority, AED and Web smoke tests; roll back image/config on service failure without automatic database rollback.
