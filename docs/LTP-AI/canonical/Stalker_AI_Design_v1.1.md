# ECHO PROTOCOL — Stalker AI Detailed Design v1.1

**Document:** `Stalker_AI_Design_v1.1.md`  
**Project:** ECHO PROTOCOL — Co-op Survival Horror Multiplayer  
**Revision:** v1.1  
**Date:** 2026-08-25  
**Parent Architecture:** `AI_Architecture_v1.1.md`  
**Parent Architecture Status:** BASELINED v1.1  
**Detailed Design Status:** BASELINED v1.1  
**Scope:** Implementation-level detailed design for The Stalker  
**Environment:** Unity `6000.5.8f1`; Photon Fusion `2.1.1 Stable`, build `2177`; Host Mode; 2–4 Players  
**Recommended Status:** BASELINED v1.1  
**Important:** This document freezes implementation behavior and ownership where evidence is sufficient. It does not claim implementation completion, test pass results, profiler results, final tuning, or final patrol-region segmentation.

---

## Statement Classification

Important design statements use the following evidence/status vocabulary:

| Classification | Meaning |
|---|---|
| **PROJECT BASELINE** | Required by the baselined architecture, M1 contract, map/network decision, or other approved project source. |
| **CURRENT IMPLEMENTATION** | Directly evidenced by supplied source/code captures. It may still require migration. |
| **DETAILED-DESIGN DECISION** | Implementation-level decision introduced here to make the baselined architecture implementable without changing it. |
| **TUNING TBD** | Numerical or balancing value intentionally unresolved until playtest/profiling evidence exists. |
| **IMPLEMENTATION BINDING TBD** | Concrete Unity/Fusion/scene binding not yet evidenced, but it does not block the design. |
| **ARCHITECTURE ESCALATION** | Used only if implementation evidence proves a top-level baseline cannot reasonably work. No such escalation is required by this revision. |

---

# 1. Document Control

| Field | Value |
|---|---|
| Owner scope | AI / Telemetry / Research |
| Architecture status | BASELINED v1.1 |
| Detailed Design status | BASELINED v1.1 |
| Detailed-design subject | The Stalker |
| Runtime model | Traditional deterministic/rule-based AI |
| Semantic FSM | `PATROL`, `DETECT`, `CHASE`, `ATTACK`, `RECOVER`, `SEARCH` |
| AI authority | Host / Fusion State Authority |
| Network topology | Photon Fusion 2 Host Mode |
| Unity editor | `6000.5.8f1` |
| Installed Fusion SDK | `2.1.1 Stable`, build `2177` |
| Multiplayer target | 2–4 players |
| Canonical map | Research Facility |
| Canonical spatial hierarchy | NavMesh → SpatialGraph → RegionGraph → CoverageMemory → GlobalPatrolPlanner → LocalPatrolSelector → Navigation |
| Search invariant | `SEARCH MUST NOT read hidden Player Transform` |
| Architecture escalation | None required |
| Implementation completion | Not claimed |

---

# 2. Purpose

This document converts `AI_Architecture_v1.1.md` into a concrete implementation contract for The Stalker in M2 Feature-Complete Alpha.

A developer implementing Stalker from this document should not need to invent:

- state ownership;
- target-selection semantics;
- Detection Meter lifecycle;
- legal memory contents;
- PATROL global/local planning;
- SEARCH candidate generation/filtering/scoring;
- path failure behavior;
- no-progress/stuck recovery;
- dynamic-door handling;
- Fusion authority boundaries;
- replication boundaries;
- debug evidence;
- quality-metric semantics;
- test coverage;
- migration order from M1-026.

The design optimizes for correctness, reproducibility, explainability, anti-cheat information boundaries, host authority, testability, observability, measurable behavior, maintainability, incremental migration, and thesis defensibility.

---

# 3. Scope

## 3.1 In scope

- Stalker runtime ownership and tick flow;
- Vision/LOS;
- target eligibility and selection;
- Detection Meter;
- six-state FSM;
- typed memory;
- patrol SpatialGraph and RegionGraph;
- designer-authored RegionDefinition;
- SpatialNode-to-Region mapping;
- coverage semantics;
- global patrol planning;
- local patrol selection;
- CHASE;
- ATTACK;
- mandatory RECOVER;
- no-cheat SEARCH;
- navigation result classification;
- progress/no-progress/stuck detection;
- recovery ladder;
- dynamic door/topology reaction;
- fixed waypoint fallback;
- Fusion Host Mode binding;
- ScenarioConfig/AED boundary;
- telemetry boundary;
- observability and reason codes;
- quality metrics;
- tests;
- current-code migration.

## 3.2 Out of scope

- seventh Stalker state;
- Behavior Tree migration;
- GOAP;
- ML/RL;
- runtime GenAI decision-making;
- hearing behavior for Stalker;
- final numerical tuning;
- final patrol-region count;
- final map geometry;
- final performance budget;
- fabricated profiler/test results;
- full combat hitbox system beyond the Stalker attack contract;
- full player movement/network design;
- full Warden or Listener detailed design.

---

# 4. Source Documents and Evidence Priority

## 4.1 Authority order

When sources disagree, apply the following authority order:

1. `AI_Architecture_v1.1.md` — canonical top-level architecture and authority boundary.
2. Approved gameplay/design contracts directly governing the behavior being implemented:
   - `M1-013_Stalker_FSM_Sensor_Contracts_FINAL.md`;
   - approved Map Flow / Research Facility contract;
   - approved multiplayer/network gameplay contract;
   - other approved gameplay contracts relevant to the concrete decision.
3. Latest implementation/source evidence.
4. Historical implementation notes, spikes, screenshots, diffs, and older project snapshots.
5. Official Unity / Photon Fusion 2 documentation for engine/network semantics.

Supporting project documents such as GDD, System Architecture, Implementation Specification, Telemetry schema, ScenarioConfig/AED policy, and Test Strategy are consulted according to the responsibility they govern. They do not silently override a higher-authority Stalker behavior contract.

## 4.2 Governance rule — approved contract beats current code

**DETAILED-DESIGN DECISION / GOVERNANCE INVARIANT**

```text
Approved architecture/behavior contract
>
current implementation
```

If current source disagrees with an approved behavior/design contract:

```text
classify as implementation gap / migration issue / bug
→ preserve approved behavior
→ record migration action
→ add/update a regression test
→ do not silently rewrite the specification to match the code
```

A detailed-design revision may change a lower-level approved behavior only when all of the following are explicit:

```text
Existing approved contract
Observed evidence/problem
Detailed-design revision decision
Why the revision is necessary
Why the architecture baseline remains valid
Migration/test impact
```

No such M1-013 gameplay-semantic revision is introduced by this correction pass. The additions in this document refine implementation ownership, compatibility, evaluation, and networking semantics while preserving M1-013 behavior.

## 4.3 Documents/evidence reviewed

Primary project sources:

1. `AI_Architecture_v1.1.md`.
2. `M1-013_Stalker_FSM_Sensor_Contracts_FINAL.md`.
3. `KLTN.docx` — *THIẾT KẾ SƠ BỘ MAP RESEARCH FACILITY VÀ VỊ TRÍ OBJECTIVE* / Map Flow Plan v0.
4. `KLTN (1).docx` — *Chốt cách tổ chức multiplayer và đồng bộ dữ liệu trong trận*.
5. `ECHO PROTO.docx` / `ECHO PROTO(1).docx`.
6. `02_ECHO_PROTOCOL_System_Architecture_REVISED.docx`.
7. `03_ECHO_PROTOCOL_Implementation_Spec_REVISED.xlsx`.
8. `Telemetry_Event_Schema_v0_FINAL.md`.
9. `M1-015_ScenarioConfig_AED_Fairness_Policy_v0_FINAL.md`.
10. `M1-020_Test_Strategy_Fixed_vs_Adaptive_Experiment_v0_FINAL.md`.
11. latest M1-026 Stalker source/evidence.
12. `ECHO-PROTOCOL-feature-m1-026-stalker-spike.zip` as verified environment/historical source evidence.

The code snapshot/captures are implementation evidence, not higher-authority behavioral specification.

---

# 5. External Research Reviewed

Only official Unity and Photon documentation is used for engine/network binding decisions.

## 5.1 Unity navigation

Relevant Unity contracts:

- `NavMeshAgent.SetDestination` accepts a destination request but the path may remain pending for later frames; therefore a successful boolean return is not equivalent to a complete usable path.
- `NavMeshPathStatus` explicitly distinguishes `PathComplete`, `PathPartial`, and `PathInvalid`.
- `NavMesh.CalculatePath` and `NavMeshAgent.CalculatePath` are synchronous path checks; Unity warns against unbounded path calculations per frame.
- `NavMeshAgent` exposes `pathPending`, `hasPath`, `pathStatus`, `remainingDistance`, `desiredVelocity`, `isPathStale`, `autoRepath`, and `isOnNavMesh`, which are relevant inputs for navigation observability/recovery.
- AI Navigation supports dynamic obstacles and links, but the project does not currently establish Stalker-specific NavMeshLink traversal behavior.

Official references:

- https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AI.NavMeshAgent.SetDestination.html
- https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AI.NavMeshAgent.html
- https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AI.NavMesh.CalculatePath.html
- https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AI.NavMeshPath.html
- https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AI.NavMeshPath-status.html
- https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AI.NavMeshPathStatus.html
- https://docs.unity3d.com/6000.0/Documentation/Manual/com.unity.ai.navigation.html
- https://docs.unity3d.com/6000.0/Documentation/Manual/com.unity.test-framework.html
- https://docs.unity3d.com/Manual/Profiler.html

## 5.2 Photon Fusion 2

Relevant Fusion contracts:

- Host Mode is a client-server topology; the Host/Server holds State Authority for authoritative object state.
- Input Authority is distinct from State Authority.
- `[Networked]` properties on `NetworkBehaviour` represent synchronized durable state.
- `FixedUpdateNetwork()` is Fusion's simulation-tick callback and is the normal location for tick-accurate network-state mutation.
- RPCs suit punctual events/requests but are not persistent state; late join/reconnect cannot reconstruct an RPC that has already occurred unless its effect is represented in synchronized state.

Official references:

- https://doc.photonengine.com/fusion/v2/manual/input/player-input
- https://doc.photonengine.com/fusion/v2/manual/data-transfer/networked-properties
- https://doc.photonengine.com/fusion/v2/manual/data-transfer/rpcs
- https://doc.photonengine.com/fusion/v2/manual/data-transfer/data-transfer
- https://doc.photonengine.com/fusion/v2/tutorials/host-mode-basics/6-remote-procedure-calls
- https://doc.photonengine.com/fusion/v2/fusion-intro

---

# 6. Baseline Architecture Dependencies

**PROJECT BASELINE**

```text
Authoritative Stalker Runtime
        │
        ▼
Vision / physical observations
        │
        ▼
Target Eligibility / Target Selection
        │
        ▼
StalkerMemory
        │
        ▼
Six-State FSM
        │
        ├── PATROL → GlobalPatrolPlanner → LocalPatrolSelector
        ├── SEARCH → StalkerSearchPlanner
        ├── CHASE  → visible-target destination policy
        └── ATTACK → StalkerAttackController
                         │
                         ▼
               Navigation / Gameplay Action
```

Spatial hierarchy:

```text
NavMesh
→ SpatialGraph
→ RegionGraph
→ CoverageMemory
→ GlobalPatrolPlanner
→ LocalPatrolSelector
→ StalkerNavigationController
```

The following are not semantic FSM states:

```text
Final Hunt
MoveToLKP
SelectRegion
Repath
NoProgress
StuckRecovery
```

---

# 7. Current Implementation Assessment

## 7.1 Source snapshot skew and governance

The supplied Unity ZIP is an older Stalker snapshot: `StalkerController` uses the six-state FSM, `StalkerVisionSensor`, `PatrolRoute`, and a basic `NavMeshAgent` destination flow, while SEARCH mainly moves to `lastKnownPosition`.

Newer M1-026 code evidence shows a later DynamicSpatial patrol integration with:

- `StalkerPatrolMode.FixedWaypoint`;
- `StalkerPatrolMode.DynamicSpatial`;
- `NavMeshSpatialGraphBuilder`;
- `NavMeshSpatialGraph`;
- `SpatialPatrolMemory`;
- `SpatialPatrolPlanner`;
- `StalkerBlackboard` spatial IDs;
- local BFS candidate selection;
- staleness/connectivity/backtrack scoring;
- dynamic-patrol debug counters;
- fixed-waypoint fallback.

**CURRENT IMPLEMENTATION:** the newer M1-026 captures are the latest supplied spatial implementation evidence.

**GOVERNANCE INVARIANT:** this does not give the captures authority over `AI_Architecture_v1.1.md` or approved gameplay contracts. If the newer code contradicts M1-013 or the architecture baseline, the contradiction is an implementation gap to migrate/fix unless this document records an explicit revision decision.

Before coding, reconcile the working branch so the newest intended M1-026 spatial files are physically present and their revision/source identity is known.

This is source-control reconciliation, not an architecture escalation.

---

## 7.2 Current implementation strengths

Keep/reuse:

- correct six-state semantic FSM;
- physical Vision/LOS concept;
- Detection Meter concept;
- LKP and no-hidden-tracking intent;
- mandatory attack RECOVER;
- `NavMeshAgent` boundary in `StalkerNavigationController`;
- `NavMeshSpatialGraphBuilder` triangulation approach;
- `SpatialPatrolMemory`;
- local `SpatialPatrolPlanner` scoring logic;
- deterministic candidate choice;
- fixed waypoint fallback;
- current spatial debug evidence.

## 7.3 Current implementation gaps

Must be added/refactored:

- typed observations for multiple players;
- explicit target eligibility service;
- typed `StalkerMemory`;
- one canonical mutable `CoverageMemory` backed by/refactored from current `SpatialPatrolMemory`;
- region layer and node-region validation;
- SpatialGraph/RegionGraph compatibility identity so build-local node IDs cannot consume stale baked mapping;
- actual global patrol objective;
- coverage semantics;
- SearchContext/SearchPlanner;
- richer navigation result/status;
- stale/no-progress/stuck handling;
- dynamic-door invalidation;
- explicit attack-episode identity/resolution guard before irreversible damage integration; current source shows timer-gated `ResolveAttackHitMoment()` followed by immediate RECOVER but does not evidence a durable exactly-once episode guard;
- read-only debug snapshot;
- Fusion Stalker binding and authoritative simulation-tick migration;
- production telemetry boundary vs development diagnostics.

---

# 8. Design Goals and Non-Goals

## 8.1 Goals

- one authoritative Stalker simulation;
- legal-information-only memory;
- deterministic planner behavior for equal state/config;
- global map pressure without fixed patrol rail;
- no local-loop starvation;
- bounded candidate/path work;
- deterministic reason codes;
- safe navigation degradation;
- high-quality test seams;
- no rewrite of working M1-026 logic.

## 8.2 Non-goals

- omniscient pursuit;
- player-position prediction after LOS loss;
- learned patrol;
- random wandering as global coverage strategy;
- a generic monster God Class;
- a universal dictionary blackboard;
- client-authoritative AI;
- every private AI field replicated to clients.

---

# 9. Runtime Data Flow

## 9.1 Authoritative simulation flow

```text
Fusion Host / State Authority Tick
        │
        ▼
StalkerNetworkBinding
        │
        ▼
StalkerRoot.Simulate(deltaTime)
        │
        ├── Perception refresh when due/triggered
        ├── Target eligibility/selection
        ├── Memory update from legal observations
        ├── FSM tick / transition
        ├── active-state planner
        ├── NavigationController / AttackController
        ├── Coverage/Search memory update
        ├── Development diagnostics
        └── authoritative gameplay telemetry facts
```

**DETAILED-DESIGN DECISION:** migrate Stalker gameplay timers away from implicit `Time.deltaTime` ownership inside arbitrary `Update()` calls toward an explicit simulation delta supplied by the authoritative root. In Fusion integration, the network-facing binding should invoke authoritative AI state mutation from the Fusion simulation-tick path.

Pure C# planners/memory remain independent of Fusion.

## 9.2 Client flow

```text
Host authoritative state
→ Fusion synchronization
→ non-authoritative proxy
→ transform/state/action presentation
→ animation / VFX / SFX / telegraph
```

No proxy independently evaluates authoritative Vision, target selection, FSM transitions, planning, attacks, or Warden decisions.

---

# 10. Ownership Matrix

| Concern | Canonical owner | Reads | Writes | Must not own |
|---|---|---|---|---|
| Physical visibility | `StalkerVisionSensor` | authoritative player candidate physical data, sensor config | `VisionObservation[]` | eligibility, target, FSM |
| Target eligibility | `StalkerTargetEligibility` | authoritative player life/session state | typed eligibility result | LOS, Detection Meter |
| Target selection | `StalkerTargetSelector` | observations + eligibility | selected candidate identity/result | FSM transition, damage |
| Target/detection knowledge | `StalkerMemory` | accepted legal observations, selector/FSM operations | target IDs, Detection Meter, LKP/bearing/time | path execution |
| Coverage history | `CoverageMemory` | confirmed physical visitation | node/region visit count/time/history | semantic FSM state |
| Search episode memory | `StalkerSearchContext` | legal Stalker knowledge at SEARCH entry + search progress | episode-local candidate/visited state | hidden Player Transform |
| Semantic state | `StalkerFSM` / controller coordinator | memory, observations, timers | state transitions | raw NavMesh implementation |
| Global patrol intent | `GlobalPatrolPlanner` | RegionGraph + CoverageMemory | persistent target Region objective | movement |
| Local patrol intent | `LocalPatrolSelector` | SpatialGraph + Region route + CoverageMemory | target SpatialNode intent | movement/FSM |
| Search intent | `StalkerSearchPlanner` | immutable episode context + spatial data | search candidate intent | hidden Player Transform |
| Motion/path | `StalkerNavigationController` | destination intent, NavMeshAgent | path/movement/recovery status | target/LKP/FSM |
| Progress classification | `NavigationProgressMonitor` | agent/path progress facts | no-progress/stuck classification | FSM/target selection |
| Attack episode + resolution | `StalkerAttackController` | already-entered ATTACK state, target validity/range | attack episode identity/guard/result and at-most-once damage request | ATTACK transition |
| Debug | `StalkerDebugProvider` | read-only runtime projections | immutable snapshot only | gameplay mutation |
| Telemetry | `StalkerTelemetryAdapter` | authoritative gameplay facts | schema-approved event / diagnostics | gameplay decisions |
| Network binding | Stalker Fusion binding | root state + Fusion authority | synchronized client-required state | private planner logic / damage resolution |

Write ownership is exclusive at the semantic level. Compatibility facades may forward to the canonical owner during migration but must not maintain a second mutable copy.

---

# 11. Memory Model

## 11.1 `MonsterRuntimeContext`

**DETAILED-DESIGN DECISION**

Keep only genuinely shared runtime context:

```text
MonsterRuntimeContext
- MonsterId
- CurrentRegionId?
- CurrentDestination?
- LastAcceptedObservationTime?
```

Do not add Stalker target semantics merely for convenience.

## 11.2 `StalkerMemory`

```text
StalkerMemory
- CurrentTargetId?
- DetectionTargetId?
- DetectionMeter
- LastKnownPosition?
- LastSeenDirection?
- TargetLastSeenTime?
- LastCurrentTargetObservation?
```

`LastCurrentTargetObservation` is a typed value or equivalent internal representation, not a live Transform reference used after LOS loss.

### Lifecycle

- create/reset on Stalker authoritative spawn/match initialization;
- target fields mutate only through target/FSM contract;
- LKP fields update only from legal `VisionObservation` for `CurrentTarget`;
- clear stale target/detection fields on contract-defined invalidation/timeout;
- reset match-scoped memory on match reset/despawn.

## 11.3 `CoverageMemory` — canonical spatial-memory source of truth

**DETAILED-DESIGN DECISION:** `CoverageMemory` is the single canonical mutable source of truth for Stalker patrol visitation.

```text
CoverageMemory
- NodeLastVisited[]
- RegionLastVisited[]
- NodeVisitCount[]
- RegionVisitCount[]
- RecentNodeHistory
- RecentRegionHistory
```

The architecture term `StalkerSpatialMemory` is implemented in v1.1 detailed design as one of:

- a type alias for `CoverageMemory`;
- a compatibility facade forwarding to `CoverageMemory`;
- a read-only projection/aggregate.

It must **not** own a second mutable set of visit arrays/history.

Current M1 class `SpatialPatrolMemory` is migrated as the implementation backing/seed of `CoverageMemory`:

```text
SpatialPatrolMemory current node state
→ extend/refactor in place
→ CoverageMemory node + region state
```

Do not create a parallel “new CoverageMemory database” while retaining mutable `SpatialPatrolMemory`.

### Write ownership

Only physical visitation integration writes coverage:

```text
Navigation/current-node resolver
→ confirmed physical node/region visit fact
→ CoverageMemory.MarkVisited(...)
```

Planners, debug, telemetry, and metrics are read-only consumers of coverage state.

---

## 11.4 `StalkerSearchContext`

Created only on SEARCH entry:

```text
StalkerSearchContext
- SearchOriginLKP
- SearchOriginDirection
- SearchStartTime
- SearchOriginRegionId?
- CurrentCandidateNodeId?
- CandidateHistory
- VisitedSearchNodes
- CandidateAttemptCount
```

`SearchDuration` and `SearchRadius` come from current validated configuration rather than being duplicated as authoritative mutable knowledge.

### Immutable during one search episode

- `SearchOriginLKP`;
- `SearchOriginDirection`;
- `SearchStartTime`;
- original `SearchOriginRegionId` if resolved.

### Mutable during episode

- current candidate;
- candidate history;
- visited search nodes;
- attempt count.

### Clear

Clear on:

- same-target legal reacquisition → CHASE;
- different eligible target accepted → DETECT;
- current target invalidation handling;
- timeout → PATROL;
- Stalker reset/despawn.

---

# 12. Configuration Model

## 12.1 Configuration ownership

Stalker gameplay configuration is separate from runtime memory.

M1-013 configurable fields:

```text
VisionDistance
VisionAngle
DetectionFillRate
DetectionDecayRate
PatrolSpeed
ChaseSpeed
SearchDuration
SearchRadius
AttackRange
AttackWindup
AttackRecovery
StalkerDamagePercent
```

M1-015 adaptive whitelist is exactly:

```text
DetectionFillRate
DetectionDecayRate
ChaseSpeed
SearchDuration
```

`configurable != adaptive-authorized`.

## 12.2 Internal planner/navigation tuning

Internal planner parameters such as BFS candidate bound, staleness normalization horizon, scoring weights, candidate-attempt bound, arrival tolerance, and stuck windows are not automatically ScenarioConfig fields and are not automatically AED-authorized.

They remain tuning/implementation configuration.

---

# 13. Vision / LOS Detailed Contract

## 13.1 Pipeline

**PROJECT BASELINE + DETAILED-DESIGN DECISION**

```text
authoritative player candidates
→ cheap physical broad phase
→ distance
→ FOV/cone
→ LOS ray query
→ typed VisionObservation[]
→ TargetEligibility / TargetSelector
```

Eligibility remains outside the physical sensor. This preserves the M1 rule that a Downed/DEAD player may still be physically visible while being invalid as a Stalker target.

## 13.2 Observation type and bearing semantics

Recommended immutable value:

```text
VisionObservation
- PlayerId
- ObservedPosition
- ObservedDirection
- ObservedAt
- Distance
```

**DETAILED-DESIGN DECISION:** `ObservedDirection` is the normalized LOS bearing from the Stalker vision origin toward `ObservedPosition` at the accepted observation:

```text
ObservedDirection
=
normalize(ObservedPosition - VisionOriginPosition)
```

Accordingly:

```text
LastSeenDirection
=
ObservedDirection from the last legally accepted
VisionObservation of CurrentTarget
```

`LastSeenDirection` is a **LOS bearing heuristic**.

It is **not**:

- target/player facing direction;
- `Player.forward`;
- target velocity;
- movement prediction;
- inferred escape direction;
- a direction read from the hidden target after LOS loss.

If implementation chooses a different field name, this semantic remains mandatory.

---

## 13.3 Sensor origin

Use an explicit serialized/authoring `visionOrigin` Transform where present. Fallback to Stalker root only if the prefab contract explicitly allows it.

The sensor origin controls:

- cone origin;
- forward vector;
- ray origin.

## 13.4 Target sample position

**DETAILED-DESIGN DECISION**

Do not equate “player object Transform” with the only valid sample point.

Use a player-provided `VisionTargetPoint`/sensor target anchor when available. Until that binding exists, current candidate transform position is a compatibility fallback.

Reason:

- character roots can be at feet;
- colliders can be multi-part;
- deterministic LOS needs a stable intended point.

Exact player component name is **IMPLEMENTATION BINDING TBD**.

## 13.5 Multiple colliders and collider filtering

LOS must:

- ignore Stalker self hierarchy;
- treat colliders belonging to the candidate player hierarchy as target hits;
- treat closed-door/wall blockers in configured blocker layers as occlusion;
- ignore unrelated trigger/helper colliders unless their collision contract marks them as blockers.

The current `RaycastAll` + distance-sort logic already demonstrates self/candidate hierarchy filtering and may be retained if tests confirm correctness. Optimize only after profiling.

## 13.6 Doors

**PROJECT BASELINE:** Closed Door blocks Stalker Vision LOS and is an absolute Stalker path obstacle.

Door state changes must affect both:

- LOS collision/blocker state;
- route/path feasibility.

## 13.7 FOV

`VisionAngle` is the full cone. Visibility condition:

```text
angleToCandidate <= VisionAngle / 2
```

## 13.8 Player state

Sensor does not remove:

- Downed;
- DEAD / Soul;
- disconnected-state representations purely because of gameplay eligibility.

`StalkerTargetEligibility` does.

## 13.9 Observation timestamp and memory write

Use authoritative simulation time/tick-derived time.

For a newly accepted `VisionObservation` belonging to `CurrentTarget`:

```text
LastKnownPosition = observation.ObservedPosition
LastSeenDirection = observation.ObservedDirection
TargetLastSeenTime = observation.ObservedAt
```

These writes are owned by `StalkerMemory` through the FSM/authoritative observation-consumption path.

After LOS loss:

- `LastKnownPosition` freezes until a new legal observation exists;
- `LastSeenDirection` freezes with the same last legal observation;
- neither field may be updated from replicated hidden Transform, target velocity, telemetry, AED, navigation, or prediction.

`LastSeenDirection` and `LastKnownPosition` therefore refer to the same accepted observation sample.

---

# 14. Target Eligibility

`StalkerTargetEligibility` is a small rule component/service.

## 14.1 Ineligible cases

At minimum:

- player no longer exists in session;
- player disconnected;
- Downed;
- DEAD / Soul / spectator;
- other player-state contract-defined invalid states.

## 14.2 Output

```text
TargetEligibilityResult
- Eligible
- Reason
```

Recommended reason set:

```text
Eligible
Disconnected
Downed
Eliminated
NotInActiveSession
OtherGameplayState
```

Do not infer visibility here.

---

# 15. Target Selection

## 15.1 PATROL acquisition

From current `VisionObservation[]`:

1. remove target-ineligible players;
2. choose nearest physically visible eligible player;
3. deterministic tie-break by stable PlayerId if equal distance is effectively tied;
4. set `DetectionTargetId`;
5. set Detection Meter to 0;
6. FSM transitions `PATROL → DETECT`.

## 15.2 DETECT lock

Once DETECT has a valid `DetectionTarget`, a newly closer visible player does not replace it merely for being closer.

Switch only when existing DetectionTarget becomes invalid according to contract.

## 15.3 CurrentTarget

`CurrentTargetId` is established by DETECT promotion only.

During SEARCH, same CurrentTarget legal reacquisition has priority over other visible candidates.

---

# 16. Detection Meter

## 16.1 Owner

`StalkerFSM`/decision controller owns the meter, stored in `StalkerMemory` for coherent state inspection.

Sensor does not mutate it.

## 16.2 Rules

```text
new DetectionTarget
→ meter = 0

DetectionTarget visible
→ meter += DetectionFillRate * dt
→ clamp [0, FULL]

DetectionTarget not visible
→ meter -= DetectionDecayRate * dt
→ clamp [0, FULL]

not visible AND meter == 0
→ clear DetectionTarget
→ PATROL

meter == FULL
→ promote DetectionTarget → CurrentTarget
→ clear DetectionTarget
→ meter = 0
→ CHASE
```

A newly acquired visible target at meter 0 is not immediately released.

## 16.3 Target invalidation

```text
DetectionTarget invalid
→ clear DetectionTarget
→ meter = 0
→ reevaluate visible eligible players
→ DETECT new target if any
→ else PATROL
```

No meter value carries to another player.

`FULL` representation remains the existing contract/config abstraction; final representation/value is implementation-owned.

---

# 17. FSM Transition Contract

| From | Guard | Required side effect | To |
|---|---|---|---|
| PATROL | nearest eligible visible candidate exists | DetectionTarget=selected; meter=0 | DETECT |
| DETECT | DetectionTarget visible until meter FULL | promote to CurrentTarget; clear DetectionTarget; meter=0 | CHASE |
| DETECT | target hidden and meter decays to 0 | clear DetectionTarget | PATROL |
| DETECT | DetectionTarget invalid | clear/reset; reevaluate | DETECT or PATROL |
| CHASE | CurrentTarget valid + visible + in AttackRange | FSM owns attack entry; start attack payload | ATTACK |
| CHASE | CurrentTarget loses legal LOS | preserve last legal observation/LKP; create SearchContext | SEARCH |
| CHASE | CurrentTarget invalid | clear/reset; reevaluate | DETECT or PATROL |
| ATTACK | attack resolves after wind-up | no invalid-target damage; begin mandatory recovery | RECOVER |
| RECOVER | recovery complete + target valid/visible | clear recovery context | CHASE |
| RECOVER | recovery complete + target valid/hidden | create SearchContext from last legal knowledge | SEARCH |
| RECOVER | recovery complete + old target invalid + another eligible visible | DetectionTarget=nearest; meter=0 | DETECT |
| RECOVER | recovery complete + old target invalid + none visible | clear stale state | PATROL |
| SEARCH | same CurrentTarget visible | clear SearchContext | CHASE |
| SEARCH | current target hidden + another eligible visible | clear old current/search; DetectionTarget=new; meter=0 | DETECT |
| SEARCH | timeout | clear target/detection/meter/search | PATROL |

No other semantic state is introduced by this document.

---

# 18. PATROL Overview

PATROL is a hierarchical spatial-planning state, not random waypoint wandering.

```text
authoritative position
→ resolve SpatialNode + Region
→ update actual visitation
→ validate current GlobalPatrolObjective
→ if absent/invalid: GlobalPatrolPlanner
→ RegionGraph route/frontier
→ LocalPatrolSelector
→ NavigationController complete-path validation
→ destination execution
→ physical visitation update
→ continue objective or select next
```

A current global objective persists across multiple local destinations until:

- target region is physically visited;
- target region becomes unreachable/disabled;
- RegionGraph version/topology invalidates the route;
- higher-priority FSM state interrupts PATROL;
- recovery concludes objective cannot be served.

This persistence is important: global planning must not collapse back into local random selection at every node.

---

# 19. SpatialGraph

## 19.1 Current implementation

`NavMeshSpatialGraphBuilder.Build()` uses `NavMesh.CalculateTriangulation()`.

Current graph semantics:

- one `SpatialNode` per valid NavMesh triangle;
- node position at triangle centroid;
- neighbor relationships from welded shared edges;
- stable IDs only within that generated graph;
- NavMesh area/triangle metadata retained;
- debug can compute node/edge/component/isolated counts.

## 19.2 v1.1 decision

Keep this graph for local topology, provided validation passes on the playable Research Facility.

Do not expose triangle nodes as semantic “rooms.”

## 19.3 Build lifecycle and compatibility identity

Build/rebuild SpatialGraph when:

- scene/NavMesh initialization requires it;
- NavMesh content changes/rebakes;
- explicit editor/runtime rebuild is invoked.

Do not rebuild each frame or per planner decision.

`SpatialNodeId` is build-local. A change in NavMesh triangulation may change node count/order/IDs even when gameplay regions retain stable `RegionId`.

Therefore every SpatialGraph instance used with a baked node mapping exposes or can derive a **compatibility identity** representing the concrete SpatialGraph build to which node IDs belong.

Conceptual name:

```text
SpatialGraphBuildSignature
```

The exact signature/hash algorithm is **IMPLEMENTATION BINDING TBD**. The behavior requirement is not TBD: two graph builds whose node IDs/mapping are not guaranteed compatible must not compare as the same compatibility identity.

A rebuilt SpatialGraph requires RegionGraph mapping compatibility validation before `NodeToRegionMap` may be consumed.

---

# 20. RegionDefinition

## 20.1 Strategy

**PROJECT BASELINE:** canonical AI Patrol Regions are designer-authored/designer-defined and deterministically validated.

**DETAILED-DESIGN DECISION:** use a hybrid authoring/runtime representation:

```text
Scene-authored RegionDefinition volumes/metadata
        ↓ editor validation/bake
RegionGraphAsset (ScriptableObject or equivalent immutable runtime asset)
        ↓
Runtime RegionGraph
```

Why this is the best fit:

- one fixed designer-controlled Research Facility;
- rooms/corridors/junctions are easiest to author in scene space;
- stable RegionId is independent from changing NavMesh triangle IDs;
- baked runtime data is deterministic and testable;
- region adjacency/door bindings can be versioned;
- the runtime planner does not need to query arbitrary authoring components every decision.

Exact Unity serialization class names are implementation bindings; the **hybrid strategy is the detailed-design decision**.

Automatic NavMesh clustering may be an editor validation/helper, never canonical RegionId authority without a future ADR.

## 20.2 Conceptual schema

```text
RegionDefinition
- RegionId
- GameplayZoneId
- authored spatial membership volume(s)
- adjacent RegionIds
- enabledByDefault
- relevant DoorIds / controlled edges
- optional semantic tags only when a consumer exists

RegionDefinitionSet / authoring package
- RegionDefinitionVersion
- RegionDefinition[]
```

## 20.3 Gameplay Zone distinction

```text
Gameplay Zone ≠ AI Patrol Region
```

The three macro zones remain:

- Research & Storage Sector;
- Power & Engineering Sector;
- Security & Containment Sector.

A gameplay zone can contain multiple AI patrol regions. Exact count is **TBD — playable map authoring/validation**.

---

# 21. RegionGraph

Runtime conceptual view:

```text
RegionGraph
- RegionsById
- RegionEdges
- NodeToRegionMap
- ConnectedComponent information
- current edge availability
- RegionDefinitionVersion
- SpatialGraphCompatibilityIdentity
```

Baked conceptual asset:

```text
RegionGraphAsset
- RegionDefinitionVersion
- SpatialGraphBuildSignature / equivalent compatibility reference
- NodeToRegionMap
- region metadata
- edge / DoorId metadata
```

Exact field/class names and signature algorithm are implementation bindings. Compatibility semantics are frozen.

## 21.1 SpatialGraph / RegionGraph compatibility guard

**DETAILED-DESIGN DECISION — HARD COMPATIBILITY INVARIANT**

Before any baked `NodeToRegionMap` is exposed to planners/coverage:

```text
current SpatialGraph compatibility identity
==
baked RegionGraphAsset compatibility identity
```

If not equal:

```text
RegionGraph validation failure
→ stale NodeToRegionMap MUST NOT be consumed
→ no Region-based CoverageMemory write using that map
→ no GlobalPatrolPlanner/LocalPatrolSelector decision using that map
→ request rebake/rebind when available
→ otherwise controlled Fixed PatrolRoute fallback
→ emit development diagnostic / typed reason
```

The runtime must never “best effort” index a stale `NodeToRegionMap` by current SpatialNodeId.

A matching version number alone is insufficient unless it is the project's actual compatibility identity. Conversely, this contract does not require any specific hash algorithm.

## 21.2 Validation contract

RegionGraph validation requires at minimum:

- unique/non-empty RegionIds;
- valid RegionDefinitionVersion;
- SpatialGraph compatibility identity match;
- every patrol-eligible SpatialNode has exactly one canonical Region mapping;
- no mapped SpatialNodeId falls outside current graph bounds;
- mapped RegionIds exist and are enabled/disabled according to authoring;
- edges reference valid regions;
- DoorIds reference known topology controls;
- connected-component analysis completes.

Validation result:

```text
RegionGraphValidationResult
- IsValid
- ReasonCode
- RegionDefinitionVersion
- CurrentSpatialGraphCompatibilityIdentity
- BakedSpatialGraphCompatibilityIdentity
```

## 21.3 Edge semantics

```text
RegionEdge
- FromRegionId
- ToRegionId
- DoorId?
- Enabled/Available
- optional RouteClass only if a concrete consumer exists
```

`MainRoute != StalkerPatrolRoute`.

Main/Alternative route metadata must not force Main Route priority.

## 21.4 Warden relationship

AI `RegionGraph` and Warden `FacilityGraph` are distinct views.

They may share:

```text
GameplayZoneId
RegionId
DoorId
```

They do not share:

- graph object ownership;
- triangle topology;
- necessarily identical edges;
- planner semantics.

---

# 22. SpatialNode-to-Region Mapping

## 22.1 Bake-time mapping

For every patrol-eligible SpatialNode:

1. obtain node centroid from the SpatialGraph build being baked;
2. evaluate authored RegionDefinition membership;
3. require exactly one canonical RegionId;
4. store `SpatialNodeId → RegionId` in baked data;
5. bind the asset to the current SpatialGraph compatibility identity;
6. store RegionDefinitionVersion;
7. validate adjacency/door metadata.

## 22.2 Boundary rule

**DETAILED-DESIGN DECISION:** ambiguous boundary membership is an authoring error, not a runtime arbitrary priority rule.

- zero matching regions → `NodeRegionUnassigned`;
- more than one matching region → `NodeRegionOverlap`;
- author fixes the volume boundary or explicit mapping;
- bake does not silently choose “first region.”

This prevents nondeterministic or hierarchy-order-dependent RegionId assignment.

## 22.3 Runtime compatibility rule

The mapping may be read only after `RegionGraphValidationResult.IsValid == true`.

```text
SpatialGraph identity mismatch
→ SpatialGraphCompatibilityMismatch
→ mapping inaccessible to patrol/coverage
→ controlled fallback
```

If runtime code receives a node ID for which no validated mapping exists, treat it as a RegionGraph failure for Region-based planning; do not infer RegionId from stale array position.

## 22.4 Disabled regions

A disabled region may retain canonical mapping but is excluded from:

- active reachable-region set;
- active coverage denominator;
- patrol selection.

## 22.5 Disconnected node

A node that exists but cannot join the intended patrol component receives a validation diagnostic. It is not repeatedly selected by planners.

## 22.6 Dynamic doors

Door changes affect edge availability/reachability only.

They never rewrite:

- canonical `RegionId`;
- node-to-region membership;
- SpatialGraph/RegionGraph compatibility identity.

---

# 23. CoverageMemory

`CoverageMemory` is the canonical mutable source of truth defined in §11.3.

## 23.1 Preconditions

Coverage writes require:

- authoritative Stalker simulation;
- valid current SpatialNode resolution;
- valid RegionGraph compatibility when writing region data;
- confirmed physical visitation.

If RegionGraph compatibility is invalid, do not write region visitation using stale mapping. Node visitation may be retained only if its current SpatialGraph identity is internally valid, but hierarchical PATROL must use controlled fallback until region mapping is rebound.

## 23.2 Actual visitation rule

Never mark a node/region visited merely because a planner selected it.

### SpatialNode visited

A SpatialNode becomes visited when authoritative physical Stalker movement confirms one of:

1. Navigation reports arrival at that selected SpatialNode destination; or
2. the current-node resolver confirms the authoritative agent is physically attributable to that node during bounded traversal sampling.

The resolver must only mark a node when:

- `NavMeshAgent.isOnNavMesh` is true;
- node resolution is valid for the current SpatialGraph;
- visitation is a physical fact, not a planner selection.

### Region visited

A Region becomes visited when the Stalker physically visits a SpatialNode whose RegionId is obtained from a **validated compatible** `NodeToRegionMap`.

Not when:

- region is selected;
- a route is calculated;
- an edge to it is planned;
- an incompatible/stale node mapping happens to contain the same numeric node ID.

## 23.3 Memory update

```text
confirmed physical node visit
→ CoverageMemory.MarkNodeVisited(nodeId, authoritativeTime)
→ NodeVisitCount++
→ NodeLastVisited=time
→ append bounded RecentNodeHistory

if RegionGraph mapping valid:
    resolve regionId from validated NodeToRegionMap
    if meaningful region entry/visit condition satisfied:
        RegionVisitCount++
        RegionLastVisited=time
        append bounded RecentRegionHistory
```

To avoid inflating counts while standing still, a visit event is recorded only on meaningful node/region entry or confirmed arrival, not every simulation tick.

## 23.4 Consumers

Read-only consumers:

- `GlobalPatrolPlanner`;
- `LocalPatrolSelector`;
- `StalkerDebugProvider`;
- metric evaluator;
- optional development diagnostics.

No consumer keeps a parallel mutable visit database.

---

# 24. GlobalPatrolPlanner

## 24.1 Inputs

```text
CurrentRegionId
RegionGraph
CoverageMemory
reachable enabled regions
recent region history
current door/topology availability
```

## 24.2 Output

```text
GlobalPatrolObjective
- TargetRegionId
- SelectedAt
- ReasonCode
- optional planned Region route / NextRegionId
```

## 24.3 Deterministic ranking

**DETAILED-DESIGN DECISION:** use lexicographic ranking rather than introducing new global score weights before tuning evidence.

Eligible regions are ordered by:

1. lower `RegionVisitCount`;
2. older `RegionLastVisited` (`NeverVisited` ranks oldest);
3. avoid immediate region backtrack when an equal-coverage alternative exists;
4. lower recent-history frequency;
5. lower RegionGraph route-hop cost from current region;
6. stable `RegionId` ordinal as final deterministic tie-break.

This preserves “least-covered / most-stale” as the primary global objective and prevents secondary convenience terms from starving an under-covered region.

No unseeded randomness is used.

## 24.4 Reachability

Reachability is calculated on current RegionGraph edge availability.

Exclude:

- disabled region;
- region outside current connected/reachable component;
- region whose route is blocked by current door topology;
- invalid region definition.

## 24.5 Objective invalidation

If target region becomes unreachable:

```text
invalidate objective
→ PatrolDecisionReason.TargetRegionBecameUnreachable
→ recompute reachable regions
→ select another objective
```

When a door later reopens, the region becomes eligible again; its low coverage/staleness naturally returns it to priority.

---

# 25. LocalPatrolSelector

## 25.1 Reuse rule

Do not delete M1-026 local logic.

Reuse:

- bounded BFS candidate generation;
- staleness;
- visit count;
- connectivity/degree;
- immediate-backtrack penalty;
- deterministic selection;
- current/previous node memory.

## 25.2 Candidate generation

Given `GlobalPatrolObjective`:

1. resolve current SpatialNode;
2. resolve RegionGraph route or next frontier region;
3. generate a bounded BFS neighborhood from current node;
4. retain candidates that:
   - belong to target region, or
   - belong to the next RegionGraph step/frontier, or
   - make explicit graph progress toward that frontier;
5. if target is current region, select useful under-covered nodes inside it.

Candidate bound remains configurable.

Current implementation evidence uses `candidateBfsDepth = 3`; this is **CURRENT VALUE**, not final tuning.

## 25.3 Candidate factors

Possible local score:

```text
LocalPatrolScore =
  + normalizedNodeStaleness * wStaleness
  + normalizedConnectivity * wConnectivity
  + globalProgress * wProgress
  - normalizedVisitCount * wVisit
  - immediateBacktrack * wBacktrack
  - recentNodePenalty * wRecent
  - normalizedTravelCost * wTravel
  - inappropriateDeadEndPenalty * wDeadEnd
```

All final weights are **TUNING TBD**.

Existing staleness/connectivity/backtrack values should be preserved initially for regression comparison, then migrated into the new selector configuration.

## 25.4 Deterministic tie-break

After score:

1. lower visit count;
2. older last visited;
3. lower stable SpatialNodeId.

No unseeded random tie-break in baseline.

## 25.5 Path validation

A candidate is not accepted solely because it exists in the graph.

Navigation path evaluation must return an exact complete path for a normal patrol destination.

Reject typed reasons such as:

- `DestinationInvalid`;
- `PathPartial`;
- `PathInvalid`;
- `DoorBlocked`;
- `Disconnected`;
- `RegionInvalid`.

Because Unity synchronous path checks can be expensive, candidate path checks are bounded; never path-check an unbounded list each frame.

---

# 26. Coverage Semantics and Guarantee

## 26.1 Invariant

```text
If a region remains reachable
AND PATROL remains active long enough
AND higher-priority states do not continuously interrupt
THEN that region remains eligible until visited.
```

## 26.2 Implementation behavior making it testable

- unvisited/least-visited regions cannot be permanently excluded by local BFS radius;
- a GlobalPatrolObjective persists while still valid;
- local candidates are selected to progress toward the global region;
- failing one local candidate does not mark the region visited;
- temporary door isolation invalidates the objective but does not falsely increase visit count;
- on reopening, low-coverage region returns to eligible set.

The system does not claim mathematical 100% coverage under arbitrary interruptions or permanently disconnected topology.

---

# 27. CHASE

## 27.1 Entry

`DETECT → CHASE` only after Detection Meter reaches FULL and DetectionTarget is promoted to `CurrentTarget`.

## 27.2 Authoritative update

While `CurrentTarget` remains eligible and newly legally visible:

```text
VisionObservation(CurrentTarget)
→ update legal observed position/direction/time
→ update StalkerMemory LKP knowledge
→ issue chase destination from ObservedPosition
```

The chase destination may be refreshed on meaningful observation/path triggers. Do not freeze an arbitrary Hz in this document.

## 27.3 No prediction baseline

No intercept/prediction system is added.

Reason: current gameplay requires vision/spatial pursuit, and no evidence establishes that predictive chase is needed.

## 27.4 LOS loss

On the first authoritative perception update that loses legal LOS:

```text
keep last legal observation
→ LastKnownPosition remains last legal ObservedPosition
→ LastSeenDirection remains last legal observation direction
→ TargetLastSeenTime remains last observation time
→ create StalkerSearchContext
→ FSM CHASE → SEARCH
```

After that transition, hidden current player position is forbidden as a chase/search source.

---

# 28. ATTACK

## 28.1 Ownership

```text
FSM decides ATTACK transition
→ StalkerAttackController begins authoritative attack episode
→ Hit Moment resolves at most once
→ authoritative gameplay side effect
→ ATTACK completes
→ mandatory RECOVER
```

Planner and sensor never transition to ATTACK.

## 28.2 Entry guard

From CHASE:

- CurrentTarget exists;
- CurrentTarget target-eligible;
- physically visible;
- within `AttackRange`.

## 28.3 Attack episode identity and resolution guard

**DETAILED-DESIGN DECISION — EXACTLY-ONCE GAMEPLAY INVARIANT**

Every authoritative `ATTACK` entry creates one new attack episode with stable authority-local identity.

Conceptually:

```text
AttackEpisode
- EpisodeIdentity / AttackSequence
- TargetIdAtEntry
- StartedAt / StartedTick
- HitMomentResolved
- Outcome
- ResolutionTime/Tick?
```

Exact field names/storage are implementation bindings. Required semantics are frozen.

One attack episode obeys:

```text
one ATTACK episode
→ Hit Moment resolver may commit once maximum
→ damage application at most once
→ authoritative attack outcome commit at most once
→ gameplay telemetry outcome at most once where applicable
```

The `StalkerAttackController` owns the episode and the resolution guard.

## 28.4 Wind-up

`AttackWindup` belongs to Stalker attack configuration.

The authoritative attack controller tracks wind-up using authoritative simulation delta/tick.

Client telegraph/animation is presentation derived from synchronized action state; client animation events are not authoritative hit callbacks.

## 28.5 Hit Moment resolution algorithm

Conceptual authoritative operation:

```text
ResolveHitMoment(activeEpisode):

1. require State Authority execution
2. require FSM semantic state == ATTACK
3. require activeEpisode identity == current attack episode
4. if activeEpisode.HitMomentResolved:
       return AlreadyResolved
       // no damage, no second authoritative outcome, no second gameplay telemetry outcome
5. atomically/authoritatively mark HitMomentResolved before invoking irreversible side effect
6. validate target gameplay eligibility / authoritative Player-Lifecycle protection
7. validate Attack Hit Range
8. resolve Hit or Miss
9. apply damage at most once if Hit
10. persist episode Outcome
11. expose one authoritative outcome fact for presentation/telemetry adapters
```

“Atomically” here is an ownership/order invariant inside the single authoritative simulation path; it does not require a database transaction.

Duplicate causes that must be harmless include:

- duplicate animation event callback;
- duplicate method invocation;
- repeated update/tick condition;
- presentation replay;
- late join reconstruction;
- proxy RPC/presentation callback;
- accidental resolver re-entry.

Duplicate invocation returns a controlled `AlreadyResolved` result and performs no irreversible gameplay side effect.

## 28.6 Hit Moment gameplay rule

**PROJECT BASELINE:** do not add a new Vision LOS/wall/door MISS condition at Hit Moment.

Baseline hit validity is:

```text
target remains gameplay-valid
AND
target within Attack Hit Range
```

Hit/damage resolution also respects authoritative Player/Life-State protection already frozen by the player contract, if any.

Combat collider/hitbox refinement belongs to the combat implementation contract.

## 28.7 Target invalid during ATTACK

```text
invalidate/clear active target eligibility immediately
→ invalid target receives no damage
→ Hit Moment may resolve once as Miss if it has not already resolved
→ ATTACK still completes
→ mandatory RECOVER
```

If the episode already resolved before target invalidation, the invalidation must not trigger a second resolution.

## 28.8 Result

Current gameplay result remains:

```text
StalkerAttackResult
- None
- Hit
- Miss
```

Recommended internal resolution result additionally distinguishes control-flow outcomes:

```text
AttackResolutionResult
- ResolvedHit
- ResolvedMiss
- AlreadyResolved
- NoActiveEpisode
- NotStateAuthority
```

This richer internal result does not add a gameplay state.

## 28.9 ATTACK completion

For Stalker v1.1 gameplay semantics, the authoritative Hit Moment resolution attempt completes the ATTACK gameplay episode.

```text
Wind-up
→ ResolveHitMoment once
→ Hit or Miss
→ ATTACK → RECOVER
```

No additional gameplay-active post-hit ATTACK duration is introduced.

A presentation/animation tail may continue visually if required, but it must not delay authoritative entry into RECOVER, enable another Hit Moment, or create a second gameplay cooldown.

RECOVER remains mandatory whether the episode Hit, Missed, or the target became invalid.

---

# 29. RECOVER

`RECOVER` is a semantic post-attack FSM state.

It is not navigation recovery.

## 29.1 Entry

Every resolved ATTACK enters RECOVER.

`AttackRecovery` is the mandatory duration.

No second hidden attack cooldown is added by this design.

## 29.2 During RECOVER

If CurrentTarget becomes invalid:

- clear/mark target invalid immediately;
- do not apply future attack damage;
- do not exit RECOVER early.

## 29.3 Exit after recovery time

- current target valid + visible → CHASE;
- current target valid + hidden → SEARCH using last legal knowledge;
- old target invalid + another visible eligible player → DETECT with meter 0;
- old target invalid + none → PATROL.

---

# 30. SEARCH Overview

**Hard invariant:**

```text
SEARCH MUST NOT read hidden Player Transform.
```

Flow:

```text
immutable SearchOriginLKP
+ immutable SearchOriginDirection (last legal LOS bearing)
+ SearchStartTime
+ legal spatial topology
→ Phase A: LKP destination if valid
→ bounded candidate generation
→ endpoint-radius filtering
→ deterministic scoring
→ complete-path navigation
→ actual candidate visitation
→ repeat
→ legal Vision reacquisition OR timeout
```

Allowed information:

- `StalkerMemory` from prior legal observation;
- `SearchRadius`;
- `SearchDuration`;
- current validated RegionGraph/SpatialGraph/door state;
- new legal `VisionObservation[]`.

Forbidden:

- hidden target current Transform;
- target velocity/facing sampled after LOS loss;
- network transform after LOS loss;
- telemetry position;
- Profile/AED analytical data.

## 30.1 `SearchRadius` semantic

**DETAILED-DESIGN DECISION:** `SearchRadius` is the legal **candidate endpoint envelope** around immutable `SearchOriginLKP`.

A candidate is inside the search envelope when:

```text
distance(candidateEndpoint, SearchOriginLKP)
<= SearchRadius
```

`SearchRadius` does **not** require every point of the NavMesh path geometry to remain inside that Euclidean radius.

Therefore a candidate endpoint inside SearchRadius may be accepted even if its complete legal NavMesh path detours outside the radius around a wall/corridor/door layout, provided:

- candidate endpoint is inside SearchRadius;
- path status is `PathComplete`;
- the path does not traverse forbidden/blocked topology;
- SearchDuration / search budget remains valid;
- travel/path cost may penalize an excessive detour in scoring/filtering.

Reason: `SearchRadius` limits the Stalker's spatial hypotheses around the last observed point. NavMesh path geometry represents legal traversal constraints and can legitimately detour around geometry. Constraining every path corner to the radius would make nearby legal hypotheses incorrectly unreachable in corridor-heavy maps.

This does not create a hidden pursuit envelope. Endpoint generation remains anchored to immutable LKP, never to the hidden target.

---

# 31. SearchContext Entry Contract

## 31.1 Who writes

`StalkerFSM` transition logic creates `StalkerSearchContext` from `StalkerMemory` at `CHASE → SEARCH` or eligible `RECOVER → SEARCH`.

## 31.2 Exact source

```text
SearchOriginLKP
= LastKnownPosition from the last legal CurrentTarget VisionObservation

SearchOriginDirection
= LastSeenDirection
= normalized bearing from Stalker vision origin
  toward the target's last legally observed point
  in that same accepted VisionObservation

SearchStartTime
= authoritative current simulation time

SearchOriginRegionId
= RegionResolver(SearchOriginLKP) if RegionGraph is valid/compatible
```

`SearchOriginDirection` is not player facing, player velocity, predicted direction, or hidden movement.

If RegionGraph compatibility is invalid, Search may still use legal LKP/spatial navigation that does not consume stale region mapping, but Region-based search candidates are unavailable until compatibility is restored.

## 31.3 SearchDuration timing

Starts at SEARCH entry, not on arrival at LKP.

## 31.4 Immutable episode values

During one SEARCH episode do not mutate:

- `SearchOriginLKP`;
- `SearchOriginDirection`;
- `SearchStartTime`;
- `SearchOriginRegionId` once legally resolved.

If same CurrentTarget becomes legally visible, SEARCH exits immediately; a new search episode would be created only after a later legal LOS loss.

---

# 32. Search Phase A — LKP Approach

First preferred candidate is `SearchOriginLKP`.

NavigationController evaluates it.

### Valid

```text
Destination valid
AND PathComplete
→ navigate to LKP
```

### Off NavMesh

**DETAILED-DESIGN DECISION:** allow a bounded NavMesh projection attempt because the observed player sample point may not lie exactly on the agent NavMesh.

```text
LKP off NavMesh
→ attempt bounded NavMesh projection
→ projected point must remain within approved projection bound of LKP
→ require PathComplete
```

Projection max distance is **TUNING TBD** and is not AED-authorized.

If projection fails, proceed to normal search candidates.

### Partial / invalid / unreachable

Reject LKP destination with typed reason and continue SEARCH candidate policy.

No new FSM state is created.

---

# 33. Search Candidate Generation

## 33.1 Candidate sources

Generate from legal spatial data anchored to immutable `SearchOriginLKP`:

- SpatialNodes whose **endpoint position** is within `SearchRadius` of LKP;
- forward-biased nodes relative to immutable `SearchOriginDirection`;
- useful junction/high-connectivity nodes inside the endpoint envelope;
- region/frontier nodes inside the endpoint envelope where validated topology makes them locally plausible.

Do not generate from current hidden target position, target velocity, network transform, telemetry, or Profile/AED data.

## 33.2 Endpoint envelope vs path geometry

Candidate endpoint test:

```text
Vector3.Distance(candidate.Position, SearchOriginLKP)
<= SearchRadius
```

Path test is separate:

```text
candidate endpoint inside radius
AND path == PathComplete
AND topology legal
→ candidate may remain eligible
```

A complete path is not rejected merely because one or more path corners detour outside the Euclidean SearchRadius.

Long detours may be:

- penalized by travel-cost score;
- rejected by an explicit bounded search-budget/travel-cost policy if one is later tuned;

but they are not rejected by misinterpreting SearchRadius as a path-volume constraint.

## 33.3 Bounded generation

Use a bounded candidate pool.

Recommended sources are collected in deterministic order and deduplicated by SpatialNodeId.

Candidate-limit value is **TUNING TBD**.

`SearchDuration` remains the hard semantic search budget. An internal attempt bound prevents pathological repeated path checks but must not extend SearchDuration.

## 33.4 No-candidate behavior

If no candidate is valid:

- remain in SEARCH;
- stop/hold safely;
- continue legal Vision perception;
- avoid busy-loop replanning every frame;
- retry only on meaningful trigger or bounded planner refresh;
- timeout still returns to PATROL.

---

# 34. Search Candidate Filtering

Recommended enum:

```text
SearchCandidateRejectReason
- None
- OutsideSearchRadius
- AlreadyVisited
- Duplicate
- InvalidNode
- DestinationInvalid
- PathPartial
- PathInvalid
- Disconnected
- DoorBlocked
- RegionInvalid
- SameAsCurrentDestination
```

Filtering order should prefer cheap checks before path calculation:

1. node validity;
2. radius;
3. duplicate/history;
4. region enabled/connectivity;
5. topology/door filter;
6. exact destination/path validation.

A candidate is not marked visited because it was rejected.

---

# 35. Search Scoring

For remaining candidates:

```text
SearchScore =
  + directionAlignment * wDirection
  + noveltyOrStaleness * wNovelty
  + connectivity * wConnectivity
  - normalizedTravelCost * wTravel
  - immediateBacktrack * wBacktrack
  - recentCandidatePenalty * wRecent
```

Final weights are **TUNING TBD**.

Normalization must be deterministic for the current candidate set/config and must handle zero-range denominators safely.

Tie-break:

1. higher score;
2. not yet visited;
3. lower search-episode visit count;
4. lower SpatialNodeId.

No unseeded random selection.

The score contains no player-hidden-position term.

---

# 36. Search Termination and Reacquisition

## 36.1 Same target

```text
new legal VisionObservation(CurrentTarget)
→ clear SearchContext
→ preserve CurrentTarget
→ CHASE immediately
```

Same target has priority if another player is also visible in that same perception update.

## 36.2 Different visible eligible player

When old CurrentTarget remains hidden:

```text
eligible visible other Player
→ clear old CurrentTarget
→ clear SearchContext
→ DetectionTarget = selected nearest eligible visible Player
→ Detection Meter = 0
→ DETECT
```

No direct CHASE of the new player.

## 36.3 CurrentTarget invalid

Handle immediately:

```text
clear CurrentTarget
clear stale DetectionTarget
meter = 0
clear SearchContext
reevaluate visible eligible players
→ DETECT if any
→ else PATROL
```

## 36.4 Timeout

At `SearchDuration`:

```text
clear CurrentTarget
clear stale DetectionTarget
Detection Meter = 0
clear SearchContext
stop/clear search destination as appropriate
→ PATROL
```

---

# 37. Navigation Contract

## 37.1 Game-specific plan result

```text
NavigationPlanStatus
- PathComplete
- DestinationInvalid
- AgentUnavailable
- PathPartial
- PathInvalid
```

## 37.2 Execution status

```text
NavigationExecutionStatus
- Idle
- Moving
- Arrived
- RepathPending
- NoProgress
- Stuck
- Failed
```

`PathStale` is represented as a failure/replan reason because it describes path validity, not a long-lived semantic behavior state.

## 37.3 Destination evaluation

For PATROL/SEARCH exact destinations:

1. verify agent exists/enabled;
2. verify `isOnNavMesh`;
3. validate/project destination only according to explicit destination policy;
4. calculate a bounded path;
5. require `PathComplete`;
6. request destination/path execution;
7. monitor `pathPending`;
8. when pending clears, verify resulting path status remains acceptable.

Important Unity semantics:

- `SetDestination == true` means destination request accepted, not “complete path confirmed.”
- static/agent path calculation can return complete or partial; inspect `NavMeshPath.status`.
- synchronous candidate path checks must remain bounded.

## 37.4 NavMeshLink / OffMeshLink

No project evidence currently requires special Stalker link traversal.

**IMPLEMENTATION BINDING TBD:** if the playable map contains NavMeshLink/OffMeshLink traversal for Stalker, each link must be explicitly classified Stalker-traversable or forbidden and covered by tests. Do not invent jump/climb behavior here.

---

# 38. Progress / No-Progress / Stuck Detection

Velocity alone is insufficient.

## 38.1 Inputs

Use a bounded sampling window over:

- authoritative world displacement;
- `remainingDistance` trend after `pathPending == false`;
- `desiredVelocity`;
- `hasPath`;
- `pathStatus`;
- `isPathStale`;
- `isOnNavMesh`;
- destination age;
- repath history.

## 38.2 Internal progression model

```text
Moving
→ SuspectedNoProgress
→ ConfirmedStuck
→ Recovery
→ Moving / Failed
```

`SuspectedNoProgress` is an internal monitor state, not a Stalker FSM state and need not appear in the public execution enum.

## 38.3 Suspected no-progress

Enter when:

- an active movement intent exists;
- path is not pending;
- expected remaining distance is meaningfully above arrival threshold;
- displacement and/or remaining-distance improvement remain insufficient during configured window.

All numerical windows/epsilons are **TBD — profiler/playtest**.

## 38.4 Confirmed stuck

Confirm only after evidence such as:

- no-progress persists through the confirmation window; and/or
- one bounded repath attempt fails to restore progress;
- agent/path remains in a state that should otherwise allow motion.

Avoid declaring stuck while legitimately waiting for `pathPending`.

---

# 39. Navigation Recovery

Recovery ladder:

```text
1 validate destination
2 evaluate path
3 PathComplete → execute
4 Partial/Invalid → reject candidate
5 next planner candidate
6 path stale/topology changed → repath
7 no progress → suspect
8 confirmed no progress → retry same logical objective once/bounded
9 alternate local destination toward same global objective
10 global objective unreachable → choose alternate global objective
11 safe behavioral fallback
```

## 39.1 PATROL fallback

If hierarchical spatial system cannot serve a valid patrol destination, fixed waypoint fallback may activate.

## 39.2 SEARCH fallback

SEARCH never switches to fixed patrol route merely because one search candidate fails.

Instead:

- try next legal search candidate;
- safe hold when exhausted;
- remain available for legal reacquisition;
- timeout according to SearchDuration.

## 39.3 Warp

`NavMeshAgent.Warp` is not normal recovery.

Emergency Warp is only permitted if all are true:

- agent is irrecoverably outside legal NavMesh due to a known scene/engine failure;
- safe recovery anchor is designer/implementation validated;
- Warp cannot create surprise attack/unfair player contact;
- event is recorded as a development diagnostic;
- it is not used to hide planner/path bugs.

Otherwise fail/replan/fallback.

---

# 40. Dynamic Door / Topology Contract

Authoritative event:

```text
DoorStateChanged(DoorId, NewState)
→ update affected RegionGraph edge availability
→ NavigationController checks active path relevance/staleness
→ active planner reevaluates if affected
```

No all-door polling each frame.

## 40.1 Door closes mid-path

- edge becomes unavailable;
- current path considered suspect/stale if it traverses affected connection or NavMesh reports stale/invalid;
- re-evaluate path;
- if current local destination no longer complete-reachable, reject/replan;
- if global target region becomes unreachable, invalidate global objective.

## 40.2 Door opens

- restore edge availability;
- do not force immediate global objective switch;
- newly reachable under-covered regions become candidates at next valid global selection.

## 40.3 SEARCH candidate blocked

Reject typed `DoorBlocked` or path-derived failure; continue candidate set.

## 40.4 Vision doorway loss

A closed door that breaks LOS produces legal LOS loss and therefore may trigger CHASE → SEARCH using the last valid observation.

---

# 41. Fixed Waypoint Fallback

Keep `PatrolRoute`.

## 41.1 Activation

Allowed reasons:

```text
SpatialGraphInitializationFailed
RegionGraphUnavailable
RegionMappingInvalidAtRuntime
NoReachableGlobalRegionAfterValidation
RepeatedPatrolPlanningFailure
ExplicitDebugFallbackMode
```

Do not activate fixed fallback for a single rejected local candidate.

## 41.2 Behavior

Fallback uses the existing deterministic `PatrolRoute` sequence and `StalkerNavigationController`.

It does not alter the six-state FSM.

## 41.3 Exit

Recommended:

- match/agent reinitialization after corrected graph becomes valid; or
- explicit controlled recovery/rebuild event proving graph/region system valid again.

Do not oscillate between dynamic and fixed modes every frame.

## 41.4 Observability

Expose:

- `FallbackActive`;
- activation reason;
- activation time;
- graph/region validation status.

Fallback is safe degradation, not a mechanism to hide a spatial defect.

---

# 42. Photon Fusion 2 Binding

## 42.1 Boundary

```text
NetworkObject
└── StalkerNetworkBinding : NetworkBehaviour
        │
        ├── authority gate
        ├── synchronized client-required state
        └── Fusion simulation callback
                │
                ▼
             StalkerRoot
                │
                └── pure C# / Unity-bound AI services
```

Exact class name `StalkerNetworkBinding` is a recommendation, not current implementation evidence.

## 42.2 State Authority behavior

Only `Object.HasStateAuthority` may advance authoritative Stalker AI.

Host/State Authority owns:

- Vision;
- target eligibility/selection;
- Detection Meter;
- `StalkerMemory`;
- FSM;
- `CoverageMemory` writes;
- GlobalPatrolPlanner;
- LocalPatrolSelector;
- SearchPlanner;
- navigation intent;
- attack episode creation;
- Hit Moment resolution;
- damage application request;
- authoritative Stalker gameplay telemetry facts.

Non-authoritative proxies do not tick those decisions independently.

## 42.3 Simulation callback

Network-state-affecting AI progression should be initiated from the State Authority's Fusion simulation tick, normally `FixedUpdateNetwork()`.

Concept:

```csharp
public override void FixedUpdateNetwork()
{
    if (!Object.HasStateAuthority)
        return;

    _stalkerRoot.Simulate(Runner.DeltaTime);
    PublishRequiredNetworkState();
}
```

This is conceptual binding, not a claim that the class already exists.

## 42.4 Exactly-once authoritative attack side effect

Fusion authority is necessary but not sufficient as an application-level duplicate-side-effect guard.

The Stalker runtime therefore requires the attack-episode invariant from §28:

```text
ATTACK entry confirmed by authoritative FSM
→ create new authoritative attack episode identity
→ Hit Moment can transition episode Unresolved → Resolved once
→ only that transition may apply damage / commit attack outcome
```

Client/presentation/network events must never call gameplay damage directly.

A duplicate resolver invocation on State Authority must see the episode already resolved and produce no side effect.

Late join/reconnect behavior:

```text
late join during wind-up
→ reconstruct current attack presentation from synchronized durable action state
→ do not create a new attack episode
→ do not replay Hit Moment

late join after Hit Moment
→ reconstruct post-hit/current attack or RECOVER presentation
→ do not replay damage/outcome
```

The exact `[Networked]` fields are **IMPLEMENTATION BINDING TBD**, but the synchronized durable state must contain enough action/attack progress identity to prevent a proxy from interpreting a historical transient as a new authoritative hit.

A viable binding may use an attack sequence/phase/resolved sequence or equivalent. Exact representation is not frozen.

## 42.5 Networked state candidates

Replicate only client-required durable/presentational state.

Recommended minimal semantics:

- semantic `StalkerState`;
- authoritative monster transform through the project's chosen Fusion transform binding;
- attack/action presentation state sufficient to identify the current attack episode/phase and whether its Hit Moment is historical/current for presentation;
- optional presentation-facing cue only if final gameplay presentation requires it.

Do not replicate by default:

- LastKnownPosition;
- LastSeenDirection;
- SearchContext;
- candidate lists;
- patrol scores;
- CoverageMemory history;
- private reason histories;
- hidden CurrentTarget identity solely for AI logic.

If target-facing presentation requires target identity, that is an explicit presentation/network decision and must not expose hidden information to gameplay UI.

## 42.6 RPC

Photon Fusion RPCs are for punctual events and are not durable network state. A late-joining or reconnecting peer cannot rely on a past RPC having occurred.

Therefore RPC must not be the sole representation of:

- current FSM state;
- current attack episode/phase;
- whether current attack Hit Moment has already occurred;
- other durable state needed after late join/resync.

RPC may be used for genuinely transient request/presentation semantics where appropriate. An RPC never bypasses State Authority or the AttackController resolution guard.

## 42.7 Transform synchronization

Host/NavMeshAgent owns physical Stalker movement.

Exact `NetworkTransform` or equivalent project component settings are **IMPLEMENTATION BINDING TBD**.

Clients consume/interpolate authoritative monster transform rather than running local NavMesh decision simulation.

## 42.8 Join/resync

A joining/reconnecting proxy must reconstruct at least:

- current transform;
- semantic state;
- current action/attack presentation state;
- current attack episode/phase identity semantics sufficient to avoid presentation-driven replay.

Private planner/search/coverage memory remains host-only.

---

# 43. ScenarioConfig / AED Boundary

## 43.1 Parameter classification

| Parameter | Stalker configurable | AED v0 authorized | Notes |
|---|---:|---:|---|
| VisionDistance | Yes | No | perception envelope |
| VisionAngle | Yes | No | full cone |
| DetectionFillRate | Yes | Yes | bounded registry/timing |
| DetectionDecayRate | Yes | Yes | bounded registry/timing |
| PatrolSpeed | Yes | No | fixed/config tuning |
| ChaseSpeed | Yes | Yes | bounded registry/timing |
| SearchDuration | Yes | Yes | bounded registry/timing |
| SearchRadius | Yes | No | fixed/config tuning |
| AttackRange | Yes | No | combat fairness envelope |
| AttackWindup | Yes | No | combat fairness envelope |
| AttackRecovery | Yes | No | mandatory recovery semantics |
| StalkerDamagePercent | Yes | No | combat fairness envelope |

## 43.2 Forbidden AED writes

AED may not mutate:

```text
CurrentTarget
DetectionTarget
DetectionMeter directly
LastKnownPosition
LastSeenDirection
SearchContext
FSM state
GlobalPatrolObjective
local navigation destination
```

AED changes only validated, authorized ScenarioConfig keys at allowed decision boundaries.

---

# 44. Telemetry

## 44.1 Runtime boundary

```text
authoritative gameplay
→ authoritative gameplay fact
→ StalkerTelemetryAdapter / owning gameplay telemetry emitter
→ schema-approved TelemetryEvent

Telemetry
X→ Stalker decision input
```

Stalker never queries telemetry storage/Profile/AED for runtime target/search decisions.

For ATTACK:

```text
StalkerAttackController
→ commits one authoritative AttackOutcomeFact maximum per AttackEpisode
→ gameplay/life-state side effect consumes that committed resolution
→ telemetry may observe the resulting authoritative fact/state transition
```

Telemetry must not invoke damage and must not cause a second attack resolution.

## 44.2 Exactly-once outcome propagation

The attack episode identity/resolution guard is the authoritative deduplication source.

Required semantic:

```text
same AttackEpisode
+ duplicate gameplay callback
+ duplicate presentation callback
+ duplicate telemetry adapter notification
→ no second damage
→ no second authoritative attack-outcome commit
→ no duplicate gameplay telemetry event for the same logical outcome
```

Where the active production event is owned by Player/Life-State (for example `PLAYER_DOWNED`), it is emitted from the authoritative state transition, not by replaying the Stalker Hit Moment.

If a future telemetry schema activates an attack-specific event, the adapter must map one committed attack outcome to at most one event for that episode.

This contract does not invent a new production event or payload.

## 44.3 Existing production telemetry

Schema v1.0 supports gameplay facts such as `PLAYER_DOWNED` with:

```text
context.monsterType = STALKER
reasonCode = STALKER_ATTACK
```

Do not log per-frame transforms.

## 44.4 Monster debug event catalog

Existing schema lists:

```text
MONSTER_TARGET_ACQUIRED
MONSTER_TARGET_LOST
MONSTER_INVESTIGATE_STARTED
MONSTER_ATTACK_RESOLVED
MONSTER_SEARCH_ENDED
```

but explicitly marks this family as not required P0 emitter baseline until event-specific payload/userId contracts are frozen.

Therefore:

- do not silently emit these as production schema events;
- use development diagnostics now;
- if a future telemetry revision activates them, map authoritative runtime facts through the approved payload contract;
- an activated `MONSTER_ATTACK_RESOLVED` must obey the authoritative attack-episode at-most-once rule.

## 44.5 Production vs development

**Production telemetry:** only schema-approved events.

**Development diagnostics:** planner reason, candidate rejection, graph compatibility, path failure, coverage/stuck/search traces, and attack-episode guard traces. These may go to local structured logs/debug capture without claiming telemetry-schema compatibility.

---

# 45. Observability

Recommended `StalkerAIDebugSnapshot`:

```text
StalkerAIDebugSnapshot
- State

- CurrentTargetId?
- DetectionTargetId?
- DetectionMeter

- LastKnownPosition?
- LastSeenDirection?          // last legal LOS bearing, not velocity/facing
- TargetLastSeenTime?

- CurrentSpatialGraphCompatibilityIdentity
- BakedRegionGraphCompatibilityIdentity
- RegionDefinitionVersion
- RegionGraphValidationStatus
- RegionGraphValidationReason

- CurrentRegionId?
- GlobalObjectiveRegionId?
- CurrentSpatialNodeId?
- CurrentDestination?

- NavigationPlanStatus
- NavigationExecutionStatus
- PathStatus
- PathPending
- IsPathStale

- PatrolDecisionReason
- PatrolCandidateNodeId?
- PatrolCandidateRejectReason?
- LocalCandidateCount

- SearchElapsed
- SearchOriginLKP
- SearchOriginDirection
- SearchCandidateNodeId?
- SearchCandidateCount
- SearchCandidateRejectReason?

- ActiveAttackEpisodeIdentity?
- AttackHitMomentResolved
- AttackOutcome
- LastAttackResolutionResult

- NoProgressState
- LastRecoveryReason

- RegionCoverage
- SpatialNodeCoverage

- FixedFallbackActive
- FixedFallbackReason?
```

Snapshot is immutable/read-only for UI/debug.

Debug visualizations may draw:

- Vision cone/ray;
- current path;
- SpatialGraph;
- Region boundaries;
- graph compatibility mismatch status;
- current/global target region;
- LKP and LastSeenDirection bearing;
- SearchRadius endpoint envelope;
- search candidates/rejections;
- blocked door edge;
- current stuck/recovery state;
- current attack episode/Hit Moment resolution state.

Debug systems never write gameplay state.

---

# 46. Reason Codes

Keep reason sets compact, typed, and testable.

## 46.1 `PatrolDecisionReason`

```text
InitialPatrolObjective
LeastVisitedRegion
MostStaleRegion
PreviousObjectiveVisited
TargetRegionBecameUnreachable
TopologyChanged
RegionGraphInvalid
RecoveryReplan
FallbackActivated
```

## 46.2 `PatrolCandidateRejectReason`

```text
None
InvalidNode
RegionInvalid
RegionGraphIncompatible
NotTowardGlobalObjective
AlreadyCurrentDestination
DestinationInvalid
PathPartial
PathInvalid
DoorBlocked
Disconnected
```

## 46.3 `SearchCandidateRejectReason`

```text
None
OutsideSearchRadius
AlreadyVisited
Duplicate
InvalidNode
DestinationInvalid
PathPartial
PathInvalid
Disconnected
DoorBlocked
RegionInvalid
RegionGraphIncompatible
SameAsCurrentDestination
```

## 46.4 `NavigationFailureReason`

```text
None
AgentUnavailable
AgentNotOnNavMesh
DestinationInvalid
PathPendingTimeout
PathPartial
PathInvalid
PathStale
DoorBlocked
NoProgress
Stuck
```

## 46.5 `RecoveryReason`

```text
PathStaleRepath
TopologyChangedRepath
RetryLogicalObjective
AlternateLocalCandidate
AlternateGlobalObjective
RegionGraphCompatibilityFallback
FixedPatrolFallback
EmergencyNavMeshRecovery
```

## 46.6 `TargetLossReason`

```text
LostLOS
TargetInvalid
Disconnected
Downed
Eliminated
SearchTimeout
ReplacedDuringSearch
```

## 46.7 `RegionGraphValidationReason`

```text
None
RegionDefinitionVersionInvalid
SpatialGraphCompatibilityMismatch
NodeRegionUnassigned
NodeRegionOverlap
NodeIdOutOfRange
RegionIdInvalid
EdgeRegionInvalid
DoorBindingInvalid
ConnectedComponentValidationFailed
```

## 46.8 `AttackResolutionResult`

```text
ResolvedHit
ResolvedMiss
AlreadyResolved
NoActiveEpisode
NotStateAuthority
```

Exact enum names may follow project naming conventions; semantic coverage is frozen here.

---

# 47. AI Quality Metrics

No acceptance threshold is frozen.

## 47.1 Metric evaluation primitives

To make independent evaluators deterministic, v1.1 freezes these logical trace units.

### `PatrolTopologySegment`

A PATROL evaluation segment with one stable RegionGraph topology/compatibility snapshot.

A segment begins when PATROL becomes eligible for measurement and ends when any of the following occurs:

- RegionGraph topology availability version changes;
- RegionGraph compatibility becomes invalid;
- fixed fallback activates;
- higher-priority FSM state interrupts PATROL;
- match/evaluation window ends.

A new PATROL segment starts when PATROL resumes under a valid stable topology snapshot.

### `NavigationExecutionEpisode`

One accepted `PathComplete` logical destination execution:

```text
Accepted
→ Moving/RepathPending...
→ terminal:
   Arrived | Failed | CancelledByHigherPriorityState | ReplacedByPlannerOrTopology
```

### `SearchEpisode`

One semantic SEARCH entry through one terminal SEARCH outcome.

### Version rule

Every serialized/evaluated metric includes a metric/protocol version. Same authoritative trace + same metric version must yield the same result.

### Zero-denominator rule

Every rate/coverage metric uses the same deterministic undefined-result rule:

```text
denominator == 0
→ MetricStatus = NOT_EVALUATED
→ value = null
```

Do not coerce an empty denominator to `0`, `1`, or another numeric value. Evaluators must preserve the status and metric version in evidence.

## 47.2 `RegionCoverage_v1.1`

| Field | Contract |
|---|---|
| Unit | unique AI Patrol Regions |
| Purpose | macro patrol coverage within stable topology |
| Numerator | count of distinct denominator Regions physically visited during the `PatrolTopologySegment` |
| Denominator | count of Regions that are enabled and reachable from the Stalker's segment-start Region in the validated RegionGraph snapshot at segment start |
| Window/reset | reset at each `PatrolTopologySegment` start |
| Source | `CoverageMemory` physical Region visit facts + validated RegionGraph snapshot |
| Exclusions | disabled/unreachable Regions at segment start |
| Interpretation | fraction of currently reachable patrol regions physically covered |
| Caveat | topology changes close the segment rather than mutating the denominator in-place |

Formula:

```text
RegionCoverage_v1.1
=
DistinctVisitedRegionsInSegment
/
EligibleReachableRegionsAtSegmentStart
```

## 47.3 `SpatialNodeCoverage_v1.1`

| Field | Contract |
|---|---|
| Unit | unique current-build SpatialNodes |
| Purpose | micro navigable coverage |
| Numerator | distinct denominator SpatialNodes physically visited during the segment |
| Denominator | SpatialNodes in the current compatible SpatialGraph mapped to denominator Regions of `RegionCoverage_v1.1` and eligible for Stalker patrol |
| Window/reset | same `PatrolTopologySegment` |
| Source | `CoverageMemory` node visit facts + compatible `NodeToRegionMap` |
| Exclusions | invalid/unmapped/disabled-region nodes |
| Interpretation | fraction of eligible local navigable nodes physically covered |
| Caveat | compare only the same SpatialGraph compatibility identity/metric version |

No geometric cell/area denominator may be mixed into this metric.

## 47.4 `RegionRevisitRate_v1.1`

This concretizes the architecture-level ambiguous `RevisitRate` at macro level.

| Field | Contract |
|---|---|
| Unit | physical PATROL Region entry events |
| Numerator | Region entry events whose Region had already been physically visited earlier in the same `PatrolTopologySegment` |
| Denominator | all physical PATROL Region entry events in the segment after the first measurable Region entry |
| Window/reset | per `PatrolTopologySegment` |
| Source | `CoverageMemory.RecentRegionHistory` / authoritative Region entry trace |
| Exclusions | non-PATROL entries; events after compatibility invalidation |
| Labels | planner-selected / topology-forced / recovery-forced may be reported as dimensions without changing numerator/denominator |
| Interpretation | repeated macro visitation under stable topology |

The ambiguous name `RevisitRate` must not be emitted without a metric-version alias mapping it to a concrete unit.

## 47.5 `SpatialNodeRevisitRate_v1.1`

| Field | Contract |
|---|---|
| Unit | physical PATROL SpatialNode visit events |
| Numerator | node visit events whose node had already been physically visited earlier in the same segment |
| Denominator | all physical PATROL node visit events in the segment after the first measurable node visit |
| Window/reset | per `PatrolTopologySegment` |
| Source | `CoverageMemory.RecentNodeHistory` |
| Exclusions | non-PATROL visits; incompatible graph periods |
| Interpretation | repeated micro traversal |

This is separate from `RegionRevisitRate_v1.1`.

## 47.6 `RegionImmediateBacktrackRate_v1.1`

This concretizes the architecture-level `ImmediateBacktrackRate` at Region level.

Construct the ordered sequence of distinct physical PATROL Region entries within one `PatrolTopologySegment`:

```text
R0, R1, R2, ...
```

| Field | Contract |
|---|---|
| Unit | eligible Region transition triplets |
| Numerator | triplets `R[i-2] → R[i-1] → R[i]` where `R[i] == R[i-2]` and `R[i] != R[i-1]` |
| Denominator | all consecutive Region transition triplets wholly inside the same segment |
| Window/reset | per `PatrolTopologySegment` |
| Source | authoritative Region-entry trace |
| Exclusions | triplets crossing segment/FSM-interruption boundaries |
| Labels | topology-forced / recovery-forced / planner-choice may be dimensions |
| Interpretation | immediate macro reversal frequency |

No node-level events are mixed into this metric.

## 47.7 `StuckRate_v1.1`

| Field | Contract |
|---|---|
| Unit | eligible `NavigationExecutionEpisode` |
| Numerator | eligible episodes that enter `ConfirmedStuck` at least once |
| Denominator | accepted `PathComplete` navigation execution episodes reaching a terminal outcome other than `CancelledByHigherPriorityState` |
| Window/reset | match or declared evaluation window; accumulator resets at window start |
| Source | `StalkerNavigationController` + `NavigationProgressMonitor` episode trace |
| Exclusions | pre-execution path rejects; higher-priority FSM cancellation |
| Interpretation | fraction of executed navigation episodes that became confirmed stuck |
| Caveat | one episode counts once even if multiple recovery attempts occur |

The denominator is exclusively the eligible `NavigationExecutionEpisode` count defined above.

## 47.8 `PathFailureRate_v1.1`

| Field | Contract |
|---|---|
| Unit | path evaluation attempts |
| Numerator | path evaluation attempts ending in `DestinationInvalid`, `AgentUnavailable`, `PathPartial`, or `PathInvalid` |
| Denominator | all path evaluation attempts that produce one terminal `NavigationPlanStatus` |
| Window/reset | match or declared evaluation window |
| Source | `StalkerNavigationController` plan-result trace |
| Exclusions | runtime `PathStale`, NoProgress, Stuck after an accepted path; those have separate diagnostics/metrics |
| Breakdown | DestinationInvalid / AgentUnavailable / Partial / Invalid |
| Interpretation | pre-execution destination/path rejection rate |

`PathStaleReplanCount` may be reported as a separate diagnostic count; it is not silently folded into `PathFailureRate_v1.1`.

## 47.9 `SearchReacquisitionRate_v1.1`

Terminal SEARCH outcomes are:

```text
SameTargetReacquired
NewEligibleTargetObserved
Timeout
CurrentTargetInvalidNoReplacement
```

| Field | Contract |
|---|---|
| Unit | terminal `SearchEpisode` |
| Numerator | episodes ending `SameTargetReacquired` or `NewEligibleTargetObserved` through new legal `VisionObservation` before timeout |
| Denominator | SEARCH episodes reaching one of the four terminal outcomes above |
| Window/reset | match or declared evaluation window |
| Source | FSM + SearchContext + VisionObservation trace |
| Exclusions | match abort/despawn before a search terminal outcome |
| Breakdown | same target → CHASE; new target → DETECT |
| Interpretation | fraction of completed searches that legally rediscover an eligible player before timeout/invalid termination |

## 47.10 Metric evidence requirements

Development/evaluation trace must preserve enough stable facts to recompute the metric:

- metric/protocol version;
- match/test run identity;
- FSM state transition facts;
- RegionGraph compatibility/topology segment identity;
- Region and SpatialNode physical visit events;
- navigation episode begin/terminal status;
- confirmed stuck transition;
- path evaluation result;
- SEARCH episode start/terminal reason.

This does not require production telemetry to add high-frequency events. Evaluation traces may be development diagnostics/evidence artifacts.

## 47.11 Metric naming invariant

```text
one metric name + metric version
→ one unit
→ one numerator
→ one denominator
→ one reset/window rule
```

If a future study needs another abstraction, create a new metric/version rather than silently changing the denominator.

---

# 48. Tests

This section defines required tests; it does not claim they pass.

## 48.1 EditMode / pure logic

| Test ID | Requirement | Expected result |
|---|---|---|
| STK-E-001 | Region IDs unique | validation success only for unique IDs |
| STK-E-002 | Node mapping exactly one region | zero/overlap rejected with typed reason |
| STK-E-003 | Connected components | deterministic component set |
| STK-E-004 | Disabled region | excluded from active reachability/coverage denominator |
| STK-E-005 | Global ranking unvisited | least-visited region wins before secondary terms |
| STK-E-006 | Global stale tie | older region wins |
| STK-E-007 | Backtrack tie | equal-coverage non-backtrack alternative wins |
| STK-E-008 | Stable tie | RegionId deterministic final tie |
| STK-E-009 | Local BFS bound | candidate count never exceeds bound policy |
| STK-E-010 | Local staleness/connectivity | existing factors preserved in selector |
| STK-E-011 | Local backtrack | previous node penalized |
| STK-E-012 | Search generation | only endpoints inside LKP-centered SearchRadius generated |
| STK-E-013 | Search hidden info | planner API has no hidden Player Transform/velocity input |
| STK-E-014 | Search filtering | each reject reason deterministic |
| STK-E-015 | Search scoring | same context/config → same candidate |
| STK-E-016 | Search tie | deterministic NodeId final tie |
| STK-E-017 | memory lifecycle | create/update/clear exactly on contract |
| STK-E-018 | no meter carry | target change resets meter |
| STK-E-019 | navigation policy | Partial/Invalid rejected for exact patrol/search |
| STK-E-020 | recovery policy | ladder ordering preserved |
| STK-E-021 | reason codes | every failure branch produces controlled code |
| STK-E-022 | CoverageMemory ownership | no second mutable spatial visit store; facade/projection is read-only |
| STK-E-023 | graph compatibility match | compatible SpatialGraph/RegionGraph asset exposes NodeToRegionMap |
| STK-E-024 | graph compatibility mismatch | `SpatialGraphCompatibilityMismatch`; mapping inaccessible |
| STK-E-025 | stale mapping no consume | planner/coverage API cannot read stale NodeToRegionMap |
| STK-E-026 | LastSeenDirection semantic | equals normalized vision-origin→observed-point bearing; not facing/velocity |
| STK-E-027 | metric determinism | same authoritative trace + same metric version → identical metric values |
| STK-E-028 | metric units | Region and SpatialNode revisit/backtrack units never mix |
| STK-E-029 | attack duplicate resolver | second resolve of same episode returns `AlreadyResolved`; no second side effect |
| STK-E-030 | metric zero denominator | returns `NOT_EVALUATED` + null; never numeric coercion |

## 48.2 PlayMode

| Test ID | Requirement | Expected result |
|---|---|---|
| STK-P-001 | Vision distance | outside distance not visible |
| STK-P-002 | FOV full cone | half-angle rule applied |
| STK-P-003 | wall LOS | wall blocks observation |
| STK-P-004 | closed door LOS | door blocks observation |
| STK-P-005 | self collider | self hit ignored |
| STK-P-006 | candidate multi-collider | candidate hierarchy accepted correctly |
| STK-P-007 | Downed visible | may observe physically, selector rejects |
| STK-P-008 | multiple visible in PATROL | nearest eligible becomes DetectionTarget |
| STK-P-009 | DetectionTarget lock | closer newcomer does not steal locked target |
| STK-P-010 | detect fill | visible target fills meter |
| STK-P-011 | detect decay | hidden target decays meter |
| STK-P-012 | detect invalid | reset/reselect per contract |
| STK-P-013 | CHASE update | only legal observation updates destination/LKP |
| STK-P-014 | LOS loss | last observation position+bearing preserved; SEARCH entered |
| STK-P-015 | hidden movement | LKP/LastSeenDirection do not follow hidden transform/velocity |
| STK-P-016 | attack entry | FSM only enters with eligible+visible+range |
| STK-P-017 | hit moment LOS | no new LOS blocker added after attack begins |
| STK-P-018 | target invalid attack | no damage; still RECOVER |
| STK-P-019 | mandatory recover | cannot exit early |
| STK-P-020 | LKP complete | SEARCH first targets LKP |
| STK-P-021 | LKP off NavMesh | bounded projection or candidate fallback |
| STK-P-022 | LKP partial | reject; continue candidate search |
| STK-P-023 | no search candidate | safe hold/perception until timeout |
| STK-P-024 | same target reacquire | immediate CHASE |
| STK-P-025 | new target during search | DETECT with reset meter |
| STK-P-026 | search timeout | clear context/target; PATROL |
| STK-P-027 | PathComplete | destination accepted |
| STK-P-028 | PathPartial | exact candidate rejected |
| STK-P-029 | PathInvalid | rejected |
| STK-P-030 | path stale | repath/replan reason emitted |
| STK-P-031 | door closes mid-path | affected path/objective reevaluated |
| STK-P-032 | door opens | region becomes eligible again |
| STK-P-033 | no-progress | suspected state without false instant stuck |
| STK-P-034 | confirmed stuck | recovery ladder starts |
| STK-P-035 | RegionGraph unavailable | fixed fallback + diagnostic |
| STK-P-036 | global coverage persistence | remote least-covered reachable region remains objective until visited/invalid |
| STK-P-037 | physical visit semantics | selection alone does not mark coverage |
| STK-P-038 | full flow | PATROL→DETECT→CHASE→SEARCH→PATROL |
| STK-P-039 | attack flow | CHASE→ATTACK→RECOVER→legal next state |
| STK-P-040 | runtime NoiseEvent | Stalker behavior unchanged by noise |
| STK-P-041 | stale RegionGraph asset | compatibility mismatch triggers controlled fallback; stale mapping never consumed |
| STK-P-042 | SearchRadius path detour | endpoint inside radius + Complete legal detour outside radius remains eligible |
| STK-P-043 | SearchRadius endpoint outside | endpoint outside radius rejected even if path is short/Complete |
| STK-P-044 | duplicate Hit Moment callback | one authoritative episode applies damage/outcome at most once |

## 48.3 Fusion multiplayer

| Test ID | Requirement | Expected result |
|---|---|---|
| STK-N-001 | State Authority | only Host advances authoritative Stalker |
| STK-N-002 | proxy FSM mutation attempt | cannot authoritatively change state |
| STK-N-003 | proxy target mutation attempt | cannot change authoritative target |
| STK-N-004 | proxy attack attempt | cannot resolve authoritative damage |
| STK-N-005 | 2-player convergence | proxies converge on state/transform/presentation |
| STK-N-006 | 3-player convergence | same |
| STK-N-007 | 4-player convergence | same |
| STK-N-008 | same authoritative attack episode | exactly one Hit Moment/damage resolution and at most one authoritative outcome |
| STK-N-009 | late join during wind-up | presentation reconstructs current episode; no duplicate episode/hit |
| STK-N-010 | late join after Hit Moment | historical Hit Moment is not replayed; no duplicate damage/outcome |
| STK-N-011 | private memory | LKP/Search/Coverage not replicated by default |
| STK-N-012 | host door event | authoritative topology reaction synchronized in observable result |
| STK-N-013 | duplicate resolve invocation on Host | resolution guard suppresses second side effect |
| STK-N-014 | RPC/presentation duplicate | transient duplicate cannot bypass authoritative guard |
| STK-N-015 | resync current attack state | durable synchronized action state reconstructs presentation without replaying damage |
| STK-N-016 | duplicate committed attack fact | applicable gameplay telemetry outcome is emitted at most once |

---

# 49. Edge-Case Matrix

| Edge case | Expected behavior | Owning component | Reason code/result | Test |
|---|---|---|---|---|
| Player Downed | physically observable if LOS; target-ineligible immediately | Eligibility/FSM | `Downed` | STK-P-007 |
| Player Eliminated | clear active target eligibility | Eligibility/FSM | `Eliminated` | target invalidation test |
| Player disconnected | clear active target; reevaluate | Eligibility/FSM | `Disconnected` | disconnect test |
| DetectionTarget invalid | clear, meter=0, reevaluate | FSM/Selector | target-invalid reason | STK-P-012 |
| CurrentTarget invalid in CHASE | clear; DETECT another or PATROL | FSM | target-invalid reason | target invalid test |
| CurrentTarget invalid in ATTACK | no damage if unresolved; ATTACK completes → RECOVER | Attack/FSM | `ResolvedMiss` or already-resolved state | STK-P-018 |
| CurrentTarget invalid in RECOVER | clear now; stay RECOVER | FSM | target-invalid reason | STK-P-019 |
| Multiple visible players | nearest eligible on acquisition; lock during DETECT | Selector | selection reason | STK-P-008/009 |
| LOS lost at doorway | preserve last legal position+bearing → SEARCH | FSM/Memory | `LostLOS` | STK-P-014 |
| Hidden target turns/runs | LastSeenDirection unchanged until legal re-observation | Memory | no update | STK-P-015 |
| LKP off NavMesh | bounded projection; otherwise candidate fallback | Navigation/Search | `DestinationInvalid` | STK-P-021 |
| LKP PathPartial | reject exact LKP | Navigation/Search | `PathPartial` | STK-P-022 |
| LKP unreachable | candidate search; no new state | SearchPlanner | path reason | STK-P-022 |
| Search endpoint inside radius, path detours outside | accept if Complete/legal and budget permits | SearchPlanner/Navigation | `None` | STK-P-042 |
| Search endpoint outside radius | reject even with Complete path | SearchPlanner | `OutsideSearchRadius` | STK-P-043 |
| RegionGraph unavailable | fixed patrol fallback | Patrol coordinator | `RegionGraphInvalid` / fallback reason | STK-P-035 |
| RegionGraph compatibility mismatch | stale mapping inaccessible; rebake/rebind or fixed fallback | RegionGraph/Patrol coordinator | `SpatialGraphCompatibilityMismatch` | STK-E-024/STK-P-041 |
| Node ID exists numerically but mapping is stale | never infer RegionId by stale index | RegionGraph | `SpatialGraphCompatibilityMismatch` | STK-E-025 |
| Region invalid | exclude; diagnostic | RegionGraph | `RegionIdInvalid` | EditMode region test |
| Region disconnected | exclude until reachable | Global planner | disconnected reason | component test |
| No reachable global region | fixed fallback/controlled hold according to fallback availability | Patrol coordinator | fallback reason | STK-P-035 |
| No local patrol candidate | next bounded local policy; if exhausted controlled fallback | Local selector | candidate reason | local failure test |
| Door closes mid-path | invalidate/repath/replan | Navigation/RegionGraph | `TopologyChangedRepath` | STK-P-031 |
| Door opens | edge restored; no forced immediate switch | RegionGraph | topology update | STK-P-032 |
| No Search candidate | safe hold + perceive until timeout | SearchPlanner | search exhaustion diagnostic | STK-P-023 |
| All Search candidates exhausted | same; no hidden tracking | SearchPlanner | exhaustion diagnostic | STK-P-023 |
| Search timeout | clear stale state → PATROL | FSM | `SearchTimeout` | STK-P-026 |
| new target during Search | old hidden + other visible → DETECT | FSM/Selector | `ReplacedDuringSearch` | STK-P-025 |
| same target during Search | CHASE immediately | FSM | reacquired | STK-P-024 |
| NavMeshAgent unavailable | navigation failure; safe fallback/hold | Navigation | `AgentUnavailable` | navigation negative test |
| Agent not on NavMesh | fail navigation; emergency recovery only under strict invariant | Navigation | `AgentNotOnNavMesh` | negative test |
| pathPending too long | typed pending timeout, re-evaluate | Navigation | `PathPendingTimeout` | PlayMode |
| PathPartial | reject exact destination | Navigation | `PathPartial` | STK-P-028 |
| PathInvalid | reject | Navigation | `PathInvalid` | STK-P-029 |
| PathStale | re-evaluate/repath | Navigation | `PathStale` | STK-P-030 |
| NoProgress | enter suspected state | ProgressMonitor | `NoProgress` | STK-P-033 |
| Stuck | recovery ladder | ProgressMonitor/Navigation | `Stuck` | STK-P-034 |
| Fixed fallback active | visible debug state/reason; deterministic route | Patrol coordinator | fallback reason | STK-P-035 |
| duplicate Hit Moment invocation | first commit only; later calls have zero gameplay side effect | AttackController | `AlreadyResolved` | STK-E-029/STK-P-044 |
| duplicate presentation/RPC callback | cannot call authoritative damage path | Fusion binding/AttackController | `NotStateAuthority` or `AlreadyResolved` | STK-N-014 |
| duplicate attack outcome notification | no duplicate schema-approved gameplay outcome for same episode/logical state transition | Telemetry adapter / owning gameplay emitter | dedup diagnostic | STK-N-016 |
| late join during wind-up | reconstruct presentation only; no new attack episode | Fusion binding | network diagnostic | STK-N-009 |
| late join after Hit Moment | reconstruct post-hit state; no damage replay | Fusion binding | network diagnostic | STK-N-010 |
| non-authoritative client AI mutation | ignored/not authoritative | Fusion binding | authority diagnostic | STK-N-002/003/004 |
| Host/client resync | reconstruct durable state, no duplicate AI/attack side effect | Fusion binding | network diagnostic | STK-N-015 |

---

# 50. Detailed Class Contracts

## 50.1 `StalkerRoot`

| Item | Contract |
|---|---|
| Purpose | Composition root and authoritative simulation coordinator. |
| Owned state | References to Stalker subsystems; no duplicate subsystem state. |
| Inputs | simulation delta, authoritative lifecycle, validated config |
| Outputs | subsystem ticks/state projection |
| Dependencies | sensor, memory, FSM, planners, navigation, attack, debug/telemetry |
| Forbidden | God-Class scoring/path internals; hidden Player transform access |
| Main operations | `Initialize`, `Simulate(dt)`, `Reset` |
| Failure | controlled disable/fallback with reason |
| Lifecycle | spawn/match init → ticks → despawn/reset |
| Config | resolved Stalker config |
| Observability | root health/authority/subsystem readiness |
| Tests | composition, authority gate integration |

Current `StalkerController` initially serves this role and is refactored incrementally rather than replaced at once.

## 50.2 `StalkerVisionSensor`

| Item | Contract |
|---|---|
| Purpose | Physical Vision/LOS facts only |
| State | sensor references/cache; no target ownership |
| Inputs | authoritative candidates, sensor config |
| Outputs | `VisionObservation[]` |
| Dependencies | physics, vision origin |
| Forbidden | eligibility, FSM, telemetry reads |
| Operations | refresh/evaluate candidates |
| Failure | empty observations + diagnostic; never fabricate visibility |
| Config | VisionDistance, VisionAngle, blocker mask |
| Tests | distance/FOV/wall/door/self/multi-collider |

## 50.3 `StalkerTargetEligibility` / `StalkerTargetSelector`

| Item | Contract |
|---|---|
| Purpose | Player-state eligibility and deterministic candidate choice |
| State | none/minimal |
| Inputs | observations, authoritative player state |
| Outputs | eligible candidate/selection result |
| Dependencies | player life/session state contract |
| Forbidden | physical raycasts, Detection Meter mutation |
| Operations | `EvaluateEligibility`, `SelectNearestEligible` |
| Failure | no candidate |
| Tests | Downed/DEAD/disconnect/multiple players/tie |

## 50.4 `StalkerMemory`

| Item | Contract |
|---|---|
| Purpose | Legal Stalker target/detection knowledge |
| Owned state | target IDs, DetectionMeter, LKP, LastSeenDirection bearing, observation time |
| Inputs | legal observations, FSM/selector operations |
| Outputs | read-only planning/FSM context |
| Forbidden | hidden player transform/facing/velocity after LOS loss; telemetry/Profile |
| Lifecycle | match scoped |
| Tests | no-cheat/update/clear/reset; bearing semantic |

---

## 50.5 `CoverageMemory` (`StalkerSpatialMemory` compatibility name only)

| Item | Contract |
|---|---|
| Purpose | Canonical mutable actual node/region visitation history |
| Owned state | node/region visit counts/times/recent histories |
| Inputs | confirmed physical node/region visitation |
| Outputs | read-only planner/metric facts |
| Backing migration | extend/refactor current `SpatialPatrolMemory`; do not duplicate storage |
| Forbidden | second mutable `StalkerSpatialMemory`; marking selected-but-unreached candidate visited; stale Region mapping write |
| Lifecycle | match/evaluation scoped; metric windows may project/reset independently |
| Tests | visit semantics, single source of truth, compatibility guard, reset/history |

---

## 50.6 `StalkerFSM`

| Item | Contract |
|---|---|
| Purpose | six semantic states/transitions |
| State | current state + state timers/context ownership coordination |
| Inputs | observations, memory, eligibility, attack/navigation completion |
| Outputs | active state; state-entry commands |
| Forbidden | raw path calculation, global region scoring |
| Operations | tick + transition guards |
| Failure | no illegal seventh state |
| Tests | transition table |

## 50.7 `RegionDefinition`

| Item | Contract |
|---|---|
| Purpose | stable authored AI patrol-region identity |
| State | RegionId, GameplayZoneId, volumes/metadata, adjacency, DoorIds |
| Inputs | designer map authoring |
| Outputs | bake input |
| Forbidden | runtime auto-cluster identity replacement |
| Failure | invalid/duplicate/overlap reported at validation |
| Tests | authoring validation |

## 50.8 `RegionGraph` / builder/asset

| Item | Contract |
|---|---|
| Purpose | global coarse patrol topology with validated SpatialGraph mapping |
| State | regions, edges, `NodeToRegionMap`, RegionDefinitionVersion, SpatialGraph compatibility reference, availability |
| Inputs | validated RegionDefinition set + concrete SpatialGraph build |
| Outputs | compatibility validation, reachability/routes/current edge state |
| Dependencies | shared stable DoorId/RegionId; current SpatialGraph identity |
| Forbidden | consuming stale node mapping; Warden policy ownership; triangle-level movement |
| Failure | typed `RegionGraphValidationResult`; mismatch makes mapping inaccessible and triggers rebake/rebind/fallback |
| Tests | mapping, compatibility match/mismatch, components, door edges |

---

## 50.9 `GlobalPatrolPlanner`

| Item | Contract |
|---|---|
| Purpose | choose persistent global target region |
| State | none beyond optional current objective supplied externally |
| Inputs | current region, RegionGraph, CoverageMemory |
| Outputs | `GlobalPatrolObjective` |
| Forbidden | NavMeshAgent calls; hidden player data |
| Operations | filter reachable → lexicographic rank → objective |
| Failure | no reachable target result |
| Tests | ranking/tie/unreachable |

## 50.10 `LocalPatrolSelector`

| Item | Contract |
|---|---|
| Purpose | choose local node that serves global objective |
| State | none or bounded scratch |
| Inputs | SpatialGraph, target region/route, coverage, previous/recent nodes |
| Outputs | patrol node intent + score/reason |
| Dependencies | path-validation boundary |
| Forbidden | state transition, SetDestination |
| Failure | typed rejected/no-candidate |
| Tests | BFS bound, score, backtrack, deterministic ties |

## 50.11 `StalkerSearchContext`

| Item | Contract |
|---|---|
| Purpose | one SEARCH episode memory |
| State | immutable LKP origin, immutable LOS-bearing direction, immutable start time/region + mutable candidate history |
| Inputs | last legal CurrentTarget observation |
| Outputs | SearchPlanner context |
| Forbidden | hidden player position/facing/velocity update |
| Lifecycle | create on entry; clear on every exit |
| Tests | immutability, bearing semantic, clearing |

---

## 50.12 `StalkerSearchPlanner`

| Item | Contract |
|---|---|
| Purpose | generate/filter/rank legal search endpoints |
| Inputs | SearchContext, SearchRadius endpoint envelope, compatible graph data when available, navigation evaluator, config |
| Outputs | SearchIntent / candidate |
| Forbidden | hidden player transform/facing/velocity; interpreting SearchRadius as hidden pursuit; FSM ATTACK |
| Failure | no candidate → safe hold until legal trigger/timeout |
| Tests | endpoint radius, path detour, generation/filter/score/no-cheat |

---

## 50.13 `StalkerNavigationController`

| Item | Contract |
|---|---|
| Purpose | destination/path/movement execution |
| State | current logical destination, plan/execution status, recovery info |
| Inputs | movement intent, NavMeshAgent, topology events |
| Outputs | status, arrival/failure reason |
| Forbidden | target selection/LKP/FSM/telemetry read |
| Operations | evaluate, set, stop, repath, clear |
| Failure | typed path/agent result |
| Config | navigation tolerances/progress parameters |
| Tests | all path statuses + stale/door |

## 50.14 `NavigationProgressMonitor`

| Item | Contract |
|---|---|
| Purpose | distinguish movement/no-progress/stuck |
| State | position/distance samples and bounded repath history |
| Inputs | agent/path observables |
| Outputs | progress classification |
| Forbidden | state transition/target mutation |
| Config | thresholds/windows TBD profiler/playtest |
| Tests | moving, pathPending, blocked, confirmed stuck |

## 50.15 `StalkerAttackController`

| Item | Contract |
|---|---|
| Purpose | execute already-authorized ATTACK with exactly-once authoritative Hit Moment side effects |
| Owned state | current attack episode identity, wind-up/action timing, HitMomentResolved guard, outcome |
| Inputs | FSM ATTACK entry, CurrentTarget validity/range, attack config, authoritative life-state API |
| Outputs | at-most-one Hit/Miss outcome and at-most-one damage request per episode; completion |
| Forbidden | deciding ATTACK entry; new Hit-Moment LOS rule; damage from proxy/presentation callback; duplicate side effect |
| Main operations | `BeginAttackEpisode`, `ResolveHitMoment`, `CompleteAttack` or equivalent |
| Result | `ResolvedHit`, `ResolvedMiss`, `AlreadyResolved`, `NoActiveEpisode`, `NotStateAuthority` |
| Lifecycle | create one episode on each authoritative ATTACK entry; retain resolution state until episode completion |
| Tests | valid hit, miss range, invalid target, duplicate resolver, mandatory RECOVER, late-join no replay |

---

## 50.16 `StalkerDebugProvider`

| Item | Contract |
|---|---|
| Purpose | immutable debug projection |
| Inputs | runtime subsystems |
| Outputs | `StalkerAIDebugSnapshot` |
| Forbidden | mutation |
| Tests | snapshot reflects state; UI cannot mutate |

## 50.17 `StalkerTelemetryAdapter`

| Item | Contract |
|---|---|
| Purpose | translate already-committed authoritative facts into approved telemetry |
| Inputs | authoritative gameplay outcomes, including attack episode identity when the source fact is attack-related |
| Outputs | schema-approved events; development diagnostics separately |
| Attack dedup | same authoritative attack episode/outcome maps to at most one applicable gameplay telemetry event |
| Forbidden | decision feedback into Stalker; invoking damage; causing/replaying Hit Moment resolution; unsupported production event |
| Tests | schema mapping, no unsupported production event, duplicate attack fact does not duplicate telemetry outcome |

## 50.18 Fusion network-facing binding

| Item | Contract |
|---|---|
| Purpose | authority gate + synchronized client-required durable presentation state |
| Base | recommended `NetworkBehaviour` on a `NetworkObject` |
| Inputs | Fusion State Authority/tick + StalkerRoot state |
| Outputs | `[Networked]` semantic/action/transform presentation state |
| Attack rule | only State Authority begins/resolves attack episode; proxy state reconstructs presentation and never applies damage |
| Forbidden | full private memory replication; proxy AI decisions; RPC-only durable attack state; presentation-driven damage |
| RPC | transient only where appropriate; cannot bypass authoritative resolution guard |
| Failure | authority mismatch diagnostic; no local authoritative fallback |
| Tests | STK-N suite including exactly-once and late join |

---

# 51. Current-to-Target Code Mapping

| Current class/module | Current responsibility | Action | Target responsibility | Migration risk |
|---|---|---|---|---|
| `StalkerController` | six-state FSM + timers + patrol/search/attack orchestration | MODIFY | temporary `StalkerRoot` + FSM coordinator; delegate subsystems incrementally | High: serialized scene compatibility |
| `StalkerVisionSensor` | current physical LOS candidate logic | MODIFY | multi-candidate typed observation sensor with frozen bearing semantic | Medium: player binding/colliders |
| `PatrolRoute` | ordered fixed patrol points | KEEP | deterministic fallback/test fixture | Low |
| `StalkerPatrolMode` | fixed vs dynamic mode | KEEP initially | migration/fallback switch | Low |
| `StalkerBlackboard` | current/destination/previous SpatialNode IDs | MODIFY | compatibility facade only; canonical target/search state in StalkerMemory, coverage in CoverageMemory | Medium |
| `NavMeshSpatialGraphBuilder` | NavMesh triangulation → triangle graph | KEEP + TEST | local SpatialGraph builder + compatibility identity source | Medium: triangulation changes |
| `NavMeshSpatialGraph` | nodes/adjacency | KEEP | local topology + current-build identity | Low/Medium |
| `SpatialNode` | triangle node metadata | KEEP | local navigable node | Low |
| `SpatialPatrolMemory` | node visitation/staleness | MODIFY | implementation backing of canonical `CoverageMemory`; extend with region stats/history | Medium |
| any new parallel `StalkerSpatialMemory` mutable store | not required | DO NOT ADD / DEPRECATE if created | alias/facade/read-only projection only | Medium: duplicate state divergence |
| `SpatialPatrolPlanner` | bounded local candidate/scoring | SPLIT | logic reused inside `LocalPatrolSelector`; global planning moves out | Medium |
| current dynamic-patrol debug fields | local IDs/score/count | DEPRECATE gradually | `StalkerAIDebugSnapshot` projection | Low |
| current navigation wrapper | bool destination/arrival/cache behavior | MODIFY | full path/status/progress/recovery controller | High |
| current SEARCH logic | navigate to LKP + timeout | SPLIT | `StalkerSearchContext` + `StalkerSearchPlanner`, fixed bearing/radius semantics | High |
| current attack code in controller | wind-up/hit/recover transition | SPLIT | `StalkerAttackController` with attack episode resolution guard; FSM retains transitions | High: irreversible side effects |
| no RegionDefinition | absent | ADD | scene-authored canonical patrol regions | Medium |
| no RegionGraph compatibility identity | absent | ADD | stale `NodeToRegionMap` guard | High: silent wrong-region risk |
| no GlobalPatrolPlanner | absent | ADD | global coverage objective | Medium |
| no Fusion Stalker binding evidenced | absent | ADD | Host authority gate + durable action synchronization | High |

Current source is evidence of migration starting point. If any row's source behavior conflicts with M1-013/architecture, the target responsibility/approved contract wins.

---

# 52. Migration Plan

## Step 1 — Regression baseline and source governance

Before structural changes:

- identify the exact working-branch source revision;
- preserve approved six-state behavior;
- add Vision/LOS contract tests;
- DetectionTarget lock;
- LKP hidden-tracking negative test;
- LastSeenDirection bearing test;
- ATTACK/RECOVER behavior;
- current spatial local planner scoring;
- fixed PatrolRoute fallback;
- current arrival behavior.

If code disagrees with approved behavior, record it as a failing regression/migration issue rather than changing the expected contract.

## Step 2 — NavigationController upgrade

Add:

- typed plan/execution result;
- Complete/Partial/Invalid classification;
- `pathPending` handling;
- stale path detection;
- progress sampling;
- no-progress/stuck;
- recovery reason codes;
- destination projection policy.

Then rerun Step 1.

## Step 3 — Typed memory and canonical CoverageMemory

Introduce/refactor:

- `MonsterRuntimeContext`;
- `StalkerMemory`;
- `CoverageMemory` as the sole mutable spatial visitation store.

Extend current `SpatialPatrolMemory` in place/back it into CoverageMemory. Do not create a parallel mutable `StalkerSpatialMemory`.

Migrate serialized debug target/LKP fields to read-only projections/compatibility properties.

## Step 4 — RegionGraph + compatibility guard

Implement hybrid region authoring:

- scene RegionDefinitions;
- RegionDefinitionVersion;
- deterministic bake/validation;
- node-region mapping;
- SpatialGraph compatibility identity binding;
- adjacency/door edges;
- runtime RegionGraph asset/view.

Validation must block stale mapping consumption.

Required failure behavior:

```text
compatibility mismatch
→ RegionGraph invalid
→ no stale NodeToRegionMap read
→ rebake/rebind or controlled fixed fallback
```

Exact region count and signature algorithm remain bindings.

## Step 5 — GlobalPatrolPlanner + LocalPatrolSelector

Split current `SpatialPatrolPlanner` responsibility:

```text
GlobalPatrolPlanner
→ target region

LocalPatrolSelector
→ SpatialNode serving target region
```

Reuse existing staleness/connectivity/backtrack code.

All region planning consumes only a validated compatible RegionGraph.

## Step 6 — SearchContext + SearchPlanner

Replace simple LKP-only wandering with:

- immutable LKP;
- immutable last legal LOS bearing;
- endpoint-based SearchRadius;
- LKP validation;
- bounded candidates;
- typed rejection;
- deterministic scoring;
- legal complete-path detours;
- no-candidate safe hold;
- reacquisition/timeout contract.

Add explicit negative tests for hidden transform/facing/velocity access.

## Step 7 — Attack episode guard + observability

Extract/upgrade `StalkerAttackController`:

- one episode per authoritative ATTACK entry;
- stable episode identity;
- `HitMomentResolved` guard;
- damage/outcome at most once;
- duplicate invocation result;
- mandatory RECOVER unchanged.

Implement:

- `StalkerAIDebugSnapshot`;
- graph compatibility fields;
- attack episode fields;
- reason codes;
- deterministic metric evaluators.

Keep production telemetry separate.

## Step 8 — Photon Fusion 2 Host Mode binding

Add network-facing root:

- only State Authority calls Stalker simulation;
- synchronized semantic/action/transform state;
- no proxy decision tick;
- no proxy damage path;
- private memory remains host-only;
- current attack episode/phase reconstructable for late join without replay;
- 2/3/4 player convergence;
- exactly-once attack tests.

Current networking spike proves Fusion/session/player spawn only; it does not prove Stalker networking is complete.

---

# 53. Implementation Order

Recommended coding order within M2:

1. reconcile working branch with newest intended M1-026 spatial files and record source revision;
2. freeze approved-contract regression tests;
3. navigation typed-result upgrade;
4. typed Stalker memory extraction;
5. make `CoverageMemory` the sole mutable spatial visitation source by extending/refactoring `SpatialPatrolMemory`;
6. player-candidate/typed `VisionObservation` migration with fixed bearing semantic;
7. target eligibility/selector extraction;
8. RegionDefinition authoring + validation tooling;
9. SpatialGraph compatibility identity + RegionGraphAsset compatibility guard;
10. RegionGraph runtime build/load;
11. GlobalPatrolPlanner;
12. split LocalPatrolSelector from current planner;
13. SearchContext with immutable LKP/bearing;
14. SearchPlanner with endpoint-based SearchRadius;
15. AttackController extraction + attack episode resolution guard;
16. debug snapshot/reason codes/deterministic metric evaluators;
17. Fusion State-Authority binding + durable attack presentation state;
18. multiplayer exactly-once/resync tests;
19. profiler-driven optimization;
20. tuning/playtest iteration.

Do not optimize raycast cadence, planner cadence, stuck thresholds, candidate bounds, or scoring weights before profiling/playtest evidence.

---

# 54. Configuration Matrix

The `Status` column intentionally uses only the requested status vocabulary.

| Field | Owner | Purpose | Source | Runtime mutable? | AED mutable? | Status |
|---|---|---|---|---:|---:|---|
| VisionDistance | Stalker config | Vision broad phase | M1-013 | config-bound | No | PROJECT BASELINE |
| VisionAngle | Stalker config | full FOV cone | M1-013 | config-bound | No | PROJECT BASELINE |
| DetectionFillRate | Stalker config | visible detection fill | M1-013/M1-015 | Yes at allowed config boundary | Yes | PROJECT BASELINE |
| DetectionDecayRate | Stalker config | hidden target decay | M1-013/M1-015 | Yes at allowed config boundary | Yes | PROJECT BASELINE |
| PatrolSpeed | Stalker config | patrol movement | M1-013 | config-bound | No | PROJECT BASELINE |
| ChaseSpeed | Stalker config | chase movement | M1-013/M1-015 | Yes at allowed config boundary | Yes | PROJECT BASELINE |
| SearchDuration | Stalker config | SEARCH hard timeout | M1-013/M1-015 | Yes at allowed config boundary | Yes | PROJECT BASELINE |
| SearchRadius | Stalker config | legal candidate-endpoint envelope around immutable SearchOriginLKP; not a path-geometry radius | M1-013 + detailed-design semantic | config-bound | No | PROJECT BASELINE |
| AttackRange | Stalker config | ATTACK entry/hit range contract | M1-013 | config-bound | No | PROJECT BASELINE |
| AttackWindup | Stalker config | attack telegraph/wind-up | M1-013 | config-bound | No | PROJECT BASELINE |
| AttackRecovery | Stalker config | mandatory RECOVER | M1-013 | config-bound | No | PROJECT BASELINE |
| StalkerDamagePercent | Stalker/combat config | damage amount | M1-013 | config-bound | No | PROJECT BASELINE |
| detectionMeterFull = 1 | current controller spike | current FULL representation | source evidence | runtime constant/config | No direct AED | CURRENT VALUE |
| DetectionFillRate = 0.5 | current controller spike | spike value | source evidence | config-bound | Yes if validator applies | CURRENT VALUE |
| DetectionDecayRate = 0.5 | current controller spike | spike value | source evidence | config-bound | Yes if validator applies | CURRENT VALUE |
| SearchDuration = 5 | current controller spike | spike value | source evidence | config-bound | Yes if validator applies | CURRENT VALUE |
| AttackRange = 1.5 | current controller spike | spike value | source evidence | config-bound | No | CURRENT VALUE |
| AttackWindup = 0.75 | current controller spike | spike value | source evidence | config-bound | No | CURRENT VALUE |
| AttackRecovery = 1.0 | current controller spike | spike value | source evidence | config-bound | No | CURRENT VALUE |
| candidateBfsDepth = 3 | current dynamic-spatial evidence | local candidate bound | M1-026 evidence | config-bound | No | CURRENT VALUE |
| stalenessHorizon = 15 | current dynamic-spatial evidence | staleness normalization | M1-026 evidence | config-bound | No | CURRENT VALUE |
| stalenessWeight = 1 | current dynamic-spatial evidence | local score | M1-026 evidence | config-bound | No | CURRENT VALUE |
| connectivityWeight = 0.15 | current dynamic-spatial evidence | local score | M1-026 evidence | config-bound | No | CURRENT VALUE |
| immediateBacktrackPenalty = 0.75 | current dynamic-spatial evidence | local score | M1-026 evidence | config-bound | No | CURRENT VALUE |
| global ranking weights | GlobalPatrolPlanner | not used in baseline lexicographic policy | this design | n/a | No | TBD — tuning |
| local new factor weights | LocalPatrolSelector | progress/visit/travel/dead-end terms | this design | config-bound | No | TBD — tuning |
| search scoring weights | SearchPlanner | legal search ranking | this design | config-bound | No | TBD — tuning |
| LKP projection max distance | Navigation/Search | bounded off-NavMesh projection | this design | config-bound | No | TBD — tuning |
| candidate-per-plan bound | planners | bound sync path work | this design | config-bound | No | TBD — profiler/playtest |
| no-progress thresholds/window | NavigationProgressMonitor | classify no-progress | this design | config-bound | No | TBD — profiler/playtest |
| stuck confirmation threshold | NavigationProgressMonitor | confirm stuck | this design | config-bound | No | TBD — profiler/playtest |
| perception evaluation cadence | StalkerRoot/Sensor | avoid unnecessary physics work | performance policy | runtime scheduling | No | TBD — profiler/playtest |
| exact Region count | map authoring | patrol granularity | map design | authoring | No | TBD — implementation binding |
| RegionDefinition Unity representation details | spatial authoring | exact component/asset binding | this design | authoring | No | TBD — implementation binding |
| SpatialGraph compatibility identity representation | SpatialGraph/RegionGraph binding | prove baked NodeToRegionMap compatibility | this design | rebuild/bake-bound | No | TBD — implementation binding |
| attack episode identity concrete representation | AttackController/Fusion binding | exactly-once resolution and presentation identity | this design | authoritative runtime | No | TBD — implementation binding |
| Stalker `[Networked]` property set | Fusion binding | client reconstruction | this design | network state | No | TBD — implementation binding |
| Stalker transform replication component settings | Fusion binding | proxy movement | network project | network state | No | TBD — implementation binding |

Current numeric values above are evidence of spike/default code, not final design values.

---

# 55. Stalker Hard Invariants

1. `SEARCH` never reads hidden Player Transform.
2. Stalker authoritative decisions execute only on Host / Fusion State Authority.
3. Approved architecture/gameplay contracts outrank current implementation; conflicting code is a migration gap unless an explicit revision is recorded.
4. Sensor produces physical observations; it does not select semantic state.
5. Target eligibility is separate from physical visibility.
6. FSM owns semantic transitions including ATTACK entry.
7. Planner does not choose ATTACK transition.
8. AttackController executes attack only after FSM entered ATTACK.
9. One authoritative ATTACK episode resolves Hit Moment at most once and applies damage at most once.
10. Duplicate callbacks, late join, presentation replay, or RPC cannot duplicate authoritative attack side effects.
11. Navigation does not select targets or mutate LKP/AI knowledge.
12. `CoverageMemory` is the single mutable source of node/region visitation history.
13. Telemetry never commands Stalker.
14. AED never directly commands FSM, target, LKP, SearchContext, or navigation destination.
15. PATROL uses a global Region objective when RegionGraph is valid.
16. Global objective is distinct from local destination.
17. A baked `NodeToRegionMap` is consumed only when its RegionGraph/SpatialGraph compatibility identity matches the current SpatialGraph.
18. Compatibility mismatch invalidates RegionGraph use and triggers rebake/rebind or controlled fixed fallback.
19. Patrol/Search exact destinations require the configured complete-reachability policy.
20. `SearchRadius` constrains candidate endpoints around immutable LKP, not every point of legal path geometry.
21. `LastSeenDirection` is the last legal LOS bearing from vision origin to observed point; it is not target facing/velocity/prediction.
22. Normal navigation recovery does not teleport.
23. Memory contains only legally observed or legally derived AI knowledge.
24. Non-authoritative clients do not independently simulate authoritative Stalker decisions.
25. Main Route is not a fixed Stalker patrol rail.
26. Gameplay Zone is not automatically an AI Patrol Region.
27. A region/node is not counted visited merely because it was selected.
28. Fixed waypoint fallback does not silently mark hierarchical patrol healthy.
29. Closed Door blocks Stalker LOS and normal path traversal according to the gameplay contract.
30. ATTACK Hit Moment does not gain an invented LOS blocker absent a revised combat contract.
31. RECOVER is mandatory after ATTACK and is distinct from navigation stuck recovery.
32. Stalker does not consume Runtime NoiseEvent as perception.
33. Baseline patrol/search decisions require no unseeded randomness.
34. One metric name/version has exactly one unit, numerator, denominator, and reset/window rule.

---

# 56. Definition of Done

The detailed design resolves the P0 implementation questions as follows.

| # | Question | Resolution |
|---:|---|---|
| 1 | Vision detects player how? | §13 physical candidate→distance→FOV→LOS→typed observation |
| 2 | Who decides eligibility? | §14 `StalkerTargetEligibility` |
| 3 | DetectionTarget vs CurrentTarget? | §15–16 detection candidate vs promoted pursuit target |
| 4 | Meter update/reset? | §16 |
| 5 | PATROL→DETECT? | nearest eligible visible candidate |
| 6 | DETECT→CHASE? | meter FULL + promotion |
| 7 | CHASE→SEARCH? | legal LOS lost |
| 8 | Who decides ATTACK? | FSM |
| 9 | Who applies damage? | State-Authority `StalkerAttackController` after guarded Hit Moment |
| 10 | How is duplicate attack prevented? | one authoritative attack episode + resolution guard; §28/§42 |
| 11 | RECOVER meaning? | mandatory post-attack semantic state |
| 12 | LKP written when? | from legal CurrentTarget observation |
| 13 | LastSeenDirection exact meaning? | normalized vision-origin→last observed point LOS bearing; not facing/velocity |
| 14 | No-cheat enforcement? | hidden transform/facing/velocity absent from Search/Planner inputs |
| 15 | SearchRadius meaning? | endpoint envelope around immutable LKP; legal Complete path may detour outside |
| 16 | Gameplay Zone vs AI Region? | distinct; §20 |
| 17 | Region authoring? | scene-authored + deterministic baked runtime asset |
| 18 | Node→Region mapping? | exactly-one deterministic bake rule |
| 19 | Stale NodeToRegion mapping? | compatibility validation failure; mapping inaccessible; rebake/rebind/fallback |
| 20 | Coverage source of truth? | canonical mutable `CoverageMemory`; no duplicate `StalkerSpatialMemory` store |
| 21 | Region visited when? | physical node entry/arrival with validated mapping |
| 22 | Node visited when? | physical attribution/arrival, never selection |
| 23 | Global Region selection? | lexicographic least-visited/stale deterministic policy |
| 24 | Local node selection? | bounded M1 logic + global progress + complete path |
| 25 | Avoid local trap? | persistent global objective independent of local BFS |
| 26 | Search candidates source? | LKP/bearing/spatial graph only |
| 27 | Candidate rejects? | typed §34 reasons |
| 28 | Search score? | deterministic normalized factor model |
| 29 | Timeout/reacquire? | §36 |
| 30 | PathPartial? | reject exact patrol/search candidate |
| 31 | PathInvalid? | reject |
| 32 | stale path? | re-evaluate/repath |
| 33 | no-progress vs stuck? | suspected evidence vs confirmed recovery condition |
| 34 | recovery ladder? | §39 |
| 35 | dynamic door? | event-driven edge/path invalidation |
| 36 | fixed fallback? | §41 controlled activation/exit |
| 37 | Fusion State Authority? | Host network-facing binding |
| 38 | client receives what? | transform + semantic/action presentation minimum |
| 39 | private memory not replicated? | LKP/Search/Coverage/candidate details |
| 40 | late join during/after attack? | reconstruct durable presentation; never replay authoritative damage |
| 41 | telemetry? | approved authoritative facts only; one-way |
| 42 | debug fields? | §45 snapshot including graph/attack guards |
| 43 | metrics exact? | §47 freezes unit/numerator/denominator/window/version |
| 44 | tests? | §48 |
| 45 | source precedence? | §4 approved contract > implementation |
| 46 | migration? | §51–52 |
| 47 | implementation order? | §53 |

All P0 behavioral, ownership, failure, authority, and evaluation semantics required by this document are resolved. Remaining items are numerical tuning, scene authoring, concrete signature/API representation, or low-level Fusion serialization bindings and do not require architecture escalation.

---

# 57. Open Tuning / Implementation Bindings

## Tuning TBD

- final local patrol weights;
- final Search weights;
- final candidate bounds;
- LKP projection bound;
- perception/planner cadence;
- arrival tolerance;
- path-pending timeout;
- no-progress/stuck windows/epsilons;
- performance budgets;
- final quality acceptance thresholds.

## Map-authoring TBD

- exact AI Patrol Region count;
- final RegionDefinition boundaries;
- final DoorId/RegionEdge mapping in playable M2 scene;
- whether Stalker uses any NavMeshLink/OffMeshLink.

## Implementation binding TBD

Behavior is already frozen; these are concrete representation/API choices only:

- exact player `VisionTargetPoint` component/API;
- exact player authoritative life-state resolver API;
- exact RegionDefinition MonoBehaviour/ScriptableObject class names;
- concrete `RegionDefinitionVersion` storage;
- exact SpatialGraph compatibility identity/signature representation or hash algorithm;
- exact Fusion Stalker `[Networked]` properties;
- exact attack episode/sequence `[Networked]` presentation binding;
- exact NetworkTransform/equivalent settings;
- exact transient RPCs, if any;
- exact production telemetry activation for reserved Monster/AI events.

These TBDs must not change the frozen behavior that stale graph mappings are rejected, SEARCH is no-cheat, or attack damage resolves at most once per authoritative episode.

---

# 58. Architecture Escalations

```text
ARCHITECTURE ESCALATION REQUIRED: NO
```

No supplied evidence demonstrates that any of these baselines are unimplementable:

- six-state FSM;
- Host authority;
- RegionGraph hierarchy;
- no-cheat SEARCH;
- FSM/Planner/Action ownership;
- Telemetry one-way boundary;
- bounded AED authority.

The source snapshot skew between the ZIP and later M1-026 spatial captures is resolved as implementation/source-control reconciliation, not an architecture conflict.

---

# 59. References

## Project sources

1. `AI_Architecture_v1.1.md`.
2. `M1-013_Stalker_FSM_Sensor_Contracts_FINAL.md`.
3. `KLTN.docx` — *THIẾT KẾ SƠ BỘ MAP RESEARCH FACILITY VÀ VỊ TRÍ OBJECTIVE* / Map Flow Plan v0.
4. `KLTN (1).docx` — *Chốt cách tổ chức multiplayer và đồng bộ dữ liệu trong trận*.
5. `ECHO PROTO.docx` / `ECHO PROTO(1).docx`.
6. `02_ECHO_PROTOCOL_System_Architecture_REVISED.docx`.
7. `03_ECHO_PROTOCOL_Implementation_Spec_REVISED.xlsx`.
8. `Telemetry_Event_Schema_v0_FINAL.md`.
9. `M1-015_ScenarioConfig_AED_Fairness_Policy_v0_FINAL.md`.
10. `M1-020_Test_Strategy_Fixed_vs_Adaptive_Experiment_v0_FINAL.md`.
11. `ECHO-PROTOCOL-feature-m1-026-stalker-spike.zip`.
12. Latest M1-026 spatial implementation evidence for `NavMeshSpatialGraphBuilder`, `NavMeshSpatialGraph`, `SpatialPatrolMemory`, `SpatialPatrolPlanner`, `StalkerBlackboard`, `StalkerNavigationController`, and `StalkerController`.

## Official external references

13. Unity Technologies — `NavMeshAgent.SetDestination`:  
    https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AI.NavMeshAgent.SetDestination.html
14. Unity Technologies — `NavMeshAgent`:  
    https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AI.NavMeshAgent.html
15. Unity Technologies — `NavMesh.CalculatePath`:  
    https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AI.NavMesh.CalculatePath.html
16. Unity Technologies — `NavMeshPath` / `NavMeshPathStatus`:  
    https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AI.NavMeshPath.html  
    https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AI.NavMeshPathStatus.html
17. Unity Technologies — AI Navigation:  
    https://docs.unity3d.com/6000.0/Documentation/Manual/com.unity.ai.navigation.html
18. Unity Technologies — Unity Test Framework:  
    https://docs.unity3d.com/6000.0/Documentation/Manual/com.unity.test-framework.html
19. Unity Technologies — Profiler:  
    https://docs.unity3d.com/Manual/Profiler.html
20. Photon Engine — Fusion 2 Client-Server Player Input:  
    https://doc.photonengine.com/fusion/v2/manual/input/player-input
21. Photon Engine — Fusion 2 Networked Properties & State Sync:  
    https://doc.photonengine.com/fusion/v2/manual/data-transfer/networked-properties
22. Photon Engine — Fusion 2 RPCs:  
    https://doc.photonengine.com/fusion/v2/manual/data-transfer/rpcs
23. Photon Engine — Fusion 2 Data Transfer:  
    https://doc.photonengine.com/fusion/v2/manual/data-transfer/data-transfer
24. Photon Engine — Fusion 2 Host Mode Basics / RPCs:  
    https://doc.photonengine.com/fusion/v2/tutorials/host-mode-basics/6-remote-procedure-calls

---

# Detailed Design Validation

```text
Architecture baseline respected: YES
Six-state FSM preserved: YES
M1-013 behavior contract preserved: YES
Host authority preserved: YES
PATROL global/local design complete: YES
SEARCH no-cheat design complete: YES
LastSeenDirection/SearchRadius semantics complete: YES
Navigation/recovery design complete: YES
Region authoring/mapping design complete: YES
SpatialGraph/RegionGraph compatibility guard complete: YES
CoverageMemory single mutable ownership complete: YES
Fusion boundary sufficiently specified: YES
Exactly-once authoritative attack contract complete: YES
Observability/metrics/test design complete: YES
Metric units/denominators deterministic: YES
Migration plan complete: YES
Architecture escalation required: NO
```

---

# Correction Report

| ID | Problem | Sections changed | Resolution | Status |
|---|---|---|---|---|
| DD-01 | Source precedence / governance | §4, §7.1, §51, §52, §55, §56 | Froze authority order: architecture → approved behavior/design contracts → implementation evidence → historical spikes → official engine semantics. Conflicting code is a migration gap/bug unless an explicit valid revision is recorded. | RESOLVED |
| DD-02 | SpatialGraph / RegionGraph compatibility | §19.3, §20.2, §21, §22, §23, §45, §46, §48, §49, §50.8, §51–53, §55–57 | Added RegionDefinitionVersion + SpatialGraph compatibility identity semantics; stale NodeToRegionMap is inaccessible, triggers validation failure and rebake/rebind or controlled fixed fallback. | RESOLVED |
| DD-03 | Metric denominator / unit ambiguity | §47, §48, §55, §56 | Replaced ambiguous mixed units with versioned deterministic metrics: RegionCoverage, SpatialNodeCoverage, RegionRevisitRate, SpatialNodeRevisitRate, RegionImmediateBacktrackRate, StuckRate, PathFailureRate, SearchReacquisitionRate; each has one unit/numerator/denominator/window/source. | RESOLVED |
| DD-04 | LastSeenDirection + SearchRadius semantics | §13.2, §13.9, §30, §31, §33, §45, §48, §49, §50.4/11/12, §54–57 | LastSeenDirection is frozen as last legal LOS bearing; SearchRadius is frozen as candidate-endpoint envelope around immutable LKP and does not constrain every path corner. | RESOLVED |
| DD-05 | Photon Fusion exactly-once authoritative attack side effect | §10, §28, §42, §45–46, §48–50, §51–53, §55–57 | Added authoritative attack episode identity + resolution guard; one episode can commit Hit Moment/damage/outcome at most once; late join/presentation/RPC cannot replay damage. | RESOLVED |

---

# Final Consistency Audit

```text
Architecture baseline respected: YES
Six-state FSM preserved: YES
M1-013 behavior contract preserved or explicitly revised: YES
Duplicate mutable Coverage ownership: NO
SEARCH hidden transform access possible: NO
RegionGraph stale node mapping silently accepted: NO
Metric denominator ambiguity remains: NO
Duplicate authoritative attack side effect possible by contract: NO
Architecture escalation required: NO
```

No P0 blocking contradiction remains in the detailed-design contract.

```text
Recommended Status:
BASELINED v1.1
```

**End of `Stalker_AI_Design_v1.1.md`**
