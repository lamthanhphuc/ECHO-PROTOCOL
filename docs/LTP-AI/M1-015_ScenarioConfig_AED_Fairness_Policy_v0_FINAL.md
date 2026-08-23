# ECHO PROTOCOL — M1-015 ScenarioConfig + AED Fairness Policy v0

**Task:** M1-015 — ScenarioConfig + fairness rules  
**Owner:** C — AI / Telemetry / Research  
**Support:** A — Gameplay / UI / Integration  
**Dependency:** M1-014, M1-013, M1-012  
**Priority:** P0  
**Status:** DONE / FROZEN  
**Policy Baseline:** v0

---

# 1. Purpose

Tài liệu này freeze contract/design cho **ScenarioConfig + AED Fairness Policy v0** của ECHO PROTOCOL.

Mục tiêu của M1-015 là định nghĩa đầy đủ luồng:

```text
PlayerAIProfile / TeamProfile / TeamPerformance
        ↓
AED Input Eligibility
        ↓
AED Policy
        ↓
Candidate ScenarioConfig
        ↓
Scenario Validator
        ↓
VALID
├─ YES → Applied ScenarioConfig
└─ NO  → FixedDirector / Fixed Fallback
```

Tài liệu phải cho phép Backend / AI / Gameplay developer triển khai mà không phải tự suy đoán:

- input nào đủ điều kiện chạy Adaptive AED;
- ScenarioConfig gồm field nào;
- field nào chỉ tồn tại trong config và field nào AED được adaptive modify;
- Stalker parameter nào nằm trong adaptive whitelist;
- parameter bounds/default nằm ở đâu;
- thời điểm nào được quyết định;
- compound-pressure nào bị cấm;
- spawn/route/support/final-hunt fairness;
- validator reject như thế nào;
- adaptive decision có atomic hay không;
- khi nào phải Fixed fallback;
- fallback config được định danh/version như thế nào;
- decision phải log/reproduce như thế nào;
- AED boundary với Traditional Monster AI.

M1-015 là **policy/configuration contract**.

M1-015 không thay đổi Monster FSM hoặc gameplay authority.

---

# 2. Scope

## 2.1. In Scope

M1-015 freeze:

1. AED Input Contract.
2. Adaptive input eligibility.
3. ScenarioConfig schema.
4. Config field vs adaptive authority.
5. Designer-authored content whitelist contract.
6. Adaptive Stalker parameter whitelist.
7. Adaptive Parameter Registry.
8. Default/min/max semantics.
9. Pressure axis semantics.
10. Pre-match decision timing.
11. Phase-boundary adjustment timing.
12. Compound-pressure fairness.
13. Objective spawn-set safety.
14. Support item budget fairness.
15. Route modifier safety.
16. Final Hunt timer contract.
17. Fairness hard rules.
18. Forbidden decisions.
19. Scenario Validator.
20. Invalid decision behavior.
21. Atomic decision application.
22. FixedDirector fallback.
23. Fallback ScenarioConfig identity/version.
24. Mid-match fallback semantics.
25. AdaptiveDecision logical contract.
26. Controlled reasonCode semantics.
27. Policy/config/content versioning.
28. Deterministic reproducibility.
29. Contract test cases.
30. Implementation constraints.
31. Completion criteria.
32. Frozen baseline summary.

## 2.2. Out of Scope

M1-015 không định nghĩa hoặc implement:

- Player Profile formula;
- TeamPerformance formula;
- partial TeamPerformance scoring;
- realtime pacing estimator;
- PacingState transition logic;
- Machine Learning;
- online reinforcement learning;
- GenAI gameplay decision;
- procedural content generation ngoài whitelist;
- adaptive Monster FSM;
- hidden-player inference;
- arbitrary difficulty formula;
- new telemetry events;
- telemetry transport/retry;
- database schema chi tiết;
- dashboard;
- analytics visualization;
- runtime Target Selection;
- Vision/Hearing Sensor implementation;
- LastKnownPosition logic;
- Attack resolution;
- realtime per-frame adaptation.

---

# 3. Source of Truth / Dependencies

M1-015 kế thừa các frozen contract trước đó.

## 3.1. M1-014 — Profile / TeamPerformance

M1-014 freeze luồng:

```text
TelemetryEvent
→ MatchTelemetry
→ MatchScore
→ PlayerAIProfile / TeamProfile / TeamPerformance
```

M1-015 không tính lại MatchScore hoặc TeamPerformance.

M1-014 current baseline cũng freeze:

```text
Teamwork = DEFERRED
ResourceEfficiency = DEFERRED
```

Do đó:

```text
TeamPerformance.status = INCOMPLETE
TeamPerformance.score = null
```

trong current baseline.

M1-015 **MUST** chấp nhận trạng thái này.

Không được invent partial TeamPerformance để ép adaptive path chạy.

---

## 3.2. M1-013 — Stalker Contract

Stalker runtime vẫn dùng Traditional AI.

Stalker configurable parameters gồm:

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

M1-015 không được suy:

```text
configurable
=
adaptive-authorized
```

Adaptive authority v0 là whitelist riêng được freeze tại Section 11.

M1-013 cũng freeze:

- Vision/LOS;
- Detection Meter;
- CurrentTarget/DetectionTarget;
- LastKnownPosition;
- Search;
- Attack;
- mandatory Recover;
- Final Hunt là phase/configuration context, không phải FSM state.

M1-015 không được phá các contract này.

---

## 3.3. AI Architecture Boundary

Architecture freeze:

```text
Player / Team Profile
        ↓
       AED
        ↓
Scenario Configuration
        ↓
Traditional Gameplay AI
```

AED:

- chỉ chuẩn bị/điều chỉnh bounded Scenario Configuration;
- không thay FSM;
- không tạo mechanic;
- không ra lệnh `CHASE`, `ATTACK`, `SEARCH`;
- không thay Sensor/Target Selection authority.

GenAI nằm ngoài gameplay decision flow.

---

## 3.4. Planning / Implementation Baseline

Planning baseline của M1-015 yêu cầu tối thiểu:

```text
Whitelist
Min / Max
Phase-boundary
Fallback
```

Implementation baseline cũng yêu cầu:

```text
Mọi adaptive parameter có min/max
Chỉ đổi 1–2 parameter tại phase boundary
Scenario Generator dùng content whitelist
Scenario Validator reject invalid route/spawn/budget
AED lỗi → Fixed Director
Adaptive decision có before/after/reason
```

Final Hunt Escape Door timer baseline:

```text
45–60 seconds
```

M1-015 chỉ freeze numerical `45..60` cho timer này vì source hiện tại đã xác định range.

Các numerical bounds khác không được tự invent.

---

# 4. Architecture Boundary

Frozen architecture:

```text
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
Gameplay Configuration Layer
        ↓
Traditional Gameplay AI
```

## 4.1. Ownership

| Component | Responsibility |
|---|---|
| Profile / TeamPerformance | Processed input; không command gameplay |
| AED Policy | Chọn bounded/configured candidate decision |
| Adaptive Parameter Registry | Whitelist metadata, bounds, timing, pressure-axis ownership |
| Content Whitelist | Designer-authored valid IDs/blocks |
| Scenario Validator | Reject invalid candidate |
| FixedDirector | PRE_MATCH: resolve full known-safe fixed ScenarioConfig; MID_MATCH: preserve last valid AppliedScenarioConfig per fallback contract |
| Host / Gameplay Config Layer | Apply validated config tại allowed timing |
| Traditional Monster AI | Consume resulting configuration và chạy frozen FSM/rules |

---

## 4.2. Fallback Boundary

Fallback semantics phụ thuộc decision point:

```text
PRE_MATCH failure
→ FULL_FIXED_CONFIG
→ resolve FIXED_BASELINE_V1
```

```text
MID_MATCH failure
→ KEEP_LAST_VALID_CONFIG
→ do not replace full live ScenarioConfig
```

---

## 4.3. Forbidden Direct Control

Không tồn tại luồng:

```text
AED → CHASE
AED → ATTACK
AED → SEARCH
AED → CurrentTarget
AED → DetectionTarget
AED → LastKnownPosition
AED → hidden Player position
AED → Vision Sensor visibility override
AED → Detection Meter bypass
AED → Attack hit resolution
```

AED chỉ output ScenarioConfig đã được validate.

---

# 5. Terminology

## 5.1. Base ScenarioConfig

`Base ScenarioConfig` là known-valid designer-authored/resolved configuration trước adaptive modification.

Adaptive AED v0 chỉ được thay đúng whitelist field/key.

---

## 5.2. Candidate ScenarioConfig

`Candidate ScenarioConfig` là config sau policy decision nhưng trước Scenario Validator.

Candidate chưa được phép apply.

---

## 5.3. Applied ScenarioConfig

Config chỉ trở thành `Applied ScenarioConfig` khi:

```text
Scenario Validator = VALID
```

hoặc tại PRE_MATCH được resolve từ valid full FixedDirector fallback. Mid-match `KEEP_LAST_VALID_CONFIG` không tạo replacement config mới.

---

## 5.4. Adaptive Authority

Adaptive authority là quyền policy được thay một field/key.

Field tồn tại trong ScenarioConfig **không** tự tạo adaptive authority.

---

## 5.5. Decision Point

Controlled decision point v0:

```text
PRE_MATCH
ALLOWED_PHASE_BOUNDARY
FINAL_HUNT_SETUP
```

Không có arbitrary realtime decision point.

---

## 5.6. Pressure Axis

Pressure axis là logical grouping dùng cho fairness validation của adaptive Stalker parameters.

Frozen pressure-axis type:

```text
DetectionPressure
ChasePressure
SearchPressure
NONE
```

`NONE` là non-Stalker-pressure value cho adaptive numerical keys như SupportItemBudget và FinalHunt.EscapeDoorTimer.

---

## 5.7. FixedDirector

`FixedDirector` là safe non-adaptive path.

Frozen action semantics:

```text
PRE_MATCH
→ FULL_FIXED_CONFIG
→ resolve designer-authored/versioned FIXED_BASELINE_V1

MID_MATCH
→ KEEP_LAST_VALID_CONFIG
→ preserve current last valid AppliedScenarioConfig
```

FixedDirector không phải một Monster AI.

---

## 5.8. FallbackAction

```text
FallbackAction
=
NONE
| FULL_FIXED_CONFIG
| KEEP_LAST_VALID_CONFIG
```

FallbackAction mô tả safe action được dùng sau decision result.

---

## 5.9. NO_CHANGE

`NO_CHANGE` là valid AdaptiveDecision result khi policy evaluate thành công nhưng không có adaptive delta.

```text
requestedChanges = []
→ current AppliedScenarioConfig unchanged
→ scenarioConfigVersion unchanged
→ no fallback
```

---

# 6. AED Input Contract

Logical input:

```text
AEDPolicyInput
{
    playerProfileRefs[],
    teamProfileRef,
    teamPerformance,
    inputVersions,
    currentScenarioConfig,
    decisionPoint,
    phaseContext,
    policyVersion,
    parameterRegistryVersion,
    contentWhitelistVersion
}
```

M1-015 không yêu cầu database object giống hệt structure này.

Semantic phải giữ nguyên.

---

## 6.1. Required TeamPerformance Input

Adaptive policy v0 yêu cầu:

```text
TeamPerformance.status = COMPLETE
TeamPerformance.score != null
```

Không dùng synthetic score.

---

## 6.2. Required Version Input

Policy phải resolve/support được các version cần thiết:

```text
profileFormulaVersion
normalizationConfigVersion
TeamPerformance formula/config metadata
policyVersion
parameterRegistryVersion
contentWhitelistVersion
```

Nếu required version unsupported:

```text
Adaptive input = INVALID
→ FixedDirector
```

---

## 6.3. Raw Telemetry Boundary

AED v0 không đọc raw `TelemetryEvent` để tự tạo difficulty heuristic.

Không:

```text
PLAYER_DOWNED vừa xảy ra
→ tăng ChaseSpeed ngay
```

Không:

```text
NOISE_EMITTED vừa xảy ra
→ đổi SearchDuration ngay
```

AED chỉ consume processed input contract.

---

# 7. Input Eligibility — FROZEN

Adaptive AED policy chỉ được evaluate khi **tất cả** điều kiện sau đúng:

```text
TeamPerformance.status = COMPLETE
AND TeamPerformance.score != null
AND required Profile/policy input valid
AND required input versions supported
AND policyVersion supported
AND parameter registry valid
AND content whitelist valid
AND policy config valid
```

Nếu bất kỳ điều kiện nào false:

```text
Adaptive AED policy MUST NOT run
→ safe fallback action theo decisionPoint
```

---

## 7.1. TeamPerformance INCOMPLETE

Frozen rule:

```text
TeamPerformance.status = INCOMPLETE
OR TeamPerformance.score = null
→ Adaptive policy does not run
→ safe fallback action theo decisionPoint
```

Không:

```text
missing component = 0
```

Không:

```text
renormalize TeamPerformance weights
```

Không:

```text
survival profile riêng
→ invent difficulty heuristic
```

Không:

```text
noise profile riêng
→ invent difficulty heuristic
```

---

## 7.2. Current Baseline Consequence

M1-014 current baseline:

```text
Teamwork = DEFERRED
ResourceEfficiency = DEFERRED
→ TeamPerformance = INCOMPLETE
```

Do đó current M1 baseline có thể đi:

```text
AED Input Gate
→ INELIGIBLE
→ safe fallback action theo decisionPoint
```

Điều này **không làm M1-015 incomplete**.

M1-015 freeze policy contract cho future input `COMPLETE`.

---

# 8. ScenarioConfig Schema — FROZEN

Không dùng free-form:

```text
parameters: anything
```

`ScenarioConfig` mô tả **resolved configuration contract** mà Gameplay Configuration Layer có thể consume.

Logical schema v0:

```text
ScenarioConfig
{
    scenarioConfigVersion: string,
    policyVersion: string,
    configSource: FIXED | ADAPTIVE,

    mapId: string,
    monsterType: string,
    objectiveSpawnSetId: string,

    supportItemBudget: number,

    monsterParameters: {
        DetectionFillRate: number,
        DetectionDecayRate: number,
        ChaseSpeed: number,
        SearchDuration: number
    },

    routeModifier: string | null,

    finalHuntParameters: {
        escapeDoorTimerSeconds: number
    },

    fallbackConfigId: string | null
}
```

---

## 8.1. Explicit Monster Parameter Object

Trong M1-015 v0, `monsterParameters` không phải arbitrary dictionary.

Object này chỉ expose các key mà **M1-015 sở hữu adaptive authority**:

```text
DetectionFillRate
DetectionDecayRate
ChaseSpeed
SearchDuration
```

Các non-adaptive runtime Stalker parameter vẫn tồn tại trong gameplay/base monster configuration theo M1-013, bao gồm:

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

Chúng không xuất hiện trong logical adaptive-owned `monsterParameters` object của M1-015 **không có nghĩa runtime không có các value này**.

Semantic chỉ là:

```text
AED v0
→ không sở hữu quyền adaptive modify các key đó
```

---

## 8.2. `configSource`

```text
FIXED
→ FixedDirector / designer-authored resolution

ADAPTIVE
→ candidate đã pass AED Policy + Scenario Validator
```

`configSource` là field phân biệt Fixed và Adaptive path.

Không dùng `policyVersion = null` để phân biệt hai path.

---

## 8.3. `policyVersion` — REQUIRED for FIXED and ADAPTIVE

`policyVersion` **MUST** tồn tại trên mọi ScenarioConfig v0.

Semantic:

```text
policyVersion
=
version của M1-015 ScenarioConfig / Fairness contract
được dùng để resolve và validate config
```

Do đó:

```text
configSource = FIXED
→ policyVersion vẫn REQUIRED
```

```text
configSource = ADAPTIVE
→ policyVersion vẫn REQUIRED
```

FIXED config có `policyVersion` không có nghĩa FixedDirector đã chạy adaptive scoring.

AdaptiveDecision vẫn trace `policyVersion` của decision policy riêng theo cùng contract versioning scheme.

---

## 8.4. `fallbackConfigId`

`fallbackConfigId` là logical reference phục vụ fallback/audit.

Full pre-match fixed fallback dùng:

```text
FallbackScenarioConfigId = FIXED_BASELINE_V1
```

Mid-match fallback **không** dùng `FIXED_BASELINE_V1` để blanket-replace toàn current ScenarioConfig; semantic này được freeze tại Sections 26–29.

# 9. Config Field vs Adaptive Authority

Frozen distinction:

```text
ScenarioConfig field exists
≠
AED may adaptively modify it
```

Authority table:

| Field / Group | Exists in ScenarioConfig | Adaptive AED v0 Authority |
|---|---:|---:|
| `mapId` | YES | **NO** |
| `monsterType` | YES | **NO** |
| `objectiveSpawnSetId` | YES | **YES — PRE_MATCH only** |
| `supportItemBudget` | YES | **YES — registry timing** |
| `monsterParameters.DetectionFillRate` | YES | **YES — registry timing** |
| `monsterParameters.DetectionDecayRate` | YES | **YES — registry timing** |
| `monsterParameters.ChaseSpeed` | YES | **YES — registry timing** |
| `monsterParameters.SearchDuration` | YES | **YES — registry timing** |
| `routeModifier` | YES | **YES — ALLOWED_PHASE_BOUNDARY only** |
| `finalHuntParameters.escapeDoorTimerSeconds` | YES | **YES — FINAL_HUNT_SETUP only** |

---

## 9.1. Map Authority

`mapId`:

```text
→ designer-authored / FixedDirector selection
→ Adaptive AED v0 MUST NOT select/change map based on TeamPerformance
```

Planning may contain scenario-generation support for map selection, nhưng M1-015 v0 intentionally narrows **adaptive authority**.

---

## 9.2. Monster Authority

`monsterType`:

```text
→ designer-authored / FixedDirector selection
→ Adaptive AED v0 MUST NOT modify
```

Không:

```text
TeamPerformance
→ Stalker đổi thành Listener/Warden
```

---

## 9.3. Route Modifier Authority

`routeModifier`:

```text
PRE_MATCH
→ Adaptive AED v0 MUST NOT modify
```

```text
ALLOWED_PHASE_BOUNDARY
→ adaptive modification may be requested
→ designer whitelist + route validation + safety rules still required
```

Không có adaptive pre-match routeModifier override trong M1-015 v0.

# 10. Content Whitelist

Mọi discrete content selection phải thuộc designer-authored whitelist.

Logical registry:

```text
ScenarioContentWhitelist
{
    contentWhitelistVersion,
    allowedMapIds[],
    allowedMonsterTypesByScenario[],
    allowedObjectiveSpawnSetIdsByMap[],
    allowedRouteModifierIdsByMap[],
    allowedSupportContentIds...
}
```

M1-015 không freeze storage format.

---

## 10.1. No Generated Content

AED v0 không được sinh:

- arbitrary map ID;
- arbitrary monster type;
- arbitrary spawn coordinate;
- arbitrary route graph;
- arbitrary door mutation;
- arbitrary item;
- arbitrary Final Hunt mechanic.

---

## 10.2. P0 Content Context

Current project baseline ưu tiên Research Facility ở P0.

Map 2 là Conditional.

M1-015 v0 không dùng adaptive policy để tự chuyển map nhằm vượt qua content prioritization.

---

# 11. Adaptive Stalker Parameter Whitelist — FROZEN

Adaptive AED v0 chỉ được modify đúng **4** Stalker parameter:

```text
DetectionFillRate
DetectionDecayRate
ChaseSpeed
SearchDuration
```

Whitelist đóng.

Unknown/non-whitelisted Stalker key:

```text
→ INVALID
→ reject adaptive decision
```

---

## 11.1. Explicit Non-Adaptive Stalker List

M1-015 v0 **MUST NOT** adapt:

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

Không tự thêm parameter khác từ M1-013 vào whitelist.

---

## 11.2. Combat Fairness Envelope

Đặc biệt không adaptive:

```text
AttackRange
AttackWindup
AttackRecovery
StalkerDamagePercent
```

M1-015 v0 giữ combat envelope ổn định.

---

## 11.3. Perception Envelope

Không adaptive:

```text
VisionDistance
VisionAngle
```

M1-015 v0 giữ physical perception envelope ổn định.

---

# 12. Adaptive Parameter Registry

Logical contract:

```text
AdaptiveParameterRule
{
    key,
    defaultValue,
    minValue,
    maxValue,
    allowedTiming[],
    pressureAxis:
        DetectionPressure
        | ChasePressure
        | SearchPressure
        | NONE,
    owner
}
```

Frozen numerical registry keys:

```text
DetectionFillRate
DetectionDecayRate
ChaseSpeed
SearchDuration
SupportItemBudget
FinalHunt.EscapeDoorTimer
```

`objectiveSpawnSetId` và `routeModifier` là discrete whitelist selection, không dùng numerical min/max registry.

---

## 12.1. Registry Mapping — FROZEN

| Key | Owner | pressureAxis |
|---|---|---|
| `DetectionFillRate` | Stalker config / Traditional AI parameter | `DetectionPressure` |
| `DetectionDecayRate` | Stalker config / Traditional AI parameter | `DetectionPressure` |
| `ChaseSpeed` | Stalker config / Traditional AI parameter | `ChasePressure` |
| `SearchDuration` | Stalker config / Traditional AI parameter | `SearchPressure` |
| `SupportItemBudget` | Scenario / support allocation config | `NONE` |
| `FinalHunt.EscapeDoorTimer` | Final Hunt gameplay/scenario config | `NONE` |

AED chỉ request bounded values.

Gameplay owner vẫn định nghĩa semantic của parameter.

---

## 12.2. `pressureAxis = NONE`

`NONE` dùng cho adaptive numerical key không phải Stalker pressure-axis knob.

Frozen rule:

```text
pressureAxis = NONE
→ không count vào Stalker aggressive-pressure-axis total
```

Nhưng:

```text
pressureAxis = NONE
≠ automatically fair
```

`SupportItemBudget` và `FinalHunt.EscapeDoorTimer` vẫn phải pass:

- bounds;
- allowed timing;
- owner contract;
- support/final-hunt fairness;
- Scenario Validator.

Unsupported/missing `pressureAxis` metadata:

```text
→ parameter registry INVALID
→ Adaptive policy MUST NOT run
→ fallback theo decision point
```

# 13. Parameter Bounds

Mọi adaptive numerical key **MUST** có:

```text
defaultValue
minValue
maxValue
```

trong versioned registry.

Rule:

```text
minValue <= defaultValue <= maxValue
```

Nếu candidate:

```text
requestedValue < minValue
OR requestedValue > maxValue
→ INVALID
```

M1-015 v0 **không silently clamp** out-of-bound candidate.

---

## 13.1. Missing Bounds

Nếu adaptive numerical key thiếu bounds:

```text
parameter registry = INVALID
→ Adaptive policy MUST NOT run
→ fallback action theo decision point
```

Không:

```text
missing bound
→ unrestricted value
```

---

## 13.2. Numerical Values Not Frozen by Source

Đối với:

```text
DetectionFillRate
DetectionDecayRate
ChaseSpeed
SearchDuration
SupportItemBudget
```

numerical:

```text
default/min/max
→ CONFIGURABLE
→ MUST BE PROVIDED BY VERSIONED PARAMETER REGISTRY
```

M1-015 không invent tuning number.

---

## 13.3. Final Hunt Escape Door Timer Bound

Source baseline freeze:

```text
45 <= FinalHunt.EscapeDoorTimer <= 60 seconds
```

Do đó M1-015 v0 freeze:

```text
min = 45
max = 60
unit = seconds
```

Default vẫn phải được versioned registry/designer config cung cấp và nằm trong `[45,60]`.

---

# 14. Pressure Axes — FROZEN

Frozen pressure-axis type:

```text
DetectionPressure
ChasePressure
SearchPressure
NONE
```

`DetectionPressure`, `ChasePressure`, `SearchPressure` là **Stalker pressure axes**.

`NONE` là non-pressure metadata value và không tham gia max-one-aggressive-Stalker-pressure-axis rule.

---

## 14.1. DetectionPressure

Mapping:

```text
DetectionFillRate
→ DetectionPressure

DetectionDecayRate
→ DetectionPressure
```

Aggressive direction:

```text
DetectionFillRate ↑
→ DetectionPressure ↑
```

```text
DetectionDecayRate ↓
→ DetectionPressure ↑
```

Vì M1-013 freeze target mất LOS làm Detection Meter decay theo `DetectionDecayRate`, decay rate thấp hơn làm detection persistence mạnh hơn.

---

## 14.2. ChasePressure

Mapping:

```text
ChaseSpeed
→ ChasePressure
```

Aggressive direction:

```text
ChaseSpeed ↑
→ ChasePressure ↑
```

---

## 14.3. SearchPressure

Mapping:

```text
SearchDuration
→ SearchPressure
```

Aggressive direction:

```text
SearchDuration ↑
→ SearchPressure ↑
```

---

## 14.4. Non-Pressure Keys

Mapping:

```text
SupportItemBudget
→ NONE

FinalHunt.EscapeDoorTimer
→ NONE
```

`NONE` không được count như Stalker pressure axis.

Tuy nhiên key vẫn phải pass fairness riêng.

---

## 14.5. Pressure Direction Validation

Policy/validator phải xác định Stalker pressure-key change là:

```text
MORE_AGGRESSIVE
LESS_AGGRESSIVE
NEUTRAL
```

theo frozen direction semantics.

Không được đổi direction semantic bằng tuning config.

Với:

```text
pressureAxis = NONE
```

không gán `MORE_AGGRESSIVE` vào Stalker pressure-axis counter.

# 15. Pre-Match Decision Contract

Decision point:

```text
PRE_MATCH
```

Chỉ chạy Adaptive path nếu AED Input Eligibility = valid.

---

## 15.1. Pre-Match Fixed Fields

Adaptive AED v0 không modify:

```text
mapId
monsterType
routeModifier
```

`mapId` và `monsterType` được lấy từ FixedDirector / designer scenario context.

`routeModifier` initial state cũng được resolve từ designer/base fixed scenario content tại PRE_MATCH; Adaptive AED v0 không override routeModifier ở PRE_MATCH.

---

## 15.2. Pre-Match Adaptive-Allowed Fields

Nếu input `COMPLETE` và registry/whitelist valid, policy có thể resolve:

```text
objectiveSpawnSetId
supportItemBudget
DetectionFillRate
DetectionDecayRate
ChaseSpeed
SearchDuration
```

Mỗi field vẫn phải pass timing/whitelist/bounds/safety contract.

Explicitly not adaptive at PRE_MATCH:

```text
routeModifier
FinalHunt.EscapeDoorTimer
```

`FinalHunt.EscapeDoorTimer` canonical adaptive timing là `FINAL_HUNT_SETUP`.

---

## 15.3. Pre-Match Pressure Fairness

So với designer/FIXED base config, pre-match adaptive resolution **MUST NOT make more than one Stalker pressure axis more aggressive**.

Ví dụ invalid:

```text
ChaseSpeed ↑
AND SearchDuration ↑
```

vì:

```text
ChasePressure ↑
AND SearchPressure ↑
```

Ví dụ structural-valid về pressure-axis count:

```text
ChaseSpeed ↑
AND SearchDuration ↓
```

nếu cả hai value nằm bounds và toàn candidate pass validator.

`SupportItemBudget` có `pressureAxis = NONE`, nên không count như Stalker pressure axis; vẫn phải pass support-budget fairness.

Không có numerical “difficulty delta” formula ở M1-015 v0.

# 16. Phase-Boundary Adjustment Contract

Decision point:

```text
ALLOWED_PHASE_BOUNDARY
```

Phase boundary phải đến từ frozen gameplay/phase lifecycle.

Không có arbitrary timer-based polling adaptation.

---

## 16.1. Maximum Changed Keys

Tại một allowed phase boundary:

```text
AED may modify at most 1–2 adaptive keys
```

`changed key` nghĩa là resolved value khác current AppliedScenarioConfig value.

Request cùng value:

```text
before == after
→ không tính là changed key
```

Nếu policy evaluate hợp lệ nhưng không có changed key:

```text
→ AdaptiveDecision.result = NO_CHANGE
```

NO_CHANGE semantic được freeze tại Section 29.

---

## 16.2. Allowed Boundary Change Types

Boundary policy có thể request:

```text
DetectionFillRate
DetectionDecayRate
ChaseSpeed
SearchDuration
SupportItemBudget
routeModifier
```

với rule:

```text
routeModifier
→ ALLOWED_PHASE_BOUNDARY only
```

`routeModifier` request ở PRE_MATCH phải bị:

```text
→ TIMING_REJECTED
```

Mỗi key vẫn phải pass registry/whitelist/timing contract tương ứng.

Không có generic:

```text
objectiveTiming
```

adaptive permission.

Final Hunt Escape Door timer chỉ dùng `FINAL_HUNT_SETUP`, không dùng generic phase-boundary update khi timer đã start.

---

## 16.3. No Realtime Change

Không được:

- update per frame;
- update per second;
- continuously interpolate policy output;
- đổi khi Attack wind-up đang diễn ra;
- đổi trực tiếp trong Detection progression như reaction tới một event;
- đổi giữa active interaction nếu timing contract không cho phép;
- đổi ngay vì Player vừa Down;
- đổi Stalker state.

# 17. Compound-Pressure Fairness

## 17.1. Maximum One More-Aggressive Pressure Axis

Freeze:

```text
At PRE_MATCH adaptive resolution:
number of Stalker pressure axes becoming MORE_AGGRESSIVE <= 1
```

```text
At ALLOWED_PHASE_BOUNDARY:
number of Stalker pressure axes becoming MORE_AGGRESSIVE <= 1
```

Nếu hai changed keys tồn tại:

- key thứ hai có thể `LESS_AGGRESSIVE`;
- key thứ hai có thể `NEUTRAL`;
- key thứ hai có thể có `pressureAxis = NONE`;
- nhưng key thứ hai **MUST NOT** làm một Stalker pressure axis khác trở thành `MORE_AGGRESSIVE`.

Không tạo numerical aggregate difficulty score trong M1-015 v0.

---

## 17.2. Double Detection Buff — Forbidden

Freeze explicit invalid combination:

```text
DetectionFillRate ↑
AND
DetectionDecayRate ↓
```

Cả hai đều làm:

```text
DetectionPressure
→ MORE_AGGRESSIVE
```

Do đó:

```text
→ CandidateValidationStatus = INVALID
→ reasonCode = PRESSURE_RULE_REJECTED
→ candidate rejected
→ no partial apply
→ final result = FIXED_FALLBACK
→ fallbackAction according to decisionPoint
```

---

## 17.3. Multi-Axis Aggressive Stack — Forbidden

Freeze explicit invalid example:

```text
ChaseSpeed ↑
AND
SearchDuration ↑
```

vì:

```text
ChasePressure ↑
AND
SearchPressure ↑
```

Do đó:

```text
→ CandidateValidationStatus = INVALID
→ reasonCode = PRESSURE_RULE_REJECTED
→ final result = FIXED_FALLBACK
```

Decision vẫn INVALID dù chỉ có hai changed keys.

---

## 17.4. Example Structurally Allowed Two-Key Change

Các example sau **MAY** pass pressure-axis rule:

```text
ChaseSpeed ↑
AND
SearchDuration ↓
```

hoặc:

```text
DetectionFillRate ↑
AND
SupportItemBudget ↑
```

Trong example thứ hai:

```text
SupportItemBudget
→ pressureAxis = NONE
```

Tuy nhiên:

```text
passing pressure-axis rule
≠ candidate automatically VALID
```

Candidate vẫn phải pass:

- parameter bounds;
- allowed timing;
- adaptive/content whitelist;
- support/route/final-hunt fairness;
- content compatibility;
- Scenario Validator.

---

## 17.5. Non-Pressure Keys

```text
SupportItemBudget
FinalHunt.EscapeDoorTimer
→ pressureAxis = NONE
```

Frozen rule:

```text
pressureAxis = NONE
→ does not count toward Stalker aggressive-axis total
```

Nhưng:

```text
pressureAxis = NONE
≠ automatically fair
```

Các key này vẫn phải pass:

- bounds;
- timing;
- owner-specific fairness;
- Scenario Validator.

---

# 18. Objective Spawn Safety

Adaptive AED v0 được phép chọn:

```text
objectiveSpawnSetId
```

tại:

```text
PRE_MATCH
```

chỉ từ designer-authored whitelist.

---

## 18.1. Required Validation

Spawn set phải:

- thuộc whitelist của map/scenario;
- tương thích `mapId`;
- không duplicate invalid objective placement;
- objective reachable;
- không gây soft-lock;
- không yêu cầu impossible traversal;
- route validation pass;
- không thay gameplay objective rule.

---

## 18.2. No Arbitrary Coordinate Generation

Không:

```text
spawnPosition = AI-generated Vector3
```

Không:

```text
LLM-generated coordinate
```

Không:

```text
AED invent new spawn point
```

Chỉ dùng designer-authored `objectiveSpawnSetId`.

---

# 19. Support Item Budget Contract

`supportItemBudget`:

```text
→ adaptive-authorized
→ numerical
→ bounded
→ registry-owned
```

Allowed timing:

```text
PRE_MATCH
OR
ALLOWED_PHASE_BOUNDARY
```

chỉ khi registry cho phép timing đó.

---

## 19.1. Non-Retroactive Rule

Phase-boundary budget change chỉ ảnh hưởng:

```text
future support allocation / future eligible spawn
```

Không được:

```text
budget giảm
→ delete item Player đang sở hữu
```

Không được:

```text
budget giảm
→ remove already-spawned item
```

trái gameplay contract.

---

## 19.2. No Direct Item Injection

Budget tăng không cho AED quyền:

```text
spawn item cạnh Player ngay lập tức
```

trừ khi gameplay/item spawn contract có valid future allocation point.

Mọi item vẫn phải thuộc content/spawn whitelist.

---

# 20. Route Modifier Contract

`routeModifier`:

```text
→ adaptive-authorized ONLY at ALLOWED_PHASE_BOUNDARY
→ MUST NOT be adaptively modified at PRE_MATCH
→ discrete designer-authored whitelist
→ route compatibility required
→ Scenario Validator required
```

Không free-form route graph mutation.

---

## 20.1. Timing — FROZEN

```text
PRE_MATCH
→ adaptive routeModifier request = INVALID
→ TIMING_REJECTED
```

```text
ALLOWED_PHASE_BOUNDARY
→ routeModifier adaptive request may proceed to whitelist/safety validation
```

---

## 20.2. Route Safety

Candidate route modifier phải:

- thuộc designer-authored whitelist;
- tương thích map/scenario;
- không soft-lock;
- không làm objective unreachable;
- không làm exit unreachable;
- giữ ít nhất một legal route;
- không trái Door/Navigation frozen contract;
- không teleport Player;
- không teleport Monster;
- không tiết lộ hidden Player location.

---

## 20.3. Invalid Route

Nếu timing/route validation fail:

```text
Candidate ScenarioConfig = INVALID
→ reject whole adaptive decision
→ fallback action theo decision point
```

Không apply approximate route.

# 21. Final Hunt Parameter Contract

Current frozen numerical adaptive Final Hunt key:

```text
FinalHunt.EscapeDoorTimer
```

ScenarioConfig field:

```text
finalHuntParameters.escapeDoorTimerSeconds
```

---

## 21.1. Bound

Frozen source-supported bound:

```text
45 <= escapeDoorTimerSeconds <= 60
```

Unit:

```text
seconds
```

---

## 21.2. Timing

Allowed adaptive timing:

```text
FINAL_HUNT_SETUP
```

Decision **MUST** be resolved before current Escape Door timer instance starts.

---

## 21.3. Immutability After Start

Sau khi timer start:

```text
escapeDoorTimerSeconds
→ immutable for current timer instance
```

Không:

```text
running timer
→ increase duration
```

Không:

```text
running timer
→ decrease duration
```

Không:

```text
policy reevaluate
→ reset timer
```

---

## 21.4. No Generic Objective Timing Permission

M1-015 v0 không tạo:

```text
ObjectiveTiming = adaptive
```

Chỉ key explicit có canonical contract/bounds/timing mới được adaptive.

Unknown objective timer key:

```text
→ NOT WHITELISTED
→ INVALID
```

---

# 22. Fairness Rules — FROZEN

M1-015 v0 freeze các hard rule sau.

1. AED chỉ modify adaptive whitelist.
2. Configurable parameter không tự đồng nghĩa adaptive-authorized.
3. Numerical adaptive key phải có default/min/max.
4. Missing bounds → policy config invalid → safe fallback theo decision point.
5. Out-of-bound request → INVALID; không silently clamp.
6. Mỗi allowed phase boundary tối đa 1–2 changed keys.
7. Tối đa một Stalker pressure axis được tăng aggression trong một boundary decision.
8. `pressureAxis = NONE` không count vào Stalker pressure-axis limit.
9. `SupportItemBudget → NONE`.
10. `FinalHunt.EscapeDoorTimer → NONE`.
11. Không double DetectionPressure bằng `DetectionFillRate ↑ + DetectionDecayRate ↓`.
12. Không multi-axis aggressive stack.
13. `routeModifier` không adaptive tại PRE_MATCH.
14. `routeModifier` chỉ adaptive tại `ALLOWED_PHASE_BOUNDARY`.
15. Không bypass LOS.
16. Không bypass Detection Meter.
17. Không thay Vision Sensor physical visibility.
18. Không thêm FSM state.
19. Không thay FSM topology.
20. Không command `CHASE / ATTACK / SEARCH`.
21. Không command `CurrentTarget / DetectionTarget`.
22. Không update `LastKnownPosition`.
23. Không dùng hidden Player transform/location.
24. Không adaptive `VisionDistance`.
25. Không adaptive `VisionAngle`.
26. Không adaptive `AttackRange`.
27. Không adaptive `AttackWindup`.
28. Không adaptive `AttackRecovery`.
29. Không adaptive `StalkerDamagePercent`.
30. Không đổi active Attack contract giữa attack.
31. Không adaptive `monsterType`.
32. Không adaptive `mapId`.
33. Không spawn objective ngoài designer whitelist.
34. Không arbitrary spawn coordinates.
35. Không route-lock / soft-lock.
36. Luôn giữ legal route theo scenario contract.
37. Không sinh content ngoài designer whitelist.
38. Không đổi core gameplay mechanic.
39. Không dùng incomplete TeamPerformance như synthetic complete signal.
40. Không renormalize partial TeamPerformance.
41. Không dùng Player survival/noise riêng để invent partial AED heuristic.
42. Không dùng raw TelemetryEvent trực tiếp làm AED rule.
43. Không dùng GenAI làm gameplay/config decision.
44. Không retroactive support-item removal.
45. Không retroactive Final Hunt timer change.
46. Validator failure phải reject candidate.
47. Adaptive decision application phải atomic.
48. PRE_MATCH fallback có thể resolve full `FIXED_BASELINE_V1`.
49. MID_MATCH fallback không được replace full current ScenarioConfig.
50. MID_MATCH fallback phải keep last valid AppliedScenarioConfig.
51. Không automatic rollback adaptive change đã apply hợp lệ trước đó.
52. FIXED ScenarioConfig vẫn phải carry supported `policyVersion`.
53. `NO_CHANGE` là valid result, không phải fallback/rejection.
54. `NO_CHANGE` không mutate config và không tạo new scenarioConfigVersion.
55. Fallback không reset gameplay runtime state.

---

# 23. Forbidden Decisions

Explicitly forbidden examples:

```text
TeamPerformance = INCOMPLETE
→ use survival only
→ adaptive ChaseSpeed
```

```text
VisionDistance += X
```

```text
AttackRange += X
```

```text
StalkerDamagePercent += X
```

```text
monsterType = LISTENER based on TeamPerformance
```

```text
DetectionFillRate ↑
+
DetectionDecayRate ↓
same boundary
```

```text
ChaseSpeed ↑
+
SearchDuration ↑
same boundary
```

```text
objectiveSpawnSetId = arbitrary AI coordinate
```

```text
routeModifier = arbitrary graph mutation
```

```text
AED → CHASE
```

```text
AED → CurrentTarget = player_02
```

```text
AED reads hidden Player position
```

```text
Final Hunt timer already running
→ change timer duration
```

```text
Scenario Validator finds one invalid field
→ apply remaining fields
```

---

# 24. Scenario Validator — FROZEN

Canonical pipeline:

```text
Processed Profile / TeamPerformance
        ↓
AED Input Eligibility
        ↓
AED Policy
        ↓
Candidate ScenarioConfig / Zero-Delta Decision
        ↓
Scenario Validator
        ↓
VALID + adaptive delta
→ CandidateValidationStatus = VALID
→ result = APPLIED

VALID + zero delta
→ CandidateValidationStatus = VALID
→ result = NO_CHANGE

INVALID
→ CandidateValidationStatus = INVALID
→ candidate rejected
→ candidate MUST NOT apply
→ resolve fallbackAction by decisionPoint
→ result = FIXED_FALLBACK
```

Nếu adaptive path không thể chạy và không có candidate:

```text
→ CandidateValidationStatus = NOT_EVALUATED
→ result = FIXED_FALLBACK
→ fallbackAction by decisionPoint
```

---

## 24.1. CandidateValidationStatus — FROZEN

Logical enum:

```text
CandidateValidationStatus
=
NOT_EVALUATED
| VALID
| INVALID
```

Semantic:

```text
NOT_EVALUATED
→ adaptive candidate was not produced/evaluated
  because adaptive path was ineligible/unavailable
  or failed before candidate validation
```

```text
VALID
→ candidate/zero-delta decision passed required Scenario Validator checks
```

```text
INVALID
→ candidate was produced/evaluated but failed required validation
→ candidate rejected
→ candidate MUST NOT apply
```

Examples:

```text
requested ChaseSpeed > max
→ CandidateValidationStatus = INVALID
→ reasonCode = BOUND_REJECTED
→ result = FIXED_FALLBACK
```

```text
routeModifier requested at PRE_MATCH
→ CandidateValidationStatus = INVALID
→ reasonCode = TIMING_REJECTED
→ result = FIXED_FALLBACK
```

```text
DetectionFillRate ↑ + DetectionDecayRate ↓
→ CandidateValidationStatus = INVALID
→ reasonCode = PRESSURE_RULE_REJECTED
→ result = FIXED_FALLBACK
```

```text
TeamPerformance INCOMPLETE
→ CandidateValidationStatus = NOT_EVALUATED
→ reasonCode = INPUT_INCOMPLETE
→ result = FIXED_FALLBACK
```

---

## 24.2. Required Validation Checks

Validator **MUST** check tối thiểu:

1. `scenarioConfigVersion` supported.
2. `policyVersion` present and supported for both FIXED and ADAPTIVE ScenarioConfig.
3. schema fields valid.
4. no unknown ScenarioConfig adaptive key.
5. requested adaptive field thuộc whitelist.
6. parameter registry version supported.
7. every numerical adaptive key has bounds/default.
8. requested numerical value inside bounds.
9. requested timing allowed.
10. decision point valid.
11. `routeModifier` adaptive request occurs only at `ALLOWED_PHASE_BOUNDARY`.
12. max changed keys rule.
13. pressureAxis metadata uses only `DetectionPressure | ChasePressure | SearchPressure | NONE`.
14. `DetectionFillRate` maps to `DetectionPressure`.
15. `DetectionDecayRate` maps to `DetectionPressure`.
16. `ChaseSpeed` maps to `ChasePressure`.
17. `SearchDuration` maps to `SearchPressure`.
18. `SupportItemBudget` maps to `NONE`.
19. `FinalHunt.EscapeDoorTimer` maps to `NONE`.
20. `NONE` does not count toward Stalker aggressive-axis total.
21. Stalker pressure-axis fairness.
22. no double DetectionPressure buff.
23. `mapId` valid.
24. `monsterType` valid.
25. adaptive policy did not modify map/monster.
26. `objectiveSpawnSetId` in content whitelist.
27. spawn set compatible with map.
28. spawn set reachable.
29. `supportItemBudget` valid.
30. `routeModifier` in whitelist when requested.
31. route graph remains valid.
32. at least one legal route remains.
33. no soft-lock.
34. no conflicting modifiers.
35. Final Hunt timer in `[45,60]`.
36. Final Hunt timer timing valid.
37. running timer immutability respected.
38. content/config combination compatible with gameplay contract.
39. no forbidden runtime command/data field.
40. `fallbackAction` is valid for `decisionPoint` and final result.
41. PRE_MATCH fixed fallback uses `FULL_FIXED_CONFIG`.
42. MID_MATCH fixed fallback uses `KEEP_LAST_VALID_CONFIG`.
43. mid-match fallback does not replace pre-match-only/immutable fields.
44. `NO_CHANGE` has zero changed keys.
45. `NO_CHANGE` does not mutate current AppliedScenarioConfig.
46. `NO_CHANGE` does not create artificial new `scenarioConfigVersion`.
47. `CandidateValidationStatus` uses only `NOT_EVALUATED | VALID | INVALID`.
48. final `AdaptiveDecision.result` uses only `APPLIED | NO_CHANGE | FIXED_FALLBACK`.
49. `NO_CHANGE` maps to `CandidateValidationStatus = VALID`.
50. invalid candidate maps to `CandidateValidationStatus = INVALID` and final `FIXED_FALLBACK`.
51. no-candidate/ineligible path maps to `NOT_EVALUATED` and final `FIXED_FALLBACK`.
52. invalid candidate requested values do not appear in `resolvedAfter` as applied.

---

## 24.3. No Silent Repair

Validator v0 không được:

```text
out-of-bound
→ clamp
→ apply
```

Không được:

```text
unknown key
→ ignore
→ apply rest
```

Không được:

```text
invalid route
→ choose nearby route automatically
```

Không được:

```text
mid-match adaptive failure
→ load full FIXED_BASELINE_V1 over live match
```

Default invalid-candidate pipeline:

```text
any required adaptive validation failure
→ CandidateValidationStatus = INVALID
→ candidate rejected
→ candidate MUST NOT apply
→ fallbackAction appropriate to decisionPoint
→ AdaptiveDecision.result = FIXED_FALLBACK
```

---

# 25. Atomic Decision Application — FROZEN

Một `AdaptiveDecision` phải atomic.

Nếu decision có:

```text
change A = valid
change B = invalid
```

thì:

```text
reject A
reject B
→ apply neither
→ CandidateValidationStatus = INVALID
→ AdaptiveDecision.result = FIXED_FALLBACK
→ fallbackAction theo decisionPoint
```

Không partial apply.

---

## 25.1. Atomic ScenarioConfig Version

Một `APPLIED` adaptive decision tạo một coherent resulting ScenarioConfig/version.

Không để client/runtime thấy half-updated config.

`NO_CHANGE` không phải apply:

```text
NO_CHANGE
→ current AppliedScenarioConfig unchanged
→ current scenarioConfigVersion unchanged
```

---

## 25.2. Host Apply Boundary

Host/gameplay layer chỉ apply validated adaptive config tại allowed boundary.

Không apply candidate trước validator.

Mid-match rejected candidate không trigger automatic rollback hoặc full baseline replacement.

# 26. FixedDirector Fallback — FROZEN

Fallback là required safety path, nhưng action phụ thuộc decision point.

Trigger tối thiểu:

```text
AED unavailable
OR AED timeout
OR TeamPerformance INCOMPLETE
OR TeamPerformance.score = null
OR required Profile input invalid
OR required input unavailable
OR unsupported input version
OR policy config invalid
OR adaptive bounds missing
OR content whitelist invalid
OR Candidate ScenarioConfig invalid
OR Scenario Validator fail
OR unsupported policy/config version
→ fixed fallback behavior
```

---

## 26.1. Fallback Action Type — FROZEN

Logical enum:

```text
FallbackAction
=
NONE
| FULL_FIXED_CONFIG
| KEEP_LAST_VALID_CONFIG
```

Mapping:

```text
APPLIED
→ fallbackAction = NONE
```

```text
NO_CHANGE
→ fallbackAction = NONE
```

```text
PRE_MATCH failure requiring FIXED_FALLBACK
→ fallbackAction = FULL_FIXED_CONFIG
```

```text
ALLOWED_PHASE_BOUNDARY failure requiring FIXED_FALLBACK
→ fallbackAction = KEEP_LAST_VALID_CONFIG
```

```text
FINAL_HUNT_SETUP adaptive failure before timer starts
→ fallbackAction = KEEP_LAST_VALID_CONFIG
→ retain already-resolved valid base/current Final Hunt timer value
→ do not replace unrelated ScenarioConfig fields
```

---

## 26.2. PRE_MATCH Fallback

Nếu Adaptive path không thể chạy tại `PRE_MATCH`:

```text
FixedDirector
→ resolve full FallbackScenarioConfig
→ FallbackScenarioConfigId = FIXED_BASELINE_V1
→ validate
→ full fixed ScenarioConfig may become AppliedScenarioConfig
```

Vì match chưa bắt đầu, full fixed config có thể resolve:

- `mapId`;
- `monsterType`;
- `objectiveSpawnSetId`;
- `supportItemBudget`;
- base monster parameters;
- initial route state;
- Final Hunt config;
- các designer-authored field khác thuộc full fallback template.

---

## 26.3. MID-MATCH Fallback

Nếu adaptive decision fail sau khi match đã bắt đầu, gồm:

```text
ALLOWED_PHASE_BOUNDARY
FINAL_HUNT_SETUP
```

thì:

```text
→ reject candidate atomically
→ DO NOT load full FIXED_BASELINE_V1 over current match
→ KEEP_LAST_VALID_CONFIG
```

Current `AppliedScenarioConfig` remains authoritative.

Không automatic rollback của adaptive decision đã apply hợp lệ trước đó.

M1-015 v0 không định nghĩa rollback contract.

---

## 26.4. No Adaptive Replacement Generation

Nếu adaptive path fail:

```text
→ use frozen fallbackAction
```

Không:

```text
AED invent new "safer" candidate
```

trong same failed decision.

# 27. Fallback ScenarioConfig Identity — FROZEN

M1-015 v0 freeze logical full fallback identity:

```text
FallbackScenarioConfigId
=
FIXED_BASELINE_V1
```

`FIXED_BASELINE_V1` là **full known-safe fixed scenario template/reference**.

Nó được dùng để resolve full fixed AppliedScenarioConfig tại PRE_MATCH fallback.

Nó **không** đồng nghĩa mid-match failure phải overwrite live config bằng full baseline.

---

## 27.1. Required Fallback Registry Contract

`FIXED_BASELINE_V1` phải resolve tới:

```text
fallbackConfigId
fallbackConfigVersion
scenarioConfigVersion
policyVersion
designer-authored ScenarioConfig
contentWhitelistVersion compatibility
validator-pass state
```

`policyVersion` REQUIRED vì mọi ScenarioConfig v0 đều phải carry supported M1-015 contract version, kể cả `configSource = FIXED`.

---

## 27.2. Full Fallback vs Mid-Match Safe Action

```text
PRE_MATCH
→ FULL_FIXED_CONFIG
→ FIXED_BASELINE_V1 may become full AppliedScenarioConfig
```

```text
MID_MATCH
→ KEEP_LAST_VALID_CONFIG
→ FIXED_BASELINE_V1 does NOT overwrite full current AppliedScenarioConfig
```

Nếu một future not-yet-started subsystem cần field-specific fixed/default value, field đó chỉ được resolve theo chính allowed timing/owner contract của nó.

Không blanket-replace entire live config.

---

## 27.3. Fatal Fallback Configuration Error

Nếu PRE_MATCH full fallback cần `FIXED_BASELINE_V1` nhưng:

```text
FIXED_BASELINE_V1 missing
OR fallback version unsupported
OR fallback config invalid
```

thì:

```text
FATAL CONFIGURATION ERROR
```

M1-015 không cho Adaptive AED invent replacement.

UX/crash handling ngoài scope.

# 28. Mid-Match Fallback Semantics — FROZEN

Frozen mode:

```text
MidMatchFallbackMode
=
KEEP_LAST_VALID_APPLIED_CONFIG
```

Equivalent `AdaptiveDecision.fallbackAction`:

```text
KEEP_LAST_VALID_CONFIG
```

---

## 28.1. ALLOWED_PHASE_BOUNDARY Failure

Nếu adaptive decision fail tại `ALLOWED_PHASE_BOUNDARY`:

```text
→ reject candidate atomically
→ result = FIXED_FALLBACK
→ fallbackAction = KEEP_LAST_VALID_CONFIG
→ current AppliedScenarioConfig remains authoritative
→ no adaptive change
```

Không:

```text
→ load full FIXED_BASELINE_V1
```

Không replace:

- `mapId`;
- `monsterType`;
- already-resolved `objectiveSpawnSetId`;
- current support state retroactively;
- current route state arbitrarily;
- Stalker config bằng unrelated pre-match baseline values;
- Final Hunt state.

---

## 28.2. No Automatic Rollback

Một adaptive change đã apply hợp lệ trước đó:

```text
previous valid adaptive value
→ remains part of last valid AppliedScenarioConfig
```

Decision failure sau đó không tự rollback value này.

M1-015 v0 không định nghĩa automatic rollback.

---

## 28.3. FINAL_HUNT_SETUP Failure

Nếu adaptive Final Hunt timer decision fail tại `FINAL_HUNT_SETUP` trước timer start:

```text
→ reject adaptive timer candidate
→ fallbackAction = KEEP_LAST_VALID_CONFIG
→ retain valid timer value already resolved in current/base AppliedScenarioConfig
→ do not replace unrelated current-match fields
```

Sau timer start, timer immutable theo Section 21.

Nếu current/base timer value itself invalid, đó là configuration validation error; AED không invent replacement.

---

## 28.4. Preserve Runtime State

Mid-match fallback không:

- reset match;
- reset Player state;
- reset objective progress;
- teleport Player;
- teleport Monster;
- reset Stalker FSM;
- command Stalker FSM transition;
- modify active Attack;
- retroactively modify active Final Hunt timer.

Traditional AI tiếp tục từ runtime state hiện tại.

Ví dụ:

```text
Stalker currently CHASE
→ KEEP_LAST_VALID_CONFIG does not force PATROL
```

# 29. AdaptiveDecision / Explainability

Logical contract:

```text
AdaptiveDecision
{
    decisionId,
    decisionPoint,
    inputStatus,
    inputSnapshotRef,

    requestedChanges[],

    candidateValidationStatus,

    resolvedBefore,
    resolvedAfter,

    result,
    reasonCode,
    fallbackAction,

    policyVersion,
    scenarioConfigVersion,
    parameterRegistryVersion,
    contentWhitelistVersion,
    fallbackConfigId
}
```

`candidateValidationStatus`:

```text
NOT_EVALUATED
VALID
INVALID
```

Final `result`:

```text
APPLIED
NO_CHANGE
FIXED_FALLBACK
```

`fallbackAction`:

```text
NONE
FULL_FIXED_CONFIG
KEEP_LAST_VALID_CONFIG
```

---

## 29.1. Requested Change Item

```text
AdaptiveRequestedChange
{
    key,
    before,
    requestedAfter
}
```

For discrete content:

```text
before / requestedAfter
→ content IDs
```

For numerical parameter:

```text
before / requestedAfter
→ number
```

Invalid requested values remain audit/debug input only and **MUST NOT** appear in `resolvedAfter` as if applied.

---

## 29.2. Candidate Validation Status — FROZEN

```text
CandidateValidationStatus
=
NOT_EVALUATED
| VALID
| INVALID
```

`NOT_EVALUATED`:

```text
adaptive candidate was not produced/evaluated
because adaptive path was ineligible/unavailable
or failed before candidate validation
```

`VALID`:

```text
candidate or zero-delta decision passed required validation
```

`INVALID`:

```text
candidate was produced/evaluated but failed required validation
→ candidate rejected
→ candidate MUST NOT apply
```

`CandidateValidationStatus` mô tả **candidate validation stage**, không phải final machine outcome.

---

## 29.3. AdaptiveDecisionResult — FROZEN

Final machine result enum:

```text
AdaptiveDecisionResult
=
APPLIED
| NO_CHANGE
| FIXED_FALLBACK
```

`REJECTED` **không phải** final `AdaptiveDecision.result` trong M1-015 v0.

### `APPLIED`

```text
policy evaluated successfully
AND CandidateValidationStatus = VALID
AND >= 1 adaptive change exists
AND changes atomically applied
→ result = APPLIED
→ fallbackAction = NONE
```

### `NO_CHANGE`

```text
policy evaluated successfully
AND adaptive delta = none
AND CandidateValidationStatus = VALID
→ result = NO_CHANGE
→ requestedChanges = []
→ fallbackAction = NONE
→ current AppliedScenarioConfig remains unchanged
→ scenarioConfigVersion remains unchanged
```

`NO_CHANGE` không phải rejection và không phải fallback.

### `FIXED_FALLBACK`

```text
adaptive path did not produce an APPLIED/NO_CHANGE outcome
because input/path/candidate/config was unavailable, ineligible or invalid
→ result = FIXED_FALLBACK
```

Fallback action:

```text
PRE_MATCH
→ FULL_FIXED_CONFIG
```

```text
ALLOWED_PHASE_BOUNDARY
→ KEEP_LAST_VALID_CONFIG
```

```text
FINAL_HUNT_SETUP
→ KEEP_LAST_VALID_CONFIG
```

---

## 29.4. Canonical Result Mapping Table

| Situation | CandidateValidationStatus | result | fallbackAction |
|---|---|---|---|
| Valid adaptive delta applied | `VALID` | `APPLIED` | `NONE` |
| Valid evaluation, zero delta | `VALID` | `NO_CHANGE` | `NONE` |
| PRE_MATCH input ineligible/unavailable | `NOT_EVALUATED` | `FIXED_FALLBACK` | `FULL_FIXED_CONFIG` |
| PRE_MATCH candidate invalid | `INVALID` | `FIXED_FALLBACK` | `FULL_FIXED_CONFIG` |
| MID_MATCH input ineligible/unavailable | `NOT_EVALUATED` | `FIXED_FALLBACK` | `KEEP_LAST_VALID_CONFIG` |
| MID_MATCH candidate invalid | `INVALID` | `FIXED_FALLBACK` | `KEEP_LAST_VALID_CONFIG` |
| FINAL_HUNT_SETUP input unavailable/ineligible | `NOT_EVALUATED` | `FIXED_FALLBACK` | `KEEP_LAST_VALID_CONFIG` |
| FINAL_HUNT_SETUP candidate invalid | `INVALID` | `FIXED_FALLBACK` | `KEEP_LAST_VALID_CONFIG` |

Unsupported combination:

```text
→ INVALID decision log / implementation state
```

Không reintroduce final result `REJECTED`.

---

## 29.5. Invalid Candidate Pipeline

```text
Candidate ScenarioConfig
        ↓
Scenario Validator
        ↓
CandidateValidationStatus = INVALID
        ↓
candidate rejected
        ↓
candidate MUST NOT apply
        ↓
resolve fallbackAction
        ↓
AdaptiveDecision.result = FIXED_FALLBACK
```

Candidate bị reject ở validation stage, nhưng final decision outcome là `FIXED_FALLBACK` vì safe fallback behavior là một phần của final outcome.

---

## 29.6. Ineligible / No-Candidate Pipeline

Examples:

```text
TeamPerformance.status = INCOMPLETE
→ Adaptive policy does not run
→ CandidateValidationStatus = NOT_EVALUATED
→ reasonCode = INPUT_INCOMPLETE
→ result = FIXED_FALLBACK
→ fallbackAction by decisionPoint
```

```text
AED unavailable
→ CandidateValidationStatus = NOT_EVALUATED
→ reasonCode = AED_UNAVAILABLE
→ result = FIXED_FALLBACK
→ fallbackAction by decisionPoint
```

```text
AED timeout before valid candidate
→ CandidateValidationStatus = NOT_EVALUATED
→ reasonCode = AED_TIMEOUT
→ result = FIXED_FALLBACK
→ fallbackAction by decisionPoint
```

---

## 29.7. Before / After Semantics

`APPLIED`:

```text
resolvedBefore
→ previous AppliedScenarioConfig

resolvedAfter
→ new validated AppliedScenarioConfig
```

`NO_CHANGE`:

```text
resolvedBefore
→ current AppliedScenarioConfig

resolvedAfter
→ same current AppliedScenarioConfig

scenarioConfigVersion
→ unchanged
```

`FIXED_FALLBACK + PRE_MATCH`:

```text
resolvedAfter
→ valid full fixed config resolved from FIXED_BASELINE_V1
```

`FIXED_FALLBACK + MID_MATCH`:

```text
resolvedAfter
→ same last valid AppliedScenarioConfig
```

`FIXED_FALLBACK + FINAL_HUNT_SETUP`:

```text
resolvedAfter
→ same last valid AppliedScenarioConfig
→ retain valid current/base Final Hunt timer value
```

Invalid candidate requested values **MUST NOT** appear in `resolvedAfter` as applied values.

---

## 29.8. FallbackAction Consistency

Required mapping:

```text
APPLIED
→ fallbackAction = NONE
```

```text
NO_CHANGE
→ fallbackAction = NONE
```

```text
FIXED_FALLBACK + PRE_MATCH
→ fallbackAction = FULL_FIXED_CONFIG
```

```text
FIXED_FALLBACK + ALLOWED_PHASE_BOUNDARY
→ fallbackAction = KEEP_LAST_VALID_CONFIG
```

```text
FIXED_FALLBACK + FINAL_HUNT_SETUP
→ fallbackAction = KEEP_LAST_VALID_CONFIG
```

Unsupported result/status/action combination:

```text
→ INVALID decision log / implementation state
```

---

# 30. Reason Codes

Machine `reasonCode` phải controlled enum/code.

Frozen baseline codes:

```text
ADAPTIVE_APPLIED
ADAPTIVE_NO_CHANGE
FIXED_FALLBACK
INPUT_INCOMPLETE
INPUT_INVALID
AED_UNAVAILABLE
AED_TIMEOUT
POLICY_CONFIG_INVALID
BOUND_REJECTED
TIMING_REJECTED
PRESSURE_RULE_REJECTED
SCENARIO_INVALID
ROUTE_INVALID
SPAWN_INVALID
UNSUPPORTED_VERSION
FALLBACK_CONFIG_INVALID
```

Không dùng arbitrary free-text làm machine reason.

Human-readable debug message có thể tồn tại như auxiliary field nhưng không thay `reasonCode`.

Frozen distinction:

```text
reasonCode
→ explains why the validation/final outcome occurred

CandidateValidationStatus
→ describes candidate validation stage

AdaptiveDecision.result
→ describes final decision outcome
```

---

## 30.1. Reason Mapping Examples

```text
valid policy evaluation
+ requestedChanges = []
→ CandidateValidationStatus = VALID
→ result = NO_CHANGE
→ reasonCode = ADAPTIVE_NO_CHANGE
```

```text
TeamPerformance INCOMPLETE
→ CandidateValidationStatus = NOT_EVALUATED
→ reasonCode = INPUT_INCOMPLETE
→ result = FIXED_FALLBACK
```

```text
requested ChaseSpeed > max
→ CandidateValidationStatus = INVALID
→ reasonCode = BOUND_REJECTED
→ result = FIXED_FALLBACK
```

```text
routeModifier requested at PRE_MATCH
→ CandidateValidationStatus = INVALID
→ reasonCode = TIMING_REJECTED
→ result = FIXED_FALLBACK
```

```text
DetectionFillRate ↑ + DetectionDecayRate ↓
→ CandidateValidationStatus = INVALID
→ reasonCode = PRESSURE_RULE_REJECTED
→ result = FIXED_FALLBACK
```

```text
unreachable objectiveSpawnSetId
→ CandidateValidationStatus = INVALID
→ reasonCode = SPAWN_INVALID
→ result = FIXED_FALLBACK
```

```text
routeModifier removes legal route
→ CandidateValidationStatus = INVALID
→ reasonCode = ROUTE_INVALID
→ result = FIXED_FALLBACK
```

```text
AED unavailable before candidate
→ CandidateValidationStatus = NOT_EVALUATED
→ reasonCode = AED_UNAVAILABLE
→ result = FIXED_FALLBACK
```

```text
AED timeout before valid candidate
→ CandidateValidationStatus = NOT_EVALUATED
→ reasonCode = AED_TIMEOUT
→ result = FIXED_FALLBACK
```

Không dùng legacy rejection value như một final `AdaptiveDecision.result`.

---

# 31. Versioning / Reproducibility

## 31.1. Required Logical Versions

M1-015 phải có khả năng resolve:

```text
scenarioConfigVersion
policyVersion
parameterRegistryVersion
contentWhitelistVersion
fallbackConfigId
fallbackConfigVersion
input profile/team formula/config versions
```

`policyVersion` **REQUIRED trên mọi ScenarioConfig**, bất kể:

```text
configSource = FIXED
hoặc
configSource = ADAPTIVE
```

`policyVersion` xác định M1-015 ScenarioConfig/Fairness contract dùng để resolve/validate config.

FIXED config có `policyVersion` không có nghĩa adaptive scoring đã chạy.

---

## 31.2. Version Ownership

Policy topology/semantic change:

```text
→ policyVersion change
```

Examples:

- change adaptive eligibility;
- change whitelist authority;
- change routeModifier timing;
- change pressureAxis type/mapping;
- change pressure-axis fairness;
- change max changes/boundary;
- change fallbackAction semantics;
- change atomic rejection semantic;
- change result enum semantics.

Parameter bounds/default/tuning change trong same topology:

```text
→ parameterRegistryVersion change
```

Content spawn/route whitelist change:

```text
→ contentWhitelistVersion change
```

Fallback template content change:

```text
→ fallbackConfigVersion change
```

Resolved ScenarioConfig content change:

```text
→ scenarioConfigVersion change
```

Exception:

```text
AdaptiveDecision.result = NO_CHANGE
→ ScenarioConfig content unchanged
→ scenarioConfigVersion MUST remain unchanged
```

---

## 31.3. Reproducibility Rule

Baseline v0 deterministic.

Frozen rule:

```text
same valid Profile/TeamPerformance input
+ same input versions
+ same policyVersion
+ same base/current AppliedScenarioConfig
+ same parameterRegistryVersion/content
+ same pressureAxis metadata
+ same contentWhitelistVersion/content
+ same decisionPoint
+ same fallbackConfigId/version
→ same requested/candidate decision
→ same validator result
→ same result
→ same fallbackAction
→ same AppliedScenarioConfig outcome
```

For NO_CHANGE:

```text
same zero-delta decision
→ same existing scenarioConfigVersion
```

Không dùng hidden randomness.

Không dùng GenAI randomness.

Nếu future policy intentionally stochastic:

```text
→ new policy/version contract required
```

# 32. PacingState Boundary

Implementation baseline có concept:

```text
CALM
BALANCED
TENSE
OVERWHELMED
```

M1-015 v0 freeze:

```text
PacingState
→ RESERVED / OUT OF CURRENT M1-015 POLICY
```

Không implement realtime pacing estimator trong M1-015.

Không dùng PacingState để bypass TeamPerformance eligibility.

Không dùng:

```text
recent PLAYER_DOWNED
→ PacingState OVERWHELMED
→ adaptive config
```

trong current M1-015 v0 contract.

Future activation:

```text
→ separate policy/version
```

---

# 33. ScenarioConfig / AED Fairness Contract Test Cases

Các test case dưới đây được định nghĩa ở M1-015 và phải được implementation verify ở milestone tương ứng.

> `[x]` nghĩa là **contract case đã được định nghĩa**, không có nghĩa implementation hiện tại đã pass integration test.

- [x] Contract case defined — TeamPerformance `COMPLETE` + non-null + valid versions/config → Adaptive policy may evaluate.
- [x] Contract case defined — TeamPerformance `INCOMPLETE` → FixedDirector.
- [x] Contract case defined — TeamPerformance score null → FixedDirector.
- [x] Contract case defined — TeamPerformance null không biến thành 0.
- [x] Contract case defined — partial TeamPerformance không renormalize.
- [x] Contract case defined — survival/noise riêng không được dùng để invent partial AED heuristic.
- [x] Contract case defined — unsupported input/config version → FixedDirector.
- [x] Contract case defined — `mapId` tồn tại trong ScenarioConfig nhưng Adaptive AED v0 không được modify.
- [x] Contract case defined — `monsterType` tồn tại nhưng không adaptive v0.
- [x] Contract case defined — adaptive monster type change → reject.
- [x] Contract case defined — `DetectionFillRate` adaptive allowed when bounds/timing valid.
- [x] Contract case defined — `DetectionDecayRate` adaptive allowed when bounds/timing valid.
- [x] Contract case defined — `ChaseSpeed` adaptive allowed when bounds/timing valid.
- [x] Contract case defined — `SearchDuration` adaptive allowed when bounds/timing valid.
- [x] Contract case defined — `VisionDistance` adaptive request → reject.
- [x] Contract case defined — `VisionAngle` adaptive request → reject.
- [x] Contract case defined — `PatrolSpeed` adaptive request → reject.
- [x] Contract case defined — `SearchRadius` adaptive request → reject.
- [x] Contract case defined — `AttackRange` adaptive request → reject.
- [x] Contract case defined — `AttackWindup` adaptive request → reject.
- [x] Contract case defined — `AttackRecovery` adaptive request → reject.
- [x] Contract case defined — `StalkerDamagePercent` adaptive request → reject.
- [x] Contract case defined — unknown adaptive Stalker key → reject.
- [x] Contract case defined — adaptive numerical key missing bounds → policy config invalid → FixedDirector.
- [x] Contract case defined — adaptive requested value below min → reject; no clamp.
- [x] Contract case defined — adaptive requested value above max → reject; no clamp.
- [x] Contract case defined — invalid `defaultValue/min/max` relationship → policy config invalid.
- [x] Contract case defined — `objectiveSpawnSetId` in valid designer whitelist + reachable → may pass spawn validation.
- [x] Contract case defined — `objectiveSpawnSetId` outside whitelist → reject.
- [x] Contract case defined — unreachable objective spawn set → reject.
- [x] Contract case defined — AED cannot generate arbitrary objective coordinate.
- [x] Contract case defined — `SupportItemBudget` inside bounds + allowed timing → candidate eligible for validation.
- [x] Contract case defined — `SupportItemBudget` out of bounds → reject.
- [x] Contract case defined — support budget reduction không delete Player-owned item.
- [x] Contract case defined — support budget change không retroactively remove already spawned item.
- [x] Contract case defined — support budget increase không bypass support spawn/content contract.
- [x] Contract case defined — `routeModifier` adaptive request at PRE_MATCH → `TIMING_REJECTED`.
- [x] Contract case defined — `routeModifier` at `ALLOWED_PHASE_BOUNDARY` + whitelist/safety valid → may pass validation.
- [x] Contract case defined — unknown/invalid `routeModifier` → reject.
- [x] Contract case defined — route modifier gây no legal route → reject.
- [x] Contract case defined — route modifier làm objective unreachable → reject.
- [x] Contract case defined — route modifier làm exit unreachable → reject.
- [x] Contract case defined — route modifier cannot teleport Player/Monster.
- [x] Contract case defined — Final Hunt timer `45..60` tại `FINAL_HUNT_SETUP` trước timer start → value structurally valid.
- [x] Contract case defined — Final Hunt timer `<45` → reject.
- [x] Contract case defined — Final Hunt timer `>60` → reject.
- [x] Contract case defined — Final Hunt timer change after timer start → reject.
- [x] Contract case defined — policy reevaluate không reset active Final Hunt timer.
- [x] Contract case defined — generic unknown objective timer key → reject.
- [x] Contract case defined — 3 changed keys at one phase boundary → reject.
- [x] Contract case defined — 2 changed keys may pass count rule but vẫn phải pass pressure rule.
- [x] Contract case defined — `DetectionFillRate ↑ + DetectionDecayRate ↓` same boundary → reject compound DetectionPressure.
- [x] Contract case defined — `ChaseSpeed ↑ + SearchDuration ↑` same boundary → reject multi-axis aggression.
- [x] Contract case defined — one aggressive pressure axis + one pressure reduction may pass pressure rule if all other validation passes.
- [x] Contract case defined — PRE_MATCH adaptive config cannot make more than one Stalker pressure axis more aggressive than fixed base.
- [x] Contract case defined — config attempted per-frame → reject timing.
- [x] Contract case defined — config attempted per-second → reject timing.
- [x] Contract case defined — config attempted in active Attack wind-up as runtime reaction → reject timing.
- [x] Contract case defined — Player vừa Down không trực tiếp trigger adaptive parameter change.
- [x] Contract case defined — ScenarioConfig cannot command `CHASE`.
- [x] Contract case defined — ScenarioConfig cannot command `ATTACK`.
- [x] Contract case defined — ScenarioConfig cannot command `SEARCH`.
- [x] Contract case defined — ScenarioConfig cannot set `CurrentTarget`.
- [x] Contract case defined — ScenarioConfig cannot set `DetectionTarget`.
- [x] Contract case defined — ScenarioConfig cannot update `LastKnownPosition`.
- [x] Contract case defined — ScenarioConfig cannot contain/use hidden Player location.
- [x] Contract case defined — invalid scenario candidate rejects whole adaptive decision.
- [x] Contract case defined — one valid + one invalid requested change → neither applied.
- [x] Contract case defined — validator does not silently clamp invalid numerical value.
- [x] Contract case defined — validator does not ignore invalid field and apply rest.
- [x] Contract case defined — AED unavailable → FixedDirector.
- [x] Contract case defined — AED timeout → FixedDirector.
- [x] Contract case defined — policy config invalid → FixedDirector.
- [x] Contract case defined — content whitelist invalid → FixedDirector.
- [x] Contract case defined — Scenario Validator fail → FixedDirector.
- [x] Contract case defined — adaptive failure at PRE_MATCH → `FIXED_FALLBACK` + `FULL_FIXED_CONFIG` resolving `FIXED_BASELINE_V1`.
- [x] Contract case defined — fallback config must be versioned + validator-pass.
- [x] Contract case defined — invalid/missing fallback config → fatal configuration error; Adaptive AED does not invent replacement.
- [x] Contract case defined — mid-match fallback does not reset Stalker FSM.
- [x] Contract case defined — mid-match fallback does not reset Player state.
- [x] Contract case defined — mid-match fallback does not reset objective progress.
- [x] Contract case defined — mid-match fallback does not teleport.
- [x] Contract case defined — invalid candidate has controlled reasonCode and final result `FIXED_FALLBACK`.
- [x] Contract case defined — applied AdaptiveDecision records before/after/reason/version.
- [x] Contract case defined — same input/version/base config/registry/whitelist → reproducible result.
- [x] Contract case defined — PacingState is not active input in M1-015 v0.
- [x] Contract case defined — no GenAI gameplay/config decision.
- [x] Contract case defined — no new gameplay mechanic/content outside whitelist.

- [x] Contract case defined — adaptive failure mid-match → `FIXED_FALLBACK` + `KEEP_LAST_VALID_CONFIG`.
- [x] Contract case defined — mid-match fallback does not change `mapId`.
- [x] Contract case defined — mid-match fallback does not change `monsterType`.
- [x] Contract case defined — mid-match fallback does not replace already-resolved `objectiveSpawnSetId`.
- [x] Contract case defined — mid-match fallback does not rollback a previous valid adaptive parameter automatically.
- [x] Contract case defined — mid-match fallback does not replace active Final Hunt timer state.
- [x] Contract case defined — `SupportItemBudget` has `pressureAxis = NONE`.
- [x] Contract case defined — `FinalHunt.EscapeDoorTimer` has `pressureAxis = NONE`.
- [x] Contract case defined — `pressureAxis = NONE` is excluded from Stalker aggressive-axis count.
- [x] Contract case defined — unsupported pressureAxis metadata → policy/registry invalid.
- [x] Contract case defined — FIXED ScenarioConfig still has supported non-null `policyVersion`.
- [x] Contract case defined — eligible policy evaluation with zero changed keys → result `NO_CHANGE`.
- [x] Contract case defined — `NO_CHANGE` uses `fallbackAction = NONE`.
- [x] Contract case defined — `NO_CHANGE` does not invoke FixedDirector.
- [x] Contract case defined — `NO_CHANGE` leaves current AppliedScenarioConfig unchanged.
- [x] Contract case defined — `NO_CHANGE` does not create artificial new `scenarioConfigVersion`.
- [x] Contract case defined — PRE_MATCH `FIXED_FALLBACK` uses `fallbackAction = FULL_FIXED_CONFIG`.
- [x] Contract case defined — mid-match `FIXED_FALLBACK` uses `fallbackAction = KEEP_LAST_VALID_CONFIG`.
- [x] Contract case defined — FINAL_HUNT_SETUP adaptive failure keeps already-resolved valid base timer and does not replace unrelated current-match fields.

---

- [x] Contract case defined — duplicate Section 13 removed and canonical section order restored.
- [x] Contract case defined — Compound-Pressure Fairness exists as Section 17 with Sections 17.1–17.5.
- [x] Contract case defined — valid adaptive candidate with delta → `CandidateValidationStatus=VALID` + `result=APPLIED`.
- [x] Contract case defined — valid zero-delta evaluation → `CandidateValidationStatus=VALID` + `result=NO_CHANGE`.
- [x] Contract case defined — invalid bound → `INVALID + BOUND_REJECTED + FIXED_FALLBACK`.
- [x] Contract case defined — `routeModifier` at PRE_MATCH → `INVALID + TIMING_REJECTED + FIXED_FALLBACK`.
- [x] Contract case defined — compound pressure violation → `INVALID + PRESSURE_RULE_REJECTED + FIXED_FALLBACK`.
- [x] Contract case defined — TeamPerformance INCOMPLETE → `NOT_EVALUATED + INPUT_INCOMPLETE + FIXED_FALLBACK`.
- [x] Contract case defined — AED unavailable before candidate → `NOT_EVALUATED + AED_UNAVAILABLE + FIXED_FALLBACK`.
- [x] Contract case defined — final `AdaptiveDecision.result` never uses `REJECTED`.
- [x] Contract case defined — invalid candidate requested values are never written to `resolvedAfter` as applied.


# 34. Implementation Constraints

1. AED consumes processed Profile/TeamPerformance input only.
2. Raw TelemetryEvent không trực tiếp drive adaptive policy.
3. TeamPerformance `INCOMPLETE` → no adaptive policy.
4. TeamPerformance score null → no adaptive policy.
5. Không partial-input AED heuristic trong v0.
6. Không dùng PlayerAIProfile survival/noise riêng để bypass eligibility.
7. Required input/config version phải supported.
8. ScenarioConfig schema phải explicit; không free-form `parameters:anything`.
9. `policyVersion` REQUIRED cho cả FIXED và ADAPTIVE ScenarioConfig.
10. `configSource` phân biệt FIXED/ADAPTIVE; không dùng null `policyVersion`.
11. Field tồn tại không đồng nghĩa adaptive authority.
12. `mapId` không adaptive v0.
13. `monsterType` không adaptive v0.
14. Adaptive Stalker whitelist chính xác: `DetectionFillRate`, `DetectionDecayRate`, `ChaseSpeed`, `SearchDuration`.
15. Non-whitelisted Stalker key không được modify.
16. `VisionDistance`/`VisionAngle` không adaptive.
17. `PatrolSpeed`/`SearchRadius` không adaptive.
18. `AttackRange`/`AttackWindup`/`AttackRecovery`/`StalkerDamagePercent` không adaptive.
19. Every adaptive numerical key phải có default/min/max.
20. Missing bounds → policy config invalid.
21. Out-of-bound candidate invalid; không silently clamp.
22. Parameter Registry phải versioned.
23. `pressureAxis` chỉ được `DetectionPressure | ChasePressure | SearchPressure | NONE`.
24. `DetectionFillRate → DetectionPressure`.
25. `DetectionDecayRate → DetectionPressure`.
26. `ChaseSpeed → ChasePressure`.
27. `SearchDuration → SearchPressure`.
28. `SupportItemBudget → NONE`.
29. `FinalHunt.EscapeDoorTimer → NONE`.
30. `NONE` không count vào Stalker aggressive-pressure-axis limit.
31. `NONE` không tự làm decision fair; owner/timing/bounds/validator vẫn bắt buộc.
32. `DetectionFillRate ↑` = DetectionPressure more aggressive.
33. `DetectionDecayRate ↓` = DetectionPressure more aggressive.
34. `ChaseSpeed ↑` = ChasePressure more aggressive.
35. `SearchDuration ↑` = SearchPressure more aggressive.
36. PRE_MATCH không được làm hơn một Stalker pressure axis more aggressive so với fixed base.
37. Phase boundary tối đa 1–2 changed keys.
38. Phase boundary tối đa một Stalker pressure axis more aggressive.
39. Không `DetectionFillRate ↑ + DetectionDecayRate ↓` cùng decision.
40. Không stack multiple aggressive Stalker pressure axes.
41. `routeModifier` MUST NOT be adaptively modified at PRE_MATCH.
42. `routeModifier` adaptive only at `ALLOWED_PHASE_BOUNDARY`.
43. Không runtime per-frame/per-second adaptation.
44. Không direct reaction parameter change vì một Player vừa Down/noise.
45. Không FSM command.
46. Không Sensor bypass.
47. Không Detection Meter bypass.
48. Không CurrentTarget/DetectionTarget command.
49. Không LastKnownPosition update.
50. Không hidden Player location.
51. Objective spawn set chỉ từ designer whitelist.
52. Không arbitrary spawn coordinate.
53. Spawn set phải reachable và compatible.
54. SupportItemBudget bounded.
55. SupportItemBudget change non-retroactive.
56. Budget change không remove Player-owned/already-spawned item trái contract.
57. RouteModifier whitelist only.
58. Ít nhất một legal route phải remain.
59. Không route soft-lock.
60. Không route modifier teleport.
61. `FinalHunt.EscapeDoorTimer` bound = `45..60s`.
62. Final Hunt timer adaptive timing = `FINAL_HUNT_SETUP`.
63. Final Hunt timer immutable after timer start.
64. Không generic objective-timing adaptation.
65. Candidate phải qua Scenario Validator trước apply.
66. Validator phải check routeModifier timing.
67. Validator phải check pressureAxis metadata/mapping.
68. Validator failure reject whole candidate.
69. Validator không clamp/repair partial candidate.
70. Adaptive decision phải atomic.
71. One valid + one invalid change → neither applied.
72. `NO_CHANGE` là explicit valid result khi policy evaluate thành công với zero delta.
73. `NO_CHANGE → fallbackAction = NONE`.
74. `NO_CHANGE` không invoke FixedDirector.
75. `NO_CHANGE` không mutate AppliedScenarioConfig.
76. `NO_CHANGE` không tạo artificial `scenarioConfigVersion`.
77. Fallback action phụ thuộc decision point.
78. PRE_MATCH failure → `FULL_FIXED_CONFIG`.
79. PRE_MATCH full fallback may resolve `FIXED_BASELINE_V1`.
80. MID_MATCH failure MUST NOT replace full current ScenarioConfig.
81. MID_MATCH failure → `KEEP_LAST_VALID_CONFIG`.
82. Mid-match current last valid AppliedScenarioConfig remains authoritative.
83. Không automatic rollback previous valid adaptive change.
84. FINAL_HUNT_SETUP adaptive failure → keep last valid config/base timer; không replace unrelated fields.
85. Fallback logical ID = `FIXED_BASELINE_V1`.
86. Fallback full template phải designer-authored, versioned, validator-pass, deterministic.
87. Invalid/missing PRE_MATCH fallback config = fatal configuration error.
88. Mid-match fallback không reset match.
89. Mid-match fallback không reset Player state.
90. Mid-match fallback không reset objective progress.
91. Mid-match fallback không reset/command Monster FSM.
92. Mid-match fallback không teleport Player/Monster.
93. Mid-match fallback không replace mapId/monsterType/objectiveSpawnSetId.
94. Mid-match fallback không retroactively replace active Final Hunt timer.
95. AdaptiveDecision phải có controlled result/reasonCode/fallbackAction.
96. Final AdaptiveDecision result enum v0 = `APPLIED | NO_CHANGE | FIXED_FALLBACK`; `REJECTED` is not a final result.
97. `ADAPTIVE_NO_CHANGE` là controlled reasonCode cho successful zero-delta evaluation.
98. Decision phải trace policy/config/registry/whitelist/fallback versions.
99. Baseline v0 deterministic.
100. PacingState reserved/out-of-scope.
101. Không ML gameplay policy.
102. Không GenAI gameplay/config decision.
103. Không procedural content ngoài whitelist.
104. Không new gameplay mechanic.
105. AED chỉ thay validated Scenario Configuration trong bounds/timing đã freeze.

---

106. `CandidateValidationStatus` enum = `NOT_EVALUATED | VALID | INVALID`.
107. Final `AdaptiveDecision.result` enum = `APPLIED | NO_CHANGE | FIXED_FALLBACK`.
108. `REJECTED` MUST NOT be used as a final AdaptiveDecision result.
109. Candidate may be rejected internally only through `CandidateValidationStatus = INVALID`.
110. INVALID candidate always resolves through the safe fallback path and final result `FIXED_FALLBACK`.
111. PRE_MATCH invalid candidate → `FULL_FIXED_CONFIG`.
112. MID_MATCH invalid candidate → `KEEP_LAST_VALID_CONFIG`.
113. FINAL_HUNT_SETUP invalid candidate → `KEEP_LAST_VALID_CONFIG`.
114. NOT_EVALUATED adaptive path → final result `FIXED_FALLBACK`.
115. Invalid candidate requested values MUST NOT become `resolvedAfter` applied values.
116. `NO_CHANGE` remains a valid zero-delta evaluation with `CandidateValidationStatus = VALID`.
117. Document structure MUST contain exactly one canonical Section 13 and one Section 17 with Sections 17.1–17.5.
118. Top-level numbered sections MUST remain canonical from Section 1 through Section 35 with no duplicate/orphan numbered section.


# 35. M1-015 Completion Criteria

Task **M1-015 — ScenarioConfig + fairness rules** được xem là hoàn thành khi:

- [x] Metadata dependency phản ánh M1-014, M1-013 và M1-012.
- [x] Source/dependency alignment với M1-014/M1-013/AI Architecture rõ.
- [x] AED input contract rõ.
- [x] Adaptive input eligibility rõ.
- [x] TeamPerformance `INCOMPLETE` → safe fixed path rõ.
- [x] Không partial TeamPerformance heuristic.
- [x] Không dùng survival/noise riêng để invent v0 adaptive heuristic.
- [x] ScenarioConfig schema explicit.
- [x] Không free-form parameter dictionary.
- [x] ScenarioConfig là resolved config contract; adaptive-owned monsterParameters scope rõ.
- [x] Config field vs adaptive authority distinction rõ.
- [x] `policyVersion` REQUIRED cho FIXED và ADAPTIVE config.
- [x] `mapId` non-adaptive v0 rõ.
- [x] `monsterType` non-adaptive v0 rõ.
- [x] designer content whitelist contract rõ.
- [x] Adaptive Stalker whitelist exact 4 keys.
- [x] Explicit non-adaptive Stalker list rõ.
- [x] combat/perception envelope non-adaptive v0 rõ.
- [x] AdaptiveParameterRule contract rõ.
- [x] `pressureAxis` supports `NONE`.
- [x] `SupportItemBudget → NONE` rõ.
- [x] `FinalHunt.EscapeDoorTimer → NONE` rõ.
- [x] `NONE` excluded khỏi Stalker aggressive-axis count.
- [x] default/min/max ownership rõ.
- [x] missing bounds → invalid/fallback rõ.
- [x] out-of-bound → reject/no clamp rõ.
- [x] numerical tuning không có source không bị invent.
- [x] Final Hunt timer source-supported `45..60s` rõ.
- [x] Pressure axes/directions rõ.
- [x] PRE_MATCH timing/authority rõ.
- [x] `routeModifier` không adaptive PRE_MATCH.
- [x] `routeModifier` adaptive only at `ALLOWED_PHASE_BOUNDARY`.
- [x] phase boundary max `1–2` changed keys rõ.
- [x] max one aggressive Stalker pressure axis rõ.
- [x] compound DetectionPressure buff forbidden rõ.
- [x] multi-axis aggressive stack forbidden rõ.
- [x] objective spawn whitelist/reachability rõ.
- [x] không arbitrary spawn coordinate.
- [x] SupportItemBudget bounded/non-retroactive rõ.
- [x] RouteModifier whitelist/safety/soft-lock rule rõ.
- [x] Final Hunt timer `FINAL_HUNT_SETUP` timing rõ.
- [x] Final Hunt timer immutable after start rõ.
- [x] không generic objective timing adaptation.
- [x] fairness hard rules đầy đủ.
- [x] forbidden direct Monster AI commands rõ.
- [x] Scenario Validator check route timing/pressure metadata/fallback/no-change semantics rõ.
- [x] validator no silent clamp/partial repair rõ.
- [x] atomic rejection/application rõ.
- [x] `NO_CHANGE` explicit valid result rõ.
- [x] `NO_CHANGE` không invoke fallback hoặc mutate/new-version config.
- [x] FixedDirector/fallback trigger set rõ.
- [x] PRE_MATCH fallback = `FULL_FIXED_CONFIG` rõ.
- [x] PRE_MATCH full fallback uses `FIXED_BASELINE_V1`.
- [x] MID_MATCH fallback = `KEEP_LAST_VALID_CONFIG` rõ.
- [x] mid-match fallback không replace whole current ScenarioConfig.
- [x] mid-match fallback preserve last valid AppliedScenarioConfig.
- [x] no automatic rollback previous valid adaptive decision.
- [x] Final Hunt setup failure không replace unrelated fields.
- [x] fallback full template known-safe/versioned/deterministic rõ.
- [x] invalid PRE_MATCH fallback config fatal semantics rõ.
- [x] mid-match fallback không reset gameplay state rõ.
- [x] AdaptiveDecision logical contract rõ.
- [x] `fallbackAction` semantic rõ.
- [x] controlled reasonCode gồm `ADAPTIVE_NO_CHANGE`.
- [x] versioning ownership rõ.
- [x] FIXED config policyVersion semantic rõ.
- [x] deterministic reproducibility rõ.
- [x] PacingState reserved/out-of-scope rõ.
- [x] no ML/GenAI gameplay decision rõ.
- [x] Contract Test Cases cover route timing, fallback modes, NONE axis, FIXED policyVersion và NO_CHANGE.
- [x] Backend / AI / Gameplay implementation không phải tự suy đoán fairness/timing/fallback semantics.

- [x] Exactly one canonical top-level Section 13 (`Parameter Bounds`) exists.
- [x] Top-level Section 17 (`Compound-Pressure Fairness`) exists exactly once.
- [x] Sections `17.1` through `17.5` exist exactly once and are coherent.
- [x] Top-level numbered sections run cleanly from `1` through `35` without duplicate/orphan numbering.
- [x] `CandidateValidationStatus = NOT_EVALUATED | VALID | INVALID` is explicit.
- [x] Final result enum contains only `APPLIED | NO_CHANGE | FIXED_FALLBACK`.
- [x] INVALID candidate → candidate rejected → safe fallback → final `FIXED_FALLBACK` is explicit.
- [x] NOT_EVALUATED path → final `FIXED_FALLBACK` is explicit.
- [x] PRE_MATCH/MID_MATCH/FINAL_HUNT_SETUP fallback mappings remain intact.
- [x] NO_CHANGE semantics/version behavior remain intact.
- [x] Tests, constraints and Frozen Baseline Summary use the same validation-status/final-result semantics.
- [x] No section uses the legacy rejection value as a final `AdaptiveDecision.result`.

**Final Status: DONE / FROZEN**

---

# Frozen Baseline Summary

```text
Architecture

TelemetryEvent
→ MatchTelemetry
→ MatchScore
→ PlayerAIProfile / TeamProfile / TeamPerformance
→ AED
→ Scenario Configuration
→ Traditional Gameplay AI
```

```text
AED
→ configuration layer only

AED MUST NOT
→ change FSM topology
→ command CHASE / ATTACK / SEARCH
→ select CurrentTarget / DetectionTarget
→ update LastKnownPosition
→ bypass LOS / Detection Meter / Attack contract
→ use hidden Player position
→ create gameplay mechanic
→ use GenAI for gameplay decision
```

```text
Adaptive Input Eligibility

TeamPerformance.status = COMPLETE
AND TeamPerformance.score != null
AND required inputs/versions/config valid
→ Adaptive policy may evaluate

otherwise
→ safe fallback action according to decision point
```

```text
Current M1-014 baseline

Teamwork = DEFERRED
ResourceEfficiency = DEFERRED
→ TeamPerformance = INCOMPLETE
→ adaptive path does not run
→ safe fixed path

This does NOT make M1-015 incomplete.
```

```text
ScenarioConfig v0

scenarioConfigVersion
policyVersion               REQUIRED for FIXED and ADAPTIVE
configSource                FIXED | ADAPTIVE

mapId
monsterType
objectiveSpawnSetId

supportItemBudget

monsterParameters
  DetectionFillRate
  DetectionDecayRate
  ChaseSpeed
  SearchDuration

routeModifier

finalHuntParameters
  escapeDoorTimerSeconds

fallbackConfigId
```

```text
ScenarioConfig
→ resolved configuration contract

monsterParameters in M1-015
→ expose only adaptive-owned Stalker keys

Non-adaptive runtime Stalker values
→ still resolved by gameplay/base monster config
→ AED v0 has no adaptive authority over them
```

```text
policyVersion

REQUIRED on configSource = FIXED
REQUIRED on configSource = ADAPTIVE

policyVersion
→ M1-015 ScenarioConfig/Fairness contract version
→ FIXED policyVersion does NOT mean adaptive scoring ran
```

```text
Field exists
≠
Adaptive authority
```

```text
Adaptive Stalker whitelist v0

DetectionFillRate
DetectionDecayRate
ChaseSpeed
SearchDuration
```

```text
NOT adaptive v0

VisionDistance
VisionAngle
PatrolSpeed
SearchRadius
AttackRange
AttackWindup
AttackRecovery
StalkerDamagePercent
monsterType
mapId
```

```text
routeModifier

PRE_MATCH
→ Adaptive AED MUST NOT modify
→ adaptive request = TIMING_REJECTED

ALLOWED_PHASE_BOUNDARY
→ adaptive request allowed
→ designer whitelist
→ route compatibility
→ route validator
→ at least one legal route
→ no objective/exit unreachable
→ no soft-lock
→ no teleport
→ no hidden Player information
```

```text
AdaptiveParameterRule

key
defaultValue
minValue
maxValue
allowedTiming
pressureAxis:
  DetectionPressure
  | ChasePressure
  | SearchPressure
  | NONE
owner
```

```text
pressureAxis mapping

DetectionFillRate
→ DetectionPressure

DetectionDecayRate
→ DetectionPressure

ChaseSpeed
→ ChasePressure

SearchDuration
→ SearchPressure

SupportItemBudget
→ NONE

FinalHunt.EscapeDoorTimer
→ NONE
```

```text
pressureAxis = NONE
→ does NOT count toward Stalker aggressive-axis total
→ does NOT automatically make decision fair
→ bounds/timing/owner-specific fairness/validator still required
```

```text
Missing bounds / invalid pressureAxis metadata
→ parameter registry invalid
→ adaptive path does not run
→ fallback action according to decision point

Out of bounds
→ candidate INVALID
→ no silent clamp
```

```text
Pressure directions

DetectionFillRate ↑ → DetectionPressure more aggressive
DetectionDecayRate ↓ → DetectionPressure more aggressive
ChaseSpeed ↑ → ChasePressure more aggressive
SearchDuration ↑ → SearchPressure more aggressive
```

```text
PRE_MATCH

Adaptive allowed:
objectiveSpawnSetId
SupportItemBudget
DetectionFillRate
DetectionDecayRate
ChaseSpeed
SearchDuration

Adaptive NOT allowed:
mapId
monsterType
routeModifier
FinalHunt.EscapeDoorTimer
```

```text
ALLOWED_PHASE_BOUNDARY

max changed keys = 1–2
max more-aggressive Stalker pressure axes = 1

routeModifier
→ adaptive allowed here only
```

```text
Forbidden compound pressure

DetectionFillRate ↑
+
DetectionDecayRate ↓
→ INVALID

ChaseSpeed ↑
+
SearchDuration ↑
→ INVALID
```

```text
objectiveSpawnSetId

adaptive PRE_MATCH allowed
→ designer-authored whitelist only
→ reachable
→ no soft-lock
→ no arbitrary coordinates
```

```text
SupportItemBudget

pressureAxis = NONE
adaptive bounded
→ PRE_MATCH / allowed boundary per registry
→ future allocation only
→ no retroactive item deletion/removal
```

```text
FinalHunt.EscapeDoorTimer

pressureAxis = NONE
45..60 seconds
timing = FINAL_HUNT_SETUP
decision resolved before timer starts
timer immutable after start
```

```text
No generic ObjectiveTiming adaptive permission.
```

```text
Scenario Validator

Candidate ScenarioConfig / decision
→ validate schema + policyVersion
→ validate adaptive whitelist
→ validate bounds
→ validate timing
→ routeModifier boundary-only timing
→ validate pressureAxis metadata/mapping
→ validate pressure fairness
→ validate spawn
→ validate route
→ validate support
→ validate Final Hunt
→ validate fallbackAction
→ validate NO_CHANGE semantics
→ validate content compatibility

VALID adaptive delta
→ atomic apply

VALID zero delta
→ NO_CHANGE

INVALID
→ reject whole adaptive decision
→ fallback action by decision point
```

```text
No partial apply.

A valid + B invalid
→ neither applied
```

```text
FallbackAction

NONE
FULL_FIXED_CONFIG
KEEP_LAST_VALID_CONFIG
```

```text
PRE_MATCH adaptive failure

result = FIXED_FALLBACK
fallbackAction = FULL_FIXED_CONFIG
FallbackScenarioConfigId = FIXED_BASELINE_V1

→ FixedDirector may resolve full known-safe ScenarioConfig
→ match has not started
```

```text
MID_MATCH adaptive failure
ALLOWED_PHASE_BOUNDARY / FINAL_HUNT_SETUP

result = FIXED_FALLBACK
fallbackAction = KEEP_LAST_VALID_CONFIG

→ reject adaptive candidate
→ current last valid AppliedScenarioConfig remains authoritative
→ do NOT load full FIXED_BASELINE_V1 over live match
→ no automatic rollback of earlier valid adaptive decision
```

```text
Mid-match fallback MUST NOT change/reset

mapId
monsterType
resolved objectiveSpawnSetId
unrelated Stalker config
current route state arbitrarily
active Final Hunt timer
match state
Player state
objective progress
Monster FSM
Player/Monster position
```

```text
FINAL_HUNT_SETUP adaptive failure before timer start

→ KEEP_LAST_VALID_CONFIG
→ retain valid timer value already resolved in current/base AppliedScenarioConfig
→ do not replace unrelated fields
```

```text
FallbackScenarioConfigId
=
FIXED_BASELINE_V1

→ full known-safe fixed template/reference
→ PRE_MATCH full fallback identity
→ not a blanket mid-match replacement command
```

```text
FIXED_BASELINE_V1
→ designer-authored
→ known-safe
→ versioned
→ policyVersion present
→ validator-pass
→ deterministic
```

```text
CandidateValidationStatus

NOT_EVALUATED
VALID
INVALID
```

```text
AdaptiveDecision

decisionId
decisionPoint
inputStatus
inputSnapshotRef
requestedChanges[]
candidateValidationStatus
resolvedBefore
resolvedAfter
result
reasonCode
fallbackAction
policyVersion
scenarioConfigVersion
parameterRegistryVersion
contentWhitelistVersion
fallbackConfigId
```

```text
AdaptiveDecision Result

APPLIED
NO_CHANGE
FIXED_FALLBACK
```

```text
Canonical final-outcome flow

VALID + adaptive delta
→ result = APPLIED
→ fallbackAction = NONE

VALID + zero delta
→ result = NO_CHANGE
→ fallbackAction = NONE
→ scenarioConfigVersion unchanged

candidate INVALID
→ candidate rejected
→ candidate MUST NOT apply
→ safe fallback action by decisionPoint
→ final result = FIXED_FALLBACK

adaptive path NOT_EVALUATED
→ safe fallback action by decisionPoint
→ final result = FIXED_FALLBACK
```

```text
Fallback mapping

PRE_MATCH FIXED_FALLBACK
→ FULL_FIXED_CONFIG
→ FIXED_BASELINE_V1

ALLOWED_PHASE_BOUNDARY FIXED_FALLBACK
→ KEEP_LAST_VALID_CONFIG

FINAL_HUNT_SETUP FIXED_FALLBACK
→ KEEP_LAST_VALID_CONFIG
```

```text
Controlled reasonCode

ADAPTIVE_APPLIED
ADAPTIVE_NO_CHANGE
FIXED_FALLBACK
INPUT_INCOMPLETE
INPUT_INVALID
AED_UNAVAILABLE
AED_TIMEOUT
POLICY_CONFIG_INVALID
BOUND_REJECTED
TIMING_REJECTED
PRESSURE_RULE_REJECTED
SCENARIO_INVALID
ROUTE_INVALID
SPAWN_INVALID
UNSUPPORTED_VERSION
FALLBACK_CONFIG_INVALID
```

```text
Versioning

policy topology/semantic
→ policyVersion

bounds/default/tuning/pressure metadata
→ parameterRegistryVersion

spawn/route/content whitelist
→ contentWhitelistVersion

fallback template content
→ fallbackConfigVersion

resolved ScenarioConfig content change
→ scenarioConfigVersion

NO_CHANGE
→ no content change
→ scenarioConfigVersion unchanged
```

```text
Reproducibility

same valid input
+ same input versions
+ same policyVersion
+ same current/base AppliedScenarioConfig
+ same parameter registry
+ same pressureAxis metadata
+ same content whitelist
+ same decision point
+ same fallback version
→ same requested/candidate decision
→ same validator result
→ same result/fallbackAction
→ same AppliedScenarioConfig outcome
```

```text
PacingState
CALM / BALANCED / TENSE / OVERWHELMED
→ RESERVED / OUT OF CURRENT M1-015 v0 POLICY
```

```text
Document structure

Section 13 = Parameter Bounds (exactly once)
Section 17 = Compound-Pressure Fairness (17.1–17.5)
Top-level numbered sections = 1..35 exactly once each
```

```text
M1-015 v0
→ deterministic
→ whitelist-based
→ bounded
→ decision-point controlled
→ route timing explicit
→ pressure-axis fair
→ atomic
→ NO_CHANGE explicit
→ pre-match full fallback
→ mid-match keep-last-valid fallback
→ explainable
→ versioned
```

**Final Status: DONE / FROZEN**

