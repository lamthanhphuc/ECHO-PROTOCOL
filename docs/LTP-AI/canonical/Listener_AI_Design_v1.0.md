# ECHO PROTOCOL — Listener AI Detailed Design v1.0

**Document:** `Listener_AI_Design_v1.0.md`  
**Project:** ECHO PROTOCOL — Co-op Survival Horror Multiplayer  
**Monster:** The Listener  
**Revision:** v1.0  
**Date:** 2026-08-25  
**Parent Architecture:** `AI_Architecture_v1.1.md`  
**Parent Architecture Status:** BASELINED v1.1  
**Reference Detailed Design:** `Stalker_AI_Design_v1.1.md` — architectural patterns only, not Listener gameplay specification  
**Environment:** Unity `6000.5.8f1`; Photon Fusion `2.1.1 Stable`, build `2177`; Host Mode; 2–4 Players  
**Detailed Design Status:** BASELINED v1.0  
**Recommended Status:** BASELINED v1.0  

> This document is an implementation contract. It does not claim that Listener implementation, tuning, automated tests, multiplayer integration, profiling, or playtest evidence are complete.

---

## Statement Classification

Important decisions use the following labels.

| Classification | Meaning |
|---|---|
| **PROJECT BASELINE** | Required by the baselined architecture or an approved project/gameplay/cross-cutting contract. |
| **CURRENT IMPLEMENTATION** | Directly evidenced by supplied Listener source. No Listener runtime source is evidenced in the supplied project material at this revision. |
| **DETAILED-DESIGN DECISION** | A v1.0 implementation-level decision introduced here to make the approved Listener behavior implementable without reopening the top-level architecture. |
| **STATIC DESIGN CONFIG** | Designer-owned configuration; not adaptive and not intended to change as runtime AI state. |
| **FIXED SCENARIO CONFIG** | Scenario-owned value selected/applied through approved fixed configuration flow. |
| **ADAPTIVE-AUTHORIZED** | Explicitly whitelisted by the current AED contract. No Listener-specific parameter has this status in M1-015 v0. |
| **TUNING TBD** | Numerical value intentionally left open until playtest/profiling evidence exists. |
| **IMPLEMENTATION BINDING TBD** | Concrete class/serialization/Fusion/API binding not yet evidenced. Behavior semantics are already fixed. |
| **MAP AUTHORING TBD** | Concrete scene/map placement or door/route binding still requires playable-map authoring. |
| **ARCHITECTURE ESCALATION** | Required only if supplied evidence proves a top-level architecture invariant cannot reasonably be implemented. No escalation is required by v1.0. |

---

# 1. Document Control

| Field | Value |
|---|---|
| Architecture Status | BASELINED v1.1 |
| Detailed Design Status | BASELINED v1.0 |
| Surgical Correction Pass | LIS-DD-01 through LIS-DD-05 resolved in-place; document version remains v1.0 |
| Runtime AI model | Traditional / deterministic rule-based AI |
| Primary pressure axis | Noise / Hearing / Investigation |
| Secondary confirmation | Weak physical vision, no Detection Meter |
| Semantic FSM | `ROAM`, `INVESTIGATE`, `CHASE`, `ATTACK`, `RECOVER` |
| AI authority | Host / Fusion State Authority |
| Runtime noise authority | Host / authoritative gameplay source |
| Network topology | Photon Fusion 2 Host Mode |
| Multiplayer target | 2–4 players |
| Runtime noise telemetry | Separate `NOISE_EMITTED` analytical pipeline |
| AED authority over Listener v1.0 | None; no Listener parameter is adaptive-authorized by M1-015 v0 |
| Current Listener implementation | NOT EVIDENCED / `MON-08` marked Not Started in supplied Implementation Specification |
| Implementation completion | Not claimed |
| Test/profiler completion | Not claimed |

---

# 2. Purpose

This document converts the Listener boundary in `AI_Architecture_v1.1.md` into an implementation-level contract for M2 Feature-Complete Alpha.

A developer implementing Listener v1.0 must be able to determine without inventing behavior:

- which gameplay facts create authoritative noise;
- how duplicate noise is prevented;
- the exact runtime-noise data boundary;
- what Listener may legally know;
- how distance attenuation and occlusion work;
- how active noise expires;
- how simultaneous noise candidates are selected;
- how investigation commitment and interruption work;
- what ListenerMemory owns;
- the Listener-specific semantic FSM;
- how hearing hypotheses become confirmed player targets;
- what happens when the source point is empty;
- what constitutes a false investigation;
- how navigation failures and dynamic doors are handled;
- what runs on Host/State Authority;
- what is synchronized to proxies;
- how telemetry remains independent from hearing;
- how deterministic Listener quality metrics are calculated;
- which tests prove every P0 invariant;
- which architectural utilities can be shared with Stalker and which cannot.

Quality goals:

```text
Correct
Deterministic where appropriate
Explainable
Host-authoritative
No omniscient tracking
Noise-driven
Testable
Observable
Measurable
Maintainable
Performance-bounded
Thesis-defensible
```

---

# 3. Scope

## 3.1 In scope

- authoritative Runtime NoiseEvent generation;
- five approved noise types from Telemetry schema v1.0;
- NoiseDefinition/NoiseCatalog contract;
- exactly-once logical noise emission;
- NoiseSystem lifecycle/dedup/bounded storage;
- distance attenuation;
- wall/door occlusion;
- HearingSensor and HearingObservation;
- legal Listener information boundary;
- typed ListenerMemory;
- weak visual confirmation required by approved MON-08 evidence;
- Listener-specific semantic FSM;
- deterministic competing-noise selection;
- investigation hysteresis/interruption;
- InvestigationEpisode lifecycle;
- target conversion;
- CHASE/ATTACK/RECOVER integration required by generic approved monster contract;
- simple Listener ROAM behavior without Stalker RegionGraph/Coverage;
- NavMesh investigation planning;
- path failure/stale/no-progress/stuck handling;
- dynamic door effects on hearing evidence and navigation;
- Photon Fusion 2 Host Mode authority/binding;
- telemetry separation;
- ScenarioConfig/AED boundary;
- read-only observability;
- deterministic metrics;
- EditMode/PlayMode/Fusion tests;
- edge cases;
- class/component contracts;
- implementation order.

## 3.2 Out of scope

- Stalker Detection Meter;
- Stalker SearchContext/LKP search;
- Stalker RegionGraph/Coverage patrol;
- GlobalPatrolPlanner/LocalPatrolSelector;
- Warden route-pressure policy;
- acoustic portal graph / room impulse simulation;
- ML/RL;
- runtime LLM/GenAI decision;
- client-authoritative hearing;
- unbounded noise history;
- adaptive Listener parameter whitelist;
- P1 `MON-10` rule-based counterplay adaptation;
- final hearing ranges/coefficients/timing thresholds;
- fabricated profiler/test/playtest results;
- final map waypoint coordinates;
- final Fusion `[Networked]` field layout.

---

# 4. Source Priority / Governance

## 4.1 Authority order

For Listener gameplay/design decisions:

1. `AI_Architecture_v1.1.md`.
2. Approved gameplay/design contracts:
   - `ECHO PROTO.docx` / `ECHO PROTO(1).docx`;
   - `01_ECHO_PROTOCOL_Project_Scope_REVISED.docx`;
   - `02_ECHO_PROTOCOL_System_Architecture_REVISED.docx`;
   - `03_ECHO_PROTOCOL_Implementation_Spec_REVISED.xlsx`;
   - approved Research Facility Map Flow document;
   - approved Photon Fusion multiplayer/network document;
   - any later approved Listener-specific contract.
3. Cross-cutting contracts:
   - `Telemetry_Event_Schema_v0_FINAL.md`;
   - `M1-015_ScenarioConfig_AED_Fairness_Policy_v0_FINAL.md`;
   - `M1-020_Test_Strategy_Fixed_vs_Adaptive_Experiment_v0_FINAL.md`.
4. Current Listener implementation/source evidence, if supplied.
5. Historical spikes/notes.
6. `Stalker_AI_Design_v1.1.md` only for reusable architectural patterns.
7. Official Unity / Photon Fusion 2 documentation for engine/network factual semantics.

For engine/API factual semantics:

```text
Official Unity / Photon Fusion documentation
>
implementation assumptions
```

For project gameplay behavior:

```text
Architecture / approved contracts
>
implementation
```

## 4.2 Approved contract beats code

If future Listener source conflicts with an approved Listener/gameplay contract:

```text
classify as implementation gap / migration issue / bug
→ preserve approved behavior
→ document migration action
→ add/update regression test
→ do not silently rewrite the specification to match code
```

## 4.3 Explicit refinement of older generic monster-state wording

The revised Implementation Specification contains generic monster rows with `Patrol`, `Investigate`, `Chase`, `Search`, `Attack`, `Recover`, and `Return/FinalHunt`, while `MON-08` states that Listener is noise-focused, investigates quickly, has weaker vision, and depends on the generic monster layer.

`AI_Architecture_v1.1.md` explicitly leaves the full Listener FSM to this document and forbids blindly copying Stalker semantics.

**DETAILED-DESIGN DECISION:**

Listener v1.0 maps the approved generic behavior into the minimum Listener-specific semantic states:

```text
ROAM
INVESTIGATE
CHASE
ATTACK
RECOVER
```

Refinements:

- generic `Patrol` + `Return` are represented by `ROAM`; returning to the route is navigation behavior inside ROAM, not a separate state;
- generic `Investigate` is retained as Listener's primary sound-response state;
- generic `Search` is not a separate Listener state because noise investigation itself is the Listener's evidence-driven search behavior; Listener has no Stalker-style LKP SearchContext;
- `CHASE`, `ATTACK`, and `RECOVER` are retained because approved MON-01/MON-05 + MON-08 dependency supports them;
- `FinalHunt` is a match/configuration context, not a semantic Listener state.

This is a detailed-design refinement, not an architecture escalation.

---

# 5. Source Evidence Summary

## 5.1 Approved Listener gameplay evidence

Current GDD establishes:

- Stalker creates pressure through sight;
- Listener creates pressure through sound;
- Warden creates pressure through routes;
- Sprint increases noise;
- Crouch reduces noise;
- Sprint, selected interactions, carrying/dropping Energy Core, and Noise Maker can create noise;
- Noise Maker is a deliberate diversion tool, especially useful against Listener;
- player counterplay is cautious movement and noise management.

## 5.2 Implementation Specification evidence

Approved revised implementation baseline includes:

```text
MON-03
Noise sensor
→ NoiseEvent in range / priority / age
→ filter / rank
→ investigate valid event
→ expired/out-of-range ignored

MON-05
Attack / Recover
→ range / cooldown
→ valid hit causes Down
→ Recover prevents chain-hit
→ revive protection respected

MON-08
The Listener
→ noise-focused
→ investigates quickly
→ vision weaker
→ standing still / crouch safer
→ Host authoritative
```

`MON-08` is marked Not Started in the supplied Implementation Specification.

## 5.3 Telemetry evidence

Telemetry schema v1.0 freezes noise types:

```text
SPRINT
INTERACTION
CORE_CARRY
CORE_DROP
NOISE_MAKER
```

and `NOISE_EMITTED` fields:

```text
context.phase
context.position
data.noiseType
data.loudness
data.hearingRadius?   // conditional
```

Telemetry explicitly separates:

```text
Runtime NoiseEvent → Listener
```

from:

```text
Runtime NoiseEvent → TelemetryEmitter → NOISE_EMITTED
```

## 5.4 Multiplayer evidence

Approved networking baseline:

```text
Photon Fusion 2
Host Mode
2–4 players
Client input/action request
→ Host validation/execution
→ authoritative state
→ synchronized clients
```

Network Spec includes:

```text
NET-009 NoiseEvent
Owner: Host
Payload concept: pos, loudness, type, expiry
Consumer: AI + optional clients
```

and:

```text
NET-010 MonsterState
Owner: Host AI
Clients never run independent full monster AI.
```

---

# 6. External Research Reviewed

Only official engine/network documentation is used to validate implementation semantics.

## 6.1 Unity

Relevant contracts:

- `NavMeshAgent.SetDestination` requests/updates a destination; the resulting path may remain pending and is not automatically proof of a complete route.
- `NavMeshPathStatus` distinguishes `PathComplete`, `PathPartial`, and `PathInvalid`.
- `NavMeshAgent` exposes `pathPending`, `hasPath`, `remainingDistance`, `pathStatus`, `isPathStale`, `desiredVelocity`, and `isOnNavMesh`, which support robust movement/recovery monitoring.
- `Physics.Raycast` accepts a `LayerMask` for selective occlusion queries.
- Unity documents non-allocating physics-query variants for repeated query workloads; actual need must be profiler-driven.
- Unity Test Framework supports EditMode/PlayMode testing.
- Unity Profiler should establish actual performance budgets before numerical limits are frozen.

Official references:

- https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AI.NavMeshAgent.SetDestination.html
- https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AI.NavMeshAgent.html
- https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AI.NavMesh.CalculatePath.html
- https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AI.NavMeshPathStatus.html
- https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Physics.Raycast.html
- https://docs.unity3d.com/6000.0/Documentation/Manual/physics-optimization-raycasts-queries.html
- https://docs.unity3d.com/6000.0/Documentation/Manual/com.unity.test-framework.html
- https://docs.unity3d.com/Manual/Profiler.html

## 6.2 Photon Fusion 2

Relevant contracts:

- Host Mode is client/server topology with Host/Server holding State Authority over authoritative game state.
- Input Authority is separate and allows client input to participate in the client/server input loop.
- `FixedUpdateNetwork()` is Fusion's simulation-tick callback.
- `[Networked]` properties are appropriate for synchronized durable state.
- RPCs are appropriate for punctual events/requests but do not persist for late join/reconnect; durable effects must exist in synchronized state if reconstruction matters.

Official references:

- https://doc.photonengine.com/fusion/v2/manual/playerref
- https://doc.photonengine.com/fusion/v2/manual/input/player-input
- https://doc.photonengine.com/fusion/v2/manual/data-transfer/networked-properties
- https://doc.photonengine.com/fusion/v2/manual/data-transfer/rpcs
- https://doc.photonengine.com/fusion/v2/concepts-and-patterns/network-simulation-loop
- https://doc.photonengine.com/fusion/v2/tutorials/host-mode-basics/overview

---

# 7. Current Implementation Assessment

```text
CURRENT IMPLEMENTATION:
NOT EVIDENCED / NOT IMPLEMENTED FROM SUPPLIED LISTENER SOURCE
```

Evidence:

- no supplied `ListenerController`, `HearingSensor`, `NoiseSystem`, or Listener runtime C# implementation was found;
- revised Implementation Specification marks `MON-08 The Listener` as `Not Started`;
- networking spike proves session/Host/client/player spawning, not Listener AI networking;
- Stalker implementation exists but is not Listener gameplay source.

Therefore this document is a clean implementation contract, not a refactor description of existing Listener code.

---

# 8. Design Goals / Non-Goals

## 8.1 Goals

- sound is Listener's primary acquisition channel;
- weak vision provides legal physical player confirmation, not Stalker-style detection;
- quiet/stealth movement materially changes risk;
- Noise Maker can create deterministic diversion opportunities without guaranteed success;
- one accepted gameplay emission produces at most one logical authoritative Runtime NoiseEvent;
- hearing knowledge is event-time evidence, not live player tracking;
- simultaneous noises resolve deterministically;
- weak noise spam does not cause oscillation;
- exact investigation destinations require complete legal navigation;
- false investigations are measurable;
- proxies reconstruct presentation without simulating Listener decisions;
- all P0 behavior has explicit ownership and failure semantics.

## 8.2 Non-goals

- acoustic simulation with room impulse response;
- sound diffraction/portal graph;
- ML audio classification;
- probabilistic hidden-player inference;
- Stalker Detection Meter;
- Stalker LKP search;
- Stalker RegionGraph coverage;
- P1 repeated-counterplay adaptation;
- arbitrary client noise injection;
- telemetry-driven hearing.

---

# 9. Runtime Data Flow

## 9.1 Canonical noise flow

```text
Player input / gameplay action
        ↓
Host validates gameplay action
        ↓
Authoritative gameplay result
        ↓
NoiseEmissionResolver
        ↓
RuntimeNoiseEvent
        ↓
NoiseSystem
        ├──────────────→ TelemetryEmitter → NOISE_EMITTED
        │
        ▼
HearingSensor
        ↓
immutable HearingObservation
        ↓
Listener decision intake
        ├─ state can consume hearing now
        │      → same-tick HearingObservation batch
        │
        └─ state cannot consume a new investigation now
               especially ATTACK / RECOVER
               → ListenerMemory.PendingHearingInbox
                         ↓ next legal decision point
                 remove expired
                 + combine with current legal hearing batch
                         ↓
                 ListenerNoiseSelector
                         ↓
                 ListenerMemory / InvestigationHypothesis
                         ↓
                 ListenerFSM same-tick arbitration
                         ↓
                 ListenerInvestigationPlanner / Chase / Attack
                         ↓
                 Navigation / Action
```

**LIS-DD-01 correction:** an already produced `HearingObservation` is legal historical perception. It is never recreated by scanning an old RuntimeNoiseEvent using later Listener/door geometry.

**LIS-DD-03 correction:** semantic FSM transition is resolved once per authoritative decision step after the relevant visual, hearing, navigation, action, and timer inputs for that step have been collected. Component callback order does not define transition priority.

## 9.2 Runtime noise is not audio

```text
RuntimeNoiseEvent
= AI stimulus

AudioSource / SFX
= presentation

NOISE_EMITTED
= analytical evidence
```

No one of these substitutes for another.

---

# 10. Ownership Matrix

| Concern | Canonical owner | Writes | Reads | Must not own |
|---|---|---|---|---|
| Noise-producing action validity | authoritative gameplay action owner | accepted action result | client request/input | Listener FSM |
| Noise definition | `NoiseCatalog` / designer config | immutable definition data | emission resolver | runtime AI state |
| Logical noise identity/dedup | `NoiseSystem` + authoritative emission source | NoiseEventId / bounded reclaimable dedup state | emission key | investigation choice |
| Dedup retention policy | `NoiseSystem` | bounded replay/sequence/window state | supported authoritative duplicate/resimulation horizon | whole-match unbounded ledger |
| Runtime noise lifecycle | `NoiseSystem` | active events/expiry | validated events | Listener FSM/AED |
| Audibility | `HearingSensor` | immutable HearingObservation | RuntimeNoiseEvent + physical world at evaluation time | noise selection/FSM |
| Pending heard evidence | `ListenerMemory.PendingHearingInbox` | bounded immutable HearingObservations | HearingSensor output + current time | re-raycasting old sound / committed investigation state |
| Weak physical visibility | `ListenerVisualConfirmationSensor` | immutable visual observations | authoritative player physical state | Detection Meter/FSM |
| Competing-noise choice | `ListenerNoiseSelector` | selection/disposition reason | current HearingObservations + pending inbox + memory | NavMesh movement |
| CHASE spatial corroboration | `ListenerNoiseSelector` / decision rules | `CorroboratesVisibleTarget` disposition | hearing position + current legal visual observed position | SourcePlayerId / hidden identity |
| Hearing/investigation knowledge | `ListenerMemory` | one current hypothesis, pending inbox, bounded history, confirmed target identity | legal observations | live hidden transform |
| Same-tick semantic arbitration | `ListenerFSM` | exactly one semantic transition result per decision step | collected legal decision inputs | callback/MonoBehaviour execution order |
| Semantic state | `ListenerFSM` | `ROAM/INVESTIGATE/CHASE/ATTACK/RECOVER` | legal observations/memory/action status | raw ray/path implementation |
| Investigation intent | `ListenerInvestigationPlanner` | navigation intent | accepted hypothesis/NavMesh evaluator | target conversion |
| Navigation | shared/Listener navigation controller | path/execution/recovery status | movement intent/NavMeshAgent | noise selection/FSM |
| Target conversion | Listener decision/confirmation logic | `ConfirmedTargetId` | weak visual observation + eligibility | hidden tracking |
| Investigation terminal cleanup | `ListenerMemory` + `ListenerFSM` | terminalize/trace once/clear or atomic replacement | episode result | retaining terminal episode as active |
| Attack | `ListenerAttackController` | authoritative attack episode/result | FSM-authorized target/action config | ATTACK transition |
| Debug | `ListenerDebugProvider` | read-only snapshot | runtime projections | gameplay mutation |
| Telemetry | `ListenerTelemetryAdapter` / gameplay emitter | approved telemetry only | authoritative facts | hearing/decision feedback |
| Network binding | Listener Fusion root | synchronized presentation state | State Authority/runtime | private AI logic |

---

# 11. Runtime NoiseEvent Contract

## 11.1 Contract

**DETAILED-DESIGN DECISION**

```text
RuntimeNoiseEvent
- NoiseEventId
- EventOrderKey
- NoiseType
- WorldPosition
- EmittedAt
- Loudness
- HearingRadius
- ExpiresAt

Host-only metadata:
- AuthoritativeEmissionKey
- SourcePlayerId?
- SourceEntityId?
```

The exact C# type names are implementation bindings. Semantics are frozen.

## 11.2 Field ownership and Listener access

| Field | Purpose | Owner | Listener may read? | Mutability/lifecycle |
|---|---|---|---:|---|
| `NoiseEventId` | stable logical emission identity | Host/NoiseSystem | Yes | immutable |
| `EventOrderKey` | stable total ordering for deterministic same-tick selection; conceptually authoritative tick + emission ordinal or equivalent | Host/NoiseSystem | Yes | immutable |
| `NoiseType` | approved gameplay noise category | NoiseDefinition/emitter | Yes | immutable |
| `WorldPosition` | authoritative emission snapshot | gameplay action owner | Yes | immutable |
| `EmittedAt` | authoritative event time/tick | Host | Yes | immutable |
| `Loudness` | configured/resolved source strength | Host from NoiseDefinition + approved modifiers | Yes | immutable |
| `HearingRadius` | broad-phase maximum radius | Host from NoiseDefinition | Yes | immutable |
| `ExpiresAt` | pre-commit eligibility deadline shared by the RuntimeNoiseEvent and any derived uncommitted HearingObservation; it is not a re-hearing window | Host/NoiseDefinition | Yes | immutable |
| `AuthoritativeEmissionKey` | exactly-once dedup key | gameplay/NoiseSystem | No | Host-only immutable |
| `SourcePlayerId?` | validation/telemetry attribution | Host | **No by default** | Host-only immutable |
| `SourceEntityId?` | source/debug/dedup attribution | Host | **No by default** | Host-only immutable |

## 11.3 Event-time snapshot invariant

A RuntimeNoiseEvent must not contain:

```text
Transform
NetworkTransform reference
Player velocity reference
Player facing reference
live entity-position callback
```

`WorldPosition` is copied at emission time.

If a player emits at position `P` and later moves to `Q`:

```text
Listener legally knows P from that event
Listener does not learn Q from that old event
```

Only a new permitted observation/event can provide a new position.

---

# 12. Noise Types / NoiseDefinition

## 12.1 Approved v0 noise categories

Telemetry schema v1.0 and gameplay sources freeze:

```text
SPRINT
INTERACTION
CORE_CARRY
CORE_DROP
NOISE_MAKER
```

Do not add a new runtime category merely because a scene AudioSource exists.

## 12.2 `NoiseDefinition`

A concrete consumer exists: authoritative emitters need designer-owned loudness/range/lifetime behavior.

```text
NoiseDefinition
- NoiseType
- BaseLoudness
- HearingRadius
- Lifetime
- EmissionMode
```

`EmissionMode`:

```text
DiscreteAction
RecurringMovement
```

Exact serialization (`ScriptableObject`, table, config asset) is **IMPLEMENTATION BINDING TBD**.

## 12.3 Emission mapping

| NoiseType | Approved gameplay source | Baseline emission semantic |
|---|---|---|
| `SPRINT` | player Sprint | recurring authoritative movement emission while sprinting and not using CORE_CARRY movement category |
| `INTERACTION` | selected noisy interactions, including approved door/puzzle/terminal/object interactions where their authoring enables noise | discrete event only for an accepted interaction explicitly configured to emit noise; not every interaction is noisy |
| `CORE_CARRY` | movement while carrying Energy Core | recurring authoritative movement emission |
| `CORE_DROP` | accepted Core drop | one discrete event at authoritative drop position |
| `NOISE_MAKER` | accepted Noise Maker use | one discrete event at authoritative spawned/activated Noise Maker position |

## 12.4 Movement-category precedence

**DETAILED-DESIGN DECISION**

A single recurring movement-emission opportunity creates at most one movement noise category.

```text
if carrying Energy Core and qualifying movement:
    CORE_CARRY
else if sprinting:
    SPRINT
else:
    no v0 recurring movement NoiseEvent
```

This prevents a Sprint+Carry movement sample from creating two logical movement noises.

If the gameplay implementation later proves sprint while carrying impossible, the precedence remains harmless.

Crouch reduces noise through the approved movement/noise modifier path. Exact multiplier is **TUNING TBD**.

## 12.5 Discrete actions may coexist

A discrete action such as `CORE_DROP` may legitimately occur in the same authoritative tick as another independent emission. Exactly-once semantics apply per logical emission, not “one noise total per player per tick.”

---

# 13. Authoritative Noise Source Validation

## 13.1 Player-originated actions

```text
Client input/request
→ Host validates action
→ Host accepts gameplay mutation
→ Host resolves authoritative source position/state
→ Host resolves NoiseDefinition
→ Host creates logical emission
→ NoiseSystem accepts/publishes
```

Clients do not send authoritative:

- final noise position;
- loudness;
- hearing radius;
- expiry;
- NoiseEventId.

## 13.2 Noise Maker

```text
Client requests use/target
→ Host validates ownership/cooldown/target/use
→ Host creates/spawns approved Noise Maker result
→ Host derives authoritative source position
→ RuntimeNoiseEvent(NOISE_MAKER)
```

Noise Maker cooldown remains the existing Team Tool contract (`300s` baseline), but the Listener does not own cooldown validation.

## 13.3 Environment noise

Architecture permits Player/Environment noise sources. No concrete environment-only noise catalog is approved in the supplied baseline.

Therefore:

```text
Environment noise type/source:
IMPLEMENTATION BINDING TBD pending explicit gameplay authoring
```

Do not invent environment noise during Listener implementation.

---

# 14. Exactly-Once Noise Semantics

## 14.1 Invariant

```text
one accepted authoritative noise-producing logical emission
→ at most one authoritative RuntimeNoiseEvent
```

This must remain true under:

- duplicate client request;
- duplicate gameplay callback;
- repeated animation event;
- local + Host emission bug;
- Fusion prediction/resimulation;
- reconnect;
- presentation replay.

## 14.2 Logical mechanism

Conceptually:

```text
AuthoritativeEmissionKey
→ NoiseSystem.TryAcceptEmission(...)
→ first valid key:
     allocate stable NoiseEventId
     publish one event
→ duplicate key:
     return Duplicate
     publish nothing
```

Exact key representation is **IMPLEMENTATION BINDING TBD**.

The dedup record for that key is not required to survive the whole match. Its retention is governed by the bounded/reclaimable contract in §15.4 and must remain valid through the supported duplicate/replay/resimulation horizon.

For recurring movement, each scheduled authoritative movement emission is a separate logical emission with a separate stable key.

## 14.3 Source of emission

Client `AudioSource.Play()` or animation SFX callback is never an authoritative noise source.

Presentation may react to an already accepted gameplay event; it cannot create AI stimulus.

---

# 15. NoiseSystem

## 15.1 Responsibility

NoiseSystem owns runtime-stimulus infrastructure only:

- authoritative event acceptance;
- logical event identity;
- emission deduplication;
- **bounded/reclaimable dedup retention**;
- publication;
- short active-event lifecycle;
- expiry;
- bounded active storage;
- deterministic event ordering;
- match reset.

NoiseSystem does not own:

- HearingSensor audibility;
- Listener pending hearing memory;
- Listener FSM;
- source choice;
- target selection;
- navigation;
- attack;
- Telemetry analysis;
- AED.

## 15.2 Main operations

Conceptual operations:

```text
TryPublish(validatedEmission)
GetActiveEvents(now)              // debug/bounded processing only
Expire(now)
RetireDedupState(authoritativeReplayProgress)
ResetForMatch()
```

`NoiseSystem` assigns a stable `EventOrderKey` to every accepted event.

### Same authoritative tick batching

For deterministic competition among noises created in the same Fusion simulation tick:

```text
authoritative gameplay emissions for tick T
→ NoiseSystem validates/deduplicates/orders
→ HearingSensor evaluates accepted events at T
→ collect audible HearingObservations for T
→ Listener decision intake routes them
   to current same-tick batch or PendingHearingInbox
→ ListenerFSM/ListenerNoiseSelector consumes them
   according to same-tick arbitration
```

Events have a stable total order even when their `EmittedAt` time is equal.

Exact scheduling/component callback order is an implementation binding; the same-tick **collect-before-arbitrate semantic** is fixed.

Event subscribers receive each accepted event once.

## 15.3 Bounded active-event storage

Active RuntimeNoiseEvent storage size is **TUNING TBD**.

Overflow behavior is fixed:

```text
1 remove expired events
2 if still at capacity:
     evict oldest active event
     tie-break by stable EventOrderKey
3 emit development diagnostic
4 accept the new authoritative event
```

Reason:

- active stimulus storage cannot be unbounded;
- newest legitimate action should not disappear because older short-lived stimuli remain;
- exact capacity must be profiler/playtest driven.

Active-event eviction does **not** retroactively delete a `HearingObservation` already stored in Listener legal memory.

## 15.4 Bounded dedup retention — LIS-DD-04

**DETAILED-DESIGN DECISION / HARD INVARIANT**

NoiseSystem dedup state MUST be bounded or reclaimable.

Allowed implementation patterns include:

```text
sliding authoritative tick window
bounded per-source sequence window
compact sequence watermark
bounded replay ledger
or behaviorally equivalent mechanism
```

The detailed design does not require one collection type.

Retirement invariant:

```text
a dedup record may be retired
only after the implementation can prove that the corresponding
logical emission can no longer be replayed or reprocessed through
the supported authoritative request / callback / resimulation path
```

Therefore, semantically:

```text
dedup retention horizon
>=
maximum supported duplicate / replay / resimulation horizon
```

The exact tick/window/count representation is:

```text
IMPLEMENTATION BINDING TBD
```

because it depends on the final authoritative action binding and Fusion/request flow.

Forbidden baseline:

```text
whole-match unbounded HashSet<AuthoritativeEmissionKey>
```

for recurring `SPRINT`, `CORE_CARRY`, or other emissions.

If the implementation detects that its retention capacity/window would retire an emission while that emission can still legally replay, it must surface a development diagnostic and must not silently weaken exactly-once behavior.

## 15.5 Reset

At match end/reset:

```text
clear active events
clear all dedup state / sequence watermarks / replay ledgers
invalidate match-scoped event identities
```

No old noise or dedup record is carried into a later match.

---

# 16. Noise Lifecycle / Expiry

## 16.1 Global event lifecycle

```text
Created/Accepted
→ Active
→ Expired
→ Removed
```

`ExpiresAt = EmittedAt + configured Lifetime`.

Lifetime is **TUNING TBD** by NoiseDefinition/category.

## 16.2 Expiry semantic

An expired RuntimeNoiseEvent:

- cannot create a new HearingObservation;
- cannot start a new InvestigationEpisode;
- cannot interrupt an existing investigation;
- may remain in development history only if a bounded debug recorder intentionally stores it.

## 16.3 Committed memory survives global expiry

Distinguish:

```text
RuntimeNoiseEvent lifetime
≠
Listener InvestigationHypothesis lifetime
```

If Listener already legally committed the event into an InvestigationEpisode:

```text
event expires globally
→ current InvestigationHypothesis remains legal memory
→ investigation continues until its own terminal condition
```

The Listener is remembering a sound it actually heard, not re-reading the expired event.

## 16.4 No retroactive hearing

A Listener outside hearing range at emission does not become able to hear the historical sound merely by walking closer before `ExpiresAt`.

Baseline processing is event-driven:

```text
RuntimeNoiseEvent published at authoritative tick T
→ HearingSensor evaluates physical audibility ONCE using geometry at T
→ not audible:
     reject permanently for that emission
→ audible:
     produce immutable HearingObservation
```

Expiry is a bounded pre-commit eligibility window, not a persistent world sound volume.

## 16.5 Pending HearingObservation lifecycle — LIS-DD-01

When a legal `HearingObservation` is produced while Listener cannot immediately consume a new investigation, especially during `ATTACK` or incomplete `RECOVER`:

```text
HearingObservation
→ ListenerMemory.PendingHearingInbox
```

The inbox contains **heard observations**, not RuntimeNoiseEvents and not InvestigationEpisodes.

At the next legal hearing decision point:

```text
PendingHearingInbox
→ remove entries where now >= ExpiresAt
→ combine remaining entries with current same-tick legal HearingObservations
→ deduplicate by NoiseEventId if necessary
→ rank with normal ListenerNoiseSelector ordering
→ apply state-specific arbitration / reachability
→ selected or otherwise consumed entry is removed/marked consumed
```

Hard rules:

1. Historical `HearingObservation` geometry is never recalculated.
2. Listener movement after `HeardAt` does not change historical `Distance`, `EffectiveIntensity`, or `OcclusionClass`.
3. Door movement after `HeardAt` does not change whether that event was historically heard.
4. An inbox entry is **not** a committed `InvestigationHypothesis`.
5. If `now >= observation.ExpiresAt` before commitment, discard it; it cannot start investigation.
6. A committed InvestigationEpisode may survive source-event expiry under §16.3.
7. The inbox is bounded by `PendingHearingInboxCapacity` (**TUNING TBD**).
8. The inbox never scans NoiseSystem to “hear again” at RECOVER completion.

### Pending inbox deterministic overflow

```text
remove expired first
→ if still over capacity:
     rank all retained/new eligible observations with
     ListenerNoiseSelector's hearing ordering
→ retain highest-ranked observations up to capacity
→ stable EventOrderKey resolves all remaining ties
→ emit development diagnostic for each capacity eviction
```

Overflow ranking uses already frozen HearingObservation values. It performs no new acoustic raycast and does not create an InvestigationEpisode.

An implementation may use a different bounded data structure only if it is deterministic and behaviorally equivalent to this retention rule.

---

# 17. Hearing Propagation Model

## 17.1 Options reviewed

| Option | Benefit | Cost/problem | v1.0 |
|---|---|---|---|
| Pure radial range | cheapest | walls/doors meaningless | Reject |
| Distance attenuation | simple | still ignores facility geometry | Reject alone |
| Distance + direct occlusion attenuation | simple, explainable, supports rooms/doors | approximate acoustics | **Selected** |
| Room/portal acoustic graph | richer propagation | authoring/complexity/test burden | Defer |
| Physics/audio simulation | high fidelity | unnecessary for KLTN/M2 | Reject |

## 17.2 Selected model

**DETAILED-DESIGN DECISION**

Listener v1.0 uses:

```text
distance attenuation
+
direct wall/door occlusion attenuation
```

No sound diffraction or multi-room acoustic graph.

## 17.3 Audibility equation

Let:

```text
d = distance(ListenerHearingOrigin, Noise.WorldPosition)
R = Noise.HearingRadius
L = Noise.Loudness
D = clamp01(1 - d / R)
O = OcclusionMultiplier(OcclusionClass)
E = L * D * O
```

Audibility:

```text
R > 0
AND d <= R
AND E >= ListenerHearingThreshold
→ audible
```

Where:

- `Loudness` is a configured unitless source strength;
- `ListenerHearingThreshold` uses the same scale;
- final loudness/range/threshold values are **TUNING TBD**.

Invalid `R <= 0`, non-finite values, or invalid definition are rejected by noise validation.

This equation is deterministic and inexpensive.

---

# 18. Occlusion / Walls / Doors

## 18.1 Occlusion class

```text
CLEAR
OPEN_DOOR
CLOSED_DOOR
SOLID_WALL
QUERY_FAILED
```

## 18.2 Policy

**DETAILED-DESIGN DECISION**

```text
CLEAR / OPEN_DOOR
→ no hearing attenuation

CLOSED_DOOR
→ attenuated, not automatically hard-blocked

SOLID_WALL
→ stronger-or-equal attenuation than CLOSED_DOOR

QUERY_FAILED
→ reject audibility conservatively
```

Coefficient invariant:

```text
0 < WallMultiplier
<= ClosedDoorMultiplier
< 1
```

Exact values are **TUNING TBD**.

## 18.3 Multiple blockers

M2 uses the strongest-blocker classification on the direct source-to-listener segment.

Example:

```text
ray intersects open door + closed door + wall
→ OcclusionClass = SOLID_WALL
→ apply WallMultiplier once
```

Do not multiply attenuation per collider. Collider counts are an unreliable acoustic model and make tuning depend on scene mesh construction.

## 18.4 Query implementation

Recommended Unity binding:

- authoritative hearing origin Transform;
- direct physics ray/query toward event snapshot;
- explicit acoustic blocker `LayerMask`;
- ignore Listener self-colliders and source-owned helper collider;
- bounded/non-alloc query if profiling justifies it.

Exact LayerMask/component binding is **IMPLEMENTATION BINDING TBD**.

## 18.5 Source at doorway

Classify using authoritative door state and the physical blocker query at emission evaluation.

The implementation must avoid source/self-collider artifacts by configured query filtering/offsets; exact epsilon is **IMPLEMENTATION BINDING TBD**, not a gameplay rule.

## 18.6 Hearing is not Vision LOS

Closed Door is an absolute blocker for Stalker Vision/path under Stalker contract.

Listener hearing is different:

```text
closed door
→ sound attenuation
≠ automatic hearing rejection
```

A sufficiently loud legal event may be heard through a closed door/wall if effective intensity remains above threshold.

---

# 19. HearingSensor

## 19.1 Question answered

> What valid RuntimeNoiseEvents are physically audible to this Listener at the authoritative event-evaluation moment?

## 19.2 Does

- event age/expiry validation;
- source/listener range test;
- distance attenuation;
- physical occlusion query;
- effective-intensity calculation;
- immutable HearingObservation creation.

## 19.3 Does not

- choose which noise to investigate;
- mutate FSM;
- choose player target;
- choose NavMesh destination;
- own `PendingHearingInbox`;
- re-evaluate old HearingObservations;
- attack;
- read telemetry/Profile/AED.

## 19.4 Event-driven one-time evaluation

Preferred flow:

```text
NoiseSystem publishes accepted event for authoritative tick T
→ HearingSensor evaluates that event once using authoritative geometry at T
→ HearingObservation OR HearingRejectReason
→ audible observations for T are collected
→ decision intake:
     interruptible/consumable state → current batch
     ATTACK / incomplete RECOVER   → ListenerMemory.PendingHearingInbox
```

If the event is rejected as inaudible at T, it is permanently inaudible for that logical emission.

If the event produces a HearingObservation at T, later Listener movement or door movement does not trigger another raycast for that historical event.

Do not raycast every historical noise every frame and do not rescan old RuntimeNoiseEvents at RECOVER completion.

---

# 20. HearingObservation

```text
HearingObservation
- NoiseEventId
- EventOrderKey
- NoiseType
- ObservedNoisePosition
- EmittedAt
- HeardAt
- ExpiresAt
- Distance
- RawLoudness
- EffectiveIntensity
- OcclusionClass
```

No `SourcePlayerId` is exposed to Listener selection/memory in baseline.

`ObservedNoisePosition` is the immutable RuntimeNoiseEvent snapshot position.

`Distance`, `EffectiveIntensity`, and `OcclusionClass` are also historical perception results fixed at `HeardAt`; they are not recomputed while the observation waits in memory.

`ExpiresAt` means:

```text
latest time this uncommitted HearingObservation
may begin or interrupt into a new InvestigationEpisode
```

A HearingObservation may be consumed immediately or may temporarily live in the bounded `ListenerMemory.PendingHearingInbox`.

It remains a fact:

```text
"The Listener heard a NOISE_MAKER at P with effective intensity E at time T."
```

It is not:

```text
"The Listener knows which player is currently at Q."
```

and it is not:

```text
"a request to raycast P again later."
```

---

# 21. Listener Legal-Information Boundary

## 21.1 Host metadata vs AI knowledge

The Host may know:

- SourcePlayerId;
- SourceEntityId;
- action/request identity;
- client InputAuthority;
- telemetry userId;
- current player Transform.

Listener AI may use only:

- permitted RuntimeNoiseEvent fields;
- HearingObservation fields;
- weak legal visual observations;
- current map/navigation/door state;
- its own position/state;
- approved config.

## 21.2 Forbidden hearing knowledge

Listener hearing must not read:

```text
current hidden Player Transform
Player velocity after emission
Player facing after emission
future predicted position
TelemetryEvent
Telemetry DB
MatchScore
PlayerProfile
TeamProfile
AED decision internals
client AudioSource
client-only animation event
```

## 21.3 Moving after emission

```text
Player emits sound at P
→ Listener hears P
→ Player moves to Q silently

Listener investigates P
X→ silently retarget Q
```

If the player emits a new legal RuntimeNoiseEvent at `Q`, that new event may legally update a hypothesis or create a new investigation decision.

---

# 22. Weak Visual Confirmation

## 22.1 Why it exists

Approved Implementation Specification `MON-08` explicitly states that Listener is noise-focused with **weaker vision**, and it depends on the generic monster perception/attack baseline.

This document therefore includes weak visual confirmation, but not the Stalker vision-target model.

## 22.2 `ListenerVisualConfirmationSensor`

Produces immutable physical observations using:

```text
authoritative candidate players
→ weak Listener distance/FOV envelope
→ occlusion/LOS
→ ListenerVisualObservation[]
```

No Detection Meter.

Conceptual observation:

```text
ListenerVisualObservation
- PlayerId
- ObservedPosition
- ObservedAt
- Distance
```

## 22.3 Weakness relative to Stalker

Exact Listener vision range/angle and approved crouch/light visibility modifiers are **TUNING TBD / IMPLEMENTATION BINDING TBD**.

Behavior invariant:

```text
Listener visual acquisition envelope
must be intentionally weaker than Stalker's sight-dominant envelope
under the approved configuration baseline.
```

No numeric ratio is invented.

## 22.4 Target eligibility

Player eligibility is separate from physical visibility.

At minimum:

- Eliminated/Spectator is not target eligible;
- Downed handling follows the shared Player/Life-State monster target contract;
- revive protection must be respected for attack consequence.

Exact shared eligibility component name is **IMPLEMENTATION BINDING TBD**.

---

# 23. Target Conversion

## 23.1 Noise hypothesis is not a player target

```text
HearingObservation
→ InvestigationHypothesis
X→ ConfirmedTarget automatically
```

Source metadata never converts by itself.

## 23.2 Conversion rule

**DETAILED-DESIGN DECISION**

A player becomes `ConfirmedTargetId` only from a new legal weak visual observation:

```text
eligible ListenerVisualObservation
→ if CurrentInvestigation exists:
     terminalize CurrentInvestigation = PlayerConfirmed
     emit terminal trace/metric source once
     archive immutable bounded history if enabled
     clear CurrentInvestigation
→ set ConfirmedTargetId
→ CHASE
```

No Detection Meter.

When multiple previously unconfirmed players are visible, select nearest eligible visible player with stable PlayerId tie-break.

The player confirmed visually need not match any Host-only noise source identity.

## 23.3 Target retention

In CHASE:

```text
ConfirmedTarget remains valid
only while target remains gameplay-eligible
AND a current legal weak visual observation exists.
```

A different visible player does not steal the current confirmed target while current target remains visible/eligible.

## 23.4 LOS loss — no resurrection of terminal investigation

On visual loss:

```text
clear ConfirmedTargetId immediately
do not store a visual LastKnownPosition for pursuit
do not reopen a terminal PlayerConfirmed InvestigationEpisode
```

Then evaluate only currently legal hearing evidence:

```text
current same-tick HearingObservations
+
unexpired PendingHearingInbox
→ normal selection / reachability
→ candidate can commit?
   ├─ YES → create NEW InvestigationEpisode → INVESTIGATE
   └─ NO  → ROAM
```

A prior episode that ended `PlayerConfirmed` is terminal and cannot silently become active again.

This is intentionally different from Stalker.

## 23.5 No Stalker LKP inheritance

Listener v1.0 does not create:

```text
LastKnownPosition
LastSeenDirection
StalkerSearchContext
SEARCH state
```

from weak vision.

Its investigation position comes only from legal hearing evidence used to create the currently active InvestigationEpisode.

---

# 24. ListenerMemory

## 24.1 Canonical mutable source

```text
ListenerMemory
- CurrentInvestigation?
- ConfirmedTargetId?
- PendingHearingInbox
- RecentNoiseDecisionHistory
- RecentInvestigationEpisodeHistory?
- LastHeardObservation?
```

No generic dictionary blackboard.

`ListenerMemory` is the sole owner of pending heard evidence and the sole owner of the current active investigation.

## 24.2 `InvestigationHypothesis`

```text
InvestigationHypothesis
- InvestigationEpisodeId
- RootNoiseEventId
- CurrentSupportingNoiseEventId
- NoiseType
- InvestigationPosition
- AcceptedAt
- LastSupportedAt
- CommittedEffectiveIntensity
- ArrivalReached
- ArrivalListenStartedAt?
- Outcome?
```

Exact field names may change; semantic ownership may not.

## 24.3 `PendingHearingInbox`

**LIS-DD-01 contract**

```text
PendingHearingInbox
= bounded collection of immutable legal HearingObservations
  that were heard but have not become a committed InvestigationEpisode
```

The inbox:

- stores no RuntimeNoiseEvent references;
- stores no live player object/Transform;
- is bounded by `PendingHearingInboxCapacity` (**TUNING TBD**);
- discards expired observations before selection/overflow comparison;
- deterministically retains the highest-ranked observations on overflow;
- never recalculates hearing geometry;
- removes or marks an observation consumed once it is used to create/merge/interrupt an InvestigationEpisode or is otherwise definitively consumed by state policy.

An observation may never simultaneously exist as two independently mutable AI records.

## 24.4 Recent decision history

Bounded history supports:

- duplicate event suppression at Listener decision level;
- debugging;
- metrics;
- repeated related-noise merge reasoning.

It stores event IDs/reasons, not live Player objects.

History bound is **TUNING TBD**.

## 24.5 Investigation episode history

Historical terminal evidence, when enabled, is:

```text
bounded
immutable
diagnostic / metric-oriented
```

It is not another active investigation store.

A terminal episode can be copied/projected into this history only after its terminal result is fixed.

History capacity is **TUNING TBD** if implemented.

## 24.6 ConfirmedTarget

`ConfirmedTargetId` stores identity only while legal visual confirmation/eligibility rules permit.

It does not store or expose a hidden live position.

## 24.7 CurrentInvestigation lifecycle invariant — LIS-DD-05

```text
CurrentInvestigation == null
OR
CurrentInvestigation refers to exactly one non-terminal InvestigationEpisode
```

On any terminal outcome:

```text
terminalize once
→ emit terminal trace/metric source once
→ optionally archive immutable bounded history
→ clear CurrentInvestigation
```

For `InterruptedByHigherPriorityNoise`, the old episode is terminalized/cleared before or atomically with installation of the new episode; there is never an observable state with two active episodes.

## 24.8 No duplicate mutable store

`ListenerNoiseSelector`, `InvestigationPlanner`, navigation, debug, metrics, and Fusion binding do not maintain competing authoritative copies of:

- `CurrentInvestigation`;
- `PendingHearingInbox`;
- `ConfirmedTargetId`.

`ListenerMemory` is canonical.

---

# 25. Semantic Listener FSM

## 25.1 State set

```text
ROAM
INVESTIGATE
CHASE
ATTACK
RECOVER
```

No other semantic state exists in Listener v1.0.

## 25.2 Why each state exists

| State | Player-visible semantic | Why separate |
|---|---|---|
| `ROAM` | Listener moves/listens without committed stimulus | default pressure mode |
| `INVESTIGATE` | Listener visibly responds to a specific heard sound | core Listener identity/counterplay |
| `CHASE` | Listener has direct weak visual confirmation of an eligible player | confirmed player pressure differs from noise hypothesis |
| `ATTACK` | authoritative attack action is in progress | generic monster attack contract |
| `RECOVER` | mandatory post-attack recovery prevents chain-hit | generic MON-05 fairness |

Not states:

```text
SelectNoise
MoveToNoise
ArrivalListen
Repath
NoProgress
StuckRecovery
ReturnToRoute
FinalHunt
```

These are planner/navigation/config phases.

---

# 26. FSM Transition Contract

## 26.1 Transition table

| From | Guard | Side effect | To |
|---|---|---|---|
| ROAM | legal eligible weak visual player exists | set ConfirmedTargetId | CHASE |
| ROAM | selector commits reachable legal HearingObservation | create new InvestigationEpisode | INVESTIGATE |
| INVESTIGATE | eligible weak visual player exists | terminalize current episode `PlayerConfirmed`, trace once, clear CurrentInvestigation, set ConfirmedTargetId | CHASE |
| INVESTIGATE | stronger qualifying unrelated noise interrupts | terminalize/trace old episode, replace with one new episode | INVESTIGATE |
| INVESTIGATE | related noise merges | update same active hypothesis/position/intensity legally | INVESTIGATE |
| INVESTIGATE | navigation becomes terminally unavailable | terminalize `NavigationFailed`, trace, clear; commit another legal hearing candidate if available | INVESTIGATE or ROAM |
| INVESTIGATE | false-investigation terminal | terminalize/trace, clear CurrentInvestigation | ROAM |
| CHASE | current target invalid or lacks legal current visual observation | clear ConfirmedTarget; evaluate current/pending legal hearing evidence for a **new** episode | INVESTIGATE or ROAM |
| CHASE | current target valid + visible + in attack range | begin authoritative attack episode | ATTACK |
| CHASE | qualifying spatially separated diversion noise meets chase interrupt contract | clear ConfirmedTarget; create new InvestigationEpisode | INVESTIGATE |
| ATTACK | authoritative Hit Moment/attack attempt resolves | enter mandatory recovery; heard observations remain pending-only | RECOVER |
| RECOVER | recovery incomplete | no semantic transition | RECOVER |
| RECOVER | recovery complete + legal current visual confirmation | set/retain ConfirmedTarget; hearing remains uncommitted | CHASE |
| RECOVER | recovery complete + best legal pending/current hearing candidate can commit | clear stale target; remove selected pending observation; create new episode | INVESTIGATE |
| RECOVER | recovery complete + no target/noise | clear stale target; no CurrentInvestigation | ROAM |

ATTACK/RECOVER do not get interrupted by noise before their gameplay sequence completes.

Hearing remains physically active; audible observations are preserved in the bounded PendingHearingInbox according to §16.5/§24.3.

## 26.2 FSM Same-Tick Transition Arbitration — LIS-DD-03

**HARD DETERMINISM CONTRACT**

A Listener decision step collects the relevant authoritative inputs first:

```text
current semantic state
current legal weak visual observations
current same-tick HearingObservation batch
unexpired PendingHearingInbox
navigation terminal/progress status
attack/recovery status
state timers / ArrivalListen terminal condition
```

Then the FSM resolves **at most one semantic transition** for that authoritative decision step.

The result must not depend on:

- C# `if` statement incidental order;
- component callback arrival order;
- table-row order;
- MonoBehaviour Script Execution Order;
- the order in which independent sensor components happened to report within the same authoritative tick.

Lower-priority inputs do not trigger a second semantic transition in the same decision step.

For hearing inputs that lose a same-step arbitration branch:

- if the decision step began in `ATTACK` or `RECOVER`, they follow PendingHearingInbox rules because the state was non-interruptible when they were heard;
- otherwise, a lower-priority current same-tick HearingObservation is considered consumed by that state decision unless an explicit merge/interrupt rule consumed it differently; it is not silently converted into a second next-tick reaction;
- an already-existing pending observation that was not selected remains pending only while unexpired and not consumed by state policy.

This prevents same-tick priority from being defeated by an implicit second transition one frame/tick later.

### ROAM priority

```text
1. legal eligible weak visual confirmation
      → CHASE

2. otherwise best reachable legal hearing candidate
      from current batch + unexpired pending inbox
      → create InvestigationEpisode → INVESTIGATE

3. otherwise
      → remain ROAM
```

Direct physical confirmation wins over hearing hypothesis.

### INVESTIGATE priority

```text
1. legal weak visual confirmation
      → terminal current episode = PlayerConfirmed
      → trace once
      → clear CurrentInvestigation
      → set ConfirmedTargetId
      → CHASE

2. qualifying stronger unrelated reachable noise
      → terminal old episode = InterruptedByHigherPriorityNoise
      → trace/clear old
      → create one new episode
      → remain INVESTIGATE

3. related supporting noise
      → merge/update current episode
      → remain INVESTIGATE

4. navigation terminal failure
      → terminal current episode = NavigationFailed
      → trace/clear
      → optionally commit one new legal candidate
      → INVESTIGATE or ROAM

5. ArrivalListen terminal condition
      → terminal current episode = FalseInvestigation
      → trace/clear
      → ROAM

6. otherwise
      → remain INVESTIGATE
```

A qualifying related or interrupting hearing observation received on the same tick that `ArrivalListenDuration` would otherwise complete is processed **before** FalseInvestigation terminalization.

### CHASE priority

```text
1. ConfirmedTarget invalid
   OR no legal current visual observation for ConfirmedTarget
      → clear ConfirmedTargetId
      → evaluate current/pending legal hearing evidence
      → if candidate commits: NEW InvestigationEpisode → INVESTIGATE
      → else: ROAM

2. target valid + visible + within attack-entry conditions
      → ATTACK

3. qualifying spatially separated diversion noise
      → clear ConfirmedTargetId
      → create NEW InvestigationEpisode
      → INVESTIGATE

4. otherwise
      → remain CHASE
```

For a valid visible in-range target and a qualifying diversion noise in the same authoritative tick:

```text
ATTACK entry wins
```

This avoids cancelling an already achieved close-range attack opportunity with same-tick sound.

### ATTACK priority

```text
authoritative AttackEpisode lifecycle / Hit Moment resolution
has priority.

Noise never transitions ATTACK.
```

Legal heard observations are routed only to `PendingHearingInbox`.

### RECOVER priority

While recovery is incomplete:

```text
remain RECOVER
```

At the exact legal recovery-completion decision step:

```text
1. legal current visual confirmation
      → CHASE

2. otherwise best eligible reachable observation
      from unexpired PendingHearingInbox + current same-tick hearing
      → remove selected pending item if present
      → create NEW InvestigationEpisode
      → INVESTIGATE

3. otherwise
      → ROAM
```

The old RuntimeNoiseEvent is never re-read or re-raycast during this arbitration.

---

# 27. Competing Noise Selection

## 27.1 Input

At a legal hearing decision point:

```text
current same-tick unexpired HearingObservations
+
unexpired ListenerMemory.PendingHearingInbox
+
ListenerMemory.CurrentInvestigation?
+
current FSM state/commitment
```

The union is deduplicated by `NoiseEventId` before ranking if an implementation path could expose the same immutable observation through both inputs.

HearingSensor does not rank.

## 27.2 Baseline deterministic ordering

**DETAILED-DESIGN DECISION**

Rank hearing candidates lexicographically by their immutable heard-time values:

1. higher `EffectiveIntensity`;
2. newer `EmittedAt`;
3. shorter `Distance`;
4. stable lower/earlier `EventOrderKey`.

No unseeded randomness.

Historical pending observations retain the same rank inputs they had at `HeardAt`; ranking does not re-sample Listener position or door geometry.

## 27.3 Source identity is not required for ranking

Listener v1.0 does not use `SourcePlayerId` to decide whether two hearing observations came from the same player.

Repeated legal emissions are handled by:

```text
same logical gameplay emission
→ NoiseSystem deduplication

different legal emissions near the current investigation hypothesis
→ spatial related-noise merge (§28.2)

hearing near current legal visual target during CHASE
→ spatial corroboration (§28.4)
```

This prevents source metadata from becoming a hidden-player tracking channel.

## 27.4 Reachability

Selection is two-stage to bound NavMesh work:

```text
hearing ranking
→ take bounded top candidates
→ InvestigationPlanner path-validates in rank order
→ first legal PathComplete candidate becomes commit candidate
```

Top candidate being unreachable does not suppress the next legal candidate.

Maximum path-validated candidates per decision is **TUNING TBD**.

A pending observation that is selected and then proves non-committable under the current explicit planner/failure rule is handled as consumed/rejected for that decision path; the implementation must not re-raycast the sound to manufacture a different HearingObservation.

## 27.5 Noise category priority

No hidden category weight is added in v1.0.

Noise Maker usefulness comes from its authored loudness/range and legal placement, not a hardcoded “always select NOISE_MAKER” rule.

This preserves the project requirement that Noise Maker is useful without guaranteeing 100% diversion.

---

# 28. Noise Priority / Hysteresis / Interrupt

## 28.1 Why hysteresis exists

Without commitment, repeated small noises could produce:

```text
A → B → A → B
```

every event, creating oscillation and poor counterplay.

## 28.2 Related-noise merge

A new HearingObservation is a related support event when its position is within `HypothesisMergeRadius` of the current active investigation hypothesis.

```text
related legal event
→ do not create a new FSM state
→ keep same InvestigationEpisode
→ update CurrentSupportingNoiseEventId
→ update InvestigationPosition to the new legal event snapshot
→ LastSupportedAt = HeardAt
→ CommittedEffectiveIntensity = max(old, new) or configured retained commitment rule
→ consume/remove that HearingObservation from pending/current decision input
→ replan if destination meaningfully changed
```

`HypothesisMergeRadius` is **TUNING TBD**.

This legal update may follow a moving player only when that player actually emits new sound.

## 28.3 Unrelated noise during INVESTIGATE

A reachable unrelated candidate interrupts only when:

```text
candidate.EffectiveIntensity
>=
CurrentInvestigation.CommittedEffectiveIntensity
+ InvestigationInterruptMargin
```

`InvestigationInterruptMargin` is **TUNING TBD**.

Equal/weaker candidate does not interrupt.

On interruption the old episode terminalizes and clears before/atomically with creation of the new episode; episodes never stack.

## 28.4 CHASE spatial corroboration — LIS-DD-02

A current legal weak visual observation of `ConfirmedTargetId` supplies:

```text
currentVisualPosition =
ListenerVisualObservation.ObservedPosition
```

For each legal HearingObservation considered as a CHASE diversion:

```text
dCorroboration =
distance(
    HearingObservation.ObservedNoisePosition,
    currentVisualPosition
)
```

If:

```text
dCorroboration <= ChaseCorroborationRadius
```

then:

```text
NoiseSelectionReason = CorroboratesVisibleTarget
→ hearing remains physically valid
→ it cannot divert CHASE
→ remain CHASE unless a higher-priority FSM rule applies
```

`ChaseCorroborationRadius` is **TUNING TBD**.

This is deliberately **spatial evidence only**.

Forbidden for corroboration:

```text
SourcePlayerId
SourceEntityId
hidden Player Transform identity
telemetry userId
```

Therefore a Noise Maker or another player's sound that happens to be spatially near the currently visible target is also treated as corroborating and does not divert CHASE.

A corroborating observation is considered consumed for CHASE-diversion purposes and is not saved merely to trigger a later oscillation after visual loss.

## 28.5 Spatially separated noise during CHASE

If:

```text
dCorroboration > ChaseCorroborationRadius
```

normal diversion rules apply.

A candidate may interrupt CHASE only when all are true:

```text
observation unexpired
AND physically heard already
AND spatially separated from current visual target
AND reachable with PathComplete investigation plan
AND candidate.EffectiveIntensity >= ChaseNoiseInterruptThreshold
AND same-tick CHASE arbitration does not select a higher-priority transition
```

On interrupt:

```text
clear ConfirmedTargetId
→ create NEW InvestigationEpisode for noise
→ INVESTIGATE
```

Threshold is **TUNING TBD**.

This is deterministic, not probability-based.

## 28.6 ATTACK / RECOVER pending hearing — LIS-DD-01

Noise does not cancel ATTACK or shorten RECOVER.

HearingSensor remains active.

```text
audible HearingObservation during ATTACK / incomplete RECOVER
→ store immutable observation in ListenerMemory.PendingHearingInbox
→ do not create InvestigationEpisode yet
```

At recovery completion:

```text
remove expired pending observations
→ combine with current same-tick hearing
→ apply RECOVER arbitration from §26.2
```

Do not scan historical RuntimeNoiseEvents and do not re-run occlusion geometry.

## 28.7 No fake-noise reliability adaptation in v1.0

Historical/generic planning includes a P1 rule-based adaptation concept that may reduce repeated fake-noise reliability while preserving a non-zero counterplay floor.

That behavior is **not part of Listener v1.0 P0 detailed design**.

Baseline v1.0 response remains deterministic from:

```text
audibility
+ reachability
+ current commitment
+ spatial corroboration
+ interrupt policy
+ same-tick FSM priority
```

Do not add probability-based Noise Maker immunity during implementation of this document.

---

# 29. InvestigationEpisode Lifecycle

## 29.1 Creation

An InvestigationEpisode starts only after:

```text
HearingObservation
→ selected
→ still unexpired
→ destination resolved
→ PathComplete confirmed
→ commitment accepted
```

An out-of-range/expired/unreachable event rejected before commitment is not an InvestigationEpisode.

A `PendingHearingInbox` entry is also not an InvestigationEpisode until commitment completes.

## 29.2 Active lifecycle

```text
Committed
→ Navigating
→ ArrivalListen
→ terminal outcome
```

Internal navigation phases do not become FSM states.

At most one active InvestigationEpisode exists per Listener.

## 29.3 Terminal outcomes

```text
PlayerConfirmed
FalseInvestigation
InterruptedByHigherPriorityNoise
NavigationFailed
CancelledByMatchEnd
CancelledByListenerDisable
```

These outcomes support metrics and debugging.

## 29.4 Terminal cleanup contract — LIS-DD-05

A terminal InvestigationEpisode must never remain as `ListenerMemory.CurrentInvestigation`.

| Terminal result | Required atomic lifecycle |
|---|---|
| `PlayerConfirmed` | set terminal outcome → emit terminal trace/metric source once → optionally archive immutable bounded history → clear `CurrentInvestigation` → set `ConfirmedTargetId` → CHASE |
| `FalseInvestigation` | set terminal outcome → trace once → optional immutable archive → clear `CurrentInvestigation` → ROAM |
| `InterruptedByHigherPriorityNoise` | terminalize/trace/archive OLD → clear OLD → create exactly one NEW episode from the new committed hearing evidence → INVESTIGATE |
| `NavigationFailed` | terminalize/trace/archive → clear old episode → if another legal hearing observation can commit, create NEW episode → INVESTIGATE; otherwise ROAM |
| `CancelledByMatchEnd` | terminalize/cancel once → trace as configured → clear `CurrentInvestigation` |
| `CancelledByListenerDisable` | terminalize/cancel once → trace as configured → clear `CurrentInvestigation` |

Implementation may perform old-clear/new-install as one atomic memory mutation, but the observable invariant is:

```text
active InvestigationEpisode count <= 1
```

and never:

```text
terminal episode == CurrentInvestigation
```

## 29.5 Terminal trace exactly once

Every InvestigationEpisode owns a terminal-resolution guard or equivalent logic:

```text
one episode
→ at most one terminal outcome
→ at most one terminal metric/debug trace
```

Metric evaluators read terminal traces/immutable episode history, not a terminal episode left in active memory.

## 29.6 No terminal resurrection

After `PlayerConfirmed`:

```text
CurrentInvestigation = null
```

If CHASE later loses legal visual confirmation:

```text
do not resume the old PlayerConfirmed episode
→ only current same-tick HearingObservations
   or unexpired PendingHearingInbox entries
   may create a NEW InvestigationEpisode
```

---

# 30. Investigation Planner

## 30.1 Input

```text
InvestigationHypothesis
+ authoritative Listener position
+ NavMesh
+ door/topology state
```

## 30.2 Destination resolution

Primary semantic destination is the heard `InvestigationPosition`.

Because an emission point can be off NavMesh:

```text
exact point on valid Listener NavMesh?
├─ YES → evaluate exact point
└─ NO  → bounded projection to nearest legal Listener NavMesh point
```

Projection bound is **TUNING TBD**.

The projection is navigation accommodation only. It does not change the remembered sound position.

Debug retains both:

```text
HeardPosition
ResolvedNavigationDestination
```

## 30.3 Path rule

Normal investigation requires:

```text
PathComplete
```

`PathPartial` and `PathInvalid` are not success.

## 30.4 Unreachable source

If top-ranked audible noise cannot resolve a complete path:

```text
reject as investigation candidate
→ reason NoiseUnreachable / path reason
→ try next bounded ranked candidate
```

Do not start an InvestigationEpisode for the rejected candidate.

---

# 31. Investigation Navigation

## 31.1 Plan status

Reuse shared navigation semantics:

```text
DestinationInvalid
AgentUnavailable
PathComplete
PathPartial
PathInvalid
```

## 31.2 Execution status

```text
Idle
Moving
Arrived
RepathPending
NoProgress
Stuck
Failed
```

## 31.3 `SetDestination` rule

A successful `NavMeshAgent.SetDestination()` request is not proof of a complete path.

For exact investigation:

```text
validate/project destination
→ evaluate path
→ require PathComplete
→ execute
→ monitor pathPending/pathStatus/progress
```

## 31.4 Partial path

```text
PathPartial
→ reject exact destination
→ do not call it Arrived
→ do not classify as false investigation
```

A false investigation is an evidence outcome, not a navigation failure.

---

# 32. Investigation Arrival

## 32.1 Arrival

On physical arrival at the resolved investigation destination:

```text
CurrentInvestigation.ArrivalReached = true
→ begin ArrivalListen phase
```

`ArrivalListen` is not an FSM state.

## 32.2 ArrivalListen behavior

For bounded `InvestigationArrivalListenDuration`:

- stop/slow according to presentation;
- continue HearingSensor;
- continue weak visual confirmation;
- related noise can merge/retarget same episode;
- qualifying stronger unrelated noise can interrupt;
- no hidden position sampling.

Duration is **TUNING TBD**.

Same-tick priority is fixed by §26.2:

```text
visual confirmation
> stronger unrelated interrupt
> related support/merge
> navigation terminal failure
> ArrivalListen false terminal
```

Therefore a legal related/supporting noise on the tick when ArrivalListen would otherwise expire is processed before FalseInvestigation.

## 32.3 Player confirmation

Any legal weak visual confirmation during INVESTIGATE:

```text
terminalize CurrentInvestigation = PlayerConfirmed
→ emit terminal trace once
→ optionally archive immutable bounded history
→ clear CurrentInvestigation
→ set ConfirmedTargetId
→ CHASE
```

The player need not be the Host metadata `SourcePlayerId`; Listener is reacting to what it physically confirms.

---

# 33. False Investigation

## 33.1 Exact definition

**DETAILED-DESIGN DECISION**

An InvestigationEpisode is `FalseInvestigation` when all are true:

```text
1. episode was legally committed from a HearingObservation;
2. Listener reached the resolved investigation destination;
3. ArrivalListenDuration completed;
4. no eligible player was legally visually confirmed during the episode;
5. no related qualifying new noise extended/retargeted the hypothesis before terminal resolution;
6. episode was not interrupted, navigation-failed, match-ended, or disabled.
```

Same-tick arbitration is part of condition 5: a qualifying related or interrupting noise received on the terminal tick is processed before false terminalization.

Then:

```text
terminal outcome = FalseInvestigation
→ emit terminal trace/metric source once
→ optionally archive immutable bounded history
→ clear CurrentInvestigation
→ ROAM
```

## 33.2 What is not false investigation

Not counted as false:

- noise rejected before commitment;
- pending observation expiring before commitment;
- PathPartial/PathInvalid;
- door makes route unreachable;
- interrupted by stronger noise;
- match ends;
- Listener disabled;
- player is legally confirmed.

This removes subjective labeling and keeps navigation/pending-expiry failures out of `FalseInvestigationRate`.

---

# 34. Target CHASE

## 34.1 Source of target position

CHASE reads only current legal `ListenerVisualObservation` of `ConfirmedTargetId`.

```text
visual observation
→ current chase destination
```

It does not read a hidden target Transform after visual loss.

## 34.2 No visual search memory / no episode resurrection

When weak vision is lost:

```text
clear ConfirmedTargetId
→ no visual LKP
→ no Listener SEARCH state
→ do not reopen any terminal PlayerConfirmed InvestigationEpisode
```

Then:

```text
current same-tick legal HearingObservations
+
unexpired PendingHearingInbox
→ normal selector / reachability
→ if candidate can commit:
     create NEW InvestigationEpisode
     → INVESTIGATE
→ otherwise:
     ROAM
```

Historical terminal investigation history is diagnostic evidence only.

## 34.3 New noises and spatial corroboration

A legal noise near the current legal visual target is classified by §28.4:

```text
distance(noise position, current visual target observed position)
<= ChaseCorroborationRadius
→ CorroboratesVisibleTarget
→ cannot divert CHASE
```

This uses no `SourcePlayerId`.

Only a spatially separated noise may enter normal CHASE diversion rules.

## 34.4 Same-tick attack vs diversion

If, in the same authoritative decision step:

```text
target remains valid + visible + attack-entry range
AND
a qualifying separated diversion noise exists
```

then:

```text
ATTACK entry wins
```

per §26.2.

---

# 35. Listener Attack / Recover

## 35.1 Source basis

Approved generic `MON-05` requires:

- attack has range/cooldown/recovery behavior;
- valid hit causes Down;
- Recover prevents chain-hit;
- authoritative revive protection is respected.

Listener does not inherit Stalker's M1-013 Hit Moment rules.

## 35.2 ATTACK entry

```text
CHASE
+ ConfirmedTargetId valid
+ current legal weak visual confirmation
+ within configured Listener attack range
→ FSM enters ATTACK
```

If a valid attack-entry condition and diversion noise occur on the same decision step, ATTACK entry wins by §26.2.

## 35.3 Authoritative episode

Use the same cross-monster reliability pattern as Stalker, not Stalker gameplay semantics:

```text
one authoritative ATTACK entry
→ one AttackEpisode identity
→ one Hit Moment resolution maximum
→ Down application at most once
→ one authoritative outcome maximum
→ RECOVER
```

Exact field representation is **IMPLEMENTATION BINDING TBD**.

## 35.4 Hit validation

At authoritative Hit Moment:

- target must remain gameplay-valid;
- target must satisfy configured attack reach/hit query;
- shared Player/Life-State revive protection must be respected.

Exact collider/hitbox implementation is **IMPLEMENTATION BINDING TBD**.

This document does not import Stalker's special “no new Hit-Moment LOS condition” contract.

## 35.5 Hearing during ATTACK

ATTACK is non-interruptible by sound.

```text
legal HearingObservation
→ PendingHearingInbox
```

No InvestigationEpisode starts until a later legal decision point.

## 35.6 RECOVER

RECOVER is mandatory after Hit or Miss.

`ListenerAttackRecovery` is **TUNING TBD** / fixed config and is not AED-authorized.

While recovery is incomplete:

```text
remain RECOVER
→ audible HearingObservations may enter PendingHearingInbox
```

At recovery completion:

```text
1. current legal visual confirmation exists
      → CHASE

2. otherwise:
      remove expired pending observations
      combine remaining pending + current same-tick hearing
      select best reachable legal candidate
      → create NEW InvestigationEpisode
      → INVESTIGATE

3. otherwise
      → ROAM
```

No old RuntimeNoiseEvent is scanned or physically re-evaluated at recovery completion.

If a pending observation expires before commitment:

```text
discard
→ it cannot start investigation
```

---

# 36. ROAM / Idle Patrol

## 36.1 Baseline

No project evidence requires Listener RegionGraph coverage.

**DETAILED-DESIGN DECISION:**

`ROAM` uses a simple designer-authored waypoint route.

```text
ListenerRoute
→ next authored waypoint
→ complete-path navigation
→ arrival
→ next waypoint
```

No coverage/staleness scoring.

## 36.2 Map authoring

Exact route/waypoints:

```text
MAP AUTHORING TBD
```

The route should expose Listener pressure to meaningful Research Facility spaces without becoming the Stalker Main Route.

## 36.3 Invalid route

If route is missing/invalid:

```text
safe hold at current valid NavMesh position
→ continue hearing/weak visual confirmation
→ expose diagnostic
```

Do not fabricate random movement or teleport.

---

# 37. Dynamic Doors

## 37.1 Hearing-time door semantics

A RuntimeNoiseEvent is evaluated against authoritative door state at its one HearingSensor evaluation moment.

```text
new noise evaluated after door closes
→ closed-door attenuation

new noise evaluated after door opens
→ open-door/no-door attenuation
```

## 37.2 No retroactive un-hearing — including pending observations

Once an event becomes a legal HearingObservation:

```text
door later closes/opens
X→ erase or rewrite that historical observation
X→ recalculate its historical OcclusionClass
X→ re-raycast the old RuntimeNoiseEvent
```

The evidence already occurred.

This remains true while the observation waits in `PendingHearingInbox`.

A door change may affect whether a **new** RuntimeNoiseEvent is heard and may affect current/future NavMesh reachability. It does not change past audibility.

## 37.3 Current navigation

`DoorStateChanged` may invalidate current investigation/roam/chase navigation.

```text
DoorStateChanged
→ relevant path/topology invalidation
→ NavigationController checks active path
→ if stale/unusable: repath
→ if active InvestigationHypothesis no longer reachable: NavigationFailed
```

## 37.4 Remembered hypothesis

A door change does not erase `InvestigationPosition`.

If an active remembered destination becomes unreachable:

```text
preserve immutable diagnostic evidence
→ terminalize episode NavigationFailed after bounded recovery
→ trace once
→ clear CurrentInvestigation
→ commit another currently legal hearing candidate if available
   else ROAM
```

Do not poll every door every frame.

---

# 38. Navigation Recovery

## 38.1 Recovery ladder

```text
1 validate agent/destination
2 evaluate path
3 PathComplete → execute
4 Partial/Invalid → reject candidate
5 path stale/topology changed → repath
6 progress monitor
7 suspected NoProgress
8 confirmed Stuck
9 bounded retry same logical investigation destination
10 if still unreachable → terminal NavigationFailed
11 choose another valid noise or ROAM
```

For ROAM, use next valid authored waypoint/hold fallback.

## 38.2 No-progress inputs

Use a bounded progress window over:

- physical displacement;
- `remainingDistance` trend;
- `desiredVelocity`;
- `pathPending`;
- `hasPath`;
- `pathStatus`;
- `isPathStale`;
- `isOnNavMesh`;
- repath history.

Numerical thresholds/windows are **TUNING TBD — profiler/playtest**.

## 38.3 Warp

`NavMeshAgent.Warp` is not normal recovery.

Emergency recovery is permitted only under separately validated scene-failure conditions and must be diagnostic, safe, and non-combat-advantageous.

---

# 39. Photon Fusion Authority

## 39.1 Authoritative owner

Host / Fusion State Authority owns:

- authoritative gameplay noise validation;
- RuntimeNoiseEvent creation;
- NoiseSystem mutation;
- bounded dedup retention/retirement;
- HearingSensor one-time evaluation;
- `ListenerMemory.PendingHearingInbox`;
- weak visual confirmation;
- ListenerMemory;
- noise selection/corroboration;
- same-tick FSM arbitration;
- FSM transitions;
- investigation terminal cleanup;
- investigation planning;
- NavMesh movement intent;
- CHASE target confirmation;
- attack resolution;
- authoritative gameplay telemetry facts.

## 39.2 Non-authoritative clients

Clients may:

- consume synchronized Listener transform/state;
- render animation;
- render VFX/SFX;
- render telegraphs/cues;
- interpolate/predict presentation only where Fusion design permits without changing State Authority;
- show optional development debug.

Clients must not:

- insert arbitrary authoritative noise;
- choose event position/loudness/range;
- run authoritative dedup retirement;
- select investigation;
- own/rebuild PendingHearingInbox;
- mutate ListenerMemory;
- choose CHASE corroboration outcome;
- arbitrate/transition Listener FSM;
- resolve attack/Down;
- run independent authoritative HearingSensor;
- re-evaluate historical sounds.

---

# 40. Fusion Network Binding

## 40.1 Boundary

Recommended architecture:

```text
NetworkObject
└── ListenerNetworkBinding : NetworkBehaviour
        ├── State Authority gate
        ├── synchronized client-required state
        └── FixedUpdateNetwork()
                 ↓
            ListenerRoot
                 ↓
        pure C# / Unity services
```

Do not make every Listener class inherit `NetworkBehaviour`.

## 40.2 Authoritative simulation tick

Conceptually:

```csharp
public override void FixedUpdateNetwork()
{
    if (!Object.HasStateAuthority)
        return;

    _listenerRoot.Simulate(Runner.DeltaTime);
    PublishRequiredNetworkState();
}
```

Exact code/API composition is **IMPLEMENTATION BINDING TBD**.

## 40.3 Networked state candidates

Durable client-required semantics:

- Listener semantic FSM state;
- transform via approved Fusion transform binding;
- current attack/action presentation phase if needed;
- current investigation presentation cue only if the art/audio layer requires durable reconstruction.

Do not replicate by default:

- NoiseSystem active queue;
- SourcePlayerId;
- ListenerMemory;
- HearingObservation history;
- competing-noise scores;
- InvestigationPosition;
- private event IDs;
- navigation reject history;
- debug history.

## 40.4 Runtime NoiseEvent network message

`NET-009` allows Host NoiseEvent broadcast to AI + optional clients.

For Listener AI:

```text
Host-local authoritative NoiseSystem
= source of truth
```

Normal clients do not need the authoritative noise queue to simulate Listener.

If clients receive noise presentation messages, they are presentation-only.

## 40.5 RPC

RPC may carry a validated action request or transient presentation cue where appropriate.

RPC must not be the sole durable representation of:

- current Listener state;
- current attack phase;
- any state required for late-join reconstruction.

No RPC bypasses Host validation/NoiseSystem dedup.

---

# 41. Late Join / Resync

A late/rejoining proxy reconstructs:

- current Listener transform;
- semantic state;
- current action/attack presentation state;
- current investigation presentation state only if required for visible cue.

It must not:

- receive/replay expired RuntimeNoiseEvents as new stimuli;
- recreate prior HearingObservations;
- reconstruct or consume Host `PendingHearingInbox`;
- restart historical InvestigationEpisodes;
- resurrect a terminal `PlayerConfirmed` InvestigationEpisode;
- rerun historical attack outcomes;
- start independent Listener decision simulation.

`PendingHearingInbox`, current private investigation memory, terminal episode history, and NoiseSystem dedup state remain Host-private.

Late join therefore cannot cause an old sound to be re-raycast or become a new AI stimulus.

---

# 42. ScenarioConfig / AED Boundary

## 42.1 M1-015 result

M1-015 v0 freezes only a Stalker adaptive parameter whitelist.

No Listener hearing/vision/investigation parameter is adaptive-authorized.

Therefore:

```text
AED mutable = NO
```

for every Listener v1.0 parameter until a future approved AED contract revision.

## 42.2 AED forbidden writes

AED must not directly set:

- RuntimeNoiseEvent;
- selected noise;
- HearingObservation;
- ListenerMemory;
- InvestigationHypothesis;
- ConfirmedTargetId;
- Listener FSM state;
- investigation destination;
- CHASE target;
- attack episode;
- navigation destination;
- noise-selection result.

---

# 43. Telemetry

## 43.1 Pipeline separation

```text
RuntimeNoiseEvent
├──→ HearingSensor → Listener
└──→ TelemetryEmitter → NOISE_EMITTED
```

Telemetry is evidence, not perception.

## 43.2 Existing production event

`NOISE_EMITTED` schema v1.0 requires:

```text
context.phase
context.position
data.noiseType
data.loudness
```

Optional:

```text
data.hearingRadius
```

Player source uses the player `userId`; environment/system source may use null according to telemetry ownership contract.

## 43.3 Listener may not read analytics

Forbidden:

```text
Telemetry DB → Listener
TelemetryEvent → HearingSensor
NOISE_EMITTED → Investigation
MatchScore → NoiseSelection
Profile → Target
```

## 43.4 Listener diagnostics

Architecture metrics require investigation evidence, but current production telemetry does not automatically activate Listener debug events.

Therefore:

- Listener decision/investigation traces are development diagnostics/evaluation evidence by default;
- do not invent a production event;
- if future telemetry needs an active `MONSTER_INVESTIGATE_*` event, mark it:

```text
PROPOSED — TELEMETRY CONTRACT REVISION REQUIRED
```

until payload/userId/version contract is approved.

---

# 44. Observability

Recommended read-only snapshot:

```text
ListenerAIDebugSnapshot
- State
- HasStateAuthority
- LastTransitionReason?
- LastSameTickArbitrationState?

- ActiveNoiseCount
- NoiseDedupEntryCount?
- NoiseDedupRetentionDiagnostic?

- LastHeardNoiseEventId?
- LastHeardNoiseType?
- LastHeardAge?
- LastHeardExpiry?
- HeardPosition?
- RawLoudness?
- EffectiveIntensity?
- HearingDistance?
- OcclusionClass?

- PendingHearingCount
- PendingHearingCapacity?
- OldestPendingHearingAge?
- LastPendingHearingDiagnostic?

- CandidateNoiseCount
- SelectedNoiseEventId?
- NoiseSelectionReason?
- LastHearingRejectReason?

- LastChaseCorroborationDistance?
- ChaseCorroborationRadius?
- LastChaseNoiseDisposition?

- CurrentInvestigationEpisodeId?
- CurrentInvestigationNoiseType?
- InvestigationPosition?
- ResolvedNavigationDestination?
- InvestigationElapsed?
- ArrivalListenElapsed?
- InvestigationOutcome?
- LastTerminalInvestigationEpisodeId?
- LastInvestigationTerminationReason?

- ConfirmedTargetId?
- HasCurrentVisualConfirmation

- NavigationPlanStatus
- NavigationExecutionStatus
- PathStatus
- PathPending
- IsPathStale
- NoProgressState
- LastRecoveryReason

- AttackEpisodeId?
- AttackResolved?
- AttackOutcome?

- LastFalseInvestigationReason?
```

Debug UI is read-only.

A debug snapshot may project pending/terminal history, but it must not own or mutate that state.

Useful gizmos:

- hearing radius for selected debug event;
- source→Listener occlusion ray and class at the actual evaluation;
- heard position;
- resolved NavMesh destination;
- current investigation path;
- weak visual confirmation cone;
- `ChaseCorroborationRadius` around current legal visual observation;
- selected/rejected/corroborating noise labels;
- pending hearing count/expiry;
- no-progress/stuck state.

Historical pending observations must not trigger new physics queries merely because debug visualization is enabled.

---

# 45. Reason Codes

Compact typed sets:

## 45.1 `NoiseValidationRejectReason`

```text
None
NotStateAuthority
UnknownNoiseType
InvalidDefinition
InvalidPosition
InvalidLoudness
InvalidHearingRadius
InvalidExpiry
DuplicateEmission
SourceActionRejected
```

## 45.2 `NoiseSystemDiagnosticReason`

```text
None
CapacityEvicted
DedupRetentionInvariantViolation
SubsystemUnavailable
```

`CapacityEvicted` is a diagnostic for the older active RuntimeNoiseEvent removed during bounded active-storage overflow; it is not a rejection of the newly accepted event.

`DedupRetentionInvariantViolation` indicates the configured/bound dedup retention mechanism cannot prove its state remains retained through the supported duplicate/replay/resimulation horizon.

## 45.3 `HearingRejectReason`

```text
None
Expired
OutsideRange
BelowThreshold
OccludedBelowThreshold
OcclusionQueryFailed
InvalidEvent
```

A legal observation that later expires in `PendingHearingInbox` is not retroactively a HearingReject; it uses pending-memory diagnostic semantics.

## 45.4 `NoiseSelectionReason`

```text
InitialHighestAudibility
NextReachableCandidate
PendingObservationSelected
RelatedNoiseMerged
CurrentInvestigationRetained
CorroboratesVisibleTarget
InterruptedByStrongerNoise
ChaseInterruptedByNoise
NoEligibleNoise
```

`CorroboratesVisibleTarget` means the sound was physically heard but, by spatial evidence, cannot divert the currently visible target.

## 45.5 `PendingHearingDiagnosticReason`

```text
ExpiredBeforeCommit
CapacityEvicted
ConsumedByStatePolicy
```

These support debug/tests without turning pending observations into InvestigationEpisodes.

## 45.6 `InvestigationTerminationReason`

```text
PlayerConfirmed
FalseInvestigation
InterruptedByHigherPriorityNoise
NavigationFailed
CancelledByMatchEnd
CancelledByListenerDisable
```

## 45.7 `TargetConversionReason`

```text
WeakVisualConfirmation
TargetVisualLost
TargetInvalid
NoiseMetadataNotSufficient
```

## 45.8 `ListenerNavigationFailureReason`

```text
None
AgentUnavailable
AgentNotOnNavMesh
DestinationInvalid
PathPartial
PathInvalid
PathStale
PathPendingTimeout
DoorBlocked
NoProgress
Stuck
```

## 45.9 `ListenerRecoveryReason`

```text
PathStaleRepath
DoorTopologyRepath
RetryInvestigationDestination
InvestigationAbandonedUnreachable
RoamWaypointSkipped
SafeHold
EmergencyNavMeshRecovery
```

---

# 46. Quality Metrics

No pass/fail thresholds are frozen.

Common rule:

```text
one metric name + version
→ one unit
→ one numerator
→ one denominator
→ one reset/window rule
```

Zero denominator:

```text
denominator == 0
→ MetricStatus = NOT_EVALUATED
→ value = null
```

Investigation terminal metrics consume an exactly-once terminal trace / immutable terminal episode record. They do not require a terminal episode to remain as `CurrentInvestigation`.

## 46.1 `NoiseResponseLatency_v1.0`

Purpose: measure decision response after legal hearing, not emission/network/nav travel time.

Eligible sample:

```text
HearingObservation selected to start a new InvestigationEpisode
```

Per-sample latency:

```text
InvestigationCommittedAt - HearingObservation.HeardAt
```

If the observation waited in `PendingHearingInbox` during ATTACK/RECOVER, that legal waiting time is included. The metric still measures hearing-to-commitment decision latency.

Metric over evaluation window:

| Field | Contract |
|---|---|
| Unit | seconds |
| Numerator | sum of eligible commitment latencies |
| Denominator | count of new InvestigationEpisodes started from HearingObservation |
| Window/reset | match or declared Listener evaluation window |
| Source | immutable HearingObservation + InvestigationEpisode start trace |
| Exclusions | related-noise merge; CHASE visual acquisition; expired pending; rejected/unreachable noise |
| Interpretation | lower = faster decision response after hearing |
| Caveat | does not include emission→hearing, path travel, or arrival time |

## 46.2 `SourceSelectionShare_v1.0`

Purpose: explain which source categories/reasons actually drive investigation.

For each `(NoiseType, NoiseSelectionReason)` bucket:

```text
Numerator
=
new InvestigationEpisode starts in bucket

Denominator
=
all new InvestigationEpisode starts
```

Unit: proportion.

Window/reset: match or declared evaluation window.

Source: NoiseSelector + InvestigationEpisode start.

Pending observations selected later use `PendingObservationSelected` as the selection-reason dimension where appropriate while preserving their original NoiseType.

Exclusions:

- related-noise merges that do not start a new episode;
- `CorroboratesVisibleTarget` hearing that does not start investigation;
- expired pending observations;
- rejected/unreachable noises.

Also record raw count for audit.

## 46.3 `FalseInvestigationRate_v1.0`

Purpose: measure how often completed evidence-resolution investigations fail to confirm a player.

Eligible terminal outcomes:

```text
PlayerConfirmed
FalseInvestigation
```

Formula:

```text
FalseInvestigationRate_v1.0
=
count(FalseInvestigation)
/
count(PlayerConfirmed + FalseInvestigation)
```

| Field | Contract |
|---|---|
| Unit | InvestigationEpisode |
| Numerator | exactly-once terminal traces with `FalseInvestigation` |
| Denominator | exactly-once terminal traces with `PlayerConfirmed` or `FalseInvestigation` |
| Window/reset | match or declared evaluation window |
| Source | InvestigationEpisode terminal trace / immutable terminal history |
| Exclusions | interrupted, NavigationFailed, match end, Listener disable, expired pending before commitment |
| Interpretation | higher = more committed hypotheses resolve without a player confirmation |
| Caveat | Noise Maker is expected to contribute valid false investigations; higher is not automatically a defect |

## 46.4 Diagnostic rates

Optional development metrics may include:

```text
InvestigationInterruptRate
InvestigationNavigationFailureRate
HearingRejectDistribution
PendingHearingExpiryCount
PendingHearingCapacityEvictionCount
ChaseCorroborationCount
```

These require their own versioned denominator contract before research use.

---

# 47. Performance Contract

Freeze principles, not budgets:

- RuntimeNoiseEvent generation is event-driven;
- recurring movement emissions are bounded/configured, not per-render-frame by default;
- expired active events are removed;
- active RuntimeNoiseEvent storage is bounded;
- `PendingHearingInbox` is bounded;
- pending observations are filtered by expiry before ranking;
- historical HearingObservations are never re-raycast/re-heard;
- duplicate emissions are rejected before sensor work;
- NoiseSystem dedup state is bounded/reclaimable;
- a whole-match unbounded dedup HashSet is forbidden baseline behavior;
- dedup state is retained at least through the supported duplicate/replay/resimulation horizon;
- distance/expiry filtering happens before occlusion raycasts;
- HearingSensor evaluates publication events rather than raycasting every active event every frame;
- path validation is performed only for a bounded top candidate set;
- do not path-check unlimited simultaneous noise events;
- same-tick arbitration consumes collected observations once rather than repeatedly transitioning FSM;
- do not run room-graph acoustics without evidence;
- cache stable references;
- prefer non-alloc physics query where profiling shows allocation cost;
- use Unity Profiler before setting Hz/ms budgets.

Numerical hearing/pending/candidate/performance bounds are:

```text
TUNING TBD — profiler/playtest
```

Dedup retention window/count representation is:

```text
IMPLEMENTATION BINDING TBD — authoritative request/Fusion binding dependent
```

---

# 48. Tests

This document defines required tests only. It does not claim they pass.

## 48.1 EditMode / pure logic

| Test ID | Requirement | Expected result |
|---|---|---|
| LIS-E-001 | valid NoiseDefinition | accepted |
| LIS-E-002 | unknown noise type | rejected |
| LIS-E-003 | invalid radius/loudness/expiry | rejected typed reason |
| LIS-E-004 | duplicate emission key | one RuntimeNoiseEvent only |
| LIS-E-005 | stable event ordering | equal inputs produce stable NoiseEventId/order semantics |
| LIS-E-006 | expiry | expired event cannot start hearing/investigation |
| LIS-E-007 | bounded storage | deterministic expired-first/oldest eviction |
| LIS-E-008 | distance factor | deterministic linear attenuation |
| LIS-E-009 | outside range | rejected |
| LIS-E-010 | closed-door multiplier | correct configured attenuation class |
| LIS-E-011 | wall vs door ordering | wall attenuation <= closed-door attenuation |
| LIS-E-012 | multiple blockers | strongest class applied once |
| LIS-E-013 | occlusion query failure | conservatively rejected |
| LIS-E-014 | hearing threshold | effective-intensity rule deterministic |
| LIS-E-015 | selector intensity rank | higher effective intensity wins |
| LIS-E-016 | selector recency/distance/tie | deterministic lexicographic order |
| LIS-E-016A | same-tick observation batch | all audible observations for tick are ranked together using EventOrderKey tie-break |
| LIS-E-017 | unreachable top noise | next bounded reachable candidate selected |
| LIS-E-018 | weak noise during investigation | current investigation retained |
| LIS-E-019 | stronger noise interrupt | interrupt only at configured margin |
| LIS-E-020 | related noise merge | same episode updated, no extra episode |
| LIS-E-021 | ListenerMemory lifecycle | exact create/update/clear |
| LIS-E-022 | no live Transform dependency | RuntimeNoiseEvent/HearingObservation/Memory have no hidden Transform input |
| LIS-E-023 | noise metadata target conversion | cannot create ConfirmedTarget |
| LIS-E-024 | weak visual confirmation | legal observation creates ConfirmedTarget |
| LIS-E-025 | visual loss | clears ConfirmedTarget; no LKP created |
| LIS-E-026 | false investigation classification | exact terminal rule |
| LIS-E-027 | navigation failure not false | Path failure excluded from false result |
| LIS-E-028 | NoiseResponseLatency | same trace → same value |
| LIS-E-029 | SourceSelectionShare | bucket counts/denominator deterministic |
| LIS-E-030 | FalseInvestigationRate | exact eligible denominator |
| LIS-E-031 | metric zero denominator | `NOT_EVALUATED` + null |
| LIS-E-032 | reason codes | all rejection/terminal branches controlled |
| LIS-E-033 | attack duplicate resolution | one Down/outcome maximum per attack episode |
| LIS-E-034 | AED boundary | no Listener parameter in v0 adaptive whitelist |

## 48.2 PlayMode

| Test ID | Scenario | Expected result |
|---|---|---|
| LIS-P-001 | Sprint authoritative movement | one logical SPRINT per defined recurring emission |
| LIS-P-002 | Crouch | approved noise reduction applied; no invented extra category |
| LIS-P-003 | Core carry movement | CORE_CARRY; no duplicate SPRINT for same movement emission |
| LIS-P-004 | Core drop | one CORE_DROP at authoritative drop position |
| LIS-P-005 | configured interaction | one INTERACTION |
| LIS-P-006 | non-noisy interaction | no RuntimeNoiseEvent |
| LIS-P-007 | Noise Maker | one NOISE_MAKER at authoritative tool position |
| LIS-P-008 | audible clear path | HearingObservation created |
| LIS-P-009 | outside range | ignored |
| LIS-P-010 | event expired before evaluation | ignored |
| LIS-P-011 | open door | no door attenuation |
| LIS-P-012 | closed door | configured attenuation |
| LIS-P-013 | wall | configured stronger/equal attenuation |
| LIS-P-014 | loud noise through wall | audible only if effective intensity remains threshold-valid |
| LIS-P-015 | two simultaneous noises | deterministic winner |
| LIS-P-016 | equal-priority noises | stable tie-break |
| LIS-P-017 | weak noise spam | no illegal oscillation |
| LIS-P-018 | stronger new noise | deterministic interrupt |
| LIS-P-019 | same-position related noise | hypothesis merges/retargets legally |
| LIS-P-020 | player moves silently after emission | Listener continues toward old legal sound position |
| LIS-P-021 | player emits new noise after moving | hypothesis may legally update from new event |
| LIS-P-022 | unreachable strongest noise | rejected; next legal candidate evaluated |
| LIS-P-023 | off-NavMesh noise | bounded projection used; heard position preserved |
| LIS-P-024 | PathPartial | not accepted as exact investigation success |
| LIS-P-025 | PathInvalid | rejected |
| LIS-P-026 | door closes mid-investigation | path re-evaluated; may NavigationFail |
| LIS-P-027 | door opens after hearing | old hearing evidence unchanged; new navigation may replan |
| LIS-P-028 | arrival no player | ArrivalListen → FalseInvestigation |
| LIS-P-029 | player visually confirmed during investigation | PlayerConfirmed → CHASE |
| LIS-P-030 | weak vision from ROAM | eligible visible player can CHASE without Detection Meter |
| LIS-P-031 | CHASE visual loss | target cleared; no hidden follow/LKP |
| LIS-P-032 | loud diversion during CHASE | threshold-valid noise → INVESTIGATE |
| LIS-P-033 | ATTACK | one authoritative attack episode |
| LIS-P-034 | valid attack hit | generic contract produces one Down outcome |
| LIS-P-035 | RECOVER | mandatory; prevents chain hit |
| LIS-P-036 | no noise | ROAM route continues |
| LIS-P-037 | route unavailable | safe hold/listen diagnostic |
| LIS-P-038 | NoProgress | suspected before confirmed stuck |
| LIS-P-039 | Stuck | bounded recovery then investigation failure/ROAM |
| LIS-P-040 | telemetry unavailable | Listener hearing unaffected |
| LIS-P-041 | fake NOISE_EMITTED TelemetryEvent | no hearing |
| LIS-P-042 | client-only AudioSource | no hearing |
| LIS-P-043 | NoiseSystem unavailable | Listener fails safe to ROAM/listen-disabled diagnostic, never telemetry fallback |

## 48.3 Fusion multiplayer

| Test ID | Scenario | Expected result |
|---|---|---|
| LIS-N-001 | client requests noisy action | Host validates; client cannot author event fields |
| LIS-N-002 | forged client noise position | ignored/rejected; Host derives position |
| LIS-N-003 | forged loudness/radius | ignored/rejected |
| LIS-N-004 | only State Authority hearing | proxy does not run authoritative HearingSensor/FSM |
| LIS-N-005 | proxy investigation mutation | no authoritative effect |
| LIS-N-006 | duplicate request/callback | at most one logical RuntimeNoiseEvent |
| LIS-N-007 | 2-player convergence | Listener transform/state/presentation converges |
| LIS-N-008 | 3-player convergence | same |
| LIS-N-009 | 4-player convergence | same |
| LIS-N-010 | proxy attack | cannot apply Down/damage |
| LIS-N-011 | duplicate Host attack resolve | one outcome maximum |
| LIS-N-012 | late join during INVESTIGATE | reconstruct state/transform; no historical noise replay |
| LIS-N-013 | late join during ATTACK/RECOVER | reconstruct durable action presentation; no outcome replay |
| LIS-N-014 | reconnect | no second Listener simulation |
| LIS-N-015 | private memory | Noise queue/ListenerMemory/Hearing history not replicated by default |
| LIS-N-016 | presentation noise message | cannot feed authoritative HearingSensor |

## 48.4 LIS-DD Surgical Correction Tests

### EditMode / pure logic

| Test ID | Issue | Requirement | Expected result |
|---|---|---|---|
| LIS-E-035 | DD-01 | pending inbox stores legal observation | immutable HearingObservation stored; no RuntimeNoiseEvent/live Transform reference |
| LIS-E-036 | DD-01 | pending expiry | `now >= ExpiresAt` removes observation; no investigation can start |
| LIS-E-037 | DD-01 | pending overflow | expired removed first; highest-ranked retained; EventOrderKey tie-break deterministic |
| LIS-E-038 | DD-01 | historical hearing no re-evaluation | pending consumption uses frozen Distance/EffectiveIntensity/OcclusionClass; no physics callback invoked |
| LIS-E-039 | DD-02 | CHASE spatial corroboration | noise within ChaseCorroborationRadius returns `CorroboratesVisibleTarget` |
| LIS-E-040 | DD-02 | corroboration information boundary | classification uses hearing position + legal visual observed position only; no SourcePlayerId/SourceEntityId |
| LIS-E-041 | DD-03 | ROAM arbitration | visual + hearing same step → CHASE exactly once |
| LIS-E-042 | DD-03 | INVESTIGATE visual vs interrupt | visual confirmation wins; old episode terminal PlayerConfirmed once |
| LIS-E-043 | DD-03 | ArrivalListen vs related noise | related legal noise merges before FalseInvestigation |
| LIS-E-044 | DD-03 | CHASE attack vs diversion | valid visible in-range attack wins over same-step separated diversion |
| LIS-E-045 | DD-03 | RECOVER completion arbitration | visual + pending/current hearing → CHASE; no second transition same step |
| LIS-E-046 | DD-04 | long recurring dedup stream | dedup retained-state size remains bounded/reclaimable under policy |
| LIS-E-047 | DD-04 | duplicate within supported horizon | duplicate is rejected |
| LIS-E-048 | DD-04 | retirement safety | record cannot retire while corresponding emission remains replayable in supported path |
| LIS-E-049 | DD-04 | match reset | dedup ledger/window/watermark state fully cleared |
| LIS-E-050 | DD-05 | PlayerConfirmed cleanup | terminal trace once; CurrentInvestigation cleared before/with CHASE target install |
| LIS-E-051 | DD-05 | interruption replacement | old episode terminalized; exactly one new active episode; no stack |
| LIS-E-052 | DD-05 | NavigationFailed replacement | old episode cleared before new candidate commitment |
| LIS-E-053 | DD-05 | terminal trace idempotency | one episode produces at most one terminal outcome/trace |
| LIS-E-054 | DD-05 | CHASE loss | terminal PlayerConfirmed episode cannot be resurrected |
| LIS-E-055 | DD-05 | active episode cardinality | CurrentInvestigation is null or one non-terminal episode only |

### PlayMode

| Test ID | Issue | Scenario | Expected result |
|---|---|---|---|
| LIS-P-044 | DD-01 | audible noise during RECOVER; recovery ends before expiry | observation remains pending and can be selected without re-raycast |
| LIS-P-045 | DD-01 | audible noise during RECOVER expires before completion | pending entry removed; no investigation |
| LIS-P-046 | DD-01 | door moves after sound was heard while pending | historical audibility/intensity/occlusion unchanged |
| LIS-P-047 | DD-02 | visible sprinting target emits nearby recurring SPRINT | CHASE retained; `CorroboratesVisibleTarget`; no oscillation |
| LIS-P-048 | DD-02 | visible target produces nearby CORE_CARRY noise | CHASE retained; no oscillation |
| LIS-P-049 | DD-02 | spatially separated legal Noise Maker/noise | may divert only if reachability/threshold/arbitration pass |
| LIS-P-050 | DD-03 | ROAM visual + noise same tick | visual → CHASE; one semantic transition |
| LIS-P-051 | DD-03 | INVESTIGATE visual + stronger interrupt noise same tick | PlayerConfirmed → CHASE; noise does not cause second same-step transition |
| LIS-P-052 | DD-03 | ArrivalListen completes + related noise same tick | merge/update investigation; not FalseInvestigation |
| LIS-P-053 | DD-03 | CHASE attack-entry + diversion same tick | ATTACK |
| LIS-P-054 | DD-03 | RECOVER completes + visual + pending/current noise same tick | CHASE |
| LIS-P-055 | DD-05 | CHASE loses vision after earlier PlayerConfirmed episode | old terminal episode not reopened; only current legal hearing may create new episode |
| LIS-P-056 | DD-04 | long SPRINT recurring stream | exactly-once behavior maintained with bounded dedup retention |
| LIS-P-057 | DD-04 | long CORE_CARRY recurring stream | exactly-once behavior maintained with bounded dedup retention |

### Fusion multiplayer / authoritative replay

| Test ID | Issue | Scenario | Expected result |
|---|---|---|---|
| LIS-N-017 | DD-04 | duplicate action/request within supported Host replay/resimulation horizon | one RuntimeNoiseEvent |
| LIS-N-018 | DD-04 | dedup retirement boundary | no valid late duplicate inside supported horizon becomes second logical event |
| LIS-N-019 | DD-04 | long Host match with recurring movement noise | Host dedup memory remains bounded/reclaimable |
| LIS-N-020 | DD-01/DD-05 | late join while Host has pending hearing / current investigation | proxy receives no private inbox/history and causes no old sound/episode replay |

The table above specifies required tests only; no passing result is claimed.

---

# 49. Edge-Case Matrix

| Edge case | Expected behavior | Owner | Reason code / result | Test |
|---|---|---|---|---|
| no noise exists | ROAM | FSM | `NoEligibleNoise` | LIS-P-036 |
| noise expires before HearingSensor evaluation | ignore permanently | HearingSensor | `Expired` | LIS-E-006/P-010 |
| duplicate NoiseEvent emission key | one logical event | NoiseSystem | `DuplicateEmission` | LIS-E-004/N-006 |
| same-source rapid valid movement emissions | each defined emission unique; hysteresis prevents oscillation | NoiseSystem/Selector | merge/retain | LIS-P-017/019 |
| two equal-priority events | stable EventOrderKey tie-break | Selector | deterministic selection | LIS-P-016 |
| multiple events in same authoritative tick | collect first; one deterministic FSM arbitration | NoiseSystem/Sensors/FSM | state-specific priority | LIS-E-041..045 |
| audible noise arrives during ATTACK | store immutable pending; no ATTACK transition | ListenerMemory/FSM | pending | LIS-E-035 |
| audible noise arrives during incomplete RECOVER | store immutable pending | ListenerMemory/FSM | pending | LIS-P-044 |
| pending observation expires before legal decision | discard; cannot commit | ListenerMemory | `ExpiredBeforeCommit` | LIS-E-036/P-045 |
| pending inbox over capacity | remove expired, retain highest-ranked deterministically | ListenerMemory/Selector | `CapacityEvicted` | LIS-E-037 |
| door moves after observation was heard but before pending consumption | historical hearing values unchanged; no re-raycast | HearingSensor/Memory | immutable evidence | LIS-E-038/P-046 |
| player disconnects after noise | old noise remains legal snapshot; no identity tracking; target invalid if visually confirmed | Memory/FSM | target invalid | multiplayer/life test |
| source player dies/Downed after noise | sound hypothesis remains; target eligibility governs later visual conversion | Memory/Eligibility | no metadata target | LIS-E-023 |
| player moves after emitting noise | investigate snapshot P, not hidden Q | Memory/Planner | legal old hypothesis | LIS-P-020 |
| new noise after movement | may merge/interrupt legally | Selector/Memory | related/stronger reason | LIS-P-021 |
| noise from another player | treated only as another legal hearing observation; source identity not used for hidden tracking | Selector/Memory | normal ranking | LIS-E-040 |
| visible target produces nearby SPRINT | physically heard but corroborates current visual target; CHASE retained | Selector/FSM | `CorroboratesVisibleTarget` | LIS-P-047 |
| visible target produces nearby CORE_CARRY | same corroboration rule; no oscillation | Selector/FSM | `CorroboratesVisibleTarget` | LIS-P-048 |
| another/Noise Maker sound is spatially near current visible target | also corroborating regardless hidden source identity | Selector/FSM | `CorroboratesVisibleTarget` | LIS-E-039/040 |
| spatially separated strong diversion during CHASE | normal threshold/reachability rules may interrupt | Selector/FSM | `ChaseInterruptedByNoise` | LIS-P-049 |
| CHASE attack-entry and diversion same tick | ATTACK wins | FSM arbitration | attack priority | LIS-E-044/P-053 |
| ROAM visual + noise same tick | visual confirmation wins | FSM arbitration | CHASE | LIS-E-041/P-050 |
| INVESTIGATE visual + stronger noise same tick | PlayerConfirmed wins; episode clears; one transition | FSM/Memory | `PlayerConfirmed` | LIS-E-042/P-051 |
| ArrivalListen expiry + related noise same tick | related noise merges before false terminal | FSM/Selector | `RelatedNoiseMerged` | LIS-E-043/P-052 |
| RECOVER completion + visual + noise same tick | visual → CHASE | FSM arbitration | CHASE | LIS-E-045/P-054 |
| noise position off NavMesh | bounded projection for navigation only | Planner | projected destination | LIS-P-023 |
| PathPartial | reject exact investigation | Navigation | `PathPartial` | LIS-P-024 |
| PathInvalid | reject | Navigation | `PathInvalid` | LIS-P-025 |
| door closes after hearing | hearing memory persists; active path may replan/fail | Navigation | topology replan | LIS-P-026/046 |
| door opens | historical hearing unchanged; route may become valid for current/future plan | Navigation | topology replan | LIS-P-027 |
| sound across wall | attenuate; threshold decides | HearingSensor | class result | LIS-P-014 |
| current investigation becomes unreachable | bounded retry → terminal NavigationFailed; clear old episode | Navigation/FSM/Memory | `NavigationFailed` | LIS-E-052/P-026/039 |
| stronger noise during investigation | interrupt if margin reached and reachable; replace episode | Selector/FSM/Memory | `InterruptedByStrongerNoise` | LIS-E-051/P-018 |
| weaker noise during investigation | retain current | Selector | `CurrentInvestigationRetained` | LIS-E-018 |
| Noise Maker during CHASE | separated + audible + reachable + threshold + arbitration may divert | Selector/FSM | `ChaseInterruptedByNoise` | LIS-P-049 |
| no player at source | bounded ArrivalListen → false terminal → clear | FSM/Memory | `FalseInvestigation` | LIS-P-028 |
| PlayerConfirmed terminal | trace once, clear active investigation, then target/CHASE | Memory/FSM | `PlayerConfirmed` | LIS-E-050 |
| CHASE later loses vision after PlayerConfirmed | terminal episode cannot reopen | FSM/Memory | new hearing or ROAM | LIS-E-054/P-055 |
| all noise candidates invalid | ROAM/current commitment retained according to state | Selector | `NoEligibleNoise` | selection negative test |
| visual confirmation from noise metadata only | forbidden | Target conversion | `NoiseMetadataNotSufficient` | LIS-E-023 |
| visual target lost | clear target, no LKP; current/pending hearing may create NEW episode | FSM | `TargetVisualLost` | LIS-P-031/P-055 |
| Listener stuck | bounded recovery; fail current investigation if unrecoverable | Navigation | `Stuck` | LIS-P-039 |
| long recurring Sprint/CoreCarry stream | dedup state remains bounded/reclaimable; exactly once preserved | NoiseSystem | retention invariant | LIS-E-046/P-056/P-057/N-019 |
| duplicate inside supported replay horizon | rejected even if old active event storage has changed | NoiseSystem | `DuplicateEmission` | LIS-E-047/N-017 |
| dedup retirement would be too early | do not silently retire; diagnostic | NoiseSystem | `DedupRetentionInvariantViolation` | LIS-E-048/N-018 |
| match reset | active events, pending Listener match memory, and dedup state reset by their owners | Root/NoiseSystem/Memory | reset | LIS-E-049 |
| NoiseSystem unavailable | no telemetry/audio fallback; safe ROAM/listen-disabled diagnostic | Root | subsystem unavailable | LIS-P-043 |
| non-authoritative client injects noise | no authoritative effect | Fusion binding | `NotStateAuthority` | LIS-N-002/003 |
| late join during investigation/pending hearing | reconstruct presentation only; no pending/history replay | Fusion | no event replay | LIS-N-012/N-020 |
| telemetry unavailable | runtime hearing unaffected | Telemetry adapter | independent pipeline | LIS-P-040 |
| debug unavailable | gameplay unchanged | Debug provider | no-op | debug failure test |

---

# 50. Class / Component Contracts

## 50.1 `RuntimeNoiseEvent`

| Item | Contract |
|---|---|
| Purpose | immutable authoritative AI stimulus snapshot |
| Owned state | none after creation |
| Inputs | validated gameplay emission |
| Outputs | event value |
| Dependencies | stable NoiseType/config/time/position/EventOrderKey |
| Forbidden | live Transform/player tracking reference |
| Lifecycle | accepted → active → expired/removed |
| Tests | validation, immutability, no hidden reference |

## 50.2 `NoiseDefinition` / `NoiseCatalog`

| Item | Contract |
|---|---|
| Purpose | designer-owned mapping from approved noise type to runtime emission properties |
| State | loudness, radius, lifetime, emission mode |
| Inputs | approved v0 noise categories |
| Outputs | immutable definition |
| Forbidden | Listener FSM state, AED runtime writes |
| Failure | unknown/malformed definition rejects emission |
| Config | STATIC DESIGN CONFIG / TUNING TBD |
| Tests | catalog completeness/validation |

## 50.3 `NoiseSystem`

| Item | Contract |
|---|---|
| Purpose | authoritative noise event infrastructure |
| Owned state | bounded active events, bounded/reclaimable dedup state, identity/EventOrderKey allocation |
| Inputs | validated logical emissions + authoritative replay/sequence progress |
| Outputs | one RuntimeNoiseEvent publication |
| Dependencies | authoritative time/match lifecycle; supported duplicate/replay/resimulation semantics |
| Forbidden | hearing selection/FSM/telemetry analysis; whole-match unbounded dedup HashSet |
| Main operations | TryPublish, Expire, RetireDedupState, Reset |
| Failure | typed reject/diagnostic; never fabricate event or retire dedup too early |
| Tests | dedup/expiry/active bounds/dedup retention/order/stress |

## 50.4 `HearingSensor`

| Item | Contract |
|---|---|
| Purpose | one-time physical audibility evaluation |
| Owned state | hearing origin/config/cache only |
| Inputs | RuntimeNoiseEvent + Listener position + world occlusion at authoritative evaluation time |
| Outputs | immutable HearingObservation or HearingRejectReason |
| Dependencies | physics LayerMask, authoritative door collision state |
| Forbidden | selecting investigation/target/FSM; pending-memory ownership; re-raycasting old observations |
| Main operations | Evaluate(event) once per accepted event/listener |
| Failure | conservative reject on query/config failure |
| Config | threshold/occlusion tuning |
| Tests | range/attenuation/wall/door/no historical re-evaluation |

## 50.5 `HearingObservation`

| Item | Contract |
|---|---|
| Purpose | immutable legal hearing fact |
| State | event/order IDs, type, snapshot position, HeardAt/ExpiresAt, frozen heard-time intensity/distance/occlusion |
| Forbidden | SourcePlayerId/live Transform; later geometry mutation |
| Consumers | selector, ListenerMemory pending inbox, debug |
| Lifecycle | immediate decision input OR bounded pending → consumed/expired; never becomes mutable world sound |
| Tests | field semantics/immutability/pending lifecycle |

## 50.6 `ListenerVisualConfirmationSensor`

| Item | Contract |
|---|---|
| Purpose | weak secondary physical player confirmation |
| Inputs | authoritative player candidates/world geometry |
| Outputs | ListenerVisualObservation[] |
| Forbidden | Detection Meter, FSM, target mutation |
| Config | weaker vision envelope; tuning TBD |
| Tests | LOS/range/weakness/eligibility separation |

## 50.7 `ListenerMemory`

| Item | Contract |
|---|---|
| Purpose | legal hearing/investigation/confirmed-target knowledge |
| Owned state | one `CurrentInvestigation?`, `ConfirmedTargetId?`, bounded `PendingHearingInbox`, bounded decision history, optional bounded immutable terminal episode history |
| Inputs | accepted hearing/visual facts + FSM lifecycle |
| Outputs | read-only decision/planner context |
| Forbidden | hidden transform/velocity/facing; telemetry/Profile; terminal episode left active |
| Lifecycle | match scoped; terminalize/trace once/clear; pending expiry/overflow deterministic |
| Invariant | active InvestigationEpisode count <= 1 |
| Tests | create/update/clear/pending/terminal cleanup/no-cheat |

## 50.8 `ListenerNoiseSelector`

| Item | Contract |
|---|---|
| Purpose | deterministic competing-noise ordering/interrupt/corroboration decision |
| Inputs | same-tick HearingObservations + pending inbox + ListenerMemory/FSM commitment + current legal visual observed position when in CHASE |
| Outputs | ranked candidates + selection/disposition reason |
| Forbidden | NavMesh movement, FSM mutation, SourcePlayerId-based self-noise classification |
| Main operations | Rank, ShouldInterrupt, IsRelated, IsSpatiallyCorroborating |
| Config | merge radius/interrupt thresholds/ChaseCorroborationRadius |
| Tests | ranking/tie/hysteresis/pending/corroboration |

## 50.9 `ListenerFSM`

| Item | Contract |
|---|---|
| Purpose | own five Listener semantic states/transitions and same-tick arbitration |
| Owned state | current state + state timers |
| Inputs | collected selector result, memory, visual confirmation, nav/action completion, pending/current hearing |
| Outputs | at most one state transition result per authoritative decision step |
| Forbidden | raw raycasts/path calculations; callback-order-dependent transitions |
| Tests | transition table, arbitration, no extra states |

## 50.10 `ListenerInvestigationPlanner`

| Item | Contract |
|---|---|
| Purpose | convert accepted sound hypothesis to legal navigation intent |
| Inputs | CurrentInvestigation, NavMesh evaluator, door state |
| Outputs | resolved destination/plan result |
| Forbidden | selecting noise/target; hidden Player position |
| Failure | typed path rejection/NavigationFailed |
| Tests | off-mesh/Complete/Partial/Invalid/door |

## 50.11 Listener navigation controller / shared navigation adapter

| Item | Contract |
|---|---|
| Purpose | path execution/progress/recovery |
| Inputs | move intent + NavMeshAgent |
| Outputs | plan/execution status |
| Forbidden | noise choice, target conversion, FSM |
| Reuse | shared navigation abstractions may be extracted from Stalker implementation |
| Tests | shared navigation suite + Listener integration |

## 50.12 `NavigationProgressMonitor`

| Item | Contract |
|---|---|
| Purpose | Moving vs NoProgress vs Stuck |
| Inputs | displacement/distance/desired velocity/path state |
| Outputs | progress classification |
| Forbidden | semantic Listener state |
| Config | thresholds/windows TBD |
| Tests | pending/moving/blocked/stuck |

## 50.13 `ListenerAttackController`

| Item | Contract |
|---|---|
| Purpose | execute FSM-authorized generic Listener attack |
| Owned state | attack episode identity/resolution guard/timer/result |
| Inputs | confirmed target, range/hit query, life-state protection |
| Outputs | Hit/Miss/Down outcome at most once |
| Forbidden | ATTACK entry decision, hidden target tracking, sound interruption |
| Lifecycle | one episode per ATTACK entry → resolve once → complete → RECOVER |
| Tests | valid/miss/protection/dedup/RECOVER |

## 50.14 `ListenerDebugProvider`

| Item | Contract |
|---|---|
| Purpose | immutable debug projection |
| Inputs | runtime subsystems including pending/dedup/arbitration projections |
| Outputs | ListenerAIDebugSnapshot |
| Forbidden | gameplay mutation or historical hearing re-query |
| Tests | snapshot truth/read-only |

## 50.15 `ListenerTelemetryAdapter`

| Item | Contract |
|---|---|
| Purpose | map authoritative facts to approved telemetry |
| Inputs | RuntimeNoiseEvent / gameplay outcomes |
| Outputs | schema-approved `NOISE_EMITTED`; diagnostics separately |
| Forbidden | feeding telemetry back into hearing/decision |
| Tests | payload mapping/no feedback |

## 50.16 Fusion Listener network-facing binding

| Item | Contract |
|---|---|
| Purpose | State Authority gate + durable presentation synchronization |
| Base | recommended NetworkBehaviour on NetworkObject |
| Inputs | Fusion tick + ListenerRoot |
| Outputs | semantic state/transform/action presentation |
| Forbidden | proxy hearing/FSM; pending inbox/private memory/dedup replication |
| Tests | LIS-N suite |

---

# 51. Current-to-Target Code Mapping

No Listener-specific source module is evidenced.

| Current class/module | Current responsibility | Action | Target responsibility | Risk |
|---|---|---|---|---|
| Listener runtime code | NOT EVIDENCED | ADD | `ListenerRoot` composition + FSM | High: new subsystem |
| Runtime NoiseEvent implementation | NOT EVIDENCED | ADD | immutable gameplay stimulus contract | High: cross-gameplay dependency |
| `NoiseEventBus` | architecture concept only | IMPLEMENT | Host runtime publication/dedup/expiry | High |
| Hearing sensor | NOT EVIDENCED | ADD | distance/occlusion → HearingObservation | Medium |
| Listener memory | NOT EVIDENCED | ADD | typed legal memory | Medium |
| Listener selector | NOT EVIDENCED | ADD | deterministic noise ranking/hysteresis | Medium |
| Listener investigation planner | NOT EVIDENCED | ADD | hypothesis→complete path | Medium |
| weak visual confirmation | required by MON-08, code not evidenced | ADD | secondary physical target conversion | Medium |
| Listener attack/recover | generic MON-05 contract, code not evidenced | ADD | exactly-once attack + mandatory Recover | High |
| Fusion Listener binding | NOT EVIDENCED | ADD | Host authority + state sync | High |
| Telemetry noise schema | CONTRACT EXISTS | KEEP / BIND | runtime event→NOISE_EMITTED adapter | Medium |

---

# 52. Shared-Code Reuse

## 52.1 Reuse recommended

From architecture/Stalker patterns:

- `MonsterRuntimeContext` only where semantics are actually shared;
- navigation plan/execution enums;
- complete/partial/invalid path evaluator;
- `NavigationProgressMonitor`;
- stuck/recovery utility;
- read-only debug snapshot conventions;
- Fusion State Authority boundary pattern;
- telemetry adapter one-way pattern;
- test fixtures/utilities;
- exactly-once authoritative action-episode guard pattern.

## 52.2 Do not reuse as Listener behavior

Do not depend on:

```text
StalkerMemory
CoverageMemory
RegionGraph patrol coverage
GlobalPatrolPlanner
LocalPatrolSelector
StalkerSearchContext
StalkerSearchPlanner
Detection Meter
Stalker TargetSelector semantics
Stalker LastKnownPosition
Stalker attack Hit Moment rules
```

## 52.3 Namespace/dependency rule

Listener code must not import `EchoProtocol.AI.Stalker.*` merely to reuse generic behavior.

If a Stalker class contains truly shared navigation/debug/network utility:

```text
extract shared primitive
→ Shared namespace/module
→ Stalker adapter + Listener adapter consume it
```

Prefer composition over deep monster inheritance.

---

# 53. Implementation Plan / Order

Recommended incremental order:

1. confirm working branch and verify no hidden Listener source exists;
2. freeze `NoiseType` v0 + RuntimeNoiseEvent contract;
3. implement NoiseDefinition/NoiseCatalog;
4. implement authoritative emission resolver for existing gameplay actions;
5. implement NoiseSystem active storage + exactly-once dedup + bounded/reclaimable `DedupRetentionPolicy`;
6. add noise validation/dedup/expiry/retention stress tests;
7. implement HearingObservation + event-driven one-time HearingSensor evaluation;
8. implement distance attenuation + wall/door occlusion;
9. implement typed ListenerMemory including bounded `PendingHearingInbox` and terminal cleanup/history semantics;
10. implement deterministic ListenerNoiseSelector + hysteresis + CHASE spatial corroboration;
11. implement weak visual confirmation sensor;
12. implement five-state ListenerFSM with explicit same-tick arbitration;
13. implement simple ROAM route;
14. implement InvestigationPlanner + shared navigation binding/recovery;
15. implement ArrivalListen + FalseInvestigation + exactly-once InvestigationEpisode terminal lifecycle;
16. implement target conversion + CHASE with no terminal-episode resurrection;
17. implement generic Listener Attack/Recover with exactly-once side-effect guard and pending-hearing intake;
18. implement reason codes/read-only debug snapshot;
19. implement deterministic metric evaluators from immutable start/terminal traces;
20. bind RuntimeNoiseEvent to active `NOISE_EMITTED` telemetry;
21. implement Fusion State Authority Listener binding;
22. add 2/3/4-player, pending-hearing, dedup-retention, same-tick, late-join/resync tests;
23. profile physics/path/network/pending/dedup cost;
24. tune hearing/interrupt/corroboration/investigation values;
25. run monster-differentiation playtests against Stalker using the same route/content scenario where appropriate.

Dependency reason:

```text
noise contract + bounded idempotency
→ one-time hearing facts
→ bounded legal memory
→ selection/corroboration
→ deterministic FSM arbitration
→ investigation terminal lifecycle
→ navigation/action
→ network/metrics
```

Building FSM first would force developers to invent unavailable hearing/pending/arbitration semantics.

---

# 54. Configuration Matrix

No Listener parameter is AED mutable under M1-015 v0.

| Field / concept | Owner | Purpose | Runtime mutable? | AED mutable? | Status |
|---|---|---|---:|---:|---|
| NoiseType v0 catalog | Gameplay/Noise contract | categories | No | No | PROJECT BASELINE |
| NoiseDefinition.BaseLoudness | NoiseCatalog | source strength | config only | No | TUNING TBD |
| NoiseDefinition.HearingRadius | NoiseCatalog | broad-phase range | config only | No | TUNING TBD |
| NoiseDefinition.Lifetime | NoiseCatalog | pre-commit validity | config only | No | TUNING TBD |
| recurring emission interval | gameplay noise emitter | Sprint/CoreCarry emission cadence | config only | No | TUNING TBD |
| crouch noise multiplier | gameplay movement/noise config | quieter crouch | config only | No | TUNING TBD |
| ListenerHearingThreshold | HearingSensor | audibility threshold | config only | No | TUNING TBD |
| ClosedDoorMultiplier | HearingSensor | closed-door attenuation | config only | No | TUNING TBD |
| WallMultiplier | HearingSensor | wall attenuation | config only | No | TUNING TBD |
| PendingHearingInboxCapacity | ListenerMemory | bound uncommitted legal heard observations | config only | No | TUNING TBD |
| HypothesisMergeRadius | NoiseSelector | merge related support event | config only | No | TUNING TBD |
| InvestigationInterruptMargin | NoiseSelector | anti-oscillation | config only | No | TUNING TBD |
| ChaseNoiseInterruptThreshold | NoiseSelector | separated sound diversion from CHASE | config only | No | TUNING TBD |
| ChaseCorroborationRadius | NoiseSelector | spatially classify heard noise near current visible target as non-diverting | config only | No | TUNING TBD |
| top path-candidate bound | Selector/Planner | bounded path work | config only | No | TUNING TBD |
| investigation NavMesh projection bound | Planner | resolve off-mesh source | config only | No | TUNING TBD |
| InvestigationArrivalListenDuration | FSM | false-investigation terminal window | config only | No | TUNING TBD |
| Listener weak vision distance | visual confirmation | secondary confirmation | config only | No | TUNING TBD |
| Listener weak vision angle | visual confirmation | secondary confirmation | config only | No | TUNING TBD |
| approved visibility modifiers | visual confirmation | crouch/light generic modifier hook | config only | No | IMPLEMENTATION BINDING TBD |
| ROAM speed | Listener movement config | idle pressure | config only | No | TUNING TBD |
| INVESTIGATE speed | Listener movement config | noise response | config only | No | TUNING TBD |
| CHASE speed | Listener movement config | confirmed-target pressure | config only | No | TUNING TBD |
| attack range/hit reach | generic monster combat config | attack eligibility | config only | No | TUNING TBD |
| attack wind-up | generic monster combat config | telegraph | config only | No | TUNING TBD |
| attack recovery | generic monster combat config | anti-chain-hit | config only | No | TUNING TBD |
| active NoiseSystem capacity | NoiseSystem | bounded active-event storage | config only | No | TUNING TBD |
| DedupRetentionPolicy / replay-window representation | NoiseSystem/Fusion binding | bounded exactly-once retention through supported duplicate horizon | runtime binding | No | IMPLEMENTATION BINDING TBD |
| optional terminal InvestigationEpisode history capacity | ListenerMemory/debug | bounded immutable diagnostics/metrics | config only | No | TUNING TBD |
| `NoiseMaker.Cooldown=300s` | Team Tool | tool reuse | Host runtime | No | PROJECT BASELINE |
| exact NoiseDefinition serialization | content/config binding | authoring | n/a | No | IMPLEMENTATION BINDING TBD |
| acoustic LayerMask | HearingSensor | blocker filtering | scene binding | No | IMPLEMENTATION BINDING TBD |
| Listener ROAM waypoint positions | map authoring | idle route | scene | No | MAP AUTHORING TBD |
| exact `[Networked]` fields | Fusion binding | client presentation | network state | No | IMPLEMENTATION BINDING TBD |
| NetworkTransform settings | Fusion binding | proxy transform | network state | No | IMPLEMENTATION BINDING TBD |

---

# 55. Listener Hard Invariants

1. Listener hearing consumes RuntimeNoiseEvent, never TelemetryEvent.
2. Runtime noise validity is Host / Fusion State Authority owned.
3. Non-authoritative clients cannot inject arbitrary authoritative noise position/loudness/range.
4. One accepted logical emission creates at most one authoritative RuntimeNoiseEvent.
5. Client audio playback and animation SFX callbacks are not authoritative AI stimulus.
6. RuntimeNoiseEvent is an event-time snapshot and contains no live Player Transform.
7. HearingSensor produces hearing observations; it does not own semantic FSM transitions.
8. ListenerMemory contains only legally heard, legally visually observed, or legally derived information.
9. A noise event does not grant continuous hidden-player tracking.
10. Listener never follows a player's current hidden Transform merely because that player emitted an earlier noise.
11. SourcePlayerId metadata is not Listener hearing knowledge by default.
12. Competing-noise selection is deterministic for equal state/config.
13. No unseeded random tie-break is required by v1.0.
14. Expired/rejected noise cannot start a new investigation.
15. A committed investigation may remember a legitimately heard sound after global event expiry.
16. Weak noise spam does not interrupt a stronger committed investigation unless the explicit interrupt contract is met.
17. Noise Maker is not automatically selected; it competes through legal audibility/intensity/reachability.
18. Noise metadata alone never creates ConfirmedTarget.
19. Weak legal visual confirmation is the target-conversion source.
20. Listener has no Detection Meter.
21. Listener has no Stalker-style LKP/SearchContext.
22. On weak visual loss, ConfirmedTarget is cleared; no hidden pursuit continues.
23. Navigation does not select which noise to investigate.
24. PathPartial/PathInvalid are not exact investigation success.
25. Navigation failure is not a FalseInvestigation.
26. FalseInvestigation has one exact terminal definition.
27. Closed doors/walls attenuate hearing according to Listener policy; Hearing is not Stalker Vision LOS.
28. Door changes do not retroactively rewrite already heard evidence.
29. Normal navigation recovery does not teleport.
30. Telemetry never commands Listener.
31. RuntimeNoiseEvent and `NOISE_EMITTED` remain separate pipelines.
32. AED never directly selects noise, investigation, target, FSM state, or navigation destination.
33. No Listener parameter is adaptive-authorized in M1-015 v0.
34. Non-authoritative clients do not independently simulate authoritative Listener decisions.
35. Private NoiseSystem/ListenerMemory/Hearing history is not replicated by default.
36. ATTACK side effects are authoritative and at most once per attack episode.
37. RECOVER is mandatory after ATTACK under generic monster contract.
38. Stalker Detection/Search/Coverage behavior is not inherited without explicit evidence.
39. Listener ROAM does not require Stalker RegionGraph coverage.
40. Metrics use one name/version → one exact definition.
41. A historical HearingObservation is never physically re-evaluated using later Listener or door geometry.
42. Uncommitted heard observations may wait only in bounded `ListenerMemory.PendingHearingInbox`.
43. An expired pending HearingObservation cannot start or interrupt into a new InvestigationEpisode.
44. CHASE diversion/corroboration never uses SourcePlayerId, SourceEntityId, telemetry identity, or hidden target identity.
45. Noise spatially corroborating the currently visible target cannot divert CHASE or create self-noise oscillation.
46. The same authoritative collected world/input state produces one deterministic FSM transition regardless of component callback/update order.
47. NoiseSystem dedup memory is bounded/reclaimable and is never an unbounded whole-match replay ledger.
48. A dedup record is not retired before its logical emission is outside the supported duplicate/replay/resimulation horizon.
49. No terminal InvestigationEpisode remains as `CurrentInvestigation`.
50. At most one active InvestigationEpisode exists per Listener.
51. A terminal `PlayerConfirmed` InvestigationEpisode cannot later be silently resumed.
52. ATTACK/incomplete RECOVER may hear noise, but those observations remain uncommitted pending evidence only.

---

# 56. Definition of Done

| # | Question | v1.0 answer |
|---:|---|---|
| 1 | What creates RuntimeNoiseEvent? | Host after an approved authoritative gameplay emission |
| 2 | Who validates it? | gameplay action owner + Host NoiseSystem |
| 3 | Can client create arbitrary noise? | No |
| 4 | What fields are in NoiseEvent? | §11 |
| 5 | What fields may Listener read? | public event fields only; not Host source identity |
| 6 | Does NoiseEvent contain live Transform? | No |
| 7 | How does expiry work? | §16; expired uncommitted evidence cannot start investigation |
| 8 | What is HearingObservation? | immutable legal heard-time audibility fact, §20 |
| 9 | Hearing range calculation? | linear distance factor + threshold, §17 |
| 10 | Walls? | attenuation, strongest blocker class |
| 11 | Doors? | open clear; closed attenuation at hearing evaluation time |
| 12 | Multiple noises? | deterministic lexicographic rank + bounded path validation |
| 13 | What interrupts investigation? | stronger reachable unrelated noise above margin, subject to same-tick priority |
| 14 | Spam prevention? | related merge + interrupt margin/commitment |
| 15 | ListenerMemory owns? | one current investigation, confirmed target ID, bounded pending inbox/history |
| 16 | Listener FSM? | ROAM/INVESTIGATE/CHASE/ATTACK/RECOVER |
| 17 | Why each state? | §25.2 |
| 18 | Heard noise → investigation? | selector + still-unexpired + PathComplete commitment |
| 19 | Investigation → confirmed target? | weak legal visual confirmation only |
| 20 | Omniscience boundary? | no SourcePlayer/current Transform/velocity from hearing |
| 21 | Player moves after noise? | old snapshot remains; no hidden retarget |
| 22 | Empty source? | ArrivalListen → exact FalseInvestigation terminal |
| 23 | False investigation exact? | §33 |
| 24 | PathPartial? | rejection/navigation failure, never arrival |
| 25 | Unreachable noise? | reject before commitment or NavigationFailed after topology change |
| 26 | Dynamic door effect? | new sound uses current door state; historical HearingObservation unchanged; active path may replan |
| 27 | Stuck recovery? | progress-based bounded ladder, §38 |
| 28 | Host-only state? | noise/hearing/pending memory/FSM/arbitration/planning/action |
| 29 | Replicated state? | transform + semantic/action presentation minimum |
| 30 | Not replicated? | noise queue, pending inbox, ListenerMemory, hearing/terminal history, source identity/scores |
| 31 | Late join? | reconstruct durable current presentation; no noise/pending/history replay |
| 32 | Duplicate noise prevention? | AuthoritativeEmissionKey/equivalent + bounded reclaimable NoiseSystem dedup |
| 33 | Telemetry affect hearing? | No |
| 34 | AED choose investigation? | No |
| 35 | NoiseResponseLatency? | mean HeardAt→InvestigationCommittedAt; includes legal pending wait |
| 36 | SourceSelection? | versioned shares of new episode starts by noiseType/reason |
| 37 | FalseInvestigationRate? | false / (false + player-confirmed) eligible terminal traces |
| 38 | Tests? | §48 |
| 39 | Stalker reuse? | navigation/debug/Fusion/test patterns only, §52 |
| 40 | Implementation order? | §53 |
| 41 | Where does hearing during ATTACK/RECOVER live? | bounded `ListenerMemory.PendingHearingInbox` |
| 42 | Is old hearing re-raycast later? | No; HearingSensor evaluates each logical event once at its authoritative hearing moment |
| 43 | What happens if pending hearing expires? | remove/discard; cannot start investigation |
| 44 | How is pending overflow handled? | expired-first, retain highest-ranked deterministically, EventOrderKey tie-break |
| 45 | Can visible target movement noise divert CHASE? | not when spatially corroborating within ChaseCorroborationRadius |
| 46 | Does CHASE corroboration use SourcePlayerId? | No; position-to-position comparison only |
| 47 | What resolves multiple same-tick FSM guards? | explicit per-state priority in §26.2; at most one semantic transition |
| 48 | Can dedup state grow unbounded for the match? | No; bounded/reclaimable retention through supported replay horizon |
| 49 | Does PlayerConfirmed leave CurrentInvestigation active? | No; terminalize/trace/clear before or atomically with CHASE target install |
| 50 | Can CHASE loss reopen old PlayerConfirmed episode? | No; only current/pending legal hearing can create a NEW episode |

All P0/P1 runtime semantics targeted by LIS-DD-01 through LIS-DD-05 are resolved.

Remaining items are numerical tuning, map authoring, and concrete Unity/Fusion binding that do not change the frozen behavior semantics.

---

# 57. Open Tuning / Bindings

## 57.1 TUNING TBD

- BaseLoudness by noise type;
- HearingRadius by noise type;
- RuntimeNoiseEvent lifetime;
- recurring Sprint/CoreCarry emission interval;
- crouch movement-noise multiplier;
- ListenerHearingThreshold;
- wall attenuation multiplier;
- closed-door attenuation multiplier;
- `PendingHearingInboxCapacity`;
- optional immutable terminal InvestigationEpisode history capacity;
- HypothesisMergeRadius;
- InvestigationInterruptMargin;
- ChaseNoiseInterruptThreshold;
- `ChaseCorroborationRadius`;
- maximum candidate paths per selection;
- NavMesh projection bound;
- ArrivalListenDuration;
- weak vision range/angle;
- ROAM/INVESTIGATE/CHASE speeds;
- attack range/wind-up/recovery;
- active NoiseSystem capacity;
- path-pending/no-progress/stuck thresholds;
- sensor/planner cadence where event-driven processing is insufficient;
- final performance budgets;
- quality acceptance thresholds.

## 57.2 IMPLEMENTATION BINDING TBD

- exact C# names/namespaces;
- exact NoiseEventBus/event-bus implementation;
- NoiseDefinition serialization;
- authoritative emission-key representation;
- event ID representation;
- exact `DedupRetentionPolicy` data structure/window/watermark representation;
- mapping from supported Fusion/request replay/resimulation horizon to dedup retirement proof;
- player/noise source component APIs;
- LayerMask and acoustic blocker components;
- exact weak-vision visibility modifier provider;
- exact NavMesh projection API wrapper;
- exact shared navigation extraction from Stalker code;
- exact Fusion `[Networked]` fields;
- exact transform sync settings;
- exact RPC/input usage;
- exact generic player life-state/attack hit-query binding.

## 57.3 MAP AUTHORING TBD

- Listener ROAM waypoint coordinates;
- acoustic blocker layers/material binding;
- exact dynamic DoorId acoustic-state binding;
- any special Listener NavMeshLink eligibility.

These TBDs may not alter:

- one-time hearing evaluation;
- bounded pending-hearing memory;
- CHASE spatial corroboration without source identity;
- same-tick arbitration priority;
- bounded/reclaimable dedup behavior;
- InvestigationEpisode terminal cleanup.

---

# 58. Architecture Escalations

```text
ARCHITECTURE ESCALATION REQUIRED: NO
```

No supplied evidence invalidates:

```text
RuntimeNoiseEvent
→ NoiseSystem
→ HearingSensor
→ HearingObservation
→ ListenerMemory
→ FSM
→ Planner
→ Navigation / Action
```

or:

- traditional/rule-based monster runtime;
- Host/State Authority;
- typed observation/memory;
- runtime-noise/telemetry separation.

The explicit five-state Listener FSM is a subsystem detailed-design refinement of older generic monster-state wording and remains inside the baselined architecture.

---

# 59. References

## Project sources

1. `AI_Architecture_v1.1.md`.
2. `ECHO PROTO.docx`.
3. `ECHO PROTO(1).docx`.
4. `01_ECHO_PROTOCOL_Project_Scope_REVISED.docx`.
5. `02_ECHO_PROTOCOL_System_Architecture_REVISED.docx`.
6. `03_ECHO_PROTOCOL_Implementation_Spec_REVISED.xlsx`.
7. `KLTN.docx` — Research Facility Map Flow / Objective Layout.
8. `KLTN (1).docx` — Multiplayer organization/synchronization decision.
9. `Telemetry_Event_Schema_v0_FINAL.md`.
10. `M1-015_ScenarioConfig_AED_Fairness_Policy_v0_FINAL.md`.
11. `M1-020_Test_Strategy_Fixed_vs_Adaptive_Experiment_v0_FINAL.md`.
12. `Stalker_AI_Design_v1.1.md` — reusable architecture patterns only.
13. `AI_Architecture_Traditional_vs_Modern.md` — historical evidence only where not superseded.

## Official external sources

14. Unity Technologies — `NavMeshAgent.SetDestination`:  
    https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AI.NavMeshAgent.SetDestination.html

15. Unity Technologies — `NavMeshAgent`:  
    https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AI.NavMeshAgent.html

16. Unity Technologies — `NavMesh.CalculatePath` / `NavMeshPathStatus`:  
    https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AI.NavMesh.CalculatePath.html  
    https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AI.NavMeshPathStatus.html

17. Unity Technologies — `Physics.Raycast`:  
    https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Physics.Raycast.html

18. Unity Technologies — physics query optimization:  
    https://docs.unity3d.com/6000.0/Documentation/Manual/physics-optimization-raycasts-queries.html

19. Unity Technologies — Unity Test Framework:  
    https://docs.unity3d.com/6000.0/Documentation/Manual/com.unity.test-framework.html

20. Unity Technologies — Profiler:  
    https://docs.unity3d.com/Manual/Profiler.html

21. Photon Engine — Fusion 2 State Authority / Input Authority:  
    https://doc.photonengine.com/fusion/v2/manual/playerref

22. Photon Engine — Fusion 2 Client-Server Player Input / `FixedUpdateNetwork`:  
    https://doc.photonengine.com/fusion/v2/manual/input/player-input

23. Photon Engine — Fusion 2 Networked Properties:  
    https://doc.photonengine.com/fusion/v2/manual/data-transfer/networked-properties

24. Photon Engine — Fusion 2 RPCs:  
    https://doc.photonengine.com/fusion/v2/manual/data-transfer/rpcs

25. Photon Engine — Fusion 2 Network Simulation Loop:  
    https://doc.photonengine.com/fusion/v2/concepts-and-patterns/network-simulation-loop

---

# Listener Detailed-Design Correction Report

| ID | Issue | Resolution | Status |
|---|---|---|---|
| LIS-DD-01 | Pending HearingObservation lifecycle during non-interruptible states | Added bounded `ListenerMemory.PendingHearingInbox`; one-time heard-time geometry; expiry/overflow/consumption semantics; no historical re-raycast | RESOLVED |
| LIS-DD-02 | CHASE corroborating/self-noise ambiguity | Added `ChaseCorroborationRadius`; nearby hearing is `CorroboratesVisibleTarget`; no SourcePlayerId/source identity dependency | RESOLVED |
| LIS-DD-03 | Same-tick FSM transition arbitration | Added explicit per-state deterministic priority and at-most-one semantic transition per authoritative decision step | RESOLVED |
| LIS-DD-04 | Bounded NoiseSystem dedup retention | Added bounded/reclaimable dedup-retention invariant tied to supported duplicate/replay/resimulation horizon; unbounded whole-match HashSet forbidden | RESOLVED |
| LIS-DD-05 | Investigation terminal cleanup / CurrentInvestigation lifecycle | Added terminal table, exactly-once trace, clear/replace semantics, at-most-one active episode, and no PlayerConfirmed resurrection | RESOLVED |

All five corrections are propagated through runtime flow, ownership, lifecycle, FSM, selection, CHASE/RECOVER, Fusion authority, observability, reason codes, metrics, performance, tests, edge cases, component contracts, configuration, invariants, DoD, and final audits.

No top-level architecture change was required.

---

# Detailed Design Validation

```text
Architecture baseline respected: YES
Runtime NoiseEvent separated from telemetry: YES
Host authority preserved: YES
Runtime NoiseEvent contract complete: YES
Noise expiry/dedup contract complete: YES
Hearing propagation/occlusion contract complete: YES
Competing-noise selection complete: YES
Listener legal-information boundary complete: YES
Listener FSM complete: YES
Investigation lifecycle complete: YES
Target-conversion semantics complete: YES
False-investigation semantics complete: YES
Navigation/recovery complete: YES
Fusion binding sufficiently specified: YES
Metrics deterministic: YES
Test plan complete: YES
Implementation plan complete: YES

Pending HearingObservation lifecycle deterministic: YES
Historical hearing is never re-evaluated: YES
Pending hearing storage bounded: YES
CHASE corroborating-noise guard complete: YES
CHASE diversion requires no hidden source identity: YES
Same-tick FSM arbitration deterministic: YES
Noise dedup retention bounded: YES
Investigation terminal cleanup complete: YES
At most one active InvestigationEpisode: YES
Terminal PlayerConfirmed episode cannot be resurrected: YES

Architecture escalation required: NO
```

---

# Final Consistency Audit

```text
Telemetry DB can feed Listener hearing: NO
Client can forge authoritative noise: NO
Noise event can expose live hidden Player Transform: NO
Expired event can start new investigation: NO
Competing-noise result can be nondeterministic without explicit seed: NO
Duplicate gameplay action can create duplicate logical noise by contract: NO
Listener blindly inherits Stalker Detection/Search/Coverage behavior: NO
AED can directly select investigation/target/FSM: NO
Proxy client can independently simulate authoritative Listener AI: NO

Old HearingObservation can be re-raycast using later geometry: NO
Expired pending HearingObservation can start investigation: NO
Pending hearing storage can grow unbounded: NO
Visible target's own nearby movement noise can force CHASE oscillation: NO
CHASE corroboration requires SourcePlayerId: NO
FSM result can depend on callback/update order: NO
Dedup ledger can grow for entire match without bound: NO
Two CurrentInvestigation episodes can exist simultaneously: NO
Terminal PlayerConfirmed investigation remains active: NO
Terminal investigation can be silently resumed: NO
```

All expected audit values are `NO`.

No critical P0/P1 runtime semantic remains unresolved by LIS-DD-01 through LIS-DD-05.

```text
Detailed Design Status: BASELINED v1.0
Recommended Status: BASELINED v1.0
Architecture Escalation Required: NO
```

**End of `Listener_AI_Design_v1.0.md`**
