# AED v0.2 Rev02 verification — 2026-10-15

**Release decision: NOT E2E READY. Keep gameplay OFF.** This is a reviewable implementation checkpoint, not completion of W0–W6. [Outstanding contract and engineering gates](AED_V02_Remaining_Contract_Decisions.md) are part of this report.

## Current implementation-only candidate (2026-10-16)

Continuation begins at 243eceec on feature/aed-v02-full-integration. All Backend compilation/tests, Unity tests, Windows builds, migrations and native gameplay are **NOT RUN** by explicit owner instruction. Older test/build figures below are historical.

| Gate | Current evidence |
|---|---|
| Backend compile/unit/integration/PostgreSQL/MongoDB | NOT RUN |
| Unity EditMode / PlayMode | NOT RUN |
| Windows Development build / startup | NOT RUN |
| Native approval -> Host -> remote Client -> consumer -> receipt | NOT RUN / UNVERIFIED |
| Twelve owner acceptance cases / Fixed runtime regression | NOT RUN / UNVERIFIED |
| Real scene author/export | Executed in the already open stopped Editor; asset-generation evidence only |
| Static review | Source/DTO/producer/verifier/consumer/receipt paths, scripts syntax, shared mirror, scene object diff, default flags and main differences; not runtime proof |

Real export: manifest bd08701c7e61ea95fa938161ddfac50c3b53a3d3736e163edb72b212df3b96bc; 289 markers (112 Tool, 107 Cabinet, 67 Cabinet entries, 3 zone entries), 7,719 triangles, 15,732 links, 20 doors. Actual spacing/tuning and semantic Tool roles are included. Scene changes add only the owned pilot-entry root/children; serialization-only churn is removed. Bootstrap stayed clean; owner Editor settings and protected assets were preserved. The authoring mechanism executed an isolated Editor authoring script and later the loaded exporter; this is not project compilation or a test result.

Current source adds Scanner/Jammer independent reconstruction, canonical lifecycle/real-result producers, new regression cases, real scene assets, native controls/2–4-process topology, actual consumer MatchId, same-revision refresh/baseline restore, Scanner match reset and owner runbook. Pilot DEFAULTS_2 reserves Cabinet budget in Z1/2 without changing A–G numbers. All gameplay defaults stay OFF.

Prior CI 38700470494 at d074a260 reported Backend compile success, 654 PASS / 3 FAIL / 0 SKIP. The three retained regression repairs (composite settings, legacy-source precedence and PostgreSQL pre-column seed/up/down/up) are authored in 243eceec; execution is still outstanding. No old CI result is attributed to this source candidate.

See OWNER_TEST_RUNBOOK and Remaining Contract Decisions. Actual historical eligibility, visual/audio/physics effects, remote replication and transport lifecycle are UNVERIFIED until owner execution.

## Historical executed tests — older checkpoint

| Suite | PASS | FAIL | SKIP | Evidence |
|---|---:|---:|---:|---|
| Backend full .NET 8 | 575 | 0 | 0 | `KLTN/Temp/AEDV02/Runs/20261015-005646759/backend-full.trx` |
| Of which real PostgreSQL integration | 52 | 0 | 0 | Included above; isolated loopback test DB/schemas, migration Up/Down/Up preserves existing rows |
| Of which real MongoDB integration | 5 | 0 | 0 | Included above; loopback replica set, unique disposable test DBs |
| Remaining Backend unit/component/SQLite | 518 | 0 | 0 | Included above; not all pure unit tests |
| Unity EditMode full | 1304 | 0 | 0 | `KLTN/Temp/AEDV02/UnityEditMode-final.json` |
| Unity PlayMode full | 194 | 0 | 0 | `KLTN/Temp/AEDV02/UnityPlayMode-final.json` |
| **Distinct full-suite total** | **2073** | **0** | **0** | Backend subgroups are not added twice |
| Backend AED focused (overlaps full) | 88 | 0 | 0 | `KLTN/Temp/AEDV02/v02-focused-current.trx` |
| Remote Photon Host/Client combined transaction | — | — | — | **NOT RUN / UNVERIFIED** |
| Twelve canonical acceptance scenarios | — | — | — | **NOT RUN**, do not schedule acceptance yet |

Earlier checkpoints were superseded. In particular, an earlier apparent 575/575 Backend result silently returned from five Mongo tests when the environment variable was missing. Those tests now fail explicitly if infrastructure is absent. The full run cited above really executed MongoDB; the local URI uses `directConnection=true` to avoid resolving Docker's internal advertised hostname. TLS/security validation was not disabled.

[Sanitized machine-readable evidence](evidence/2026-10-15-local-verification.json) records counters and SHA256 hashes of raw local artifacts. Raw test logs/credentials are not committed. The PR workflow uploads its own TRX, separately from these local runs.

### Test sensitivity (intentional failures, excluded from PASS total)

- Isolated source copy: removed only `roster != world.RosterIdentity ||` from receipt verification. Five cases executed: four PASS, one expected FAIL (`missing-player`). Mutation killed; original source guard remained intact. Artifact: `KLTN/Temp/AEDV02/MutationReceipt_20261015/Results/mutation-receipt.trx`.
- Missing Mongo environment: five tests executed and five failed explicitly, confirming they cannot silently report PASS. Artifact: `KLTN/Temp/AEDV02/NegativeEnvironment/mongo-missing-env.trx`.
- Initial PlayMode run found 14 failures from Cabinet publishing a network property before Fusion Spawned. Guard fixed at the publisher; both final suites above rerun successfully. Assertions were retained.

## What the positive chain proves

`V02_RealApproval_PreMatchTwoSamePhaseWindowsAndObservedReceipts` invokes the real ingestion/independent verifier/decision service with versioned authoritative-event fixtures, a registered manifest and actual SQLite ledger. It approves PreMatch revision 1, two distinct same-phase windows at revisions 2/3, refuses premature receipts until a later matching world sample, verifies effect/roster/tools/Minions and receipt replay/conflict. No fake verifier grants approval. These are executable integration fixtures, not a captured Unity match.

`V02_ApprovedHostPlan_IsConsumedByStalkerAndReplayDoesNotIncrementRevision` separately exercises Host validation, actual Stalker parameter consumption and duplicate protection. `AEDV02FusionStateTests` separately uses real Fusion Single storage and rejects corrupt reconstruction. Real sensor, warning audio/visual, hiding and supply tests run in Unity.

**A single native transaction from real Backend approval through remote Client replication, gameplay effect and receipt has not been demonstrated.** These separate proofs must not be joined into an E2E PASS claim.

## Source proof and fail-closed boundaries

Verified in executable source tests: actual spawn/despawn and zero-Minions census, player/tool custody and charge lineage, Minion steal/drop, revive/FirstAid consume correlation, closed-door mesh reachability, required runtime supply versus scene items, optional reservations/materialization, plan revision issuance, cabinet clue range/bounds/player/threat lineage.

At that historical checkpoint, normalized history, complete R/O and TP were unsupported; current producer/reconstruction code is described above and remains unexecuted. Five-channel native pressure therefore remains incomplete/Unknown; Cabinet does not reinterpret it as PP0 or a valid team TP. Non-R0 placement and support were unfinished at that older checkpoint; current source is present but unverified by execution. No validation/fingerprint/approval bypass was added to make a test pass.

## Build / startup

- Unity `6000.5.8f1`, `StandaloneWindows64`, Development + DetailedBuildReport.
- Final build `build_bac0e24f83a1`: **Succeeded**, 0 errors, 6 warnings, 1,792,152,422 bytes, 42,588 ms.
- Local artifact: `KLTN/Builds/AEDV02-Rev02-91d8aba/EchoProtocol.exe` (moved from ignored Temp output after build; all companion files retained).
- Startup probe: hidden `-batchmode -nographics`, local API asset `http://127.0.0.1:5042`; process remained alive after 15 seconds, zero `Exception`/`error` markers in startup log, then only that process was stopped. This does not prove login, gameplay, audio or multiplayer.
- Pipeline warning: no RuntimePipelineConfig asset, so Player runtime automation is disabled. Remaining warnings concern obsolete existing object discovery, an unused field and nullable DTO serialization. No warning was suppressed to obtain success.

## Fixed/main compatibility and security

- Compared `origin/main` at `fdf4f853487ea9cbe62427654ff337f9224bd75f`. Stalker/Minion prefab tuning unchanged. Fixed Normal retains caps 1/2/0, existing Special minimum 300/5/3 and the ordinary failed-attempt backoff.
- Feature has no Lobby scene/UI or MatchDifficulty changes relative to its LTP base. LobbyManager adds two v0.2 PreMatch staging/cleanup calls. Inherited LTP/main differences are not certified as whole-repository gameplay parity.
- SciFi feature change adds three AED zone-entry components and one child marker at the existing Zone3 transition. Exported geometry/doors and gameplay references are tested; no debug teleport coordinate is used as an entry source.
- Gameplay toggles default OFF. User RuntimeLogSettings and TMP fallback edits remain uncommitted. No .env, credentials, Docker local override, production API/database change or deploy.
- Migration `20261014102100_AddAEDV02PilotLedger` adds four v0.2 tables/indexes; a tested deployment migration still requires normal review before any production release. No production migration was run.

### Main advanced during final fetch

Final fetch found `origin/main=31ca1603a8572387cc437536256079745ada53dd`, five commits beyond the comparison SHA above; `origin/LTP` remains `10391b1d`. Those changes are not merged into this LTP-based feature. They add Resonance/Minion disruption, new Lobby presentation/settings, Voice and player changes. Shared files include CreepMinionRuntime, TeamToolWorldSpawn, MatchAuthorityRuntime and NetworkPlayerMovement. In particular, main's new `medkits` Lobby setting can omit FirstAid, whereas the approved Adaptive pilot requires five mandatory types. Future main/LTP synchronization needs an explicit Adaptive-versus-Fixed policy reconciliation for that setting; this feature does not silently override it. Prefab tuning is still unchanged, but parity with the newly advanced main gameplay is **UNVERIFIED**, not PASS. No claim that all current main/LTP differences are AED-only is made.

## Reproduce and trace

From `D:\Bin\KLTN` with local Docker and an idle Unity Editor:

```powershell
rtk proxy powershell -NoProfile -File scripts/Sync-AEDV02Contract.ps1 -Check
rtk proxy powershell -NoProfile -File scripts/Test-AEDV02.ps1
```

The script configures only loopback test PostgreSQL/MongoDB, captures credentials without printing them, restores environment variables, and writes a unique `KLTN/Temp/AEDV02/Runs/<UTC>/` summary. `-BackendOnly` omits Unity. Missing infrastructure, empty/failed/skipped suites are failures, never PASS. Unity requires the project's Pipeline connection; the script does not open or stop an unrelated Editor.

For review, use [323-ID traceability](AED_V02_Traceability.md), [implementation map](AED_V02_Implementation_Map.md), [source contract](AED_V02_Source_Contract_Rev02.md), [deviations](AED_V02_Deviation_Log.md) and [remaining gates](AED_V02_Remaining_Contract_Decisions.md). Per-scenario status remains UNVERIFIED where only partial unit/component coverage exists.

## Reserved final acceptance — not requested at this checkpoint

The owner's canonical [acceptance document](rev02/AED_V02_FINAL_E2E_ACCEPTANCE.md) retains the twelve scenarios: E2E-P4-01/02/03, E2E-CAB-01/02/03, E2E-PP-01/02, E2E-Z3-01, E2E-TT-01, E2E-F4-01, E2E-AED-01. All are NOT RUN. Complete the listed contract/engineering gates before making a pilot build for those scenarios.

When ready, capture one Editor Host + Windows Client with identical feature source. Compare `[AED_V02][FUSION_CONFIG]` MatchId, catalog version, revision, fingerprint, roster and scene hash on both peers; pair `[AED_V02][HOST_COMMIT]` decision/window identity with Backend approval and later receipt. Require actual consumer effect and exactly one revision change. Any mismatch, premature APPLIED, unsafe force-exit, missing cue/route, Fixed adaptation or stacked positive axes fails acceptance. Do not insert synthetic telemetry or change flags in production to obtain these logs.

## Final static inspection notes

- Shared six-file source and manifest copies compare byte identical.
- PowerShell scripts are parsed only; no test/helper campaign is executed.
- New independent Tool fixtures: 8 Jammer and 12 Scanner cases, including actual ingestion, positive/censored and fail-closed cases. Unity source adds 7 Tool serialization/shape cases and 5 Cabinet-budget cases. Native comparator adds 6 topology cases. All NOT RUN.
- Source review found and addressed retained Jammer phase identity, reconnect PlayerRef identity, old-match consumer readback/same-revision refresh, lingering v0.2 baseline, Scanner match display, native aim pitch and manual input restoration.
- Native session generation authenticates only to the same-candidate isolated loopback stack; passwords stay in memory and separate child environments. Owner can reuse actual test identities/history.
- Latest main f849d45b is inspected but not merged. Scanner match reset is selectively carried over; broader UV/Resonance, medkits/Voice, movement/fuel/convoy/UI changes remain outside this feature synchronization.
- No current compile/test/build/transport/physics/audio result follows from these static checks.
