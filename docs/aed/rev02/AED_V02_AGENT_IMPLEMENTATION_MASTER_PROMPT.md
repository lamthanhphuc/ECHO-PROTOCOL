# AED v0.2 — MASTER IMPLEMENTATION TASK FOR CODING AGENT

**Project:** ECHO PROTOCOL / KLTN
**Repository:** `lamthanhphuc/ECHO-PROTOCOL`
**Base branch:** `LTP` (baseline checked at `10391b1d820a9f25d2352d925dbf5bfc39962e50`, 2026-10-14; always refresh HEAD)
**Target:** Complete implementation and automated verification. **Human performs final in-game E2E only.**
**Deliver:** feature branch, PR into `LTP`, source/migrations/tests/scenes/config/docs, test evidence, reproducible E2E build and checklist. **Never auto-merge.**

## 0. Decision and source-of-truth contract

Authoritative specification files to ingest in full **before coding**:

1. `AED_Dynamic_Pacing_Design_v0.2_Stalker_Rev02_Complete.md` — sections 0–XVIII, especially XV–XVIII overriding older candidate ranges; keep scenario IDs/acceptance gates.
2. `AED_Stalker_V0.2_Bang_Duyet_Rev02.md` — values A–G, invariants, 20 simulations and 12 E2E cases.
3. Existing `docs/aed/*` in `LTP`, AED/Phase8 API contracts, Unity scripts, Backend tests and configuration. **Actual current source wins for facts about existing implementation; newly approved policy wins for intended behavior**. Report conflicts rather than silently guessing.

**Authorization guard:** The project's three Stalker principles and three earlier decisions (Adaptive Zone3 Minion; optional Tool budget; Boundary+Safe Window together) have been confirmed. Values **A–G are candidate tuning, NOT approved yet in the document**. Only implement them as the initial **test/pilot profile** after the owner explicitly says `Chốt A–G theo Rev02`. If approval has not been given, you may build the code, interfaces, tests and shadow/local test profiles, but keep gameplay feature gates OFF; never present tuning or production enablement as approved. Do not ask the owner to approve each coding subtask.

### Confirmed, non-negotiable gameplay rules

- Adaptive only; **all Fixed difficulty modes and Normal baseline remain unchanged**. `MatchDifficultyProfiles.Normal.Zone3MinionCap` remains **0**. Adaptive Zone3 has separate approved 0→1/1→0 policy (no forced despawn).
- Each zone retains **five mandatory Tool types** (`Scanner`, `NoiseMaker`, `FirstAid`, `DoorJammer`, `CoreStabilizer`). Optional supply may go 2→1→0 **only if not yet spawned**. Never delete/teleport a spawned/held/dropped Tool.
- PreMatch, Phase Boundary and Safe Window coexist in **one release**, with shared global budget and separate ledger identities; 3 zones and 4 logical AED phases must map to the actual `NetworkMatchPhase` FSM, without replacing it.
- P4 (PP≥0.85 candidate) in a protected Hidden Box **cannot be force-exited**, even if watched entering or flashlight is on, during an active 10s candidate token; enforce **immediately before** `TryForceExitHidingAuthoritative()`, including inspection and ambush paths. Existing token persists to TTL through temporary evidence/Backend failures. No permanent invulnerability or infinite farming.
- Cabinet Ambush is a **new** special encounter, NOT a fake Down trigger: authentic evidence/clue -> target eligible PP0/1 -> Host validation -> audio+visual warning visible to clients -> Jump In **near**, not inside, the cabinet -> valid NavMesh/door route -> approach -> recheck -> inspect -> force-exit ONLY if still eligible. Abort safely otherwise. Pilot candidate: Z1/Z2 only, no Phase4, 8–12m path entry, min player distance 7m, warning≥2.5s, own cooldown 420s, quotas 1/zone, 2/match, 1/player. No cue, route or escape path: HOLD/abort. Respect existing Special minimum 300s cooldown, JumpOut≥5s and JumpIn≥3s; never loosen silently.
- **Player Pressure 5-source** (`Stalker`, `Minion`, `Down`, `Resource`, `Objective`) tracked per player, authoritative and provenance-aware; do not double-count Minion→Down. Distinguish `PP_UNKNOWN` from `PP0`, team pacing pressure `TP`, and **global pressure-change budget**. No new adverse adaptation on stale/unverified sources. PP candidate weights S=.30/M=.20/D=.25/R=.15/O=.10; observation 120s, half-life 90s, minimum 30s; candidate PP thresholds .25/.45/.65/.85 and hysteresis .05.
- Initial pilot: **at most one positive pressure token** across Stalker, Minion, optional Tool squeeze and Cabinet Ambush. No stacking +1 Stalker with Adaptive Zone3 Minion 1, no Cabinet Ambush with Zone3 Minion +1, no new pressure increase during Final Hunt/Escape. Fairness adjustments may relieve stress but cannot break LOS/occlusion/game rules or become compound aggression.
- Missing or stale Backend/source/roster/authority => `HOLD` for **adaptation**, continue legitimate phase/objective progression (do not softlock gameplay). Do not record an `APPLIED` receipt without real effect evidence. Never bypass verified-sources gate by hardcoding `true`.

## 1. Working mode and rules

- Begin from latest `LTP`, record base SHA and working tree status; create feature branch `feature/aed-v02-full-integration` (or collision-free equivalent). Do **not** commit on `LTP`/`main`; do **not** auto-merge, force-push, or silently modify secrets/environment-wide production flags.
- Read full spec and inspect current source before editing; produce `docs/aed/AED_V02_Implementation_Map.md` mapping each requirement and scenario family to exact files, implementation points, tests and actual blockers. Reuse V1/V2 directors, source collectors, existing Fusion/net lifecycle. Avoid parallel duplicate AED architecture.
- Execute end to end **without waiting for human sign-off between stages**. On minor undocumented choices choose conservative/fail-closed behavior, record choices in `docs/aed/AED_V02_Deviation_Log.md`. Only stop for true missing access, legal/prod-credentials issue, missing Unity scene authoring assets, or explicit contradicting design decisions. When blocked, complete every unaffected task and list exact blocker, impact, owner action and proof.
- Commit incremental vertical slices, run affected tests after each slice, fix failures/retry, run full regression before PR. If unable to run a test in your environment, label **NOT RUN** with reason; do not claim success from static inspection. Never weaken/skip an existing failing test to make CI green.
- Ask for permissions/licenses only through the approved tooling; never invent credentials. Preserve project asset `.meta` IDs and serialized Unity references. Migrations must be reversible/tested and backward-compatible.

## 2. Implementation sequence (finish all, not only prototype)

### W0 — Environment and baseline

1. Inventory GitHub branch/commit, Unity project version in `KLTN/ProjectSettings/ProjectVersion.txt`, package dependencies (`Fusion`), C# test assemblies, .NET SDK, DB provider/migrations, API launch and sample config. Inspect differences since verified `10391b1`.
2. Run existing `dotnet test EchoProtocol.Backend/tests/EchoProtocol.Api.Tests/EchoProtocol.Api.Tests.csproj` before changes. Run available Unity EditMode/PlayMode suites when licensed. Save baseline stdout/results; classify pre-existing failures separately.
3. Add a reproducible test/run script (`scripts/` or existing conventions) and machine-readable summary. Repo currently has a Backend `.csproj` and Unity EditMode/PlayMode `.asmdef`, but no `.github/workflows/` found on inspected `LTP`; set up suitable CI for executable tests if permissions allow, and document limitations for licensed Unity runners.

### W1 — Backend contract, ledger and verification (P0)

1. Version API/DTO/catalog/DB schema for `PRE_MATCH`, `PHASE_BOUNDARY`, `SAFE_WINDOW`. Create canonical `(matchId, phaseOrdinal, decisionPoint, windowSequence/windowId)` identity and ordered `planRevision`; source range/fingerprint; roster/lease/policy/catalog; expiry; idempotent `decisionId` and `receiptId`; single-key in-match delta vs atomic composite PreMatch.
2. Fix predecessor lookup (`PhaseOrdinal` alone is insufficient), pending/abort/reconcile and unique indexes for multiple windows in one phase. Preserve V1/V2 compatibility; implement migrations and migration tests.
3. Remove unconditional policy `HOLD`/PreMatch committed block **only through new validated version**, not by global bypass. Full ability to approve and apply after independent verification; default to `HOLD` otherwise.
4. Complete Phase8 independent source verification for historical comparable match evidence, bounded player/team pressure, cause & opportunity attribution, minion population census, resource inventory/world/placement supply and lifecycle/roster. Verify canonical events/server source signatures, source coverage and scene marker/route trust. Evidence booleans provided by client are NOT proof. Unit/property/negative/replay tests required.
5. Make authority lease, stale revision/fingerprint, rollback/abort, commit receipt and effect observation robust under timeout, duplicate requests, reconnect. Backend never silently approves because of missing data.

### W2 — Shared AED Pacing Coordinator (P0)

1. Implement typed, versioned `PreMatch + Boundary + SafeWindow` context and orchestration leveraging existing `AEDv2Authority`, `AEDv2BoundaryCoordinator`, policy/directors. Support safe-window scheduling independent of `TryAdvancePhase` and never block mission progression for Backend approval.
2. Compute validated historical skill with minimum opportunities; bounded pressure/evidence, source attribution and confidence. Maintain separate per-player `PP`, team `TP` and global pressure budget; dedupe correlated sources and avoid known exploit/farming loops.
3. Cumulative +1 token cap, per-axis cooldown/dwell/hysteresis, handoff/reversion as separate approved one-delta transaction; no multi-axis increase in a single in-match commit; no Phase4 adverse increase. `HOLD` with reason is first-class.
4. Host revalidates actual world state, phase, roster, active Stalker/Minion threats, objectives, door/route, expected revision and source age at **apply**, including stale asynchronous approvals; publish versioned Fusion revision/config fingerprint. Refuse unsafe transitions without blocking gameplay.
5. Implement observable application evidence, idempotency, trace IDs, counters/telemetry and receipt reconciler.

### W3 — Stalker Player Pressure / fairness / Hidden Box (P0)

1. Integrate five authoritative pressure streams into Player Pressure, with source evidence and dedup; `PP_UNKNOWN` neutral sensory profile (1.0) and **not** ambush-eligible. Candidate sensory multipliers PP0..PP4 `1.08/1.04/1.00/.92/.85`; transition smoothing/freeze during committed Chase; no through-wall LOS, fabricated sound or sudden mid-attack retarget.
2. Extend existing `AdaptiveStalkerTargetPolicy`, `StalkerTargetHistoryMemory`, `StalkerTargetPolicySignalBuilder`, vision/hearing and Core carrier/Seek target fairness. Respect objective carrier behavior, legitimate target eligibility and protective recovery; candidate score offsets `+.15/+.08/0/-.15/-.25` and seek recovery 15s.
3. Add host-authoritative P4 protection token lifecycle: 10s window, 120s/player cooldown, 2 grants/match/player, 3s post-grace, PP3 25% lighter reinspection; stable `playerId+hideSpotId+tokenId+TTL`, replication and replay logging. **Check the guard before every force-exit**, especially `StalkerUnityHideSpotAdapter.TryResolveInspection()`. Ignore *only* linked flashlight/clue while protected, never all lights/other players; no Stalker camping and immediate reinspection at TTL expiry; honor existing token if Backend goes down.
4. Candidate hide-reveal track/grace per tier: PP0 `.45/1.25s`, PP1 `.40/1.35s`, PP2 `.35/1.50s`, PP3 `.25/1.80s`, PP4 `.20/2.20s` only outside active protection, UNKNOWN baseline. Preserve animation/Down lifecycle and state authority.

### W4 — Cabinet Ambush (P0)

1. Implement independent encounter FSM (`candidate/reserve/cue/warning/jump/approach/revalidate/inspect/success-or-abort/recovery/cooldown`) alongside the existing Down-triggered Special, with mutual exclusion. Use **verified authored/gameplay clues only**; occupancy is allowed for revalidation of a clue-selected spot, never to infer location from PP/hidden network flags.
2. Enforce profile, quotas, player recovery, no simultaneous threats, host authority, cap and route/door/escape validity; validate visual+audio warning presentable to relevant clients **before** 2.5s reaction period. Abort safely if missed cue, path blocked, player left, pressure rises, protection acquired, authority changes, objective sensitive, or world evidence becomes stale.
3. Emit traceable cue/entry/approach/inspection/force-exit/success/abort events with immutable encounter ID. No teleport directly inside/adjacent to the box and no instant force-exit from the teleport step.
4. Gate pilot to Zone1/Zone2 only, exclude Phase4, keep production-off until E2E approval. Do not change existing Special minimum 300/5/3 seconds.

### W5 — Minion & TeamTool (P0)

1. Introduce **Adaptive-only** Zone3 Minion cap key/policy, approval and applied profile, actual `PlayerSpawner` population tracking, spawn only in legal safe state, 0→1 after sufficient verified Zone3 evidence and 1→0 without forcing despawn. Preserve Fixed/Normal 0 and existing Zone1/2 baseline. Never stack with Stalker +1 or Ambush in pilot.
2. Refactor `TeamToolWorldSpawn.TrySpawnInitial()` to **reserve/place/spawn per-zone** while preserving the 5 mandatory types per zone, stable deterministic IDs, Fusion authority and item persistence. Add optional budget 0..2/match; 2→1→0 affects only extras not materialized; Safe Window may spawn approved support only at validated marker/routes. Never remove/teleport already spawned or carried objects.
3. Introduce production `ZoneEntryMarker`/near-corridor/goal identifiers for Zone1/2/3. Implement reachability as actual NavMesh path + closed-door/scene state validity, not just straight-line proximity. Handle missing markers and blocked doors with safe baseline/abort and logs. Respect Unity scene serialization and references.
4. Zone2/Zone3 debug skip must exclude tainted historical/pressure evidence; cannot be treated as ordinary completed phase. Reconcile normal phase FSM and subphase mapping. Do not use debug teleport coordinates as production source.

### W6 — Runtime, observability and automated verification (P0)

1. Ensure Host writes and Fusion synchronizes revision, authoritative world entities, token/cue state and receipt mapping; remote and late-join clients do not replay spawn or ambush. Confirm Backend APPLIED only after real observed effect; reconcile retries and partial failures.
2. Add deterministic C# unit/property tests for source confidence, cause/provenance dedup, five-source PP, tier/hysteresis/unknown, budget, distinct window idempotency, stale lease/fingerprint, two-phase commit/receipt, token guard, Cabinet Ambush FSM, minion caps and optional Tool planner. Use existing `EchoProtocol.Api.Tests` and Unity test assemblies instead of test-free pseudo-code.
3. Add automated Host/client integration or deterministic simulation tests where infrastructure permits. Test null states, authority migration, backend outage, navmesh/door blockage, scene reload and debug skip. Run all existing tests, not only AED new tests.
4. Keep gameplay feature flags OFF by default in production. Make local/test profile explicit and safe for human E2E. Provide configurations/scripts/startup commands without secrets and emit run-scoped structured logs.
5. Document an honest coverage table with `PASS/FAIL/NOT RUN` and artifact paths, including 12 canonical E2E IDs from the spec. Build smoke-testable artifacts (Windows Unity player or reproducible editor run if possible).

## 3. Quality and final delivery gates

**Do not claim “DONE” until all true:**

- Source committed on one or more reviewable feature PRs targeting `LTP`; code compiles in available environments; Backend full suite and Unity EditMode/Stalker PlayMode tests pass **where executed**; automated E2E/integration test evidence exists.
- All mandatory Source Verification and Backend approval paths are real, safe and test-covered (no fake source flags/receipts); PreMatch and multiple SafeWindows per phase work; no broken gameplay progression on Backend timeout.
- P4 protected hide cannot force-exit; Ambush always warns, approaches via real route and revalidates; pressure source five-fold and dedup; budget invariant; Adaptive Zone3 only; five required Tools preserved; Fixed modes unchanged; Phase4 no increase.
- Every requirement in main Rev02 doc and every ID from `PF/VH/HB/HT/CC/SK/JP/CAB/SIM/ACC/E2E` mapped to a specific test or a documented manual E2E and code location, including negative paths.
- Ready-to-run final E2E checklist includes exact steps, expected result, scene setup, client count, credentials-free local config, log/trace capture and known limitations. No production gameplay enablement and no auto-merge pending final E2E approval.

## 4. Return format to owner (single final report)

1. `Feature PR`: URL, base SHA, head SHA, changed files, migrations, scene asset changes.
2. `Implementation`: Done list per W0–W6 with traceability and deviations.
3. `Verification`: exact commands, environment, PASS/FAIL/NOT RUN totals, direct test/log artifacts. If Unity tests were not run, say exactly so.
4. `Remaining blockers`: only environmental/manual limitations and tests that cannot be automated, severity P0/P1, evidence, how to reproduce; no surprise at final E2E.
5. `Final E2E`: local/test configuration, install/run steps and checklist of `E2E-P4-01…03`, `E2E-CAB-01…03`, `E2E-PP-01…02`, `E2E-Z3-01`, `E2E-TT-01`, `E2E-F4-01`, `E2E-AED-01`.
6. `Release decision`: keep gameplay OFF until owner performs final E2E, signs off, and separately authorizes merge/production flags.

**Operational rule:** Implement without intermediate owner approvals on coding details. Never silently change finalized gameplay rules, forge tests/verification, auto-merge, or switch on production AED to claim completion. If anything essential cannot be built/tested, report precisely rather than declaring that the owner only has E2E left.
