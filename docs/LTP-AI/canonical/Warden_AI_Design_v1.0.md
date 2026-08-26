# ECHO PROTOCOL — Warden AI Detailed Design v1.0

**Document:** `Warden_AI_Design_v1.0.md`  
**Project:** ECHO PROTOCOL — Co-op Survival Horror Multiplayer  
**Monster / Controller:** The Warden  
**Revision:** v1.0  
**Parent Architecture:** `AI_Architecture_v1.1.md`  
**Parent Architecture Status:** BASELINED v1.1  
**Reference Detailed Designs:** `Stalker_AI_Design_v1.1.md`, `Listener_AI_Design_v1.0.md` — reusable engineering patterns only  
**Environment:** Unity `6000.5.8f1`; Photon Fusion `2.1.1 Stable`, build `2177`; Host Mode; 2–4 Players  
**Detailed Design Status:** BASELINED v1.0  
**Recommended Status:** BASELINED v1.0  

> This document is an implementation contract. It does not claim that Warden implementation, map binding, automated tests, multiplayer integration, profiling, telemetry revision, or playtest tuning are complete.

---

# 1. Document Control

| Field | Value |
|---|---|
| Document Role | This document fulfills the Warden route-pressure detailed-design scope required by `AI_Architecture_v1.1` §19.5. |
| Parent architecture requested name | `Warden_Route_Pressure_Design_v1.0.md` |
| Canonical output filename for this task | `Warden_AI_Design_v1.0.md` |
| Competing Warden design document | **Must not be created**; this file is the implementation contract for that architecture scope. |
| Architecture Status | BASELINED v1.1 |
| Detailed Design Status | BASELINED v1.0 |
| Runtime identity | Spatial Pressure Controller |
| Canonical graph | `FacilityGraph` |
| AI `RegionGraph` relationship | Separate graph view; stable spatial IDs may be shared |
| Runtime route authority | Host / Fusion State Authority |
| Network topology | Photon Fusion 2 Host Mode |
| Multiplayer target | 2–4 players |
| Current Warden source implementation | NOT EVIDENCED / NOT IMPLEMENTED FROM SUPPLIED SOURCE |
| Physical Warden chase/combat | OUT OF SCOPE / NOT EVIDENCED for v1.0 |
| Maximum active Warden pressure locks | **1** — DETAILED-DESIGN DECISION |
| Warden adaptive authority | none; Warden consumes validated ScenarioConfig constraints only |
| `ScenarioConfig.routeModifier` | separate scenario-level contract; adaptive only at `ALLOWED_PHASE_BOUNDARY` under M1-015 |
| Test/profiler completion | Not claimed |

## 1.1 Surgical correction record

This v1.0 document completed a surgical consistency pass without redesigning Warden gameplay.

| Issue | Resolution | Status |
|---|---|---|
| `WAR-DD-01` | canonical `WardenRouteLockDefinition` owns Warden eligibility/support and the complete `AffectedPlayerRouteEdgeIds[]` footprint; candidate simulation, telegraph, apply, release, debug, and tests consume that same footprint | RESOLVED |
| `WAR-DD-02` | graph-definition, candidate-eligibility, and runtime-safety reason taxonomies have non-overlapping ownership; candidate-bound scheduling is not a rejection reason | RESOLVED |
| `WAR-DD-03` | only safe candidates with `MetricStatus = VALID` and `RoutePressure_v1.0 > 0` are policy-selectable; zero-pressure-only safe sets produce `NoMeaningfulPressure` and no telegraph/lock | RESOLVED |
| `WAR-SG-01` | `PressureCandidateBound` must cover the complete authored cheap-eligible `WardenRouteLockDefinition` set; overflow is a configuration/content validation failure with no truncation and no Warden action | RESOLVED |

Correction-pass working status was `REVIEW REQUIRED`. Because all three WAR-DD issues are resolved and propagated, the released document remains:

```text
Detailed Design Status: BASELINED v1.0
Recommended Status: BASELINED v1.0
```

No revision bump is introduced.

## 1.2 Statement classification

Important decisions use:

| Classification | Meaning |
|---|---|
| **PROJECT BASELINE** | Frozen by parent architecture or approved project/gameplay/cross-cutting contract. |
| **CURRENT IMPLEMENTATION** | Directly evidenced in supplied Unity source. |
| **DETAILED-DESIGN DECISION** | v1.0 subsystem decision needed to make the approved architecture implementable. |
| **STATIC DESIGN CONFIG** | Designer-owned content/config, not adaptive runtime state. |
| **FIXED SCENARIO CONFIG** | Scenario-owned applied config. |
| **ADAPTIVE-AUTHORIZED** | Explicitly allowed by current AED contract and timing rules. |
| **TUNING TBD** | Numerical value requires playtest/profiling evidence. |
| **IMPLEMENTATION BINDING TBD** | Concrete Unity/Fusion/data structure/API representation is open; semantic behavior is fixed. |
| **MAP AUTHORING TBD** | Exact map-specific IDs/bindings/placement remain to be authored/validated. |
| **ARCHITECTURE ESCALATION** | Required only if evidence proves a parent architecture invariant cannot reasonably be implemented. |

---

# 2. Purpose

This document converts the Warden architecture boundary into an implementation-level contract for M2 Feature-Complete Alpha.

A developer must be able to determine without inventing behavior:

- what `FacilityGraph` represents;
- why it is not Stalker `RegionGraph`;
- how facility nodes/edges/doors are authored, baked, and validated;
- what runtime state changes graph traversal;
- how objective/exit route requirements are resolved;
- exactly what "objective remains reachable" means;
- which door/route points the Warden may consider;
- how safe pressure candidates are generated;
- how `RoutePressure_v1.0` is calculated;
- how candidates are selected deterministically;
- how repetition is prevented;
- how Warden telegraphing works;
- how topology change during telegraph is handled;
- why precommit validation is mandatory;
- how a Warden action applies/releases exactly once;
- how post-apply validation and fail-safe reopening work;
- how Door Jammer, scenario route modifiers, objective gates, and Warden overlays coexist;
- what Warden may legally know;
- what runs on Fusion State Authority;
- which state clients require;
- how late join reconstructs telegraph/lock presentation;
- how telemetry remains one-way;
- how route safety and route pressure are measured;
- what tests prove soft-lock resistance;
- which implementation sequence minimizes risk.

Quality goals:

```text
Correct
Deterministic where appropriate
Explainable
Host-authoritative
Fairness-preserving
Soft-lock resistant
Graph-driven
Event-driven
Testable
Observable
Measurable
Performance-bounded
Maintainable
Thesis-defensible
```

---

# 3. Scope

## 3.1 In scope

- Warden route-pressure controller;
- stable Facility IDs;
- `FacilityGraphDefinition`;
- directed runtime FacilityGraph;
- designer-authorized Warden lock points;
- deterministic graph validation/bake;
- runtime edge-state overlay;
- door-state composition;
- Door Jammer coexistence;
- scenario routeModifier coexistence;
- objective/phase route obligations;
- reachability quantifier;
- current route snapshot;
- candidate generation and bounded ranking;
- `RoutePressure_v1.0`;
- repetition/cooldown;
- one-active-lock policy;
- telegraph lifecycle;
- TOCTOU/precommit validation;
- atomic apply/release;
- post-apply verification;
- fail-safe release;
- Final Hunt route protection;
- Fusion Host/State Authority;
- durable telegraph/lock presentation state;
- late join/resync;
- telemetry boundary;
- read-only observability;
- exact metrics;
- EditMode, PlayMode, Fusion tests;
- edge cases;
- component contracts;
- implementation plan.

## 3.2 Out of scope

- Stalker perception/detection/search/coverage;
- Listener noise/hearing/investigation;
- Warden player pursuit;
- Warden vision/hearing target acquisition;
- Warden chase FSM;
- Warden attack/recover FSM;
- Warden physical NavMesh patrol;
- ML/RL;
- runtime GenAI;
- arbitrary procedural route generation;
- arbitrary door locking;
- multi-lock runtime policy in v1.0;
- exact WardenEligible DoorIds;
- exact lock/telegraph/cooldown timings;
- invented production telemetry events;
- client-authoritative route decisions;
- exact Fusion field/API layout;
- fabricated test/profiler results.

---

# 4. Source Priority / Governance

## 4.1 Warden gameplay/design authority

```text
1. AI_Architecture_v1.1.md
2. approved gameplay/design contracts
3. cross-cutting contracts
4. current Warden implementation evidence
5. historical spikes/notes
6. Stalker_AI_Design_v1.1.md — engineering patterns only
7. Listener_AI_Design_v1.0.md — engineering patterns only
8. official Unity / Photon Fusion docs — engine/network factual semantics
```

Approved gameplay/design contracts include:

- `ECHO PROTO.docx`;
- `ECHO PROTO(1).docx`;
- Project Scope;
- System Architecture;
- Implementation Specification;
- `KLTN.docx` — Research Facility Map Flow / Objective Layout;
- `KLTN (1).docx` — multiplayer organization/synchronization;
- any later approved Warden-specific contract.

Cross-cutting contracts include:

- `M1-015_ScenarioConfig_AED_Fairness_Policy_v0_FINAL.md`;
- `Telemetry_Event_Schema_v0_FINAL.md`;
- `M1-020_Test_Strategy_Fixed_vs_Adaptive_Experiment_v0_FINAL.md`.

## 4.2 Source conflict rule

For project behavior:

```text
Architecture / approved contracts
>
current implementation
>
historical assumptions
```

If later Warden source conflicts with approved behavior:

```text
classify implementation gap / migration issue / bug
→ preserve approved behavior
→ document migration
→ add regression test
→ do not silently rewrite specification to match code
```

For engine/API facts:

```text
official Unity / Photon Fusion documentation
>
implementation assumptions
```

---

# 5. Source Evidence Summary

## 5.1 GDD

Approved gameplay establishes:

- Stalker creates pressure through sight;
- Listener creates pressure through sound;
- Warden creates pressure through route/door control;
- Warden may temporarily lock prepared routes/doors;
- a warning/telegraph must occur before a route is locked;
- at least one legal route must remain so the team can continue;
- alternative routes are intended counterplay;
- Research Facility is fixed/learnable rather than procedural;
- Door Jammer temporarily prevents monsters traversing some valid doors for one minute and is not valid on every door.

## 5.2 Research Facility Map Flow

Map baseline establishes:

```text
Zone 1 — Research & Storage Sector
Zone 2 — Power & Engineering Sector
Zone 3 — Security & Containment Sector
```

Main progression:

```text
Start
→ Energy Core areas
→ Power Hub / Power Puzzle
→ Security Terminal
→ Final Hunt
→ Exit
```

Main Route is convenient, not mandatory.

Alternative Routes support:

- avoiding monsters;
- breaking Stalker LOS;
- routing around Listener pressure;
- routing around Warden-locked doors;
- returning to rescue teammates;
- avoiding linear play.

The map-flow image visibly contains Warden lock markers (`W1`–`W3`) as authoring evidence that specific lock points exist. The image is not treated as a frozen machine-readable runtime DoorId contract.

Therefore:

```text
Warden lock locations exist conceptually
Exact WardenEligible DoorId binding
= MAP AUTHORING TBD
```

## 5.3 Architecture

Parent architecture freezes:

```text
FacilityGraph
→ Current Route Model
→ Pressure Candidate Generator
→ Safety Validator
→ Warden Policy
→ Telegraph
→ Door/Route Action
→ Revalidate
```

and:

```text
FacilityGraph != AI RegionGraph
```

Only designer-authorized Warden-controllable door/route points may become candidates.

## 5.4 ScenarioConfig / AED

M1-015 freezes:

```text
routeModifier
→ adaptive-authorized ONLY at ALLOWED_PHASE_BOUNDARY
→ not adaptively modified at PRE_MATCH
→ designer whitelist
→ route compatibility validation
→ at least one legal route
→ objective/exit reachable
→ no soft-lock
→ no teleport
→ no hidden Player information
```

This is not permission for AED to choose a runtime Warden DoorId.

## 5.5 Telemetry

Current telemetry schema has no active Warden telegraph/route-lock/fail-safe production event.

The schema contains possible `WARDEN_ATTACK` as a generic `PLAYER_DOWNED.reasonCode`, but that catalog entry is not sufficient evidence that Warden physical combat exists in the approved v1.0 gameplay.

## 5.6 Networking

Project environment:

```text
Unity 6000.5.8f1
Photon Fusion 2.1.1 Stable build 2177
Host Mode
2–4 players
```

Parent architecture binds authoritative Warden route actions to Host/State Authority.

---

# 6. External Research

External research is used only to validate implementation mechanisms.

## 6.1 Unity

Relevant official concepts:

- ScriptableObject is suitable for designer-authored serialized data assets, but the exact Warden graph serialization format is not frozen.
- component/lifecycle callback order should not define gameplay correctness;
- physics overlap/query APIs can support doorway-occupancy commit checks if the selected door implementation physically closes;
- Unity Test Framework supports EditMode/PlayMode test separation;
- Unity Profiler should establish actual budgets before Hz/ms targets are frozen.

Official references:

- https://docs.unity3d.com/6000.0/Documentation/Manual/class-ScriptableObject.html
- https://docs.unity3d.com/6000.0/Documentation/Manual/ExecutionOrder.html
- https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Physics.OverlapBox.html
- https://docs.unity3d.com/6000.0/Documentation/Manual/com.unity.test-framework.html
- https://docs.unity3d.com/Manual/Profiler.html

## 6.2 Photon Fusion 2

Relevant official concepts:

- State Authority owns authoritative synchronized state in Host/Server topology;
- Input Authority is separate from State Authority;
- `NetworkObject` + boundary `NetworkBehaviour` are appropriate for network-facing state;
- `[Networked]` properties are durable synchronized state;
- RPC is a transient message mechanism and is not a durable late-join history;
- `FixedUpdateNetwork()` is the simulation callback for authoritative networked simulation;
- action/timer presentation that must survive late join should be represented by durable state/end-tick semantics rather than an RPC alone.

Official references:

- https://doc.photonengine.com/fusion/v2/manual/playerref
- https://doc.photonengine.com/fusion/v2/manual/data-transfer/networked-properties
- https://doc.photonengine.com/fusion/v2/manual/data-transfer/rpcs
- https://doc.photonengine.com/fusion/v2/concepts-and-patterns/network-simulation-loop
- https://doc.photonengine.com/fusion/v2/tutorials/host-mode-basics/overview

## 6.3 Graph algorithms

v1.0 needs only standard deterministic graph operations:

- directed adjacency;
- BFS/DFS for unweighted reachability;
- Dijkstra-style shortest positive-cost route calculation;
- connected/reachable-set validation.

Do not enumerate all simple paths.

---

# 7. Current Implementation Assessment

```text
CURRENT IMPLEMENTATION:
NOT EVIDENCED / NOT IMPLEMENTED FROM SUPPLIED SOURCE
```

Evidence assessment:

- supplied M1-026 Unity project snapshot contains Stalker implementation evidence;
- no Warden-named C# file or `Warden` reference was found in that supplied project snapshot;
- no supplied `FacilityGraph`, `WardenSafetyValidator`, `WardenPolicy`, or Warden route-action runtime source was evidenced;
- networking spike proves Fusion session/Host/client/player spawning, not Warden networking;
- the map/GDD define Warden behavior, but do not prove implementation completion.

This design therefore describes target implementation, not existing Warden classes.

---

# 8. Design Goals / Non-Goals

## 8.1 Goals

- Warden changes route pressure, not player target pressure;
- only authored lock points are used;
- every route action is safety-checked;
- safety uses current combined authoritative route state;
- objective/exit route obligations are deterministic;
- telegraph is actionable and binds to the exact warned door;
- stale safety results cannot authorize a lock;
- route actions apply/release at most once;
- an unexpected unsafe result self-recovers by releasing Warden-owned state;
- route pressure is measurable and reproducible;
- repeated door oscillation is bounded;
- clients render, never decide;
- no hidden exact-player route targeting;
- graph work is event-driven and bounded.

## 8.2 Non-goals

- maximize player harm;
- close every possible route;
- predict individual player movement;
- chase visible players;
- use noise/vision to select doors;
- replace the map's objective system;
- override Door Jammer;
- override ScenarioConfig validation;
- create new routes;
- use physics/NavMesh triangle topology as FacilityGraph identity.

---

# 9. Warden Runtime Role Resolution

| Capability | Source evidence | v1.0 decision | Classification |
|---|---|---|---|
| route pressure | GDD + architecture | required | PROJECT BASELINE |
| temporarily restrict prepared door/route | GDD | required | PROJECT BASELINE |
| pre-lock warning | GDD | required | PROJECT BASELINE |
| preserve route to progression | GDD + architecture | required | PROJECT BASELINE |
| FacilityGraph reasoning | architecture | required | PROJECT BASELINE |
| physical Warden transform/avatar | no required gameplay evidence | optional presentation only if art/network source later requires | IMPLEMENTATION BINDING TBD |
| physical patrol | not evidenced | out of scope | OUT OF SCOPE / NOT EVIDENCED |
| vision/hearing | not evidenced for route policy | out of scope | OUT OF SCOPE / NOT EVIDENCED |
| chase | not evidenced | out of scope | OUT OF SCOPE / NOT EVIDENCED |
| attack/recover | telemetry catalog is insufficient gameplay evidence | out of scope | OUT OF SCOPE / NOT EVIDENCED |
| NavMesh movement | no physical movement requirement | not required by route controller | OUT OF SCOPE / NOT EVIDENCED |

**DETAILED-DESIGN DECISION:**

Warden v1.0 is implemented as a route-pressure gameplay controller. No combat FSM is introduced.

If a later approved gameplay contract requires physical chase/attack, that subsystem must be designed separately and must never bypass this route SafetyValidator.

---

# 10. Runtime Data Flow

```text
Objective / Phase / Door / Route / Tool / Scenario facts
                    ↓
         Authoritative DoorStateAdapters
                    ↓
      FacilityGraph runtime edge overlay
                    ↓
        RequiredRouteTargetResolver
                    ↓
            CurrentRouteModel
                    ↓
      PressureCandidateGenerator
                    ↓
       candidate eligibility filter
                    ↓
         WardenSafetyValidator
                    ↓
       WardenRoutePressureEvaluator
                    ↓
              WardenPolicy
                    ↓
        exact candidate selected
                    ↓
      pre-telegraph safety precheck
                    ↓
       WardenTelegraphController
                    ↓
     relevant world event revalidation
                    ↓
     mandatory PRECOMMIT validation
                    ↓
     WardenRouteActionController
                    ↓
      exactly-once overlay apply
                    ↓
      FacilityGraph revision/update
                    ↓
      POST-APPLY safety validation
            ┌────────┴─────────┐
          valid             invalid
            ↓                  ↓
      active lock       fail-safe release
            ↓                  ↓
         expiry             revalidate
            ↓
    exactly-once release
            ↓
      FacilityGraph update
            ↓
         cooldown
```

No telemetry/Profile/GenAI branch feeds the policy.

---

# 11. Ownership Matrix

| Concern | Canonical owner | Owns/writes | Reads | Must not own |
|---|---|---|---|---|
| authored spatial IDs | facility/map content | stable IDs | map definitions | runtime lock state |
| graph topology definition | `FacilityGraphDefinition` | immutable nodes/edges metadata | authored facility data | current door overlay |
| canonical Warden route-lock authoring | map/facility content via `WardenRouteLockDefinition` | DoorId eligibility/support + complete affected player-route edge footprint | validated FacilityGraph identities | runtime lock/timer state |
| runtime edge state | `FacilityGraph` / door-state integration | authoritative effective traversal snapshot + revision | door/scenario/phase/tool/Warden facts | Warden policy history |
| base door state | door gameplay system | physical/base traversal state | interaction/door mechanics | Warden cooldown |
| Door Jammer state | Team Tool / jammer system | monster-traversal overlay + timer | tool action | Warden lock |
| scenario route state | ScenarioConfig/application layer | scenario route overlay | validated ScenarioConfig | current Warden DoorId |
| Warden route overlay | `WardenRouteActionController` | current Warden player-route restriction | selected Warden action | base/Jammer/scenario state |
| objective obligations | `RequiredRouteTargetResolver` | immutable per-evaluation route obligations | objective/phase state | hidden player tracking |
| route snapshot | `CurrentRouteModelBuilder` | immutable evaluation snapshot | graph + obligations + Warden context | persistent door authority |
| Warden config/content compatibility | `WardenConfigurationValidator` | bound/content validation result | PressureCandidateBound + canonical route-lock definitions | graph/safety/policy result |
| candidate generation | `WardenPressureCandidateGenerator` | complete current cheap-eligible candidate set within validated bound | snapshot/config | final apply |
| route safety | `WardenSafetyValidator` | validation result/reason | current combined state + candidate | door mutation |
| pressure metric | `WardenRoutePressureEvaluator` | metric result | safe pre/post graph snapshots | safety override |
| selection | `WardenPolicy` | candidate choice/reason | safe candidates + history/cooldown | graph mutation |
| action context | `WardenPressureContext` | selected/current action/history/cooldown | policy/action lifecycle | canonical door availability |
| telegraph | `WardenTelegraphController` | warned action identity/presentation timing | selected action | alternate silent candidate |
| atomic action | `WardenRouteActionController` | apply/release guards + Warden overlay | precommit result | non-Warden overlays |
| fail-safe | `WardenFailSafeController` | Warden-owned release command | post-apply/current safety | teleport/objective mutation |
| network | `WardenNetworkBinding` | durable client-required state | authoritative controller | policy on proxy |
| telemetry | `WardenTelemetryAdapter` | approved events only | authoritative facts | runtime route decision |
| debug | `WardenDebugProvider` | read-only snapshot | runtime state | mutation |

---

# 12. Legal Information Boundary

## 12.1 Warden may know

Warden route policy may read:

```text
current objective/phase identity
FacilityGraph topology/version
authoritative effective player-traversal edge state
WardenEligible metadata
scenario routeModifier constraints
active Warden action
recent Warden action history
cooldown state
required route obligations
door occupancy commit result for selected physical door
```

## 12.2 Warden may not know for candidate targeting

Forbidden inputs:

```text
exact hidden Player Transform
Player velocity
predicted player route
Stalker CurrentTarget/LKP
Listener hearing source
raw telemetry events
Telemetry DB
MatchScore
PlayerProfile
TeamProfile
GenAI output
client camera/view state
```

## 12.3 Safety origins are not player tracking

v1.0 fairness uses **designer-authored route-origin anchors**, not exact active player position.

These anchors are safety semantics, not target evidence.

This avoids:

```text
Warden reads exact hidden player location
→ locks the door directly in front of that player
```

without an approved gameplay contract.

---

# 13. Facility Spatial IDs

Shared facility identities:

```text
MapId
GameplayZoneId
RegionId
DoorId
FacilityNodeId
FacilityEdgeId
```

Rules:

- IDs are stable designer/content identities;
- Warden and Stalker may reuse `GameplayZoneId`, `RegionId`, and `DoorId` when they refer to the same physical entity;
- Warden does not reuse Stalker SpatialNodeId/NavMesh triangle identity;
- graph bake/validation must reject duplicate or dangling identities;
- exact string/enum/GUID representation is IMPLEMENTATION BINDING TBD.

---

# 14. FacilityGraph Definition

## 14.1 Canonical meaning

`FacilityGraph` is a gameplay-route graph representing player progression connectivity.

It is not:

- NavMesh triangulation;
- Stalker patrol graph;
- a list of every corridor polygon;
- a dynamic pathfinding replacement.

## 14.2 Canonical directed runtime representation

**DETAILED-DESIGN DECISION**

Runtime FacilityGraph uses directed traversal arcs.

A bidirectional authored connection is compiled as:

```text
A → B
B → A
```

A one-way gameplay route is represented only in the legal direction.

This handles bidirectional doors, one-way transitions, phase gates, and future authored asymmetry with one consistent reachability algorithm.

## 14.3 Conceptual schema

```text
FacilityGraphDefinition
- MapId
- MapVersion
- FacilityGraphDefinitionVersion
- Nodes[]
- Edges[]
- DoorDefinitions[]
- WardenRouteLockDefinitions[]

FacilityNodeDefinition
- FacilityNodeId
- RegionId?
- GameplayZoneId?
- ObjectiveTags[] only where consumed
- RouteOriginTags[] only where consumed

FacilityEdgeDefinition
- FacilityEdgeId
- FromNodeId
- ToNodeId
- DoorId?
- BaseTraversalCost
- PlayerTraversableByDefault
- MonsterTraversableByDefault?
- RouteClass? only if consumed

DoorDefinition
- DoorId
- associated directed FacilityEdgeIds[]
- SupportsDoorJammer?  // only if authored Team Tool binding uses it

WardenRouteLockDefinition
- DoorId
- WardenEligible
- SupportsWardenRouteLock
- AffectedPlayerRouteEdgeIds[]
```

Exact C# class/serialization names are not frozen.

### 14.3.1 Canonical Warden route-action footprint — WAR-DD-01

**DETAILED-DESIGN DECISION**

`WardenRouteLockDefinition` is the single canonical authoring definition for a Warden route-lock action.

```text
one DoorId
→ zero or one WardenRouteLockDefinition
→ one complete immutable AffectedPlayerRouteEdgeIds[] footprint
```

`WardenEligible` and `SupportsWardenRouteLock` are owned **only** by `WardenRouteLockDefinition`.

They are not duplicated on:

```text
FacilityEdgeDefinition
DoorDefinition
PressureCandidate
WardenPressureAction
```

For a normal bidirectional door:

```text
A → B = Edge_AB
B → A = Edge_BA

WardenRouteLockDefinition(Door_A)
- AffectedPlayerRouteEdgeIds = [Edge_AB, Edge_BA]
```

The complete footprint is atomic:

```text
candidate simulation footprint
=
precommit validation footprint
=
actual Warden overlay apply footprint
=
release footprint
=
post-apply validation footprint
```

A candidate may never simulate only one directed edge and then apply a DoorId action that blocks a larger set.

Authoring validation must reject a Warden route-lock definition when:

- `AffectedPlayerRouteEdgeIds[]` is empty;
- an affected edge does not exist;
- an affected edge is duplicated inside the footprint;
- an affected edge is not bound to the same `DoorId` represented by the definition;
- more than one `WardenRouteLockDefinition` claims the same `DoorId`;
- Warden route eligibility/support is authored anywhere other than the canonical definition.

A one-way authored route-lock point may legitimately contain one directed edge. A bidirectional physical door normally contains both directed player-route edges.

Exact serialization/storage remains IMPLEMENTATION BINDING TBD; the footprint semantics are frozen.

## 14.4 RouteClass

Because Map Flow distinguishes Main and Alternative routes, FacilityEdge may consume:

```text
Main
Alternative
Connector
```

only as authored diagnostic/policy metadata.

`RouteClass` is not safety authority.

Warden may not assume Main Route is mandatory or automatically select Main Route.

---

# 15. FacilityGraph Authoring / Validation

## 15.1 Strategy

Canonical strategy:

```text
designer-authored route topology
+
deterministic editor/bake validation
+
immutable runtime topology
+
authoritative runtime edge-state overlay
```

Suitable Unity representations:

- ScriptableObject;
- scene metadata;
- generated serialized asset from map definitions;
- hybrid source + baked runtime asset.

The storage format is IMPLEMENTATION BINDING TBD.

## 15.2 Validation rules

Build/editor validation must detect at least:

```text
MissingDefinition
VersionMismatch
MissingMapId
DuplicateNodeId
DuplicateEdgeId
DuplicateDoorId
DanglingEdge
UnknownDoorReference
DoorBindingMismatch
InvalidTraversalCost
MissingRouteOrigin
MissingObjectiveBinding
DuplicateWardenRouteLockDoorId
InvalidWardenRouteLockFootprint
UnsupportedRouteClass
```

`BaseTraversalCost` must be finite and strictly positive for route-cost evaluation.

## 15.3 Compatibility invariant

```text
runtime map/content identity
!= FacilityGraphDefinition compatibility identity
→ FacilityGraph invalid
→ Warden route pressure disabled
→ no telegraph
→ no lock
→ critical diagnostic
```

Stale DoorId/RegionId mappings are never consumed silently.

## 15.4 Warden lock map markers

Map Flow visually evidences prepared Warden lock markers.

Do not convert visual marker names directly into production DoorIds without the map-authoring binding/validation step.

---

# 16. Runtime Edge State

`FacilityGraph` keeps immutable topology separate from mutable traversal state.

Conceptual effective directed-edge view:

```text
FacilityRuntimeEdgeState
- FacilityEdgeId
- PlayerTraversable
- MonsterTraversable
- WardenRouteLockRuntimeAvailable   // derived runtime availability only; not WardenEligible ownership
- ActiveBlockingReasons
- Revision
```

Effective edge state is recomputed from authoritative overlay sources when a relevant event changes.

FacilityGraph maintains a monotonically changing `FacilityGraphRevision` or equivalent compatibility revision whenever route-relevant effective state changes.

Exact integer/version type is IMPLEMENTATION BINDING TBD.

---

# 17. Door State Composition

## 17.1 Not one `IsLocked`

```text
EffectiveDoorState
=
BaseDoorState
+ ScenarioRouteState
+ ObjectivePhaseState
+ WardenRouteOverlay
+ DoorJammerOverlay
```

No subsystem directly overwrites another subsystem's source state.

## 17.2 Traversal channels

Conceptually:

```text
EffectivePlayerTraversable
=
BasePlayerTraversable
AND ScenarioPlayerTraversable
AND ObjectivePhasePlayerTraversable
AND NOT WardenPlayerRouteBlocked

EffectiveMonsterTraversable
=
BaseMonsterTraversable
AND ScenarioMonsterTraversable
AND ObjectivePhaseMonsterTraversable
AND NOT DoorJammerMonsterBlocked
AND other approved monster-door rules
```

Warden route safety uses the **player traversal channel**.

Door Jammer uses the **monster traversal channel** under current GDD evidence.

If the eventual physical door binding closes a collider for all entities, the adapter must translate that physical state without conflating ownership semantics.

## 17.3 Runtime events

```text
DoorStateChanged
ScenarioRouteModifierChanged
ObjectivePhaseGateChanged
DoorJammerStateChanged
WardenActionApplied
WardenActionReleased
→ recompute affected edge channels
→ increment FacilityGraphRevision
→ notify active Warden reasoning/lifecycle
```

Do not poll every door every frame.

---

# 18. Door Jammer Interaction

## 18.1 Approved semantics

Door Jammer:

```text
temporarily blocks monsters through supported doors
duration baseline = 1 minute
not valid on every door
```

It is not source evidence for blocking player route traversal.

## 18.2 v1.0 independent-overlay decision

**DETAILED-DESIGN DECISION**

Door Jammer and Warden route lock are independent overlays.

Door Jammer does not:

- cancel an active Warden route lock;
- grant immunity from Warden selection;
- close the player's route by itself;
- overwrite Warden timer;
- alter Warden action identity.

Warden does not:

- cancel Door Jammer;
- reset its timer;
- overwrite its monster traversal state.

## 18.3 Interaction matrix

| State pair | Coexist? | Player traversal effect | Monster traversal effect | Timer owner | Warden may select? | SafetyValidator |
|---|---:|---|---|---|---:|---|
| Base open + no overlay | Yes | base | base | base system | if canonical `WardenRouteLockDefinition` is eligible/supported | current effective player state |
| Warden lock + Base | Yes | Warden-blocked | base/physical binding dependent | Warden | already active door not selectable | include Warden overlay |
| Door Jammer + Base | Yes | unchanged by Jammer | Jammer-blocked | Door Jammer system | Yes if otherwise eligible | Jammer does not close player route |
| Warden lock + Door Jammer | Yes | Warden-blocked | Jammer-blocked plus approved physical rules | independent timers | active door not reselected | include player-affecting overlays only |
| Scenario modifier + Warden | Yes if compatible | composition | composition | Scenario / Warden separately | only if scenario permits | combined current state |
| Objective/phase restriction + Warden | only if candidate valid | composition | composition | objective system / Warden | protected edge may be ineligible | combined current state |

## 18.4 Physical Warden embodiment caveat

Because Warden physical traversal is not evidenced in v1.0, Door Jammer has no Warden-policy cancellation effect.

If later approved Warden physical movement is added, Door Jammer may affect that movement channel without changing route-control ownership.

---

# 19. Current Route Model

`CurrentRouteModel` is an immutable decision snapshot.

Conceptual contents:

```text
CurrentRouteModel
- FacilityGraphRevision
- CurrentPhase
- RequiredRouteObligations[]
- EffectivePlayerTraversalSnapshot
- ActiveWardenAction?
- AppliedScenarioRouteModifierId?
- WardenRouteLockDefinitions[]
- RecentWardenActionHistoryProjection
- CooldownReady
```

It does not store:

- hidden Player Transform;
- persistent door authority;
- mutable duplicate graph state.

A candidate/safety/pressure evaluation references one graph revision.

---

# 20. Required Route Target Resolver

## 20.1 Purpose

`RequiredRouteTargetResolver` converts objective/phase state into **route obligations**.

It answers:

> From which designer-authored fairness origins must which current objective destination(s) remain reachable?

## 20.2 Route obligation contract

```text
RequiredRouteObligation
- ObligationId
- OriginNodeIds[]
- DestinationNodeIds[]
- RequireEveryOrigin = true
- RequireAnyDestination = true
- Purpose
```

Multiple mandatory objective locations are represented by multiple obligations rather than weakening one obligation with a broad destination set.

## 20.3 Objective binding

The v1.0 route-obligation semantics are fixed even though exact FacilityNodeIds remain map bindings.

| Gameplay context | Required route obligation(s) | Exit protected as current target? | Classification |
|---|---|---:|---|
| Core Collection / Delivery | **One obligation per remaining active required Energy Core location**, each from `CoreCollectionRouteOrigins` to that Core node; plus **one Power Hub/Core Receiver obligation** while Core delivery remains required | No | PROJECT BASELINE gameplay + DETAILED-DESIGN binding rule |
| Power Puzzle | **Two separate obligations**: `PowerPuzzleRouteOrigins → Power Control` and `PowerPuzzleRouteOrigins → Distribution Panel` | No | PROJECT BASELINE gameplay + DETAILED-DESIGN binding rule |
| Security Hold | `SecurityHoldRouteOrigins → Security Terminal` | No | PROJECT BASELINE |
| Final Hunt | `FinalHuntRouteOrigins → Exit` | **Yes** | PROJECT BASELINE |
| Match complete/ended | no new Warden pressure action | n/a | DETAILED-DESIGN DECISION |

Rules:

```text
remaining active Energy Core locations
=
authoritative Objective System's currently required Core nodes
```

Warden does not choose which Core spawned and does not infer a Core from player position.

A separate obligation is used for each mandatory destination so that:

```text
one reachable Core
X→ proves all remaining required Cores are reachable
```

Likewise, Power Control accessibility cannot substitute for Distribution Panel accessibility.

Exact FacilityNodeIds and the membership of each `*RouteOrigins` set are:

```text
MAP AUTHORING TBD
```

but every required binding must exist before the FacilityGraph is considered valid for Warden pressure.

## 20.4 No objective-name guessing at runtime

Runtime resolver consumes explicit authoritative objective IDs/bindings.

It must not parse display strings such as `"Power Puzzle"` to find graph nodes.

---

# 21. Reachability Contract

## 21.1 v1.0 quantifier

**DETAILED-DESIGN DECISION**

Safety is defined over designer-authored **fairness origin anchors**, not current player Transform.

For every `RequiredRouteObligation`:

```text
FOR EVERY origin O in obligation.OriginNodeIds
THERE MUST EXIST
AT LEAST ONE directed legal player-traversable path
from O
to ANY destination D in obligation.DestinationNodeIds
```

All active obligations must pass.

Equivalent:

```text
∀ obligation
  ∀ origin ∈ obligation.origins
    ∃ destination ∈ obligation.destinations
      ReachablePlayerPath(origin, destination) == true
```

## 21.2 Why this policy

Compared with alternatives:

| Policy | Problem/benefit | Decision |
|---|---|---|
| exact current player regions | creates hidden-player route-target channel and network dependency | reject |
| one team-level origin only | can strand valid progression/rescue areas | reject |
| designer-authored safe origin set | deterministic, conservative, testable, no hidden-player tracking | **selected** |
| every FacilityNode | overly restrictive and may protect irrelevant maintenance/dead-end spaces | reject |

## 21.3 Origin authoring rule

Each gameplay phase/objective binding must provide a non-empty set of route origins representing locations from which players are legitimately expected to continue that objective.

Final Hunt origin authoring should cover the relevant rescue/return space needed by the approved map flow without requiring exact player locations.

Exact nodes are MAP AUTHORING TBD.

## 21.4 Traversal semantics

Reachability uses:

- directed FacilityGraph arcs;
- effective **player** traversal state;
- scenario route modifier;
- objective/phase gates;
- base door state;
- currently active Warden overlay;
- candidate Warden overlay when simulating;
- other approved player-route overlays.

Door Jammer by itself does not remove a player edge.

## 21.5 At least one route

The existential path inside every origin obligation is the project's "at least one legal route" fairness rule.

Warden need not preserve two alternative paths after its action.

---

# 22. WardenSafetyValidator

## 22.1 Contract

```text
CurrentRouteModel
+ candidate overlay
→ simulated combined player-route graph
→ validate graph/revision
→ evaluate every RequiredRouteObligation
→ validate Final Hunt exit obligation when active
→ WardenSafetyValidationResult
```

## 22.2 Result

```text
WardenSafetyValidationResult
- Status = VALID | REJECTED | NOT_EVALUATED
- RejectReason
- GraphRevision
- CandidateActionId?
- CandidateDoorId?
- CandidateAffectedPlayerRouteEdgeIds[]
- FailedObligationId?
- FailedOriginNodeId?
```

## 22.3 Safety result ownership

`WardenSafetyValidator` owns only runtime route-safety/commit outcomes.

It does **not** own:

- malformed FacilityGraph authoring;
- Warden eligibility filtering;
- cooldown/active-lock filtering;
- candidate-bound scheduling.

Canonical safety status/reason rule:

```text
VALID
→ RejectReason = None

REJECTED
→ RejectReason is one WardenSafetyRejectReason

NOT_EVALUATED
→ the safety proof did not run to a meaningful result;
  graph-definition/configuration failure is reported by its owning validator,
  not duplicated as a WardenSafetyRejectReason
```

Canonical `WardenSafetyRejectReason` values are defined in §46.3.

Door Jammer conflict is not automatically a safety failure because it does not close the player route channel.

## 22.4 Combined-state requirement

Validation input is:

```text
base topology
+ current base/physical player traversal
+ scenario route modifier
+ objective/phase restrictions
+ current Warden overlay
+ all relevant external player-route overlays
+ candidate Warden overlay applied to the complete canonical `AffectedPlayerRouteEdgeIds[]` footprint
```

Never validate against pristine topology while ignoring active modifiers.

## 22.5 Safety dominates policy

```text
SafetyStatus != Valid
→ candidate cannot be selected/applied
```

No pressure score, random value, AED request, telemetry value, or debug control can override this.

---

# 23. Pressure Candidate Eligibility

The candidate unit is one canonical `WardenRouteLockDefinition`, never an individual FacilityEdge.

A route-lock definition may become a candidate only when all are true:

```text
FacilityGraph valid
Warden action lifecycle READY
Cooldown complete
no active Warden pressure lock
WardenRouteLockDefinition exists and passed graph-definition validation
WardenRouteLockDefinition.WardenEligible == true
WardenRouteLockDefinition.SupportsWardenRouteLock == true
DoorId exists
AffectedPlayerRouteEdgeIds[] is the canonical validated non-empty footprint
at least one affected player-route edge is currently traversable
the action is not permanently unavailable/protected by objective or phase authoring
current ScenarioConfig permits this route action
no incompatible door action is active
doorway commit capability exists if physical closure is required
```

The candidate and every later lifecycle stage carry or resolve the **same complete footprint**.

Then SafetyValidator simulates that complete footprint atomically.

Important:

```text
WardenRouteLockDefinition.WardenEligible
=
may be considered
```

not:

```text
safe to lock
```

`FacilityEdgeDefinition` and `DoorDefinition` cannot independently authorize Warden selection.

---

# 24. Pressure Candidate Generator

## 24.1 Input

```text
CurrentRouteModel
WardenPressureContext
Warden static config
```

## 24.2 Output

Bounded:

```text
PressureCandidate[]
```

Conceptual candidate:

```text
PressureCandidate
- CandidateId
- DoorId
- AffectedPlayerRouteEdgeIds[]
- ActionType = TemporarilyBlockPlayerRoute
- GraphRevision
- PreActionRouteFacts
- SimulatedPostActionRouteFacts?
- SafetyResult
- RoutePressureResult?
- RecentUseFacts
```

## 24.3 Generation order

```text
authored WardenRouteLockDefinitions
→ canonical WardenEligible / support check
→ validate full-set bound contract
→ apply current cheap runtime eligibility filters
→ stable DoorId order
→ evaluate the ENTIRE current cheap-eligible candidate set
→ SafetyValidator using each candidate's full AffectedPlayerRouteEdgeIds[]
→ RoutePressure only for safe candidates
```

`PressureCandidateBound` is a declared maximum supported candidate-set capacity for one logical Warden policy evaluation.

Its exact value remains:

```text
TUNING TBD
```

but the v1.0 relation is frozen:

```text
PressureCandidateBound
>=
AuthoredCheapEligibleWardenRouteLockDefinitionCount
```

where `AuthoredCheapEligibleWardenRouteLockDefinitionCount` means the number of authoring-valid route-lock definitions that are:

```text
WardenEligible == true
AND
SupportsWardenRouteLock == true
```

before transient runtime filters such as cooldown, current door traversal state, objective protection, or current ScenarioConfig availability.

Because the current runtime cheap-eligible set is a subset of that authored set, one logical policy evaluation can always inspect the complete current candidate set when configuration/content is valid.

Candidate generation must not use hidden player state.

Optional authored route/objective relevance metadata may participate in deterministic policy ranking only after the complete candidate set has been admitted; it must not be used to truncate the set.

## 24.4 Candidate-bound full-set safeguard — WAR-SG-01

**DETAILED-DESIGN DECISION**

v1.0 does **not** use:

```text
rotating windows
continuation scheduling
first-N truncation
window-scoped NoSafeCandidate
window-scoped NoMeaningfulPressure
```

Validation rule:

```text
if PressureCandidateBound < AuthoredCheapEligibleWardenRouteLockDefinitionCount:
    ConfigurationStatus = INVALID
    ConfigurationReason = PressureCandidateBoundTooSmall
    → do not run Warden candidate policy
    → no telegraph
    → no lock
    → emit development diagnostic
```

Runtime defensive guard:

```text
if CurrentCheapEligibleCandidateCount > PressureCandidateBound:
    treat as the same configuration/content contract failure
    → do not truncate
    → do not evaluate a partial candidate set
    → no Warden action
    → diagnostic
```

This runtime guard exists for content hot-reload/version mismatch/incorrect binding defense; valid authored content should fail earlier during deterministic initialization/content validation.

Consequences:

```text
one logical Warden policy evaluation
=
one complete current cheap-eligible candidate set
```

Therefore:

- `NoSafeCandidate` is global for that logical evaluation;
- `NoMeaningfulPressure` is global for that logical evaluation;
- no scheduler lifecycle exists;
- no extra timer/cadence is introduced;
- no starvation proof or continuation trigger is required;
- performance remains bounded by the validated authored candidate-capacity contract.

The safeguard does not change Warden authority, SafetyValidator, RoutePressure, one-active-lock, telegraph, or route gameplay semantics.

---

# 25. RoutePressure Model

## 25.1 Selected concept

**DETAILED-DESIGN DECISION**

Warden pressure uses the increase in **mean shortest legal route cost across current route obligations**.

Why:

- deterministic;
- no all-simple-path enumeration;
- interpretable as detour pressure;
- works with directed graph;
- maps to the Research Facility's learnable alternate-route design;
- inexpensive on a small authored graph;
- independent of hidden player position.

## 25.2 Route cost

For each obligation and each origin:

```text
ShortestCost(origin, obligation.DestinationNodeIds)
=
minimum finite directed path cost to any allowed destination
```

`BaseTraversalCost` is positive authored/baked static route cost.

Dynamic player-specific speed is not included.

## 25.3 Aggregate route cost

Let `P` be all `(obligation, origin)` pairs.

```text
MeanShortestRouteCost(G)
=
sum(ShortestCost_G(pair))
/
count(P)
```

Safety validation guarantees all required costs are finite for a safe candidate.

---

# 26. RoutePressure Metric

## 26.1 `RoutePressure_v1.0`

For a safe candidate:

```text
Cpre  = MeanShortestRouteCost(current combined graph)
Cpost = MeanShortestRouteCost(simulated graph + candidate)
```

Because a v1.0 Warden action only removes/restricts player traversal, a valid candidate should satisfy:

```text
Cpost >= Cpre
```

Metric:

```text
if Cpre > 0 and Cpost >= Cpre:
    MetricStatus = VALID
    RoutePressure_v1.0 = (Cpost - Cpre) / Cpost

if Cpre == 0 and Cpost == 0:
    MetricStatus = VALID
    RoutePressure_v1.0 = 0

if Cpre == 0 and Cpost > 0:
    MetricStatus = NOT_EVALUATED
    RoutePressure_v1.0 = null
    diagnostic = DegeneratePreRouteCost

if Cpost < Cpre:
    MetricStatus = INVALID
    RoutePressure_v1.0 = null
    diagnostic = UnexpectedRouteCostDecrease
```

For the normal case:

```text
0 <= RoutePressure_v1.0 < 1
```

## 26.2 Exact semantics

| Field | Definition |
|---|---|
| Unit | unitless ratio |
| Input graph | same `CurrentRouteModel` revision used by candidate |
| Origin set | every origin from every active RequiredRouteObligation |
| Destination | nearest legal destination inside each obligation |
| Pre-action value | `Cpre` |
| Post-action value | `Cpost` after candidate overlay |
| Numerator | `Cpost - Cpre` |
| Denominator | `Cpost` |
| Aggregation | mean shortest cost before metric, not mean of per-origin ratios |
| Reset/window | per candidate evaluation; aggregated analyses must version their own window |
| Interpretation | 0 = no shortest-route detour; larger = greater normalized detour |
| Caveat | pressure is descriptive, not a guarantee of fun/fairness |

## 26.3 Separate diagnostics

Do not overload RoutePressure:

```text
MeanShortestRouteCost
ReachableOriginCount
CandidateSafetyStatus
AlternativeRouteDiagnosticCount   // optional diagnostic only
```

No `RoutePressure` denominator ambiguity is allowed.

---

# 27. Warden Policy

## 27.1 Selection pipeline

```text
complete current cheap-eligible route-lock definition set
→ SafetyValidator using full canonical footprint
→ keep safe
→ compute RoutePressure
→ keep only MeaningfulPressure candidates
→ apply repetition partition
→ deterministic selection
```

A `MeaningfulPressure` candidate is frozen as:

```text
SafetyStatus == VALID
AND
MetricStatus == VALID
AND
RoutePressure_v1.0 > 0
```

There is no gameplay-tuning threshold above zero in v1.0.

## 27.2 Zero-pressure / `NoMeaningfulPressure` semantics — WAR-DD-03

**DETAILED-DESIGN DECISION**

A safe candidate with:

```text
MetricStatus = VALID
RoutePressure_v1.0 = 0
```

is legal from a reachability perspective but **not selectable by WardenPolicy v1.0**, because the current RoutePressure model proves no shortest-route detour pressure.

Likewise, a candidate with:

```text
MetricStatus = NOT_EVALUATED
or
MetricStatus = INVALID
```

is not policy-selectable. Its metric diagnostic remains observable and must not be converted to zero for ranking.

Canonical policy outcomes for the complete current cheap-eligible candidate set:

```text
no safe candidate
→ NO_ACTION
→ SelectionReason = NoSafeCandidate

one or more safe candidates exist
BUT no safe candidate satisfies MeaningfulPressure
→ NO_ACTION
→ SelectionReason = NoMeaningfulPressure

at least one MeaningfulPressure candidate exists
→ selection continues only over MeaningfulPressure candidates
```

`NoMeaningfulPressure` therefore has one exact meaning. It is not an eligibility rejection, SafetyValidator rejection, or metric status.

A zero-pressure/no-action evaluation:

```text
does not start telegraph
does not apply a route lock
does not consume cooldown
does not add RecentWardenActionHistory
```

The policy waits for the next normal meaningful evaluation trigger. It does not spin/re-evaluate in the same simulation step.

## 27.3 Deterministic variety

Candidate selection:

```text
1. MeaningfulPressure candidates only
2. if any candidate DoorId is NOT in RecentWardenActionHistory:
       consider only those fresh candidates
   else:
       consider all MeaningfulPressure candidates
3. choose highest RoutePressure_v1.0
4. tie → least recently used DoorId
5. tie → stable DoorId
```

No unseeded randomness.

This prevents the policy from simply choosing the maximum-pressure same door forever.

## 27.4 No-action safety

```text
NoSafeCandidate
or NoMeaningfulPressure
→ no telegraph
→ no lock
→ remain READY / evaluation dormant until the next normal meaningful trigger
```

Because §24.4 forbids partial candidate-set evaluation, these reasons are global for the logical policy evaluation.

If the configured bound cannot cover the authored candidate set, policy does not run and neither selection reason is emitted as a substitute for the configuration/content validation failure.

No arbitrary lock fallback.

---

# 28. Repetition / Cooldown / Budget

## 28.1 One active lock

**DETAILED-DESIGN DECISION**

```text
MaxActiveWardenLocks = 1
```

for v1.0.

Reasons:

- simpler safety proof;
- clearer telegraph;
- easier overlay ownership;
- easier exactly-once networking;
- easier fail-safe;
- sufficient M2 route-pressure identity;
- lower thesis evaluation ambiguity.

SafetyValidator still supports combined-state simulation so the safety logic is not dependent on a pristine graph.

## 28.2 Recent action history

`RecentWardenActionHistory` is bounded.

Stores immutable:

```text
DoorId
AffectedPlayerRouteEdgeIds[]   // immutable canonical footprint snapshot or definition reference
ActionEndTime/Tick
ActionResult
```

Capacity is TUNING TBD.

No player identity/position.

## 28.3 Cooldown-only baseline

v1.0 uses:

```text
one active lock
+
one WardenCooldownDuration
```

No phase action-budget subsystem is added without evidence.

`WardenCooldownDuration` is TUNING TBD.

## 28.4 Cooldown consumption

```text
candidate rejected before telegraph
→ no cooldown

telegraph starts
→ pressure opportunity is committed

telegraph later cancels
→ enter cooldown

lock applies then expires/releases
→ enter cooldown
```

This prevents repeated invalid telegraph spam.

---

# 29. WardenPressureContext

Canonical mutable Warden-owned state:

```text
WardenPressureContext
- CurrentAction?
- RecentWardenActionHistory
- CooldownEnd?
- LastPolicyEvaluationRevision?
- LastSelectionReason?
- LastSafetyResult?
- LastFailSafeResult?
```

`CurrentAction` owns Warden action lifecycle state only.

It does not duplicate canonical door availability.

Conceptual action:

```text
WardenPressureAction
- WardenActionId
- DoorId
- AffectedPlayerRouteEdgeIds[]
- ActionPhase
- SelectedGraphRevision
- TelegraphEndTime/Tick?
- AppliedAt?
- LockEndTime/Tick?
- ApplyResolved
- ReleaseResolved
```

---

# 30. Route-Action Lifecycle

Route-pressure lifecycle:

```text
READY
→ CANDIDATE_SELECTED
→ TELEGRAPHING
→ PRECOMMIT_VALIDATION
→ APPLIED
→ RELEASING
→ COOLDOWN
→ READY
```

Cancellation paths:

```text
CANDIDATE_SELECTED validation failure
→ READY

TELEGRAPHING relevant revalidation failure
→ CANCELLED
→ COOLDOWN

PRECOMMIT_VALIDATION failure
→ CANCELLED
→ COOLDOWN

APPLIED post-check failure
→ FAIL_SAFE_RELEASING
→ COOLDOWN
```

These are Warden route-action lifecycle phases, not a monster combat FSM.

---

# 31. Telegraph Contract

## 31.1 Required flow

```text
candidate selected
→ safety precheck
→ allocate/bind WardenActionId
→ bind exact DoorId + action
→ TELEGRAPHING
→ synchronize visible/audible warning
→ telegraph window
→ PRECOMMIT validation
→ apply same warned action OR cancel
```

## 31.2 Door identity invariant

```text
telegraph Door A + canonical footprint F
X→ silently commit Door B
X→ silently commit a different footprint F2
```

If Door A is no longer valid:

```text
cancel old action
→ cooldown
→ future new candidate requires new WardenActionId + new telegraph
```

## 31.3 Telegraph duration

```text
WardenTelegraphDuration
= TUNING TBD
```

Must be long enough for approved player-facing warning/counterplay, but no number is invented here.

## 31.4 Relevant world change during telegraph

On route-relevant graph revision:

```text
mark CurrentAction safety-dirty
→ immediately revalidate exact warned candidate against new combined state
→ if invalid: cancel telegraph
→ if valid: continue same telegraph
→ regardless, mandatory precommit validation still runs at commit boundary
```

This is event-driven.

---

# 32. Precommit Revalidation / TOCTOU

## 32.1 Mandatory check

Immediately before apply:

```text
current authoritative FacilityGraph snapshot
+ current objective obligations
+ current scenario/phase/door overlays
+ exact warned Warden candidate
→ WardenSafetyValidator
```

The pre-telegraph result is never sufficient by itself.

## 32.2 Revision handling

```text
candidate selected on revision N
→ telegraph
→ revision N+1
→ old validation cannot authorize apply
```

Even if event-time revalidation passed, final precommit validation runs again.

## 32.3 Door occupancy commit gate

If Warden lock uses physical closure/collision:

```text
doorway occupancy unsafe
→ PreCommit failure = DoorwayOccupied
→ cancel
→ route unchanged
→ cooldown
```

Do not delay and later close the door without a new telegraph.

Exact overlap/collider implementation is IMPLEMENTATION BINDING TBD.

---

# 33. Atomic Door / Route Action

## 33.1 Exactly-once apply

```text
one WardenActionId
→ precommit Valid for the same DoorId + AffectedPlayerRouteEdgeIds[]
→ ApplyResolved == false
→ write WardenRouteOverlay once to that complete canonical footprint
→ ApplyResolved = true
→ FacilityGraph recompute/revision
→ synchronize durable action/door state
→ authoritative outcome fact once
→ post-apply validation
```

Duplicate callback/resimulation:

```text
ApplyResolved == true
→ no second overlay mutation
→ no timer restart
→ no duplicate gameplay outcome
```

## 33.2 Ownership

Action controller writes only Warden-owned overlay.

It must not mutate:

- base door state;
- Door Jammer timer/state;
- ScenarioConfig object;
- objective state.

---

# 34. Post-Apply Revalidation

After actual edge-state update:

```text
build fresh CurrentRouteModel
→ validate every active route obligation
```

Valid:

```text
action remains APPLIED
```

Invalid:

```text
immediate fail-safe
```

Post-apply validation checks the real resulting combined state, not the candidate simulation result object.

---

# 35. Fail-Safe Reopen

## 35.1 v1.0 policy

Because v1.0 permits one active Warden lock:

```text
active Warden action contributes to unsafe state
→ release current Warden overlay immediately
→ recompute FacilityGraph
→ revalidate
→ critical development diagnostic
```

## 35.2 If route remains invalid after Warden release

The Warden cannot overwrite other systems to repair the map.

```text
release Warden-owned state
→ graph still invalid due external/base/scenario state
→ disable new Warden pressure actions
→ surface critical facility safety diagnostic
→ await a later authoritative graph revision that validates
```

Do not:

- teleport players;
- teleport monsters;
- change objective;
- invent a route;
- remove Door Jammer;
- alter ScenarioConfig directly.

## 35.3 Determinism

With one active Warden lock there is no release-order ambiguity.

If a future version supports concurrent Warden locks, that design must define deterministic release order and cumulative safety before increasing the active-lock cap.

---

# 36. Lock Expiry / Release

`WardenLockDuration` is independent from Door Jammer's one-minute duration.

```text
WardenLockDuration
= TUNING TBD
```

Lifecycle:

```text
APPLIED
→ authoritative lock expiry reached
→ release guard checks ReleaseResolved == false
→ remove the exact Warden overlay from the same canonical AffectedPlayerRouteEdgeIds[] once
→ ReleaseResolved = true
→ FacilityGraph recompute/revision
→ post-release safety diagnostic
→ COOLDOWN
```

Manual fail-safe release uses the same exactly-once release guard.

Expiry never resets unrelated door overlays.

---

# 37. Objective / Phase Changes

On:

```text
ObjectiveStateChanged
PhaseChanged
ScenarioRouteModifierChanged
ObjectivePhaseGateChanged
```

Host:

```text
recompute affected FacilityGraph state
→ resolve new RequiredRouteObligations
→ if TELEGRAPHING:
     revalidate exact candidate immediately
→ if APPLIED:
     revalidate active Warden lock
→ unsafe active lock:
     fail-safe release
→ READY:
     future policy uses new route model
```

Do not wait for lock expiry to discover objective progression is blocked.

---

# 38. Final Hunt

Final Hunt is gameplay/scenario context, not Warden FSM state.

Required route contract:

```text
Final Hunt
→ Exit destination becomes mandatory route obligation
→ every authored Final-Hunt fairness origin must retain a legal player path to Exit
```

The map permits players to return to rescue teammates; Final-Hunt route-origin authoring must represent the approved rescue-relevant accessible space without using hidden exact player positions.

Warden may continue normal route-pressure behavior only if the candidate passes the same safety/telegraph/cooldown contract.

No special Final Hunt Warden power is introduced.

---

# 39. Physical Warden / Combat Integration

## 39.1 v1.0 status

```text
Physical patrol: NOT EVIDENCED
Vision/Hearing pursuit: NOT EVIDENCED
CHASE: NOT EVIDENCED
ATTACK: NOT EVIDENCED
RECOVER: NOT EVIDENCED
```

The generic telemetry catalog's possible `WARDEN_ATTACK` reason does not override gameplay-source precedence.

## 39.2 Implementation consequence

Do not add:

- NavMeshAgent solely because other monsters have one;
- combat target memory;
- Stalker/Listener FSM states;
- attack collider;
- chase network state.

A presentation avatar/ambient entity may be added later if approved, but it must remain separate from route-policy authority unless a gameplay contract expands Warden behavior.

---

# 40. ScenarioConfig / AED Boundary

## 40.1 Critical separation

```text
ScenarioConfig.routeModifier
=
validated scenario-level route configuration / allowed route context

Warden runtime route action
=
Host-authoritative temporary action selected by WardenPolicy
inside the currently applied scenario constraints
```

AED does not choose:

```text
current DoorId
current AffectedPlayerRouteEdgeIds[]
WardenActionId
telegraph target
lock start
lock release
fail-safe target
```

## 40.2 M1-015 timing

```text
PRE_MATCH
→ adaptive routeModifier request INVALID
→ TIMING_REJECTED

ALLOWED_PHASE_BOUNDARY
→ adaptive routeModifier may be requested
→ whitelist
→ route compatibility
→ Scenario Validator
→ no objective/exit unreachable
→ at least one legal route
→ no soft-lock
→ atomic apply
```

At PRE_MATCH, the routeModifier initial state comes from fixed/designer scenario content.

## 40.3 Runtime response to valid routeModifier change

```text
validated ScenarioConfig apply
→ ScenarioRouteState update
→ FacilityGraph revision
→ Warden telegraph/active lock revalidation
```

Warden does not partially repair or override a rejected ScenarioConfig.

---

# 41. Telemetry

## 41.1 One-way boundary

```text
Warden authoritative gameplay facts
→ WardenTelemetryAdapter
→ approved telemetry

Telemetry
X→ Warden candidate/policy/safety input
```

Forbidden runtime inputs:

- raw TelemetryEvent;
- MatchTelemetry;
- MatchScore;
- Profile;
- analytics DB.

## 41.2 Current production schema

Current schema does not freeze active Warden-specific production events for:

- telegraph start/cancel;
- route lock apply;
- candidate rejection;
- fail-safe reopen.

Therefore these remain development diagnostics in v1.0.

If persistent production research later requires them:

```text
PROPOSED — TELEMETRY CONTRACT REVISION REQUIRED
```

with explicit payload/userId/reason/schema version before emission.

Do not reuse unrelated `MONSTER_*` events to smuggle Warden route data.

---

# 42. Photon Fusion Authority

Host / Fusion State Authority owns:

```text
FacilityGraph authoritative runtime edge state
RequiredRouteTargetResolver inputs/output
CurrentRouteModel
candidate generation
SafetyValidator
RoutePressure evaluation
WardenPolicy
WardenPressureContext
telegraph start/cancel
precommit revalidation
Warden overlay apply
post-apply validation
lock expiry/release
fail-safe release
authoritative Warden gameplay facts
```

Clients may:

```text
render telegraph
render current door/lock presentation
render optional Warden avatar presentation
consume synchronized durable state
show read-only debug where allowed
```

Clients must not:

```text
select candidate
run SafetyValidator as authority
change routeModifier
apply/cancel/release lock
mutate FacilityGraph
restart timers
run WardenPolicy independently
```

---

# 43. Fusion Network Binding

## 43.1 Boundary

Recommended structure:

```text
NetworkObject
└── WardenNetworkBinding : NetworkBehaviour
        ├── Object.HasStateAuthority gate
        ├── durable action/telegraph/lock presentation state
        └── FixedUpdateNetwork()
                 ↓
      WardenRoutePressureController
                 ↓
     pure C# graph/safety/policy services
```

Exact classes are IMPLEMENTATION BINDING TBD.

## 43.2 Durable synchronized state candidates

Only state clients actually need:

```text
CurrentWardenActionPhase
CurrentWardenActionId?          // if presentation identity requires it
CurrentDoorId?                  // if telegraph/lock presentation requires it
TelegraphEndTick/Timer state?
LockEndTick/Timer state?
CurrentWardenLockActive
effective authoritative door presentation state
```

Do not replicate by default:

- full FacilityGraph;
- all candidate scores;
- RequiredRouteObligations;
- WardenPressureContext history;
- SafetyValidator internals;
- route costs;
- debug traces.

## 43.3 RPC

RPC may carry:

- player door interaction/tool request where existing gameplay contract uses it;
- non-durable presentation cue as supplement.

RPC is not sole durable state for active telegraph/lock because late join must reconstruct current state.

---

# 44. Late Join / Resync

Late join reconstructs:

```text
current authoritative door state
current Warden action phase
current telegraphed DoorId if telegraph still active
remaining telegraph state if presentation requires
current active Warden lock
remaining lock state if presentation requires
```

It must not:

- replay a completed telegraph as new;
- rerun candidate generation;
- apply a lock twice;
- restart telegraph/lock timer;
- rerun an old fail-safe;
- independently execute policy on proxy.

Authoritative exactly-once guards remain Host-private.

---

# 45. Observability

Read-only `WardenAIDebugSnapshot`:

```text
HasStateAuthority

FacilityGraphRevision
FacilityGraphDefinitionVersion
FacilityGraphValid
LastGraphValidationReason?

CurrentPhase
RouteObligationCount
RequiredOriginCount
RequiredDestinationCount
FinalHuntExitProtected

CurrentWardenActionId?
CurrentActionPhase?
CurrentDoorId?
CurrentAffectedPlayerRouteEdgeIds[]?
TelegraphRemaining?
LockRemaining?
CooldownRemaining?

AuthoredCheapEligibleDefinitionCount
PressureCandidateBound
ConfigurationValidationStatus
ConfigurationValidationReason?

CandidateCount
SafeCandidateCount
MeaningfulPressureCandidateCount
SelectedCandidateDoorId?
SelectedCandidateAffectedPlayerRouteEdgeIds[]?
SelectedCandidatePressure?
SelectionReason?

SafetyValidationStatus
SafetyRejectReason?
ValidationGraphRevision?
PreCommitGraphRevision?
PostApplyValidationStatus?

PreMeanShortestRouteCost?
PostMeanShortestRouteCost?
RoutePressure_v1.0?

ActiveWardenLockCount
RecentDoorHistory

ScenarioRouteModifierId?
DoorJammerActiveOnCurrentDoor?
EffectivePlayerTraversable?
EffectiveMonsterTraversable?

ObjectiveReachable
ExitReachable
LastFailedObligationId?
LastFailedOriginNodeId?

ApplyResolved?
ReleaseResolved?
LastActionResult?
LastFailSafeReason?
```

Debug is a projection only.

Debug UI cannot:

- force a candidate;
- toggle safety result;
- mutate graph;
- apply/release door.

---

# 46. Reason Codes

## 46.1 Reason ownership rule — WAR-DD-02

Each failure/decision condition has one canonical owner.

| Layer | Canonical type | Owns | Must not duplicate |
|---|---|---|---|
| graph definition / bake | `FacilityGraphValidationReason` | malformed/incompatible authored graph and route-lock footprint | configuration capacity, candidate eligibility, reachability outcome |
| Warden config/content compatibility | `WardenConfigurationValidationReason` | whether `PressureCandidateBound` can cover the full authored cheap-eligible route-lock set | graph schema, candidate eligibility, reachability |
| cheap candidate eligibility | `PressureCandidateRejectReason` | one otherwise well-formed canonical route-lock definition being ineligible before safety | graph/config validation failure, route reachability |
| runtime safety proof / precommit | `WardenSafetyRejectReason` | current-state route safety, stale revision, objective/exit reachability, commit occupancy | graph/config defects, cooldown |
| policy selection | `WardenSelectionReason` | why a safe/meaningful candidate was selected or why the complete current candidate set produced no action | graph/config/safety failure details |

Candidate bound is not a rejection/scheduling layer:

```text
candidate count exceeds validated bound
→ WardenConfigurationValidationReason
→ no partial candidate evaluation
→ no PressureCandidateRejectReason
→ no WardenSafetyRejectReason
```

If `FacilityGraphValidationReason != Valid`:

```text
Warden route-pressure evaluation does not start
→ no PressureCandidateRejectReason is fabricated
→ SafetyValidator is NOT_EVALUATED / not invoked as an authority proof
→ graph reason remains the canonical diagnostic
```

## 46.2 `FacilityGraphValidationReason`

Structural/authoring only:

```text
Valid
MissingDefinition
VersionMismatch
MissingMapId
DuplicateNodeId
DuplicateEdgeId
DuplicateDoorId
DanglingEdge
UnknownDoorReference
DoorBindingMismatch
InvalidTraversalCost
MissingRouteOrigin
MissingObjectiveBinding
DuplicateWardenRouteLockDoorId
InvalidWardenRouteLockFootprint
UnsupportedRouteClass
```

## 46.3 `WardenConfigurationValidationReason`

Configuration/content compatibility only:

```text
Valid
PressureCandidateBoundTooSmall
```

`PressureCandidateBoundTooSmall` means:

```text
PressureCandidateBound
<
AuthoredCheapEligibleWardenRouteLockDefinitionCount
```

or the defensive runtime equivalent:

```text
CurrentCheapEligibleCandidateCount
>
PressureCandidateBound
```

Result:

```text
no candidate truncation
no policy evaluation
no Warden action
development diagnostic
```

## 46.4 `PressureCandidateRejectReason`

Cheap candidate-eligibility only:

```text
None
NotWardenEligible
ActionUnsupported
AlreadyActive
Cooldown
NoPlayerRouteEffect
ProtectedByObjective
ScenarioConflict
```

Notes:

- `GraphInvalid` is intentionally absent; graph validity belongs to `FacilityGraphValidationReason`.
- candidate-bound overflow is not a candidate rejection; it is a `WardenConfigurationValidationReason` and aborts the logical evaluation without truncation.
- `NoPlayerRouteEffect` means no affected edge in the canonical footprint is currently player-traversable; it replaces edge-singular `DoorNotPlayerTraversable`.

## 46.5 `WardenSafetyRejectReason`

Runtime safety/precommit only:

```text
None
GraphRevisionChanged
ObjectiveUnknown
RequiredOriginMissing
RequiredDestinationMissing
ObjectiveUnreachable
ExitUnreachable
NoLegalRoute
DoorStateConflict
DoorwayOccupied
```

Notes:

- graph-definition/schema/version failures are not duplicated here;
- Warden eligibility/support is not duplicated here;
- cooldown/active-lock/candidate-bound outcomes are not duplicated here;
- every candidate safety proof applies the full canonical `AffectedPlayerRouteEdgeIds[]`.

## 46.6 `WardenSelectionReason`

```text
HighestPressureFreshDoor
HighestPressureAfterHistoryExhausted
StableTieBreak
NoSafeCandidate
NoMeaningfulPressure
```

Exact no-action semantics:

```text
NoSafeCandidate
=
no candidate in the complete current cheap-eligible candidate set passed SafetyValidator

NoMeaningfulPressure
=
at least one safe candidate exists in the complete current cheap-eligible candidate set
AND
none has MetricStatus = VALID with RoutePressure_v1.0 > 0
```

Neither value is a graph-validation or candidate-rejection reason.

## 46.7 `WardenActionCancelReason`

```text
SafetyPrecheckFailed
GraphChangedDuringTelegraph
ObjectiveChangedDuringTelegraph
DoorStateChangedDuringTelegraph
ScenarioChangedDuringTelegraph
PreCommitSafetyFailed
DoorwayOccupied
MatchEnded
ControllerDisabled
```

## 46.8 `WardenActionResult`

```text
Applied
ExpiredReleased
CancelledBeforeApply
FailSafeReleased
NoAction
```

## 46.9 `WardenFailSafeReason`

```text
PostApplyObjectiveUnreachable
PostApplyExitUnreachable
PostApplyNoLegalRoute
ActiveLockInvalidAfterObjectiveChange
ActiveLockInvalidAfterScenarioChange
GraphInvalidWhileApplied
ExternalUnsafeStateAfterWardenRelease
```

---

# 47. Quality Metrics

Common rule:

```text
one metric name + version
→ one exact unit
→ one numerator
→ one denominator
→ one source
→ one reset/window
```

Zero denominator:

```text
denominator == 0
→ MetricStatus = NOT_EVALUATED
→ value = null
```

No acceptance threshold is invented.

## 47.1 `ObjectiveReachabilityCheck_v1.0`

Per authoritative safety verification fact:

```text
true
=
all current RequiredRouteObligations pass the §21 quantifier

false
=
at least one required obligation/origin has no legal destination path
```

Unit: boolean fact.

This is not a rate.

## 47.2 `ObjectiveReachabilityRate_v1.0`

Purpose: measure actual Warden-controlled active-state safety, not candidate filtering quality.

Eligible checks:

```text
post-apply safety verification
+
active-lock revalidation after route/objective/phase/scenario changes
```

Formula:

```text
count(eligible checks where ObjectiveReachabilityCheck == true)
/
count(all eligible checks)
```

Candidate prechecks/rejections are excluded.

Window/reset: match or declared Warden evaluation window.

Source: authoritative WardenSafetyValidator trace.

## 47.3 `InvalidLockRate_v1.0`

An invalid lock episode is an **applied** Warden action that requires fail-safe release for one of the Warden-owned safety failure outcomes:

```text
PostApplyObjectiveUnreachable
PostApplyExitUnreachable
PostApplyNoLegalRoute
ActiveLockInvalidAfterObjectiveChange
ActiveLockInvalidAfterScenarioChange
GraphInvalidWhileApplied
```

These are fail-safe/action outcomes, not `WardenSafetyRejectReason` duplicates. `ExternalUnsafeStateAfterWardenRelease` is excluded because it describes an unsafe state that remains after the Warden-owned overlay has already been removed.

Formula:

```text
count(applied WardenActionIds with safety fail-safe release)
/
count(applied WardenActionIds)
```

Cancelled telegraphs and rejected candidates are excluded.

Window/reset: match or declared evaluation window.

Interpretation:

- lower is safer;
- candidate rejection is not an invalid applied lock.

## 47.4 `CandidateSafetyRejectRate_v1.0`

Separate diagnostic:

```text
count(candidate safety validations rejected)
/
count(candidate safety validations evaluated)
```

Unit: candidate validation.

Do not combine with `InvalidLockRate_v1.0`.

## 47.5 `RoutePressure_v1.0`

Exact per-candidate metric is defined in §26.

For research aggregation, persist:

- pre-route cost;
- simulated post-route cost;
- RoutePressure value/status;
- candidate selection outcome;
- applied/not-applied.

Higher is not automatically better.

---

# 48. Performance Contract

Freeze principles:

- topology authored/baked once, not rebuilt every frame;
- runtime mutations are edge overlays;
- graph revision changes are event-driven;
- policy reevaluates on meaningful lifecycle/event triggers;
- candidate count bounded by validated content/config relation; one logical policy evaluation evaluates the complete current cheap-eligible set;
- cheap eligibility before graph simulation;
- safety reachability uses bounded graph traversal;
- shortest path uses deterministic positive-cost graph algorithm;
- no all-simple-path enumeration;
- immutable topology reused across candidate simulations;
- graph overlay simulation avoids unnecessary full-object allocations where practical;
- one active Warden lock reduces concurrent complexity;
- no player-position polling;
- no door polling every frame;
- Profile/telemetry never polled by route policy;
- use Unity Profiler before freezing timing/ms budgets.

Numerical performance limits:

```text
TUNING TBD — profiler/playtest
```

---

# 49. EditMode / Pure Logic Tests

No test is claimed passed.

| Test ID | Requirement | Expected result |
|---|---|---|
| WAR-E-001 | valid FacilityGraphDefinition | Valid |
| WAR-E-002 | duplicate FacilityNodeId | validation fail |
| WAR-E-003 | duplicate FacilityEdgeId | validation fail |
| WAR-E-004 | duplicate DoorId | validation fail |
| WAR-E-005 | dangling edge | validation fail |
| WAR-E-006 | invalid traversal cost | validation fail |
| WAR-E-007 | bidirectional authored route | compiles/evaluates both directed arcs |
| WAR-E-008 | one-way route | reverse reachability not invented |
| WAR-E-009 | deterministic graph build | same definition → same graph |
| WAR-E-010 | graph version mismatch | Warden disabled/fail safe |
| WAR-E-011 | WardenEligible false | candidate rejected |
| WAR-E-012 | WardenEligible true | candidate may proceed, not automatically safe |
| WAR-E-013 | route obligation resolver Core context | mandatory bindings produced from objective data |
| WAR-E-014 | Power Puzzle resolver | PC/DP obligations according to bindings |
| WAR-E-015 | Security Hold resolver | Security Terminal obligation |
| WAR-E-016 | Final Hunt resolver | Exit obligation active |
| WAR-E-017 | all-origin reachability | every origin must reach any destination |
| WAR-E-018 | one origin stranded | safety reject |
| WAR-E-019 | current combined overlays | safety uses base+scenario+phase+Warden+player-route overlays |
| WAR-E-020 | Door Jammer only | does not close player route |
| WAR-E-021 | Warden + Jammer coexistence | independent traversal channels/timers |
| WAR-E-022 | scenario modifier conflict | candidate rejected |
| WAR-E-023 | safe candidate | safety Valid |
| WAR-E-024 | objective unreachable | reject |
| WAR-E-025 | Final Hunt exit unreachable | reject |
| WAR-E-026 | no legal route | reject |
| WAR-E-027 | candidate bound | bounded deterministic generation |
| WAR-E-028 | candidate stable order | deterministic |
| WAR-E-029 | RoutePressure no detour | 0 |
| WAR-E-030 | RoutePressure detour | exact formula |
| WAR-E-031 | RoutePressure degenerate pre-cost | NOT_EVALUATED rule |
| WAR-E-032 | pressure cost decrease | INVALID |
| WAR-E-033 | fresh-door policy | fresh candidate partition preferred |
| WAR-E-034 | pressure ranking | highest pressure in allowed partition |
| WAR-E-035 | tie-break | least recent then stable DoorId/EdgeId |
| WAR-E-036 | bounded recent history | deterministic eviction |
| WAR-E-037 | one-active-lock policy | second runtime candidate ineligible |
| WAR-E-038 | cumulative safety injection | validator rejects candidate unsafe with existing simulated Warden overlay |
| WAR-E-039 | telegraph binds door | identity immutable |
| WAR-E-040 | graph change during telegraph | exact candidate revalidated |
| WAR-E-041 | stale precheck | cannot authorize commit |
| WAR-E-042 | precommit failure | no apply |
| WAR-E-043 | doorway occupied | cancel, route unchanged |
| WAR-E-044 | apply guard duplicate | overlay written once |
| WAR-E-045 | release guard duplicate | overlay removed once |
| WAR-E-046 | post-apply fail | fail-safe triggered |
| WAR-E-047 | objective change active lock | revalidate; release if unsafe |
| WAR-E-048 | fail-safe one-lock policy | releases current Warden overlay |
| WAR-E-049 | external graph unsafe after Warden release | Warden disables new pressure |
| WAR-E-050 | cooldown after telegraph cancel | cooldown entered |
| WAR-E-051 | cooldown after normal release | cooldown entered |
| WAR-E-052 | routeModifier PRE_MATCH adaptive request | Warden never accepts direct request; M1-015 timing remains invalid |
| WAR-E-053 | routeModifier legal boundary update | graph revision + Warden revalidation |
| WAR-E-054 | ObjectiveReachabilityCheck | exact boolean |
| WAR-E-055 | ObjectiveReachabilityRate zero denominator | NOT_EVALUATED/null |
| WAR-E-056 | InvalidLockRate | applied-action denominator only |
| WAR-E-057 | CandidateSafetyRejectRate | candidate-validation denominator only |
| WAR-E-058 | reason code coverage | controlled branch mapping |
| WAR-E-059 | no hidden player input dependency | policy graph inputs contain no Transform |
| WAR-E-060 | exactly-once action trace | one apply/release outcome maximum |
| WAR-E-061 | DD-01 bidirectional canonical footprint | one DoorId definition contains both directed player-route edges; candidate contains same complete footprint |
| WAR-E-062 | DD-01 simulation/apply footprint equality | SafetyValidator simulation edge set == applied Warden overlay edge set == release edge set |
| WAR-E-063 | DD-01 single WardenEligible ownership | FacilityEdge/Door metadata cannot independently authorize selection; only WardenRouteLockDefinition owns eligibility/support |
| WAR-E-064 | DD-01 invalid route-lock footprint | empty/unknown/duplicate/wrong-Door affected edge causes FacilityGraph definition validation failure |
| WAR-E-065 | DD-02 graph reason ownership | invalid graph produces FacilityGraphValidationReason only; no fabricated candidate/safety reject reason |
| WAR-E-066 | DD-02 candidate-bound taxonomy | bound overflow is `WardenConfigurationValidationReason`, not PressureCandidateRejectReason/WardenSafetyRejectReason |
| WAR-E-067 | DD-03 zero pressure only | safe candidates all have VALID RoutePressure=0 → NoMeaningfulPressure; no telegraph/cooldown/history |
| WAR-E-068 | DD-03 positive + zero pressure | zero candidate excluded from policy selection; positive meaningful candidate remains selectable |
| WAR-E-069 | DD-03 non-valid pressure metric | NOT_EVALUATED/INVALID metric candidate is not policy-selectable and is not coerced to zero |
| WAR-E-070 | SG-01 full-set bound valid | `PressureCandidateBound >= AuthoredCheapEligibleWardenRouteLockDefinitionCount` → one logical evaluation admits the full current cheap-eligible set |
| WAR-E-071 | SG-01 bound too small | configured/authored count exceeds bound → configuration/content validation failure; no truncation, no policy, no Warden action |

---

# 50. PlayMode / Integration Tests

| Test ID | Scenario | Expected result |
|---|---|---|
| WAR-P-001 | Warden runs with authored graph | valid initialization |
| WAR-P-002 | FacilityGraph missing | no telegraph/lock; diagnostic |
| WAR-P-003 | graph version mismatch | no Warden action |
| WAR-P-004 | non-WardenEligible scene door | never selected |
| WAR-P-005 | designer-authorized candidate | may enter safety evaluation |
| WAR-P-006 | telegraph before lock | warning occurs before player traversal restriction |
| WAR-P-007 | telegraph Door A | only Door A/action may apply |
| WAR-P-008 | candidate invalidated during telegraph | cancel; no substitute |
| WAR-P-009 | objective changes during telegraph | immediate exact-candidate revalidation + mandatory precommit |
| WAR-P-010 | door changes during telegraph | revalidate |
| WAR-P-011 | scenario routeModifier changes at legal boundary | revalidate |
| WAR-P-012 | valid lock | required objective routes remain legal |
| WAR-P-013 | Final Hunt valid lock | Exit route remains legal |
| WAR-P-014 | attempted objective soft-lock | rejected |
| WAR-P-015 | attempted all-route removal | rejected |
| WAR-P-016 | one-active-lock cap | second Warden lock not started |
| WAR-P-017 | cumulative unsafe injected state | SafetyValidator rejects new candidate |
| WAR-P-018 | normal lock expiry | only Warden overlay removed |
| WAR-P-019 | overlay recompute | Door Jammer/scenario/base state retained |
| WAR-P-020 | active lock unsafe after objective change | fail-safe release |
| WAR-P-021 | post-apply unexpected invalid graph | fail-safe release |
| WAR-P-022 | graph still unsafe after release | Warden disables pressure; critical diagnostic |
| WAR-P-023 | Door Jammer active | monster channel blocked; player route unchanged by Jammer |
| WAR-P-024 | Door Jammer + Warden same door | independent overlays |
| WAR-P-025 | Jammer expires during Warden lock | Warden player-route lock remains |
| WAR-P-026 | Warden lock expires during Jammer | Jammer remains |
| WAR-P-027 | occupied doorway at commit | cancel; no physical crush/trap |
| WAR-P-028 | no valid candidates | safe no-action |
| WAR-P-029 | repeated same door | history/cooldown prevents immediate oscillation |
| WAR-P-030 | Main Route candidate | not privileged simply because Main |
| WAR-P-031 | Alternative Route candidate | legal if WardenEligible + safe |
| WAR-P-032 | one-way graph | safety respects direction |
| WAR-P-033 | Final Hunt rescue-relevant origin anchor | Exit remains reachable |
| WAR-P-034 | telemetry unavailable | Warden route behavior unchanged |
| WAR-P-035 | debug unavailable | Warden gameplay unchanged |
| WAR-P-036 | no physical Warden GameObject | route controller still functions if presentation design permits |
| WAR-P-037 | DD-01 bidirectional door lock | warned DoorId applies the complete canonical multi-edge footprint; no unsimulated reverse arc remains |
| WAR-P-038 | DD-03 zero-pressure-only safe set | Host produces NoMeaningfulPressure with no telegraph/lock/cooldown consumption |
| WAR-P-039 | SG-01 bound mismatch | authored/current cheap-eligible count exceeds bound → no telegraph/lock; diagnostic; no partial first-N evaluation |
| WAR-P-040 | DD-02 graph-definition failure | Warden stops before candidate/safety policy and exposes canonical FacilityGraphValidationReason |

---

# 51. Fusion Multiplayer Tests

| Test ID | Scenario | Expected result |
|---|---|---|
| WAR-N-001 | proxy attempts Warden lock | no authoritative effect |
| WAR-N-002 | only State Authority policy | one authoritative Warden decision source |
| WAR-N-003 | telegraph state | converges to clients |
| WAR-N-004 | lock/door presentation | converges |
| WAR-N-005 | duplicate apply callback/resimulation | one overlay apply |
| WAR-N-006 | duplicate release callback | one overlay release |
| WAR-N-007 | 2-player session | Warden action state converges |
| WAR-N-008 | 3-player session | converges |
| WAR-N-009 | 4-player session | converges |
| WAR-N-010 | late join during telegraph | current telegraph reconstructs; no restart |
| WAR-N-011 | late join during active lock | current lock reconstructs; no second apply |
| WAR-N-012 | reconnect | no old candidate/policy replay |
| WAR-N-013 | fail-safe reopen | converges |
| WAR-N-014 | proxy FacilityGraph mutation attempt | no authoritative effect |
| WAR-N-015 | RPC replay/presentation duplication | durable state prevents second action |
| WAR-N-016 | routeModifier boundary sync | Host-applied valid config reflected; proxy does not apply independently |
| WAR-N-017 | DD-01 canonical multi-edge action replication | one authoritative WardenActionId/DoorId represents one full footprint; proxy does not independently apply per-edge actions |

---

# 52. Edge-Case Matrix

| Edge case | Expected behavior | Owner | Reason/result | Test |
|---|---|---|---|---|
| no WardenEligible route-lock definitions | NO_ACTION | Candidate Generator/Policy | `NoSafeCandidate` for the complete current candidate set | WAR-P-028 |
| FacilityGraph missing | route pressure disabled | Root/Validator | `MissingDefinition` | WAR-P-002 |
| graph version mismatch | no action | Graph Validator | `VersionMismatch` | WAR-P-003 |
| malformed/unknown DoorId binding in graph definition | disable Warden evaluation | Graph Validator | canonical `FacilityGraphValidationReason` | WAR-E-004/064 |
| objective unknown | reject/no action | Resolver/Safety | `ObjectiveUnknown` | resolver negative |
| objective changes during evaluation | graph/revision snapshot invalidated; reevaluate | Controller | `GraphRevisionChanged` | WAR-E-047 |
| objective changes during telegraph | revalidate exact warned candidate; cancel if unsafe | Telegraph/Safety | action cancel reason | WAR-P-009 |
| objective changes during active lock | active safety check; fail-safe if unsafe | Safety/FailSafe | active invalid reason | WAR-P-020 |
| Exit becomes required | Final Hunt Exit obligation added | Resolver | valid obligations | WAR-E-016 |
| door already Warden locked | not candidate | Eligibility | `AlreadyActive` | WAR-E-037 |
| door closed by base gameplay | not player-traversable; reject candidate | Door adapter | `DoorStateConflict` | door integration |
| Door Jammer active | independent monster overlay | Jammer adapter | coexist | WAR-P-023 |
| Door Jammer activates during telegraph | Warden player-route safety unchanged unless physical/base channel changes | adapters | graph may update monster channel only | integration |
| Door Jammer expires during Warden action | Warden overlay remains | adapters | independent expiry | WAR-P-025 |
| routeModifier changes at legal boundary | recompute graph + revalidate | Scenario adapter | current revision | WAR-P-011 |
| candidate safe alone but unsafe with active overlays | reject | Safety | route failure | WAR-E-019 |
| simulated second Warden lock unsafe | reject in validator; runtime also max-one | Safety/Eligibility | cumulative safety | WAR-E-038/P-017 |
| all candidates unsafe | NO_ACTION | Policy | `NoSafeCandidate` | WAR-P-028 |
| equal pressure candidates | recent-use/stable ID tie-break | Policy | deterministic | WAR-E-035 |
| same door repeatedly selected | fresh partition + cooldown/history | Policy | anti-repeat | WAR-P-029 |
| telegraph candidate becomes invalid | cancel; no replacement | Telegraph | cancel reason | WAR-P-008 |
| doorway occupied | cancel; no delayed hidden close | Precommit | `DoorwayOccupied` | WAR-P-027 |
| precommit graph revision mismatch | rebuild current snapshot + validate; old result unusable | Safety | `GraphRevisionChanged` | WAR-E-041 |
| apply succeeds but post-check fails | fail-safe release | FailSafe | post-apply reason | WAR-P-021 |
| lock expiry | release once, recompute overlays | ActionController | `ExpiredReleased` | WAR-P-018 |
| Warden release still leaves unsafe graph | disable new pressure; do not mutate external systems | FailSafe | `ExternalUnsafeStateAfterWardenRelease` | WAR-P-022 |
| host migration/disconnect | follow supported Host Mode session behavior; do not invent Warden host migration semantics | Fusion integration | IMPLEMENTATION BINDING TBD | network integration |
| late join during telegraph | reconstruct current durable telegraph | Network binding | no replay | WAR-N-010 |
| late join during lock | reconstruct current lock | Network binding | no reapply | WAR-N-011 |
| debug unavailable | gameplay unaffected | Debug | no-op | WAR-P-035 |
| telemetry unavailable | route policy unaffected | Telemetry | one-way boundary | WAR-P-034 |
| bidirectional DoorId has two player-route arcs | candidate/safety/apply/release use the same two-edge canonical footprint | RouteLockDefinition/Safety/Action | no partial-direction proof | WAR-E-061/062, WAR-P-037 |
| safe candidates all RoutePressure = 0 | NO_ACTION; no telegraph/cooldown/history | Policy | `NoMeaningfulPressure` | WAR-E-067, WAR-P-038 |
| configured bound smaller than authored cheap-eligible set | configuration/content validation failure; no truncation/no Warden action | Configuration Validator | `PressureCandidateBoundTooSmall` | WAR-E-071, WAR-P-039 |
| hidden player directly queried by policy | forbidden dependency/test failure | Policy | contract violation | WAR-E-059 |

---

# 53. Class / Component Contracts

## 53.1 `FacilityGraphDefinition`

| Item | Contract |
|---|---|
| Purpose | immutable authored/baked route topology |
| Owns | node/edge/door definitions, versions |
| Inputs | map/facility authored metadata |
| Outputs | validated graph definition |
| Forbidden | runtime lock/timer state |
| Tests | WAR-E-001..010 |

## 53.2 `FacilityGraphBuilder / Validator`

| Item | Contract |
|---|---|
| Purpose | deterministically validate/build runtime topology |
| Inputs | FacilityGraphDefinition + map compatibility identity |
| Outputs | valid FacilityGraph topology or typed failure |
| Forbidden | repair unknown IDs silently |
| Failure | disable Warden route pressure |
| Tests | validation suite |

## 53.3 `FacilityGraph`

| Item | Contract |
|---|---|
| Purpose | authoritative current route topology view |
| Owns | immutable topology reference + effective edge-state overlay + revision |
| Inputs | door/scenario/objective/Jammer/Warden events |
| Outputs | directed traversal queries/snapshot |
| Forbidden | Warden policy/history |
| Tests | overlay/direction/revision |

## 53.4 `FacilityRuntimeEdgeState`

| Item | Contract |
|---|---|
| Purpose | effective player/monster traversal state |
| Owns | resolved channel state only |
| Inputs | composed overlays |
| Forbidden | source-system timers |
| Tests | Door Jammer/Warden/scenario composition |

## 53.5 `DoorDefinition`

| Item | Contract |
|---|---|
| Purpose | stable physical/gameplay door identity and general edge binding |
| Owns | DoorId, associated directed edge bindings, non-Warden door capabilities such as Door Jammer support where authored |
| Forbidden | Warden eligibility/support ownership; current Warden lock state |
| Status | MAP AUTHORING / STATIC CONFIG |

## 53.5A `WardenRouteLockDefinition`

| Item | Contract |
|---|---|
| Purpose | single canonical designer-authorized Warden route-lock definition for one DoorId |
| Owns | `WardenEligible`, `SupportsWardenRouteLock`, complete immutable `AffectedPlayerRouteEdgeIds[]` |
| Inputs | validated FacilityGraph DoorId/edge identities |
| Outputs | canonical candidate/simulation/apply/release footprint |
| Forbidden | runtime lock/timer state; duplicate eligibility on Door/Edge definitions |
| Failure | malformed footprint invalidates FacilityGraph definition for Warden pressure |
| Tests | WAR-E-061..064; WAR-P-037 |

## 53.6 `DoorStateAdapter`

| Item | Contract |
|---|---|
| Purpose | translate authoritative door gameplay state to graph overlay |
| Inputs | base physical/interaction door state |
| Outputs | route-affecting change event |
| Forbidden | Warden policy |
| Tests | open/closed/change |

## 53.7 `DoorJammerIntegrationAdapter`

| Item | Contract |
|---|---|
| Purpose | map Team Tool jammer state to monster traversal channel |
| Inputs | authoritative Jammer activation/expiry |
| Outputs | Jammer overlay |
| Forbidden | Warden lock cancellation / player-route closure |
| Tests | WAR-E-020/021; WAR-P-023..026 |

## 53.8 `RequiredRouteTargetResolver`

| Item | Contract |
|---|---|
| Purpose | produce explicit current route obligations |
| Inputs | objective/phase state + authored graph bindings |
| Outputs | immutable RequiredRouteObligation[] |
| Forbidden | hidden player Transform |
| Failure | ObjectiveUnknown / missing binding → no Warden action |
| Tests | WAR-E-013..018 |

## 53.9 `CurrentRouteModelBuilder`

| Item | Contract |
|---|---|
| Purpose | one immutable evaluation snapshot |
| Inputs | graph revision, obligations, scenario, Warden context |
| Outputs | CurrentRouteModel |
| Forbidden | persistent duplicate door authority |
| Tests | snapshot/revision |

## 53.9A `WardenConfigurationValidator`

| Item | Contract |
|---|---|
| Purpose | validate Warden config/content compatibility before policy evaluation |
| Inputs | `PressureCandidateBound`, validated canonical `WardenRouteLockDefinition` content |
| Outputs | `WardenConfigurationValidationReason` |
| Rule | bound must cover the complete authored cheap-eligible route-lock definition count |
| Failure | no partial candidate generation; no Warden action; development diagnostic |
| Tests | WAR-E-066/070/071; WAR-P-039 |

## 53.10 `WardenPressureCandidateGenerator`

| Item | Contract |
|---|---|
| Purpose | bounded full-set candidate generation over canonical route-lock definitions |
| Inputs | CurrentRouteModel/config + validated Warden route-lock content |
| Outputs | complete current cheap-eligible `PressureCandidate[]`, each carrying DoorId + full `AffectedPlayerRouteEdgeIds[]` |
| Forbidden | hidden player data, arbitrary per-edge Warden actions, first-N/partial-set truncation |
| Bound rule | `PressureCandidateBound >= AuthoredCheapEligibleWardenRouteLockDefinitionCount`; mismatch aborts evaluation as configuration/content failure |
| Tests | WAR-E-011/012/027/028/061/066/070/071 |

## 53.11 `WardenRoutePressureEvaluator`

| Item | Contract |
|---|---|
| Purpose | calculate shortest-route detour metric |
| Inputs | safe pre/post graph snapshots |
| Outputs | RoutePressure_v1.0 + route-cost diagnostics |
| Forbidden | fairness override |
| Tests | WAR-E-029..032 |

## 53.12 `WardenSafetyValidator`

| Item | Contract |
|---|---|
| Purpose | prove required route obligations remain reachable |
| Inputs | current combined state + candidate's complete canonical `AffectedPlayerRouteEdgeIds[]` |
| Outputs | typed WardenSafetyValidationResult |
| Forbidden | door mutation/policy override |
| Tests | WAR-E-017..026/038/041 |

## 53.13 `WardenPolicy`

| Item | Contract |
|---|---|
| Purpose | deterministic selection among safe candidates |
| Inputs | safe candidates, RoutePressure metric status/value, recent history, cooldown |
| Outputs | selected positive-meaningful-pressure candidate + reason or NO_ACTION |
| Forbidden | SafetyValidator bypass, hidden player data |
| Tests | WAR-E-033..035 |

## 53.14 `WardenPressureContext`

| Item | Contract |
|---|---|
| Purpose | Warden-owned lifecycle/history/cooldown state |
| Owns | one CurrentAction, bounded history, cooldown |
| Forbidden | canonical door availability |
| Tests | lifecycle/history |

## 53.15 `WardenTelegraphController`

| Item | Contract |
|---|---|
| Purpose | fair warning bound to exact WardenActionId/DoorId |
| Inputs | selected safe action |
| Outputs | durable telegraph state |
| Forbidden | substitute door silently |
| Tests | WAR-E-039/040; WAR-P-006..010 |

## 53.16 `WardenRouteActionController`

| Item | Contract |
|---|---|
| Purpose | apply/release exact Warden overlay at most once |
| Owns | ApplyResolved/ReleaseResolved guard |
| Inputs | valid precommit action |
| Outputs | overlay change + action result |
| Forbidden | overwrite base/Jammer/scenario state |
| Tests | WAR-E-044/045/060 |

## 53.17 `WardenFailSafeController`

| Item | Contract |
|---|---|
| Purpose | remove unsafe Warden-owned route pressure |
| Inputs | post-apply/active validation failure |
| Outputs | deterministic Warden release + critical diagnostic |
| Forbidden | teleport/objective/scenario/Jammer repair |
| Tests | WAR-E-046..049; WAR-P-020..022 |

## 53.18 `WardenDebugProvider`

| Item | Contract |
|---|---|
| Purpose | read-only WardenAIDebugSnapshot |
| Forbidden | gameplay mutation |
| Tests | snapshot truth/read-only |

## 53.19 `WardenTelemetryAdapter`

| Item | Contract |
|---|---|
| Purpose | translate authoritative Warden facts only after schema approval |
| Current production | no Warden route event invented |
| Forbidden | telemetry→policy |
| Tests | no unsupported event/no feedback |

## 53.20 `WardenNetworkBinding`

| Item | Contract |
|---|---|
| Purpose | State Authority gate + durable presentation synchronization |
| Recommended base | NetworkBehaviour on NetworkObject boundary |
| Inputs | Fusion simulation + controller state |
| Outputs | client-required telegraph/lock/door presentation |
| Forbidden | proxy policy/safety/apply |
| Tests | WAR-N suite |

---

# 54. Current-to-Target Mapping

| Current class/module | Current responsibility | KEEP/MODIFY/SPLIT/ADD | Target responsibility | Risk |
|---|---|---|---|---|
| Warden runtime code | NOT EVIDENCED | ADD | route-pressure controller | High |
| FacilityGraph source | NOT EVIDENCED | ADD | authored route topology/runtime view | High |
| Door route metadata | visual/map evidence only | ADD/BIND | stable DoorId/edge definitions | High |
| Objective graph binding | NOT EVIDENCED | ADD | RequiredRouteTargetResolver inputs | High |
| SafetyValidator | NOT EVIDENCED | ADD | hard fairness gate | High |
| Warden Policy | NOT EVIDENCED | ADD | deterministic safe selection | Medium |
| telegraph runtime | gameplay requirement, code not evidenced | ADD | exact action warning lifecycle | High |
| Warden lock overlay | gameplay requirement, code not evidenced | ADD | compositional player-route restriction | High |
| Door Jammer | gameplay contract exists; integration code not evidenced here | BIND | independent monster-traversal overlay | Medium |
| Fusion Warden binding | NOT EVIDENCED | ADD | Host authority + durable state | High |
| Warden telemetry | no active route event contract | DEFER production / ADD diagnostics | development evidence until schema revision | Low/Medium |

No current class is renamed or claimed to exist.

---

# 55. Shared-Code Reuse

Good reusable patterns:

- stable ID conventions;
- typed validation/result objects;
- immutable decision snapshots;
- exactly-once action guards;
- read-only debug snapshot pattern;
- Fusion State Authority boundary adapter;
- telemetry one-way adapter;
- test utilities;
- deterministic metric evaluators.

Do not reuse blindly:

```text
StalkerMemory
Stalker RegionGraph
SpatialGraph
CoverageMemory
GlobalPatrolPlanner
LocalPatrolSelector
StalkerSearchPlanner
Detection Meter

ListenerMemory
NoiseSystem
HearingSensor
PendingHearingInbox
InvestigationEpisode
ListenerNoiseSelector
```

Navigation utilities are not needed unless a future approved physical Warden creates a real consumer.

Prefer composition.

---

# 56. Configuration Matrix

| Field | Owner | Purpose | Source | Runtime mutable? | AED mutable? | Status |
|---|---|---|---|---:|---:|---|
| MapId/MapVersion | Map content | compatibility | map | No | No | PROJECT BASELINE |
| FacilityGraphDefinitionVersion | FacilityGraph asset | compatibility/reproducibility | graph content | No | No | STATIC DESIGN CONFIG |
| FacilityNode/Edge definitions | map authoring | route topology | map | No | No | MAP AUTHORING TBD |
| WardenRouteLockDefinitions (DoorId + WardenEligible + support + complete affected-edge footprint) | map authoring | canonical legal Warden route-lock actions | Map Flow concept + WAR-DD-01 | No | No | MAP AUTHORING TBD |
| BaseTraversalCost | graph content | shortest-route cost | authored/baked | No | No | MAP AUTHORING TBD |
| RouteClass | graph content | diagnostic/topology metadata | Map Flow | No | No | MAP AUTHORING TBD |
| RequiredRouteOrigin sets | objective/map authoring | fairness quantifier | design | per phase resolution | No | MAP AUTHORING TBD |
| objective→destination bindings | objective/map authoring | route obligations | gameplay flow | per phase resolution | No | MAP AUTHORING TBD |
| MaxActiveWardenLocks | Warden design | concurrency bound | this design | No | No | **DETAILED-DESIGN DECISION = 1** |
| WardenLockDuration | Warden config | temporary lock lifetime | GDD temporary | config | No | TUNING TBD |
| WardenTelegraphDuration | Warden config | warning window | GDD telegraph | config | No | TUNING TBD |
| WardenCooldownDuration | Warden config | anti-spam | this design | config | No | TUNING TBD |
| PressureCandidateBound | Warden config | maximum supported full candidate-set size per logical policy evaluation; must cover authored cheap-eligible route-lock definitions | performance/content capacity | config | No | TUNING TBD with frozen lower-bound relation |
| RecentActionHistoryCapacity | Warden config | anti-repeat | this design | config | No | TUNING TBD |
| RoutePressure formula | evaluator | pressure semantics | this design | No | No | PROJECT/DETAILED-DESIGN BASELINE |
| Door Jammer duration | Team Tool | monster traversal block | GDD | Host timer | No | PROJECT BASELINE = 1 minute |
| Door Jammer door eligibility | map/tool authoring | tool target set | GDD | No | No | MAP AUTHORING TBD |
| Door occupancy query binding | door adapter | safe physical commit | this design | runtime | No | IMPLEMENTATION BINDING TBD |
| `ScenarioConfig.routeModifier` | ScenarioConfig | scenario route context | M1-015 | only validated boundary apply | **YES only at ALLOWED_PHASE_BOUNDARY** | ADAPTIVE-AUTHORIZED |
| Current Warden DoorId | Warden Policy | runtime action | this design | Host runtime | **No** | runtime state |
| WardenActionId representation | action controller | exactly once | this design | Host runtime | No | IMPLEMENTATION BINDING TBD |
| Fusion durable fields | network binding | client presentation | network | Host network state | No | IMPLEMENTATION BINDING TBD |
| performance budgets | project | profiling | none yet | n/a | No | TUNING TBD |
| metric acceptance thresholds | research | evaluation | none yet | n/a | No | TUNING TBD |

---

# 57. Implementation Plan

```text
Step 1
Reconcile map/door/objective source + verify no Warden source exists

Step 2
Freeze stable Facility IDs + DoorDefinition + canonical WardenRouteLockDefinition footprint authoring contract

Step 3
Implement FacilityGraphDefinition + deterministic validation/bake

Step 4
Implement runtime directed FacilityGraph edge-state overlay

Step 5
Implement RequiredRouteTargetResolver + authored fairness origins

Step 6
Implement WardenSafetyValidator + exhaustive pure graph tests

Step 7
Implement MeanShortestRouteCost + RoutePressure_v1.0 evaluator

Step 8
Implement WardenConfigurationValidator + full-set bounded PressureCandidateGenerator + canonical footprint candidates + deterministic positive-pressure WardenPolicy

Step 9
Implement WardenPressureContext + one-lock policy + recent history + cooldown

Step 10
Implement Telegraph lifecycle bound to exact action/door

Step 11
Implement event-driven telegraph revalidation + mandatory precommit validation

Step 12
Implement exactly-once WardenRouteActionController

Step 13
Implement post-apply validation + fail-safe release

Step 14
Integrate Door Jammer/base/scenario/objective door-state adapters

Step 15
Implement objective/phase/routeModifier revalidation hooks

Step 16
Implement observability + reason codes + deterministic metric traces

Step 17
Implement Photon Fusion Host/State Authority network binding

Step 18
Run EditMode/PlayMode/2–4 player tests

Step 19
Profile graph/door/network work

Step 20
Tune telegraph/lock/cooldown/history/candidate bounds and author final map bindings
```

The order places safety graph logic before visible lock mechanics so a door cannot be implemented first and safety added later as an afterthought.

---

# 58. Warden Hard Invariants

1. Warden is primarily a Spatial Pressure Controller; it is not a Stalker/Listener clone.
2. FacilityGraph is not Stalker RegionGraph.
3. FacilityGraph does not use NavMesh triangle topology as canonical route identity.
4. FacilityGraph runtime traversal is based on explicit directed arcs.
5. `WardenRouteLockDefinition` is the canonical designer-authored Warden route-action definition.
6. `WardenEligible` and `SupportsWardenRouteLock` are owned only by `WardenRouteLockDefinition`.
7. `FacilityEdgeDefinition` and `DoorDefinition` cannot independently authorize Warden selection.
8. Every Warden route-lock definition owns one complete immutable `AffectedPlayerRouteEdgeIds[]` footprint.
9. Candidate simulation footprint equals precommit footprint equals applied overlay footprint equals release footprint.
10. A bidirectional DoorId cannot be safety-simulated as one arc and then applied as both arcs.
11. WardenEligible means eligible for consideration, not safe.
12. Every candidate must pass WardenSafetyValidator.
13. SafetyValidator uses the current combined authoritative player-route state.
14. Every active RequiredRouteObligation must remain reachable under the §21 quantifier.
15. Every authored fairness origin in an active obligation retains at least one path to an allowed destination.
16. Warden does not need or read exact hidden Player Transform for route targeting.
17. Main Route is not mandatory and is not automatically Warden's preferred target.
18. Graph-definition failures use only `FacilityGraphValidationReason`; they are not duplicated as candidate or safety reject reasons.
19. Cheap candidate ineligibility uses only `PressureCandidateRejectReason`.
20. Runtime route-safety/precommit rejection uses only `WardenSafetyRejectReason`.
21. `PressureCandidateBound` must cover the complete authored cheap-eligible Warden route-lock definition set.
22. Bound overflow is a configuration/content validation failure; Warden must never silently truncate or evaluate only a partial first-N candidate set.
23. A safe candidate is policy-selectable only when `MetricStatus = VALID` and `RoutePressure_v1.0 > 0`.
24. A valid zero-pressure candidate is not selected/applied in v1.0.
25. Safe candidates with no positive meaningful pressure produce `NoMeaningfulPressure`, no telegraph, no lock, no cooldown, and no history entry.
26. Candidate safety is revalidated after relevant graph changes during telegraph.
27. Candidate safety is always revalidated immediately before apply.
28. A stale pre-telegraph validation result cannot authorize apply.
29. Telegraph binds to the exact WardenActionId/DoorId/canonical footprint being warned.
30. A cancelled/invalid telegraph cannot silently substitute another door or footprint.
31. Doorway unsafe at physical commit cancels the action; it does not later close without a new telegraph.
32. One WardenActionId applies at most once.
33. One WardenActionId releases/expires at most once.
34. Apply/release duplication cannot restart a timer or duplicate outcome.
35. Applied route state is revalidated after the actual graph update.
36. Unexpected post-apply safety failure triggers deterministic fail-safe release.
37. v1.0 supports at most one active Warden pressure lock.
38. Safety logic nevertheless evaluates all current combined overlays and cannot assume pristine topology.
39. Warden fail-safe releases Warden-owned state only.
40. Fail-safe does not teleport players or monsters.
41. Fail-safe does not invent graph edges or change the objective.
42. Door/route ownership is compositional; Warden must not overwrite base, scenario, objective, or Door Jammer state.
43. Door Jammer and Warden route lock are independent overlays.
44. Door Jammer is not automatically player-route closure.
45. Door Jammer does not automatically cancel Warden route pressure.
46. Warden lock duration is not copied from Door Jammer duration.
47. Objective/phase changes revalidate an active Warden action immediately.
48. Final Hunt protects Exit as a mandatory route obligation.
49. AED does not directly choose a current Warden DoorId/action.
50. `ScenarioConfig.routeModifier` obeys M1-015 whitelist/timing/safety rules.
51. Adaptive routeModifier is invalid at PRE_MATCH.
52. Telemetry does not command Warden.
53. Profile/MatchScore/GenAI do not select current Warden route actions.
54. Host / Fusion State Authority owns Warden policy and route mutation.
55. Proxy clients do not independently run Warden policy or SafetyValidator as authority.
56. RPC is not the sole durable state for active telegraph/lock presentation.
57. Late join does not replay historical Warden actions.
58. RoutePressure_v1.0 has one exact formula and one frozen policy-consumption rule.
59. Higher RoutePressure is not automatically better or safer.
60. Candidate rejection is not counted as an invalid applied lock.
61. Research metrics use explicit version/unit/denominator/reset semantics.
62. Debug UI is read-only.
63. No Warden combat FSM is introduced without approved gameplay evidence.

---

# 59. Definition of Done

| # | Question | v1.0 answer |
|---:|---|---|
| 1 | What exactly is FacilityGraph? | authored directed gameplay-route topology + authoritative traversal overlay |
| 2 | Why is it not RegionGraph? | different route/safety semantics and resolution; shares IDs only where physical identity overlaps |
| 3 | What IDs does it use? | §13 |
| 4 | Who authors Warden eligibility? | map/facility designer content through canonical `WardenRouteLockDefinition`; not FacilityEdge/Door duplicates |
| 5 | How are definitions validated? | deterministic builder/validator §15 |
| 6 | Directed vs undirected semantics? | runtime directed; bidirectional connection = two arcs; one Warden door action carries the complete authored affected-edge footprint |
| 7 | What runtime events change edge state? | §17.3 |
| 8 | What is CurrentRouteModel? | immutable evaluation snapshot §19 |
| 9 | What may Warden legally know? | §12 |
| 10 | Does Warden use hidden Player Transform? | No |
| 11 | What is current required route target? | remaining Core(s)+Power Hub during collection/delivery; PC+DP during puzzle; Security Terminal during Hold; Exit during Final Hunt, represented as separate obligations |
| 12 | From where is reachability evaluated? | designer-authored fairness origin nodes |
| 13 | Fairness per player/team/authored source? | authored source set; all origins protected |
| 14 | Traversal rules? | current directed effective player traversal |
| 15 | Candidate eligibility? | canonical WardenRouteLockDefinition unit, §23 |
| 16 | What makes candidate safe? | every active obligation passes §21 |
| 17 | How current locks included? | current combined overlay in safety snapshot |
| 18 | Candidate ranking? | full current cheap-eligible set → safe → positive meaningful pressure only → fresh partition → RoutePressure → last-used → stable DoorId |
| 19 | RoutePressure_v1.0? | normalized mean-shortest-route detour §26 |
| 20 | Why not max pressure always? | only safe positive-pressure candidates are selectable; fresh-door anti-repeat partition + hard safety/cooldown remain |
| 21 | Repeat oscillation prevention? | one lock + cooldown + bounded recent history |
| 22 | WardenPressureContext? | one Warden-owned action/history/cooldown state §29 |
| 23 | Route-action lifecycle? | §30 |
| 24 | Telegraph? | exact warned action before commit §31 |
| 25 | Change warned door after telegraph? | No |
| 26 | Graph/objective changes during telegraph? | revalidate exact candidate; cancel if invalid |
| 27 | When SafetyValidator reruns? | precheck, relevant telegraph changes, mandatory precommit, post-apply, active route changes |
| 28 | Action exactly once? | WardenActionId apply/release guards |
| 29 | Post-apply unsafe? | immediate fail-safe release |
| 30 | Fail-safe reopen? | remove current Warden overlay, revalidate, disable pressure if external state still unsafe |
| 31 | Active Warden locks? | maximum one |
| 32 | Lock expiry? | exactly-once release; other overlays retained |
| 33 | Cooldown/budget owner? | WardenPressureContext; cooldown-only v1.0 |
| 34 | Door Jammer interaction? | independent monster-traversal overlay §18 |
| 35 | Door Jammer player route effect? | none by itself |
| 36 | Overlay composition? | §17 |
| 37 | Objective changes during lock? | revalidate; fail-safe if unsafe |
| 38 | Final Hunt? | Exit protected; no special power |
| 39 | Warden physically moves/chases/attacks? | not evidenced; out of scope |
| 40 | Source proving physical behavior? | none sufficient in supplied approved sources |
| 41 | Host owns? | §42 |
| 42 | Client replicates? | telegraph/lock/door presentation only as needed |
| 43 | Late join? | durable current state, no replay |
| 44 | Client forge lock? | No |
| 45 | routeModifier vs runtime action? | scenario-level validated context vs Host Warden temporary action |
| 46 | AED routeModifier timing? | ALLOWED_PHASE_BOUNDARY only |
| 47 | AED choose DoorId? | No |
| 48 | ObjectiveReachability? | §47.1/47.2 |
| 49 | InvalidLockRate? | fail-safe released applied actions / applied actions |
| 50 | RoutePressure? | §26 |
| 51 | Test proving no soft-lock? | WAR-E-024..026 + WAR-P-012..015 |
| 52 | Test cumulative state safety? | WAR-E-019/038 + WAR-P-017 |
| 53 | Telegraph TOCTOU test? | WAR-E-040/041 + WAR-P-008..011 |
| 54 | Fail-safe test? | WAR-E-046..049 + WAR-P-020..022 |
| 55 | Canonical route-action footprint? | one `WardenRouteLockDefinition` DoorId + full `AffectedPlayerRouteEdgeIds[]`; simulation/apply/release must match exactly |
| 56 | Reason taxonomy ownership? | graph definition → FacilityGraphValidationReason; config/content capacity → WardenConfigurationValidationReason; eligibility → PressureCandidateRejectReason; runtime safety → WardenSafetyRejectReason |
| 57 | Zero-pressure policy? | safe `RoutePressure_v1.0 = 0` is not selectable; zero-only safe set → `NoMeaningfulPressure`, no action |
| 58 | Candidate-bound safeguard? | bound must cover full authored cheap-eligible set; overflow → configuration/content validation failure, no truncation/no action |
| 59 | Implementation order? | §57 |

All P0 behavior/authority/safety questions required by this document are resolved.

---

# 60. Open Tuning / Bindings

## 60.1 TUNING TBD

- Warden lock duration;
- telegraph duration;
- cooldown;
- `PressureCandidateBound` numeric value, subject to the frozen full-set lower-bound relation;
- RecentWardenActionHistory capacity;
- optional policy evaluation debounce;
- profiler budgets;
- quality/metric acceptance thresholds.

No pressure weighting is needed by RoutePressure_v1.0.

## 60.2 MAP AUTHORING TBD

- exact `WardenRouteLockDefinition` entries corresponding to authored map markers, including complete `AffectedPlayerRouteEdgeIds[]`;
- exact FacilityNode/FacilityEdge mapping;
- final Main/Alternative/Connector annotations;
- RequiredRouteOrigin sets per objective context;
- objective/FacilityNode bindings;
- exact BaseTraversalCost bake/authoring;
- exact physical door bindings;
- Door Jammer eligible DoorIds.

## 60.3 IMPLEMENTATION BINDING TBD

- exact ScriptableObject/scene metadata representation;
- graph serialization;
- FacilityGraph revision integer/type;
- door-state adapter implementation;
- doorway occupancy query/collider mask;
- WardenActionId representation;
- exactly-once guard representation;
- authoritative timer/end-tick representation;
- Fusion `[Networked]` properties;
- exact RPC/input calls;
- NetworkObject placement;
- Warden presentation avatar, if any.

These TBDs may not weaken:

- canonical WardenRouteLockDefinition eligibility/footprint requirement;
- reachability quantifier;
- combined-state SafetyValidator;
- telegraph-before-lock;
- precommit/post-apply validation;
- fail-safe release;
- compositional door ownership;
- Host authority;
- routeModifier boundary;
- deterministic RoutePressure semantics.

---

# 61. Architecture Escalations

```text
ARCHITECTURE ESCALATION REQUIRED: NO
```

No supplied evidence invalidates:

```text
Warden = Spatial Pressure Controller
FacilityGraph != RegionGraph
designer-authorized canonical Warden route-lock definitions
mandatory route safety
telegraph-before-lock
SafetyValidator before apply
Host authority
bounded ScenarioConfig/AED authority
```

The following v1.0 decisions do not require architecture escalation:

- directed FacilityGraph runtime arcs;
- authored fairness-origin reachability quantifier;
- one active Warden lock;
- cooldown-only anti-spam;
- independent Door Jammer overlay;
- mean-shortest-route detour pressure metric;
- no physical Warden combat without evidence.

---

# 62. References

## 62.1 Project sources

1. `AI_Architecture_v1.1.md`.
2. `ECHO PROTO.docx`.
3. `ECHO PROTO(1).docx`.
4. `01_ECHO_PROTOCOL_Project_Scope_REVISED.docx`.
5. `02_ECHO_PROTOCOL_System_Architecture_REVISED.docx`.
6. `03_ECHO_PROTOCOL_Implementation_Spec_REVISED.xlsx`.
7. `KLTN.docx` — Research Facility Map Flow / Objective Layout.
8. `KLTN (1).docx` — multiplayer organization and synchronization decision.
9. `M1-015_ScenarioConfig_AED_Fairness_Policy_v0_FINAL.md`.
10. `Telemetry_Event_Schema_v0_FINAL.md`.
11. `M1-020_Test_Strategy_Fixed_vs_Adaptive_Experiment_v0_FINAL.md`.
12. `Stalker_AI_Design_v1.1.md` — engineering patterns only.
13. `Listener_AI_Design_v1.0.md` — engineering patterns only.
14. `ECHO-PROTOCOL-feature-m1-026-stalker-spike.zip` — implementation environment/source evidence; no Warden runtime source evidenced.

## 62.2 Official external references

15. Unity — ScriptableObject:  
    https://docs.unity3d.com/6000.0/Documentation/Manual/class-ScriptableObject.html

16. Unity — Execution order / lifecycle:  
    https://docs.unity3d.com/6000.0/Documentation/Manual/ExecutionOrder.html

17. Unity — Physics.OverlapBox:  
    https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Physics.OverlapBox.html

18. Unity — Test Framework:  
    https://docs.unity3d.com/6000.0/Documentation/Manual/com.unity.test-framework.html

19. Unity — Profiler:  
    https://docs.unity3d.com/Manual/Profiler.html

20. Photon Fusion 2 — State/Input Authority:  
    https://doc.photonengine.com/fusion/v2/manual/playerref

21. Photon Fusion 2 — Networked Properties:  
    https://doc.photonengine.com/fusion/v2/manual/data-transfer/networked-properties

22. Photon Fusion 2 — RPCs:  
    https://doc.photonengine.com/fusion/v2/manual/data-transfer/rpcs

23. Photon Fusion 2 — Network Simulation Loop:  
    https://doc.photonengine.com/fusion/v2/concepts-and-patterns/network-simulation-loop

24. Photon Fusion 2 — Host Mode Basics:  
    https://doc.photonengine.com/fusion/v2/tutorials/host-mode-basics/overview

---

# Detailed Design Validation

```text
Architecture baseline respected: YES
Warden remains Spatial Pressure Controller: YES
FacilityGraph separated from RegionGraph: YES
FacilityGraph authoring/validation complete: YES
Door state ownership/composition complete: YES
Door Jammer interaction complete: YES
Required route-target resolution complete: YES
Reachability quantifier unambiguous: YES
Candidate eligibility complete: YES
RoutePressure metric deterministic: YES
SafetyValidator complete: YES
Cumulative-lock/combined-state safety complete: YES
Telegraph contract complete: YES
Precommit revalidation complete: YES
Atomic apply/exactly-once complete: YES
Post-apply revalidation complete: YES
Fail-safe reopening complete: YES
Host authority preserved: YES
ScenarioConfig/AED boundary preserved: YES
Metrics deterministic: YES
Test plan complete: YES
Implementation plan complete: YES
WAR-DD-01 canonical route-action footprint complete: YES
WAR-DD-02 reason taxonomy normalized: YES
WAR-DD-03 zero-pressure policy semantics frozen: YES
WAR-SG-01 full-set candidate-bound safeguard complete: YES
Architecture escalation required: NO
```

---

# Final Consistency Audit

```text
Client can author Warden route lock: NO
Warden can lock non-WardenEligible arbitrary door: NO
Candidate can bypass SafetyValidator: NO
Telegraphed Door A can silently become Door B at commit: NO
Stale pre-telegraph validation can authorize apply: NO
Warden lock can intentionally make required objective unreachable: NO
Warden lock can intentionally remove all legal routes: NO
Cumulative/current overlays can bypass combined-state validation: NO
Post-apply safety failure can remain active silently: NO
Fail-safe uses player/monster teleport as normal repair: NO
Door Jammer state can be accidentally overwritten by Warden: NO
Door Jammer is automatically treated as player-route closure: NO
Telemetry can command current Warden candidate: NO
AED can choose current runtime DoorId: NO
routeModifier can be adaptively changed PRE_MATCH: NO
Proxy independently runs Warden policy: NO
RoutePressure uses ambiguous denominator/meaning: NO
Higher RoutePressure is assumed automatically better: NO
Hidden exact Player Transform is used without approved contract: NO
Warden combat behavior is invented from generic telemetry catalog: NO
FacilityEdgeDefinition duplicates WardenEligible ownership: NO
DoorDefinition duplicates WardenEligible ownership: NO
Candidate simulates a smaller footprint than runtime apply: NO
Bidirectional DoorId can leave an unsimulated reverse arc when applied: NO
Graph-definition failure is duplicated as candidate/safety reject reason: NO
Candidate-bound overflow is silently truncated: NO
Candidate-bound overflow is misclassified as candidate/safety rejection: NO
One logical policy evaluation can inspect only a partial valid candidate set: NO
Safe zero-pressure candidate can start telegraph/lock: NO
NoMeaningfulPressure has ambiguous policy semantics: NO
NoSafeCandidate is window-scoped instead of full-set scoped: NO
Candidate scheduling continuation state exists in Warden v1.0: NO
```

All expected audit answers are `NO`.

No P0/P1 architecture, authority, fairness, route-safety, lifecycle, metric, or implementation-contract ambiguity remains in this v1.0 detailed design.

```text
Detailed Design Status: BASELINED v1.0
Recommended Status: BASELINED v1.0
Architecture Escalation Required: NO
```

**End of `Warden_AI_Design_v1.0.md`**
