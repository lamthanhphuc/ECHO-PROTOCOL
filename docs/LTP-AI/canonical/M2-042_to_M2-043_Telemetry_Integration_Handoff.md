# M2-042 -> M2-043 Telemetry Integration Handoff

**Revision:** Phase 6R alignment update - 2026-09-02
**Purpose:** Unity M2-042 -> Backend M2-043 integration handoff, including the explicit legacy v1.0 compatibility decision needed by the current v1.1 ingestion/storage path.

## 0. Source of Truth

Tài liệu canonical hiện tại:

- `docs/LTP-AI/canonical/Telemetry_Contract_v1.1.md`
- `docs/LTP-AI/canonical/Listener_AI_Design_v1.0.md`
- `docs/LTP-AI/canonical/M2_AI_Implementation_Plan_v1.0.md`

Trong đó:

> `Telemetry_Contract_v1.1.md` là source-of-truth chính cho Unity M2-042 và Backend M2-043.

Wire version hiện tại:

```text
schemaVersion = "1.1"
```

Backend phải phân biệt rõ schema version và không silently downgrade event `1.1` thành `1.0`.

### 0.1 Legacy v1.0 Compatibility Decision - Phase 6R

Frozen legacy serialized contract vẫn tồn tại với:

```text
schemaVersion = "1.0"
```

Tuy nhiên frozen v1.0 contract không có `eventSequence`, trong khi current v1.1 ingestion/storage contract dùng:

```text
(matchId, eventSequence)
```

làm identity/order boundary và cấm dùng backend arrival order làm gameplay order.

Vì vậy current M2 decision là:

```text
v1.0 schema
-> recognized as frozen legacy
-> NOT rewritten to v1.1
-> NOT assigned eventSequence from backend arrival order
-> live M2 ingestion/storage remains v1.1-only
-> v1.0 ingestion is explicitly deferred until a separate legacy storage/migration ordering contract is frozen
```

Backend current behavior cho event `schemaVersion = "1.0"`:

```text
PERMANENTLY_REJECTED
reason = TELEMETRY_LEGACY_V10_UNSUPPORTED
```

Đây là explicit compatibility boundary, không phải silent fallback và không được xem là permission để invent sequence/order semantics cho v1.0.

Nếu sau này cần ingest legacy v1.0, phải freeze một contract riêng, ví dụ legacy storage path không dùng v1.1 sequence identity hoặc một migration/order contract chính thức. Không tự suy diễn từ arrival order.

---

# 1. Common TelemetryEvent v1.1

Mỗi event có cấu trúc logic:

```text
id
matchId
userId
eventType
ts
valueJson
  context
  data
reasonCode
schemaVersion = "1.1"
```

Không có top-level generic `value`.

Mọi event `1.1` bắt buộc có trong `valueJson.context`:

```text
eventSequence
authorityTick
scenarioConfigVersion
policyVersion
configSource
```

`configSource`:

```text
FIXED
ADAPTIVE
```

`authorityTick` có thể `null` nếu event lifecycle xảy ra ngoài một Fusion simulation tick có ý nghĩa.

`ts` là UTC ISO-8601 occurrence time và không được thay đổi khi retry.

---

# 2. ACTIVE_PRODUCTION Event Catalog

```text
MATCH_STARTED
MATCH_ENDED
PHASE_STARTED
PHASE_COMPLETED

CORE_PICKED_UP
CORE_DROPPED
CORE_PLACED
PUZZLE_COMPLETED
SECURITY_HOLD_INTERRUPTED

PLAYER_DOWNED
PLAYER_REVIVED
PLAYER_ELIMINATED
PLAYER_ESCAPED

TEAM_TOOL_USED
HELP_PING_USED

NOISE_EMITTED
```

## 2.1 Không implement các tên sau như production event v1.1

```text
PLAYER_SPRINT_STARTED
PLAYER_SPRINT_ENDED
PLAYER_ISOLATION_ENDED
PLAYER_DETECTED
HIDING_SPOT_ENTERED
HIDING_SPOT_EXITED
PLAYER_REVIVED_TEAMMATE
```

Các tên trên chưa thuộc canonical v1.1.

---

# 3. RESEARCH_CAPTURE Event Catalog

Ngoài `ACTIVE_PRODUCTION`, schema `1.1` còn có các event `RESEARCH_CAPTURE`, chỉ hợp lệ khi:

```text
researchCaptureEnabled = true
```

Danh mục:

```text
MONSTER_INVESTIGATE_STARTED
MONSTER_INVESTIGATE_RESOLVED
MONSTER_ATTACK_RESOLVED
MONSTER_SEARCH_ENDED

WARDEN_TELEGRAPH_STARTED
WARDEN_ROUTE_ACTION_APPLIED
WARDEN_ROUTE_SAFETY_CHECKED
WARDEN_ROUTE_ACTION_RELEASED
```

Quy tắc:

```text
userId = null
```

Các event này:

- không phải normal production gameplay analytics;
- chỉ được emit/accept khi `researchCaptureEnabled = true`;
- phải validate đúng event-specific `context`, `data`, `reasonCode`, enum theo `Telemetry_Contract_v1.1.md`.

Các event `RESERVED_NOT_EMITTED` phải bị reject dưới schema `1.1`.

---

# 4. SRS Data Mapping

## 4.1 Sprint

```text
authoritative Sprint movement
-> RuntimeNoiseEvent
-> NOISE_EMITTED
```

Mapping:

```text
eventType = NOISE_EMITTED
data.noiseType = SPRINT
reasonCode = PLAYER_SPRINT
```

Đây là recurring movement noise, không phải một event duy nhất cho mỗi sprint session.

Không được mặc định:

```text
count(NOISE_EMITTED where noiseType=SPRINT)
= sprint session count
```

Sprint duration hiện chưa có frozen telemetry source.

Không thêm `PLAYER_SPRINT_STARTED` / `PLAYER_SPRINT_ENDED` nếu chưa có contract revision.

## 4.2 Noise

```text
eventType = NOISE_EMITTED
userId = acting player
```

Canonical reasonCode:

```text
PLAYER_SPRINT
OBJECT_INTERACTION
CORE_CARRY_MOVEMENT
CORE_DROP
NOISE_MAKER_USED
```

Required context:

```text
context.phase
context.position
```

Required data:

```text
data.noiseEventId
data.noiseType
data.loudness
```

Optional:

```text
data.hearingRadius
```

Canonical `noiseType`:

```text
SPRINT
INTERACTION
CORE_CARRY
CORE_DROP
NOISE_MAKER
```

## 4.3 Objective Carrying

```text
CORE_PICKED_UP
CORE_DROPPED
CORE_PLACED
```

```text
userId = acting player
data.coreId = Core identity
```

Nếu carry/drop tạo runtime noise thì đó là fact riêng:

```text
CORE_DROPPED
+
NOISE_EMITTED { noiseType = CORE_DROP }
```

Không merge hai fact thành một telemetry event.

## 4.4 Downed

```text
PLAYER_DOWNED
```

Source phải là authoritative Player Life-State transition, không phải Monster animation hoặc hit callback.

```text
userId = affected/downed player
```

Canonical reason hiện tại:

```text
STALKER_ATTACK
LISTENER_ATTACK
```

Không dùng `WARDEN_ATTACK` nếu canonical contract chưa cho phép.

## 4.5 Revive

```text
PLAYER_REVIVED
```

```text
userId = player được revive
data.reviverPlayerId = player thực hiện revive
```

`data.reviverPlayerId` phải là identity hợp lệ theo backend contract; không dùng arbitrary text.

Không dùng `PLAYER_REVIVED_TEAMMATE`.

## 4.6 Isolation

Current v1.1 chưa có event/sampling contract cho isolation/split time.

Không tự thêm:

```text
PLAYER_ISOLATION_ENDED
```

## 4.7 Detection

Current production schema không có:

```text
PLAYER_DETECTED
```

Các event:

```text
MONSTER_TARGET_ACQUIRED
MONSTER_TARGET_LOST
```

vẫn là:

```text
RESERVED_NOT_EMITTED
```

Không dùng Detection Meter hoặc Monster FSM state làm production player telemetry.

## 4.8 Hiding

Current canonical v1.1 chưa có hiding telemetry event.

Không tự thêm:

```text
HIDING_SPOT_ENTERED
HIDING_SPOT_EXITED
```

---

# 5. Ownership / Authority

Production pipeline:

```text
Client request/input
-> Host validates
-> authoritative gameplay fact commits
-> Telemetry Adapter
-> TelemetryEvent
-> Host buffer
-> backend
```

Proxy client không được independently emit authoritative gameplay telemetry.

## 5.1 userId semantics

```text
player action
-> acting player

player outcome
-> affected player

system/team/objective/monster episode
-> null
```

Ví dụ:

```text
CORE_PICKED_UP.userId = player nhặt Core
PLAYER_DOWNED.userId = player bị Down
PLAYER_REVIVED.userId = player được revive
PLAYER_REVIVED.data.reviverPlayerId = player thực hiện revive
MATCH_STARTED.userId = null
PHASE_STARTED.userId = null
```

---

# 6. IMPORTANT - Backend Authentication != Telemetry userId Ownership

M2-043 không được mặc định:

```text
event.userId == JWT.subject
```

cho Host telemetry batch.

Target transport:

```text
Host/service authenticated
POST /telemetry/batch
```

`event.userId` là semantic subject của gameplay fact, không phải identity của HTTP sender.

Nếu backend hiện tại có rule:

```text
userId != null
-> userId phải bằng user trong JWT
```

thì đây là integration mismatch cần xử lý trong M2-043.

Không được sửa bằng cách:

- đổi `userId` thành Host user;
- set toàn bộ `userId = null`;
- cho từng client tự emit authoritative outcome event.

Backend nên validate rằng authenticated Host/service có quyền submit telemetry cho authoritative match/roster tương ứng.

---

# 7. matchId

`matchId` phải là một authoritative identity chung cho toàn trận.

Host owns telemetry ordering:

```text
MATCH_STARTED.eventSequence = 1
```

Sau đó:

```text
2, 3, 4, ...
```

Toàn match dùng một `TelemetrySequenceAllocator` duy nhất.

Client không tự tạo telemetry `matchId` riêng.

Nếu backend M2-043 bắt buộc UUID thì binding cần freeze theo hướng:

```text
Host authoritative match creation
-> allocate one match UUID
-> synchronize/use same matchId throughout session
```

Không để mỗi client tự sinh `matchId` riêng cho cùng một trận.

---

# 8. Event Identity / Retry

Một logical event:

```text
id
matchId
eventSequence
ts
eventType
reasonCode
context
data
schemaVersion
```

Sau khi tạo thì immutable.

Retry phải giữ nguyên:

```text
id
eventSequence
ts
payload
```

Không tạo ID mới khi retry.

Backend expected behavior:

```text
same id + same semantic event
-> DUPLICATE_ALREADY_ACCEPTED

same id + different payload
-> IDENTITY_CONFLICT

same matchId/eventSequence + different id
-> SEQUENCE_CONFLICT
```

Transport:

```text
at-least-once
```

Storage phải idempotent.

Không claim exactly-once network delivery.

---

# 9. Batch Response / Retry

Endpoint:

```text
POST /telemetry/batch
```

Target contract:

```text
Host/service authenticated batch
```

Per-item result:

```text
ACCEPTED
DUPLICATE_ALREADY_ACCEPTED
PERMANENTLY_REJECTED
TRANSIENT_FAILURE
```

Sender-local state:

```text
NOT_ACKNOWLEDGED
```

Retry policy:

```text
ACCEPTED
-> remove

DUPLICATE_ALREADY_ACCEPTED
-> remove

PERMANENTLY_REJECTED
-> quarantine / do not retry

TRANSIENT_FAILURE
-> retry same immutable event

NOT_ACKNOWLEDGED
-> retry same immutable event
```

Match end:

```text
create MATCH_ENDED
-> enqueue
-> immediate best-effort flush
```

---

# 10. Batch / Local Log Tuning

Các value sau chưa frozen trong canonical contract:

```text
BufferCapacity
BatchSize
FlushInterval
RetryBackoff
MaxRetry
RetryAge
PayloadSizeLimit
```

Ví dụ `20-50 events`, `5-10 seconds`, `16 KB/event` nếu backend chọn dùng thì phải ghi là implementation configuration, không phải canonical telemetry rule.

`Application.persistentDataPath` cũng là implementation binding, không phải wire contract.

---

# 11. Units / Meaning

```text
ts
-> UTC ISO-8601 with Z

durationSeconds
-> seconds

authorityTick
-> Fusion authoritative tick hoặc null

eventSequence
-> positive monotonic integer within match

position
-> immutable Unity world-position snapshot tại occurrence

loudness
-> configured unitless source strength

hearingRadius
-> Runtime Noise broad-phase radius/distance
```

Không giả định `loudness` bắt buộc nằm trong `0-1` nếu canonical tuning chưa freeze.

Không log Player Transform mỗi frame.

---

# 12. Unity Gameplay Hooks - Current Source State

Exact callback names chưa thể bàn giao vì gameplay systems tương ứng chưa tồn tại đầy đủ.

```text
PlayerMovement
-> basic movement / CharacterController
-> chưa có authoritative Sprint callbacks

RuntimeNoiseEvent / NoiseSystem
-> chưa implement production

Player Life-State / Down / Revive
-> chưa có production implementation hoàn chỉnh

Core/objective gameplay runtime
-> chưa có implementation hoàn chỉnh để bind telemetry

Hiding system
-> chưa có

TelemetryEventFactory / Buffer / Sender / Adapters
-> M2-042 sẽ implement
```

Backend không nên hard-code Unity callback names như:

```text
PlayerMovement.OnSprintStarted
PlayerHealth.OnDowned
ReviveSystem.OnReviveCompleted
```

Backend chỉ phụ thuộc wire event contract.

---

# 13. Test Flow

M2-042 cần deterministic telemetry acceptance harness / PlayMode flow.

Tối thiểu:

```text
MATCH_STARTED
PHASE_STARTED
PHASE_COMPLETED
CORE_PICKED_UP
CORE_DROPPED
CORE_PLACED
NOISE_EMITTED
PLAYER_DOWNED
PLAYER_REVIVED
PLAYER_ELIMINATED
PLAYER_ESCAPED
MATCH_ENDED

retry same id
duplicate delivery
partial acknowledgement
backend unavailable
match-end flush
buffer overflow

2-4P Host authority
no proxy duplicate emission
```

Không yêu cầu production acceptance cho Sprint duration, Isolation, Hiding usage hoặc `PLAYER_DETECTED` trong v1.1 nếu chưa revision contract.

---

# 14. Stability

Các event `ACTIVE_PRODUCTION` trong canonical v1.1 là stable wire names.

Không rename giữa Unity và Backend.

Nếu cần thêm:

```text
Sprint session/count/duration
Isolation
Player detection
Hiding usage
```

thì phải revision Telemetry contract trước.

---

# 15. IMPORTANT - This Handoff Is a Summary, Not the Full Validator Registry

AI/backend developer phải đọc trực tiếp:

```text
Telemetry_Contract_v1.1.md
```

đặc biệt:

- event status/catalog;
- `userId` ownership;
- canonical enum registry;
- backend validation;
- per-event contracts/examples.

Cho mỗi emittable event phải implement đúng canonical:

```text
required context
optional context
required data
optional data
allowed reasonCode
allowed enum values
userId nullability / ownership
position rule
event status
```

Unknown/unregistered enum token không được silently map sang token khác.

---

# 16. v1.1 Strict Validator Rules

Cho `schemaVersion = "1.1"`:

```text
unknown schemaVersion
-> reject

unknown eventType
-> reject

RESERVED_NOT_EMITTED event
-> reject

RESEARCH_CAPTURE while researchCaptureEnabled != true
-> reject

unknown/unregistered reasonCode
-> reject

unknown strict enum value
-> reject

unknown common/event-specific field
-> reject unless contract explicitly allows an extension point
```

`MATCH_STARTED`:

```text
eventSequence = 1
```

Identity:

```text
(matchId, eventSequence)
```

phải map tới một logical event duy nhất.

```text
same (matchId,eventSequence) + different id
-> SEQUENCE_CONFLICT

same id + different semantic payload
-> IDENTITY_CONFLICT
```

Accepted `MATCH_ENDED` xác định terminal sequence boundary cho clean stream.

Sau terminal match occurrence:

```text
không tạo new TelemetryEvent cho match đó
```

Backend arrival order không được dùng làm gameplay order.

Gameplay order phải dựa trên:

```text
eventSequence
```

---

# 17. Backend M2-043 Responsibilities

M2-043 có thể triển khai độc lập:

```text
schemaVersion dispatcher
v1.0 legacy schema recognition + explicit deferred/unsupported ingestion boundary
v1.1 strict validator
event status validation
reasonCode / enum validation
idempotency by id
uniqueness by (matchId,eventSequence) for v1.1
identity conflict detection
sequence conflict detection
partial per-item batch acknowledgement
raw immutable v1.1 event storage
Host/service authorization binding
```

Current Phase 6R boundary:

```text
schemaVersion = "1.0"
-> TELEMETRY_LEGACY_V10_UNSUPPORTED
-> PERMANENTLY_REJECTED
```

M2-043 không được tạo synthetic `eventSequence` cho v1.0 và không được rewrite v1.0 record thành v1.1.

Backend không phụ thuộc tên callback C# trong Unity.

---

# 18. Unity M2-042 Responsibilities

M2-042 chịu trách nhiệm:

```text
authoritative gameplay fact -> telemetry mapping
TelemetryEvent creation
TelemetrySequenceAllocator
event identity creation
immutable retry payload
Host buffer
batch sender
local diagnostic/log path
deterministic acceptance harness
proxy duplicate suppression
```

---

# 19. Things M2-043 Must NOT Assume

Không tự giả định các event sau tồn tại:

```text
PLAYER_SPRINT_STARTED
PLAYER_SPRINT_ENDED
PLAYER_ISOLATION_ENDED
PLAYER_DETECTED
HIDING_SPOT_ENTERED
HIDING_SPOT_EXITED
PLAYER_REVIVED_TEAMMATE
```

Không tự activate:

```text
MONSTER_TARGET_ACQUIRED
MONSTER_TARGET_LOST
```

Không giả định:

```text
event.userId == JWT.subject
```

Không:

- downgrade `1.1 -> 1.0`;
- upgrade/rewrite stored `1.0 -> 1.1`;
- dùng backend arrival order làm gameplay order;
- tạo synthetic `eventSequence` cho legacy v1.0;
- sinh event ID mới khi retry;
- ép UUID representation cho `matchId` nếu cross-team binding chưa freeze;
- coi `RESEARCH_CAPTURE` là normal production analytics;
- accept `RESERVED_NOT_EMITTED`.

---

# 20. Integration Blockers / Decisions Still Requiring Alignment

## 20.1 matchId representation

Nếu backend bắt buộc UUID:

```text
Host allocate once
-> same matchId for entire match
-> shared authoritative identity
```

## 20.2 Host/service authentication

Backend cần authority model cho phép Host/service submit event có `userId` là player khác trong authoritative match.

Không dùng simplistic rule:

```text
event.userId == JWT.subject
```

cho Host telemetry batch.

## 20.3 Gameplay callback bindings

Unity exact callback names chưa frozen.

Backend không phụ thuộc callback names.

## 20.4 Tuning

```text
batch size
flush interval
buffer capacity
retry timing
payload limit
```

là implementation configuration cho đến khi contract freeze chúng.

## 20.5 Legacy v1.0 ingestion/storage compatibility

Frozen v1.0 remains a known historical wire schema, nhưng current handoff không có đủ semantics để map legacy records vào v1.1 order/storage model vì v1.0 không có `eventSequence`.

Decision hiện tại:

```text
legacy v1.0 contract remains frozen and recognized
live M2 emitter/ingestion path = v1.1
v1.0 -> explicit permanent rejection
no silent downgrade/upgrade
no arrival-order sequencing
no fabricated eventSequence
```

Status:

```text
DEFERRED - separate legacy storage/migration ordering contract required before v1.0 ingestion can be enabled safely
```

---

# 21. Final Contract Summary

```text
Unity authoritative gameplay
        |
        v
Telemetry Adapter
        |
        v
TelemetryEvent v1.1
        |
        v
Host sequence allocator
        |
        v
Host buffer / immutable local event
        |
        v
POST /telemetry/batch
        |
        v
Backend schema dispatcher
        |
        +-- schemaVersion 1.1 -> strict validator -> atomic/idempotent v1.1 storage
        |
        +-- schemaVersion 1.0 -> recognized legacy -> explicit PERMANENTLY_REJECTED
```

Core rules:

```text
one authoritative gameplay fact
-> one canonical telemetry fact

Host owns authoritative ordering

eventSequence defines gameplay order for v1.1

retry preserves event identity and payload

transport is at-least-once

storage must be idempotent

proxy clients do not independently emit authoritative gameplay telemetry

canonical event names are stable

legacy v1.0 is not rewritten as v1.1

backend arrival order is never gameplay order

new analytics or legacy-migration requirements require contract revision/freeze
```

---

# 22. Required Files for Backend AI / Member D

Bắt buộc:

```text
docs/LTP-AI/canonical/Telemetry_Contract_v1.1.md
```

Nên gửi thêm:

```text
docs/LTP-AI/canonical/Listener_AI_Design_v1.0.md
docs/LTP-AI/canonical/M2_AI_Implementation_Plan_v1.0.md
```

Legacy reference khi cần audit historical compatibility:

```text
docs/LTP-AI/archive/m1/Telemetry_Event_Schema_v0_FINAL.md
```

Thứ tự ưu tiên:

```text
Telemetry_Contract_v1.1.md
> canonical implementation plan
> supporting AI design docs
> frozen v1.0 archive when investigating legacy compatibility
> this handoff summary
```

Nếu summary này mâu thuẫn với `Telemetry_Contract_v1.1.md` thì canonical contract thắng.

---

# 23. Phase 6R Validation Evidence Snapshot

Current Phase 6R hardening evidence recorded during integration closure:

```text
Backend build                         PASS - 0 warnings / 0 errors
TelemetryService focused tests       PASS - 57/57
Backend test suite                    PASS - 69/69
Mongo replica-set integration tests  PASS - 5/5
Mongo concurrent terminal race       PASS
```

Lưu ý: integration tests dùng Mongo replica set thật cho `MongoTelemetryEventRepository.AtomicCommitBatchAsync`; legacy v1.0 compatibility vẫn ở trạng thái deferred như Section 0.1 và 20.5.
