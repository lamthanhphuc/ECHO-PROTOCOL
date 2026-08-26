# ECHO PROTOCOL — M2 AI Implementation Plan v1.0

**Canonical document:** `M2_AI_Implementation_Plan_v1.0.md`  
**Project:** ECHO PROTOCOL — Co-op Survival Horror Multiplayer  
**Document Revision:** `v1.0`  
**Document Role:** **IMPLEMENTATION EXECUTION PLAN**  
**Repository evidence snapshot:** archive `ECHO-PROTOCOL-feature-m1-026-stalker-spatial-v2.zip`, archive comment/commit reference `28f56fd6eae4af3b4a0fdf7990e0c00143b42d05`  
**Repository branch/package label:** `feature-m1-026-stalker-spatial-v2`  
**Official M2 window:** `2026-08-25` → `2026-09-20`  
**Plan Status:** **COMPLETE**  
**Architecture Escalation Required:** **NO**  
**Implementation Plan Target Scope Mode:** `ACCELERATED_FEATURE_COMPLETE_ALPHA`  
**Formal PM M2 Acceptance Mode:** `OFFICIAL_BASELINE`  
**Project-Management Rebaseline Required:** **YES**  
**Current Implementation:** **PARTIAL**  
**M2 Accelerated Feature-Complete Alpha:** **NOT READY**  
**Main Fixed-vs-Adaptive Experiment Execution:** **NOT READY**

> This plan converts already baselined/locked architecture and detailed contracts into executable implementation work. It is not a replacement architecture, does not invent implementation that is absent from the repository, and does not claim tests passed when the required toolchain/test evidence is unavailable.

---
# 1. Document Control

| Field | Value |
|---|---|
| Plan owner scope | AI / Telemetry / Profile / AED / Research infrastructure |
| Canonical architecture | `AI_Architecture_v1.1.md` — BASELINED v1.1 |
| Runtime AI model | Traditional deterministic/rule-based |
| Multiplayer authority | Photon Fusion Host Mode; Host / State Authority authoritative |
| Current Unity evidence | 6000.5.8f1 |
| Current Fusion evidence | 2.1.1 Stable build 2177 |
| Current Player count contract | 2–4 |
| Telemetry wire | v1.1; new events serialize `schemaVersion = "1.1"` |
| AED policy | `AED_SCENARIO_POLICY_V1_1` |
| Experiment protocol | `FIXED_ADAPTIVE_EXPERIMENT_PROTOCOL_V1_1` |
| Implementation Plan Target Scope Mode | `ACCELERATED_FEATURE_COMPLETE_ALPHA` |
| Formal PM M2 Acceptance Mode | `OFFICIAL_BASELINE` until an approved Project Plan/PM baseline revision promotes the accelerated scope |
| Project-Management Rebaseline Required | YES |
| Official project-plan M2 target | narrower Prototype: Research Facility + Stalker + 2–4P + core loop + basic telemetry |
| Official project-plan feature-complete P0 target | M4 Beta by 2026-10-31 |
| Build/test execution in this review | NOT EXECUTED — Unity and .NET CLI unavailable in review environment |
| Automated test evidence in repository | no project-owned Unity AI/data tests; backend `tests/` contains only `.gitkeep` |
| CI evidence | no project CI workflow found; only `docker/docker-compose.yml` among top-level YAML |

## 1.1 Status model

This plan separates four independent questions:

```text
Contract Ready
Code Implemented
Test Verified
M2 Acceptance
```

No row is marked `VERIFIED` merely because code exists.

## 1.2 M2 Scope Mode Governance — M2-PLAN-04

Canonical plan-level enum:

```text
M2ScopeMode
{
    OFFICIAL_BASELINE
    ACCELERATED_FEATURE_COMPLETE_ALPHA
}
```

### `OFFICIAL_BASELINE`

The current approved Project Plan milestone semantics remain authoritative. The supplied Project Plan freezes M2 (`2026-08-25` → `2026-09-20`) as the narrower **Prototype** milestone: full Research Facility prototype for 2–4 Players with Stalker, core loop, lobby/runtime sync, and basic telemetry/support. The same approved plan places three-Monster + Player/Team Modeling + AED P0 feature completion in M4 Beta, and Fixed-vs-Adaptive experiment/playtest work in M5.

### `ACCELERATED_FEATURE_COMPLETE_ALPHA`

The broader implementation target requested by this plan: Stalker + Listener + Warden + Telemetry v1.1 + Profile + Fixed/Scenario + AED + research-infrastructure foundations pulled forward for an accelerated Feature-Complete Alpha.

Frozen current governance:

```text
Implementation Plan Target Scope Mode:
ACCELERATED_FEATURE_COMPLETE_ALPHA

Formal PM M2 Acceptance Mode:
OFFICIAL_BASELINE

Project-Management Rebaseline Required:
YES
```

The accelerated target is an execution target only. It does **not** silently rewrite the approved Project Plan.

Promotion rule:

```text
approved Project Plan / PM baseline revision
explicitly promotes accelerated scope into M2
→ Formal PM M2 Acceptance Mode = ACCELERATED_FEATURE_COMPLETE_ALPHA
```

Until that evidence exists, incomplete Listener/Warden/Profile/AED does not by itself fail the **official** M2 Prototype gate, but it does fail the **accelerated** Feature-Complete Alpha gate.

---

# 2. Purpose

The purpose is to make implementation start immediately without another architecture pass.

Every implementation item must state:

```text
what to implement
where to integrate it
what it depends on
which canonical contract owns the behavior
which tests prove it
which evidence closes it
which values may remain tuning TBD
what blocks the accelerated M2 Feature-Complete Alpha target
```

The plan favors surgical migration of working code over full rewrites. When current code conflicts with a locked contract, the code is classified as an implementation gap/migration target rather than making the contract conform to the prototype.

---

# 3. M2 Goal and Delivery-Scope Variance

## 3.1 Requested delivery goal

```text
Implementation Plan Target Scope Mode:
ACCELERATED_FEATURE_COMPLETE_ALPHA
→ AI / Telemetry / Profile / AED / Research infrastructure
```

The intent is to move most core AI/research implementation into M2 so later milestones are primarily:

- bug fixing;
- integration stabilization;
- playtest;
- balance/tuning;
- profiling;
- Fixed-vs-Adaptive pilot/main experiment;
- research analysis;
- polish/documentation.

## 3.2 Official planning evidence

The current revised project plan defines:

```text
M2 Prototype: 2026-08-25 → 2026-09-20
→ Research Facility prototype
→ Stalker
→ 2–4 player multiplayer
→ Core/Puzzle/Security Hold/Final Hunt/Down-Revive
→ basic telemetry/support

M4 Beta: 2026-09-26 → 2026-10-31
→ feature-complete P0
→ 3 Monsters
→ Player/Team Modeling
→ AED
→ full backend/system scope

M5 Beta Improve: 2026-11-01 → 2026-12-01
→ Fixed-vs-Adaptive experiment/playtest
→ balance/performance/research evidence
```

Therefore the requested `ACCELERATED_FEATURE_COMPLETE_ALPHA` target is a **delivery-scope acceleration** relative to the official baseline.

Classification:

```text
AI Architecture Escalation Required: NO
Project-Management Rebaseline Required: YES
```

This plan still targets the requested broader M2 scope, but no schedule-feasibility claim is made without team velocity/capacity evidence. If the broader target is made a formal milestone acceptance gate, the Project Plan/PM baseline must be explicitly rebaselined rather than silently treating M4 work as already committed to M2.

---

# 4. Scope / Non-Goals

## 4.1 In scope

- reconcile current repository with canonical contracts;
- finish/migrate Stalker;
- implement Listener;
- implement Warden;
- map/spatial authoring required by Stalker and Warden;
- Host/Fusion authoritative AI integration;
- Telemetry v1.1 end-to-end;
- Player/Profile processing and `AdaptiveInputSnapshot`;
- ScenarioConfig foundation, FixedDirector, ScenarioValidator;
- AED v1.1;
- research/experiment instrumentation needed for later pilot/readiness;
- automated tests, evidence, observability, profiling hooks.

## 4.2 Out of scope / not to redesign

- no new Stalker/Listener/Warden design;
- no generic Monster FSM replacing monster-specific semantics;
- no new Telemetry wire schema;
- no new Profile formulas;
- no new AED policy;
- no runtime ML/RL/GenAI gameplay decisions;
- no MAIN Fixed-vs-Adaptive result or statistical analysis;
- no invented final balance numbers;
- no persistent gameplay Team identity;
- no client-authoritative Monster/AED behavior.

---

# 5. Canonical Source Register

| Area | Canonical Source | Semantic Version / ID | Status | Supersedes / Notes |
|---|---|---|---|---|
| AI architecture | `AI_Architecture_v1.1.md` | v1.1 | BASELINED | parent authority |
| Stalker | `Stalker_AI_Design_v1.1.md` | v1.1 | BASELINED / LOCKED | supersedes M1-013 semantics where changed |
| Listener | `Listener_AI_Design_v1.0.md` | v1.0 | BASELINED / LOCKED | current Listener detailed owner |
| Warden | `Warden_AI_Design_v1.0.md` | v1.0 | BASELINED / LOCKED | current Warden detailed owner |
| Telemetry | `Telemetry_Contract_v1.1.md` | wire `"1.1"` | BASELINED / LOCKED | legacy wire `"1.0"` remains frozen compatibility path |
| Profile | `Player_Team_Profile_Contract_v1.1.md` | v1.1 / `PROFILE_FORMULA_V1_1` | BASELINED / LOCKED | M1-014 predecessor is historical only |
| AED/Scenario | `AED_ScenarioConfig_Contract_v1.1.md` | `AED_SCENARIO_POLICY_V1_1` | BASELINED / LOCKED | M1-015 predecessor is historical only |
| Experiment | `Fixed_vs_Adaptive_Experiment_Contract_v1.1.md` | `FIXED_ADAPTIVE_EXPERIMENT_PROTOCOL_V1_1` | BASELINED / LOCKED | M1-020 predecessor is historical only |
| Gameplay | repo `docs/ECHO_PROTO.md` + revised approved scope/spec | current approved baseline | APPROVED | gameplay source-of-truth precedence per project docs |
| Project scope | `docs/01_ECHO_PROTOCOL_Project_Scope_REVISED.md` | baseline 2026-08-19 | APPROVED | M2/M4 scope boundary |
| System architecture | `docs/02_ECHO_PROTOCOL_System_Architecture_REVISED.md` | revised | APPROVED | implementation context |
| Implementation spec | `docs/03_ECHO_PROTOCOL_Implementation_Spec_REVISED.md` | revised | APPROVED | gameplay/system binding context |
| PM baseline | `docs/04_ECHO_PROTOCOL_Project_Management_Baseline_REVISED.md` | revised | APPROVED | ownership/scope context |
| Project plan | `docs/05_ECHO_PROTOCOL_Project_Plan_4P_2026_REVISED.md` | revised | APPROVED | official dates/work sequencing |
| Current code | source archive commit reference above | current evidence snapshot | EVIDENCE ONLY | does not override locked behavior |

Historical duplicate filenames such as `(1)`, `(2)`, `(3)` are not semantic versions and cannot override this register.

---

# 6. Environment Verification

## 6.1 Verified from repository

| Item | Evidence | Result |
|---|---|---|
| Unity | `KLTN/ProjectSettings/ProjectVersion.txt` | `6000.5.8f1 (5cb7df797b7d)` |
| Fusion | `KLTN/Assets/Photon/Fusion/build_info.txt` | `2.1.1 Stable 2177` |
| AI Navigation | `KLTN/Packages/manifest.json` | `com.unity.ai.navigation 2.0.14` |
| Unity Test Framework | package manifest | `1.7.0` package present |
| Input System | package manifest | `1.20.0` |
| URP | package manifest | `17.5.0` |
| Network topology evidence | `Assets/Scripts/Networking/LobbyManager.cs` | uses `GameMode.Host` / `GameMode.Client` |
| Fusion runner asset | `Assets/Prefabs/NetworkRunner.prefab` | NetworkRunner + default scene/object providers |
| Backend | `EchoProtocol.Api.csproj` | ASP.NET Core `net8.0`, EF Core/PostgreSQL/JWT |

No environment drift was found relative to the current approved Unity/Fusion binding.

## 6.2 Review-toolchain limitation

In the review container:

```text
dotnet: NOT FOUND
unity-editor: NOT FOUND
unity: NOT FOUND
Unity: NOT FOUND
```

Therefore:

```text
Unity compile/test execution: NOT EXECUTED
Backend dotnet build/test: NOT EXECUTED
Reason: TOOLCHAIN UNAVAILABLE IN REVIEW ENVIRONMENT
```

This is not a PASS or FAIL claim about the project build.

---

# 7. Architecture Boundaries

## 7.1 Runtime AI

```text
Host / Fusion State Authority
→ authoritative Monster simulation
→ authoritative gameplay side effects
```

Proxies render replicated state/presentation and do not independently author Monster FSM, target, attack outcome, Listener hearing decision, Warden lock, AED decision, or ScenarioConfig mutation.

## 7.2 Telemetry

```text
Authoritative Gameplay Fact
→ Telemetry Adapter
→ immutable TelemetryEvent
→ bounded buffer/batch
→ Backend Validation
→ idempotent raw storage
→ completeness/processed evidence
→ Profile
→ AED
```

Never:

```text
Telemetry → Monster FSM
Telemetry → Listener hearing
Telemetry → direct ScenarioConfig mutation
```

## 7.3 Listener runtime noise

```text
RuntimeNoiseEvent != TelemetryEvent
```

Canonical split:

```text
authoritative gameplay action
→ RuntimeNoiseEvent
   ├→ NoiseSystem → Listener Hearing
   └→ NoiseTelemetryAdapter → NOISE_EMITTED
```

## 7.4 Profile / AED / Experiment

```text
Profile
→ immutable AdaptiveInputSnapshot
→ AEDInputGate
→ deterministic AED policy
→ CandidateScenarioConfig
→ ScenarioValidator
→ Host-authoritative apply
```

Experiment assignment/evidence is research state only and does not command gameplay.

---

# 8. Repository Audit Method

The audit used static repository inspection of:

- `KLTN/Assets/Scripts`;
- user scenes under `KLTN/Assets/Scenes`;
- user prefabs/assets excluding Photon package internals;
- `ProjectSettings` and `Packages`;
- Fusion imported package/build information;
- `EchoProtocol.Backend/src` and backend test directory;
- revised project docs;
- canonical/baselined contracts in the supplied workspace.

Checks included:

- file inventory;
- namespace/component search;
- `NetworkBehaviour`, `[Networked]`, `FixedUpdateNetwork`, authority marker search;
- scene serialization and build settings;
- ScriptableObject/config/prefab inventory;
- backend controller/entity/service inventory;
- automated-test and CI inventory;
- comparison against canonical logical components.

Static inspection cannot prove runtime behavior. All code-only findings remain `IMPLEMENTED_UNVERIFIED`, `PARTIAL`, or `MIGRATION_REQUIRED` until executable evidence exists.

---

# 9. Current Implementation Evidence Audit

| Area / Expected Contract | Evidence Found | Actual Path | Status | Gap / Required Action |
|---|---|---|---|---|
| Unity/Fusion project foundation | Unity 6000.5.8f1; Fusion 2.1.1/2177 imported | `KLTN/ProjectSettings`, `Assets/Photon/Fusion` | IMPLEMENTED_UNVERIFIED | execute clean build and network smoke |
| Room Host/Client foundation | `GameMode.Host` / `GameMode.Client` | `Assets/Scripts/Networking/LobbyManager.cs` | PARTIAL | no authoritative gameplay spawn/state path evidenced |
| Durable gameplay NetworkBehaviour layer | no project-owned `NetworkBehaviour`, `[Networked]`, `FixedUpdateNetwork` found | custom scripts | NOT_STARTED | implement player/Monster/config authority bindings |
| Stalker six-state enum | exact six states present | `Assets/Scripts/AI/Stalker/StalkerState.cs` | IMPLEMENTED_UNVERIFIED | tests not present/executed |
| Stalker basic FSM spike | PATROL/DETECT/CHASE/ATTACK/RECOVER/SEARCH in one MonoBehaviour `Update()` | `StalkerController.cs` | MIGRATION_REQUIRED | migrate to canonical memory/FSM/planner/action/authority boundaries |
| Stalker vision | distance/FOV/LOS against one serialized `candidate` Transform | `StalkerVisionSensor.cs` | MIGRATION_REQUIRED | multi-player eligibility + typed Observation + target separation |
| Detection meter | fill/decay and detection target present | `StalkerController.cs` | PARTIAL | canonical typed memory/authority/tests missing |
| Stalker LKP | updated from visible observation | `StalkerController.cs` | PARTIAL | immutable search context + direction + explicit memory ownership missing |
| Stalker SEARCH | move to LKP + timer | `StalkerController.cs` | MIGRATION_REQUIRED | canonical SearchEpisode/candidates/filter/scoring/outcome absent |
| Stalker attack | windup then Hit/Miss then RECOVER | `StalkerController.cs` | MIGRATION_REQUIRED | no AttackEpisode identity / exactly-once guard / Life-State damage integration |
| Stalker navigation | thin NavMeshAgent destination wrapper | `StalkerNavigationController.cs` | MIGRATION_REQUIRED | path result/progress/repath/stuck/recovery contract missing |
| SpatialGraph builder | NavMesh triangulation graph | `AI/Stalker/Spatial/*` | IMPLEMENTED_UNVERIFIED | tests/bake compatibility not evidenced |
| Dynamic node patrol | SpatialPatrolPlanner + memory wired in DynamicSpatial mode | `StalkerController.cs`, `SpatialPatrolPlanner.cs` | PARTIAL | not canonical RegionGraph global/local coverage design |
| Confidence patrol prototype | planner + coverage memory code exists | `ConfidenceSpatialPatrolPlanner.cs`, `SpatialCoverageMemory.cs` | IMPLEMENTED_UNVERIFIED | not referenced/wired |
| `ConfidenceSpatial` mode | enum exists | `StalkerPatrolMode.cs` | MIGRATION_REQUIRED | controller does not handle it; falls to fixed route |
| RegionDefinition / RegionGraph | no implementation/assets found | repository search | NOT_STARTED | implement authoring + mapping + compatibility |
| Stalker AI scene | spike + baked NavMesh + graph debug | `Assets/Scenes/AI/AI_Stalker_SpatialV2.unity` | PARTIAL | scene lacks serialized current patrol-mode fields; not in Build Settings |
| Gameplay scene AI integration | `Game.unity` contains no evidenced gameplay/AI logic | `Assets/Scenes/Game.unity` | NOT_STARTED | cross-team integration dependency |
| Player runtime | local CharacterController movement only in SampleScene | `Assets/Scripts/Player/PlayerMovement.cs` | PARTIAL | no Fusion player object/life-state/action authority evidenced |
| Listener | no Listener runtime source found | repository search | NOT_STARTED | implement full v1.0 contract |
| RuntimeNoiseEvent / NoiseSystem | no project source found | repository search | NOT_STARTED | prerequisite for Listener + NOISE telemetry |
| Warden | no Warden source/assets found | repository search | NOT_STARTED | implement FacilityGraph/safety/runtime |
| FacilityGraph/Warden lock authoring | none found | repository search | NOT_STARTED | MAP_BINDING_REQUIRED |
| Telemetry Unity runtime | no Telemetry source found | repository search | NOT_STARTED | implement v1.1 producer/buffer/transport |
| Telemetry backend | no telemetry controller/entity/storage | `EchoProtocol.Backend/src/EchoProtocol.Api` | NOT_STARTED | implement validator/idempotent raw storage |
| Backend foundation | Auth/health/User/Wallet/account PlayerProfile | backend source | IMPLEMENTED_UNVERIFIED | useful platform base, not AI Profile |
| Canonical PlayerAIProfile | no implementation found | backend search | NOT_STARTED | do not reuse account `PlayerProfile` semantics |
| ScenarioConfig/FixedDirector/Validator | no implementation found | repository search | NOT_STARTED | implement fixed path first |
| AED | no AED source found | repository search | NOT_STARTED | depends on Profile + Scenario foundation |
| Experiment infra | no experiment source found | repository search | NOT_STARTED | implement instrumentation, not MAIN analysis |
| Project-owned Unity tests | none found | repository scan | NOT_STARTED | create test assemblies/suites |
| Backend test project | none; `tests/.gitkeep` only | backend | NOT_STARTED | create test project |
| Project CI | none found | repository scan | NOT_STARTED | add minimal compile/test automation |

## 9.1 Scene/config evidence notes

- Build Settings includes `Bootstrap`, `Login`, `MainMenu`, `Lobby`, `Game`, `Result` only.
- AI spike scenes are not in current Build Settings.
- `AI_Stalker_SpatialV2.unity` stores graph-debug values consistent with a 34-node/36-edge/1-component graph snapshot, but this is serialized debug evidence, not runtime test proof.
- User prefabs excluding Photon package assets: only `Assets/Prefabs/NetworkRunner.prefab` was found.
- No Monster prefab, RegionDefinition asset, FacilityGraph asset, WardenRouteLockDefinition asset, Telemetry config, Profile config, AED config, or Experiment config was found.

---

# 10. Current Build / Test Baseline

| Baseline | Evidence | Status |
|---|---|---|
| Repository source can be statically enumerated | yes | OBSERVED |
| Unity Editor build | not executable in review environment | NOT EXECUTED |
| Unity EditMode tests | no project suite found; Editor unavailable | NOT EXECUTED |
| Unity PlayMode tests | no project suite found; Editor unavailable | NOT EXECUTED |
| Fusion multiplayer automated tests | no project suite/harness found | NOT EXECUTED |
| Backend build | `dotnet` unavailable | NOT EXECUTED |
| Backend tests | no test project + `dotnet` unavailable | NOT EXECUTED |
| CI | no project workflow found | NOT_STARTED |

Wave 0 must establish a **green executable baseline** on a machine with Unity 6000.5.8f1 and .NET 8 before deep migration. If baseline compile fails, feature work pauses until failures are classified as existing defect vs migration change.

---

# 11. Status Vocabulary

Only these implementation statuses are used:

| Status | Meaning |
|---|---|
| `NOT_STARTED` | no implementation evidence found |
| `PARTIAL` | some contract behavior exists but material required behavior is absent |
| `IMPLEMENTED_UNVERIFIED` | code/asset exists and appears relevant, but required executable evidence is absent |
| `VERIFIED` | implementation evidence + required passing tests/evidence are supplied |
| `MIGRATION_REQUIRED` | existing code conflicts with or predates the canonical contract and must be adapted/replaced |
| `BLOCKED` | implementation cannot proceed until a named dependency is supplied |
| `DEFERRED` | deliberately not in the current implementation target |
| `NOT_APPLICABLE` | contract item does not apply to this component |

No current AI/data subsystem is marked `VERIFIED` in this plan because no required automated test output was supplied/executable in the review environment.

---

# 12. M2 Acceptance Definition

This section defines the **accelerated** `ACCELERATED_FEATURE_COMPLETE_ALPHA` gate. It does not replace the current formal `OFFICIAL_BASELINE` Project Plan gate unless PM rebaseline is approved.

The accelerated Feature-Complete Alpha is not “all tuning final.” It means the canonical functional architecture is implemented and integration-testable.

Required dimensions:

```text
Functional Complete
Network Complete
Data Complete
Test Verified
Map Binding Complete
Observability Complete
Research Infrastructure Complete
Known-Tuning-TBD explicitly registered
```

Hard blockers include:

- project cannot build on approved toolchain;
- any canonical Monster core path missing;
- client-authoritative AI/config mutation;
- hidden-player cheat;
- attack duplicate authoritative side effect possible;
- Listener hearing coupled to telemetry storage;
- Warden can soft-lock required objective/Exit route;
- Telemetry event identity/order/idempotency broken;
- Profile correction/retraction/replay missing;
- Fixed path not operational;
- AED can bypass ScenarioValidator;
- ScenarioConfig authoritative replication invalid;
- required critical automated tests absent/failing;
- map bindings required by Stalker/Warden absent.

Balance-only values may remain `TUNING_AFTER_EVIDENCE` when safe designer-authored defaults/configs are permitted by the owning contract.

---

# 13. Dependency DAG

## 13.1 Dependency Semantics — M2-PLAN-01

Every work package uses three distinct dependency levels:

```text
START_DEPENDENCIES
= minimum contracts/interfaces/fixtures/toolchain needed to begin safely

INTEGRATION_DEPENDENCIES
= real upstream runtime/data/content needed to connect the implementation

ACCEPTANCE_DEPENDENCIES
= integration + canonical test/evidence/content required before DONE/VERIFIED
```

`HARD_BLOCKER` is reserved for a condition that genuinely prevents safe implementation start, such as an unavailable approved toolchain or a missing mandatory semantic contract/interface that would otherwise force invention. Live production data, final content, or an unwired upstream runtime is **not** a start blocker when canonical fixtures/interfaces are sufficient.

Canonical fixture-first DAG:

```text
                              ┌─ Profile pure/ledger/snapshot code (contract fixtures) ─┐
                              │                                                          ├─ real Telemetry integration
Toolchain usable ─────────────┼─ AED gate/policy/registry pure code (snapshot fixtures) ┤        ↓
      │                       │                                                          ├─ live AdaptiveInputSnapshot
      ├─ FND-001 build baseline ─────┐                                                   │        ↓
      ├─ TST-001 smoke harness ──────┤                                                   └─ AED live integration
      └─ TST-003 backend smoke ──────┘
                  │
                  ├─ shared IDs/authority/navigation foundations
                  │      ├─ Stalker migration ────────────────┐
                  │      ├─ RuntimeNoise → Listener runtime ──┤
                  │      └─ Warden pure graph/safety ─────────┤─ production map/door/objective integration
                  │
                  ├─ Telemetry runtime/backend ───────────────→ Profile live acceptance
                  │
                  └─ ScenarioConfig/Fixed/Validator pure ─────→ Host apply ─→ AED live apply

Experiment RunManifest / assignment / enrollment pure backend work
can start from Experiment v1.1 fixtures before live Telemetry/AED evidence;
production acceptance waits for real provenance/evidence integration.
```

Important non-dependencies:

- Listener does not wait for Stalker feature completion; it waits for the shared interfaces it actually consumes plus RuntimeNoise integration.
- Warden pure graph/reachability/RoutePressure/candidate policy does not wait for final production map assets; production acceptance does.
- Telemetry can progress in parallel with Monster implementation after event ownership/interfaces are stable.
- Profile pure processing/backend components can start against canonical MatchTelemetry fixtures before live Telemetry v1.1 integration.
- AED PolicyDefinition/rules/registry code can start against canonical AdaptiveInputSnapshot/Scenario fixtures before live Profile integration.
- ScenarioValidator pure rules can start with authored fixtures; production route/spawn acceptance waits for real map/objective/FacilityGraph adapters.
- Experiment manifest/readiness/assignment/enrollment logic can start from contract fixtures; production acceptance waits for real experiment provenance and AED evidence.

---

# 14. Critical Path

The plan has **acceptance critical chains**, not one false serial implementation chain.

| Chain / Node | START dependency | Production / Acceptance dependency | Why Critical | Parallel Opportunity |
|---|---|---|---|---|
| Toolchain/build baseline | approved Unity/.NET toolchain | Unity compile + backend build evidence | executable evidence floor | TST-001/TST-003 bootstrap can overlap after projects open |
| Minimum test bootstrap | importable Unity/backend projects + contracts | runnable smoke tests | prevents code-presence-only acceptance | component pure tests authored alongside implementation |
| Host gameplay/network foundation | Fusion package + authority contracts/interfaces | real Player/Monster network entities + 2–4P harness | authoritative side effects/config apply | backend/graph/policy pure work |
| Stalker production integration | Stalker contract + current spike | production target/life-state/map/Fusion bindings | official M2 Monster path | Listener/Warden/data pure work |
| Listener production integration | Listener contract + RuntimeNoise fixtures | gameplay noise hooks + authority/nav/life-state | accelerated target | Stalker/Warden/Telemetry |
| Warden production integration | Warden contract + graph fixtures | FacilityGraph/DoorId/objective/Exit bindings + Fusion | route safety | pure Warden graph/safety can start early |
| Telemetry live chain | Telemetry v1.1 contract + fixtures | authoritative producers → transport → accepted storage/completeness | live Profile evidence | Profile pure work starts earlier |
| Profile live chain | Profile v1.1 contract + MatchTelemetry fixtures | real accepted MatchTelemetry + persistence/replay evidence | live AdaptiveInputSnapshot | pure scoring/ledger/snapshot code starts earlier |
| Fixed/Scenario live chain | Scenario contract + map/config fixtures | fixed baseline + route/spawn adapters + Host apply | safe independent Fixed path | validator pure code starts early |
| AED live chain | AED contract + policy/snapshot fixtures | live snapshot + Fixed/Validator + Host apply | adaptive execution | PolicyDefinition/config/rule tests start early |
| Experiment integration | Experiment contract + persistence fixtures | real MATCH_STARTED provenance + AED decision evidence | research-alpha handoff | manifest/assignment/enrollment pure work starts early |

Production acceptance chains that remain genuinely ordered include:

```text
Telemetry live integration
→ Profile live integration
→ AdaptiveInputSnapshot live
→ AED live integration
```

and:

```text
ScenarioConfig
→ FixedDirector
→ ScenarioValidator production adapters
→ Host authoritative apply
→ AED live application
```

The pure/domain portions of downstream modules are not forced to wait for those live chains.

---

# 15. Parallel Workstreams

Recommended concurrency if ownership capacity exists:

| Track | Can Start With | Real Integration Needed Later | Acceptance Evidence |
|---|---|---|---|
| A — Stalker | canonical contract + current spike + fixtures | Player identity/Life-State, map regions, Fusion | STK canonical tests + 2–4P |
| B — RuntimeNoise/Listener | Listener contract + RuntimeNoise/noise fixtures | gameplay noise hooks, nav, Life-State, Fusion | LIS full test ranges + runtime integration |
| C — Warden | FacilityGraph/route fixtures | production doors/objective/Exit/FacilityGraph | WAR tests + production map binding |
| D — Telemetry | Telemetry contract/event fixtures | authoritative owner hooks + backend storage | TEL contract tests + real event evidence |
| E — Profile | MatchTelemetry/Profile fixtures | accepted Telemetry/completeness + persistence | PRO tests + live replay/staleness evidence |
| F — Scenario/Fixed/Validator | Scenario/map fixtures | real map/objective/route adapters + Host apply | fixed/validator/apply evidence |
| G — AED | snapshot/scenario fixtures + locked policy | live AdaptiveInputSnapshot + Scenario apply | AED full ranges + E2E |
| H — Test/CI/observability | usable toolchain + contracts | component runtime as it appears | executable suites/CI evidence |
| I — Experiment infra | Experiment fixtures + test persistence | MATCH_STARTED provenance + AED evidence | assignment/enrollment/eligibility integration |

If one developer owns multiple tracks, this table defines safe context-switch boundaries; it does not imply fictional staffing.

## 15.1 Cross-Team / External Dependency Register

| Dependency | Owning Domain | AI/Data Consumer | Needed To Start? | Needed To Integrate? | Needed For Acceptance? | Current Evidence |
|---|---|---|---|---|---|---|
| Player NetworkObject / authoritative Player identity | Multiplayer/Networking | FND, Stalker, Listener, Telemetry | No — narrow approved interface/fixture is sufficient for pure work | YES | YES | Fusion lobby/room foundation exists; project-owned Monster gameplay NetworkBehaviour authority is not evidenced |
| Player Life-State / Down-Revive API | Gameplay + Networking | Stalker/Listener attack adapters, Telemetry | No — AttackEpisode/idempotency can use a narrow contract fixture | YES | YES | contract/work-plan dependency exists; current authoritative integration not evidenced |
| Research Facility production map integration | Gameplay/Map | Stalker RegionGraph, Warden, ScenarioValidator | No for pure graph/validator code | YES | YES | current source audit does not evidence final Region/Facility bindings |
| Door definitions / Door Jammer capability | Gameplay/Map | Warden, ScenarioValidator | No for pure route fixtures | YES | YES | production DoorId/Jammer authoring not evidenced |
| objective / Exit bindings | Gameplay/Map | Warden safety, ScenarioValidator | No for pure safety fixtures | YES | YES | production obligations/bindings not evidenced |
| gameplay noise-emission hooks | Gameplay | RuntimeNoise/Listener, Telemetry noise adapter | No for pure resolver/hearing tests | YES | YES | RuntimeNoise/NoiseSystem not implemented in snapshot |
| match lifecycle / phase lifecycle | Gameplay/Networking | Telemetry, Profile, AED, Experiment | No for pure data-policy fixtures | YES | YES | full authoritative lifecycle integration not evidenced in current snapshot |

No team-member names are invented here.

### Life-State dependency rule

```text
START:
approved attack/life-state contract or narrow interface fixture

INTEGRATION:
real authoritative Player Life-State implementation

ACCEPTANCE:
2–4P one-hit/one-down exactly-once evidence
```

### Map dependency rule

```text
PURE IMPLEMENTATION:
Warden graph structures/reachability/RoutePressure/candidate policy with fixtures

CONTENT INTEGRATION:
production FacilityGraph / DoorIds / route footprints / objective-Exit obligations

PRODUCTION ACCEPTANCE:
validated production assets + WAR tests + PlayMode/Fusion evidence
```

### ScenarioValidator dependency rule

Pure rules and candidate validation start from fixtures. Only the production route/spawn adapters require actual map/objective/FacilityGraph bindings. `Warden complete → ScenarioValidator may start` is not a valid dependency.

---

# 16. Wave 0 — Repository Reconciliation / Build Green

`M2-AI-FND-001` and `M2-TST-001` are overlapping bootstrap packages, not a cycle.

Wave 0 completion is an aggregate gate:

```text
FND-001 build/toolchain baseline established
+
TST-001 minimum Unity smoke harness executable
+
TST-003 minimum backend test project/smoke executable
```

Exit Wave 0 only when:

- the exact source snapshot/branch is recorded;
- Unity 6000.5.8f1 opens/compiles on an approved machine;
- backend .NET 8 restores/builds;
- existing scenes/assets import without blocker errors;
- current test inventory is recorded;
- Unity EditMode/PlayMode scaffolding can run at least one smoke test;
- backend minimum test project/smoke can execute;
- current warnings/errors are captured;
- canonical source register is linked in PR templates/issues;
- no duplicate/legacy document is used as active semantic authority.

FND-001 does **not** require TST-001 to be DONE. TST-001 may begin once the approved Unity toolchain can open/import the project sufficiently to create and execute test assemblies.

Do not use Wave 0 to redesign AI.

---

# 17. Shared AI Foundation

Implement only abstractions genuinely shared by contracts:

- stable `PlayerId`, `MonsterId`, episode/action/observation IDs or existing equivalents;
- authoritative simulation tick/time access;
- deterministic stable ordering/tie-break helpers;
- State Authority guard boundary;
- target eligibility registry/adapters shared by vision-capable Monsters where semantics match;
- navigation request/result/progress/repath/stuck primitives that do not own Monster decisions;
- bounded history/dedup utilities only when lifecycle semantics match;
- debug/evidence interfaces;
- map physical ID types reusable by Stalker/Warden without merging their graphs.

Forbidden:

```text
giant MonsterFSM
shared blackboard containing all Monster memory
one universal perception model
one graph type that erases RegionGraph vs FacilityGraph semantics
```

---

# 18. Map / Spatial Authoring

## 18.1 Stalker

Required:

- `RegionDefinition` representation;
- final Research Facility patrol regions;
- deterministic SpatialGraph→Region mapping;
- graph/map/region compatibility version;
- authoring validator/bake;
- RegionGraph connectivity validation;
- debug view of RegionId/node binding.

## 18.2 Warden

Required:

- stable `GameplayZoneId`, `RegionId`, `DoorId` where approved;
- FacilityGraphDefinition;
- directed FacilityEdge definitions;
- canonical DoorDefinition binding;
- WardenRouteLockDefinition with complete `AffectedPlayerRouteEdgeIds[]`;
- RequiredRouteObligations for objective/Exit;
- Warden-authorized lock points;
- Door Jammer capability binding;
- map/objective/Exit validation.

Until these bindings exist, Warden status is `MAP_BINDING_REQUIRED`; visible door locking must not be implemented as an unsafe shortcut.

---

# 19. Stalker Implementation

Current Stalker is a valuable spike but is not contract-complete.

Migration sequence:

1. preserve the exact six-state semantic enum;
2. split physical vision observation from target eligibility/selection;
3. replace the single serialized candidate with authoritative candidate enumeration/eligibility;
4. move DetectionTarget/CurrentTarget/LKP/LastSeenDirection/SearchEpisode/AttackEpisode ownership into typed memory/context;
5. keep DetectionTarget lock semantics and no meter carry;
6. implement RegionGraph + CoverageMemory global/local patrol planning rather than shipping node-only prototype as final design;
7. implement canonical SEARCH candidate generation/filter/scoring/terminal outcome with no hidden transform follow;
8. implement navigation progress/repath/stuck/recovery;
9. add AttackEpisode identity + `HitMomentResolved` exactly-once guard;
10. route attack consequence through authoritative Player Life-State owner;
11. bind authoritative simulation to Fusion Host/State Authority;
12. replicate only durable gameplay/presentation state required by clients/late join;
13. add telemetry adapters and debug evidence;
14. execute canonical `STK-E-*`, `STK-P-*`, `STK-N-*` tests.

Do not delete the working spatial graph spike until the RegionGraph replacement/migration path is tested.

---

# 20. Listener Implementation

Implement the locked five-state Listener from zero against the current contract:

```text
ROAM → INVESTIGATE → CHASE → ATTACK → RECOVER
```

Required ordered foundation:

1. authoritative NoiseEmissionResolver;
2. immutable RuntimeNoiseEvent identity + bounded dedup/expiry;
3. NoiseSystem query/propagation;
4. HearingSensor + occlusion/attenuation;
5. immutable HearingObservation using geometry at source evaluation time;
6. bounded PendingHearingInbox owned by ListenerMemory;
7. deterministic selection/disposition reasons;
8. InvestigationEpisode lifecycle/merge/interruption/terminal cleanup;
9. weak vision confirmation and CHASE corroboration guard;
10. deterministic same-tick transition arbitration with one semantic transition per decision step;
11. planner/navigation/action;
12. Listener AttackEpisode resolution without importing Stalker Hit Moment mechanics;
13. RECOVER;
14. Host/Fusion authority and late-join presentation state;
15. telemetry adapters/metrics/debug;
16. canonical `LIS-E-*`, `LIS-P-*`, `LIS-N-*` tests.

Runtime Noise and analytics Telemetry remain separate branches from the same authoritative gameplay action.

---

# 21. Warden Implementation

Warden remains a Spatial Pressure Controller.

Implementation order is safety-first:

```text
FacilityGraph authoring/runtime
→ RequiredRouteTargetResolver / obligations
→ reachability + shortest-route evaluator
→ WardenSafetyValidator
→ RoutePressure_v1.0
→ full-set candidate generation
→ candidate-bound/config validation
→ deterministic policy/history/cooldown
→ telegraph
→ precommit revalidation
→ atomic full-footprint apply
→ post-apply verification
→ release / fail-safe
→ Fusion presentation/state
```

Hard requirements:

- one DoorId maps to zero/one canonical WardenRouteLockDefinition;
- bidirectional doors include all affected directed player-route edges;
- simulation/apply/release/post-validate footprint equality;
- `PressureCandidateBound >= AuthoredCheapEligibleWardenRouteLockDefinitionCount`;
- no silent truncation/window cursor;
- zero-pressure candidate not selected;
- one active Warden lock;
- Door Jammer overlay remains independent;
- objective/Exit reachability must survive every Warden-owned lock;
- AED never chooses Warden current DoorId;
- canonical `WAR-E-*`, `WAR-P-*`, `WAR-N-*` tests.

---

# 22. Multiplayer / Fusion Integration

Current networking is room/session foundation only. M2 requires a gameplay authority layer.

Minimum implementation:

- authoritative Player network entity/input/state required by Monster target/life-state contracts;
- Monster network entity/controller wrapper with Host State Authority guard;
- authoritative AI tick driven in Fusion simulation context where contract-relevant;
- durable replicated Monster semantic state/presentation fields only;
- authoritative attack side-effect path;
- ScenarioConfig durable applied state;
- networked door/Warden presentation where required;
- late join reconstructs current state without replaying historical side effects;
- duplicate RPC/reconnect cannot duplicate attack/config/door side effects;
- 2/3/4 player validation.

Do not move pure deterministic planning logic into network-specific classes unless needed; prefer pure services called from a thin authoritative NetworkBehaviour boundary.

---

# 23. Telemetry v1.1

Implement without changing wire semantics:

```text
Authoritative occurrence
→ source duplicate guard
→ global match-wide eventSequence allocation
→ immutable TelemetryEvent
→ bounded Host/client-side buffer as contracted
→ batch/retry
→ per-event acknowledgement
→ backend schema-version routing
→ semantic validation
→ idempotent immutable raw storage
→ sequence/completeness evaluation
→ late-event recomputation
```

Required integration categories:

- match/phase/objective;
- player life-state;
- noise;
- Stalker/Listener research capture;
- Warden telegraph/apply/safety/release;
- Scenario/AED provenance where contract-owned;
- experiment provenance on MATCH_STARTED.

Retry must preserve the logical event ID, timestamp, sequence, payload, and provenance. Unknown strict enum values are permanently rejected; Listener/Warden/attack event semantics must match the corrected v1.1 contract.

---

# 24. Player / Team Profile

Backend implementation path:

```text
accepted Telemetry
→ MatchTelemetry projection
→ Telemetry/Profile integrity gate
→ MatchProfileEligibility
→ MetricAvailability / MetricFinality
→ PlayerMatchScore pure functions
→ Profile semantic/config validation
→ ProfileDimensionObservation ledger
→ dimension-scoped apply/retract/replay
→ PlayerAIProfile
→ match-scoped TeamProfile
```

Only active Player dimensions:

```text
SURVIVAL
NOISE
```

All seven other Player dimensions remain DEFERRED. `TeamPerformance` remains honest:

```text
TeamPerformance.status = INCOMPLETE
TeamPerformance.score = null
```

Do not renormalize missing TeamPerformance components or fabricate Teamwork/ResourceEfficiency merely to unblock AED.

Do not repurpose the existing account entity `Entities/PlayerProfile.cs` as the canonical AI Profile unless a deliberate migration/schema design proves equivalent ownership; current evidence shows it only stores account/display/match-count statistics.

Retraction correction uses canonical replay of remaining CONTRIBUTING observations, never inverse EMA.

---

# 25. AdaptiveInputSnapshot

Implement after Profile persistence semantics are stable:

- immutable PlayerProfileSnapshot with exact revision/semantic provenance;
- per-dimension `ProfileDimensionComparisonKey` resolution;
- RosterProfileSummary with exact compatible-key aggregation;
- no silent dropping of incompatible players;
- VALID/PARTIAL/INVALID snapshot semantics;
- snapshot fingerprint;
- roster identity/current source revision binding;
- source Profile correction/retraction makes old snapshots stale, never mutates them;
- no raw Telemetry or hidden gameplay state in snapshot.

AED live use is blocked until this object can be built and stale-checked deterministically.

---

# 26. ScenarioConfig Foundation

Implement the data/config boundary before AED:

- typed ScenarioConfig matching the v1.1 contract;
- scenario content/version identity;
- `ScenarioConfigBaseRef` semantics;
- current fixed base resolver;
- adaptive parameter registry representation;
- content whitelist registry;
- map/monster/spawn/route/final-hunt field binding;
- current applied config durable representation;
- configSource/policyVersion provenance.

Do not use an untyped dictionary for arbitrary adaptive fields.

---

# 27. FixedDirector

Fixed path must be independently operational before Adaptive readiness.

PRE_MATCH:

```text
resolve FIXED_BASELINE_V1 (or exact current frozen baseline identity)
→ validate full ScenarioConfig
→ Host apply
→ configSource = FIXED
```

Mid-match/Final-Hunt fallback:

```text
KEEP_LAST_VALID_CONFIG
→ no blanket baseline reload
→ no Monster FSM reset
→ no rollback of valid prior Adaptive content
```

If the required PRE_MATCH fixed baseline is absent/invalid, treat it as a configuration blocker rather than inventing values.

---

# 28. ScenarioValidator

Implement as deterministic validation over explicit input/version/current authoritative state.

Must validate at least:

- schema/version;
- requested mode/decision point;
- policy-active key/authority;
- registry/candidate values/bounds;
- timing;
- pressure-axis fairness;
- no double Detection pressure increase;
- max changed-key contract;
- objective spawn whitelist/reachability;
- support budget semantics;
- routeModifier timing and FacilityGraph safety;
- Final Hunt timer bounds/start state;
- base ScenarioConfig revision/fingerprint;
- current decision window;
- no hidden Player data/direct Monster command.

Whole candidate is atomic: one invalid change rejects the candidate.

---

# 29. AED

Implement exactly `AED_SCENARIO_POLICY_V1_1`:

```text
AdaptiveInputSnapshot
→ AEDInputGate
→ AEDEvidencePolicy
→ AEDPolicyDefinition
→ AEDPolicyConfig tuning
→ score bands
→ canonical ordered rule evaluation
→ AdaptiveParameterRegistry
→ NEXT_HIGHER_REGISTERED_VALUE
→ CandidateScenarioConfig
→ ScenarioValidator
→ APPLIED | NO_CHANGE | FIXED_FALLBACK
→ Host-authoritative apply where APPLIED
```

Current PRE_MATCH semantics remain:

- SURVIVAL LOW → RELIEVE → SupportItemBudget + one registered step;
- else NOISE LOW → RELIEVE → SupportItemBudget + one registered step;
- SURVIVAL HIGH + NOISE HIGH + Stalker → INCREASE_PRESSURE → ChaseSpeed + one registered step;
- remaining combinations → HOLD/NO_CHANGE;
- boundary and Final-Hunt current policy → HOLD/NO_CHANGE.

Corrected ownership to preserve:

- sample/evidence thresholds owned only by AEDEvidencePolicy/evidencePolicyVersion;
- rule topology/priority/active keys/strategy bindings owned by policy semantic definition/policyVersion;
- score-band numbers owned by policyConfigVersion;
- PRE_MATCH CAS compares exact authoritative base identity/fingerprint; MID_MATCH base is current AppliedScenarioConfig;
- registry/config invalid before candidate → NOT_EVALUATED; valid registry + invalid constructed candidate → INVALID.

---

# 30. Experiment Instrumentation

M2 research infrastructure must support later pilot/readiness without pretending MAIN experiment is ready.

Core instrumentation target:

- ExperimentRunManifest model/storage;
- ExperimentReadinessEvaluator skeleton/gates;
- balanced sequence schedule representation/generator contract;
- immutable ExperimentAssignmentLedger;
- MainParticipantEnrollmentIndex;
- atomic MainPairEnrollmentCoordinator;
- separate ExperimentPeriodBindingLedger;
- MATCH_STARTED condition/protocol provenance;
- AdaptiveFallbackExperimentClassifier;
- ExperimentMatchRecord;
- ExperimentEligibilityEvaluator;
- ExperimentPairBuilder;
- `ExperimentTeamSurvival_v1.1` extractor;
- AdaptiveExposureClassifier;
- deterministic AnalysisDatasetBuilder and late-invalidation rebuild if capacity permits;
- ExperimentReproductionVerifier if capacity permits.

Atomic enrollment invariant:

```text
immutable AssignmentRecord
+ schedule slot CONSUMED/ownership
+ complete participant membership
= one idempotent logical atomic commit
```

Period→match binding remains separate.

Not an M2 Feature-Complete-Alpha blocker unless explicitly promoted by PM scope:

- final `analysisPlanVersion`;
- final sample size;
- MAIN `plannedPairSlots`;
- MAIN random allocation schedule;
- experiment results.

---

# 31. GenAI / Low-Priority Work

GenAI Mission Briefing remains presentation-only with no gameplay authority.

M2 classification:

```text
M2_STRETCH / P3
```

unless a formal M2 scope change explicitly promotes it.

Do not let GenAI implementation delay Monster runtime, Telemetry, Profile, Fixed path, AED, or critical tests.

---

# 32. Observability

Minimum read-only evidence surfaces:

- **Stalker:** state, DetectionTarget/CurrentTarget IDs, meter, LKP/direction, SearchEpisode, region/node, planner result, nav failure/recovery, AttackEpisode/HitMoment result.
- **Listener:** RuntimeNoise summary, HearingObservation, pending inbox, selection/disposition reason, InvestigationEpisode, transition arbitration result, attack outcome.
- **Warden:** graph revision, route obligations, candidate count, safety result/reason, pressure, current lock footprint, telegraph, release/fail-safe.
- **Telemetry:** buffer depth, eventSequence, retry/ack state, validation/quarantine/completeness.
- **Profile:** source eligibility, dimension observation CONTRIBUTING/RETRACTED, replay/retraction, revision, comparison keys, snapshot validity.
- **AED:** decision ID, snapshot fingerprint, gate, rule, requested delta, base ref, validation status, result/fallback/config version.
- **Experiment:** run/pair/slot/enrollment identity, fallback class, eligibility, dataset version where implemented.

Debug UI must not become an alternate gameplay control path.

---

# 33. Test Strategy

The test pyramid is contract-driven:

```text
Pure/EditMode
→ PlayMode
→ Fusion multiplayer
→ Backend/Data
→ End-to-End
→ manual gameplay validation
```

A work item is not done because it compiles. Critical deterministic semantics require automated tests at the lowest viable layer plus integration tests where Unity/Fusion/DB behavior matters.

Current repository has no project-owned AI/data test suite; creating runnable test infrastructure is itself an M2 required work item.

---

# 34. EditMode / Pure Tests

Prioritize:

- deterministic FSM transition/arbitration;
- target/observation memory transitions;
- Stalker search candidate scoring/filtering;
- graph validation/reachability/RoutePressure;
- Listener pending/dedup/selection lifecycle;
- exactly-once episode guards;
- Telemetry identity/schema/enum validation;
- Profile MatchScore/idempotency/replay/retraction/comparison keys;
- AED evidence/policy/rules/registry/CAS validation;
- experiment assignment/fallback/enrollment/data transformations.

Use canonical test IDs from owning contracts; do not replace them with plan-only IDs.

---

# 35. PlayMode Tests

Required areas:

- NavMesh destination/path behavior;
- Stalker perception/state/search/attack lifecycle;
- RuntimeNoise emission/Listener hearing/investigation;
- Warden telegraph/door overlays/release/fail-safe;
- objective/Exit reachability integration;
- ScenarioConfig application;
- authoritative telemetry producer wiring;
- life-state attack consequence;
- scene/config asset validation.

---

# 36. Fusion Multiplayer Tests

Run Host + 1/2/3 clients for 2–4 Player configurations where contract relevant.

Verify:

- Host owns Monster decisions;
- proxies do not run authoritative AI;
- proxy cannot mutate config/door/attack outcome;
- no duplicate damage after RPC/reconnect/presentation replay;
- current Monster/config/Warden state converges;
- late join receives current durable state without replaying historical side effects;
- disconnect/rejoin behavior follows approved gameplay contract;
- 2/3/4 player target selection remains deterministic.

---

# 37. Backend / Data Tests

Create a real .NET test project and cover:

- Telemetry v1.0/v1.1 routing;
- event enum/payload validation;
- idempotent event storage/sequence conflicts;
- batch partial acknowledgement;
- completeness/late-event behavior;
- Profile metric availability;
- dimension apply/conflict;
- canonical ordered replay/retraction/restoration;
- snapshot semantic-key compatibility;
- AED decision ledger/CAS if backend-owned;
- experiment atomic enrollment if backend-owned;
- dataset deterministic rebuild where implemented.

Current backend `tests/` has only `.gitkeep`; no PASS status is available.

---

# 38. End-to-End Tests

Target chain:

```text
Host authoritative gameplay
→ Monster decision/action
→ authoritative Telemetry event
→ accepted backend raw event
→ Profile processing
→ AdaptiveInputSnapshot
→ AED decision
→ ScenarioValidator
→ Host applied ScenarioConfig
→ next gameplay evidence
```

At least one controlled end-to-end scenario must prove the Fixed path independently and one must prove Adaptive APPLIED/NO_CHANGE/FALLBACK without bypassing authority or validation.

---

# 39. Profiling

No numerical performance budget is invented.

Status:

```text
PERFORMANCE TARGET TBD AFTER BASELINE PROFILE
```

Measure:

| Subsystem | Operation | Tool | Current Evidence | Threshold Owner |
|---|---|---|---|---|
| Stalker | LOS + region/spatial planning | Unity Profiler | none | performance baseline/tuning |
| Listener | noise propagation/hearing/pending inbox | Unity Profiler | none | performance baseline/tuning |
| Warden | reachability/shortest route/full candidate evaluation | Unity Profiler | none | performance baseline/tuning |
| Nav | SetDestination/path/repath frequency | Unity Profiler | none | navigation implementation |
| Telemetry | serialization/buffer/batch | Unity + backend metrics | none | Telemetry implementation |
| Profile | replay/retraction cost | backend profiler | none | Profile implementation |
| AED | decision latency | backend/Unity timing | none | AED implementation |
| Fusion | replicated state/bandwidth | Fusion stats | none | multiplayer implementation |

Performance regressions that prevent playable 2–4P are correctness/release blockers even before final tuning targets are frozen.

---

# 40. Tuning vs Correctness

## 40.1 Correctness configuration — cannot be deferred

Examples:

- Warden candidate bound relation covers complete authored cheap-eligible set;
- buffer capacities are finite/bounded;
- Profile/AED semantic/version configs resolve;
- fixed fallback exists and validates;
- route obligations and lock footprints are complete;
- strict enum registries exist;
- evidence-policy required fields exist.

## 40.2 Balance tuning — may remain after core implementation

Examples:

- Stalker speed/detection/search numbers;
- Listener hearing/attenuation numbers;
- Warden telegraph/cooldown;
- AED score-band/sample thresholds;
- registered support/chase candidate values.

Use `TUNING_REQUIRED` / `TUNING_AFTER_EVIDENCE`, not fabricated “final” values.

---

# 41. Work Package Register

The following cards are the executable backlog. `PROPOSED` paths are target bindings, not claims that files already exist.

Canonical work-package dependency fields:

```text
START_DEPENDENCIES
INTEGRATION_DEPENDENCIES
ACCEPTANCE_DEPENDENCIES
HARD_BLOCKER?   // only when safe implementation genuinely cannot begin
```

Canonical milestone fields:

```text
Official Milestone Classification
Accelerated Target Classification
```

`Official Milestone Classification` follows the supplied approved Project Plan. `Accelerated Target Classification` follows this plan's `ACCELERATED_FEATURE_COMPLETE_ALPHA` execution target. They are intentionally different until PM rebaseline is approved.

A fixture/interface used for START must preserve the owning locked contract. Fixture availability is never permission to invent semantics.

## M2-AI-FND-001 — Repository build/test baseline

- **Priority:** P0
- **Canonical owner/source:** AI Architecture + project environment
- **Reason:** Static repository is auditable but executable baseline is not proven.
- **Current evidence/status:** IMPLEMENTED_UNVERIFIED
- **START_DEPENDENCIES:** exact repository snapshot; approved Unity 6000.5.8f1 and .NET 8 toolchain availability
- **INTEGRATION_DEPENDENCIES:** Unity project import/compile and backend restore/build
- **ACCEPTANCE_DEPENDENCIES:** baseline compile/build logs; existing test inventory; warnings/errors captured
- **HARD_BLOCKER:** approved Unity/.NET toolchain unavailable on the implementation machine
- **Implementation scope:** Open/import/compile Unity; restore/build backend; record the existing test inventory and baseline errors. The final new contract-test harness is owned by TST-001/TST-003, not FND-001.
- **Expected code/assets:** Existing project roots; PROPOSED test asmdefs/project under current structure.
- **Authority/thread/network boundary:** No gameplay authority change.
- **Inputs:** source snapshot, package/project settings
- **Outputs:** build/test baseline report
- **Failure behavior:** Compile/import failure blocks feature work; classify root cause.
- **Required tests:** existing/baseline smoke where already runnable; Wave 0 aggregate gate later requires TST-001/TST-003 smoke execution
- **Evidence required:** Unity console/build logs; dotnet build output; smoke test output
- **Definition of Done:** Exact snapshot/toolchain recorded; Unity project imports/compiles to the current baseline; backend restores/builds; existing warnings/errors and test inventory archived. TST-001 does not need to be DONE.
- **Estimated complexity:** M
- **Parallelizable with:** all static/pure work
- **Official Milestone Classification:** M2_REQUIRED — prototype build/toolchain baseline
- **Accelerated Target Classification:** M2_REQUIRED

## M2-AI-FND-002 — Stable IDs, tick/time, deterministic helpers

- **Priority:** P0
- **Canonical owner/source:** AI Architecture + Monster contracts
- **Reason:** Episode/observation/action identity and deterministic tie-break are cross-cutting.
- **Current evidence/status:** NOT_STARTED
- **START_DEPENDENCIES:** locked AI/Monster contracts; importable project; deterministic helper test fixtures
- **INTEGRATION_DEPENDENCIES:** authoritative tick/time and gameplay identity providers as they become available
- **ACCEPTANCE_DEPENDENCIES:** selected STK/LIS/WAR determinism/exactly-once tests + source evidence
- **Implementation scope:** Implement stable gameplay IDs/equality; authoritative tick/time abstraction; stable sort/tie-break helpers; bounded identity utilities where semantics match.
- **Expected code/assets:** PROPOSED under `Assets/Scripts/AI/Common` or existing coherent shared folder; avoid Monster blackboard.
- **Authority/thread/network boundary:** Pure helpers; authority supplied by caller.
- **Inputs:** authoritative tick, stable IDs
- **Outputs:** typed identities/deterministic helpers
- **Failure behavior:** invalid/duplicate IDs reject or fail closed.
- **Required tests:** selected STK/LIS/WAR exactly-once/determinism EditMode tests
- **Evidence required:** source paths + test output
- **Definition of Done:** No monster-specific memory leaked into shared layer; deterministic tests pass.
- **Estimated complexity:** M
- **Parallelizable with:** TEL/SCN pure work
- **Official Milestone Classification:** M2_REQUIRED — Stalker/network/telemetry foundation
- **Accelerated Target Classification:** M2_REQUIRED

## M2-AI-FND-003 — Fusion authoritative gameplay/Monster boundary

- **Priority:** P0
- **Canonical owner/source:** AI Architecture + Photon Host Mode
- **Reason:** No project-owned NetworkBehaviour gameplay AI path is evidenced.
- **Current evidence/status:** NOT_STARTED
- **START_DEPENDENCIES:** AI Architecture/Fusion authority contract; Fusion package; narrow Player/Monster network identity interfaces/fixtures
- **INTEGRATION_DEPENDENCIES:** real Player NetworkObject identity, authoritative Life-State/network gameplay entities
- **ACCEPTANCE_DEPENDENCIES:** TST-002 executable network harness + STK-N/LIS-N/WAR-N/AED-N authority, proxy, late-join evidence
- **Implementation scope:** Create thin authoritative network wrappers; State Authority guard; authoritative simulation entry; durable replicated semantic/presentation state; late-join policy.
- **Expected code/assets:** PROPOSED under existing `Assets/Scripts/Networking` + monster-specific network adapters.
- **Authority/thread/network boundary:** Host/State Authority only mutates AI; proxies presentation-only.
- **Inputs:** Fusion Runner, player/monster network objects
- **Outputs:** authoritative ticks + replicated durable state
- **Failure behavior:** proxy mutation ignored/rejected; authority loss fails safe.
- **Required tests:** STK-N-001..016; LIS-N-001..020; WAR-N-001..017; AED-N-001..013 as components come online
- **Evidence required:** 2/3/4P logs/videos + automated network test output
- **Definition of Done:** No proxy-authoritative Monster/config mutation; late join current state validated.
- **Estimated complexity:** XL
- **Parallelizable with:** Monster pure logic, telemetry backend
- **Official Milestone Classification:** M2_REQUIRED — official Host Monster sync/runtime authority slice
- **Accelerated Target Classification:** M2_REQUIRED

## M2-AI-FND-004 — Navigation progress/repath/stuck/recovery layer

- **Priority:** P0
- **Canonical owner/source:** Stalker/Listener detailed designs
- **Reason:** Current Stalker navigation wrapper only caches destination/arrival.
- **Current evidence/status:** MIGRATION_REQUIRED
- **START_DEPENDENCIES:** Stalker/Listener navigation contracts; NavMeshAgent/path fixtures
- **INTEGRATION_DEPENDENCIES:** real Stalker/Listener navigation callers and production NavMesh
- **ACCEPTANCE_DEPENDENCIES:** relevant STK/LIS PlayMode path-failure/stuck/recovery evidence
- **Implementation scope:** Add destination validation/result, progress tracking, repath cadence, failure categories, stuck detection/recovery ladder as navigation-owned mechanics.
- **Expected code/assets:** ADAPT `StalkerNavigationController.cs`; PROPOSED reusable nav service only where Listener shares exact semantics.
- **Authority/thread/network boundary:** Host-authoritative movement decisions; pure planners do not own transforms.
- **Inputs:** NavMeshAgent, destination request
- **Outputs:** typed nav result/progress/failure
- **Failure behavior:** bounded retry/recovery; terminal failure returned to owning planner/FSM.
- **Required tests:** relevant STK-P navigation tests; LIS-P navigation tests
- **Evidence required:** PlayMode test output + debug traces
- **Definition of Done:** Path failures/stuck cannot silently freeze Monster; semantics remain owner-specific.
- **Estimated complexity:** L
- **Parallelizable with:** STK/LIS memory/planner work
- **Official Milestone Classification:** M2_REQUIRED — Stalker prototype navigation/recovery support
- **Accelerated Target Classification:** M2_REQUIRED

## M2-AI-FND-005 — Map/spatial authoring validators

- **Priority:** P0
- **Canonical owner/source:** AI Architecture + Stalker/Warden + map baseline
- **Reason:** RegionGraph and FacilityGraph assets/bindings are absent.
- **Current evidence/status:** NOT_STARTED
- **START_DEPENDENCIES:** Stalker/Warden graph contracts; stable physical-ID fixtures; editor validation harness
- **INTEGRATION_DEPENDENCIES:** Research Facility production map, NavMesh, DoorIds, objective/Exit bindings
- **ACCEPTANCE_DEPENDENCIES:** validated production Region/Facility assets + bake/validation tests
- **Implementation scope:** Implement stable physical IDs, deterministic bake/validation entry points for Stalker Region and Warden Facility definitions; no shared semantic graph.
- **Expected code/assets:** PROPOSED authoring scripts/assets under `Assets/Scripts/AI/.../Spatial` and project config folders.
- **Authority/thread/network boundary:** Editor/content validation only; runtime consumes validated definitions.
- **Inputs:** Research Facility map, NavMesh, doors, objective/Exit bindings
- **Outputs:** validated region/facility definitions + version/fingerprint
- **Failure behavior:** invalid content blocks affected Monster feature activation.
- **Required tests:** STK spatial validation tests; WAR-E graph/footprint tests
- **Evidence required:** validated assets + bake logs + tests
- **Definition of Done:** No missing/dangling IDs; required map bindings green.
- **Estimated complexity:** XL
- **Parallelizable with:** pure graph/planner code
- **Official Milestone Classification:** SPLIT: M2_REQUIRED for Research Facility/NavMesh/Stalker authoring; M4_REQUIRED for Warden FacilityGraph/door-obligation authoring
- **Accelerated Target Classification:** M2_REQUIRED

## M2-AI-STK-001 — Stalker perception, eligibility, memory, FSM migration

- **Priority:** P0
- **Canonical owner/source:** Stalker_AI_Design_v1.1
- **Reason:** Six-state spike exists but single-candidate vision and monolithic state/memory violate final ownership.
- **Current evidence/status:** MIGRATION_REQUIRED
- **START_DEPENDENCIES:** Stalker v1.1 contract; current spike source; FND-002 logical helpers/fixtures
- **INTEGRATION_DEPENDENCIES:** FND-003 authoritative Player target identity/eligibility and runtime bindings
- **ACCEPTANCE_DEPENDENCIES:** STK-E/P perception-memory-FSM coverage + Host integration evidence
- **Implementation scope:** Keep six states; typed VisionObservation; target registry/eligibility; DetectionTarget lock; CurrentTarget promotion; typed StalkerMemory; FSM owns transitions.
- **Expected code/assets:** ADAPT existing `Assets/Scripts/AI/Stalker/StalkerController.cs`, `StalkerVisionSensor.cs`, `StalkerBlackboard.cs`; proposed split under same folder.
- **Authority/thread/network boundary:** Host authoritative; clients no perception/FSM.
- **Inputs:** eligible players, LOS geometry, config
- **Outputs:** observations, memory, one semantic transition
- **Failure behavior:** invalid target clears by contract; no hidden transform access.
- **Required tests:** STK-E-001..030 relevant perception/FSM; STK-P perception/detect/chase tests
- **Evidence required:** source + EditMode/PlayMode + 2–4P evidence
- **Definition of Done:** Multi-player deterministic target selection; canonical memory/transition ownership; six-state contract intact.
- **Estimated complexity:** XL
- **Parallelizable with:** STK-002/003 pure work after memory interfaces
- **Official Milestone Classification:** M2_REQUIRED — official Prototype/Stalker scope
- **Accelerated Target Classification:** M2_REQUIRED

## M2-AI-STK-002 — Stalker RegionGraph coverage patrol

- **Priority:** P0
- **Canonical owner/source:** Stalker_AI_Design_v1.1
- **Reason:** Node graph/dynamic planner exists; RegionGraph/global-local final architecture missing.
- **Current evidence/status:** PARTIAL
- **START_DEPENDENCIES:** Stalker spatial contract; graph/region fixtures; current spatial spike
- **INTEGRATION_DEPENDENCIES:** FND-005 production RegionDefinition/SpatialGraph→Region bindings; STK-001 runtime planner interfaces
- **ACCEPTANCE_DEPENDENCIES:** canonical spatial EditMode/PlayMode tests + validated Research Facility region assets
- **Implementation scope:** Reuse NavMeshSpatialGraph where compatible; add RegionDefinition/RegionGraph/compatibility; CoverageMemory; GlobalPatrolPlanner + LocalPatrolSelector; migrate or retire unused Confidence prototype only after tests.
- **Expected code/assets:** ADAPT `AI/Stalker/Spatial/*`; PROPOSED Region layer beside existing code.
- **Authority/thread/network boundary:** Pure deterministic planning; Host executes navigation.
- **Inputs:** validated SpatialGraph/RegionGraph, coverage memory
- **Outputs:** patrol intent/destination
- **Failure behavior:** invalid graph/version → deterministic fallback/feature block per contract.
- **Required tests:** STK spatial EditMode + PlayMode patrol tests
- **Evidence required:** authoring assets + tests + debug region traces
- **Definition of Done:** Canonical region/global/local patrol active; no silent fixed fallback due missing scene serialization.
- **Estimated complexity:** XL
- **Parallelizable with:** STK-003/004
- **Official Milestone Classification:** M2_REQUIRED — official Prototype/Stalker scope
- **Accelerated Target Classification:** M2_REQUIRED

## M2-AI-STK-003 — Stalker canonical SEARCH episode

- **Priority:** P0
- **Canonical owner/source:** Stalker_AI_Design_v1.1
- **Reason:** Current SEARCH is LKP destination + timer only.
- **Current evidence/status:** MIGRATION_REQUIRED
- **START_DEPENDENCIES:** Stalker SEARCH contract; memory/navigation/search fixtures
- **INTEGRATION_DEPENDENCIES:** STK-001 memory/FSM + FND-004 navigation + STK-002 region context where required
- **ACCEPTANCE_DEPENDENCIES:** STK SEARCH/no-cheat/terminal PlayMode tests through canonical range
- **Implementation scope:** Implement immutable SearchContext(LKP, LastSeenDirection); SearchEpisode identity; candidate generation/filter/scoring; move-to-LKP/select region; terminal outcome/reacquisition; no hidden transform follow.
- **Expected code/assets:** ADAPT Stalker folder; PROPOSED search-specific pure classes/services.
- **Authority/thread/network boundary:** Host FSM owns state; planner uses memory only.
- **Inputs:** legal vision memory + graph/nav
- **Outputs:** search intents/outcome
- **Failure behavior:** no valid candidate/path → bounded terminal handling; never hidden target transform.
- **Required tests:** STK-E search cases; STK-P-033..044 where applicable
- **Evidence required:** test output + no-cheat debug evidence
- **Definition of Done:** Search behavior matches v1.1 and Search metrics can be emitted.
- **Estimated complexity:** L
- **Parallelizable with:** STK-002,004
- **Official Milestone Classification:** M2_REQUIRED — official Prototype/Stalker scope
- **Accelerated Target Classification:** M2_REQUIRED

## M2-AI-STK-004 — Stalker AttackEpisode exactly-once + Life-State hook

- **Priority:** P0
- **Canonical owner/source:** Stalker_AI_Design_v1.1 + Player Life-State
- **Reason:** Current hit/miss shape exists but no stable episode/side-effect guard/damage owner.
- **Current evidence/status:** MIGRATION_REQUIRED
- **START_DEPENDENCIES:** Stalker AttackEpisode contract; narrow Player Life-State interface/fixture; FND-002 identity helpers
- **INTEGRATION_DEPENDENCIES:** real authoritative Player Life-State + FND-003 Host network binding
- **ACCEPTANCE_DEPENDENCIES:** exactly-once attack tests + 2–4P one-hit/one-down evidence
- **Implementation scope:** Create one AttackEpisode per ATTACK entry; one authoritative Hit Moment resolve attempt; Hit/Miss; guard duplicate callbacks; request Life-State consequence through owner; immediate semantic RECOVER after resolution.
- **Expected code/assets:** ADAPT Stalker controller; PROPOSED attack controller/episode types under Stalker folder.
- **Authority/thread/network boundary:** Host State Authority only; presentation callbacks cannot own damage.
- **Inputs:** CurrentTarget legal attack context, range at resolution
- **Outputs:** attack research fact + optional Life-State transition request
- **Failure behavior:** duplicate resolver/callback/reconnect no second logical hit/damage.
- **Required tests:** STK attack EditMode/PlayMode/Network tests; TEL-E-ATK-01..07
- **Evidence required:** test output + attack/life-state logs
- **Definition of Done:** One AttackEpisode/one resolve/one possible life-state effect; telemetry separate from damage.
- **Estimated complexity:** L
- **Parallelizable with:** STK-003,Telemetry adapters
- **Official Milestone Classification:** M2_REQUIRED — official Prototype/Stalker scope
- **Accelerated Target Classification:** M2_REQUIRED

## M2-AI-STK-005 — Stalker navigation failure/recovery integration

- **Priority:** P1
- **Canonical owner/source:** Stalker_AI_Design_v1.1
- **Reason:** Current FSM ignores most path/stuck failure semantics.
- **Current evidence/status:** MIGRATION_REQUIRED
- **START_DEPENDENCIES:** navigation failure/recovery contract + FND-004 interfaces/fixtures
- **INTEGRATION_DEPENDENCIES:** STK-001/002/003 runtime planners and production navigation
- **ACCEPTANCE_DEPENDENCIES:** canonical navigation PlayMode tests + stuck/recovery traces
- **Implementation scope:** Bind navigation outcomes to state-specific planners; repath/stuck recovery; destination invalidation; graph/map revision behavior.
- **Expected code/assets:** ADAPT current navigation/controller integration.
- **Authority/thread/network boundary:** Host authoritative navigation.
- **Inputs:** state intent + nav results
- **Outputs:** stable progress/recovery/outcome
- **Failure behavior:** bounded failure cannot spin indefinitely.
- **Required tests:** relevant STK-P path/stuck/repath tests
- **Evidence required:** PlayMode traces + test output
- **Definition of Done:** All canonical nav failure paths implemented and observable.
- **Estimated complexity:** M
- **Parallelizable with:** FND-004,STK-002
- **Official Milestone Classification:** M2_REQUIRED — official Prototype/Stalker scope
- **Accelerated Target Classification:** M2_REQUIRED

## M2-AI-STK-006 — Stalker Fusion, telemetry, observability integration

- **Priority:** P0
- **Canonical owner/source:** Stalker + Telemetry + Architecture
- **Reason:** No Fusion AI binding or telemetry adapters exist.
- **Current evidence/status:** NOT_STARTED
- **START_DEPENDENCIES:** Stalker/Fusion/Telemetry contracts; adapter interfaces and event fixtures
- **INTEGRATION_DEPENDENCIES:** STK-001..005 runtime + FND-003 network authority + TEL-001/002 producer pipeline
- **ACCEPTANCE_DEPENDENCIES:** STK-N-001..016 + relevant TEL producer tests + late-join/debug evidence
- **Implementation scope:** Bind authoritative Monster NetworkObject; durable semantic presentation; late join; emit target/attack/search research events at canonical occurrences; debug snapshot.
- **Expected code/assets:** PROPOSED adapters under Stalker/Networking/Telemetry without moving pure core unnecessarily.
- **Authority/thread/network boundary:** Host produces events/state; proxies render only.
- **Inputs:** Stalker runtime outcomes
- **Outputs:** replicated state + telemetry occurrences
- **Failure behavior:** duplicate network/presentation callback cannot duplicate event/action.
- **Required tests:** STK-N-001..016; TEL-E-ATK-*; relevant telemetry integration tests
- **Evidence required:** 2–4P evidence + accepted backend events
- **Definition of Done:** Stalker complete across runtime/network/data boundaries.
- **Estimated complexity:** L
- **Parallelizable with:** TEL backend
- **Official Milestone Classification:** M2_REQUIRED — official Prototype/Stalker scope
- **Accelerated Target Classification:** M2_REQUIRED

## M2-AI-LIS-001 — RuntimeNoiseEvent / NoiseSystem authoritative pipeline

- **Priority:** P0
- **Canonical owner/source:** Listener_AI_Design_v1.0 + Telemetry boundary
- **Reason:** No runtime noise implementation found.
- **Current evidence/status:** NOT_STARTED
- **START_DEPENDENCIES:** Listener v1.0 RuntimeNoise contract; stable IDs; RuntimeNoise/NoiseSystem fixtures
- **INTEGRATION_DEPENDENCIES:** gameplay action/noise-emission hooks + FND-003 authoritative runtime
- **ACCEPTANCE_DEPENDENCIES:** canonical LIS RuntimeNoise/dedup/expiry tests + real action emission evidence
- **Implementation scope:** Implement NoiseEmissionResolver, immutable runtime event, source occurrence identity, bounded dedup retention/expiry, authoritative NoiseSystem; branch telemetry adapter separately.
- **Expected code/assets:** PROPOSED `Assets/Scripts/AI/Listener/Noise` or shared runtime-noise folder if ownership stays explicit.
- **Authority/thread/network boundary:** Host validates action/emits; clients cannot inject authoritative noise.
- **Inputs:** authoritative player/world actions
- **Outputs:** RuntimeNoiseEvent stream + telemetry adapter occurrence
- **Failure behavior:** duplicate source action no duplicate runtime noise; expired/dedup bounded.
- **Required tests:** LIS-E noise/dedup tests; LIS-P runtime noise; TEL noise integration
- **Evidence required:** tests + debug event logs
- **Definition of Done:** Listener can hear runtime events without reading Telemetry.
- **Estimated complexity:** XL
- **Parallelizable with:** TEL-002
- **Official Milestone Classification:** M4_REQUIRED — official Beta 3-Monster scope
- **Accelerated Target Classification:** M2_REQUIRED

## M2-AI-LIS-002 — Listener hearing, observations, pending memory

- **Priority:** P0
- **Canonical owner/source:** Listener_AI_Design_v1.0
- **Reason:** No Listener source exists.
- **Current evidence/status:** NOT_STARTED
- **START_DEPENDENCIES:** Listener hearing contract; NoiseSystem/HearingObservation fixtures
- **INTEGRATION_DEPENDENCIES:** LIS-001 authoritative RuntimeNoise pipeline
- **ACCEPTANCE_DEPENDENCIES:** LIS-E/P hearing/pending/immutable-observation tests through canonical range
- **Implementation scope:** Implement HearingSensor/occlusion/attenuation; immutable HearingObservation at authoritative tick; current batch; bounded pending inbox; ListenerMemory; exact selection/disposition reasons.
- **Expected code/assets:** PROPOSED Listener folder.
- **Authority/thread/network boundary:** Host only evaluates hearing.
- **Inputs:** RuntimeNoiseEvent + geometry at event evaluation tick
- **Outputs:** HearingObservation/current+pending memory
- **Failure behavior:** pending obs not re-raycast/reheard later; bounded lifecycle.
- **Required tests:** LIS-E-001..055 hearing/memory relevant tests
- **Evidence required:** EditMode/PlayMode evidence
- **Definition of Done:** Historical observations immutable; pending lifecycle exact.
- **Estimated complexity:** L
- **Parallelizable with:** LIS-003
- **Official Milestone Classification:** M4_REQUIRED — official Beta 3-Monster scope
- **Accelerated Target Classification:** M2_REQUIRED

## M2-AI-LIS-003 — Listener FSM / InvestigationEpisode / chase corroboration

- **Priority:** P0
- **Canonical owner/source:** Listener_AI_Design_v1.0
- **Reason:** No Listener FSM exists.
- **Current evidence/status:** NOT_STARTED
- **START_DEPENDENCIES:** Listener FSM/Investigation contract; observation/memory fixtures
- **INTEGRATION_DEPENDENCIES:** LIS-002 + FND-004 navigation result integration
- **ACCEPTANCE_DEPENDENCIES:** LIS deterministic arbitration/corroboration/investigation cleanup tests
- **Implementation scope:** Implement five-state FSM; same-tick arbitration; one InvestigationEpisode; merge/retain/interrupt; terminal cleanup; weak vision confirmation; CHASE self-noise/spatial corroboration.
- **Expected code/assets:** PROPOSED Listener runtime classes under `Assets/Scripts/AI/Listener`.
- **Authority/thread/network boundary:** Host semantic state authority.
- **Inputs:** ListenerMemory observations + target confirmations
- **Outputs:** state transition + investigation result
- **Failure behavior:** exactly one transition/decision step; terminal episode cannot resurrect.
- **Required tests:** LIS-E transition/investigation tests; LIS-P behavior tests
- **Evidence required:** state/debug trace + tests
- **Definition of Done:** Five-state behavior deterministic/no hidden source cheat.
- **Estimated complexity:** XL
- **Parallelizable with:** LIS-004
- **Official Milestone Classification:** M4_REQUIRED — official Beta 3-Monster scope
- **Accelerated Target Classification:** M2_REQUIRED

## M2-AI-LIS-004 — Listener planner/navigation/attack/recover

- **Priority:** P0
- **Canonical owner/source:** Listener_AI_Design_v1.0
- **Reason:** No action/navigation implementation exists.
- **Current evidence/status:** NOT_STARTED
- **START_DEPENDENCIES:** Listener planner/attack contracts; nav and Player Life-State interfaces/fixtures
- **INTEGRATION_DEPENDENCIES:** LIS-003 runtime + FND-004 + real Player Life-State + FND-003 authority
- **ACCEPTANCE_DEPENDENCIES:** Listener planner/navigation/attack/recover PlayMode + 2–4P exactly-once evidence
- **Implementation scope:** Implement roam/investigate/chase plans; navigation failure handling; Listener-owned AttackEpisode resolution; RECOVER; do not copy Stalker Hit Moment semantic.
- **Expected code/assets:** PROPOSED Listener planners/action controllers.
- **Authority/thread/network boundary:** Host authority; life-state consequence owner external.
- **Inputs:** state intent + target/observation + nav
- **Outputs:** movement/action outcomes
- **Failure behavior:** duplicate callback cannot duplicate attack research or damage.
- **Required tests:** LIS-P attack/nav/recover; LIS-N network tests; TEL-E-ATK-02/04/05/06/07
- **Evidence required:** PlayMode/Fusion logs + tests
- **Definition of Done:** Listener full action loop works 2–4P and attack semantics remain Listener-owned.
- **Estimated complexity:** XL
- **Parallelizable with:** LIS-005,TEL-002
- **Official Milestone Classification:** M4_REQUIRED — official Beta 3-Monster scope
- **Accelerated Target Classification:** M2_REQUIRED

## M2-AI-LIS-005 — Listener Fusion / telemetry / metrics / debug

- **Priority:** P1
- **Canonical owner/source:** Listener + Telemetry contracts
- **Reason:** No Listener network/telemetry evidence.
- **Current evidence/status:** NOT_STARTED
- **START_DEPENDENCIES:** Listener network/telemetry/metric contracts; adapter/event fixtures
- **INTEGRATION_DEPENDENCIES:** LIS-001..004 + FND-003 + TEL-001/002
- **ACCEPTANCE_DEPENDENCIES:** LIS-N-001..020 + TEL adapter/metric evidence + late join/debug
- **Implementation scope:** Replicate durable presentation state; late join; emit investigate started/resolved and attack facts; debug/metric fields.
- **Expected code/assets:** PROPOSED network/telemetry adapters.
- **Authority/thread/network boundary:** Host producer; proxy no AI/event authority.
- **Inputs:** Listener authoritative episodes
- **Outputs:** network presentation + research events
- **Failure behavior:** reconnect/presentation replay no duplicate occurrence.
- **Required tests:** LIS-N-001..020; TEL-E-ENUM-* relevant; TEL-E-ATK-*
- **Evidence required:** 2–4P + backend event evidence
- **Definition of Done:** Listener data/network boundary verified.
- **Estimated complexity:** L
- **Parallelizable with:** TEL-003
- **Official Milestone Classification:** M4_REQUIRED — official Beta 3-Monster scope
- **Accelerated Target Classification:** M2_REQUIRED

## M2-AI-WAR-001 — FacilityGraph + canonical route-lock authoring/runtime

- **Priority:** P0
- **Canonical owner/source:** Warden_AI_Design_v1.0
- **Reason:** No Warden or FacilityGraph implementation/assets found.
- **Current evidence/status:** NOT_STARTED
- **START_DEPENDENCIES:** Warden contract; FacilityGraph/Door/route-lock fixtures; stable-ID types
- **INTEGRATION_DEPENDENCIES:** FND-005 production FacilityGraph/DoorId/objective authoring
- **ACCEPTANCE_DEPENDENCIES:** WAR graph/footprint tests + validated production authoring assets
- **Implementation scope:** Implement FacilityGraphDefinition/runtime directed graph; DoorDefinition; WardenRouteLockDefinition; exact multi-edge footprints; independent Door Jammer overlay state.
- **Expected code/assets:** PROPOSED Warden Spatial folders + authored assets.
- **Authority/thread/network boundary:** Graph pure; Host applies runtime overlays.
- **Inputs:** validated map/door/objective content
- **Outputs:** FacilityGraph + runtime edge/door states
- **Failure behavior:** invalid footprint/ID blocks Warden activation.
- **Required tests:** WAR-E-001.. graph/footprint incl WAR-E-061..064
- **Evidence required:** assets + validation tests
- **Definition of Done:** Graph/footprints deterministic and authoring-valid.
- **Estimated complexity:** XL
- **Parallelizable with:** WAR-002
- **Official Milestone Classification:** M4_REQUIRED — official Beta 3-Monster scope
- **Accelerated Target Classification:** M2_REQUIRED

## M2-AI-WAR-002 — Required route obligations + WardenSafetyValidator

- **Priority:** P0
- **Canonical owner/source:** Warden_AI_Design_v1.0
- **Reason:** Safety subsystem absent.
- **Current evidence/status:** NOT_STARTED
- **START_DEPENDENCIES:** Warden safety/obligation contract; WAR-001 fixture graph
- **INTEGRATION_DEPENDENCIES:** production objective/Exit obligations and current route-state adapters
- **ACCEPTANCE_DEPENDENCIES:** WAR safety/reachability tests + production obligation validation
- **Implementation scope:** Implement required origin/destination resolver; objective/Exit obligations; reachability/shortest route; precommit/postapply/active revalidation; occupancy checks.
- **Expected code/assets:** PROPOSED pure Warden safety services.
- **Authority/thread/network boundary:** Pure validator consumes authoritative route state; no hidden Player data.
- **Inputs:** FacilityGraph/current overlays/objective state
- **Outputs:** WardenSafetyValidationResult
- **Failure behavior:** unsafe candidate rejected; unsafe active lock released by Warden fail-safe.
- **Required tests:** WAR-E safety suite; WAR-P route safety
- **Evidence required:** pure tests + PlayMode route evidence
- **Definition of Done:** Cannot soft-lock required objective/Exit route.
- **Estimated complexity:** XL
- **Parallelizable with:** WAR-003
- **Official Milestone Classification:** M4_REQUIRED — official Beta 3-Monster scope
- **Accelerated Target Classification:** M2_REQUIRED

## M2-AI-WAR-003 — RoutePressure / full-set candidate generation / deterministic policy

- **Priority:** P0
- **Canonical owner/source:** Warden_AI_Design_v1.0
- **Reason:** No policy code exists.
- **Current evidence/status:** NOT_STARTED
- **START_DEPENDENCIES:** Warden RoutePressure/full-set policy contract; WAR-001/002 fixtures
- **INTEGRATION_DEPENDENCIES:** live CurrentRouteModel and production safety evaluator
- **ACCEPTANCE_DEPENDENCIES:** WAR RoutePressure/full-set/deterministic policy tests including corrected ranges
- **Implementation scope:** Implement cheap eligibility, full candidate set, PressureCandidateBound config validator, safety pass, RoutePressure_v1.0, meaningful-pressure filter, fresh/history tie-break.
- **Expected code/assets:** PROPOSED pure Warden policy services.
- **Authority/thread/network boundary:** Pure deterministic policy; Host invokes.
- **Inputs:** validated route lock definitions/current route model
- **Outputs:** selected candidate or NO_ACTION reason
- **Failure behavior:** bound too small → config failure/no action; no truncation; zero pressure unselectable.
- **Required tests:** WAR-E-066..071 + policy tests
- **Evidence required:** tests + debug candidate counts
- **Definition of Done:** No rotating cursor/partial-set semantics; deterministic global result.
- **Estimated complexity:** L
- **Parallelizable with:** WAR-004
- **Official Milestone Classification:** M4_REQUIRED — official Beta 3-Monster scope
- **Accelerated Target Classification:** M2_REQUIRED

## M2-AI-WAR-004 — Telegraph / atomic lock-release / fail-safe lifecycle

- **Priority:** P0
- **Canonical owner/source:** Warden_AI_Design_v1.0
- **Reason:** Visible lock lifecycle absent.
- **Current evidence/status:** NOT_STARTED
- **START_DEPENDENCIES:** Warden lifecycle contract; fake door/overlay/safety fixtures
- **INTEGRATION_DEPENDENCIES:** WAR-002/003 + real door state/Jammer + FND-003 Host binding
- **ACCEPTANCE_DEPENDENCIES:** WAR PlayMode telegraph/apply/release/failsafe + 2–4P evidence
- **Implementation scope:** Implement WardenAction identity; telegraph exact DoorId+footprint; precommit revalidate; atomic apply/release; expiry; post-apply validation; fail-safe reasons; one active lock.
- **Expected code/assets:** PROPOSED Warden runtime action controller + door overlay adapter.
- **Authority/thread/network boundary:** Host only changes Warden overlay; clients present telegraph/door state.
- **Inputs:** selected candidate + current graph state
- **Outputs:** active/released Warden action
- **Failure behavior:** TOCTOU failure rejects; release exactly once; no partial edge apply.
- **Required tests:** WAR-P lock/release/failsafe; WAR-N-017; telemetry WAR tests
- **Evidence required:** PlayMode/Fusion evidence
- **Definition of Done:** Lock cannot partially apply or survive unsafe state.
- **Estimated complexity:** XL
- **Parallelizable with:** WAR-005,TEL-002
- **Official Milestone Classification:** M4_REQUIRED — official Beta 3-Monster scope
- **Accelerated Target Classification:** M2_REQUIRED

## M2-AI-WAR-005 — Warden Fusion / telemetry / debug integration

- **Priority:** P1
- **Canonical owner/source:** Warden + Telemetry contracts
- **Reason:** No network/telemetry Warden implementation.
- **Current evidence/status:** NOT_STARTED
- **START_DEPENDENCIES:** Warden Fusion/Telemetry contracts; adapter fixtures
- **INTEGRATION_DEPENDENCIES:** WAR-004 + FND-003 + TEL-001/002
- **ACCEPTANCE_DEPENDENCIES:** WAR-N-001..017 + Warden telemetry/debug evidence
- **Implementation scope:** Replicate current lock/telegraph durable state; late join; emit telegraph/applied/safety/released research events; debug snapshot.
- **Expected code/assets:** PROPOSED Warden adapters.
- **Authority/thread/network boundary:** Host events/actions; proxies presentation-only.
- **Inputs:** Warden action lifecycle
- **Outputs:** network state + research events
- **Failure behavior:** duplicate apply/release callbacks do not duplicate events.
- **Required tests:** WAR-N-001..017; TEL-E-WAR-01..08
- **Evidence required:** 2–4P + backend evidence
- **Definition of Done:** Warden lifecycle research/network evidence exact.
- **Estimated complexity:** L
- **Parallelizable with:** TEL-003
- **Official Milestone Classification:** M4_REQUIRED — official Beta 3-Monster scope
- **Accelerated Target Classification:** M2_REQUIRED

## M2-TEL-001 — Telemetry runtime core: event factory/sequence/buffer/transport

- **Priority:** P0
- **Canonical owner/source:** Telemetry_Contract_v1.1
- **Reason:** No Unity telemetry runtime found.
- **Current evidence/status:** NOT_STARTED
- **START_DEPENDENCIES:** Telemetry v1.1 contract; FND-002 identity helpers; match/event fixtures
- **INTEGRATION_DEPENDENCIES:** authoritative match identity/runtime transport integration
- **ACCEPTANCE_DEPENDENCIES:** TEL identity/sequence/buffer/transport tests + real bounded transport evidence
- **Implementation scope:** Implement immutable event creation; match-wide sequence allocator; retry-stable IDs/payload; bounded buffer; batch send; partial ack/retry.
- **Expected code/assets:** PROPOSED `Assets/Scripts/Telemetry` preserving project style.
- **Authority/thread/network boundary:** Authoritative occurrence adapters produce; transport has no gameplay authority.
- **Inputs:** authoritative facts + context provenance
- **Outputs:** serialized v1.1 batches
- **Failure behavior:** overflow observable; retry immutable; no event reordering assumptions.
- **Required tests:** TEL-E identity/order/buffer; TEL-N integration families
- **Evidence required:** unit tests + transport logs
- **Definition of Done:** Wire matches v1.1; retry/idempotency inputs exact.
- **Estimated complexity:** XL
- **Parallelizable with:** TEL-003,Monster adapters
- **Official Milestone Classification:** M2_REQUIRED — official runtime telemetry emitter/basic support
- **Accelerated Target Classification:** M2_REQUIRED

## M2-TEL-002 — Gameplay/Monster/Scenario telemetry producers

- **Priority:** P0
- **Canonical owner/source:** Telemetry + owning gameplay/AI contracts
- **Reason:** No producers found.
- **Current evidence/status:** NOT_STARTED
- **START_DEPENDENCIES:** Telemetry event ownership contracts; owner-adapter interfaces/fixtures
- **INTEGRATION_DEPENDENCIES:** owning gameplay/Monster/Scenario implementations + TEL-001
- **ACCEPTANCE_DEPENDENCIES:** canonical producer/PlayMode tests including TEL-P-001..024 + occurrence evidence
- **Implementation scope:** Map each active event to semantic owner and SourceOccurrenceKey; implement thin adapters at authoritative occurrences only.
- **Expected code/assets:** PROPOSED adapters near owner or Telemetry adapter layer.
- **Authority/thread/network boundary:** Host/gameplay owner emits; proxy never duplicates.
- **Inputs:** match/player/objective/noise/Monster/Warden/AED occurrences
- **Outputs:** TelemetryEvent creation requests
- **Failure behavior:** duplicate callback guarded by source occurrence identity.
- **Required tests:** TEL-P-001..024; TEL-E-ENUM-*; TEL-E-ATK-*; TEL-E-WAR-*
- **Evidence required:** accepted events matched to gameplay logs
- **Definition of Done:** All active event producers mapped/tested; no invented events.
- **Estimated complexity:** XL
- **Parallelizable with:** Monster/Scenario work
- **Official Milestone Classification:** SPLIT: M2_REQUIRED for core/Stalker/match producers; M4_REQUIRED for Listener/Warden/AED producer extensions
- **Accelerated Target Classification:** M2_REQUIRED

## M2-TEL-003 — Backend telemetry validator + immutable idempotent raw storage

- **Priority:** P0
- **Canonical owner/source:** Telemetry_Contract_v1.1
- **Reason:** Backend currently auth only; no telemetry models/controllers.
- **Current evidence/status:** NOT_STARTED
- **START_DEPENDENCIES:** Telemetry v1.1 validator/storage contract; backend test DB; event/batch fixtures
- **INTEGRATION_DEPENDENCIES:** real TEL-001 batches/transport
- **ACCEPTANCE_DEPENDENCIES:** backend schema/idempotency/conflict tests + immutable accepted storage evidence
- **Implementation scope:** Implement ingest API/service; v1.0/v1.1 routing; common/event-specific validator; enum/conditional validation; idempotent IDs and sequence uniqueness; immutable raw storage; partial ack.
- **Expected code/assets:** PROPOSED under existing API architecture; add migrations/entities/services/controllers clearly separated from auth.
- **Authority/thread/network boundary:** Backend analytical/data plane only; cannot command gameplay.
- **Inputs:** Telemetry batches
- **Outputs:** accepted immutable raw events + per-event ack/quarantine
- **Failure behavior:** same ID conflicting payload or sequence conflict quarantined/rejected per contract.
- **Required tests:** TEL-I-001..020; TEL-E validator suites
- **Evidence required:** backend test DB + API test output
- **Definition of Done:** Deterministic accept/reject/idempotency with immutable original schemaVersion.
- **Estimated complexity:** XL
- **Parallelizable with:** TEL-001
- **Official Milestone Classification:** M2_REQUIRED — official telemetry batch ingest/storage
- **Accelerated Target Classification:** M2_REQUIRED

## M2-TEL-004 — Completeness / late-event / research provenance processing

- **Priority:** P0
- **Canonical owner/source:** Telemetry_Contract_v1.1
- **Reason:** No completeness processor exists.
- **Current evidence/status:** NOT_STARTED
- **START_DEPENDENCIES:** Telemetry completeness/late-event contract; accepted-event/storage fixtures
- **INTEGRATION_DEPENDENCIES:** TEL-003 real accepted raw storage
- **ACCEPTANCE_DEPENDENCIES:** late-event/completeness/research-provenance integration evidence + canonical TEL tests
- **Implementation scope:** Implement match stream projection/order; gaps/invalidity/completeness; late fill; metric evidence availability; researchCapture boundaries/provenance.
- **Expected code/assets:** PROPOSED backend processing services.
- **Authority/thread/network boundary:** Processed data only; no gameplay authority.
- **Inputs:** accepted raw events
- **Outputs:** MatchTelemetry/completeness evidence
- **Failure behavior:** late data recomputes; missing != zero; invalidity propagated.
- **Required tests:** TEL completeness/late tests; Profile integration fixtures
- **Evidence required:** deterministic rebuild tests
- **Definition of Done:** Profile receives current, reproducible evidence state.
- **Estimated complexity:** L
- **Parallelizable with:** PRO-001
- **Official Milestone Classification:** M4_REQUIRED for full v1.1 completeness/research-modeling support; M3 adds phase/objective timing metrics
- **Accelerated Target Classification:** M2_REQUIRED

## M2-TEL-005 — Telemetry observability/quarantine/operational tests

- **Priority:** P1
- **Canonical owner/source:** Telemetry_Contract_v1.1
- **Reason:** No telemetry ops evidence.
- **Current evidence/status:** NOT_STARTED
- **START_DEPENDENCIES:** Telemetry observability/quarantine contract; diagnostic fixtures
- **INTEGRATION_DEPENDENCIES:** TEL-001..004 runtime/backend
- **ACCEPTANCE_DEPENDENCIES:** operational conflict/gap/overflow/quarantine tests and logs
- **Implementation scope:** Implement buffer/backlog/ack/conflict/quarantine/completeness debug/admin evidence; load/retry failure tests.
- **Expected code/assets:** PROPOSED metrics/logging endpoints/internal diagnostics; no gameplay UI authority.
- **Authority/thread/network boundary:** Read-only/data operations.
- **Inputs:** telemetry pipeline state
- **Outputs:** operational diagnostics
- **Failure behavior:** diagnostic failure cannot mutate gameplay/event history.
- **Required tests:** TEL-I/N integration tests
- **Evidence required:** logs/test outputs
- **Definition of Done:** Conflicts/gaps/overflow can be diagnosed without raw-event mutation.
- **Estimated complexity:** M
- **Parallelizable with:** OBS-001
- **Official Milestone Classification:** M4_REQUIRED for full operational telemetry diagnostics/research support
- **Accelerated Target Classification:** M2_REQUIRED

## M2-PRO-001 — MatchTelemetry projection / eligibility / metric availability

- **Priority:** P0
- **Canonical owner/source:** Player_Team_Profile_Contract_v1.1
- **Reason:** No canonical Profile processor found.
- **Current evidence/status:** NOT_STARTED
- **START_DEPENDENCIES:** Telemetry v1.1 contract; canonical MatchTelemetry fixtures; backend test infrastructure
- **INTEGRATION_DEPENDENCIES:** TEL-003 accepted raw storage; TEL-004 completeness/processed evidence
- **ACCEPTANCE_DEPENDENCIES:** real accepted MatchTelemetry; late-event/completeness integration evidence; Profile contract tests
- **Implementation scope:** Implement integrity gate; MatchProfileEligibility; MetricAvailability/Finality; source version checks; survival/noise source extraction.
- **Expected code/assets:** PROPOSED backend `Profiles/Processing` or coherent equivalent.
- **Authority/thread/network boundary:** Processed data only; no gameplay authority.
- **Inputs:** MatchTelemetry
- **Outputs:** eligibility + metric evidence
- **Failure behavior:** INELIGIBLE/INVALID suppresses contributions; missing not zero.
- **Required tests:** PRO-E-001..062 relevant to eligibility/availability and correction semantics
- **Evidence required:** pure/backend test output
- **Definition of Done:** Availability/eligibility exact and reproducible.
- **Estimated complexity:** L
- **Parallelizable with:** PRO-002
- **Official Milestone Classification:** M4_REQUIRED — official Player/Team Modeling/AED foundation scope
- **Accelerated Target Classification:** M2_REQUIRED

## M2-PRO-002 — Survival/Noise MatchScore + semantic config registry

- **Priority:** P0
- **Canonical owner/source:** Profile v1.1
- **Reason:** No AI MatchScore implementation.
- **Current evidence/status:** NOT_STARTED
- **START_DEPENDENCIES:** Profile v1.1 formula/config semantics; metric fixtures
- **INTEGRATION_DEPENDENCIES:** PRO-001 real eligibility/MetricAvailability outputs
- **ACCEPTANCE_DEPENDENCIES:** real eligible MatchTelemetry inputs + canonical PRO-E formula/config tests
- **Implementation scope:** Implement pure active dimension scores only; PROFILE_FORMULA_V1_1 semantic registry; normalization/filter/alpha versions; no deferred dimension values.
- **Expected code/assets:** PROPOSED backend pure domain/services.
- **Authority/thread/network boundary:** Pure analytics.
- **Inputs:** valid metric evidence + config
- **Outputs:** PlayerMatchScore
- **Failure behavior:** invalid config/metric → unavailable/invalid, not zero.
- **Required tests:** PRO-E score/version tests incl 047..053
- **Evidence required:** pure tests
- **Definition of Done:** SURVIVAL/NOISE exact; first observation not blended with cold 50.
- **Estimated complexity:** M
- **Parallelizable with:** PRO-003
- **Official Milestone Classification:** M4_REQUIRED — official Player/Team Modeling/AED foundation scope
- **Accelerated Target Classification:** M2_REQUIRED

## M2-PRO-003 — Dimension observation ledger / idempotent apply / retraction / replay

- **Priority:** P0
- **Canonical owner/source:** Profile v1.1 PRO-DD-01/04
- **Reason:** No persistent AI Profile pipeline.
- **Current evidence/status:** NOT_STARTED
- **START_DEPENDENCIES:** Profile v1.1 ledger/idempotency/retraction contract; PlayerMatchScore fixtures; backend persistence test harness
- **INTEGRATION_DEPENDENCIES:** PRO-002 production score outputs and durable persistence
- **ACCEPTANCE_DEPENDENCIES:** PRO-E/PRO-B apply/conflict/retraction/replay/crash tests + persisted revision evidence
- **Implementation scope:** Implement ProfileDimensionApplyKey; immutable semantic observation; CONTRIBUTING/RETRACTED; conflict; late same-match completion; canonical per-dimension replay; global/metric invalidation; cold-start restore; lineage migration semantics.
- **Expected code/assets:** PROPOSED backend entities/services/migrations distinct from account PlayerProfile.
- **Authority/thread/network boundary:** Backend processed state only.
- **Inputs:** dimension observations + source validity
- **Outputs:** PlayerDimensionState/profileRevision
- **Failure behavior:** no inverse EMA; stale worker precommit revalidation; duplicate retraction no-op.
- **Required tests:** PRO-E-041..062; PRO-B-016..029
- **Evidence required:** backend concurrency/restart tests
- **Definition of Done:** Retry/crash/order/source correction converge exactly.
- **Estimated complexity:** XL
- **Parallelizable with:** PRO-004
- **Official Milestone Classification:** M4_REQUIRED — official Player/Team Modeling/AED foundation scope
- **Accelerated Target Classification:** M2_REQUIRED

## M2-PRO-004 — PlayerProfileSnapshot / RosterProfileSummary / comparison keys

- **Priority:** P0
- **Canonical owner/source:** Profile v1.1 PRO-DD-02/03
- **Reason:** No snapshot/roster implementation.
- **Current evidence/status:** NOT_STARTED
- **START_DEPENDENCIES:** Profile snapshot/comparison-key contract; persisted-profile fixtures
- **INTEGRATION_DEPENDENCIES:** PRO-003 real PlayerAIProfile revisions/lineages
- **ACCEPTANCE_DEPENDENCIES:** PRO-S semantic compatibility/snapshot tests + real persisted revision evidence
- **Implementation scope:** Implement immutable snapshots; semantic comparison key per active dimension; coherent roster aggregation; no silent incompatible-player drop; match-scoped TeamProfile where required.
- **Expected code/assets:** PROPOSED backend domain/services.
- **Authority/thread/network boundary:** No gameplay authority; no persistent team identity.
- **Inputs:** current PlayerAIProfiles + roster
- **Outputs:** snapshots + RosterProfileSummary
- **Failure behavior:** mixed semantic keys → INVALID/FORMULA_VERSION_CONFLICT.
- **Required tests:** PRO-E-047..053; PRO-S-019..025
- **Evidence required:** pure/backend tests
- **Definition of Done:** Cross-player aggregation semantics exact.
- **Estimated complexity:** L
- **Parallelizable with:** PRO-005
- **Official Milestone Classification:** M4_REQUIRED — official Player/Team Modeling/AED foundation scope
- **Accelerated Target Classification:** M2_REQUIRED

## M2-PRO-005 — AdaptiveInputSnapshot build / staleness / data API

- **Priority:** P0
- **Canonical owner/source:** Profile v1.1 + AED boundary
- **Reason:** No AdaptiveInputSnapshot implementation.
- **Current evidence/status:** NOT_STARTED
- **START_DEPENDENCIES:** AdaptiveInputSnapshot/Profile v1.1 contract; PlayerProfileSnapshot/Roster fixtures
- **INTEGRATION_DEPENDENCIES:** PRO-004 real snapshot provider + current roster/revision source
- **ACCEPTANCE_DEPENDENCIES:** PRO-S staleness/roster/revision tests + live snapshot handoff evidence
- **Implementation scope:** Build immutable decision-scoped snapshot; fingerprint; target match/roster; source revisions; VALID/PARTIAL/INVALID; current-match processed evidence boundary; stale detection.
- **Expected code/assets:** PROPOSED backend/profile API or host-access binding; exact transport implementation-bound.
- **Authority/thread/network boundary:** Profile produces; AED reads; no direct gameplay mutation.
- **Inputs:** current roster + PlayerProfileSnapshots
- **Outputs:** AdaptiveInputSnapshot
- **Failure behavior:** new profile revision/retraction makes old snapshot stale, not mutated.
- **Required tests:** PRO-S-001..028 incl 026..028
- **Evidence required:** snapshot tests + AED fixture
- **Definition of Done:** AED can consume current coherent snapshot without raw telemetry.
- **Estimated complexity:** L
- **Parallelizable with:** AED-001
- **Official Milestone Classification:** M4_REQUIRED — official Player/Team Modeling/AED foundation scope
- **Accelerated Target Classification:** M2_REQUIRED

## M2-SCN-001 — ScenarioConfig schema / base reference / registries / fixed baseline asset

- **Priority:** P0
- **Canonical owner/source:** AED_ScenarioConfig_Contract_v1.1
- **Reason:** No ScenarioConfig implementation/assets found.
- **Current evidence/status:** NOT_STARTED
- **START_DEPENDENCIES:** AED/Scenario contract; typed ScenarioConfig/BaseRef/registry fixtures
- **INTEGRATION_DEPENDENCIES:** production map/content registry and fixed baseline content
- **ACCEPTANCE_DEPENDENCIES:** resolvable valid fixed baseline + version/fingerprint/config validation evidence
- **Implementation scope:** Implement typed schema, version/fingerprint, ScenarioConfigBaseRef, fixed baseline provider data, parameter/content registries.
- **Expected code/assets:** PROPOSED Unity config assets/classes + backend if architecture requires persistence.
- **Authority/thread/network boundary:** Config data; no policy authority.
- **Inputs:** designer-authored content
- **Outputs:** resolved base/fixed config
- **Failure behavior:** missing/invalid mandatory config blocks Fixed/Adaptive safely.
- **Required tests:** AED-E config/version/registry tests
- **Evidence required:** assets + validation tests
- **Definition of Done:** Known-valid fixed baseline resolvable; exact version provenance.
- **Estimated complexity:** L
- **Parallelizable with:** SCN-002/003
- **Official Milestone Classification:** M4_REQUIRED — official ScenarioConfig/Scenario Validator/AED integration scope
- **Accelerated Target Classification:** M2_REQUIRED

## M2-SCN-002 — FixedDirector

- **Priority:** P0
- **Canonical owner/source:** AED_ScenarioConfig_Contract_v1.1
- **Reason:** No FixedDirector found.
- **Current evidence/status:** NOT_STARTED
- **START_DEPENDENCIES:** FixedDirector contract; SCN-001 fixture baseline/provider
- **INTEGRATION_DEPENDENCIES:** SCN-001 production fixed baseline registry
- **ACCEPTANCE_DEPENDENCIES:** Fixed path tests proving Profile/AED independence + valid resolved config evidence
- **Implementation scope:** Implement PRE_MATCH full fixed resolution and mid-match KEEP_LAST_VALID_CONFIG behavior; independent of Profile/AED availability.
- **Expected code/assets:** PROPOSED Scenario/Director service.
- **Authority/thread/network boundary:** Host applies only after validation.
- **Inputs:** resolution mode/decision point/base config
- **Outputs:** fixed candidate/resolution result
- **Failure behavior:** invalid fixed baseline → explicit configuration failure.
- **Required tests:** AED-E-001; AED-P fixed/fallback tests
- **Evidence required:** Unity/backend tests + applied config evidence
- **Definition of Done:** Fixed match can start safely without Adaptive.
- **Estimated complexity:** M
- **Parallelizable with:** SCN-003/004
- **Official Milestone Classification:** M4_REQUIRED — official ScenarioConfig/Scenario Validator/AED integration scope
- **Accelerated Target Classification:** M2_REQUIRED

## M2-SCN-003 — ScenarioValidator + route/spawn/fairness adapters

- **Priority:** P0
- **Canonical owner/source:** AED Scenario contract + Warden/map
- **Reason:** No validator found.
- **Current evidence/status:** NOT_STARTED
- **START_DEPENDENCIES:** ScenarioValidator contract; route/spawn/fairness fixtures
- **INTEGRATION_DEPENDENCIES:** production map/objective/spawn adapters + FacilityGraph route-safety abstraction
- **ACCEPTANCE_DEPENDENCIES:** AED-E/R validator tests + production route/spawn safety evidence
- **Implementation scope:** Implement pure validation: schema/version/key authority/timing/bounds/pressure, spawn, FacilityGraph route safety, final hunt, base/window semantics.
- **Expected code/assets:** PROPOSED ScenarioValidator + adapters.
- **Authority/thread/network boundary:** Pure validator; consumes current authoritative state snapshots.
- **Inputs:** candidate + base/current map/route state
- **Outputs:** VALID/INVALID reason
- **Failure behavior:** no silent repair/clamp; whole candidate rejects on any invalid field.
- **Required tests:** AED-E-030..057 relevant; AED-R-001..010
- **Evidence required:** pure tests + route fixtures
- **Definition of Done:** Cannot apply unsafe/unapproved ScenarioConfig.
- **Estimated complexity:** XL
- **Parallelizable with:** SCN-002,AED pure work
- **Official Milestone Classification:** M4_REQUIRED — official ScenarioConfig/Scenario Validator/AED integration scope
- **Accelerated Target Classification:** M2_REQUIRED

## M2-SCN-004 — Host ScenarioConfig apply / replication / CAS

- **Priority:** P0
- **Canonical owner/source:** AI Architecture + AED Scenario contract
- **Reason:** No applied-config network path evidenced.
- **Current evidence/status:** NOT_STARTED
- **START_DEPENDENCIES:** Scenario apply/Fusion contract; Host/CAS fixtures; SCN interfaces
- **INTEGRATION_DEPENDENCIES:** FND-003 + SCN-002/003 production components
- **ACCEPTANCE_DEPENDENCIES:** AED-P/N stale-base/atomic apply/late-join tests + authoritative replicated state evidence
- **Implementation scope:** Implement atomic authoritative apply; PRE_MATCH base identity/fingerprint revalidation; MID_MATCH current Applied version CAS; durable current config replication; late join.
- **Expected code/assets:** PROPOSED NetworkBehaviour apply controller + pure services.
- **Authority/thread/network boundary:** Host/State Authority only.
- **Inputs:** validated resolution/candidate
- **Outputs:** new applied config or no-change/fallback
- **Failure behavior:** stale base/window → no overwrite; proxy request cannot apply.
- **Required tests:** AED-P/N/B stale/apply tests
- **Evidence required:** 2–4P + reconnect/late-join evidence
- **Definition of Done:** One authoritative config version per apply; NO_CHANGE no version.
- **Estimated complexity:** XL
- **Parallelizable with:** AED-003
- **Official Milestone Classification:** M4_REQUIRED — official ScenarioConfig/Scenario Validator/AED integration scope
- **Accelerated Target Classification:** M2_REQUIRED

## M2-AED-001 — AdaptiveInputGate + AEDEvidencePolicy provider

- **Priority:** P0
- **Canonical owner/source:** AED_ScenarioConfig_Contract_v1.1
- **Reason:** No AED implementation.
- **Current evidence/status:** NOT_STARTED
- **START_DEPENDENCIES:** AED_SCENARIO_POLICY_V1_1 contract; canonical AdaptiveInputSnapshot fixtures; AEDEvidencePolicy fixtures
- **INTEGRATION_DEPENDENCIES:** PRO-005 real AdaptiveInputSnapshot provider; SCN-001 version registry
- **ACCEPTANCE_DEPENDENCIES:** complete AED input-gate/evidence tests + live current-snapshot integration
- **Implementation scope:** Consume immutable snapshot; validate target/roster/source revisions/semantic support; VALID/PARTIAL/INVALID; evidence thresholds owned only by evidencePolicyVersion.
- **Expected code/assets:** PROPOSED AED pure services/config provider.
- **Authority/thread/network boundary:** No gameplay authority.
- **Inputs:** AdaptiveInputSnapshot + evidence policy
- **Outputs:** ELIGIBLE/INELIGIBLE/INVALID gate result
- **Failure behavior:** missing/malformed evidence config → NOT_EVALUATED fallback path.
- **Required tests:** AED-E-002..016 + DD-01 tests
- **Evidence required:** pure tests
- **Definition of Done:** No duplicate threshold owner; stale input blocked.
- **Estimated complexity:** L
- **Parallelizable with:** AED-002
- **Official Milestone Classification:** M4_REQUIRED — official AED Beta scope
- **Accelerated Target Classification:** M2_REQUIRED

## M2-AED-002 — PolicyDefinition / tuning config / score bands / canonical rules

- **Priority:** P0
- **Canonical owner/source:** AED_SCENARIO_POLICY_V1_1
- **Reason:** No policy code/config.
- **Current evidence/status:** NOT_STARTED
- **START_DEPENDENCIES:** AED_SCENARIO_POLICY_V1_1 contract; PolicyDefinition semantics; tuning-config schema/fixtures
- **INTEGRATION_DEPENDENCIES:** AED-001 gate output and real AdaptiveInputSnapshot provider for integration
- **ACCEPTANCE_DEPENDENCIES:** complete AED rule/priority/mirror/config tests; same semantic input reproduces same rule
- **Implementation scope:** Encode canonical rules/priorities/intent/active keys/strategy bindings in PolicyDefinition; tuning-only score bands in AEDPolicyConfig; mirrors must exact-match definition if serialized.
- **Expected code/assets:** PROPOSED pure policy + versioned config asset/backend representation.
- **Authority/thread/network boundary:** Pure deterministic.
- **Inputs:** eligible snapshot means + config
- **Outputs:** RuleId/Intent/action strategy
- **Failure behavior:** semantic mirror mismatch → POLICY_CONFIG_INVALID; no execution.
- **Required tests:** AED-E band/rule priority + AED-DD-02 tests
- **Evidence required:** pure determinism tests
- **Definition of Done:** Same semantic input/config → same rule; policyConfigVersion cannot change semantics.
- **Estimated complexity:** L
- **Parallelizable with:** AED-003
- **Official Milestone Classification:** M4_REQUIRED — official AED Beta scope
- **Accelerated Target Classification:** M2_REQUIRED

## M2-AED-003 — Parameter registry / candidate builder / decision ledger / fallback

- **Priority:** P0
- **Canonical owner/source:** AED Scenario contract
- **Reason:** No implementation.
- **Current evidence/status:** NOT_STARTED
- **START_DEPENDENCIES:** AED/Scenario contracts; parameter-registry, candidate, decision-ledger and fallback fixtures
- **INTEGRATION_DEPENDENCIES:** AED-002 policy output + SCN-003 validator + SCN-004 authoritative apply
- **ACCEPTANCE_DEPENDENCIES:** AED registry/candidate/idempotency/fallback tests + real Scenario candidate/apply evidence
- **Implementation scope:** Implement candidateValues; NEXT_HIGHER_REGISTERED_VALUE; active key checks; candidate build; APPLIED/NO_CHANGE/FIXED_FALLBACK; decisionId/fingerprint/idempotency; prebuild registry invalid vs candidate invalid stages; fallback resolver.
- **Expected code/assets:** PROPOSED AED/Scenario services + decision persistence.
- **Authority/thread/network boundary:** Policy proposes; ScenarioValidator validates; Host apply controller commits.
- **Inputs:** policy rule + base ref + registry
- **Outputs:** decision/candidate/result
- **Failure behavior:** retry idempotent; conflict quarantined; NO_CHANGE no FixedDirector/version.
- **Required tests:** AED-E-026..057 + AED-B-001..025
- **Evidence required:** pure/backend tests
- **Definition of Done:** Correct failure stage/reason and exactly-once decision semantics.
- **Estimated complexity:** XL
- **Parallelizable with:** SCN-004
- **Official Milestone Classification:** M4_REQUIRED — official AED Beta scope
- **Accelerated Target Classification:** M2_REQUIRED

## M2-AED-004 — AED integration / authority / telemetry-observability

- **Priority:** P1
- **Canonical owner/source:** AED + Telemetry + Fusion
- **Reason:** No integration.
- **Current evidence/status:** NOT_STARTED
- **START_DEPENDENCIES:** AED integration contracts; stable interfaces from AED/Scenario/Telemetry
- **INTEGRATION_DEPENDENCIES:** AED-001..003 + SCN-004 + TEL-001/002 live paths
- **ACCEPTANCE_DEPENDENCIES:** AED-P-001..024, AED-N-001..013, AED-B-001..025 + E2E authority/evidence
- **Implementation scope:** Wire requested FIXED/ADAPTIVE resolution; Host apply; decision evidence; debug snapshot; proxy restrictions; late join current config.
- **Expected code/assets:** PROPOSED integration adapters.
- **Authority/thread/network boundary:** Host only authoritative apply; client no policy authority.
- **Inputs:** resolution request/snapshot
- **Outputs:** applied/nochange/fallback + evidence
- **Failure behavior:** AED unavailable/timeouts fall back exactly by decision point.
- **Required tests:** AED-P-001..024; AED-N-001..013; AED-B integration
- **Evidence required:** 2–4P + evidence logs
- **Definition of Done:** End-to-end Fixed and Adaptive paths validated.
- **Estimated complexity:** L
- **Parallelizable with:** EXP instrumentation
- **Official Milestone Classification:** M4_REQUIRED — official AED Beta scope
- **Accelerated Target Classification:** M2_REQUIRED

## M2-EXP-001 — Experiment run/readiness/provenance foundation

- **Priority:** P1
- **Canonical owner/source:** Fixed_vs_Adaptive_Experiment_Contract_v1.1
- **Reason:** No experiment infra found.
- **Current evidence/status:** NOT_STARTED
- **START_DEPENDENCIES:** Experiment Contract v1.1; test DB/persistence; frozen version semantics and manifest fixtures
- **INTEGRATION_DEPENDENCIES:** Telemetry experiment provenance + AED decision/resolution evidence
- **ACCEPTANCE_DEPENDENCIES:** real MATCH_STARTED experiment evidence + run/version drift/readiness integration tests
- **Implementation scope:** Implement ExperimentRunManifest logical storage; readiness evaluator; MATCH_STARTED protocol/condition provenance integration; run version drift checks.
- **Expected code/assets:** PROPOSED research backend/services; no gameplay command.
- **Authority/thread/network boundary:** Research plane only; condition request goes through existing resolution path.
- **Inputs:** run config/version evidence
- **Outputs:** run/readiness records + match provenance
- **Failure behavior:** contract existence never counts as readiness PASS.
- **Required tests:** EXP-E-001..032 relevant pre-run tests
- **Evidence required:** backend tests + provenance records
- **Definition of Done:** Pilot/readiness instrumentation can prove exact versions/condition.
- **Estimated complexity:** L
- **Parallelizable with:** EXP-002
- **Official Milestone Classification:** M5_REQUIRED / PRE-MAIN — official Fixed-vs-Adaptive experiment/playtest scope
- **Accelerated Target Classification:** M2_REQUIRED

## M2-EXP-002 — Assignment / atomic MAIN enrollment / period binding / fallback classifier

- **Priority:** P1
- **Canonical owner/source:** Experiment v1.1 EXP-DD-01..05
- **Reason:** No assignment/enrollment implementation.
- **Current evidence/status:** NOT_STARTED
- **START_DEPENDENCIES:** Experiment v1.1 assignment/enrollment contract; test DB; schedule/roster fixtures
- **INTEGRATION_DEPENDENCIES:** EXP-001 run-manifest/version integration + authoritative provenance refs when available
- **ACCEPTANCE_DEPENDENCIES:** EXP assignment/lifecycle/enrollment/fallback tests incl. concurrency/crash-retry + immutable ledger evidence
- **Implementation scope:** Implement balanced schedule representation; immutable assignment; MainParticipantEnrollmentIndex; atomic assignment+slot+membership commit; separate PeriodBinding; deterministic fallback classification.
- **Expected code/assets:** PROPOSED research backend services/ledger.
- **Authority/thread/network boundary:** Research state only; no gameplay state transaction.
- **Inputs:** run schedule/roster/AED fallback evidence
- **Outputs:** assignment/binding/fallback classification
- **Failure behavior:** shared-player/slot races one winner; exact retry idempotent; crash safe.
- **Required tests:** EXP-A-*, EXP-A-LIFE-*, EXP-ENR-001..010, EXP-FB-*
- **Evidence required:** concurrency/crash-retry tests
- **Definition of Done:** No slot recycle/participant reuse; period match IDs never mutate assignment.
- **Estimated complexity:** XL
- **Parallelizable with:** EXP-003
- **Official Milestone Classification:** M5_REQUIRED / PRE-MAIN — official Fixed-vs-Adaptive experiment/playtest scope
- **Accelerated Target Classification:** M2_REQUIRED

## M2-EXP-003 — Experiment match/pair eligibility + TeamSurvival/exposure

- **Priority:** P1
- **Canonical owner/source:** Experiment v1.1
- **Reason:** No eligibility/outcome implementation.
- **Current evidence/status:** NOT_STARTED
- **START_DEPENDENCIES:** Experiment eligibility/endpoint contract; canonical match/pair/AED evidence fixtures
- **INTEGRATION_DEPENDENCIES:** EXP-001/002 + TEL-004 real evidence + AED decision evidence
- **ACCEPTANCE_DEPENDENCIES:** real provenance-backed eligibility/TeamSurvival/exposure integration tests
- **Implementation scope:** Implement ExperimentMatchRecord; eligibility; same-roster pair builder; participant uniqueness audit; TeamSurvival extractor; Adaptive exposure classifier; missing/null rules.
- **Expected code/assets:** PROPOSED research backend pure/services.
- **Authority/thread/network boundary:** Research analytics only.
- **Inputs:** accepted evidence refs
- **Outputs:** match/pair/outcome/exposure records
- **Failure behavior:** fallback never relabels A; missing terminal → unavailable; no outcome-dependent exclusion.
- **Required tests:** EXP-C-*, EXP-L-*, EXP-O-*, EXP-X-*, EXP-M-*
- **Evidence required:** pure/backend tests
- **Definition of Done:** Primary endpoint and eligibility deterministic/reproducible.
- **Estimated complexity:** L
- **Parallelizable with:** EXP-004
- **Official Milestone Classification:** M5_REQUIRED / PRE-MAIN — official Fixed-vs-Adaptive experiment/playtest scope
- **Accelerated Target Classification:** M2_REQUIRED

## M2-EXP-004 — Dataset builder / late invalidation / reproduction verifier

- **Priority:** P2
- **Canonical owner/source:** Experiment v1.1
- **Reason:** No dataset infra.
- **Current evidence/status:** NOT_STARTED
- **START_DEPENDENCIES:** Experiment dataset/reproduction contract; synthetic immutable Experiment records
- **INTEGRATION_DEPENDENCIES:** EXP-003 real records + Profile correction/late-invalidation evidence
- **ACCEPTANCE_DEPENDENCIES:** EXP-D reproduction/late-invalidation tests + deterministic dataset fingerprint evidence
- **Implementation scope:** Implement versioned immutable dataset manifest, cutoff semantics, deterministic rebuild before cutoff, new revision after cutoff, reproduction fingerprint.
- **Expected code/assets:** PROPOSED research backend/tooling.
- **Authority/thread/network boundary:** Offline/research; no gameplay authority.
- **Inputs:** experiment records + evidence refs
- **Outputs:** dataset manifest/version/fingerprint
- **Failure behavior:** manual row edits cannot become authoritative; late invalidation audited.
- **Required tests:** EXP-D-*
- **Evidence required:** dataset reproduction tests
- **Definition of Done:** Same frozen evidence → same dataset.
- **Estimated complexity:** L
- **Parallelizable with:** OBS/CI
- **Official Milestone Classification:** M5_REQUIRED / PRE-MAIN — official Fixed-vs-Adaptive experiment/playtest scope
- **Accelerated Target Classification:** M2_TARGET

## M2-TST-001 — Unity project test assemblies + contract test harness

- **Priority:** P0
- **Canonical owner/source:** All AI contracts
- **Reason:** No project-owned tests found.
- **Current evidence/status:** NOT_STARTED
- **START_DEPENDENCIES:** approved Unity 6000.5.8f1 toolchain usable; repository importable enough to create/run tests; canonical contracts
- **INTEGRATION_DEPENDENCIES:** component implementations as they become available
- **ACCEPTANCE_DEPENDENCIES:** headless/editor EditMode/PlayMode smoke + canonical contract suites runnable on team machine
- **Implementation scope:** Create EditMode/PlayMode asmdefs/test assemblies; fixture builders; deterministic fake clock/tick/IDs; reusable contract test utilities without semantic leakage. This may begin as soon as the approved Unity toolchain can open/import the project; FND-001 completion is not a prerequisite.
- **Expected code/assets:** PROPOSED `Assets/Tests/EditMode`, `Assets/Tests/PlayMode` or coherent existing convention.
- **Authority/thread/network boundary:** Tests only.
- **Inputs:** contract fixtures
- **Outputs:** runnable Unity tests
- **Failure behavior:** harness failure blocks VERIFIED status.
- **Required tests:** smoke + first canonical STK/LIS/WAR tests
- **Evidence required:** test runner output
- **Definition of Done:** Canonical tests can run headless/editor on team machine.
- **Estimated complexity:** L
- **Parallelizable with:** component implementation
- **Official Milestone Classification:** M2_REQUIRED — prototype smoke/regression infrastructure
- **Accelerated Target Classification:** M2_REQUIRED

## M2-TST-002 — Fusion 2–4P automated/manual network harness

- **Priority:** P0
- **Canonical owner/source:** AI Architecture + Monster/AED network contracts
- **Reason:** No project network test harness.
- **Current evidence/status:** NOT_STARTED
- **START_DEPENDENCIES:** Fusion 2.1.1 toolchain; importable multiplayer project; network-test scene/launcher scaffolding
- **INTEGRATION_DEPENDENCIES:** FND-003 real authority layer + Monster/Scenario network components
- **ACCEPTANCE_DEPENDENCIES:** 2/3/4P authority/proxy/late-join/retry evidence for real components
- **Implementation scope:** Create repeatable Host+clients harness or controlled multi-instance workflow; capture authority/state/latejoin/retry evidence.
- **Expected code/assets:** PROPOSED test scene/build/harness; keep production Build Settings clean.
- **Authority/thread/network boundary:** Test environment only.
- **Inputs:** network build/config
- **Outputs:** 2/3/4P evidence
- **Failure behavior:** harness failure never reclassified as feature PASS.
- **Required tests:** STK-N/LIS-N/WAR-N/AED-N umbrella execution
- **Evidence required:** logs/videos/network stats
- **Definition of Done:** Required network contract tests executable and archived.
- **Estimated complexity:** XL
- **Parallelizable with:** Monster network integration
- **Official Milestone Classification:** SPLIT: M2_REQUIRED for prototype 2–4P smoke workflow; M3_REQUIRED for dedicated latency/diagnostics harness
- **Accelerated Target Classification:** M2_REQUIRED

## M2-TST-003 — Backend test project + data/concurrency harness

- **Priority:** P0
- **Canonical owner/source:** Telemetry/Profile/AED/Experiment backend contracts
- **Reason:** No backend test project exists.
- **Current evidence/status:** NOT_STARTED
- **START_DEPENDENCIES:** approved .NET 8 toolchain; backend project restore; data contracts/test fixtures
- **INTEGRATION_DEPENDENCIES:** TEL/PRO/EXP backend components as they become available
- **ACCEPTANCE_DEPENDENCIES:** backend schema/idempotency/replay/concurrency suites executable and evidenced
- **Implementation scope:** Create .NET test project; deterministic DB fixture; migration lifecycle; concurrency/retry/failure injection; API tests.
- **Expected code/assets:** PROPOSED `EchoProtocol.Backend/tests/EchoProtocol.Api.Tests` or equivalent.
- **Authority/thread/network boundary:** Tests only.
- **Inputs:** backend services/DB
- **Outputs:** repeatable unit/integration test suite
- **Failure behavior:** test DB isolated; no production data mutation.
- **Required tests:** TEL-I, PRO-B/S, AED-B, EXP-ENR/D families
- **Evidence required:** dotnet test output
- **Definition of Done:** Backend contracts can be VERIFIED with repeatable tests.
- **Estimated complexity:** L
- **Parallelizable with:** backend implementations
- **Official Milestone Classification:** SPLIT: M2_REQUIRED for backend/telemetry smoke; M4_REQUIRED for full Profile/AED/data concurrency suites
- **Accelerated Target Classification:** M2_REQUIRED

## M2-TST-004 — Cross-system E2E acceptance harness

- **Priority:** P0
- **Canonical owner/source:** All canonical contracts + official M2 2–4P goal
- **Reason:** Game scene/core loop integration not evidenced.
- **Current evidence/status:** NOT_STARTED
- **START_DEPENDENCIES:** canonical E2E scenario contracts + harness scaffolding + fixture providers
- **INTEGRATION_DEPENDENCIES:** Monster/Data/Scenario/Fusion core + gameplay core-loop/life-state/objective integration
- **ACCEPTANCE_DEPENDENCIES:** authoritative gameplay→Telemetry→Profile→AED→Scenario→evidence run with required critical tests
- **Implementation scope:** Build controlled Research Facility E2E flow; Fixed and Adaptive fixtures; evidence capture; 2–4P matrix; no soft-lock.
- **Expected code/assets:** PROPOSED acceptance scene/build workflow reusing actual Game scene once integrated.
- **Authority/thread/network boundary:** Host authority end-to-end.
- **Inputs:** full build/backend/test config
- **Outputs:** acceptance evidence bundle
- **Failure behavior:** any critical authority/safety/data failure blocks acceptance of the scope that consumes this full E2E slice; it blocks the accelerated gate.
- **Required tests:** M2-E2E-001 umbrella referencing canonical tests; official T-001/T-002/T-008 etc
- **Evidence required:** video/logs/test outputs/backend records
- **Definition of Done:** Full core pipeline observable and reproducible.
- **Estimated complexity:** XL
- **Parallelizable with:** OBS/TUN
- **Official Milestone Classification:** SPLIT: M2_REQUIRED for official prototype E2E slice; M4_REQUIRED for expanded all-system acceptance
- **Accelerated Target Classification:** M2_REQUIRED

## M2-OBS-001 — Cross-system read-only debug/evidence surfaces

- **Priority:** P1
- **Canonical owner/source:** All detailed contracts
- **Reason:** Stalker debug exists; cross-system observability absent.
- **Current evidence/status:** PARTIAL
- **START_DEPENDENCIES:** read-only observability contracts/interfaces and debug fixture models
- **INTEGRATION_DEPENDENCIES:** component runtime implementations
- **ACCEPTANCE_DEPENDENCIES:** required subsystem diagnostics/evidence captured without gameplay mutation
- **Implementation scope:** Implement typed read-only debug snapshots/logging; scene gizmos; event/profile/AED evidence correlations; no debug mutation controls.
- **Expected code/assets:** ADAPT Stalker debug; PROPOSED subsystem debug providers.
- **Authority/thread/network boundary:** Read-only.
- **Inputs:** runtime/data state
- **Outputs:** debug snapshots/evidence
- **Failure behavior:** debug failure cannot affect gameplay.
- **Required tests:** observability assertions in component tests
- **Evidence required:** screens/logs + source
- **Definition of Done:** P0/P1 failures diagnosable without hidden state mutation.
- **Estimated complexity:** M
- **Parallelizable with:** all workstreams
- **Official Milestone Classification:** SPLIT: M2 basic Stalker/prototype diagnostics; M4 full Monster/AED observability
- **Accelerated Target Classification:** M2_REQUIRED

## M2-TUN-001 — Profiling baseline + tuning registry handoff

- **Priority:** P2
- **Canonical owner/source:** Performance/tuning ownership in contracts
- **Reason:** No profiler/tuning evidence supplied.
- **Current evidence/status:** NOT_STARTED
- **START_DEPENDENCIES:** profiling checklist + versioned tuning/config registries
- **INTEGRATION_DEPENDENCIES:** feature implementations sufficiently integrated to measure
- **ACCEPTANCE_DEPENDENCIES:** profiler captures + tuning handoff evidence; no fabricated final values
- **Implementation scope:** Capture baseline profiler data; classify correctness config vs balance tuning; maintain TUNING_REQUIRED register; do not freeze final values here.
- **Expected code/assets:** Profiler captures + versioned config assets.
- **Authority/thread/network boundary:** No authority change.
- **Inputs:** integrated build
- **Outputs:** profiling/tuning evidence
- **Failure behavior:** performance blocker escalated; balance values remain non-final until evidence.
- **Required tests:** profiling checklist/manual playtest
- **Evidence required:** profiler captures + config diffs
- **Definition of Done:** Known bottlenecks and tuning TBDs explicitly owned.
- **Estimated complexity:** M
- **Parallelizable with:** integration
- **Official Milestone Classification:** SPLIT: M3 Stalker fairness tuning; M5 full 3-Monster/AED/performance tuning
- **Accelerated Target Classification:** TUNING_AFTER_EVIDENCE

---

# 42. Contract Test Traceability Matrix

Hard precedence:

```text
Canonical Test Contract
>
Implementation Plan shorthand
```

If a later locked contract adds or changes a canonical test, affected plan traceability must be updated before the work package can be accepted. A missing ID/range in this plan never removes the underlying contract requirement.

Current canonical regression ranges explicitly confirmed by this plan:

```text
Stalker:
STK-E-001..030
STK-P-001..044
STK-N-001..016

Listener:
LIS-E-001..055
LIS-P-001..057
LIS-N-001..020

Warden:
WAR-E-001..071
WAR-P-001..040
WAR-N-001..017

Telemetry:
TEL-P-001..024
TEL-N-001..015

Profile:
PRO-E-001..062
PRO-B-001..029
PRO-S-001..028

AED:
AED-E-001..075
AED-R-001..010
AED-P-001..024
AED-N-001..013
AED-B-001..025
```

Telemetry/Experiment wildcard namespaces such as `TEL-E-*`, `EXP-E-*`, `EXP-FB-*`, `EXP-ENR-*` remain wildcarded where the canonical contract intentionally uses several independent test-ID namespaces.

| Work Item / Area | Canonical Test IDs | Planned Test Placement | Current Evidence | Required Before Done |
|---|---|---|---|---|
| Stalker core | `STK-E-001..030`, applicable `STK-P-001..044` | PROPOSED Unity EditMode/PlayMode suites | none | required subset executes/pass for implemented behavior |
| Stalker network | `STK-N-001..016` | PROPOSED Fusion harness | none | Host/proxy/latejoin/exactly-once evidence |
| Listener core | `LIS-E-001..055`, `LIS-P-001..057` | PROPOSED Unity suites | none | runtime-noise/hearing/FSM/action tests pass |
| Listener network | `LIS-N-001..020` | PROPOSED Fusion harness | none | Host/proxy/latejoin evidence |
| Warden pure | `WAR-E-001..071` | PROPOSED EditMode/pure graph suite | none | graph/safety/pressure/full-set semantics pass |
| Warden PlayMode/network | `WAR-P-001..040`, `WAR-N-001..017` | PROPOSED PlayMode/Fusion suite | none | telegraph/apply/release/failsafe/replication pass |
| Telemetry | `TEL-E-*`, `TEL-I-001..020`, `TEL-P-001..024`, `TEL-N-001..015` | PROPOSED Unity + backend suites | none | wire/identity/order/idempotency/integration verified |
| Telemetry enum/attack/Warden corrections | `TEL-E-ENUM-01..07`, `TEL-E-ATK-01..07`, `TEL-E-WAR-01..08` | backend validator + source adapters | none | corrected event semantics exact |
| Profile pure | `PRO-E-001..062` | PROPOSED backend pure tests | none | formula/idempotency/replay/retraction semantics pass |
| Profile backend | `PRO-B-001..029` | PROPOSED backend integration tests | none | concurrency/restart/replay converge |
| Profile snapshot | `PRO-S-001..028` | PROPOSED backend snapshot tests | none | semantic compatibility/staleness/immutability pass |
| AED pure | `AED-E-001..075`, `AED-R-001..010` | PROPOSED pure/config/route tests | none | gate/rules/registry/base/fallback semantics pass |
| AED PlayMode/network/backend | `AED-P-001..024`, `AED-N-001..013`, `AED-B-001..025` | Unity/Fusion/backend suites | none | authoritative apply/idempotency/stale guards pass |
| Experiment run/assignment | `EXP-E-*`, `EXP-A-*`, `EXP-A-LIFE-*`, `EXP-ENR-001..010` | PROPOSED backend research tests | none | immutable/atomic assignment semantics pass |
| Experiment fallback/eligibility | `EXP-FB-*`, `EXP-C-*`, `EXP-L-*`, `EXP-O-*`, `EXP-X-*` | PROPOSED backend pure/integration tests | none | A assignment/exposure/eligibility exact |
| Experiment outcomes/dataset | `EXP-M-*`, `EXP-D-*`, `EXP-SAF-*` where activated | PROPOSED backend/research tests | none | Team Survival/data rebuild/safety evidence exact |

Plan-level umbrella IDs such as `M2-E2E-001`, `M2-NET-001`, or `M2-ACC-001` may aggregate execution, but they must reference the owning canonical IDs rather than replace them.

---

# 43. M2 Implementation Evidence Matrix

| Workstream | Contract | Implementation Evidence | Test Evidence | Scene/Config Evidence | Network Evidence | Current Status |
|---|---|---|---|---|---|---|
| Shared foundation | Architecture | room/Fusion base only | none | NetworkRunner prefab | session Host/Client only | PARTIAL |
| Stalker | Stalker v1.1 | 15 source files, FSM/spatial spike | none | two AI scenes, one NavMesh asset | none | MIGRATION_REQUIRED |
| Listener | Listener v1.0 | none | none | none | none | NOT_STARTED |
| Warden | Warden v1.0 | none | none | none | none | NOT_STARTED |
| Map/Spatial authoring | Architecture/Stalker/Warden | NavMesh graph spike | none | no Region/Facility assets | n/a | PARTIAL |
| Telemetry | Telemetry v1.1 | none | none | none | none | NOT_STARTED |
| Profile | Profile v1.1 | account PlayerProfile only; not canonical AI Profile | none | none | n/a | NOT_STARTED |
| AdaptiveInputSnapshot | Profile v1.1 | none | none | none | n/a | NOT_STARTED |
| Fixed/Scenario | AED Scenario v1.1 | none | none | no config assets found | none | NOT_STARTED |
| AED | AED v1.1 | none | none | no AED configs | none | NOT_STARTED |
| Experiment instrumentation | Experiment v1.1 | none | none | none | n/a | NOT_STARTED |
| Automated tests | all | no project-owned suites | none | n/a | none | NOT_STARTED |
| CI | project | none | none | docker compose only | n/a | NOT_STARTED |
| Observability | detailed designs | Stalker serialized debug fields/gizmo only | none | AI graph debug scene | none | PARTIAL |

---

# 44. Config / Asset Register

| Artifact | Owner | Current Evidence | Required for M2 | Classification |
|---|---|---|---|---|
| Stalker fixed patrol route | Stalker | scene/component exists | yes as fallback/test | EXISTING |
| Stalker NavMesh data | Map/Stalker | `AI_Stalker_Spike/NavMesh-Navigation.asset` | yes | EXISTING_UNVERIFIED |
| RegionDefinition set | Stalker/map | not found | yes | MAP AUTHORING |
| SpatialGraph→Region bake/map | Stalker/map | not found | yes | REQUIRED FOR M2 |
| FacilityGraphDefinition | Warden/map | not found | yes | MAP AUTHORING |
| WardenRouteLockDefinitions | Warden/map | not found | yes | MAP AUTHORING |
| DoorDefinition/Jammer capability | gameplay/Warden | not found in AI assets | yes | MAP AUTHORING / CROSS-TEAM |
| RequiredRouteObligations | Warden/objectives | not found | yes | REQUIRED FOR M2 |
| Telemetry runtime config | Telemetry | not found | yes | REQUIRED FOR M2 |
| Profile formula/version config | Profile | not found | yes | REQUIRED FOR M2 |
| Fixed ScenarioConfig baseline | Scenario | not found | yes | REQUIRED FOR M2 |
| AEDEvidencePolicy | AED | not found | yes | REQUIRED FOR M2; numeric tuning later |
| AEDPolicyConfig | AED | not found | yes | REQUIRED FOR M2; band values tuning later |
| AdaptiveParameterRegistry | AED | not found | yes | REQUIRED FOR M2; candidate values tuning later |
| Content whitelist | Scenario/AED | not found | yes | REQUIRED FOR M2 |
| ExperimentRunManifest template/schema | Experiment | not found | research alpha | M2 REQUIRED instrumentation |
| MAIN allocation schedule | Experiment | not applicable yet | no | POST-M2 / PRE-MAIN |
| final analysisPlanVersion | Experiment research | not frozen | no | POST-M2 / PRE-MAIN |

---

# 45. Migration Register

| Current Path / Component | Action | Target | Reason | Migration Risk | Regression Guard |
|---|---|---|---|---|---|
| `StalkerController.cs` monolith | ADAPT | thinner authoritative orchestration + typed FSM/memory/planner/action components | canonical ownership/testability | High | STK-E/P/N |
| `StalkerVisionSensor.cs` single candidate | REPLACE/ADAPT | physical visibility observation over eligible target set | multiplayer/canonical separation | High | target/LOS/detection tests |
| `StalkerBlackboard.cs` spatial IDs only | ADAPT | keep spatial fields where useful; move semantic memory to StalkerMemory | canonical typed memory | Medium | memory/state tests |
| `SpatialPatrolPlanner.cs` node patrol | ADAPT | retained as local/spatial primitive if compatible beneath RegionGraph | useful spike not final architecture | Medium | spatial/region tests |
| `ConfidenceSpatialPatrolPlanner.cs` unused | KEEP TEMPORARILY then ADAPT/DELETE_AFTER_MIGRATION | extract compatible scoring concepts only if canonical | dead/unwired prototype | Medium | patrol regression |
| `StalkerPatrolMode.ConfidenceSpatial` | ADAPT | explicit canonical mode or remove legacy enum after migration | currently falls through to fixed | Low | mode selection tests |
| `StalkerNavigationController.cs` | ADAPT | typed progress/failure/recovery | canonical navigation ownership | Medium | PlayMode nav tests |
| account `Entities/PlayerProfile.cs` | KEEP | account/profile-stat domain remains separate from AI PlayerAIProfile | avoid semantic collision | High if conflated | schema/API tests |
| Auth/backend services | KEEP | extend backend beside them | currently useful foundation | Low | existing auth smoke/regression |
| Bootstrap/Lobby room foundation | KEEP/ADAPT | add gameplay network lifecycle without rewriting auth/lobby unnecessarily | working base | Medium | room/network smoke |
| AI spike scenes | KEEP AS TEST ASSET | migrate/resave bindings; do not make them production Game scene automatically | useful isolated validation | Low | scene validation |

No mass folder reorganization is required. Extend the existing `Assets/Scripts/AI/Stalker` structure and add Listener/Warden/data folders only as needed.

---

# 46. PR / Changeset Strategy

Recommended logical PR boundaries; adjust to actual dependencies, but avoid one giant PR:

| PR | Scope | Must be green before merge |
|---|---|---|
| PR-01 | test/build baseline + shared IDs/authority contracts | compile + smoke tests |
| PR-02 | Stalker perception/memory/FSM migration | STK EditMode/PlayMode subset |
| PR-03 | Stalker RegionGraph/patrol/search/navigation | spatial/search tests |
| PR-04 | Stalker attack + Fusion binding | exactly-once + network tests |
| PR-05 | RuntimeNoise foundation | noise/dedup tests |
| PR-06 | Listener hearing/FSM/actions | Listener EditMode/PlayMode |
| PR-07 | Listener Fusion/telemetry | Listener network/data tests |
| PR-08 | FacilityGraph/Warden safety | WAR pure graph/safety tests |
| PR-09 | Warden runtime/Fusion/telemetry | WAR PlayMode/Fusion |
| PR-10 | Telemetry Unity core/producers | TEL unit/producer tests |
| PR-11 | Telemetry backend | backend validator/idempotency tests |
| PR-12 | Profile processing/persistence | PRO-E/B tests |
| PR-13 | Profile snapshots/AED handoff | PRO-S tests |
| PR-14 | ScenarioConfig/FixedDirector/Validator | AED pure/route tests |
| PR-15 | Scenario Host apply + AED | AED PlayMode/Fusion/backend |
| PR-16 | Experiment instrumentation | EXP assignment/fallback/eligibility tests |
| PR-17 | cross-system acceptance/observability/CI | E2E + 2–4P evidence |

Every PR should compile and should not leave a knowingly unsafe half-feature active. Use feature flags/config gating when an incomplete component would otherwise become reachable.

Dependency-level rule for PR planning:

- PR-12/13 Profile pure portions may open against canonical Telemetry/Profile fixtures before live TEL-004 integration; merge acceptance for production wiring still requires real accepted evidence.
- PR-14/15 Scenario/AED pure validator/policy portions may open against fixtures before live AdaptiveInputSnapshot; live AED acceptance still waits for PRO-005 + SCN Host apply.
- PR-16 experiment manifest/assignment/enrollment portions may open with test persistence and canonical fixtures before live Telemetry/AED provenance; production research acceptance waits for real evidence.
- PR-01 must not encode `FND-001 DONE → only then create TST-001`; build baseline and minimum smoke harness may be developed in overlapping commits once the project opens.

---

# 47. CI / Automation

Current CI evidence: none.

Minimum proposed M2 automation:

```text
PR/push
→ backend restore/build/test
→ Unity batchmode compile/EditMode tests
→ Unity PlayMode contract subset where stable
→ schema/contract fixture tests
→ deterministic pure tests
→ artifact/log retention
```

Fusion multi-instance tests may remain a separately triggered harness if CI infrastructure cannot host them reliably, but the execution steps and evidence artifact format must be scripted/repeatable.

Do not claim CI exists until the workflow is committed and demonstrated.

---

# 48. Manual Test Scenarios

Automated tests are primary for deterministic contracts; manual validation remains required for gameplay integration.

| Scenario | Players | Key Checks | Evidence |
|---|---:|---|---|
| Stalker sight cycle | 2–4 | PATROL→DETECT→CHASE→SEARCH; no hidden follow | video + AI debug |
| Stalker attack | 2–4 | one hit resolution, correct life-state, no duplicate | video + logs |
| Stalker region patrol | 2–4 | coverage/global/local movement, path recovery | graph overlay + logs |
| Listener competing noises | 2–4 | deterministic selected observation/pending behavior | noise/hearing debug |
| Listener false investigation | 2–4 | terminal cleanup/no resurrection | episode log |
| Listener chase corroboration | 2–4 | self-noise does not create hidden identity shortcut | debug trace |
| Warden safe route lock | 2–4 | telegraph + alternative route + full footprint | route overlay/video |
| Warden + Door Jammer | 2–4 | overlays coexist per contract | route/debug log |
| Warden fail-safe | 2–4 | invalid active lock releases exactly once | fail-safe log |
| Fixed Scenario | 2–4 | fixed baseline without Profile/AED | config log |
| Adaptive APPLIED | 2–4 | exact policy rule/registered step/validator/apply | decision/config evidence |
| Adaptive NO_CHANGE | 2–4 | no new config version/source relabel | decision evidence |
| Adaptive fallback | 2–4 | fallback by point; match remains playable | decision/config evidence |
| late join | 2–4 | current Monster/Warden/config state reconstructed; no side-effect replay | network capture |
| full Research Facility | 2–4 | objective progression + AI + no soft-lock/desync | end-to-end video/logs |

---

# 49. Risk Register

| Risk | Probability | Impact | Detection | Mitigation | Fallback | Owner | Blocks |
|---|---|---|---|---|---|---|---|
| Requested M2 scope exceeds official M2 baseline | High | Critical | work package burn-down vs 2026-09-20 | explicit PM rebaseline, parallelize, protect P0 correctness | keep official M2 prototype gate while broader work continues if governance rejects acceleration | Project/AI lead | broader Feature-Complete Alpha |
| Existing Stalker spike migration complexity | High | High | failing STK regression / coupling | incremental split, preserve working spike until replacement tested | keep isolated spike scene for diagnosis | AI | Stalker |
| Region/map binding drift | High | High | authoring validator | stable IDs/versioned bake | block feature rather than guess | AI+Map | Stalker/Warden |
| Warden map-authoring dependency | High | Critical | missing FacilityGraph/door/objective IDs | authoring work before visible locks | Warden remains disabled | AI+Map | Warden |
| RuntimeNoise accidentally coupled to telemetry | Medium | Critical | architecture/test review | separate types/pipelines, tests | disable Listener rather than read telemetry DB | AI | Listener |
| Fusion authority mistake | High | Critical | 2–4P/proxy mutation tests | thin State Authority boundary | fail closed/no proxy apply | AI+Network | all runtime AI/AED |
| Attack duplicate side effect | Medium | Critical | episode/network retry tests | stable episode ID + resolved guard | suppress duplicate callback | AI+Gameplay | Stalker/Listener |
| Telemetry order/identity defect | High | Critical | TEL conflict/retry tests | global sequence allocator + immutable identity | quarantine/retry; no Profile use | AI/Data | Profile/AED/research |
| Backend retry/idempotency bug | Medium | Critical | concurrent integration tests | unique identities + transactions/CAS | reject conflict; rebuild | Backend/Data | Telemetry/Profile/Experiment |
| Profile replay/retraction complexity | High | High | PRO-B crash/replay tests | append/audit observations + canonical replay | mark lineage rebuild-invalid | Data | AED |
| AED version/config ownership drift | Medium | High | AED semantic mirror/config tests | separate Definition/Evidence/Tuning owners | Fixed fallback | AI/Data | Adaptive |
| ScenarioConfig network synchronization | High | Critical | Fusion CAS/latejoin tests | durable Host-owned current config | Fixed current config | Network/AI | AED |
| No project tests/CI | High | Critical | current audit | test scaffolding in Wave 0/parallel | no VERIFIED claims | all | official prototype verification + accelerated acceptance |
| Performance regression | Medium | High | profiler/4P soak | profile hotspots early | tuning/optimization after correctness | AI+Network | playable alpha |
| Cross-team Game scene/life-state/objective incomplete | High | Critical | Game scene integration gate | explicit dependency interface + early E2E slice | isolated subsystem tests only, no acceptance | Gameplay/Network | AI integration |

---

# 50. M2 Calendar / Wave Plan

The official M2 window remains `2026-08-25 → 2026-09-20` from the approved Project Plan.

The rows below are explicitly **Accelerated Dependency Targets** for `ACCELERATED_FEATURE_COMPLETE_ALPHA`. They are not evidence that Listener/Warden/Profile/AED were historically committed to official M2, and they are not a capacity/feasibility guarantee.

| Date Range | Goal | Required Work Packages / Tracks | Exit Criteria | Risk Buffer / Note |
|---|---|---|---|---|
| 25–30 Aug | Wave 0 + shared foundations + Stalker migration start | FND-001 and TST-001/TST-003 bootstrap **overlap**; FND-002..004, STK-001; TEL/PRO fixture scaffolding | aggregate Wave 0 build + Unity/backend smoke gate; Stalker migration branch compiling | accelerated dependency target; do not serialize fixture-based PRO behind live TEL |
| 31 Aug–06 Sep | Stalker canonical core + RuntimeNoise + telemetry core | STK-002..005, LIS-001/002, TEL-001/003, FND-005 | Stalker no-cheat/search/attack path testable; runtime noise + telemetry skeleton | official plan expected Stalker only; scope acceleration starts here |
| 07–13 Sep | Listener core + Warden graph safety + Profile + Scenario Fixed | LIS-003..005, WAR-001..003, PRO-001..003, SCN-001..003 | Listener isolated playable; Warden pure safety green; Profile replay tests; Fixed path testable | high integration risk; map authoring must be ready |
| 14–17 Sep | Warden runtime + Profile snapshot + AED + Host apply | WAR-004/005, PRO-004/005, SCN-004, AED-001..003, TEL-002/004 | all three Monsters + data path integration candidates | retain rollback/feature gating; no final tuning |
| 18–20 Sep | Integration / 2–4P / regression / review | AED-004, EXP-001..003 target, TST-002..004, OBS-001 | critical tests executed; acceptance blockers enumerated; playable integrated build if capacity allows | final days reserved for fixes/evidence, not new core features |

### Calendar governance

If P0 implementation cannot fit this window without skipping safety/tests, do not weaken canonical contracts. Escalate the delivery scope/date to project management. The official baseline places Listener/Warden and Player/Team Modeling/AED feature expansion/completion in M4 and Fixed-vs-Adaptive experiment work in M5. Correctness wins over pretending this accelerated target is an approved/feasible official M2 commitment. PM rebaseline is required before the accelerated scope becomes the formal M2 gate.

---

# 51. Scope-Cut Policy

If capacity is exceeded, cut in this order:

1. visual/debug polish beyond minimum observability;
2. optional dashboards;
3. optional secondary experiment metrics;
4. GenAI briefing work;
5. non-essential balance tuning;
6. advanced experiment dataset/report convenience tooling;
7. profiler polish after blocking performance issues are understood.

Do **not** cut:

- Host authority;
- Monster core contract behavior;
- no-cheat semantics;
- exactly-once side effects;
- Warden objective/Exit safety;
- Telemetry identity/order/idempotency;
- Profile correction/replay needed by AED;
- FixedDirector and fallback;
- ScenarioValidator;
- critical automated regression tests;
- map bindings required for Monster correctness.

---

# 52. M2 Acceptance Matrix

This matrix separates the formal approved Project Plan gate from the accelerated target.

| Area | Contract Ready | Implementation | Tests | Integration | Official Milestone Classification | Accelerated M2 Target | Official M2 Gate Status | Accelerated Gate Status | Current Blockers |
|---|---|---|---|---|---|---|---|---|---|
| Shared Foundation | READY | PARTIAL | NOT RUN | PARTIAL | M2_REQUIRED | M2_REQUIRED | NOT READY | NOT READY | gameplay NetworkBehaviour + build/test evidence |
| Stalker | READY | MIGRATION_REQUIRED | NOT RUN | spike only | M2_REQUIRED | M2_REQUIRED | NOT READY | NOT READY | memory/search/attack/region/Fusion |
| Listener | READY | NOT_STARTED | NOT RUN | none | M4_REQUIRED | M2_REQUIRED | NOT APPLICABLE TO OFFICIAL M2 | NOT READY | RuntimeNoise + full runtime |
| Warden | READY | NOT_STARTED | NOT RUN | none | M4_REQUIRED | M2_REQUIRED | NOT APPLICABLE TO OFFICIAL M2 | NOT READY | FacilityGraph/map binding/safety |
| Multiplayer AI Authority | READY | PARTIAL room only | NOT RUN | none for Monsters | M2_REQUIRED | M2_REQUIRED | NOT READY | NOT READY | player/Monster network entities |
| Map/Spatial Authoring | READY strategy | PARTIAL NavMesh spike | NOT RUN | incomplete | M2 Stalker/RF slice; M4 Warden FacilityGraph slice | M2_REQUIRED | NOT READY for official Stalker slice | NOT READY | Region/Facility/door/objective assets |
| Telemetry | READY | NOT_STARTED | NOT RUN | none | M2 basic emitter/storage; M3 phase metrics; M4 expanded modeling support | M2_REQUIRED | NOT READY | NOT READY | Unity+backend pipeline |
| Profile | READY | NOT_STARTED | NOT RUN | none | M4_REQUIRED | M2_REQUIRED | NOT APPLICABLE TO OFFICIAL M2 | NOT READY | telemetry + persistence/replay |
| AdaptiveInputSnapshot | READY | NOT_STARTED | NOT RUN | none | M4_REQUIRED | M2_REQUIRED | NOT APPLICABLE TO OFFICIAL M2 | NOT READY | Profile snapshot pipeline |
| FixedDirector | READY | NOT_STARTED | NOT RUN | none | M4_REQUIRED | M2_REQUIRED | NOT APPLICABLE TO OFFICIAL M2 | NOT READY | ScenarioConfig baseline |
| ScenarioValidator | READY | NOT_STARTED | NOT RUN | none | M4_REQUIRED | M2_REQUIRED | NOT APPLICABLE TO OFFICIAL M2 | NOT READY | route/spawn adapters |
| ScenarioConfig Apply | READY | NOT_STARTED | NOT RUN | none | M4_REQUIRED | M2_REQUIRED | NOT APPLICABLE TO OFFICIAL M2 | NOT READY | Fusion authority/CAS |
| AED | READY | NOT_STARTED | NOT RUN | none | M4_REQUIRED | M2_REQUIRED | NOT APPLICABLE TO OFFICIAL M2 | NOT READY | snapshot + scenario foundation |
| Experiment Instrumentation | READY | NOT_STARTED | NOT RUN | none | M5_REQUIRED / PRE-MAIN | M2_REQUIRED core instrumentation; MAIN analysis remains POST_M2/PRE_MAIN | NOT APPLICABLE TO OFFICIAL M2 | NOT READY | run/provenance/assignment/eligibility implementation |
| Observability | READY | PARTIAL Stalker | NOT RUN | limited | M2 basic prototype diagnostics; M4 full AI/AED | M2_REQUIRED | PARTIAL / evidence incomplete | NOT READY | subsystem debug/evidence |
| Automated Tests | READY contracts | NOT_STARTED | NOT RUN | none | M2 prototype smoke/regression; M4 full system-contract verification | M2_REQUIRED | NOT READY | NOT READY | test projects/harness |
| Profiling | READY methodology | NOT_STARTED | none | none | M3 Stalker tuning + M5 full performance/tuning | TUNING_AFTER_EVIDENCE | NOT APPLICABLE AS FULL PROFILING GATE | NOT READY | integrated build |

Interpretation guard:

```text
incomplete Listener
→ does NOT fail current OFFICIAL_BASELINE M2 by itself
→ DOES fail ACCELERATED_FEATURE_COMPLETE_ALPHA
```

The same distinction applies to Warden/Profile/AED according to their actual official M4 placement.

---

# 53. Final Feature-Complete Checklist

Nothing below is checked because supplied evidence does not prove completion.

## Build / foundation

- [ ] Approved Unity project compiles/imports cleanly.
- [ ] Backend restores/builds on .NET 8.
- [ ] Project-owned Unity EditMode/PlayMode tests execute.
- [ ] Backend test project executes.
- [ ] Host/State Authority gameplay boundary exists and is tested.

## Stalker

- [ ] Six-state canonical FSM operational under Host authority.
- [ ] Multi-player physical vision + target eligibility/selection implemented.
- [ ] DetectionTarget/CurrentTarget/LKP/LastSeenDirection memory exact.
- [ ] RegionGraph/CoverageMemory/global-local patrol operational.
- [ ] SEARCH candidate/terminal/no-cheat contract operational.
- [ ] Navigation failure/stuck/recovery operational.
- [ ] AttackEpisode exactly-once + Player Life-State hook operational.
- [ ] Stalker Fusion/late-join/telemetry/debug evidence complete.

## Listener

- [ ] RuntimeNoiseEvent/NoiseSystem authoritative pipeline operational.
- [ ] HearingObservation/pending lifecycle bounded and immutable.
- [ ] Five-state FSM + deterministic same-tick arbitration operational.
- [ ] InvestigationEpisode merge/interruption/cleanup exact.
- [ ] CHASE corroboration/no-hidden-source semantics exact.
- [ ] Attack/Recover/navigation operational.
- [ ] Listener Fusion/telemetry/debug evidence complete.

## Warden

- [ ] FacilityGraph/door/route-lock authored and validated.
- [ ] Required objective/Exit route obligations operational.
- [ ] WardenSafetyValidator/RoutePressure/full-set candidate policy operational.
- [ ] Telegraph/precommit/atomic lock/release/postapply/failsafe operational.
- [ ] Door Jammer coexistence tested.
- [ ] Warden Fusion/telemetry/debug evidence complete.

## Data / Adaptive

- [ ] Telemetry v1.1 runtime + backend + completeness operational.
- [ ] Profile active SURVIVAL/NOISE pipeline operational.
- [ ] Dimension apply/replay/retraction/cold-start semantics tested.
- [ ] PlayerProfileSnapshot/RosterProfileSummary/semantic compatibility operational.
- [ ] AdaptiveInputSnapshot current/stale semantics operational.
- [ ] ScenarioConfig typed foundation + fixed baseline operational.
- [ ] FixedDirector independently operational.
- [ ] ScenarioValidator cannot be bypassed.
- [ ] Host applied ScenarioConfig/CAS/late join operational.
- [ ] `AED_SCENARIO_POLICY_V1_1` gate/rules/registry/fallback/no-change operational.

## Research / evidence

- [ ] Experiment run/provenance instrumentation operational.
- [ ] Atomic assignment/enrollment + period binding testable.
- [ ] Fallback classification/TeamSurvival extraction deterministic.
- [ ] Dataset/reproduction foundations implemented to chosen M2 classification.
- [ ] Main experiment remains separately gated by tuning/analysis/sample-size/readiness.

## Verification

- [ ] Required canonical EditMode/Pure tests pass.
- [ ] Required PlayMode tests pass.
- [ ] Required 2–4P Fusion tests pass.
- [ ] Required backend/data tests pass.
- [ ] Cross-system Fixed path passes.
- [ ] Cross-system Adaptive APPLIED/NO_CHANGE/FALLBACK paths pass.
- [ ] No P0/P1 known canonical contract violation remains.

---

# 54. Open Implementation Bindings

Allowed implementation bindings/TBDs:

- exact C# class/interface names for logical components not yet implemented;
- exact folder split where the existing repository organization is preserved;
- ScriptableObject vs JSON/backend registry storage where contract permits;
- DB table/index names;
- transaction/CAS primitive;
- GUID/ULID/hash representation;
- Fusion exact `[Networked]` fields/RPC presentation binding;
- test fixture class/file names;
- CI provider;
- profiler numeric budgets after baseline measurement.

Allowed tuning TBDs:

- final Stalker/Listener/Warden balance numbers;
- AED numerical thresholds/candidate values;
- performance thresholds after profile;
- final pilot-derived balance.

Not implementation-bound:

- Host authority;
- Monster semantic FSM/contracts;
- RuntimeNoise vs Telemetry separation;
- Telemetry identity/order/idempotency;
- Profile replay/retraction semantics;
- Fixed/AED authority and fallback semantics;
- Warden safety invariants;
- experiment assignment/enrollment semantics where implemented.

---

# 55. Post-M2 / Pilot / Analysis Handoff

Once M2 core implementation/evidence is genuinely complete:

```text
integration evidence
→ pilot
→ profiling
→ AED/Profile/fixed-baseline tuning freeze
→ Fixed_vs_Adaptive_Analysis_Plan_v1.1
→ statistical model / uncertainty freeze
→ plannedPairSlots
→ MAIN allocation schedule
→ ExperimentRunManifest freeze
→ readiness review
→ MAIN experiment
```

Do not invent sample size, alpha, power, effect size, or final statistical model inside this implementation plan.

---

# 56. Final Consistency Audit

```text
Canonical source set frozen?
YES

Older duplicate docs prevented from overriding locked docs?
YES

Actual code inspected before assigning implementation status?
YES

Code presence distinguished from verified behavior?
YES

Shared foundation avoids giant generic Monster FSM?
YES

Stalker six-state contract preserved?
YES

Stalker SEARCH hidden-transform access forbidden?
YES

Stalker attack exactly-once planned/tested?
YES

Listener RuntimeNoise separate from Telemetry?
YES

Listener pending observation bounded?
YES

Listener same-tick arbitration deterministic?
YES

Warden remains Spatial Pressure Controller?
YES

Warden graph safety planned before visible lock mechanics?
YES

Warden arbitrary door locking forbidden?
YES

Host authority preserved?
YES

Proxy-authoritative Monster/AED decisions forbidden?
YES

Telemetry v1.1 pipeline remains one-way?
YES

Profile correction/retraction/replay included?
YES

Deferred Profile values not fabricated?
YES

TeamPerformance remains INCOMPLETE/null unless its own contract later changes?
YES

AdaptiveInputSnapshot implemented before AED live use?
YES

FixedDirector operational before Adaptive readiness?
YES

ScenarioValidator cannot be bypassed?
YES

AED policy remains AED_SCENARIO_POLICY_V1_1?
YES

No arbitrary adaptive float generation?
YES

Experiment instrumentation separated from MAIN readiness?
YES

Main experiment not declared ready from code completion alone?
YES

Every P0/P1 work item has tests/evidence requirements?
YES

Every claimed VERIFIED item has actual evidence?
YES — no current subsystem is claimed VERIFIED without executable evidence

Tuning distinguished from correctness?
YES

Accelerated M2 Feature-Complete Alpha has exact acceptance gate?
YES
```

---

# 56.1 M2 Implementation Plan Surgical Validation

```text
M2-PLAN-01 dependency types exact? YES
False serialization removed? YES
Pure Profile work can start with fixtures? YES
Pure AED policy work can start with fixtures? YES
Experiment manifest/readiness work can start before live integration? YES

M2-PLAN-02 bootstrap cycle removed? YES
FND-001 no longer requires TST-001 DONE? YES
TST-001 can start after toolchain is usable? YES
Wave 0 aggregates build + smoke evidence without circular dependency? YES
Network harness bootstrap cycle removed? YES

M2-PLAN-03 canonical test ranges current? YES
Listener PlayMode extends through LIS-P-057? YES
Telemetry PlayMode extends through TEL-P-024? YES
No superseded Listener PlayMode range ending at 045 remains? YES
No superseded Telemetry PlayMode range ending at 023 remains? YES

M2-PLAN-04 scope mode exact? YES
OFFICIAL_BASELINE defined? YES
ACCELERATED_FEATURE_COMPLETE_ALPHA defined? YES
Current plan target = ACCELERATED_FEATURE_COMPLETE_ALPHA? YES
Formal PM gate still = OFFICIAL_BASELINE until rebaseline? YES
PM rebaseline requirement explicit? YES
Acceptance matrix separates official vs accelerated requirement? YES

Architecture regression introduced? NO
Implementation status hallucinated? NO
Test PASS fabricated? NO
```

## Final Consistency Audit

```text
Can Profile pure processing begin before live TEL-004 integration? YES
Can Profile be accepted for production before required Telemetry integration? NO
Can AED PolicyDefinition be implemented before live Profile snapshots exist? YES
Can live AED be accepted before AdaptiveInputSnapshot integration? NO
Can ExperimentRunManifest logic be developed with fixtures before live AED? YES
Can experiment integration be accepted without real provenance evidence? NO
Must FND-001 be completely DONE before any test harness file can be created? NO
Can Wave 0 finish without any runnable smoke test? NO
Is LIS-P-045 the final canonical Listener PlayMode test? NO
Is LIS-P-057 part of the canonical Listener contract? YES
Is TEL-P-023 the final canonical Telemetry PlayMode test? NO
Is TEL-P-024 required by canonical Telemetry? YES
Does the current approved PM baseline already make expanded 3-Monster/Profile/AED scope official M2? NO
Is the implementation plan intentionally targeting the accelerated scope? YES
Does that automatically rewrite the approved Project Plan? NO
Is PM rebaseline required before accelerated scope becomes formal M2 gate? YES
```

# Surgical Correction Report

| Issue | Status | Sections Changed | Resolution |
|---|---|---|---|
| M2-PLAN-01 | RESOLVED | Dependency DAG, Critical Path, Parallel Workstreams, Cross-Team Register, Work Package Register, PR/Calendar | Replaced false single-level dependencies with START / INTEGRATION / ACCEPTANCE semantics; fixture-first work is no longer serialized behind live upstream integration. |
| M2-PLAN-02 | RESOLVED | Wave 0, FND-001, TST-001, FND-003, TST-002 | Build baseline and test-harness bootstrap now overlap; Wave 0 is the aggregate build+smoke gate rather than a card-level cycle. |
| M2-PLAN-03 | RESOLVED | Work-package tests, Contract Test Traceability Matrix, validation/audit | Listener range corrected through `LIS-P-057`; Telemetry through `TEL-P-024`; canonical current ranges are explicitly registered and plan shorthand is subordinate to locked contracts. |
| M2-PLAN-04 | RESOLVED | Document Control, Scope Governance, work-package classifications, Acceptance Matrix, Calendar, Final Status | Added `M2ScopeMode`; current target is accelerated while formal PM gate remains official baseline until approved rebaseline. |

---

# 57. Final Status

```text
Document:
M2_AI_Implementation_Plan_v1.0.md

Document Revision:
v1.0

Plan Status:
COMPLETE

Architecture Escalation Required:
NO

Plan Target Scope Mode:
ACCELERATED_FEATURE_COMPLETE_ALPHA

Formal PM M2 Acceptance Mode:
OFFICIAL_BASELINE

Project-Management Rebaseline Required:
YES

Canonical Contract Set:
FROZEN / LOCKED

Current Implementation:
PARTIAL

M2 Accelerated Feature-Complete Alpha:
NOT READY

Critical P0 Implementation Blockers:
11 blocker groups
1. executable build/test baseline and project-owned test harness
2. Host/Fusion gameplay + Monster authority foundation
3. canonical Stalker migration/integration
4. RuntimeNoise + complete Listener runtime for accelerated target
5. FacilityGraph/map authoring + complete Warden safety/runtime for accelerated target
6. Research Facility gameplay/life-state/objective/door bindings required by AI
7. Telemetry v1.1 Unity-to-backend pipeline
8. Profile v1.1 persistence/correction/snapshot pipeline for accelerated target
9. ScenarioConfig + FixedDirector + ScenarioValidator + Host apply for accelerated target
10. AED_SCENARIO_POLICY_V1_1 implementation/integration for accelerated target
11. critical 2–4P / backend / end-to-end verification evidence

P1 Implementation Blockers:
4 blocker groups
1. cross-system observability/evidence completeness
2. experiment instrumentation needed for research-alpha handoff
3. telemetry/profile/AED operational diagnostics and failure evidence
4. minimal repeatable CI/automation

Tuning Remaining:
Stalker/Listener/Warden balance, AED numerical thresholds/candidate values,
performance budgets and final fixed/adaptive balance remain evidence-driven tuning.

Main Fixed-vs-Adaptive Experiment:
NOT READY
```

### Governance interpretation

The approved Project Plan still owns formal milestone acceptance. This corrected plan intentionally targets a broader accelerated scope, but no PM approval is fabricated. Incomplete Listener/Warden/Profile/AED therefore blocks the accelerated Feature-Complete Alpha while remaining outside the current official M2 Prototype scope until a formal rebaseline says otherwise.

### Why accelerated status is NOT READY

The current repository contains a useful Stalker spatial/FSM spike, Fusion room foundation, auth/backend foundation, and limited Stalker debug evidence. It does not yet contain Listener, Warden, Telemetry v1.1 pipeline, canonical AI Profile, ScenarioConfig/FixedDirector/Validator, AED, experiment infrastructure, authoritative Monster NetworkBehaviour integration, or project-owned automated test suites. Build/test execution was also not possible in the review environment.

The corrected plan removes dependency false-serialization and governance ambiguity; it does not convert missing implementation/test evidence into completion.
