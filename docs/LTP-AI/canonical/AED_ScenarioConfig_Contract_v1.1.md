# ECHO PROTOCOL — AED / ScenarioConfig Contract v1.1

**Canonical document:** `AED_ScenarioConfig_Contract_v1.1.md`  
**Project:** ECHO PROTOCOL — Co-op Survival Horror Multiplayer  
**Document Revision:** `v1.1`  
**Parent Architecture:** `AI_Architecture_v1.1.md` — **BASELINED v1.1**  
**Upstream Telemetry:** `Telemetry_Contract_v1.1.md` — **BASELINED v1.1**, wire `schemaVersion = "1.1"`  
**Upstream Profile:** `Player_Team_Profile_Contract_v1.1.md` — **BASELINED v1.1**  
**Historical predecessor:** `M1-015_ScenarioConfig_AED_Fairness_Policy_v0_FINAL.md`  
**Experiment dependency:** `M1-020_Test_Strategy_Fixed_vs_Adaptive_Experiment_v0_FINAL.md`  
**Policy semantic identity:** logical `AED_SCENARIO_POLICY_V1_1`  
**Recommended Status:** **BASELINED v1.1**  
**Architecture Escalation Required:** **NO**  
**AED Implementation:** **NOT EVIDENCED / NOT COMPLETE FROM SUPPLIED SOURCE**  
**Live Adaptive Execution:** **NOT READY**

> This is an implementation contract, not an implementation-status claim. It freezes the decision semantics, authority, safety, reproducibility, failure behavior, and test obligations required to implement the bounded rule-based Adaptive Experience Director (AED) for M2. It does not claim that policy code, tuning values, backend services, Fusion binding, automated tests, profiling, or the Fixed-vs-Adaptive experiment have been completed.

**Surgical Correction Pass:**

```text
AED-DD-01 — RESOLVED
AED-DD-02 — RESOLVED
AED-DD-03 — RESOLVED
AED-DD-04 — RESOLVED
```

---

# 1. Document Control

| Field | Contract |
|---|---|
| Role | Downstream Profile → AED → ScenarioConfig implementation contract |
| Parent architecture status | `BASELINED v1.1` |
| Telemetry wire | current `"1.1"`; legacy `"1.0"` remains governed by Telemetry contract |
| Profile input object | immutable `AdaptiveInputSnapshot` |
| Runtime adaptation model | deterministic, rule-based, bounded |
| Requested resolution mode | `FIXED | ADAPTIVE` |
| Current policy semantic ID | logical `AED_SCENARIO_POLICY_V1_1` |
| Current policy-generated adaptive deltas | PRE_MATCH only |
| Current policy ACTIVE Player dimensions | `SURVIVAL`, `NOISE` |
| TeamPerformance | remains `INCOMPLETE`, `score = null`; not synthetically completed |
| Listener adaptive authority | none in v1.1 |
| Warden runtime-action adaptive authority | none in v1.1 |
| Scenario `routeModifier` | historical adaptive authority retained at `ALLOWED_PHASE_BOUNDARY`; policy v1.1 does not select it |
| Host authority | Fusion Host / State Authority owns authoritative ScenarioConfig apply |
| Persistent team identity | forbidden |
| Raw Telemetry as policy input | forbidden |
| ML / RL / GenAI gameplay decisions | forbidden |
| Current implementation | not evidenced from supplied source |
| Live Fixed-vs-Adaptive experiment | not ready |

## 1.1 Statement classification

| Label | Meaning |
|---|---|
| **ARCHITECTURE BASELINE** | Required by `AI_Architecture_v1.1.md`. |
| **UPSTREAM CONTRACT** | Required by Telemetry/Profile v1.1. |
| **PROJECT BASELINE** | Required by approved gameplay/map/network contracts. |
| **PREDECESSOR — KEEP** | M1-015 v0 behavior retained. |
| **PREDECESSOR — MODIFY** | v0 concept retained but updated for v1.1 dependency/semantics. |
| **PREDECESSOR — SUPERSEDE** | v0 semantic replaced by an explicit v1.1 semantic. |
| **POLICY v1.1 DECISION** | New detailed-policy decision required to make implementation deterministic. |
| **STATIC / VERSIONED CONFIG** | Designer/config-owned value, not runtime policy state. |
| **TUNING TBD** | Numerical tuning value must be supplied by a versioned config before Adaptive activation. |
| **IMPLEMENTATION BINDING TBD** | Storage/API/class/Fusion binding may vary without changing behavior. |

---

# 2. Purpose

The contract closes the implementation gap between the Profile v1.1 producer and authoritative ScenarioConfig application.

Canonical flow:

```text
Authoritative Gameplay
→ Telemetry
→ Profile / Processed Evidence
→ AdaptiveInputSnapshot
→ AED Input Gate
→ deterministic AED Policy
→ Candidate ScenarioConfig
→ Scenario Validator
→ authoritative Apply
   OR
→ FixedDirector / safe fallback
→ Traditional Gameplay AI
```

A developer reading this document must be able to implement without inventing:

- requested FIXED versus ADAPTIVE resolution semantics;
- the exact Profile object consumed;
- snapshot validity behavior;
- evidence sufficiency topology;
- current policy inputs;
- deterministic score-band classification;
- policy rule precedence;
- policy-active parameter set;
- deterministic numerical adjustment mechanism;
- candidate construction;
- fairness and route validation;
- exactly-once application;
- stale-input and stale-config rejection;
- fallback behavior;
- configSource/version semantics;
- research evidence;
- Host/Fusion ownership.

---

# 3. Scope and Non-Goals

## 3.1 In scope

- M1-015 v0 → v1.1 validity review;
- `ScenarioResolutionMode`;
- `AdaptiveInputSnapshot` consumption;
- AED input gate;
- evidence policy;
- deterministic rule-based policy;
- policy rule IDs and tie-break;
- adaptive authority versus policy activation;
- parameter registry;
- numerical candidate-value selection;
- ScenarioConfig schema/lifecycle;
- ScenarioValidator;
- FixedDirector;
- route/FacilityGraph safety integration;
- Final Hunt timing validation;
- decision idempotency;
- stale snapshot/config/window handling;
- Host-authoritative application;
- network presentation/state boundary;
- decision evidence/reproducibility;
- required tests and implementation sequence.

## 3.2 Non-goals

This contract does **not**:

- modify Stalker/Listener/Warden FSM behavior;
- create a realtime pacing estimator;
- activate M1-015 `PacingState`;
- use raw TelemetryEvent as a policy input;
- redefine Player/Team Profile formulas;
- fabricate Teamwork or ResourceEfficiency;
- fabricate TeamPerformance;
- activate DEFERRED Profile dimensions;
- infer hidden Player position;
- choose current Monster targets;
- command Warden DoorId;
- create Listener adaptive hearing knobs;
- change attack damage/range/timing;
- create arbitrary map/spawn/route content;
- introduce a persistent team identity;
- use ML, online RL, or GenAI for gameplay/config decisions;
- create a new production telemetry event;
- declare the Fixed-vs-Adaptive experiment ready.

---

# 4. Source Priority / Governance

Project semantics use this authority order:

```text
1. AI_Architecture_v1.1.md
2. Player_Team_Profile_Contract_v1.1.md
3. Telemetry_Contract_v1.1.md
4. approved gameplay/map/objective/door/network contracts
5. Stalker_AI_Design_v1.1.md
6. Listener_AI_Design_v1.0.md
7. Warden_AI_Design_v1.0.md
8. M1-015 v0 predecessor
9. M1-020 v0 experiment contract
10. current implementation evidence
11. historical notes
12. external research for methodology only
```

Rules:

```text
current baselined architecture/profile/telemetry
>
historical M1 assumption
```

```text
approved gameplay semantic
>
AED convenience
```

```text
deterministic measurable evidence
>
desired Adaptive readiness
```

Current code cannot silently override an approved contract. If implementation later differs, classify it as a gap/migration issue unless an approved contract revision explicitly changes behavior.

## 4.1 Engine/network factual semantics

Official Unity/Photon documentation outranks implementation assumptions for API/network facts.

## 4.2 External research use

External DDA literature is used only to justify methodology:

- bounded adaptation is preferable to opaque/unconstrained mutation;
- interpretable rules and reproducible configuration improve analysis;
- the policy should avoid uncontrolled oscillation and preserve consistent game rules.

Research does not create ECHO PROTOCOL gameplay facts.

---

# 5. M1-015 v0 → AED v1.1 Validity Review

| v0 decision | v1.1 | Reason |
|---|---|---|
| Profile/TeamPerformance input bundle | **SUPERSEDE** | Profile v1.1 now owns immutable `AdaptiveInputSnapshot`. |
| TeamPerformance `COMPLETE + non-null` mandatory gate | **SUPERSEDE for AED v1.1** | Current honest TeamPerformance is INCOMPLETE/null; v1.1 consumes explicit Profile dimensions without fabricating TeamPerformance. |
| No partial TeamPerformance renormalization | **KEEP** | Still forbidden. |
| Raw telemetry forbidden | **KEEP** | Architecture boundary. |
| `ScenarioConfig` topology | **KEEP** | No source requires schema redesign. |
| `configSource = FIXED | ADAPTIVE` | **KEEP / clarify** | Applied-config provenance, not requested mode or experiment assignment. |
| `policyVersion` required on FIXED and ADAPTIVE config | **KEEP** | Contract/validation provenance. |
| adaptive authority whitelist | **KEEP** | Field existence != adaptive authority. |
| Stalker 4-key whitelist | **KEEP** | `DetectionFillRate`, `DetectionDecayRate`, `ChaseSpeed`, `SearchDuration`. |
| non-adaptive Stalker fields | **KEEP** | Perception/combat envelopes remain stable. |
| `AdaptiveParameterRegistry` | **MODIFY** | Add deterministic `candidateValues[]`/adjustment metadata. |
| min/default/max | **KEEP** | Required for numerical keys; no clamp. |
| pressureAxis | **KEEP** | Detection/Chase/Search/NONE. |
| PRE_MATCH | **KEEP** | Current policy's only delta-producing point. |
| ALLOWED_PHASE_BOUNDARY | **KEEP authority; MODIFY policy activation** | Controlled point retained, but policy v1.1 generates HOLD/NO_CHANGE only. |
| FINAL_HUNT_SETUP | **KEEP authority; MODIFY policy activation** | Timer authority retained, but policy v1.1 does not select it. |
| max 1–2 changed keys at boundary | **KEEP fairness envelope** | v1.1 policy is stricter: generates at most one changed key. |
| max one aggressive Stalker pressure axis | **KEEP** | Fairness. |
| double Detection buff forbidden | **KEEP** | Fairness. |
| `objectiveSpawnSetId` | **KEEP authority / POLICY_NOT_SELECTED** | No defensible current deterministic selection rule. |
| `supportItemBudget` | **KEEP authority / POLICY_ACTIVE PRE_MATCH** | Used as the single v1.1 relief action. |
| `routeModifier` | **KEEP authority / POLICY_NOT_SELECTED** | Safety infrastructure retained; v1.1 policy does not choose route content. |
| Final Hunt timer 45..60 | **KEEP authority / POLICY_NOT_SELECTED** | Timing/bounds retained; current policy does not select it. |
| ScenarioValidator | **KEEP + expand** | Add snapshot linkage, policy activation, stale-state and base-CAS checks. |
| atomic application | **KEEP** | No partial apply. |
| `NO_CHANGE` | **KEEP** | Successful zero-delta outcome. |
| FixedDirector | **KEEP** | Independent safe path. |
| `FULL_FIXED_CONFIG` PRE_MATCH | **KEEP** | Full fixed fallback before match start. |
| `KEEP_LAST_VALID_CONFIG` mid-match | **KEEP** | No live blanket reset. |
| `FIXED_BASELINE_V1` | **KEEP** | Do not rename if fixed template content did not change. |
| `AdaptiveDecision` | **KEEP + expand** | Add snapshot fingerprint/base config/mode/evidence/rule/stale provenance. |
| reasonCode | **KEEP + expand minimally** | Add stale/identity categories required by v1.1. |
| deterministic reproducibility | **KEEP + strengthen** | Rule IDs, config fingerprints, exact source revisions. |
| `PacingState` | **DEFER** | Still not current policy state. |

## 5.1 Core v1.1 semantic change

```text
M1-015 v0:
TeamPerformance COMPLETE
→ policy may evaluate
```

becomes:

```text
AED v1.1:
AdaptiveInputSnapshot
→ current-revision validation
→ evidence policy
→ policy may evaluate
```

This is a policy-semantic change. Therefore v1.1 does not reuse the predecessor's policy semantic identity.

---

# 6. Architecture Boundary

```text
Profile
owns processed evidence and AdaptiveInputSnapshot construction

AED
owns consumption eligibility + deterministic policy

ScenarioValidator
owns candidate legality

Host / State Authority
owns authoritative application

Monster AI
consumes validated configuration through its normal config boundary
```

Forbidden shortcuts:

```text
TelemetryEvent → AED rule
Profile score → CHASE
AED → ATTACK
AED → CurrentTarget
AED → HearingObservation
AED → Warden current DoorId
AED → hidden Player Transform
```

---

# 7. Terminology

## 7.1 `ScenarioResolutionMode`

```text
FIXED
ADAPTIVE
```

Requested by match/session/experiment orchestration. It is not inferred from `configSource`.

## 7.2 `SnapshotValidity`

Profile-owned:

```text
VALID
PARTIAL
INVALID
```

AED consumes but does not rewrite it.

## 7.3 `AEDInputGateStatus`

```text
ELIGIBLE
INELIGIBLE
INVALID
```

## 7.4 `AdaptationIntent`

Policy v1.1 activates:

```text
RELIEVE
HOLD
INCREASE_PRESSURE
```

This is **not** M1-015 `PacingState`.

## 7.5 `CandidateValidationStatus`

```text
NOT_EVALUATED
VALID
INVALID
```

## 7.6 `AdaptiveDecisionResult`

```text
APPLIED
NO_CHANGE
FIXED_FALLBACK
```

`REJECTED` remains an internal candidate outcome, not a final decision result.

---

# 8. Resolution Mode — FIXED vs ADAPTIVE

## 8.1 FIXED

```text
ScenarioResolutionMode = FIXED
→ AdaptiveInputGate not required for gameplay resolution
→ AEDPolicyEvaluator MUST NOT execute
→ FixedDirector path
```

At PRE_MATCH:

```text
FixedDirector
→ resolve/validate full fixed ScenarioConfig
→ configSource = FIXED
```

At ALLOWED_PHASE_BOUNDARY or FINAL_HUNT_SETUP:

```text
FixedDirector
→ KEEP_LAST_VALID_CONFIG
→ no replacement config
→ existing configSource remains unchanged
```

FIXED mode is not an Adaptive failure.

## 8.2 ADAPTIVE

```text
ScenarioResolutionMode = ADAPTIVE
→ validate AdaptiveInputSnapshot
→ AED Input Gate
→ if ELIGIBLE: evaluate policy
→ if INELIGIBLE/INVALID/unavailable: safe FixedDirector fallback
```

## 8.3 Resolution request identity

Logical input:

```text
ScenarioResolutionRequest
{
    resolutionId
    targetMatchId
    resolutionMode
    decisionPoint
    phaseContext?
    experimentCondition?
}
```

Exact class/storage is an implementation binding.

A FIXED request does not require an `AdaptiveDecision`. A compact `ScenarioResolutionRecord` or equivalent audit entry may reference the FixedDirector outcome.

---

# 9. Experiment Condition vs `configSource`

Hard distinction:

```text
experimentCondition
≠
ScenarioResolutionMode
≠
AppliedScenarioConfig.configSource
```

Typical experiment mapping:

```text
Condition F
→ resolutionMode = FIXED

Condition A
→ resolutionMode = ADAPTIVE
```

But:

```text
experimentCondition = ADAPTIVE
+ AED gate fails
→ Fixed fallback
→ resulting PRE_MATCH configSource may be FIXED
→ experimentCondition remains ADAPTIVE
```

Fallback exposure is research evidence; it does not relabel experimental assignment.

---

# 10. AdaptiveInputSnapshot Consumption

AED v1.1 consumes the Profile-owned immutable object:

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
        dimensionComparisonKeys{}
        teamPerformanceFormulaVersion
        sourceProfileRevisions[]
        currentMatchTelemetrySchemaVersion?
    }
}
```

AED does not reinterpret raw telemetry or reconstruct Player Profile history at decision time.

## 10.1 Required consumption checks

- targetMatchId matches current authoritative target match;
- decisionPoint matches request;
- phaseContext matches current authoritative phase where applicable;
- rosterIdentity still matches current roster;
- each sourceProfileRevision is current;
- Profile semantic IDs/keys are supported;
- snapshot fingerprint validates;
- snapshot is not superseded by a source-profile retraction/replay;
- `snapshotValidity` obeys §11;
- required evidence obeys §13.

---

# 11. Snapshot Validity Policy

v1.1 uses a conservative gate.

| SnapshotValidity | AED behavior |
|---|---|
| `VALID` | May continue to evidence sufficiency checks. |
| `PARTIAL` | `INELIGIBLE`; Adaptive policy does not run. |
| `INVALID` | `INVALID`; Adaptive policy does not run. |

There is **no** v1.1 allowlist of PARTIAL reasons that permits adaptation.

Rationale:

- Profile already distinguishes structural invalidity from evidence incompleteness;
- using only VALID snapshots removes ambiguous partial-data behavior from the first implementable policy;
- v1.1 does not need to fabricate missing evidence.

Fallback then follows the decision point.

---

# 12. COLD_START and DEFERRED Semantics

## 12.1 COLD_START

Profile state:

```text
score = 50
status = COLD_START
sampleCount = 0
```

AED interpretation:

```text
COLD_START
→ initialization only
→ not observed performance
→ excluded from observed means/bands
```

Because v1.1 requires a VALID full-roster snapshot for Adaptive eligibility, any current roster member lacking an observed ACTIVE value in a required dimension makes Adaptive evidence insufficient.

## 12.2 DEFERRED

The following are not AED numeric inputs:

```text
OBJECTIVE
TEAMWORK
EXPLORATION
NAVIGATION
TOOL_USAGE
RISK
REVIVE
```

Forbidden:

```text
DEFERRED → 50
DEFERRED → 0
null → neutral difficulty
```

---

# 13. AED Evidence Sufficiency Policy

`AEDEvidencePolicy` is the **single authoritative owner** of Adaptive evidence eligibility configuration.

Logical versioned configuration:

```text
AEDEvidencePolicy
{
    evidencePolicyVersion

    requiredPlayerDimensions = [SURVIVAL, NOISE]

    requireFullRosterObservedCoverage = true

    minimumSampleCountPerObservedPlayer {
        SURVIVAL
        NOISE
    }

    allowedSnapshotValidity = [VALID]

    allowedCurrentMatchMetrics = []
    allowedCurrentMatchFinalityByMetric = {}
}
```

Hard ownership invariant:

```text
minimumSampleCountPerObservedPlayer
→ owned only by AEDEvidencePolicy
→ versioned only through evidencePolicyVersion
```

`AEDPolicyConfig` MUST NOT contain a second authoritative `minimumSampleCountPerObservedPlayer` value. It may reference only `evidencePolicyVersion`.

A legacy/serialized DTO that still exposes a duplicate sample-threshold field cannot treat that field as tuning authority. If both representations are present and disagree, configuration validation fails before policy execution:

```text
→ CandidateValidationStatus = NOT_EVALUATED
→ reasonCode = POLICY_CONFIG_INVALID
→ Adaptive policy MUST NOT run
→ fallback by decision point
```

## 13.1 Frozen topology

For PRE_MATCH Adaptive eligibility:

```text
snapshotValidity == VALID
AND
SURVIVAL aggregationStatus == AVAILABLE
AND
NOISE aggregationStatus == AVAILABLE
AND
SURVIVAL observedActiveCount == teamSize
AND
NOISE observedActiveCount == teamSize
AND
all included SURVIVAL comparison keys coherent
AND
all included NOISE comparison keys coherent
AND
every roster player:
    SURVIVAL status = ACTIVE
    NOISE status = ACTIVE
    SURVIVAL score != null
    NOISE score != null
    SURVIVAL sampleCount >= configured minimum
    NOISE sampleCount >= configured minimum
AND
all source revisions current
→ evidence may be ELIGIBLE
```

`requireFullRosterObservedCoverage = true` is a frozen v1.1 evidence-policy semantic, equivalent to required observed coverage ratio `1.0` for both policy dimensions.

## 13.2 Sample-count thresholds

Exact threshold values remain:

```text
TUNING TBD
```

but their ownership is exact:

```text
minimumSampleCountPerObservedPlayer
→ AEDEvidencePolicy
→ evidencePolicyVersion
```

Runtime validity rule:

```text
threshold missing
OR threshold < 1
OR evidencePolicyVersion unsupported
OR threshold content changed without a new evidencePolicyVersion
→ referenced AEDEvidencePolicy invalid before candidate construction
→ CandidateValidationStatus = NOT_EVALUATED
→ reasonCode = POLICY_CONFIG_INVALID
→ Adaptive policy MUST NOT run
→ fallback by decision point
```

No developer may invent a default value in code.

A change to a sample threshold requires a new `evidencePolicyVersion`. It does **not** by itself require a new `policyConfigVersion`.

## 13.3 Evidence-policy version ownership

```text
evidence eligibility topology change
→ evidencePolicyVersion changes

minimumSampleCountPerObservedPlayer change
→ evidencePolicyVersion changes

snapshot validity allowance change
→ evidencePolicyVersion changes

full-roster requirement change
→ evidencePolicyVersion changes

current-match evidence metric/finality allowance change
→ evidencePolicyVersion changes
```

If `requiredPlayerDimensions` changes, evidence topology changes and therefore `evidencePolicyVersion` changes. Because the current rule predicates and policy meaning are defined over `SURVIVAL` and `NOISE`, changing that input topology also requires the policy semantic impact to be evaluated; for `AED_SCENARIO_POLICY_V1_1`, changing the required dimensions requires a new `policyVersion` as well.

One product change may therefore require multiple coordinated version bumps when it crosses multiple ownership domains.

## 13.4 Profile freshness

v1.1 does **not** invent a wall-clock `maxProfileAge`.

```text
profileRevision currency
= mandatory

age threshold
= not active in policy v1.1
```

A future freshness threshold belongs to `AEDEvidencePolicy`; activating it changes evidence eligibility semantics and therefore requires a new `evidencePolicyVersion`.

---

# 14. AED Input Gate

Logical result:

```text
AEDInputGateResult
{
    status
    reasons[]
    snapshotId
    snapshotContentFingerprint
    targetMatchId
    decisionPoint
    rosterIdentity
    sourceProfileRevisions[]
    resolvedEvidencePolicyVersion
    resolvedPolicyVersion
}
```

## 14.1 Status

### ELIGIBLE

All structure, version, current-revision, validity, and evidence checks pass.

### INELIGIBLE

Input is validly structured but policy prerequisites are not satisfied, for example:

- snapshot PARTIAL;
- COLD_START/insufficient sample coverage;
- required current roster evidence missing.

### INVALID

Input is unsafe/incompatible, for example:

- snapshot INVALID;
- source revision mismatch;
- roster mismatch;
- formula semantic conflict;
- unsupported Profile/policy version;
- fingerprint mismatch.

## 14.2 Gate result ownership

AED may map Profile reasons to higher-level categories while retaining source reason references. It must not clone or diverge the entire Profile reason registry.

---

# 15. PRE_MATCH Input Contract

Required source:

```text
current target-match authoritative roster
+
immutable current PlayerProfileSnapshots
+
RosterProfileSummary
```

Forbidden:

```text
current-match TeamProfile
previous-match TeamProfile
raw TelemetryEvent
raw Monster telemetry
hidden runtime state
```

`targetMatchId` must already be resolvable.

This preserves the Profile-side SC-03 correction without inventing persistent team identity.

---

# 16. ALLOWED_PHASE_BOUNDARY Input Contract

The Profile snapshot may include:

- historical PlayerProfileSnapshots;
- RosterProfileSummary;
- currentMatchProcessedEvidence.

However, current policy v1.1 consumes:

```text
allowedCurrentMatchMetrics = []
```

Therefore:

```text
FINAL current-match evidence
→ not consumed by policy v1.1

PROVISIONAL current-match evidence
→ not consumed by policy v1.1
```

The field may remain present for future policy/research but cannot affect current requested changes.

---

# 17. FINAL_HUNT_SETUP Input Contract

Same processed-data boundary as §16.

Policy v1.1 consumes no current-match metric at this point.

It does not infer:

- future match outcome;
- final Team Survival before match end;
- raw Warden state;
- Monster target state;
- Player Transform.

---

# 18. Current-Match Evidence Allowlist

| Decision Point | Metric | Finality Allowed | Policy v1.1 Consumer |
|---|---|---|---|
| PRE_MATCH | none | n/a | none |
| ALLOWED_PHASE_BOUNDARY | none | n/a | none |
| FINAL_HUNT_SETUP | none | n/a | none |

This is intentional, not missing design.

Rationale:

- the GDD's evidenced v1.1 use case is historical Profile informing Scenario Configuration;
- current Profile contract exposes finality but does not require AED to consume it;
- no current source establishes a defensible current-match metric→parameter rule;
- the first policy stays bounded and reproducible.

Activating any current-match metric requires a policy semantic/config revision and tests.

---

# 19. TeamPerformance Gate Decision

## 19.1 v0 status

M1-015 v0 required:

```text
TeamPerformance.status = COMPLETE
AND
TeamPerformance.score != null
```

Profile v1.1 currently and honestly provides:

```text
TeamPerformance.status = INCOMPLETE
TeamPerformance.score = null
```

because Teamwork and ResourceEfficiency remain DEFERRED.

## 19.2 v1.1 decision

**PREDECESSOR — SUPERSEDE for AED input eligibility**

AED v1.1 does **not** require TeamPerformance COMPLETE.

Instead it consumes the Profile-owned decision-scoped evidence:

```text
RosterProfileSummary.SURVIVAL
RosterProfileSummary.NOISE
```

under §13.

This is **not**:

```text
Survival + Noise = TeamPerformance
```

No TeamPerformance value is fabricated, renormalized, or renamed.

## 19.3 Consequence

Because input eligibility changed, policy semantic identity changes.

M1-020 v0's experiment readiness gate no longer describes AED v1.1 eligibility and must be revised before any Fixed-vs-Adaptive experiment execution.

This document does not silently rewrite M1-020 and does not claim the experiment is ready.

---

# 20. Policy Semantic Version

Canonical logical identity:

```text
AED_SCENARIO_POLICY_V1_1
```

Exact persisted encoding:

```text
IMPLEMENTATION BINDING TBD
```

Hard rule:

```text
AED_SCENARIO_POLICY_V1_1 eligibility/rule semantics
!=
M1-015 v0 TeamPerformance-COMPLETE semantics
```

`policyVersion` owns semantic policy behavior, including:

- policy rule topology;
- canonical rule priority;
- canonical predicates;
- policy-active key set;
- AdaptationIntent meaning/mapping;
- strategy binding;
- fairness semantic changes;
- fallback semantic changes;
- required policy input topology when that topology changes the meaning of the policy.

Tuning-only score-band threshold changes are owned by `policyConfigVersion`.

Evidence eligibility settings are not `policyConfigVersion` tuning. They are owned by `AEDEvidencePolicy` / `evidencePolicyVersion`.

## 20.1 Canonical `AEDPolicyDefinition`

`AEDPolicyDefinition` is the semantic source of truth for `AED_SCENARIO_POLICY_V1_1`.

Conceptual model:

```text
AEDPolicyDefinition
{
    policySemanticId = AED_SCENARIO_POLICY_V1_1

    canonicalRuleIds
    canonicalRulePriorities
    canonicalPredicates
    canonicalIntentMappings

    canonicalActivePolicyKeys
    canonicalStrategyBindings
}
```

For `AED_SCENARIO_POLICY_V1_1`, freeze:

```text
canonicalRuleIds / priorities

10  AED-PRE-010-SURVIVAL-LOW-RELIEVE
20  AED-PRE-020-NOISE-LOW-RELIEVE
30  AED-PRE-030-BOTH-HIGH-INCREASE
40  AED-PRE-040-MIXED-HOLD
100 AED-BND-100-HOLD
110 AED-FH-110-HOLD
```

```text
canonicalPredicates / intents

10:
SURVIVAL = LOW
→ RELIEVE

20:
SURVIVAL != LOW
AND NOISE = LOW
→ RELIEVE

30:
SURVIVAL = HIGH
AND NOISE = HIGH
→ INCREASE_PRESSURE

40:
all remaining valid PRE_MATCH band combinations
→ HOLD

100:
ALLOWED_PHASE_BOUNDARY
→ HOLD

110:
FINAL_HUNT_SETUP
→ HOLD
```

```text
canonicalActivePolicyKeys

SupportItemBudget
ChaseSpeed
```

```text
canonicalStrategyBindings

AED-PRE-010-SURVIVAL-LOW-RELIEVE
→ SupportItemBudget
→ NEXT_HIGHER_REGISTERED_VALUE

AED-PRE-020-NOISE-LOW-RELIEVE
→ SupportItemBudget
→ NEXT_HIGHER_REGISTERED_VALUE

AED-PRE-030-BOTH-HIGH-INCREASE
→ if primary monsterType == STALKER:
     ChaseSpeed
     NEXT_HIGHER_REGISTERED_VALUE
  otherwise:
     zero delta / NO_CHANGE

AED-PRE-040-MIXED-HOLD
→ zero delta

AED-BND-100-HOLD
→ zero delta

AED-FH-110-HOLD
→ zero delta
```

These semantics cannot be changed by `policyConfigVersion`.

## 20.2 Serialized semantic mirrors

An implementation may serialize fields such as:

```text
activeRuleIds[]
rulePriorities{}
activePolicyKeys[]
strategyBindings{}
```

for inspection, caching, or asset validation.

For:

```text
policySemanticId = AED_SCENARIO_POLICY_V1_1
```

those fields are **mirrors only**, not tuning authority.

Invariant:

```text
serialized activeRuleIds
serialized rulePriorities
serialized activePolicyKeys
serialized strategyBindings
MUST exactly match canonical AEDPolicyDefinition
```

Mismatch:

```text
→ CandidateValidationStatus = NOT_EVALUATED
→ reasonCode = POLICY_CONFIG_INVALID
→ policy MUST NOT execute
→ fallback by decision point
```

Therefore `policyConfigVersion` MUST NOT be sufficient to change:

- rule topology;
- canonical priority;
- predicates;
- AdaptationIntent mapping;
- active policy key set;
- strategy binding.

Any such semantic change requires a new supported `policyVersion` / policy semantic identity.

---

# 21. Actual AED Policy Topology

```text
AdaptiveInputSnapshot
→ AEDInputGate
→ RosterProfileSummary:
     SURVIVAL meanObservedScore
     NOISE meanObservedScore
→ versioned score bands
→ deterministic rule table
→ AdaptationIntent
→ one policy-active action strategy
→ requested change or zero delta
→ CandidateScenarioConfig
→ ScenarioValidator
```

There is no opaque aggregate skill score.

---

# 22. Score Band Contract

Policy v1.1 uses independent bands for `SURVIVAL` and `NOISE`.

For each dimension D:

```text
0 <= score <= 100

LOW:
0 <= score < lowThreshold[D]

MID:
lowThreshold[D] <= score < highThreshold[D]

HIGH:
highThreshold[D] <= score <= 100
```

Boundary behavior is exact:

- value equal to `lowThreshold` is MID;
- value equal to `highThreshold` is HIGH.

Required threshold constraints:

```text
0 < lowThreshold[D] < highThreshold[D] < 100
```

Exact numerical thresholds:

```text
TUNING TBD
```

They must be present in a supported `AEDPolicyConfig` before Adaptive execution.

Missing/invalid thresholds:

```text
→ POLICY_CONFIG_INVALID
→ no policy evaluation
→ Fixed fallback
```

## 22.1 Direction semantics

```text
higher SURVIVAL
→ stronger historical survival outcome

higher NOISE
→ fewer / lower filtered penalty-noise signals under the Profile contract
```

AED does not reverse these directions.

---

# 23. AdaptationIntent

```text
RELIEVE
HOLD
INCREASE_PRESSURE
```

Semantics:

- `RELIEVE`: current historical evidence indicates the first v1.1 policy should make one bounded player-support change.
- `HOLD`: evidence does not justify a v1.1 change.
- `INCREASE_PRESSURE`: both required dimensions are HIGH and one bounded Stalker pressure change may be requested if applicable.

This is a policy decision label only. It does not directly mutate gameplay.

---

# 24. Canonical Policy Rule Table

This table is the canonical human-readable projection of `AEDPolicyDefinition` in §20.1. It is not a second independently configurable rule source.

Rule priority is frozen by `AED_SCENARIO_POLICY_V1_1` and evaluated top-to-bottom by numeric priority, never container/dictionary iteration order.

| Priority | Rule ID | Decision Point | Required Evidence | Predicate | Intent | Candidate strategy |
|---:|---|---|---|---|---|---|
| 10 | `AED-PRE-010-SURVIVAL-LOW-RELIEVE` | PRE_MATCH | VALID, sufficient SURVIVAL+NOISE | SURVIVAL = LOW | RELIEVE | Increase SupportItemBudget by one registered candidate step |
| 20 | `AED-PRE-020-NOISE-LOW-RELIEVE` | PRE_MATCH | same | SURVIVAL != LOW AND NOISE = LOW | RELIEVE | Increase SupportItemBudget by one registered candidate step |
| 30 | `AED-PRE-030-BOTH-HIGH-INCREASE` | PRE_MATCH | same | SURVIVAL = HIGH AND NOISE = HIGH | INCREASE_PRESSURE | If scenario primary monster is STALKER, increase ChaseSpeed one registered candidate step; otherwise zero delta |
| 40 | `AED-PRE-040-MIXED-HOLD` | PRE_MATCH | same | all remaining valid band combinations | HOLD | zero delta |
| 100 | `AED-BND-100-HOLD` | ALLOWED_PHASE_BOUNDARY | gate ELIGIBLE if ADAPTIVE decision is requested | always | HOLD | zero delta |
| 110 | `AED-FH-110-HOLD` | FINAL_HUNT_SETUP | gate ELIGIBLE if ADAPTIVE decision is requested | always | HOLD | zero delta |

## 24.1 Why LOW causes relief

A LOW observed value in either required dimension is treated conservatively:

- low Survival should not cause additional Monster pressure;
- low Noise score represents worse penalty-noise behavior under Profile semantics, so increasing pressure would compound difficulty rather than compensate safely.

## 24.2 Why both HIGH can increase pressure

Only the conjunction:

```text
SURVIVAL = HIGH
AND
NOISE = HIGH
```

can produce `INCREASE_PRESSURE`.

This avoids an increase based on one strong dimension while the other indicates poor outcomes.

## 24.3 No matching rule

The canonical rule set is exhaustive for valid bands. An implementation that cannot map valid inputs to exactly one rule has a policy-definition/config integrity failure:

```text
→ CandidateValidationStatus = NOT_EVALUATED
→ reasonCode = POLICY_CONFIG_INVALID
→ no candidate is built
→ fallback by decision point
```

---

# 25. Rule Priority / Tie-Break

For `AED_SCENARIO_POLICY_V1_1`, priority is semantic policy definition, not tuning.

Determinism:

```text
canonical AEDPolicyDefinition
→ evaluate rules in frozen priority order:
   10, 20, 30, 40, 100, 110
→ first matching rule wins
```

Duplicate canonical priority is not supported by v1.1.

If an implementation stores mirrored priority data and it differs from §20.1/§24:

```text
→ CandidateValidationStatus = NOT_EVALUATED
→ reasonCode = POLICY_CONFIG_INVALID
→ policy MUST NOT execute
```

No RuleId tie-break is needed for a valid v1.1 definition because canonical priorities are unique.

No random tie-break.

---

# 26. Policy Stability / Anti-Thrashing

Policy v1.1 does not require a runtime hysteresis timer/cooldown.

Reason:

```text
only PRE_MATCH produces adaptive deltas
+
at most one delta is generated
+
phase-boundary and Final-Hunt policy rules are HOLD
```

Therefore there is no repeated within-match increase/decrease oscillation to smooth.

v1.1 does **not** introduce:

- per-frame pacing;
- event-triggered difficulty reaction;
- moving-average pressure estimator;
- policy cooldown state;
- PacingState.

If later policy activates repeated mid-match changes, hysteresis/cooldown becomes a policy-semantic/config revision.

---

# 27. Adaptive Authority vs Policy Activation

Hard distinction:

```text
ADAPTIVE_AUTHORIZED
!=
POLICY_ACTIVE
```

A key may remain safely authorized by the ScenarioConfig contract while the current rule set never selects it.

---

# 28. Policy Activation Matrix

The `v1.1 Policy active?` column is derived from the canonical `AEDPolicyDefinition` in §20.1. It is not independently configurable by `AEDPolicyConfig`.

| Candidate key | Adaptive authority | v1.1 Policy active? | Allowed timing by authority | v1.1 rule |
|---|---:|---:|---|---|
| `objectiveSpawnSetId` | YES | NO | PRE_MATCH | `POLICY_NOT_SELECTED` |
| `supportItemBudget` | YES | **YES** | PRE_MATCH / allowed boundary per registry | PRE_MATCH relief |
| `DetectionFillRate` | YES | NO | registry timing | `POLICY_NOT_SELECTED` |
| `DetectionDecayRate` | YES | NO | registry timing | `POLICY_NOT_SELECTED` |
| `ChaseSpeed` | YES | **YES** | registry timing | PRE_MATCH pressure increase when `monsterType=STALKER` |
| `SearchDuration` | YES | NO | registry timing | `POLICY_NOT_SELECTED` |
| `routeModifier` | YES | NO | ALLOWED_PHASE_BOUNDARY only | `POLICY_NOT_SELECTED` |
| `FinalHunt.EscapeDoorTimer` | YES | NO | FINAL_HUNT_SETUP only | `POLICY_NOT_SELECTED` |

Current policy-generated changed-key maximum:

```text
<= 1
```

at every decision point.

This is stricter than the predecessor's phase-boundary fairness envelope of 1–2 changes.

Changing the active set for the same `policySemanticId` is invalid. A different active set requires a new supported `policyVersion`.

---

# 29. Stalker Adaptive Authority

Closed authority whitelist remains:

```text
DetectionFillRate
DetectionDecayRate
ChaseSpeed
SearchDuration
```

Explicit non-adaptive:

```text
VisionDistance
VisionAngle
PatrolSpeed
SearchRadius
AttackRange
AttackWindup
AttackRecovery
StalkerDamagePercent
```

Current policy v1.1 selects only:

```text
ChaseSpeed
```

The other three remain authorized but inactive.

No authority expansion occurs.

---

# 30. Listener / Warden Adaptive Authority

## 30.1 Listener

```text
Listener adaptive runtime authority = NONE
```

No hearing range, attenuation, investigation threshold, FSM state, target, or memory field is adaptive-authorized.

## 30.2 Warden

```text
Warden runtime-action adaptive authority = NONE
```

AED cannot choose:

- current DoorId;
- Warden candidate;
- lock timing;
- telegraph target;
- route-pressure action.

`routeModifier` is a Scenario-level route configuration field, not a Warden command.

---

# 31. Adaptive Parameter Registry

Logical contract:

```text
AdaptiveParameterRule
{
    key
    owner

    defaultValue
    minValue
    maxValue

    allowedTiming[]
    pressureAxis

    candidateValues[]?
    adjustmentRuleId?
}
```

Numerical registry keys remain:

```text
DetectionFillRate
DetectionDecayRate
ChaseSpeed
SearchDuration
SupportItemBudget
FinalHunt.EscapeDoorTimer
```

Discrete whitelist selections:

```text
objectiveSpawnSetId
routeModifier
```

## 31.1 Active-key completeness

For every policy-active numerical key:

```text
min/default/max
candidateValues[]
allowedTiming
owner
pressureAxis
adjustmentRuleId
```

must resolve from one supported `parameterRegistryVersion`.

Registry/config integrity is a **pre-candidate-construction** stage.

Examples that make the registry unusable before candidate construction:

- parameter registry version unsupported;
- registry malformed;
- policy-active key entry missing;
- required `candidateValues[]` missing;
- required bounds/default missing;
- `pressureAxis` metadata missing/unsupported;
- `adjustmentRuleId` metadata missing/unsupported;
- current authoritative base value cannot be reconciled with the valid registered value set required by the active adjustment rule.

Canonical result:

```text
CandidateValidationStatus = NOT_EVALUATED
result = FIXED_FALLBACK
reasonCode = PARAMETER_REGISTRY_INVALID
candidate MUST NOT be built
fallbackAction = FULL_FIXED_CONFIG at PRE_MATCH
              OR KEEP_LAST_VALID_CONFIG at ALLOWED_PHASE_BOUNDARY / FINAL_HUNT_SETUP
```

There is no `INVALID` candidate in this case because candidate validation never occurred.

## 31.2 Valid registry vs invalid candidate

Once the registry has passed pre-build validation, a constructed/requested candidate can still violate registry-governed rules.

Examples:

- requested value is outside valid bounds;
- requested key is not policy-active;
- requestedAfter is not a legal registered candidate value;
- adjustment output is inconsistent with `NEXT_HIGHER_REGISTERED_VALUE`.

Those are **candidate validation** failures:

```text
CandidateValidationStatus = INVALID
result = FIXED_FALLBACK
```

with the exact reason owned by the violated rule, such as:

```text
BOUND_REJECTED
POLICY_KEY_NOT_ACTIVE
REGISTERED_VALUE_REJECTED
```

---

# 32. Numerical Adjustment Rule

v1.1 uses **designer-authored candidate values**, not arbitrary generated floats.

For active key K:

```text
candidateValues[K]
=
strictly ordered finite set of legal resolved values
```

Registry validation requires:

- every candidate is finite;
- every candidate lies within `[minValue,maxValue]`;
- no duplicate candidate;
- ordering deterministic;
- current adaptive base value is represented or explicitly rejected as registry-incompatible.

## 32.1 `NEXT_HIGHER_REGISTERED_VALUE`

Used for both active strategies:

```text
select the smallest registered candidate value > currentValue
```

If none:

```text
requested delta = none
→ NO_CHANGE
→ PolicyNoChangeReason = VALUE_LIMIT_REACHED
```

No clamp.

No interpolation.

No random value.

No arbitrary AI-generated float.

## 32.2 Relief strategy

```text
RELIEVE
→ SupportItemBudget
→ NEXT_HIGHER_REGISTERED_VALUE
```

This affects future eligible support allocation only.

## 32.3 Increase-pressure strategy

```text
INCREASE_PRESSURE
+ monsterType = STALKER
→ ChaseSpeed
→ NEXT_HIGHER_REGISTERED_VALUE
```

If current scenario primary `monsterType != STALKER`:

```text
no applicable v1.1 pressure key
→ zero delta
→ NO_CHANGE
→ PolicyNoChangeReason = KEY_NOT_APPLICABLE
```

AED never changes `monsterType`.

---

# 33. Bounds and Registry Failure

Lifecycle ownership is exact.

## 33.1 Registry/config failure before candidate construction

```text
unsupported/malformed parameter registry
OR missing policy-active registry entry
OR missing candidateValues/bounds/adjustment metadata
→ CandidateValidationStatus = NOT_EVALUATED
→ result = FIXED_FALLBACK
→ reasonCode = PARAMETER_REGISTRY_INVALID
→ candidate MUST NOT be built
```

No `INVALID` candidate exists in this case.

## 33.2 Candidate violation after valid registry resolution

For a numerical adaptive request built against a valid registry:

```text
requested < min
OR requested > max
→ CandidateValidationStatus = INVALID
→ result = FIXED_FALLBACK
→ reasonCode = BOUND_REJECTED
```

For a requested numerical value that is within broad bounds but is not a legal registered candidate under the active adjustment rule:

```text
→ CandidateValidationStatus = INVALID
→ result = FIXED_FALLBACK
→ reasonCode = REGISTERED_VALUE_REJECTED
```

For a policy-inactive key:

```text
→ CandidateValidationStatus = INVALID
→ result = FIXED_FALLBACK
→ reasonCode = POLICY_KEY_NOT_ACTIVE
```

No silent clamp.

Final Hunt timer retains frozen bound:

```text
45 <= escapeDoorTimerSeconds <= 60
```

---

# 34. Pressure-Axis Fairness

Canonical mapping:

```text
DetectionFillRate ↑
→ DetectionPressure MORE_AGGRESSIVE

DetectionDecayRate ↓
→ DetectionPressure MORE_AGGRESSIVE

ChaseSpeed ↑
→ ChasePressure MORE_AGGRESSIVE

SearchDuration ↑
→ SearchPressure MORE_AGGRESSIVE
```

Axes:

```text
DetectionPressure
ChasePressure
SearchPressure
NONE
```

`SupportItemBudget` and Final Hunt timer map to `NONE`.

## 34.1 Hard fairness

```text
PRE_MATCH:
more-aggressive Stalker axes <= 1

ALLOWED_PHASE_BOUNDARY:
more-aggressive Stalker axes <= 1
```

Forbidden:

```text
DetectionFillRate ↑
+
DetectionDecayRate ↓
```

Forbidden:

```text
ChaseSpeed ↑
+
SearchDuration ↑
```

v1.1 policy itself generates at most one changed key, but ScenarioValidator still enforces the inherited fairness envelope defensively.

---

# 35. Objective Spawn Set

`objectiveSpawnSetId` remains:

```text
ADAPTIVE_AUTHORIZED at PRE_MATCH
POLICY_NOT_SELECTED in AED_SCENARIO_POLICY_V1_1
```

Any fixed or future adaptive candidate must use a designer-authored whitelist and pass:

- map/scenario compatibility;
- objective validity;
- objective reachability;
- no soft-lock;
- legal traversal.

AED cannot generate a Vector3 spawn.

---

# 36. Support Item Budget

`supportItemBudget` is:

```text
adaptive-authorized
bounded
registry-owned
pressureAxis = NONE
policy-active for PRE_MATCH RELIEVE
```

A v1.1 increase:

```text
→ changes future allocation capacity only
```

It does not:

- spawn an item beside a Player;
- move an item;
- add a Player-owned item immediately;
- retroactively remove already-spawned content;
- delete inventory.

Actual support content remains gameplay/content-whitelist owned.

---

# 37. Route Modifier

`routeModifier` remains:

```text
PRE_MATCH adaptive request
→ INVALID
→ TIMING_REJECTED

ALLOWED_PHASE_BOUNDARY
→ adaptive authority exists
→ current v1.1 policy does not select it
```

It is a designer-authored discrete scenario content ID.

It is not:

- a DoorId;
- a Warden candidate;
- an arbitrary graph diff;
- an instruction to teleport;
- a hidden Player-location response.

---

# 38. Warden / FacilityGraph Route Safety

Scenario route validation must use the current effective player-route state.

Conceptually:

```text
base player-route state
+ current Scenario route overlay
+ objective/phase constraints
+ current Warden route overlay
+ other approved traversal overlays
+ candidate Scenario routeModifier
→ validate resulting player-route state
```

Candidate routeModifier must preserve:

- required objective reachability;
- Exit reachability where required;
- at least one legal route;
- no soft-lock;
- no impossible traversal.

The validator must consume the current authoritative FacilityGraph/route-safety abstraction, not Stalker NavMesh triangle coverage.

Hard invariant:

```text
unsafe candidate
X→ apply first and expect Warden fail-safe to repair later
```

Warden remains independently responsible for revalidating its own active route lock after a valid Scenario graph/config revision.

---

# 39. Final Hunt Escape Door Timer

Authority retained:

```text
FinalHunt.EscapeDoorTimer
```

Scenario field:

```text
finalHuntParameters.escapeDoorTimerSeconds
```

Bounds:

```text
45..60 seconds inclusive
```

Timing:

```text
FINAL_HUNT_SETUP
```

Precondition:

```text
current Escape Door timer instance has not started
```

After start:

```text
value immutable for that timer instance
```

Policy v1.1 classification:

```text
ADAPTIVE_AUTHORIZED
POLICY_NOT_SELECTED
```

No generic objective-timing authority exists.

---

# 40. `monsterType` Validity Audit

Existing ScenarioConfig contains singular:

```text
monsterType
```

**v1.1 decision: KEEP.**

Evidence:

- M1-015 v0 explicitly treats one `monsterType` as designer/FixedDirector scenario content and non-adaptive;
- experiment controls refer to Monster identity as a condition/confounder;
- supplied current approved evidence does not establish a contract requiring multiple simultaneously configured Monster types inside one ScenarioConfig.

Therefore:

```text
ScenarioConfig v1.1
→ one primary scenario monsterType
→ designer/content-whitelist selected
→ never Adaptive-selected
```

If a later approved gameplay contract requires multiple concurrently configured Monster types in one scenario, ScenarioConfig schema must receive a dedicated contract revision before activation. That would be a schema/design revision, not permission for v1.1 code to guess.

---

# 41. ScenarioConfig v1.1 Schema

Logical schema remains:

```text
ScenarioConfig
{
    scenarioConfigVersion
    policyVersion
    configSource

    mapId
    monsterType
    objectiveSpawnSetId

    supportItemBudget

    monsterParameters {
        DetectionFillRate
        DetectionDecayRate
        ChaseSpeed
        SearchDuration
    }

    routeModifier

    finalHuntParameters {
        escapeDoorTimerSeconds
    }

    fallbackConfigId
}
```

No free-form parameter dictionary.

## 41.1 Classification

| Field | v1.1 action | Adaptive policy v1.1 |
|---|---|---|
| scenarioConfigVersion | KEEP | apply/version owner |
| policyVersion | KEEP | required on FIXED and ADAPTIVE |
| configSource | KEEP / clarify | output provenance |
| mapId | KEEP | non-adaptive |
| monsterType | KEEP | non-adaptive |
| objectiveSpawnSetId | KEEP | authorized, inactive |
| supportItemBudget | KEEP | active PRE_MATCH RELIEVE |
| DetectionFillRate | KEEP | authorized, inactive |
| DetectionDecayRate | KEEP | authorized, inactive |
| ChaseSpeed | KEEP | active PRE_MATCH INCREASE |
| SearchDuration | KEEP | authorized, inactive |
| routeModifier | KEEP | authorized boundary-only, inactive |
| escapeDoorTimerSeconds | KEEP | authorized Final-Hunt-only, inactive |
| fallbackConfigId | KEEP | fallback/audit reference |

---

# 42. `configSource` Lifecycle

```text
FIXED
ADAPTIVE
```

### Adaptive APPLIED

```text
validated adaptive content delta
→ new AppliedScenarioConfig
→ configSource = ADAPTIVE
```

### Adaptive NO_CHANGE

```text
no new config
→ existing configSource unchanged
```

### PRE_MATCH full FixedDirector result

```text
new full fixed AppliedScenarioConfig
→ configSource = FIXED
```

### Mid-match fallback

```text
KEEP_LAST_VALID_CONFIG
→ no replacement config
→ existing configSource unchanged
```

A current ADAPTIVE config therefore remains marked ADAPTIVE after a later failed mid-match adaptive attempt that keeps it.

---

# 43. `policyVersion` on FIXED and ADAPTIVE Config

`policyVersion` is required on both.

Semantics:

```text
policyVersion
=
ScenarioConfig/AED fairness semantic contract used to resolve/validate
the configuration
```

It does not prove Adaptive policy executed.

Current logical identity:

```text
AED_SCENARIO_POLICY_V1_1
```

---

# 44. Candidate Construction

All candidate construction uses one exact logical base reference.

Conceptual identity:

```text
ScenarioConfigBaseRef
{
    baseConfigId
    baseScenarioConfigVersion
    baseContentFingerprint
    baseKind:
        PRE_MATCH_RESOLVED_BASE
        | APPLIED_SCENARIO_CONFIG
}
```

Exact class/serialization/hash binding is implementation-specific. The semantic meaning is not.

Global invariant:

```text
baseScenarioConfigVersion
=
version of the exact authoritative ScenarioConfig base content
from which CandidateScenarioConfig was constructed
```

`baseContentFingerprint` identifies the exact resolved base content. `baseConfigId` identifies the authoritative base source/selection identity.

`fallbackConfigId` is not automatically the same as `baseConfigId`; the PRE_MATCH candidate may be built from the authoritative designer/fixed base selected for the match even when fallback was not invoked.

## 44.1 PRE_MATCH

PRE_MATCH adaptive candidate base:

```text
exact authoritative resolved designer/fixed base ScenarioConfig
selected for the current match setup
```

This content does **not** have to be a live `AppliedScenarioConfig` before the adaptive candidate is built.

Flow:

```text
authoritative PRE_MATCH base resolver
→ resolve exact base ScenarioConfig content
→ capture ScenarioConfigBaseRef:
     baseConfigId
     baseScenarioConfigVersion
     baseContentFingerprint
→ copy exact base content
→ apply only selected authorized v1.1 delta
→ CandidateScenarioConfig
```

Adaptive policy cannot modify mapId/monsterType/routeModifier at PRE_MATCH.

Before commit, the authoritative PRE_MATCH base resolver must still resolve the same captured identity/revision/content:

```text
current baseConfigId == captured baseConfigId
AND
current baseScenarioConfigVersion == captured baseScenarioConfigVersion
AND
current baseContentFingerprint == captured baseContentFingerprint
```

If any comparison fails:

```text
→ CandidateValidationStatus = INVALID
→ reasonCode = STALE_BASE_CONFIG
→ candidate cannot apply
→ PRE_MATCH result = FIXED_FALLBACK
→ fallbackAction = FULL_FIXED_CONFIG
```

The old candidate is never silently rebased.

If the decision window is still open, a later independent attempt may rebuild from:

- the current authoritative PRE_MATCH base;
- a currently valid snapshot;
- a new decision identity.

## 44.2 MID-MATCH

MID_MATCH candidate base:

```text
current last valid AppliedScenarioConfig
```

Flow:

```text
current last valid AppliedScenarioConfig
→ capture ScenarioConfigBaseRef:
     baseConfigId
     baseScenarioConfigVersion
     baseContentFingerprint
     baseKind = APPLIED_SCENARIO_CONFIG
→ copy current content
→ apply only selected current decision delta
→ CandidateScenarioConfig
```

Immediately before apply:

```text
current AppliedScenarioConfigVersion
==
captured baseScenarioConfigVersion
```

and the authoritative current applied-content fingerprint must still match the captured `baseContentFingerprint`.

If not:

```text
→ CandidateValidationStatus = INVALID
→ reasonCode = STALE_BASE_CONFIG
→ no overwrite
→ result = FIXED_FALLBACK
→ fallbackAction = KEEP_LAST_VALID_CONFIG
```

Never rebuild a mid-match candidate from the original pre-match baseline, because that could erase prior valid decisions.

Current policy v1.1 generates zero mid-match deltas, but these builder/validator semantics remain frozen.

---

# 45. Scenario Validator

ScenarioValidator is deterministic for the same explicit inputs and current authoritative revision identities.

The lifecycle has two distinct stages.

## 45.1 Pre-candidate prerequisites

These must pass **before** `CandidateScenarioConfig` is built:

1. supported `policyVersion`;
2. canonical `AEDPolicyDefinition` resolvable;
3. `AEDPolicyConfig` structurally valid and semantic mirrors, if serialized, match the canonical PolicyDefinition exactly;
4. referenced `AEDEvidencePolicy` valid;
5. snapshot/input gate eligible;
6. supported `parameterRegistryVersion`;
7. policy-active registry entries complete and valid;
8. required content whitelist/registry versions resolvable;
9. authoritative `ScenarioConfigBaseRef` resolvable.

Failure at this stage means:

```text
CandidateValidationStatus = NOT_EVALUATED
result = FIXED_FALLBACK
candidate MUST NOT be built
```

Reason is exact to the failure category, including:

```text
POLICY_CONFIG_INVALID
PARAMETER_REGISTRY_INVALID
INPUT_INCOMPLETE
INPUT_INVALID
UNSUPPORTED_VERSION
```

## 45.2 Candidate validation

After a candidate exists, ScenarioValidator must validate:

1. ScenarioConfig schema.
2. resolution mode/decision-point consistency.
3. AdaptiveInputSnapshot linkage when ADAPTIVE policy ran.
4. snapshot current-revision evidence.
5. selected rule ID is canonical for current `policyVersion`.
6. requested key belongs to adaptive authority.
7. requested key is canonical policy-active under current `policyVersion`.
8. requestedAfter obeys registered candidate-value/adjustment semantics.
9. numerical bounds.
10. allowed timing.
11. pressureAxis metadata/direction.
12. policy max changed keys.
13. inherited fairness max aggressive axes.
14. double Detection prohibition.
15. map/monster non-adaptive invariants.
16. objective spawn whitelist/safety.
17. support-budget semantics.
18. routeModifier timing.
19. route whitelist.
20. route/FacilityGraph safety using current effective state.
21. Final Hunt timer bound.
22. Final Hunt timer-not-started rule.
23. content whitelist/version.
24. captured `ScenarioConfigBaseRef` consistency.
25. current decision window.
26. fallback/result/status consistency.
27. NO_CHANGE zero-delta semantics.
28. forbidden fields/data.
29. no hidden Player location.
30. no direct Monster command.

Candidate-stage failure means:

```text
CandidateValidationStatus = INVALID
result = FIXED_FALLBACK
candidate MUST NOT apply
```

No silent repair.

---

# 46. Policy-Active Key Validation

A key can be authority-valid but policy-invalid.

Example:

```text
routeModifier
+ ALLOWED_PHASE_BOUNDARY
→ authority/timing may be valid

but
policyVersion = AED_SCENARIO_POLICY_V1_1
→ routeModifier POLICY_NOT_SELECTED
→ current policy candidate containing it is invalid
```

Reason category:

```text
POLICY_KEY_NOT_ACTIVE
```

This prevents the current rule engine from using dormant authority without a policy revision.

---

# 47. Whole-Candidate Atomicity

```text
change A valid
change B invalid
→ reject entire candidate
→ apply neither
```

No partial apply.

No partial `scenarioConfigVersion`.

No client observes half-updated config.

---

# 48. `NO_CHANGE`

Canonical:

```text
policy evaluated successfully
+ selected rule produces zero legal delta
→ CandidateValidationStatus = VALID
→ result = NO_CHANGE
→ requestedChanges = []
→ fallbackAction = NONE
→ current AppliedScenarioConfig unchanged
→ scenarioConfigVersion unchanged
→ configSource unchanged
```

Valid zero-delta causes include:

```text
HOLD rule
VALUE_LIMIT_REACHED
KEY_NOT_APPLICABLE
```

These may be stored as controlled policy-detail reason/provenance; final `AdaptiveDecision.reasonCode` remains `ADAPTIVE_NO_CHANGE`.

NO_CHANGE does not invoke FixedDirector.

---

# 49. FixedDirector

FixedDirector is a deterministic safe non-adaptive path, not Monster AI.

## 49.1 PRE_MATCH

```text
FULL_FIXED_CONFIG
→ resolve FIXED_BASELINE_V1
→ validate
→ apply full fixed config
```

## 49.2 ALLOWED_PHASE_BOUNDARY

```text
KEEP_LAST_VALID_CONFIG
→ no replacement
```

## 49.3 FINAL_HUNT_SETUP

```text
KEEP_LAST_VALID_CONFIG
→ retain already valid configured timer value
→ no unrelated replacement
```

---

# 50. `FIXED_BASELINE_V1`

Logical identity retained:

```text
FIXED_BASELINE_V1
```

Required properties:

- designer-authored;
- known-safe;
- versioned;
- compatible with map/content whitelist;
- valid under current ScenarioConfig/Fairness contract;
- deterministic.

Required resolvable metadata:

```text
fallbackConfigId
fallbackConfigVersion
scenarioConfigVersion
policyVersion
contentWhitelistVersion
```

If full PRE_MATCH fallback is required but the baseline is missing/unsupported/invalid:

```text
FATAL CONFIGURATION ERROR
```

AED does not invent a replacement.

---

# 51. Fallback Mapping

| Situation | CandidateValidationStatus | AdaptiveDecision result | fallbackAction | Config mutation |
|---|---|---|---|---|
| Valid adaptive delta applied | VALID | APPLIED | NONE | atomic new config |
| Valid evaluation, zero delta | VALID | NO_CHANGE | NONE | none |
| PRE_MATCH input ineligible/invalid before candidate construction | NOT_EVALUATED | FIXED_FALLBACK | FULL_FIXED_CONFIG | valid fixed config may replace |
| PRE_MATCH `AEDPolicyConfig` invalid before candidate construction | NOT_EVALUATED | FIXED_FALLBACK | FULL_FIXED_CONFIG | valid fixed config may replace |
| PRE_MATCH `AEDEvidencePolicy` invalid before candidate construction | NOT_EVALUATED | FIXED_FALLBACK | FULL_FIXED_CONFIG | valid fixed config may replace |
| PRE_MATCH parameter registry invalid before candidate construction | NOT_EVALUATED | FIXED_FALLBACK | FULL_FIXED_CONFIG | valid fixed config may replace |
| PRE_MATCH candidate exists but is invalid | INVALID | FIXED_FALLBACK | FULL_FIXED_CONFIG | valid fixed config may replace |
| ALLOWED_PHASE_BOUNDARY input invalid/ineligible before candidate construction | NOT_EVALUATED | FIXED_FALLBACK | KEEP_LAST_VALID_CONFIG | none |
| ALLOWED_PHASE_BOUNDARY `AEDPolicyConfig` or `AEDEvidencePolicy` invalid before candidate construction | NOT_EVALUATED | FIXED_FALLBACK | KEEP_LAST_VALID_CONFIG | none |
| ALLOWED_PHASE_BOUNDARY parameter registry invalid before candidate construction | NOT_EVALUATED | FIXED_FALLBACK | KEEP_LAST_VALID_CONFIG | none |
| ALLOWED_PHASE_BOUNDARY candidate exists but is invalid | INVALID | FIXED_FALLBACK | KEEP_LAST_VALID_CONFIG | none |
| FINAL_HUNT_SETUP input invalid/ineligible before candidate construction | NOT_EVALUATED | FIXED_FALLBACK | KEEP_LAST_VALID_CONFIG | none |
| FINAL_HUNT_SETUP `AEDPolicyConfig` or `AEDEvidencePolicy` invalid before candidate construction | NOT_EVALUATED | FIXED_FALLBACK | KEEP_LAST_VALID_CONFIG | none |
| FINAL_HUNT_SETUP parameter registry invalid before candidate construction | NOT_EVALUATED | FIXED_FALLBACK | KEEP_LAST_VALID_CONFIG | none |
| FINAL_HUNT_SETUP candidate exists but is invalid | INVALID | FIXED_FALLBACK | KEEP_LAST_VALID_CONFIG | none |

Lifecycle distinction:

```text
no CandidateScenarioConfig was constructed
→ CandidateValidationStatus = NOT_EVALUATED
```

```text
CandidateScenarioConfig exists and fails candidate/precommit validation
→ CandidateValidationStatus = INVALID
```

FIXED requested mode goes directly to FixedDirector and is not misclassified as an Adaptive failure.

---

# 52. AdaptiveDecision v1.1

Logical record:

```text
AdaptiveDecision
{
    decisionId
    targetMatchId
    decisionPoint
    phaseContext?
    resolutionMode = ADAPTIVE

    inputStatus
    snapshotId
    snapshotContentFingerprint
    sourceProfileRevisions[]
    rosterIdentity

    selectedPolicyRuleId?
    adaptationIntent?
    policyNoChangeReason?

    requestedChanges[]

    candidateValidationStatus
    resolvedBefore
    resolvedAfter

    result
    reasonCode
    fallbackAction

    baseConfigId
    baseScenarioConfigVersion
    baseContentFingerprint
    baseKind

    scenarioConfigVersion

    policyVersion
    policyConfigVersion
    evidencePolicyVersion
    parameterRegistryVersion
    contentWhitelistVersion
    fallbackConfigId
    fallbackConfigVersion

    experimentConditionRef?
    staleInputDetected
    staleBaseConfigDetected
}
```

Base fields are the persisted projection of the exact `ScenarioConfigBaseRef` from §44.

`baseScenarioConfigVersion` has one meaning at all decision points:

```text
version of the exact authoritative ScenarioConfig base content
from which CandidateScenarioConfig was constructed
```

At PRE_MATCH that base may be resolved designer/fixed base content that has not yet become `AppliedScenarioConfig`. At MID_MATCH it is the current last valid `AppliedScenarioConfig`.

Do not copy full Profile data if immutable snapshot reference + fingerprint + retained source snapshots are sufficient.

---

# 53. Requested Change Item

```text
AdaptiveRequestedChange
{
    key
    before
    requestedAfter
    ruleId
    adjustmentRuleId
}
```

Invalid requested values may remain audit evidence in the decision record, but may not appear in `resolvedAfter` as applied.

---

# 54. Reason Codes

Core controlled final reason codes:

```text
ADAPTIVE_APPLIED
ADAPTIVE_NO_CHANGE
FIXED_FALLBACK

INPUT_INCOMPLETE
INPUT_INVALID
STALE_INPUT

AED_UNAVAILABLE
AED_TIMEOUT

POLICY_CONFIG_INVALID
POLICY_KEY_NOT_ACTIVE
PARAMETER_REGISTRY_INVALID
REGISTERED_VALUE_REJECTED

BOUND_REJECTED
TIMING_REJECTED
PRESSURE_RULE_REJECTED
SCENARIO_INVALID
ROUTE_INVALID
SPAWN_INVALID
UNSUPPORTED_VERSION

STALE_BASE_CONFIG
DECISION_WINDOW_CLOSED
DECISION_IDENTITY_CONFLICT

FALLBACK_CONFIG_INVALID
```

`POLICY_CONFIG_INVALID` includes invalid/missing `AEDPolicyConfig` and invalid/missing referenced `AEDEvidencePolicy` configuration that prevents policy evaluation.

`PARAMETER_REGISTRY_INVALID` is reserved for a parameter-registry/config integrity failure detected before candidate construction.

`REGISTERED_VALUE_REJECTED` is narrowly scoped to a candidate-stage requested/constructed numerical value that does not belong to the legal registered candidate output set for the active adjustment rule after the registry itself has already passed pre-build validation.

Profile source reasons may be referenced separately rather than copied into this enum.

No free-text authoritative reason replaces the controlled code.

---

# 55. Decision Identity / Exactly-Once

Stable logical:

```text
decisionId
```

Invariant:

```text
same decisionId
+ same semantic decision fingerprint
→ idempotent same committed result
```

```text
same decisionId
+ different semantic decision fingerprint
→ DECISION_IDENTITY_CONFLICT
→ no overwrite
→ no apply
```

An APPLIED decision creates at most one new ScenarioConfig version.

Retry after acknowledgement loss cannot create a second config.

---

# 56. Decision Semantic Fingerprint

At minimum derived from:

```text
targetMatchId
decisionPoint / phaseContext
resolutionMode
snapshotContentFingerprint

baseConfigId
baseScenarioConfigVersion
baseContentFingerprint
baseKind

selectedPolicyRuleId
requestedChanges

policyVersion
policyConfigVersion
evidencePolicyVersion
parameterRegistryVersion
contentWhitelistVersion
fallbackConfigVersion

relevant route/content revision identities
```

The fingerprint binds the decision to the exact base content used for construction; a PRE_MATCH resolved base and a MID_MATCH applied base cannot collide merely because they share a numeric version token.

Exact hash algorithm is an implementation binding.

---

# 57. Snapshot / Profile Staleness

Between snapshot creation and commit, validate:

```text
rosterIdentity still current
sourceProfileRevisions still current
source Profile validity still current
snapshot targetMatchId still current
```

If Profile contribution retraction/replay produced a newer revision:

```text
old snapshot
→ historical immutable artifact
→ stale for future decision
→ STALE_INPUT
→ no apply
```

No in-place snapshot mutation.

No gameplay rewind for an already-consumed historical snapshot.

---

# 58. Base ScenarioConfig CAS / Staleness

The stale-base rule compares the candidate against the exact authoritative base from which it was constructed.

Canonical base reference:

```text
ScenarioConfigBaseRef
{
    baseConfigId
    baseScenarioConfigVersion
    baseContentFingerprint
    baseKind
}
```

## 58.1 PRE_MATCH CAS

PRE_MATCH does not require a previously live `AppliedScenarioConfig`.

Candidate base:

```text
exact authoritative resolved designer/fixed base ScenarioConfig
for the current match setup
```

Captured at build time:

```text
baseConfigId
baseScenarioConfigVersion
baseContentFingerprint
baseKind = PRE_MATCH_RESOLVED_BASE
```

Immediately before commit, re-resolve the current authoritative PRE_MATCH base identity/revision/content and require:

```text
current baseConfigId == captured baseConfigId
AND
current baseScenarioConfigVersion == captured baseScenarioConfigVersion
AND
current baseContentFingerprint == captured baseContentFingerprint
```

Mismatch:

```text
→ CandidateValidationStatus = INVALID
→ result = FIXED_FALLBACK
→ reasonCode = STALE_BASE_CONFIG
→ fallbackAction = FULL_FIXED_CONFIG
→ old candidate MUST NOT apply
→ old candidate MUST NOT be silently rebased
```

If the PRE_MATCH decision window is still open, a new attempt may be created from the current base and current snapshot under a new decision identity.

## 58.2 MID_MATCH CAS

MID_MATCH base is:

```text
current last valid AppliedScenarioConfig
```

Captured:

```text
baseScenarioConfigVersion = V
baseContentFingerprint = F
baseKind = APPLIED_SCENARIO_CONFIG
```

Immediately before apply:

```text
current AppliedScenarioConfigVersion == V
AND
current AppliedScenarioConfig content fingerprint == F
```

Mismatch:

```text
→ CandidateValidationStatus = INVALID
→ result = FIXED_FALLBACK
→ reasonCode = STALE_BASE_CONFIG
→ fallbackAction = KEEP_LAST_VALID_CONFIG
→ no overwrite
```

The stale candidate itself is never reused.

---

# 59. Decision-Window Revalidation

Immediately before commit:

```text
PRE_MATCH
→ match still not started

ALLOWED_PHASE_BOUNDARY
→ current authoritative boundary still open

FINAL_HUNT_SETUP
→ still before Escape Door timer start
```

If the applicable decision window is no longer open at precommit revalidation:

```text
→ CandidateValidationStatus = INVALID
→ reasonCode = DECISION_WINDOW_CLOSED
→ no apply
→ result = FIXED_FALLBACK
```

Fallback is exact:

```text
PRE_MATCH
→ FULL_FIXED_CONFIG

ALLOWED_PHASE_BOUNDARY
→ KEEP_LAST_VALID_CONFIG

FINAL_HUNT_SETUP
→ KEEP_LAST_VALID_CONFIG
```

`TIMING_REJECTED` remains for a candidate that requests a field/key at a disallowed timing under its contract, such as PRE_MATCH `routeModifier` or a Final Hunt timer mutation after the timer has started. `DECISION_WINDOW_CLOSED` is reserved for a decision whose previously valid commit window closed before authoritative commit.

---

# 60. Decision Serialization / Concurrency

Per match:

```text
one authoritative ScenarioConfig mutation commit at a time
```

Permitted implementation mechanisms:

- Host serialized controller;
- transaction;
- optimistic CAS;
- decision queue;
- equivalent mechanism that preserves the frozen semantics.

Required result:

```text
worker/network completion order
X→ changes final authoritative config
```

A remote/back-end decision result remains a candidate until current Host validation/commit succeeds.

Before commit the serialized/transactional boundary must revalidate:

```text
current snapshot/profile revision identity
current decision window
current ScenarioConfigBaseRef
```

For PRE_MATCH, the CAS target is the authoritative resolved PRE_MATCH base identity/revision/content from §58.1; it is not an assumed `AppliedScenarioConfig`.

For MID_MATCH, the CAS target is the current last valid `AppliedScenarioConfig` version/fingerprint from §58.2.

---

# 61. Host / Photon Fusion Authority

Project topology remains Host Mode.

Authority:

```text
Host / Fusion State Authority
→ owns authoritative AppliedScenarioConfig mutation
```

Non-authoritative clients:

- receive durable applied state needed for gameplay/presentation;
- do not run authoritative policy selection;
- do not validate as final authority;
- do not change parameter values;
- do not alter routeModifier/timer;
- do not apply backend candidate directly.

If an RPC is used for a request/notification, it cannot be the sole durable state.

---

# 62. Network Replication / Late Join

Replicate only required durable applied state, for example semantic categories:

```text
scenarioConfigVersion
configSource
current gameplay-relevant resolved config values
```

Exact `[Networked]` fields and NetworkObject binding are IMPLEMENTATION BINDING TBD.

Do not replicate by default:

- AdaptiveInputSnapshot;
- PlayerAIProfiles;
- score bands;
- policy rule candidates;
- full AdaptiveDecision history;
- content whitelist;
- Warden FacilityGraph;
- hidden policy internals.

Late join:

```text
→ reconstruct current AppliedScenarioConfig
→ do not replay historical adaptive candidate/decision
→ do not reapply old config mutation
```

---

# 63. Route / Config Apply Ordering

For a route-affecting ScenarioConfig mutation if a future policy version selects it:

```text
current snapshot/config validation
→ CandidateScenarioConfig build
→ Scenario route safety using current effective route state
→ final precommit validation
→ atomic ScenarioConfig apply
→ publish config/graph revision
→ Warden independently revalidates its own active lock
```

No DoorId command is sent from AED to Warden.

---

# 64. Policy Configuration

`AEDPolicyConfig` contains tuning/dependency references only. It does not own policy semantics or evidence thresholds.

Logical:

```text
AEDPolicyConfig
{
    policySemanticId = AED_SCENARIO_POLICY_V1_1
    policyConfigVersion

    evidencePolicyVersion

    scoreBandThresholds {
        SURVIVAL { lowThreshold, highThreshold }
        NOISE { lowThreshold, highThreshold }
    }

    parameterRegistryVersion
    contentWhitelistVersion
}
```

`minimumSampleCountPerObservedPlayer` is intentionally absent. It is owned only by `AEDEvidencePolicy`.

The following are also intentionally absent as tuning authority:

```text
activeRuleIds[]
rulePriorities{}
activePolicyKeys[]
strategyBindings{}
```

Their authoritative values are frozen in `AEDPolicyDefinition` §20.1.

If an implementation serializes those fields as mirrors, §20.2 applies: they must exactly match the canonical definition or the config is invalid.

Score-band thresholds are the only policy-evaluation tuning values owned by `policyConfigVersion` in v1.1.

No hysteresis/cooldown fields are active in v1.1.

Missing/inconsistent required policy config:

```text
→ CandidateValidationStatus = NOT_EVALUATED
→ reasonCode = POLICY_CONFIG_INVALID
→ policy MUST NOT run
→ fallback by decision point
```

---

# 65. Version Ownership

Canonical ownership:

| Semantic/configuration change | Version owner |
|---|---|
| policy rule topology / canonical priority / predicates / AdaptationIntent meaning or mapping / active policy keys / strategy bindings | `policyVersion` |
| score-band numerical thresholds only | `policyConfigVersion` |
| evidence topology / full-roster requirement / `minimumSampleCountPerObservedPlayer` / allowed snapshot validity / current-match evidence metric or finality allowance | `evidencePolicyVersion` |
| parameter bounds / defaults / `candidateValues` / pressure-axis metadata / adjustment metadata | `parameterRegistryVersion` |
| spawn/route content whitelist | `contentWhitelistVersion` |
| fixed baseline content | `fallbackConfigVersion` |
| resolved applied ScenarioConfig content | `scenarioConfigVersion` |
| Profile score/comparison semantics | Profile-owned versions |
| raw event wire | Telemetry `schemaVersion` |

Additional rule for `requiredPlayerDimensions`:

```text
requiredPlayerDimensions change
→ evidencePolicyVersion changes

if that change alters the semantic inputs/predicates of AED_SCENARIO_POLICY_V1_1
→ policyVersion changes too
```

Cross-version dependency rule:

```text
one product change
may require multiple coordinated version bumps
when it crosses multiple ownership domains
```

Examples:

```text
sample threshold only changes
→ evidencePolicyVersion changes
→ policyConfigVersion does not change for that reason

score-band threshold only changes
→ policyConfigVersion changes
→ policyVersion may remain AED_SCENARIO_POLICY_V1_1

rule priority changes
→ policyVersion changes
→ policyConfigVersion alone is insufficient

candidateValues change
→ parameterRegistryVersion changes
```

Document revision is not a substitute for runtime semantic versions.

---

# 66. ScenarioConfig Versioning

```text
APPLIED
+ actual content change
→ new scenarioConfigVersion
```

```text
NO_CHANGE
→ unchanged scenarioConfigVersion
```

```text
mid-match KEEP_LAST_VALID_CONFIG
→ unchanged scenarioConfigVersion
```

Rejected/invalid candidates never create applied versions.

---

# 67. Deterministic Reproducibility

Frozen rule:

```text
same AdaptiveInputSnapshot semantic content
+ same current roster/source revisions
+ same ScenarioResolutionMode
+ same decision point / phase context

+ same ScenarioConfigBaseRef:
    baseConfigId
    baseScenarioConfigVersion
    baseContentFingerprint
    baseKind

+ same policyVersion
+ same canonical AEDPolicyDefinition for that policyVersion
+ same AEDPolicyConfig content/version
+ same AEDEvidencePolicy content/version
+ same parameter registry content/version
+ same content whitelist content/version
+ same route/content revision identities
+ same fallback registry content/version

→ same input-gate result
→ same score bands
→ same PolicyRuleId
→ same AdaptationIntent
→ same requestedChanges
→ same CandidateValidationStatus
→ same result/reason/fallback
→ same resulting AppliedScenarioConfig content
```

A sample-threshold change is therefore distinguishable through `evidencePolicyVersion`, even when `policyConfigVersion` is unchanged.

A score-band tuning change is distinguishable through `policyConfigVersion` without redefining the canonical rule semantics.

No unseeded randomness.

---

# 68. Decision Evidence Persistence

Retain enough immutable evidence for thesis/research reproduction:

- decisionId;
- target match;
- experiment condition reference if active;
- resolution mode;
- snapshotId;
- snapshot semantic fingerprint;
- source profile revisions;
- decision point/phase;
- `baseConfigId`;
- `baseScenarioConfigVersion`;
- `baseContentFingerprint`;
- `baseKind`;
- score-band results;
- selected PolicyRuleId;
- AdaptationIntent;
- requested changes;
- candidate validation result;
- final result/reason/fallback;
- resulting scenarioConfigVersion;
- `policyVersion`;
- `policyConfigVersion`;
- `evidencePolicyVersion`;
- `parameterRegistryVersion`;
- `contentWhitelistVersion`;
- `fallbackConfigId`;
- `fallbackConfigVersion`;
- stale-state diagnostics;
- relevant route/content revision IDs if consumed.

Exact threshold/value payloads need not be duplicated in each decision record if the referenced immutable versioned artifacts are durably resolvable for replay. If versioned artifacts are not durably resolvable, the implementation must persist their resolved fingerprints/content references so reproduction remains possible.

Exact DB/table technology is implementation-bound.

---

# 69. Telemetry Boundary

Do not add a new production Telemetry event in this contract.

Telemetry v1.1 already records occurrence-time ScenarioConfig provenance on gameplay facts.

`AdaptiveDecision` / `ScenarioResolutionRecord` may be a separate configuration/research artifact.

If a future design requires a new wire Telemetry event:

```text
TELEMETRY CONTRACT REVISION REQUIRED
```

AED does not write a telemetry event and then read that event back as gameplay input.

---

# 70. Experiment Readiness

Four distinct states:

```text
AED contract complete
AED implementation complete
Adaptive runtime activation allowed
Fixed-vs-Adaptive experiment READY
```

They are not equivalent.

Current state:

```text
AED v1.1 contract:
BASELINED

Profile-side SC-03:
RESOLVED

AED input lifecycle:
RESOLVED BY THIS CONTRACT

AED implementation:
NOT EVIDENCED / NOT COMPLETE

policy tuning:
NOT FROZEN

M1-020:
v1.1 REVISION REQUIRED before experiment execution

Live Fixed-vs-Adaptive experiment:
NOT READY
```

Why M1-020 needs revision:

```text
M1-020 v0 gate
=
TeamPerformance COMPLETE/non-null

AED_SCENARIO_POLICY_V1_1 gate
=
AdaptiveInputSnapshot
+ full-roster SURVIVAL/NOISE evidence
+ versioned evidence policy
```

The experiment contract must be updated explicitly; this document does not silently overwrite it.

---

# 71. Fixed Condition Independence

A valid Fixed path must work even when:

- Profile is missing;
- snapshot is PARTIAL/INVALID;
- AED service is unavailable;
- policy config is invalid;
- tuning is not frozen.

At PRE_MATCH a valid `FIXED_BASELINE_V1` allows the game to resolve a fixed scenario without Adaptive.

Adaptive failure must not make Fixed gameplay impossible when a valid fixed template exists.

---

# 72. Current Implementation Assessment

```text
CURRENT IMPLEMENTATION:
NOT EVIDENCED / NOT IMPLEMENTED FROM SUPPLIED SOURCE
```

No supplied current source proves concrete implementations of the AED policy, input gate, ScenarioValidator, FixedDirector integration, decision ledger, Host apply binding, or Profile/AED backend service.

| Target module | Current evidence | Action | Target responsibility | Risk |
|---|---|---|---|---|
| ScenarioResolutionModeResolver | not evidenced | ADD | FIXED vs ADAPTIVE request resolution | High if conflated with configSource |
| AEDInputGate | not evidenced | ADD | snapshot/version/evidence eligibility | High |
| AEDEvidencePolicy validator | not evidenced | ADD | sole owner/validator of evidence topology and sample thresholds | High |
| AEDPolicyDefinition provider/validator | not evidenced | ADD | canonical rule IDs/priorities/predicates/intents/active keys/strategy bindings for `AED_SCENARIO_POLICY_V1_1` | Critical if made tuning-owned |
| AEDPolicyConfig validator | not evidenced | ADD | score-threshold tuning + dependency references; reject semantic mirror mismatch | High |
| AEDPolicyEvaluator | not evidenced | ADD | deterministic evaluation of canonical PolicyDefinition | High |
| AdaptiveParameterRegistry | predecessor contract only | ADD/BIND | bounds/candidateValues/axes/adjustment metadata; pre-build integrity gate | High |
| AdaptiveCandidateBuilder | not evidenced | ADD | exact base ref + legal deltas | High |
| ScenarioConfigBaseRef resolver | not evidenced | ADD | PRE_MATCH resolved-base and MID_MATCH applied-base identity/version/fingerprint | Critical |
| ScenarioValidator | predecessor design only | ADD | pure candidate legality/fairness/safety | Critical |
| ScenarioRouteSafetyAdapter | Warden/FacilityGraph design exists | ADD/BIND | current effective route safety | Critical |
| FixedDirector | predecessor design only | ADD | deterministic fixed/fallback resolution | Critical |
| ScenarioConfigApplyController | not evidenced | ADD | Host atomic apply + exact PRE_MATCH/MID_MATCH CAS | Critical |
| AdaptiveDecisionLedger | not evidenced | ADD | idempotency/audit | High |
| AEDDebugProvider | not evidenced | ADD | read-only observability | Medium |

Avoid one monolithic `AdaptiveDifficultyManager` owning every concern.

---

# 73. Observability

Read-only logical:

```text
AEDDebugSnapshot
{
    resolutionMode
    decisionPoint
    targetMatchId

    snapshotId?
    snapshotContentFingerprint?
    inputGateStatus
    inputReasons[]

    survivalObservedMean?
    survivalBand?
    noiseObservedMean?
    noiseBand?

    policyVersion
    policyConfigVersion
    evidencePolicyVersion
    selectedRuleId?
    adaptationIntent?
    policyNoChangeReason?

    baseConfigId
    baseScenarioConfigVersion
    baseContentFingerprint
    baseKind

    requestedChanges[]
    candidateValidationStatus

    result?
    reasonCode?
    fallbackAction?

    resultingScenarioConfigVersion?

    parameterRegistryVersion
    contentWhitelistVersion
    fallbackConfigVersion

    staleInputDetected
    staleBaseConfigDetected
    decisionWindowValid

    hasStateAuthority
}
```

Debug output reports version ownership; it does not redefine it.

In particular:

```text
sample thresholds
→ resolve from evidencePolicyVersion

score-band thresholds
→ resolve from policyConfigVersion

rule semantics / priorities / active keys
→ resolve from policyVersion / canonical AEDPolicyDefinition
```

Debug UI cannot mutate policy, config, Profile, or gameplay.

---

# 74. Performance Contract

No arbitrary ms/Hz budget is frozen.

Principles:

- no per-frame AED evaluation;
- no per-second policy polling;
- explicit decision points only;
- finite rule table;
- finite active-key set;
- finite candidateValues arrays;
- bounded route/spawn validation;
- no raw telemetry scan at decision time;
- no full Profile history scan at decision time;
- snapshot already contains processed evidence;
- cache immutable registries by version;
- profiler-derived budgets later.

---

# 75. Pure Policy / Validator Test Matrix

These are required tests, not pass claims.

| ID | Case | Expected |
|---|---|---|
| AED-E-001 | FIXED mode | Adaptive policy not invoked |
| AED-E-002 | ADAPTIVE mode | enters input gate |
| AED-E-003 | VALID snapshot + sufficient evidence | gate ELIGIBLE |
| AED-E-004 | PARTIAL snapshot | gate INELIGIBLE |
| AED-E-005 | INVALID snapshot | gate INVALID |
| AED-E-006 | COLD_START value 50 | not observed evidence |
| AED-E-007 | DEFERRED dimension | not numeric input |
| AED-E-008 | profile missing | ineligible |
| AED-E-009 | incompatible comparison key | invalid |
| AED-E-010 | stale profile revision | invalid/stale |
| AED-E-011 | roster changed | invalid/stale |
| AED-E-012 | unsupported Profile semantic version | invalid |
| AED-E-013 | missing sample threshold in referenced AEDEvidencePolicy | NOT_EVALUATED + POLICY_CONFIG_INVALID |
| AED-E-014 | sample threshold < 1 | NOT_EVALUATED + POLICY_CONFIG_INVALID |
| AED-E-015 | missing band threshold | NOT_EVALUATED + POLICY_CONFIG_INVALID |
| AED-E-016 | lowThreshold >= highThreshold | NOT_EVALUATED + POLICY_CONFIG_INVALID |
| AED-E-017 | score == lowThreshold | MID |
| AED-E-018 | score == highThreshold | HIGH |
| AED-E-019 | survival LOW | rule PRE-010 |
| AED-E-020 | survival MID + noise LOW | rule PRE-020 |
| AED-E-021 | survival HIGH + noise HIGH + Stalker | rule PRE-030 |
| AED-E-022 | mixed non-low/non-both-high | rule PRE-040 |
| AED-E-023 | same semantic input twice | same rule and changes |
| AED-E-024 | serialized priority changed while policyVersion remains AED_SCENARIO_POLICY_V1_1 | NOT_EVALUATED + POLICY_CONFIG_INVALID |
| AED-E-025 | dictionary/serialization iteration order changed | no output change |
| AED-E-026 | RELIEVE with next support candidate | one registered increase |
| AED-E-027 | RELIEVE at support max | NO_CHANGE |
| AED-E-028 | INCREASE with Stalker + next Chase candidate | one registered increase |
| AED-E-029 | INCREASE with non-Stalker | NO_CHANGE / key not applicable |
| AED-E-030 | requestedAfter within broad bounds but not in legal candidateValues | INVALID + REGISTERED_VALUE_REJECTED |
| AED-E-031 | candidate outside bounds | INVALID + BOUND_REJECTED |
| AED-E-032 | active-key registry entry/base compatibility malformed before build | NOT_EVALUATED + PARAMETER_REGISTRY_INVALID |
| AED-E-033 | inactive DetectionFillRate candidate under v1.1 | INVALID + POLICY_KEY_NOT_ACTIVE |
| AED-E-034 | inactive routeModifier candidate under v1.1 | INVALID + POLICY_KEY_NOT_ACTIVE |
| AED-E-035 | unknown adaptive key | INVALID |
| AED-E-036 | VisionDistance candidate | INVALID |
| AED-E-037 | AttackRange candidate | INVALID |
| AED-E-038 | StalkerDamagePercent candidate | INVALID |
| AED-E-039 | routeModifier PRE_MATCH | INVALID + TIMING_REJECTED |
| AED-E-040 | Final Hunt timer 45 | bound-valid |
| AED-E-041 | Final Hunt timer 60 | bound-valid |
| AED-E-042 | Final Hunt timer below 45 | INVALID + BOUND_REJECTED |
| AED-E-043 | Final Hunt timer above 60 | INVALID + BOUND_REJECTED |
| AED-E-044 | timer changed after start | INVALID + TIMING_REJECTED |
| AED-E-045 | DetectionFillRate up + DetectionDecayRate down | INVALID + PRESSURE_RULE_REJECTED |
| AED-E-046 | ChaseSpeed up + SearchDuration up | INVALID + PRESSURE_RULE_REJECTED |
| AED-E-047 | two aggressive axes | INVALID + PRESSURE_RULE_REJECTED |
| AED-E-048 | current policy outputs two changed keys | INVALID current-policy output |
| AED-E-049 | support budget increase | non-retroactive semantics |
| AED-E-050 | zero delta | VALID + NO_CHANGE |
| AED-E-051 | NO_CHANGE | no version/configSource change |
| AED-E-052 | one valid + one invalid requested change | whole candidate rejected |
| AED-E-053 | same decisionId same fingerprint | duplicate no-op |
| AED-E-054 | same decisionId different fingerprint | DECISION_IDENTITY_CONFLICT |
| AED-E-055 | PRE_MATCH authoritative base ref changes after candidate build | INVALID + STALE_BASE_CONFIG + FULL_FIXED_CONFIG |
| AED-E-056 | closed decision window | INVALID + no apply |
| AED-E-057 | profile retraction creates newer revision | old snapshot stale |
| AED-E-058 | phase-boundary valid input | AED-BND-100-HOLD → NO_CHANGE |
| AED-E-059 | final-hunt valid input | AED-FH-110-HOLD → NO_CHANGE |
| AED-E-060 | currentMatchProcessedEvidence varies | v1.1 policy output unchanged |
| AED-E-061 | sample threshold changes with new evidencePolicyVersion while policyConfigVersion is unchanged | legal evidence-config revision; replay distinguished by evidencePolicyVersion |
| AED-E-062 | sample threshold content changes while evidencePolicyVersion is unchanged | NOT_EVALUATED + POLICY_CONFIG_INVALID |
| AED-E-063 | duplicate conflicting sample threshold appears in AEDPolicyConfig and AEDEvidencePolicy | NOT_EVALUATED + POLICY_CONFIG_INVALID; no duplicate owner accepted |
| AED-E-064 | extra RuleId mirrored for AED_SCENARIO_POLICY_V1_1 | NOT_EVALUATED + POLICY_CONFIG_INVALID |
| AED-E-065 | canonical RuleId missing from serialized mirror | NOT_EVALUATED + POLICY_CONFIG_INVALID |
| AED-E-066 | activePolicyKeys mirror differs from canonical definition | NOT_EVALUATED + POLICY_CONFIG_INVALID |
| AED-E-067 | strategy binding mirror differs from canonical definition | NOT_EVALUATED + POLICY_CONFIG_INVALID |
| AED-E-068 | semantic predicate/priority/key change supplied under same policyVersion | NOT_EVALUATED + POLICY_CONFIG_INVALID; semantic change requires new/unsupported policyVersion |
| AED-E-069 | score-band threshold-only change with new policyConfigVersion and unchanged canonical PolicyDefinition | legal tuning revision |
| AED-E-070 | parameter registry malformed before build | NOT_EVALUATED + PARAMETER_REGISTRY_INVALID |
| AED-E-071 | valid registry + bound-violating requested candidate | INVALID + BOUND_REJECTED |
| AED-E-072 | valid registry + policy-inactive requested key | INVALID + POLICY_KEY_NOT_ACTIVE |
| AED-E-073 | valid registry + illegal registered value | INVALID + REGISTERED_VALUE_REJECTED |
| AED-E-074 | failure-status taxonomy audit | no slash-separated lifecycle status exists for a deterministic condition |
| AED-E-075 | MID_MATCH AppliedScenarioConfigVersion changes after candidate build | INVALID + STALE_BASE_CONFIG + no overwrite |

---

# 76. Route / Warden Safety Tests

| ID | Case | Expected |
|---|---|---|
| AED-R-001 | AED routeModifier abstraction | no Warden DoorId selected |
| AED-R-002 | candidate + current Warden overlay | route safety uses combined effective state |
| AED-R-003 | candidate soft-locks only because current Warden overlay exists | reject before apply |
| AED-R-004 | candidate makes objective unreachable | ROUTE_INVALID |
| AED-R-005 | candidate makes Exit unreachable when required | ROUTE_INVALID |
| AED-R-006 | candidate leaves no legal route | ROUTE_INVALID |
| AED-R-007 | safe route candidate in isolated validator test | safety result valid |
| AED-R-008 | safe Scenario revision applied | Warden sees normal graph/config revision and independently revalidates |
| AED-R-009 | unsafe candidate | no reliance on Warden fail-safe |
| AED-R-010 | routeModifier at PRE_MATCH | timing reject before route mutation |

---

# 77. Unity / PlayMode Tests

| ID | Case | Expected |
|---|---|---|
| AED-P-001 | PRE_MATCH FIXED mode | FixedDirector full config |
| AED-P-002 | PRE_MATCH valid ADAPTIVE + RELIEVE | validated SupportItemBudget candidate applies |
| AED-P-003 | PRE_MATCH valid ADAPTIVE + high/high Stalker | one ChaseSpeed candidate applies |
| AED-P-004 | PRE_MATCH gate failure | full fixed fallback |
| AED-P-005 | boundary valid ADAPTIVE | NO_CHANGE; no new version |
| AED-P-006 | boundary gate failure | keep current config |
| AED-P-007 | Final Hunt valid ADAPTIVE | HOLD/NO_CHANGE |
| AED-P-008 | Final Hunt gate failure | keep current timer/config |
| AED-P-009 | timer already started | no timer mutation |
| AED-P-010 | whole candidate apply | atomic visible state |
| AED-P-011 | rejected candidate | never visible as AppliedScenarioConfig |
| AED-P-012 | NO_CHANGE | no publication of new config version |
| AED-P-013 | mid-match fallback | Stalker FSM not reset |
| AED-P-014 | mid-match fallback | Player/objective state preserved |
| AED-P-015 | config adapter | only configuration-owned values change |
| AED-P-016 | Listener | no adaptive hearing/state mutation |
| AED-P-017 | Warden | no current DoorId command |
| AED-P-018 | snapshot stale immediately before apply | reject |
| AED-P-019 | PRE_MATCH base revision/fingerprint changes after candidate build | STALE_BASE_CONFIG; candidate rejected; FULL_FIXED_CONFIG fallback |
| AED-P-020 | decision window closes before apply | reject/fallback |
| AED-P-021 | FIXED mode with invalid/missing Profile | Fixed still resolves when baseline valid |
| AED-P-022 | PRE_MATCH stale candidate after base change | old candidate is never silently rebased |
| AED-P-023 | MID_MATCH AppliedScenarioConfigVersion changes after candidate build | STALE_BASE_CONFIG; no overwrite; KEEP_LAST_VALID_CONFIG |
| AED-P-024 | stale candidate retry | cannot overwrite current config; new attempt requires new decision identity/current base |

---

# 78. Fusion Multiplayer Tests

Run Host + 1, 2, and 3 client configurations as applicable.

| ID | Case | Expected |
|---|---|---|
| AED-N-001 | Host decision | only State Authority commits config |
| AED-N-002 | proxy attempts config mutation | rejected/ignored |
| AED-N-003 | proxy attempts parameter change | no authoritative change |
| AED-N-004 | duplicate decision request/response | one apply/version |
| AED-N-005 | late join after adaptive apply | receives current applied config state |
| AED-N-006 | late join | does not replay historical AdaptiveDecision |
| AED-N-007 | reconnect | no duplicate config apply |
| AED-N-008 | stale client-side candidate | cannot overwrite current config |
| AED-N-009 | proxy state | no Profile/snapshot internals replicated |
| AED-N-010 | configSource/scenarioConfigVersion | converges across proxies |
| AED-N-011 | current gameplay config value | converges across proxies |
| AED-N-012 | RPC presentation/request duplication | cannot bypass decision/apply guard |
| AED-N-013 | Host loses authority/session invalid | no proxy authoritative takeover of AED apply |

---

# 79. Backend / Integration Tests

If policy computation is backend-connected, these are target integration tests; backend implementation is not evidenced.

| ID | Case | Expected |
|---|---|---|
| AED-B-001 | fetch exact snapshot revision | deterministic resolve |
| AED-B-002 | decision retry | same decision result |
| AED-B-003 | decision ledger duplicate | idempotent |
| AED-B-004 | same ID conflicting payload | conflict/quarantine |
| AED-B-005 | backend timeout | fallback by point |
| AED-B-006 | backend unavailable | fallback by point |
| AED-B-007 | stale snapshot response | no Host apply |
| AED-B-008 | PRE_MATCH authoritative base changes while backend computes | STALE_BASE_CONFIG + FULL_FIXED_CONFIG; old candidate not rebased |
| AED-B-009 | policy version mismatch | UNSUPPORTED_VERSION |
| AED-B-010 | policy config semantic mirror mismatch | NOT_EVALUATED + POLICY_CONFIG_INVALID |
| AED-B-011 | parameter registry malformed/unsupported before candidate build | NOT_EVALUATED + PARAMETER_REGISTRY_INVALID |
| AED-B-012 | whitelist mismatch | no apply |
| AED-B-013 | crash after decision persisted before apply | retry converges; no false applied state |
| AED-B-014 | crash after apply before acknowledgement | retry discovers committed apply; no second version |
| AED-B-015 | concurrent decisions same match | serialized/CAS deterministic winner |
| AED-B-016 | source profile revision changes during decision | stale result rejected |
| AED-B-017 | fallback registry missing PRE_MATCH | fatal configuration error |
| AED-B-018 | same inputs/config reconstructed | same rule/request/result |
| AED-B-019 | evidence policy absent/invalid | NOT_EVALUATED + POLICY_CONFIG_INVALID; no Adaptive policy execution |
| AED-B-020 | experimentCondition ADAPTIVE but fallback occurs | condition preserved; exposure records fallback |
| AED-B-021 | sample thresholds changed with same evidencePolicyVersion | reject evidence config integrity |
| AED-B-022 | score thresholds changed with new policyConfigVersion only | legal tuning revision when canonical policy definition matches |
| AED-B-023 | rule priority/active-key/strategy mirror changed under same policyVersion | NOT_EVALUATED + POLICY_CONFIG_INVALID |
| AED-B-024 | MID_MATCH AppliedScenarioConfigVersion changes while backend computes | INVALID + STALE_BASE_CONFIG; no overwrite |
| AED-B-025 | stale PRE_MATCH candidate is retried unchanged after base change | rejected again; never automatically rebased |

---

# 80. Failure Matrix

Each deterministic condition maps to one lifecycle status, one reason, and one explicit fallback rule.

| Failure / condition | CandidateValidationStatus | Final Result / path | Fallback | Config Mutation | Diagnostic |
|---|---|---|---|---|---|
| FIXED requested PRE_MATCH | n/a | Fixed path | FULL_FIXED_CONFIG | valid fixed config | mode FIXED |
| FIXED requested ALLOWED_PHASE_BOUNDARY / FINAL_HUNT_SETUP | n/a | Fixed path | KEEP_LAST_VALID_CONFIG | none | mode FIXED |
| ADAPTIVE PRE_MATCH snapshot PARTIAL | NOT_EVALUATED | FIXED_FALLBACK | FULL_FIXED_CONFIG | valid fixed config may replace | INPUT_INCOMPLETE |
| ADAPTIVE boundary/final-hunt snapshot PARTIAL | NOT_EVALUATED | FIXED_FALLBACK | KEEP_LAST_VALID_CONFIG | none | INPUT_INCOMPLETE |
| ADAPTIVE PRE_MATCH snapshot INVALID | NOT_EVALUATED | FIXED_FALLBACK | FULL_FIXED_CONFIG | valid fixed config may replace | INPUT_INVALID |
| ADAPTIVE boundary/final-hunt snapshot INVALID | NOT_EVALUATED | FIXED_FALLBACK | KEEP_LAST_VALID_CONFIG | none | INPUT_INVALID |
| PRE_MATCH roster/profile revision becomes stale before candidate build | NOT_EVALUATED | FIXED_FALLBACK | FULL_FIXED_CONFIG | valid fixed config may replace | STALE_INPUT |
| boundary/final-hunt roster/profile revision becomes stale before candidate build | NOT_EVALUATED | FIXED_FALLBACK | KEEP_LAST_VALID_CONFIG | none | STALE_INPUT |
| evidence sample threshold missing/invalid | NOT_EVALUATED | FIXED_FALLBACK | FULL_FIXED_CONFIG at PRE_MATCH; KEEP_LAST_VALID_CONFIG otherwise | fixed only at PRE_MATCH | POLICY_CONFIG_INVALID |
| AED policy service unavailable | NOT_EVALUATED | FIXED_FALLBACK | FULL_FIXED_CONFIG at PRE_MATCH; KEEP_LAST_VALID_CONFIG otherwise | fixed only at PRE_MATCH | AED_UNAVAILABLE |
| AED policy timeout before candidate build | NOT_EVALUATED | FIXED_FALLBACK | FULL_FIXED_CONFIG at PRE_MATCH; KEEP_LAST_VALID_CONFIG otherwise | fixed only at PRE_MATCH | AED_TIMEOUT |
| AEDPolicyConfig / canonical policy mirror invalid | NOT_EVALUATED | FIXED_FALLBACK | FULL_FIXED_CONFIG at PRE_MATCH; KEEP_LAST_VALID_CONFIG otherwise | fixed only at PRE_MATCH | POLICY_CONFIG_INVALID |
| unsupported Profile/policy semantic version before candidate build | NOT_EVALUATED | FIXED_FALLBACK | FULL_FIXED_CONFIG at PRE_MATCH; KEEP_LAST_VALID_CONFIG otherwise | fixed only at PRE_MATCH | UNSUPPORTED_VERSION |
| parameter registry malformed/unsupported/missing active metadata before candidate build | NOT_EVALUATED | FIXED_FALLBACK | FULL_FIXED_CONFIG at PRE_MATCH; KEEP_LAST_VALID_CONFIG otherwise | fixed only at PRE_MATCH | PARAMETER_REGISTRY_INVALID |
| whitelist/content dependency unavailable before candidate build | NOT_EVALUATED | FIXED_FALLBACK | FULL_FIXED_CONFIG at PRE_MATCH; KEEP_LAST_VALID_CONFIG otherwise | fixed only at PRE_MATCH | SCENARIO_INVALID |
| inactive policy key requested in constructed candidate | INVALID | FIXED_FALLBACK | FULL_FIXED_CONFIG at PRE_MATCH; KEEP_LAST_VALID_CONFIG otherwise | none except fixed fallback replacement at PRE_MATCH | POLICY_KEY_NOT_ACTIVE |
| requested numerical value not in legal registered candidate set | INVALID | FIXED_FALLBACK | FULL_FIXED_CONFIG at PRE_MATCH; KEEP_LAST_VALID_CONFIG otherwise | none except fixed fallback replacement at PRE_MATCH | REGISTERED_VALUE_REJECTED |
| numerical bound violation | INVALID | FIXED_FALLBACK | FULL_FIXED_CONFIG at PRE_MATCH; KEEP_LAST_VALID_CONFIG otherwise | none except fixed fallback replacement at PRE_MATCH | BOUND_REJECTED |
| routeModifier requested PRE_MATCH | INVALID | FIXED_FALLBACK | FULL_FIXED_CONFIG | valid fixed config may replace | TIMING_REJECTED |
| other timing violation after candidate construction | INVALID | FIXED_FALLBACK | FULL_FIXED_CONFIG at PRE_MATCH; KEEP_LAST_VALID_CONFIG otherwise | none except fixed fallback replacement at PRE_MATCH | TIMING_REJECTED |
| pressure fairness violation | INVALID | FIXED_FALLBACK | FULL_FIXED_CONFIG at PRE_MATCH; KEEP_LAST_VALID_CONFIG otherwise | none except fixed fallback replacement at PRE_MATCH | PRESSURE_RULE_REJECTED |
| spawn candidate invalid | INVALID | FIXED_FALLBACK | FULL_FIXED_CONFIG at PRE_MATCH; KEEP_LAST_VALID_CONFIG otherwise | none except fixed fallback replacement at PRE_MATCH | SPAWN_INVALID |
| route candidate invalid | INVALID | FIXED_FALLBACK | KEEP_LAST_VALID_CONFIG | none | ROUTE_INVALID |
| Final Hunt timer value below 45 or above 60 before start | INVALID | FIXED_FALLBACK | KEEP_LAST_VALID_CONFIG | none | BOUND_REJECTED |
| Final Hunt timer mutation requested after timer start | INVALID | FIXED_FALLBACK | KEEP_LAST_VALID_CONFIG | none | TIMING_REJECTED |
| PRE_MATCH base identity/version/fingerprint changes after candidate build | INVALID | FIXED_FALLBACK | FULL_FIXED_CONFIG | old adaptive candidate not applied | STALE_BASE_CONFIG |
| MID_MATCH AppliedScenarioConfig version/fingerprint changes after candidate build | INVALID | FIXED_FALLBACK | KEEP_LAST_VALID_CONFIG | no overwrite | STALE_BASE_CONFIG |
| decision window closes after candidate build | INVALID | FIXED_FALLBACK | FULL_FIXED_CONFIG at PRE_MATCH; KEEP_LAST_VALID_CONFIG otherwise | none except fixed fallback replacement at PRE_MATCH | DECISION_WINDOW_CLOSED |
| decision identity conflict | INVALID | conflicting decision not committed | retain current authoritative config | none | DECISION_IDENTITY_CONFLICT |
| PRE_MATCH fallback config invalid | n/a | fatal configuration error | unavailable | none | FALLBACK_CONFIG_INVALID |
| Host/State Authority unavailable | n/a | no authoritative commit | no proxy apply | none | authority diagnostic |

Canonical lifecycle rule:

```text
failure before CandidateScenarioConfig construction
→ NOT_EVALUATED

candidate exists and fails candidate/precommit validation
→ INVALID

successful zero-delta candidate/evaluation
→ VALID + NO_CHANGE
```

No deterministic condition in this matrix uses a slash-separated status.

---

# 81. Implementation Plan

Recommended dependency order:

```text
1. Audit M1-015 v0 integration points and current ScenarioConfig storage.
2. Freeze runtime policy semantic ID AED_SCENARIO_POLICY_V1_1.
3. Implement canonical AEDPolicyDefinition for that semantic ID.
4. Bind Profile-owned AdaptiveInputSnapshot DTO/reference.
5. Implement ScenarioResolutionMode resolver.
6. Implement current roster/profile revision validator.
7. Implement AEDEvidencePolicy schema/validator as the sole sample-threshold owner.
8. Implement AEDInputGate.
9. Implement AEDPolicyConfig tuning/dependency schema without semantic rule ownership.
10. Validate any serialized rule/key/strategy mirrors against canonical AEDPolicyDefinition.
11. Implement score-band classifier.
12. Implement deterministic canonical rule evaluator.
13. Implement AdaptationIntent mapping.
14. Implement AdaptiveParameterRegistry candidateValues validation as a pre-build gate.
15. Implement v1.1 active strategies: SupportItemBudget relief, ChaseSpeed pressure increase.
16. Implement ScenarioConfigBaseRef and PRE_MATCH resolved-base capture.
17. Implement CandidateScenarioConfig builder.
18. Implement pure ScenarioValidator with exact NOT_EVALUATED vs INVALID stages.
19. Integrate objective/spawn validation.
20. Integrate FacilityGraph/current-overlay route safety adapter.
21. Implement FixedDirector / FIXED_BASELINE_V1 resolver.
22. Implement AdaptiveDecision semantic fingerprint and durable ledger.
23. Implement Host authoritative apply controller + exact PRE_MATCH/MID_MATCH base CAS.
24. Implement final precommit snapshot/profile/base/window revalidation.
25. Implement scenarioConfigVersion/configSource publication.
26. Bind durable Fusion replication for current applied config.
27. Implement decision/research evidence persistence.
28. Implement read-only AEDDebugSnapshot.
29. Execute pure/PlayMode/Fusion/backend tests.
30. Profile decision-point cost.
31. Freeze score-band thresholds in AEDPolicyConfig.
32. Freeze sample-count thresholds in AEDEvidencePolicy.
33. Freeze candidateValues/bounds in AdaptiveParameterRegistry.
34. Revise M1-020 into Fixed_vs_Adaptive_Experiment_Contract_v1.1.md.
35. Only then reassess runtime Adaptive activation and experiment readiness.
```

---

# 82. Migration Plan from M1-015 v0

1. Keep ScenarioConfig field topology and fixed fallback semantics.
2. Replace old loose input bundle with `AdaptiveInputSnapshot`.
3. Replace TeamPerformance COMPLETE input gate with v1.1 evidence gate.
4. Change policy semantic identity to `AED_SCENARIO_POLICY_V1_1`.
5. Retain all predecessor authority boundaries.
6. Separate `ScenarioResolutionMode` from `configSource` and experimentCondition.
7. Make `AEDEvidencePolicy` the sole owner of sample-count thresholds and evidence eligibility settings.
8. Remove duplicate `minimumSampleCountPerObservedPlayer` authority from `AEDPolicyConfig`.
9. Separate canonical `AEDPolicyDefinition` semantics from tuning-only `AEDPolicyConfig`.
10. If legacy serialized rule/priority/key/strategy mirrors remain, validate exact equality with the canonical definition.
11. Add explicit Rule IDs/priority without making them tuning-owned.
12. Add candidateValues adjustment mechanism to active numerical registry entries.
13. Mark authorized-but-unused keys `POLICY_NOT_SELECTED`.
14. Introduce `ScenarioConfigBaseRef` semantics for exact PRE_MATCH and MID_MATCH candidate bases.
15. Add PRE_MATCH base identity/version/fingerprint precommit revalidation.
16. Preserve MID_MATCH current AppliedScenarioConfig CAS.
17. Separate pre-build parameter-registry failure (`NOT_EVALUATED`) from candidate violations (`INVALID`).
18. Add stale snapshot/profile/base-config/window checks.
19. Add durable exactly-once decision/apply ledger.
20. Keep FixedDirector independently usable.
21. Do not activate M1-020 experiment until its gate/protocol is revised and implementation/tuning prerequisites pass.

---

# 83. Hard Invariants

1. AED consumes processed Profile evidence only.
2. Raw TelemetryEvent never directly drives a difficulty decision.
3. AED never commands Monster FSM states.
4. AED never selects CurrentTarget/DetectionTarget.
5. AED never writes LastKnownPosition.
6. AED never reads exact hidden Player Transform as policy input.
7. AED never applies damage.
8. Runtime NoiseEvent and Telemetry remain outside AED direct gameplay authority.
9. FIXED requested mode never runs Adaptive policy.
10. experimentCondition and configSource are different concepts.
11. configSource is applied-config provenance, not requested mode.
12. ADAPTIVE fallback never relabels experimentCondition to FIXED.
13. PARTIAL snapshot cannot be silently treated as VALID.
14. INVALID snapshot cannot run policy.
15. COLD_START 50 is not observed performance.
16. DEFERRED fields are not numeric evidence.
17. TeamPerformance is never synthetically completed.
18. TeamPerformance INCOMPLETE/null remains honest.
19. AED v1.1 uses SURVIVAL and NOISE as separate evidence dimensions.
20. Input-eligibility semantic change uses a distinct policy identity.
21. M1-020 v0 is not silently rewritten.
22. `AEDEvidencePolicy` is the single authoritative owner of evidence eligibility configuration.
23. `minimumSampleCountPerObservedPlayer` has one owner: `AEDEvidencePolicy`.
24. Sample-threshold changes require `evidencePolicyVersion`; they are not `policyConfigVersion` tuning.
25. `AEDPolicyDefinition` owns rule topology, priorities, predicates, intent mappings, active keys, and strategy bindings.
26. `AEDPolicyConfig` cannot change canonical policy semantics under the same `policyVersion`.
27. Serialized semantic mirrors must exactly match `AED_SCENARIO_POLICY_V1_1` or policy execution is invalid.
28. Every policy output resolves through a stable Rule ID.
29. Same semantic input/config yields the same rule and changes.
30. No unseeded policy randomness exists.
31. Policy cannot generate arbitrary numerical values.
32. Adaptive authority is distinct from policy-active status.
33. Unknown/non-authorized key is rejected.
34. Non-policy-active key cannot be emitted by AED_SCENARIO_POLICY_V1_1.
35. Stalker adaptive authority remains the closed four-key whitelist.
36. VisionDistance/VisionAngle are not adaptive.
37. AttackRange/Windup/Recovery/Damage are not adaptive.
38. Listener adaptive runtime authority is not invented.
39. Warden current DoorId is never an AED output.
40. routeModifier is scenario-level only.
41. routeModifier is not adaptively changed PRE_MATCH.
42. A route candidate must be safe before apply.
43. AED cannot rely on Warden fail-safe to legalize an unsafe candidate.
44. At most one Stalker pressure axis becomes more aggressive per allowed decision.
45. Double Detection buff is forbidden.
46. v1.1 policy produces at most one changed key.
47. Whole candidate is atomic.
48. No partial apply.
49. Out-of-bound values are rejected, not clamped.
50. Support budget changes are non-retroactive.
51. Final Hunt timer cannot change after its timer starts.
52. NO_CHANGE is a successful zero-delta result.
53. NO_CHANGE does not invoke FixedDirector.
54. NO_CHANGE creates no ScenarioConfig version.
55. NO_CHANGE does not relabel configSource.
56. Mid-match fallback never loads full baseline over the live match.
57. Mid-match fallback never rolls back a prior valid adaptive decision.
58. Mid-match fallback never resets Player/objective/Monster runtime state.
59. One decisionId applies at most once.
60. Same decisionId cannot represent conflicting semantics.
61. Stale Profile/snapshot cannot apply.
62. `baseScenarioConfigVersion` always means the version of the exact authoritative base content used to construct the candidate.
63. PRE_MATCH candidate base does not require a previously live AppliedScenarioConfig.
64. PRE_MATCH base identity/version/fingerprint is revalidated before commit.
65. A stale PRE_MATCH candidate is never silently rebased.
66. MID_MATCH candidate base is current last valid AppliedScenarioConfig.
67. Stale MID_MATCH AppliedScenarioConfig cannot be overwritten.
68. Closed decision window cannot apply.
69. Parameter-registry integrity failure before candidate construction always maps to `NOT_EVALUATED + PARAMETER_REGISTRY_INVALID`.
70. Candidate violation after a valid registry always maps to `INVALID` with the exact violation reason.
71. No deterministic failure condition may use slash-separated candidate lifecycle status.
72. One authoritative config mutation commit occurs at a time per match.
73. Host/State Authority owns authoritative config apply.
74. Proxy clients cannot authoritatively evaluate/apply ScenarioConfig.
75. RPC is not the sole durable representation of applied config.
76. Late join reconstructs current applied config without replaying historical decisions.
77. AdaptiveDecision audit evidence is immutable after final commit.
78. Profile correction never causes AED to rewind already-executed gameplay.
79. Gameplay telemetry failure never grants AED additional authority.
80. Adaptive failure cannot disable a valid Fixed path.
81. Contract readiness, implementation readiness, runtime activation, and experiment readiness are separate.
82. No persistent team identity is created.
83. No ML/RL/GenAI gameplay decision is introduced.

---

# 84. Definition of Done

A developer can answer all of the following without inventing semantics:

| # | Question | v1.1 answer |
|---:|---|---|
| 1 | Current policy semantic identity? | `AED_SCENARIO_POLICY_V1_1` logical identity |
| 2 | Why distinct from v0? | snapshot/evidence gate supersedes TeamPerformance COMPLETE |
| 3 | What object does AED consume? | immutable `AdaptiveInputSnapshot` |
| 4 | Can AED read raw telemetry? | No |
| 5 | FIXED mode? | bypass policy; FixedDirector |
| 6 | ADAPTIVE mode? | snapshot gate → policy or fallback |
| 7 | Is configSource requested mode? | No |
| 8 | experimentCondition == configSource? | No |
| 9 | VALID snapshot? | may proceed to evidence policy |
| 10 | PARTIAL snapshot? | ineligible/fallback |
| 11 | INVALID snapshot? | invalid/fallback |
| 12 | Who owns `minimumSampleCountPerObservedPlayer`? | `AEDEvidencePolicy` only |
| 13 | Which version changes when sample thresholds change? | `evidencePolicyVersion` |
| 14 | Can `AEDPolicyConfig` carry a second authoritative sample threshold? | No |
| 15 | COLD_START count? | No |
| 16 | Policy Player dimensions? | SURVIVAL and NOISE only |
| 17 | TeamPerformance COMPLETE still mandatory? | No for AED v1.1 |
| 18 | What supersedes it? | explicit AdaptiveInputSnapshot evidence gate |
| 19 | Does AED redefine TeamPerformance? | No |
| 20 | Does this make M1-020 READY? | No |
| 21 | Current-match metrics consumed? | none in policy v1.1 |
| 22 | Current-match finality consumed? | none |
| 23 | Who owns policy rule semantics? | canonical `AEDPolicyDefinition` identified by `policyVersion` |
| 24 | Can policyConfigVersion change rule priority/topology/active keys/strategy? | No |
| 25 | What does policyConfigVersion tune? | score-band thresholds only in v1.1 |
| 26 | Policy Rule IDs/priorities? | canonical values in §20.1 and §24 |
| 27 | Semantic mirror mismatch under same policyVersion? | NOT_EVALUATED + POLICY_CONFIG_INVALID |
| 28 | Bands? | exact LOW/MID/HIGH boundary semantics; numerical thresholds versioned by policyConfigVersion |
| 29 | Authority-approved keys? | predecessor whitelist/Scenario fields retained |
| 30 | Policy-active keys? | SupportItemBudget and ChaseSpeed only |
| 31 | Numeric computation? | next higher registered candidate value |
| 32 | Arbitrary float? | forbidden |
| 33 | Non-adaptive Stalker keys? | §29 |
| 34 | Listener adaptive? | none |
| 35 | AED can command Warden door? | no |
| 36 | routeModifier meaning? | scenario-level whitelisted route content |
| 37 | route timing? | adaptive authority only at ALLOWED_PHASE_BOUNDARY |
| 38 | route safety? | current effective FacilityGraph/player-route state |
| 39 | include Warden overlay? | yes |
| 40 | unsafe then Warden repair? | forbidden |
| 41 | pressure-axis rules? | §34 |
| 42 | max changed keys? | current policy <=1; inherited fairness still validated |
| 43 | NO_CHANGE? | valid zero-delta |
| 44 | NO_CHANGE creates version? | no |
| 45 | invalid candidate? | whole reject → fallback |
| 46 | PRE_MATCH failure? | FULL_FIXED_CONFIG |
| 47 | mid-match failure? | KEEP_LAST_VALID_CONFIG |
| 48 | Final Hunt failure? | KEEP_LAST_VALID_CONFIG |
| 49 | rollback previous adaptive? | no |
| 50 | fallback relabel configSource? | no if no replacement |
| 51 | FIXED_BASELINE_V1? | designer-authored known-safe full pre-match template |
| 52 | invalid fallback config? | fatal configuration error |
| 53 | decisionId? | stable logical idempotency identity |
| 54 | duplicate retry? | same semantic result, no duplicate apply |
| 55 | What does `baseScenarioConfigVersion` mean? | version of the exact authoritative base content used to construct the candidate |
| 56 | PRE_MATCH candidate base? | exact authoritative resolved designer/fixed base ScenarioConfig; no prior AppliedScenarioConfig required |
| 57 | PRE_MATCH CAS compares what? | captured baseConfigId + baseScenarioConfigVersion + baseContentFingerprint against current authoritative PRE_MATCH base |
| 58 | PRE_MATCH base changes after build? | INVALID + STALE_BASE_CONFIG + FULL_FIXED_CONFIG; old candidate never rebased |
| 59 | MID_MATCH candidate base? | current last valid AppliedScenarioConfig |
| 60 | MID_MATCH AppliedScenarioConfig version changes? | INVALID + STALE_BASE_CONFIG; no overwrite |
| 61 | Registry malformed before candidate build? | NOT_EVALUATED + PARAMETER_REGISTRY_INVALID |
| 62 | Valid registry + bound violation? | INVALID + BOUND_REJECTED |
| 63 | Valid registry + inactive key? | INVALID + POLICY_KEY_NOT_ACTIVE |
| 64 | Valid registry + unregistered requestedAfter? | INVALID + REGISTERED_VALUE_REJECTED |
| 65 | roster changed? | stale input, rebuild/new decision if window open |
| 66 | source profile revision changed? | stale input; old snapshot cannot authorize future apply |
| 67 | authoritative apply owner? | Host / Fusion State Authority |
| 68 | clients apply? | no |
| 69 | research evidence? | §68 |
| 70 | versions? | §65 |
| 71 | implementation status? | not evidenced/not complete |
| 72 | Live Adaptive experiment ready? | no |
| 73 | next required contract revision? | M1-020 → Fixed_vs_Adaptive_Experiment_Contract_v1.1.md before execution |

---

# 85. Open Tuning / Implementation Bindings

## 85.1 TUNING TBD

Allowed tuning values have exact owners.

```text
SURVIVAL low/high score-band thresholds
NOISE low/high score-band thresholds
→ AEDPolicyConfig
→ policyConfigVersion
```

```text
minimum sampleCount per Player/dimension
→ AEDEvidencePolicy
→ evidencePolicyVersion
```

```text
candidateValues
default/min/max
pressure-axis metadata
adjustment metadata
for adaptive numerical keys
→ AdaptiveParameterRegistry
→ parameterRegistryVersion
```

Also allowed:

- other numerical default/min/max values not already source-frozen, under their owning registry;
- profiler performance budgets.

Before runtime Adaptive activation, required values must exist in their exact versioned owner. Missing values produce safe fallback, not guessed defaults.

## 85.2 IMPLEMENTATION BINDING TBD

Allowed:

- exact C# class/interface names;
- ScriptableObject/JSON/backend registry representation;
- DB table names;
- decisionId GUID/ULID representation;
- semantic fingerprint/hash algorithm;
- transaction/CAS primitive;
- local Host versus backend policy computation placement;
- exact Fusion NetworkObject/NetworkBehaviour/[Networked] layout;
- exact RPC usage;
- exact persisted token encoding for `AED_SCENARIO_POLICY_V1_1`;
- exact storage representation of `ScenarioConfigBaseRef`.

## 85.3 Not allowed as TBD

- AED input object;
- FIXED/ADAPTIVE semantics;
- PARTIAL/INVALID handling;
- COLD_START/DEFERRED behavior;
- TeamPerformance gate decision;
- required policy dimensions;
- current-match evidence consumption;
- single ownership of sample-count thresholds;
- sample-threshold version owner;
- canonical policy definition;
- rule priority;
- active-key set;
- strategy binding;
- policyConfigVersion semantic limits;
- candidate-value adjustment topology;
- PRE_MATCH candidate-base meaning;
- MID_MATCH candidate-base meaning;
- stale PRE_MATCH base detection;
- parameter-registry failure stage classification;
- routeModifier timing;
- pressure fairness;
- whole-candidate atomicity;
- NO_CHANGE;
- fallback mapping;
- configSource lifecycle;
- decision idempotency;
- stale snapshot/base config/window behavior;
- Host apply authority;
- experiment-readiness distinction.

---

# 86. Architecture Escalation

No architecture escalation is required.

This contract preserves:

```text
Gameplay
→ Telemetry
→ Profile
→ AdaptiveInputSnapshot
→ AED
→ ScenarioConfig
→ Traditional AI
```

It does not introduce:

- Profile → direct Monster command;
- telemetry → direct AI;
- persistent team identity;
- ML/GenAI runtime policy;
- client-authoritative config.

A later ScenarioConfig schema revision for genuinely simultaneous multi-monster configuration, if approved gameplay requires it, does not retroactively authorize v1.1 code to guess that representation.

---

# 87. AED / ScenarioConfig Contract Validation

```text
M1-015 v0 validity review complete: YES

AdaptiveInputSnapshot consumption exact: YES
FIXED/ADAPTIVE requested-mode semantics exact: YES
experimentCondition vs configSource separated: YES

Snapshot VALID handling exact: YES
Snapshot PARTIAL handling exact: YES
Snapshot INVALID handling exact: YES
COLD_START handling exact: YES
DEFERRED handling exact: YES
Evidence sufficiency policy exact: YES

AED-DD-01:
AEDEvidencePolicy single evidence owner: YES
minimumSampleCount duplicate ownership removed: YES
sample threshold version owner exact: YES
AEDPolicyConfig references evidencePolicyVersion only: YES

AED-DD-02:
canonical AEDPolicyDefinition explicit: YES
policy semantic fields frozen outside tuning config: YES
policyConfigVersion cannot redefine semantics: YES
serialized semantic mirror mismatch fails closed: YES

TeamPerformance gate decision explicit: YES
No synthetic TeamPerformance: YES
Policy semantic identity distinct when eligibility changed: YES

Actual deterministic policy topology complete: YES
Policy rules unchanged: YES
Rule priority deterministic: YES
Policy-active key set unchanged: YES
Numerical adjustment topology unchanged: YES

Stalker adaptive authority exact: YES
Listener adaptive authority exact: YES
Warden runtime authority boundary exact: YES

Pressure fairness exact: YES
Objective spawn safety exact: YES
Support budget semantics exact: YES
RouteModifier semantics exact: YES
Warden/FacilityGraph safety integration exact: YES
Final Hunt timer exact: YES

ScenarioConfig schema exact: YES
configSource lifecycle exact: YES

AED-DD-03:
baseScenarioConfigVersion has one meaning: YES
PRE_MATCH authoritative base semantics exact: YES
PRE_MATCH base identity/fingerprint captured: YES
PRE_MATCH stale-base CAS exact: YES
PRE_MATCH silent rebase forbidden: YES
MID_MATCH AppliedScenarioConfig CAS exact: YES

ScenarioValidator exact: YES
Whole candidate atomicity exact: YES

AED-DD-04:
pre-build registry failure = NOT_EVALUATED: YES
candidate registry-rule violation = INVALID: YES
failure matrix slash-status ambiguity remains: NO

NO_CHANGE exact: YES
FixedDirector exact: YES
PRE_MATCH fallback exact: YES
MID_MATCH fallback exact: YES
Final Hunt fallback exact: YES

Decision identity/idempotency exact: YES
Stale snapshot prevention exact: YES
Stale base config prevention exact: YES
Decision-window validation exact: YES
Concurrency semantics exact: YES

Host authority preserved: YES
Client authoritative apply forbidden: YES

Version ownership complete: YES
Cross-version dependency rule complete: YES
Reproducibility complete: YES
Research evidence retention complete: YES

M1-020 impact explicit: YES
No synthetic Adaptive readiness: YES
Implementation status honest: YES
Test plan complete: YES

Architecture escalation required: NO
```

---

# 88. Final Consistency Audit

```text
AED can read raw TelemetryEvent for difficulty reaction: NO
AED can command CHASE/ATTACK/SEARCH: NO
AED can set CurrentTarget: NO
AED can update LastKnownPosition: NO
AED can use exact hidden Player Transform: NO

FIXED experiment condition can accidentally run Adaptive policy: NO
configSource is the experiment assignment: NO
ADAPTIVE experiment fallback relabels experiment condition FIXED: NO

COLD_START 50 counts as observed score: NO
DEFERRED Profile field becomes zero: NO
PARTIAL snapshot may be silently treated as VALID: NO
TeamPerformance is fabricated from Survival/Noise: NO

Changing input eligibility can silently reuse v0 policy semantic identity: NO

Sample thresholds can have two authoritative owners: NO
AEDPolicyConfig can independently author minimumSampleCountPerObservedPlayer: NO
Sample-threshold change can masquerade under unchanged evidencePolicyVersion: NO
Sample-threshold change requires policyConfigVersion instead of evidencePolicyVersion: NO

policyConfigVersion can silently change rule semantics: NO
policyConfigVersion can change canonical rule priority: NO
policyConfigVersion can change active policy keys: NO
policyConfigVersion can change strategy binding: NO
Serialized semantic mirrors can diverge from AEDPolicyDefinition and still execute: NO

Policy may produce arbitrary float inside min/max: NO
Policy rule tie may depend on dictionary iteration order: NO
Unseeded randomness may choose adaptive key: NO

Field exists means AED may adapt it: NO
VisionDistance may be adapted: NO
AttackRange may be adapted: NO
Damage may be adapted: NO

AED may invent Listener adaptive hearing knob: NO
AED routeModifier may command Warden DoorId: NO
AED may rely on Warden fail-safe to repair unsafe route candidate: NO

routeModifier may change PRE_MATCH: NO
Final Hunt timer may change after start: NO
Out-of-bounds value may be silently clamped: NO

DetectionFillRate up + DetectionDecayRate down may apply together: NO
Multiple aggressive Stalker pressure axes may apply together: NO

One valid + one invalid requested change may partially apply: NO

NO_CHANGE invokes FixedDirector: NO
NO_CHANGE creates scenarioConfigVersion: NO
NO_CHANGE relabels configSource: NO

Mid-match failure loads full FIXED_BASELINE_V1: NO
Mid-match failure rolls back previous valid Adaptive change: NO
Mid-match failure resets Monster FSM: NO

PRE_MATCH candidate requires a previously live AppliedScenarioConfig: NO
Old PRE_MATCH candidate can silently rebase after base change: NO
Stale base config can overwrite newer state: NO
MID_MATCH stale AppliedScenarioConfig can be overwritten: NO

Registry-invalid pre-build condition can be arbitrarily classified as NOT_EVALUATED or INVALID: NO
Valid registry + invalid requested candidate can be classified as NOT_EVALUATED: NO
Failure matrix contains slash-separated candidate lifecycle ambiguity: NO

Same decision retry can create two ScenarioConfig versions: NO
Stale snapshot can apply: NO
Late decision after window closes can apply: NO

Proxy client can authoritatively apply ScenarioConfig: NO

New AED v1.1 contract alone proves Fixed-vs-Adaptive experiment READY: NO
M1-020 v0 can be silently ignored if its gate no longer matches AED v1.1: NO

Current-match raw/research evidence can silently influence policy v1.1: NO
Authorized-but-policy-inactive key can be selected without policy revision: NO
Policy v1.1 can change more than one key: NO
Non-Stalker scenario can be silently changed into Stalker for pressure increase: NO
```

All expected audit answers are `NO`.

## Correction Verification

| ID | Issue | Resolution | Affected Sections | Tests |
|---|---|---|---|---|
| AED-DD-01 | Duplicate evidence ownership between `AEDEvidencePolicy` and `AEDPolicyConfig` | **RESOLVED** — `AEDEvidencePolicy` is sole owner of sample thresholds/evidence eligibility; `AEDPolicyConfig` references only `evidencePolicyVersion`; sample-threshold changes bump `evidencePolicyVersion`. | §13, §64–65, §67–68, §73, §75, §81–85, §87–88 | AED-E-013/014/061/062/063, AED-B-019/021 |
| AED-DD-02 | Policy semantic fields could be changed through tuning config without `policyVersion` | **RESOLVED** — canonical `AEDPolicyDefinition` owns rules/priorities/predicates/intents/active keys/strategy bindings; serialized mirrors must match; tuning config owns score thresholds only. | §20, §24–25, §28, §45, §64–65, §67, §72–75, §81–85, §87–88 | AED-E-024/064–069, AED-B-010/022/023 |
| AED-DD-03 | PRE_MATCH `baseScenarioConfigVersion` / CAS lifecycle ambiguous | **RESOLVED** — one `ScenarioConfigBaseRef` semantic; PRE_MATCH uses exact resolved authoritative base identity/version/fingerprint; MID_MATCH uses current last valid AppliedScenarioConfig; stale candidate never rebases. | §44–45, §52, §56, §58, §60, §68, §73, §75, §77, §79–85, §87–88 | AED-E-055/075, AED-P-019/022/023/024, AED-B-008/024/025 |
| AED-DD-04 | Parameter-registry failure stage ambiguous | **RESOLVED** — registry/config integrity failure before candidate build is `NOT_EVALUATED + PARAMETER_REGISTRY_INVALID`; violations after valid registry and candidate construction are `INVALID` with exact violation reason. | §31, §33, §45, §51, §54, §75, §79–80, §82–85, §87–88 | AED-E-030–033/070–074, AED-B-011 |

## Final Detailed-Design Status

```text
BASELINE APPROVED
```

```text
Document Revision: v1.1
Recommended Status: BASELINED v1.1
Architecture Escalation Required: NO
P0 Remaining: 0
P1 Remaining: 0
```

---

# 89. References

## 89.1 Project contracts

1. `AI_Architecture_v1.1.md`.
2. `Player_Team_Profile_Contract_v1.1.md`.
3. `Telemetry_Contract_v1.1.md`.
4. `Stalker_AI_Design_v1.1.md`.
5. `Listener_AI_Design_v1.0.md`.
6. `Warden_AI_Design_v1.0.md`.
7. `M1-015_ScenarioConfig_AED_Fairness_Policy_v0_FINAL.md`.
8. `M1-020_Test_Strategy_Fixed_vs_Adaptive_Experiment_v0_FINAL.md`.
9. `M1-014_Player_Team_Profile_Fields_Formulas_v0_FINAL.md`.
10. `ECHO PROTO.docx` / current GDD.
11. `KLTN.docx` — Research Facility Map Flow.
12. `KLTN (1).docx` — multiplayer organization/synchronization.

## 89.2 External methodology / engine references

13. Zohaib, M. (2018). *Dynamic Difficulty Adjustment (DDA) in Computer Games: A Review*. Advances in Human-Computer Interaction, 2018, 5681652. DOI: `10.1155/2018/5681652`.
14. Hunicke, R., LeBlanc, M., & Zubek, R. (2004). *MDA: A Formal Approach to Game Design and Game Research*. AAAI Workshop.
15. Spronck, P. H. M., Ponsen, M. J. V., Sprinkhuizen-Kuyper, I. G., & Postma, E. O. (2006). *Adaptive Game AI with Dynamic Scripting*. Machine Learning, 63, 217–248. DOI: `10.1007/s10994-006-6205-6`.
16. Photon Engine — Fusion 2, PlayerRef / State Authority / Input Authority: `https://doc.photonengine.com/fusion/v2/manual/playerref`.
17. Photon Engine — Fusion 2, Data Transfer / Networked Properties: `https://doc.photonengine.com/fusion/v2/manual/data-transfer/data-transfer`.
18. Photon Engine — Fusion 2, RPCs: `https://doc.photonengine.com/fusion/v2/manual/data-transfer/rpcs`.

Methodology note:

- DDA literature supports adapting bounded game parameters/scenarios in response to player evidence, but ECHO PROTOCOL deliberately narrows that general idea to designer-authored, versioned, deterministic rules.
- MDA is used as a research/design framing for traceability between mechanics, observed dynamics, and evaluation; it does not define ECHO gameplay.
- Dynamic-scripting research motivates clarity/consistency/scalability requirements for adaptive systems; ECHO does **not** adopt online learning/dynamic scripting in runtime policy v1.1.
- Photon references support Host/State Authority ownership and durable network state; RPCs are not treated as durable late-join state.

---

# 90. Final Status

```text
BASELINE APPROVED

Document Revision: v1.1
Recommended Status: BASELINED v1.1
Architecture Escalation Required: NO
P0 Remaining: 0
P1 Remaining: 0

Surgical Correction Pass:
AED-DD-01 — RESOLVED
AED-DD-02 — RESOLVED
AED-DD-03 — RESOLVED
AED-DD-04 — RESOLVED

Policy Semantic Identity:
AED_SCENARIO_POLICY_V1_1

Profile-side SC-03:
RESOLVED

AED-side AdaptiveInputSnapshot consumption:
RESOLVED

TeamPerformance.status:
INCOMPLETE

TeamPerformance.score:
null

AED Implementation:
NOT EVIDENCED / NOT COMPLETE

Policy numerical tuning:
NOT FROZEN

M1-020 experiment protocol:
v1.1 REVISION REQUIRED BEFORE EXECUTION

Live Adaptive execution:
NOT READY

Live Fixed-vs-Adaptive experiment:
NOT READY
```

The contract remains v1.1. The surgical correction closes AED-DD-01 through AED-DD-04 without changing current gameplay policy, adaptive authority, Monster runtime behavior, or the architecture boundary.

**End of `AED_ScenarioConfig_Contract_v1.1.md`**
