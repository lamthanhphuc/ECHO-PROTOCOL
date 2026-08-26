# ECHO PROTOCOL — AI Architecture v1.1

**Document:** `AI_Architecture_v1.1.md`  
**Project:** ECHO PROTOCOL — Co-op Survival Horror Multiplayer  
**Architecture Role:** AI / Telemetry / Research baseline for Feature-Complete Alpha  
**Revision:** v1.1  
**Date:** 2026-08-25  
**Architecture Status:** BASELINED v1.1  
**Scope of Baseline:** Top-level AI architecture and authority boundaries  
**Implementation Status:** See Implementation Readiness Matrix  
**Supersedes:** `AI_Architecture_Traditional_vs_Modern.md` as the current AI architecture baseline where this document explicitly revises it.  
**Important:** v1.1 is an architecture baseline, not a claim that all implementation, tuning, playtest, or research evidence is complete.

---

## Revision Correction Notes

This correction pass preserves the v1.1 architecture and applies six consistency corrections before M2 baseline use:

1. separated common runtime context from Stalker-specific memory;
2. clarified FSM / Planner / Action ownership;
3. changed `AdaptiveInputSnapshot` to a recommended pending contract rather than a schema frozen by this document;
4. added an Implementation Readiness Matrix;
5. replaced ambiguous `MapCoverage` with `SpatialNodeCoverage`;
6. clarified architecture baseline status versus subsystem implementation and experiment readiness.

### Map / Network Binding Correction

1. resolved networking binding to Unity `6000.5.8f1` + Photon Fusion 2 Host Mode;
2. resolved logical monster multiplayer authority against the project network contract;
3. established `Gameplay Zone ≠ AI Patrol Region`;
4. strengthened `RegionDefinition` toward designer-authored, deterministically validated canonical patrol regions;
5. established shared stable spatial identifiers between AI `RegionGraph` and Warden `FacilityGraph` where physical semantics overlap;
6. retained exact patrol-region segmentation as map-authoring/validation TBD.

### Final Environment / Terminology Correction

1. normalized the malformed Unity editor identifier in the networking document to the project-verified `6000.5.8f1`;
2. separated the resolved Photon Fusion framework/topology decision from the installed SDK/build environment binding;
3. removed the remaining generic `Monster Memory` label from the top-level system view and kept monster-specific typed memory;
4. clarified that the client presentation restriction applies specifically to authoritative monster-AI decisions, not to player Input Authority or supported local prediction.

Project evidence used for environment verification:

```text
ProjectSettings/ProjectVersion.txt
→ m_EditorVersion: 6000.5.8f1
→ revision: 5cb7df797b7d

Assets/Photon/Fusion/build_info.txt
→ Fusion 2.1.1 Stable
→ build 2177
```

The networking source document contains a malformed editor-version string. The supplied Unity project directly identifies `6000.5.8f1`, and Unity's official release page confirms the same editor release and changeset.

No architectural breaking change was found; the document remains v1.1.

# 1. Document Control

| Field | Value |
|---|---|
| Document owner | AI / Telemetry / Research |
| Review intent | Re-evaluate v1.0 against gameplay intent, implementation evidence, multiplayer authority, testability, maintainability, performance, and research validity |
| Baseline rule | M1 documents are treated as v1.0 evidence, not immutable truth |
| Change policy | A v1.0 decision is changed only when a concrete gameplay, correctness, implementation, testing, networking, performance, maintainability, or research reason exists |
| Architecture style | Composition-first, explicit data ownership, bounded authority, deterministic/testable runtime decisions |
| Runtime monster AI | Traditional / rule-based |
| Adaptive authority | ScenarioConfig only, behind validation |
| GenAI authority | Presentation only |
| Engine binding | Unity 6000.5.8f1 — verified from supplied `ProjectSettings/ProjectVersion.txt` |
| Network framework/topology binding | Photon Fusion 2 — Host Mode |
| Installed Fusion SDK build | Fusion 2.1.1 Stable, build 2177 — verified from supplied `Assets/Photon/Fusion/build_info.txt` |
| Multiplayer target | 2–4 Players |
| Multiplayer authority | Host authoritative; Fusion Host/Server holds State Authority over authoritative monster state |
| Monster AI authority | Host authoritative |
| Monster AI Fusion integration | NOT YET EVIDENCED / implementation required |

A status such as `DONE` or `FROZEN` in an M1 document is evidence that a prior contract was intentionally closed; it is **not** sufficient evidence that the decision is still the best implementation baseline.

---

# 2. Purpose

The purpose of v1.1 is to produce an implementation-oriented AI architecture that can support a Feature-Complete Alpha without forcing M3–M6 to invent core AI structure.

v1.1 specifically:

1. reviews the M1/v1.0 decisions;
2. preserves boundaries that remain technically sound;
3. revises incomplete or implementation-hostile decisions;
4. adds missing multiplayer, spatial, navigation recovery, observability, quality-measurement, and dependency architecture;
5. turns Stalker patrol/search from local behaviors into explicit spatial-planning problems;
6. establishes shared abstractions that Listener and Warden can reuse without forcing them into the Stalker design;
7. preserves reproducibility required by the Fixed-vs-Adaptive research plan.

This document is intentionally not a Behavior Tree/GOAP/ML redesign. No such change is justified by the current project scope.

---

# 3. Scope

## 3.1 In scope

- authoritative runtime AI ownership;
- shared monster component responsibilities;
- perception observations;
- monster-specific typed memory and shared `MonsterRuntimeContext`;
- FSM/rule ownership;
- planning boundaries;
- navigation robustness;
- spatial graph / region graph / coverage planning;
- Stalker architecture;
- Listener architecture boundary;
- Warden architecture boundary;
- telemetry separation;
- Player/Team Profile boundary;
- AED bounded authority;
- GenAI presentation boundary;
- observability;
- test architecture;
- measurable AI-quality signals;
- performance principles;
- failure and recovery;
- code-module and folder recommendations;
- migration from current Stalker code without rewrite.

## 3.2 Out of scope

- final tuning values;
- fixed Hz budgets before profiling;
- final ms performance budgets before profiling;
- final acceptance thresholds for coverage/revisit/stuck metrics before baseline collection;
- full Listener FSM;
- full Warden policy state machine;
- ML training;
- runtime generative decision-making;
- procedural map generation;
- low-level Photon Fusion monster property/RPC/callback implementation beyond the resolved Host Mode binding;
- invented Fixed-vs-Adaptive experiment results.

---

# 4. Inputs / Documents Reviewed

The review used the current gameplay baseline, planning package, M1 AI contracts, and latest Stalker spatial-patrol implementation evidence.

Primary project inputs:

- `ECHO PROTO.docx` / `ECHO PROTO(1).docx`
- `01_ECHO_PROTOCOL_Project_Scope_REVISED.docx`
- `02_ECHO_PROTOCOL_System_Architecture_REVISED.docx`
- `03_ECHO_PROTOCOL_Implementation_Spec_REVISED.xlsx`
- `04_ECHO_PROTOCOL_Project_Management_Baseline_REVISED.xlsx`
- `05_ECHO_PROTOCOL_Project_Plan_4P_2026_REVISED.xlsx`
- `AI_Architecture_Traditional_vs_Modern.md`
- `M1-013_Stalker_FSM_Sensor_Contracts_FINAL.md`
- `Telemetry_Event_Schema_v0_FINAL.md`
- `M1-014_Player_Team_Profile_Fields_Formulas_v0_FINAL.md`
- `M1-015_ScenarioConfig_AED_Fairness_Policy_v0_FINAL.md`
- `M1-019_GenAI_Mission_Briefing_Scope_Safety_Contract_v0_FINAL.md`
- `M1-020_Test_Strategy_Fixed_vs_Adaptive_Experiment_v0_FINAL.md`
- `KLTN.docx` — **THIẾT KẾ SƠ BỘ MAP RESEARCH FACILITY VÀ VỊ TRÍ OBJECTIVE** / Map Flow Plan v0
- `KLTN (1).docx` — **Chốt cách tổ chức multiplayer và đồng bộ dữ liệu trong trận**
- `ECHO-PROTOCOL-feature-m1-026-stalker-spike.zip` — supplied Unity project snapshot used to verify `ProjectSettings/ProjectVersion.txt` and installed Photon Fusion `build_info.txt`
- latest implementation/code notes for `NavMeshSpatialGraphBuilder`, `SpatialPatrolMemory`, `SpatialPatrolPlanner`, `StalkerBlackboard`, `StalkerNavigationController`, and Stalker controller integration.

Key project intent retained from GDD: Stalker creates vision/spatial pressure, Listener creates noise/hearing pressure, and Warden changes route pressure while preserving an alternative route. The Research Facility is intentionally learnable rather than an arbitrary procedural maze.

---

# 5. External Research Reviewed

The architecture uses external sources only where they support a project-relevant decision.

## 5.1 Unity sources

1. **Unity — Editor release verification / NavMeshPath status**  
   The supplied project reports `m_EditorVersion: 6000.5.8f1`; Unity's official release page lists Unity `6000.5.8f1` with changeset `5cb7df797b7d`, matching the supplied `ProjectVersion.txt`. Unity also explicitly distinguishes `PathComplete`, `PathPartial`, and `PathInvalid` for NavMesh paths.  
   https://unity.com/releases/editor/whats-new/6000.5.8f1  
   https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AI.NavMeshPath-status.html

2. **Photon Fusion 2 — State Authority / Input Authority**  
   Photon Fusion 2 documents that in Host/Server Mode the server/host owns State Authority for networked game state; Input Authority is separate and is used for client/server input ownership.  
   https://doc.photonengine.com/fusion/v2/manual/playerref

3. **Photon Fusion 2 — Networked Properties / RPCs**  
   `[Networked]` properties on `NetworkBehaviour` represent synchronized state. RPCs are transient calls and are not a substitute for durable synchronized monster state.  
   https://doc.photonengine.com/fusion/v2/manual/data-transfer/networked-properties  
   https://doc.photonengine.com/fusion/v2/manual/data-transfer/rpcs

4. **Unity Test Framework**  
   Unity supports Edit Mode and Play Mode testing. This maps well to separating pure graph/scoring tests from runtime NavMesh/perception/network integration tests.  
   https://docs.unity3d.com/6000.0/Documentation/Manual/com.unity.test-framework.html

5. **Unity Profiler**  
   Performance budgets are to be derived from profiling rather than invented in architecture.  
   https://docs.unity3d.com/Manual/Profiler.html

## 5.2 Academic / research sources

1. Hunicke, LeBlanc, Zubek, **MDA: A Formal Approach to Game Design and Game Research** (AAAI workshop, 2004).  
   Applicability: reinforces that implementation mechanics should be evaluated by the gameplay dynamics they produce, not because a pattern is fashionable.  
   https://aaai.org/papers/ws04-04-001-mda-a-formal-approach-to-game-design-and-game-research/

2. Yannakakis & Hallam, **Real-Time Game Adaptation for Optimizing Player Satisfaction**, IEEE TCIAIG 1(2), 2009, DOI `10.1109/TCIAIG.2009.2024533`.  
   Applicability: adaptive systems depend on a player model and controllable parameters; it does **not** imply that ECHO requires online ML. ECHO uses a deliberately bounded rule-based policy for explainability and reproducibility.

3. Zohaib, **Dynamic Difficulty Adjustment (DDA) in Computer Games: A Review**, Advances in Human-Computer Interaction, 2018, Article 5681652.  
   Applicability: supports treating DDA as controlled modification of game parameters/scenarios based on player performance, but does not justify unbounded monster commands.  
   https://doi.org/10.1155/2018/5681652

4. Spronck et al., **Adaptive Game AI with Dynamic Scripting**, Machine Learning 63, 217–248, 2006, DOI `10.1007/s10994-006-6205-6`.  
   Applicability: highlights clarity, robustness, efficiency, consistency, variety, and scalability as relevant requirements for adaptive game AI. ECHO specifically prioritizes clarity/reproducibility over learning complexity.

### Research conclusion for ECHO

Research supports **adaptation with measurable inputs and controlled outputs**. It does not create evidence that neural networks, reinforcement learning, GOAP, or runtime GenAI are appropriate here. For a student KLTN with multiplayer, limited development time, reproducibility requirements, and an explicit Fixed-vs-Adaptive comparison, bounded rule-based adaptation is the stronger architectural choice.

---

# 6. Problems Found in v1.0

## 6.1 M1 Review Table

| Area | v1.0 Decision | Evaluation | v1.1 Decision | Reason |
|---|---|---|---|---|
| Runtime AI type | Traditional monster AI | KEEP | Rule/FSM runtime AI | Deterministic, testable, sufficient for current gameplay |
| Stalker perception | Vision/LOS | KEEP | Physical observations remain sensor output | Correct counterplay and anti-omniscience boundary |
| Stalker FSM | PATROL/DETECT/CHASE/ATTACK/RECOVER/SEARCH | KEEP | Keep six semantic states | Current gaps are planning/navigation, not missing top-level states |
| Stalker Search | LKP + SearchRadius + SearchDuration | MODIFY | Add explicit SearchContext, candidate planner, visited memory, budget | Existing contract prevents cheating but is too weak to implement robust search |
| Stalker Patrol | local candidates + staleness/backtrack | MODIFY | hierarchical region-aware coverage | local scoring reduces repetition but cannot guarantee global map coverage |
| Spatial representation | NavMesh/spatial graph | MODIFY | NavMesh + SpatialGraph + RegionGraph | triangle-level local graph needs a global abstraction |
| AI blackboard | partial StalkerBlackboard | MODIFY | shared `MonsterRuntimeContext` + monster-specific typed memory | common context must not impose Stalker target/search semantics on Listener or Warden |
| Navigation | SetDestination / arrival-oriented | REPLACE | path evaluation + progress monitor + recovery ladder | partial/invalid/stale/stuck cases require first-class handling |
| Inheritance | not explicitly constrained | ADD | composition-first | 3 monsters have different pressure models; deep base classes would couple them |
| Listener | NoiseEvent → Hearing → AI concept | ADD | pipeline boundary + shared observation/memory/navigation contracts | implementation-level ownership missing |
| Warden | route/door control concept | ADD | FacilityGraph → RouteAnalysis → Candidate → SafetyValidator → Action | Warden is not a Stalker clone and needs graph-level fairness |
| Monster networking | authority insufficiently explicit in AI docs | ADD | host/server authoritative AI | shared monsters cannot run independently authoritative on clients |
| Telemetry | gameplay → telemetry → profile | KEEP | keep one-way analytical boundary | correct separation of facts vs analysis |
| Runtime noise vs telemetry | conceptually separate | KEEP | separate buses/pipelines | Listener cannot “hear” analytics events |
| Player/Team Profile | processed data, no direct monster command | KEEP | maintain boundary | research validity and modularity |
| TeamProfile lifecycle | match-scoped, while GDD expects next-match AED | MODIFY | require a decision-scoped adaptive input boundary; `AdaptiveInputSnapshot` is the recommended pending contract name | resolves the architecture boundary without freezing a Profile/AED schema in this document |
| AED | bounded ScenarioConfig authority | KEEP | InputGate → Policy → CandidateConfig → Validator → Apply/Fallback | correct for explainability/reproducibility |
| AED direct pacing concepts in older Tier-A | runtime pacing rows inconsistent with later bounded contract | MODIFY | only explicit allowed decision points/whitelist | prevents undocumented per-frame/ad hoc adaptation |
| GenAI | briefing + validator + cache/retry/fallback | MODIFY | retain safety core; defer nonessential complexity | P0 value is safe presentation; not a gameplay-control research contribution |
| Observability | debug fields exist locally | ADD | first-class `AIDebugSnapshot` | current scattered serialized debug fields do not provide uniform evidence |
| AI metrics | partial telemetry/test metrics | ADD | coverage, revisit, stuck, path failure, reacquisition etc. | implementation quality cannot be defended by subjective observation alone |
| Performance | no architecture-level evaluation cadence | ADD | event/tick-driven + bounded queries + profiler-derived budgets | avoids Update-driven scaling and invented budgets |
| Test architecture | strategy exists | MODIFY | map architecture layers to EditMode/PlayMode/network tests | v1.1 needs direct implementation boundaries |
| Failure recovery | fallback concepts fragmented | ADD | explicit navigation/planning recovery ladder | necessary for non-soft-lock M2/M4 goal |

## 6.2 Concrete v1.0 conflicts

### Conflict A — Spatial patrol sophistication vs actual coverage goal

The latest implementation constructs a graph from NavMesh triangulation and keeps visit staleness, connectivity weighting, and immediate-backtrack penalties. This is a useful local exploration baseline. However, candidate selection is bounded by local graph depth. A local objective cannot guarantee visiting remote graph areas.

**Revision:** local staleness becomes one scoring feature under a global region objective.

### Conflict B — Older Tier-A “Final Hunt state” wording vs Stalker contract

The implementation specification contains older wording describing a distinct Final Hunt monster state. The Stalker contract correctly treats Final Hunt as gameplay/configuration context rather than a seventh FSM state.

**Decision:** keep six-state FSM; Final Hunt modifies only explicitly allowed configuration. No new Stalker state.

### Conflict C — Team profile next-match intent vs match-scoped TeamProfile v1.0

The GDD says post-match profile data can influence later ScenarioConfig. M1-014 correctly rejected an invented persistent party identity and defined TeamProfile v1.0 as match-scoped.

**Revision:** do not convert TeamProfile into a fake persistent party object. v1.1 freezes only the need for a **decision-scoped adaptive input boundary** between processed Profile data and the AED Input Gate. `AdaptiveInputSnapshot` is the recommended contract name, pending Profile v1.1 + AED v1.1 approval.

```text
Current Lobby Composition / References
+ permitted processed Profile inputs
+ completeness / availability
+ relevant source / version references
→ decision-scoped adaptive input boundary
  (recommended name: AdaptiveInputSnapshot)
→ AED Input Gate
```

This architecture document does **not** freeze exact fields, formulas, eligibility rules, or persistence semantics for that boundary. Profile v1.1 and AED v1.1 must approve/finalize the schema. If they choose an equivalent contract name, the architectural requirement remains unchanged. If required processed inputs are unavailable, the AED gate must use the approved Fixed/Fallback behavior.

### Conflict D — generic AI architecture too coarse for Warden

`Sensors + FSM/Rules + Navigation` is suitable as a high-level category but misleading for Warden, whose meaningful problem is route topology and door constraints.

**Revision:** Warden uses facility graph analysis and safety validation. It may animate a physical entity, but its core reasoning is not NavMesh-agent pursuit.

---

# 7. Design Principles

1. **Gameplay intent before pattern choice.**
2. **One authoritative owner for gameplay facts.**
3. **Sensors report observations; they do not make gameplay decisions.**
4. **Memory stores AI-known state; it must not become an omniscient data cache.**
5. **FSM owns semantic behavior mode; planner owns destination/action intent inside a mode.**
6. **Navigation owns motion/path execution and recovery, not target selection.**
7. **Global spatial goal and local destination are different concepts.**
8. **Composition over deep monster inheritance.**
9. **Bounded adaptation over opaque adaptation.**
10. **Telemetry observes; it never commands runtime AI.**
11. **Debug views are read-only projections.**
12. **Performance budgets come from profiling.**
13. **Architecture must create test seams.**
14. **No new abstraction without a concrete consumer.**

---

# 8. System AI Boundaries

Recommended top-level architecture:

```text
                         GAMEPLAY AUTHORITY
                                │
                    Host / Server Runtime
                                │
        ┌───────────────────────┼─────────────────────────┐
        │                       │                         │
        ▼                       ▼                         ▼
     STALKER                 LISTENER                  WARDEN
  Vision/LOS              Runtime Noise           Facility Graph
      │                        │                        │
  Observations             Hearing Obs.             Route Model
      │                        │                        │
  StalkerMemory           ListenerMemory         WardenPressureContext
      │                        │                        │
  6-State FSM             Rules/FSM TBD           Route Policy
      │                        │                        │
  Spatial Planner          Planner                 Safety Validator
      │                        │                        │
  Navigation               Navigation             Door/Route Action
        └───────────────────────┬─────────────────────────┘
                                │
                         Gameplay Facts/Events
                                │
                  ┌─────────────▼──────────────┐
                  │ Telemetry Emitter/Buffer  │
                  └─────────────┬──────────────┘
                                ▼
                         Aggregation/Profile
                                │
                                ▼
                     AED Input Gate / Policy
                                │
                                ▼
                    Candidate ScenarioConfig
                                │
                                ▼
                           Validator
                                │
                                ▼
                       Applied Config/Fallback
                         at allowed boundary
```

GenAI is a separate presentation branch:

```text
Validated Mission Facts
        ↓
GenAI Adapter / Provider
        ↓
Briefing Validator
        ├── valid → Presentation
        └── fail  → Cache / Deterministic Template
```

There is no path from GenAI output to monster gameplay.

---

# 9. Runtime Authority Model

## 9.1 Authoritative runtime

The host/server owns:

- monster perception evaluation;
- observation acceptance;
- target selection;
- Detection Meter progression;
- FSM transitions;
- AI memory mutation;
- patrol/search planning;
- navigation destination decisions;
- attack resolution request and authoritative hit validation;
- runtime NoiseEvent evaluation used by Listener;
- Warden route/door decisions;
- Warden route-validity checks;
- ScenarioConfig application;
- authoritative gameplay telemetry facts.

## 9.2 Non-authoritative client role within the monster-AI boundary

Within the **authoritative monster-AI boundary**, non-authoritative clients do not own monster decisions. Their responsibilities are synchronized monster-state consumption, local presentation, and Fusion-supported non-authoritative interpolation/prediction where applicable without changing State Authority:

- replicated monster transform/state;
- animation;
- VFX;
- SFX;
- telegraphs;
- local presentation;
- non-authoritative debug visualization where allowed;
- permitted interpolation/prediction that does not mutate authoritative monster state.

This restriction applies specifically to monster AI. It does **not** mean clients lack `InputAuthority`, local input collection, prediction, rollback, or reconciliation responsibilities for player-controlled entities where the Fusion design assigns them.

Non-authoritative clients MUST NOT:

- select authoritative monster targets;
- mutate authoritative monster memory/context;
- transition authoritative monster FSM;
- resolve authoritative monster attacks;
- apply authoritative Warden route locks.

## 9.3 Photon Fusion 2 Host Mode binding

The project binding is **Photon Fusion 2 — Host Mode** on Unity `6000.5.8f1`, targeting 2–4 players. The supplied project snapshot contains Fusion **2.1.1 Stable build 2177**; this records the current implementation environment and does not change the architecture-level decision, which remains `Photon Fusion 2 + Host Mode`.

```text
Client
→ input / action request
→ Host validates and executes
→ authoritative state
→ Fusion synchronization
→ Clients
```

For monster objects, the Host/Server is the authoritative simulation peer. In Fusion terminology, Host/Server Mode keeps authoritative network state under **State Authority** on the Host/Server. `InputAuthority` is relevant to player input ownership and does not grant clients authority over monster decision state.

This architecture does not freeze the exact `[Networked]` property set, RPC list, or simulation-callback implementation until concrete monster networking code is supplied and tested.

---

# 10. Shared Monster Architecture

Recommended per-monster runtime composition:

```text
MonsterAgentFacade / MonsterRoot
│
├── Perception component(s)
│   └── produces immutable Observation records
│
├── TargetEligibility / TargetSelector (when applicable)
│
├── MonsterRuntimeContext
│   └── only genuinely shared runtime facts
│
├── Monster-specific Memory
│   └── StalkerMemory / ListenerMemory / WardenPressureContext as applicable
│
├── Decision Controller
│   └── FSM / explicit rules
│
├── Planner
│   └── turns state intent into destination/action intent
│
├── NavigationController (if physical movement is used)
│
├── ActionController
│   └── executes authoritative gameplay actions already selected by the owning decision/policy layer
│
├── AIObservability
│   └── produces AIDebugSnapshot
│
└── TelemetryAdapter
    └── emits facts; never feeds decisions back
```

`MonsterAgentFacade` coordinates lifecycle and ticks but must not accumulate all behavior logic.

---

# 11. Perception Layer

## 11.1 Responsibility

Perception answers: **what physical evidence is currently observable under the sensor contract?**

It does not answer:

- whom to attack;
- whether a player is gameplay-eligible;
- whether a state transition occurs;
- where to navigate;
- what difficulty should be used.

## 11.2 Observation model

Use small immutable observation values rather than giving downstream code direct unrestricted access to Player GameObjects where possible.

Example:

```csharp
public readonly struct VisionObservation
{
    public PlayerId PlayerId { get; init; }
    public Vector3 ObservedPosition { get; init; }
    public Vector3 ObservedDirection { get; init; }
    public float ObservedAt { get; init; }
    public float Distance { get; init; }
}
```

The authoritative resolver may map `PlayerId` back to a runtime entity when an action is required, but memory should preserve only information the monster legitimately knew.

## 11.3 Vision

Vision stages:

```text
candidate players
→ broad distance/cone filtering
→ LOS query
→ VisionObservation[]
```

Avoid full raycast-to-every-player-every-frame by default. Evaluation cadence should be profiled and may be ticked/cached.

## 11.4 Hearing

Hearing consumes **Runtime NoiseEvent**, not TelemetryEvent.

```text
Gameplay Action
→ Runtime NoiseEvent
→ NoiseSystem
→ HearingSensor
→ HearingObservation
```

In parallel:

```text
Runtime NoiseEvent
→ TelemetryEmitter
→ NOISE_EMITTED
```

The two pipelines share a source fact but have different responsibilities.

---

# 12. Memory Model

## 12.1 Decision

v1.1 uses a **small shared runtime context plus monster-specific typed memory**. It does not define a universal `MonsterMemory` containing Stalker target/search concepts.

Reason:

- `DetectionTarget`, LKP, LastSeenDirection, and detection knowledge are Stalker semantics;
- Listener requires hearing-specific memory;
- Warden reasons over pressure/route context rather than Stalker target memory;
- tests still need explicit ownership without creating a generic untyped dictionary blackboard;
- this follows the rule that shared code exists only when semantics are genuinely shared.

## 12.2 Shared runtime context

```text
MonsterRuntimeContext
- MonsterId
- CurrentRegionId?
- CurrentDestination?
- LastAcceptedObservationTime?
```

A field belongs here only if multiple monster/module implementations actually share the same meaning. If later evidence shows that `CurrentRegionId`, `CurrentDestination`, or `LastAcceptedObservationTime` do not have common semantics, they should move to the relevant monster-specific context rather than being preserved for API symmetry.

## 12.3 Stalker memory

```text
StalkerMemory
- CurrentTargetId?
- DetectionTargetId?
- LastKnownPosition?
- LastSeenDirection?
- TargetLastSeenTime?
- detection-related knowledge required by the Stalker contract
```

`StalkerMemory` stores only knowledge that Stalker may legitimately know through permitted observations and state transitions.

## 12.4 Stalker spatial memory

```text
StalkerSpatialMemory
- NodeLastVisited[]
- RegionLastVisited[]
- NodeVisitCount[]
- RegionVisitCount[]
- RecentNodeHistory
- RecentRegionHistory
```

This memory stores coverage history, not semantic FSM state.

## 12.5 Stalker search context

```text
StalkerSearchContext
- LastKnownPosition
- LastSeenDirection
- SearchStartTime
- SearchBudget
- SearchOriginRegion
- CandidateHistory
- VisitedSearchNodes
- CurrentSearchCandidate
```

`SearchBudget` is a logical bounded-resource concept. The actual numerical limit is configuration/tuning, not invented here.

## 12.6 Other monsters

Listener will define `ListenerMemory` with hearing-specific semantics in `Listener_AI_Design_v1.0.md`. Warden uses a `WardenPressureContext` or equivalent route/pressure context defined by its detailed design. Neither is required to carry Stalker target, detection, LKP, or search fields.

## 12.7 Anti-cheat invariant

Memory is updated only from permitted observations/events.

**SEARCH MUST NOT read hidden Player Transform.**

Network replication making a Player transform technically available to the host does not make it valid AI knowledge.

---

# 13. Decision / FSM Layer

## 13.1 Ownership

FSM owns **semantic behavior mode and state transitions**, including the decision to transition into `ATTACK`.

When the FSM enters `ATTACK`, the `AttackController` executes the authoritative attack action/resolution defined by the gameplay contract. The planner does not decide whether ATTACK should begin.

FSM does not own:

- raw LOS implementation;
- spatial graph construction;
- path-status classification;
- local/global candidate generation;
- telemetry aggregation.

## 13.2 Stalker state decision

Keep:

```text
PATROL
DETECT
CHASE
ATTACK
RECOVER
SEARCH
```

The six states remain sufficient because the newly required complexity is sub-state planning, not a new externally meaningful behavior mode.

Examples:

- “move to LKP” is SEARCH planning, not a seventh state;
- “select next region” is PATROL planning, not a state;
- “repath” is navigation recovery, not a state;
- “Final Hunt” is match/config context, not a Stalker state.

This keeps regression semantics stable while fixing implementation weaknesses.

---

# 14. Planning Layer

Planner answers:

> Given the current semantic state and legal AI knowledge, what should the monster try to do next?

Planner output is an intent **inside the already-active semantic state**. Shared planner intents may include:

```text
MoveIntent
PatrolIntent
SearchIntent
InvestigateIntent
HoldIntent
RoutePressureCandidate
NoAction
```

The planner does not directly move transforms and does not own the transition into `ATTACK`.

Authoritative Stalker attack ownership is:

```text
FSM decides ATTACK transition
→ AttackController executes attack payload
→ authoritative attack resolution
```

This document therefore does not use a generic `AttackTarget` planner intent. If a later implementation uses an `AttackIntent` value, it may only be a payload created **after** the FSM has already selected `ATTACK`; it cannot be the source of the transition.

For Stalker, use distinct planners/strategies behind one interface:

- Patrol planner;
- Search planner;
- Chase destination policy can stay simple unless evidence requires prediction;
- attack entry stays FSM/action logic, not a general-purpose tactical planner.

---

# 15. Navigation Layer

## 15.1 Responsibility

Navigation owns:

- destination validation;
- path request;
- path status;
- movement progress;
- arrival;
- repath;
- stale-path detection;
- stuck detection;
- stopping;
- path execution status.

Navigation does not own target selection or AI state.

## 15.2 Result contract

Use a status richer than bool:

```text
NavigationPlanResult
- Accepted
- DestinationInvalid
- PathComplete
- PathPartial
- PathInvalid
- AgentUnavailable
```

Runtime execution status:

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

Unity's `NavMeshPathStatus` is the lower-level source for Complete/Partial/Invalid classification; v1.1 wraps it in game-specific meaning.

## 15.3 Complete / Partial / Invalid

- `PathComplete`: candidate may be accepted.
- `PathPartial`: reject for objectives that require exact reachability; may only be used by a specifically documented “approach nearest reachable point” behavior.
- `PathInvalid`: reject.
- destination off/invalid NavMesh: reject or sample according to explicit destination-validation policy.

Patrol/Search destinations should normally require complete reachability.

---

# 16. Spatial Reasoning Layer

This is the largest v1.1 change.

## 16.1 Problem

Current logic is essentially:

```text
NavMesh triangulation
→ SpatialGraph nodes
→ bounded BFS candidate set
→ staleness/connectivity/backtrack scoring
→ destination
```

This is useful **local exploration**.

It cannot guarantee global coverage because remote regions outside the candidate horizon cannot compete.

Weighted randomness does not repair this. Randomness can diversify local choices, but it does not create a global invariant.

## 16.2 v1.1 hierarchy

```text
NavMesh
  ↓
SpatialGraph
  ↓
RegionGraph
  ↓
CoverageMemory
  ↓
GlobalPatrolPlanner
  ↓
LocalPatrolSelector
  ↓
NavigationController
```

## 16.3 SpatialGraph

Purpose:

- navigable local topology;
- node adjacency;
- node position;
- optional local connectivity metadata.

Keep the existing triangulation-based graph builder if tests show stable adjacency on the Research Facility. It is already implemented and has a concrete use.

Do **not** expose every triangle as a gameplay “room.”

## 16.4 RegionGraph

Purpose:

- global coarse topology;
- meaningful patrol-coverage units;
- route between distant areas;
- disconnect detection;
- dead-end classification;
- stable map identities for door/topology events, debugging, telemetry, and reproducibility.

### Gameplay Zone ≠ AI Patrol Region

The approved Map Flow Plan defines three macro gameplay zones:

```text
Zone 1 — Research & Storage Sector
Zone 2 — Power & Engineering Sector
Zone 3 — Security & Containment Sector
```

These are progression/gameplay-purpose units, not automatically the Stalker's patrol regions.

```text
Gameplay Zone
≠
AI Patrol Region
```

A zone can contain multiple rooms, corridors, junctions, loops, and alternative routes. Three giant patrol regions would be too coarse for useful coverage planning.

```text
Research Facility
├── Gameplay Zone 1
│   ├── AI Patrol Region ...
│   └── ...
├── Gameplay Zone 2
│   ├── AI Patrol Region ...
│   └── ...
└── Gameplay Zone 3
    ├── AI Patrol Region ...
    └── ...
```

**Exact AI Patrol Region segmentation: TBD — playable map authoring/validation.**

### Canonical region-authoring strategy

v1.1 freezes the strategy, not the Unity serialization format:

> **Canonical AI Patrol Regions are designer-authored / designer-defined and deterministically validated.**

This fits one designer-controlled Research Facility whose rooms, corridors, junctions, doors, loops, and objective areas have gameplay meaning. It improves determinism, debugging, versioning, thesis explanation, and Warden/topology integration.

Possible Unity representations remain implementation bindings:

- scene volumes;
- ScriptableObject definitions;
- map metadata;
- another deterministic authored representation.

Automatic NavMesh clustering may assist authoring or validation, but does not automatically become canonical `RegionId` identity unless later implementation evidence justifies an ADR change.

### Conceptual RegionDefinition

```text
RegionDefinition
- RegionId
- GameplayZoneId
- spatial membership / volume mapping
- SpatialNode membership or mapping
- adjacent RegionIds
- enabled/reachable state
- relevant DoorIds / controlled edges
- optional semantic tags only where a concrete consumer exists
```

Exact serialization format is not frozen here.

```text
RegionEdge
- FromRegion
- ToRegion
- controllingDoorId?
- routeClass?   // only if a concrete consumer is approved
```

If `routeClass` is later used, values may distinguish `Main`, `Alternative`, or `Connector`; the metadata must not imply that Stalker always prioritizes Main Route.

### Main Route ≠ Stalker Patrol Route

The Map Flow Plan defines a convenient Main Route and Alternative Routes. Main Route is explicitly not mandatory for players and must not become a fixed Stalker patrol rail.

```text
MainRoute
≠
StalkerPatrolRoute
```

Main/Alternative route information may contribute topology metadata. `GlobalPatrolPlanner` remains primarily based on reachability, coverage, staleness, recent history, topology, and local path feasibility.

### Shared spatial identities with Warden

AI `RegionGraph` and Warden `FacilityGraph` remain separate graph views over overlapping physical identities:

```text
Facility Spatial Definitions
        │
        ├── GameplayZoneId
        ├── RegionId
        └── DoorId
              │
        ┌─────┴──────────┐
        ↓                ↓
 AI RegionGraph     Warden FacilityGraph
```

They do not need the same graph object, edge semantics, resolution, or NavMesh triangle topology. Reusing stable identifiers where semantics overlap improves debugging, telemetry correlation, route validation, map-change handling, and thesis evidence without creating a God Graph.

### Door/topology updates

```text
DoorStateChanged
→ update affected RegionGraph edge
→ update affected FacilityGraph edge
→ active AI path/reasoning re-evaluates if affected
```

Use event-driven invalidation where possible. Do not rebuild all graph data every frame or continuously poll every door when authoritative door-state events are available.

Spatial definitions used for evaluation should be reproducible against map/content and region-definition versions where those versions have a concrete telemetry/test consumer.

## 16.5 CoverageMemory

Coverage memory stores visitation facts, not behavior decisions.

```text
RegionLastVisited
RegionVisitCount
NodeLastVisited
NodeVisitCount
RecentRouteHistory
```

## 16.6 Global objective

Global Patrol chooses a **target region**.

Preferred policy:

1. determine currently reachable region set;
2. exclude temporarily invalid/disconnected regions;
3. prioritize least-covered / most-stale reachable regions;
4. apply bounded recent-region/backtrack penalties;
5. select a global region objective;
6. route toward it through RegionGraph.

This is deterministic under equal inputs unless a seeded tie-breaker is intentionally used.

## 16.7 Local destination

Local selector chooses a concrete SpatialNode inside or toward the target region.

Local score may use:

- node staleness;
- node visit count;
- connectivity;
- distance/cost;
- immediate-backtrack penalty;
- dead-end penalty when inappropriate;
- current route progression.

## 16.8 Coverage guarantee semantics

v1.1 does **not** claim mathematical full coverage under arbitrary dynamic doors, pursuit interruptions, or disconnected NavMesh.

It guarantees a stronger architecture invariant:

> PATROL continually selects globally least-covered reachable regions rather than indefinitely optimizing only a local neighborhood.

A testable coverage goal becomes possible:

```text
If a region remains reachable
AND PATROL remains active long enough
AND no higher-priority gameplay state continuously interrupts patrol
THEN it remains eligible as a global patrol objective until visited.
```

This is the appropriate guarantee for a horror game with interruptions.

## 16.9 Disconnected regions

At graph build/validation:

- compute connected components;
- identify the component containing Stalker;
- mark unreachable regions;
- do not repeatedly select unreachable objectives;
- emit debug/telemetry warning because disconnected content may indicate map/NavMesh authoring error.

Dynamic door state may temporarily alter route feasibility. RegionGraph must re-evaluate affected edges rather than rebuilding the full graph every frame.

---

# 17. Stalker Architecture

## 17.1 Component layout

```text
StalkerRoot
├── VisionSensor
├── TargetSelector
├── StalkerMemory
├── StalkerFSM
├── StalkerPatrolPlanner
│   ├── GlobalPatrolPlanner
│   └── LocalPatrolSelector
├── StalkerSearchPlanner
├── StalkerNavigationController
├── StalkerAttackController
├── StalkerDebugProvider
└── StalkerTelemetryAdapter
```

## 17.2 PATROL

PATROL is no longer “pick a random waypoint.”

Flow:

```text
Current position
→ current SpatialNode / Region
→ reachable Region set
→ coverage ranking
→ Global target Region
→ route / frontier toward Region
→ local candidate generation
→ complete-path validation
→ local destination
→ Navigation
→ arrival / progress update
→ coverage memory update
→ repeat
```

Fixed waypoint patrol may remain as:

- deterministic fallback;
- test fixture;
- safe degradation when spatial graph initialization fails.

It should not remain the primary behavior if the dynamic coverage architecture passes validation.

## 17.3 DETECT

Retain M1-013 semantics:

- acquire from valid visible observations;
- `DetectionTarget` locked according to contract;
- meter belongs to decision/controller logic;
- no meter carry across target;
- target eligibility filtering is outside physical sensor.

## 17.4 CHASE

CHASE follows `CurrentTarget` only while legitimately visible/eligible according to the Stalker contract.

When LOS is lost:

```text
last valid VisionObservation
→ update LKP + LastSeenDirection + time
→ enter SEARCH
```

No hidden-transform follow.

## 17.5 SEARCH

SEARCH gets an implementation-level design.

### Entry

```text
SearchContext:
- origin = LastKnownPosition
- lastSeenDirection
- startTime
- searchRadius
- searchDuration
- originRegion
- visited search candidates = empty
```

### Phase A — LKP approach

The first preferred destination is LKP if it is still a valid complete-path destination.

Failure to reach exact LKP does not create a new FSM state. Planner moves to fallback search candidates.

### Phase B — candidate generation

Candidate sources can include:

- SpatialGraph nodes within SearchRadius of LKP;
- nodes forward-biased by LastSeenDirection;
- nearby junction/high-connectivity nodes;
- adjacent region frontier nodes if within legal search radius/contract.

Do not sample the hidden Player position.

### Candidate filtering

Reject:

- outside SearchRadius;
- already exhausted candidates;
- unreachable/partial/invalid paths;
- candidate behind currently blocked/disconnected route when no path exists;
- identical current destination unless re-evaluation explicitly requires it.

### Scoring

A practical score can combine normalized factors:

```text
score =
  directionBias
+ novelty/staleness
+ usefulConnectivity
- travelCostPenalty
- immediateBacktrackPenalty
- recentSearchCandidatePenalty
```

Exact weights are tuning data, not architecture constants.

### Search budget

SEARCH is bounded by `SearchDuration` and optionally a bounded candidate-attempt budget. Time remains the contract-level termination guard.

### Reacquisition

- same CurrentTarget visible again → CHASE immediately;
- another eligible Player visible while old target hidden → clear old search context → DETECT new target with meter reset;
- old target invalid → clear promptly according to target-validity contract.

### Termination

On SearchDuration expiry:

```text
clear CurrentTarget
clear DetectionTarget if stale
clear Detection Meter
clear SearchContext
→ PATROL
```

**Hard invariant: SEARCH MUST NOT read hidden Player Transform.**

---

# 18. Listener Architecture Boundary

Listener is intentionally not fully designed here.

Required architecture:

```text
Gameplay Action
→ Runtime NoiseEvent
→ NoiseSystem
→ HearingSensor
→ HearingObservation
→ ListenerMemory
→ Listener Decision / FSM
→ Listener Planner
→ Navigation / Action
```

Shared abstractions with Stalker:

- typed observation pattern;
- memory ownership pattern;
- decision/planner/navigation separation;
- navigation robustness;
- debug snapshot contract;
- telemetry adapter boundary.

Not shared blindly:

- vision target logic;
- Detection Meter;
- Stalker SearchContext;
- patrol coverage weights;
- attack semantics.

A separate implementation document is required:

**`Listener_AI_Design_v1.0.md`**

It must define at minimum NoiseEvent semantics, propagation/range/occlusion policy if used, expiry, source priority, competing-noise selection, investigation memory, target conversion, and false-investigation behavior.

---

# 19. Warden Architecture Boundary

## 19.1 Decision

Warden is modeled primarily as a **Spatial Pressure Controller**, not as a cloned NavMesh pursuer.

Core pipeline:

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

## 19.2 FacilityGraph

FacilityGraph represents gameplay-relevant route topology:

- objective-relevant regions;
- doors/route edges;
- Main/Alternative route connectivity where useful;
- exit;
- current objective destinations;
- edge availability.

`FacilityGraph` is not the AI `RegionGraph`. They are separate graph views over overlapping physical identities. When both refer to the same physical area or door, they should reuse stable `GameplayZoneId`, `RegionId`, and `DoorId` values from shared facility spatial definitions.

Warden must not freely lock arbitrary map edges. Only designer-authorized Warden-controllable door/route points may become pressure candidates.

```text
DoorDefinition
- DoorId
- FromRegion
- ToRegion
- WardenEligible
```

Exact Warden-eligible door mapping remains:

```text
TBD — map authoring binding
```

FacilityGraph must not be forced to reuse NavMesh triangle topology or Stalker-specific patrol edge semantics.

## 19.3 Safety invariant

Before applying a Warden route modification:

```text
Candidate modification
→ simulate on FacilityGraph
→ resolve current required objective/exit target
→ reachability query
→ valid alternative route?
   ├─ YES → may continue
   └─ NO  → reject
```

**Hard invariant:** after Warden route modification, the active objective remains reachable under the fairness contract.

## 19.4 Telegraph

Telegraph is part of the action contract, not a cosmetic afterthought. The GDD requires warning before route lock so players can react.

## 19.5 Required separate document

**`Warden_Route_Pressure_Design_v1.0.md`**

It must define candidate eligibility, route-pressure scoring, telegraph timing contract, door states, interactions with Door Jammer, objective reachability query, cooldown/budget source, and fail-safe reopening behavior.

---

# 20. Multiplayer AI Authority

Project binding:

```text
Engine: Unity 6000.5.8f1
Networking Framework: Photon Fusion 2
Installed Fusion SDK: 2.1.1 Stable build 2177
Topology: Host Mode
Target: 2–4 Players
MonsterState: Host authoritative
```

In Photon Fusion 2 Host/Server Mode, the Host/Server owns **State Authority** for authoritative monster network state. Player `InputAuthority` does not imply monster decision authority. Clients must not independently tick authoritative AI decision logic.

Logical authority matrix:

| Concern | Authoritative owner | Client role |
|---|---|---|
| Vision/Hearing evaluation | Host/Server | optional debug replica only |
| Target selection | Host/Server | display replicated cues |
| FSM state | Host/Server | replicate/animate |
| Planner decision | Host/Server | none |
| Nav destination/path intent | Host/Server | replicate transform/cues |
| Attack resolution | Host/Server | presentation |
| Runtime NoiseEvent validity | Host/Server | request/emit action intent |
| Warden route action | Host/Server | render telegraph/door state |
| ScenarioConfig application | Host/Server after validation | consume replicated config-derived presentation |
| Telemetry gameplay facts | authoritative runtime emitter | optional non-authoritative local diagnostics |
| Debug UI | read-only | read-only |

## 20.1 Fusion network boundary

Keep Fusion integration at the runtime/network boundary rather than making every AI service a `NetworkBehaviour`.

```text
Photon Fusion NetworkObject / network-facing behaviour
        │
        ├── authoritative Host gate
        ├── synchronized monster state required by clients
        └── presentation/network callbacks
                 │
                 ▼
          StalkerRoot / AI services
          (pure C# where practical)
```

Fusion `NetworkObject`, `NetworkBehaviour`, `[Networked]` state, `NetworkRunner`, and RPCs are official mechanisms. This architecture does not require every AI class to inherit from a Fusion type. Persistent authoritative monster state should be synchronized as network state; RPCs are used only where transient request/event semantics are appropriate or the durable result is also represented in synchronized state.

Replicate only what clients need:

- monster transform;
- semantic state;
- animation/action cue;
- target-facing cue where gameplay presentation needs it;
- Warden telegraph/door state;
- optional reason/debug information only in development builds.

Do not replicate full private AI memory to normal clients unless required for debugging.

## 20.2 Determinism

Perfect cross-client deterministic AI simulation is unnecessary because the Fusion Host is authoritative for monster state. Deterministic/reproducible planner functions are still desirable for tests and research logs.

---

# 21. Telemetry / Profile Boundary

Canonical direction:

```text
Gameplay Runtime Facts
→ TelemetryEmitter
→ Buffer/Batch
→ Validation/Storage
→ MatchTelemetry
→ MatchScore
→ PlayerAIProfile / TeamProfile
→ decision-scoped adaptive input boundary
  (recommended: AdaptiveInputSnapshot; pending Profile/AED v1.1)
→ AED
```

Telemetry is analytical data.

Forbidden:

```text
TelemetryEvent → StalkerFSM
Telemetry DB → Listener hearing
MatchScore → ATTACK
Profile → CurrentTarget
```

## 21.1 Runtime facts vs analytical data

Example:

```text
Player Sprint
→ Runtime NoiseEvent
   ├→ Listener Hearing
   └→ TelemetryEmitter → NOISE_EMITTED
```

The runtime event has immediate gameplay semantics. The telemetry representation is evidence.

## 21.2 Decision-scoped adaptive input boundary

To resolve TeamProfile lifecycle ambiguity without inventing a persistent team identity, v1.1 requires a decision-scoped boundary between processed Profile data and the AED Input Gate.

**Recommended contract name:** `AdaptiveInputSnapshot`  
**Contract status:** recommended architecture boundary; **pending Profile v1.1 + AED v1.1 approval**.

At architecture level the boundary must represent categories such as:

```text
- current lobby composition/reference
- permitted processed Profile inputs
- completeness / availability
- relevant source / version references
```

This document does **not** freeze exact fields, formulas, eligibility rules, aggregation rules, or persistence semantics. Those belong to Profile/AED specifications.

Required dependency:

```text
Profile v1.1
+
AED v1.1
→ approve/finalize AdaptiveInputSnapshot schema
  OR an equivalent decision-scoped contract
```

The boundary must not become a long-lived invented “team personality.” If valid processed inputs are unavailable, the AED Input Gate must select the approved Fixed/Fallback behavior.

---

# 22. AED Boundary

Recommended pipeline:

```text
Decision-scoped adaptive input boundary
(recommended: AdaptiveInputSnapshot; pending Profile/AED v1.1)
→ AED Input Gate
→ Adaptive Policy
→ Candidate ScenarioConfig
→ Scenario Validator
→ Apply
   ├─ valid → Applied ScenarioConfig
   └─ invalid/unavailable → Fixed Fallback / Keep Last Valid
```

## 22.1 Allowed authority

AED may only change keys explicitly declared:

- adaptive-authorized;
- versioned;
- bounded;
- valid at the current decision point.

The existing Stalker adaptive whitelist remains valid unless M1-015 is explicitly revised through ADR/contract update.

## 22.2 Forbidden authority

AED must not:

- command ATTACK;
- command CHASE;
- command SEARCH;
- select CurrentTarget;
- select DetectionTarget;
- edit LastKnownPosition;
- edit SearchContext;
- read hidden runtime player state outside its input contract;
- add a gameplay mechanic;
- alter FSM topology;
- bypass Warden route safety validation.

## 22.3 Fixed fallback

If input is incomplete, policy unavailable, candidate invalid, or version incompatible:

- PRE_MATCH: use known-valid fixed configuration;
- allowed mid-match decision boundary: preserve last valid config unless the active contract explicitly defines another safe fallback.

## 22.4 Explainability

Every adaptive decision stores:

- input snapshot ID/hash;
- policy version;
- candidate changes;
- validation result;
- applied result;
- fallback action;
- reason code.

This is more valuable to the thesis than adding opaque model complexity.

---

# 23. GenAI Boundary

v1.1 keeps only complexity that produces P0 safety/reliability value.

Keep:

```text
Trusted Mission Facts
→ Prompt/Adapter
→ Provider
→ Validator
→ valid briefing
```

Fallback:

```text
cache if compatible
→ finite retry if configured
→ deterministic template
```

Hard rules:

- no gameplay authority;
- no monster commands;
- no ScenarioConfig generation from free-form output;
- no hidden runtime state;
- failure never blocks a correctly packaged match.

## 23.1 Defer

The following are non-core unless the thesis specifically evaluates them:

- elaborate lore generation;
- multi-stage narrative agents;
- vector-memory/RAG for runtime lore;
- autonomous tool-use agents;
- complex semantic moderation beyond the actual briefing contract.

The P0 contribution is a safe bounded integration, not a general LLM platform.

---

# 24. Observability

Observability is first-class.

## 24.1 AIDebugSnapshot

```text
AIDebugSnapshot
- MonsterId
- State
- CurrentTargetId?
- DetectionTargetId?
- LastKnownPosition?
- LastSeenDirection?
- CurrentRegionId?
- GlobalObjectiveRegionId?
- CurrentDestination?
- PathStatus
- NavigationExecutionStatus
- PlannerDecisionType
- PlannerReasonCode
- SearchCandidateId?
- SearchElapsed
- SearchCandidateCount
- StuckState
- LastRecoveryAction
- CoverageSummary
```

Monster-specific extension fields are allowed.

## 24.2 Rules

- produced from authoritative runtime state;
- immutable/read-only to UI;
- can be sampled at a debug cadence;
- not used as an input by the AI;
- may be compiled out or restricted in release.

## 24.3 Scene visualization

Useful developer visualization:

- region boundaries;
- current region;
- target region;
- SpatialGraph nodes;
- current path;
- LKP;
- LastSeenDirection;
- search candidates;
- rejected candidate reason;
- Warden graph edge lock and remaining alternative route.

---

# 25. Testing Architecture

## 25.1 Layered test plan

### Pure EditMode / unit-style

Test deterministic logic without scene runtime where possible:

- SpatialGraph adjacency utilities;
- RegionGraph connectivity;
- connected-component detection;
- coverage ranking;
- immediate-backtrack penalty;
- local candidate scoring;
- Search candidate filtering/scoring;
- Warden reachability validator;
- AED Input Gate;
- Scenario Validator;
- dependency-direction tests where practical.

### PlayMode component/integration

- Vision distance/cone/LOS;
- Stalker target-selection contract;
- DETECT meter;
- LKP update;
- SEARCH no-cheat behavior;
- NavMesh complete/partial/invalid handling;
- no-progress/stuck recovery;
- dynamic door effects;
- patrol region coverage behavior;
- Listener Runtime NoiseEvent path;
- Warden telegraph + route modification.

### Multiplayer integration

- only host/server transitions monster state;
- clients converge on replicated state/cues;
- no duplicate attack resolution;
- no client-authoritative Warden lock;
- noise requests validated authoritatively;
- reconnect/state-resync does not create independent AI.

### System / research

- telemetry facts reconstruct required AI metrics;
- Fixed/Adaptive condition trace;
- AED input/config/version reproducibility;
- GenAI provider failure fallback;
- Warden never creates objective soft-lock in tested route set.

## 25.2 Regression rule

Any change to:

- sensor semantics;
- target eligibility;
- FSM transition;
- spatial graph;
- navigation recovery;
- ScenarioConfig authority;
- route safety;

must add/update an automated test or a reproducible scenario test.

---

# 26. AI Quality Metrics

Metrics are defined now; thresholds are not invented.

## 26.1 Stalker

### RegionCoverage

```text
visited eligible reachable Regions / eligible reachable Regions
```

Report over an observation window or patrol episode with interruptions labeled.

### SpatialNodeCoverage

Micro-level navigable coverage:

```text
visited eligible reachable SpatialNodes
/
eligible reachable SpatialNodes
```

The denominator semantics must be versioned. A metric name/version may have exactly one denominator definition. Designer coverage cells must not be mixed into `SpatialNodeCoverage`.

If a future study requires geometric/cell coverage, define a separate `NavigableAreaCoverage` metric with its own protocol and denominator.

### RevisitRate

Fraction of selected destinations/regions that were revisits within the chosen evaluation window.

### ImmediateBacktrackRate

Fraction of transitions `A → B → A` unless required by topology/recovery.

### StuckRate

Count/rate of navigation attempts entering confirmed stuck recovery.

### PathFailureRate

Rejected/failed path attempts grouped by:

- Partial;
- Invalid;
- destination invalid;
- dynamic obstruction;
- no progress.

### SearchReacquisitionRate

Search episodes where Stalker legitimately reacquires a player through new VisionObservation before timeout.

Also record whether reacquisition is same target or new target through DETECT.

## 26.2 Listener

- `NoiseResponseLatency`
- `SourceSelection` distribution/reason
- `FalseInvestigationRate`

“False investigation” requires a design definition in Listener document; do not assume a threshold here.

## 26.3 Warden

- `ObjectiveReachability` after every candidate/apply;
- `InvalidLockRate`;
- `RoutePressure` using a documented graph-based metric.

Do not claim higher RoutePressure is always better.

## 26.4 Architecture vs acceptance

Metric existence is architectural. Pass/fail thresholds are evaluation/tuning artifacts established after baseline runs.

---

# 27. Performance Principles

1. Sensor work should be event/tick/interval driven when exact per-frame evaluation is not required.
2. Apply cheap broad-phase tests before raycasts.
3. Cache stable references and graph data.
4. Do not rebuild SpatialGraph every Update.
5. Recompute RegionGraph connectivity only when topology-affecting state changes.
6. Bound candidate generation.
7. Do not calculate paths for unlimited candidate sets.
8. Planner reevaluation should occur on meaningful triggers: arrival, state transition, target change, path failure, topology change, search step, or bounded refresh.
9. Telemetry should emit meaningful events, not raw per-frame positions by default.
10. Development builds may expose extra instrumentation; release builds should avoid excessive debug allocation/logging.
11. Use Unity Profiler to determine real CPU/physics/navigation/network cost before setting numerical budgets.

No v1.1 document should invent “10 Hz,” “2 ms,” or similar as a project requirement without profiling evidence.

---

# 28. Failure / Recovery Architecture

## 28.1 Navigation recovery ladder

A navigation attempt follows a bounded ladder.

```text
1. Validate destination
2. Calculate/evaluate path
3. If complete → execute
4. If partial/invalid → reject candidate
5. Try next planner candidate
6. If active path becomes stale/topology changes → repath
7. If no progress → confirm no-progress/stuck condition
8. Retry path to same logical objective if still valid
9. Select alternate local destination toward same global objective
10. Select alternate global objective if current objective became unreachable
11. Safe behavioral fallback
```

Safe behavioral fallback may be:

- fixed patrol route for PATROL;
- next valid search candidate / timeout behavior for SEARCH;
- stop/reacquire rule for other states.

## 28.2 Stuck detection

Use progress-based detection, not only velocity.

Inputs can include:

- remaining distance trend;
- position displacement;
- path state;
- agent state;
- destination age;
- repeated failed repaths.

Exact windows/thresholds are tuning data.

## 28.3 Warp/teleport

`NavMeshAgent.Warp` or equivalent teleport is **not normal recovery**.

Emergency use is only permitted if a separately documented invariant is met, such as:

- AI is irrecoverably outside legal NavMesh due to a known engine/scene defect;
- teleport target is a validated safe recovery anchor;
- action is logged;
- it cannot create unfair surprise/attack;
- it is not used to hide planner/path bugs.

For normal gameplay, reject/replan/fallback.

## 28.4 Dynamic door/obstacle

Topology-affecting door event:

```text
DoorStateChanged
→ invalidate affected route cache
→ update Region/Facility edge availability
→ active navigation checks path relevance
→ planner re-evaluates if required
```

Do not poll all doors every frame.

---

# 29. Data Flow Diagrams

## 29.1 Stalker

```text
Authoritative Player State
        │
        ▼
    VisionSensor
        │ observations
        ▼
 TargetEligibility/Selector
        │
        ▼
   StalkerMemory
        │
        ▼
      FSM
   ┌────┴───────────────┐
   │                    │
PATROL                 SEARCH
   │                    │
Global Coverage     SearchContext
Planner             + Candidate Planner
   │                    │
Local Selector       Candidate Selector
   └──────────┬─────────┘
              ▼
      NavigationController
              │
              ▼
        Runtime Movement
```

## 29.2 Listener

```text
Gameplay Action
→ Runtime NoiseEvent
→ NoiseSystem
→ HearingSensor
→ HearingObservation
→ ListenerMemory
→ Listener Rules/FSM
→ Planner
→ Navigation/Action
```

## 29.3 Warden

```text
Objective / Door / Route State
→ FacilityGraph
→ RouteAnalysis
→ Pressure Candidates
→ SafetyValidator
→ Warden Policy
→ Telegraph
→ Door/Route Action
→ FacilityGraph update
```

## 29.4 Analytics / AED

```text
Authoritative Gameplay Facts
→ Telemetry
→ Aggregation
→ MatchScore/Profile
→ decision-scoped adaptive input boundary
  (recommended: AdaptiveInputSnapshot; pending Profile/AED v1.1)
→ AED Input Gate
→ Policy
→ Candidate ScenarioConfig
→ Validator
→ Apply/Fallback
```

## 29.5 GenAI

```text
Validated ScenarioConfig
+ Designer Content Registry
→ Trusted Mission Facts
→ GenAI Adapter
→ Provider
→ Validator
→ Presentation

failure
→ compatible cache
→ finite retry
→ deterministic template
```

---

# 30. Dependency Rules

## 30.1 Allowed

```text
Sensor → Observation
Observation → TargetSelection
Observation → monster-specific Memory
TargetSelection → StalkerMemory (Stalker only)
MonsterRuntimeContext → FSM/Planner where shared context is required
monster-specific Memory → owning FSM/Planner
FSM → Planner
FSM ATTACK transition → AttackController
Planner → NavigationIntent
NavigationIntent → NavigationController
NavigationController → Movement

NavMesh → SpatialGraph
SpatialGraph → RegionGraph
RegionGraph → CoveragePlanner
CoverageMemory → CoveragePlanner

Runtime NoiseEvent → HearingSensor
Runtime NoiseEvent → TelemetryEmitter

Gameplay Runtime → Telemetry
Telemetry → Aggregation
Aggregation → Profile
Profile → decision-scoped adaptive input boundary → AED
AED → Candidate ScenarioConfig
Candidate ScenarioConfig → Validator
Validator → Applied ScenarioConfig

FacilityGraph → Warden RouteAnalysis
Warden Candidate → SafetyValidator
SafetyValidator → Door/Route Action

Authoritative Runtime → AIDebugSnapshot
```

## 30.2 One-way authority rule

A downstream analytical or presentation layer may inspect facts but cannot become an undocumented command channel back to runtime behavior.

---

# 31. Forbidden Dependencies

```text
Telemetry → Monster FSM
Telemetry → CurrentTarget
Telemetry DB → HearingSensor
Profile → ATTACK
Profile → CHASE
Profile → SEARCH
Profile → LastKnownPosition
AED → CurrentTarget
AED → DetectionTarget
AED → LastKnownPosition
AED → Search candidate
GenAI → Gameplay command
GenAI → ScenarioConfig from free-form text
GenAI → Monster FSM
Debug UI → AI Memory mutation
Client → authoritative monster state
Client → authoritative Warden route lock
Navigation → update LastKnownPosition
Sensor → choose ATTACK
Planner → choose ATTACK transition
SpatialPatrolPlanner → read hidden Player Transform
SEARCH → hidden Player Transform
```

Forbidden dependencies should be code-review checklist items.

---

# 32. Implementation Constraints

1. No Behavior Tree migration unless the six-state FSM measurably becomes difficult to maintain.
2. No GOAP for current monsters.
3. No ML/GenAI runtime monster decisions.
4. No global service locator for AI knowledge.
5. No deep inheritance tree such as `MonsterBase → VisionMonster → PatrolMonster → Stalker`.
6. Shared code is extracted only when semantics are truly shared.
7. Memory must not store authoritative world truth that the monster did not observe.
8. Planner output must be inspectable/loggable.
9. SpatialGraph/RegionGraph build must be deterministic for the same baked map data/config.
10. Candidate lists must be bounded.
11. Path queries must have explicit rejection reasons.
12. Every emergency recovery must be observable.
13. Warden action must run SafetyValidator immediately before apply.
14. ScenarioConfig change must be versioned and validated.
15. Test/debug configuration may increase observability but not change gameplay semantics invisibly.

---

# 33. Recommended Unity Component Structure

Recommended GameObject-level composition for Stalker:

```text
Stalker
├── Photon Fusion NetworkObject / network-facing binding
├── NavMeshAgent
├── StalkerRoot
├── VisionSensor
├── StalkerTargetSelector
├── StalkerDecisionController
├── StalkerNavigationBehaviour
├── StalkerAttackBehaviour
├── StalkerDebugBehaviour
└── presentation components
```

Pure C# services owned by the root:

```text
MonsterRuntimeContext
StalkerMemory
StalkerSpatialMemory
SpatialGraph
RegionGraph
CoverageMemory
GlobalPatrolPlanner
LocalPatrolSelector
StalkerSearchPlanner
NavigationProgressMonitor
```

Prefer pure C# for graph, scoring, memory, policy, and testable calculations. Use MonoBehaviours where Unity lifecycle, physics, NavMeshAgent, or scene binding is required.

## 33.1 Suggested interfaces

```csharp
public interface IPerceptionSensor<TObservation>
{
    IReadOnlyList<TObservation> Observe();
}

public interface IAIPlanner<TContext, TIntent>
{
    bool TryPlan(in TContext context, out TIntent intent);
}

public interface INavigationController
{
    NavigationPlanResult TryPlanDestination(Vector3 destination);
    NavigationExecutionStatus TickExecution();
    void Stop();
}

public interface IAIDebugSource
{
    AIDebugSnapshot CaptureDebugSnapshot();
}
```

Do not create interfaces for classes that have only one implementation unless they create a real testing or dependency boundary.

---

# 34. Recommended Folder Structure

```text
Assets/Scripts/AI/
├── Shared/
│   ├── Perception/
│   ├── Memory/
│   ├── Navigation/
│   ├── Spatial/
│   ├── Observability/
│   └── Contracts/
│
├── Stalker/
│   ├── Runtime/
│   ├── Perception/
│   ├── Decision/
│   ├── Planning/
│   │   ├── Patrol/
│   │   └── Search/
│   ├── Spatial/
│   ├── Navigation/
│   ├── Actions/
│   └── Debug/
│
├── Listener/
│   ├── Runtime/
│   ├── Hearing/
│   ├── Decision/
│   ├── Planning/
│   └── Debug/
│
├── Warden/
│   ├── Runtime/
│   ├── FacilityGraph/
│   ├── Policy/
│   ├── Validation/
│   └── Debug/
│
├── AED/
│   ├── Contracts/
│   ├── Input/
│   ├── Policy/
│   ├── Validation/
│   └── Debug/
│
└── Telemetry/
    ├── Contracts/
    ├── Emitters/
    └── Metrics/
```

Tests:

```text
Assets/Tests/AI/
├── EditMode/
│   ├── Spatial/
│   ├── Stalker/
│   ├── Warden/
│   └── AED/
└── PlayMode/
    ├── Stalker/
    ├── Listener/
    ├── Warden/
    ├── Navigation/
    └── Multiplayer/
```

---

# 35. Migration from v1.0

Migration is incremental. Do not rewrite Stalker.

## 35.1 Current implementation evidence

Current useful assets:

```text
NavMeshSpatialGraphBuilder
NavMeshSpatialGraph / SpatialNode
SpatialPatrolMemory
SpatialPatrolPlanner
StalkerNavigationController
StalkerBlackboard
StalkerController / existing six-state flow
fixed waypoint patrol fallback
```

The current controller already demonstrates composition emerging around navigation and spatial planning. v1.1 formalizes this direction.

## 35.2 Migration table

| Current class / responsibility | v1.1 action | Target responsibility |
|---|---|---|
| `NavMeshSpatialGraphBuilder` | KEEP + TEST | build local SpatialGraph |
| `NavMeshSpatialGraph` / `SpatialNode` | KEEP | local topology |
| `SpatialPatrolMemory` | MODIFY | become/underpin `CoverageMemory` with node + region stats |
| `SpatialPatrolPlanner` | SPLIT | local selection logic moves to `LocalPatrolSelector`; global objective moves to new planner |
| `StalkerBlackboard` | MODIFY | migrate shared fields to `MonsterRuntimeContext` only where semantics are truly shared; Stalker target/search knowledge goes to typed `StalkerMemory`; compatibility facade allowed temporarily |
| controller serialized target/LKP fields | MIGRATE | authoritative state moves into `StalkerMemory`; serialized debug becomes projection |
| `StalkerNavigationController` | KEEP + EXPAND | path status, progress, stuck/repath/recovery result |
| fixed waypoint patrol | KEEP AS FALLBACK | deterministic safe fallback/test path |
| `StalkerController.Tick*` | KEEP initially | gradually delegate planning/navigation/actions; controller remains FSM coordinator |
| direct debug fields | DEPRECATE gradually | generated `AIDebugSnapshot` |

## 35.3 New classes

```text
RegionDefinition / RegionGraph
RegionGraphBuilder or RegionGraphAsset
CoverageMemory
GlobalPatrolPlanner
LocalPatrolSelector
StalkerSearchContext
StalkerSearchPlanner
NavigationProgressMonitor
AIDebugSnapshot
StalkerDebugProvider
```

Avoid adding a generic `AIManager` God Class.

## 35.4 Migration order

### Step 1 — freeze current behavior with regression tests

Add tests for:

- six FSM states/transitions;
- Vision/LOS;
- LKP no-cheat;
- current local patrol planner scoring;
- fallback route;
- existing navigation arrival.

No structural change yet.

### Step 2 — upgrade NavigationController contract

Add:

- path result classification;
- progress monitor;
- stale/repath triggers;
- no-progress/stuck state;
- recovery reason.

Run:

- complete path test;
- partial path rejection;
- invalid destination;
- dynamic obstacle/door;
- no-progress recovery;
- fallback.

### Step 3 — introduce typed memory

Move genuinely shared runtime fields into `MonsterRuntimeContext` and Stalker target/detection knowledge into `StalkerMemory`, while preserving public debug properties as compatibility adapters.

Run all Stalker regression tests.

### Step 4 — introduce RegionGraph

Author canonical AI Patrol Regions for the playable Research Facility and map SpatialNodes to them. Treat the three approved Gameplay Zones as macro progression metadata, not an automatic 1:1 patrol-region segmentation.

Use designer-authored region identity with deterministic validation. Exact Unity representation and final region count remain implementation/map-authoring bindings.

Tests:

- all region IDs valid;
- expected adjacency;
- connected components;
- known doors alter only intended edges;
- all P0 patrol regions reachable in baseline map unless intentionally excluded.

### Step 5 — split patrol planner

Current:

```text
SpatialPatrolPlanner
= candidate generation + local scoring
```

Target:

```text
GlobalPatrolPlanner
→ target region

LocalPatrolSelector
→ target SpatialNode
```

Reuse current staleness/connectivity/backtrack scoring inside the local selector instead of deleting it.

Run:

- local scoring regression;
- region selection test;
- long-run deterministic simulation;
- coverage/revisit/backtrack metric collection.

### Step 6 — SearchContext + SearchPlanner

Keep SEARCH state; replace simple LKP wandering logic.

Run:

- LKP approach;
- direction-biased candidates;
- candidate rejection;
- timeout;
- same-target reacquisition;
- new-target DETECT;
- hidden-transform access negative test.

### Step 7 — observability

Replace scattered debug fields with snapshot source while maintaining Inspector convenience fields if desired.

Validate debug UI cannot mutate AI.

### Step 8 — Photon Fusion 2 Host Mode binding

Bind the existing AI root to the project's Fusion Host Mode boundary.

```text
only Host / State Authority executes authoritative:
- perception
- target selection
- AI memory mutation
- FSM
- planning
- navigation intent
- action resolution
- Warden route decisions
```

Clients receive required synchronized monster state and presentation. The networking spike demonstrates Fusion integration/configuration, `NetworkRunner`, test session creation, Host/Client connection, and Network Player spawning; it does **not** evidence monster AI networking completion.

Exact monster `[Networked]` property/RPC/simulation-callback binding remains `TBD — implementation binding` until monster network code is supplied and tested. The installed SDK build is verified, but that does not evidence monster networking completion.

Run 2/3/4-player monster-state convergence and attack/door/noise tests after binding.

## 35.5 No-rewrite principle

Do not delete working M1-026 code simply to match class names in this document. First change responsibility boundaries, then rename only when the code semantics have stabilized.

---

# 36. Architecture Decisions / ADR Candidates

| ADR | Decision needed | v1.1 recommendation |
|---|---|---|
| ADR-AI-001 | Monster runtime decision model | Six-state Stalker FSM; rule/FSM per monster; no BT migration now |
| ADR-AI-002 | Multiplayer monster authority | Host/server authoritative |
| ADR-AI-003 | Stalker spatial abstraction | SpatialGraph + RegionGraph |
| ADR-AI-004 | Region authoring | RESOLVED strategy: canonical AI Patrol Regions are designer-authored/designer-defined + deterministically validated; exact Unity representation and segmentation remain TBD |
| ADR-AI-005 | Patrol objective policy | global least-covered/stale reachable region + local selector |
| ADR-AI-006 | SEARCH knowledge | LKP/direction/observation only; hidden transforms forbidden |
| ADR-AI-007 | Navigation recovery | bounded reject/repath/replan/fallback ladder; no normal Warp |
| ADR-AI-008 | Warden model | Spatial Pressure Controller + FacilityGraph SafetyValidator |
| ADR-AI-009 | Runtime hearing | Runtime NoiseEvent pipeline separate from telemetry |
| ADR-AI-010 | Adaptive input lifecycle | decision-scoped boundary required; `AdaptiveInputSnapshot` recommended pending Profile/AED v1.1; no invented persistent team identity |
| ADR-AI-011 | Debug architecture | read-only AIDebugSnapshot |
| ADR-AI-012 | GenAI | presentation-only safety core; defer nonessential complexity |
| ADR-NET-001 | Networking framework/topology | **RESOLVED:** Photon Fusion 2 — Host Mode. Host/Server State Authority owns authoritative monster simulation; clients provide player input/requests where authorized and consume synchronized monster state/presentation. **Environment binding:** Unity 6000.5.8f1 and installed Fusion 2.1.1 Stable build 2177 are verified from the supplied project snapshot. Monster AI Fusion integration remains implementation work, not an architecture TBD. |

---

# 37. Open Issues / TBD

These are not unresolved P0 architecture boundaries; they are implementation/configuration bindings.

1. Canonical Region authoring strategy: **RESOLVED — designer-authored/designer-defined + deterministic validation**.
2. Exact Unity representation of `RegionDefinition`: TBD — implementation binding.
3. Final AI Patrol Region segmentation/count for the playable Research Facility: TBD — map authoring/validation.
4. Exact Warden-eligible `DoorId` / route-edge mapping: TBD — map authoring binding.
5. Exact tuning weights for coverage/local search scoring.
6. Stuck/no-progress numerical thresholds after profiler/playtest evidence.
7. Listener full FSM and hearing-selection contract — required separate design.
8. Warden full candidate-scoring and telegraph timing contract — required separate design.
9. Profile v1.1 + AED v1.1 must approve/finalize the recommended `AdaptiveInputSnapshot` schema or an equivalent decision-scoped adaptive input contract before live Adaptive execution.
10. Final Fixed Director config/version.
11. Performance targets after baseline profiling.
12. AI-quality acceptance thresholds after baseline collection.
13. Exact Photon Fusion monster `[Networked]` state / RPC / simulation-callback binding: TBD — implementation binding; framework/topology and installed SDK environment are already resolved/verified in Document Control and ADR-NET-001 and are not reopened by this item.

### Important adaptive readiness note

Current Profile/Test contracts indicate `TeamPerformance` is incomplete because some components are deferred. v1.1 does not fake missing values. Architecture may be baselined while **live Adaptive experiment remains gated** until valid processed inputs exist.

---

# 38. Implementation Readiness

Architecture readiness, implementation completion, and experiment readiness are tracked separately. No row below claims implementation completion without supporting evidence.

| Area | Architecture | Detailed Design | Implementation Readiness |
|---|---|---|---|
| Shared AI Architecture | BASELINED | sufficient | READY |
| Stalker | BASELINED | sufficiently specified by v1.1 + Stalker revision | READY / after migration steps |
| Spatial Region Authoring | BASELINED strategy | designer-authored + deterministic validation; exact segmentation/Unity representation TBD | AUTHORING / VALIDATION REQUIRED |
| Listener | BASELINED boundary | `Listener_AI_Design_v1.0` required | BLOCKED |
| Warden | BASELINED boundary | `Warden_Route_Pressure_Design_v1.0` required | BLOCKED |
| Multiplayer AI Authority | BASELINED | Framework/topology RESOLVED: Photon Fusion 2 Host Mode; current SDK environment VERIFIED: 2.1.1 Stable build 2177; exact monster network-state/RPC/callback binding still required | Monster AI Fusion Integration: NOT YET EVIDENCED / IMPLEMENTATION REQUIRED |
| Telemetry boundary | BASELINED | existing schema usable; transport/integration revision may still be required | PARTIAL |
| Profile → AED boundary | BASELINED | Profile v1.1 + AED v1.1 required | BLOCKED FOR LIVE ADAPTIVE |
| FixedDirector / ScenarioValidator | architecture ready | implementation contract/evidence dependent | PARTIAL |
| GenAI Mission Briefing | BASELINED safety boundary | current contract mostly sufficient | READY / LOW PRIORITY |
| Live Fixed-vs-Adaptive experiment | architecture supported | valid complete adaptive input required | NOT READY |

```text
Architecture Ready
≠
Implementation Complete
≠
Experiment Ready
```

---

# 39. Completion Criteria

v1.1 is complete when a developer can answer the following without inventing architecture:

| Question | v1.1 answer |
|---|---|
| 1. Monster AI chạy ở đâu? | Host/server authoritative runtime |
| 2. Ai authoritative? | Host/server for monster gameplay decisions |
| 3. Sensor sở hữu gì? | physical observations only |
| 4. Memory sở hữu gì? | legally known AI facts/history/context |
| 5. FSM sở hữu gì? | semantic state and transitions |
| 6. Planner sở hữu gì? | destination/action intent inside state |
| 7. Navigation sở hữu gì? | path validation/execution/progress/recovery |
| 8. Stalker khác Listener/Warden thế nào? | Vision pursuit vs hearing investigation vs facility route pressure |
| 9. Patrol đạt global coverage thế nào? | RegionGraph global objective + CoverageMemory + local selector |
| 10. SEARCH không cheat thế nào? | only LKP, LastSeenDirection, SearchContext, new VisionObservation |
| 11. Navigation failure xử lý thế nào? | classify → reject/repath/replan → bounded fallback |
| 12. Telemetry điều khiển monster? | No |
| 13. AED authority? | bounded validated ScenarioConfig only |
| 14. GenAI authority? | presentation only |
| 15. Observability? | read-only AIDebugSnapshot + visual/debug evidence |
| 16. AI quality đo thế nào? | explicit coverage/revisit/stuck/path/search/noise/route metrics |
| 17. Test thế nào? | EditMode pure logic + PlayMode + multiplayer + system/research |
| 18. Migrate code? | incremental split/reuse; no rewrite |

All P0 **top-level architecture boundaries required by this document** are resolved.

Subsystem implementation readiness is tracked separately and may remain blocked by required detailed-design documents. Implementation-specific values and separate monster design details remain TBD by design and do not require reopening the top-level architecture unless they expose a new P0 boundary conflict.

---

# 40. v1.1 Baseline Summary

The architecture baseline is:

```text
Monster Runtime
= authoritative Traditional AI
= Perception → typed Observation → Memory → FSM/Rules
  → Planner → Navigation/Action
```

For Stalker:

```text
PATROL / DETECT / CHASE / ATTACK / RECOVER / SEARCH
```

remains the semantic FSM.

The key revision is spatial:

```text
NavMesh
→ SpatialGraph
→ RegionGraph
→ CoverageMemory
→ GlobalPatrolPlanner
→ LocalPatrolSelector
→ NavigationController
```

Search becomes:

```text
LKP + LastSeenDirection + SearchContext
→ bounded candidate generation
→ complete-path filtering
→ scoring + visited memory
→ navigation
→ new VisionObservation reacquisition
→ timeout
```

with:

```text
SEARCH MUST NOT read hidden Player Transform
```

Listener:

```text
Runtime NoiseEvent
→ HearingObservation
→ Listener Memory/Decision/Planner
```

Warden:

```text
FacilityGraph
→ RouteAnalysis
→ PressureCandidate
→ SafetyValidator
→ Telegraph
→ Door/RouteAction
```

with objective reachability preserved.

Analytics:

```text
Gameplay Runtime
→ Telemetry
→ Aggregation
→ Profile
→ decision-scoped adaptive input boundary
  (recommended: AdaptiveInputSnapshot; pending Profile/AED v1.1)
→ AED
→ Candidate ScenarioConfig
→ Validator
→ Apply/Fallback
```

There is no `Telemetry → Monster command`.

GenAI remains:

```text
Trusted Facts
→ Generation
→ Validation
→ Presentation/Fallback
```

and has no gameplay authority.

The architecture explicitly adds:

- server/host authority;
- typed memory;
- global-vs-local spatial reasoning;
- navigation recovery;
- observability;
- measurable AI-quality signals;
- implementation migration seams;
- dependency and forbidden-dependency rules.

**Architecture Status: BASELINED v1.1**  
**Scope of Baseline: Top-level AI architecture and authority boundaries**  
**Implementation Status: See Implementation Readiness Matrix**

---

# 41. Final Correction Validation

```text
Unity version identifier verified/normalized: YES — 6000.5.8f1
Photon Fusion framework/topology resolved: YES
Installed Fusion SDK/build environment verified: YES — 2.1.1 Stable build 2177
Framework/topology vs SDK environment vs monster integration separated: YES
No universal MonsterMemory terminology remains: YES
Client authority wording scoped specifically to monster AI: YES
Map/Region architecture unchanged: YES
Stalker architecture unchanged: YES
No new P0 architecture blocker found: YES
```

The supplied Unity project resolves the editor-version ambiguity: `ProjectSettings/ProjectVersion.txt` reports `6000.5.8f1`, which matches Unity's official `6000.5.8f1` release and changeset. The installed Fusion SDK build is also directly observable as `2.1.1 Stable build 2177`; this is recorded as the current implementation environment, while **Monster AI Fusion Integration remains NOT YET EVIDENCED / IMPLEMENTATION REQUIRED**.

**Architecture Status: BASELINED v1.1**  
**Scope of Baseline: Top-level AI architecture and authority boundaries**  
**Implementation Status: See Implementation Readiness Matrix**

---

# 42. References

## Project sources

1. `ECHO PROTO.docx` / `ECHO PROTO(1).docx`.
2. `01_ECHO_PROTOCOL_Project_Scope_REVISED.docx`.
3. `02_ECHO_PROTOCOL_System_Architecture_REVISED.docx`.
4. `03_ECHO_PROTOCOL_Implementation_Spec_REVISED.xlsx`.
5. `04_ECHO_PROTOCOL_Project_Management_Baseline_REVISED.xlsx`.
6. `05_ECHO_PROTOCOL_Project_Plan_4P_2026_REVISED.xlsx`.
7. `AI_Architecture_Traditional_vs_Modern.md`.
8. `M1-013_Stalker_FSM_Sensor_Contracts_FINAL.md`.
9. `Telemetry_Event_Schema_v0_FINAL.md`.
10. `M1-014_Player_Team_Profile_Fields_Formulas_v0_FINAL.md`.
11. `M1-015_ScenarioConfig_AED_Fairness_Policy_v0_FINAL.md`.
12. `M1-019_GenAI_Mission_Briefing_Scope_Safety_Contract_v0_FINAL.md`.
13. `M1-020_Test_Strategy_Fixed_vs_Adaptive_Experiment_v0_FINAL.md`.
14. `KLTN.docx` — *THIẾT KẾ SƠ BỘ MAP RESEARCH FACILITY VÀ VỊ TRÍ OBJECTIVE* / Map Flow Plan v0.
15. `KLTN (1).docx` — *Chốt cách tổ chức multiplayer và đồng bộ dữ liệu trong trận*.
16. `ECHO-PROTOCOL-feature-m1-026-stalker-spike.zip` — supplied project snapshot; `ProjectSettings/ProjectVersion.txt` verifies Unity `6000.5.8f1`, and `Assets/Photon/Fusion/build_info.txt` verifies Fusion `2.1.1 Stable build 2177`.
17. Current Stalker spatial-patrol code / implementation notes including `NavMeshSpatialGraphBuilder`, `SpatialPatrolMemory`, `SpatialPatrolPlanner`, `StalkerBlackboard`, and `StalkerNavigationController`.

## External sources

18. Unity Technologies. Unity `6000.5.8f1` release notes / changeset `5cb7df797b7d`.  
    https://unity.com/releases/editor/whats-new/6000.5.8f1

19. Unity Technologies. `NavMeshPath.status` / `NavMeshPathStatus`, Unity 6 documentation.  
    https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AI.NavMeshPath-status.html

20. Photon Engine. Fusion 2 — PlayerRef / State Authority / Input Authority.  
    https://doc.photonengine.com/fusion/v2/manual/playerref

21. Photon Engine. Fusion 2 — Networked Properties & State Sync.  
    https://doc.photonengine.com/fusion/v2/manual/data-transfer/networked-properties

22. Photon Engine. Fusion 2 — RPCs / Data Transfer.  
    https://doc.photonengine.com/fusion/v2/manual/data-transfer/rpcs  
    https://doc.photonengine.com/fusion/v2/manual/data-transfer/data-transfer

23. Unity Technologies. Unity Test Framework, Unity 6 manual.  
    https://docs.unity3d.com/6000.0/Documentation/Manual/com.unity.test-framework.html

24. Unity Technologies. Unity Profiler manual.  
    https://docs.unity3d.com/Manual/Profiler.html

25. Hunicke, R., LeBlanc, M., & Zubek, R. (2004). *MDA: A Formal Approach to Game Design and Game Research*. AAAI Workshop.  
    https://aaai.org/papers/ws04-04-001-mda-a-formal-approach-to-game-design-and-game-research/

26. Yannakakis, G. N., & Hallam, J. (2009). *Real-Time Game Adaptation for Optimizing Player Satisfaction*. IEEE Transactions on Computational Intelligence and AI in Games, 1(2), 121–133. DOI: `10.1109/TCIAIG.2009.2024533`.

27. Zohaib, M. (2018). *Dynamic Difficulty Adjustment (DDA) in Computer Games: A Review*. Advances in Human-Computer Interaction, 2018, Article 5681652. DOI: `10.1155/2018/5681652`.

28. Spronck, P. H. M., Ponsen, M. J. V., Sprinkhuizen-Kuyper, I. G., & Postma, E. O. (2006). *Adaptive Game AI with Dynamic Scripting*. Machine Learning, 63, 217–248. DOI: `10.1007/s10994-006-6205-6`.

---

**End of `AI_Architecture_v1.1.md`**
