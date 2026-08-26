# ECHO PROTOCOL — Player / Team Profile Contract v1.1

**Canonical document:** `Player_Team_Profile_Contract_v1.1.md`  
**Project:** ECHO PROTOCOL — Co-op Survival Horror Multiplayer  
**Document Revision:** `v1.1`  
**Parent Architecture:** `AI_Architecture_v1.1.md` — **BASELINED v1.1**  
**Current Telemetry Contract:** `Telemetry_Contract_v1.1.md` — **BASELINED v1.1**, wire `schemaVersion = "1.1"`  
**Historical predecessor:** `M1-014_Player_Team_Profile_Fields_Formulas_v0_FINAL.md`  
**Downstream predecessor:** `M1-015_ScenarioConfig_AED_Fairness_Policy_v0_FINAL.md`  
**Experiment dependency:** `M1-020_Test_Strategy_Fixed_vs_Adaptive_Experiment_v0_FINAL.md`  
**Recommended Status:** **BASELINED v1.1**  
**Architecture Escalation Required:** **NO**  
**Live Adaptive Execution:** **NOT READY**

> This is a processed-data contract. It specifies deterministic aggregation, profile semantics, lifecycle, versioning, idempotency, and the producer side of the decision-scoped adaptive input boundary. It does not claim that the backend/profile implementation exists, that tests pass, or that the Fixed-vs-Adaptive experiment is ready.

---

# 1. Document Control

| Field | Contract |
|---|---|
| Role | Profile / processed-data implementation contract |
| Current raw source | Validated Telemetry wire `"1.1"` |
| Legacy raw source | Frozen Telemetry wire `"1.0"` through its own legacy validator only |
| Persistent player identity | `userId` |
| Team identity | **match-scoped only**, `teamKey = matchId` |
| Persistent party/team identity | **FORBIDDEN unless separately approved upstream** |
| Player score scale | `[0,100]` for non-null ACTIVE observed scores |
| Current Player ACTIVE dimensions | `survival`, `noise` |
| Current Player DEFERRED dimensions | `objective`, `teamwork`, `exploration`, `navigation`, `toolUsage`, `risk`, `revive` |
| TeamProfile ACTIVE field | `objectiveTime` |
| TeamProfile DEFERRED fields | `splitTime`, `avgDistance`, `reviveSuccess`, `resourceEfficiency`, `communication`, `wipeRecovery` |
| TeamPerformance topology | ObjectiveSpeed + Survival + Teamwork + ResourceEfficiency |
| TeamPerformance current status | **INCOMPLETE / null** because Teamwork and ResourceEfficiency remain DEFERRED |
| Adaptive boundary | immutable decision-scoped `AdaptiveInputSnapshot` |
| Gameplay authority | **NONE** |
| Runtime Monster authority | unchanged; Profile never commands Stalker/Listener/Warden |
| Current Profile formula semantic identity | logical `PROFILE_FORMULA_V1_1`; exact persisted token encoding is an implementation binding |
| Persistent apply identity | dimension-scoped `ProfileDimensionApplyKey` |
| Cross-player score comparison | exact per-dimension semantic-key equality required before direct roster aggregation |

## 1.1 Surgical correction record

This v1.1 contract completed a surgical detailed-contract correction pass without changing Telemetry wire `"1.1"`, TeamProfile identity, TeamPerformance topology, ACTIVE/DEFERRED dimensions, AED authority, or Adaptive readiness.

| Issue | Resolution | Status |
|---|---|---|
| `PRO-DD-01` | persistent score application is dimension-scoped; later same-match dimension completion is legal and obeys canonical per-dimension replay ordering | RESOLVED |
| `PRO-DD-02` | cross-player aggregation requires one exact `ProfileDimensionComparisonKey` per aggregated ACTIVE dimension; incompatible semantics invalidate the snapshot rather than being averaged or silently dropped | RESOLVED |
| `PRO-DD-03` | Profile v1.1 first-sample behavior has explicit semantic identity `PROFILE_FORMULA_V1_1`, distinct from predecessor first-sample blending semantics; old lineages require migration/rebuild or a new cold-start lineage | RESOLVED |
| `PRO-DD-04` | previously contributing observations become non-contributing when their source match/metric later becomes invalid; correction is idempotent logical retraction plus affected-dimension canonical replay | RESOLVED |

Classification labels used below:

`PROFILE v1.1 DECISION`, `KEEP`, `MODIFY`, `SUPERSEDE`, `DEFER`, `REMOVE`, `TUNING TBD`, `IMPLEMENTATION BINDING TBD`, `TELEMETRY CONTRACT REVISION REQUIRED`, `AED v1.1 REVISION REQUIRED`.

---

# 2. Purpose

This contract answers, without implementation guesswork:

- how validated Telemetry `"1.1"` becomes `MatchTelemetry`;
- what `TelemetryStreamCompleteness` means to Profile;
- when a match may update persistent Player profiles;
- when one metric is `AVAILABLE`, `UNAVAILABLE`, or `INVALID`;
- which Player and Team constructs are measurable now;
- how MatchScore, cold start, EMA, sampleCount, idempotency, and ordering work;
- how late/out-of-order matches are handled deterministically;
- how a previously contributing observation is retracted and replayed when its source match/metric later becomes invalid;
- how formula/config changes create compatible or incompatible profile lineages;
- what TeamProfile means without inventing a persistent team identity;
- why Teamwork and ResourceEfficiency remain DEFERRED;
- how Profile resolves the Profile-side portion of M1-020 SC-03;
- what immutable processed evidence can be handed to a future AED v1.1.

Correctness and reproducibility take priority over making Adaptive appear ready.

---

# 3. Scope / Non-Goals

## 3.1 In scope

- Telemetry `"1.1"` → MatchTelemetry binding;
- legacy `"1.0"` compatibility boundary;
- match/profile integrity gate;
- `MatchProfileEligibilityResult`;
- `MetricAvailability`;
- Player dimension audit;
- Player MatchScore formulas;
- normalization;
- cold start / first observed sample;
- EMA;
- sampleCount;
- update idempotency;
- cross-match ordering;
- late-match replay;
- formula/config compatibility;
- PlayerAIProfile logical schema;
- match-scoped TeamProfile;
- TeamPerformance validity;
- decision-scoped PlayerProfileSnapshot, RosterProfileSummary, AdaptiveInputSnapshot;
- PRE_MATCH / phase-boundary / FINAL_HUNT_SETUP evidence;
- backend component boundaries;
- concurrency semantics;
- tests, failure handling, observability, migration.

## 3.2 Non-goals

This document does **not**:

- command gameplay or Monster AI;
- modify ScenarioConfig;
- redesign M1-015 AED policy;
- invent Teamwork or ResourceEfficiency formulas;
- invent persistent party/team identity;
- activate research-capture Monster/Warden events as Player Profile inputs;
- add raw telemetry fields;
- add Player transform sampling;
- add ML, clustering, labels, recommendation models, or GenAI-generated scores;
- choose final numerical alpha/normalization/weight values;
- choose database vendor/table/index technology.

---

# 4. Source Priority / Governance

```text
1. AI_Architecture_v1.1.md
2. Telemetry_Contract_v1.1.md
3. current approved gameplay/system contracts
4. Player_Team_Profile_Contract_v1.1.md current draft under this correction pass
5. M1-014_Player_Team_Profile_Fields_Formulas_v0_FINAL.md predecessor
6. M1-015_ScenarioConfig_AED_Fairness_Policy_v0_FINAL.md
7. M1-020_Test_Strategy_Fixed_vs_Adaptive_Experiment_v0_FINAL.md
8. current implementation evidence if supplied
```

Rules:

```text
current architecture/telemetry
>
historical M1 assumptions

processed-data correctness
>
convenient implementation

reproducibility
>
backend arrival order

approved gameplay semantic
>
analytics convenience

measurable evidence
>
desired Adaptive readiness
```

Research methodology can justify concepts such as construct validity, deterministic preprocessing, and controlled adaptation. It cannot create ECHO PROTOCOL gameplay facts.

---

# 5. M1-014 v0 → Profile v1.1 Validity Review

| v0 decision | v1.1 classification | Reason |
|---|---|---|
| 9 Player dimensions retained | KEEP | stable public processed-data vocabulary; no evidence requires removal |
| `survival`, `noise` ACTIVE | KEEP | Telemetry v1.1 still provides sufficient authoritative source facts |
| seven other Player dimensions DEFERRED | KEEP / DEFER | new event names do not establish construct/formula validity |
| ACTIVE cold start `50/COLD_START/0` | MODIFY | retain neutral representation but remove pseudo-evidence from first observed update |
| DEFERRED = null | KEEP | prevents synthetic neutral evidence |
| EMA after observations exist | KEEP | simple, explainable, versionable |
| first valid MatchScore blended with initialization 50 | **SUPERSEDE** | initialization must not act as an unobserved pseudo-match; v1.1 therefore uses a distinct Profile formula semantic identity |
| sampleCount = number of valid applied dimension observations | MODIFY | current v1.1 sampleCount counts currently CONTRIBUTING observations; processed-but-retracted observations remain auditable but excluded |
| `MatchProfileEligibility = ELIGIBLE/INELIGIBLE` only | MODIFY | v1.1 needs explicit `PENDING` while match/finalization unresolved plus controlled reasons |
| `MetricAvailability = AVAILABLE/UNAVAILABLE` | MODIFY | add `INVALID` for present-but-contradictory/invalid metric evidence |
| survival `ESCAPED=100`, `ELIMINATED=0` | KEEP | terminal construct remains direct and attributable |
| Down Count automatically changes survival | REJECT / REMOVE | no source-backed formula justification |
| ProfileNoiseFilter | KEEP | configurable/versioned and not all noise is bad |
| TeamProfile match-scoped | KEEP | architecture forbids invented persistent team identity |
| `objectiveTime` | KEEP | Telemetry v1.1 retains canonical phase boundary timestamps |
| six TeamProfile deferred fields | KEEP / DEFER | required source/construct remains insufficient |
| TeamPerformance four-component topology | KEEP | changing it to two measurable components would change construct mainly to bypass current missingness |
| Teamwork | DEFER | action events do not measure teamwork quality |
| ResourceEfficiency | DEFER | tool-use count does not measure value/waste/opportunity |
| missing component renormalization | REJECT | changes formula semantics and hides missing constructs |
| formula/config version metadata | EXPAND | required for replay, lineage, snapshots, experiment reproducibility |
| Profile → AED loose refs | SUPERSEDE on Profile producer side | Profile v1.1 produces immutable decision-scoped snapshot; AED v1.1 must adopt it |
| persistent team EMA | REJECT | no stable team identity contract |

**v1.1 major semantic correction:** cold-start value `50` remains a neutral **representation**, but it is no longer used as an observed prior in the first valid profile update.

This is a persistent-profile formula semantic change, not a document-only editorial change:

```text
Profile v1.1 semantic identity
=
PROFILE_FORMULA_V1_1

PROFILE_FORMULA_V1_1
!=
any predecessor formula semantic identity that blends COLD_START 50
into the first observed MatchScore
```

The exact persisted token encoding is `IMPLEMENTATION BINDING TBD`; the semantic distinction is not TBD.

---

# 6. Architecture Boundary

Canonical one-way data flow:

```text
Authoritative Gameplay
→ TelemetryEvent
→ validated immutable raw storage
→ MatchTelemetry
→ Profile integrity / eligibility
→ MatchMetric<T>
→ PlayerMatchScore / TeamProfile
→ ordered idempotent PlayerAIProfile update
→ immutable PlayerProfileSnapshot
→ decision-scoped RosterProfileSummary
→ AdaptiveInputSnapshot
→ future AED v1.1 input gate
→ ScenarioConfig policy/validation
```

Forbidden reverse paths:

```text
Profile → Stalker FSM
Profile → Listener investigation
Profile → Warden route candidate
Profile → target selection
Profile → navigation destination
Profile → attack
Profile → damage
Profile → raw runtime hearing
```

`AdaptiveInputSnapshot` is an analytical/processed input package, not gameplay authority.

---

# 7. Telemetry v1.1 Migration

Current source is wire:

```text
schemaVersion = "1.1"
```

Legacy `"1.0"` remains historical and is never rewritten to `"1.1"`.

Historical wire `"1.0"` processing is supported as a **separate legacy compatibility path** because the predecessor Profile contract is a real project source.

```text
wire "1.0"
→ frozen v1.0 validation
→ frozen legacy aggregation semantics
→ legacy Profile/MatchScore result or migration source
```

Rules:

- no event is relabeled `"1.1"` after storage;
- legacy results are never silently inserted into a v1.1 profile lineage whose semantics differ;
- migration into a v1.1 lineage requires deterministic recomputation/replay under the target v1.1 formula/config rules when sufficient source evidence exists;
- if a required v1.1 semantic cannot be reconstructed from legacy source, the affected metric is UNAVAILABLE rather than fabricated.


A single finalized match used by Profile v1.1 must resolve to one coherent telemetry semantic path. Mixed wire semantics in one match are `UNSUPPORTED_SCHEMA_VERSION` / `FORMULA_VERSION_CONFLICT` unless an explicit future migration contract proves compatibility.

## 7.1 Telemetry → Profile Input Compatibility Matrix

| Profile input | Required event(s) | Current schema | Required fields | Join / order | Completeness / availability | Consumer |
|---|---|---|---|---|---|---|
| match provenance | `MATCH_STARTED` | `"1.1"` | matchId, `eventSequence=1`, teamSize, build/map/config provenance, `data.mapId` | matchId; sequence 1 | required to finalize Profile eligibility | all |
| match terminal | `MATCH_ENDED` | `"1.1"` | outcome, durationSeconds, survivorCount, terminal reason | matchId; terminal sequence N | required for final persistent match processing | eligibility, Team Survival |
| phase duration | `PHASE_STARTED` + `PHASE_COMPLETED` | `"1.1"` | context.phase, `ts`, sequence | `(matchId, phase identity)`; sequence validates lifecycle, `ts` measures duration | each required phase pair must be closed and valid | objectiveTime |
| Down Count raw metric | `PLAYER_DOWNED` | `"1.1"` | userId, authoritative Life-State occurrence | userId+matchId; sequence | positive events count only; absence requires source coverage before zero | raw metric only |
| Revive Count raw metric | `PLAYER_REVIVED` | `"1.1"` | revived userId, `reviverPlayerId` | matchId/userId | event proves successful revive only | raw metric; no revive-quality dimension |
| Player survival | `PLAYER_ESCAPED` / `PLAYER_ELIMINATED` | `"1.1"` | affected userId, valid terminal fact | one terminal outcome per user/match | exactly one valid terminal fact required | Player survival |
| Team survival | `MATCH_STARTED` + `MATCH_ENDED` | `"1.1"` | teamSize, survivorCount | matchId | both anchors valid | TeamPerformance Survival |
| tool-use fact | `TEAM_TOOL_USED` | `"1.1"` | userId, strict toolType | matchId/userId/sequence | event occurrence is sufficient for usage count only | raw usage evidence |
| help-ping fact | `HELP_PING_USED` | `"1.1"` | acting Downed userId | matchId/userId/sequence | event occurrence is sufficient for help-ping count only | raw communication evidence |
| Player noise | `NOISE_EMITTED` | `"1.1"` | userId, noiseEventId, strict noiseType, phase, loudness | `(matchId, noiseEventId)`; sequence | absence becomes zero only with consumer coverage AVAILABLE | Player noise |
| match duration | `MATCH_STARTED` + `MATCH_ENDED` | `"1.1"` | `ts` anchors / duration | matchId | both anchors valid | research/raw metric |

Monster/Listener/Warden `RESEARCH_CAPTURE` events are **not** Player Profile inputs in v1.1 merely because they exist.

## 7.2 Strict catalogs consumed by Profile

Profile v1.1 consumes Telemetry's canonical `NOISE_EMITTED.data.noiseType` domain:

```text
SPRINT
INTERACTION
CORE_CARRY
CORE_DROP
NOISE_MAKER
```

Profile does not invent extra NoiseType tokens.

`TEAM_TOOL_USED.data.toolType` remains a usage-fact domain:

```text
FIELD_SCANNER
NOISE_MAKER
FIRST_AID_KIT
DOOR_JAMMER
```

Tool presence does not activate `toolUsage`, Teamwork, or ResourceEfficiency scoring by itself.

---

# 8. Processed Data Pipeline

```text
ValidatedTelemetryReader
→ MatchTelemetryAggregator
→ TelemetryProfileIntegrityGate
→ ProfileEligibilityEvaluator
→ MatchMetric calculators
→ PlayerMatchScoreCalculator
→ PlayerProfileUpdatePlanner
→ ProfileSourceInvalidationDetector
→ ProfileContributionRetractionPlanner
→ dimension-scoped ordered/idempotent PlayerProfileUpdater / Replay
→ TeamProfileBuilder
→ TeamPerformanceCalculator
→ PlayerProfileSnapshotBuilder
→ RosterProfileSummaryBuilder
→ AdaptiveInputSnapshotBuilder
```

No single `ProfileManager` may own raw validation, formulas, persistence, snapshot construction, and AED policy.

Logical `MatchTelemetry` is a deterministic projection of **validated accepted events**, ordered by `(matchId,eventSequence)`. Backend arrival order is never gameplay order.

---

# 9. Telemetry Completeness → Profile Policy

Telemetry owns:

```text
COMPLETE
INCOMPLETE
INVALID
UNKNOWN
```

Profile v1.1 chooses **metric-scoped degradation for INCOMPLETE streams**.

## 9.1 Canonical mapping

| Telemetry state / condition | Profile eligibility result | Persistent update rule |
|---|---|---|
| `COMPLETE` + valid final match + supported provenance | `ELIGIBLE` | valid AVAILABLE observations may contribute; preserve existing valid contributions and process newly AVAILABLE independent dimensions under PRO-DD-01 |
| `INCOMPLETE` + valid start/end + no corruption + not aborted | `ELIGIBLE` with `STREAM_INCOMPLETE` reason | only independently provable AVAILABLE metrics may contribute; affected/absence-dependent metrics are UNAVAILABLE |
| `INVALID` | `INELIGIBLE` | no observation from the match may contribute; if earlier eligible processing already contributed, retract those observations and replay affected dimensions |
| `UNKNOWN` | `PENDING` | no new final persistent contribution; ordinary pre-final PENDING has no earlier final contribution to roll back |
| `MATCH_ABORTED` | `INELIGIBLE` | no observation may contribute; retract prior contributions if an earlier eligible interpretation had already contributed |
| missing `MATCH_STARTED` in known-ended match | `INELIGIBLE` | no final Profile result |
| missing `MATCH_ENDED` | `PENDING` while terminal unresolved; `INELIGIBLE` if known terminal evidence is permanently unavailable | no final update |
| identity conflict | `INELIGIBLE` | no contribution permitted; retract existing match contributions in affected lineage(s) |
| sequence conflict | `INELIGIBLE` | no contribution permitted; retract existing match contributions in affected lineage(s) |
| invalid/contradictory provenance | `INELIGIBLE` | no contribution permitted; retract existing match contributions in affected lineage(s) |
| unsupported schema/profile version | `INELIGIBLE` | no contribution permitted; retract existing contributions only where the reason invalidates the match for the current Profile lineage |
| buffer overflow / permanent loss | usually stream `INCOMPLETE` | metric-scoped availability; never assume missing event = zero |

## 9.2 Why INCOMPLETE is not automatically whole-match ineligible

Telemetry v1.1 explicitly separates full-stream completeness from consumer-specific evidence. A missing enabled research event may make the stream incomplete without affecting Player survival or another independent production metric.

Therefore:

```text
INCOMPLETE
≠
automatic INELIGIBLE
```

But an incomplete stream **cannot** justify absence-based zero unless the relevant source coverage is independently closed.

Example:

```text
known telemetry gap
+ no matching NOISE_EMITTED
→ noise penalty count is NOT proven zero
→ noise MetricAvailability = UNAVAILABLE
```

By contrast, an accepted, non-contradictory `PLAYER_ESCAPED` terminal fact may still make that Player's survival metric AVAILABLE if the authoritative terminal-state contract proves one terminal outcome and no integrity conflict exists.

## 9.3 Eligibility / availability transition matrix — PRO-DD-04

Current source eligibility controls whether a historical observation is allowed to participate in the **current derived** `PlayerAIProfile` state.

| Transition | Profile effect |
|---|---|
| `INCOMPLETE / ELIGIBLE → COMPLETE / ELIGIBLE` | retain existing valid contributions; process newly AVAILABLE independent metrics through PRO-DD-01; no unnecessary retraction |
| `INCOMPLETE / ELIGIBLE → INCOMPLETE / ELIGIBLE` with additional valid evidence | PRO-DD-01 late dimension completion / canonical ordering rules |
| `INCOMPLETE / ELIGIBLE → INVALID / INELIGIBLE` | retract every currently CONTRIBUTING Player dimension observation sourced from that match in the target lineage; replay every affected ACTIVE dimension |
| `COMPLETE / ELIGIBLE → INVALID / INELIGIBLE` if later integrity/provenance conflict is discovered | same global retraction/replay rule; accepted raw telemetry remains immutable |
| metric `AVAILABLE → INVALID` while match remains `ELIGIBLE` | retract that metric/dimension observation only; replay that ACTIVE dimension only |
| metric `UNAVAILABLE → AVAILABLE` | PRO-DD-01 apply/late-order replay; this is not retraction |

Telemetry v1.1 defines `INVALID` for identity/sequence corruption or contradictory provenance and permits late events to improve derived completeness. It does not define an authoritative repair protocol that converts a canonical corrupted `INVALID` interpretation back to valid. Therefore:

```text
INCOMPLETE gap fill → COMPLETE
= recoverable completeness improvement

canonical INVALID integrity/provenance interpretation
→ contributing observations are retracted
→ restoration is NOT automatic in Profile v1.1
→ requires an explicit future upstream authoritative correction/recovery contract
```

A temporary/live `PENDING` state is not used as a rollback trigger for an observation that could never have been finally applied while PENDING. An unexpected `ELIGIBLE → PENDING` regression after final contribution must stop/revalidate source state rather than invent an implicit rollback rule.

---

# 10. MatchProfileEligibility

Logical contract:

```text
MatchProfileEligibilityStatus
=
ELIGIBLE
| INELIGIBLE
| PENDING
```

```text
MatchProfileEligibilityResult
{
    status
    reasons[]
    telemetryCompleteness
    matchTerminalStatus
    provenanceStatus
    sourceSchemaVersion
}
```

Controlled reasons:

```text
STREAM_INCOMPLETE
MATCH_ABORTED
MATCH_NOT_FINALIZED
MISSING_MATCH_START
MISSING_MATCH_END
TELEMETRY_INVALID
IDENTITY_CONFLICT
SEQUENCE_CONFLICT
PROVENANCE_INVALID
UNSUPPORTED_SCHEMA_VERSION
UNSUPPORTED_PROFILE_VERSION
FORMULA_VERSION_CONFLICT
```

Rules:

- free-text reasons are not authoritative;
- `ELIGIBLE` means the match may contribute **only** metrics whose own availability is valid;
- `PENDING` creates no new final contribution; ordinary pre-final PENDING does not retract because no final contribution could have been committed from that state;
- `INELIGIBLE` means observations from the match may not participate in the current derived Profile state;
- if a match becomes `INELIGIBLE` after earlier valid contributions exist, those contributions must be logically retracted and affected dimensions replayed;
- `ELIGIBLE + STREAM_INCOMPLETE` does not imply every metric is usable.

## 10.1 Global source invalidation scope

```text
match becomes globally INELIGIBLE
→ for each profileLineageId in which that match currently contributes
   → mark every CONTRIBUTING Player dimension observation from that match RETRACTED
   → replay only the ACTIVE dimensions that actually lost a contribution
```

Examples of controlled reasons that can invalidate the whole match for a current Profile lineage include `IDENTITY_CONFLICT`, `SEQUENCE_CONFLICT`, `PROVENANCE_INVALID`, `UNSUPPORTED_SCHEMA_VERSION`, `UNSUPPORTED_PROFILE_VERSION`, and `FORMULA_VERSION_CONFLICT` **only when the reason's scope actually makes the match unusable for that lineage**.

Downstream AED snapshot unsuitability alone is not a Profile-source invalidation reason and does not retract Profile history.

---

# 11. MetricAvailability

v1.1 freezes:

```text
MetricAvailability
=
AVAILABLE
| UNAVAILABLE
| INVALID
```

```text
MatchMetric<T>
{
    value: T?
    availability
    reason
    finality
    sourceCoverage
}
```

Semantics:

- `AVAILABLE`: required source evidence and config are sufficient; `value` may legitimately be `0`.
- `UNAVAILABLE`: evidence is insufficient/not yet final; `value = null`.
- `INVALID`: relevant source/config exists but is contradictory or invalid; `value = null`.

`DEFERRED` is **not** a MetricAvailability value. It is a formula/construct status.

## 11.1 Canonical zero / null / status truth table

| Representation | Meaning | Observed evidence? | May enter numeric aggregation? |
|---|---|---:|---:|
| `0` with `AVAILABLE` | legitimate observed zero | Yes | Yes |
| `null` + `UNAVAILABLE` | required evidence cannot currently be established | No | No |
| `null` + `INVALID` | relevant evidence/config is contradictory or invalid | No valid evidence | No |
| `score=null, status=DEFERRED` | formula/construct intentionally inactive | No | No |
| `score=50, status=COLD_START, sampleCount=0` | neutral initialization only | No | No as observed evidence |
| `score=50, status=ACTIVE, sampleCount>0` | measured profile value happens to equal 50 | Yes | Yes |

No developer or AED consumer may infer semantic state from the number alone.

Recommended `MetricFinality` for decision-scoped current-match evidence:

```text
FINAL
PROVISIONAL
UNAVAILABLE
```

A final post-match Profile metric uses `FINAL`. Mid-match cumulative evidence may be `PROVISIONAL`.

## 11.2 Metric validity transition and contribution scope

`MetricAvailability` is the **current validity of the computed source metric**. It is distinct from an already-known observation's `ContributionStatus`.

```text
AVAILABLE final metric
→ may create/maintain one CONTRIBUTING observation when match is ELIGIBLE

AVAILABLE → INVALID
while match remains ELIGIBLE
→ preserve immutable historical MatchScore payload
→ mark that dimension observation RETRACTED
→ replay that dimension only

UNAVAILABLE → AVAILABLE
→ PRO-DD-01 path
```

An `INVALID` current metric result does not overwrite its historical previously computed score inside the observation audit record. The immutable observation records what was originally processed; `ContributionStatus` records whether it still participates in the current derived profile.

---

# 12. Player Dimension Validity Audit

| Dimension | Construct | Current source | Attribution | Formula evidence | Main validity risk | v1.1 |
|---|---|---|---|---|---|---|
| `survival` | terminal player survival outcome | `PLAYER_ESCAPED` / `PLAYER_ELIMINATED` | direct affected userId | exact binary score | contradictory/missing terminal fact | **ACTIVE** |
| `noise` | penalty-signal frequency under versioned filter | player `NOISE_EMITTED` | direct acting userId | exact higher-is-worse normalization | filter choice / missing-event bias | **ACTIVE** |
| `objective` | individual objective contribution quality | objective/team lifecycle events insufficient for attribution | weak | no exact formula | team event ≠ player contribution | **DEFERRED** |
| `teamwork` | individual cooperation quality | revive/tool/ping action facts | partial | no denominator/quality construct | frequency ≠ quality | **DEFERRED** |
| `exploration` | individual exploration quality | no approved player sampling/coverage source | none | none | measurement absent | **DEFERRED** |
| `navigation` | individual navigation quality | no approved player path-efficiency source | none | none | measurement absent | **DEFERRED** |
| `toolUsage` | quality/effectiveness of tool use | `TEAM_TOOL_USED` proves use | direct action | no quality/opportunity denominator | count ≠ quality | **DEFERRED** |
| `risk` | meaningful player risk-taking quality | no approved construct | none | none | construct undefined | **DEFERRED** |
| `revive` | revive skill/quality | successful `PLAYER_REVIVED` only | revived user + reviver id | no attempt/opportunity denominator | success-only numerator | **DEFERRED** |

Activation requires authoritative source, attribution, availability, direction, formula, zero/missing semantics, version owner, consumer, and tests.

---

# 13. PlayerMatchScore

Logical object:

```text
PlayerMatchScore
{
    userId
    matchId
    matchEndTs
    matchScoreFormulaVersion
    normalizationConfigVersion
    profileNoiseFilterVersion

    dimensions {
        survival: MatchScoreDimension?
        noise: MatchScoreDimension?
        deferred dimensions: null
    }

    eligibilityRef
    sourceTelemetrySchemaVersion
}
```

Each dimension result records:

```text
score: number?
availability
formulaVersion
configVersion
sourceEvidenceRef/fingerprint
```

Only non-null, AVAILABLE, eligible dimension scores can be proposed for persistent update.

Once a valid dimension MatchScore is materialized into a `ProfileDimensionObservation`, its semantic payload is immutable for audit. Later source invalidation changes `contributionStatus`; it does not rewrite the historical score payload. A different semantic payload for the same `ProfileDimensionApplyKey` remains a PRO-DD-01 conflict, not a retraction update.

---

# 14. Player Survival

## 14.1 Formula

```text
valid authoritative PLAYER_ESCAPED
→ survival MatchScore = 100

valid authoritative PLAYER_ELIMINATED
→ survival MatchScore = 0
```

No Down Count is mixed into survival.

## 14.2 Availability

`AVAILABLE` requires:

- match is final enough for Profile;
- exactly one legal terminal survival fact exists for the player;
- event userId attribution is valid;
- no contradictory terminal fact/integrity conflict;
- source schema/profile version supported.

`UNAVAILABLE`:

- no terminal fact and source coverage cannot prove why;
- match not finalized;
- required terminal event permanently missing.

`INVALID`:

- both escaped and eliminated accepted for same player/match;
- malformed attribution;
- semantic/version conflict.

A legitimate score `0` means observed elimination, not missingness.

---

# 15. Player Noise

```text
ProfileNoisePenaltyCount
=
count(
    validated player-attributed NOISE_EMITTED
    where ProfileNoiseFilter.matches(event) == true
)
```

`ProfileNoiseFilter` is configurable and versioned. It may match allowed fields such as canonical `noiseType`, controlled reason, and phase if explicitly defined by the config. It must not classify every NoiseType as harmful by default.

## 15.1 Noise score

Higher penalty count is worse:

```text
x = ProfileNoisePenaltyCount

noiseScore
=
100 * (
    1 - clamp(
        (x - ProfileNoiseCountMin)
        /
        (ProfileNoiseCountMax - ProfileNoiseCountMin),
        0,
        1
    )
)
```

Required:

```text
ProfileNoiseCountMax > ProfileNoiseCountMin
```

## 15.2 Availability

```text
source coverage AVAILABLE
+ zero matching events
→ ProfileNoisePenaltyCount = 0
→ valid observed zero
```

```text
stream/source gap could contain missing NOISE_EMITTED
→ UNAVAILABLE
→ score = null
```

```text
invalid/unsupported ProfileNoiseFilter
OR invalid normalization config
→ INVALID
→ score = null
```

System/null-user research events are not Player noise inputs. Current v1.1 `NOISE_EMITTED` requires acting player userId for the current noise catalog.

Backend event deduplication happens before Profile aggregation. Profile still uses stable logical event identity so rerunning aggregation does not double-count.

---

# 16. Deferred Player Dimensions

Current evidence does not justify activation.

- `TEAM_TOOL_USED` proves accepted tool use, not good tool use.
- `PLAYER_REVIVED` proves a completed revive, not revive opportunity/attempt rate.
- `HELP_PING_USED` proves a request action, not communication quality.
- no raw Player transform sampling exists for exploration/navigation.
- no approved player-specific objective contribution metric exists.
- no approved risk construct exists.

Therefore:

```text
dimension.status = DEFERRED
dimension.score = null
MatchScore[dimension] = null
sampleCount does not increment
```

If future telemetry is needed to activate a construct, this document records that as a future telemetry contract requirement rather than fabricating a formula now.

---

# 17. Normalization

Two current active continuous/count normalization families:

## 17.1 Higher-is-worse

```text
score
=
100 * (
    1 - clamp(
        (x - Min) / (Max - Min),
        0,
        1
    )
)
```

Used by Player Noise and ObjectiveSpeed.

## 17.2 Direct bounded proportion

```text
score = 100 * clamp(numerator / denominator, 0, 1)
```

Used by Team Survival.

For every normalization config:

```text
Max > Min
```

If `Max <= Min`:

```text
MetricAvailability = INVALID
score = null
no silent repair/default
```

Numerical thresholds remain versioned `TUNING TBD`.

---

# 18. PlayerAIProfile Schema

Logical structure:

```text
PlayerAIProfile
{
    userId

    profileRevision
    profileLineageId

    profileFormulaVersion
    matchScoreFormulaVersion
    normalizationConfigVersion
    profileNoiseFilterVersion
    alphaConfigVersion

    dimensions {
        survival: PlayerDimensionState
        objective: PlayerDimensionState
        teamwork: PlayerDimensionState
        exploration: PlayerDimensionState
        navigation: PlayerDimensionState
        toolUsage: PlayerDimensionState
        risk: PlayerDimensionState
        noise: PlayerDimensionState
        revive: PlayerDimensionState
    }

    updatedAt?
}
```

For v1.1, `profileFormulaVersion` must resolve the logical semantic identity:

```text
PROFILE_FORMULA_V1_1
```

Exact persisted encoding may differ, but a token that means the predecessor first-sample blending rule cannot be reused for v1.1.

```text
PlayerDimensionState
{
    score: number?
    status: COLD_START | ACTIVE | DEFERRED
    sampleCount
    lastCanonicalMatchOrderKey?
    lastUpdatedAt?
}
```

`lastCanonicalMatchOrderKey` is dimension-local because one dimension may become available later than another for the same match. A global last-match marker cannot suppress a legitimate later dimension observation.

A `ProfileDimensionComparisonKey` for each observed ACTIVE dimension must be deterministically resolvable from the profile/config provenance defined in §25.4. It may be materialized or derived; duplicate storage is not required.

Freshness is not overloaded into the dimension status enum.

## 18.1 Orthogonal validity and freshness

`PlayerDimensionState.status` remains minimal:

```text
COLD_START | ACTIVE | DEFERRED
```

Profile-record validity and snapshot validity are separate. An old profile is **not** silently reset or changed to DEFERRED.

Profile v1.1 exposes `updatedAt` / per-dimension `lastUpdatedAt` so a downstream policy can evaluate freshness. No staleness duration is frozen here.

```text
profile age threshold
→ TUNING / POLICY TBD
→ expected AED v1.1 ownership unless later reassigned
```

---

# 19. Cold Start

For an ACTIVE contract dimension with no observed sample:

```text
score = 50
status = COLD_START
sampleCount = 0
```

This preserves a neutral presentation/default while making its non-observed nature explicit.

Critical rule:

```text
COLD_START score 50
≠
observed ACTIVE score 50
```

AED/snapshot consumers receive `status` and `sampleCount`; they must never infer evidence from the numeric value alone.

DEFERRED dimensions:

```text
score = null
status = DEFERRED
sampleCount = 0
```

---

# 20. EMA

For a dimension with at least one prior observed sample:

```text
newScore_d
=
(1 - alpha_d) * oldScore_d
+
alpha_d * matchScore_d

0 < alpha_d <= 1
```

Then clamp to `[0,100]`.

## 20.1 First valid sample — v1.1 correction

When:

```text
status = COLD_START
sampleCount = 0
```

the first valid observed MatchScore applies:

```text
newScore_d = matchScore_d
status = ACTIVE
sampleCount = 1
```

The initialization `50` is **not** an observed pseudo-sample and does not influence the first observation.

This intentionally supersedes the v0 rule that blended the first match with `50`.

The semantic owner is the Profile persistent-update formula, so the v1.1 rule is identified by:

```text
PROFILE_FORMULA_V1_1
```

and must not share a semantic formula identity with a predecessor rule that blends the initialization value into the first observation. This change does **not** by itself change Telemetry schema, Survival MatchScore formula, Noise MatchScore formula, or TeamPerformance formula topology.

## 20.2 Later samples

```text
status = ACTIVE
sampleCount > 0
+ eligible AVAILABLE MatchScore
→ create/confirm CONTRIBUTING observation
→ EMA over canonical CONTRIBUTING observation order
→ sampleCount reflects current CONTRIBUTING count
```

Null/unavailable/invalid/ineligible results do not create a new contribution. If a previously contributing source later becomes invalid/ineligible, PRO-DD-04 retracts it and recomputes by replay; Profile never attempts inverse EMA arithmetic.

---

# 21. sampleCount

`sampleCount` means:

```text
number of currently CONTRIBUTING valid observed MatchScores
used to derive this PlayerDimensionState
within this profile lineage
```

It does not count:

- matches merely played;
- raw telemetry events;
- unavailable metrics;
- ineligible matches;
- duplicate processing attempts;
- COLD_START initialization;
- DEFERRED dimensions;
- observations whose `contributionStatus = RETRACTED`.

Historical processing/audit count is a different concept.

```text
observation M once CONTRIBUTING
→ later RETRACTED
→ historical audit still records M
→ current sampleCount excludes M
```

If retraction leaves no CONTRIBUTING observations:

```text
score = 50
status = COLD_START
sampleCount = 0
```

This is the defined zero-observation state under `PROFILE_FORMULA_V1_1`, not a fabricated observation.


---

# 22. Profile Update Idempotency

**P0 contract**

Persistent score-application idempotency is dimension-scoped.

## 22.1 Canonical application identity — PRO-DD-01

```text
ProfileDimensionApplyKey
=
(
    userId,
    matchId,
    profileLineageId,
    dimensionKey
)
```

`dimensionKey` is a contract Player dimension identity such as `SURVIVAL` or `NOISE`.

No successful apply key/receipt is created for a DEFERRED dimension that has no valid MatchScore.

The key includes `profileLineageId` because formula migration/rebuild creates a distinct target lineage. The same historical match/dimension may therefore be reconstructed into a new target lineage without colliding with receipts from an old incompatible lineage.

## 22.2 Apply semantics

For dimension `D`:

```text
eligible + AVAILABLE + valid MatchScore(D)
+ no existing ProfileDimensionApplyKey(D)
→ incorporate the observation exactly once
→ create durable successful dimension receipt
→ update D
→ increment D.sampleCount exactly once
```

```text
same ProfileDimensionApplyKey
+ same immutable semantic MatchScore payload
→ DUPLICATE_NO_OP
→ no score change
→ no sampleCount change
```

```text
same ProfileDimensionApplyKey
+ different immutable semantic MatchScore payload
→ PROFILE_DIMENSION_APPLY_CONFLICT
→ no overwrite
→ no sampleCount change
→ diagnostic / quarantine / rebuild investigation
```

The receipt/fingerprint must resolve enough immutable semantics to distinguish a real duplicate from a conflict, including at minimum:

```text
dimensionKey
matchScore value
matchScore formula/config provenance
canonical match order key
target profile lineage
```

Exact database representation and semantic fingerprint implementation remain implementation bindings.

## 22.3 UNAVAILABLE / INVALID does not create a new successful application

When no observation for this key has previously been successfully processed:

```text
MetricAvailability = UNAVAILABLE
or INVALID
or MatchScore = null
→ do not create a new successful ProfileDimensionApply receipt/observation
→ no PlayerDimensionState contribution
→ no sampleCount increment
```

This rule does **not** delete a receipt that was legitimately created earlier while the metric was AVAILABLE. If that previously contributing source later becomes INVALID/INELIGIBLE:

```text
existing ProfileDimensionObservation remains auditable
→ contributionStatus becomes RETRACTED under PRO-DD-04
→ affected dimension replays
```

Example:

```text
first processing of match M:

survival = AVAILABLE
noise = UNAVAILABLE

→ SURVIVAL receipt exists
→ NOISE receipt does not exist
```

Later, if the same match obtains sufficient valid evidence:

```text
noise(M) becomes AVAILABLE
→ NOISE may be incorporated exactly once
→ existing SURVIVAL receipt remains a duplicate guard
→ SURVIVAL is not applied again
```

Applying one dimension must never permanently block another independent dimension from the same match.

## 22.4 Match-level processing/audit identity is separate

A match-level audit object may exist:

```text
ProfileMatchProcessingRecord
{
    userId
    matchId
    previousEligibility?
    eligibility
    telemetryCompleteness
    processingAttempts
    availableDimensions[]
    unavailableDimensions[]
    invalidDimensions[]
    affectedContributionDimensions[]?
    retractionStatus?
    replayStatus?
    provenance
}
```

Its exact persistence shape is optional/implementation-bound.

It is **not** the score-application idempotency identity and must not prevent a previously unavailable independent dimension from being incorporated later.

```text
match processing/audit identity
!=
dimension score-application identity
```

## 22.5 Same-match late dimension completion

For a newly available dimension observation `D(M)`:

```text
if no newer observation for D has already been incorporated:
    incorporate D(M) once
    create its dimension receipt
else:
    do not append by backend arrival order
    add/resolve D(M) as one logical observation
    deterministically replay only dimension D
    in canonical match order
```

Example:

```text
Match A OrderKey = 10
Match B OrderKey = 20

initial:
noise(A) = UNAVAILABLE
noise(B) = AVAILABLE and incorporated

later:
noise(A) becomes legitimately AVAILABLE

required:
replay NOISE dimension as A → B

forbidden:
append A after B
```

The Profile aggregate record may advance `profileRevision` because persisted state changed.

## 22.6 Dimension-local replay and receipt semantics

Replay operates at the smallest semantically affected scope.

```text
late Noise evidence only
→ replay NOISE lineage
→ do not replay SURVIVAL
→ SURVIVAL.sampleCount unchanged
```

A replay is not a second observation. Logical history remains:

```text
at most one valid observation
per
(userId, matchId, profileLineageId, dimensionKey)
```

Recomputation reconstructs derived `PlayerDimensionState` from the canonical ordered observation set represented by successful dimension receipts and their immutable source/provenance.

Creating a late historical receipt plus publishing the rebuilt dimension state must be atomic or behaviorally equivalent under failure/retry. Exact transaction technology remains an implementation binding.

A `ProfileUpdateReceipt` / per-dimension `AppliedObservationLedger` or behaviorally equivalent durable mechanism is required.

## 22.7 Contribution lifecycle — PRO-DD-04

A successful apply receipt means **the logical observation was processed**, not that it contributes forever.

```text
ProfileDimensionObservation
{
    key: ProfileDimensionApplyKey
    immutableSemanticPayload
    canonicalOrderKey
    contributionStatus: CONTRIBUTING | RETRACTED
    contributionStatusReason?
    sourceEligibilityReasonRef?
    sourceMetricReasonRef?
    provenance
}
```

```text
ProfileDimensionApplyKey / observation identity
= has this logical match-dimension observation already been processed?

ContributionStatus
= does that observation participate in the current derived PlayerDimensionState?
```

Therefore:

```text
RETRACTED != observation never existed
RETRACTED != delete audit history
RETRACTED != permission to recreate the same logical observation
```

The immutable semantic payload remains auditable after retraction.

## 22.8 Retraction scope

```text
source match becomes INELIGIBLE
→ all CONTRIBUTING observations from that match become RETRACTED
→ replay each ACTIVE dimension that actually lost a contribution
```

```text
match remains ELIGIBLE
+ one previously contributing metric becomes INVALID
→ retract only that metric's observation
→ replay only that dimension
→ unrelated ACTIVE dimensions unchanged
```

If a match contributed only SURVIVAL, global invalidation replays SURVIVAL only. If it contributed SURVIVAL and NOISE, both dimensions replay independently. DEFERRED dimensions are never replayed.

Retraction is lineage-scoped. If the same historical match contributes to more than one profile lineage, each lineage independently processes invalidation for its own observation ledger.

Controlled retraction category:

```text
ContributionRetractionCategory
=
MATCH_BECAME_INELIGIBLE
| METRIC_BECAME_INVALID
```

Do not duplicate every upstream integrity/config reason into a second Profile registry.

```text
MATCH_BECAME_INELIGIBLE
→ sourceEligibilityReasonRef = canonical MatchProfileEligibility reason(s)

METRIC_BECAME_INVALID
→ sourceMetricReasonRef = canonical MetricAvailability/calculator reason
```

Examples such as `IDENTITY_CONFLICT`, `SEQUENCE_CONFLICT`, `PROVENANCE_INVALID`, or version conflicts remain owned by the canonical eligibility/metric layer. `contributionStatusReason` is controlled/category-based; no free-text authoritative retraction reason is used.

## 22.9 Idempotent retraction

v1.1 does not require a second persistent identity object solely for retraction. A minimal equivalent uses the canonical observation identity plus current source invalidation state and an atomic contribution-status transition.

```text
ProfileDimensionApplyKey
+ current source invalidation state/fingerprint
+ CONTRIBUTING → RETRACTED
```

```text
same source invalidation processed twice
→ first processing may change CONTRIBUTING → RETRACTED and trigger replay
→ duplicate sees equivalent RETRACTED state
→ DUPLICATE_NO_OP
→ no second semantic retraction
→ no second sampleCount effect
→ no second profileRevision for pure duplicate no-op
```

Exact tombstone/status-column/event-sourced representation is an implementation binding.

## 22.10 Retraction correction algorithm

Forbidden:

```text
oldScore - contribution
inverse EMA
sampleCount-- without recomputing score
```

EMA is order-sensitive and is not generally invertible into the same canonical result.

Required:

```text
derive ordered observation set where contributionStatus = CONTRIBUTING
→ replay from canonical beginning or a verified equivalent checkpoint
→ produce exact PlayerDimensionState
```

Checkpoint optimization is allowed only if its observable result is identical to full deterministic replay.

## 22.11 Pre-commit source revalidation

Immediately before committing a **new** CONTRIBUTING observation, revalidate:

```text
target match still MatchProfileEligibility = ELIGIBLE
AND target metric still AVAILABLE + valid + FINAL for persistent profile use
AND target profileLineageId / formula semantics still match the apply plan
AND semantic payload/fingerprint still matches the planned observation
```

If validation fails:

```text
do not create CONTRIBUTING observation
→ discard stale plan
→ replan from current source state
```

This prevents a stale worker from adding an observation after invalidation is already known. It does not remove the need for PRO-DD-04 because invalidation may still be discovered after a prior successful commit.

## 22.12 Restoration policy

Telemetry v1.1 supports recoverable completeness improvement such as `INCOMPLETE → COMPLETE`. That path does not retract a valid independent contribution; newly available metrics follow PRO-DD-01.

Current Telemetry v1.1 does not define an authoritative correction protocol that converts a canonical identity/sequence/provenance `INVALID` interpretation back to valid. Therefore:

```text
RETRACTED due canonical INVALID
→ does not automatically return to CONTRIBUTING in Profile v1.1
```

A future upstream correction/recovery contract may explicitly re-approve the exact logical source. Only then may the **same logical observation** return to CONTRIBUTING under the same target-lineage semantics, followed by canonical replay. It is never a second logical observation.

## 22.13 Profile revision semantics for contribution-set changes

```text
successful new contribution
or successful CONTRIBUTING → RETRACTED transition
or future contract-approved RETRACTED → CONTRIBUTING restoration
or canonical replay that changes current derived state
→ commit a new PlayerAIProfile.profileRevision
```

```text
duplicate apply/retraction notification
+ no semantic observation/contribution/derived-state change
→ profileRevision unchanged
```

The exact revision allocation/CAS mechanism is an implementation binding; semantic no-op behavior is not.

## 22.14 Correction commit visibility

For one source invalidation affecting a profile lineage, the contribution-status changes, recomputed affected `PlayerDimensionState` values, their `sampleCount` values, and the resulting `profileRevision` form one logical correction commit.

Implementation may use one database transaction or behaviorally equivalent staged/revision-gated processing, but readers/snapshot builders must not observe a state presented as current where:

```text
source match is already canonical INELIGIBLE
AND some affected observation is marked RETRACTED
AND the published PlayerDimensionState still contains the old contribution
```

For global invalidation affecting SURVIVAL and NOISE, both affected dimensions must converge before the corrected profile revision is exposed as current. A crash/retry resumes/recomputes from durable source/observation state and converges to the same result.

---

# 23. Cross-Match Ordering

EMA is order-sensitive, so backend arrival order is forbidden as the semantic order.

Canonical eligible-match order:

```text
(matchEndTs, matchId)
```

where:

- `matchEndTs` = authoritative `MATCH_ENDED.ts`;
- `matchId` is the deterministic tie-break.

For each ACTIVE dimension, the current derivation sequence is the subset of observations whose match remains Profile-ELIGIBLE, whose metric remains valid for that lineage, and whose `contributionStatus = CONTRIBUTING`, ordered by the same canonical key.

The Profile result must be reproducible from the same ordered per-dimension CONTRIBUTING observation sets. Availability arriving late or contribution retraction does not change the ordering rule.

---

# 24. Late Match Handling

**Chosen policy: deterministic replay/recomputation.**

If an older valid observation becomes available after newer observations for the **same dimension** have already been incorporated:

```text
OrderKey(A) < OrderKey(B)
```

Profile v1.1 must not append A after B.

Instead:

```text
resolve A's valid PlayerMatchScore for dimension D
→ add/confirm exactly one ProfileDimensionApplyKey(A,D)
→ rebuild dimension D in canonical order
→ leave unrelated dimensions unchanged
→ produce a new aggregate profileRevision
```

Required retained evidence:

- each successful per-dimension MatchScore observation or sufficient immutable source to deterministically recompute it;
- its formula/config provenance;
- canonical order key;
- `ProfileDimensionApplyKey`.

No replay occurs from backend arrival order.

Example:

```text
noise(B) already incorporated
noise(A) becomes AVAILABLE later
OrderKey(A) < OrderKey(B)

→ replay NOISE A → B
→ SURVIVAL state/sampleCount unchanged
```

If historical source required for a mandated dimension rebuild is unavailable:

```text
affected dimension/lineage = INVALID/REBUILD_REQUIRED
→ do not silently approximate
```

A broader lineage replay is required only when a shared semantic migration/version change affects multiple dimensions; ordinary late availability remains dimension-local.

PRO-DD-04 uses the same replay primitive for removal:

```text
A → M → B
M later RETRACTED
→ replay A → B
```

The updater never appends a compensating negative observation and never attempts inverse EMA.

---

# 25. Formula / Config Version Compatibility

Profile v1.1 distinguishes document revision, persistent Profile formula semantics, MatchScore semantics, and numerical/config semantics.

## 25.1 Explicit Profile v1.1 formula semantic identity — PRO-DD-03

The current persistent-update semantic identity is frozen logically as:

```text
PROFILE_FORMULA_V1_1
```

This identifier means, at minimum:

```text
COLD_START 50 is representation only
first valid observed MatchScore → direct first ACTIVE score
later valid observations → EMA
sampleCount counts incorporated observations
```

Hard compatibility rule:

```text
PROFILE_FORMULA_V1_1
!=
any predecessor Profile formula semantic identity
whose first-sample rule blends COLD_START initialization into
the first observed MatchScore
```

The exact persisted encoding may be `"1.1"`, `"PROFILE_FORMULA_1_1"`, a registry key, or equivalent and remains `IMPLEMENTATION BINDING TBD`. The semantic identity itself is **not** TBD.

`profileFormulaSemanticId` is the normalized/resolved semantic identity of `profileFormulaVersion`; it is not a second independent version owner.

Document revision and formula semantic version are different concepts:

```text
Player_Team_Profile_Contract document revision = v1.1
X→ sufficient proof of score semantic compatibility

profileFormulaVersion / semantic registry identity
→ owns persistent Profile score/update semantics
```

## 25.2 Version ownership — do not bump unrelated layers

A Profile persistent-update semantic change requires a distinct `profileFormulaVersion`.

It does **not** automatically change:

```text
Telemetry schemaVersion
Survival MatchScore formula version
Noise MatchScore formula version
TeamPerformance formula version
```

when those contracts' mathematical semantics are unchanged.

Config changes such as alpha values, normalization Min/Max, ProfileNoiseFilter, and TeamPerformance weights use their own version owners.

## 25.3 No silent lineage mixing / migration

Any change that changes the numerical meaning or update dynamics of a Player profile dimension is incompatible with the current `profileLineageId` unless the lineage is deterministically rebuilt under one coherent target version set.

Canonical migration:

```text
old lineage
→ remains immutable/readable
→ cannot silently continue as PROFILE_FORMULA_V1_1
```

Create a target lineage:

```text
target profileFormulaVersion resolves PROFILE_FORMULA_V1_1
→ create new profileLineageId

if sufficient historical source is available:
    recompute/re-resolve eligible historical MatchScores as required
    replay each affected ACTIVE dimension in canonical order
    first observation uses v1.1 direct-first-sample rule
else:
    start target lineage at genuine COLD_START
    score = 50
    sampleCount = 0
    record migration provenance
```

No semantically incompatible scores share one EMA lineage.

Receipts from an old lineage do not collide with rebuilt v1.1 receipts because `profileLineageId` is part of `ProfileDimensionApplyKey`. Within the target lineage, each historical match/dimension still represents at most one logical observation.

TeamPerformance uses its own formula/config version and is unchanged by this Profile persistent-update semantic correction.

## 25.4 `ProfileDimensionComparisonKey` — PRO-DD-02

`profileLineageId` is user/history-specific and is **not** a cross-player comparison identity.

For every observed ACTIVE Player dimension, Profile v1.1 must resolve one canonical semantic compatibility identity:

```text
ProfileDimensionComparisonKey
```

Logical rule:

```text
two observed Player scores are directly comparable/aggregatable
only if
their ProfileDimensionComparisonKey values are exactly equal
AND
the common key is supported for the target Profile/snapshot contract
```

Exact equality is necessary; target-version support is also required. v1.1 has no implicit cross-version equivalence registry. Different keys are not assumed numerically equivalent.

The key contains only version/config inputs that materially determine the numerical meaning/history of that dimension.

### SURVIVAL

```text
SURVIVAL comparison key
=
(
    dimensionKey = SURVIVAL,
    profileFormulaVersion,
    survivalMatchScoreFormulaVersion,
    survivalAlphaConfigVersion
)
```

Survival excludes Noise filter and Noise normalization metadata because those do not affect Survival semantics.

### NOISE

```text
NOISE comparison key
=
(
    dimensionKey = NOISE,
    profileFormulaVersion,
    noiseMatchScoreFormulaVersion,
    noiseNormalizationConfigVersion,
    noiseAlphaConfigVersion,
    profileNoiseFilterVersion
)
```

If implementation currently uses registry-wide version tokens rather than per-dimension tokens, the resolver may use those tokens conservatively, but the semantic dependency set above remains the ownership rule.

`alphaConfigVersion` participates because alpha changes the historical EMA trajectory/current persisted score meaning. `profileFormulaVersion` participates because predecessor first-sample blending and `PROFILE_FORMULA_V1_1` direct-first-sample semantics differ.

Explicitly excluded from the comparison key:

```text
userId
profileRevision
profileLineageId
lastUpdatedAt
matchId
```

Different `profileRevision` values and different `profileLineageId` values may therefore still be directly comparable when the resolved semantic comparison key is equal.

Exact key serialization/hash representation is an implementation binding; the semantic tuple and equality rule are frozen.

## 25.5 Migration before cross-player aggregation

If current Player profiles resolve different comparison keys for an ACTIVE dimension:

```text
do not average anyway
do not silently drop incompatible players
do not convert scores ad hoc inside RosterProfileSummaryBuilder
```

Preferred remediation:

```text
migrate/rebuild source Player profiles into one approved target semantic version
→ produce new PlayerProfileSnapshot revisions
→ resolve one common ProfileDimensionComparisonKey
→ then aggregate
```

A future explicit compatibility/conversion registry would require its own approved contract. None is invented in Profile v1.1.

## 25.6 Source invalidation across lineages — PRO-DD-04

Retraction is evaluated per target lineage because `ProfileDimensionApplyKey` includes `profileLineageId`.

```text
same source match M
contributes in old lineage L0
and rebuilt PROFILE_FORMULA_V1_1 lineage L1

M later becomes globally invalid
→ process L0 observation(s) according to L0 audit/replay semantics
→ process L1 observation(s) according to L1 audit/replay semantics
```

Invalidating an observation in one lineage does not implicitly mutate another lineage. The invalidation detector must identify every lineage in which that source observation is currently CONTRIBUTING.

---

# 26. PlayerProfileSnapshot

Immutable downstream view:

```text
PlayerProfileSnapshot
{
    userId
    profileRevision
    profileLineageId
    capturedAt

    profileFormulaVersion
    matchScoreFormulaVersion
    normalizationConfigVersion
    profileNoiseFilterVersion
    alphaConfigVersion

    dimensions {
        score?
        status
        sampleCount
        lastCanonicalMatchOrderKey?
        lastUpdatedAt?
        comparisonSemanticKey?   // required/resolvable for observed ACTIVE values
    }
}
```

For a snapshot containing observed ACTIVE values, `profileFormulaVersion` must identify the semantic formula that actually produced those scores. A document revision string is not a substitute.

`comparisonSemanticKey` may be serialized directly or resolved deterministically from the snapshot's exact version/config provenance. It must resolve to the §25.4 `ProfileDimensionComparisonKey` before cross-player aggregation.

Snapshot values are copied/frozen at creation. Later profile updates — including contribution retraction/replay — create newer revisions; they do not mutate an existing snapshot.

A snapshot built from `profileRevision = R` remains an immutable historical artifact if later source invalidation produces revision `R+1`. Future decision validation must resolve current source profile revisions and must not silently reuse `R` as current.

---

# 27. TeamProfile v1.1

TeamProfile remains:

```text
MATCH-SCOPED
teamKey = matchId
no cross-match merge
no historical Team EMA
```

Logical structure:

```text
TeamProfile
{
    teamKey = matchId
    matchId
    fields
    profileFormulaVersion
    normalizationConfigVersion
    sourceTelemetrySchemaVersion
    processingStatus
}
```

Same roster in match A and B creates different TeamProfile records.

Previous-match TeamProfile can never fill missing current-match fields.

---

# 28. Team Field Audit

| Field | v1.1 decision | Evidence |
|---|---|---|
| `objectiveTime` | **KEEP ACTIVE** | canonical PHASE_STARTED/PHASE_COMPLETED timing remains |
| `splitTime` | DEFER | no approved team split sampling/definition |
| `avgDistance` | DEFER | no approved continuous/sampled position contract |
| `reviveSuccess` | DEFER | success event exists but attempt/opportunity denominator absent |
| `resourceEfficiency` | DEFER | tool use does not prove value/waste/opportunity |
| `communication` | DEFER | HELP_PING_USED proves action, not communication quality |
| `wipeRecovery` | DEFER | source/episode definition not frozen |

Deferred Team fields remain present for compatibility but have:

```text
value = null
status = DEFERRED
```

---

# 29. objectiveTime

```text
objectiveTime
=
sum(
    PHASE_COMPLETED.ts
    - matching PHASE_STARTED.ts
    for objective-bearing phase identities
)
```

Unit: seconds.

## 29.1 Exact rules

- event ordering is checked using `eventSequence`;
- duration is measured from authoritative occurrence `ts`;
- pair must share match and phase identity;
- each logical start/completion is deduplicated by telemetry identity before aggregation;
- duplicate accepted retries do not double-count;
- missing start or completion for a required objective-bearing phase → metric UNAVAILABLE;
- negative elapsed duration → INVALID;
- overlapping objective-bearing phases are valid only if gameplay phase registry explicitly permits overlap; otherwise INVALID;
- `SECURITY_HOLD_INTERRUPTED` does not subtract time; interruption remains inside wall-clock elapsed phase duration;
- objective-bearing phase membership belongs to approved gameplay/phase registry, not analytics inference.

Current v1.1 keeps the v0 objectiveTime semantic.

---

# 30. Team Survival

Sources:

```text
MATCH_STARTED.context.teamSize
MATCH_ENDED.data.survivorCount
```

Formula:

```text
Survival
=
100 * clamp(survivorCount / teamSize, 0, 1)
```

Required:

```text
teamSize > 0
0 <= survivorCount <= teamSize
match eligible
source anchors AVAILABLE
```

Aborted/ineligible/unavailable/invalid inputs produce `null`.

Disconnected-player treatment is not invented here. If gameplay later changes teamSize/survivor semantics, that source contract must be revised explicitly.

---

# 31. Teamwork Validity Review

Current relevant evidence:

- `PLAYER_REVIVED` — successful revive;
- `TEAM_TOOL_USED` — accepted tool action;
- `HELP_PING_USED` — accepted Need Help ping.

These are action facts. They do not establish:

- opportunity denominator;
- coordination quality;
- whether action helped another player;
- whether timing was appropriate;
- whether an alternative would have been better.

Therefore:

```text
Teamwork = DEFERRED
```

**Minimum future source requirement:** an approved, privacy-bounded cooperation outcome/opportunity contract with exact numerator/denominator or equivalent construct semantics.

If implemented through new telemetry, that requires:

```text
TELEMETRY CONTRACT REVISION REQUIRED
```

No revision is made by this document.

---

# 32. ResourceEfficiency Validity Review

`TEAM_TOOL_USED` tells Profile:

```text
tool X was validly used by player Y at a phase
```

It does not tell Profile:

- tool opportunity/budget denominator;
- waste;
- outcome caused;
- saved resource;
- utility/value created;
- whether use was necessary.

Therefore:

```text
ResourceEfficiency = DEFERRED
```

**Minimum future source semantic:** versioned resource budget/consumption plus approved useful-outcome/opportunity semantics sufficient to distinguish efficient from merely frequent use.

If that source requires new telemetry:

```text
TELEMETRY CONTRACT REVISION REQUIRED
```

---

# 33. TeamPerformance v1.1

v1.1 deliberately **preserves** the v0 construct topology:

```text
ObjectiveSpeed
Survival
Teamwork
ResourceEfficiency
```

```text
TeamPerformance
=
wObjective * ObjectiveSpeed
+
wSurvival * Survival
+
wTeamwork * Teamwork
+
wResource * ResourceEfficiency
```

Constraints:

```text
each weight >= 0
sum(weights) = 1
weights are configurable/versioned
```

ObjectiveSpeed:

```text
100 * (
    1 - clamp(
        (objectiveTime - ObjectiveTimeMin)
        /
        (ObjectiveTimeMax - ObjectiveTimeMin),
        0,
        1
    )
)
```

No raw seconds enter the weighted sum.

`COMPLETE` only when all four required components are non-null, valid, and current-match owned.

Current result:

```text
ObjectiveSpeed = potentially available
Survival = potentially available
Teamwork = DEFERRED
ResourceEfficiency = DEFERRED

→ TeamPerformance.status = INCOMPLETE
→ TeamPerformance.score = null
```

No missing component is set to zero. No weights are renormalized.

## 33.1 TeamPerformance formula-version decision

The **formula topology is unchanged from the predecessor**. Therefore document revision `v1.1` alone does not require pretending that TeamPerformance has a new mathematical topology.

```text
same four required components
+ same weighted-sum topology
→ same TeamPerformance formula semantic lineage

weight/normalization changes
→ config version change

required-component/topology change
→ TeamPerformance formula version change
```

If the implementation already has a version token for the v0 four-component formula, it may remain that formula token while Profile document revision becomes v1.1. Exact token encoding is an implementation binding; the compatibility rule above is frozen.

---

# 34. TeamPerformance Activation Decision

| Component | Current evidence | Valid formula possible now? | v1.1 decision | Blocker |
|---|---|---:|---|---|
| ObjectiveSpeed | objectiveTime phase pairs | Yes | ACTIVE | normalization thresholds remain config |
| Survival | teamSize + survivorCount | Yes | ACTIVE | valid final match required |
| Teamwork | revive/tool/help action facts | No | **DEFERRED** | no quality/opportunity construct |
| ResourceEfficiency | tool-use fact | No | **DEFERRED** | no value/waste/opportunity denominator |

**Decision:** preserve four-component topology and honest incompleteness. Do not create a two-component replacement merely to pass the Adaptive gate.

---

# 35. SC-03 — PRE_MATCH Team Input Blocker

A new match has no current-match TeamProfile.

Forbidden:

```text
PRE_MATCH → previous TeamProfile as current TeamProfile
same roster → persistent team identity
synthetic current TeamPerformance
```

Profile-side resolution:

```text
current lobby roster
→ immutable PlayerProfileSnapshots
→ deterministic decision-scoped RosterProfileSummary
→ AdaptiveInputSnapshot
```

This does not create a persistent team record.

```text
PROFILE-SIDE SC-03: RESOLVED
AED-SIDE CONSUMPTION REVISION: REQUIRED
```

M1-015 v0 still expects `teamProfileRef + TeamPerformance COMPLETE`; AED v1.1 must consume/validate the new snapshot lifecycle or explicitly map it without inventing a team identity.

---

# 36. Decision-Scoped RosterProfileSummary

`RosterProfileSummary` is not a TeamProfile.

Logical schema:

```text
RosterProfileSummary
{
    rosterIdentity
    teamSize

    per ACTIVE Player dimension {
        observedActiveCount
        coldStartCount
        missingCount
        unsupportedCount
        observedCoverageRatio

        aggregationStatus
        comparisonSemanticKey?
        meanObservedScore?
    }
}
```

Recommended minimal per-dimension aggregation status:

```text
AVAILABLE
UNAVAILABLE
INVALID
```

where `INVALID` includes semantic incompatibility.

## 36.1 Cross-player semantic compatibility — PRO-DD-02

For one ACTIVE dimension:

```text
candidate observed PlayerDimensionState
=
snapshot.status == ACTIVE
AND sampleCount > 0
AND score != null
AND ProfileDimensionComparisonKey resolves successfully
```

Before calculating `meanObservedScore`:

```text
1. collect all observed candidate states for the roster
2. resolve each ProfileDimensionComparisonKey
3. require exactly one coherent key value across all observed candidates
4. only then aggregate
```

If no observed candidate exists:

```text
aggregationStatus = UNAVAILABLE
comparisonSemanticKey = null
meanObservedScore = null
```

If all observed candidates resolve the same key `K`:

```text
aggregationStatus = AVAILABLE
comparisonSemanticKey = K

meanObservedScore
=
sum(observed scores) / observedActiveCount
```

If more than one distinct comparison key exists:

```text
aggregationStatus = INVALID
comparisonSemanticKey = null
meanObservedScore = null
reason = FORMULA_VERSION_CONFLICT
AdaptiveInputSnapshot = INVALID
```

Forbidden:

```text
mixed semantic keys → average anyway
```

Forbidden:

```text
mixed semantic keys
→ silently drop incompatible player(s)
→ average the remaining subset
```

because selective dropping changes the roster evidence and introduces bias.

`profileRevision` and `profileLineageId` are not equality requirements:

```text
Player A profileRevision = 4
Player B profileRevision = 9
same ProfileDimensionComparisonKey
→ compatible
```

```text
Player A profileLineageId = LA
Player B profileLineageId = LB
same ProfileDimensionComparisonKey
→ compatible
```

Conversely:

```text
both formula/config versions individually supported
but comparison keys differ
→ not directly aggregatable
```

## 36.2 Evidence coverage

Cold-start `50`, null, missing, DEFERRED, or unsupported values are **not averaged as zero** and are not included in `meanObservedScore`.

```text
observedCoverageRatio
=
observedActiveCount / teamSize
```

This is evidence coverage, not a performance score.

`PARTIAL` snapshot status may still arise from evidence insufficiency such as COLD_START or PROFILE_MISSING, but semantic incompatibility among observed numeric scores is `INVALID`, not ordinary missingness.

No min/max is included in v1.1 because no current downstream contract requires it.

## 36.3 Roster identity

Canonical semantic input:

```text
rosterIdentityContent
=
(targetMatchId, sorted unique userIds in the current authoritative roster)
```

`rosterIdentity` is a deterministic hash/identifier of that canonical content. Hash algorithm/encoding is an implementation binding; membership semantics are not.

Player profile revisions are **not** folded into roster membership identity. They are carried separately as source provenance so the same roster can produce a newer snapshot when profiles change.

## 36.4 Evidence sufficiency

Profile exposes:

```text
sampleCount
status
lastUpdatedAt
observedCoverageRatio
ProfileDimensionComparisonKey
source formula/config versions
```

Profile v1.1 does **not** freeze a rule such as "N matches means enough evidence." Minimum adaptive evidence thresholds belong to AED v1.1 policy unless a later contract explicitly transfers ownership.

---

# 37. AdaptiveInputSnapshot

Smallest sufficient producer-side contract:

```text
AdaptiveInputSnapshot
{
    snapshotId
    snapshotContentFingerprint
    targetMatchId
    decisionPoint
    phaseContext?
    createdAt

    rosterIdentity
    teamSize
    playerProfileSnapshots[]
    rosterProfileSummary

    currentMatchProcessedEvidence?

    snapshotValidity
    reasonCodes[]

    provenance {
        profileFormulaSemanticId
        dimensionComparisonKeys {
            SURVIVAL?
            NOISE?
        }
        teamPerformanceFormulaVersion
        sourceProfileRevisions[]
        currentMatchTelemetrySchemaVersion?
    }
}
```

Hard properties:

- immutable after creation;
- decision-scoped;
- deterministic semantic content from the same source revisions;
- no persistent team identity;
- no raw TelemetryEvent;
- no hidden Player state;
- no Player Transform;
- no Monster memory;
- no Listener HearingObservation;
- no Warden candidate/graph dump;
- no stale TeamProfile substitution;
- exact source profile revisions;
- source revisions are current for the decision when the snapshot is validated/consumed;
- later profile retraction never mutates an already-created snapshot;
- exact formula/config provenance;
- one exact resolved `ProfileDimensionComparisonKey` for every VALID aggregated ACTIVE dimension;
- no claim of a common semantic key when source profiles disagree.

`targetMatchId` must be a stable match/setup identity before the snapshot can authorize an AED evaluation. If target match identity is not yet resolvable, snapshot validation fails and downstream uses Fixed/fallback.

## 37.1 Snapshot identity rule

`AdaptiveInputSnapshot.snapshotId` is a unique immutable construction identity. It is **not required to be content-addressed**.

The builder also computes a deterministic semantic fingerprint from:

```text
targetMatchId
decisionPoint / phaseContext
rosterIdentity
sorted source profile revisions
current-match processed-evidence values/status/finality
all required formula/config version provenance
resolved per-dimension comparison semantic keys
```

Therefore:

```text
same semantic inputs reconstructed twice
→ snapshotId may differ
→ snapshotContentFingerprint MUST match
→ semantic snapshot content MUST match
```

This makes retries/audits traceable without forcing globally deterministic GUID generation.

---

# 38. PRE_MATCH Snapshot

Sources:

```text
current target-match roster
+
existing persistent PlayerAIProfile revisions
```

Not sources:

```text
current-match TeamProfile
previous TeamProfile
raw current telemetry
```

Handling:

| Condition | Snapshot result |
|---|---|
| all players ACTIVE/supported + each aggregated dimension has one coherent comparison key | `VALID` if other structural/provenance checks pass |
| all players COLD_START | `PARTIAL`, reason `INSUFFICIENT_OBSERVED_PROFILE_EVIDENCE` |
| mixed ACTIVE/COLD_START + observed values semantically coherent | `PARTIAL` |
| profile missing | `PARTIAL`, `PROFILE_MISSING` |
| unsupported formula lineage | `INVALID`, `PROFILE_VERSION_UNSUPPORTED` |
| multiple incompatible comparison keys for one aggregated ACTIVE dimension | `INVALID`, `FORMULA_VERSION_CONFLICT` |
| different profileRevision values but same comparison keys | allowed; revisions remain exact provenance |
| unresolved profile revision | `INVALID`, `PROFILE_REVISION_UNRESOLVABLE` |
| player joins/leaves before snapshot is consumed | old snapshot not reusable; build new snapshot |
| roster mutation after creation | snapshot bytes remain immutable; validation against current roster returns `ROSTER_CHANGED` and requires replacement |

A missing profile is not silently synthesized as observed score 50. Account/profile initialization may create a genuine COLD_START profile through a separate explicit lifecycle.

---

# 39. Phase-Boundary Snapshot

At `ALLOWED_PHASE_BOUNDARY`, current-match evidence may be included only after Profile aggregation has converted it into explicit processed metrics.

Example:

```text
completed objective-bearing phase duration
→ FINAL for that closed phase

cumulative objectiveTimeSoFar
→ PROVISIONAL for the whole match
```

Final Team Survival:

```text
UNAVAILABLE before MATCH_ENDED
```

Final TeamPerformance:

```text
not fabricated mid-match
```

`currentMatchProcessedEvidence` entries carry:

```text
metricName/version
value?
availability
finality
sourceBoundarySequence
```

AED v1.1 must not treat `PROVISIONAL` as final unless its own policy explicitly allows that metric/finality at that decision point.

---

# 40. FINAL_HUNT_SETUP Snapshot

`FINAL_HUNT_SETUP` uses the same processed-data rules as a controlled phase boundary.

Allowed Profile-side evidence:

- frozen roster PlayerProfileSnapshots;
- roster summary;
- completed prior-phase processed evidence;
- explicitly provisional cumulative current-match metrics with finality label.

Not allowed:

- future match outcome;
- synthetic final Survival;
- Monster runtime state;
- raw Warden/Listener/Stalker research telemetry.

No special Profile authority is created by the Final Hunt phase.

---

# 41. Snapshot Validity / Roster Mutation

```text
SnapshotValidity
=
VALID
| PARTIAL
| INVALID
```

Controlled reasons:

```text
PROFILE_MISSING
PROFILE_VERSION_UNSUPPORTED
PROFILE_REVISION_UNRESOLVABLE
SOURCE_PROFILE_INVALID
SOURCE_PROFILE_REVISION_CHANGED
FORMULA_VERSION_CONFLICT
INSUFFICIENT_OBSERVED_PROFILE_EVIDENCE
CURRENT_MATCH_EVIDENCE_UNAVAILABLE
ROSTER_CHANGED
ROSTER_EMPTY
TARGET_MATCH_UNRESOLVABLE
```

`PARTIAL` means structurally valid but evidence coverage is incomplete. It is not permission for AED to improvise a score.

`INVALID` means the snapshot cannot safely be used as an adaptive input.

For cross-player semantics:

```text
same dimension + multiple distinct ProfileDimensionComparisonKey values
→ FORMULA_VERSION_CONFLICT
→ INVALID
```

The builder must not downgrade this to PARTIAL by dropping incompatible players.

Roster mutation rule:

```text
snapshot S created for roster R1
current roster becomes R2 before adaptive decision commits
→ S remains immutable history
→ S fails current-roster validation
→ create S2 for R2
```

No in-place mutation.

Source-profile revision correction rule:

```text
snapshot S1 references Player profileRevision 10
→ later source invalidation retracts an observation
→ canonical replay commits profileRevision 11

S1 remains immutable historical evidence
but fails current-source-revision validation for a future adaptive decision
→ reason = SOURCE_PROFILE_REVISION_CHANGED
→ build S2 from revision 11
```

If S1 was already consumed by an earlier gameplay decision, Profile does not rewind gameplay or rewrite past ScenarioConfig/Monster behavior. Research/experiment consumers may later flag/exclude that historical decision using preserved provenance under their own policy.

## 41.1 Current-match vs historical data taxonomy

```text
PlayerAIProfile
= persistent cross-match processed player history

TeamProfile
= one match-scoped processed team result

RosterProfileSummary
= decision-scoped aggregate of current roster PlayerProfileSnapshots

CurrentMatchProcessedEvidence
= decision-scoped processed facts from the target match, with finality

AdaptiveInputSnapshot
= immutable decision-scoped package combining only permitted processed inputs
```

These concepts are not aliases and are never merged into one persistent Team object.

---

# 42. AED v1.1 Handoff

Profile v1.1 guarantees AED v1.1 can validate:

- exact target match and decision point;
- exact roster identity;
- immutable PlayerProfileSnapshot revisions;
- current ACTIVE/COLD_START/DEFERRED status and sampleCount;
- observed roster coverage;
- formula/config versions;
- current-match evidence availability/finality;
- snapshot validity/reasons;
- no raw telemetry or hidden runtime state.

M1-015 v0 assumption:

```text
playerProfileRefs[]
+ teamProfileRef
+ TeamPerformance COMPLETE
```

is not sufficient for Profile v1.1 PRE_MATCH SC-03.

Required downstream revision:

```text
AED v1.1 REVISION REQUIRED
→ consume AdaptiveInputSnapshot (or exact equivalent)
→ validate decision point, versions, roster, coverage/finality
→ define minimum evidence policy
→ choose Fixed/fallback when input is PARTIAL/INVALID or policy prerequisites fail
```

This contract does not choose ScenarioConfig fields or difficulty policy.

---

# 43. Research Validity

## 43.1 Player Survival

Risks:

- binary outcome is coarse;
- disconnect semantics remain gameplay-contract dependent;
- incomplete terminal telemetry can bias samples.

Mitigation: exact terminal source, explicit availability, no Down Count substitution.

## 43.2 Player Noise

Risks:

- construct depends on ProfileNoiseFilter;
- normalization threshold sensitivity;
- missing event bias;
- different config versions reduce cross-run comparability.

Mitigation: versioned filter/config, no missing→zero, retain provenance.

## 43.3 Team ObjectiveSpeed

Risks:

- wall-clock objectiveTime includes interruption;
- map/content differences affect time;
- normalization sensitivity.

Mitigation: fixed semantic, map/build provenance, versioned thresholds.

## 43.4 Team Survival

Risks:

- small team sizes make one player materially change the score;
- disconnect meaning is not inferred.

Mitigation: expose teamSize/survivorCount source and preserve experimental unit context.

## 43.5 Deferred constructs

Teamwork and ResourceEfficiency remain DEFERRED specifically to avoid construct-validity claims that current telemetry cannot support.

---

# 44. Versioning / Reproducibility

Processed outputs must retain or resolve:

```text
profileFormulaVersion / `PROFILE_FORMULA_V1_1` semantic identity
matchScoreFormulaVersion
teamPerformanceFormulaVersion
normalizationConfigVersion
profileNoiseFilterVersion
alphaConfigVersion
per-dimension `ProfileDimensionComparisonKey`
sourceTelemetrySchemaVersion
source matchId
source Player profile revisions
```

Reproducibility requirement:

```text
same validated source evidence
+ same eligibility/availability
+ same canonical match ordering
+ same formula/config versions
+ same source profile revision set
→ same PlayerMatchScore
→ same per-dimension logical observation identities + contribution statuses
→ same canonical CONTRIBUTING observation set / replay
→ same profile lineage state
→ same resolved per-dimension comparison keys
→ same TeamProfile
→ same TeamPerformance status/score
→ same AdaptiveInputSnapshot semantic content
```

For experiment runs, these versions must be frozen/traceable according to M1-020.

---

# 45. Processing Configuration

Logical config:

```text
ProfileProcessingConfig
{
    profileFormulaVersion   // must resolve PROFILE_FORMULA_V1_1 for current v1.1 lineage
    matchScoreFormulaVersion
    teamPerformanceFormulaVersion

    normalizationConfigVersion
    profileNoiseFilterVersion
    alphaConfigVersion

    alphaByActiveDimension
    ProfileNoiseFilter
    ProfileNoiseCountMin
    ProfileNoiseCountMax

    ObjectiveTimeMin
    ObjectiveTimeMax

    TeamPerformanceWeights
}
```

Validation runs before formulas.

Invalid config:

```text
→ affected metric INVALID
→ no affected Player profile update
→ TeamPerformance INCOMPLETE if required component affected
→ no silent default
```

unless the requested version explicitly resolves a previously approved immutable default.

---

# 46. Backend Component Contracts

| Component | Owns | Must not own |
|---|---|---|
| `MatchTelemetryAggregator` | validated ordered match projection | profile formula policy |
| `TelemetryProfileIntegrityGate` | consumer-required source coverage/integrity | gameplay semantics |
| `ProfileEligibilityEvaluator` | match eligibility status/reasons | Metric formula |
| `PlayerMatchScoreCalculator` | pure Player score formulas | persistence |
| `ProfileConfigRegistry` | versioned config resolution/validation + `ProfileDimensionComparisonKey` resolution | gameplay |
| `PlayerProfileUpdatePlanner` | dimension-scoped canonical ordered apply/replay plan | DB-specific transaction |
| `ProfileSourceInvalidationDetector` | detect authoritative match-level or metric-level source invalidation affecting existing contributions | gameplay/AED policy |
| `ProfileContributionRetractionPlanner` | idempotent affected-lineage/dimension retraction + canonical replay plan | inverse EMA / unrelated-dimension replay |
| `PlayerProfileUpdater` | atomic profile revision + per-dimension observation status/apply/retraction/rebuild commits | raw telemetry validation |
| `TeamProfileBuilder` | match-scoped team fields | cross-match team identity |
| `TeamPerformanceCalculator` | exact four-component formula/status | missing-value invention |
| `PlayerProfileSnapshotBuilder` | immutable per-player snapshot | profile mutation |
| `RosterProfileSummaryBuilder` | decision-scoped deterministic aggregate after cross-player comparison-key validation | persistent TeamProfile; ad-hoc score conversion; silent incompatible-player dropping |
| `AdaptiveInputSnapshotBuilder` | immutable processed handoff package | AED policy |
| `ProfileProcessingDebugProvider` | read-only diagnostics | mutation |

Exact class names are implementation bindings.

---

# 47. Concurrency

Multiple workers may process the same user or same match.

Required semantic guarantees:

```text
no lost update
no duplicate dimension observation application
independent dimensions from one match may complete separately
canonical source eligibility/metric validity wins over stale worker plans
no lost retraction
no resurrection from stale apply plan
canonical per-dimension ordering preserved
dimension-local replay/retraction is retry-safe
profileRevision monotonic on semantic contribution-set changes
semantic duplicate/no-op leaves profileRevision unchanged
conflicting dimension apply payload not last-write-wins
```

Allowed implementation techniques:

- transaction / serializable update;
- optimistic revision compare-and-swap;
- per-user serialized work queue;
- equivalent mechanism.

The database primitive is TBD; the semantic outcome is not.

P0 race example:

```text
Worker A plans late NOISE availability
Worker B processes match invalidation
```

Before A commits, §22.11 revalidation plus revision/CAS/transaction semantics must reject/replan A if the match/metric is no longer valid. Final state is derived from canonical source eligibility, never whichever worker writes last.

Two workers processing the same invalidation must converge to one semantic `CONTRIBUTING → RETRACTED` transition and one resulting replay state.

---

# 48. Observability

Read-only logical snapshot:

```text
ProfileProcessingDebugSnapshot
{
    matchId
    sourceSchemaVersion
    telemetryCompleteness

    matchProfileEligibility
    eligibilityReasons[]

    metricAvailability{}
    computedMatchScores{}

    canonicalMatchOrderKey

    dimensionApplyKeys{}
    dimensionApplyResults{}

    dimensionContributionStatus{}
    dimensionRetractionTriggered{}
    dimensionRetractionReason{}
    replayExcludedObservationKeys{}
    dimensionReplayTriggered{}
    dimensionReplayFromOrderKey{}

    previousMatchProfileEligibility?
    currentMatchProfileEligibility
    affectedDimensionKeys[]

    profileRevisionBefore
    profileRevisionAfter

    dimensionComparisonKeys{}
    comparisonCompatibilityStatus{}

    teamProfileStatus
    teamPerformanceStatus

    adaptiveInputSnapshotId?
    snapshotValidity?
    snapshotReasons[]
}
```

Debug information may not mutate Profile, AED, ScenarioConfig, or gameplay.

---

# 49. Tests — Pure Processing

Required tests only; no PASS claim.

| ID | Requirement | Expected |
|---|---|---|
| PRO-E-001 | escaped survival | score 100 |
| PRO-E-002 | eliminated survival | score 0 |
| PRO-E-003 | contradictory terminal facts | survival INVALID/null |
| PRO-E-004 | MATCH_ABORTED | match INELIGIBLE |
| PRO-E-005 | COMPLETE valid stream | normal eligibility evaluation |
| PRO-E-006 | INCOMPLETE unrelated research loss | independent Profile metric may remain AVAILABLE |
| PRO-E-007 | INCOMPLETE unknown gap + no noise events | noise UNAVAILABLE, not zero |
| PRO-E-008 | INVALID telemetry | no new contribution; if prior contributions from an earlier eligible interpretation exist, PRO-DD-04 retracts/replays them |
| PRO-E-009 | UNKNOWN/live match | PENDING |
| PRO-E-010 | valid zero noise penalty | count 0 and valid normalized score |
| PRO-E-011 | unavailable noise source | null |
| PRO-E-012 | ProfileNoiseFilter match | counted once |
| PRO-E-013 | filter non-match | not penalty |
| PRO-E-014 | invalid filter | noise INVALID/null |
| PRO-E-015 | null never becomes zero | preserved |
| PRO-E-016 | normalization lower/upper clamp | deterministic `[0,100]` |
| PRO-E-017 | Max <= Min | INVALID |
| PRO-E-018 | cold start representation | 50/COLD_START/0 |
| PRO-E-019 | first valid sample | score = MatchScore, no blend with 50 |
| PRO-E-020 | later EMA | exact formula |
| PRO-E-021 | null MatchScore | no score/sampleCount change |
| PRO-E-022 | sampleCount | increments only on applied observation |
| PRO-E-023 | deferred dimension | null/DEFERRED |
| PRO-E-024 | duplicate ProfileDimensionApplyKey | no second dimension update |
| PRO-E-025 | same dimension apply key different payload | `PROFILE_DIMENSION_APPLY_CONFLICT`; no overwrite |
| PRO-E-026 | match B arrives before older A | replay canonical A→B |
| PRO-E-027 | equal matchEndTs | deterministic matchId tie-break |
| PRO-E-028 | formula version mismatch | no silent mix |
| PRO-E-029 | config change | new/rebuilt lineage per contract |
| PRO-E-030 | TeamProfile identity | matchId scoped |
| PRO-E-031 | same roster different matches | distinct TeamProfiles |
| PRO-E-032 | previous TeamProfile reuse attempt | rejected |
| PRO-E-033 | objective phase pair | exact elapsed ts |
| PRO-E-034 | missing objective phase pair | objectiveTime UNAVAILABLE |
| PRO-E-035 | negative duration | INVALID |
| PRO-E-036 | TeamPerformance missing Teamwork | INCOMPLETE/null |
| PRO-E-037 | missing component renormalization attempt | forbidden |
| PRO-E-038 | valid Team Survival | exact ratio |
| PRO-E-039 | invalid survivorCount | null/INVALID |
| PRO-E-040 | research-capture event as Player Profile input | rejected by mapping |
| PRO-E-041 | survival AVAILABLE + noise UNAVAILABLE | only SURVIVAL dimension receipt created |
| PRO-E-042 | same match later gains valid noise evidence | NOISE applies once; SURVIVAL unchanged |
| PRO-E-043 | duplicate ProfileDimensionApplyKey + same payload | `DUPLICATE_NO_OP` |
| PRO-E-044 | same ProfileDimensionApplyKey + different payload | `PROFILE_DIMENSION_APPLY_CONFLICT`; no overwrite |
| PRO-E-045 | late older newly available dimension observation | affected dimension canonical replay |
| PRO-E-046 | dimension-local replay | unrelated dimension sampleCount unchanged |
| PRO-E-047 | same comparison semantic key + different profileRevision | comparison compatible |
| PRO-E-048 | different profileFormulaVersion semantics | roster aggregation rejected |
| PRO-E-049 | different Noise ProfileNoiseFilterVersion | Noise mean not aggregated |
| PRO-E-050 | different alpha semantics affecting persisted score history | incompatible comparison key |
| PRO-E-051 | predecessor blended-first-sample lineage vs v1.1 direct-first-sample lineage | distinct Profile formula semantic identity |
| PRO-E-052 | old lineage direct continuation under v1.1 semantic identity | rejected; migration/new lineage required |
| PRO-E-053 | historical replay into v1.1 target lineage | first observation uses direct-first-sample rule |
| PRO-E-054 | previously applied SURVIVAL + source match later globally INELIGIBLE | observation RETRACTED; replay excludes match |
| PRO-E-055 | same match contributed SURVIVAL + NOISE + global invalidation | both observations RETRACTED; both affected dimensions replay |
| PRO-E-056 | NOISE later INVALID while match stays ELIGIBLE | NOISE retracted/replayed; SURVIVAL unchanged |
| PRO-E-057 | retraction leaves zero observations | score 50 / COLD_START / sampleCount 0 |
| PRO-E-058 | contributing A,M,B then M retracted | canonical replay exactly A→B |
| PRO-E-059 | same invalidation processed twice | idempotent no-op after first semantic retraction; sampleCount stable |
| PRO-E-060 | INCOMPLETE→COMPLETE without invalidation | existing valid contribution retained |
| PRO-E-061 | metric UNAVAILABLE→AVAILABLE | PRO-DD-01 apply/replay path; no retraction |
| PRO-E-062 | metric AVAILABLE→INVALID | immutable historical payload retained for audit; contribution excluded from current derived profile |

---

# 50. Tests — Backend / Integration

| ID | Scenario | Expected |
|---|---|---|
| PRO-B-001 | Telemetry `"1.1"` → MatchTelemetry | accepted events ordered by eventSequence |
| PRO-B-002 | job retry | existing ProfileDimensionApplyKey causes no second dimension mutation |
| PRO-B-003 | worker crash after receipt commit | restart does not double-apply |
| PRO-B-004 | concurrent user updates | no lost update |
| PRO-B-005 | profile revision CAS conflict | retry/replan from current revision |
| PRO-B-006 | late older match | deterministic replay |
| PRO-B-007 | replay service restart | same final profile |
| PRO-B-008 | unsupported Profile config version | no affected update |
| PRO-B-009 | legacy `"1.0"` path if enabled | frozen legacy validation; no rewrite |
| PRO-B-010 | persisted provenance | all required formula/config/source versions resolvable |
| PRO-B-011 | snapshot read after later profile update | old snapshot unchanged |
| PRO-B-012 | stale TeamProfile lookup | cannot satisfy current match |
| PRO-B-013 | identity/sequence corrupted telemetry | Profile INELIGIBLE; any prior match contributions are retracted/replayed |
| PRO-B-014 | incomplete stream with independent terminal survival fact | survival consumer applies only if its coverage contract passes |
| PRO-B-015 | incomplete stream with uncertain absence count | count metric UNAVAILABLE |
| PRO-B-016 | initial match processing applies one dimension only | successful receipt exists only for AVAILABLE dimension |
| PRO-B-017 | same match later completes second dimension | second dimension applies once |
| PRO-B-018 | worker retry after one dimension receipt exists | existing dimension unchanged; newly available independent dimension still eligible |
| PRO-B-019 | late historical second-dimension evidence after newer observations | deterministic affected-dimension replay |
| PRO-B-020 | dimension replay service restart | same final dimension state/sampleCount |
| PRO-B-021 | same dimension key conflicting semantic result | conflict/quarantine; no overwrite |
| PRO-B-022 | formula migration creates distinct target lineage | target-lineage receipts do not collide with old-lineage receipts |
| PRO-B-023 | crash during retraction/replay | restart converges to same canonical profile |
| PRO-B-024 | duplicate invalidation processing | idempotent; no second semantic retraction/revision |
| PRO-B-025 | snapshot created before later invalidation | old snapshot immutable; new profileRevision produced; subsequent snapshot uses rebuilt revision |
| PRO-B-026 | stale contribution apply races match invalidation | precommit revalidation/CAS prevents resurrection |
| PRO-B-027 | two workers retract same observation | one semantic retraction |
| PRO-B-028 | one worker retracts NOISE while another applies independent SURVIVAL | final state preserves canonical valid source set; no lost independent update |
| PRO-B-029 | global invalidation after two dimensions applied | retry/transaction eventually retracts both and replays both affected dimensions |

---

# 51. Tests — Snapshot / AED Handoff

| ID | Scenario | Expected |
|---|---|---|
| PRO-S-001 | PRE_MATCH 4-player ACTIVE roster | deterministic VALID snapshot |
| PRO-S-002 | PRE_MATCH new COLD_START player | PARTIAL; 50 not observed mean |
| PRO-S-003 | missing profile | PARTIAL/PROFILE_MISSING |
| PRO-S-004 | mixed profile revisions compatible | exact revisions preserved |
| PRO-S-005 | unsupported profile formula | INVALID |
| PRO-S-006 | same roster different target match | different roster/snapshot context; no persistent team identity |
| PRO-S-007 | roster changes after snapshot | old snapshot invalid for current decision; new snapshot required |
| PRO-S-008 | phase boundary | provisional/final current-match evidence labeled |
| PRO-S-009 | PRE_MATCH no TeamProfile | snapshot still structurally buildable from Player profiles |
| PRO-S-010 | previous TeamProfile substitution | rejected |
| PRO-S-011 | raw TelemetryEvent inserted into snapshot | contract violation |
| PRO-S-012 | hidden Monster data inserted | contract violation |
| PRO-S-013 | source profile updated later | existing snapshot immutable |
| PRO-S-014 | same source revisions + same roster + same config | same semantic snapshot content |
| PRO-S-015 | COLD_START-only roster meanObservedScore | null with coverage 0 |
| PRO-S-016 | null/deferred scores | not averaged as zero |
| PRO-S-017 | current match survival before MATCH_ENDED | UNAVAILABLE |
| PRO-S-018 | roster identity order | independent of input list order; canonical sorted membership |
| PRO-S-019 | different profileRevision, same comparison key | valid aggregation; exact revisions preserved |
| PRO-S-020 | different Profile dimension semantic key | snapshot `INVALID / FORMULA_VERSION_CONFLICT` |
| PRO-S-021 | different Noise filter/config semantic version | Noise roster mean unavailable; snapshot invalid for semantic conflict |
| PRO-S-022 | attempt to omit incompatible Player from average | rejected; no selective dropping |
| PRO-S-023 | all Player profiles migrated to one target semantic key | aggregation valid subject to evidence sufficiency |
| PRO-S-024 | snapshot provenance claims common key while sources disagree | INVALID |
| PRO-S-025 | same semantic profiles with different lineage IDs | compatibility uses semantic key, not lineage ID |
| PRO-S-026 | snapshot S1 created before contribution retraction | S1 remains immutable historical artifact |
| PRO-S-027 | new snapshot after replay | references new profileRevision and excludes invalidated contribution |
| PRO-S-028 | attempt to reuse old snapshot after source profile revision changed by retraction | `SOURCE_PROFILE_REVISION_CHANGED`; build current snapshot; no past gameplay rewind |

---

# 52. Failure Matrix

| Failure | Processed-data result | Persistent update? | Snapshot effect | Diagnostic |
|---|---|---:|---|---|
| missing terminal event | affected metric UNAVAILABLE / match PENDING or INELIGIBLE by lifecycle | No affected final update | missing current evidence | `MISSING_MATCH_END` / metric reason |
| sequence gap | stream INCOMPLETE | only provably independent metrics | PARTIAL if relevant | `STREAM_INCOMPLETE` |
| invalid telemetry stream | match INELIGIBLE; retract any prior contributions from an earlier eligible interpretation | no new contribution; corrective retraction/replay if needed | INVALID if stale source was used | `TELEMETRY_INVALID` |
| match aborted | INELIGIBLE; no contribution may remain from that match | no new contribution; retract any prior contribution if one exists | post-match result excluded | `MATCH_ABORTED` |
| invalid normalization config | affected metric INVALID | No affected dimension | version invalid if required | config error |
| unsupported formula version | INELIGIBLE/metric invalid by scope | No | INVALID | `PROFILE_VERSION_UNSUPPORTED` |
| duplicate per-dimension processing | duplicate no-op for same semantic receipt | already once for that dimension observation | none | duplicate dimension receipt |
| out-of-order older match | replay plan | Yes, by deterministic rebuild not append | newer snapshots remain immutable historical | `REPLAY_TRIGGERED` |
| missing player profile | no fabricated profile evidence | n/a | PARTIAL | `PROFILE_MISSING` |
| COLD_START-only roster | valid structures, no observed mean | n/a | PARTIAL | `INSUFFICIENT_OBSERVED_PROFILE_EVIDENCE` |
| TeamPerformance incomplete | score null | TeamPerformance not made complete | AED input remains insufficient under v0 gate | `REQUIRED_COMPONENT_DEFERRED` |
| roster changed | no mutation of old snapshot | n/a | old snapshot unusable for current decision | `ROSTER_CHANGED` |
| profile revision missing | provenance unresolved | n/a | INVALID | `PROFILE_REVISION_UNRESOLVABLE` |
| snapshot source profileRevision superseded by retraction/replay | historical snapshot remains immutable; cannot be reused as current | n/a | build replacement snapshot | `SOURCE_PROFILE_REVISION_CHANGED` |
| current metric provisional | preserved with finality | no final Player profile update from provisional evidence | explicit PROVISIONAL | `CURRENT_MATCH_EVIDENCE_PROVISIONAL` |
| DB concurrency conflict | retry/replan | not until atomic success | none | revision conflict |
| late same-match dimension completion | apply new dimension once, or replay affected dimension if older than already-incorporated same-dimension observations | Yes, exactly once for new dimension | newer existing snapshots remain immutable; future snapshot sees rebuilt revision | `LATE_DIMENSION_COMPLETION` / `REPLAY_TRIGGERED` |
| same ProfileDimensionApplyKey + different payload | conflict/quarantine; no overwrite | No | INVALID if unresolved source used | `PROFILE_DIMENSION_APPLY_CONFLICT` |
| mixed profile revisions + equal comparison semantic key | compatible | n/a | aggregation allowed | exact revisions retained |
| mixed incompatible formula/config semantic keys | roster dimension aggregation invalid | No profile mutation | `INVALID / FORMULA_VERSION_CONFLICT` | comparison-key mismatch |
| incompatible player silently omitted from mean attempt | reject aggregation attempt | n/a | INVALID | `FORMULA_VERSION_CONFLICT` |
| predecessor lineage presented as current v1.1 semantic lineage | reject/migrate/rebuild; no silent continuation | No direct continuation | INVALID until supported target lineage exists | `PROFILE_VERSION_UNSUPPORTED` / migration required |
| contributing match becomes globally INELIGIBLE | mark all its currently CONTRIBUTING dimension observations RETRACTED and replay each affected ACTIVE dimension | corrective mutation/rebuild only | future snapshot uses new revision; old snapshots immutable | eligibility reason + retraction category |
| contributing metric becomes INVALID while match remains ELIGIBLE | retract/replay that dimension only | corrective mutation/rebuild only | future snapshot reflects corrected dimension | metric reason + `METRIC_BECAME_INVALID` category |
| duplicate invalidation notification | semantic no-op after first retraction | No second effect | none beyond existing corrected revision | duplicate retraction diagnostic |
| crash during retraction/replay | retry from durable observation/status + revision state | converges exactly | no partial state exposed as final | retry/replay diagnostic |
| stale worker applies after invalidation | precommit revalidation/CAS rejects stale plan | No stale contribution | none | stale plan/revision conflict |
| only observation in dimension retracted | replay empty set → 50/COLD_START/0 | corrective update | future snapshots show COLD_START | retraction reason |
| invalidated observation restoration request | v1.1 canonical INVALID source has no automatic recovery; require explicit future upstream correction contract | No automatic restore | unchanged corrected state | source remains invalid |
| historical replay evidence unavailable | affected dimension/lineage explicit INVALID/REBUILD_REQUIRED; never approximate | No fabricated score | snapshot INVALID if required | rebuild evidence missing |

---

# 53. Implementation Plan

```text
1. Audit M1-014 v0 migration decisions.
2. Bind Telemetry v1.1 event registry and legacy policy.
3. Implement MatchTelemetry ordered projection.
4. Implement Telemetry/Profile integrity gate.
5. Implement MatchProfileEligibility.
6. Implement MetricAvailability + MetricFinality.
7. Implement pure survival/noise MatchScore functions.
8. Implement config/version registry validation.
9. Freeze current Profile formula semantic identity as PROFILE_FORMULA_V1_1.
10. Implement ProfileDimensionApplyKey + durable per-dimension observation/receipt semantics.
11. Implement ContributionStatus = CONTRIBUTING | RETRACTED with audit-preserving immutable payload.
12. Implement independent late same-match dimension completion.
13. Implement canonical per-dimension match ordering.
14. Implement source match/metric invalidation detector.
15. Implement idempotent contribution retraction planner.
16. Implement precommit eligibility/metric/lineage revalidation for new contributions.
17. Implement dimension-local deterministic replay for addition/retraction and zero-observation COLD_START restoration.
18. Implement crash/retry-safe atomic contribution-status + replay/profileRevision commit.
19. Implement PlayerAIProfile lineage persistence.
20. Implement predecessor → v1.1 formula-lineage migration validation/rebuild.
21. Implement per-dimension ProfileDimensionComparisonKey resolution.
22. Implement PlayerProfileSnapshot with exact semantic provenance/current source revisions.
23. Implement roster cross-player semantic-compatibility validation.
24. Implement RosterProfileSummary only after compatible-key validation.
25. Implement TeamProfile match-scoped builder.
26. Implement TeamPerformance exact incomplete/complete rules.
27. Implement AdaptiveInputSnapshot with coherent per-dimension comparison-key provenance.
28. Implement roster/source-profile-revision validation/replacement.
29. Implement AED v1.1 handoff adapter after AED contract is approved.
30. Run PRO-DD-01..04 pure/backend/snapshot/integration regressions including invalidation races.
31. Profile backend processing/replay cost; evaluate checkpoint optimization only against exact replay equivalence.
32. Freeze numerical tuning/config later.
```

No additional runtime gameplay cadence is introduced; these are processed-data/backend dependency steps.


---

# 54. Migration Plan

## 54.1 M1-014 data/logic migration

- preserve dimension names;
- preserve survival/noise source concepts;
- preserve match-scoped TeamProfile;
- preserve four-component TeamPerformance topology;
- migrate current raw source to Telemetry `"1.1"`;
- introduce Profile lineage/version metadata;
- freeze logical current Profile semantic identity `PROFILE_FORMULA_V1_1`;
- revise first-sample semantics;
- prevent predecessor blended-first-sample lineages from silently continuing under v1.1;
- migrate by deterministic rebuild into a new v1.1 lineage when sufficient history exists, otherwise create a genuine v1.1 COLD_START lineage with migration provenance;
- replace coarse match-level apply identity with durable `ProfileDimensionApplyKey` observation/receipt semantics;
- represent current contribution status (`CONTRIBUTING` / `RETRACTED`) without deleting immutable historical MatchScore payload;
- allow legitimate later same-match completion of previously unavailable independent dimensions;
- add source invalidation detection + idempotent retraction + canonical affected-dimension replay;
- add canonical per-dimension ordering/replay;
- add per-dimension comparison semantic keys before roster aggregation;
- add snapshot types with coherent semantic provenance.

## 54.2 Existing implementation evidence

Supplied M1-026 project snapshot contains backend:

```text
EchoProtocol.Api/Entities/PlayerProfile.cs
```

with account/statistics-style fields:

```text
Id
UserId
DisplayName
TotalMatches
TotalWins
CreatedAt
UpdatedAt
```

No supplied code evidence implements the v1.1 `PlayerAIProfile` dimensions, MatchScore calculators, TeamProfile, TeamPerformance, per-dimension apply ledger, ordered replay, RosterProfileSummary, or AdaptiveInputSnapshot.

Therefore:

```text
CURRENT IMPLEMENTATION:
PARTIAL NAME/ACCOUNT PROFILE ENTITY EXISTS
PROFILE v1.1 PROCESSED-DATA PIPELINE NOT EVIDENCED
```

| Current module | Evidence | KEEP / MODIFY / ADD | Target responsibility | Risk |
|---|---|---|---|---|
| backend `PlayerProfile.cs` | code: user/display name + TotalMatches/TotalWins | KEEP for account/statistics role; do not assume it is AI profile | general player/account stats | Medium if overloaded |
| `PlayerAIProfile` processed dimensions | docs only | ADD | persistent versioned AI profile lineage | High |
| MatchTelemetry/Profile aggregator | not evidenced | ADD | ordered validated aggregation | High |
| eligibility/availability evaluators | not evidenced | ADD | integrity gate | High |
| MatchScore calculators | not evidenced | ADD | pure survival/noise formulas | Medium |
| Profile dimension observation/apply/retraction ledger | not evidenced | ADD | exactly-once independent dimension application + audit-safe contribution status + deterministic dimension replay/retraction | High |
| TeamProfile builder | not evidenced | ADD | match-scoped team data | Medium |
| TeamPerformance calculator | contract only | ADD | exact incomplete/complete semantics | Medium |
| PlayerProfileSnapshot / RosterProfileSummary | not evidenced | ADD | decision-scoped processed input | Medium |
| AdaptiveInputSnapshot | not evidenced | ADD | Profile→AED handoff | High |

Do not mutate the existing general `PlayerProfile` entity into an overloaded God object without a deliberate backend data model binding. Exact persistence shape is an implementation decision.

---

# 55. Profile v1.1 Hard Invariants

1. Raw telemetry does not directly update AED.
2. Profile never commands Monster AI.
3. Missing data is never converted to zero.
4. DEFERRED is never represented as observed neutral performance.
5. COLD_START is distinguishable from observed score 50.
6. Cold-start initialization 50 does not influence the first observed v1.1 score.
7. `PROFILE_FORMULA_V1_1` is semantically distinct from predecessor first-sample blending semantics.
8. Profile document revision and Profile formula semantic version are different concepts.
9. An old incompatible formula lineage cannot silently continue receiving v1.1 observations; migration/rebuild or a new target lineage is required.
10. PENDING creates no new final contribution; INELIGIBLE observations cannot remain in the current derived PlayerAIProfile, and any earlier contributions from a match that becomes INELIGIBLE are retracted under PRO-DD-04.
11. An observation whose source is UNAVAILABLE/INVALID before first successful processing creates no successful new `ProfileDimensionApplyKey` receipt; if a previously CONTRIBUTING observation later becomes invalid, its existing audit identity remains and its status becomes RETRACTED.
12. A later legitimately AVAILABLE independent dimension from the same match may be incorporated exactly once.
13. Applying one dimension does not permanently block another independent dimension from the same match.
14. One logical observed MatchScore exists at most once per `(userId, matchId, profileLineageId, dimensionKey)`.
15. Backend retry cannot double-apply a dimension observation or increment its sampleCount twice.
16. Same dimension apply key + different semantic payload is a conflict; no overwrite.
17. Persistent profile result cannot depend on arbitrary backend arrival order.
18. Canonical cross-match order remains `(matchEndTs, matchId)`.
19. A late older dimension observation is replayed in canonical order, not appended by arrival order.
20. Dimension replay does not increment or recompute unrelated dimension sampleCount unless a shared semantic migration explicitly requires broader rebuild.
21. Replay reconstructs derived state from the same logical observation set; replay does not create duplicate observations.
22. Formula-incompatible scores are not silently mixed in one profile lineage.
23. `profileLineageId` is not a cross-player comparison semantic identity.
24. `profileRevision` is not a cross-player comparison semantic identity.
25. Direct cross-player aggregation requires exact equality of `ProfileDimensionComparisonKey` for that dimension.
26. RosterProfileSummary never averages numerically incomparable score semantics.
27. An incompatible Player cannot be silently dropped so the remaining roster can produce a mean.
28. Mixed profile revisions may aggregate when semantic comparison keys are equal.
29. Snapshot provenance cannot claim one common comparison key when source Player snapshots disagree.
30. Profile/config versions and comparison keys are traceable.
31. TeamProfile remains match-scoped.
32. Previous-match TeamProfile cannot be reused as current TeamProfile.
33. Same roster does not create persistent team identity.
34. AdaptiveInputSnapshot is decision-scoped and immutable.
35. PRE_MATCH does not require a fabricated current TeamProfile.
36. RosterProfileSummary is not a TeamProfile.
37. Snapshot contains processed evidence, not raw telemetry.
38. Snapshot contains no hidden Monster/player runtime state.
39. Null/deferred/missing/COLD_START players are never averaged as observed zero.
40. Teamwork is not inferred from action frequency without a frozen construct.
41. ResourceEfficiency is not inferred from tool count alone.
42. TeamPerformance is not forced COMPLETE to bypass AED gate.
43. Changing TeamPerformance required components changes its own formula version; this pass does not change that topology.
44. Metric availability and match eligibility remain distinct.
45. Telemetry schema-valid does not automatically mean Profile-eligible.
46. Telemetry stream completeness does not automatically decide every metric.
47. Telemetry wire schemaVersion remains `"1.1"`; legacy `"1.0"` remains frozen.
48. Experiment readiness remains owned by the full M1-020 gate.
49. A match that is currently Profile-INELIGIBLE contributes no observation to the current derived PlayerAIProfile.
50. If a previously contributing match later becomes INELIGIBLE, its existing contributions are deterministically retracted.
51. Global match invalidation retracts every currently CONTRIBUTING Player dimension observation from that match in each affected lineage.
52. Metric-only invalidation retracts only that metric/dimension observation while unrelated valid dimensions remain unchanged.
53. Retraction never uses inverse EMA arithmetic or a score-subtraction shortcut.
54. Retraction derives current state by canonical replay of remaining CONTRIBUTING observations.
55. A retracted observation remains auditable and cannot be recreated/double-counted as a new logical observation.
56. Duplicate invalidation cannot cause duplicate semantic retraction or duplicate profileRevision change.
57. Current dimension sampleCount counts current CONTRIBUTING observations, not historical processing attempts.
58. If retraction leaves zero CONTRIBUTING observations, the v1.1 dimension returns to 50/COLD_START/0 without creating evidence.
59. Retraction/replay of one dimension does not alter unrelated dimensions unless a separately required shared semantic migration applies.
60. A stale apply plan cannot resurrect an observation after its source becomes invalid.
61. Existing immutable AdaptiveInputSnapshot objects are never mutated by later Profile retraction.
62. Later Profile correction never rewinds previously executed gameplay or past ScenarioConfig/Monster behavior.
63. Backend worker arrival/race order cannot change the final canonical Profile result.
64. A corrected profile revision is not exposed as current with a partially retracted but unreplayed affected dimension set.
65. Canonical INVALID source interpretations do not automatically restore RETRACTED observations in v1.1; restoration requires an explicit upstream correction contract.

---

# 56. Definition of Done

A developer can answer:

| Question | v1.1 answer |
|---|---|
| Current telemetry schema? | `"1.1"` |
| Legacy telemetry? | `"1.0"` frozen validator; no rewrite |
| What makes match eligible? | §9–10 |
| INCOMPLETE telemetry? | metric-scoped degradation; not automatic whole-match rejection |
| INVALID telemetry? | match INELIGIBLE |
| UNKNOWN telemetry? | PENDING; no final update |
| Valid zero? | AVAILABLE observed zero |
| Null? | no valid score |
| ACTIVE Player dimensions? | survival, noise |
| DEFERRED Player dimensions? | remaining seven |
| Survival formula? | escape 100 / eliminated 0 |
| Noise formula? | filtered penalty count, higher-is-worse normalized |
| Cold start? | 50/COLD_START/0, initialization only |
| First valid update? | direct observed MatchScore, not EMA from 50 |
| Later EMA? | §20 |
| sampleCount? | applied observed dimension MatchScores |
| Duplicate apply prevention? | dimension-scoped ProfileDimensionApplyKey + durable receipt |
| Cross-match order? | `(matchEndTs, matchId)` |
| Late old observation? | deterministic affected-dimension replay in `(matchEndTs, matchId)` order |
| Profile formula semantic version? | current logical identity `PROFILE_FORMULA_V1_1`; distinct from predecessor blended-first-sample semantics |
| Version change? | new/rebuilt lineage; no silent mix |
| TeamProfile identity? | matchId only |
| Previous TeamProfile reuse? | forbidden |
| RosterProfileSummary? | decision-scoped observed-profile aggregate |
| Persistent? | No |
| AdaptiveInputSnapshot? | immutable decision-scoped processed package |
| PRE_MATCH source? | current roster + frozen Player profile snapshots |
| Phase-boundary source? | processed current evidence with finality |
| Teamwork ACTIVE? | No |
| ResourceEfficiency ACTIVE? | No |
| TeamPerformance topology changed? | No |
| TeamPerformance COMPLETE now? | No |
| Does Profile v1.1 make Adaptive ready? | No |
| SC-03 Profile side? | resolved |
| AED side? | v1.1 revision required |
| Reproducibility versions? | §44 |
| Processing idempotency tests? | PRO-E/B |
| Ordering tests? | PRO-E-026/027 + PRO-B-006/007 |
| Stale team reuse tests? | PRO-E-032 / PRO-S-010 / PRO-B-012 |
| Is profile age itself a score? | No; freshness is separate metadata/policy |
| Can COLD_START enter observed roster mean? | No |
| Snapshot ID deterministic? | unique construction ID; deterministic content fingerprint |
| Roster identity includes profile revisions? | No; membership identity is targetMatchId + sorted userIds; revisions are separate provenance |
| Legacy v1.0 rewritten as v1.1? | No |
| Can legacy MatchScores silently mix into v1.1 lineage? | No |
| TeamPerformance formula topology changed? | No |
| Does document revision alone force TeamPerformance formula token change? | No |
| Can Survival receipt block later Noise from same match? | No; receipts are dimension-scoped |
| Does UNAVAILABLE create successful apply receipt? | No |
| Late same-match Noise after newer Noise? | replay NOISE only in canonical order; do not append |
| Cross-player compatibility owner? | per-dimension `ProfileDimensionComparisonKey` |
| Same profileRevision required to aggregate? | No |
| Same profileLineageId required to aggregate? | No |
| Mixed comparison keys? | no mean; snapshot INVALID / FORMULA_VERSION_CONFLICT |
| Can incompatible Player be silently dropped? | No |
| Can v0 blended-first-sample lineage silently continue as v1.1? | No; migrate/rebuild/new lineage |
| Can an INCOMPLETE but independently provable match contribute? | Yes, only valid AVAILABLE dimensions |
| Can that match later become globally INELIGIBLE? | Yes, if later canonical integrity/provenance evidence establishes invalidity |
| What happens to already-incorporated observations? | logically RETRACTED; affected dimensions replay from remaining CONTRIBUTING set |
| Do we reverse EMA mathematically? | No |
| Does global match invalidation retract every dimension? | every dimension to which that match currently contributes in the affected lineage |
| Does metric-only Noise invalidation retract Survival? | No, while match remains globally eligible |
| Does a RETRACTED observation disappear from audit history? | No |
| Does a RETRACTED observation count in current sampleCount? | No |
| What if retraction leaves no observations? | 50 / COLD_START / sampleCount 0 |
| Can duplicate invalidation double-retract? | No; semantic no-op after first transition |
| Can old immutable snapshots be edited? | No; future snapshot binds corrected profileRevision |
| Does Profile correction rewind past gameplay? | No |
| Current processed Profile implementation evidenced? | No; only general backend PlayerProfile account/statistics entity is evidenced |

All P0 Profile semantics required by this contract are resolved.

---

# 57. Open TBDs

## 57.1 TUNING / POLICY TBD

- numerical alpha values;
- Player Noise Min/Max;
- ObjectiveTime Min/Max;
- TeamPerformance numerical weights;
- profile freshness duration if AED uses freshness;
- minimum evidence/sample threshold for adaptive use, owned by AED v1.1 unless explicitly moved;
- research acceptance thresholds.

## 57.2 IMPLEMENTATION BINDING TBD

- database tables/indexes;
- transaction/CAS technology;
- cache;
- job scheduler;
- exact DTO/C# class names;
- profile lineage ID representation;
- `ProfileDimensionApplyKey` database/table/index encoding;
- per-dimension observation/receipt table/transaction representation;
- tombstone vs contribution-status column/event representation;
- retraction transaction/CAS primitive;
- queue/worker technology;
- replay checkpoint optimization representation;
- semantic fingerprints;
- exact persisted encoding of logical `PROFILE_FORMULA_V1_1`;
- `ProfileDimensionComparisonKey` hash/serialization representation;
- snapshot ID/hash representation;
- roster identity hash representation.

## 57.3 Not allowed as TBD

- eligibility semantics;
- MetricAvailability semantics;
- zero vs null;
- Player ACTIVE/DEFERRED set;
- survival/noise formula topology;
- first-sample semantics;
- v1.1 vs predecessor Profile formula semantic distinction;
- dimension-scoped application identity;
- late same-match dimension completion semantics;
- duplicate/conflicting dimension application behavior;
- dimension-local replay semantics;
- contribution invalidation/retraction lifecycle;
- global vs metric-scoped invalidation semantics;
- current sampleCount after retraction;
- zero-observation COLD_START restoration;
- retraction idempotency;
- controlled retraction category + canonical source-reason reference ownership;
- profileRevision behavior for semantic contribution-set changes vs duplicate no-op;
- stale-worker/precommit behavior;
- snapshot immutability after retraction;
- idempotency;
- canonical cross-match order;
- late-match policy;
- formula/version compatibility;
- cross-player comparison-key ownership/equality rule;
- incompatible-roster aggregation behavior;
- TeamProfile identity;
- stale TeamProfile reuse;
- PRE_MATCH source;
- snapshot ownership/immutability;
- Teamwork activation decision;
- ResourceEfficiency activation decision;
- TeamPerformance completeness;
- Profile → AED authority boundary.

---

# 58. Architecture Escalations

```text
ARCHITECTURE ESCALATION REQUIRED: NO
```

This contract preserves:

```text
Gameplay
→ Telemetry
→ Aggregation/Profile
→ AED
→ ScenarioConfig
```

and:

```text
no persistent team identity
no direct Profile → Monster runtime command
```

Profile-side SC-03 is solved inside the existing architecture by a decision-scoped roster aggregate and immutable adaptive input boundary.

The required AED v1.1 input-lifecycle revision is a downstream detailed-contract update, not a top-level architecture escalation.

---

# 59. Profile Contract Validation

```text
Telemetry v1.1 migration complete: YES
Match eligibility exact: YES
MetricAvailability exact: YES
Zero/null distinction exact: YES

Player dimension validity audit complete: YES
Player ACTIVE/DEFERRED set explicit: YES
Survival formula complete: YES
Noise formula complete: YES
Cold-start semantics complete: YES
First-sample semantics complete: YES
EMA semantics complete: YES

Profile update idempotency complete: YES
PRO-DD-01 dimension-scoped apply identity resolved: YES
Independent late same-match dimension completion resolved: YES
Unavailable dimension creates no successful apply receipt: YES
Late dimension canonical replay resolved: YES
Dimension-local replay semantics exact: YES
Cross-match ordering deterministic: YES
Late-match handling complete: YES
Formula-version compatibility complete: YES

PRO-DD-02 Player dimension comparison key resolved: YES
Cross-player semantic compatibility exact: YES
Mixed profile revisions with same semantics allowed: YES
Mixed incompatible semantics rejected: YES
Incompatible players cannot be silently dropped: YES
Snapshot provenance semantic coherence exact: YES

PRO-DD-03 v1.1 Profile formula semantic identity explicit: YES
v1.1 first-sample version distinct from predecessor semantics: YES
Old lineage cannot silently continue under v1.1 semantics: YES
Migration/rebuild behavior exact: YES

PRO-DD-04 contribution invalidation lifecycle resolved: YES
Global match invalidation retraction exact: YES
Metric-scoped invalidation retraction exact: YES
ContributionStatus semantics exact: YES
Historical observation audit preservation exact: YES
Current sampleCount after retraction exact: YES
Zero-observation COLD_START restoration exact: YES
Retraction idempotency exact: YES
Retraction replay canonical: YES
Inverse EMA forbidden: YES
Dimension-local retraction exact: YES
Global multi-dimension retraction exact: YES
Precommit eligibility revalidation exact: YES
Stale worker resurrection prevented: YES
Snapshot-after-retraction semantics exact: YES
Correction commit visibility exact: YES

PRO-DD-01 remains resolved: YES
PRO-DD-02 remains resolved: YES
PRO-DD-03 remains resolved: YES

TeamProfile identity exact: YES
No cross-match TeamProfile reuse: YES
Teamwork activation decision exact: YES
ResourceEfficiency activation decision exact: YES
TeamPerformance topology/version exact: YES
TeamPerformance completeness exact: YES

PRE_MATCH Profile-side SC-03 resolved: YES
Decision-scoped roster aggregation exact: YES
AdaptiveInputSnapshot schema exact: YES
Snapshot immutability exact: YES
Roster-change behavior exact: YES
AED v1.1 handoff exact: YES

No synthetic Adaptive readiness: YES
Reproducibility metadata complete: YES
Test plan complete: YES

Telemetry wire remains "1.1": YES
TeamProfile remains match-scoped: YES
TeamPerformance remains honest INCOMPLETE/null: YES
Profile-side SC-03 remains resolved: YES
AED-side revision remains required: YES
Live Adaptive execution remains NOT READY: YES
Architecture escalation required: NO
```

---

# 60. Final Consistency Audit

```text
Raw TelemetryEvent can directly command AED: NO
Profile can directly command Stalker/Listener/Warden: NO
Missing metric becomes zero: NO
DEFERRED dimension becomes score 50: NO
COLD_START score 50 is indistinguishable from measured 50: NO
COLD_START initialization affects first observed v1.1 MatchScore: NO
Aborted match updates persistent profile: NO
UNKNOWN/live match updates persistent profile: NO
Same MatchScore retry can apply EMA twice: NO
Backend arrival order can arbitrarily change PlayerAIProfile: NO
Late older match is appended after newer match: NO
Incompatible formula versions can silently share one EMA lineage: NO
Previous TeamProfile can fill current-match missing fields: NO
Same player roster creates a persistent Team identity: NO
PRE_MATCH fabricates a current TeamProfile: NO
RosterProfileSummary is persisted as TeamProfile: NO
AdaptiveInputSnapshot contains raw telemetry: NO
AdaptiveInputSnapshot contains hidden Monster state: NO
Null Player scores are averaged as zero: NO
COLD_START is treated as observed evidence in meanObservedScore: NO
TEAM_TOOL_USED count automatically means high Teamwork: NO
TEAM_TOOL_USED count alone automatically means ResourceEfficiency: NO
Missing TeamPerformance components are silently renormalized: NO
TeamPerformance is made COMPLETE only to enable Adaptive: NO
Research-capture Monster/Warden telemetry automatically becomes Player Profile input: NO
Telemetry INCOMPLETE automatically forces every unrelated Profile metric unavailable: NO
Profile v1.1 alone proves Adaptive experiment READY: NO
Does this require Architecture v1.2: NO

Can applying Survival for one match permanently prevent a later valid Noise score from the same match being incorporated: NO
Can an UNAVAILABLE dimension create a successful apply receipt: NO
Can the same dimension MatchScore be incorporated twice due to retry: NO
Can a late older Noise observation be appended after newer Noise observations: NO
Can replay of Noise increment Survival sampleCount: NO
Can two Player scores be averaged merely because both versions are individually supported: NO
Can players with incompatible score semantics be silently excluded so the remaining roster can still produce a mean: NO
Does profileLineageId itself define cross-player score comparability: NO
Can two different profileRevision values still be semantically compatible: YES
Can v0 and v1.1 first-sample formula semantics share the same semantic Profile formula identity: NO
Can an old Profile lineage silently continue receiving v1.1 observations without migration/rebuild: NO
Does fixing these issues require Telemetry v1.2: NO
Does fixing these issues require Architecture v1.2: NO

Can a currently INELIGIBLE match remain in the current derived PlayerAIProfile because it contributed earlier: NO
Can a globally invalidated match keep its old Survival EMA contribution: NO
Can a metric-only invalidation remove unrelated dimension contributions: NO
Can a contribution be removed by simply decrementing sampleCount without replaying score: NO
Can Profile attempt inverse EMA rollback: NO
Can a RETRACTED observation disappear from audit history: NO
Can a RETRACTED observation still count in current sampleCount: NO
Can duplicate retraction alter the derived Profile twice: NO
Can a stale worker restore a contribution after global invalidation: NO
Can a previously created AdaptiveInputSnapshot be mutated after retraction: NO
Does correction of Profile history retroactively change already executed gameplay: NO
Does PRO-DD-04 require Telemetry v1.2: NO
Does PRO-DD-04 require Architecture v1.2: NO
```

All expected audit answers hold.

# 61. References

## 61.1 Project sources

1. `AI_Architecture_v1.1.md` — top-level AI authority, Profile/AED boundary, decision-scoped adaptive input requirement.
2. `Telemetry_Contract_v1.1.md` — current wire `"1.1"`, ordering, provenance, event contracts, completeness, Profile input coverage.
3. `M1-014_Player_Team_Profile_Fields_Formulas_v0_FINAL.md` — predecessor processed-data contract.
4. `M1-015_ScenarioConfig_AED_Fairness_Policy_v0_FINAL.md` — downstream AED input/fallback predecessor.
5. `M1-020_Test_Strategy_Fixed_vs_Adaptive_Experiment_v0_FINAL.md` — readiness, reproducibility, SC-03, experiment eligibility.
6. `Stalker_AI_Design_v1.1.md`, `Listener_AI_Design_v1.0.md`, `Warden_AI_Design_v1.0.md` — only where Telemetry v1.1 persists approved research facts; none are used to invent Player Profile constructs.
7. Supplied M1-026 project snapshot — implementation evidence; current backend `PlayerProfile.cs` is not the processed `PlayerAIProfile` contract.

## 61.2 Methodological context already reviewed by the parent architecture

- Hunicke, LeBlanc, Zubek — *MDA: A Formal Approach to Game Design and Game Research*.
- Yannakakis & Hallam — *Real-Time Game Adaptation for Optimizing Player Satisfaction*.
- Zohaib — *Dynamic Difficulty Adjustment (DDA) in Computer Games: A Review*.
- Spronck et al. — *Adaptive Game AI with Dynamic Scripting*.

These sources support measurable, controlled, reproducible adaptation. They do not supply ECHO PROTOCOL gameplay semantics or justify synthetic metrics.

---

# Final Status

```text
Document Revision: v1.1
Recommended Status: BASELINED v1.1
Architecture Escalation Required: NO

PRO-DD-01: RESOLVED
PRO-DD-02: RESOLVED
PRO-DD-03: RESOLVED
PRO-DD-04: RESOLVED

Profile-side SC-03: RESOLVED
AED-side consumption revision: REQUIRED

TeamPerformance.status: INCOMPLETE
TeamPerformance.score: null

Live Adaptive execution: NOT READY
```

The strongest defensible Profile v1.1 contract is therefore baselined without inventing Teamwork, ResourceEfficiency, persistent team identity, or synthetic Adaptive readiness.
