# ECHO PROTOCOL — Telemetry Contract v1.1

**Canonical document:** `Telemetry_Contract_v1.1.md`  
**Game:** ECHO PROTOCOL — Co-op Survival Horror Multiplayer  
**Document Revision:** v1.1  
**Wire schema version introduced by this document:** `"1.1"`  
**Legacy wire schema preserved:** `"1.0"`  
**Parent Architecture:** `AI_Architecture_v1.1.md` — BASELINED v1.1  
**Predecessor:** `Telemetry_Event_Schema_v0_FINAL.md` — M1-008 DONE / FROZEN, serialized `schemaVersion = "1.0"`  
**Referenced detailed designs:** `Stalker_AI_Design_v1.1.md`, `Listener_AI_Design_v1.0.md`, `Warden_AI_Design_v1.0.md`  
**Environment:** Unity `6000.5.8f1`; Photon Fusion `2.1.1 Stable`, build `2177`; Host Mode; 2–4 Players  
**Contract Status:** BASELINED v1.1  
**Implementation Status:** See §73 — Current Implementation Assessment

> This document is a schema and implementation contract. It does not claim that Unity telemetry emitters, backend ingestion, tests, profiling, research capture, or Fixed-vs-Adaptive experiment execution are already complete.

---

# 1. Document Control

| Field | Contract |
|---|---|
| Document Role | **M2 telemetry implementation contract and schema evolution contract.** |
| Predecessor | `Telemetry_Event_Schema_v0_FINAL.md` |
| Document Revision | `v1.1` |
| Wire Schema Version | **`"1.1"`** |
| Legacy Wire Schema | **`"1.0"` remains frozen and valid on its own validator path** |
| Canonical raw entity | `TelemetryEvent` |
| Canonical transport | Host/service-authenticated `POST /telemetry/batch` semantic |
| Delivery model | at-least-once transport attempt + stable logical identity + idempotent backend storage |
| Ordering owner | one Host-owned `TelemetrySequenceAllocator` per match |
| Gameplay authority | gameplay systems remain authoritative; telemetry only observes committed facts |
| Backend authority | validation, idempotent raw storage, acknowledgement, data-quality evidence |
| Profile/AED boundary | telemetry → MatchTelemetry → MatchScore/Profile → AED; never reverse into runtime monster decisions |
| Experiment readiness | **NOT READY merely because this contract exists**; M1-020 gates remain authoritative |
| Current implementation | **NOT EVIDENCED FROM SUPPLIED SOURCE** beyond contracts/planning evidence |

## 1.1 Classification labels

`PROJECT BASELINE`, `SCHEMA v1.1 DECISION`, `TRANSPORT v1.1 DECISION`, `RESEARCH CONTRACT`, `CURRENT IMPLEMENTATION`, `TUNING TBD`, `IMPLEMENTATION BINDING TBD`, `TELEMETRY CONTRACT REVISION REQUIRED`, `ARCHITECTURE ESCALATION`.

---

# 2. Purpose

This document combines:

1. the v1.1 event schema/evolution contract; and
2. the transport, ordering, idempotency, completeness, and research-evidence contract.

A developer must not have to guess what one logical event is, who may create it, how it is ordered, whether retry creates a new event, how partial batches are retried, which AI facts are persistent, or whether missing data is zero.

Quality goals:

```text
Authoritative
Deterministic where semantics require it
Idempotent
Order-reconstructable
Versioned
Backward-aware
Research-reproducible
Privacy-bounded
Performance-bounded
Failure-aware
Testable
Observable
No gameplay authority inversion
```

---

# 3. Scope / Non-Goals

In scope: authoritative occurrence, `id`, time/order/provenance, catalog/status, all v1.0 migration decisions, minimum Listener/Warden research events, bounded buffer/batch/retry, backend validation/idempotency, data-quality/completeness, Profile/research coverage, and tests.

Out of scope: gameplay control from telemetry, Profile/AED formula redesign, deferred position/team sampling contracts, raw voice/chat/camera capture, per-frame transform telemetry, HTTP/database framework choice, fabricated profiler/test results, and declaring the live Fixed-vs-Adaptive experiment ready.

---

# 4. Source Priority / Governance

```text
1. AI_Architecture_v1.1.md
2. Telemetry_Event_Schema_v0_FINAL.md
3. approved project gameplay/system contracts
4. Stalker_AI_Design_v1.1.md
5. Listener_AI_Design_v1.0.md
6. Warden_AI_Design_v1.0.md
7. M1-015_ScenarioConfig_AED_Fairness_Policy_v0_FINAL.md
8. M1-014_Player_Team_Profile_Fields_Formulas_v0_FINAL.md
9. M1-020_Test_Strategy_Fixed_vs_Adaptive_Experiment_v0_FINAL.md
10. current implementation evidence
11. historical notes/spikes
12. official Unity / Photon Fusion 2 documentation for engine/network facts only
```

```text
approved project behavior > current implementation assumptions
detailed-design semantic owner > telemetry interpretation of that gameplay semantic
official engine/network semantics > implementation assumptions
```

Telemetry observes authoritative gameplay facts. It does not redefine Stalker, Listener, Warden, Player Life-State, or Warden route-safety behavior. A conflicting implementation is an implementation gap/migration issue; it does not silently rewrite this contract. Historical wire `"1.0"` is not retroactively changed.

---

# 5. Core Telemetry Boundary

```text
Authoritative Gameplay Fact
→ Telemetry Adapter
→ TelemetryEvent
→ Buffer / Batch
→ Backend Validation
→ Idempotent Raw Storage
→ MatchTelemetry
→ MatchScore
→ PlayerAIProfile / TeamProfile
→ decision-scoped adaptive input boundary
→ AED Input Gate
```

Forbidden:

```text
TelemetryEvent → Stalker FSM
Telemetry DB → Listener Hearing
TelemetryEvent → Warden Policy
Telemetry → Target Selection / Navigation / Attack / Damage
Telemetry → direct ScenarioConfig mutation
```

Runtime fact and telemetry representation are different contracts. For Listener:

```text
authoritative gameplay action
→ RuntimeNoiseEvent
   ├→ NoiseSystem → Hearing
   └→ NoiseTelemetryAdapter → NOISE_EMITTED
```

---

# 6. Document Revision vs Serialized Schema Version

Predecessor:

```text
document: Telemetry Event Schema v0
wire: schemaVersion = "1.0"
```

v1.1:

```text
document: Telemetry_Contract_v1.1.md
wire: schemaVersion = "1.1"
```

Wire `"1.1"` is required because this contract adds required ordering/provenance context, `NOISE_EMITTED.data.noiseEventId`, newly emittable research events, Warden event types, and versioned reason validation.

```text
"1.0" → frozen v1.0 validator
"1.1" → this validator
stored v1.0 event X→ rewritten as v1.1
```

A v1.1-capable backend should support both validator paths.

---

# 7. Common `TelemetryEvent` v1.1

Serialized top-level fields remain:

```text
id
matchId
userId
eventType
ts
valueJson
reasonCode
schemaVersion
```

Do not serialize `eventId` in addition to `id`.

| Field | v1.1 | Semantic |
|---|---|---|
| `id` | REQUIRED | globally unique logical telemetry identity |
| `matchId` | REQUIRED | authoritative match identity |
| `userId` | event-specific | primary player subject or required null |
| `eventType` | REQUIRED | registered v1.1 event type |
| `ts` | REQUIRED | authoritative occurrence UTC timestamp |
| `valueJson` | REQUIRED | `{context:{}, data:{}}` |
| `reasonCode` | controlled event-specific value or null | occurrence reason |
| `schemaVersion` | REQUIRED | exactly `"1.1"` |

## 7.1 Required common context on every v1.1 event

```text
context.eventSequence
context.authorityTick
context.scenarioConfigVersion
context.policyVersion
context.configSource
```

| Field | Rule |
|---|---|
| `eventSequence` | positive integer; one Host match-wide monotonic sequence |
| `authorityTick` | integer >= 0 or explicit `null` when no meaningful Fusion simulation tick exists |
| `scenarioConfigVersion` | AppliedScenarioConfig version active at occurrence |
| `policyVersion` | M1-015 ScenarioConfig/Fairness contract version on active config; required for FIXED and ADAPTIVE |
| `configSource` | `FIXED` or `ADAPTIVE` |
| `phase` | conditional-required by event contract |
| `position` | conditional event-time snapshot only |

Retry cannot refresh these fields from later runtime state.

---

# 8. `valueJson` Contract

Preserve:

```json
{
  "context": {},
  "data": {}
}
```

`context` = shared occurrence/provenance.  
`data` = event-specific payload.

`scenarioConfigVersion` remains under `valueJson.context.scenarioConfigVersion`.

v1.1 validation is strict by default: unknown common/event-specific fields are rejected unless an explicit versioned extension point permits them.

---

# 9. Authoritative Event Occurrence

```text
client input/request
→ Host validation / State Authority simulation
→ authoritative gameplay fact commits
→ telemetry event may be created
```

Client input, prediction, proxy interpolation, local SFX, UI callbacks, or animation events are not authoritative production facts by themselves.

---

# 10. `TelemetryEvent.id` / Logical Identity

**SCHEMA v1.1 DECISION**

```text
TelemetryEvent.id
=
globally unique logical event identity
```

Exact GUID/ULID/collision-safe string format is `IMPLEMENTATION BINDING TBD`; uniqueness scope is not.

One logical event has one stable:

```text
id
eventSequence
ts
payload
schemaVersion
```

across retry.

Every adapter supplies a stable `SourceOccurrenceKey`. Existing authoritative episode/action IDs are preferred:

```text
noiseEventId
monsterId + attackEpisodeId
monsterId + investigationEpisodeId
monsterId + searchEpisodeId
wardenActionId + lifecycle stage
```

Systems without such an ID must expose a stable authoritative transition occurrence identity/ordinal. Entity identity alone is insufficient for repeatable transitions.

Conceptual factory:

```text
CreateOnce(SourceOccurrenceKey, EventType)
→ same logical occurrence reuses same id + sequence + immutable event
```

Exact derivation/retention implementation is TBD, but retention must cover the supported duplicate/callback/resimulation horizon.

---

# 11. Gameplay Episode ID != Telemetry ID

Gameplay identities remain gameplay-owned:

```text
noiseEventId
attackEpisodeId
investigationEpisodeId
searchEpisodeId
wardenActionId
```

They may be serialized only where a correlation consumer exists. They never replace `TelemetryEvent.id`.

Default join scopes:

```text
noise   → matchId + noiseEventId
monster → matchId + monsterId + episodeId
Warden  → matchId + wardenActionId
```

---

# 12. Exactly-Once Logical Semantics

Transport may deliver more than once:

```text
same immutable TelemetryEvent.id delivered N times
→ one logical stored raw event
```

Source duplication must also be guarded:

```text
one authoritative state transition
→ one canonical telemetry owner
→ zero or one schema-approved event of each applicable type
```

Examples:

```text
one RuntimeNoiseEvent → at most one NOISE_EMITTED
one AttackEpisode resolution → at most one MONSTER_ATTACK_RESOLVED
one WardenActionId + APPLIED → at most one WARDEN_ROUTE_ACTION_APPLIED
```

---

# 13. Same `id` + Different Payload

Semantic-equivalent retry:

```text
same id
+ same immutable semantic fields
→ DUPLICATE_ALREADY_ACCEPTED
```

Parsed JSON object key order/whitespace do not create a semantic difference; array order remains semantic.

Conflict:

```text
same id
+ different eventType/matchId/userId/ts/reason/schemaVersion/context/data
→ IDENTITY_CONFLICT
→ do not overwrite
→ permanently reject/quarantine incoming copy
→ critical diagnostic
```

No last-write-wins.

---

# 14. `ts` Semantics

```text
ts
=
authoritative fact occurrence wall-clock timestamp
```

Required: UTC ISO-8601 with `Z`.

Not backend receive time, batch time, retry time, proxy time, or presentation time.

Retry never changes `ts`.

M1-014 phase/objective time continues to derive from matching `PHASE_STARTED.ts` / `PHASE_COMPLETED.ts`.

---

# 15. Authoritative Tick + Event Order

`context.authorityTick` = Fusion authoritative simulation tick associated with commit when meaningful; otherwise explicit null.

`context.eventSequence` = Host-owned monotonically increasing logical telemetry sequence within a match.

Rules:

```text
first match event = sequence 1
new logical event consumes next sequence exactly once
sequence never reused
retry reuses sequence
duplicate logical occurrence reuses original event/sequence
```

A valid stream starts:

```text
MATCH_STARTED.eventSequence = 1
```

Canonical ordering:

```text
(matchId, eventSequence)
```

Same tick:

```text
same authorityTick
→ eventSequence resolves total order
```

Backend receive order is not gameplay order.

Backend also enforces:

```text
(matchId,eventSequence) → exactly one logical TelemetryEvent.id
```

Same sequence + different ID = `SEQUENCE_CONFLICT`.

---

# 16. Central Order Ownership

One Host-owned `TelemetrySequenceAllocator` owns the entire match sequence domain.

Do not create separate Stalker/Listener/Player/Warden sequences.

Sequence is allocated when the logical telemetry event is created after its source fact commits. If one gameplay action commits multiple distinct facts, each fact receives its own sequence in authoritative source commit order.

---

# 17. Events Outside `FixedUpdateNetwork()`

An authoritative lifecycle fact outside a meaningful Fusion simulation tick still gets:

```text
id
eventSequence
ts
scenarioConfigVersion
policyVersion
configSource
```

and:

```json
"authorityTick": null
```

Do not fabricate a tick.

---

# 18. Retry Must Not Mutate Occurrence

After creation, all logical event fields are immutable.

Transport-only metadata may include attempt count, batch transport ID, last attempt time, and transport error, but none of it changes the TelemetryEvent.

---

# 19. Arrival Order

If sequence 102 arrives before 99, storage preserves both and ordered aggregation uses `eventSequence`.

A later valid event may fill a previously detected sequence gap. Raw records are never reordered/mutated to match arrival time.

---

# 20. Sequence Gap / Data Completeness

```text
sequences accepted: 1,2,3,5
accepted final MATCH_ENDED.sequence = 5
→ sequence 4 missing
→ stream INCOMPLETE
```

A gap does not reveal the cause.

`MATCH_ENDED.eventSequence` is the expected upper bound when accepted and no later match TelemetryEvent is allowed after the authoritative terminal occurrence.

Canonical:

```text
TelemetryStreamCompleteness
= COMPLETE | INCOMPLETE | INVALID | UNKNOWN
```

`COMPLETE` requires:

1. accepted `MATCH_STARTED` sequence 1;
2. accepted terminal `MATCH_ENDED` sequence N;
3. exactly one accepted logical event for every sequence 1..N;
4. no identity/sequence conflict;
5. required provenance valid.

`INCOMPLETE`: gaps, missing required lifecycle evidence, permanent loss/rejection, observable buffer overflow, or known-ended match without complete stream.

`INVALID`: identity/sequence corruption or contradictory provenance that makes interpretation unsafe.

`UNKNOWN`: live/not-yet-finalized stream or unresolved terminal status.

Late events may improve derived completeness; raw events remain immutable.

```text
Telemetry completeness != Profile eligibility != Experiment eligibility
```

---

# 21. Match Lifecycle / Final Sequence

No new `MATCH_ABORTED` event is introduced.

Preserve:

```text
MATCH_ENDED.reasonCode = MATCH_ABORTED
```

when the Host can record an abort.

If Host/process dies before terminal emission, do not fabricate `MATCH_ENDED`. The stream remains UNKNOWN or becomes INCOMPLETE when an external Match record proves termination.

---

# 22. Version / Provenance Metadata

Every v1.1 event self-identifies active config:

```text
context.scenarioConfigVersion
context.policyVersion
context.configSource
```

`MATCH_STARTED` additionally anchors:

```text
context.teamSize
context.buildVersion
context.mapContentVersion
context.contentWhitelistVersion
context.researchCaptureEnabled
data.mapId
```

Conditional when experiment active:

```text
context.experimentCondition
context.experimentProtocolVersion
```

Optional/test-harness dependent:

```text
context.testRunId
context.experimentRunId
```

Conditional when required by run/config evidence:

```text
context.parameterRegistryVersion
context.fixedBaselineId
```

Do not put Profile formula versions on every raw event; they belong to processed outputs/evidence manifests.

---

# 23. Mid-Match ScenarioConfig Version

Each event captures:

```text
scenarioConfigVersion = config active at that occurrence
policyVersion         = policy contract on that config
configSource          = FIXED | ADAPTIVE on that config
```

A retry after a later config application retains the earlier values.

---

# 24. Fixed vs Adaptive Condition

When no approved experiment is active, experiment fields are absent.

When active:

```text
MATCH_STARTED.context.experimentCondition = FIXED | ADAPTIVE
MATCH_STARTED.context.experimentProtocolVersion = approved version
```

Assigned condition is distinct from event-time `configSource`.

Example:

```text
experimentCondition = ADAPTIVE
later configSource = FIXED
```

is valid evidence of fallback/exposure and does not relabel the match.

Telemetry support does not make live Adaptive experiment ready.

---

# 25. Event Status Model

| Status | Serializable? | Intended use |
|---|---:|---|
| `ACTIVE_PRODUCTION` | Yes | normal backend telemetry |
| `RESEARCH_CAPTURE` | Yes when approved research capture is enabled | backend thesis/research evidence |
| `RESERVED_NOT_EMITTED` | No | future name; validator rejects |
| `DEVELOPMENT_DIAGNOSTIC_ONLY` | No | local/debug/evidence only |

Research-capture events obey the same identity/order/retry/schema rules as production events.

Every `RESEARCH_CAPTURE` event additionally requires:

```text
context.researchCaptureEnabled = true
```

The Host producer must not create these events when the match/run capture flag is false. A v1.1 backend rejects a research event whose required capture flag is absent/false; this is instrumentation validation only and never changes gameplay.

---

# 26. Canonical Event Catalog Migration Matrix

| Event | v1.0 | v1.1 | Owner | userId | Consumer / note |
|---|---|---|---|---|---|
| `MATCH_STARTED` | ACTIVE | ACTIVE_PRODUCTION | Match | null | provenance/completeness |
| `MATCH_ENDED` | ACTIVE | ACTIVE_PRODUCTION | Match | null | outcome/completeness |
| `PHASE_STARTED` | ACTIVE | ACTIVE_PRODUCTION | Phase | null | lifecycle/objective time |
| `PHASE_COMPLETED` | ACTIVE | ACTIVE_PRODUCTION | Phase | null | lifecycle/objective time |
| `CORE_PICKED_UP` | ACTIVE | ACTIVE_PRODUCTION | Core/Objective | acting player | objective fact |
| `CORE_DROPPED` | ACTIVE | ACTIVE_PRODUCTION | Core/Objective | acting player | objective fact |
| `CORE_PLACED` | ACTIVE | ACTIVE_PRODUCTION | Core/Objective | acting player | objective fact |
| `PUZZLE_COMPLETED` | ACTIVE | ACTIVE_PRODUCTION | Puzzle/Objective | null | objective fact |
| `PUZZLE_FAILED` | RESERVED/CONDITIONAL | RESERVED_NOT_EMITTED | Puzzle | unresolved | no frozen consequence |
| `SECURITY_HOLD_STARTED` | RESERVED | RESERVED_NOT_EMITTED | Phase | null | generic phase event remains source |
| `SECURITY_HOLD_INTERRUPTED` | ACTIVE | ACTIVE_PRODUCTION | Security Hold | null | unique interruption |
| `SECURITY_HOLD_COMPLETED` | RESERVED | RESERVED_NOT_EMITTED | Phase | null | generic phase event remains source |
| `FINAL_HUNT_STARTED` | RESERVED | RESERVED_NOT_EMITTED | Phase | null | generic phase event remains source |
| `PLAYER_DOWNED` | ACTIVE | ACTIVE_PRODUCTION | Life-State | affected player | survival |
| `PLAYER_REVIVED` | ACTIVE | ACTIVE_PRODUCTION | Life-State | revived player | revive fact |
| `PLAYER_ELIMINATED` | ACTIVE | ACTIVE_PRODUCTION | Life-State | affected player | survival |
| `PLAYER_ESCAPED` | ACTIVE | ACTIVE_PRODUCTION | Exit/Life-State | escaped player | survival |
| `TEAM_TOOL_USED` | ACTIVE | ACTIVE_PRODUCTION | Team Tool | acting player | usage fact |
| `PLAYER_RESCUED` | RESERVED | RESERVED_NOT_EMITTED | unresolved | unresolved | still no independent rescue outcome |
| `HELP_PING_USED` | ACTIVE | ACTIVE_PRODUCTION | Ping/Life-State | acting Downed player | help-ping fact |
| `NOISE_EMITTED` | ACTIVE | ACTIVE_PRODUCTION | Runtime Noise | acting player for current v0 NoiseType | noise/profile + Listener correlation |
| `MONSTER_TARGET_ACQUIRED` | RESERVED | RESERVED_NOT_EMITTED | Stalker | — | no current required persistent consumer |
| `MONSTER_TARGET_LOST` | RESERVED | RESERVED_NOT_EMITTED | Stalker | — | avoid unnecessary target/position persistence |
| `MONSTER_INVESTIGATE_STARTED` | RESERVED | RESEARCH_CAPTURE | Listener | null | latency/source selection |
| `MONSTER_ATTACK_RESOLVED` | RESERVED | RESEARCH_CAPTURE | Stalker/Listener | null | attack episode evidence |
| `MONSTER_SEARCH_ENDED` | RESERVED | RESEARCH_CAPTURE | Stalker | null | search reacquisition |
| `MONSTER_INVESTIGATE_RESOLVED` | absent | RESEARCH_CAPTURE — NEW | Listener | null | false investigation |
| `WARDEN_TELEGRAPH_STARTED` | absent | RESEARCH_CAPTURE — NEW | Warden | null | telegraph fairness |
| `WARDEN_ROUTE_ACTION_APPLIED` | absent | RESEARCH_CAPTURE — NEW | Warden | null | route pressure / applied action |
| `WARDEN_ROUTE_SAFETY_CHECKED` | absent | RESEARCH_CAPTURE — NEW | Warden | null | objective reachability |
| `WARDEN_ROUTE_ACTION_RELEASED` | absent | RESEARCH_CAPTURE — NEW | Warden | null | fail-safe/invalid lock |

Payload enum legality for all emittable schema `"1.1"` events is governed by the canonical registry in §55.1 (or its explicitly named versioned semantic-owner registry). Event status does not authorize an unregistered enum value.

## 26.1 `PLAYER_DOWNED` v1.1 reason correction

v1.0 historical validator preserves legacy `WARDEN_ATTACK`.

v1.1 allows:

```text
STALKER_ATTACK
LISTENER_ATTACK
```

and rejects `WARDEN_ATTACK`, because Warden v1.0 does not evidence physical combat. Future combat would require a later contract revision.

---

# 27. `userId` Ownership

```text
player action → acting player
player outcome → affected player
team/system/objective/monster episode/Warden route episode → null
```

Two-player event example:

```text
PLAYER_REVIVED.userId = revived player
PLAYER_REVIVED.data.reviverPlayerId = reviver
```

Monster/Listener/Warden research events use `userId = null` to minimize persisted player identity and avoid confusing analytical episode data with gameplay target authority.

---

# 28. Monster Telemetry Activation Audit

- `MONSTER_TARGET_ACQUIRED` — **KEEP RESERVED**; no required persistent current metric and unnecessary target identity.
- `MONSTER_TARGET_LOST` — **KEEP RESERVED**; no required persistent metric and position/no-cheat semantics need no backend event.
- `MONSTER_INVESTIGATE_STARTED` — **RESEARCH_CAPTURE, Listener only**; required for Listener response/source metrics.
- `MONSTER_ATTACK_RESOLVED` — **RESEARCH_CAPTURE, Stalker/Listener only**; exactly-once episode outcome evidence, not Life-State ownership.
- `MONSTER_SEARCH_ENDED` — **RESEARCH_CAPTURE, Stalker only**; persistent SearchReacquisitionRate evidence.

---

# 29. Stalker Mapping

Production consequence:

```text
AttackEpisode resolution
→ Life-State transition
→ PLAYER_DOWNED only when Down actually occurs
```

Life-State owns `PLAYER_DOWNED`.

Research capture may emit:

```text
MONSTER_ATTACK_RESOLVED
MONSTER_SEARCH_ENDED
```

Do not persist every Detection Meter tick, path candidate, SpatialNode, transform, or planner score. Region/Node coverage, revisit, stuck, path-failure metrics may remain local deterministic evaluation artifacts.

---

# 30. Listener Mapping

Minimum persistent research chain:

```text
NOISE_EMITTED
  │ noiseEventId
  ▼
MONSTER_INVESTIGATE_STARTED
  │ investigationEpisodeId
  ▼
MONSTER_INVESTIGATE_RESOLVED
```

Supports:

```text
NoiseResponseLatency_v1.0
SourceSelectionShare_v1.0
FalseInvestigationRate_v1.0
```

`MONSTER_INVESTIGATE_*` use `userId = null`.

Backend analysis may join `noiseEventId` to player-attributed `NOISE_EMITTED`, but telemetry never returns that identity to Listener AI.

`MONSTER_INVESTIGATE_STARTED` persists only **new InvestigationEpisode commitment reasons** from §55.1. Related-noise merge, retained-current-investigation, CHASE corroboration, and no-selection dispositions are not episode starts and therefore produce no STARTED event.

---

# 31. Listener Noise ID Correlation

v1.1 requires:

```text
NOISE_EMITTED.data.noiseEventId
```

from authoritative RuntimeNoiseEvent.

This is correlation metadata only; it does not replace `TelemetryEvent.id`, grant a target, or contain a live Transform.

---

# 32. Warden Research Event Model

Low-frequency schema-approved research events:

```text
WARDEN_TELEGRAPH_STARTED
WARDEN_ROUTE_ACTION_APPLIED
WARDEN_ROUTE_SAFETY_CHECKED
WARDEN_ROUTE_ACTION_RELEASED
```

Candidate generation/reject spam, all FacilityGraph edges, policy score tables, and every precheck remain development diagnostics.

---

# 33. Warden Safety / Pressure Evidence

Applied action stores:

```text
wardenActionId
doorId
routeFootprintIdentity
routePressure
preMeanShortestRouteCost
postMeanShortestRouteCost
```

`routeFootprintIdentity` identifies the canonical WardenRouteLockDefinition footprint within `mapContentVersion`; exact representation is an implementation binding.

`WARDEN_ROUTE_SAFETY_CHECKED` is emitted only for Warden metric-eligible checks:

```text
POST_APPLY
ACTIVE_LOCK_REVALIDATION
```

Candidate safety prechecks/rejections remain local diagnostics and do not enter `ObjectiveReachabilityRate_v1.0`.

`InvalidLockRate_v1.0` is reconstructable only from canonical applied/release lifecycle facts:

```text
numerator
=
count unique successfully applied wardenActionId
whose terminal WARDEN_ROUTE_ACTION_RELEASED has:
    releaseReason = FAIL_SAFE
    AND failSafeReason in CanonicalWardenInvalidActiveLockReason_v1.1

denominator
=
count unique successfully applied wardenActionId
```

`CanonicalWardenInvalidActiveLockReason_v1.1` contains exactly the six Warden-owned reasons defined in §55.1. A rejected candidate, candidate safety precheck, cancelled telegraph, never-applied action, normal expiry, or `ExternalUnsafeStateAfterWardenRelease` does not enter the numerator. One `wardenActionId` contributes at most once.

---

# 34. Production vs Research vs Debug

**Production:** lifecycle/objective/life/tool/help/noise.  
**Research capture:** Listener investigation, Stalker/Listener attack, Stalker search, Warden lifecycle/safety.  
**Debug only:** high-frequency AI state, candidate/path/score traces, Warden candidate rejects, profiler details.

A development trace is not automatically a backend API contract.

---

# 35. Profile Input Coverage

| Metric/profile input | Required facts | v1.1 | Status |
|---|---|---|---|
| Player Survival | `PLAYER_ESCAPED` / `PLAYER_ELIMINATED` | Yes | ACTIVE source |
| Player Noise | player-attributed `NOISE_EMITTED` + downstream ProfileNoiseFilter | Yes | ACTIVE source |
| Down Count | `PLAYER_DOWNED` | Yes | raw metric |
| Revive Count | `PLAYER_REVIVED` | Yes | raw metric |
| Match Duration | MATCH start/end | Yes | ACTIVE source |
| Team Objective Time | objective-bearing phase start/completed pairs | Yes | ACTIVE source |
| Team Survival | `MATCH_STARTED.teamSize` + `MATCH_ENDED.survivorCount` | Yes | ACTIVE source |
| splitTime | sampling source not frozen | No | DEFERRED |
| avgDistance | sampling source not frozen | No | DEFERRED |
| reviveSuccess | attempt denominator missing | No | DEFERRED |
| resourceEfficiency | outcome/waste semantic missing | No | DEFERRED |
| communication | quality semantic missing | No | DEFERRED |
| wipeRecovery | source definition incomplete | No | DEFERRED |
| Rescue Count | separate rescue outcome unfrozen | No | DEFERRED |

Telemetry does not redefine M1-014 formulas.

---

# 36. No Synthetic Profile Readiness

```text
missing telemetry != zero
deferred metric != invent event
```

Telemetry v1.1 does not make Teamwork/ResourceEfficiency/TeamPerformance complete and does not make the live Fixed-vs-Adaptive experiment ready.

---

# 37. Experiment Evidence Matrix

| Evidence | Source |
|---|---|
| assigned condition | MATCH_STARTED experimentCondition |
| protocol | MATCH_STARTED experimentProtocolVersion |
| build | MATCH_STARTED buildVersion |
| map/content | mapId + mapContentVersion |
| wire schema | every schemaVersion |
| event-time config | scenarioConfigVersion + policyVersion + configSource |
| research capture mode | MATCH_STARTED researchCaptureEnabled |
| ordering | eventSequence + nullable authorityTick |
| duplicate/conflict evidence | ack/data-quality layer |
| gap/completeness | sequence/lifecycle evaluator |
| match abort | MATCH_ENDED reason `MATCH_ABORTED` |
| final profile/experiment eligibility | M1-014/M1-020, not telemetry |

---

# 38. Position Logging

Hard rule:

```text
NO per-frame Player transform telemetry
```

All allowed positions are authoritative event-time Unity world snapshots.

| Event | Position semantic |
|---|---|
| CORE_PICKED_UP | OPTIONAL Core/interact pickup position |
| CORE_DROPPED | OPTIONAL dropped Core position |
| CORE_PLACED | OPTIONAL placement position |
| PLAYER_DOWNED | OPTIONAL affected player position at Down |
| HELP_PING_USED | OPTIONAL legal accepted ping position |
| NOISE_EMITTED | **REQUIRED** RuntimeNoiseEvent WorldPosition |
| Monster research events | FORBIDDEN in current v1.1 research contracts |
| Warden events | FORBIDDEN |

No position field is a live Transform.

---

# 39. Privacy / Data Minimization

Do not persist raw microphone audio, voice transcripts, free-text chat, passwords/tokens, camera streams, unnecessary PII, per-frame tracks, full FacilityGraph snapshots, or hidden AI memory without a real research consumer.

Research capture is configuration-controlled and never gameplay authority.

---

# 40. Batch Transport Contract

Planning baseline:

```text
POST /telemetry/batch
Host/service auth
events[]
```

Batch contains immutable TelemetryEvents. Batch packaging does not change logical identity/order.

Optional batch transport IDs may exist outside gameplay event semantics.

---

# 41. Partial Batch Acknowledgement

Per submitted event:

```text
ACCEPTED
DUPLICATE_ALREADY_ACCEPTED
PERMANENTLY_REJECTED
TRANSIENT_FAILURE
```

If no trustworthy item result arrives, client classifies it `NOT_ACKNOWLEDGED`.

Conceptual:

```text
TelemetryAckItem
- id
- status
- rejectReason?
```

Exact HTTP DTO/status mapping is an implementation binding.

---

# 42. Retry Classification

```text
ACCEPTED → remove buffer
DUPLICATE_ALREADY_ACCEPTED → remove buffer
PERMANENTLY_REJECTED → stop retry; quarantine/quality evidence
TRANSIENT_FAILURE → retry same immutable event
NOT_ACKNOWLEDGED → retry same immutable event
```

Permanent schema-invalid events are not retried forever.

---

# 43. Delivery Guarantee

```text
Unity/Host → Backend = at-least-once delivery attempt while process/session is alive
Backend logical storage = idempotent by TelemetryEvent.id
```

Do not claim exactly-once network delivery.

---

# 44. Host `TelemetryBuffer`

Bounded Host-owned queue/equivalent owns pending immutable events, batch assembly state, acknowledgement state, and retry metadata.

It does not own gameplay, monster state, Profile, or AED.

`BufferCapacity` / `BatchSize` remain evidence-driven.

---

# 45. Buffer Overflow

Simple v1.1 policy:

```text
buffer full
+ new logical event already created
→ do not silently evict old buffered event
→ do not change gameplay
→ new enqueue fails
→ already allocated eventSequence is never reused
→ local stream quality becomes INCOMPLETE
→ BufferOverflowCount/diagnostic increments
```

Later accepted sequences expose a gap. If terminal telemetry is also lost, clean completeness cannot be claimed.

No complex QoS hierarchy is introduced.

---

# 46. Retry / Backoff

Retry is bounded, non-busy-loop, and immutable.

Exact backoff/max-retry/retry-age values are TBD. Exhaustion is an observable incomplete-delivery condition, never a successful delivery.

---

# 47. Match End Flush

At authoritative match end:

```text
create final MATCH_ENDED
→ enqueue if possible
→ best-effort immediate flush
→ process acknowledgements
```

"flush attempted" != "backend acknowledged".

Abort uses `MATCH_ENDED.reasonCode = MATCH_ABORTED` when emit-able.

Application shutdown/network loss/process crash have only best-effort semantics; no impossible delivery guarantee is claimed.

---

# 48. Backend Validation

v1.1 validator checks at minimum:

- schemaVersion supported;
- event type/status emittable, including `researchCaptureEnabled = true` for RESEARCH_CAPTURE;
- required top-level/common context;
- userId semantic;
- context/data shape;
- reasonCode;
- UTC timestamp;
- eventSequence and authorityTick;
- provenance;
- event-specific enums/data against the canonical registry in §55.1 or its explicitly named versioned semantic-owner registry;
- event-specific conditional enum relations;
- `MONSTER_INVESTIGATE_STARTED.data.selectionReason` against the **new-InvestigationEpisode start subset**, not the full Listener disposition reason set;
- `WARDEN_ROUTE_ACTION_RELEASED.releaseReason/failSafeReason` as a conditional pair;
- `MONSTER_ATTACK_RESOLVED.data.outcome` and the `monsterType`-specific authoritative source binding without changing the common wire event;
- position bounds/finite values;
- payload size;
- ID conflict;
- sequence conflict;
- v1.1 PlayerDowned Warden reason restriction.

Strict wire enums serialize as stable `UPPER_SNAKE_CASE`. Runtime `PascalCase` enum/member names are implementation details and MUST NOT be serialized implicitly as the wire contract.

```text
unknown / unregistered strict event-specific enum
→ INVALID_EVENT_ENUM
→ PERMANENTLY_REJECTED

known enum token in an invalid event-specific combination
→ INVALID_EVENT_ENUM_COMBINATION
→ PERMANENTLY_REJECTED
```

The validator MUST NOT silently map an unknown enum to `OTHER`, `UNKNOWN`, a numeric default, or the first runtime enum member unless that token is explicitly part of the frozen registry.

Invalid gameplay is never reinterpreted as valid.

---

# 49. Unknown Fields / Forward Compatibility

```text
unknown schemaVersion → reject
unknown eventType → reject
RESERVED event → reject
unknown required enum/reason → reject
unknown common/event field → reject unless explicitly versioned as allowed extension
```

A new backend supports legacy v1.0 by dispatching to the v1.0 validator, not by pretending v1.0 contains v1.1 metadata.

---

# 50. v1.0 → v1.1 Compatibility

| Producer | Backend | Contract |
|---|---|---|
| v1.0 | v1.0 validator | frozen v1.0 |
| v1.0 | v1.1-capable backend | dispatch to v1.0 path |
| v1.1 | v1.0-only backend | unsupported schema; reject |
| v1.1 | v1.1 backend | v1.1 validation |
| stored v1.0 | new analytics | derived normalization may preserve `sourceSchemaVersion=1.0`; raw remains unchanged |

No silent downgrade/up-conversion of raw events.

---

# 51. Raw Event Immutability After Storage

Raw accepted events are logically append-only.

Analytics corrections belong to derived processing versions, exclusion/data-quality metadata, and recomputation — never mutation of the historical authoritative event payload.

---

# 52. Event Ownership Matrix

| Family | Gameplay fact owner | Telemetry adapter | Idempotency source |
|---|---|---|---|
| match | Match system | MatchTelemetryAdapter | match lifecycle transition |
| phase | Phase system | MatchTelemetryAdapter | phase transition identity |
| core/objective | Core/Objective | ObjectiveTelemetryAdapter | state-transition occurrence identity |
| puzzle | Puzzle | ObjectiveTelemetryAdapter | completion transition |
| Security interruption | Security Hold | ObjectiveTelemetryAdapter | interruption occurrence |
| life state | Life-State | PlayerTelemetryAdapter | life transition identity |
| escape | Exit/Life-State | PlayerTelemetryAdapter | escape transition |
| Team Tool | Tool | PlayerTelemetryAdapter | accepted tool action |
| Help Ping | Ping/Life-State | PlayerTelemetryAdapter | accepted ping action |
| noise | Runtime Noise | NoiseTelemetryAdapter | noiseEventId |
| Stalker attack/search | Stalker | StalkerTelemetryAdapter | episode ID + stage |
| Listener investigation/attack | Listener | ListenerTelemetryAdapter | episode ID + stage |
| Warden action | Warden route action | WardenTelemetryAdapter | wardenActionId + stage |
| Warden safety check | SafetyValidator trace owner | WardenTelemetryAdapter | wardenActionId + safetyCheckId |

---

# 53. One Fact — One Owner

`PLAYER_DOWNED` comes from authoritative Life-State transition, not from Stalker/Listener adapters and not from proxy animation callbacks.

`MONSTER_ATTACK_RESOLVED` is a distinct attack-episode research fact and never applies damage.

---

# 54. `reasonCode` Governance

`reasonCode` is controlled `UPPER_SNAKE_CASE`, stable/versioned, or explicitly null. It is never free text and never substitutes for eventType.

Gameplay reasonCode is separate from transport/schema rejection reasons.

---

# 55. Event Payload Contract Template

Every emittable v1.1 event in §56 specifies:

```text
status
purpose
authoritative source
telemetry owner
userId
occurrence moment
idempotency source
required/optional context/data
allowed reasonCode
position
ordering
consumer/metric use
invalid examples
valid JSON
```

Common v1.1 context from §7 applies to every event.

## 55.1 Canonical Event Payload Enum Registry v1.1 — TEL-DD-01 / TEL-DD-02 / TEL-DD-03

**SCHEMA v1.1 DECISION**

This subsection is the canonical wire-level source for strict event-specific enum domains used by emittable schema `"1.1"` events. An event contract in §56 references this registry instead of redefining an independent allowed set.

Serialization rule:

```text
runtime enum/member naming
≠ wire naming

wire strict enum token
=
stable UPPER_SNAKE_CASE value from this registry
or from an explicitly named versioned semantic-owner registry
```

JSON examples illustrate valid instances only. Example values are **not** an enum definition and cannot extend an allowed domain.

Unknown/unregistered strict enum tokens are permanent schema/semantic rejections under §48.

### 55.1.1 Canonical registry table

| Event field | Semantic owner | Canonical wire domain / registry | Conditional rule |
|---|---|---|---|
| `MATCH_ENDED.data.outcome` | authoritative Match lifecycle contract | **`MatchOutcomeRegistry` — versioned semantic-owner registry**. Telemetry does not invent additional match outcomes. The predecessor evidences `SUCCESS` as a valid example, but that JSON example is not the complete registry. The backend schema deployment MUST bind schema `"1.1"` to one approved `MatchOutcomeRegistry` version and accept only tokens registered there. Registry-version ownership belongs to the Match lifecycle/gameplay contract and its build/backend validation registry; this correction adds no new wire field. | Unregistered token → permanent reject. The registry is gameplay-owned; telemetry only validates and persists it. |
| `TEAM_TOOL_USED.data.toolType` | approved Team Tool catalog / predecessor telemetry contract | `FIELD_SCANNER`, `NOISE_MAKER`, `FIRST_AID_KIT`, `DOOR_JAMMER` | Any other token → permanent reject in schema `"1.1"` unless a later schema/catalog revision explicitly extends the domain. |
| `NOISE_EMITTED.data.noiseType` | RuntimeNoiseEvent / approved noise catalog | `SPRINT`, `INTERACTION`, `CORE_CARRY`, `CORE_DROP`, `NOISE_MAKER` | Must also satisfy the event's controlled `reasonCode` mapping. |
| `MONSTER_INVESTIGATE_STARTED.data.noiseType` | Listener legal HearingObservation / RuntimeNoiseEvent catalog | `SPRINT`, `INTERACTION`, `CORE_CARRY`, `CORE_DROP`, `NOISE_MAKER` | Same noise catalog as the originating authoritative RuntimeNoiseEvent; telemetry does not create a new noise category. |
| `MONSTER_INVESTIGATE_STARTED.data.selectionReason` | Listener `NoiseSelectionReason` + InvestigationEpisode lifecycle | `INITIAL_HIGHEST_AUDIBILITY`, `NEXT_REACHABLE_CANDIDATE`, `PENDING_OBSERVATION_SELECTED`, `INTERRUPTED_BY_STRONGER_NOISE`, `CHASE_INTERRUPTED_BY_NOISE` | **Start-only subset.** Each value must correspond to commitment of a **new** InvestigationEpisode. |
| `MONSTER_INVESTIGATE_RESOLVED.data.outcome` | Listener `InvestigationTerminationReason` | `PLAYER_CONFIRMED`, `FALSE_INVESTIGATION`, `INTERRUPTED_BY_HIGHER_PRIORITY_NOISE`, `NAVIGATION_FAILED`, `CANCELLED_BY_MATCH_END`, `CANCELLED_BY_LISTENER_DISABLE` | Exactly one terminal outcome per InvestigationEpisode. |
| `MONSTER_ATTACK_RESOLVED.data.outcome` | owning Monster AttackEpisode contract | `HIT`, `MISS` | This is attack-episode research outcome only; it is not Player Life-State. |
| `MONSTER_SEARCH_ENDED.data.outcome` | Stalker SearchEpisode contract | `SAME_TARGET_REACQUIRED`, `NEW_ELIGIBLE_TARGET_OBSERVED`, `TIMEOUT`, `CURRENT_TARGET_INVALID_NO_REPLACEMENT` | Stalker only. |
| `WARDEN_TELEGRAPH_STARTED.data.selectionReason` | Warden `WardenSelectionReason` | `HIGHEST_PRESSURE_FRESH_DOOR`, `HIGHEST_PRESSURE_AFTER_HISTORY_EXHAUSTED`, `STABLE_TIE_BREAK` | Only reasons that actually selected a candidate may start a telegraph. `NO_SAFE_CANDIDATE` and `NO_MEANINGFUL_PRESSURE` are no-action reasons and are invalid here. |
| `WARDEN_ROUTE_ACTION_APPLIED.data.safetyStatus` | Warden applied-action contract | `VALID` | An applied v1.0 Warden action cannot serialize a non-valid pre-apply safety state. |
| `WARDEN_ROUTE_SAFETY_CHECKED.data.checkType` | Warden safety metric contract | `POST_APPLY`, `ACTIVE_LOCK_REVALIDATION` | Candidate prechecks are not persistable through this event. |
| `WARDEN_ROUTE_SAFETY_CHECKED.data.safetyStatus` | Warden `WardenSafetyValidationResult` | `VALID`, `REJECTED` | `NOT_EVALUATED` is not a completed metric-eligible safety proof and therefore is not emitted as this research event. |
| `WARDEN_ROUTE_SAFETY_CHECKED.data.safetyReason` | Warden `WardenSafetyRejectReason` | `GRAPH_REVISION_CHANGED`, `OBJECTIVE_UNKNOWN`, `REQUIRED_ORIGIN_MISSING`, `REQUIRED_DESTINATION_MISSING`, `OBJECTIVE_UNREACHABLE`, `EXIT_UNREACHABLE`, `NO_LEGAL_ROUTE`, `DOOR_STATE_CONFLICT`, `DOORWAY_OCCUPIED` | REQUIRED when `safetyStatus = REJECTED`; FORBIDDEN when `safetyStatus = VALID`. `NONE` is represented by absence, not a serialized reason token. |
| `WARDEN_ROUTE_ACTION_RELEASED.data.releaseReason` | Warden applied-action lifecycle | `EXPIRED`, `FAIL_SAFE` | `EXPIRED` is normal exactly-once release. `FAIL_SAFE` means Warden-owned active state required fail-safe removal. |
| `WARDEN_ROUTE_ACTION_RELEASED.data.failSafeReason` | Warden `WardenFailSafeReason` filtered to invalid-active-lock outcomes | `POST_APPLY_OBJECTIVE_UNREACHABLE`, `POST_APPLY_EXIT_UNREACHABLE`, `POST_APPLY_NO_LEGAL_ROUTE`, `ACTIVE_LOCK_INVALID_AFTER_OBJECTIVE_CHANGE`, `ACTIVE_LOCK_INVALID_AFTER_SCENARIO_CHANGE`, `GRAPH_INVALID_WHILE_APPLIED` | REQUIRED iff `releaseReason = FAIL_SAFE`; FORBIDDEN when `releaseReason = EXPIRED`. |

### 55.1.2 Listener `MONSTER_INVESTIGATE_STARTED.selectionReason` start-only boundary

Listener's internal decision/disposition taxonomy is intentionally larger than this wire field. `MONSTER_INVESTIGATE_STARTED` means a **new InvestigationEpisode was committed**, so the following internal outcomes are **not** legal values for this event:

```text
RELATED_NOISE_MERGED
CURRENT_INVESTIGATION_RETAINED
CORROBORATES_VISIBLE_TARGET
NO_ELIGIBLE_NOISE
```

Semantics:

```text
RELATED_NOISE_MERGED
→ existing InvestigationEpisode updated
→ no new STARTED event

CURRENT_INVESTIGATION_RETAINED
→ current commitment retained
→ no new STARTED event

CORROBORATES_VISIBLE_TARGET
→ heard evidence is a CHASE disposition
→ CHASE retained
→ no new InvestigationEpisode

NO_ELIGIBLE_NOISE
→ no investigation commitment
→ no new STARTED event
```

`PENDING_OBSERVATION_SELECTED` **is legal** when an already-heard, still-unexpired `PendingHearingInbox` observation later commits a new InvestigationEpisode at a legal decision point.

`INTERRUPTED_BY_STRONGER_NOISE` is legal for the **new** episode created after the old episode is terminalized as `INTERRUPTED_BY_HIGHER_PRIORITY_NOISE`.

`CHASE_INTERRUPTED_BY_NOISE` is legal when a qualifying spatially separated hearing observation legally diverts CHASE and creates a new InvestigationEpisode.

### 55.1.3 Warden fail-safe wire boundary

The Warden detailed design has a broader local fail-safe diagnostic taxonomy. For telemetry release evidence, only an applied Warden action whose **own active overlay** required fail-safe removal may serialize `data.failSafeReason`.

`EXTERNAL_UNSAFE_STATE_AFTER_WARDEN_RELEASE` is therefore:

```text
Warden local/debug fail-safe diagnostic
after Warden-owned overlay removal
→ NOT a valid WARDEN_ROUTE_ACTION_RELEASED.data.failSafeReason
→ NOT evidence that the removed action belongs in InvalidLockRate_v1.0 numerator
```

This preserves the difference between:

```text
unsafe while Warden-owned active lock exists
```

and:

```text
external/combined graph remains unsafe after Warden overlay is already removed
```

### 55.1.4 Generic Monster attack outcome boundary

`MONSTER_ATTACK_RESOLVED.data.outcome` has one generic wire domain:

```text
HIT
MISS
```

The event does not define when a specific monster's attack resolves. The authoritative resolution point is owned by that monster's baselined detailed design (§56.19).

---

# 56. Emittable v1.1 Event Payload Contracts

## 56.1 `MATCH_STARTED`

| Field | Contract |
|---|---|
| Status | ACTIVE_PRODUCTION |
| Purpose | Canonical match-start and provenance anchor. |
| Authoritative Source | Authoritative Match system after valid match/scenario initialization. |
| Telemetry Owner | MatchTelemetryAdapter |
| `userId` semantic | `null` — system/team event. |
| Occurrence moment | Authoritative match enters started/running state; first TelemetryEvent of match. |
| Idempotency source | `(matchId, MATCH_STARTED)` single-shot transition. |
| Required context | `eventSequence = 1`<br>`authorityTick (nullable)`<br>`scenarioConfigVersion`<br>`policyVersion`<br>`configSource`<br>`teamSize`<br>`buildVersion`<br>`mapContentVersion`<br>`contentWhitelistVersion`<br>`researchCaptureEnabled` |
| Optional context | `experimentCondition + experimentProtocolVersion when experiment active`<br>`testRunId`<br>`experimentRunId`<br>`parameterRegistryVersion`<br>`fixedBaselineId` |
| Required data | `mapId` |
| Optional data | None |
| Allowed `reasonCode` | `MATCH_READY` |
| Position semantic | FORBIDDEN. |
| Ordering | Must be sequence 1; no prior match event. |
| Downstream consumers | Match provenance, completeness, Team Survival denominator, experiment evidence. |
| Metric/research usage | Match start/provenance anchor. |
| Invalid examples | userId non-null<br>sequence != 1<br>experimentCondition without experimentProtocolVersion |

Valid v1.1 example:

```json
{
  "id": "EXAMPLE_EVT_0001",
  "matchId": "EXAMPLE_MATCH_001",
  "userId": null,
  "eventType": "MATCH_STARTED",
  "ts": "2026-08-26T02:00:00.000Z",
  "valueJson": {
    "context": {
      "eventSequence": 1,
      "authorityTick": null,
      "scenarioConfigVersion": "EXAMPLE_SCENARIO_CONFIG_VERSION",
      "policyVersion": "M1-015-v0",
      "configSource": "FIXED",
      "teamSize": 4,
      "buildVersion": "EXAMPLE_BUILD",
      "mapContentVersion": "EXAMPLE_RF_CONTENT",
      "contentWhitelistVersion": "EXAMPLE_WHITELIST",
      "researchCaptureEnabled": true
    },
    "data": {
      "mapId": "RESEARCH_FACILITY"
    }
  },
  "reasonCode": "MATCH_READY",
  "schemaVersion": "1.1"
}
```

## 56.2 `MATCH_ENDED`

| Field | Contract |
|---|---|
| Status | ACTIVE_PRODUCTION |
| Purpose | Canonical match terminal outcome and final sequence boundary. |
| Authoritative Source | Authoritative Match system. |
| Telemetry Owner | MatchTelemetryAdapter |
| `userId` semantic | `null`. |
| Occurrence moment | Authoritative match terminal transition after earlier match telemetry facts are created. |
| Idempotency source | `(matchId, MATCH_ENDED)`. |
| Required context | `common v1.1 context`<br>`phase = MATCH_END` |
| Optional context | None |
| Required data | `outcome` — registry-bound per §55.1 (`MatchOutcomeRegistry`)<br>`durationSeconds`<br>`survivorCount` |
| Optional data | None |
| Allowed `reasonCode` | `TEAM_ESCAPED` | `TEAM_ELIMINATED` | `MATCH_ABORTED` |
| Position semantic | FORBIDDEN. |
| Ordering | Its sequence is expected upper bound for clean stream. |
| Downstream consumers | Match result/completeness, survival/profile, experiment evidence. |
| Metric/research usage | Match duration/check; survivor count. |
| Invalid examples | created twice<br>later match TelemetryEvent created after terminal occurrence<br>negative survivorCount<br>unregistered `outcome` token |

Valid v1.1 example:

```json
{
  "id": "EXAMPLE_EVT_1200",
  "matchId": "EXAMPLE_MATCH_001",
  "userId": null,
  "eventType": "MATCH_ENDED",
  "ts": "2026-08-26T02:17:12.400Z",
  "valueJson": {
    "context": {
      "eventSequence": 1200,
      "authorityTick": 840,
      "scenarioConfigVersion": "EXAMPLE_SCENARIO_CONFIG_VERSION",
      "policyVersion": "M1-015-v0",
      "configSource": "FIXED",
      "phase": "MATCH_END"
    },
    "data": {
      "outcome": "SUCCESS",
      "durationSeconds": 1032.4,
      "survivorCount": 3
    }
  },
  "reasonCode": "TEAM_ESCAPED",
  "schemaVersion": "1.1"
}
```

## 56.3 `PHASE_STARTED`

| Field | Contract |
|---|---|
| Status | ACTIVE_PRODUCTION |
| Purpose | Canonical gameplay phase start. |
| Authoritative Source | Authoritative Phase system. |
| Telemetry Owner | MatchTelemetryAdapter |
| `userId` semantic | `null`. |
| Occurrence moment | Named phase transition commits. |
| Idempotency source | `(matchId, phaseIdentity, STARTED)`. |
| Required context | `common v1.1 context`<br>`phase` |
| Optional context | None |
| Required data | None |
| Optional data | None |
| Allowed `reasonCode` | `PREVIOUS_PHASE_COMPLETED` | `null` |
| Position semantic | FORBIDDEN. |
| Ordering | Sequence at authoritative phase transition. |
| Downstream consumers | Phase duration/objectiveTime. |
| Metric/research usage | Duration source-of-truth uses matching start/completed `ts`. |
| Invalid examples | missing phase<br>duplicate specialized phase lifecycle for same transition |

Valid v1.1 example:

```json
{
  "id": "EXAMPLE_EVT_0101",
  "matchId": "EXAMPLE_MATCH_001",
  "userId": null,
  "eventType": "PHASE_STARTED",
  "ts": "2026-08-26T02:12:30.125Z",
  "valueJson": {
    "context": {
      "eventSequence": 101,
      "authorityTick": 840,
      "scenarioConfigVersion": "EXAMPLE_SCENARIO_CONFIG_VERSION",
      "policyVersion": "M1-015-v0",
      "configSource": "FIXED",
      "phase": "SECURITY_HOLD"
    },
    "data": {}
  },
  "reasonCode": "PREVIOUS_PHASE_COMPLETED",
  "schemaVersion": "1.1"
}
```

## 56.4 `PHASE_COMPLETED`

| Field | Contract |
|---|---|
| Status | ACTIVE_PRODUCTION |
| Purpose | Canonical gameplay phase completion. |
| Authoritative Source | Authoritative Phase system. |
| Telemetry Owner | MatchTelemetryAdapter |
| `userId` semantic | `null`. |
| Occurrence moment | Named phase transition commits. |
| Idempotency source | `(matchId, phaseIdentity, COMPLETED)`. |
| Required context | `common v1.1 context`<br>`phase` |
| Optional context | None |
| Required data | None |
| Optional data | `durationSeconds convenience/debug only` |
| Allowed `reasonCode` | `OBJECTIVE_COMPLETED` | `null` |
| Position semantic | FORBIDDEN. |
| Ordering | Sequence at authoritative phase transition. |
| Downstream consumers | Phase duration/objectiveTime. |
| Metric/research usage | Duration source-of-truth uses matching start/completed `ts`. |
| Invalid examples | missing phase<br>duplicate specialized phase lifecycle for same transition |

Valid v1.1 example:

```json
{
  "id": "EXAMPLE_EVT_0145",
  "matchId": "EXAMPLE_MATCH_001",
  "userId": null,
  "eventType": "PHASE_COMPLETED",
  "ts": "2026-08-26T02:12:30.125Z",
  "valueJson": {
    "context": {
      "eventSequence": 145,
      "authorityTick": 840,
      "scenarioConfigVersion": "EXAMPLE_SCENARIO_CONFIG_VERSION",
      "policyVersion": "M1-015-v0",
      "configSource": "FIXED",
      "phase": "SECURITY_HOLD"
    },
    "data": {
      "durationSeconds": 24.8
    }
  },
  "reasonCode": "OBJECTIVE_COMPLETED",
  "schemaVersion": "1.1"
}
```

## 56.5 `SECURITY_HOLD_INTERRUPTED`

| Field | Contract |
|---|---|
| Status | ACTIVE_PRODUCTION |
| Purpose | Security Hold interaction/progress interruption distinct from lifecycle. |
| Authoritative Source | Authoritative Security Terminal/Hold system. |
| Telemetry Owner | ObjectiveTelemetryAdapter |
| `userId` semantic | `null`. |
| Occurrence moment | Accepted Hold interaction transitions to interrupted/paused. |
| Idempotency source | Stable interruption occurrence identity/ordinal. |
| Required context | `common v1.1 context`<br>`phase = SECURITY_HOLD` |
| Optional context | None |
| Required data | None |
| Optional data | None |
| Allowed `reasonCode` | `null` |
| Position semantic | FORBIDDEN. |
| Ordering | Sequence at interruption transition. |
| Downstream consumers | Interruption evidence; not lifecycle replacement. |
| Metric/research usage | M1-014 objectiveTime still includes interruption elapsed time. |
| Invalid examples | emitted on phase start/end<br>invented progress/damage payload |

Valid v1.1 example:

```json
{
  "id": "EXAMPLE_EVT_0133",
  "matchId": "EXAMPLE_MATCH_001",
  "userId": null,
  "eventType": "SECURITY_HOLD_INTERRUPTED",
  "ts": "2026-08-26T02:12:30.125Z",
  "valueJson": {
    "context": {
      "eventSequence": 133,
      "authorityTick": 840,
      "scenarioConfigVersion": "EXAMPLE_SCENARIO_CONFIG_VERSION",
      "policyVersion": "M1-015-v0",
      "configSource": "FIXED",
      "phase": "SECURITY_HOLD"
    },
    "data": {}
  },
  "reasonCode": null,
  "schemaVersion": "1.1"
}
```

## 56.6 `CORE_PICKED_UP`

| Field | Contract |
|---|---|
| Status | ACTIVE_PRODUCTION |
| Purpose | Committed Energy Core pickup. |
| Authoritative Source | Authoritative Core/Objective system. |
| Telemetry Owner | ObjectiveTelemetryAdapter |
| `userId` semantic | REQUIRED — acting player. |
| Occurrence moment | Immediately after named authoritative Core transition commits. |
| Idempotency source | Stable Core state-transition occurrence identity; `coreId` alone is not sufficient for repeat cycles. |
| Required context | `common v1.1 context`<br>`phase = CORE_COLLECTION` |
| Optional context | `position` |
| Required data | `coreId` |
| Optional data | None |
| Allowed `reasonCode` | `PLAYER_PICKUP` |
| Position semantic | OPTIONAL event-time Core/interact/drop/placement world position. |
| Ordering | Sequence at Core state transition; derived RuntimeNoiseEvent is a separate fact. |
| Downstream consumers | Objective/resource raw telemetry. |
| Metric/research usage | Does not synthesize deferred ResourceEfficiency. |
| Invalid examples | missing coreId<br>coreId alone used as duplicate key<br>live Transform reference |

Valid v1.1 example:

```json
{
  "id": "EXAMPLE_EVT_0020",
  "matchId": "EXAMPLE_MATCH_001",
  "userId": "player_02",
  "eventType": "CORE_PICKED_UP",
  "ts": "2026-08-26T02:12:30.125Z",
  "valueJson": {
    "context": {
      "eventSequence": 20,
      "authorityTick": 840,
      "scenarioConfigVersion": "EXAMPLE_SCENARIO_CONFIG_VERSION",
      "policyVersion": "M1-015-v0",
      "configSource": "FIXED",
      "phase": "CORE_COLLECTION",
      "position": {
        "x": 12.5,
        "y": 0.0,
        "z": 8.3
      }
    },
    "data": {
      "coreId": "core_01"
    }
  },
  "reasonCode": "PLAYER_PICKUP",
  "schemaVersion": "1.1"
}
```

## 56.7 `CORE_DROPPED`

| Field | Contract |
|---|---|
| Status | ACTIVE_PRODUCTION |
| Purpose | Committed Energy Core drop. |
| Authoritative Source | Authoritative Core/Objective system. |
| Telemetry Owner | ObjectiveTelemetryAdapter |
| `userId` semantic | REQUIRED — acting player. |
| Occurrence moment | Immediately after named authoritative Core transition commits. |
| Idempotency source | Stable Core state-transition occurrence identity; `coreId` alone is not sufficient for repeat cycles. |
| Required context | `common v1.1 context`<br>`phase = CORE_COLLECTION` |
| Optional context | `position` |
| Required data | `coreId` |
| Optional data | None |
| Allowed `reasonCode` | `PLAYER_DROP` |
| Position semantic | OPTIONAL event-time Core/interact/drop/placement world position. |
| Ordering | Sequence at Core state transition; derived RuntimeNoiseEvent is a separate fact. |
| Downstream consumers | Objective/resource raw telemetry. |
| Metric/research usage | Does not synthesize deferred ResourceEfficiency. |
| Invalid examples | missing coreId<br>coreId alone used as duplicate key<br>live Transform reference |

Valid v1.1 example:

```json
{
  "id": "EXAMPLE_EVT_0026",
  "matchId": "EXAMPLE_MATCH_001",
  "userId": "player_02",
  "eventType": "CORE_DROPPED",
  "ts": "2026-08-26T02:12:30.125Z",
  "valueJson": {
    "context": {
      "eventSequence": 26,
      "authorityTick": 840,
      "scenarioConfigVersion": "EXAMPLE_SCENARIO_CONFIG_VERSION",
      "policyVersion": "M1-015-v0",
      "configSource": "FIXED",
      "phase": "CORE_COLLECTION",
      "position": {
        "x": 12.5,
        "y": 0.0,
        "z": 8.3
      }
    },
    "data": {
      "coreId": "core_01"
    }
  },
  "reasonCode": "PLAYER_DROP",
  "schemaVersion": "1.1"
}
```

## 56.8 `CORE_PLACED`

| Field | Contract |
|---|---|
| Status | ACTIVE_PRODUCTION |
| Purpose | Committed Core objective placement. |
| Authoritative Source | Authoritative Core/Objective system. |
| Telemetry Owner | ObjectiveTelemetryAdapter |
| `userId` semantic | REQUIRED — acting player. |
| Occurrence moment | Immediately after named authoritative Core transition commits. |
| Idempotency source | Stable Core state-transition occurrence identity; `coreId` alone is not sufficient for repeat cycles. |
| Required context | `common v1.1 context`<br>`phase = CORE_COLLECTION` |
| Optional context | `position` |
| Required data | `coreId` |
| Optional data | None |
| Allowed `reasonCode` | `CORE_OBJECTIVE_PLACED` |
| Position semantic | OPTIONAL event-time Core/interact/drop/placement world position. |
| Ordering | Sequence at Core state transition; derived RuntimeNoiseEvent is a separate fact. |
| Downstream consumers | Objective/resource raw telemetry. |
| Metric/research usage | Does not synthesize deferred ResourceEfficiency. |
| Invalid examples | missing coreId<br>coreId alone used as duplicate key<br>live Transform reference |

Valid v1.1 example:

```json
{
  "id": "EXAMPLE_EVT_0052",
  "matchId": "EXAMPLE_MATCH_001",
  "userId": "player_02",
  "eventType": "CORE_PLACED",
  "ts": "2026-08-26T02:12:30.125Z",
  "valueJson": {
    "context": {
      "eventSequence": 52,
      "authorityTick": 840,
      "scenarioConfigVersion": "EXAMPLE_SCENARIO_CONFIG_VERSION",
      "policyVersion": "M1-015-v0",
      "configSource": "FIXED",
      "phase": "CORE_COLLECTION"
    },
    "data": {
      "coreId": "core_01"
    }
  },
  "reasonCode": "CORE_OBJECTIVE_PLACED",
  "schemaVersion": "1.1"
}
```

## 56.9 `PUZZLE_COMPLETED`

| Field | Contract |
|---|---|
| Status | ACTIVE_PRODUCTION |
| Purpose | Power Puzzle objective completion; distinct from phase completion. |
| Authoritative Source | Authoritative Puzzle/Objective system. |
| Telemetry Owner | ObjectiveTelemetryAdapter |
| `userId` semantic | `null`. |
| Occurrence moment | Puzzle completion state commits. |
| Idempotency source | Single puzzle completion transition identity. |
| Required context | `common v1.1 context`<br>`phase = POWER_PUZZLE` |
| Optional context | None |
| Required data | None |
| Optional data | None |
| Allowed `reasonCode` | `null` |
| Position semantic | FORBIDDEN. |
| Ordering | Sequence at completion; PHASE_COMPLETED remains separate when phase ends. |
| Downstream consumers | Objective telemetry. |
| Metric/research usage | Does not replace phase lifecycle. |
| Invalid examples | PUZZLE_FAILED emitted for ordinary wrong input<br>phase lifecycle omitted when it also completes |

Valid v1.1 example:

```json
{
  "id": "EXAMPLE_EVT_0080",
  "matchId": "EXAMPLE_MATCH_001",
  "userId": null,
  "eventType": "PUZZLE_COMPLETED",
  "ts": "2026-08-26T02:12:30.125Z",
  "valueJson": {
    "context": {
      "eventSequence": 80,
      "authorityTick": 840,
      "scenarioConfigVersion": "EXAMPLE_SCENARIO_CONFIG_VERSION",
      "policyVersion": "M1-015-v0",
      "configSource": "FIXED",
      "phase": "POWER_PUZZLE"
    },
    "data": {}
  },
  "reasonCode": null,
  "schemaVersion": "1.1"
}
```

## 56.10 `PLAYER_DOWNED`

| Field | Contract |
|---|---|
| Status | ACTIVE_PRODUCTION |
| Purpose | Affected player enters Downed. |
| Authoritative Source | Authoritative Player Life-State system. |
| Telemetry Owner | PlayerTelemetryAdapter |
| `userId` semantic | REQUIRED — affected/downed player. |
| Occurrence moment | Life-State transition to Down commits. |
| Idempotency source | Player life-state transition identity/ordinal. |
| Required context | `common v1.1 context`<br>`phase`<br>`monsterType when attack reason` |
| Optional context | `position` |
| Required data | None |
| Optional data | `downCount snapshot only` |
| Allowed `reasonCode` | `STALKER_ATTACK` | `LISTENER_ATTACK` |
| Position semantic | OPTIONAL affected-player position at Down transition. |
| Ordering | Sequence at Life-State transition; never attack animation callback. |
| Downstream consumers | Down Count/survival/co-op. |
| Metric/research usage | Down Count = count valid events; never sum downCount snapshot. |
| Invalid examples | WARDEN_ATTACK in v1.1<br>Monster adapter duplicates Life-State event<br>proxy emission |

Valid v1.1 example:

```json
{
  "id": "EXAMPLE_EVT_0220",
  "matchId": "EXAMPLE_MATCH_001",
  "userId": "player_03",
  "eventType": "PLAYER_DOWNED",
  "ts": "2026-08-26T02:12:30.125Z",
  "valueJson": {
    "context": {
      "eventSequence": 220,
      "authorityTick": 840,
      "scenarioConfigVersion": "EXAMPLE_SCENARIO_CONFIG_VERSION",
      "policyVersion": "M1-015-v0",
      "configSource": "FIXED",
      "phase": "FINAL_HUNT",
      "monsterType": "STALKER",
      "position": {
        "x": 21.2,
        "y": 0.0,
        "z": 5.4
      }
    },
    "data": {
      "downCount": 2
    }
  },
  "reasonCode": "STALKER_ATTACK",
  "schemaVersion": "1.1"
}
```

## 56.11 `PLAYER_REVIVED`

| Field | Contract |
|---|---|
| Status | ACTIVE_PRODUCTION |
| Purpose | Successful revive of a Downed player. |
| Authoritative Source | Authoritative Player Life-State system. |
| Telemetry Owner | PlayerTelemetryAdapter |
| `userId` semantic | REQUIRED — revived player. |
| Occurrence moment | Revive completes and Life-State transitions. |
| Idempotency source | Revive transition identity/ordinal. |
| Required context | `common v1.1 context`<br>`phase` |
| Optional context | None |
| Required data | `reviverPlayerId` |
| Optional data | `reviveCount snapshot`<br>`usedFirstAidKit snapshot` |
| Allowed `reasonCode` | `TEAMMATE_REVIVE` |
| Position semantic | FORBIDDEN. |
| Ordering | Sequence at successful transition, not request start. |
| Downstream consumers | Revive Count/co-op fact. |
| Metric/research usage | Revive Count = valid event count. |
| Invalid examples | userId is reviver<br>emitted before revive completion |

Valid v1.1 example:

```json
{
  "id": "EXAMPLE_EVT_0240",
  "matchId": "EXAMPLE_MATCH_001",
  "userId": "player_03",
  "eventType": "PLAYER_REVIVED",
  "ts": "2026-08-26T02:12:30.125Z",
  "valueJson": {
    "context": {
      "eventSequence": 240,
      "authorityTick": 840,
      "scenarioConfigVersion": "EXAMPLE_SCENARIO_CONFIG_VERSION",
      "policyVersion": "M1-015-v0",
      "configSource": "FIXED",
      "phase": "FINAL_HUNT"
    },
    "data": {
      "reviverPlayerId": "player_01",
      "reviveCount": 2,
      "usedFirstAidKit": false
    }
  },
  "reasonCode": "TEAMMATE_REVIVE",
  "schemaVersion": "1.1"
}
```

## 56.12 `PLAYER_ELIMINATED`

| Field | Contract |
|---|---|
| Status | ACTIVE_PRODUCTION |
| Purpose | Affected player reaches Eliminated terminal state. |
| Authoritative Source | Authoritative Life-State system. |
| Telemetry Owner | PlayerTelemetryAdapter |
| `userId` semantic | REQUIRED — eliminated player. |
| Occurrence moment | Eliminated transition commits. |
| Idempotency source | Player terminal transition identity. |
| Required context | `common v1.1 context`<br>`phase` |
| Optional context | None |
| Required data | None |
| Optional data | `reviveCount snapshot` |
| Allowed `reasonCode` | `REVIVE_LIMIT_REACHED` |
| Position semantic | FORBIDDEN in v1.1. |
| Ordering | Sequence at terminal transition. |
| Downstream consumers | Player Survival/Eliminated Count. |
| Metric/research usage | Terminal survival fact. |
| Invalid examples | duplicate terminal event<br>unknown reason |

Valid v1.1 example:

```json
{
  "id": "EXAMPLE_EVT_0300",
  "matchId": "EXAMPLE_MATCH_001",
  "userId": "player_03",
  "eventType": "PLAYER_ELIMINATED",
  "ts": "2026-08-26T02:12:30.125Z",
  "valueJson": {
    "context": {
      "eventSequence": 300,
      "authorityTick": 840,
      "scenarioConfigVersion": "EXAMPLE_SCENARIO_CONFIG_VERSION",
      "policyVersion": "M1-015-v0",
      "configSource": "FIXED",
      "phase": "FINAL_HUNT"
    },
    "data": {
      "reviveCount": 2
    }
  },
  "reasonCode": "REVIVE_LIMIT_REACHED",
  "schemaVersion": "1.1"
}
```

## 56.13 `PLAYER_ESCAPED`

| Field | Contract |
|---|---|
| Status | ACTIVE_PRODUCTION |
| Purpose | Player reaches authoritative escape condition. |
| Authoritative Source | Authoritative Exit/Life-State system. |
| Telemetry Owner | PlayerTelemetryAdapter |
| `userId` semantic | REQUIRED — escaped player. |
| Occurrence moment | Escape transition commits. |
| Idempotency source | Player escape transition identity. |
| Required context | `common v1.1 context`<br>`phase = FINAL_HUNT` |
| Optional context | None |
| Required data | None |
| Optional data | `rescuedTeammate legacy conditional snapshot` |
| Allowed `reasonCode` | `EXIT_REACHED` |
| Position semantic | FORBIDDEN. |
| Ordering | Sequence at escape transition. |
| Downstream consumers | Player Survival/Escape. |
| Metric/research usage | Not independent Rescue Count. |
| Invalid examples | synthesizing PLAYER_RESCUED<br>duplicate escape |

Valid v1.1 example:

```json
{
  "id": "EXAMPLE_EVT_0350",
  "matchId": "EXAMPLE_MATCH_001",
  "userId": "player_01",
  "eventType": "PLAYER_ESCAPED",
  "ts": "2026-08-26T02:12:30.125Z",
  "valueJson": {
    "context": {
      "eventSequence": 350,
      "authorityTick": 840,
      "scenarioConfigVersion": "EXAMPLE_SCENARIO_CONFIG_VERSION",
      "policyVersion": "M1-015-v0",
      "configSource": "FIXED",
      "phase": "FINAL_HUNT"
    },
    "data": {
      "rescuedTeammate": true
    }
  },
  "reasonCode": "EXIT_REACHED",
  "schemaVersion": "1.1"
}
```

## 56.14 `TEAM_TOOL_USED`

| Field | Contract |
|---|---|
| Status | ACTIVE_PRODUCTION |
| Purpose | Authoritative Team Tool use succeeds. |
| Authoritative Source | Authoritative Team Tool system. |
| Telemetry Owner | PlayerTelemetryAdapter |
| `userId` semantic | REQUIRED — acting player. |
| Occurrence moment | Tool use accepted/committed, not request time. |
| Idempotency source | Authoritative tool-use action identity. |
| Required context | `common v1.1 context`<br>`phase` |
| Optional context | None |
| Required data | `toolType` — strict enum per §55.1 |
| Optional data | `targetId` |
| Allowed `reasonCode` | `PLAYER_ACTIVATED_TOOL` |
| Position semantic | FORBIDDEN unless later tool-specific revision. |
| Ordering | Sequence at accepted use. |
| Downstream consumers | Tool usage evidence. |
| Metric/research usage | Does not define ResourceEfficiency/Tool Assist quality. |
| Invalid examples | client request before Host validation<br>unknown toolType |

Valid v1.1 example:

```json
{
  "id": "EXAMPLE_EVT_0060",
  "matchId": "EXAMPLE_MATCH_001",
  "userId": "player_01",
  "eventType": "TEAM_TOOL_USED",
  "ts": "2026-08-26T02:12:30.125Z",
  "valueJson": {
    "context": {
      "eventSequence": 60,
      "authorityTick": 840,
      "scenarioConfigVersion": "EXAMPLE_SCENARIO_CONFIG_VERSION",
      "policyVersion": "M1-015-v0",
      "configSource": "FIXED",
      "phase": "CORE_COLLECTION"
    },
    "data": {
      "toolType": "NOISE_MAKER",
      "targetId": null
    }
  },
  "reasonCode": "PLAYER_ACTIVATED_TOOL",
  "schemaVersion": "1.1"
}
```

## 56.15 `HELP_PING_USED`

| Field | Contract |
|---|---|
| Status | ACTIVE_PRODUCTION |
| Purpose | Downed player successfully uses Need Help ping. |
| Authoritative Source | Authoritative Ping/Life-State validation. |
| Telemetry Owner | PlayerTelemetryAdapter |
| `userId` semantic | REQUIRED — acting Downed player. |
| Occurrence moment | Ping accepted under role/cooldown rules. |
| Idempotency source | Accepted ping action identity. |
| Required context | `common v1.1 context`<br>`phase` |
| Optional context | `position` |
| Required data | None |
| Optional data | None |
| Allowed `reasonCode` | `PLAYER_REQUESTED_HELP` |
| Position semantic | OPTIONAL legal accepted ping position; must not expose Spectator-hidden information. |
| Ordering | Sequence at accepted ping. |
| Downstream consumers | Communication/help-use raw evidence. |
| Metric/research usage | Does not define communication quality. |
| Invalid examples | Spectator hidden-info ping<br>client-only unvalidated ping |

Valid v1.1 example:

```json
{
  "id": "EXAMPLE_EVT_0270",
  "matchId": "EXAMPLE_MATCH_001",
  "userId": "player_03",
  "eventType": "HELP_PING_USED",
  "ts": "2026-08-26T02:12:30.125Z",
  "valueJson": {
    "context": {
      "eventSequence": 270,
      "authorityTick": 840,
      "scenarioConfigVersion": "EXAMPLE_SCENARIO_CONFIG_VERSION",
      "policyVersion": "M1-015-v0",
      "configSource": "FIXED",
      "phase": "FINAL_HUNT",
      "position": {
        "x": 22.1,
        "y": 0.0,
        "z": 5.8
      }
    },
    "data": {}
  },
  "reasonCode": "PLAYER_REQUESTED_HELP",
  "schemaVersion": "1.1"
}
```

## 56.16 `NOISE_EMITTED`

| Field | Contract |
|---|---|
| Status | ACTIVE_PRODUCTION |
| Purpose | Analytical record of one authoritative RuntimeNoiseEvent. |
| Authoritative Source | Authoritative Runtime Noise emission/NoiseSystem acceptance. |
| Telemetry Owner | NoiseTelemetryAdapter |
| `userId` semantic | REQUIRED for current v0 NoiseTypes — acting player. |
| Occurrence moment | Same occurrence as RuntimeNoiseEvent.EmittedAt. |
| Idempotency source | `(matchId, noiseEventId)`. |
| Required context | `common v1.1 context`<br>`phase`<br>`position` |
| Optional context | None |
| Required data | `noiseEventId`<br>`noiseType` — strict enum per §55.1<br>`loudness` |
| Optional data | `hearingRadius` |
| Allowed `reasonCode` | `PLAYER_SPRINT` | `OBJECT_INTERACTION` | `CORE_CARRY_MOVEMENT` | `CORE_DROP` | `NOISE_MAKER_USED` |
| Position semantic | REQUIRED RuntimeNoiseEvent WorldPosition snapshot. |
| Ordering | Use RuntimeNoiseEvent authoritative tick/time where available. |
| Downstream consumers | Noise profile/raw metrics; Listener correlation. |
| Metric/research usage | Noise count/type/phase and M1-014 Player Noise. |
| Invalid examples | missing noiseEventId<br>AudioSource as source<br>position tracks player later<br>noiseType/reason mismatch |

Valid v1.1 example:

```json
{
  "id": "EXAMPLE_EVT_0061",
  "matchId": "EXAMPLE_MATCH_001",
  "userId": "player_02",
  "eventType": "NOISE_EMITTED",
  "ts": "2026-08-26T02:12:30.125Z",
  "valueJson": {
    "context": {
      "eventSequence": 61,
      "authorityTick": 840,
      "scenarioConfigVersion": "EXAMPLE_SCENARIO_CONFIG_VERSION",
      "policyVersion": "M1-015-v0",
      "configSource": "FIXED",
      "phase": "CORE_COLLECTION",
      "position": {
        "x": 12.5,
        "y": 0.0,
        "z": 8.3
      }
    },
    "data": {
      "noiseEventId": "noise_0042",
      "noiseType": "SPRINT",
      "loudness": 0.7,
      "hearingRadius": 12.0
    }
  },
  "reasonCode": "PLAYER_SPRINT",
  "schemaVersion": "1.1"
}
```

## 56.17 `MONSTER_INVESTIGATE_STARTED`

| Field | Contract |
|---|---|
| Status | RESEARCH_CAPTURE |
| Purpose | Listener InvestigationEpisode commitment evidence. |
| Authoritative Source | Listener FSM/Memory after a legal HearingObservation becomes a new committed episode. |
| Telemetry Owner | ListenerTelemetryAdapter |
| `userId` semantic | `null`. |
| Occurrence moment | `InvestigationCommittedAt`. |
| Idempotency source | `(matchId, monsterId, investigationEpisodeId, STARTED)`. |
| Required context | `common v1.1 context`<br>`phase`<br>`monsterType = LISTENER`<br>`monsterId`<br>`researchCaptureEnabled = true` |
| Optional context | None |
| Required data | `investigationEpisodeId`<br>`noiseEventId`<br>`noiseType` — strict enum per §55.1<br>`heardAt`<br>`selectionReason` — strict **new-episode-start subset** per §55.1 |
| Optional data | None |
| Allowed `reasonCode` | `null` |
| Position semantic | FORBIDDEN. |
| Ordering | Sequence at commitment; heardAt is immutable earlier/equal hearing timestamp. |
| Downstream consumers | Listener NoiseResponseLatency/SourceSelection. |
| Metric/research usage | Latency = event.ts - heardAt; SourceSelection = noiseType + selectionReason over **new InvestigationEpisode starts only**. `PENDING_OBSERVATION_SELECTED` identifies a legal pending-origin start. |
| Invalid examples | userId from hidden noise source<br>missing noiseEventId<br>`RELATED_NOISE_MERGED` / `CURRENT_INVESTIGATION_RETAINED` / `CORROBORATES_VISIBLE_TARGET` / `NO_ELIGIBLE_NOISE` serialized as a new episode start<br>unknown selectionReason |

Valid v1.1 example:

```json
{
  "id": "EXAMPLE_EVT_0400",
  "matchId": "EXAMPLE_MATCH_001",
  "userId": null,
  "eventType": "MONSTER_INVESTIGATE_STARTED",
  "ts": "2026-08-26T02:12:30.125Z",
  "valueJson": {
    "context": {
      "eventSequence": 400,
      "authorityTick": 840,
      "scenarioConfigVersion": "EXAMPLE_SCENARIO_CONFIG_VERSION",
      "policyVersion": "M1-015-v0",
      "configSource": "FIXED",
      "researchCaptureEnabled": true,
      "phase": "CORE_COLLECTION",
      "monsterType": "LISTENER",
      "monsterId": "listener_01"
    },
    "data": {
      "investigationEpisodeId": "inv_010",
      "noiseEventId": "noise_0042",
      "noiseType": "SPRINT",
      "heardAt": "2026-08-26T02:12:29.900Z",
      "selectionReason": "INITIAL_HIGHEST_AUDIBILITY"
    }
  },
  "reasonCode": null,
  "schemaVersion": "1.1"
}
```

`MONSTER_INVESTIGATE_STARTED.data.selectionReason` is a start cause, not a generic Listener disposition trace. The canonical allowed set is only the five start-capable wire values in §55.1. The Listener runtime remains the authority on whether a new episode was actually created; telemetry must not manufacture an episode in order to serialize a reason.

## 56.18 `MONSTER_INVESTIGATE_RESOLVED`

| Field | Contract |
|---|---|
| Status | RESEARCH_CAPTURE |
| Purpose | Exactly-once Listener InvestigationEpisode terminal outcome. |
| Authoritative Source | Listener terminal cleanup contract. |
| Telemetry Owner | ListenerTelemetryAdapter |
| `userId` semantic | `null`. |
| Occurrence moment | Episode terminal outcome is fixed before/with CurrentInvestigation cleanup. |
| Idempotency source | `(matchId, monsterId, investigationEpisodeId, RESOLVED)`. |
| Required context | `common v1.1 context`<br>`phase`<br>`monsterType = LISTENER`<br>`monsterId`<br>`researchCaptureEnabled = true` |
| Optional context | None |
| Required data | `investigationEpisodeId`<br>`outcome` — strict enum per §55.1 |
| Optional data | None |
| Allowed `reasonCode` | `null` |
| Position semantic | FORBIDDEN. |
| Ordering | Sequence at exactly-once terminalization. |
| Downstream consumers | FalseInvestigationRate/lifecycle audit. |
| Metric/research usage | Terminal outcome domain is canonical in §55.1; each InvestigationEpisode resolves at most once. |
| Invalid examples | two terminal events per episode<br>unknown outcome<br>hidden source identity |

Valid v1.1 example:

```json
{
  "id": "EXAMPLE_EVT_0430",
  "matchId": "EXAMPLE_MATCH_001",
  "userId": null,
  "eventType": "MONSTER_INVESTIGATE_RESOLVED",
  "ts": "2026-08-26T02:12:30.125Z",
  "valueJson": {
    "context": {
      "eventSequence": 430,
      "authorityTick": 840,
      "scenarioConfigVersion": "EXAMPLE_SCENARIO_CONFIG_VERSION",
      "policyVersion": "M1-015-v0",
      "configSource": "FIXED",
      "researchCaptureEnabled": true,
      "phase": "CORE_COLLECTION",
      "monsterType": "LISTENER",
      "monsterId": "listener_01"
    },
    "data": {
      "investigationEpisodeId": "inv_010",
      "outcome": "FALSE_INVESTIGATION"
    }
  },
  "reasonCode": null,
  "schemaVersion": "1.1"
}
```

## 56.19 `MONSTER_ATTACK_RESOLVED`

| Field | Contract |
|---|---|
| Status | RESEARCH_CAPTURE |
| Purpose | Exactly-once Stalker/Listener AttackEpisode `HIT`/`MISS` research evidence; not Player Life-State outcome. |
| Authoritative Source | Authoritative Stalker or Listener AttackController after that monster's own AttackEpisode resolution guard. |
| Telemetry Owner | StalkerTelemetryAdapter or ListenerTelemetryAdapter |
| `userId` semantic | `null`. |
| Occurrence moment | **The authoritative Monster AttackEpisode gameplay resolution commits once.** |
| Idempotency source | `(matchId, monsterId, attackEpisodeId, RESOLVED)`. |
| Required context | `common v1.1 context`<br>`phase`<br>`monsterType = STALKER | LISTENER`<br>`monsterId`<br>`researchCaptureEnabled = true` |
| Optional context | None |
| Required data | `attackEpisodeId`<br>`outcome` — strict `HIT` / `MISS` enum per §55.1 |
| Optional data | None |
| Allowed `reasonCode` | `null` |
| Position semantic | FORBIDDEN. |
| Ordering | Sequence after the owning Monster's authoritative attack outcome commit; never presentation replay. |
| Downstream consumers | Attack exactly-once research evidence. |
| Metric/research usage | AttackEpisode outcome only; it is not a `PLAYER_DOWNED` substitute. |
| Invalid examples | monsterType WARDEN<br>unknown outcome<br>second resolution<br>proxy event<br>attack telemetry used to fabricate a Life-State transition |

Valid v1.1 example:

```json
{
  "id": "EXAMPLE_EVT_0500",
  "matchId": "EXAMPLE_MATCH_001",
  "userId": null,
  "eventType": "MONSTER_ATTACK_RESOLVED",
  "ts": "2026-08-26T02:12:30.125Z",
  "valueJson": {
    "context": {
      "eventSequence": 500,
      "authorityTick": 840,
      "scenarioConfigVersion": "EXAMPLE_SCENARIO_CONFIG_VERSION",
      "policyVersion": "M1-015-v0",
      "configSource": "FIXED",
      "researchCaptureEnabled": true,
      "phase": "FINAL_HUNT",
      "monsterType": "STALKER",
      "monsterId": "stalker_01"
    },
    "data": {
      "attackEpisodeId": "atk_004",
      "outcome": "HIT"
    }
  },
  "reasonCode": null,
  "schemaVersion": "1.1"
}
```

### 56.19.1 Monster-specific authoritative resolution binding — TEL-DD-03

Telemetry intentionally keeps one common event while deferring attack timing/physics semantics to the gameplay owner:

| `monsterType` | Authoritative occurrence binding |
|---|---|
| `STALKER` | The authoritative Stalker attack resolution point defined by `Stalker_AI_Design_v1.1.md`; for Stalker v1.1 this is the Stalker authoritative Hit Moment resolution attempt. |
| `LISTENER` | The authoritative Listener AttackEpisode resolution point defined by `Listener_AI_Design_v1.0.md`. Telemetry does **not** require Listener to inherit Stalker's Hit Moment timing, LOS rule, collider rule, or ATTACK-completion rule. |

Canonical consequence separation:

```text
Monster AttackEpisode resolves HIT
→ gameplay/Life-State consequence is evaluated by gameplay
→ MONSTER_ATTACK_RESOLVED may record the attack research fact
→ PLAYER_DOWNED is emitted separately only if authoritative Player Life-State actually enters Downed
```

Therefore:

- `HIT` does not mean `DOWN`;
- `MONSTER_ATTACK_RESOLVED` never applies damage;
- `MONSTER_ATTACK_RESOLVED` never authors a Player Life-State transition;
- `PLAYER_DOWNED` remains exclusively owned by the authoritative Player Life-State transition;
- duplicate animation callbacks, resolver invocations, proxy presentation, reconnect/late join, and Fusion resimulation cannot create a second logical attack research event for the same `(matchId, monsterId, attackEpisodeId, RESOLVED)` occurrence.

## 56.20 `MONSTER_SEARCH_ENDED`

| Field | Contract |
|---|---|
| Status | RESEARCH_CAPTURE |
| Purpose | Stalker SearchEpisode terminal outcome. |
| Authoritative Source | Stalker FSM/SearchContext terminal transition. |
| Telemetry Owner | StalkerTelemetryAdapter |
| `userId` semantic | `null`. |
| Occurrence moment | SEARCH reaches one frozen terminal outcome. |
| Idempotency source | `(matchId, monsterId, searchEpisodeId, ENDED)`. |
| Required context | `common v1.1 context`<br>`phase`<br>`monsterType = STALKER`<br>`monsterId`<br>`researchCaptureEnabled = true` |
| Optional context | None |
| Required data | `searchEpisodeId`<br>`outcome` — strict enum per §55.1 |
| Optional data | None |
| Allowed `reasonCode` | `null` |
| Position semantic | FORBIDDEN. |
| Ordering | Sequence at SEARCH terminal transition. |
| Downstream consumers | SearchReacquisitionRate_v1.1. |
| Metric/research usage | Search terminal outcome domain is canonical in §55.1. |
| Invalid examples | Listener/Warden emits it<br>hidden target position<br>unknown outcome |

Valid v1.1 example:

```json
{
  "id": "EXAMPLE_EVT_0550",
  "matchId": "EXAMPLE_MATCH_001",
  "userId": null,
  "eventType": "MONSTER_SEARCH_ENDED",
  "ts": "2026-08-26T02:12:30.125Z",
  "valueJson": {
    "context": {
      "eventSequence": 550,
      "authorityTick": 840,
      "scenarioConfigVersion": "EXAMPLE_SCENARIO_CONFIG_VERSION",
      "policyVersion": "M1-015-v0",
      "configSource": "FIXED",
      "researchCaptureEnabled": true,
      "phase": "FINAL_HUNT",
      "monsterType": "STALKER",
      "monsterId": "stalker_01"
    },
    "data": {
      "searchEpisodeId": "search_008",
      "outcome": "SAME_TARGET_REACQUIRED"
    }
  },
  "reasonCode": null,
  "schemaVersion": "1.1"
}
```

## 56.21 `WARDEN_TELEGRAPH_STARTED`

| Field | Contract |
|---|---|
| Status | RESEARCH_CAPTURE |
| Purpose | Evidence exact Warden action/door/footprint was telegraphed before apply. |
| Authoritative Source | WardenTelegraphController after policy selection. |
| Telemetry Owner | WardenTelemetryAdapter |
| `userId` semantic | `null`. |
| Occurrence moment | TELEGRAPHING begins. |
| Idempotency source | `(matchId, wardenActionId, TELEGRAPH_STARTED)`. |
| Required context | `common v1.1 context`<br>`phase`<br>`researchCaptureEnabled = true` |
| Optional context | None |
| Required data | `wardenActionId`<br>`doorId`<br>`routeFootprintIdentity`<br>`selectionReason` — strict selected-action enum per §55.1 |
| Optional data | None |
| Allowed `reasonCode` | `null` |
| Position semantic | FORBIDDEN. |
| Ordering | Sequence at start; matching APPLIED must be later. |
| Downstream consumers | Telegraph-before-lock fairness audit. |
| Metric/research usage | Selection-reason domain is canonical in §55.1; Warden no-action reasons never produce a telegraph-start event. |
| Invalid examples | rejected/no-action candidate<br>`NO_SAFE_CANDIDATE` or `NO_MEANINGFUL_PRESSURE` serialized as telegraph selection<br>Door/footprint silently changed under same action<br>graph dump |

Valid v1.1 example:

```json
{
  "id": "EXAMPLE_EVT_0600",
  "matchId": "EXAMPLE_MATCH_001",
  "userId": null,
  "eventType": "WARDEN_TELEGRAPH_STARTED",
  "ts": "2026-08-26T02:12:30.125Z",
  "valueJson": {
    "context": {
      "eventSequence": 600,
      "authorityTick": 840,
      "scenarioConfigVersion": "EXAMPLE_SCENARIO_CONFIG_VERSION",
      "policyVersion": "M1-015-v0",
      "configSource": "FIXED",
      "researchCaptureEnabled": true,
      "phase": "FINAL_HUNT"
    },
    "data": {
      "wardenActionId": "warden_action_003",
      "doorId": "door_W2",
      "routeFootprintIdentity": "EXAMPLE_ROUTE_LOCK_DEF_W2",
      "selectionReason": "HIGHEST_PRESSURE_FRESH_DOOR"
    }
  },
  "reasonCode": null,
  "schemaVersion": "1.1"
}
```

## 56.22 `WARDEN_ROUTE_ACTION_APPLIED`

| Field | Contract |
|---|---|
| Status | RESEARCH_CAPTURE |
| Purpose | Exactly-once applied Warden route action and RoutePressure evidence. |
| Authoritative Source | WardenRouteActionController after precommit Valid and actual overlay apply. |
| Telemetry Owner | WardenTelemetryAdapter |
| `userId` semantic | `null`. |
| Occurrence moment | Action enters APPLIED after overlay write. |
| Idempotency source | `(matchId, wardenActionId, APPLIED)`. |
| Required context | `common v1.1 context`<br>`phase`<br>`researchCaptureEnabled = true` |
| Optional context | None |
| Required data | `wardenActionId`<br>`doorId`<br>`routeFootprintIdentity`<br>`routePressure`<br>`preMeanShortestRouteCost`<br>`postMeanShortestRouteCost`<br>`safetyStatus` — strict `VALID` per §55.1 |
| Optional data | None |
| Allowed `reasonCode` | `null` |
| Position semantic | FORBIDDEN. |
| Ordering | Must follow matching telegraph; sequence after authoritative apply. |
| Downstream consumers | RoutePressure_v1.0; applied denominator for InvalidLockRate. |
| Metric/research usage | v1.0 applied action requires `safetyStatus = VALID`; RoutePressure metric is valid and > 0. The strict status token is canonical in §55.1. |
| Invalid examples | routePressure 0 applied<br>footprint mismatch<br>hypothetical candidate emitted as applied |

Valid v1.1 example:

```json
{
  "id": "EXAMPLE_EVT_0620",
  "matchId": "EXAMPLE_MATCH_001",
  "userId": null,
  "eventType": "WARDEN_ROUTE_ACTION_APPLIED",
  "ts": "2026-08-26T02:12:30.125Z",
  "valueJson": {
    "context": {
      "eventSequence": 620,
      "authorityTick": 840,
      "scenarioConfigVersion": "EXAMPLE_SCENARIO_CONFIG_VERSION",
      "policyVersion": "M1-015-v0",
      "configSource": "FIXED",
      "researchCaptureEnabled": true,
      "phase": "FINAL_HUNT"
    },
    "data": {
      "wardenActionId": "warden_action_003",
      "doorId": "door_W2",
      "routeFootprintIdentity": "EXAMPLE_ROUTE_LOCK_DEF_W2",
      "routePressure": 0.18,
      "preMeanShortestRouteCost": 12.0,
      "postMeanShortestRouteCost": 14.64,
      "safetyStatus": "VALID"
    }
  },
  "reasonCode": null,
  "schemaVersion": "1.1"
}
```

## 56.23 `WARDEN_ROUTE_SAFETY_CHECKED`

| Field | Contract |
|---|---|
| Status | RESEARCH_CAPTURE |
| Purpose | Metric-eligible post-apply/active-lock reachability check. |
| Authoritative Source | WardenSafetyValidator trace owner. |
| Telemetry Owner | WardenTelemetryAdapter |
| `userId` semantic | `null`. |
| Occurrence moment | POST_APPLY or ACTIVE_LOCK_REVALIDATION result fixed. |
| Idempotency source | `(matchId, wardenActionId, safetyCheckId)`. |
| Required context | `common v1.1 context`<br>`phase`<br>`researchCaptureEnabled = true` |
| Optional context | None |
| Required data | `wardenActionId`<br>`safetyCheckId`<br>`checkType` — strict enum per §55.1<br>`objectiveReachable`<br>`safetyStatus` — strict enum per §55.1 |
| Optional data | `safetyReason` — REQUIRED when `safetyStatus = REJECTED`, FORBIDDEN when `VALID`; strict Warden safety-reason domain per §55.1 |
| Allowed `reasonCode` | `null` |
| Position semantic | FORBIDDEN. |
| Ordering | Sequence at completed check; candidate prechecks excluded. |
| Downstream consumers | ObjectiveReachabilityRate_v1.0. |
| Metric/research usage | `checkType` is strictly `POST_APPLY | ACTIVE_LOCK_REVALIDATION`; candidate prechecks/rejections are excluded from persistent ObjectiveReachabilityRate evidence. |
| Invalid examples | candidate precheck emitted<br>`NOT_EVALUATED` emitted as a completed research check<br>REJECTED missing safetyReason<br>VALID with safetyReason<br>unknown safetyReason<br>graph dump |

Valid v1.1 example:

```json
{
  "id": "EXAMPLE_EVT_0621",
  "matchId": "EXAMPLE_MATCH_001",
  "userId": null,
  "eventType": "WARDEN_ROUTE_SAFETY_CHECKED",
  "ts": "2026-08-26T02:12:30.125Z",
  "valueJson": {
    "context": {
      "eventSequence": 621,
      "authorityTick": 840,
      "scenarioConfigVersion": "EXAMPLE_SCENARIO_CONFIG_VERSION",
      "policyVersion": "M1-015-v0",
      "configSource": "FIXED",
      "researchCaptureEnabled": true,
      "phase": "FINAL_HUNT"
    },
    "data": {
      "wardenActionId": "warden_action_003",
      "safetyCheckId": "safety_003_01",
      "checkType": "POST_APPLY",
      "objectiveReachable": true,
      "safetyStatus": "VALID"
    }
  },
  "reasonCode": null,
  "schemaVersion": "1.1"
}
```

## 56.24 `WARDEN_ROUTE_ACTION_RELEASED`

| Field | Contract |
|---|---|
| Status | RESEARCH_CAPTURE |
| Purpose | Applied Warden lock release/fail-safe evidence. |
| Authoritative Source | WardenRouteActionController after exactly-once overlay release. |
| Telemetry Owner | WardenTelemetryAdapter |
| `userId` semantic | `null`. |
| Occurrence moment | Applied lock leaves APPLIED and overlay removed once. |
| Idempotency source | `(matchId, wardenActionId, RELEASED)`. |
| Required context | `common v1.1 context`<br>`phase`<br>`researchCaptureEnabled = true` |
| Optional context | None |
| Required data | `wardenActionId`<br>`doorId`<br>`routeFootprintIdentity`<br>`releaseReason` — strict enum per §55.1 |
| Optional data | `failSafeReason` — REQUIRED iff `releaseReason = FAIL_SAFE`; FORBIDDEN when `EXPIRED`; strict enum per §55.1 |
| Allowed `reasonCode` | `null` |
| Position semantic | FORBIDDEN. |
| Ordering | Sequence after apply/release commit. |
| Downstream consumers | InvalidLockRate / lifecycle. |
| Metric/research usage | `releaseReason` is `EXPIRED` or `FAIL_SAFE`. A `FAIL_SAFE` release enters `InvalidLockRate_v1.0` numerator only when its canonical `failSafeReason` is one of the six Warden-owned invalid-active-lock reasons in §55.1. Normal expiry never enters the numerator. |
| Invalid examples | release without apply<br>FAIL_SAFE missing reason<br>EXPIRED with failSafeReason<br>`EXTERNAL_UNSAFE_STATE_AFTER_WARDEN_RELEASE` used as failSafeReason<br>telegraph cancellation represented as release |

Valid v1.1 example:

```json
{
  "id": "EXAMPLE_EVT_0700",
  "matchId": "EXAMPLE_MATCH_001",
  "userId": null,
  "eventType": "WARDEN_ROUTE_ACTION_RELEASED",
  "ts": "2026-08-26T02:12:30.125Z",
  "valueJson": {
    "context": {
      "eventSequence": 700,
      "authorityTick": 840,
      "scenarioConfigVersion": "EXAMPLE_SCENARIO_CONFIG_VERSION",
      "policyVersion": "M1-015-v0",
      "configSource": "FIXED",
      "researchCaptureEnabled": true,
      "phase": "FINAL_HUNT"
    },
    "data": {
      "wardenActionId": "warden_action_003",
      "doorId": "door_W2",
      "routeFootprintIdentity": "EXAMPLE_ROUTE_LOCK_DEF_W2",
      "releaseReason": "EXPIRED"
    }
  },
  "reasonCode": null,
  "schemaVersion": "1.1"
}
```

`EXTERNAL_UNSAFE_STATE_AFTER_WARDEN_RELEASE` remains a Warden local/debug fail-safe diagnostic after the Warden-owned overlay has already been removed. It is not serializable as this event's `failSafeReason` and is excluded from `InvalidLockRate_v1.0`.

---

# 57. Research Metric Reconstructability

| Metric | Raw event(s) | Join key | Ordering | Completeness requirement | Reconstructable? |
|---|---|---|---|---|---|
| Match Duration | `MATCH_STARTED`, `MATCH_ENDED` | matchId | ts + lifecycle order | both valid | YES |
| Team Objective Time | objective-bearing `PHASE_STARTED`, `PHASE_COMPLETED` | matchId + phase | sequence + ts | required phase pairs complete | YES |
| Player Survival | `PLAYER_ESCAPED` / `PLAYER_ELIMINATED` | matchId + userId | terminal consistency | eligible match | YES |
| Player Noise | `NOISE_EMITTED` | matchId + userId | count/type | schema-valid noise + downstream filter version | YES |
| Listener NoiseResponseLatency_v1.0 | `MONSTER_INVESTIGATE_STARTED` | matchId + monsterId + investigationEpisodeId | event.ts vs heardAt | research capture complete for metric | YES |
| Listener SourceSelectionShare_v1.0 | `MONSTER_INVESTIGATE_STARTED` | same | start occurrence | complete starts | YES |
| Listener FalseInvestigationRate_v1.0 | `MONSTER_INVESTIGATE_RESOLVED` | episode key | terminal exactly once | eligible terminals complete | YES |
| Stalker SearchReacquisitionRate_v1.1 | `MONSTER_SEARCH_ENDED` | matchId + monsterId + searchEpisodeId | terminal | complete search terminal capture | YES |
| Stalker coverage/revisit/stuck/path metrics | local deterministic evaluation traces | local trace IDs | subsystem-specific | backend persistence not required | LOCAL EVIDENCE BY DESIGN |
| Warden RoutePressure_v1.0, applied actions | `WARDEN_ROUTE_ACTION_APPLIED` | matchId + wardenActionId | apply | Warden capture complete | YES |
| Warden ObjectiveReachabilityRate_v1.0 | `WARDEN_ROUTE_SAFETY_CHECKED` | action + safetyCheckId | eventSequence | all metric-eligible checks | YES |
| Warden InvalidLockRate_v1.0 | applied + released events | wardenActionId | lifecycle | applied/release complete | YES |
| Warden CandidateSafetyRejectRate | local candidate diagnostics | local evaluation trace | local | persistent event deliberately absent | LOCAL / DEFERRED |
| Fixed-vs-Adaptive analysis | production/Profile facts + provenance | matchId/testRun | canonical order | M1-020 data-quality/readiness gates | TELEMETRY SUPPORTS; EXPERIMENT NOT READY |

## 57.1 Listener `SourceSelectionShare_v1.0` exact persistence semantics — TEL-DD-01

For each valid `(noiseType, selectionReason)` bucket:

```text
numerator
=
count valid MONSTER_INVESTIGATE_STARTED events in that bucket

denominator
=
count all valid MONSTER_INVESTIGATE_STARTED events
for the declared Listener evaluation window
```

This denominator is therefore **all NEW InvestigationEpisode starts**, not all hearing decisions.

Only the five start-capable `selectionReason` tokens in §55.1 are legal. In particular:

```text
PENDING_OBSERVATION_SELECTED
→ included as a pending-origin new episode start

RELATED_NOISE_MERGED
CURRENT_INVESTIGATION_RETAINED
CORROBORATES_VISIBLE_TARGET
NO_ELIGIBLE_NOISE
→ excluded because they cannot produce MONSTER_INVESTIGATE_STARTED
```

This makes pending-origin starts distinguishable without persisting the Host-private pending inbox.

## 57.2 Warden `ObjectiveReachabilityRate_v1.0` persistent eligibility — TEL-DD-02

The persistent event denominator contains only:

```text
WARDEN_ROUTE_SAFETY_CHECKED
where checkType = POST_APPLY
or checkType = ACTIVE_LOCK_REVALIDATION
```

Candidate prechecks/rejections remain excluded, even when they use the same WardenSafetyValidator internally.

## 57.3 Warden `InvalidLockRate_v1.0` exact reconstruction — TEL-DD-02

Unit: unique successfully applied `wardenActionId`.

```text
numerator
=
count unique successfully applied wardenActionId
whose exactly-once terminal WARDEN_ROUTE_ACTION_RELEASED has:
    releaseReason = FAIL_SAFE
    AND failSafeReason ∈ CanonicalWardenInvalidActiveLockReason_v1.1 (§55.1)

denominator
=
count unique successfully applied wardenActionId
```

Exclusions:

- rejected candidates;
- candidate safety prechecks;
- cancelled telegraphs;
- never-applied actions;
- `releaseReason = EXPIRED`;
- `EXTERNAL_UNSAFE_STATE_AFTER_WARDEN_RELEASE`;
- duplicate delivery of an already accepted lifecycle event.

One `wardenActionId` contributes at most once to the numerator. Zero denominator follows the owning metric protocol's normal not-evaluated/null rule; no acceptance threshold is frozen here.

## 57.4 `MONSTER_ATTACK_RESOLVED` research boundary — TEL-DD-03

`MONSTER_ATTACK_RESOLVED` is AttackEpisode research evidence only.

```text
HIT
≠
PLAYER_DOWNED
```

A down is reconstructable only from the independently Life-State-owned `PLAYER_DOWNED` event. Telemetry never infers or fabricates Down from attack outcome.

Missing required evidence makes the metric unavailable, not zero.

---

# 58. Telemetry Data Quality

Data-quality evidence is not a gameplay TelemetryEvent and does not consume gameplay `eventSequence`.

Conceptual record:

```text
TelemetryDataQualityRecord_v1.1
- matchId?
- detectedAt
- reason
- relatedEventId?
- relatedEventSequence?
- schemaVersion?
- severity
- bounded details?
```

Canonical reasons:

```text
SCHEMA_INVALID_EVENT
UNSUPPORTED_SCHEMA_VERSION
RESERVED_EVENT_RECEIVED
DUPLICATE_RETRY
IDENTITY_CONFLICT
SEQUENCE_CONFLICT
SEQUENCE_GAP_DETECTED
MISSING_MATCH_STARTED
MISSING_CLEAN_MATCH_END
INCOMPLETE_PHASE_PAIR
PERMANENT_REJECTION
BUFFER_OVERFLOW
RETRY_EXHAUSTED
CRITICAL_INSTRUMENTATION_FAILURE
UNKNOWN_EXPERIMENT_CONDITION
EXPERIMENT_CONDITION_MISMATCH
SCENARIO_CONFIG_VERSION_UNKNOWN
PROVENANCE_MISMATCH
```

These diagnose telemetry quality only; they never command gameplay.

---

# 59. Completeness / Availability Boundary

Telemetry stream state:

```text
COMPLETE | INCOMPLETE | INVALID | UNKNOWN
```

Raw metric input state may be:

```text
AVAILABLE | UNAVAILABLE
```

M1-014 still owns `MetricAvailability` and `MatchProfileEligibility`. M1-020 owns experiment inclusion/exclusion.

Examples:

```text
researchCaptureEnabled=false
+ no Listener RESEARCH_CAPTURE events
→ normal production stream may still be COMPLETE
→ Listener research metric was not captured
```

```text
researchCaptureEnabled=true
+ eligible Listener terminal event missing
→ full telemetry stream may be INCOMPLETE
→ affected research metric unavailable/incomplete
→ owning research protocol decides eligibility
```

Loss of an enabled `RESEARCH_CAPTURE` event does **not** automatically make unrelated Profile metrics unavailable. Each Profile, research, and experiment consumer evaluates only the evidence its own contract requires.

```text
Telemetry completeness
!= Profile eligibility
!= Experiment eligibility
```

No separate production/research `eventSequence` domain is introduced.

---

# 60. Observability

Read-only `TelemetryDebugSnapshot`:

```text
HasStateAuthority
CurrentMatchId
ActiveSchemaVersion

LastAllocatedEventSequence
PendingEventCount
PendingBatchCount
OldestPendingAge

EventsCreated
EventsAcked
DuplicateAckCount
PermanentRejectCount
TransientRetryCount
IdentityConflictCount
SequenceConflictCount
BufferOverflowCount
RetryExhaustedCount

LocalStreamCompleteness
LastRejectReason?
LastTransportError?
LastAckTime?

BuildVersion
MapContentVersion
ScenarioConfigVersion
PolicyVersion
ConfigSource
ExperimentCondition?
ResearchCaptureEnabled
```

Backend observability should expose accepted/duplicate/reject/conflict counts and matches with gaps/missing lifecycle anchors.

Debug cannot create, mutate, reorder, delete, or reclassify gameplay events.

---

# 61. Reason / Result Types

## 61.1 `TelemetryValidationRejectReason`

```text
NONE
UNSUPPORTED_SCHEMA_VERSION
EVENT_TYPE_NOT_REGISTERED
EVENT_STATUS_NOT_EMITTABLE
RESEARCH_CAPTURE_NOT_ENABLED
MISSING_REQUIRED_FIELD
INVALID_FIELD_TYPE
INVALID_USER_ID_SEMANTIC
INVALID_REASON_CODE
INVALID_CONTEXT
INVALID_DATA
INVALID_EVENT_ENUM
INVALID_EVENT_ENUM_COMBINATION
INVALID_TIMESTAMP
INVALID_EVENT_SEQUENCE
INVALID_AUTHORITY_TICK
INVALID_PROVENANCE
INVALID_POSITION
PAYLOAD_TOO_LARGE
IDENTITY_CONFLICT
SEQUENCE_CONFLICT
```

## 61.2 `TelemetryAckStatus`

```text
ACCEPTED
DUPLICATE_ALREADY_ACCEPTED
PERMANENTLY_REJECTED
TRANSIENT_FAILURE
```

## 61.3 `TelemetryBufferFailureReason`

```text
BUFFER_CAPACITY_EXCEEDED
RETRY_EXHAUSTED
SERIALIZATION_FAILED
```

## 61.4 `TelemetryStreamCompleteness`

```text
COMPLETE
INCOMPLETE
INVALID
UNKNOWN
```

Transport/schema reasons are not gameplay `reasonCode`.

---

# 62. Unity / Host Component Contracts

## 62.1 `TelemetryEventFactory`

| Item | Contract |
|---|---|
| Purpose | construct one immutable event per authoritative SourceOccurrenceKey |
| Owns | event ID creation/reuse and immutable snapshot assembly |
| Inputs | event contract, source fact, provenance, sequence allocator |
| Outputs | immutable TelemetryEvent |
| Forbidden | gameplay mutation; retry-time mutation |
| Tests | identity/common-context/immutability |

## 62.2 `TelemetrySequenceAllocator`

| Item | Contract |
|---|---|
| Purpose | single monotonic match ordering domain |
| Lifecycle | initialize new match; first sequence = 1 |
| Forbidden | independent per-emitter sequence domains |
| Tests | monotonicity/same-tick/no reuse |

## 62.3 `TelemetryProvenanceProvider`

Snapshots AppliedScenarioConfig/build/experiment provenance. It cannot change those sources.

## 62.4 `TelemetryRouter / Emitter`

Routes schema-approved immutable events. It may not activate reserved event types.

## 62.5 `TelemetryBuffer`

Bounded pending/retry store; overflow behavior is §45.

## 62.6 `TelemetryBatchSender`

Assembles bounded batches and sends without changing events.

## 62.7 `TelemetryAckProcessor`

Applies per-event ack: remove/retry/quarantine.

## 62.8 `TelemetryDebugProvider`

Read-only projection.

## 62.9 Domain adapters

```text
MatchTelemetryAdapter
PlayerTelemetryAdapter
ObjectiveTelemetryAdapter
NoiseTelemetryAdapter
StalkerTelemetryAdapter
ListenerTelemetryAdapter
WardenTelemetryAdapter
```

Do not create a God `TelemetryManager` owning gameplay semantics, Profile, transport, and backend at once.

---

# 63. Backend Contracts

## 63.1 `TelemetryBatchEndpoint`

Host/service authenticated, bounded `events[]`, per-event semantic ack.

## 63.2 `TelemetryValidator`

Dispatch by `schemaVersion`; preserve frozen v1.0 and v1.1 paths.

## 63.3 `TelemetryIdempotencyStore`

Conceptually enforces:

```text
id → immutable semantic fingerprint/stored identity
(matchId,eventSequence) → id
```

Exact transaction/database technology is TBD.

## 63.4 `TelemetryEventRepository`

Immutable accepted raw events with original schemaVersion.

## 63.5 `TelemetryDataQualityEvaluator`

Sequence/lifecycle/provenance evidence without raw mutation.

## 63.6 `TelemetryAggregationInput`

Validated ordered events + source schema + data-quality state for MatchTelemetry processing.

Backend must not repair invalid gameplay into a different valid fact.

---

# 64. Photon Fusion Authority

Baseline:

```text
Unity 6000.5.8f1
Photon Fusion 2.1.1 Stable build 2177
Host Mode
2–4 players
```

State Authority owns committed gameplay facts that generate authoritative gameplay telemetry.

Input Authority may send player input/requests; only accepted Host outcomes become authoritative telemetry.

Proxy clients must not independently emit monster, life-state, route, objective, or RuntimeNoise outcome telemetry.

Telemetry HTTP transport is not required to pass through Fusion RPC/networked state.

---

# 65. Late Join / Reconnect

Late join reconstructs current gameplay/network presentation, not historical telemetry.

```text
join during Warden lock
→ current lock presentation
X→ WARDEN_ROUTE_ACTION_APPLIED again
```

```text
join while Stalker already in CHASE
X→ historical target event
```

Reconnect does not restart historical event IDs/sequences.

---

# 66. Resimulation / Duplicate Callback Safety

Proxy prediction/resimulation cannot emit authoritative gameplay telemetry.

Stable SourceOccurrenceKey plus source exactly-once lifecycle means repeated callback, animation replay, response loss, late join, or resync cannot create a second logical event.

---

# 67. Performance Contract

No arbitrary Hz/ms budget.

Freeze:

- event-driven emission;
- no per-frame transform events;
- bounded buffer/batch/retry;
- low-frequency AI research capture only;
- candidate/path/debug spam stays local;
- no full FacilityGraph/AI-memory payloads;
- serialize immutable events efficiently;
- profile before numeric budget freeze.

TBD: BufferCapacity, BatchSize, FlushInterval, RetryBackoff, MaxRetry/retry age, PayloadSizeLimit.

---

# 68. Tests — Schema / Pure Logic

No test is claimed passed.

| ID | Requirement | Expected |
|---|---|---|
| TEL-E-001 | valid common v1.1 event | accepted |
| TEL-E-002 | missing required common field | reject |
| TEL-E-003 | unsupported version | reject |
| TEL-E-004 | reserved event | reject |
| TEL-E-005 | unknown eventType | reject |
| TEL-E-006 | invalid userId | reject |
| TEL-E-007 | invalid reasonCode | reject |
| TEL-E-008 | unknown field | reject |
| TEL-E-009 | retry stable id | unchanged |
| TEL-E-010 | retry stable ts | unchanged |
| TEL-E-011 | retry stable sequence | unchanged |
| TEL-E-012 | identical duplicate | idempotent duplicate |
| TEL-E-013 | same id different payload | IDENTITY_CONFLICT |
| TEL-E-014 | same sequence different id | SEQUENCE_CONFLICT |
| TEL-E-015 | sequence monotonicity | 1,2,3... no reuse |
| TEL-E-016 | multiple same tick | sequence total order |
| TEL-E-017 | null authorityTick valid case | accepted |
| TEL-E-018 | v1.0 into v1.1 backend | v1.0 validator path |
| TEL-E-019 | v1.1 into v1.0 backend | unsupported |
| TEL-E-020 | mid-match config change | old/new occurrence provenance correct |
| TEL-E-021 | experiment condition missing protocol | reject |
| TEL-E-022 | non-experiment omits experiment fields | valid |
| TEL-E-023 | NOISE_EMITTED missing noiseEventId | reject |
| TEL-E-024 | Noise type/reason mismatch | reject |
| TEL-E-025 | v1.1 PLAYER_DOWNED WARDEN_ATTACK | reject |
| TEL-E-026 | v1.0 WARDEN_ATTACK legacy | preserve v1.0 validation |
| TEL-E-027 | Listener investigation start enums | validate |
| TEL-E-028 | Listener terminal outcome enum | validate |
| TEL-E-029 | Stalker search outcome enum | validate |
| TEL-E-030 | Warden applied RoutePressure=0 | reject v1.1 Warden contract |
| TEL-E-031 | Warden candidate-precheck safety research event | reject checkType |
| TEL-E-032 | FAIL_SAFE release missing failSafeReason | reject |
| TEL-E-033 | finite xyz position | valid |
| TEL-E-034 | forbidden Warden position | reject |
| TEL-E-035 | same JSON semantic value with key-order difference | duplicate, not conflict |
| TEL-E-036 | changed array order | semantic conflict if same id |
| TEL-E-037 | gap | INCOMPLETE until legitimate fill |
| TEL-E-038 | contiguous 1..N + valid lifecycle | COMPLETE |
| TEL-E-039 | missing MATCH_STARTED | cannot be COMPLETE |
| TEL-E-040 | identity/sequence corruption | INVALID |
| TEL-E-ENUM-01 | valid `MONSTER_INVESTIGATE_STARTED.selectionReason` start token | accepted |
| TEL-E-ENUM-02 | `PENDING_OBSERVATION_SELECTED` on legal pending-origin new episode | accepted |
| TEL-E-ENUM-03 | `RELATED_NOISE_MERGED` serialized as INVESTIGATE_STARTED reason | permanent reject `INVALID_EVENT_ENUM_COMBINATION` |
| TEL-E-ENUM-04 | `CURRENT_INVESTIGATION_RETAINED` serialized as INVESTIGATE_STARTED reason | permanent reject `INVALID_EVENT_ENUM_COMBINATION` |
| TEL-E-ENUM-05 | `CORROBORATES_VISIBLE_TARGET` serialized as INVESTIGATE_STARTED reason | permanent reject `INVALID_EVENT_ENUM_COMBINATION` |
| TEL-E-ENUM-06 | unknown `selectionReason` | permanent reject `INVALID_EVENT_ENUM` |
| TEL-E-ENUM-07 | unknown strict event-specific enum in another registered field | permanent reject `INVALID_EVENT_ENUM` |
| TEL-E-WAR-01 | `FAIL_SAFE` + one canonical Warden-owned `failSafeReason` | accepted |
| TEL-E-WAR-02 | `FAIL_SAFE` without `failSafeReason` | reject `INVALID_EVENT_ENUM_COMBINATION` / required-field validation |
| TEL-E-WAR-03 | `EXPIRED` with `failSafeReason` | reject `INVALID_EVENT_ENUM_COMBINATION` |
| TEL-E-WAR-04 | `EXTERNAL_UNSAFE_STATE_AFTER_WARDEN_RELEASE` used as release `failSafeReason` | permanent reject |
| TEL-E-WAR-05 | InvalidLockRate numerator with duplicate delivery of one qualifying applied action | one unique qualifying `wardenActionId` counted once |
| TEL-E-WAR-06 | normal `EXPIRED` release | applied denominator only; not numerator |
| TEL-E-WAR-07 | cancelled telegraph | neither numerator nor applied denominator |
| TEL-E-WAR-08 | candidate safety reject | not InvalidLockRate numerator; no persistent applied action created |
| TEL-E-ATK-01 | Stalker authoritative AttackEpisode resolution | one `MONSTER_ATTACK_RESOLVED` |
| TEL-E-ATK-02 | Listener authoritative AttackEpisode resolution | one `MONSTER_ATTACK_RESOLVED` |
| TEL-E-ATK-03 | Stalker occurrence binding | uses Stalker-owned authoritative resolution point |
| TEL-E-ATK-04 | Listener occurrence binding | validator/event contract does not require Stalker Hit Moment implementation/timing |
| TEL-E-ATK-05 | `HIT` without Life-State Down transition | no fabricated `PLAYER_DOWNED` |
| TEL-E-ATK-06 | `HIT` followed by real authoritative Down transition | attack research event + independently Life-State-owned `PLAYER_DOWNED` |
| TEL-E-ATK-07 | duplicate attack resolution callback/delivery path | one logical attack research event by stable attack-episode idempotency key |

These are required contract tests only; no passing execution result is claimed.

---

# 69. Tests — Unity / PlayMode

| ID | Scenario | Expected |
|---|---|---|
| TEL-P-001 | Host accepted Sprint | one RuntimeNoiseEvent + one NOISE_EMITTED sharing noiseEventId |
| TEL-P-002 | client-only SFX | no authoritative NOISE_EMITTED |
| TEL-P-003 | Core drop + runtime noise | two facts/events with stable commit order |
| TEL-P-004 | Down transition | one PLAYER_DOWNED from Life-State |
| TEL-P-005 | duplicate attack callback | no duplicate outcome/life event |
| TEL-P-006 | phase lifecycle | valid start/completed pair |
| TEL-P-007 | Security interruption | interruption without duplicate lifecycle |
| TEL-P-008 | Listener investigation start | noise correlation, null userId |
| TEL-P-009 | Listener false terminal | one FALSE_INVESTIGATION resolved event |
| TEL-P-010 | Listener non-false terminals | exact outcome |
| TEL-P-011 | Warden telegraph→apply | same action/door/footprint; ordered |
| TEL-P-012 | Warden zero pressure | no applied telemetry |
| TEL-P-013 | Warden fail-safe | one released event with fail-safe evidence |
| TEL-P-014 | research capture disabled | no RESEARCH_CAPTURE; gameplay/production unchanged |
| TEL-P-015 | config changes | event-time provenance frozen correctly |
| TEL-P-016 | position snapshot | no live Transform tracking |
| TEL-P-017 | retry | event immutable |
| TEL-P-018 | transient backend failure | retained/retried |
| TEL-P-019 | permanent reject | no infinite retry |
| TEL-P-020 | buffer overflow | gameplay unchanged; sequence not reused; incomplete evidence |
| TEL-P-021 | normal match end | final MATCH_ENDED + flush attempt |
| TEL-P-022 | shutdown | best effort; no guaranteed-delivery claim |
| TEL-P-023 | telemetry unavailable | monster/gameplay unaffected |
| TEL-P-024 | no per-frame position emitter | integration/static contract check |

---

# 70. Tests — Backend / Integration

| ID | Scenario | Expected |
|---|---|---|
| TEL-I-001 | valid v1.1 batch | accepted |
| TEL-I-002 | mixed batch | per-event partial ack |
| TEL-I-003 | response lost after commit + retry | duplicate accepted, one raw record |
| TEL-I-004 | same id different payload | conflict/quarantine |
| TEL-I-005 | same sequence different IDs | sequence conflict |
| TEL-I-006 | late batch | accepted if valid |
| TEL-I-007 | out-of-arrival-order | aggregate by sequence |
| TEL-I-008 | gap then late fill | completeness recomputed |
| TEL-I-009 | unsupported version | permanent reject |
| TEL-I-010 | v1.0 legacy | frozen validator |
| TEL-I-011 | v1.1 | v1.1 validator |
| TEL-I-012 | reserved event | reject |
| TEL-I-013 | transient storage error | transient ack |
| TEL-I-014 | raw event | immutable |
| TEL-I-015 | missing clean end | not COMPLETE |
| TEL-I-016 | contiguous finalized stream | COMPLETE |
| TEL-I-017 | experiment provenance mismatch | quality/exclusion evidence only |
| TEL-I-018 | permanent event reject | stream cannot silently be COMPLETE |
| TEL-I-019 | research event | valid persistence |
| TEL-I-020 | aggregation | only validated events |

---

# 71. 2–4 Player Fusion Tests

| ID | Scenario | Expected |
|---|---|---|
| TEL-N-001 | Host + 1 client | Host-authoritative event owner |
| TEL-N-002 | Host + 2 clients | no proxy duplicates |
| TEL-N-003 | Host + 3 clients | no proxy duplicates |
| TEL-N-004 | simultaneous player actions | distinct IDs/sequences |
| TEL-N-005 | same authoritative tick | equal tick allowed; sequence total order |
| TEL-N-006 | proxy life-state presentation | no PLAYER_DOWNED emission |
| TEL-N-007 | Listener proxy | no investigation emission |
| TEL-N-008 | Warden proxy | no route emission |
| TEL-N-009 | reconnect | no historical replay |
| TEL-N-010 | late join Warden lock | presentation only |
| TEL-N-011 | late join Stalker state | no historical AI event |
| TEL-N-012 | Host rejects client action | no authoritative outcome telemetry |
| TEL-N-013 | client resimulation | no production event |
| TEL-N-014 | Host config provenance | correct per occurrence |
| TEL-N-015 | client arrival order differs | Host occurrence order retained |

---

# 72. Failure Matrix

| Failure | Expected behavior | Retry? | Data quality | Owner |
|---|---|---:|---|---|
| backend unavailable | keep pending while policy permits | Yes | incomplete if ultimately lost | Sender/Buffer |
| timeout/no response | NOT_ACKNOWLEDGED | Yes | none until exhausted | Sender |
| partial response | apply per item | Selective | per item | AckProcessor |
| invalid schema | permanent reject | No | invalid/incomplete | Validator |
| unknown version | permanent reject | No | unsupported | Validator |
| reserved event | permanent reject | No | instrumentation failure | Validator |
| unknown/unregistered strict event enum | permanent reject | No | schema/semantic invalid | Validator |
| invalid event-specific enum combination | permanent reject | No | schema/semantic invalid | Validator |
| duplicate identical | duplicate accepted | No | duplicate count | Idempotency |
| same id/different payload | conflict/quarantine | No | INVALID | Idempotency |
| same sequence/different ID | conflict/quarantine | No | INVALID | Idempotency |
| buffer full | fail new enqueue; no silent eviction | n/a | INCOMPLETE | Buffer |
| retry exhausted | no success claim | No | INCOMPLETE | Sender |
| match ends pending | enqueue terminal if possible; flush attempt | while alive | based on ack/gaps | Match/Buffer |
| Host shutdown | bounded best effort | best effort | possibly incomplete | Host |
| missing start | not COMPLETE | n/a | incomplete/unknown | Quality |
| missing end | no final bound | n/a | incomplete/unknown | Quality |
| sequence gap | accept late fill | n/a | incomplete until filled | Quality |
| invalid experiment condition | gameplay unaffected | n/a | research provenance invalid | Quality |
| unknown ScenarioConfig version | reject/quarantine | No | invalid provenance | Validator |
| telemetry adapter missing | gameplay continues | n/a | instrumentation incomplete | Unity/QA |
| debug unavailable | gameplay/core telemetry unchanged | n/a | debug only | Debug |

---

# 73. Current Implementation Assessment

Supplied source/evidence was checked.

- Planning Implementation Spec defines `TEL-01..TEL-04`; those rows are **Not Started**.
- Planning API specifies `POST /telemetry/batch`, Host/service auth, `events[]`, response `accepted,rejected`; implementation source is not supplied.
- Planning data model defines the predecessor `TelemetryEvent` top-level fields.
- Supplied M1-026 Unity project snapshot contains the frozen telemetry schema document but no project runtime `TelemetryEventFactory`, `TelemetryBuffer`, sender, adapters, or backend telemetry source. Photon library classes containing "Event" are not ECHO telemetry implementation.

Therefore:

```text
CURRENT IMPLEMENTATION:
NOT EVIDENCED FROM SUPPLIED SOURCE
```

| Module | Evidence | Action | Target | Risk |
|---|---|---|---|---|
| schema v1.0 doc | evidenced | KEEP legacy | v1.0 validator | Low |
| v1.1 runtime event model | not evidenced | ADD | immutable event | Medium |
| sequence allocator | not evidenced | ADD | order | High |
| factory/provenance | not evidenced | ADD | identity/snapshot | High |
| gameplay adapters | not evidenced | ADD | one-fact emission | High |
| buffer/sender/ack | planning only | ADD | transport | High |
| backend batch endpoint | planning only | ADD | validation/ack | High |
| idempotency store | not evidenced | ADD | conflict/dedup | High |
| quality evaluator | not evidenced | ADD | completeness | Medium |
| aggregation/Profile | contracts only | separate milestone | downstream | High |

No implementation class is fabricated.

---

# 74. Implementation Order

```text
1  audit legacy schema/source
2  implement schema registry: frozen 1.0 + new 1.1
3  implement pure v1.1 validator/common context
4  implement TelemetrySequenceAllocator + SourceOccurrenceKey/EventFactory
5  implement Match/Phase/Player/Objective/Noise adapters
6  implement NOISE_EMITTED noiseEventId correlation
7  implement optional Stalker/Listener RESEARCH_CAPTURE
8  implement optional Warden RESEARCH_CAPTURE
9  implement bounded buffer + overflow quality state
10 implement batch sender + ack processor
11 implement immutable retry/backoff
12 implement backend version dispatcher/validators
13 implement transactional ID + sequence idempotency checks
14 implement immutable raw repository
15 implement sequence/lifecycle/provenance quality evaluator
16 validate Profile raw-source coverage without redefining formulas
17 implement experiment provenance
18 run pure/Unity/backend/Fusion tests
19 profile serialization/buffer/batch/backend cost
20 tune capacities/backoff/flush and freeze evidence package
```

Identity/order/version must precede emitters.

---

# 75. Configuration Matrix

| Config | Owner | Purpose | Runtime mutable? | AED mutable? | Status |
|---|---|---|---:|---:|---|
| producer `schemaVersion` | Schema Registry | wire contract | No | No | CURRENT v1.1 = `"1.1"` |
| backend `supportedSchemaVersions` | Backend | migration | deploy config | No | target `"1.0","1.1"` |
| BufferCapacity | transport | bound memory | config | No | TUNING TBD |
| BatchSize | transport | batch size | config | No | TUNING TBD |
| FlushInterval | transport | latency/request | config | No | TUNING TBD |
| RetryBackoff | transport | retry | config | No | TUNING TBD |
| MaxRetry/retry age | transport | bound retry | config | No | TUNING TBD |
| PayloadSizeLimit | validator | payload bound | deploy config | No | TUNING/BINDING TBD |
| researchCaptureEnabled | QA/Research | research events | fixed per match/run | No | FIXED RUN CONFIG |
| diagnostic capture | Dev | local traces | dev config | No | BINDING TBD |
| buildVersion | build manifest | provenance | No | No | BINDING |
| mapContentVersion | content manifest | provenance | No | No | BINDING |
| experimentProtocolVersion | Research | protocol | fixed run | No | conditional |
| scenarioConfigVersion | AppliedScenarioConfig | event-time config | valid boundaries only | indirect only through approved ScenarioConfig | PROJECT BASELINE |
| policyVersion | AppliedScenarioConfig | fairness contract provenance | valid config only | No direct telemetry authority | PROJECT BASELINE |
| configSource | AppliedScenarioConfig | FIXED/ADAPTIVE | valid config only | No direct telemetry authority | PROJECT BASELINE |

No transport setting is AED gameplay authority.

---

# 76. Telemetry Hard Invariants

1. Telemetry observes authoritative gameplay and never commands it.
2. RuntimeNoiseEvent and `NOISE_EMITTED` remain separate.
3. `id` identifies one globally unique logical telemetry event.
4. Retry never creates a new ID.
5. Retry never mutates occurrence/order/provenance/payload.
6. Same ID + different semantic content is conflict, not overwrite.
7. `(matchId,eventSequence)` maps to one logical event.
8. Backend arrival order is not gameplay order.
9. One Host match-wide sequence allocator owns ordering.
10. `authorityTick` is provenance; null only when no meaningful tick exists.
11. `ts` is authoritative UTC occurrence time.
12. One authoritative fact has one telemetry owner.
13. Proxy clients do not duplicate authoritative events.
14. Resimulation/presentation replay does not create new logical telemetry.
15. Late join does not replay historical telemetry.
16. Reserved events are rejected under 1.1.
17. Every emittable event has explicit userId/payload/reason rules.
18. `scenarioConfigVersion` is occurrence-time applied config.
19. `policyVersion` is required for FIXED and ADAPTIVE.
20. `configSource` is distinct from assigned experiment condition.
21. No per-frame Player Transform telemetry.
22. Position is event-time snapshot only.
23. Telemetry never returns hidden AI information into runtime Monster AI.
24. Debug traces are not automatically backend events.
25. Warden route data is not hidden in unrelated MONSTER events.
26. Warden candidate reject spam stays diagnostic.
27. Missing telemetry is not zero.
28. Incompleteness is observable.
29. Buffer overflow cannot silently claim completeness.
30. A consumed missing sequence is never reused.
31. At-least-once delivery can duplicate transport; storage is idempotent.
32. Raw accepted events are immutable.
33. schemaVersion is preserved per event.
34. v1.0 raw events remain v1.0.
35. v1.1 is never silently downgraded.
36. Experiment provenance cannot change gameplay assignment.
37. Telemetry does not implement Profile/AED formulas.
38. `PLAYER_DOWNED` is Life-State-owned.
39. `MONSTER_ATTACK_RESOLVED` never invokes damage.
40. Listener research events use userId null.
41. Listener correlation does not change Listener legal-information boundary.
42. Warden research uses action/door/footprint identity, not graph dumps.
43. Research-capture enablement does not alter gameplay.
44. A RESEARCH_CAPTURE event is created only when `researchCaptureEnabled = true`.
45. `MATCH_STARTED` is sequence 1.
46. accepted `MATCH_ENDED` is final sequence boundary for clean stream.
47. No match TelemetryEvent is created after terminal occurrence.
48. Duplicate delivery never double-counts metrics.
49. Data-quality diagnostics never command gameplay.
50. Every strict event-specific wire enum has one canonical registry source; JSON examples never define or extend an enum.
51. Unknown/unregistered strict event-specific enum tokens are permanently rejected and are never silently mapped to a fallback token.
52. `MONSTER_INVESTIGATE_STARTED.selectionReason` contains only reasons that actually commit a NEW Listener InvestigationEpisode.
53. `RELATED_NOISE_MERGED`, `CURRENT_INVESTIGATION_RETAINED`, `CORROBORATES_VISIBLE_TARGET`, and `NO_ELIGIBLE_NOISE` cannot be serialized as `MONSTER_INVESTIGATE_STARTED.selectionReason`.
54. `PENDING_OBSERVATION_SELECTED` is legal only when an unexpired legally heard pending observation commits a new InvestigationEpisode.
55. `WARDEN_ROUTE_ACTION_RELEASED.failSafeReason` is required only for `FAIL_SAFE`, forbidden for `EXPIRED`, and limited to the six canonical invalid-active-lock reasons in §55.1.
56. `EXTERNAL_UNSAFE_STATE_AFTER_WARDEN_RELEASE` remains local/debug diagnostic evidence and does not enter `InvalidLockRate_v1.0`.
57. `InvalidLockRate_v1.0` denominator contains only successfully applied Warden actions; rejected candidates and cancelled telegraphs never enter it.
58. `WARDEN_ROUTE_SAFETY_CHECKED` persistent metric evidence is limited to `POST_APPLY` and `ACTIVE_LOCK_REVALIDATION`; candidate prechecks stay diagnostic.
59. `MONSTER_ATTACK_RESOLVED` occurs at the authoritative resolution point owned by the specific Monster detailed design; telemetry does not impose Stalker attack timing on Listener.
60. The Stalker Hit Moment remains Stalker-owned; Listener attack timing and resolution remain Listener-owned.
61. `MONSTER_ATTACK_RESOLVED` never creates, implies, or owns `PLAYER_DOWNED`; authoritative Player Life-State owns Down transition telemetry.
62. One Monster AttackEpisode produces at most one logical `MONSTER_ATTACK_RESOLVED` for its stable resolution identity.

---

# 77. Definition of Done

| # | Question | v1.1 answer |
|---:|---|---|
| 1 | Serialized schema version? | `"1.1"` |
| 2 | Relation to document revision? | doc v1.1 introduces wire 1.1; legacy wire 1.0 frozen |
| 3 | `id` meaning? | globally unique logical event |
| 4 | uniqueness scope? | global telemetry namespace |
| 5 | retry ID? | same |
| 6 | same ID/different payload? | conflict/quarantine |
| 7 | `ts`? | authoritative UTC occurrence time |
| 8 | authoritative ordering? | matchId + eventSequence |
| 9 | same tick? | eventSequence |
| 10 | arrival order trusted? | No |
| 11 | sequence owner? | Host allocator |
| 12 | resimulation? | no proxy emit; same occurrence reuses event |
| 13 | late join? | no replay |
| 14 | build/config provenance? | MATCH_STARTED + per-event config context |
| 15 | current ScenarioConfig? | required on every event |
| 16 | experiment condition? | conditional MATCH_STARTED condition/protocol; distinct from configSource |
| 17 | catalog statuses? | §26 |
| 18 | newly emittable? | selected research capture events |
| 19 | reserved? | §26 |
| 20 | Monster events? | investigate start/resolved, attack resolved, search ended as specified |
| 21 | Stalker attack idempotency? | monsterId + AttackEpisodeId + RESOLVED |
| 22 | Listener latency? | investigate-start ts - heardAt |
| 23 | FalseInvestigation? | investigation-resolved outcomes |
| 24 | Warden persistent research events? | telegraph/applied/safety/released |
| 25 | Warden RoutePressure? | applied routePressure + pre/post mean costs |
| 26 | AI debug-only facts? | high-frequency candidates/paths/scores/reject spam |
| 27 | userId? | explicit per §56 |
| 28 | positions? | §38 only |
| 29 | per-frame Transform? | forbidden |
| 30 | Runtime Noise vs telemetry? | separate |
| 31 | batch? | immutable events[] |
| 32 | partial ack? | per-event |
| 33 | retry? | transient/unack only, same event |
| 34 | permanent reject? | stop retry + quality evidence |
| 35 | delivery? | at-least-once attempt + idempotent storage |
| 36 | buffer owner? | Host telemetry transport |
| 37 | overflow? | no eviction; failed new enqueue; sequence not reused; incomplete |
| 38 | match end? | final event + best-effort flush |
| 39 | backend validation? | strict §48 |
| 40 | unknown schema/event? | reject |
| 41 | v1.0 to new backend? | frozen v1.0 path |
| 42 | v1.1 to old backend? | reject unsupported |
| 43 | raw storage mutable? | No |
| 44 | one-fact owner? | §52–53 |
| 45 | reason governance? | controlled UPPER_SNAKE_CASE/null |
| 46 | Profile formulas here? | No |
| 47 | deferred Profile fields synthesized? | No |
| 48 | completeness? | contiguous sequence + valid lifecycle/provenance |
| 49 | missing data zero? | No |
| 50 | research capture disabled? | absence does not invalidate normal production stream |
| 51 | Fixed-vs-Adaptive ready? | No |
| 52 | tests? | TEL-E/P/I/N |
| 53 | implementation complete? | Not evidenced |
| 54 | implementation order? | §74 |
| 55 | Canonical source for strict payload enums? | §55.1; one wire registry source per field; examples are non-normative |
| 56 | Unknown strict event enum? | permanent reject; no silent fallback |
| 57 | Listener reasons that may start a new InvestigationEpisode? | exactly the five start-capable `selectionReason` values in §55.1 |
| 58 | `PENDING_OBSERVATION_SELECTED` persistable? | Yes, only for a legal pending-origin **new** episode |
| 59 | `RELATED_NOISE_MERGED` persistable as INVESTIGATE_STARTED? | No |
| 60 | `CORROBORATES_VISIBLE_TARGET` persistable as INVESTIGATE_STARTED? | No |
| 61 | Warden `FAIL_SAFE` release reasons? | exactly the six canonical Warden-owned invalid-active-lock values in §55.1 |
| 62 | `InvalidLockRate_v1.0` numerator? | unique applied actions terminally released `FAIL_SAFE` with one of those six reasons |
| 63 | normal Warden expiry invalid lock? | No; denominator only |
| 64 | cancelled telegraph an applied lock? | No; neither applied denominator nor numerator |
| 65 | `ExternalUnsafeStateAfterWardenRelease`? | local/debug diagnostic after overlay removal; not release failSafeReason and not numerator evidence |
| 66 | What causes Stalker `MONSTER_ATTACK_RESOLVED`? | Stalker-owned authoritative AttackEpisode resolution point; currently its authoritative Hit Moment resolution |
| 67 | What causes Listener `MONSTER_ATTACK_RESOLVED`? | Listener-owned authoritative AttackEpisode resolution point from Listener detailed design |
| 68 | Does telemetry define Listener Hit Moment timing? | No |
| 69 | Does `MONSTER_ATTACK_RESOLVED` apply damage? | No |
| 70 | Who owns `PLAYER_DOWNED`? | authoritative Player Life-State transition only |
| 71 | Can one AttackEpisode create duplicate research events? | No; stable `(matchId, monsterId, attackEpisodeId, RESOLVED)` idempotency |
| 72 | Wire version after this correction? | remains `"1.1"` |
| 73 | Legacy wire `"1.0"`? | unchanged frozen compatibility path |
| 74 | Architecture escalation? | No |

All P0 identity/order/authority/versioning/event-contract questions are resolved.

---

# 78. Open Tuning / Implementation Bindings

## 78.1 TUNING TBD

BufferCapacity, BatchSize, FlushInterval, retry backoff, MaxRetry/retry age, PayloadSizeLimit, transport timeouts, profiler budgets, later research acceptance thresholds.

## 78.2 IMPLEMENTATION BINDING TBD

- GUID/ULID/collision-safe ID representation;
- SourceOccurrenceKey representation where gameplay has no episode ID;
- bounded producer duplicate-occurrence retention;
- UTC clock provider;
- sequence integer width;
- exact C# immutable type/serializer;
- HTTP framework/client;
- ack DTO names;
- database/index/transaction technology;
- semantic fingerprint algorithm;
- retry scheduler/local quarantine;
- build/map content version providers;
- experiment/test-run providers;
- Warden routeFootprintIdentity and safetyCheckId representations.

## 78.3 Not allowed as TBD

Host authority, ID stability, one match-wide sequence domain, ts meaning, retry immutability, backend idempotency, identity conflict behavior, runtime-noise separation, wire `"1.1"`, active payload/userId rules, no per-frame position, missing != zero, one-fact owner, reserved-event rejection, canonical strict payload-enum domains/registry ownership, unknown-enum rejection, Listener INVESTIGATE_STARTED start-reason subset, Warden `releaseReason/failSafeReason` relation, `InvalidLockRate_v1.0` numerator/denominator semantics, generic Monster attack occurrence binding, or `PLAYER_DOWNED` Life-State ownership.

---

# 79. Architecture Escalations

```text
ARCHITECTURE ESCALATION REQUIRED: NO
```

No source requires changing:

```text
Gameplay → Telemetry → Aggregation → Profile → AED
RuntimeNoiseEvent != TelemetryEvent
Host-authoritative Monster AI
Telemetry one-way analytical boundary
```

v1.1 remains inside the baselined architecture.

---

# 80. References

## 80.1 Project sources

1. `AI_Architecture_v1.1.md`.
2. `Telemetry_Event_Schema_v0_FINAL.md`.
3. `ECHO PROTO.docx` / `ECHO PROTO(1).docx`.
4. `01_ECHO_PROTOCOL_Project_Scope_REVISED.docx`.
5. `02_ECHO_PROTOCOL_System_Architecture_REVISED.docx`.
6. `03_ECHO_PROTOCOL_Implementation_Spec_REVISED.xlsx`.
7. `KLTN.docx` — Research Facility Map Flow / Objective Layout.
8. `KLTN (1).docx` — multiplayer organization/synchronization baseline.
9. `Stalker_AI_Design_v1.1.md`.
10. `Listener_AI_Design_v1.0.md`.
11. `Warden_AI_Design_v1.0.md`.
12. `M1-014_Player_Team_Profile_Fields_Formulas_v0_FINAL.md`.
13. `M1-015_ScenarioConfig_AED_Fairness_Policy_v0_FINAL.md`.
14. `M1-020_Test_Strategy_Fixed_vs_Adaptive_Experiment_v0_FINAL.md`.
15. supplied M1-026 Unity snapshot / implementation evidence.

## 80.2 Official external references

16. Photon Fusion 2 — State/Input Authority:  
    https://doc.photonengine.com/fusion/v2/manual/playerref

17. Photon Fusion 2 — Networked Properties:  
    https://doc.photonengine.com/fusion/v2/manual/data-transfer/networked-properties

18. Photon Fusion 2 — RPC transient/late-join semantics:  
    https://doc.photonengine.com/fusion/v2/manual/data-transfer/rpcs

19. Photon Fusion 2 — Network simulation / `FixedUpdateNetwork()`:  
    https://doc.photonengine.com/fusion/v2/concepts-and-patterns/network-simulation-loop

20. Photon Fusion 2 — Data Transfer:  
    https://doc.photonengine.com/fusion/v2/manual/data-transfer/data-transfer

21. Unity Test Framework:  
    https://docs.unity3d.com/6000.0/Documentation/Manual/com.unity.test-framework.html

22. Unity Profiler:  
    https://docs.unity3d.com/Manual/Profiler.html

---

# Telemetry Detailed-Contract Correction Report

| ID | Issue | Resolution | Status |
|---|---|---|---|
| `TEL-DD-01` | Canonical event-specific payload enum registry / Listener `selectionReason` alignment | Added §55.1 as the canonical strict wire-enum registry, froze UPPER_SNAKE_CASE serialization and unknown-enum rejection, and restricted `MONSTER_INVESTIGATE_STARTED.selectionReason` to start-capable Listener reasons only. | RESOLVED |
| `TEL-DD-02` | Warden fail-safe mapping / `InvalidLockRate_v1.0` | Froze `EXPIRED` / `FAIL_SAFE`, six valid fail-safe release reasons, excluded `ExternalUnsafeStateAfterWardenRelease`, and defined exact unique applied-action numerator/denominator semantics plus safety-check eligibility. | RESOLVED |
| `TEL-DD-03` | Generic `MONSTER_ATTACK_RESOLVED` occurrence semantics | Replaced universal Hit Moment wording with generic Monster AttackEpisode resolution; Stalker and Listener bind to their own detailed-design resolution points; `PLAYER_DOWNED` remains Life-State-owned. | RESOLVED |

No gameplay, Monster detailed design, wire version, Host authority, event-sequence domain, Profile/AED formula, or experiment-readiness rule is changed by this correction pass.

---

# Telemetry Contract Validation

```text
Architecture boundary preserved: YES
Predecessor v1.0 contract accounted for: YES
Wire schema version explicitly resolved: YES — "1.1"
Common event contract complete: YES
Logical event identity unambiguous: YES
Retry preserves logical identity: YES
Same-ID conflict semantics complete: YES
Timestamp semantics complete: YES
Authoritative ordering reconstructable: YES
Same-tick ordering deterministic: YES
Arrival-order ambiguity removed: YES
Schema evolution/backward compatibility complete: YES
Event status catalog complete: YES
userId ownership complete: YES
reasonCode governance complete: YES
Stalker telemetry mapping complete: YES
Listener telemetry mapping complete: YES
Warden telemetry mapping complete: YES
Production/research/debug boundary complete: YES
Batch acknowledgement semantics complete: YES
Retry classification complete: YES
Buffer failure semantics complete: YES
Backend validation complete: YES
Idempotent storage semantics complete: YES
Data completeness semantics complete: YES
Profile input coverage audited: YES
Experiment provenance auditable: YES
Fusion authority preserved: YES
Late join/resimulation duplicate prevention complete: YES
Test plan complete: YES
Implementation plan complete: YES

TEL-DD-01 canonical payload enum domains resolved: YES
Listener INVESTIGATE_STARTED selectionReason subset exact: YES
Disposition-only Listener reasons rejected as STARTED: YES

TEL-DD-02 Warden release/fail-safe mapping exact: YES
InvalidLockRate numerator exact: YES
ExternalUnsafeStateAfterWardenRelease handled correctly: YES

TEL-DD-03 generic Monster attack occurrence exact: YES
Stalker Hit Moment remains Stalker-owned: YES
Listener attack timing remains Listener-owned: YES
PLAYER_DOWNED remains Life-State-owned: YES

Wire schemaVersion remains "1.1": YES
Legacy "1.0" compatibility preserved: YES
Host authority preserved: YES
Telemetry gameplay authority remains NONE: YES
Architecture escalation required: NO
```

Wire-version resolution:

```text
Document Revision:
v1.1

Serialized wire schemaVersion for new v1.1 events:
"1.1"

Frozen legacy serialized schemaVersion:
"1.0"
```

---

# Final Consistency Audit

```text
Telemetry can command Monster AI: NO
Telemetry can apply damage: NO
Telemetry can choose Warden DoorId: NO
Listener can hear NOISE_EMITTED telemetry: NO
Proxy can duplicate authoritative gameplay telemetry: NO
Retry can generate a new logical event ID: NO
Retry can change occurrence timestamp: NO
Backend arrival order is treated as gameplay order: NO
Same event ID can overwrite a different payload: NO
Unsupported schema version can be silently accepted: NO
Reserved event can be emitted as production: NO
Active event can lack defined userId semantics: NO
Active event can lack payload contract: NO
Stalker duplicate attack callback can produce duplicate outcome telemetry: NO
Late join can recreate historical Monster telemetry: NO
Fusion resimulation can create duplicate logical production events: NO
Warden route data is smuggled through unrelated MONSTER events: NO
Per-frame Player transform telemetry is allowed: NO
Missing telemetry is converted to zero: NO
Telemetry buffer overflow can remain invisible: NO
v1.0 stored events are rewritten as v1.1: NO
Experiment condition metadata changes gameplay condition: NO
Telemetry directly computes AED gameplay decisions: NO
Deferred Profile fields are fabricated to make Adaptive ready: NO

Runtime NoiseEvent can be replaced by NOISE_EMITTED: NO
Same match eventSequence can identify two different logical events: NO
PLAYER_DOWNED can be independently emitted by both Monster and Life-State owners: NO
WARDEN_ATTACK is an active v1.1 PLAYER_DOWNED reason: NO
Listener research telemetry changes Listener legal-information boundary: NO
Warden candidate-rejection spam is ACTIVE_PRODUCTION telemetry: NO
RESEARCH_CAPTURE enablement changes gameplay: NO
Telemetry v1.1 alone makes the live Fixed-vs-Adaptive experiment READY: NO

Can a Listener disposition-only reason be serialized as a new InvestigationEpisode start: NO
Can an unknown event-specific enum be silently accepted: NO
Can JSON example values substitute for canonical enum contracts: NO
Can ExternalUnsafeStateAfterWardenRelease automatically count as an invalid Warden lock: NO
Can a rejected/cancelled Warden candidate enter InvalidLockRate denominator: NO
Can normal Warden expiry enter InvalidLockRate numerator: NO
Does MONSTER_ATTACK_RESOLVED force Listener to implement Stalker Hit Moment semantics: NO
Can MONSTER_ATTACK_RESOLVED directly create PLAYER_DOWNED: NO
Can PLAYER_DOWNED have both Monster adapter and Life-State adapter as authoritative owners: NO
Does this correction require wire schemaVersion "1.2": NO
Does this correction require Architecture v1.2: NO
```

All expected audit answers are `NO`.

No P0 identity, ordering, authority, wire-version, payload, idempotency, compatibility, research-evidence, or completeness ambiguity remains.

```text
Document Revision: v1.1
Serialized Wire Schema Version: "1.1"
Recommended Status: BASELINED v1.1
Architecture Escalation Required: NO
```

**End of `Telemetry_Contract_v1.1.md`**
