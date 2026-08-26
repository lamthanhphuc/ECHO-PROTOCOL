# ECHO PROTOCOL — M1-014 Player/Team Profile Fields + Formulas v0

**Task:** M1-014 — Player/Team Profile fields + formulas v0  
**Owner:** C — AI / Telemetry / Research  
**Support:** D — Backend / Data  
**Dependency:** M1-008 — Telemetry Event Schema v0  
**Priority:** P0  
**Status:** DONE / FROZEN  
**Profile Formula Version:** `1.0`

---

# 1. Purpose

Tài liệu này freeze contract/design cho **PlayerAIProfile**, **TeamProfile**, **MatchScore aggregation** và **TeamPerformance structure** của ECHO PROTOCOL.

Mục tiêu của M1-014:

- chuyển raw/aggregated telemetry thành processed score có semantic rõ ràng;
- freeze 9 Player Profile dimensions;
- xác định dimension nào `ACTIVE`, `COLD_START` hoặc `DEFERRED`;
- freeze scale `0..100`;
- freeze normalization pattern;
- freeze cold-start behavior;
- freeze missing-data behavior;
- freeze `MetricAvailability = AVAILABLE | UNAVAILABLE`;
- freeze `MatchProfileEligibility = ELIGIBLE | INELIGIBLE`;
- ngăn partial/aborted match cập nhật persistent Player Profile hoặc tạo valid Team result;
- freeze Player Profile update bằng EMA;
- freeze noise profile signal qua configurable/versioned `ProfileNoiseFilter`;
- freeze TeamProfile schema và identity semantic;
- freeze TeamProfile v1.0 là **match-scoped**, không phải historical persistent party profile;
- freeze `objectiveTime` là elapsed wall-clock phase duration;
- freeze TeamPerformance formula structure và completeness rule;
- tách formula topology khỏi numerical tuning;
- bảo đảm cùng input + cùng eligibility/availability + cùng formula/config version có thể reproduce cùng output;
- giữ ranh giới rõ giữa Telemetry, Profile và AED;
- cho phép Backend / Data / AI implementation mà không phải tự suy đoán formula, missing-data behavior, ownership hoặc lifecycle.

M1-014 là **processed data/model contract**.

M1-014 không trực tiếp điều khiển gameplay runtime.

**Final Status chỉ được giữ `DONE / FROZEN` khi toàn bộ eligibility, availability, formula, ownership và boundary trong tài liệu này không contradiction.**

---

# 2. Scope

## 2.1. In Scope

M1-014 freeze:

- `PlayerAIProfile` fields;
- `PlayerDimensionState`;
- 9 Player Profile dimensions;
- Player dimension `ACTIVE / COLD_START / DEFERRED` semantics;
- `MatchProfileEligibility`;
- `MetricAvailability`;
- MatchScore scale;
- current Player MatchScore source mapping;
- normalization contract;
- `ProfileNoisePenaltyCount` và `ProfileNoiseFilter`;
- cold-start contract;
- missing-data contract;
- `sampleCount` semantics;
- Player profile EMA update formula;
- persistent Player Profile ownership theo `userId`;
- `TeamProfile` schema;
- TeamProfile identity theo current match;
- Team field `ACTIVE / DEFERRED` semantics;
- Team metric source/dependency mapping;
- `objectiveTime` elapsed wall-clock semantic;
- `TeamPerformance` component ownership;
- `TeamPerformance` weighted formula structure;
- TeamPerformance `COMPLETE / INCOMPLETE` semantics;
- configurable formula/filter parameters;
- formula/config version metadata;
- Telemetry boundary;
- AED / M1-015 boundary;
- contract test cases;
- implementation constraints;
- completion criteria.

## 2.2. Out of Scope

M1-014 không định nghĩa hoặc implement:

- AED decision policy;
- Scenario Configuration mapping;
- rule `Profile/TeamPerformance → monster parameter`;
- runtime Monster AI;
- Stalker / Listener / Warden FSM;
- target selection;
- navigation behavior;
- Machine Learning;
- GenAI;
- clustering;
- player classification labels;
- prediction model;
- recommendation engine;
- persistent historical party/team identity;
- cross-match TeamProfile EMA;
- dashboard;
- analytics UI;
- telemetry transport;
- `/telemetry/batch` implementation;
- retry/completeness-detection transport implementation;
- database migration/schema chi tiết;
- telemetry event mới;
- gameplay mechanic mới;
- metric chưa có source contract;
- numerical tuning cuối cùng nếu source hiện tại chưa freeze.

---

# 3. Architecture Boundary

Architecture đã freeze:

```text
Gameplay Runtime
        ↓
TelemetryEvent
        ↓
MatchTelemetry
        ↓
MatchScore
        ↓
Player / Team Profile
        ↓
AED
        ↓
Scenario Configuration
```

M1-014 bổ sung hai contract gate/status ở processed-data layer:

```text
TelemetryEvent
        ↓
MatchTelemetry
        ├─ MetricAvailability
        └─ MatchProfileEligibility
        ↓
MatchScore / TeamProfile
        ↓
PlayerAIProfile / TeamPerformance
```

Hai gate này **không thay đổi Telemetry Event Schema**.

Trách nhiệm:

| Layer | Responsibility |
|---|---|
| Gameplay Runtime | Tạo gameplay outcome/event authoritative |
| TelemetryEvent | Ghi nhận structured gameplay data |
| MatchTelemetry | Aggregate raw telemetry và expose metric availability |
| MatchProfileEligibility | Xác định match có được phép apply vào Profile hay không |
| MatchScore | Normalize metric hợp lệ thành processed Player score |
| PlayerAIProfile | Persistent processed profile theo `userId` |
| TeamProfile | Match-scoped processed/aggregated team fields cho đúng một `matchId` |
| TeamPerformance | Composite normalized score của current match nếu đủ required components |
| AED | Consume Profile/TeamPerformance và áp policy ở M1-015 |
| Scenario Configuration | Cung cấp bounded configuration cho gameplay systems |

Không tồn tại luồng:

```text
PlayerAIProfile → Stalker FSM trực tiếp
TeamPerformance → CHASE / ATTACK / SEARCH
Profile → Target Selection
Profile → Navigation command
M1-014 → tự đổi Scenario Configuration
```

Profile chỉ tạo **processed input** cho AED.

---

# 4. Data Flow

Frozen logical flow:

```text
TelemetryEvent v1.0
        ↓
validated aggregation
        ↓
MatchTelemetry
        ├─ MatchProfileEligibility
        └─ MatchMetric<T> / MetricAvailability
        ↓
MatchScore
        ↓
eligibility gate
        ↓
per-dimension EMA update
        ↓
PlayerAIProfile
```

Player profile apply rule:

```text
MatchProfileEligibility = ELIGIBLE
AND MatchScore[d] != null
→ được phép apply EMA cho dimension d
```

```text
MatchProfileEligibility = INELIGIBLE
→ không apply bất kỳ Player MatchScore nào vào persistent PlayerAIProfile
→ không tăng sampleCount
```

Song song ở team level:

```text
TelemetryEvent / MatchTelemetry
        ↓
MatchProfileEligibility
        ↓
TeamProfile(matchId)
        ↓
normalized TeamPerformance components
        ↓
TeamPerformance(current match)
```

Nếu match ineligible:

```text
TeamProfile
→ không persist như valid profile result

TeamPerformance
→ score = null
→ status = INCOMPLETE
```

Raw telemetry vẫn có thể được lưu/debug theo Telemetry contract.

Sau M1-014:

```text
PlayerAIProfile
+
eligible current-match TeamProfile
+
eligible current-match TeamPerformance
        ↓
M1-015 / AED
        ↓
Scenario Configuration
```

M1-014 dừng trước AED policy.

---

# 5. Profile Terminology

## 5.1. Raw Telemetry

`TelemetryEvent` là event structured được freeze ở Telemetry schema.

Ví dụ:

```text
PLAYER_DOWNED
PLAYER_REVIVED
PLAYER_ELIMINATED
PLAYER_ESCAPED
PHASE_STARTED
PHASE_COMPLETED
SECURITY_HOLD_INTERRUPTED
NOISE_EMITTED
TEAM_TOOL_USED
MATCH_ENDED
```

Event tồn tại hoặc schema-valid không đồng nghĩa metric/profile dimension tự động đủ semantic để `ACTIVE`.

---

## 5.2. MatchTelemetry

`MatchTelemetry` là tập metric đã aggregate trong một match.

Ví dụ logical metric:

```text
playerTerminalOutcome
profileNoisePenaltyCount
teamObjectiveTimeSeconds
teamSurvivorCount
teamSize
```

M1-014 không freeze database schema của `MatchTelemetry`.

M1-014 freeze consumer-side availability semantic:

```text
MetricAvailability
=
AVAILABLE | UNAVAILABLE
```

Logical representation:

```text
MatchMetric<T>
{
    value: T | null,
    availability: AVAILABLE | UNAVAILABLE
}
```

Rule:

```text
availability = AVAILABLE
→ value có semantic hợp lệ để formula consume
```

```text
availability = UNAVAILABLE
→ formula không được tạo synthetic zero
→ MatchScore/component tương ứng = null
```

Aggregation/transport xác định *vì sao* metric unavailable nằm ngoài scope M1-014.

---

## 5.3. MatchProfileEligibility

`MatchProfileEligibility` xác định match có được phép update processed profile hay không:

```text
ELIGIBLE
INELIGIBLE
```

Frozen v1.0:

```text
MATCH_ENDED.reasonCode = MATCH_ABORTED
→ MatchProfileEligibility = INELIGIBLE
```

Match kết thúc hợp lệ khác chỉ được coi `ELIGIBLE` khi aggregation có `MATCH_ENDED` hợp lệ và không thuộc explicit ineligible rule của current formula version.

Telemetry validity khác Profile eligibility:

```text
schema-valid TelemetryEvent
≠
match automatically profile-eligible
```

M1-014 không xóa/sửa telemetry của ineligible match.

---

## 5.4. MatchScore

`MatchScore` là processed Player score của một match.

Mỗi Player dimension:

```text
number trong [0,100]
hoặc
null nếu không có score hợp lệ
```

`null` không có nghĩa performance = 0.

`MatchScore` có thể được tính để debug một ineligible match, nhưng **không được apply vào persistent PlayerAIProfile** khi `MatchProfileEligibility = INELIGIBLE`.

---

## 5.5. PlayerAIProfile

`PlayerAIProfile` là profile persistent theo Player `userId`.

Profile chỉ được cập nhật từ MatchScore hợp lệ của match `ELIGIBLE` bằng EMA.

Profile không đọc raw telemetry để tự suy đoán formula khác với M1-014.

---

## 5.6. TeamProfile

`TeamProfile` v1.0 là **match-scoped processed team profile**.

Identity:

```text
teamKey = matchId
```

Mỗi match tạo một TeamProfile logical record riêng.

Không có persistent historical party/team identity trong M1-014 v1.0.

Một TeamProfile field có thể:

```text
ACTIVE
DEFERRED
```

ACTIVE field vẫn có thể `value = null` khi current-match metric `UNAVAILABLE`.

---

## 5.7. TeamPerformance

`TeamPerformance` là composite score của **current eligible match**:

```text
ObjectiveSpeed
Survival
Teamwork
ResourceEfficiency
```

Chỉ được `COMPLETE` khi:

- match profile-eligible;
- tất cả required component available/non-null/valid;
- weight config hợp lệ.

---

# 6. PlayerAIProfile Schema

## 6.1. Frozen Dimensions

Schema v0 giữ đúng 9 dimension:

```text
survival
objective
teamwork
exploration
navigation
toolUsage
risk
noise
revive
```

Không bỏ hoặc gộp dimension trong M1-014 v0.

---

## 6.2. Player Dimension State

Mỗi dimension có logical structure:

```text
PlayerDimensionState
{
    score: number | null,
    status: COLD_START | ACTIVE | DEFERRED,
    sampleCount: integer
}
```

Constraints:

```text
sampleCount >= 0
```

Nếu:

```text
status = COLD_START
```

thì baseline:

```text
score = 50
sampleCount = 0
```

Nếu:

```text
status = ACTIVE
```

thì:

```text
score ∈ [0,100]
sampleCount >= 1
```

Nếu:

```text
status = DEFERRED
```

thì:

```text
score = null
```

`DEFERRED` không được biểu diễn bằng neutral score `50`.

---

## 6.3. Logical PlayerAIProfile Object

```text
PlayerAIProfile
{
    userId,
    survival,
    objective,
    teamwork,
    exploration,
    navigation,
    toolUsage,
    risk,
    noise,
    revive,
    profileFormulaVersion,
    normalizationConfigVersion,
    updatedAt
}
```

Trong đó 9 dimension dùng `PlayerDimensionState`.

Metadata persistence/storage representation cụ thể là Backend implementation concern, nhưng semantic của field phải giữ nguyên.

---

# 7. Player Dimension Status / Source Mapping

M1-014 không mặc định cả 9 dimension đều ACTIVE.

Frozen current baseline:

| Dimension | Purpose | Status in v1.0 | Current metric source | MatchScore formula status | Missing-data behavior | Notes / dependency |
|---|---|---|---|---|---|---|
| `survival` | Player terminal survival outcome | **ACTIVE** | `PLAYER_ESCAPED`, `PLAYER_ELIMINATED` | FROZEN — terminal categorical score | unavailable/null → no update | Chỉ apply khi match `ELIGIBLE` |
| `objective` | Individual objective performance | **DEFERRED** | Phase/objective timing hiện chủ yếu system/team-level | Không đủ Player attribution | score null | Objective timing không đủ chứng minh individual contribution |
| `teamwork` | Chất lượng co-op/team contribution | **DEFERRED** | Revive/tool/help events tồn tại nhưng không đủ quality semantic | Chưa freeze | score null | Không suy `tool use nhiều = teamwork tốt` |
| `exploration` | Exploration behavior/performance | **DEFERRED** | Chưa có source đầy đủ | Chưa freeze | score null | Không invent position/sampling event |
| `navigation` | Navigation efficiency | **DEFERRED** | `average distance`, `split time`, `backtrack`, `wrong-route` chưa freeze source/sampling đầy đủ | Chưa freeze | score null | Phụ thuộc navigation sampling/metric contract sau |
| `toolUsage` | Tool-use behavior/quality | **DEFERRED** | `TEAM_TOOL_USED` chứng minh usage | Không đủ quality/efficiency semantic | score null | Event usage tồn tại nhưng direction tốt/xấu chưa freeze |
| `risk` | Risk behavior/performance | **DEFERRED** | Risk metric chưa có source/formula đầy đủ | Chưa freeze | score null | Không suy risk chỉ từ down/noise |
| `noise` | Noise discipline theo profile penalty signal được cấu hình | **ACTIVE** | Player-attributed `NOISE_EMITTED` **matching `ProfileNoiseFilter`** | FROZEN — higher-is-worse normalization của `ProfileNoisePenaltyCount` | unavailable/null → no update | Không mặc định mọi noiseType là bad |
| `revive` | Revive performance/contribution | **DEFERRED** | `PLAYER_REVIVED.data.reviverPlayerId` tồn tại | Chưa đủ denominator/opportunity semantic | score null | Không tự định nghĩa revive success rate nếu chưa có attempt/opportunity contract |

---

# 8. MatchScore Contract

## 8.1. Scale

Mọi non-null Player MatchScore dimension:

```text
0 <= MatchScore[d] <= 100
```

Output phải clamp về `[0,100]`.

---

## 8.2. Logical MatchScore Object

```text
PlayerMatchScore
{
    matchId,
    userId,
    matchProfileEligibility: ELIGIBLE | INELIGIBLE,
    survival: number | null,
    objective: number | null,
    teamwork: number | null,
    exploration: number | null,
    navigation: number | null,
    toolUsage: number | null,
    risk: number | null,
    noise: number | null,
    revive: number | null,
    matchScoreFormulaVersion,
    normalizationConfigVersion
}
```

Không có field nào được tự chuyển từ `null` sang `0`.

---

## 8.3. Match Eligibility Gate

Frozen rule:

```text
MATCH_ENDED.reasonCode = MATCH_ABORTED
→ MatchProfileEligibility = INELIGIBLE
```

Khi:

```text
MatchProfileEligibility = INELIGIBLE
```

thì:

```text
→ không apply PlayerMatchScore vào PlayerAIProfile
→ không tăng bất kỳ Player dimension sampleCount nào
→ không persist TeamProfile như valid profile result
→ không tạo TeamPerformance COMPLETE
→ TeamPerformance.score = null
→ TeamPerformance.status = INCOMPLETE
```

Raw telemetry vẫn có thể được lưu/debug.

Telemetry event schema-valid không tự làm match profile-eligible.

---

## 8.4. `survival` MatchScore — ACTIVE

Source:

```text
PLAYER_ESCAPED
PLAYER_ELIMINATED
```

Metric/outcome phải `AVAILABLE`.

Frozen formula:

```text
valid PLAYER_ESCAPED terminal outcome
→ survival MatchScore = 100
```

```text
valid PLAYER_ELIMINATED terminal outcome
→ survival MatchScore = 0
```

Nếu terminal outcome `UNAVAILABLE`:

```text
survival MatchScore = null
```

Nếu aggregation phát hiện contradictory terminal outcome:

```text
PLAYER_ESCAPED
+
PLAYER_ELIMINATED
```

thì:

```text
survival MatchScore = null
→ aggregation/validation error
→ không update survival profile
```

Direction:

```text
higher-is-better
```

M1-014 không tự thêm Down Count weight vào survival score v1.0.

`PLAYER_DOWNED` không được tự trộn vào formula khi weight/semantic chưa freeze.

**Eligibility apply rule:**

```text
MATCH_ABORTED
→ có thể có partial terminal telemetry
→ nhưng không update persistent survival profile
→ không tăng survival.sampleCount
```

---

## 8.5. `noise` MatchScore — ACTIVE

Source event family:

```text
NOISE_EMITTED
```

M1-014 **không** freeze mọi player-attributed `NOISE_EMITTED` là bad behavior.

Frozen logical metric:

```text
ProfileNoisePenaltyCount
=
count(
    valid player-attributed NOISE_EMITTED
    matching ProfileNoiseFilter
)
```

Event candidate condition:

```text
eventType = NOISE_EMITTED
AND userId = Player cần tính
AND event active/valid trong schemaVersion 1.0
AND event matches ProfileNoiseFilter
```

`ProfileNoiseFilter`:

```text
→ CONFIGURABLE
→ VERSIONED qua normalization/config version
→ xác định noiseType / reason/rule nào được tính là profile penalty signal
```

M1-014 không hard-code:

```text
SPRINT = bad
NOISE_MAKER = bad
CORE_DROP = bad
```

nếu filter/config chưa freeze các mapping đó.

Direction:

```text
higher-is-worse
```

Frozen normalization:

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

Constraint:

```text
ProfileNoiseCountMax > ProfileNoiseCountMin
```

Threshold là configurable, không hard-code số.

Availability rule:

```text
MetricAvailability = AVAILABLE
AND không có NOISE_EMITTED matching ProfileNoiseFilter
→ ProfileNoisePenaltyCount = 0
→ valid zero
→ compute noise MatchScore
```

```text
MetricAvailability = UNAVAILABLE
→ noise MatchScore = null
```

Config rule:

```text
ProfileNoiseFilter invalid
OR normalization config invalid
→ noise MatchScore = null
→ configuration validation error
→ không update noise profile
```

Noise MatchScore của match `INELIGIBLE` không được apply vào persistent Player Profile dù có thể compute để debug.

---

## 8.6. Deferred MatchScore Dimensions

Các dimension sau trong current baseline:

```text
objective
teamwork
exploration
navigation
toolUsage
risk
revive
```

không được tính score bằng heuristic tự phát.

Frozen rule:

```text
dimension.status = DEFERRED
→ MatchScore[d] = null
→ PlayerAIProfile[d].score = null
→ không update EMA
→ không tăng sampleCount
```

Future activation yêu cầu:

```text
source metric rõ
+
MetricAvailability semantic
+
formula semantic rõ
+
normalization direction rõ
+
formula/config version tương ứng
```

---

# 9. Normalization Contract

## 9.1. General Rule

Normalization chỉ được áp dụng cho metric có:

- source hợp lệ;
- aggregation semantic rõ;
- direction rõ;
- threshold/config hợp lệ.

Output:

```text
score ∈ [0,100]
```

---

## 9.2. Positive Metric — Higher Is Better

Pattern:

```text
score
=
100 * clamp(
    (x - Min) / (Max - Min),
    0,
    1
)
```

Constraint:

```text
Max > Min
```

---

## 9.3. Negative Metric — Higher Is Worse

Pattern:

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

Constraint:

```text
Max > Min
```

---

## 9.4. Target-Range Metric

Nếu future ACTIVE metric có target range, formula phải được freeze riêng với:

```text
TargetMin
TargetMax
LowerBad
UpperBad
```

M1-014 v1.0 không có ACTIVE Player dimension cần target-range normalization.

Không tự chọn target-range formula cho DEFERRED metric.

---

## 9.5. Threshold Ownership

Các threshold như:

```text
Min
Max
MinGood
MaxGood
MaxBad
Target
NormalizationMin
NormalizationMax
```

nếu được dùng:

```text
→ configurable data
```

Không được hard-code tuning number vào source code nếu M1-014 không freeze số đó.

Formula topology/semantic:

```text
FROZEN
```

Numerical tuning:

```text
CONFIGURABLE
```

---

# 10. Cold Start Contract

Cold start chỉ áp dụng cho dimension có contract `ACTIVE`.

Player mới:

```text
score = 50
status = COLD_START
sampleCount = 0
```

`50` là neutral baseline.

Phải phân biệt:

```text
score = 50
status = COLD_START
sampleCount = 0
```

với:

```text
score = 50
status = ACTIVE
sampleCount > 0
```

Hai state có semantic khác nhau.

---

## 10.1. First Valid Match

Khi có MatchScore hợp lệ đầu tiên:

```text
newScore
=
(1 - alpha_d) * 50
+
alpha_d * matchScore_d
```

Sau update:

```text
status = ACTIVE
sampleCount = 1
```

Không invent special first-match overwrite rule.

---

## 10.2. Cold Start + Missing MatchScore

Nếu dimension `COLD_START` và match không có score hợp lệ:

```text
matchScore[d] = null
```

thì:

```text
score = 50
status = COLD_START
sampleCount = 0
```

Không chuyển sang ACTIVE.

---

## 10.3. Deferred Dimension

Nếu dimension `DEFERRED`:

```text
score = null
status = DEFERRED
```

Cold-start neutral `50` không áp dụng.

---

# 11. Missing Data / Metric Availability Contract

Đây là rule bắt buộc của M1-014.

## 11.1. MetricAvailability

Frozen enum:

```text
MetricAvailability
=
AVAILABLE | UNAVAILABLE
```

Logical form:

```text
MatchMetric<T>
{
    value: T | null,
    availability: AVAILABLE | UNAVAILABLE
}
```

Consumer rule:

```text
availability = AVAILABLE
→ aggregation xác nhận metric đủ semantic/coverage để formula dùng
```

```text
availability = UNAVAILABLE
→ MatchScore/component = null
→ không synthetic zero
```

M1-014 không implement transport/retry/completeness detection; chỉ freeze consumer-side semantic.

---

## 11.2. Player MatchScore Missing Rule

Nếu:

```text
matchScore[d] = null
```

thì:

```text
PlayerAIProfile[d].score
→ giữ nguyên

PlayerAIProfile[d].status
→ giữ nguyên nếu contract hiện tại vẫn phù hợp

PlayerAIProfile[d].sampleCount
→ không tăng
```

Không được:

```text
null → 0
missing telemetry → poor performance
missing metric → synthetic metric
missing metric → lấy metric khác thay thế
```

Không được tự renormalize bằng dữ liệu giả.

---

## 11.3. Valid Zero vs Missing

Phải phân biệt:

```text
availability = AVAILABLE
AND observed value/count = 0
→ valid zero
```

với:

```text
availability = UNAVAILABLE
→ null
```

Không được suy:

```text
không thấy event
→ count = 0
```

trừ khi aggregation đã xác nhận `MetricAvailability = AVAILABLE`.

Noise example:

```text
MetricAvailability = AVAILABLE
AND không có event matching ProfileNoiseFilter
→ ProfileNoisePenaltyCount = 0
```

```text
MetricAvailability = UNAVAILABLE
→ noise MatchScore = null
```

Phase timing example:

```text
required PHASE_STARTED / PHASE_COMPLETED pair incomplete
→ objectiveTime availability = UNAVAILABLE
→ objectiveTime = null
```

---

## 11.4. Match Ineligibility Is Not Missing Data

`MatchProfileEligibility = INELIGIBLE` khác `MetricAvailability = UNAVAILABLE`.

Một aborted match có thể chứa metric `AVAILABLE` để debug, nhưng:

```text
INELIGIBLE
→ không apply vào persistent PlayerAIProfile
→ không persist TeamProfile như valid result
→ TeamPerformance INCOMPLETE
```

---

# 12. Player Profile Update Formula

## 12.1. EMA — Frozen

Với mỗi dimension `d` có `matchScore_d != null`:

```text
newScore_d
=
(1 - alpha_d) * oldScore_d
+
alpha_d * matchScore_d
```

Sau đó:

```text
newScore_d
=
clamp(newScore_d, 0, 100)
```

---

## 12.2. Alpha Constraint

```text
0 < alpha_d <= 1
```

`alpha_d`:

- configurable;
- có thể khác nhau giữa dimension;
- không hard-code numerical value trong implementation;
- M1-014 không freeze numerical alpha cụ thể.

Ví dụ logical config:

```text
ProfileAlpha.survival
ProfileAlpha.noise
```

DEFERRED dimension không cần runtime alpha trong current baseline.

---

## 12.3. Sample Count

`sampleCount` đếm số MatchScore non-null đã thực sự được apply vào dimension.

Rule:

```text
valid non-null MatchScore update
→ sampleCount = sampleCount + 1
```

```text
null MatchScore
→ sampleCount giữ nguyên
```

```text
DEFERRED dimension
→ sampleCount không tăng từ metric chưa tồn tại
```

---

## 12.4. Status Transition

ACTIVE-contract dimension:

```text
COLD_START
+
first valid MatchScore update
→ ACTIVE
```

Sau khi đã `ACTIVE`:

```text
valid MatchScore
→ ACTIVE
```

```text
null MatchScore
→ giữ ACTIVE + giữ score + giữ sampleCount
```

DEFERRED dimension không tự chuyển sang ACTIVE trong cùng formula baseline.

Việc activate future dimension yêu cầu formula/version change.

---

# 13. Player Profile Ownership / Persistence

## 13.1. Player Ownership

`PlayerAIProfile` là persistent profile theo:

```text
userId
```

Mỗi Player có một logical persistent profile record.

M1-014 không freeze database table/index/storage engine.

---

## 13.2. Update Authority

Profile update chỉ được apply từ:

```text
MatchProfileEligibility = ELIGIBLE
+
validated non-null MatchScore
+
frozen formula/config
```

Không update Profile trực tiếp từ arbitrary client claim.

Không update trực tiếp từ raw telemetry event bằng formula ngoài M1-014.

---

## 13.3. Eligibility Gate

Nếu:

```text
MatchProfileEligibility = INELIGIBLE
```

thì:

```text
không apply EMA cho bất kỳ dimension nào
không tăng bất kỳ sampleCount nào
không đổi COLD_START → ACTIVE
```

Dù một MatchScore debug value có thể tính được từ partial telemetry, nó không được persist vào PlayerAIProfile.

---

## 13.4. Partial Dimension Update

Với match `ELIGIBLE`, không yêu cầu mọi dimension update trong cùng match.

Ví dụ:

```text
survival MatchScore = 100
noise MatchScore = null
```

thì:

```text
survival → EMA update + sampleCount tăng
noise → giữ nguyên score/status/sampleCount
```

---

# 14. TeamProfile Schema

## 14.1. Frozen Team Fields

TeamProfile v0 giữ đúng:

```text
objectiveTime
splitTime
avgDistance
reviveSuccess
resourceEfficiency
communication
wipeRecovery
```

Không bỏ hoặc gộp field trong v0.

---

## 14.2. Team Field State

Logical structure:

```text
TeamFieldState
{
    value: number | null,
    status: ACTIVE | DEFERRED,
    availability: AVAILABLE | UNAVAILABLE,
    source: string
}
```

Nếu:

```text
status = DEFERRED
```

thì:

```text
value = null
availability = UNAVAILABLE
```

ACTIVE field có thể:

```text
availability = UNAVAILABLE
value = null
```

cho một current match cụ thể.

---

## 14.3. TeamProfile Identity — Frozen

TeamProfile v1.0 là **MATCH-SCOPED**.

Frozen identity semantic:

```text
teamKey = matchId
```

hoặc tương đương:

```text
TeamProfileIdentity
→ authoritative match identity
```

Logical object:

```text
TeamProfile
{
    teamKey = matchId,
    matchId,
    objectiveTime,
    splitTime,
    avgDistance,
    reviveSuccess,
    resourceEfficiency,
    communication,
    wipeRecovery,
    profileFormulaVersion,
    normalizationConfigVersion
}
```

Mỗi match tạo một TeamProfile logical record riêng.

Ví dụ:

```text
Match A roster = Player 1,2,3,4
→ TeamProfile(matchA)

Match B roster = Player 1,2,3,4
→ TeamProfile(matchB)
```

Hai TeamProfile là khác nhau.

Không suy:

```text
same roster
→ same persistent team identity
```

M1-014 v1.0 chưa freeze historical persistent Party/Team Profile.

---

# 15. Team Field Status / Source Mapping

Frozen current baseline:

| Team Field | Semantic | Status | Current Source | Formula / Aggregation | Missing Behavior | Dependency |
|---|---|---|---|---|---|---|
| `objectiveTime` | Elapsed wall-clock duration của objective-bearing gameplay phases | **ACTIVE** | `PHASE_STARTED` + `PHASE_COMPLETED` | Sum valid matched elapsed phase durations | UNAVAILABLE → null | Objective-bearing phase membership từ frozen gameplay/phase config |
| `splitTime` | Team split behavior/time | **DEFERRED** | Source/sampling chưa freeze đầy đủ | Chưa freeze | null | Cần navigation/team sampling contract |
| `avgDistance` | Average team spacing/distance | **DEFERRED** | Continuous/sampled position source chưa freeze | Chưa freeze | null | Cần sampling policy |
| `reviveSuccess` | Revive success quality/rate | **DEFERRED** | `PLAYER_REVIVED` có success event nhưng không có revive-attempt denominator | Chưa freeze denominator | null | Cần attempt/opportunity contract |
| `resourceEfficiency` | Chất lượng dùng resource/tool | **DEFERRED** | `TEAM_TOOL_USED` chỉ chứng minh usage | Không đủ efficiency semantic | null | Cần outcome/waste definition |
| `communication` | Chất lượng communication/co-op signal | **DEFERRED** | `HELP_PING_USED` chưa đủ quality semantic | Chưa freeze | null | Không dùng số ping như quality score |
| `wipeRecovery` | Team recovery sau pressure/wipe-like state | **DEFERRED** | Chưa có frozen source/definition đầy đủ | Chưa freeze | null | Cần gameplay/team-state definition |

---

## 15.1. `objectiveTime` Aggregation

`objectiveTime` là raw/aggregated **elapsed wall-clock phase duration**, không phải normalized score.

Logical formula:

```text
objectiveTime
=
sum(
    PHASE_COMPLETED.ts
    - matching PHASE_STARTED.ts
    cho các objective-bearing phase hợp lệ
)
```

Điều kiện:

- `MatchProfileEligibility = ELIGIBLE` để TeamProfile được persist như valid profile result;
- metric availability phải `AVAILABLE`;
- start/completed pair phải hợp lệ;
- cùng match;
- phase identity phải match;
- không double-count reserved specialized phase events;
- objective-bearing phase membership lấy từ frozen gameplay/phase configuration;
- thiếu required pair → `availability = UNAVAILABLE`, `objectiveTime = null`.

Unit:

```text
seconds
```

### Interruption / Pause Semantic — Frozen

Trong M1-014 v1.0:

```text
objectiveTime
= elapsed wall-clock duration từ PHASE_STARTED.ts tới PHASE_COMPLETED.ts
```

Do đó:

```text
SECURITY_HOLD_INTERRUPTED
hoặc progress pause/interruption nằm giữa hai phase boundary
→ thời gian interruption VẪN nằm trong objectiveTime
```

M1-014 **không subtract interruption duration**.

Không tính semantic:

```text
active interaction time excluding pause
```

vì current telemetry contract chưa freeze đủ source/formula cho metric đó.

Nếu future cần active-only duration:

```text
→ formula/version change riêng
```

---

# 16. TeamProfile Ownership / Persistence

## 16.1. Match-Scoped Ownership

TeamProfile v1.0 thuộc đúng một match:

```text
teamKey = matchId
```

Không phải persistent party/team historical profile.

---

## 16.2. No Cross-Match Merge

Không merge:

```text
TeamProfile(matchA)
+
TeamProfile(matchB)
```

dù roster giống nhau.

Không carry field của match trước sang match hiện tại.

Không dùng previous-match TeamProfile field để lấp missing current-match field.

---

## 16.3. Match Eligibility

Nếu:

```text
MatchProfileEligibility = ELIGIBLE
```

thì TeamProfile current match có thể được persist như valid processed result theo availability/status từng field.

Nếu:

```text
MatchProfileEligibility = INELIGIBLE
```

thì:

```text
→ không persist TeamProfile như valid profile result
→ không dùng TeamProfile đó làm input valid cho TeamPerformance COMPLETE
```

Raw/debug aggregate vẫn có thể tồn tại ở data pipeline, nhưng không được gắn semantic “valid TeamProfile result”.

---

## 16.4. No Historical Team EMA

M1-014 v1.0 không freeze:

```text
TeamProfile_old
→ EMA
→ TeamProfile_new
```

Nếu future cần persistent historical Party/Team Profile:

```text
→ phải có Party/Team identity contract
→ formula/version riêng
```

---

# 17. TeamPerformance Contract

## 17.1. Required Components

TeamPerformance v0 dùng đúng 4 normalized component:

```text
ObjectiveSpeed
Survival
Teamwork
ResourceEfficiency
```

Mỗi component khi available:

```text
score ∈ [0,100]
```

Không đưa raw seconds/count trực tiếp vào weighted formula.

---

## 17.2. Formula Structure — Frozen

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
wObjective >= 0
wSurvival >= 0
wTeamwork >= 0
wResource >= 0
```

```text
wObjective
+ wSurvival
+ wTeamwork
+ wResource
= 1
```

Weights:

```text
CONFIGURABLE
```

M1-014 không freeze numerical weight cụ thể.

Không hard-code `30/25/25/20` như frozen baseline value.

---

# 18. TeamPerformance Component Ownership

## 18.1. ObjectiveSpeed

Source:

```text
TeamProfile.objectiveTime
```

Required:

```text
MatchProfileEligibility = ELIGIBLE
AND objectiveTime.availability = AVAILABLE
AND objectiveTime != null
```

Direction:

```text
higher objectiveTime = worse ObjectiveSpeed
```

Normalization:

```text
ObjectiveSpeed
=
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

Constraint:

```text
ObjectiveTimeMax > ObjectiveTimeMin
```

Threshold configurable.

`objectiveTime` là elapsed wall-clock phase duration; interruption nằm giữa phase start/completion không bị subtract trong v1.0.

Nếu input/config invalid hoặc unavailable:

```text
ObjectiveSpeed = null
```

---

## 18.2. Survival

Team Survival source:

```text
MATCH_STARTED.context.teamSize
MATCH_ENDED.data.survivorCount
```

Required:

```text
MatchProfileEligibility = ELIGIBLE
AND MetricAvailability = AVAILABLE
AND teamSize > 0
AND survivorCount hợp lệ
```

Formula:

```text
Survival
=
100 * clamp(
    survivorCount / teamSize,
    0,
    1
)
```

Nếu:

```text
MATCH_ABORTED
OR MatchProfileEligibility = INELIGIBLE
OR metric UNAVAILABLE
OR teamSize <= 0
OR survivorCount invalid
```

thì:

```text
Survival = null
```

Không invent member outcomes.

---

## 18.3. Teamwork

Current baseline:

```text
DEFERRED
Teamwork = null
```

`PLAYER_REVIVED`, `TEAM_TOOL_USED`, `HELP_PING_USED` chưa đủ formula semantic để tính normalized Teamwork quality.

Không tự tạo heuristic.

---

## 18.4. ResourceEfficiency

Current baseline:

```text
DEFERRED
ResourceEfficiency = null
```

`TEAM_TOOL_USED` không đủ để xác định efficient/wasteful/high-value usage.

Không tự tạo denominator/outcome.

---

# 19. TeamPerformance Completeness Contract

## 19.1. Logical Structure

```text
TeamPerformance
{
    matchId,
    score: number | null,
    status: COMPLETE | INCOMPLETE,
    components: {
        ObjectiveSpeed,
        Survival,
        Teamwork,
        ResourceEfficiency
    },
    formulaVersion,
    normalizationConfigVersion
}
```

TeamPerformance là current-match result.

---

## 19.2. COMPLETE

Chỉ khi:

```text
MatchProfileEligibility = ELIGIBLE
AND ObjectiveSpeed != null
AND Survival != null
AND Teamwork != null
AND ResourceEfficiency != null
AND weight config valid
```

thì:

```text
status = COMPLETE
```

và:

```text
score
=
clamp(
    wObjective * ObjectiveSpeed
    + wSurvival * Survival
    + wTeamwork * Teamwork
    + wResource * ResourceEfficiency,
    0,
    100
)
```

---

## 19.3. INCOMPLETE

Nếu match ineligible hoặc bất kỳ required component:

```text
null
DEFERRED
UNAVAILABLE
invalid
```

thì:

```text
TeamPerformance.score = null
TeamPerformance.status = INCOMPLETE
```

Đặc biệt:

```text
MATCH_ENDED.reasonCode = MATCH_ABORTED
→ INELIGIBLE
→ TeamPerformance = INCOMPLETE / null
```

---

## 19.4. Forbidden Missing-Component Behaviors

Không được:

```text
missing component = 0
```

Không được:

```text
bỏ missing weight rồi renormalize remaining weights
```

Không được:

```text
copy heuristic từ unrelated Player/team metric
```

Không được:

```text
previous-match TeamProfile/component
→ current-match component
```

TeamPerformance phải giữ cùng semantic giữa mọi match.

---

## 19.5. Current Baseline Consequence

Current baseline:

```text
ObjectiveSpeed = có thể ACTIVE
Survival = ACTIVE
Teamwork = DEFERRED
ResourceEfficiency = DEFERRED
```

Do đó current TeamPerformance:

```text
status = INCOMPLETE
score = null
```

Điều này **không làm M1-014 incomplete**.

M1-014 freeze formula topology, availability, eligibility và missing-component semantics.

---

# 20. Configurable Parameters

M1-014 sở hữu logical configurable data cho formula.

## 20.1. Player EMA

```text
ProfileAlpha.survival
ProfileAlpha.noise
```

Future activated dimension có alpha riêng khi formula được freeze.

Constraint:

```text
0 < alpha_d <= 1
```

---

## 20.2. Noise Profile Configuration

```text
ProfileNoiseFilter
ProfileNoiseCountMin
ProfileNoiseCountMax
```

`ProfileNoiseFilter` xác định `noiseType` / reason/rule nào được tính vào:

```text
ProfileNoisePenaltyCount
```

Filter không được hard-code semantic “mọi noise đều xấu”.

Constraint:

```text
ProfileNoiseCountMax > ProfileNoiseCountMin
```

Filter và threshold phải được resolve từ versioned normalization/config data.

---

## 20.3. Team Normalization

```text
ObjectiveTimeMin
ObjectiveTimeMax
```

Constraint:

```text
ObjectiveTimeMax > ObjectiveTimeMin
```

---

## 20.4. TeamPerformance Weights

```text
wObjective
wSurvival
wTeamwork
wResource
```

Constraints:

```text
mọi weight >= 0
sum(weights) = 1
```

Không freeze numerical `30/25/25/20`.

---

## 20.5. Frozen vs Configurable

```text
Formula topology / semantic
Match eligibility semantics
Metric availability semantics
TeamProfile identity semantics
→ FROZEN
```

```text
Numerical alpha
Normalization thresholds
ProfileNoiseFilter
TeamPerformance weights
→ CONFIGURABLE / VERSIONED
```

Changing configurable value không được đổi formula topology.

---

# 21. Versioning / Reproducibility

## 21.1. Required Logical Metadata

Processed profile/score phải ghi hoặc có khả năng resolve:

```text
profileFormulaVersion
matchScoreFormulaVersion
normalizationConfigVersion
```

`normalizationConfigVersion` phải resolve đủ configurable state dùng trong computation, gồm khi relevant:

```text
alpha_d
normalization thresholds
ProfileNoiseFilter
TeamPerformance weights
```

---

## 21.2. Reproducibility Rule

Với:

```text
same raw/aggregated inputs
+ same MatchProfileEligibility
+ same MetricAvailability
+ same old PlayerAIProfile
+ same formula version
+ same normalization/config version
+ same configurable values/filter
```

phải tạo:

```text
same MatchScore
same PlayerAIProfile update
same TeamProfile current-match values/status
same TeamPerformance result/status
```

---

## 21.3. Version Change Rule

Nếu thay đổi formula topology/semantic:

```text
→ formula version change
```

Ví dụ:

- đổi survival formula;
- đổi TeamPerformance required components;
- đổi TeamProfile identity semantic;
- đổi meaning của `objectiveTime`;
- đổi `ProfileNoisePenaltyCount` formula topology.

Nếu chỉ thay configurable/filter/tuning trong topology đã freeze:

```text
→ normalization/config version change
```

Ví dụ:

- alpha;
- ProfileNoiseFilter;
- normalization threshold;
- TeamPerformance weights.

M1-014 không thiết kế database migration.

---

# 22. Telemetry Boundary

Telemetry chỉ cung cấp source data.

Profile layer không được:

- tạo telemetry event mới;
- sửa Telemetry Event Schema v1.0;
- sử dụng RESERVED / NOT EMITTED event làm current source;
- suy `missing event = poor performance`;
- suy `absence of event = zero` nếu `MetricAvailability` chưa `AVAILABLE`;
- đọc runtime NoiseEvent trực tiếp;
- dùng hidden gameplay state không được telemetry/aggregation contract expose;
- invent navigation/resource/risk metric.

Frozen distinctions:

```text
Telemetry validity
≠ Profile eligibility
```

```text
event absence
≠ valid zero
```

```text
event exists
≠ metric semantic đủ để ACTIVE
```

Telemetry baseline cho phép `MATCH_ABORTED`; M1-014 chỉ áp eligibility gate, không xóa/sửa event.

Noise:

```text
NOISE_EMITTED
→ source event family
```

nhưng:

```text
ProfileNoisePenaltyCount
→ chỉ count event matching versioned ProfileNoiseFilter
```

Không mặc định toàn bộ noiseType là penalty.

---

# 23. AED / M1-015 Boundary

M1-014 dừng tại:

```text
TelemetryEvent
↓
MatchTelemetry
↓
MatchScore
↓
PlayerAIProfile
↓
TeamProfile
↓
TeamPerformance
```

M1-015 / AED chịu trách nhiệm:

```text
Profile / TeamPerformance
↓
AED policy
↓
Scenario Configuration
```

M1-014 không định nghĩa:

```text
TeamPerformance < X
→ StalkerSpeed += Y
```

Không định nghĩa:

```text
risk > X
→ spawn Listener
```

Không định nghĩa:

```text
noise > X
→ force SEARCH
```

Profile không trực tiếp điều khiển:

```text
Stalker
Listener
Warden
FSM
Target Selection
Navigation
Attack
runtime gameplay state
```

AED chỉ consume processed profile theo architecture đã freeze.

---

# 24. Profile / Formula Contract Test Cases

Các test case dưới đây đã được định nghĩa ở M1-014 và phải được implementation verify ở milestone tương ứng.

> `[x]` nghĩa là **contract case đã được định nghĩa**, không có nghĩa implementation hiện tại đã pass integration test.

- [x] Contract case defined — Player mới, ACTIVE-contract dimension → `score=50`, `status=COLD_START`, `sampleCount=0`.
- [x] Contract case defined — DEFERRED dimension → `score=null`, `status=DEFERRED`.
- [x] Contract case defined — first valid MatchScore dùng EMA với old score 50; không special overwrite.
- [x] Contract case defined — valid non-null MatchScore của eligible match → clamp `[0,100]` và `sampleCount += 1`.
- [x] Contract case defined — missing MatchScore → không update score và không tăng sampleCount.
- [x] Contract case defined — `null` không bao giờ tự convert thành `0`.
- [x] Contract case defined — COLD_START + missing score → giữ `50/COLD_START/0`.
- [x] Contract case defined — `MATCH_ABORTED` → `MatchProfileEligibility = INELIGIBLE`.
- [x] Contract case defined — `MATCH_ABORTED` → PlayerAIProfile score không update.
- [x] Contract case defined — `MATCH_ABORTED` → sampleCount không tăng.
- [x] Contract case defined — `MATCH_ABORTED` → TeamProfile không persist như valid profile result.
- [x] Contract case defined — `MATCH_ABORTED` → TeamPerformance `INCOMPLETE`, score null.
- [x] Contract case defined — schema-valid telemetry không tự đồng nghĩa profile-eligible.
- [x] Contract case defined — metric `AVAILABLE` + observed zero → valid zero.
- [x] Contract case defined — metric `UNAVAILABLE` → MatchScore/component null.
- [x] Contract case defined — absence of event không tự đồng nghĩa zero nếu availability chưa `AVAILABLE`.
- [x] Contract case defined — `PLAYER_ESCAPED` valid terminal outcome + eligible match → survival MatchScore 100 và được phép update.
- [x] Contract case defined — `PLAYER_ELIMINATED` valid terminal outcome + eligible match → survival MatchScore 0 và được phép update.
- [x] Contract case defined — contradictory escaped + eliminated → survival null / aggregation error.
- [x] Contract case defined — partial survival events trong aborted match không update persistent survival profile.
- [x] Contract case defined — Noise score chỉ count `NOISE_EMITTED` matching `ProfileNoiseFilter`.
- [x] Contract case defined — `NOISE_EMITTED` không matching filter → không tăng `ProfileNoisePenaltyCount`.
- [x] Contract case defined — `MetricAvailability=AVAILABLE` và không event matching filter → penalty count 0.
- [x] Contract case defined — invalid `ProfileNoiseFilter`/noise normalization config → noise MatchScore null + config validation error.
- [x] Contract case defined — M1-014 không hard-code `SPRINT`, `NOISE_MAKER` hoặc noiseType khác là bad nếu config không quy định.
- [x] Contract case defined — `TEAM_TOOL_USED` không tự activate `toolUsage`, `teamwork` hoặc `resourceEfficiency`.
- [x] Contract case defined — `PLAYER_REVIVED` không tự tạo revive success rate nếu denominator chưa freeze.
- [x] Contract case defined — RESERVED / NOT EMITTED telemetry event không được dùng làm current profile source.
- [x] Contract case defined — navigation/risk/resource metric không có source/formula đầy đủ → giữ DEFERRED.
- [x] Contract case defined — TeamProfile identity v1.0 là match-scoped, `teamKey = matchId`.
- [x] Contract case defined — cùng roster ở hai match khác nhau → hai TeamProfile khác nhau.
- [x] Contract case defined — TeamProfile match trước không được dùng làm current-match TeamPerformance component.
- [x] Contract case defined — objectiveTime chỉ tính từ valid matched objective-bearing `PHASE_STARTED/PHASE_COMPLETED`.
- [x] Contract case defined — incomplete phase pair → objectiveTime `UNAVAILABLE/null`.
- [x] Contract case defined — `SECURITY_HOLD_INTERRUPTED` nằm giữa start/completed → duration vẫn nằm trong elapsed objectiveTime.
- [x] Contract case defined — M1-014 không subtract interruption/pause khỏi objectiveTime.
- [x] Contract case defined — `reviveSuccess` không dùng denominator chưa có source.
- [x] Contract case defined — `communication` không dùng số `HELP_PING_USED` như quality.
- [x] Contract case defined — `ResourceEfficiency` không suy từ raw `TEAM_TOOL_USED` count.
- [x] Contract case defined — Team Survival chỉ compute khi eligible + metric AVAILABLE + teamSize > 0 + survivorCount valid.
- [x] Contract case defined — aborted/unavailable/invalid Team Survival input → Survival null.
- [x] Contract case defined — TeamPerformance chỉ COMPLETE khi eligible và đủ 4 required component.
- [x] Contract case defined — required component missing → `INCOMPLETE + null`.
- [x] Contract case defined — missing component không được coi 0.
- [x] Contract case defined — missing component không trigger weight renormalization.
- [x] Contract case defined — stale previous-match component không được dùng current match.
- [x] Contract case defined — TeamPerformance weights non-negative và sum = 1.
- [x] Contract case defined — invalid weight config → TeamPerformance INCOMPLETE/null.
- [x] Contract case defined — changing configurable alpha/filter/threshold/weight không đổi formula topology.
- [x] Contract case defined — same inputs + same eligibility/availability + same formula/config version → reproduce same result.
- [x] Contract case defined — Profile không trực tiếp update Scenario Configuration.
- [x] Contract case defined — Profile không command Monster AI / FSM / Target Selection / Navigation / Attack.

---

# 25. Implementation Constraints

1. Giữ đúng 9 PlayerAIProfile dimensions: `survival`, `objective`, `teamwork`, `exploration`, `navigation`, `toolUsage`, `risk`, `noise`, `revive`.
2. Không mặc định mọi dimension là ACTIVE.
3. `DEFERRED → score=null`; không dùng `DEFERRED → 50`.
4. ACTIVE cold start dùng `score=50`, `status=COLD_START`, `sampleCount=0`.
5. Mọi non-null processed score phải nằm trong `[0,100]`.
6. Player profile update phải dùng EMA; `0 < alpha_d <= 1`.
7. Alpha configurable; không hard-code numerical alpha.
8. First valid MatchScore không dùng special overwrite formula.
9. Missing MatchScore không update score và không tăng sampleCount.
10. Không convert `null → 0`.
11. `MetricAvailability = UNAVAILABLE` phải tạo null ở consumer formula.
12. Không coi absence of event là zero nếu availability chưa được xác nhận `AVAILABLE`.
13. Telemetry schema-valid không đồng nghĩa Profile-eligible.
14. `MATCH_ENDED.reasonCode = MATCH_ABORTED` → `MatchProfileEligibility = INELIGIBLE`.
15. Không update persistent PlayerAIProfile từ ineligible match.
16. Không tăng sampleCount từ ineligible match.
17. Không đổi COLD_START → ACTIVE từ ineligible match.
18. `survival` chỉ dùng terminal `PLAYER_ESCAPED` / `PLAYER_ELIMINATED`; không tự trộn Down Count.
19. Aborted match không update survival profile dù có partial terminal telemetry.
20. `noise` dùng `ProfileNoisePenaltyCount`, không dùng toàn bộ NoiseEvent count như automatic penalty.
21. `ProfileNoisePenaltyCount` chỉ count player-attributed `NOISE_EMITTED` matching `ProfileNoiseFilter`.
22. `ProfileNoiseFilter` configurable/versioned; không hard-code noiseType nào “bad” nếu config không quy định.
23. Invalid noise filter/config → noise MatchScore null; không update noise profile.
24. Normalization thresholds configurable/versioned.
25. `objective`, `teamwork`, `exploration`, `navigation`, `toolUsage`, `risk`, `revive` giữ DEFERRED current baseline.
26. Không activate dimension chỉ vì có event tên gần giống.
27. Không dùng reserved/not-emitted event làm source.
28. Không thêm telemetry event mới.
29. TeamProfile giữ đúng 7 frozen fields.
30. TeamProfile v1.0 là match-scoped; `teamKey = matchId`.
31. Không merge TeamProfile giữa các match dù roster giống nhau.
32. Không carry/reuse stale TeamProfile field của match trước vào current match.
33. Ineligible match không persist TeamProfile như valid profile result.
34. TeamProfile v1.0 không có historical EMA.
35. `objectiveTime` là elapsed wall-clock phase duration.
36. `SECURITY_HOLD_INTERRUPTED`/pause nằm giữa phase boundaries không bị subtract khỏi `objectiveTime`.
37. Không tự định nghĩa active-only objective duration trong v1.0.
38. TeamProfile DEFERRED field phải `value=null`.
39. Không tự định nghĩa revive-attempt denominator.
40. Không dùng `HELP_PING_USED` count như communication quality.
41. Không dùng `TEAM_TOOL_USED` count như resource efficiency.
42. TeamPerformance dùng normalized `ObjectiveSpeed`, `Survival`, `Teamwork`, `ResourceEfficiency`.
43. Team Survival chỉ compute khi eligible + availability AVAILABLE + valid teamSize/survivorCount.
44. Không đưa raw seconds trực tiếp vào TeamPerformance weighted formula.
45. TeamPerformance weights non-negative, sum = 1, configurable.
46. Không freeze numerical 30/25/25/20.
47. Ineligible match hoặc required component null/deferred/unavailable/invalid → TeamPerformance INCOMPLETE/null.
48. Không missing-component=0.
49. Không implicit weight renormalization.
50. Không dùng previous-match TeamProfile/component để làm current-match TeamPerformance COMPLETE.
51. Formula topology/semantic frozen; tuning/filter configurable và versioned.
52. Formula/config version phải đủ để reproduce/debug cùng eligibility/availability.
53. PlayerAIProfile update authority chỉ nhận eligible validated MatchScore.
54. Profile không trực tiếp điều khiển Stalker/Listener/Warden.
55. Profile không trực tiếp điều khiển FSM, Target Selection, Navigation hoặc Attack.
56. M1-014 không map Profile/TeamPerformance sang Scenario Configuration.
57. AED policy thuộc M1-015.

---

# 26. M1-014 Completion Criteria

Task **M1-014 — Player/Team Profile fields + formulas v0** được xem là hoàn thành khi:

- [x] 9 Player Profile dimensions được freeze.
- [x] `ACTIVE / COLD_START / DEFERRED` semantics rõ.
- [x] cold start `50 / COLD_START / sampleCount 0` rõ.
- [x] `DEFERRED = null` rõ.
- [x] processed score scale `0..100` rõ.
- [x] EMA formula và configurable alpha rõ.
- [x] missing metric không update score/sampleCount.
- [x] `MetricAvailability = AVAILABLE | UNAVAILABLE` rõ.
- [x] valid zero vs missing rõ.
- [x] absence of event không tự đồng nghĩa zero.
- [x] `MatchProfileEligibility = ELIGIBLE | INELIGIBLE` rõ.
- [x] `MATCH_ABORTED → INELIGIBLE` rõ.
- [x] ineligible match không update persistent PlayerAIProfile/sampleCount.
- [x] ineligible match không persist TeamProfile như valid result.
- [x] ineligible match → TeamPerformance INCOMPLETE/null.
- [x] survival source/formula/contradiction behavior rõ.
- [x] aborted match không update survival profile.
- [x] noise dùng `ProfileNoisePenaltyCount` rõ.
- [x] `ProfileNoiseFilter` configurable/versioned rõ.
- [x] không mặc định mọi noiseType là penalty.
- [x] invalid noise config → null/no update rõ.
- [x] Player dimension source/status table rõ.
- [x] DEFERRED Player dimensions không bị invent formula.
- [x] normalization direction/threshold contract rõ.
- [x] sampleCount semantics rõ.
- [x] TeamProfile schema frozen.
- [x] TeamProfile match-scoped identity `teamKey=matchId` rõ.
- [x] cùng roster ở match khác nhau không merge TeamProfile.
- [x] không stale TeamProfile reuse.
- [x] TeamProfile ACTIVE/DEFERRED source mapping rõ.
- [x] `objectiveTime` elapsed wall-clock semantic rõ.
- [x] interruption/pause không bị subtract khỏi objectiveTime v1.0.
- [x] không invent revive denominator.
- [x] không invent communication quality từ help ping count.
- [x] không invent resource efficiency từ tool usage count.
- [x] TeamPerformance required components frozen.
- [x] TeamPerformance formula topology frozen.
- [x] TeamPerformance weights configurable và sum = 1.
- [x] required component missing → `INCOMPLETE + null`.
- [x] không missing=0.
- [x] không implicit weight renormalization.
- [x] Team Survival eligibility/availability rule rõ.
- [x] formula/config versioning và reproducibility gồm eligibility/availability/filter.
- [x] Telemetry boundary rõ.
- [x] không dùng RESERVED event.
- [x] AED / M1-015 boundary rõ.
- [x] Profile không trực tiếp điều khiển Monster AI.
- [x] Profile / Formula Contract Test Cases được định nghĩa.
- [x] Backend / Data / AI implementation không phải tự suy đoán behavior/formula semantics.

**Final Status: DONE / FROZEN**

---

# 27. Frozen Baseline Summary

```text
Architecture
Gameplay Runtime
→ TelemetryEvent
→ MatchTelemetry
→ MatchScore
→ Player / Team Profile
→ AED
→ Scenario Configuration
```

```text
M1-014
→ dừng tại MatchScore / PlayerAIProfile / TeamProfile / TeamPerformance
→ không định nghĩa AED policy
→ không trực tiếp điều khiển Monster AI
```

```text
MatchProfileEligibility
ELIGIBLE | INELIGIBLE

MATCH_ENDED.reasonCode = MATCH_ABORTED
→ INELIGIBLE
→ không update PlayerAIProfile
→ không tăng sampleCount
→ không persist TeamProfile như valid result
→ TeamPerformance = INCOMPLETE / null
```

```text
Telemetry validity
≠ Profile eligibility
```

```text
MetricAvailability
AVAILABLE | UNAVAILABLE

AVAILABLE + observed zero
→ valid zero

UNAVAILABLE
→ null
→ không synthetic 0
```

```text
PlayerAIProfile dimensions
survival
objective
teamwork
exploration
navigation
toolUsage
risk
noise
revive
```

```text
PlayerDimensionState
score: number | null
status: COLD_START | ACTIVE | DEFERRED
sampleCount: integer
```

```text
ACTIVE cold start
score = 50
status = COLD_START
sampleCount = 0
```

```text
DEFERRED
score = null
status = DEFERRED
```

```text
Current Player ACTIVE
survival
noise

Current Player DEFERRED
objective
teamwork
exploration
navigation
toolUsage
risk
revive
```

```text
survival MatchScore
eligible + PLAYER_ESCAPED → 100
eligible + PLAYER_ELIMINATED → 0
unavailable/contradictory → null
aborted match → không apply persistent profile
```

```text
ProfileNoisePenaltyCount
=
count(
  valid player-attributed NOISE_EMITTED
  matching ProfileNoiseFilter
)

ProfileNoiseFilter
→ configurable
→ versioned
→ M1-014 không hard-code mọi noiseType là bad
```

```text
noise MatchScore
direction = higher-is-worse
threshold = configurable
output = [0,100]

AVAILABLE + no matching event
→ penaltyCount = 0

UNAVAILABLE / invalid filter-config
→ noise MatchScore = null
→ không update profile
```

```text
Player profile EMA
newScore_d
=
(1 - alpha_d) * oldScore_d
+
alpha_d * matchScore_d

0 < alpha_d <= 1
alpha_d configurable
eligible match only
```

```text
missing MatchScore
→ không update score
→ không tăng sampleCount
→ null không thành 0
```

```text
TeamProfile v1.0
→ MATCH-SCOPED
→ teamKey = matchId
→ không persistent party identity
→ không cross-match merge
→ không stale field reuse
```

```text
TeamProfile fields
objectiveTime
splitTime
avgDistance
reviveSuccess
resourceEfficiency
communication
wipeRecovery
```

```text
Current Team ACTIVE
objectiveTime

Current Team DEFERRED
splitTime
avgDistance
reviveSuccess
resourceEfficiency
communication
wipeRecovery
```

```text
objectiveTime
=
sum elapsed(
  PHASE_STARTED.ts
  → matching PHASE_COMPLETED.ts
  cho objective-bearing phases
)

unit = seconds

SECURITY_HOLD_INTERRUPTED / pause ở giữa
→ vẫn nằm trong elapsed objectiveTime
→ v1.0 không subtract interruption duration
```

```text
TeamPerformance components
ObjectiveSpeed
Survival
Teamwork
ResourceEfficiency
```

```text
ObjectiveSpeed
← normalized current-match objectiveTime

Survival
=
100 * clamp(survivorCount / teamSize, 0, 1)
chỉ khi eligible + availability AVAILABLE + input valid
```

```text
Current Teamwork
= DEFERRED

Current ResourceEfficiency
= DEFERRED

→ current TeamPerformance
= INCOMPLETE / null
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

weights >= 0
sum(weights) = 1
weights configurable
```

```text
required component missing / unavailable / deferred / invalid
OR match ineligible
→ score = null
→ status = INCOMPLETE
→ không missing=0
→ không renormalize remaining weights
```

```text
Formula topology / semantic
= FROZEN

Numerical alpha
Normalization thresholds
ProfileNoiseFilter
TeamPerformance weights
= CONFIGURABLE / VERSIONED
```

```text
Reproducibility
same inputs
+ same MatchProfileEligibility
+ same MetricAvailability
+ same formula version
+ same normalization/config version
+ same filter/tuning
→ same output
```

```text
Profile
→ processed input cho AED
→ không command Stalker / Listener / Warden
→ không command FSM / Target Selection / Navigation / Attack
→ không map trực tiếp sang Scenario Configuration trong M1-014
```

Đây là frozen baseline chính thức cho **M1-014 — Player/Team Profile fields + formulas v0**, đủ để Backend / Data / AI triển khai MatchScore aggregation, persistent PlayerAIProfile, match-scoped TeamProfile và TeamPerformance contract mà không phải tự suy đoán eligibility, availability, formula, missing-data behavior, team identity hoặc AED boundary.

