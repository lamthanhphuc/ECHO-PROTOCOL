# ECHO PROTOCOL — Fixed vs Adaptive Experiment Contract v1.1

**Canonical document:** `Fixed_vs_Adaptive_Experiment_Contract_v1.1.md`  
**Project:** ECHO PROTOCOL — Co-op Survival Horror Multiplayer  
**Document Revision:** `v1.1`  
**Experiment Protocol Semantic ID:** logical `FIXED_ADAPTIVE_EXPERIMENT_PROTOCOL_V1_1`  
**Parent Architecture:** `AI_Architecture_v1.1.md` — **BASELINED v1.1**  
**Upstream Telemetry:** `Telemetry_Contract_v1.1.md` — **BASELINED v1.1**, wire `schemaVersion = "1.1"`  
**Upstream Profile:** `Player_Team_Profile_Contract_v1.1.md` — **BASELINED v1.1**  
**Upstream AED / ScenarioConfig:** `AED_ScenarioConfig_Contract_v1.1.md` — **BASELINED v1.1**  
**AED Policy Semantic ID:** `AED_SCENARIO_POLICY_V1_1`  
**Historical predecessor:** `M1-020_Test_Strategy_Fixed_vs_Adaptive_Experiment_v0_FINAL.md`  
**Recommended Status:** **BASELINED v1.1**  
**Architecture Escalation Required:** **NO**  
**Contract Design:** **COMPLETE**  
**Experiment Implementation:** **NOT COMPLETE / NOT EVIDENCED FROM SUPPLIED SOURCE**  
**Tuning Freeze:** **NOT COMPLETE**  
**Analysis Plan Freeze:** **NOT COMPLETE**  
**Main Fixed-vs-Adaptive Experiment Execution:** **NOT READY**
**Surgical Correction Pass:** **EXP-DD-01 through EXP-DD-05 resolved in-place; document revision remains v1.1**

> This document is an experiment/research protocol contract. It is not an experiment result, does not claim implementation completion, and does not claim that Adaptive is better than Fixed. It migrates the experiment-specific semantics of M1-020 v0 onto the current v1.1 Telemetry → Profile → AdaptiveInputSnapshot → AED → ScenarioConfig pipeline without reopening gameplay architecture.

---

# 1. Document Control

| Field | Contract |
|---|---|
| Research factor | Assigned Scenario Resolution condition: `FIXED` vs `ADAPTIVE` |
| Condition F | `experimentCondition = FIXED`; `ScenarioResolutionMode = FIXED` |
| Condition A | `experimentCondition = ADAPTIVE`; `ScenarioResolutionMode = ADAPTIVE`; AED policy `AED_SCENARIO_POLICY_V1_1` |
| Assignment truth | Immutable assignment-time `ExperimentAssignmentRecord`; later period→match identity is owned by append-only `ExperimentPeriodBindingRecord`; `MATCH_STARTED.experimentCondition + experimentProtocolVersion` is authoritative occurrence evidence |
| Experimental unit | `team-match` |
| Paired unit | same canonical roster completing one F period and one A period under one `ExperimentPair`; one stable player `userId` may belong to at most one MAIN confirmatory pair per `experimentRunId` |
| Design | two-period, two-sequence, counterbalanced within-roster repeated-measures design |
| Sequence classes | `F→A`, `A→F`; frozen schedule length = `plannedPairSlots`; assigned slots are consumed and never recycled |
| MAIN enrollment commit | assignment record + schedule-slot ownership + complete same-run participant membership are one idempotent logical atomic commit; period→match binding remains separate |
| Classical no-carryover assumption | **NOT MADE** |
| Persistent Player Profile updates | remain enabled normally |
| Primary estimand | assigned-condition difference, not realized `configSource` difference |
| Confirmatory primary endpoint | Team Survival, as defined in §35 |
| Objective completion | secondary only after a versioned MatchOutcome→ObjectiveCompletion mapping is available |
| `objectiveTime` | conditional secondary among source-valid completed objective-bearing phase pairs |
| TeamPerformance | remains `INCOMPLETE`, `score = null`; never synthesized |
| Adaptive exposure | secondary/exploratory APPLIED / NO_CHANGE / FIXED_FALLBACK evidence plus deterministic experiment-owned fallback classification from the upstream AED reason |
| Main-study sample size | `TBD BEFORE MAIN STUDY` |
| Main statistical model/test | `TBD BEFORE MAIN STUDY` under a frozen `analysisPlanVersion` |
| Experiment runtime authority | none; experiment layer does not command gameplay |
| ScenarioConfig apply authority | Host / Fusion State Authority through the AED/ScenarioConfig contract |
| Persistent gameplay team identity | forbidden |
| Main experiment current state | `NOT READY` |

## 1.1 Statement classification

| Label | Meaning |
|---|---|
| **UPSTREAM BASELINE** | Directly required by a current baselined v1.1 contract. |
| **KEEP** | M1-020 v0 decision remains valid. |
| **MODIFY** | predecessor decision remains but requires v1.1 semantic refinement. |
| **SUPERSEDE** | predecessor semantic is replaced by current v1.1 upstream semantics. |
| **DEFER** | not activated because source/implementation/evidence is insufficient. |
| **REMOVE** | predecessor concept is intentionally not carried forward. |
| **EXPERIMENT v1.1 DECISION** | experiment-level design decision introduced here. |
| **TBD BEFORE MAIN STUDY** | numerical/statistical choice that must be frozen before main collection. |
| **IMPLEMENTATION BINDING TBD** | storage/class/hash/RNG technology may vary without changing protocol semantics. |
| **BLOCKS MAIN EXPERIMENT** | unresolved execution prerequisite; does not necessarily block document baseline. |

---

# 2. Purpose

This contract defines how ECHO PROTOCOL will compare the fixed ScenarioConfig path with the bounded adaptive ScenarioConfig path under the current v1.1 architecture.

Canonical research pipeline:

```text
Gameplay
→ Telemetry v1.1
→ Profile v1.1
→ AdaptiveInputSnapshot
→ AED_SCENARIO_POLICY_V1_1
→ ScenarioConfig
→ authoritative gameplay
→ Telemetry / processed outcomes
→ experiment eligibility
→ immutable analysis dataset
→ frozen analysis plan
→ research report
```

The document answers:

- what F and A mean;
- how sequence assignment is generated and frozen;
- how MAIN enrollment commits assignment, slot ownership, and participant membership atomically under concurrency/crash/retry;
- why `configSource` is not treatment assignment;
- how an A-assigned fallback is classified;
- what the experimental and paired units are;
- how persistent Profile evolution affects the design;
- which endpoint is confirmatory;
- how missing/invalid evidence is handled;
- how matches and pairs are excluded;
- how actual Adaptive exposure is reported;
- what versions must remain homogeneous;
- what data is retained for replay;
- what must be frozen before main collection;
- what technical failures suspend a run;
- when experiment execution can become READY.

---

# 3. Scope and Non-Goals

## 3.1 In scope

- migration of experiment semantics from M1-020 v0 to v1.1;
- run-level readiness;
- match-level experiment eligibility;
- same-roster F/A pairing;
- sequence allocation;
- assignment provenance;
- condition and exposure classification;
- outcome validity;
- missingness and exclusion topology;
- pilot/main separation;
- version/run freeze;
- dataset rebuild and cutoff;
- reproducibility;
- statistical-analysis freeze requirements;
- experiment implementation components;
- atomic MAIN enrollment, conflict, concurrency, crash/retry semantics;
- experiment-specific pre-run and data tests.

## 3.2 Non-goals

This contract does not:

- repeat the complete subsystem QA strategy from M1-020 v0;
- redesign AED;
- change `AED_SCENARIO_POLICY_V1_1`;
- change Telemetry wire `"1.1"`;
- change Profile formulas;
- activate Teamwork or ResourceEfficiency;
- fabricate TeamPerformance;
- change Stalker, Listener, or Warden runtime semantics;
- create a persistent TeamProfile/Party identity;
- turn experiment assignment into gameplay authority;
- add new production telemetry events by default;
- select a sample size without evidence;
- choose a statistical test after observing main outcomes;
- create experiment results.

Subsystem correctness remains governed by the owning contracts. This document defines the evidence gates proving those systems are fit for main experiment use.

---

# 4. Source Priority

Project-semantic precedence:

```text
1. AI_Architecture_v1.1.md
2. Telemetry_Contract_v1.1.md
3. Player_Team_Profile_Contract_v1.1.md
4. AED_ScenarioConfig_Contract_v1.1.md
5. approved gameplay/map/objective/network contracts
6. approved Stalker/Listener/Warden detailed designs where relevant
7. M1-020 v0 historical predecessor
8. current implementation/test evidence
9. academic/technical literature for experiment/statistical methodology
```

Rules:

```text
current baselined v1.1 contract
>
M1 predecessor
```

```text
actual measurable project evidence
>
desired research conclusion
```

```text
predeclared analysis
>
post-hoc significance seeking
```

Academic literature may guide randomization, repeated-measures analysis, period/order effects, missing data, endpoint hierarchy, sample-size planning, uncertainty reporting, and pilot/main separation. It does not define ECHO PROTOCOL gameplay.

---

# 5. M1-020 v0 → Experiment v1.1 Validity Review

| v0 decision | v1.1 classification | v1.1 contract |
|---|---|---|
| Research question | KEEP / refine | compare assigned Fixed vs Adaptive under frozen gameplay/content; non-directional |
| Condition F | MODIFY | assignment is `FIXED` + requested FIXED resolution; not inferred from `configSource` |
| Condition A | MODIFY | assignment is `ADAPTIVE` + requested ADAPTIVE resolution; APPLIED/NO_CHANGE/FALLBACK are realized exposure |
| TeamPerformance COMPLETE readiness gate | **SUPERSEDE** | use current `AdaptiveInputSnapshot` + AED v1.1 evidence gate |
| H0/H1 non-directional | KEEP | no a-priori “Adaptive is better” claim |
| Primary experimental unit = team-match | KEEP | Player rows are nested |
| Player-match observations | KEEP | valid nested data, not independent replicates |
| within-team crossover wording | MODIFY | two-period counterbalanced within-roster repeated-measures; no washout/no-carryover assumption |
| F→A / A→F | KEEP | exact two sequence classes |
| randomization mechanism | MODIFY / resolve | use versioned balanced randomized pre-generated schedule of exactly `plannedPairSlots`; assigned slots are never recycled |
| stable roster for pair | KEEP / strengthen | exact same canonical roster required for confirmatory pair; replacement roster requires a new pair + unused slot |
| normal Profile evolution | KEEP / strengthen | record it; never freeze Profile for experiment convenience |
| old primary metric set | MODIFY | Team Survival confirmatory; Objective Completion secondary when mapping available; objectiveTime conditional secondary |
| secondary metrics | KEEP only where current source exists | no invented/deferred outcomes |
| safety/fairness metrics | KEEP | remain non-negotiable safety evidence |
| data quality metrics | KEEP / update | use Telemetry/Profile v1.1 quality semantics |
| questionnaire | DEFER / optional | requires approved instrument before use |
| assigned-condition primary analysis | KEEP / strengthen | assignment is analysis truth |
| fallback | KEEP / clarify | every A fallback remains A-assigned, but upstream AED reason deterministically controls experiment eligibility/run handling |
| sample size | KEEP TBD | must be resolved by a pre-main planning step |
| statistical test | KEEP TBD | must be frozen in `analysisPlanVersion` before main |
| pilot/main separation | KEEP / strengthen | tuning/planning pilot excluded by default from confirmatory main |
| version/reproducibility | KEEP / expand | include current Profile/AED/evidence/registry versions |
| live experiment readiness | MODIFY | no longer blocked by synthetic TeamPerformance requirement; still blocked by implementation/tuning/analysis evidence |

## 5.1 Critical migration

The obsolete predecessor gate:

```text
TeamPerformance.status = COMPLETE
AND
TeamPerformance.score != null
```

is not valid for AED v1.1.

Current policy consumes:

```text
AdaptiveInputSnapshot
+
full-roster observed SURVIVAL
+
full-roster observed NOISE
+
AEDEvidencePolicy
+
supported/current Profile semantics
```

This contract never reconstructs a synthetic TeamPerformance.

---

# 6. Architecture / Research Boundary

```text
Experiment protocol
→ assigns condition
→ records evidence
→ determines analysis eligibility
→ builds research dataset

Experiment protocol
X→ mutates Monster runtime state
X→ writes Player Profile
X→ bypasses AED/ScenarioValidator
X→ applies ScenarioConfig directly
```

Condition assignment controls which existing Scenario resolution path is requested. Runtime application remains owned by the authoritative gameplay/AED boundary.

---

# 7. Experiment Protocol Semantic Identity

Canonical logical identity:

```text
FIXED_ADAPTIVE_EXPERIMENT_PROTOCOL_V1_1
```

Exact serialized token format is IMPLEMENTATION BINDING TBD.

Hard rule:

```text
M1-020-v0 protocol semantics
!=
FIXED_ADAPTIVE_EXPERIMENT_PROTOCOL_V1_1 semantics
```

because the Adaptive eligibility gate, Profile input boundary, and analysis treatment of persistent Profile evolution have materially changed.

`experimentProtocolVersion` owns:

- F/A assignment semantics;
- two-period design;
- sequence allocation topology;
- pair identity;
- readiness versus match eligibility distinction;
- expected fallback classification;
- match/metric/pair inclusion topology;
- assigned-condition estimand;
- pilot/main separation;
- run freeze rules.

It does not own AED policy rules or statistical model implementation.

---

# 8. Version Concepts Are Distinct

```text
experimentProtocolVersion
!=
analysisPlanVersion
!=
policyVersion
!=
policyConfigVersion
!=
evidencePolicyVersion
!=
parameterRegistryVersion
!=
scenarioConfigVersion
```

Document revision is also not a runtime semantic version.

---

# 9. Research Questions

## 9.1 RQ1 — Confirmatory performance question

> Under the frozen ECHO PROTOCOL experiment scope, is assigned ADAPTIVE resolution associated with a different Team Survival outcome from assigned FIXED resolution in the two-period same-roster design?

This is deliberately non-directional.

## 9.2 RQ2 — Secondary gameplay-performance questions

Where valid source evidence exists:

- do Match Duration, objective completion, Down Count, Revive Count, player survival, or other approved secondary metrics differ by assigned condition?
- among matches where the source-defined objectiveTime is observable, how does conditional objectiveTime differ?

## 9.3 RQ3 — Mechanistic Adaptive exposure

Exploratory:

- how often does A assignment produce `APPLIED`, `NO_CHANGE`, or `FIXED_FALLBACK`?
- which registered keys actually change?
- what fallback reasons dominate?

## 9.4 RQ4 — Player-perceived experience

Optional/exploratory only if an instrument is separately approved and frozen before use.

Telemetry alone cannot establish a subjective experience effect.

---

# 10. Hypotheses

For the confirmatory endpoint:

```text
H0:
No assigned-condition difference exists in the frozen confirmatory endpoint
under the pre-specified analysis plan.

H1:
An assigned-condition difference exists in the frozen confirmatory endpoint
under the pre-specified analysis plan.
```

Forbidden pre-result claim:

```text
Adaptive is better than Fixed
```

No directional superiority hypothesis is introduced by this contract.

---

# 11. Three Different Concepts

Freeze:

```text
Experiment Condition Assignment
!=
ScenarioResolutionMode
!=
AppliedScenarioConfig.configSource
```

Canonical mapping:

```text
Condition F
→ experimentCondition = FIXED
→ ScenarioResolutionMode = FIXED
```

```text
Condition A
→ experimentCondition = ADAPTIVE
→ ScenarioResolutionMode = ADAPTIVE
```

Realized A behavior may be:

```text
ADAPTIVE + APPLIED
ADAPTIVE + NO_CHANGE
ADAPTIVE + FIXED_FALLBACK
```

All remain assigned condition A.

---

# 12. Authoritative Assignment Source

Assignment is made by the experiment assignment service/ledger before the relevant outcome is known.

The canonical occurrence evidence at match start is:

```text
MATCH_STARTED.context.experimentCondition
MATCH_STARTED.context.experimentProtocolVersion
```

when the experiment is active.

Required provenance includes the current Telemetry v1.1 match-start fields and available experiment references:

- `matchId`;
- `experimentCondition`;
- `experimentProtocolVersion`;
- `experimentRunId` when active;
- `buildVersion`;
- `mapId`;
- `mapContentVersion`;
- `scenarioConfigVersion`;
- `policyVersion`;
- `configSource`;
- `teamSize`;
- `contentWhitelistVersion`;
- `researchCaptureEnabled`;
- `parameterRegistryVersion` when available;
- `fixedBaselineId` when relevant.

Telemetry is evidence of the authoritative assignment; Telemetry does not choose or change it.

---

# 13. Assignment Immutability and Period-Binding Lifecycle

Assignment-time facts and future match identity are different lifecycle domains.

Canonical assignment sequence:

```text
candidate MAIN roster
→ advisory/preliminary enrollment checks may run
→ SequenceAssignmentService proposes the next UNUSED scheduleSlotId
→ MainPairEnrollmentCommit revalidates authoritative commit-time preconditions
→ one logical atomic commit establishes together:
     immutable ExperimentAssignmentRecord
     scheduleSlotId → pairId, state = CONSUMED
     every (experimentRunId,userId) participant membership → pairId
→ Period 1 condition and Period 2 condition are fixed
→ only then may later match IDs be bound through separate period-binding records
```

The preliminary checks are not the uniqueness authority. Participant, slot, pair identity, roster identity, sequence, and run/schedule state are revalidated at the authoritative enrollment commit boundary defined in §22.4.

Hard invariant:

```text
Assignment immutability
!=
period-to-match binding lifecycle
```

An `ExperimentAssignmentRecord` is immutable after successful commit. It is never mutated to append future match IDs.

After assignment:

```text
outcome observed
X→ relabel sequence
X→ swap condition to improve balance
X→ move poor result to other arm
X→ replace consumed scheduleSlotId
X→ append period1MatchId/period2MatchId into AssignmentRecord
```

A correction for proven assignment-ledger corruption must create explicit audit/correction evidence under research governance; it cannot silently overwrite the committed assignment and can never be outcome-driven.

## 13.1 Period binding

A period match enters experiment identity only after an authoritative `matchId` exists.

Canonical order:

```text
authoritative matchId allocated
→ ExperimentPeriodBindingRecord commits (pairId, periodIndex, matchId)
→ binding is validated against immutable assignment
→ MATCH_STARTED occurs with matching experimentCondition + experimentProtocolVersion
```

The binding commit may be implemented immediately before or atomically with match-start admission, but the semantic key is fixed:

```text
(experimentRunId, pairId, periodIndex)
→ at most one authoritative matchId
```

Retry semantics:

```text
same semantic key + same matchId
→ idempotent same binding
```

Conflict semantics:

```text
same semantic key + different matchId
→ PERIOD_BINDING_IDENTITY_CONFLICT
→ no overwrite
→ no second authoritative binding
```

If a bound Period-2 match never starts or never reaches usable terminal evidence, the assignment and binding remain immutable historical evidence; the pair may remain incomplete/excluded. No rebind and no slot recycling occurs.

---

# 14. Experiment Records

Minimum logical records:

```text
ExperimentRunManifest
ExperimentAssignmentRecord
ExperimentPeriodBindingRecord
ExperimentMatchRecord
ExperimentPairRecord
AnalysisDatasetManifest
```

No record duplicates raw Telemetry as a second authority.

## 14.1 `ExperimentAssignmentRecord`

Contains only facts known and committed at assignment time:

```text
ExperimentAssignmentRecord
{
    experimentRunId
    experimentProtocolVersion

    pairId
    canonicalRosterIdentity
    sequenceAssignment
    scheduleSlotId

    assignmentMethodVersion
    allocationScheduleVersion
    assignedAt
}
```

Forbidden fields in the immutable assignment-time payload:

```text
period1MatchId
period2MatchId
future match identity
future outcome
future eligibility
```

## 14.2 `ExperimentPeriodBindingRecord`

Append-only / immutable logical record:

```text
ExperimentPeriodBindingRecord
{
    experimentRunId
    pairId
    periodIndex       // 1 | 2
    matchId
    boundAt
}
```

Canonical identity:

```text
(experimentRunId, pairId, periodIndex)
```

Rules:

```text
same key + same matchId
→ idempotent

same key + different matchId
→ PERIOD_BINDING_IDENTITY_CONFLICT
→ no overwrite
```

The exact table/class/transaction binding is IMPLEMENTATION BINDING TBD; the lifecycle is not TBD.

## 14.3 `ExperimentMatchRecord`

References, rather than copies, authoritative evidence:

```text
matchId
pairId
periodIndex
assignedCondition
resolutionMode
periodBindingRef
scheduleSlotId
MATCH_STARTED evidence ref
terminal evidence ref
AdaptiveDecision / ScenarioResolution evidence ref?
matchEligibility
metricAvailability{}
exclusionReasons[]
exposureClass?
fallbackExperimentClassification?
sourceVersionRefs
```

`ExperimentMatchRecord.matchId` must match the authoritative `ExperimentPeriodBindingRecord` for its `(pairId, periodIndex)`.

## 14.4 `ExperimentPairRecord`

`ExperimentPairRecord` is a derived/final research projection, not the owner of assignment or binding identity.

After bindings exist it may contain:

```text
pairId
canonicalRosterIdentity
sequenceAssignment
scheduleSlotId
period1MatchId?
period2MatchId?
pairStatus
metricPairEligibility{}
profileRevisionProvenance{}
```

`period1MatchId` / `period2MatchId` are derived from immutable period-binding records. They are not written back into `ExperimentAssignmentRecord`.

## 14.5 `MainPairEnrollmentCommit`

`MainPairEnrollmentCommit` is a logical operation/transaction boundary, not a required persisted DTO or class name.

Canonical semantic input:

```text
MainPairEnrollmentCommit
{
    experimentRunId
    pairId
    canonicalRosterIdentity
    rosterUserIds[]
    scheduleSlotId
    sequenceAssignment

    experimentProtocolVersion
    assignmentMethodVersion
    allocationScheduleVersion
}
```

Its successful logical effect is exactly the atomic state transition frozen in §22.4. It does **not** contain `period1MatchId`, `period2MatchId`, Profile state, Telemetry ingestion, AED decisions, ScenarioConfig application, Monster state, or match outcomes.

Exact class/function name and persistence mechanism are IMPLEMENTATION BINDING TBD.

## 14.6 `AnalysisDatasetManifest`

Defined in §60.

---

# 15. Experimental Unit

Canonical:

```text
Primary Experimental Unit = team-match
```

Rationale:

- Scenario resolution is shared at match level;
- players share objective progression, map, Monster, and ScenarioConfig;
- player rows inside one match are correlated;
- treating 2–4 players as 2–4 independent treatment replicates would be pseudo-replication.

Player-level telemetry/Profile observations remain valid nested observations.

Any player-level analysis must retain:

- team-match cluster;
- repeated player identity where available;
- pair/roster membership;
- period;
- sequence.

---

# 16. Paired Unit

Canonical:

```text
ExperimentPair
=
same canonical roster
+ one Period-1 team-match
+ one Period-2 team-match
+ exactly one FIXED assignment
+ exactly one ADAPTIVE assignment
```

The pair is a research identity only.

It never becomes:

- persistent TeamProfile identity;
- gameplay party identity;
- AED input identity.

## 16.1 Cross-pair participant uniqueness — v1.1 decision

Within one MAIN `experimentRunId`:

```text
one stable player userId
→ at most one MAIN confirmatory ExperimentPair
```

A preliminary participant-uniqueness check may run before slot selection, but it is not sufficient for correctness. The authoritative constraint is revalidated and reserved as part of `MainPairEnrollmentCommit`.

```text
(experimentRunId,userId) already belongs to existingPairId
+ existingPairId != requestedPairId
→ CROSS_PAIR_PARTICIPANT_REUSE
→ enrollment commit fails atomically
→ no AssignmentRecord created by that request
→ no slot consumed by that request
→ no participant membership created by that request
```

Two concurrent enrollment attempts sharing any stable `userId` therefore cannot both commit successfully.

This is research-process isolation only. It does not create a persistent gameplay team identity, modify matchmaking semantics, or freeze Player Profile updates.

Pilot participation does not itself consume a MAIN pair slot. A pilot participant may enter MAIN only under the declared pilot/main policy, with pilot data kept outside the MAIN confirmatory dataset by default and `priorExperimentExposureCount` retained.

Different `experimentRunId` values are governed by that run's own enrollment contract; this v1.1 rule does not impose cross-run participant uniqueness.

---

# 17. Canonical Roster Identity

The experiment reuses the current roster-membership semantics rather than inventing a persistent team.

Conceptually:

```text
canonicalRosterIdentity
=
target experiment pair scope
+
sorted stable player user IDs
```

Profile revisions are not part of membership identity; they are separate provenance.

For a confirmatory pair:

```text
roster(F) == roster(A)
```

must hold exactly.

A different roster creates a different research pair identity.

For MAIN enrollment, every member of the candidate canonical roster may be prechecked under §16.1, but authoritative uniqueness is enforced again inside `MainPairEnrollmentCommit`; a stale precheck cannot authorize a conflicting commit.

Late discovery that one stable participant identity already belonged to another assigned MAIN pair is a research-integrity condition:

```text
CROSS_PAIR_PARTICIPANT_REUSE
→ affected pair cannot be silently treated as statistically independent
→ confirmatory pair eligibility fails
→ assignment/slot history remains immutable
→ no slot recycling
→ run integrity review before further enrollment
```

---

# 18. Roster Change / Replacement

If a player joins/leaves/replaces between the two periods:

```text
old pair
→ not a valid same-roster confirmatory pair
```

Already completed match evidence remains auditable/descriptive if otherwise valid.

The original `scheduleSlotId` remains `CONSUMED`; it is never transferred to the replacement roster. The original `MainParticipantMembershipKey` assignments remain immutable/auditable assignment history for that run and are not erased to make those Players available for another MAIN confirmatory pair.

If the new roster is to enter MAIN research:

```text
new roster
→ new canonicalRosterIdentity
→ new pairId
→ pass preliminary cross-pair participant uniqueness
→ require a still-UNUSED pre-generated scheduleSlotId
→ commit through a new atomic MainPairEnrollmentCommit
```

If no unused slot remains:

```text
no new MAIN confirmatory pair under the current frozen schedule
```

No post-hoc schedule extension is permitted in v1.1.

The experiment layer does not mutate Player Profile to preserve a pair.

---

# 19. Two-Period Repeated-Measures Design

v1.1 canonical description:

```text
two-period
two-sequence
counterbalanced
within-roster
repeated-measures design
```

Periods:

```text
Period 1 = one team-match
Period 2 = one team-match
```

Each valid pair receives both assigned conditions once.

The term “crossover” may be used descriptively only with this qualification:

```text
no classical washout assumption
no zero-carryover assumption
```

---

# 20. Why Classical No-Carryover Crossover Is Not Assumed

PlayerAIProfile is persistent and may change after Period 1.

Player learning/familiarity may also persist.

Therefore:

```text
F first
→ Period-1 outcome may update Profile
→ later A uses a later legitimate Profile state
```

whereas:

```text
A first
→ A uses the earlier Profile state
→ later F occurs after additional play experience
```

No experiment-only Profile freeze is permitted.

The analysis must retain period, sequence, prior exposure, and Profile revision provenance rather than assume these effects disappear.

---

# 21. Sequence Classes

Exactly:

```text
F→A
A→F
```

For each match store:

```text
pairId
sequenceAssignment
periodIndex = 1 | 2
priorExperimentExposureCount
```

The condition order is analysis evidence, not a gameplay parameter.

---

# 22. Sequence Assignment Mechanism

**EXPERIMENT v1.1 DECISION**

Use a **versioned balanced randomized pre-generated sequence schedule**.

Logical assignment method:

```text
BALANCED_RANDOM_SEQUENCE_SCHEDULE_V1
```

## 22.1 `plannedPairSlots` exact meaning

```text
plannedPairSlots
=
number of MAIN ExperimentPair assignment opportunities frozen
before MAIN enrollment
```

It is **not**:

```text
desired completed pair count
eligible pair count
number to keep enrolling until achieved
```

Schedule generation occurs only after sample-size/planning work has resolved `plannedPairSlots` without inventing a number in this document.

## 22.2 Schedule generation

```text
n_FA = floor(plannedPairSlots / 2)
n_AF = plannedPairSlots - n_FA
```

Create exactly `plannedPairSlots` stable schedule entries. Each entry has:

```text
scheduleSlotId
sequenceAssignment = F→A | A→F
slotState = UNUSED | CONSUMED
```

Randomize entry order using the frozen deterministic PRNG implementation and preserve the resulting schedule/fingerprint.

Balance guarantee:

```text
abs(n_FA - n_AF) <= 1
```

No block size is invented.

## 22.3 Reproducibility requirements

Before MAIN enrollment/assignment begins, freeze:

- `plannedPairSlots`;
- `assignmentMethodVersion`;
- exact PRNG algorithm/version;
- random seed or equivalent deterministic generator state;
- generated allocation schedule;
- stable `scheduleSlotId` values;
- schedule fingerprint/hash;
- assignment service version.

The concrete RNG library is IMPLEMENTATION BINDING TBD until this freeze.

## 22.4 Assignment execution / atomic MAIN enrollment — EXP-DD-05

Enrollment may perform advisory reads before commit, but the authoritative enrollment state is established only by one logical `MainPairEnrollmentCommit`.

Canonical flow:

```text
candidate roster
→ preliminary participant/roster/run checks
→ SequenceAssignmentService proposes next UNUSED scheduleSlotId
→ build MainPairEnrollmentCommit request
→ commit-time revalidation + uniqueness reservation
→ atomic commit
→ only then Period 1 may be bound/started
```

The implementation must remain correct even when preliminary reads become stale before commit.

### 22.4.1 Stable logical uniqueness keys

```text
AssignmentIdentityKey
=
(experimentRunId, pairId)
```

```text
SlotConsumptionKey
=
(experimentRunId, scheduleSlotId)
```

```text
MainParticipantMembershipKey
=
(experimentRunId, userId)
```

Frozen semantics:

```text
one AssignmentIdentityKey
→ one immutable assignment semantic

one SlotConsumptionKey
→ at most one pairId

one MainParticipantMembershipKey
→ at most one MAIN pairId
```

Physical unique indexes/keys are implementation bindings; these observable uniqueness semantics are not.

### 22.4.2 Commit-time preconditions

Immediately at the authoritative logical commit boundary, revalidate all of the following against current committed experiment state:

```text
experimentRunId
→ current MAIN run
→ enrollment still open
→ assignedPairCount < plannedPairSlots
```

```text
AssignmentIdentityKey
→ no conflicting committed assignment
```

```text
scheduleSlotId
→ exists in current frozen allocation schedule
→ belongs to allocationScheduleVersion in the request
→ state = UNUSED for a new commit
```

For every submitted roster member:

```text
MainParticipantMembershipKey
→ no committed membership to a different pairId
```

Also:

```text
canonicalRosterIdentity
=
canonical identity recomputed from the submitted canonical roster
```

```text
sequenceAssignment
=
frozen sequence owned by scheduleSlotId
```

```text
experimentProtocolVersion / assignmentMethodVersion / allocationScheduleVersion
=
current frozen run semantics
```

An earlier uniqueness/slot precheck is never sufficient by itself.

### 22.4.3 Atomic success / failure semantics

A new successful logical enrollment commit establishes the equivalent of all of the following together:

```text
A. immutable ExperimentAssignmentRecord(pairId,...)

B. scheduleSlotId
   → pairId
   → slotState = CONSUMED

C. for every roster userId:
   (experimentRunId,userId)
   → pairId
```

Hard boundary:

```text
NEW COMMIT SUCCESS
→ ALL A + B + complete C are committed
```

```text
NEW COMMIT FAILURE
→ NONE of A + B + C are committed by that request
```

No externally stable state may represent a successful enrollment with only a subset of these facts.

Internal prepare/transaction metadata may exist if the chosen storage technology requires it, but it is not a successful assignment, cannot start Period 1, and must not be exposed as committed experiment truth.

### 22.4.4 Assignment idempotency / conflict

Define a deterministic enrollment semantic fingerprint over at least:

```text
experimentRunId
pairId
canonicalRosterIdentity
canonical rosterUserIds[]
scheduleSlotId
sequenceAssignment
experimentProtocolVersion
assignmentMethodVersion
allocationScheduleVersion
```

After an existing successful commit:

```text
same AssignmentIdentityKey
+ same enrollment semantic fingerprint
→ IDEMPOTENT_SUCCESS
→ return/reference existing assignment
→ no second slot consumption
→ no second participant reservation
→ assignedPairCount unchanged
```

```text
same AssignmentIdentityKey
+ different semantic fingerprint
→ ASSIGNMENT_IDENTITY_CONFLICT
→ no overwrite
→ no partial mutation
→ no automatic replacement
```

Differences include a different roster, canonicalRosterIdentity, scheduleSlotId, sequenceAssignment, or frozen assignment semantics.

### 22.4.5 Participant conflict

```text
any MainParticipantMembershipKey
→ existingPairId
AND existingPairId != requestedPairId
→ CROSS_PAIR_PARTICIPANT_REUSE
→ enrollment fails atomically
```

This applies identically to a pre-existing membership and to two concurrent requests racing for the same Player. At most one conflicting request may commit.

### 22.4.6 Schedule-slot conflict and retry

```text
SlotConsumptionKey
→ existingPairId
AND existingPairId != requestedPairId
→ SCHEDULE_SLOT_CONFLICT
→ no overwrite
→ losing request commits no AssignmentRecord
→ losing request commits no participant memberships
```

No implementation may silently substitute another slot inside the already-conflicting logical commit.

A new **full enrollment attempt** for the same uncommitted pair/roster may select the next currently UNUSED slot only if all of these are still true:

```text
no committed AssignmentIdentityKey exists for the pair
no roster member has been committed to another MAIN pair
Period 1 has not started/bound for this pair
run enrollment remains open
frozen schedule/version semantics are unchanged
no outcome has been observed for this uncommitted pair
```

The new attempt reruns the complete commit-time validation. This is contention retry, not slot recycling, outcome-dependent replacement, or post-hoc schedule modification.

### 22.4.7 Crash / retry semantics

Crash before durable logical commit:

```text
no successful AssignmentRecord
slot remains logically UNUSED
no participant membership exists for this request
→ safe full retry is allowed
```

Crash after durable commit but before caller acknowledgement:

```text
A + B + complete C already committed
→ retry exact same logical request
→ IDEMPOTENT_SUCCESS
→ discover existing assignment
→ no second slot
→ no duplicate membership
→ assignedPairCount unchanged
```

A caller timeout or duplicate network/backend command cannot decide whether the prior commit happened by guessing; it must resolve by the stable logical identities/fingerprint above.

### 22.4.8 Assigned-pair count and integrity audits

Under valid committed state:

```text
assignedPairCount
=
count(unique committed AssignmentIdentityKey)
=
count(CONSUMED SlotConsumptionKey)
```

Retry, rejected enrollment, and crashed pre-commit attempts do not increment it.

Participant audit invariant:

```text
committed ExperimentAssignmentRecord
↔
all expected MainParticipantMembershipKey values for its canonical roster
point to exactly that pairId
```

Slot audit invariant:

```text
committed ExperimentAssignmentRecord
↔
exactly one corresponding CONSUMED scheduleSlotId owned by that pairId
```

and:

```text
CONSUMED scheduleSlotId
↔
exactly one committed ExperimentAssignmentRecord
```

Detected mismatch, including an orphan consumed slot, AssignmentRecord pointing to an UNUSED slot, missing/partial participant memberships, or `assignedPairCount` mismatch, is:

```text
ENROLLMENT_INTEGRITY_FAILURE
→ not silently reconciled during MAIN
→ suspend/investigate
→ recovery, if performed, requires explicit auditable governance
```

### 22.4.9 Atomicity scope

`MainPairEnrollmentCommit` is research-assignment state only.

It includes only:

```text
immutable assignment
+ schedule-slot consumption
+ complete participant membership reservation
```

It does **not** transactionally include:

```text
ExperimentPeriodBindingRecord
Profile update
Telemetry ingestion
AdaptiveInputSnapshot
AED decision
ScenarioConfig apply
Monster state
match outcome
```

Exact transaction/CAS/serialization technology remains IMPLEMENTATION BINDING TBD. Observable atomicity, idempotency, uniqueness, conflict, and crash-retry semantics are frozen.

## 22.5 No replacement / extension

v1.1 freezes:

```text
NO slot recycling
NO outcome-dependent replacement
NO post-hoc schedule regeneration
NO post-hoc schedule extension
```

Enrollment stop rule:

```text
assignedPairCount == plannedPairSlots
→ no further MAIN pair enrollment under this experimentRunId
```

A later replacement/extension policy would require a protocol revision frozen before outcome inspection; none exists in v1.1.

## 22.6 Count terminology

```text
plannedPairSlots
= frozen number of assignment opportunities

assignedPairCount
= count unique successfully committed AssignmentIdentityKey
= count CONSUMED scheduleSlotId

completedPairCount
= assigned pairs with authoritative bindings for both periods and both bound matches reaching terminal match evidence

eligiblePairCount
= completed pairs satisfying confirmatory primary pair eligibility at the relevant dataset cutoff

excludedPairCount
= assigned pairs deterministically classified EXCLUDED for confirmatory pair analysis
```

These quantities are reported separately and are never conflated. `assignedPairCount` is a committed-state count, never an attempt counter. If its two equivalent committed-state derivations disagree, the run has `ENROLLMENT_INTEGRITY_FAILURE`; code must not silently choose one value.

Forbidden:

- developer manually chooses sequence after seeing roster behavior;
- assignment after Period-1 outcome;
- rebalancing after observing condition results;
- choosing the next sequence based on Profile score;
- recycling a slot because its prior pair had a poor outcome;
- regenerating the schedule after any outcome has been observed.

---

# 23. Allocation Concealment / Operational Separation

Where practical, the service that generates the full schedule should not expose future sequence slots to the gameplay operator responsible for enrollment.

At minimum:

- the complete schedule is immutable after MAIN freeze;
- each `scheduleSlotId` has stable identity;
- only an UNUSED slot may be assigned;
- successful assignment permanently consumes that slot;
- failed `MainPairEnrollmentCommit` consumes/reserves no slot or participant membership for that request;
- future schedule slots are not manually selected based on observed outcomes;
- assignment timestamp precedes outcome;
- post-hoc sequence editing, slot recycling, schedule regeneration, and schedule extension are prohibited.

This is research-process isolation, not gameplay authority.

---

# 24. Condition F — Fixed

Canonical:

```text
experimentCondition = FIXED
ScenarioResolutionMode = FIXED
```

Fixed path:

```text
FixedDirector
→ frozen run baseline
→ validated authoritative ScenarioConfig
```

Condition F is **not**:

```text
configSource == FIXED
```

because an A-assigned match may also realize a Fixed config through fallback.

Before main execution freeze:

```text
fixedBaselineId
fallbackConfigVersion / fixed baseline version
resolved fixed ScenarioConfig content or immutable fingerprint
```

No numerical baseline values are invented here.

---

# 25. Condition A — Adaptive

Canonical:

```text
experimentCondition = ADAPTIVE
ScenarioResolutionMode = ADAPTIVE
policyVersion = AED_SCENARIO_POLICY_V1_1
```

Normal path:

```text
AdaptiveInputSnapshot
→ AEDInputGate
→ AED policy
→ CandidateScenarioConfig
→ ScenarioValidator
→ APPLIED | NO_CHANGE | FIXED_FALLBACK
```

Condition A does not require an adaptive delta to be applied.

---

# 26. Condition / Exposure / Fallback Classification Table

| Assigned Condition | ResolutionMode | Authoritative AED/Resolution outcome | Possible `configSource` | Primary assignment label | Exposure class | Experiment fallback classification |
|---|---|---|---|---|---|---|
| FIXED | FIXED | FixedDirector resolved | FIXED | F | `FIXED_BASELINE_EXPOSURE` | n/a |
| ADAPTIVE | ADAPTIVE | APPLIED | ADAPTIVE | A | `ADAPTIVE_DELTA_APPLIED` | n/a |
| ADAPTIVE | ADAPTIVE | NO_CHANGE | unchanged/current authoritative source | A | `ADAPTIVE_EVALUATED_NO_CHANGE` | n/a |
| ADAPTIVE | ADAPTIVE | FIXED_FALLBACK | may be FIXED at PRE_MATCH fallback or retained current source later | A | `ADAPTIVE_FIXED_FALLBACK` | determined by §28 from upstream status/reason |
| ADAPTIVE | ADAPTIVE | fatal/identity path with no normal fallback commit | authoritative current config retained/unavailable per AED contract | A | `ADAPTIVE_INTEGRITY_FAILURE` | determined by §28 |

Primary treatment classification never uses `configSource`, exposure class, or fallback class alone.

---

# 27. Adaptive Exposure Classification

For current `AED_SCENARIO_POLICY_V1_1`, adaptive deltas are generated only at PRE_MATCH.

The ordinary match-level exposure class is determined from the authoritative PRE_MATCH resolution:

### `ADAPTIVE_DELTA_APPLIED`

```text
A-assigned
+ PRE_MATCH AdaptiveDecision result = APPLIED
+ at least one authoritative requested change applied
```

### `ADAPTIVE_EVALUATED_NO_CHANGE`

```text
A-assigned
+ PRE_MATCH Adaptive policy evaluated successfully
+ result = NO_CHANGE
```

### `ADAPTIVE_FIXED_FALLBACK`

```text
A-assigned
+ PRE_MATCH Adaptive result = FIXED_FALLBACK
```

`ADAPTIVE_FIXED_FALLBACK` alone says nothing about confirmatory eligibility. §28 must classify the authoritative upstream AED reason.

### `ADAPTIVE_INTEGRITY_FAILURE`

Audit/process-only exposure class for an A-assigned resolution that terminates through an integrity/fatal path without a normal `FIXED_FALLBACK` commit, including `DECISION_IDENTITY_CONFLICT` or `FALLBACK_CONFIG_INVALID` when those upstream semantics apply.

It is not a normal treatment exposure and is excluded under §28/§47.

Later boundary/Final-Hunt HOLD/NO_CHANGE decisions remain process evidence but do not reassign the match.

---

# 28. Adaptive Fallback Reason → Experiment Classification

The experiment layer does **not** redefine AED reason codes. It consumes the authoritative `CandidateValidationStatus`, final AED result/path, and controlled upstream reason, then maps them deterministically to experiment semantics.

Classification semantics are owned by:

```text
experimentProtocolVersion = FIXED_ADAPTIVE_EXPERIMENT_PROTOCOL_V1_1
```

Controlled experiment classes:

```text
EXPECTED_TREATMENT_FALLBACK
SYSTEM_FAILURE_FALLBACK
PROTOCOL_OR_CONFIG_INVALID
SAFETY_OR_VALIDATION_REJECTION
```

Every legitimately recorded A-assigned match remains assigned A in audit history, even when the match is excluded.

## 28.1 Normative mapping

| Upstream AED status/result/reason | Experiment fallback class | Assigned condition | Match eligibility effect | Pair effect | Run effect | Exposure class |
|---|---|---|---|---|---|---|
| `FIXED_FALLBACK + INPUT_INCOMPLETE` | `EXPECTED_TREATMENT_FALLBACK` | A | **ELIGIBLE if all other experiment criteria pass** | pair may remain eligible | continue | `ADAPTIVE_FIXED_FALLBACK` |
| `FIXED_FALLBACK + STALE_INPUT` | `EXPECTED_TREATMENT_FALLBACK` | A | **ELIGIBLE if all other criteria pass** | pair may remain eligible | continue | `ADAPTIVE_FIXED_FALLBACK` |
| `FIXED_FALLBACK + INPUT_INVALID` | `PROTOCOL_OR_CONFIG_INVALID` | A | **EXCLUDED** | confirmatory pair incomplete/ineligible | **SUSPEND + readiness review** | `ADAPTIVE_FIXED_FALLBACK` |
| `FIXED_FALLBACK + AED_UNAVAILABLE` | `SYSTEM_FAILURE_FALLBACK` | A | **EXCLUDED** | confirmatory pair incomplete/ineligible | **SUSPEND + readiness review** | `ADAPTIVE_FIXED_FALLBACK` |
| `FIXED_FALLBACK + AED_TIMEOUT` | `SYSTEM_FAILURE_FALLBACK` | A | **EXCLUDED** | confirmatory pair incomplete/ineligible | **SUSPEND + readiness review** | `ADAPTIVE_FIXED_FALLBACK` |
| `FIXED_FALLBACK + POLICY_CONFIG_INVALID` | `PROTOCOL_OR_CONFIG_INVALID` | A | **EXCLUDED** | confirmatory pair incomplete/ineligible | **SUSPEND** | `ADAPTIVE_FIXED_FALLBACK` |
| `FIXED_FALLBACK + PARAMETER_REGISTRY_INVALID` | `PROTOCOL_OR_CONFIG_INVALID` | A | **EXCLUDED** | confirmatory pair incomplete/ineligible | **SUSPEND** | `ADAPTIVE_FIXED_FALLBACK` |
| `FIXED_FALLBACK + UNSUPPORTED_VERSION` | `PROTOCOL_OR_CONFIG_INVALID` | A | **EXCLUDED** | confirmatory pair incomplete/ineligible | **SUSPEND** | `ADAPTIVE_FIXED_FALLBACK` |
| `INVALID/FIXED_FALLBACK + POLICY_KEY_NOT_ACTIVE` | `PROTOCOL_OR_CONFIG_INVALID` | A | **EXCLUDED** | confirmatory pair incomplete/ineligible | **SUSPEND** | `ADAPTIVE_FIXED_FALLBACK` |
| `INVALID/FIXED_FALLBACK + REGISTERED_VALUE_REJECTED` | `PROTOCOL_OR_CONFIG_INVALID` | A | **EXCLUDED** | confirmatory pair incomplete/ineligible | **SUSPEND** | `ADAPTIVE_FIXED_FALLBACK` |
| `INVALID/FIXED_FALLBACK + BOUND_REJECTED` | `PROTOCOL_OR_CONFIG_INVALID` | A | **EXCLUDED** | confirmatory pair incomplete/ineligible | **SUSPEND** | `ADAPTIVE_FIXED_FALLBACK` |
| `INVALID/FIXED_FALLBACK + TIMING_REJECTED` | `PROTOCOL_OR_CONFIG_INVALID` | A | **EXCLUDED** | confirmatory pair incomplete/ineligible | **SUSPEND** | `ADAPTIVE_FIXED_FALLBACK` |
| `INVALID/FIXED_FALLBACK + PRESSURE_RULE_REJECTED` | `PROTOCOL_OR_CONFIG_INVALID` | A | **EXCLUDED** | confirmatory pair incomplete/ineligible | **SUSPEND** | `ADAPTIVE_FIXED_FALLBACK` |
| `NOT_EVALUATED/FIXED_FALLBACK + SCENARIO_INVALID` | `PROTOCOL_OR_CONFIG_INVALID` | A | **EXCLUDED** | confirmatory pair incomplete/ineligible | **SUSPEND** | `ADAPTIVE_FIXED_FALLBACK` |
| `INVALID/FIXED_FALLBACK + SCENARIO_INVALID` | `SAFETY_OR_VALIDATION_REJECTION` | A | **ELIGIBLE if fallback succeeds and all other criteria pass** | pair may remain eligible | continue; retain safety evidence | `ADAPTIVE_FIXED_FALLBACK` |
| `INVALID/FIXED_FALLBACK + ROUTE_INVALID` | `SAFETY_OR_VALIDATION_REJECTION` | A | **ELIGIBLE if fallback succeeds and all other criteria pass** | pair may remain eligible | continue; retain safety evidence | `ADAPTIVE_FIXED_FALLBACK` |
| `INVALID/FIXED_FALLBACK + SPAWN_INVALID` | `SAFETY_OR_VALIDATION_REJECTION` | A | **ELIGIBLE if fallback succeeds and all other criteria pass** | pair may remain eligible | continue; retain safety evidence | `ADAPTIVE_FIXED_FALLBACK` |
| `INVALID/FIXED_FALLBACK + STALE_BASE_CONFIG` | `SAFETY_OR_VALIDATION_REJECTION` | A | **ELIGIBLE if fallback succeeds and all other criteria pass** | pair may remain eligible | continue; retain concurrency evidence | `ADAPTIVE_FIXED_FALLBACK` |
| `INVALID/FIXED_FALLBACK + DECISION_WINDOW_CLOSED` | `SAFETY_OR_VALIDATION_REJECTION` | A | **ELIGIBLE if fallback succeeds and all other criteria pass** | pair may remain eligible | continue; retain timing evidence | `ADAPTIVE_FIXED_FALLBACK` |
| `DECISION_IDENTITY_CONFLICT` | `PROTOCOL_OR_CONFIG_INVALID` | A | **EXCLUDED** | confirmatory pair incomplete/ineligible | **SUSPEND immediately** | `ADAPTIVE_INTEGRITY_FAILURE` |
| `FALLBACK_CONFIG_INVALID` | `PROTOCOL_OR_CONFIG_INVALID` | A | **EXCLUDED** | confirmatory pair incomplete/ineligible | **SUSPEND immediately** | `ADAPTIVE_INTEGRITY_FAILURE` |

## 28.2 Interpretation

`EXPECTED_TREATMENT_FALLBACK` means the frozen treatment implementation is healthy, but the current match/snapshot lacks sufficient current evidence or becomes legitimately stale under the immutable Profile/AED lifecycle.

`SYSTEM_FAILURE_FALLBACK` means the assigned Adaptive service failed to execute because it was unavailable or timed out. v1.1 makes an explicit choice: these matches remain A-assigned in audit history but are **not** confirmatory treatment observations. No arbitrary tolerated-rate threshold is invented; either condition suspends the run for readiness review before further MAIN collection.

`PROTOCOL_OR_CONFIG_INVALID` means the frozen treatment/config/integrity contract was not valid for execution. It is never normalized into ordinary expected fallback.

`SAFETY_OR_VALIDATION_REJECTION` means a constructed/current candidate or commit attempt was stopped by the designed Scenario/route/spawn/concurrency safety boundary while the frozen policy/config/registry infrastructure itself remained valid. That rejection is part of the bounded Adaptive system behavior and may remain confirmatory-eligible when fallback succeeds and no separate experiment exclusion applies.

It may be assigned only when authoritative evidence proves that pre-build policy/config/registry validation passed and that the candidate was authorized/current-policy-active. If that prerequisite is not provable, the experiment classification is `PROTOCOL_OR_CONFIG_INVALID`, not safety rejection.

`BOUND_REJECTED`, `TIMING_REJECTED`, and `PRESSURE_RULE_REJECTED` are classified as `PROTOCOL_OR_CONFIG_INVALID` in this v1.1 experiment because a frozen valid `AED_SCENARIO_POLICY_V1_1` candidate must already obey registered bounds, allowed timing, and the pressure-rule contract; those reasons therefore evidence an invalid treatment implementation/request rather than an ordinary environment-dependent safety veto.

`SCENARIO_INVALID` is intentionally disambiguated by the upstream `CandidateValidationStatus`: pre-candidate `NOT_EVALUATED` dependency/config failure is not the same as candidate-stage `INVALID` safety rejection.

## 28.3 Mandatory assignment rule

```text
A assigned
+ any row above
→ assignedCondition remains A
```

Exclusion changes analysis eligibility, not historical assignment.

---

# 29. Protocol Violation vs Expected Fallback

Expected behavior:

```text
run-level readiness = PASS
A assigned
+ INPUT_INCOMPLETE or legitimate STALE_INPUT
→ EXPECTED_TREATMENT_FALLBACK
→ assignment remains A
→ match may remain ELIGIBLE
```

Expected bounded-policy behavior:

```text
run-level readiness = PASS
A assigned
+ SAFETY_OR_VALIDATION_REJECTION
+ authoritative fallback succeeds
→ assignment remains A
→ match may remain ELIGIBLE
→ safety/rejection evidence retained
```

Broken treatment infrastructure:

```text
SYSTEM_FAILURE_FALLBACK
OR PROTOCOL_OR_CONFIG_INVALID
→ assignment remains A
→ match EXCLUDED
→ pair incomplete/ineligible for confirmatory comparison
→ run SUSPENDED for readiness review
```

Independent protocol violation:

```text
run-level readiness = FAIL
but operator starts a MAIN A-assigned match
→ READINESS_GATE_VIOLATION
→ assignment remains A in audit history
→ match EXCLUDED
→ run SUSPENDED/investigated
```

Do not conflate safety fallback, participant/evidence insufficiency, and broken frozen infrastructure.

---

# 30. Two Readiness Levels

## 30.1 Level 1 — Experiment System Readiness

Global/run-level gate that must PASS before main collection begins.

## 30.2 Level 2 — Match-Level AED Eligibility

Per-A-match runtime result owned by the AED v1.1 input gate.

A system can be ready while an individual A-assigned match legitimately falls back.

---

# 31. Experiment System Readiness Gate v1.1

Before MAIN collection, all required evidence must show:

| Gate | Required evidence |
|---|---|
| Game/AI build | frozen build passes required gameplay regression |
| Telemetry v1.1 | implementation and validation operational |
| Experiment provenance | MATCH_STARTED assignment/protocol fields working |
| Profile v1.1 | implementation and correction/replay semantics validated |
| AdaptiveInputSnapshot | current revision/roster validity implemented |
| AED policy | `AED_SCENARIO_POLICY_V1_1` implemented |
| AEDEvidencePolicy | supported and frozen |
| AEDPolicyConfig | supported and frozen |
| AdaptiveParameterRegistry | supported and frozen |
| content whitelist | supported and frozen |
| ScenarioValidator | operational and tested |
| FixedDirector | operational and tested |
| Fixed baseline | valid, resolvable, frozen |
| Host apply | authoritative apply/CAS path operational |
| AdaptiveDecision evidence | exactly-once ledger/provenance operational |
| Experiment protocol | `FIXED_ADAPTIVE_EXPERIMENT_PROTOCOL_V1_1` frozen |
| Assignment lifecycle | immutable assignment + immutable period-binding implementation validated |
| MAIN enrollment atomicity | assignment + slot consumption + complete participant membership commit atomically; exact retry/conflict/crash semantics validated |
| Main participant uniqueness | same-run cross-pair uniqueness enforced at commit time under concurrency, not only by an advisory precheck |
| Assignment mechanism | implementation validated; `plannedPairSlots` and main schedule frozen |
| Slot lifecycle | one slot owner under concurrency; UNUSED→CONSUMED only inside successful enrollment commit; no recycle/extension path |
| Fallback classifier | §28 deterministic mapping implemented from authoritative AED status/result/reason |
| Analysis plan | `analysisPlanVersion` frozen |
| Primary endpoint extractor | validated |
| Dataset builder | deterministic and validated |
| Data retention | run/assignment/binding/match/pair/dataset evidence durable |
| Environment | run execution environment declared |
| Pilot/tuning | required tuning completed and separated from main |
| Sample-size plan | `plannedPairSlots` frozen from approved planning inputs |

A contract document alone cannot satisfy an implementation-dependent gate. MAIN enrollment readiness specifically requires executed evidence for slot uniqueness, participant uniqueness under concurrent commits, assignment idempotency, crash/retry convergence, and absence/detection of partial durable enrollment state.

During MAIN collection, occurrence of `SYSTEM_FAILURE_FALLBACK` or `PROTOCOL_OR_CONFIG_INVALID` invalidates the assumption that the frozen treatment implementation remains healthy and therefore suspends the run for readiness review before further enrollment/collection.

---

# 32. Current Readiness

From supplied evidence:

```text
Experiment Contract Design:
COMPLETE

Main Experiment Execution:
NOT READY
```

Known blockers are listed in §72.

The old TeamPerformance COMPLETE gate is **not** a current blocker because it has been superseded by Profile/AED v1.1. TeamPerformance nevertheless remains honestly `INCOMPLETE/null`. A MAIN run also requires the corrected assignment/binding, fallback-classification, slot-lifecycle, and participant-uniqueness implementation evidence in §31.

---

# 33. Match-Level Adaptive Eligibility

This contract records but does not redefine the AED gate.

Current AED v1.1 requires, among other current contract conditions:

- `AdaptiveInputSnapshot.snapshotValidity = VALID`;
- full-roster observed `SURVIVAL`;
- full-roster observed `NOISE`;
- coherent Profile dimension comparison semantics;
- minimum sample counts from `AEDEvidencePolicy`;
- current source Profile revisions;
- supported policy/evidence/config versions.

Per-match AED ineligibility produces the AED-defined fallback and may remain valid assigned-condition evidence.

---

# 34. Primary Outcome Validity Review

Historical M1-020 treated three performance outcomes as primary candidates. v1.1 audits them separately.

| Candidate | Current source validity | Selection-bias concern | v1.1 role |
|---|---|---|---|
| Team Survival | exact current source: match-start teamSize + match-end survivorCount | none from completion conditioning | **CONFIRMATORY PRIMARY** |
| Objective Completion | canonical Match outcome exists, but exact `MatchOutcomeRegistry → ObjectiveCompletion` mapping is not supplied here | depends on mapping | **SECONDARY WHEN MAPPING FROZEN; otherwise UNAVAILABLE** |
| `objectiveTime` | exact duration only for valid objective-bearing completed phase pairs | conditioning on completion can select post-treatment subset | **CONDITIONAL SECONDARY** |

v1.1 intentionally avoids an undefined “three co-primary endpoints” design.

---

# 35. Confirmatory Primary Endpoint — Team Survival

Logical endpoint:

```text
ExperimentTeamSurvival_v1.1
```

Canonical endpoint identifier: `ExperimentTeamSurvival_v1.1`.

## 35.1 Unit

One value per experiment-eligible team-match.

## 35.2 Source

```text
MATCH_STARTED.context.teamSize
MATCH_ENDED.data.survivorCount
```

with valid terminal Match lifecycle and compatible source versions.

## 35.3 Value

Preferred analysis-scale value:

```text
TeamSurvivalProportion
=
survivorCount / teamSize
```

where:

```text
teamSize > 0
0 <= survivorCount <= teamSize
```

This is the `[0,1]` linear equivalent of the Profile Team Survival component `100 * survivorCount/teamSize`. The experiment uses the raw proportion to avoid implying TeamPerformance completion.

## 35.4 Missing and zero

```text
survivorCount = 0
→ valid observed TeamSurvivalProportion = 0
```

but:

```text
missing/invalid terminal evidence
→ UNAVAILABLE
→ null
```

## 35.5 Direction

Higher = more surviving team members at match end.

## 35.6 Pair-level estimand input

For one eligible pair:

```text
D_pair
=
TeamSurvivalProportion_A
-
TeamSurvivalProportion_F
```

The final inferential estimator/model is owned by the frozen analysis plan.

---

# 36. Objective Completion

The current Telemetry contract delegates `MATCH_ENDED.data.outcome` to a versioned gameplay-owned `MatchOutcomeRegistry`; a JSON `SUCCESS` example is not the complete domain.

Therefore this experiment contract does **not** invent an outcome mapping.

Before Objective Completion can be analyzed:

```text
approved MatchOutcomeRegistryVersion
+
versioned mapping:
MatchOutcome → ObjectiveCompletion {true, false, UNAVAILABLE}
```

must be frozen.

Until then:

```text
ObjectiveCompletion
=
UNAVAILABLE FOR CONFIRMATORY/SECONDARY INFERENCE
```

Once a valid mapping exists, Objective Completion may be activated as a **secondary** endpoint under the analysis plan without becoming a second confirmatory primary unless the protocol/analysis hierarchy is explicitly revised before main collection.

---

# 37. `objectiveTime` — Conditional Secondary

Current source:

```text
sum(
    PHASE_COMPLETED.ts
    -
    matching PHASE_STARTED.ts
)
for required objective-bearing completed phase pairs
```

Unit: seconds.

Security Hold interruption remains inside elapsed wall-clock duration under the existing Profile semantic.

## 37.1 v1.1 analysis decision

No source-supported censoring/time-to-event endpoint exists for failed/incomplete objectives.

Therefore:

```text
objectiveTime
→ analyzed only among matches where its complete source-valid phase-pair definition is AVAILABLE
→ SECONDARY / CONDITIONAL
```

Forbidden:

```text
objective failure → objectiveTime = 0
objective failure → invented penalty duration
```

Interpretation must explicitly say the comparison is conditional on source-valid objective completion and may be affected by post-treatment selection.

It is not used as the confirmatory primary endpoint.

---

# 38. Secondary Outcomes

Eligible only when source contracts and availability support them:

- Match Duration;
- per-phase duration;
- Objective Completion after §36 mapping is frozen;
- conditional `objectiveTime`;
- Down Count;
- Revive Count;
- Eliminated Count;
- player escape/survival;
- Player Survival MatchScore/Profile evidence;
- raw Noise count/by type/by phase;
- `ProfileNoisePenaltyCount`;
- Player Noise score when filter/normalization versions are compatible;
- pair period/sequence summaries.

Raw Noise count does not inherently mean “good” or “bad.”

---

# 39. Adaptive Exposure / Process Outcomes

Secondary/exploratory:

- count/share `ADAPTIVE_DELTA_APPLIED`;
- count/share `ADAPTIVE_EVALUATED_NO_CHANGE`;
- count/share `ADAPTIVE_FIXED_FALLBACK`;
- fallback by reason;
- fallback by decision point;
- changed-key count;
- changed key identity;
- resulting ScenarioConfig version;
- policy rule selected;
- AdaptationIntent;
- stale-input/base rejection counts if execution reaches those paths.

These explain mechanism/exposure; they do not redefine assignment.

---

# 40. AI-Specific Research Metrics

Telemetry v1.1 may enable exact Monster research metrics through `RESEARCH_CAPTURE`.

They are exploratory mechanistic outcomes only when:

- their owning detailed design defines exact metric semantics;
- required research events are enabled and complete;
- the run manifest declares `researchCaptureEnabled`.

`researchCaptureEnabled = false` does not invalidate Team Survival or other ordinary primary data.

No Monster research metric becomes a primary endpoint by convenience.

---

# 41. Safety / Fairness Evidence

Track at minimum where available:

- unauthorized adaptive-key request;
- parameter-registry invalidity;
- bound rejection;
- policy-active-key rejection;
- timing rejection;
- pressure-rule rejection;
- route/spawn invalidity;
- atomic partial-apply violation;
- soft-lock;
- objective unreachable;
- Exit unreachable;
- Host authority violation;
- decision identity conflict;
- deterministic reproduction mismatch;
- invalid fixed baseline/fallback.

Hard safety invariant violation:

```text
cannot be accepted because its observed rate is small
```

It triggers the suspension/investigation policy in §61.

---

# 42. Data Quality Evidence

Track:

- Telemetry COMPLETE/INCOMPLETE/INVALID;
- schema rejection;
- identity conflict;
- sequence conflict/gap;
- unsupported version;
- experiment-condition mismatch;
- missing assignment provenance;
- `MATCH_ABORTED`;
- metric availability;
- Profile/source invalidation;
- run version drift;
- match exclusion count/reason;
- pair exclusion count/reason;
- dataset rebuild count/reason.

No data-quality failure becomes outcome zero.

---

# 43. Optional Subjective Experience Layer

Status:

```text
OPTIONAL
NOT CONFIRMATORY IN CURRENT v1.1
```

Before activation freeze:

- `instrumentVersion`;
- exact question wording;
- response scale;
- administration timing;
- scoring;
- missing-response rule;
- language/version;
- analysis role.

No claim of psychometric validity may be made without evidence.

---

# 44. Validity Layers

Keep separate:

```text
Telemetry validity
!=
Profile eligibility
!=
AED match eligibility
!=
Experiment match eligibility
!=
Metric availability
!=
Pair eligibility
```

The experiment layer owns only experiment-level inclusion/exclusion.

---

# 45. Experiment Match Status

Canonical logical status:

```text
PENDING
ELIGIBLE
EXCLUDED
```

A match is `PENDING` until enough terminal and quality evidence exists to classify it for the relevant frozen dataset.

---

# 46. Controlled Experiment Exclusion / Integrity Reasons

Compact experiment-owned set:

```text
MATCH_ABORTED
ASSIGNMENT_MISSING
ASSIGNMENT_CONFLICT
ASSIGNMENT_IDENTITY_CONFLICT
SCHEDULE_SLOT_CONFLICT
ENROLLMENT_INTEGRITY_FAILURE
PERIOD_BINDING_MISSING
PERIOD_BINDING_IDENTITY_CONFLICT
EXPERIMENT_PROTOCOL_MISMATCH
RUN_VERSION_DRIFT
BUILD_OR_CONTENT_DRIFT
CRITICAL_TELEMETRY_CORRUPTION
PRIMARY_ENDPOINT_UNRECONSTRUCTABLE
ROSTER_PROTOCOL_VIOLATION
CROSS_PAIR_PARTICIPANT_REUSE
READINESS_GATE_VIOLATION
AED_SYSTEM_FAILURE_FALLBACK
AED_PROTOCOL_OR_CONFIG_INVALID
FATAL_SAFETY_VIOLATION
```

Where possible store the exact upstream AED/Profile/Telemetry reason as a referenced provenance field rather than duplicating its semantic definition.

`ASSIGNMENT_IDENTITY_CONFLICT`, `SCHEDULE_SLOT_CONFLICT`, and `ENROLLMENT_INTEGRITY_FAILURE` are experiment enrollment-lifecycle diagnostics owned by §22.4. `ASSIGNMENT_CONFLICT` remains the broader match/dataset exclusion umbrella when authoritative assignment truth cannot be resolved.

`AED_SYSTEM_FAILURE_FALLBACK` and `AED_PROTOCOL_OR_CONFIG_INVALID` are experiment classifications derived by §28; they do not replace upstream AED reason codes.

---

# 47. Hard Match Exclusion

A match is excluded from confirmatory MAIN analysis when a predeclared critical condition applies, including:

- `MATCH_ABORTED`;
- assigned condition cannot be proven;
- period binding is missing/conflicting for the match;
- experiment protocol mismatch;
- incompatible build/config/content version;
- critical Telemetry corruption prevents primary endpoint reconstruction;
- same-roster pair protocol violated for pair-confirmatory analysis;
- late-discovered cross-pair participant reuse violates §16.1;
- MAIN A match was launched while System Readiness was FAIL;
- §28 classifies the Adaptive resolution as `SYSTEM_FAILURE_FALLBACK`;
- §28 classifies the Adaptive resolution as `PROTOCOL_OR_CONFIG_INVALID`;
- fatal safety/config defect invalidates the intended treatment implementation.

Do **not** exclude merely because:

- A produced `NO_CHANGE`;
- A produced `EXPECTED_TREATMENT_FALLBACK`;
- A produced `SAFETY_OR_VALIDATION_REJECTION` with successful authoritative fallback and no separate exclusion;
- Adaptive performed poorly;
- result weakens H1;
- one secondary metric is missing while the primary remains valid.

Every excluded A-assigned match remains labeled A in audit history.

---

# 48. Metric-Level Unavailability

For an experiment-eligible match:

```text
metric M unavailable
→ M = null
→ match remains eligible for analyses not requiring M
```

No zero substitution.

No implicit imputation.

Any imputation/missing-data model requires explicit `analysisPlanVersion` ownership.

---

# 49. Pair Eligibility

Base confirmatory pair eligibility requires:

```text
same pairId
same canonical roster
same compatible frozen experimentRunId
one immutable Period-1 binding
one immutable Period-2 binding
one F match
one A match
both whole-match experiment statuses ELIGIBLE
all members satisfy same-run one-MAIN-pair participant rule
```

Period match identity is resolved only from `ExperimentPeriodBindingRecord`; a derived PairRecord cannot override it.

For metric `M`:

```text
M_F AVAILABLE
AND
M_A AVAILABLE
→ pair eligible for paired analysis of M
```

For the confirmatory primary endpoint this defines `eligiblePairCount` at the dataset cutoff.

A pair with withdrawal, missing Period 2, roster mutation, excluded match, or cross-pair participant overlap remains historical assigned evidence but is not a confirmatory eligible pair.

No fabricated paired value and no replacement assignment is created.

---

# 50. Outcome-Independent Exclusion / Replacement

Exclusion criteria and slot lifecycle are frozen before MAIN outcome inspection.

Forbidden:

```text
remove A fallback because it dilutes Adaptive effect
remove slow F match because it looks like an outlier
remove a pair because A survival was poor
recycle an excluded pair's schedule slot
add a replacement pair because prior outcome was poor
keep enrolling until a desired number of eligible/completed pairs appears
regenerate/extend the allocation schedule after observing outcomes
```

If outlier handling is later approved, it belongs to `analysisPlanVersion` and must be frozen before MAIN collection at the declared freeze point.

Expected dropout/exclusion must be accounted for in the pre-main planning that produces `plannedPairSlots`; v1.1 has no post-hoc replacement rule.

---

# 51. Primary Estimand

Canonical conceptual estimand:

```text
AssignedConditionDifference_v1.1
=
expected TeamSurvivalProportion under ADAPTIVE assignment
-
expected TeamSurvivalProportion under FIXED assignment
```

evaluated under the frozen two-period same-roster protocol.

This is an **assigned-condition** comparison.

It is not redefined by:

- whether an adaptive delta applied;
- `configSource`;
- fallback;
- NO_CHANGE.

Because persistent Profile evolution and learning can create period/order dependence, the report must not casually label this an unconditional causal treatment effect unless the frozen model and assumptions support that interpretation.

---

# 52. Secondary Actual-Exposure Analysis

A separate analysis may compare or describe realized exposure classes.

It is:

```text
SECONDARY / EXPLORATORY
```

because exposure after A assignment depends on Profile evidence, policy state, and safety/fallback conditions.

Never substitute an exposure-based subset analysis for the primary assigned-condition analysis.

---

# 53. Profile Evolution / Carryover

Normal Profile lifecycle remains active.

For every A decision retain:

```text
snapshotId
snapshotContentFingerprint
sourceProfileRevisions[]
profileFormulaSemanticId
dimensionComparisonKeys{}
evidencePolicyVersion
policyVersion
```

For every pair retain Profile revisions/evidence status before both periods where available.

The analysis/report must discuss:

- period effect;
- sequence effect;
- learning/familiarity;
- map familiarity;
- Profile evolution;
- possible carryover.

No washout interval is invented because a gameplay/profile “washout” is not supported by project semantics.

---

# 54. Controlled Constants

Within one homogeneous MAIN run, freeze or explicitly stratify all behavior/metric-relevant versions:

- gameplay rules/build;
- Research Facility map scope;
- `mapContentVersion`;
- objective chain;
- primary monster identity/content scope;
- Telemetry wire `"1.1"`;
- relevant Telemetry semantic registries;
- Profile formula semantic ID;
- Profile metric/filter/normalization versions used in outcomes;
- AED `policyVersion`;
- `policyConfigVersion`;
- `evidencePolicyVersion`;
- `parameterRegistryVersion`;
- `contentWhitelistVersion`;
- fixed baseline identity/version/fingerprint;
- experiment protocol version;
- assignment method version;
- `plannedPairSlots`;
- allocation schedule version/fingerprint;
- cross-pair participant uniqueness rule;
- §28 fallback-classification semantics;
- analysis plan version;
- research-capture setting;
- network/test environment definition.

If a material version changes during MAIN collection:

```text
suspend/close current homogeneous run
→ new run/cohort
```

unless the pre-frozen analysis plan explicitly defines a valid compatibility stratum.

The existing schedule is not regenerated to accommodate the new run; a new run requires its own planning/freeze lifecycle.

---

# 55. ExperimentRunManifest

Logical immutable/versioned record:

```text
ExperimentRunManifest
{
    experimentRunId
    experimentProtocolVersion
    analysisPlanVersion

    runStatus
    dataCollectionWindow?
    dataCutoff?

    buildVersion
    mapId
    mapContentVersion
    monsterScope

    telemetrySchemaVersion = "1.1"

    profileFormulaSemanticId
    profileMetricConfigVersions{}

    policyVersion = AED_SCENARIO_POLICY_V1_1
    policyConfigVersion
    evidencePolicyVersion
    parameterRegistryVersion
    contentWhitelistVersion

    fixedBaselineId
    fallbackConfigVersion
    fixedBaselineFingerprint

    plannedPairSlots
    assignmentMethodVersion
    allocationScheduleVersion
    allocationScheduleFingerprint
    assignmentGeneratorVersion
    assignmentServiceVersion
    assignmentSeedRef

    participantReusePolicy = ONE_MAIN_CONFIRMATORY_PAIR_PER_PLAYER_PER_RUN
    fallbackClassificationOwner = FIXED_ADAPTIVE_EXPERIMENT_PROTOCOL_V1_1

    researchCaptureEnabled
    environmentProfileRef
}
```

`plannedPairSlots` is the frozen number of assignment opportunities, not a promise that the same number of pairs will complete or remain eligible.

Only use version fields that actually exist in their owner contract/implementation. `assignmentServiceVersion` is implementation provenance for reproducing the atomic enrollment path, not a new experiment semantic owner. `participantReusePolicy` and `fallbackClassificationOwner` may be stored through equivalent run metadata; exact serialization is an IMPLEMENTATION BINDING TBD, but the semantics are mandatory.

The exact storage schema is IMPLEMENTATION BINDING TBD.

---

# 56. Run Freeze Rule

Before MAIN collection begins, freeze the run manifest and allocation schedule.

After freeze, changes such as:

- AED score thresholds;
- evidence sample threshold;
- parameter candidateValues/bounds;
- Fixed baseline;
- Profile normalization/filter;
- build;
- map content;
- experiment assignment semantics;
- `plannedPairSlots`;
- allocation schedule entries/order;
- participant-reuse rule;
- fallback-classification semantics;
- primary endpoint semantics;

must not silently continue within the same homogeneous run.

The run must be suspended/closed and a new run/cohort created unless an already-frozen compatibility/stratification rule explicitly permits otherwise.

Within the current run:

```text
CONSUMED schedule slot
→ never returns to UNUSED
```

and:

```text
assignedPairCount == plannedPairSlots
→ enrollment closed
```

No post-hoc schedule extension exists in v1.1.

---

# 57. Version Ownership

| Semantic | Owner |
|---|---|
| experiment design, assignment, pair/inclusion topology | `experimentProtocolVersion` |
| analysis model, endpoint hierarchy, missingness, multiplicity, uncertainty | `analysisPlanVersion` |
| AED semantic rules | `policyVersion` |
| AED score-band numerical thresholds | `policyConfigVersion` |
| Adaptive evidence eligibility thresholds/topology | `evidencePolicyVersion` |
| adaptive parameter bounds/defaults/candidate values/pressure metadata | `parameterRegistryVersion` |
| adaptive content whitelist | `contentWhitelistVersion` |
| fixed baseline content | `fallbackConfigVersion` / baseline identity |
| Profile formula/comparison semantics | Profile-owned versions |
| Telemetry wire | `schemaVersion` |
| gameplay-owned Match outcome tokens | `MatchOutcomeRegistry` version |
| applied ScenarioConfig content | `scenarioConfigVersion` |
| analysis dataset content | `datasetVersion` |

One product change may require coordinated version changes across more than one owner.

---

# 58. Data Cutoff

A frozen analysis dataset has:

```text
dataCutoff
```

Late evidence after cutoff:

- remains auditable;
- cannot silently mutate the analyzed dataset;
- may trigger a new dataset version and amended/re-run analysis if accepted under governance.

---

# 59. Late Data Invalidation

Before cutoff:

```text
late Telemetry/Profile correction
→ recompute affected match eligibility
→ recompute metric availability
→ recompute pair eligibility
→ rebuild dataset deterministically
```

After cutoff:

```text
old dataset remains immutable
→ correction creates explicit dataset revision
```

No manual spreadsheet row editing is an authoritative correction path.

---

# 60. AnalysisDatasetManifest

```text
AnalysisDatasetManifest
{
    datasetVersion
    experimentRunId
    experimentProtocolVersion
    analysisPlanVersion
    dataCutoff

    plannedPairSlots
    assignedPairCount
    completedPairCount
    eligiblePairCount
    excludedPairCount

    assignmentRecordRefs[]
    enrollmentAttemptAuditRefs[]
    slotOwnershipByScheduleSlotId{}
    periodBindingRefs[]

    includedMatchIds[]
    excludedMatches[] { matchId, reasons[] }

    includedPairIds[]
    excludedPairIds[]
    pairMetricEligibility{}

    assignedConditionByMatch{}
    exposureClassByAdaptiveMatch{}
    fallbackExperimentClassificationByAdaptiveMatch{}

    participantToMainPairMembership{}

    sourceEvidenceFingerprint
    runManifestFingerprint
}
```

`participantToMainPairMembership` exists for research-integrity/reproduction of the one-pair-per-player rule; it is not a persistent gameplay Team identity. `slotOwnershipByScheduleSlotId` and `enrollmentAttemptAuditRefs` preserve enough research evidence to verify atomic commit/idempotency/conflict handling without making failed attempts into assigned pairs.

Dataset version/fingerprint must allow exact reconstruction.

---

# 61. Stop / Suspension Rules

Suspend the MAIN run and investigate on any hard technical/safety/integrity defect such as:

```text
soft-lock
objective or Exit unreachable
unauthorized adaptive mutation
partial/nonatomic ScenarioConfig apply
Host authority violation
decision idempotency/reproducibility failure
assignment corruption
period-binding identity conflict
late-detected cross-pair participant reuse
critical Telemetry identity/sequence corruption
fixed fallback unavailable
fatal ScenarioConfig validation defect
POLICY_CONFIG_INVALID during MAIN
PARAMETER_REGISTRY_INVALID during MAIN
UNSUPPORTED_VERSION during MAIN
DECISION_IDENTITY_CONFLICT
FALLBACK_CONFIG_INVALID
AED_UNAVAILABLE
AED_TIMEOUT
dataset reproducibility failure
```

The exact upstream reason is retained. Experiment suspension does not rewrite that reason.

A known broken implementation must not continue collecting confirmatory data merely to reach `plannedPairSlots` or a desired eligible-pair count.

---

# 62. No Outcome-Based Early Stopping

Do not stop because:

```text
Adaptive looks better
Adaptive looks worse
a p-value crosses a threshold
```

unless a separate sequential-analysis plan was approved and frozen before main collection.

Technical/safety suspension is different from statistical early stopping.

---

# 63. Pilot

Pilot is explicitly separate from MAIN confirmatory collection.

Pilot may validate:

- instrumentation;
- match/provenance reconstruction;
- assignment/period-binding lifecycle;
- schedule-slot consumption;
- cross-pair participant uniqueness enforcement;
- fallback classification;
- roster/pair workflow;
- AED tuning;
- fixed baseline;
- endpoint extractor;
- variability/event rate;
- missingness/dropout;
- analysis code;
- operational duration/feasibility.

Default rule:

```text
pilot influences tuning, endpoint selection, statistical model, or sample-size planning
→ pilot excluded from confirmatory MAIN dataset
```

Pilot participation does not consume a MAIN schedule slot and is outside the same-run MAIN uniqueness rule unless the pilot itself is explicitly using the same MAIN `experimentRunId`—which is not the default design.

If a pilot participant later enters MAIN, `priorExperimentExposureCount` and relevant prior-exposure provenance remain available for analysis/limitations; pilot data is not silently pooled.

Any exception must be declared before MAIN collection.

---

# 64. Tuning Freeze

Before MAIN collection freeze at minimum:

- AEDPolicyConfig score-band thresholds;
- AEDEvidencePolicy sample thresholds;
- AdaptiveParameterRegistry candidateValues/bounds;
- Fixed baseline content;
- Profile metric/filter/normalization configs used by analysis.

Do not tune on main outcomes and then analyze those same matches as though the policy had been pre-specified.

---

# 65. Confirmatory Endpoint Hierarchy

v1.1 freezes a **single confirmatory primary endpoint**:

```text
1. ExperimentTeamSurvival_v1.1 — CONFIRMATORY PRIMARY
```

All other currently available gameplay outcomes are secondary/exploratory unless a protocol revision before main collection explicitly changes the hierarchy.

Consequences:

- no multiple-primary multiplicity problem is created by the current protocol;
- secondary p-values, if any, cannot be promoted post hoc to replace a null primary result;
- adding co-primary endpoints requires an explicit pre-main protocol/analysis revision and multiplicity plan.

---

# 66. Analysis Plan Contract

Logical identity:

```text
analysisPlanVersion
```

It must be frozen before MAIN confirmatory collection.

Required contents:

- primary endpoint extractor/version;
- assigned-condition estimand;
- pair eligibility;
- statistical model/test;
- effect measure;
- period handling;
- sequence handling;
- Profile-evolution/carryover interpretation;
- missing-data method;
- outlier policy if any;
- uncertainty interval method/level;
- significance threshold if formal hypothesis testing is used;
- secondary endpoint analysis;
- Objective Completion mapping/version if activated;
- conditional objectiveTime interpretation;
- exposure/fallback secondary analysis;
- pilot exclusion/inclusion;
- any subgroup/monster strata;
- subjective layer if activated;
- multiplicity rule for any confirmatory expansion.

---

# 67. Statistical Model Selection

Current exact model/test:

```text
TBD BEFORE MAIN STUDY
BLOCKS MAIN EXPERIMENT
```

This is intentional because current evidence does not freeze:

- main N;
- distribution/event rate;
- within-pair variance/correlation;
- practical missingness;
- approved alpha/power/effect target.

## 67.1 Required design properties

Whatever model is frozen must:

- respect same-roster repeated observations;
- not treat Player rows as independent treatment replicates;
- include/adjust/report period effect;
- retain sequence assignment;
- address order/Profile evolution explicitly;
- use the assigned-condition estimand;
- be valid for the Team Survival endpoint scale and observed sample size.

## 67.2 Carryover handling

Do not use the flawed workflow:

```text
test carryover
→ if significant discard Period 2
→ otherwise use both periods
```

as an unplanned data-dependent model selector.

Carryover/Profile-evolution effects are design characteristics to be modeled/reported or used to limit interpretation under the pre-frozen analysis plan.

---

# 68. Effect Size and Uncertainty

Final report must present:

- condition summaries;
- assigned-condition effect estimate;
- uncertainty interval;
- eligible N/pairs;
- period and sequence summaries;
- missing/exclusion counts;
- actual Adaptive exposure;
- limitations.

Do not report only a p-value.

Exact confidence/uncertainty level and formal significance threshold remain:

```text
TBD BEFORE MAIN STUDY
BLOCKS MAIN EXPERIMENT
```

until the analysis plan is frozen.

---

# 69. Sample Size / Planned Pair Slots

Exact pair/player/match count is not supported by current evidence.

Therefore:

```text
MAIN SAMPLE SIZE
=
TBD BEFORE MAIN STUDY
```

Resolution requires:

- frozen confirmatory endpoint;
- chosen statistical model;
- paired/repeated design;
- pilot or justified prior estimate of variance/event rate;
- within-pair correlation where needed;
- approved minimum effect of interest if used;
- approved power target if inferential testing is used;
- participant/roster feasibility;
- expected pair dropout/exclusion;
- one-MAIN-pair-per-player constraint.

The planning process must resolve a single run-level quantity:

```text
plannedPairSlots
=
number of pair assignment opportunities permitted in the frozen MAIN schedule
```

Expected dropout/exclusion must be incorporated **before** freeze when determining `plannedPairSlots`. This contract does not define a separate target that authorizes continued enrollment until a desired number of eligible/completed pairs is achieved.

After `plannedPairSlots` is frozen, generate exactly that many schedule entries under §22.

No arbitrary “30 players,” “50 matches,” or similar number is permitted.

---

# 70. Multiple Endpoints

Current protocol has one confirmatory primary endpoint.

Secondary/exploratory endpoint multiplicity does not permit confirmatory claims unless a frozen analysis plan explicitly controls that family.

If future pre-main revision introduces co-primary endpoints:

```text
endpoint hierarchy
+
multiplicity method
+
analysisPlanVersion update
```

are mandatory before collection.

---

# 71. Current Implementation Assessment

```text
CURRENT EXPERIMENT IMPLEMENTATION:
NOT EVIDENCED FROM SUPPLIED SOURCE
```

The supplied project contracts specify required behavior, but no evidence currently proves completion of the following experiment-specific runtime/research modules.

| Module | Evidence | Status | Needed work |
|---|---|---|---|
| ExperimentRunManager | not evidenced | NOT IMPLEMENTED / UNKNOWN | run lifecycle and freeze |
| ExperimentReadinessEvaluator | not evidenced | NOT IMPLEMENTED / UNKNOWN | evidence-backed readiness gate |
| SequenceAssignmentService | not evidenced | NOT IMPLEMENTED / UNKNOWN | §22 frozen schedule + slot proposal; no independent final consumption |
| MainPairEnrollmentCoordinator | not evidenced | NOT IMPLEMENTED / UNKNOWN | logical atomic assignment + slot + complete participant-membership commit |
| ExperimentAssignmentLedger | not evidenced | NOT IMPLEMENTED / UNKNOWN | immutable assignment-time facts committed inside enrollment boundary |
| ExperimentPeriodBindingLedger | not evidenced | NOT IMPLEMENTED / UNKNOWN | immutable `(pairId,periodIndex)→matchId` binding |
| MainParticipantEnrollmentIndex | not evidenced | NOT IMPLEMENTED / UNKNOWN | authoritative same-run uniqueness constraint participating in atomic enrollment, not eventually-consistent precheck cache |
| AdaptiveFallbackExperimentClassifier | not evidenced | NOT IMPLEMENTED / UNKNOWN | §28 authoritative AED reason mapping |
| ExperimentEvidenceCollector | not evidenced | NOT IMPLEMENTED / UNKNOWN | evidence references |
| ExperimentEligibilityEvaluator | not evidenced | NOT IMPLEMENTED / UNKNOWN | match/metric exclusions |
| ExperimentPairBuilder | not evidenced | NOT IMPLEMENTED / UNKNOWN | same-roster pair construction from bindings |
| OutcomeExtractor | not evidenced | NOT IMPLEMENTED / UNKNOWN | Team Survival + valid secondaries |
| AdaptiveExposureClassifier | not evidenced | NOT IMPLEMENTED / UNKNOWN | APPLIED/NO_CHANGE/FALLBACK/integrity evidence |
| AnalysisDatasetBuilder | not evidenced | NOT IMPLEMENTED / UNKNOWN | immutable versioned dataset |
| ExperimentReproductionVerifier | not evidenced | NOT IMPLEMENTED / UNKNOWN | deterministic rebuild |
| Analysis pipeline | not evidenced | NOT COMPLETE | frozen plan execution |

Exact class names are implementation bindings.

---

# 72. Main Experiment Current Blockers

The contract design is complete, but MAIN execution is NOT READY because supplied evidence does not demonstrate completion of:

1. AED implementation and authoritative integration.
2. AED policy/evidence/parameter tuning freeze.
3. Fixed baseline run-specific content freeze.
4. Atomic MAIN enrollment implementation: assignment identity + slot ownership + participant uniqueness + idempotency/conflict/crash-retry semantics, plus separate period-binding and fallback-classification implementation.
5. Experiment readiness/evidence/dataset implementation.
6. Pre-run validation test execution and passing evidence.
7. Pilot/instrumentation validation where needed.
8. `analysisPlanVersion` freeze.
9. Exact statistical model/test freeze.
10. uncertainty/significance policy freeze if formal inference is used.
11. main-study sample-size / `plannedPairSlots` determination.
12. main balanced allocation schedule generation/freeze.
13. immutable `ExperimentRunManifest` freeze.

These are execution blockers, not reasons to fabricate an incomplete protocol.

---

# 73. Experiment Implementation Components

Recommended logical responsibilities:

### `ExperimentRunManager`

Owns run lifecycle and immutable manifest freeze.

### `ExperimentReadinessEvaluator`

Consumes implementation/test/config evidence and returns PASS/FAIL for MAIN readiness. It does not alter gameplay.

### `MainPairEnrollmentCoordinator`

Owns the logical `MainPairEnrollmentCommit`: authoritative commit-time precondition revalidation, pair identity/idempotency, slot ownership, complete participant membership, deterministic conflicts, and crash/retry convergence. Exact coordinator/class name is an implementation binding.

### `SequenceAssignmentService`

Generates/reads the frozen `plannedPairSlots` schedule and proposes the next UNUSED slot candidate. It does not independently finalize `CONSUMED` state outside `MainPairEnrollmentCommit`.

### `ExperimentAssignmentLedger`

Stores immutable successful assignment-time provenance as part of the logical enrollment commit only. It never receives future period match IDs and never overwrites conflicting assignment semantics.

### `ExperimentPeriodBindingLedger`

Owns append-only/immutable `(experimentRunId,pairId,periodIndex)→matchId` bindings and rejects conflicting rebinds.

### `MainParticipantEnrollmentIndex`

Represents/enforces the authoritative logical `MainParticipantMembershipKey` uniqueness constraint inside the same successful enrollment commit. It may support an advisory precheck, but it cannot be merely an eventually-consistent cache whose stale read can permit two commits.

### `AdaptiveFallbackExperimentClassifier`

Consumes authoritative AED `CandidateValidationStatus + result/path + reasonCode` and applies §28. It never changes AED reasons or gameplay behavior.

### `ExperimentEvidenceCollector`

Resolves references to Telemetry, Profile snapshots, AdaptiveDecision, ScenarioConfig, assignment, binding, and run configuration without duplicating their authoritative semantics.

### `ExperimentEligibilityEvaluator`

Applies experiment-owned match exclusion rules, including fallback classification and cross-pair integrity.

### `ExperimentPairBuilder`

Builds same-roster pairs from immutable assignment + period bindings; derived pair match IDs never override binding truth.

### `OutcomeExtractor`

Derives only source-supported experiment outcomes.

### `AdaptiveExposureClassifier`

Maps authoritative A-assigned resolution evidence to APPLIED/NO_CHANGE/FIXED_FALLBACK/integrity exposure classes without changing assignment.

### `AnalysisDatasetBuilder`

Produces immutable `AnalysisDatasetManifest` and data snapshot, including assignment/slot/count/binding/participant-membership and enrollment-integrity evidence.

### `ExperimentReproductionVerifier`

Rebuilds committed assignment identity, slot ownership, participant memberships, assignedPairCount, bindings, fallback classification, eligibility, and dataset from the same frozen evidence/version package and compares fingerprints.

Avoid a monolithic `ResearchManager`.

---

# 74. Experiment Run Lifecycle

```text
DRAFT
→ PILOT_READY
→ PILOT
→ MAIN_FREEZE_PENDING
→ MAIN_READY
→ MAIN_COLLECTING
→ SUSPENDED?
→ COLLECTION_CLOSED
→ DATASET_FROZEN
→ ANALYZED
→ REPORTED
```

Exact enum names are implementation binding.

Hard semantics:

- `MAIN_READY` requires §31 PASS;
- `MAIN_COLLECTING` cannot begin from FAIL;
- `plannedPairSlots` and schedule are frozen before MAIN enrollment;
- advisory participant uniqueness may be checked before slot proposal, but authoritative uniqueness is revalidated/reserved inside `MainPairEnrollmentCommit`;
- successful enrollment atomically commits immutable AssignmentRecord + one consumed slot + the complete roster participant membership set;
- period match IDs enter only through immutable PeriodBinding records;
- hard safety/system/config integrity defects may transition to SUSPENDED;
- version drift may close/split a run;
- `assignedPairCount == plannedPairSlots` closes further enrollment even if some pairs later drop out/exclude;
- DATASET_FROZEN requires explicit cutoff.

---

# 75. Pre-Run Validation Tests

Required; **no PASS is claimed**.

| ID | Test | Expected |
|---|---|---|
| EXP-E-001 | protocol semantic ID resolves | supported exact version |
| EXP-E-002 | run manifest required fields | valid/frozen before MAIN |
| EXP-E-003 | Telemetry wire version | `"1.1"` |
| EXP-E-004 | MATCH_STARTED experiment provenance | condition + protocol captured |
| EXP-E-005 | Fixed baseline ID/version/fingerprint | resolvable |
| EXP-E-006 | AED `policyVersion` | `AED_SCENARIO_POLICY_V1_1` supported |
| EXP-E-007 | policyConfigVersion | resolvable/frozen |
| EXP-E-008 | evidencePolicyVersion | resolvable/frozen |
| EXP-E-009 | parameterRegistryVersion | valid/frozen |
| EXP-E-010 | contentWhitelistVersion | valid/frozen |
| EXP-E-011 | Profile v1.1 pipeline | current snapshots resolvable |
| EXP-E-012 | AdaptiveInputSnapshot | deterministic fingerprint/current revision |
| EXP-E-013 | ScenarioValidator | operational |
| EXP-E-014 | FixedDirector | operational |
| EXP-E-015 | Host authoritative config apply | operational |
| EXP-E-016 | AdaptiveDecision ledger | exactly-once evidence operational |
| EXP-E-017 | assignment service | frozen method + exact `plannedPairSlots` schedule |
| EXP-E-018 | assignment record schema | no period match IDs; immutable after commit |
| EXP-E-019 | period-binding ledger | idempotent same binding; conflicting match rejected |
| EXP-E-020 | participant uniqueness index | authoritative commit-time same-run second-pair membership is rejected atomically; advisory precheck alone is not acceptance authority |
| EXP-E-021 | fallback classifier | exact §28 mapping for authoritative AED reasons |
| EXP-E-022 | slot lifecycle | UNUSED→CONSUMED once; no recycle path |
| EXP-E-023 | analysisPlanVersion | frozen |
| EXP-E-024 | Team Survival extractor | source-valid reproduction |
| EXP-E-025 | dataset builder | deterministic |
| EXP-E-026 | source-version drift detector | detects incompatible drift |
| EXP-E-027 | readiness FAIL | prevents MAIN A scheduling |
| EXP-E-028 | MAIN enrollment atomicity implementation | assignment + slot + complete participant membership share one logical commit boundary |
| EXP-E-029 | participant uniqueness concurrency test evidence | two shared-player commits cannot both succeed |
| EXP-E-030 | slot uniqueness concurrency test evidence | one schedule slot cannot have two committed pair owners |
| EXP-E-031 | enrollment crash/retry evidence | pre-commit crash leaves none; post-commit/pre-ACK retry is idempotent |
| EXP-E-032 | partial durable enrollment audit | orphan/partial state detected as integrity failure, not accepted MAIN state |

Passing results must come from actual execution evidence; this document does not assert them.

---

# 76. Assignment / Binding / Slot / Participant Tests

Required; no PASS is claimed.

## 76.1 Existing assignment mechanism tests

| ID | Case | Expected |
|---|---|---|
| EXP-A-001 | even `plannedPairSlots` | equal F→A and A→F slots |
| EXP-A-002 | odd `plannedPairSlots` | sequence count difference exactly 1 |
| EXP-A-003 | same seed/method/plannedPairSlots | same schedule fingerprint |
| EXP-A-004 | different implementation order of map/dictionary | no schedule semantic change |
| EXP-A-005 | eligible pair enrollment | one atomic enrollment commit owns assignment + one slot + complete roster membership |
| EXP-A-006 | exact assignment retry after success | IDEMPOTENT_SUCCESS; same assignment; no duplicate count/slot/membership |
| EXP-A-007 | same pair assignment identity with different sequence/roster/slot | ASSIGNMENT_IDENTITY_CONFLICT; no overwrite/no partial mutation |
| EXP-A-008 | Period-1 outcome exists | assignment cannot change |
| EXP-A-009 | F→A record | immutable |
| EXP-A-010 | A→F record | immutable |
| EXP-A-011 | future schedule slot hidden operationally where supported | no manual selection path |

## 76.2 EXP-DD-01 lifecycle tests

| ID | Case | Expected |
|---|---|---|
| EXP-A-LIFE-001 | assignment committed before Period-1 match exists | valid immutable assignment; no future match ID required |
| EXP-A-LIFE-002 | bind Period-1 match later | AssignmentRecord byte/semantic content unchanged |
| EXP-A-LIFE-003 | bind Period-2 match later | AssignmentRecord unchanged |
| EXP-A-LIFE-004 | duplicate same `(pairId,periodIndex,matchId)` binding | idempotent |
| EXP-A-LIFE-005 | same pair/period bound to different `matchId` | `PERIOD_BINDING_IDENTITY_CONFLICT`; no overwrite |
| EXP-A-LIFE-006 | Period 2 never occurs | assignment remains valid historical evidence; pair incomplete; slot still consumed |

## 76.3 EXP-DD-03 slot/dropout tests

| ID | Case | Expected |
|---|---|---|
| EXP-SLOT-001 | `plannedPairSlots` frozen before assignment | schedule length exactly equals `plannedPairSlots` |
| EXP-SLOT-002 | pair assigned | slot consumed exactly once |
| EXP-SLOT-003 | pair withdraws after assignment | slot not reused |
| EXP-SLOT-004 | Period 2 missing | slot not reused |
| EXP-SLOT-005 | roster changes | old pair invalid; replacement roster requires new pair + unused slot |
| EXP-SLOT-006 | all slots consumed | no further MAIN enrollment |
| EXP-SLOT-007 | request extra pair because prior outcome poor | prohibited |
| EXP-SLOT-008 | request replacement because prior pair excluded | prohibited; no v1.1 replacement rule |
| EXP-SLOT-009 | schedule regeneration after observed outcome | prohibited |

## 76.4 EXP-DD-04 participant tests

| ID | Case | Expected |
|---|---|---|
| EXP-PART-001 | Player appears in first MAIN pair | accepted if other gates pass |
| EXP-PART-002 | same Player attempts second MAIN pair in same run | `CROSS_PAIR_PARTICIPANT_REUSE`; atomic commit fails; request consumes no slot or new participant membership |
| EXP-PART-003 | same Player existed in PILOT | MAIN governed by pilot/main policy; pilot not silently pooled; prior exposure retained |
| EXP-PART-004 | same Player in different `experimentRunId` | governed by that run's enrollment contract |
| EXP-PART-005 | overlap discovered after assignment via identity conflict | affected pair not silently independent; confirmatory eligibility fails; slot remains consumed; integrity review |

## 76.5 EXP-DD-05 atomic MAIN enrollment tests

| ID | Case | Expected |
|---|---|---|
| EXP-ENR-001 | P1 and P2 concurrently enroll with shared User A | exactly one commit succeeds; loser = `CROSS_PAIR_PARTICIPANT_REUSE`; User A has one membership only |
| EXP-ENR-002 | P1 and P2 race for same UNUSED scheduleSlotId | exactly one commit succeeds; slot has one pair owner; loser = `SCHEDULE_SLOT_CONFLICT`; loser leaves no AssignmentRecord/membership |
| EXP-ENR-003 | process crashes before durable logical enrollment commit | no AssignmentRecord; slot logically UNUSED; no participant reservation |
| EXP-ENR-004 | commit durable, caller crashes/times out before ACK, exact request retried | `IDEMPOTENT_SUCCESS`; exactly one AssignmentRecord, one consumed slot, one complete membership set |
| EXP-ENR-005 | exact retry of committed pair+roster+slot+sequence | existing assignment returned; `assignedPairCount` unchanged |
| EXP-ENR-006 | same pairId but different roster/slot/sequence | `ASSIGNMENT_IDENTITY_CONFLICT`; no overwrite/no partial mutation |
| EXP-ENR-007 | user already committed to P1; P2 enrollment requests otherwise-unused slot | `CROSS_PAIR_PARTICIPANT_REUSE`; P2 slot remains UNUSED; no P2 assignment/membership |
| EXP-ENR-008 | schedule slot already consumed by P1; P2 requests same slot | `SCHEDULE_SLOT_CONFLICT`; no overwrite; no P2 assignment/membership |
| EXP-ENR-009 | after duplicate/crash/retry scenarios | `assignedPairCount == unique committed AssignmentIdentityKey == CONSUMED slot count` |
| EXP-ENR-010 | every committed pair audit | all expected roster membership keys point to exactly that pair; no partial roster reservation |

---

# 77. Condition Classification Tests

| ID | Evidence | Expected primary label |
|---|---|---|
| EXP-C-001 | F assigned + FixedDirector | F |
| EXP-C-002 | A assigned + APPLIED | A |
| EXP-C-003 | A assigned + NO_CHANGE | A |
| EXP-C-004 | A assigned + FIXED_FALLBACK | A |
| EXP-C-005 | A fallback gives `configSource=FIXED` | still A |
| EXP-C-006 | configSource ADAPTIVE without valid A assignment | assignment invalid; do not infer A |
| EXP-C-007 | condition changed after outcome | assignment conflict/protocol violation |

---

# 78. Eligibility / Fallback Tests

Required; no PASS is claimed.

## 78.1 General eligibility

| ID | Case | Expected |
|---|---|---|
| EXP-L-001 | MATCH_ABORTED | match EXCLUDED |
| EXP-L-002 | assignment missing | EXCLUDED |
| EXP-L-003 | assignment conflict | EXCLUDED |
| EXP-L-004 | period binding conflict | EXCLUDED; no overwrite |
| EXP-L-005 | protocol mismatch | EXCLUDED |
| EXP-L-006 | incompatible build drift | EXCLUDED or separate predeclared cohort; never silent pool |
| EXP-L-007 | critical Telemetry corruption prevents primary | EXCLUDED |
| EXP-L-008 | secondary metric unavailable only | whole match retained; metric null |
| EXP-L-009 | A started while system readiness FAIL | EXCLUDED + protocol violation |
| EXP-L-010 | poor A outcome | no exclusion |
| EXP-L-011 | poor F outcome | no exclusion |
| EXP-L-012 | roster mismatch across periods | pair invalid |
| EXP-L-013 | cross-pair participant overlap | confirmatory pair invalid |
| EXP-L-014 | F metric available, A metric unavailable | no paired metric observation |
| EXP-L-015 | both primary Team Survival values available | primary pair eligible if all other pair rules pass |
| EXP-L-016 | late source invalidation before cutoff | eligibility/dataset rebuilt |

## 78.2 EXP-DD-02 fallback classification

| ID | Case | Expected |
|---|---|---|
| EXP-FB-001 | A + `INPUT_INCOMPLETE` expected fallback | assignment A retained; `EXPECTED_TREATMENT_FALLBACK`; normally eligible if other criteria pass |
| EXP-FB-002 | A + `POLICY_CONFIG_INVALID` | assignment A retained; `PROTOCOL_OR_CONFIG_INVALID`; match excluded; run suspended |
| EXP-FB-003 | A + `PARAMETER_REGISTRY_INVALID` | deterministic `PROTOCOL_OR_CONFIG_INVALID`; exclude + suspend |
| EXP-FB-004 | A + `UNSUPPORTED_VERSION` | deterministic integrity handling; exclude + suspend |
| EXP-FB-005 | A + valid candidate-stage Scenario/route/spawn/concurrency safety rejection with frozen infrastructure valid | `SAFETY_OR_VALIDATION_REJECTION`; assignment A; eligible if fallback succeeds/other criteria pass |
| EXP-FB-006 | A + `FALLBACK_CONFIG_INVALID` | assignment A retained; fatal/integrity exclusion; run suspended; no normal confirmatory exposure |
| EXP-FB-007 | any fallback exclusion | never relabel A to F |
| EXP-FB-008 | poor Adaptive gameplay outcome alone | never causes exclusion |
| EXP-FB-009 | A + `AED_UNAVAILABLE` | A retained; `SYSTEM_FAILURE_FALLBACK`; excluded; run suspended |
| EXP-FB-010 | A + `AED_TIMEOUT` | A retained; `SYSTEM_FAILURE_FALLBACK`; excluded; run suspended |
| EXP-FB-011 | A + `STALE_INPUT` | expected treatment fallback if upstream reason exact; normally eligible |
| EXP-FB-012 | `SCENARIO_INVALID + NOT_EVALUATED` | protocol/config invalid; excluded + suspend |
| EXP-FB-013 | `SCENARIO_INVALID + INVALID` candidate-stage | safety/validation rejection; may remain eligible |
| EXP-FB-014 | `DECISION_IDENTITY_CONFLICT` | A retained; excluded; run suspended immediately |
| EXP-FB-015 | `BOUND_REJECTED` under MAIN v1.1 policy | `PROTOCOL_OR_CONFIG_INVALID`; exclude + suspend |
| EXP-FB-016 | `TIMING_REJECTED` under MAIN v1.1 policy | `PROTOCOL_OR_CONFIG_INVALID`; exclude + suspend |
| EXP-FB-017 | `PRESSURE_RULE_REJECTED` under MAIN v1.1 policy | `PROTOCOL_OR_CONFIG_INVALID`; exclude + suspend |

---

# 79. Profile Evolution / Order Tests

| ID | Case | Expected |
|---|---|---|
| EXP-O-001 | F→A | both period labels and Profile revisions retained |
| EXP-O-002 | A→F | both period labels and Profile revisions retained |
| EXP-O-003 | Period-1 Profile update | allowed; no experiment freeze |
| EXP-O-004 | second-period Profile differs | legitimate evidence; not stale by itself |
| EXP-O-005 | stale AdaptiveInputSnapshot | upstream AED rejects; experiment records result |
| EXP-O-006 | analysis data | includes period + sequence |
| EXP-O-007 | attempt to assume carryover=0 silently | protocol violation |
| EXP-O-008 | attempt to discard Period 2 after post-hoc carryover test | analysis-plan violation |

---

# 80. Exposure Tests

| ID | Case | Expected |
|---|---|---|
| EXP-X-001 | A PRE_MATCH APPLIED | `ADAPTIVE_DELTA_APPLIED` |
| EXP-X-002 | A PRE_MATCH NO_CHANGE | `ADAPTIVE_EVALUATED_NO_CHANGE` |
| EXP-X-003 | A PRE_MATCH FIXED_FALLBACK | `ADAPTIVE_FIXED_FALLBACK` plus §28 fallback class |
| EXP-X-004 | A NO_CHANGE with FIXED configSource | assignment remains A |
| EXP-X-005 | A fallback excluded by §28 | assignment remains A; exclusion does not relabel |
| EXP-X-006 | boundary HOLD/NO_CHANGE | does not rewrite match assignment |
| EXP-X-007 | identity/fatal Adaptive path | `ADAPTIVE_INTEGRITY_FAILURE`; not normal confirmatory exposure |
| EXP-X-008 | exposure analysis used as primary replacement | reject analysis configuration |

---

# 81. Outcome Tests

| ID | Case | Expected |
|---|---|---|
| EXP-M-001 | teamSize=4, survivorCount=3 valid | TeamSurvivalProportion=0.75 |
| EXP-M-002 | survivorCount=0 valid | valid primary value 0 |
| EXP-M-003 | missing MATCH_ENDED | Team Survival unavailable |
| EXP-M-004 | survivorCount > teamSize | invalid |
| EXP-M-005 | objective outcome token not in frozen mapping | ObjectiveCompletion unavailable |
| EXP-M-006 | only JSON example `SUCCESS` known | no invented complete mapping |
| EXP-M-007 | valid complete objective-bearing phase pairs | objectiveTime computed by source semantic |
| EXP-M-008 | incomplete objective | objectiveTime null for conditional analysis; not zero |
| EXP-M-009 | Security Hold interruption | source wall-clock objectiveTime semantic preserved |
| EXP-M-010 | raw Noise count | not interpreted as intrinsically good/bad |

---

# 82. Dataset / Reproduction Tests

| ID | Case | Expected |
|---|---|---|
| EXP-D-001 | same frozen evidence | same match eligibility |
| EXP-D-002 | same evidence | same pair eligibility |
| EXP-D-003 | same AED status/result/reason | same fallback classification + exposure class |
| EXP-D-004 | same assignment/bindings | same period match identity |
| EXP-D-005 | same participant membership | same cross-pair uniqueness result |
| EXP-D-006 | same frozen evidence | same dataset fingerprint |
| EXP-D-007 | excluded match | remains auditable |
| EXP-D-008 | missing metric | remains null |
| EXP-D-009 | late invalidation before cutoff | new deterministic dataset content/version |
| EXP-D-010 | late invalidation after cutoff | frozen old dataset unchanged; explicit new revision |
| EXP-D-011 | incompatible run versions | not silently pooled |
| EXP-D-012 | duplicate Telemetry transport | no duplicate team-match unit |
| EXP-D-013 | manual spreadsheet row edit | cannot become authoritative dataset build |
| EXP-D-014 | same team-match | counted once per assigned exposure |
| EXP-D-015 | dropped/excluded pair | does not free or remove consumed schedule slot from run accounting |
| EXP-D-016 | same committed enrollment evidence | same assignment, slot owner, participant memberships, and assignedPairCount |
| EXP-D-017 | exact retry audit evidence present | does not create second assigned pair in reproduction |
| EXP-D-018 | partial enrollment durable state detected | dataset build/readiness flags `ENROLLMENT_INTEGRITY_FAILURE`; no silent repair |

---

# 83. Safety / Suspension Tests

| ID | Case | Expected |
|---|---|---|
| EXP-SAF-001 | unauthorized adaptive mutation | suspend/investigate |
| EXP-SAF-002 | partial config apply | suspend/investigate |
| EXP-SAF-003 | objective/Exit unreachable | suspend/investigate |
| EXP-SAF-004 | proxy-authoritative ScenarioConfig apply | suspend/investigate |
| EXP-SAF-005 | decision reproduction mismatch | suspend/investigate |
| EXP-SAF-006 | fixed fallback unavailable | suspend/no MAIN continuation |
| EXP-SAF-007 | condition assignment corruption | suspend/investigate |
| EXP-SAF-008 | period-binding identity conflict | suspend/investigate; no overwrite |
| EXP-SAF-009 | AED_UNAVAILABLE/TIMEOUT during MAIN | affected A match excluded; run suspended for readiness review |
| EXP-SAF-010 | frozen policy/registry/version invalid | affected A match excluded; run suspended |
| EXP-SAF-011 | p-value becomes small | no automatic stop |
| EXP-SAF-012 | Adaptive looks poor | no automatic stop |
| EXP-SAF-013 | partial durable MAIN enrollment state detected | suspend/investigate; no Period start from partial state |
| EXP-SAF-014 | two committed pairs share one MainParticipantMembershipKey | enrollment integrity violation; suspend/investigate |
| EXP-SAF-015 | one consumed slot maps to two assignments or no assignment | enrollment integrity violation; suspend/investigate |

---

# 84. Failure Matrix

## 84.1 MAIN Enrollment Atomicity Failures

| Failure / condition | Enrollment result | Assignment effect | Slot effect | Participant effect | Run effect |
|---|---|---|---|---|---|
| exact retry of already committed semantic request | `IDEMPOTENT_SUCCESS` | existing immutable assignment returned; no duplicate | existing consumed slot unchanged | existing complete membership set unchanged | continue |
| same AssignmentIdentityKey + different semantic payload | FAIL / `ASSIGNMENT_IDENTITY_CONFLICT` | no overwrite | no new slot mutation by request | no new membership mutation | investigate if systemic |
| concurrent/pre-existing participant ownership by different pair | FAIL / `CROSS_PAIR_PARTICIPANT_REUSE` | no assignment for losing request | requested slot remains unconsumed by losing request | no membership committed by losing request | continue after expected conflict; investigate if committed-state overlap exists |
| same slot already/ concurrently consumed by different pair | FAIL / `SCHEDULE_SLOT_CONFLICT` | no assignment for losing request | no overwrite; one pair owner only | no membership committed by losing request | contention retry may start as a new full attempt if §22.4.6 preconditions hold |
| crash before durable logical commit | NO COMMIT | none | logically UNUSED | none | safe full retry |
| crash after durable commit before ACK | committed; exact retry → `IDEMPOTENT_SUCCESS` | exactly one immutable assignment | exactly one consumed slot | exactly one complete membership set | continue after state resolution |
| AssignmentRecord exists but slot logically UNUSED | `ENROLLMENT_INTEGRITY_FAILURE` | partial durable state prohibited | inconsistent | possibly inconsistent | **SUSPEND + investigate** |
| slot CONSUMED without committed AssignmentRecord | `ENROLLMENT_INTEGRITY_FAILURE` | missing | orphan consumed slot | possibly orphaned/none | **SUSPEND + investigate** |
| only subset of roster participant memberships exists for committed assignment | `ENROLLMENT_INTEGRITY_FAILURE` | assignment cannot be accepted as healthy MAIN enrollment | corresponding slot state cannot justify success alone | partial roster reservation | **SUSPEND + investigate** |
| assignedPairCount differs from unique assignments/consumed slots | `ENROLLMENT_INTEGRITY_FAILURE` | audit mismatch | audit mismatch | no silent reconciliation | **SUSPEND + investigate** |

No partial durable state is silently repaired during MAIN. Any governed recovery must be auditable and cannot manufacture an outcome-dependent replacement or recycle a committed slot.

## 84.2 Post-enrollment / experiment analysis failures

| Failure / Condition | Assignment | Match Status | Pair Effect | Slot Effect | Run Effect |
|---|---|---|---|---|---|
| `MATCH_ABORTED` | unchanged | EXCLUDED | confirmatory pair incomplete/ineligible | consumed if assignment existed; no reuse | continue unless systemic |
| assignment missing/conflict | unresolved/audit | EXCLUDED | pair invalid | no valid new slot mutation | investigate if systemic |
| period-binding conflict | unchanged | EXCLUDED | pair invalid | slot remains consumed | suspend/investigate |
| protocol mismatch | unchanged | EXCLUDED | pair invalid | slot remains consumed | split/suspend run |
| critical Telemetry corruption | unchanged | EXCLUDED if primary unreconstructable | pair may be incomplete | slot remains consumed | investigate systemic corruption |
| one secondary metric unavailable | unchanged | ELIGIBLE if primary valid | metric pair may be unavailable | no change | continue |
| roster changes between periods | unchanged | completed match may remain descriptive | old pair not confirmatory | old slot remains consumed | new roster may use only another UNUSED slot |
| cross-pair participant reuse detected pre-assignment | none yet | no MAIN match created | enrollment rejected | no slot consumed | continue after integrity check |
| cross-pair reuse discovered after assignment | unchanged | affected match/pair excluded as applicable | pair not confirmatory | slot remains consumed | integrity review/suspend if systemic |
| build/config semantic drift | unchanged | incompatible | pair/run invalid unless predeclared stratum | no slot recycling | close/split run |
| `INPUT_INCOMPLETE` fallback | A | ELIGIBLE if otherwise valid | pair may remain | slot unchanged/consumed | continue |
| `STALE_INPUT` fallback | A | ELIGIBLE if otherwise valid | pair may remain | slot unchanged/consumed | continue |
| safety/validation rejection with valid frozen infrastructure | A | ELIGIBLE if fallback succeeds/other rules pass | pair may remain | slot unchanged/consumed | continue + retain safety evidence |
| `AED_UNAVAILABLE` / `AED_TIMEOUT` | A | EXCLUDED | pair incomplete/ineligible | slot remains consumed | suspend + readiness review |
| policy/config/registry/version invalid | A | EXCLUDED | pair incomplete/ineligible | slot remains consumed | suspend |
| `DECISION_IDENTITY_CONFLICT` | A | EXCLUDED | pair invalid | slot remains consumed | suspend immediately |
| `FALLBACK_CONFIG_INVALID` | A | EXCLUDED | pair invalid | slot remains consumed | suspend immediately |
| A launched while readiness FAIL | A | EXCLUDED | pair invalid | slot remains consumed if already assigned | suspend/investigate |
| hard gameplay safety violation | unchanged | EXCLUDED | pair invalid | slot remains consumed | suspend immediately |
| pair withdraws / Period 2 never occurs | unchanged | existing match evidence retained | pair incomplete | slot remains consumed | continue; no replacement |
| all slots consumed | n/a | n/a | realized eligible count may be lower | no UNUSED slots | close enrollment |
| analysis plan missing at MAIN start | n/a | do not start | n/a | no assignment | MAIN NOT READY |
| late source invalidation before cutoff | unchanged | recompute | recompute | no slot change | rebuild dataset |
| late source invalidation after cutoff | unchanged | old dataset immutable | new revision only | no slot change | governance review |
| sample-size/planned-slot plan unresolved | n/a | no MAIN collection | n/a | no schedule | MAIN NOT READY |

---

# 85. Reporting Minimum

The final experiment report must include:

- experiment protocol version;
- analysis plan version;
- immutable RunManifest reference;
- build/map/config/version scope;
- `plannedPairSlots`;
- `assignedPairCount`;
- `completedPairCount`;
- `eligiblePairCount`;
- `excludedPairCount`;
- F→A / A→F allocated and realized counts;
- no-slot-recycling statement;
- MAIN enrollment atomicity/integrity incident count and governed recoveries, if any;
- participant reuse policy and any detected violations;
- completed/eligible/excluded match counts;
- exclusion reasons;
- Team Survival condition summaries;
- assigned-condition effect estimate;
- uncertainty;
- period/sequence summaries;
- Profile evolution provenance/limitations;
- Adaptive exposure distribution;
- fallback experiment-class distribution + upstream reason distribution;
- secondary endpoint results;
- safety/fairness evidence;
- data-quality evidence;
- pilot/main separation statement;
- deviations from protocol;
- limitations.

Do not report only statistical significance.

---

# 86. Conclusion Language

Allowed style:

```text
observed assigned-condition difference
estimated effect
uncertainty
data quality
period/sequence evidence
Adaptive exposure
limitations
```

Forbidden without supporting evidence:

```text
Adaptive PASS
Fixed FAIL
Adaptive proven better
AED definitely improves player experience
```

---

# 87. Reproducibility Contract

A retained evidence package must allow reconstruction of:

```text
why this pair received F→A or A→F
which stable scheduleSlotId was consumed and by which pair
why that slot could not be reused
what exact roster membership keys were committed with the assignment
which MainPairEnrollmentCommit semantic fingerprint/identity committed
how duplicate/retry/conflict attempts were resolved when present
what exact roster formed the pair
whether any player appeared in another MAIN pair
when each period matchId was bound
what exact build/content ran
what exact Profile evidence existed before A
what exact AED policy/config/evidence registry existed
what exact Fixed baseline existed
what ScenarioConfig was applied
what upstream AED status/result/reason occurred
what fallbackExperimentClassification was assigned
whether A produced APPLIED / NO_CHANGE / FIXED_FALLBACK / integrity failure
what Telemetry was accepted
why each match/metric/pair was included
what dataset version was frozen
what analysis plan produced the report
```

No hidden manual correction step.

Assignment history, atomic enrollment commit evidence, conflict/retry audit evidence when present, period bindings, consumed-slot ownership, and participant memberships are immutable/auditable inputs to reproduction.

---

# 88. Reproduction Test

Given identical:

```text
ExperimentRunManifest
allocation schedule
ExperimentAssignmentRecord set
MAIN enrollment commit/audit evidence
allocation slot ownership state
MAIN participant membership index
ExperimentPeriodBindingRecord set
validated Telemetry evidence
Profile snapshots/revisions
AdaptiveDecision evidence
ScenarioConfig evidence
fallback-classification rules
eligibility rules
dataCutoff
analysisPlanVersion
```

the reproduction process must generate:

```text
same assignment
same slot ownership/consumption state
same participant membership mapping
same assignedPairCount
same period-to-match bindings
same participant uniqueness decisions
same fallback classification
same match eligibility
same metric availability
same pair eligibility
same exposure class
same count summaries
same analysis dataset
same analysis configuration
```

Exact software language/tooling is implementation-bound.

---

# 89. GenAI Isolation

GenAI Mission Briefing remains presentation-only.

For F/A comparison:

- use the same approved GenAI briefing policy/config where applicable;
- provider/cache/template differences cannot mutate ScenarioConfig;
- GenAI fallback is not Adaptive gameplay exposure;
- generated text is not the experiment treatment.

A GenAI experiment would require a separate protocol.

---

# 90. Network / Environment Control

Current game environment remains:

```text
Unity 6000.5.8f1
Photon Fusion 2.1.1 Stable build 2177
Host Mode
2–4 players
```

The experiment run must record or freeze the actual supported execution profile used for data collection.

Do not invent synthetic network impairment merely to make the experiment statistically cleaner.

Host/State Authority remains the owner of authoritative ScenarioConfig application; the experiment service cannot bypass it.

---

# 91. Implementation Order

Recommended dependency order:

```text
1. audit M1-020 v0 against current v1.1 contracts
2. freeze FIXED_ADAPTIVE_EXPERIMENT_PROTOCOL_V1_1
3. implement ExperimentRunManifest
4. implement ExperimentReadinessEvaluator
5. implement BALANCED_RANDOM_SEQUENCE_SCHEDULE_V1
6. implement immutable ExperimentAssignmentLedger storage semantics
7. implement authoritative MainParticipantEnrollmentIndex uniqueness constraint
8. implement MainPairEnrollmentCoordinator / logical atomic commit with slot ownership + participant membership + assignment idempotency/conflicts
9. implement crash/retry + enrollment-integrity audits
10. implement immutable ExperimentPeriodBindingLedger separately from enrollment
11. integrate MATCH_STARTED experiment provenance + binding validation
12. implement AdaptiveFallbackExperimentClassifier from AED status/result/reason
13. integrate AdaptiveDecision / ScenarioResolution evidence
14. implement ExperimentMatchRecord and eligibility evaluator
15. implement canonical roster/pair builder
16. implement Team Survival endpoint extractor
17. implement valid secondary metric extractors
18. implement Adaptive exposure classifier
19. implement version/config drift checks
20. implement AnalysisDatasetBuilder with slot/binding/participant evidence
21. implement late-invalidation dataset rebuild
22. implement ExperimentReproductionVerifier
23. execute instrumentation/pilot validation
24. freeze AED/Profile/fixed-baseline tuning used by the run
25. freeze MatchOutcome→ObjectiveCompletion mapping if secondary outcome is used
26. freeze analysisPlanVersion
27. select/freeze exact statistical model and uncertainty policy
28. determine/freeze MAIN `plannedPairSlots`
29. generate/freeze exact balanced MAIN allocation schedule
30. execute full readiness review
31. only then begin MAIN collection
32. stop enrollment when assignedPairCount == plannedPairSlots
33. lock dataCutoff
34. build immutable analysis dataset
35. execute frozen analysis
36. report effect + uncertainty + exposure + limitations
```

---
# 92. Open TBDs

## 92.1 `TBD BEFORE MAIN STUDY` — BLOCKS MAIN EXPERIMENT

- exact `plannedPairSlots` value;
- approved power target, if formal power-based inference is used;
- approved minimum effect of interest, if used;
- exact significance threshold, if formal hypothesis testing is used;
- exact primary statistical model/test;
- exact uncertainty interval method/level;
- final handling of any formally modeled condition×period/sequence interaction;
- run-specific fixed baseline content/version;
- final numerical AED tuning/evidence thresholds/candidate values;
- actual allocation schedule seed/PRNG implementation and generated schedule;
- execution environment profile;
- ObjectiveCompletion mapping/version if ObjectiveCompletion is to be analyzed;
- questionnaire instrument if subjective layer is activated.

## 92.2 IMPLEMENTATION BINDING TBD

- DB/table names;
- C# class names;
- assignment-ledger storage;
- atomic enrollment transaction/CAS/serialized-worker mechanism;
- physical unique-index/key layout for pair/slot/participant identities;
- retry transport/command envelope;
- period-binding storage/transaction mechanism;
- participant-membership index storage;
- manifest serialization;
- hash/fingerprint algorithm;
- exact deterministic PRNG implementation;
- analysis language/tooling;
- dashboard/report-generation tooling;
- artifact storage paths.

## 92.3 Not allowed as TBD

- condition assignment semantics;
- F/A → resolution mode mapping;
- assignment vs `configSource`;
- A fallback assignment classification;
- §28 upstream AED reason → experiment fallback class/eligibility/run handling;
- A NO_CHANGE classification;
- immutable AssignmentRecord lifecycle;
- MAIN enrollment atomic commit boundary;
- AssignmentIdentityKey / SlotConsumptionKey / MainParticipantMembershipKey semantics;
- exact-retry idempotency and conflicting-retry rejection;
- concurrent shared-player / same-slot conflict behavior;
- crash-before / crash-after-commit semantics;
- assignedPairCount committed-state invariant;
- partial durable enrollment integrity handling;
- period→match binding owner and identity conflict behavior;
- primary experimental unit;
- paired unit;
- same-roster requirement;
- one MAIN confirmatory pair per player per run;
- roster-replacement behavior;
- `plannedPairSlots` meaning;
- slot consumption/no-recycle rule;
- no outcome-dependent replacement/extension;
- two periods / two sequences;
- persistent Profile evolution acknowledgment;
- system readiness vs match AED eligibility;
- primary assigned-condition estimand;
- Team Survival confirmatory role;
- objectiveTime conditional-secondary status;
- missing != zero;
- match/metric/pair exclusion topology;
- pilot/main separation;
- run-version freeze;
- dataset version/cutoff semantics;
- no outcome-dependent exclusion.

---

# 93. Fixed vs Adaptive Experiment v1.1 Hard Invariants

1. Experiment assignment is not inferred from `configSource`.
2. Condition F maps to requested FIXED resolution.
3. Condition A maps to requested ADAPTIVE resolution.
4. Adaptive fallback does not relabel assignment.
5. NO_CHANGE does not relabel assignment.
6. `ExperimentAssignmentRecord` contains only assignment-time facts and is immutable after commit.
7. Future match IDs are never added by mutating `ExperimentAssignmentRecord`.
8. Period→match identity is owned by immutable/append-only `ExperimentPeriodBindingRecord`.
9. One `(experimentRunId,pairId,periodIndex)` binds to at most one authoritative `matchId`.
10. Duplicate same period binding is idempotent; different matchId is conflict/no overwrite.
11. Sequence assignment occurs before Period-1 outcome.
12. Primary experimental unit is `team-match`.
13. Player rows inside one team-match are not independent treatment replicates.
14. Confirmatory pair requires the same canonical roster identity.
15. ExperimentPair never becomes persistent gameplay Team identity.
16. Within one MAIN run, one stable player `userId` belongs to at most one confirmatory ExperimentPair.
17. Cross-pair participant reuse is checked before schedule-slot consumption when identity is known.
18. Late-detected cross-pair reuse cannot be silently treated as independent.
19. Persistent Player Profile lifecycle is not disabled for experiment convenience.
20. No classical washout or zero-carryover assumption is silently made.
21. Period and sequence are retained for analysis.
22. Profile revisions before Adaptive decisions are retained.
23. Raw Telemetry never becomes AED input through the experiment layer.
24. TeamPerformance is never fabricated.
25. TeamPerformance may remain INCOMPLETE/null without restoring the v0 gate.
26. COLD_START 50 is not converted into observed performance evidence.
27. Missing metric is not zero.
28. Every A-assigned fallback remains assigned A, including excluded matches.
29. Not every `FIXED_FALLBACK` is confirmatory-eligible.
30. Authoritative AED status/result/reason deterministically maps through §28.
31. Frozen-run config/integrity corruption is distinct from expected treatment fallback.
32. Environment/concurrency safety rejection is distinct from candidate-generation policy/config corruption; bound/timing/pressure violations are protocol/config invalid in MAIN v1.1.
33. `AED_UNAVAILABLE` and `AED_TIMEOUT` are system-failure fallbacks: exclude affected MAIN A match and suspend readiness review.
34. `plannedPairSlots` means frozen number of MAIN assignment opportunities.
35. `plannedPairSlots` is not completed/eligible pair count.
36. Each successful assignment permanently consumes one stable schedule slot.
37. Assigned schedule slots are never recycled after dropout, missing Period 2, roster change, exclusion, or missing endpoint.
38. Roster replacement requires a new pair identity and a still-unused pre-frozen slot.
39. v1.1 has no outcome-dependent replacement rule.
40. v1.1 has no post-hoc schedule extension/regeneration rule.
41. Main allocation schedule is never regenerated after outcome observation.
42. `assignedPairCount == plannedPairSlots` closes MAIN enrollment.
43. Actual Adaptive exposure is not treatment assignment.
44. Expected treatment fallback is not automatically excluded.
45. Main Adaptive collection cannot begin while System Readiness is FAIL.
46. Hard safety/integrity violations cannot be normalized by a low average rate.
47. Main semantic/tuning versions are frozen within a homogeneous run.
48. Pilot-tuned data is not silently mixed with confirmatory MAIN data.
49. Sample size / `plannedPairSlots` value is not invented.
50. Statistical test is not chosen post hoc for significance.
51. Team Survival is the sole v1.1 confirmatory primary endpoint.
52. ObjectiveCompletion is not invented from an incomplete MatchOutcome registry.
53. `objectiveTime` is not assigned zero or a fabricated penalty for non-completion.
54. Conditional objectiveTime is not interpreted as an unconditional treatment effect.
55. Adding confirmatory endpoints requires a pre-MAIN hierarchy/multiplicity revision.
56. Data exclusion cannot depend on whether the result supports H1.
57. Same frozen evidence yields the same analysis dataset.
58. Late source corrections create an explicit dataset revision.
59. A frozen dataset is never silently mutated after cutoff.
60. Subjective claims require an approved instrument.
61. GenAI remains outside the gameplay treatment factor.
62. Experiment layer cannot command Monster runtime state.
63. Experiment layer cannot rewrite Profile history.
64. Experiment layer cannot bypass AED/ScenarioValidator to apply config.
65. Host / Fusion State Authority remains authoritative for ScenarioConfig apply.
66. Proxy clients cannot authoritatively assign/apply experiment gameplay state.
67. Contract baseline does not mean MAIN experiment execution is READY.
68. No experiment result is fabricated in this document.

## 93.1 EXP-DD-05 MAIN enrollment atomicity invariants

1. MAIN pair enrollment is one logical atomic commit.
2. AssignmentRecord, schedule-slot consumption, and complete participant membership cannot become independently successful.
3. Same stable Player cannot be concurrently committed to two MAIN pairs in one run.
4. Same schedule slot cannot be concurrently committed to two pairs.
5. Exact enrollment retry after a committed success is idempotent.
6. Conflicting retry never overwrites the prior assignment.
7. Crash before the durable logical commit leaves no committed enrollment state.
8. Crash after durable commit but before acknowledgement cannot cause duplicate assignment, duplicate membership, or a second consumed slot.
9. `assignedPairCount` counts committed unique assignments, never attempts.
10. One committed assignment corresponds to exactly one consumed slot owned by the same pair.
11. One consumed slot corresponds to exactly one committed assignment.
12. Every committed assignment has the complete canonical roster participant-membership set pointing to that pair.
13. Period→match binding remains outside `MainPairEnrollmentCommit`.
14. Later roster change/dropout/exclusion never rolls back a committed assignment or recycles its slot/membership history.
15. Physical storage/transaction technology may vary; atomic observable semantics may not.

---

# 94. Definition of Done

A researcher/developer can answer:

| # | Question | v1.1 answer |
|---:|---|---|
| 1 | Protocol semantic ID? | `FIXED_ADAPTIVE_EXPERIMENT_PROTOCOL_V1_1` |
| 2 | Condition F? | assigned FIXED; requested FIXED resolution |
| 3 | Condition A? | assigned ADAPTIVE; requested ADAPTIVE resolution |
| 4 | Assignment source? | immutable assignment-time record; MATCH_STARTED condition/protocol is occurrence evidence |
| 5 | Does AssignmentRecord contain future match IDs? | No |
| 6 | Who binds pair periods to matches? | immutable/append-only ExperimentPeriodBindingRecord |
| 7 | Can one pair/period bind to two match IDs? | No; conflict/no overwrite |
| 8 | Is configSource assignment? | No |
| 9 | Is A fallback still A? | Yes, even if excluded |
| 10 | Is A NO_CHANGE still A? | Yes |
| 11 | Are all A fallbacks experiment-eligible? | No; §28 reason classification controls |
| 12 | AED unavailable/timeout policy? | A retained; match excluded; run suspended for readiness review |
| 13 | Expected evidence fallback? | INPUT_INCOMPLETE / STALE_INPUT normally eligible if other gates pass |
| 14 | Config/registry/version corruption? | exclude + suspend |
| 15 | Safety/validator rejection? | may remain eligible if frozen infrastructure valid and fallback succeeds |
| 16 | Primary experimental unit? | team-match |
| 17 | Paired unit? | same-roster one-F + one-A ExperimentPair |
| 18 | Can roster differ? | not for confirmatory pair |
| 19 | Cross-pair participant reuse? | one player at most one MAIN confirmatory pair per run |
| 20 | Does ExperimentPair become gameplay Team identity? | No |
| 21 | Design? | two-period two-sequence within-roster repeated measures |
| 22 | Classical no-carryover crossover? | No |
| 23 | Sequences? | F→A and A→F |
| 24 | Assignment mechanism? | balanced randomized pre-generated schedule |
| 25 | `plannedPairSlots` meaning? | frozen number of MAIN pair assignment opportunities |
| 26 | Does dropout free a slot? | No |
| 27 | Can excluded pair be replaced? | No v1.1 replacement rule |
| 28 | Can schedule be extended/regenerated post hoc? | No |
| 29 | Roster replacement? | new pair + unused slot only |
| 30 | When does enrollment stop? | assignedPairCount == plannedPairSlots |
| 31 | Assignment timing? | before Period-1 outcome |
| 32 | MAIN enrollment atomic boundary? | immutable AssignmentRecord + one consumed slot + complete roster membership set commit together |
| 33 | Exact successful retry? | `IDEMPOTENT_SUCCESS`; no duplicate count/slot/membership |
| 34 | Same pairId with different semantic assignment? | `ASSIGNMENT_IDENTITY_CONFLICT`; no overwrite |
| 35 | Concurrent shared Player? | at most one commit; loser `CROSS_PAIR_PARTICIPANT_REUSE` |
| 36 | Concurrent same slot? | at most one commit; loser `SCHEDULE_SLOT_CONFLICT` |
| 37 | Crash before commit? | no AssignmentRecord, no consumed slot, no membership |
| 38 | Crash after commit before ACK? | committed state survives; exact retry idempotent |
| 39 | assignedPairCount meaning? | count committed unique assignments = count consumed slots; not attempt count |
| 40 | Can partial enrollment state be accepted? | No; `ENROLLMENT_INTEGRITY_FAILURE` + suspend/investigate |
| 41 | Does enrollment include period match IDs? | No; PeriodBinding remains separate |
| 42 | System readiness? | §31 run-level implementation/evidence gate |
| 43 | Match AED eligibility? | upstream AED input gate result |
| 44 | Confirmatory endpoint? | Team Survival |
| 45 | ObjectiveCompletion? | secondary only after approved outcome mapping; otherwise unavailable |
| 46 | objectiveTime? | conditional secondary among valid completers |
| 47 | Missing metric? | null; never zero |
| 48 | Whole-match exclusions? | §47 |
| 49 | Pair valid? | same roster + immutable period bindings + one F + one A + both eligible + participant uniqueness |
| 50 | Primary estimand? | assigned-condition Team Survival difference |
| 51 | Actual exposure analysis? | secondary/exploratory |
| 52 | Profile evolution? | retained and recorded |
| 53 | Period/sequence? | retained and modeled/reported |
| 54 | Sample size/planned slot value fixed? | No; pre-MAIN planning required |
| 55 | Statistical model fixed? | No; analysis plan freeze required |
| 56 | Pilot mixed with MAIN? | no by default if pilot informs tuning/planning |
| 57 | Version drift? | no silent pooling |
| 58 | dataCutoff? | explicit frozen dataset boundary |
| 59 | late invalidation? | deterministic dataset revision |
| 60 | main safety/system failure? | suspend/investigate |
| 61 | Subjective result current? | not confirmatory; instrument not approved here |
| 62 | Experiment result present? | No |
| 63 | Main experiment READY now? | No |

---
# 95. Architecture Escalation

```text
Architecture Escalation Required: NO
```

This protocol remains downstream of:

```text
Gameplay
→ Telemetry
→ Profile
→ AdaptiveInputSnapshot
→ AED
→ ScenarioConfig
```

It introduces no runtime gameplay authority and no persistent Team identity.

---

# 96. Fixed vs Adaptive Experiment Contract Validation

```text
M1-020 v0 validity review complete: YES

Protocol semantic identity exact: YES

Condition F exact: YES
Condition A exact: YES
experimentCondition vs configSource exact: YES
Fallback assignment semantics exact: YES
NO_CHANGE assignment semantics exact: YES

EXP-DD-01 remains resolved: YES
EXP-DD-02 remains resolved: YES
EXP-DD-03 remains resolved: YES
EXP-DD-04 remains resolved: YES

AssignmentRecord lifecycle exact: YES
AssignmentRecord immutable: YES
Future match IDs excluded from AssignmentRecord: YES
Period-to-match binding exact: YES
Conflicting period binding rejected: YES

EXP-DD-05 MAIN enrollment logical atomicity exact: YES
Participant reservation atomic with assignment: YES
Slot consumption atomic with assignment: YES
Concurrent shared-player race safe: YES
Concurrent same-slot race safe: YES
Exact enrollment retry idempotent: YES
Conflicting enrollment retry rejected: YES
Crash-before-commit semantics exact: YES
Crash-after-commit-before-ACK semantics exact: YES
Partial durable enrollment state prohibited: YES
assignedPairCount committed-state invariant exact: YES
Period binding remains outside enrollment commit: YES
No slot recycling preserved after atomic commit: YES
Cross-pair participant uniqueness preserved under concurrency: YES

Experimental unit exact: YES
Pair identity exact: YES
Stable-roster requirement exact: YES
Cross-pair participant reuse policy exact: YES

F→A exact: YES
A→F exact: YES
Assignment mechanism exact/versioned: YES
plannedPairSlots semantic exact: YES
Consumed slots never recycled: YES
Outcome-dependent replacement prohibited: YES
Post-hoc schedule regeneration/extension prohibited: YES

Fallback reason classification exact: YES
Expected treatment fallback distinct: YES
System failure fallback distinct: YES
Protocol/config invalid distinct: YES
Safety/validation rejection distinct: YES
AED_UNAVAILABLE/AED_TIMEOUT policy exact: YES

Profile evolution addressed: YES
Carryover/no-washout issue addressed: YES
Period effect retained: YES
Sequence effect retained: YES

System readiness gate exact: YES
Match AED eligibility separate: YES
No TeamPerformance gate regression: YES
No synthetic readiness: YES

Primary outcome validity audit complete: YES
Team Survival confirmatory endpoint exact: YES
ObjectiveCompletion unsupported mapping handled without fabrication: YES
ObjectiveTime bias handling explicit: YES
Confirmatory endpoint hierarchy exact: YES
Secondary metrics exact: YES
Safety metrics exact: YES
Data quality metrics exact: YES

Match exclusion exact: YES
Metric unavailability exact: YES
Pair eligibility exact: YES
No outcome-dependent exclusion: YES

Assigned-condition estimand exact: YES
Exposure analysis clearly secondary: YES

Sample-size policy honest: YES
Pilot/main separation exact: YES
AnalysisPlanVersion contract exact: YES
No post-hoc test selection: YES

Run-version freeze exact: YES
ExperimentRunManifest exact: YES
Dataset version/cutoff exact: YES
Late invalidation behavior exact: YES
Reproducibility exact: YES

Stop/suspension rules exact: YES
No fabricated result: YES

Current implementation status honest: YES
Main experiment readiness honest: YES

Architecture escalation required: NO
```

The `YES` values above validate document semantics only. They are **not** claims that runtime implementation, automated tests, pilot, tuning, or MAIN experiment execution has passed.

---

# 97. Final Consistency Audit

```text
Can experiment condition be derived from configSource?
NO

Can Adaptive fallback relabel assignment FIXED?
NO

Can NO_CHANGE relabel assignment FIXED?
NO

Can an excluded A fallback be relabeled F?
NO

Can AssignmentRecord be mutated to append Period-2 matchId?
NO

Can two match IDs occupy one pair/period binding?
NO

Can period binding overwrite a prior different matchId?
NO

Can two concurrent rosters containing the same Player both commit?
NO

Can two pairs consume the same scheduleSlotId?
NO

Can AssignmentRecord exist as a successful enrollment while its slot is logically UNUSED?
NO

Can a slot be CONSUMED as a successful enrollment without its ExperimentAssignmentRecord?
NO

Can only part of the canonical roster be participant-reserved for a successful enrollment?
NO

Can an exact retry create a second assignment?
NO

Can the same committed pairId be silently changed to another roster/slot/sequence?
NO

Can a crash after durable commit cause slot recycling or a second slot assignment?
NO

Can assignedPairCount count failed attempts?
NO

Can EXP-DD-05 move period match IDs back into ExperimentAssignmentRecord?
NO

Can a committed enrollment be rolled back because Period 2 later fails?
NO

Can implementation choose any transaction/CAS/serialization technology while preserving the frozen observable atomic semantics?
YES

Can assignment change after observing result?
NO

Can every FIXED_FALLBACK be automatically treated as confirmatory-eligible?
NO

Can POLICY_CONFIG_INVALID be normalized as ordinary expected treatment fallback?
NO

Can AED_UNAVAILABLE/AED_TIMEOUT continue MAIN collection without readiness suspension under v1.1?
NO

Can a safety validator rejection automatically prove the frozen policy/config is corrupt?
NO

Can plannedPairSlots mean desired eligible/completed pair count?
NO

Can a consumed schedule slot be recycled after dropout/exclusion?
NO

Can a replacement roster inherit the old pair's consumed slot?
NO

Can the schedule be regenerated or extended after observing outcomes?
NO

Can enrollment continue after assignedPairCount == plannedPairSlots?
NO

Can one stable Player belong to two MAIN confirmatory pairs in the same run?
NO

Can four Player rows inside one match count as four independent
experimental units?
NO

Can different rosters form one confirmatory pair?
NO

Does ExperimentPair create persistent gameplay Team identity?
NO

Can Profile updates be disabled merely to make the repeated design cleaner?
NO

Can the protocol silently assume no carryover?
NO

Can period/sequence be discarded?
NO

Can TeamPerformance be synthesized from Survival/Noise?
NO

Can COLD_START 50 count as observed performance?
NO

Can metric UNAVAILABLE become zero?
NO

Can fallback match be removed merely because it reduces Adaptive effect?
NO

Can config/version drift be silently pooled?
NO

Can pilot-tuned data silently enter confirmatory MAIN analysis?
NO

Can plannedPairSlots be invented without planning evidence?
NO

Can statistical test be chosen after seeing which gives significance?
NO

Can objective failure be assigned objectiveTime = 0?
NO

Can ObjectiveCompletion mapping be invented from the SUCCESS JSON example?
NO

Can multiple confirmatory endpoints be added without predeclared
hierarchy/multiplicity handling?
NO

Can raw TelemetryEvent become AED input through this experiment?
NO

Can GenAI become the manipulated gameplay factor?
NO

Can excluded raw evidence be deleted merely because it is excluded?
NO

Can late invalidation silently mutate an already frozen dataset?
NO

Can actual Adaptive exposure replace assigned condition in the primary analysis?
NO

Can a hard safety violation be accepted because its rate is low?
NO

Can the contract claim Adaptive is better without experiment data?
NO

Does BASELINED experiment contract automatically mean MAIN experiment READY?
NO
```

All expected audit answers above hold. The implementation-mechanism freedom question is intentionally `YES`; the contract-level gameplay/research integrity questions remain `NO` as specified.

---

# 98. References

## 98.1 Project contracts

1. `AI_Architecture_v1.1.md`.
2. `Telemetry_Contract_v1.1.md`.
3. `Player_Team_Profile_Contract_v1.1.md`.
4. `AED_ScenarioConfig_Contract_v1.1.md`.
5. `Stalker_AI_Design_v1.1.md`.
6. `Listener_AI_Design_v1.0.md`.
7. `Warden_AI_Design_v1.0.md`.
8. `M1-020_Test_Strategy_Fixed_vs_Adaptive_Experiment_v0_FINAL.md`.
9. `M1-015_ScenarioConfig_AED_Fairness_Policy_v0_FINAL.md`.
10. `M1-014_Player_Team_Profile_Fields_Formulas_v0_FINAL.md`.
11. `ECHO PROTO.docx` / current GDD.
12. `KLTN.docx` — Research Facility Map Flow.
13. Approved Photon Fusion multiplayer/network contract.

## 98.2 Methodology references

14. Dwan, K., Li, T., Altman, D. G., & Elbourne, D. (2019). **CONSORT 2010 statement: extension to randomised crossover trials.** *BMJ*, 366, l4378. DOI: `10.1136/bmj.l4378`.
15. NIST/SEMATECH. **e-Handbook of Statistical Methods — Randomized Block Designs.** Section 5.3.3.2. `https://www.itl.nist.gov/div898/handbook/pri/section3/pri332.htm`.
16. U.S. Food and Drug Administration. (2022). **Multiple Endpoints in Clinical Trials — Guidance for Industry.** `https://www.fda.gov/regulatory-information/search-fda-guidance-documents/multiple-endpoints-clinical-trials`.
17. VanderWeele, T. J. (2011). **Principal Stratification — Uses and Limitations.** *International Journal of Biostatistics*, 7(1):28. DOI: `10.2202/1557-4679.1329`.
18. NIST/SEMATECH. **e-Handbook of Statistical Methods — Choosing an Experimental Design.** `https://www.itl.nist.gov/div898/handbook/pri/section3/pri3.htm`.

### Methodology application note

- The crossover literature is used as an analogy for two-period/two-sequence repeated measures, sequence assignment, within-unit correlation, and period/carryover reporting. ECHO PROTOCOL does **not** claim a clinical-trial washout or no-carryover condition.
- NIST randomized-design guidance supports balancing controllable nuisance structure and randomizing assignment rather than allowing manual outcome-aware allocation.
- Multiple-endpoint guidance supports predeclaring endpoint hierarchy/multiplicity rather than searching across outcomes for significance.
- Post-treatment-selection literature motivates the conservative classification of `objectiveTime` as conditional secondary because it is observed only for source-valid completed objective phases; this contract does not import clinical causal estimands into gameplay research.

---

# 99. Surgical Correction Report

| Issue | Status | Sections Changed | Resolution |
|---|---|---|---|
| EXP-DD-01 | RESOLVED | §§1, 13–14, 31, 46–49, 55, 60–61, 71, 73–76, 82, 84, 87–88, 91–97 | `ExperimentAssignmentRecord` now contains assignment-time facts only. Immutable `ExperimentPeriodBindingRecord` owns later `(pairId, periodIndex)→matchId` binding; duplicate same binding is idempotent and conflicting match ID is rejected/no-overwrite. |
| EXP-DD-02 | RESOLVED | §§1, 5, 26–31, 46–47, 54–55, 60–61, 71, 73, 75, 78, 80, 82–85, 87–88, 91–97 | Added deterministic experiment-owned mapping from authoritative AED status/result/reason into `EXPECTED_TREATMENT_FALLBACK`, `SYSTEM_FAILURE_FALLBACK`, `PROTOCOL_OR_CONFIG_INVALID`, or `SAFETY_OR_VALIDATION_REJECTION`. Every A match remains assigned A; eligibility/run handling now differs by reason class. |
| EXP-DD-03 | RESOLVED | §§1, 5, 18, 22–23, 31, 49–50, 54–56, 60–61, 69, 71, 73–76, 82, 84–85, 87–88, 91–97 | `plannedPairSlots` now means frozen MAIN assignment opportunities. Stable schedule slots become permanently CONSUMED on assignment; no recycling, outcome-dependent replacement, regeneration, or extension. Replacement roster requires new pair + unused frozen slot. |
| EXP-DD-04 | RESOLVED | §§1, 5, 16–18, 22, 31, 46–49, 55, 60–61, 63, 69, 71, 73–76, 82, 84–85, 87–88, 91–97 | v1.1 freezes one stable player `userId` → at most one MAIN confirmatory `ExperimentPair` per `experimentRunId`. Authoritative commit-time uniqueness prevents concurrent reuse; late overlap is an integrity failure, not normal independence. |
| EXP-DD-05 | RESOLVED | §§1–3, 13–18, 22–23, 31, 46, 55, 60, 69, 71–76, 82–85, 87–88, 91–100 | MAIN enrollment now commits participant membership + schedule-slot consumption + immutable assignment as one idempotent logical atomic operation. Stable pair/slot/player uniqueness keys, deterministic `ASSIGNMENT_IDENTITY_CONFLICT` / `SCHEDULE_SLOT_CONFLICT` / `CROSS_PAIR_PARTICIPANT_REUSE`, crash-before/after-commit behavior, `assignedPairCount` integrity, and partial-state suspension are exact; PeriodBinding remains separate. |

No upstream gameplay, Telemetry wire, Profile formula, AED policy, ScenarioConfig authority, Monster runtime behavior, primary endpoint, research question, sample-size policy, or statistical-analysis policy was changed.

# 100. Final Validation

```text
EXP-DD-01 remains resolved? YES
EXP-DD-02 remains resolved? YES
EXP-DD-03 remains resolved? YES
EXP-DD-04 remains resolved? YES

MAIN enrollment logical atomicity exact? YES
Participant reservation atomic with assignment? YES
Slot consumption atomic with assignment? YES
Concurrent shared-player race safe? YES
Concurrent same-slot race safe? YES
Exact retry idempotent? YES
Conflicting retry rejected? YES
Crash-before-commit semantics exact? YES
Crash-after-commit-before-ACK semantics exact? YES
Partial durable enrollment state prohibited? YES
assignedPairCount invariant exact? YES
Period-binding remains separate? YES
No slot recycling preserved? YES
Cross-pair participant uniqueness preserved? YES

Assignment record lifecycle exact? YES
Assignment remains immutable? YES
Period-to-match binding exact? YES
Conflicting period binding rejected? YES
Fallback keeps assigned condition A? YES
Fallback reason classification exact? YES
Broken config distinguishable from expected fallback? YES
Slot-count semantic exact? YES
Dropout/replacement policy outcome-independent? YES
Primary experimental unit unchanged? YES
Same-roster paired design unchanged? YES
Profile evolution preserved? YES
Team Survival remains sole confirmatory primary? YES
ObjectiveCompletion mapping still not fabricated? YES
objectiveTime remains conditional secondary? YES
missing != zero preserved? YES
no upstream gameplay authority changed? YES
Main experiment readiness stated honestly? YES
Architecture escalation required? NO
```

These are document-contract validation answers only. No implementation/test PASS is claimed.

## Final status block

```text
Document Revision: v1.1
Experiment Protocol Semantic ID: FIXED_ADAPTIVE_EXPERIMENT_PROTOCOL_V1_1

Recommended Status:
BASELINED v1.1

Architecture Escalation Required:
NO

P0 Remaining:
0

P1 Remaining:
0

Contract Design:
COMPLETE

Experiment Implementation:
NOT COMPLETE

Main Fixed-vs-Adaptive Experiment Execution:
NOT READY
```

Execution remains blocked by the already-declared implementation/tuning/analysis-plan/sample-size/readiness prerequisites. This final surgical contract correction resolves EXP-DD-05 while preserving EXP-DD-01 through EXP-DD-04 and does not convert semantic completeness into implementation readiness.

**End of `Fixed_vs_Adaptive_Experiment_Contract_v1.1.md`**
