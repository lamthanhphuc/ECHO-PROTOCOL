# ECHO PROTOCOL — M1-020 Test Strategy + Fixed vs Adaptive Experiment v0

**Task:** M1-020 — Test Strategy + Fixed vs Adaptive Experiment  
**Owner:** C — AI / Telemetry / Research  
**Support baseline:** A — Gameplay / UI / Integration; D — Backend/Data khi test boundary liên quan  
**Priority:** P0  
**Document Status:** **DONE / FROZEN — Test Strategy & Experiment Protocol v0**  
**Live Adaptive Experiment Status:** **NOT READY trong current M1 baseline**  
**Protocol Version:** `M1-020-v0`  
**Date baseline:** 2026-08-21

> **Quan trọng:** `DONE / FROZEN` ở đây chỉ có nghĩa **test strategy + experiment protocol đã được chốt ở mức design/contract**. Nó **không** có nghĩa implementation đã pass test, không có nghĩa Adaptive AED đã chạy live, và không có nghĩa Fixed hay Adaptive “thắng” trong experiment.

---

# 1. Purpose

Tài liệu này định nghĩa **Test Strategy v0** và **Fixed vs Adaptive Experiment Protocol v0** cho ECHO PROTOCOL, nhằm tạo một baseline thống nhất để Unity, Backend, AI/Telemetry và QA có thể kiểm thử các system theo đúng contract đã freeze mà không tự suy đoán gameplay hoặc adaptive authority.

M1-020 có các mục tiêu chính:

1. Xác định test level cho contract, component, integration, system/E2E và experiment.
2. Xác định cách kiểm thử Traditional AI, Stalker, Telemetry, Profile, TeamPerformance, ScenarioConfig, AED Input Gate, Scenario Validator, FixedDirector và GenAI Mission Briefing.
3. Xác định negative/boundary/fallback tests bắt buộc cho AED fairness.
4. Xác định điều kiện nào cho phép hoặc không cho phép Adaptive experiment chạy.
5. Định nghĩa protocol khoa học để sau này so sánh **FixedDirector** với **Adaptive AED** bằng cùng gameplay/content constraints.
6. Định nghĩa evidence, versioning và reproducibility để một kết quả quan trọng có thể trace về build + config + data + condition.
7. Tách rõ:

```text
Test Strategy
≠ Test Execution
```

và:

```text
Experiment Design
≠ Experiment Result
```

M1-020 **không** implement test code, không chạy playtest thật và không tạo kết quả giả.

---

# 2. Scope

## 2.1. In Scope

M1-020 v0 bao phủ:

- contract testing cho M1-007, M1-008, M1-013, M1-014, M1-015, M1-019;
- unit/component test strategy;
- integration test strategy;
- system/end-to-end test strategy;
- Traditional AI/Stalker regression strategy;
- Telemetry schema/validation/data-quality strategy;
- Profile/TeamPerformance formula, eligibility, availability, normalization và EMA strategy;
- AED Input Gate, ScenarioConfig, Scenario Validator, fairness, atomicity và fallback strategy;
- FixedDirector baseline/fallback strategy;
- GenAI Mission Briefing safety/fallback strategy;
- Fixed vs Adaptive research question, condition, experimental unit, counterbalancing và confounder control;
- metric classification;
- sample-size policy;
- data eligibility/exclusion;
- analysis/reporting protocol;
- versioning/reproducibility;
- evidence/artifact requirements;
- representative P0, negative, boundary và readiness test cases;
- current M1 experiment readiness.

## 2.2. Current M1 Executable Scope

Trong current baseline, chiến lược cho phép kiểm thử ở mức phù hợp với implementation availability:

- FixedDirector / fixed ScenarioConfig;
- Scenario Validator;
- fallback behavior;
- Adaptive Input Gate với valid/invalid/incomplete input;
- contract semantics và negative tests;
- deterministic reproduction;
- Traditional AI / Stalker contracts;
- Telemetry schema/validation;
- Profile formulas chỉ với source đã `ACTIVE`;
- invalid Adaptive input handling;
- GenAI Mission Briefing boundary/fallback contract.

## 2.3. Deferred Execution Scope

Live Fixed-vs-Adaptive gameplay experiment chỉ được chạy khi:

```text
Adaptive Readiness Gate = PASS
```

Current immediate blocker:

```text
Teamwork = DEFERRED
ResourceEfficiency = DEFERRED
→ TeamPerformance.status = INCOMPLETE
→ TeamPerformance.score = null
```

Ngoài immediate blocker trên, live execution còn phụ thuộc các prerequisite chưa freeze/complete được liệt kê tại **Section 35.2 — Non-Conflict TBDs**. Các prerequisite execution này không thay đổi trạng thái completion của M1-020 document/protocol.

Current baseline không cho phép fake/synthetic TeamPerformance để vượt gate.

---

# 3. Source of Truth / Dependencies

## 3.1. Mandatory Source Order Reviewed

Tài liệu này được xây dựng theo thứ tự bắt buộc:

### Tier A — Project Source of Truth / Planning Baseline

| Order | Source | Role in M1-020 |
|---:|---|---|
| 1 | `ECHO PROTO(1).docx` — attachment copy supplied as `ECHO PROTO(2).docx` | Gameplay Source of Truth: gameplay scope, match flow, monsters, AED intent |
| 2 | `01_ECHO_PROTOCOL_Project_Scope_REVISED.docx` | Project/KLTN scope, QA/research deliverables, milestone gates |
| 3 | `02_ECHO_PROTOCOL_System_Architecture_REVISED.docx` | Architecture boundaries, components, runtime/data/service responsibility |
| 4 | `03_ECHO_PROTOCOL_Implementation_Spec_REVISED.xlsx` | Requirement/test baseline, QA-04, AED experiment flag, TestRun/evidence expectations |
| 5 | `04_ECHO_PROTOCOL_Project_Management_Baseline_REVISED.xlsx` | Ownership, dependency, DoD, risk and M5 research dependency |
| 6 | `05_ECHO_PROTOCOL_Project_Plan_4P_2026_REVISED.xlsx` | M1-020 task, milestone ownership, M5/M6 experiment execution/reporting |

### Tier B — Frozen Contracts of C

| Order | Source | Contract used by M1-020 |
|---:|---|---|
| 7 | `AI_Architecture_Traditional_vs_Modern(1).md` — supplied copy `(2)` | M1-007 — Traditional AI vs AED vs GenAI separation |
| 8 | `Telemetry_Event_Schema_v0_FINAL.md` — supplied copy `(1)` | M1-008 — TelemetryEvent v1.0 |
| 9 | `M1-013_Stalker_FSM_Sensor_Contracts_FINAL.md` — supplied copy `(1)` | Stalker FSM, Vision/LOS, Target Selection, LKP, no Hearing |
| 10 | `M1-014_Player_Team_Profile_Fields_Formulas_v0_FINAL (1).md` — supplied copy `(1)(1)` | Profile/TeamProfile/TeamPerformance formulas and completeness |
| 11 | `M1-015_ScenarioConfig_AED_Fairness_Policy_v0_FINAL.md` — supplied copy `(1)` | Adaptive eligibility, whitelist, fairness, validator, fallback, reproducibility |
| 12 | `M1-019_GenAI_Mission_Briefing_Scope_Safety_Contract_v0_FINAL.md` — supplied copy `(1)` | GenAI Mission Briefing presentation-only safety contract |

`Stalker_Basic_AI_Specification(2).md` được cung cấp kèm nhưng **không nằm trong mandatory Source-of-Truth list của M1-020**, vì vậy không được dùng để override Tier A/Tier B frozen contracts.

## 3.2. Source Precedence Rule

```text
Tier A — Project / Gameplay Source of Truth
↓
Architecture / Implementation / Planning baseline
↓
Frozen task-specific M1 contracts
   = implementation/test clarification
     derived from the approved project baseline
↓
M1-020 Test Strategy
```

Tier A vẫn là **Project / Gameplay Source of Truth**. Frozen task-specific contracts không được M1-020 dùng như một cơ chế để tự ý override Tier A; chúng là current frozen implementation/test clarification cho phạm vi của task tương ứng.

Nếu generic/older Tier A wording mâu thuẫn với một newer task-specific frozen contract:

```text
DO NOT silently override Tier A
DO NOT merge both behaviors
DO NOT edit upstream/frozen source from M1-020
DO NOT invent a replacement rule
→ record explicit Open Issue / Source Conflict
→ M1-020 test strategy uses the current frozen task-specific contract
   for the scope of that specific task
→ upstream Tier A must be aligned/revised by the project owner
   before final implementation acceptance
```

Ví dụ đối với Stalker, M1-013 là current frozen implementation/test clarification dùng cho Stalker regression testing, trong khi contradiction với generic/older Tier A wording vẫn được giữ explicit tại `SC-01` và phải được giải quyết bằng upstream revision; M1-020 không tự đặt M1-013 “cao hơn” Project Source of Truth.

Khi conflict chưa được upstream resolve, M1-020 chỉ tiếp tục các test semantic không phụ thuộc phần conflict hoặc dùng task-specific frozen clarification đúng phạm vi như đã ghi rõ trong Source Conflict tương ứng.

## 3.3. Primary Dependencies

```text
M1-007 Architecture
  ↓
M1-008 Telemetry
  ↓
M1-014 Profile / TeamPerformance
  ↓
M1-015 ScenarioConfig / AED Fairness
  ↓
M1-020 Test Strategy / Experiment Protocol
```

Stalker regression phụ thuộc M1-013. GenAI test boundary phụ thuộc M1-019.

---

# 4. Test Architecture

Canonical architecture được M1-020 giữ nguyên:

```text
Gameplay Runtime
        ↓
TelemetryEvent
        ↓
MatchTelemetry
        ↓
MatchScore
        ↓
PlayerAIProfile / TeamProfile / TeamPerformance
        ↓
AED Input Gate
        ↓
AED Policy
        ↓
Candidate ScenarioConfig
        ↓
Scenario Validator
        ↓
Applied ScenarioConfig
        ↓
Traditional Gameplay AI
```

GenAI nằm ở presentation branch riêng:

```text
Validated ScenarioConfig
+
Designer Content Registry
        ↓
Trusted Mission Facts Resolver
        ↓
MissionBriefingFacts
        ↓
Backend / GenAI Adapter
        ↓
LLM Provider
        ↓
MissionBriefingValidator
        ↓
UI / Presentation Only
```

Không tồn tại test expectation hợp lệ nào dựa trên các luồng sau:

```text
GenAI → Monster FSM
Telemetry → direct Monster command
Profile → CHASE / ATTACK / SEARCH
AED → ATTACK
AED → CurrentTarget / DetectionTarget
AED → LastKnownPosition
AED → hidden Player position
MissionBriefingOutput → ScenarioConfig
```

## 4.1. Test Boundary Principle

Mỗi test phải xác định rõ **system under test (SUT)** và boundary:

- perception facts không đồng nghĩa target eligibility;
- raw telemetry không đồng nghĩa profile score;
- profile input không đồng nghĩa Adaptive eligibility;
- candidate config không đồng nghĩa Applied config;
- GenAI output không đồng nghĩa gameplay authority.

---

# 5. Test Objectives

M1-020 định nghĩa các test objective sau:

1. **Contract conformance:** behavior đúng frozen contract.
2. **Boundary safety:** không subsystem nào vượt authority boundary.
3. **Data correctness:** telemetry/profile data có schema, ownership, availability và eligibility đúng.
4. **Fairness enforcement:** Adaptive candidate bị giới hạn bởi whitelist, bounds, timing, pressure rules và Scenario Validator.
5. **Fallback safety:** FixedDirector/fallback tạo hoặc giữ safe config đúng decision point.
6. **Regression safety:** ScenarioConfig không phá Traditional AI/Stalker FSM contract.
7. **End-to-end traceability:** match outcome có thể trace qua telemetry/profile/config/evidence.
8. **Reproducibility:** deterministic parts tái tạo được với cùng input/version/config.
9. **Experiment validity:** Fixed và Adaptive khác nhau chỉ ở adaptive-authorized decisions, không khác core gameplay/content baseline ngoài protocol.
10. **Research integrity:** không fake TeamPerformance, không fake sample, không fake significance/result.

---

# 6. Test Levels

## 6.1. Levels

### Contract Test

Kiểm tra logical behavior frozen của từng M1 contract độc lập với implementation technology cụ thể.

### Unit / Component Test

Kiểm tra một component có input/output/guard rõ, ví dụ Vision Sensor, Target Selection, Scenario Validator, Profile formula, MissionBriefingValidator.

### Integration Test

Kiểm tra nhiều component trao đổi đúng contract, ví dụ Vision → Target Selection → FSM hoặc Telemetry → MatchScore → Profile.

### System / E2E Test

Kiểm tra full match flow và hậu xử lý từ Lobby tới Match End/Telemetry/Profile.

### Experiment Readiness Test

Kiểm tra điều kiện để một match/condition được phép tham gia Fixed-vs-Adaptive experiment.

### Experiment Execution

Chỉ thực hiện ở milestone đủ implementation/data và chỉ khi Adaptive Readiness Gate PASS. M1-020 **không thực thi bước này**.

## 6.2. Test Level Matrix

| Test Area | Unit/Contract | Integration | System | Experiment | Current M1 Executable? |
|---|---:|---:|---:|---:|---|
| Traditional AI | Yes | Yes | Yes | Regression control | **YES — contract/design; runtime depends implementation availability** |
| Stalker | Yes | Yes | Yes | Regression control | **YES — contract/design; runtime depends implementation availability** |
| Telemetry | Yes | Yes | Yes | Data source | **YES — schema/contract; ingest/runtime depends implementation availability** |
| Profile | Yes | Yes | Post-match | Metric/input support | **PARTIAL — ACTIVE formulas only** |
| AED Input Gate | Yes | Yes | Yes | Readiness gate | **YES — contract/negative-path testable** |
| Scenario Validator | Yes | Yes | Yes | Safety gate | **YES — contract/design; runtime depends implementation availability** |
| FixedDirector | Yes | Yes | Yes | Condition F | **YES — subject to fixed config implementation** |
| Adaptive AED | Yes | Yes | Future | Condition A | **Contract/design YES; live gameplay execution NOT READY** |
| GenAI | Yes | Yes | Presentation E2E | Controlled/non-gameplay | **YES — boundary/fallback contract; implementation dependent** |
| Full Match | N/A | Yes | Yes | Experimental unit source | **Fixed-path: READY subject to build; Adaptive live: NOT READY** |

---

# 7. Test Environment & Versioning

## 7.1. Environment Principle

M1-020 không freeze hardware, device count, network profile hoặc test runner chưa được source chốt.

```text
Exact test environment values
→ CONFIGURATION-OWNED / TBD before execution
```

Mỗi execution phải lưu environment identity đủ để reproduce/debug, tối thiểu theo khả năng implementation:

- game/build version;
- map/content package version;
- test environment identifier;
- match ID;
- test run / experiment run ID nếu implementation có;
- team roster identity ở mức project cho phép;
- experiment condition;
- ScenarioConfig/config versions;
- telemetry/profile/AED versions liên quan.

## 7.2. Required Version Trace

| Version / Identity | Requirement |
|---|---|
| `game/build version` | Required for test/experiment evidence |
| `telemetry schemaVersion` | Current frozen serialized baseline: `1.0` |
| `profileFormulaVersion` | Current frozen baseline: `1.0` |
| `matchScoreFormulaVersion` | Must be traceable when profile computation used |
| `normalizationConfigVersion` | Required for thresholds/filter/weights resolution |
| `scenarioConfigVersion` | Required for Fixed/Adaptive config trace |
| `policyVersion` | Required for both FIXED and ADAPTIVE ScenarioConfig |
| `parameterRegistryVersion` | Required for adaptive bounds/timing/pressure metadata |
| `contentWhitelistVersion` | Required for adaptive content compatibility |
| `fallbackConfigId` / version | Required when fallback used |
| Fixed experiment baseline ID/version | **TBD / configuration-owned before experiment** |
| `experimentProtocolVersion` | `M1-020-v0` or versioned successor |
| GenAI briefing contract/prompt/validation/generation versions | Required when GenAI test evidence is relevant |

## 7.3. Version Mixing Rule

Không gộp các match vào cùng confirmatory comparison nếu build/config/schema/policy khác nhau theo cách làm thay đổi behavior hoặc metric semantic mà analysis protocol chưa cho phép.

Version mismatch phải được:

```text
record
→ classify
→ exclude hoặc stratify theo protocol đã pre-freeze
```

Không silently pool incompatible versions.

---

# 8. Contract Test Strategy

M1-020 không copy toàn bộ test case từ frozen documents; nó gom các logical contract thành test strategy chung.

| Contract | Primary Boundary | Test Level | Core Expected Result | Primary Owner Boundary |
|---|---|---|---|---|
| M1-007 AI Architecture | Traditional AI vs AED vs GenAI | Contract + Integration | Không cross-authority; Monster runtime rule-based, AED config-only, GenAI presentation-only | C — AI/Telemetry/Research |
| M1-008 Telemetry | Gameplay outcome → TelemetryEvent | Contract + Component + Integration | Schema/ownership/version/reason valid; telemetry không điều khiển AI | C + Backend integration |
| M1-013 Stalker | Vision/Target/FSM/LKP/Attack | Component + Integration + Regression | LOS/target/state transitions đúng; no Hearing; no omniscient tracking | C — AI |
| M1-014 Profile | Telemetry aggregate → MatchScore/Profile/TeamPerformance | Component + Integration | Availability/eligibility/null/EMA/normalization đúng; no synthetic metric | C + Backend/Data |
| M1-015 Scenario/AED | Profile → Gate → Policy → Validator → Config | Contract + Component + Integration + System | Whitelist/bounds/timing/fairness/atomicity/fallback đúng | C + Gameplay config owner |
| M1-019 GenAI | Trusted facts → LLM → Validator → UI | Component + Integration + System | No invented authority; timeout/cache/template fallback; non-blocking gameplay | C + D Backend + UI |

## 8.1. Cross-Contract Invariants

Các invariant phải có regression test khi các component kết nối:

```text
Telemetry validity
≠ Profile eligibility
≠ Experiment eligibility
```

```text
Configurable parameter
≠ Adaptive-authorized parameter
```

```text
MissionBriefingOutput
≠ Gameplay input
```

```text
Candidate ScenarioConfig
≠ Applied ScenarioConfig
```

---

# 9. Traditional AI / Stalker Test Strategy

## 9.1. Component Scope

### Vision Sensor

Test:

- distance boundary theo configured `VisionDistance`;
- full-cone semantic `VisionAngle`, dùng half-angle khi evaluate;
- wall/architecture/Closed Door block LOS;
- Open Door không tự block LOS nếu không có blocker khác;
- output chỉ là physical `VisibleObservations[]`;
- không lọc gameplay target eligibility;
- không đọc Telemetry hoặc Runtime NoiseEvent để detect Stalker target.

### Target Selection

Test:

- chỉ chọn từ valid `VisibleObservations[]`;
- filter `Downed`, `DEAD / Soul` và target-invalid states;
- nearest visible eligible Player khi acquire mới;
- DetectionTarget lock trong DETECT;
- không switch CurrentTarget chỉ vì Player khác gần hơn;
- target invalid được clear theo contract.

### Detection Meter

Test:

- target mới → meter = 0;
- visible → fill theo configured rate;
- mất LOS → decay;
- decay về 0 mới release target;
- FULL → promote CurrentTarget → CHASE;
- meter không carry giữa Player.

### Last Known Position

Test:

- chỉ update từ valid Vision Observation của CurrentTarget;
- sau mất LOS giữ last observed position;
- không lấy hidden transform/network-known position;
- Telemetry/AED/Navigation không update LKP.

### FSM / Attack / Recover

Frozen Stalker states dùng cho test:

```text
PATROL
DETECT
CHASE
ATTACK
RECOVER
SEARCH
```

Test:

- PATROL → DETECT qua valid target acquisition;
- DETECT → CHASE khi meter FULL;
- CHASE mất LOS → SEARCH ngay;
- Navigation tới LKP là behavior bên trong SEARCH, không phải state mới;
- SEARCH thấy lại same CurrentTarget → CHASE;
- SEARCH chỉ thấy Player khác khi old target hidden → DETECT Player mới;
- Attack phải có wind-up → hit moment → RECOVER;
- target invalid tại Hit Moment → no damage;
- target ngoài Attack Hit Range tại Hit Moment → MISS;
- RECOVER bắt buộc, không bypass;
- target invalid trong ATTACK/RECOVER không tạo stale active target;
- Final Hunt không tạo Stalker FSM state mới.

## 9.2. Traditional AI Regression Under ScenarioConfig

Mọi Fixed/Adaptive config test phải xác minh:

- Vision/LOS vẫn đúng;
- Detection Meter vẫn đúng;
- Target Selection không bypass;
- LKP không bị AED sửa;
- Search/Attack/Recover transition giữ nguyên;
- Downed/invalid target behavior giữ nguyên;
- Final Hunt chỉ là phase/configuration context;
- Stalker không có Hearing;
- AED không command FSM state.

Adaptive chỉ có quyền trên keys được M1-015 freeze, không trên toàn bộ configurable Stalker parameters.

---

# 10. Telemetry Test Strategy

## 10.1. Common Schema Validation

M1-008 serialized baseline:

```text
schemaVersion = "1.0"
```

Validator test tối thiểu:

- `id` required;
- `matchId` required;
- `eventType` required và active/supported;
- `ts` parseable UTC timestamp;
- `valueJson` required;
- `valueJson.context` là object;
- `valueJson.data` là object;
- `schemaVersion` required/supported;
- `userId` đúng ownership semantic;
- `reasonCode` đúng allowed controlled set khi required;
- required event payload đầy đủ;
- reserved/not-emitted event bị reject ở current schema version.

## 10.2. `userId` Ownership

Test phải phân biệt:

- Player-specific event → `userId` là Player subject/owner theo contract;
- system/objective/phase event → `userId = null` khi contract yêu cầu;
- `NOISE_EMITTED` player source → Player `userId`;
- environment/system noise không resolve Player → `userId = null`.

## 10.3. `reasonCode`

Test:

- controlled enum/code;
- `UPPER_SNAKE_CASE`;
- không free text;
- không thay thế `eventType`;
- `null` chỉ khi event contract cho phép;
- invalid code → reject.

## 10.4. Lifecycle Events

Canonical lifecycle:

```text
MATCH_STARTED
MATCH_ENDED
PHASE_STARTED
PHASE_COMPLETED
```

Test:

- phase start/completed pairing;
- match boundary pairing;
- `MATCH_STARTED` chứa `valueJson.context.scenarioConfigVersion` theo schema v1.0;
- phase metrics không double-count bằng reserved specialized lifecycle events;
- `SECURITY_HOLD_INTERRUPTED` là interruption semantic, không thay phase lifecycle source-of-truth.

## 10.5. Duplicate / Idempotency

M1-008 yêu cầu event ID ổn định và cùng event `id` được retry không tạo hai logical telemetry records giống nhau.

Test strategy:

```text
same event id resent
→ no duplicate logical record
```

Transport/retry queue implementation chi tiết nằm ngoài M1-020.

## 10.6. Noise Separation

Phải test hai luồng độc lập:

```text
Runtime NoiseEvent
→ Noise System
→ Hearing Sensor
→ Listener AI
```

và:

```text
Runtime NoiseEvent
→ Telemetry Emitter
→ NOISE_EMITTED
→ Backend / Storage
```

Negative test bắt buộc:

```text
Telemetry DB / TelemetryEvent
→ Listener Hearing
```

phải **không tồn tại**.

Đối với Stalker:

```text
Runtime NoiseEvent
→ no DetectionTarget
→ no CurrentTarget
→ no Stalker FSM transition
```

## 10.7. Metric Source Validation

Chỉ metric có active source contract được dùng:

- Match Duration ← `MATCH_STARTED + MATCH_ENDED`;
- Phase Duration ← `PHASE_STARTED + PHASE_COMPLETED`;
- objective time ← objective-bearing phase pairs;
- Down Count ← valid `PLAYER_DOWNED` count;
- Revive Count ← valid `PLAYER_REVIVED` count;
- Eliminated Count ← `PLAYER_ELIMINATED`;
- Escape/Survival ← `PLAYER_ESCAPED` + match outcome/survivor count semantics;
- Noise Count/By Type/By Phase ← `NOISE_EMITTED`.

`Rescue Count` và metric phụ thuộc `PLAYER_RESCUED` **không ACTIVE trong current v1.0 baseline**.

---

# 11. Profile / TeamPerformance Test Strategy

## 11.1. Eligibility vs Availability

Frozen distinction:

```text
MetricAvailability
= AVAILABLE | UNAVAILABLE
```

```text
MatchProfileEligibility
= ELIGIBLE | INELIGIBLE
```

Test bắt buộc:

- `UNAVAILABLE` → score/component `null`;
- không `null → 0`;
- absence of event không tự bằng zero;
- `AVAILABLE + observed zero` mới là valid zero;
- `MATCH_ENDED.reasonCode = MATCH_ABORTED` → `INELIGIBLE`;
- ineligible match không update persistent PlayerAIProfile;
- ineligible match không tăng sampleCount;
- ineligible match không persist TeamProfile như valid result;
- ineligible match → TeamPerformance `INCOMPLETE/null`.

## 11.2. Current Player Profile Status

Current ACTIVE Player dimensions:

```text
survival
noise
```

Current DEFERRED Player dimensions:

```text
objective
teamwork
exploration
navigation
toolUsage
risk
revive
```

M1-020 không test invented formulas cho DEFERRED dimensions.

## 11.3. Survival MatchScore

Representative component tests:

```text
eligible + valid PLAYER_ESCAPED
→ survival MatchScore = 100
```

```text
eligible + valid PLAYER_ELIMINATED
→ survival MatchScore = 0
```

```text
contradictory terminal outcome
→ survival = null
→ aggregation/validation error
→ no profile update
```

## 11.4. Noise MatchScore / Normalization

Profile signal:

```text
ProfileNoisePenaltyCount
=
count(valid player-attributed NOISE_EMITTED matching ProfileNoiseFilter)
```

`ProfileNoiseFilter` và normalization thresholds là configurable/versioned.

Test:

- matching event count đúng;
- non-matching noise không trở thành penalty;
- `AVAILABLE` + no matching event → valid zero penalty count;
- `UNAVAILABLE` → score null;
- invalid filter/config → score null + config validation error;
- không hard-code “mọi noise đều xấu”.

## 11.5. EMA

Frozen formula:

```text
newScore_d
=
(1 - alpha_d) * oldScore_d
+
alpha_d * matchScore_d
```

Constraint:

```text
0 < alpha_d <= 1
```

Numerical `alpha_d` = CONFIGURABLE.

Test:

- cold start ACTIVE dimension = `50 / COLD_START / sampleCount 0`;
- first valid score dùng EMA từ 50, không overwrite đặc biệt;
- non-null eligible MatchScore → EMA + sampleCount increment;
- null → giữ score/status/sampleCount;
- DEFERRED → null và không update EMA.

## 11.6. TeamProfile

Frozen v1.0:

```text
TeamProfile
→ match-scoped
→ teamKey = matchId
```

Test:

- cùng roster ở hai match khác nhau → hai TeamProfile khác nhau;
- không cross-match merge;
- không dùng previous-match field để lấp missing current match;
- `objectiveTime` ACTIVE khi required phase pairs valid;
- interruption Security Hold vẫn nằm trong elapsed wall-clock objectiveTime;
- các team field deferred vẫn null.

## 11.7. TeamPerformance Completeness

Required components:

```text
ObjectiveSpeed
Survival
Teamwork
ResourceEfficiency
```

Weights:

```text
weight >= 0
sum(weights) = 1
exact numerical weights = CONFIGURABLE / VERSIONED
```

Current baseline:

```text
Teamwork = DEFERRED
ResourceEfficiency = DEFERRED
→ TeamPerformance.status = INCOMPLETE
→ TeamPerformance.score = null
```

Forbidden test expectations:

```text
missing component = 0
```

```text
renormalize remaining weights
```

```text
use unrelated Player metric as replacement
```

```text
reuse previous-match TeamProfile component
```

---

# 12. ScenarioConfig / AED Test Strategy

## 12.1. Adaptive Input Gate

Adaptive policy may evaluate only if all required conditions are valid:

```text
TeamPerformance.status = COMPLETE
AND TeamPerformance.score != null
AND required Profile input valid
AND required input versions supported
AND policyVersion supported
AND parameter registry valid
AND content whitelist valid
AND policy config valid
```

Nếu một điều kiện fail:

```text
Adaptive policy MUST NOT run
→ safe fallback by decisionPoint
```

Current baseline phải test `INCOMPLETE → NOT_EVALUATED/FIXED_FALLBACK`, không fake COMPLETE.

## 12.2. ScenarioConfig Validation

M1-020 kiểm tra ScenarioConfig theo M1-015, gồm:

- supported `scenarioConfigVersion`;
- `policyVersion` required cho cả FIXED và ADAPTIVE;
- explicit schema, không free-form arbitrary parameter object;
- `configSource = FIXED | ADAPTIVE`;
- valid content identifiers;
- whitelist/timing/bounds/fairness;
- fallback semantics.

## 12.3. Adaptive Stalker Whitelist

Adaptive AED v0 chỉ được modify:

```text
DetectionFillRate
DetectionDecayRate
ChaseSpeed
SearchDuration
```

Explicit non-adaptive Stalker keys:

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

Unknown key → reject.

## 12.4. Parameter Bounds

Mọi adaptive numerical key phải có:

```text
defaultValue
minValue
maxValue
allowedTiming
pressureAxis
owner
```

Rule:

```text
minValue <= defaultValue <= maxValue
```

Out-of-bound candidate:

```text
→ INVALID
→ no silent clamp
```

Nếu source không freeze exact numerical bound:

```text
CONFIGURABLE / VERSIONED
```

Current source-supported Final Hunt bound:

```text
45 <= FinalHunt.EscapeDoorTimer <= 60 seconds
```

## 12.5. Timing

Controlled decision points:

```text
PRE_MATCH
ALLOWED_PHASE_BOUNDARY
FINAL_HUNT_SETUP
```

No arbitrary realtime adaptation.

Specific tests:

- `routeModifier` at PRE_MATCH → `TIMING_REJECTED`;
- `routeModifier` at ALLOWED_PHASE_BOUNDARY → may proceed if all validation pass;
- Final Hunt timer only at `FINAL_HUNT_SETUP` before timer starts;
- running Final Hunt timer immutable;
- no per-frame/per-second policy changes;
- no reactive adaptation directly because Player just Downed/noise occurred.

## 12.6. Pressure Axis

Frozen aggressive directions:

```text
DetectionFillRate ↑ → DetectionPressure MORE_AGGRESSIVE
DetectionDecayRate ↓ → DetectionPressure MORE_AGGRESSIVE
ChaseSpeed ↑ → ChasePressure MORE_AGGRESSIVE
SearchDuration ↑ → SearchPressure MORE_AGGRESSIVE
```

`SupportItemBudget` và `FinalHunt.EscapeDoorTimer` có `pressureAxis = NONE`, nhưng vẫn phải pass owner-specific fairness.

Hard tests:

```text
DetectionFillRate ↑ + DetectionDecayRate ↓
→ reject
```

```text
ChaseSpeed ↑ + SearchDuration ↑
→ reject
```

Tại `ALLOWED_PHASE_BOUNDARY`:

```text
changed keys <= 1–2
more-aggressive Stalker pressure axes <= 1
```

## 12.7. Spawn / Route / Support / Final Hunt Safety

### Objective Spawn

- designer-authored whitelist only;
- map compatible;
- reachable;
- no duplicate-invalid placement;
- no impossible traversal;
- no soft-lock;
- no arbitrary generated coordinate.

### Route

- whitelist only;
- map/scenario compatible;
- at least one legal route remains;
- objective reachable;
- exit reachable;
- no soft-lock;
- no teleport;
- no hidden Player location.

### Support Item Budget

- bounded;
- timing valid;
- future allocation only at phase-boundary change;
- no retroactive removal of Player-owned/already-spawned item;
- no direct arbitrary item injection.

### Final Hunt Timer

- `[45,60]` seconds;
- resolve at `FINAL_HUNT_SETUP`;
- before active timer starts;
- immutable after start;
- no generic objective-timing permission.

## 12.8. Atomic Apply

```text
A valid + B invalid
→ reject A
→ reject B
→ apply neither
```

No partial apply, no silent repair.

## 12.9. Adaptive Decision Result Semantics

Candidate validation status:

```text
NOT_EVALUATED | VALID | INVALID
```

Final result:

```text
APPLIED | NO_CHANGE | FIXED_FALLBACK
```

`REJECTED` không phải final AdaptiveDecision result.

---

# 13. FixedDirector & Fallback Test Strategy

## 13.1. FixedDirector Role

`FixedDirector` là safe non-adaptive path, không phải Monster AI.

### PRE_MATCH

```text
failure/ineligible Adaptive path
→ FIXED_FALLBACK
→ FULL_FIXED_CONFIG
→ resolve designer-authored/versioned fixed config
```

M1-015 full fallback reference:

```text
FIXED_BASELINE_V1
```

### MID_MATCH / ALLOWED_PHASE_BOUNDARY

```text
adaptive failure
→ FIXED_FALLBACK
→ KEEP_LAST_VALID_CONFIG
```

Không blanket-replace live ScenarioConfig bằng full baseline.

### FINAL_HUNT_SETUP

```text
adaptive failure before timer start
→ KEEP_LAST_VALID_CONFIG
→ retain valid current/base timer value
→ no unrelated field replacement
```

## 13.2. Failure / Fallback Matrix

| Failure | Candidate Status | PRE_MATCH Expected | MID_MATCH / Boundary Expected |
|---|---|---|---|
| AED unavailable | `NOT_EVALUATED` | `FIXED_FALLBACK + FULL_FIXED_CONFIG` | `FIXED_FALLBACK + KEEP_LAST_VALID_CONFIG` |
| AED timeout before valid candidate | `NOT_EVALUATED` | Full fixed fallback | Keep last valid config |
| TeamPerformance `INCOMPLETE` | `NOT_EVALUATED` | Full fixed fallback | Keep last valid config if evaluated at later allowed point |
| TeamPerformance score `null` | `NOT_EVALUATED` | Full fixed fallback | Keep last valid config |
| Unsupported required version | `NOT_EVALUATED` | Full fixed fallback | Keep last valid config |
| Invalid required Profile input | `NOT_EVALUATED` | Full fixed fallback | Keep last valid config |
| Invalid parameter registry | `NOT_EVALUATED` / pre-candidate failure | Full fixed fallback | Keep last valid config |
| Invalid content whitelist | `NOT_EVALUATED` / pre-candidate failure | Full fixed fallback | Keep last valid config |
| Invalid candidate | `INVALID` | Full fixed fallback | Keep last valid config |
| Scenario Validator fail | `INVALID` | Full fixed fallback | Keep last valid config |

> Fallback là **expected safety behavior**, không tự động là “test failure”. Test PASS khi fallback xảy ra đúng contract và không mutate forbidden runtime state.

## 13.3. Mid-Match Preservation Tests

Fallback mid-match không được:

- reset match;
- reset Player state;
- reset objective progress;
- teleport Player/Monster;
- reset Stalker FSM;
- command FSM transition;
- rollback previous valid adaptive change automatically;
- modify active Attack;
- retroactively modify active Final Hunt timer.

## 13.4. Fixed Baseline Identity

M1-015 freezes `FIXED_BASELINE_V1` as PRE_MATCH fallback identity. Tuy nhiên **research Condition F baseline identity/version phải được explicitly frozen cho experiment execution**.

M1-020 không tự giả định mọi experiment Fixed match dùng đúng cùng fallback object nếu M5 implementation tạo nhiều designer-authored fixed baselines theo monster/scenario.

```text
Research Fixed baseline exact values/ID
→ configuration-owned / TBD before main experiment
```

---

# 14. GenAI Test Strategy

GenAI test chỉ nằm trong M1-019 boundary.

## 14.1. Valid Flow

```text
Validated ScenarioConfig
+ Designer Content Registry
→ Trusted Mission Facts
→ GenAI Adapter
→ candidate text
→ MissionBriefingValidator
→ UI
```

Test:

- valid facts produce allowable candidate;
- output non-empty;
- requested/allowed language;
- configured length limit;
- no invented authoritative facts;
- valid output → `source=GENAI`, `status=VALID`.

## 14.2. Negative / Safety

Reject candidate nếu invent:

- gameplay stat;
- Stalker/AED parameter;
- new objective count absent from trusted facts;
- map/layout;
- spawn coordinate;
- route graph;
- FSM/Target command;
- hidden Player position;
- item ability/power;
- reward/economy/payment value;
- gameplay code used as authority.

## 14.3. Cache

M1-019 uses `CACHE-FIRST`.

Test:

- compatible validated cache → return cache, provider call not required;
- incompatible facts/config/language/contract/prompt/validation version → cache miss;
- invalid candidate never becomes valid reusable cache artifact.

## 14.4. Timeout / Retry / Fallback

`timeoutMs` và `maxRetryCount`:

```text
CONFIGURABLE / VERSIONED
```

Không invent exact values.

Test:

- timeout finite;
- retry finite;
- retry exhaustion → deterministic template fallback;
- provider unavailable → template fallback;
- template fallback LLM-independent, compatible và presentation-only;
- missing/invalid template fallback → packaging/configuration error;
- GenAI không invent emergency authoritative gameplay facts.

## 14.5. Match Start Non-Blocking

For correctly packaged supported P0 content:

```text
GenAI failure
≠ match failure
```

Cache miss/provider timeout/invalid output/retry exhaustion không được block normal match start khi valid template fallback tồn tại.

## 14.6. Experiment Isolation

GenAI **không** được dùng làm uncontrolled gameplay variable trong Fixed-vs-Adaptive experiment.

Preferred experiment control:

- same Mission Briefing contract/content handling policy across F and A;
- briefing output remains presentation-only;
- GenAI availability/fallback must not change gameplay condition classification.

Nếu nhóm muốn nghiên cứu GenAI variation riêng, đó là protocol khác ngoài M1-020 v0.

---

# 15. Integration Test Strategy

## 15.1. Stalker Perception Integration

```text
Vision Sensor
→ VisibleObservations
→ Target Selection
→ Stalker FSM
```

Verify:

- physical visibility does not bypass target eligibility;
- Downed/Dead visible observation not selected;
- DETECT required before initial CHASE;
- LKP receives only valid observed position;
- no noise-to-Stalker side channel.

## 15.2. Telemetry / Profile Integration

```text
Runtime Gameplay
→ TelemetryEvent
→ MatchTelemetry
→ MatchScore
→ PlayerAIProfile / TeamProfile / TeamPerformance
```

Verify:

- schema-valid events aggregate to correct active metrics;
- availability set correctly;
- aborted/ineligible match does not persist profile update;
- ACTIVE formulas reproduce;
- DEFERRED metrics remain null;
- TeamPerformance completeness rules hold.

## 15.3. Profile / AED Integration

```text
Profile / TeamPerformance
→ AED Input Gate
→ AED Policy
→ Scenario Validator
→ ScenarioConfig
```

Current baseline integration should emphasize invalid/incomplete input path:

```text
TeamPerformance INCOMPLETE
→ policy MUST NOT run
→ safe fallback
```

Future COMPLETE-path integration is gated by Section 19.

## 15.4. ScenarioConfig / Traditional AI Integration

```text
Applied ScenarioConfig
→ Gameplay Configuration Layer
→ Traditional Gameplay AI
```

Verify:

- only validated config applied;
- adaptive-authorized parameter values consumed without changing FSM topology;
- no direct state/target/LKP command;
- non-adaptive combat/perception envelope remains unchanged.

## 15.5. ScenarioConfig / GenAI Integration

```text
Validated ScenarioConfig
→ Trusted Mission Facts
→ GenAI Adapter
→ MissionBriefingValidator
→ UI
```

Verify:

- facts deterministic from authoritative sources;
- raw ScenarioConfig/internal state not dumped to LLM;
- generated text never flows back to gameplay config.

---

# 16. System / End-to-End Test Strategy

Minimum system flow:

```text
Lobby
→ Match Start
→ Core
→ Power Puzzle
→ Security Hold
→ Final Hunt
→ Match End
→ Telemetry
→ Profile processing
```

## 16.1. Match Gameplay Baseline

E2E test must preserve source gameplay:

- lobby 2–4 players;
- Research Facility P0 scope;
- 3 Energy Core;
- Power Puzzle;
- Security Hold;
- Final Hunt;
- result/profile processing.

M1-020 không add mechanic để thuận tiện test.

## 16.2. E2E Evidence Points

At minimum capture:

- build/version;
- match ID;
- scenario config + source;
- phase lifecycle telemetry;
- match-start/match-end telemetry;
- Player terminal outcomes;
- Profile processing output/status;
- fallback/adaptive decision evidence if applicable.

## 16.3. Current Baseline E2E Rule

Adaptive không bắt buộc phải run trong current M1 E2E.

Current correct behavior có thể là:

```text
Lobby
→ AED Input Gate detects TeamPerformance INCOMPLETE
→ safe Fixed path
→ full match
→ telemetry/profile processing
```

Đây là **correct contract behavior**, không phải degraded fake experiment.

---

# 17. Fixed vs Adaptive Research Question

Research question v0:

> **Trong cùng gameplay scope và cùng content constraints, Adaptive AED có tạo trải nghiệm và/hoặc hiệu suất chơi khác FixedDirector hay không?**

Câu hỏi không giả định Adaptive tốt hơn.

M1-020 chỉ thiết kế protocol để thu evidence cho câu hỏi này; không trả lời research question trước khi experiment thực tế được chạy và phân tích.

---

# 18. Experimental Conditions

## 18.1. Condition F — Fixed

```text
configSource = FIXED
```

Condition F sử dụng designer-authored, known-valid, versioned FixedDirector baseline.

Exact numerical values:

```text
configuration-owned / TBD before execution
```

Không invent arbitrary fixed values trong M1-020.

## 18.2. Condition A — Adaptive

```text
configSource = ADAPTIVE
```

Condition A chỉ hợp lệ nếu:

```text
Adaptive Readiness Gate = PASS
```

Adaptive chỉ được tạo thay đổi thuộc M1-015 authority và pass Scenario Validator.

## 18.3. Controlled Constants Across F and A

Cả hai condition phải giữ nguyên trong paired/comparable execution:

- gameplay rules;
- Research Facility/core map scope dùng cho comparison;
- monster identity trong matched comparison;
- objective chain;
- content whitelist universe;
- build/version;
- telemetry schema;
- Profile formula/config version dùng cho metric processing;
- test environment;
- mission briefing gameplay authority boundary;
- experiment protocol version.

Chỉ khác:

```text
adaptive-authorized ScenarioConfig decisions
```

được M1-015 cho phép.

## 18.4. Same Core Content Requirement

QA-04/AED experiment baseline yêu cầu Fixed và Adaptive dùng **same core content**. Map 2 không cần được đưa vào experiment P0 nếu Conditional scope chưa được activate.

---

# 19. Adaptive Readiness Gate

## 19.1. Gate Definition

Adaptive experiment execution chỉ được `READY` nếu tất cả điều kiện tối thiểu sau PASS:

| Gate Item | PASS Requirement |
|---|---|
| TeamPerformance status | `COMPLETE` |
| TeamPerformance score | non-null |
| Required Profile inputs | Valid and resolvable |
| Match/Profile formula/config versions | Supported |
| Policy config | Valid |
| `policyVersion` | Supported |
| Parameter registry | Valid + supported version |
| Adaptive bounds/default/timing metadata | Complete/valid |
| Content whitelist | Valid + compatible |
| Scenario Validator | Operational and validated |
| Fixed fallback | Known-valid/versioned and resolvable |
| Telemetry required for experiment | Available and validated |
| Experiment condition logging | Correct and traceable |
| Build/config lock for comparison | Frozen for the experimental run |
| Data/evidence pipeline | Able to retain required artifacts |

Nếu bất kỳ item nào FAIL:

```text
Adaptive experiment execution = NOT READY
```

## 19.2. No Synthetic Readiness

Forbidden:

```text
Teamwork missing → 0
ResourceEfficiency missing → 0
```

```text
remove missing weights → renormalize others
```

```text
survival/noise alone → synthetic TeamPerformance
```

```text
fake telemetry/profile → make gate PASS
```

## 19.3. Current M1 Gate Result

Current frozen profile baseline:

```text
Teamwork = DEFERRED
ResourceEfficiency = DEFERRED
→ TeamPerformance.status = INCOMPLETE
→ TeamPerformance.score = null
```

Therefore:

```text
Adaptive Readiness Gate = FAIL
Live Adaptive experiment = NOT READY
```

Immediate blocking reason:

```text
TeamPerformance.status = INCOMPLETE
TeamPerformance.score = null
```

Additional execution prerequisites vẫn phải được hoàn tất trước live Fixed-vs-Adaptive execution; xem **Section 35.2 — Non-Conflict TBDs**. Việc còn các execution prerequisites này **không** làm thay đổi `M1-020 document / protocol = DONE / FROZEN` ở mức Test Strategy + Experiment Protocol v0.

---

# 20. Hypotheses

## H0 — Null Hypothesis

> Fixed và Adaptive không tạo khác biệt có ý nghĩa theo analysis protocol đã pre-freeze trên các metric experiment hợp lệ.

## H1 — Alternative Hypothesis

> Fixed và Adaptive tạo khác biệt trên ít nhất một metric experiment hợp lệ theo analysis protocol đã pre-freeze.

M1-020 không freeze directional claim kiểu:

```text
Adaptive > Fixed
```

vì source chưa cung cấp evidence để khẳng định direction.

Numerical effect size target, significance threshold và statistical power:

```text
TBD
```

cho tới khi analysis plan được freeze trước main experiment.

---

# 21. Experimental Unit

## 21.1. Primary Experimental Unit

M1-020 chọn:

```text
Experimental Unit = team-match
```

Lý do:

1. Một match có một experiment condition/config path chung ở level scenario.
2. TeamProfile/TeamPerformance hiện là current-match result.
3. Nhiều Player trong cùng match cùng chịu shared scenario, phase progression và team interaction.
4. Coi từng Player trong cùng team-match là independent sample sẽ tạo pseudo-replication nếu analysis không mô hình hóa nested/repeated structure.

## 21.2. Player-Match Data

`player-match` vẫn là observation level hợp lệ cho player-specific telemetry/Profile metric, nhưng:

```text
player-match observations within same team-match
≠ automatically independent experimental units
```

Player-level analysis phải:

- giữ `team-match` grouping;
- xử lý repeated Player/team exposure theo analysis plan;
- không tăng sample count giả bằng cách coi mọi Player row là độc lập.

## 21.3. Paired Unit for Crossover

Khi cùng roster/team thực hiện cả F và A, primary comparison có thể hình thành một **paired team-condition set**. Pair eligibility phải được kiểm tra per metric và per protocol.

---

# 22. Assignment / Counterbalancing

## 22.1. Chosen Strategy

Phù hợp QA-04 planning baseline, M1-020 chọn:

```text
within-team crossover
+
counterbalanced condition order
```

Hai sequence logic:

```text
F → A
A → F
```

Mục tiêu không phải tạo thêm gameplay, mà giảm bias do order/learning/familiarity.

## 22.2. Assignment Rule

- cùng team/roster nên giữ nhất quán trong paired comparison khi operationally feasible;
- sequence allocation phải được lưu trong experiment metadata;
- cách chọn sequence có thể randomized nếu experiment implementation hỗ trợ, nhưng **randomization mechanism chưa được project source freeze**;
- nếu không randomized, phải dùng một balanced/predefined sequence allocation và document rõ;
- không đổi condition sau khi thấy outcome để “cân dữ liệu”.

## 22.3. Learning / Order / Familiarity Control

Control bằng:

- counterbalanced order;
- record exposure/session order;
- same core content/build;
- same map scope;
- same monster identity trong matched comparison;
- same telemetry/version baseline;
- paired/repeated analysis thích hợp;
- report order/sequence effect như confounder/limitation.

## 22.4. Profile Evolution Between Matches

Profile có thể update sau match, vì vậy repeated exposure có thể thay input của Adaptive condition.

M1-020 **không tự freeze Profile** hoặc disable update, vì source không cho phép test strategy thay lifecycle chỉ để thuận tiện experiment.

Required evidence cho mỗi Adaptive match:

```text
exact pre-match Profile/TeamPerformance input snapshot/reference
+ versions
```

Cách resolve historical/team input cho new match phải theo future valid contract; xem Open Issue `SC-03`.

---

# 23. Metrics

Chỉ activate metric có source contract. Metric chưa đủ semantic phải `DEFERRED/TBD`, không invent formula.

## 23.1. Primary Metrics

| Metric | Level | Source / Semantic | Current Contract Status |
|---|---|---|---|
| Match outcome / objective completion | team-match | `MATCH_ENDED` outcome/reason + valid match lifecycle | ACTIVE source |
| Team survival | team-match | `MATCH_STARTED.context.teamSize` + `MATCH_ENDED.data.survivorCount`; M1-014 Survival component when eligible | ACTIVE |
| `objectiveTime` | team-match | elapsed wall-clock objective-bearing `PHASE_STARTED` → `PHASE_COMPLETED` | ACTIVE |

Primary metric set được dùng để trả lời performance side của research question. Exact statistical test/estimand presentation được freeze tại Section 27 trước main experiment.

## 23.2. Secondary Metrics

Có thể dùng khi source/availability hợp lệ:

- total Match Duration;
- per-phase duration;
- Down Count;
- Revive Count;
- Eliminated Count;
- player escape/survival outcome;
- Player `survival` MatchScore/Profile update;
- raw noise event count/by type/by phase;
- `ProfileNoisePenaltyCount` / `noise` MatchScore khi `ProfileNoiseFilter` và normalization config hợp lệ;
- AdaptiveDecision result distribution: APPLIED / NO_CHANGE / FIXED_FALLBACK;
- adaptive changed-key count;
- fallback by decision point/reasonCode.

Raw Noise Count không tự mang direction “good/bad”. Chỉ Profile noise penalty signal đã qua versioned filter mới có semantic score.

## 23.3. Metrics Not Active in Current Baseline

Không dùng như active experiment outcome khi chưa có contract:

```text
Player objective
Player teamwork
Player exploration
Player navigation
Player toolUsage quality
Player risk
Player revive quality
Team splitTime
Team avgDistance
Team reviveSuccess
Team resourceEfficiency
Team communication
Team wipeRecovery
Teamwork component
ResourceEfficiency component
Rescue Count
Tool Assist quality
```

## 23.4. Safety / Fairness Metrics

Các metric/process evidence sau dùng để đánh giá safety contract:

- invalid candidate count/rate;
- fallback count/rate;
- fallback by `reasonCode`;
- unauthorized key rejection count;
- bound rejection count;
- timing rejection count;
- pressure-rule rejection count;
- route/spawn invalid count;
- atomic partial-apply violation count;
- soft-lock occurrence count;
- unreachable objective occurrence count;
- unreachable exit occurrence count;
- deterministic reproduction mismatch count.

Các hard-rule violation không được “chấp nhận vì rate nhỏ”; validator phải reject theo contract.

## 23.5. Data Quality Metrics

- missing required telemetry count/rate;
- schema-invalid event count/rate;
- unsupported schema/config/version count;
- duplicate logical event handling evidence;
- incomplete phase-pair count;
- `MATCH_ABORTED` count;
- unknown/mismatched experiment condition count;
- critical instrumentation failure count;
- match/pair eligibility counts by exclusion reason.

## 23.6. Optional Subjective Player Feedback

Planning baseline cho phép telemetry + questionnaire/playtest feedback. Tuy nhiên questionnaire instrument chưa freeze.

Status:

```text
OPTIONAL / REQUIRES PROJECT APPROVAL
```

Neutral post-match topics có thể gồm:

- perceived pressure;
- perceived fairness;
- perceived difficulty;
- enjoyment;
- clarity.

Question wording, response scale, timing và scoring:

```text
TBD before use
```

Không claim validated psychological scale nếu chưa có approved source. Subjective survey không trở thành frozen gameplay metric.

---

# 24. Confounders

| Confounder | Risk | Control / Evidence |
|---|---|---|
| Learning effect | Team chơi tốt hơn ở lần sau | Counterbalanced order; log exposure/session order |
| Order effect | F→A khác A→F | Counterbalanced sequence; analyze/report sequence |
| Player skill | Skill khác nhau giữa teams | Within-team comparison; preserve grouping |
| Team composition | Coordination differs by roster | Keep roster stable for paired comparison when feasible; log roster |
| Repeated exposure | Familiarity improves route/objective knowledge | Record repetition count/order; report limitation |
| Map familiarity | Research Facility becomes easier with knowledge | Same map and counterbalance; record prior exposure where approved |
| Monster identity | Different monsters create different strategy | Same monster identity within matched F/A comparison; stratify by monster if multiple included |
| Build/version drift | Behavior/metrics change | Freeze build/config for run; exclude incompatible versions |
| Content whitelist drift | Different content universe | Freeze/version whitelist |
| Fixed baseline drift | Condition F no longer stable | Version fixed baseline and lock for run |
| Profile evolution | Adaptive input changes after earlier match | Capture pre-match snapshot/version; order-aware analysis |
| Fallback frequency | “Adaptive” match may spend little/no adaptive exposure | Preserve assigned condition; report fallback/exposure separately |
| GenAI stochastic text | Presentation differs | Keep presentation-only; same GenAI policy; do not treat as gameplay factor |
| Instrumentation drift | Metric missing/change semantics | Version telemetry/profile configs; data quality gate |

Không thay gameplay mechanics để kiểm soát confounder.

---

# 25. Data Eligibility / Exclusion

## 25.1. Three Separate Validity Layers

```text
Telemetry Event valid
≠ Match eligible for Profile
≠ Match eligible for Experiment
```

M1-020 bắt buộc lưu ba trạng thái riêng.

## 25.2. Hard Match Exclusion for Experiment Analysis

Một match không được dùng trong confirmatory Fixed-vs-Adaptive analysis nếu có ít nhất một critical condition sau:

- `MATCH_ABORTED`;
- experiment condition không xác định hoặc condition log mismatch;
- build/config/version incompatible với frozen experimental run;
- critical instrumentation failure làm mất khả năng xác định condition hoặc primary outcome;
- corrupted/schema-invalid telemetry ở mức làm primary analysis không reconstruct được;
- Adaptive match được scheduled dù Adaptive Readiness Gate không PASS;
- content/build mismatch làm Fixed và Adaptive không còn cùng experimental scope;
- required experiment identity/version evidence không thể resolve.

Raw telemetry/evidence của excluded match vẫn có thể giữ cho debug/audit; exclusion không có nghĩa xóa dữ liệu.

## 25.3. Metric-Level Unavailability

Nếu một match vẫn hợp lệ nhưng một metric cụ thể:

```text
MetricAvailability = UNAVAILABLE
```

thì:

```text
metric = null
→ do not replace with zero
→ exclude that match only from analyses requiring that metric
```

nếu protocol không có approved missing-data method khác.

## 25.4. Pair Eligibility

Với paired crossover analysis:

```text
F metric valid
AND A metric valid
→ eligible pair for paired analysis of that metric
```

Nếu một bên missing/ineligible:

- pair không được fake bằng zero;
- có thể giữ eligible match cho descriptive reporting;
- confirmatory paired analysis không dùng incomplete pair trừ khi một pre-frozen statistical plan explicitly supports missingness.

## 25.5. Fallback Within Adaptive-Assigned Match

Fallback là expected safety behavior, không tự động làm match “invalid”.

Primary analysis nên preserve **assigned condition** để tránh re-label sau khi outcome đã xảy ra, đồng thời phải report:

- number/rate of `APPLIED` decisions;
- `NO_CHANGE`;
- `FIXED_FALLBACK`;
- fallback reason/timing;
- actual adaptive exposure.

Nếu Adaptive match hoàn toàn không tạo valid adaptive exposure vì repeated fallback, đó là limitation/implementation-readiness signal. Secondary analysis theo actual exposure chỉ được dùng nếu predeclared và phải label rõ là secondary.

---

# 26. Sample Size Policy

Current project source không freeze participant/match sample size.

Therefore:

```text
Sample Size = TBD
```

Không ghi arbitrary:

```text
30 players
50 matches
100 matches
```

## 26.1. Future Sample Size Resolution Strategy

Sample size chỉ được freeze sau khi có đủ:

- pilot data nếu project cho phép;
- participant availability/constraints;
- repeated-measure/crossover structure;
- selected statistical test/model;
- empirical variability/effect-size assumption;
- power/effect-size target được nhóm nghiên cứu phê duyệt.

Numerical power target/effect size/significance threshold không được M1-020 tự invent.

## 26.2. Small-Sample Risk

Planning đã nhận diện rủi ro thiếu participant và đề xuất internal pilot + counter-balanced small-sample reporting với limitation. Điều này **không** tự freeze một N cụ thể và không cho phép phóng đại statistical conclusion.

---

# 27. Analysis Protocol

## 27.1. Analysis Freeze Point

Trước main experiment, nhóm phải freeze một analysis plan version chứa:

- final primary metrics;
- exact eligibility/exclusion rules;
- pairing rules;
- selected statistical test/model;
- effect estimate/reporting format;
- uncertainty reporting;
- handling of order/sequence;
- handling of fallback/exposure;
- treatment of pilot data;
- approved questionnaire scoring nếu subjective layer được activate.

## 27.2. Primary Comparison

Primary estimand concept:

```text
Condition difference = Adaptive − Fixed
```

ở **team-match / paired-team level** cho primary metrics.

Không coi từng Player row trong same team-match là independent replicate.

## 27.3. Statistical Test Selection

Exact test:

```text
TBD
```

cho tới khi pilot/sample/distribution/repeated structure được review.

Selection criteria:

- metric type;
- paired/repeated structure;
- sample size;
- distribution/robustness;
- missingness pattern;
- predeclared analysis assumptions.

Không post-hoc đổi test chỉ để đạt significance.

## 27.4. Reporting Minimum

Mỗi primary metric report tối thiểu:

- number of eligible team-matches/pairs;
- condition summaries;
- observed condition difference;
- effect estimate theo frozen analysis plan;
- uncertainty theo frozen analysis plan;
- data-quality/exclusion count;
- order/sequence information;
- fallback/adaptive-exposure information;
- limitations.

## 27.5. Hypothesis Evidence

Experiment report dùng ngôn ngữ:

- evidence consistent/inconsistent with H0/H1 according to pre-frozen analysis;
- observed difference;
- uncertainty;
- limitation;
- data quality.

Không dùng:

```text
PASS = Adaptive thắng
FAIL = Fixed thắng
```

## 27.6. Pilot vs Main Data

Nếu pilot được dùng để tune config/instrument/analysis:

- pilot must be labeled;
- rule có/không đưa pilot vào main analysis phải freeze trước main collection;
- không silently mix data được dùng để tune với confirmatory data nếu analysis plan không cho phép.

---

# 28. Safety / Fairness Evaluation

Safety/fairness evaluation kế thừa M1-015, không tạo rule mới.

## 28.1. Hard Fairness Validation Categories

1. Adaptive parameter whitelist.
2. Numerical min/max/default.
3. Allowed timing.
4. Pressure-axis mapping.
5. Maximum changed keys at boundary.
6. Maximum one aggressive Stalker pressure axis.
7. Spawn whitelist/reachability.
8. Route whitelist/legal-route/no-soft-lock.
9. Support budget non-retroactive behavior.
10. Final Hunt timer range/timing/immutability.
11. Scenario Validator.
12. Atomic apply.
13. Fallback behavior.
14. No direct Monster AI command.
15. No hidden Player information.
16. Deterministic reproduction.

## 28.2. Mandatory Negative Examples

| Input / Request | Expected Result |
|---|---|
| Modify unknown adaptive key | `INVALID` → reject → fallback |
| Modify non-whitelisted `AttackRange` | reject |
| Value below/above registry bound | reject; **no silent clamp** |
| One valid + one invalid change | reject whole decision; apply neither |
| Adaptive `routeModifier` at PRE_MATCH | `TIMING_REJECTED` |
| `DetectionFillRate ↑ + DetectionDecayRate ↓` | `PRESSURE_RULE_REJECTED` |
| `ChaseSpeed ↑ + SearchDuration ↑` | `PRESSURE_RULE_REJECTED` |
| > allowed changed-key count at boundary | reject |
| Invalid/unreachable objective spawn set | reject |
| Route removes last legal route | `ROUTE_INVALID` / reject |
| Route makes objective/exit unreachable | reject |
| Final Hunt timer outside `[45,60]` | reject |
| Final Hunt timer change after start | reject |
| Scenario Validator finds one invalid field | reject entire candidate |
| AED failure PRE_MATCH | `FULL_FIXED_CONFIG` |
| AED failure MID_MATCH | `KEEP_LAST_VALID_CONFIG` |

## 28.3. Safety Pass Principle

A negative test **passes** when invalid input is safely rejected and the correct fallback/preservation action occurs.

A fallback event in an experiment match is not automatically an experiment execution error; it is an observable safety outcome unless the readiness/exposure criteria say otherwise.

---

# 29. Reproducibility

## 29.1. Profile Reproduction

Same:

```text
raw/aggregated inputs
+ MatchProfileEligibility
+ MetricAvailability
+ old PlayerAIProfile
+ formula version
+ normalization/config version
+ configurable filter/tuning
```

must reproduce the same MatchScore/Profile/TeamPerformance status/result.

## 29.2. AED Reproduction

Same valid:

```text
Profile/TeamPerformance input snapshot
+ input versions
+ policyVersion
+ base/current AppliedScenarioConfig
+ parameterRegistryVersion/content
+ pressureAxis metadata
+ contentWhitelistVersion/content
+ decisionPoint
+ fallbackConfigId/version
```

must reproduce:

```text
same requested/candidate decision
same validator result
same AdaptiveDecision result
same fallbackAction
same AppliedScenarioConfig outcome
```

No hidden randomness in M1-015 v0.

## 29.3. Fixed Reproduction

Condition F must resolve from a versioned designer-authored fixed baseline. Same fixed baseline ID/version + same build/content environment must reproduce the same fixed config resolution, subject to any separately versioned deterministic content-selection contract.

If implementation uses a random seed for any allowed content selection:

```text
seed/config identity must be stored if applicable
```

M1-020 does not invent a seed requirement where implementation has no such randomness.

## 29.4. GenAI Reproduction Boundary

Exact LLM wording need not be deterministic. Required:

- trace facts/config/prompt/validation/model/generation versions;
- deterministic template fallback for same template/facts/language;
- deterministic cache reuse for compatible key.

GenAI stochasticity cannot alter gameplay config/state.

---

# 30. Evidence / Artifact Strategy

## 30.1. Required Evidence Types

Tùy test level, retain:

- test log;
- test case result;
- build/version metadata;
- ScenarioConfig before/after;
- fixed baseline ID/version;
- AdaptiveDecision log;
- `inputSnapshotRef` or equivalent processed-input evidence;
- parameter registry version;
- content whitelist version;
- telemetry export;
- MatchTelemetry/Profile processing output/status;
- experiment condition;
- screenshots/video khi behavior/visual state cần chứng minh;
- reproduction config/seed nếu applicable;
- GenAI request/facts identity/output/validator/fallback metadata khi test M1-019.

## 30.2. Evidence Trace Rule

```text
important result
→ build
→ match/test run
→ config/version
→ data
→ condition
→ test case/protocol version
```

Mọi claim trong KLTN report phải có trace chain tương ứng nếu claim phụ thuộc test/experiment data.

## 30.3. Suggested Evidence Manifest

Logical record:

```text
EvidenceManifest
{
  testCaseId,
  testRunId,
  matchId,
  buildVersion,
  experimentProtocolVersion,
  condition,
  scenarioConfigVersion,
  fixedBaselineIdOrNull,
  policyVersionOrNull,
  parameterRegistryVersionOrNull,
  contentWhitelistVersion,
  telemetrySchemaVersion,
  profileFormulaVersionOrNull,
  normalizationConfigVersionOrNull,
  artifactRefs[],
  result,
  notes
}
```

Exact storage schema = implementation-owned / TBD; semantic trace requirement mới là frozen strategy.

---

# 31. Representative Test Cases

## 31.1. Standard Test Case Format

```text
Test Case ID
Requirement / Contract Reference
Precondition
Input
Steps
Expected Result
Telemetry / Evidence
Pass Criteria
Execution Milestone
```

## 31.2. Representative P0 / Negative / Boundary / Readiness Cases

### TC-M1020-001 — Stalker Vision / Closed Door

**Requirement / Contract Reference:** M1-013 Vision/LOS.  
**Precondition:** Stalker + target candidate; Closed Door between them.  
**Input:** Candidate Player inside distance/angle but LOS blocked by Closed Door.  
**Steps:** Evaluate Vision Sensor.  
**Expected Result:** Player not visible; no DetectionTarget created from that blocked observation.  
**Telemetry / Evidence:** Sensor debug/evidence if implementation exposes it; state trace.  
**Pass Criteria:** No LOS bypass and no target acquisition.  
**Execution Milestone:** M2+ implementation verification.

### TC-M1020-002 — Stalker Noise Exclusion

**Requirement / Contract Reference:** M1-007, M1-013.  
**Precondition:** Stalker has no valid visible target.  
**Input:** Runtime NoiseEvent near Stalker.  
**Steps:** Emit runtime noise and observe Stalker perception/FSM.  
**Expected Result:** No DETECT/SEARCH/target/LKP change from noise.  
**Evidence:** Runtime noise evidence + Stalker state trace.  
**Pass Criteria:** Stalker remains Vision/LOS-only.  
**Execution Milestone:** M2+.

### TC-M1020-003 — DetectionTarget Lock

**Reference:** M1-013.  
**Precondition:** Player A is DetectionTarget; Player B becomes nearer while A remains visible.  
**Input:** Valid observations.  
**Steps:** Update perception during DETECT.  
**Expected Result:** DetectionTarget remains A; no switch solely because B is nearer.  
**Evidence:** Target/meter debug trace.  
**Pass:** Frozen lock rule preserved.  
**Milestone:** M2+.

### TC-M1020-004 — LastKnownPosition No Hidden Tracking

**Reference:** M1-013.  
**Precondition:** CurrentTarget loses LOS after a known observed position.  
**Input:** Hidden target continues moving.  
**Steps:** Enter SEARCH; compare LKP vs hidden transform.  
**Expected Result:** LKP remains last valid observed position; hidden transform does not update LKP.  
**Evidence:** observation/LKP/state trace.  
**Pass:** No omniscient tracking.  
**Milestone:** M2+.

### TC-M1020-005 — Telemetry Missing Required Field

**Reference:** M1-008.  
**Precondition:** Telemetry validator active.  
**Input:** Event missing required `id`/`matchId`/`ts`/`valueJson`/`schemaVersion` as applicable.  
**Steps:** Validate event.  
**Expected Result:** Reject event.  
**Evidence:** Validation result/reason.  
**Pass:** Invalid event not accepted as valid TelemetryEvent.  
**Milestone:** M2+/Backend telemetry implementation.

### TC-M1020-006 — Duplicate Telemetry ID

**Reference:** M1-008 Duplicate/Idempotency.  
**Precondition:** Valid event already accepted.  
**Input:** Same event ID retried.  
**Steps:** Re-submit through relevant ingest boundary.  
**Expected Result:** No second logical telemetry record representing same event.  
**Evidence:** ingest/storage log.  
**Pass:** Logical idempotency preserved.  
**Milestone:** Backend telemetry implementation.

### TC-M1020-007 — MATCH_ABORTED Profile Eligibility

**Reference:** M1-014.  
**Precondition:** Match has partial valid telemetry.  
**Input:** `MATCH_ENDED.reasonCode = MATCH_ABORTED`.  
**Steps:** Process MatchTelemetry/Profile.  
**Expected Result:** MatchProfileEligibility=INELIGIBLE; no PlayerAIProfile update/sampleCount; TeamPerformance INCOMPLETE/null.  
**Evidence:** Match/profile processing record.  
**Pass:** No partial aborted match persistence as valid profile result.  
**Milestone:** M2+/profile implementation.

### TC-M1020-008 — Missing Metric Is Not Zero

**Reference:** M1-014.  
**Precondition:** Required metric coverage unavailable.  
**Input:** `MetricAvailability=UNAVAILABLE`.  
**Steps:** Run consumer formula.  
**Expected Result:** component/MatchScore=null; no synthetic zero.  
**Evidence:** aggregation/formula output.  
**Pass:** Null/missing semantics preserved.  
**Milestone:** Profile implementation.

### TC-M1020-009 — EMA Reproduction

**Reference:** M1-014.  
**Precondition:** Same old profile, same eligible non-null MatchScore, same alpha/config version.  
**Input:** Identical processing input twice in isolated reproduction harness.  
**Steps:** Compute EMA.  
**Expected Result:** Same clamped score/status/sampleCount result.  
**Evidence:** input/output/version snapshot.  
**Pass:** Deterministic formula reproduction.  
**Milestone:** Profile implementation.

### TC-M1020-010 — Current TeamPerformance Incomplete Gate

**Reference:** M1-014 + M1-015.  
**Precondition:** Teamwork/ResourceEfficiency remain DEFERRED.  
**Input:** `TeamPerformance.status=INCOMPLETE`, `score=null`.  
**Steps:** Evaluate AED Input Gate.  
**Expected Result:** Policy does not run; candidate not evaluated; safe fixed/fallback path.  
**Evidence:** gate/decision log with `INPUT_INCOMPLETE`.  
**Pass:** No synthetic adaptive path.  
**Milestone:** Current M1 contract test / later implementation.

### TC-M1020-011 — Unknown Adaptive Key

**Reference:** M1-015 whitelist.  
**Precondition:** Adaptive path otherwise eligible in test harness/future implementation.  
**Input:** Request unknown/non-whitelisted key.  
**Steps:** Validate candidate.  
**Expected Result:** `INVALID`; whole candidate rejected; fallback by decision point.  
**Evidence:** AdaptiveDecision log.  
**Pass:** No unknown key applied.  
**Milestone:** AED/validator implementation.

### TC-M1020-012 — Out-of-Bound No Clamp

**Reference:** M1-015 bounds.  
**Precondition:** Versioned registry has valid min/max.  
**Input:** Requested value outside bound.  
**Steps:** Validate candidate.  
**Expected Result:** `INVALID + BOUND_REJECTED + FIXED_FALLBACK`; requested value not silently clamped/applied.  
**Evidence:** before/requested/resolvedAfter/decision log.  
**Pass:** No silent repair.  
**Milestone:** AED/validator implementation.

### TC-M1020-013 — Atomic Reject

**Reference:** M1-015 Atomic Decision Application.  
**Precondition:** Candidate has change A valid, change B invalid.  
**Input:** Two-change decision.  
**Steps:** Scenario Validator.  
**Expected Result:** apply neither A nor B; candidate INVALID; fallback.  
**Evidence:** resolvedBefore/resolvedAfter.  
**Pass:** No partial apply.  
**Milestone:** AED/validator implementation.

### TC-M1020-014 — PRE_MATCH Route Modifier Rejected

**Reference:** M1-015 route timing.  
**Precondition:** PRE_MATCH decision point.  
**Input:** adaptive `routeModifier`.  
**Steps:** Validate timing.  
**Expected Result:** `INVALID + TIMING_REJECTED + FIXED_FALLBACK + FULL_FIXED_CONFIG`.  
**Evidence:** AdaptiveDecision.  
**Pass:** No adaptive route override PRE_MATCH.  
**Milestone:** AED/validator implementation.

### TC-M1020-015 — Compound Pressure Reject

**Reference:** M1-015 Compound-Pressure Fairness.  
**Precondition:** valid bounds/timing.  
**Input:** `ChaseSpeed ↑` + `SearchDuration ↑` in same decision.  
**Steps:** Validate pressure axes.  
**Expected Result:** `INVALID + PRESSURE_RULE_REJECTED`; whole decision rejected.  
**Evidence:** pressure mapping + decision log.  
**Pass:** max-one aggressive-axis rule preserved.  
**Milestone:** AED/validator implementation.

### TC-M1020-016 — Invalid Route Causes Unreachable Objective

**Reference:** M1-015 Route Safety.  
**Precondition:** ALLOWED_PHASE_BOUNDARY.  
**Input:** whitelisted or candidate route modifier whose resolved graph makes objective unreachable.  
**Steps:** Scenario Validator route check.  
**Expected Result:** reject; no route apply; fallback keeps last valid config mid-match.  
**Evidence:** route validation result + config snapshot.  
**Pass:** no soft-lock/unreachable objective.  
**Milestone:** Route/AED integration.

### TC-M1020-017 — PRE_MATCH AED Unavailable

**Reference:** M1-015 fallback.  
**Precondition:** PRE_MATCH, valid fallback package.  
**Input:** AED unavailable.  
**Steps:** Resolve fallback.  
**Expected Result:** `NOT_EVALUATED + AED_UNAVAILABLE + FIXED_FALLBACK + FULL_FIXED_CONFIG`; known-valid fixed ScenarioConfig becomes applied after validation.  
**Evidence:** decision/fallback/config log.  
**Pass:** match can proceed on fixed safe path.  
**Milestone:** FixedDirector/AED integration.

### TC-M1020-018 — MID_MATCH AED Failure

**Reference:** M1-015 fallback.  
**Precondition:** Match started with valid AppliedScenarioConfig.  
**Input:** AED timeout or invalid candidate at allowed boundary.  
**Steps:** Resolve failure.  
**Expected Result:** `KEEP_LAST_VALID_CONFIG`; no full baseline replacement, no FSM/player/objective reset.  
**Evidence:** before/after config + runtime state trace.  
**Pass:** last valid config and runtime state preserved.  
**Milestone:** AED/gameplay integration.

### TC-M1020-019 — Final Hunt Timer Boundary

**Reference:** M1-015 Final Hunt.  
**Precondition:** `FINAL_HUNT_SETUP` before timer start.  
**Input:** lower-bound, upper-bound, below-bound, above-bound candidates using registry/contract.  
**Steps:** Validate.  
**Expected Result:** values inside `[45,60]` structurally eligible; outside reject. After timer starts, any adaptive change rejects.  
**Evidence:** validator/decision log.  
**Pass:** bound + timing + immutability preserved.  
**Milestone:** Final Hunt/AED integration.

### TC-M1020-020 — GenAI Invented Gameplay Fact

**Reference:** M1-019.  
**Precondition:** Trusted facts do not contain an asserted gameplay stat/detail.  
**Input:** LLM candidate invents stat/objective count/route/ability.  
**Steps:** MissionBriefingValidator.  
**Expected Result:** candidate INVALID → retry/fallback; gameplay unchanged.  
**Evidence:** facts identity + candidate + validator result.  
**Pass:** invented authoritative fact never reaches gameplay authority.  
**Milestone:** GenAI integration.

### TC-M1020-021 — GenAI Timeout Non-Blocking

**Reference:** M1-019.  
**Precondition:** Supported P0 scenario has valid template fallback; no compatible cache.  
**Input:** Provider timeout / retry exhaustion.  
**Steps:** Execute briefing pipeline.  
**Expected Result:** template fallback; match start not blocked by GenAI failure.  
**Evidence:** timeout/retry/fallback log + UI output source.  
**Pass:** presentation fallback succeeds without gameplay mutation.  
**Milestone:** GenAI/Backend integration.

### TC-M1020-022 — Experiment Readiness Gate Current Baseline

**Reference:** M1-014 + M1-015 + M1-020.  
**Precondition:** Current baseline unchanged.  
**Input:** TeamPerformance INCOMPLETE/null.  
**Steps:** Evaluate Adaptive Readiness Gate for planned live experiment.  
**Expected Result:** `NOT READY`; Condition A execution not scheduled.  
**Evidence:** readiness checklist.  
**Pass:** no fake TeamPerformance/data generated.  
**Milestone:** M1/M5 preflight.

### TC-M1020-023 — Fixed vs Adaptive Condition Integrity

**Reference:** QA-04 / AED-12 / M1-020.  
**Precondition:** Future gate PASS and both conditions implemented.  
**Input:** Paired Fixed and Adaptive matches.  
**Steps:** Compare condition metadata/content/build.  
**Expected Result:** same core content/build/map/monster/objective chain; only M1-015-authorized adaptive config decisions differ.  
**Evidence:** condition log + config diff + build/content versions.  
**Pass:** no uncontrolled condition mismatch.  
**Milestone:** M5 experiment setup.

### TC-M1020-024 — Deterministic AED Reproduction

**Reference:** M1-015 reproducibility.  
**Precondition:** Valid Adaptive input snapshot and all versions/config captured.  
**Input:** Identical input snapshot + base config + registry + whitelist + decision point.  
**Steps:** Re-run deterministic policy/validator reproduction.  
**Expected Result:** same requested candidate, validation result, decision result, fallbackAction and AppliedScenarioConfig outcome.  
**Evidence:** two reproduction logs.  
**Pass:** exact deterministic contract reproduction.  
**Milestone:** AED implementation / M5 evidence.

---

# 32. Experiment Reporting Template

```text
Experiment Protocol Version:
Build Version:
Data Collection Window:
Fixed Baseline ID/Version:
Adaptive Policy Version:
Parameter Registry Version:
Content Whitelist Version:
Telemetry Schema Version:
Profile Formula Version:
Normalization Config Version:

Research Question:
H0:
H1:

Adaptive Readiness Gate:
- Status:
- Evidence:

Experimental Unit:
Assignment/Counterbalancing:
Condition F Definition:
Condition A Definition:
Controlled Constants:

Sample Size:
- Planned: TBD/approved value
- Collected:
- Eligible:
- Excluded:
- Exclusion Reasons:

Primary Metrics:
Secondary Metrics:
Safety/Fairness Metrics:
Data Quality Metrics:
Optional Subjective Metrics:

Observed Condition Differences:
Uncertainty:
Order/Sequence Effects:
Fallback/Adaptive Exposure:
Data Quality Issues:
Limitations:

Hypothesis Evidence:
- H0/H1 interpretation according to pre-frozen analysis plan

Conclusion:
- Do not report "Adaptive PASS/FAIL"
- Report observed difference + uncertainty + limitation
```

No field in this template implies results already exist.

---

# 33. Current M1 Experiment Readiness

| Area | Current Status | Meaning |
|---|---|---|
| Fixed-path testing | **READY / subject to implementation availability** | FixedDirector/fixed config/fallback/validator contracts can be verified without Adaptive eligibility |
| Traditional AI/Stalker contract testing | **READY at contract/design level** | Runtime execution depends M2+ implementation |
| Telemetry contract testing | **READY at schema/design level** | Transport/storage runtime depends implementation |
| ACTIVE Profile formula testing | **READY at contract/component level** | survival/noise + eligibility/availability/EMA semantics can be tested when pipeline exists |
| AED Input Gate negative-path testing | **READY** | Current INCOMPLETE input should produce safe fixed path |
| Scenario Validator/Fairness contract testing | **READY at contract/design level** | Can define/verify invalid candidate behavior in implementation/test harness |
| Adaptive policy COMPLETE-path contract testing | **READY as design contract** | Runtime execution remains prerequisite-dependent; legitimate COMPLETE input is required and synthetic experiment data is forbidden |
| Live Fixed-vs-Adaptive gameplay experiment | **NOT READY** | **Immediate blocker:** TeamPerformance is INCOMPLETE/null. **Additional prerequisites:** Section 35.2 |

Current source-supported conclusion:

```text
Fixed-path testing:
READY / theo implementation availability

Adaptive policy contract testing:
READY ở mức contract/design

Live Fixed-vs-Adaptive gameplay experiment:
NOT READY trong current baseline

Immediate blocker:
TeamPerformance.status = INCOMPLETE
TeamPerformance.score = null

Additional prerequisites:
see Section 35.2 — Non-Conflict TBDs
```

Current immediate blocker originates from the frozen baseline:

```text
Teamwork = DEFERRED
ResourceEfficiency = DEFERRED
→ TeamPerformance.status = INCOMPLETE
→ TeamPerformance.score = null
```

`NOT READY` của live experiment **không làm M1-020 thất bại**. M1-020 freeze protocol để M5/M6 có thể execute đúng khi immediate blocker và các prerequisite execution liên quan được giải quyết hợp lệ, không bằng synthetic data hoặc thay đổi frozen semantic.

---

# 34. Out of Scope

M1-020 không:

- implement Unity test code;
- implement AED;
- implement Telemetry backend;
- sửa Profile formula;
- kích hoạt DEFERRED metric;
- sửa ScenarioConfig contract;
- sửa Stalker FSM;
- tạo Listener/Warden behavior mới;
- chạy playtest thật;
- tạo fake experiment result;
- tạo fake telemetry;
- tạo synthetic TeamPerformance;
- invent sample size;
- invent effect size/statistical significance;
- viết kết quả KLTN giả;
- thay gameplay;
- thêm mechanic;
- dùng GenAI làm gameplay authority;
- tạo realtime adaptive pacing ngoài M1-015 v0;
- resolve source conflicts bằng cách sửa frozen file.

---

# 35. Open Issues / TBD

## 35.1. Source Conflicts

### SC-01 — Stalker FSM topology wording in Tier A vs frozen M1-013

Tier A Architecture/Implementation còn các generic/older rows dùng:

```text
Patrol → Investigate → Chase → Search → Attack → Recover → Return/Final Hunt
```

và Implementation Spec có wording `Final Hunt state` / `Investigate` cho monster core.

Frozen M1-007/M1-013 Stalker-specific contract dùng chính xác:

```text
PATROL
DETECT
CHASE
ATTACK
RECOVER
SEARCH
```

và:

```text
FINAL_HUNT = gameplay phase/configuration context
not a Stalker FSM state
Stalker = Vision/LOS only
```

**M1-020 handling:** không sửa Tier A, không silently merge state. Stalker regression tests của M1-020 bám frozen Stalker contract theo task requirement. Upstream Architecture/Implementation rows cần project owner align ở một revision riêng trước final implementation acceptance.

**Impact on live experiment:** any build used for experiment must pass the frozen Stalker regression contract; otherwise experiment build is not eligible.

### SC-02 — TeamPerformance numerical weights

Tier A Architecture/Implementation có older initial model:

```text
30% ObjectiveSpeed
25% SurvivalRate
25% Teamwork
20% ResourceEfficiency
```

M1-014 explicitly freezes:

```text
formula topology = FROZEN
weights = CONFIGURABLE / VERSIONED
sum(weights) = 1
DO NOT hard-code 30/25/25/20 as frozen value
```

**M1-020 handling:** exact experiment/Profile weight values are configuration-owned and versioned; M1-020 does not use 30/25/25/20 as frozen baseline.

**Impact:** upstream documentation should be aligned so implementation does not hard-code old initial weights.

### SC-03 — Historical Team Profile wording vs match-scoped TeamProfile v1.0

Gameplay/Tier A language says post-match Player/Team Profile may be used for subsequent matches. M1-014 freezes:

```text
TeamProfile v1.0
→ match-scoped
→ teamKey = matchId
→ no persistent historical party/team identity
→ no cross-match TeamProfile EMA
```

M1-015 AED input contains `teamProfileRef` and current TeamPerformance but does not allow M1-020 to invent a new persistent team identity.

**Severity / Impact:**

```text
CRITICAL FOR LIVE ADAPTIVE EXECUTION
NON-BLOCKING FOR M1-020 DESIGN COMPLETION
```

SC-03 **does not block** `M1-020 document / protocol = DONE / FROZEN`, vì M1-020 chỉ phải freeze test strategy/readiness semantics. Tuy nhiên SC-03 **does block live PRE_MATCH Adaptive execution** cho tới khi một upstream Profile/AED lifecycle contract định nghĩa cách resolve valid pre-match processed team input cho một new match/lobby mà không vi phạm M1-014 identity semantics.

**Open issue:** before live Adaptive experiment, project must freeze how PRE_MATCH Adaptive input resolves the valid team/current-prior processed snapshot for a new lobby/team without violating M1-014 identity semantics.

```text
Historical/team input snapshot resolution
→ TBD / requires explicit upstream Profile/AED lifecycle contract
```

M1-020 chỉ ghi nhận dependency này. M1-020 **không** giải quyết SC-03 bằng:

```text
stale previous-match TeamProfile reuse
synthetic team aggregation
invented persistent historical team identity
```

Không được tự tạo `PersistentTeamProfile`, `HistoricalTeamEMA`, `PartyProfile` hoặc model tương đương để bypass dependency này.

### SC-04 — PacingState / generic runtime pacing rows

Some Tier A Architecture/Implementation/Planning rows reference pacing/cooldown/in-phase adjustment concepts. M1-015 v0 freezes:

```text
PacingState = RESERVED / OUT OF CURRENT POLICY
no per-frame/per-second adaptation
only PRE_MATCH / ALLOWED_PHASE_BOUNDARY / FINAL_HUNT_SETUP
```

**M1-020 handling:** no active test/experiment authority is granted to unspecified realtime PacingState. Future activation requires separate policy/version contract.

### SC-05 — Attachment revision naming

Requested source names and uploaded copy suffixes are not identical for several files, e.g. requested `ECHO PROTO(1).docx` while supplied attachment is `ECHO PROTO(2).docx`.

**M1-020 handling:** this document uses the content of the supplied attachment set. For formal KLTN archive/release signoff, project should confirm canonical revision/checksum mapping.

## 35.2. Non-Conflict TBDs

Các mục dưới đây là **additional execution prerequisites** ngoài immediate current blocker `TeamPerformance = INCOMPLETE / null`. Chúng không thay đổi `M1-020 document / protocol = DONE / FROZEN`, nhưng phải được resolve/freeze phù hợp trước live/main experiment theo cột `Blocks Live Experiment?`.

| TBD | Status / Owner Expectation | Blocks M1-020 Document? | Blocks Live Experiment? |
|---|---|---:|---:|
| Exact fixed experiment baseline ID/version/values | Designer/config owner + M5 experiment setup | No | **Yes** |
| Legitimate COMPLETE TeamPerformance inputs/formulas for deferred components | Future Profile/TeamPerformance contract | No | **Yes** |
| Team input snapshot resolution across matches | Upstream Profile/AED lifecycle contract | No | **Yes** |
| Exact parameter bounds/defaults except frozen Final Hunt timer | Versioned Parameter Registry | No | **Yes for Adaptive execution** |
| Exact `ProfileNoiseFilter`/normalization thresholds/alpha | Versioned normalization config | No | For metrics using them |
| Exact questionnaire instrument | Optional project approval | No | No, unless subjective outcome is required |
| Sample size | Research plan after pilot/constraints | No | **Yes before main study** |
| Exact statistical test/model | Analysis-plan freeze | No | **Yes before main study** |
| Significance/effect-size/power assumptions | Research approval | No | **Yes if inferential claim planned** |
| Test hardware/network/environment matrix | Implementation/QA | No | Yes for controlled execution |
| Randomized sequence allocation mechanism | Experiment implementation | No | Must be decided before main experiment |
| Pilot data inclusion policy | Research protocol | No | Must be frozen before main experiment |

---

# 36. Implementation Constraints

1. Do not change gameplay to make testing easier.
2. Do not add a Stalker FSM state outside M1-013.
3. Do not add Hearing to Stalker.
4. Do not use Telemetry to command monster runtime.
5. Do not use raw TelemetryEvent as direct Adaptive heuristic.
6. Do not allow Profile to command FSM/Target/Navigation/Attack.
7. Do not make every Player Profile dimension ACTIVE.
8. Do not convert missing/null/deferred metric to zero.
9. Do not renormalize TeamPerformance weights around missing components.
10. Do not reuse stale previous-match TeamProfile fields as current-match components.
11. Do not run Adaptive policy when TeamPerformance is INCOMPLETE/null.
12. Do not use Player survival/noise alone to bypass Adaptive eligibility.
13. Do not make `configurable = adaptive-authorized`.
14. Adaptive Stalker authority stays exactly on M1-015 whitelist.
15. Numerical adaptive keys require versioned bounds/defaults.
16. Out-of-bound candidate must reject; no silent clamp.
17. Timing must be decision-point controlled.
18. Phase boundary changed-key and pressure-axis limits must be enforced.
19. Scenario Validator must execute before apply.
20. Adaptive decision must be atomic.
21. Invalid candidate must not appear as applied `resolvedAfter`.
22. PRE_MATCH failure uses full fixed fallback.
23. MID_MATCH failure keeps last valid config and runtime state.
24. Fallback does not automatically equal experiment failure.
25. Fixed ScenarioConfig still requires supported `policyVersion`.
26. Adaptive decision/version/input snapshot must be traceable.
27. GenAI receives trusted mission facts only.
28. GenAI output remains presentation-only.
29. GenAI failure must not block correctly packaged supported match start.
30. Do not use GenAI as a gameplay variable in Fixed-vs-Adaptive comparison.
31. Do not invent questionnaire validation/scoring.
32. Do not invent sample size, power, effect size or significance result.
33. Do not claim Adaptive is better without experiment evidence.
34. Do not treat player rows inside a team-match as automatically independent samples.
35. Every experiment match must preserve condition/build/config/version evidence.
36. Main analysis plan must be frozen before confirmatory data interpretation.
37. Source conflicts in Section 35 must remain explicit until upstream owners resolve them.

---

# 37. M1-020 Completion Criteria

M1-020 Test Strategy / Protocol v0 completion checklist:

- [x] Test objectives.
- [x] Test scope.
- [x] Test levels.
- [x] Contract test strategy.
- [x] Integration test strategy.
- [x] System/E2E strategy.
- [x] Traditional AI regression strategy.
- [x] Telemetry/Profile testing.
- [x] AED fairness/validator testing.
- [x] FixedDirector/fallback testing.
- [x] Fixed vs Adaptive research question.
- [x] Fixed condition.
- [x] Adaptive condition.
- [x] Adaptive Readiness Gate.
- [x] Hypotheses.
- [x] Experimental unit.
- [x] Assignment/counterbalancing strategy.
- [x] Metrics.
- [x] Confounder control.
- [x] Data exclusion.
- [x] Versioning/reproducibility.
- [x] Evidence strategy.
- [x] Experiment analysis/reporting protocol.
- [x] Explicit current-baseline limitation.
- [x] No invented Adaptive eligibility.
- [x] No invented gameplay mechanic.
- [x] Open Issues/TBD list.
- [x] Source conflicts explicitly recorded instead of silently reconciled.
- [x] Sample Size remains TBD.
- [x] Current TeamPerformance remains INCOMPLETE/null.
- [x] Live Adaptive experiment explicitly NOT READY.

## 37.1. Completion Decision

```text
M1-020 document / protocol status
= DONE / FROZEN
```

Reason:

- all required test-strategy and experiment-design contracts are defined;
- current Adaptive limitation is explicitly represented by a readiness gate;
- missing numerical/statistical values remain configurable/TBD rather than invented;
- live execution dependencies are separated from design completion;
- source conflicts are logged as external alignment issues rather than silently changed.

However:

```text
Live Fixed-vs-Adaptive gameplay experiment execution
= NOT READY
```

Immediate blocker:

```text
Teamwork = DEFERRED
ResourceEfficiency = DEFERRED
→ TeamPerformance.status = INCOMPLETE
→ TeamPerformance.score = null
```

Additional execution prerequisites:

```text
see Section 35.2 — Non-Conflict TBDs
```

`DONE / FROZEN` ở đây chỉ xác nhận **Test Strategy + Experiment Protocol v0 completed**; nó không có nghĩa implementation đã pass, live experiment đã completed, hoặc Adaptive đã được chứng minh tốt hơn Fixed.

The document status must be revised if an upstream frozen contract changes materially.

---

# 38. Frozen Baseline Summary

```text
M1-020
= Test Strategy + Experiment Protocol
≠ test execution
≠ experiment result
```

```text
Architecture
Gameplay Runtime
→ TelemetryEvent
→ MatchTelemetry
→ MatchScore
→ PlayerAIProfile / TeamProfile / TeamPerformance
→ AED Input Gate
→ AED Policy
→ Candidate ScenarioConfig
→ Scenario Validator
→ Applied ScenarioConfig
→ Traditional Gameplay AI
```

```text
Monster runtime
= Traditional AI
```

```text
Stalker
= Vision / LOS only
FSM = PATROL / DETECT / CHASE / ATTACK / RECOVER / SEARCH
Final Hunt = phase/config context, not FSM state
no Hearing
```

```text
TelemetryEvent
= schemaVersion 1.0
context inside valueJson.context
Telemetry records gameplay
Telemetry does not command Monster AI
Runtime NoiseEvent ≠ TelemetryEvent
```

```text
Player Profile current ACTIVE
= survival + noise

other Player dimensions
= DEFERRED
```

```text
TeamProfile v1.0
= match-scoped
teamKey = matchId
no stale cross-match reuse
```

```text
Current TeamPerformance
ObjectiveSpeed = potentially active when valid
Survival = active when valid
Teamwork = DEFERRED
ResourceEfficiency = DEFERRED

→ TeamPerformance.status = INCOMPLETE
→ TeamPerformance.score = null
```

```text
Missing component
→ NOT zero
→ NOT weight renormalization
→ NOT synthetic heuristic
```

```text
Adaptive Input Eligibility
TeamPerformance COMPLETE
+ score non-null
+ valid required Profile/config/version/registry/whitelist
→ policy may evaluate

otherwise
→ safe fixed/fallback path
```

```text
Adaptive Stalker whitelist
DetectionFillRate
DetectionDecayRate
ChaseSpeed
SearchDuration
```

```text
Adaptive timing
PRE_MATCH
ALLOWED_PHASE_BOUNDARY
FINAL_HUNT_SETUP
```

```text
Fairness
out-of-bound → reject, no clamp
invalid key → reject
one valid + one invalid → reject whole decision
route PRE_MATCH → reject
max 1–2 changed keys at boundary
max one more-aggressive Stalker pressure axis
invalid route/spawn/soft-lock → reject
```

```text
Fallback
PRE_MATCH failure
→ FULL_FIXED_CONFIG

MID_MATCH / FINAL_HUNT_SETUP failure
→ KEEP_LAST_VALID_CONFIG
```

```text
GenAI
= Mission Briefing / presentation only
failure ≠ match failure
cache/finite retry/template fallback
no gameplay authority
```

```text
Fixed-vs-Adaptive Research
Condition F = FIXED
Condition A = ADAPTIVE only after Readiness Gate PASS
Experimental Unit = team-match
Assignment = within-team crossover + counterbalanced order
Sample Size = TBD
No fake result
No claim Adaptive is better before evidence
```

```text
Current M1 Experiment Readiness
Fixed-path testing
= READY / subject to implementation availability

Adaptive contract/design testing
= READY

Live Fixed-vs-Adaptive gameplay experiment
= NOT READY

Immediate blocker
= TeamPerformance.status = INCOMPLETE
+ TeamPerformance.score = null

Additional execution prerequisites
= Section 35.2 — Non-Conflict TBDs
```

```text
M1-020 status
= DONE / FROZEN at Test Strategy + Experiment Protocol v0 level

Experiment execution
= deferred until readiness prerequisites are legitimately satisfied
```

---

**End of `M1-020_Test_Strategy_Fixed_vs_Adaptive_Experiment_v0_FINAL_REVISED.md`**
