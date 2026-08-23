# ECHO PROTOCOL — Telemetry Event Schema v0

**Task:** M1-008 — Định nghĩa Telemetry Event Schema v0  
**Owner:** C — AI / Telemetry / Research  
**Support:** D — Backend / Shop / Payment  
**Priority:** P0  
**Dependency:** M1-007  
**Status:** DONE / FROZEN  
**Schema Version:** `1.0`

---

## 1. Purpose

Tài liệu này định nghĩa **Telemetry Event Schema v0** cho ECHO PROTOCOL.

Mục tiêu của schema:

- chuẩn hóa event gameplay trước khi bước sang implementation;
- bảo đảm Unity/Host và Backend dùng cùng một contract;
- tạo dữ liệu đầu vào để tổng hợp `MatchTelemetry`, `MatchScore`, `Player/Team Profile`;
- hỗ trợ Adaptive Experience Director (AED) ở các milestone sau;
- hỗ trợ nghiên cứu và playtest Fixed vs Adaptive;
- bảo đảm event có thể kiểm tra, tái tính metric và debug.

Task M1-008 chỉ chốt **data contract**.

Trong M1 **chưa yêu cầu**:

- implement `/telemetry/batch`;
- implement database ingest;
- implement aggregation service;
- implement AED;
- gửi position mỗi frame;
- hoàn thiện toàn bộ metric M4/M5.

---

## 2. Source-of-Truth Alignment

Implementation Spec hiện chốt `TelemetryEvent` ở data model như sau:

```text
TelemetryEvent
- id
- matchId
- userId
- eventType
- ts
- valueJson
- reasonCode
- schemaVersion
```

TEL-01 cũng yêu cầu event phải có:

```text
match / player / time / type / value / context / reasonCode
```

### M1-008 Design Decision

Data model không có cột `context` riêng.

Vì vậy trong schema v0:

> `context` được lưu bên trong `valueJson.context`.

Không bổ sung cột database mới trong M1-008.

---

## 3. Telemetry Architecture

Luồng dữ liệu mục tiêu:

```text
Gameplay Runtime Event
        ↓
Telemetry Emitter
        ↓
TelemetryEvent
        ↓
Buffer / Batch
        ↓
Backend Validation
        ↓
TelemetryEvent Storage
        ↓
MatchTelemetry
        ↓
MatchScore
        ↓
Player / Team Profile
        ↓
AED
```

Telemetry chỉ **ghi nhận gameplay**.

Telemetry không được dùng để thay thế gameplay authority.

Ví dụ đối với Noise:

```text
Player Sprint
     ↓
Runtime NoiseEvent
     ↓
Hearing Sensor
     ↓
Listener AI
```

Song song:

```text
Runtime NoiseEvent
     ↓
Telemetry Emitter
     ↓
NOISE_EMITTED
```

> Listener không đọc database hoặc TelemetryEvent để nghe tiếng động.

---

# 4. Common TelemetryEvent Schema

## 4.1. Required Structure

```json
{
  "id": "evt_01J...",
  "matchId": "match_01J...",
  "userId": "user_01J...",
  "eventType": "PLAYER_DOWNED",
  "ts": "2026-08-20T09:15:32.125Z",
  "valueJson": {
    "context": {},
    "data": {}
  },
  "reasonCode": "STALKER_ATTACK",
  "schemaVersion": "1.0"
}
```

---

## 4.2. Common Fields

| Field | Type | Required | Description |
|---|---|---:|---|
| `id` | string | Yes | ID duy nhất của telemetry event |
| `matchId` | string | Yes | Match chứa event |
| `userId` | string / null | Conditional | Player liên quan trực tiếp đến event |
| `eventType` | enum/string | Yes | Loại event |
| `ts` | UTC timestamp | Yes | Thời điểm event xảy ra |
| `valueJson` | JSON object | Yes | Payload riêng của event |
| `reasonCode` | enum/string / null | Conditional | Nguyên nhân hoặc lý do event xảy ra |
| `schemaVersion` | string | Yes | Version của event schema |

---

## 4.3. `userId` Rule

`userId` biểu diễn **Player trực tiếp sở hữu hoặc là subject của event**. Unity/Host không được tự chọn `userId` theo convenience của implementation.

`userId` bắt buộc đối với các Player-specific event active trong schemaVersion `1.0`:

```text
PLAYER_DOWNED
PLAYER_REVIVED
PLAYER_ELIMINATED
PLAYER_ESCAPED
CORE_PICKED_UP
CORE_DROPPED
CORE_PLACED
TEAM_TOOL_USED
HELP_PING_USED
NOISE_EMITTED khi runtime NoiseEvent source là Player
```

`userId` phải là `null` đối với các system/objective/phase-level event active sau:

```text
MATCH_STARTED
MATCH_ENDED
PHASE_STARTED
PHASE_COMPLETED
PUZZLE_COMPLETED
SECURITY_HOLD_INTERRUPTED
```

Đối với `NOISE_EMITTED`:

```text
runtime NoiseEvent source resolve tới một Player
→ userId = Player đó

runtime NoiseEvent source là Environment/System và không resolve tới một Player
→ userId = null
```

Đối với `PUZZLE_FAILED`, event đang ở trạng thái **RESERVED / CONDITIONAL** và **NOT EMITTED IN CURRENT v1.0 BASELINE**. Khi event được activate bởi gameplay/spec đã freeze:

```text
failure được quy cho một Player cụ thể
→ userId bắt buộc

failure là system/team-level theo contract đã freeze
→ userId = null
```

Gameplay/spec kích hoạt `PUZZLE_FAILED` phải chốt ownership cho từng failure condition/reasonCode; implementation không được tự quyết định player-level hay system/team-level theo từng lần emit.

Đối với `PLAYER_RESCUED`, event đang ở trạng thái **RESERVED / NOT EMITTED IN schemaVersion 1.0**. Nếu event này được activate ở version sau, `userId` bắt buộc và là Player được rescue; ID của Player thực hiện rescue, nếu contract cần, nằm trong `valueJson.data.rescuerPlayerId`.

Các Monster / AI Debug Events tại Section 8.7 không thuộc required P0 emitter baseline. Nếu một milestone implementation cần activate chúng, event-specific payload contract phải chốt `userId` ownership trước khi emit; dev không được tự suy đoán từ tên event.

Nếu cần xác định nhiều Player cho một event active, ID bổ sung nằm trong `valueJson.data` theo payload contract của event đó.

---

# 5. `valueJson` Convention

Schema v0 dùng cấu trúc:

```json
{
  "context": {
    "phase": "EXPLORE",
    "position": {
      "x": 0.0,
      "y": 0.0,
      "z": 0.0
    }
  },
  "data": {}
}
```

## 5.1. `context`

Chứa dữ liệu mô tả bối cảnh chung tại thời điểm event.

Các field có thể có:

| Field | Required | Meaning |
|---|---:|---|
| `phase` | Conditional | Phase gameplay hiện tại |
| `position` | Conditional | Vị trí có ý nghĩa tại thời điểm event |
| `monsterType` | Conditional | Monster liên quan |
| `teamSize` | Conditional | Quy mô lobby nếu cần |
| `scenarioConfigVersion` | Conditional | Version config của toàn match; bắt buộc trong `MATCH_STARTED` và luôn nằm ở `valueJson.context` |

Không bắt buộc mọi event phải có toàn bộ context.

---

## 5.2. `data`

Chứa payload riêng theo từng `eventType`.

Ví dụ:

```json
{
  "context": {
    "phase": "CORE_COLLECTION"
  },
  "data": {
    "coreId": "core_02"
  }
}
```

---

# 6. Naming Convention

## 6.1. Event Type

Sử dụng:

```text
UPPER_SNAKE_CASE
```

Ví dụ:

```text
MATCH_STARTED
PLAYER_DOWNED
CORE_DROPPED
NOISE_EMITTED
```

Không dùng:

```text
PlayerDown
player-downed
Player Downed
```

---

## 6.2. reasonCode

`reasonCode` cũng sử dụng:

```text
UPPER_SNAKE_CASE
```

Ví dụ:

```text
STALKER_ATTACK
TEAMMATE_REVIVE
PLAYER_SPRINT
CORE_DROP
NOISE_MAKER_USED
```

`reasonCode` phải là code ổn định, không phải câu mô tả tự do.

Không dùng:

```text
"Người chơi bị Stalker đánh nên bị down"
```

---

# 7. Schema Versioning

> **Naming note:** `v0` là tên của milestone/design iteration dùng để chốt Telemetry Event Schema trong M1-008; `schemaVersion = "1.0"` là version serialized đầu tiên của data contract được freeze để Unity/Host và Backend trao đổi dữ liệu. Hai khái niệm này không mâu thuẫn nhau.

Schema v0 sử dụng:

```text
schemaVersion = "1.0"
```

Mọi event phải gửi version.

Ví dụ:

```json
{
  "eventType": "PLAYER_DOWNED",
  "schemaVersion": "1.0"
}
```

Backend phải có khả năng xác định schema của event dựa trên `schemaVersion`.

Nguyên tắc versioning:

```text
1.0
→ baseline đầu tiên

1.x
→ thay đổi tương thích ngược

2.0
→ thay đổi breaking contract
```

Trong M1 chỉ freeze:

```text
1.0
```

---

# 8. Event Catalog v0

Schema v0 chia event thành các nhóm sau.

## 8.1. Match Events

| Event Type | Purpose |
|---|---|
| `MATCH_STARTED` | Ghi nhận bắt đầu match |
| `MATCH_ENDED` | Ghi nhận kết thúc match |

---

## 8.2. Phase Events

| Event Type | Purpose |
|---|---|
| `PHASE_STARTED` | Bắt đầu một gameplay phase |
| `PHASE_COMPLETED` | Hoàn thành một gameplay phase |

Dùng để tính:

- match duration;
- phase duration;
- objective time đối với objective được biểu diễn bằng gameplay phase.

### Phase Lifecycle Emission Rule

`PHASE_STARTED` / `PHASE_COMPLETED` là **source-of-truth cho lifecycle của gameplay phase**.

Specialized event chỉ tồn tại ở trạng thái active nếu event đó mang semantic hoặc payload riêng mà `PHASE_STARTED` / `PHASE_COMPLETED` không biểu diễn được.

Không được double-count cùng một lifecycle transition chỉ vì catalog có cả generic phase event và specialized event.

Baseline schemaVersion `1.0`:

```text
Security Hold bắt đầu
→ PHASE_STARTED { phase = SECURITY_HOLD }

Security Hold interaction bị ngắt
→ SECURITY_HOLD_INTERRUPTED

Security Hold hoàn thành
→ PHASE_COMPLETED { phase = SECURITY_HOLD }

Final Hunt bắt đầu
→ PHASE_STARTED { phase = FINAL_HUNT }
```

Vì `SECURITY_HOLD_STARTED`, `SECURITY_HOLD_COMPLETED` và `FINAL_HUNT_STARTED` không có semantic riêng ngoài phase transition trong baseline hiện tại, các tên event này được giữ ở trạng thái **RESERVED / NOT EMITTED IN schemaVersion 1.0**.

---

## 8.3. Objective Events

| Event Type | Status in schemaVersion 1.0 | Purpose |
|---|---|---|
| `CORE_PICKED_UP` | ACTIVE | Player nhặt Energy Core |
| `CORE_DROPPED` | ACTIVE | Player làm rơi Energy Core |
| `CORE_PLACED` | ACTIVE | Energy Core được đặt đúng objective |
| `PUZZLE_COMPLETED` | ACTIVE | Power Puzzle hoàn thành |
| `PUZZLE_FAILED` | **RESERVED / CONDITIONAL — NOT EMITTED IN CURRENT v1.0 BASELINE** | Chỉ dành cho failure có gameplay consequence đã được spec freeze |
| `SECURITY_HOLD_STARTED` | **RESERVED / NOT EMITTED** | Tên reserved; phase start dùng `PHASE_STARTED { phase = SECURITY_HOLD }` |
| `SECURITY_HOLD_INTERRUPTED` | ACTIVE | Interaction bị ngắt, progress pause; semantic riêng ngoài phase lifecycle |
| `SECURITY_HOLD_COMPLETED` | **RESERVED / NOT EMITTED** | Tên reserved; phase completion dùng `PHASE_COMPLETED { phase = SECURITY_HOLD }` |
| `FINAL_HUNT_STARTED` | **RESERVED / NOT EMITTED** | Tên reserved; phase start dùng `PHASE_STARTED { phase = FINAL_HUNT }` |

Event có status **RESERVED / NOT EMITTED** hoặc **RESERVED / CONDITIONAL — NOT EMITTED IN CURRENT v1.0 BASELINE** không được Unity/Host emit trong frozen baseline `1.0`. Backend validator của baseline `1.0` phải reject các event này nếu nhận được. Việc activate một reserved event phải đi qua gameplay/spec freeze và schema versioning tương ứng.

---

## 8.4. Survival Events

| Event Type | Purpose |
|---|---|
| `PLAYER_DOWNED` | Player chuyển sang Downed |
| `PLAYER_REVIVED` | Player đang Downed được teammate Revive thành công |
| `PLAYER_ELIMINATED` | Player bị Eliminated |
| `PLAYER_ESCAPED` | Player đạt điều kiện thoát khỏi match |

---

## 8.5. Co-op / Tool Events

| Event Type | Status in schemaVersion 1.0 | Purpose |
|---|---|---|
| `TEAM_TOOL_USED` | ACTIVE | Player sử dụng Team Tool |
| `PLAYER_RESCUED` | **RESERVED / NOT EMITTED** | Chỉ dành cho rescue outcome riêng biệt, không đồng nghĩa với Revive hoặc Escape |
| `HELP_PING_USED` | ACTIVE | Player Downed dùng Need Help ping |

`PLAYER_RESCUED` không được dùng để duplicate `PLAYER_REVIVED` hoặc `PLAYER_ESCAPED`. Baseline hiện tại chưa freeze một rescue mechanic/outcome riêng, vì vậy event này không được emit trong schemaVersion `1.0`.

---

## 8.6. Noise Events

| Event Type | Purpose |
|---|---|
| `NOISE_EMITTED` | Ghi nguồn noise có ý nghĩa cho telemetry |

Noise source baseline theo gameplay:

```text
SPRINT
INTERACTION
CORE_CARRY
CORE_DROP
NOISE_MAKER
```

---

## 8.7. Monster / AI Debug Events

Các tên event sau được giữ cho metric/debug khi implementation milestone tương ứng cần. Chúng **không thuộc required P0 emitter baseline của M1-008**; trước khi emit phải có event-specific payload/`userId` contract được freeze, không tự suy đoán từ catalog.

| Event Type | Purpose |
|---|---|
| `MONSTER_TARGET_ACQUIRED` | Monster acquire target hợp lệ |
| `MONSTER_TARGET_LOST` | Monster mất target |
| `MONSTER_INVESTIGATE_STARTED` | Monster bắt đầu investigate |
| `MONSTER_ATTACK_RESOLVED` | Attack hit/miss |
| `MONSTER_SEARCH_ENDED` | Search kết thúc |

Các event này **không thay đổi gameplay**; chỉ ghi lại kết quả từ Traditional AI.

---

# 9. Event Payload Definitions

## 9.1. `MATCH_STARTED`

```json
{
  "eventType": "MATCH_STARTED",
  "userId": null,
  "valueJson": {
    "context": {
      "teamSize": 4,
      "scenarioConfigVersion": "1.0"
    },
    "data": {
      "mapId": "RESEARCH_FACILITY"
    }
  },
  "reasonCode": "MATCH_READY",
  "schemaVersion": "1.0"
}
```

Required payload:

```text
data.mapId
context.teamSize
context.scenarioConfigVersion
```

`scenarioConfigVersion` là context của toàn match. Trong schemaVersion `1.0`, field này không được đặt dưới `valueJson.data`.

---

## 9.2. `MATCH_ENDED`

```json
{
  "eventType": "MATCH_ENDED",
  "userId": null,
  "valueJson": {
    "context": {
      "phase": "MATCH_END"
    },
    "data": {
      "outcome": "SUCCESS",
      "durationSeconds": 1032.4,
      "survivorCount": 3
    }
  },
  "reasonCode": "TEAM_ESCAPED",
  "schemaVersion": "1.0"
}
```

Required payload:

```text
data.outcome
data.durationSeconds
data.survivorCount
```

Example `reasonCode`:

```text
TEAM_ESCAPED
TEAM_ELIMINATED
MATCH_ABORTED
```

---

## 9.3. `PHASE_STARTED`

```json
{
  "eventType": "PHASE_STARTED",
  "userId": null,
  "valueJson": {
    "context": {
      "phase": "SECURITY_HOLD"
    },
    "data": {}
  },
  "reasonCode": "PREVIOUS_PHASE_COMPLETED",
  "schemaVersion": "1.0"
}
```

Required payload:

```text
context.phase
```

`PHASE_STARTED` là source-of-truth cho phase start. Không emit thêm `SECURITY_HOLD_STARTED` hoặc `FINAL_HUNT_STARTED` cho cùng transition trong baseline `1.0`.

---

## 9.4. `PHASE_COMPLETED`

```json
{
  "eventType": "PHASE_COMPLETED",
  "userId": null,
  "valueJson": {
    "context": {
      "phase": "SECURITY_HOLD"
    },
    "data": {
      "durationSeconds": 24.8
    }
  },
  "reasonCode": "OBJECTIVE_COMPLETED",
  "schemaVersion": "1.0"
}
```

Required payload:

```text
context.phase
```

`data.durationSeconds` có thể được gửi như giá trị tiện dụng/debug, nhưng Phase Duration source-of-truth được tính từ `PHASE_STARTED.ts` và `PHASE_COMPLETED.ts`. Không emit thêm `SECURITY_HOLD_COMPLETED` cho cùng transition trong baseline `1.0`.

---

## 9.5. `SECURITY_HOLD_INTERRUPTED`

`SECURITY_HOLD_INTERRUPTED` được giữ active vì nó biểu diễn interruption/progress pause riêng, không phải phase start hoặc phase completion.

```json
{
  "eventType": "SECURITY_HOLD_INTERRUPTED",
  "userId": null,
  "valueJson": {
    "context": {
      "phase": "SECURITY_HOLD"
    },
    "data": {}
  },
  "reasonCode": null,
  "schemaVersion": "1.0"
}
```

Required payload:

```text
context.phase
```

Trong baseline `1.0`, event này là objective/system-level nên `userId = null`. Không tự thêm progress amount, timer penalty, damage, aggro hoặc consequence khác nếu gameplay/spec chưa chốt.

---

## 9.6. `CORE_PICKED_UP`

```json
{
  "eventType": "CORE_PICKED_UP",
  "userId": "player_02",
  "valueJson": {
    "context": {
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
  "schemaVersion": "1.0"
}
```

Required payload:

```text
data.coreId
```

---

## 9.7. `CORE_DROPPED`

```json
{
  "eventType": "CORE_DROPPED",
  "userId": "player_02",
  "valueJson": {
    "context": {
      "phase": "CORE_COLLECTION",
      "position": {
        "x": 15.1,
        "y": 0.0,
        "z": 10.7
      }
    },
    "data": {
      "coreId": "core_01"
    }
  },
  "reasonCode": "PLAYER_DROP",
  "schemaVersion": "1.0"
}
```

Required payload:

```text
data.coreId
```

Nếu Core Drop đồng thời phát runtime noise:

```text
CORE_DROPPED
+
NOISE_EMITTED
```

Hai event có trách nhiệm khác nhau:

```text
CORE_DROPPED
→ objective/resource telemetry

NOISE_EMITTED
→ noise telemetry
```

---

## 9.8. `PLAYER_DOWNED`

```json
{
  "eventType": "PLAYER_DOWNED",
  "userId": "player_03",
  "valueJson": {
    "context": {
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
  "schemaVersion": "1.0"
}
```

Required payload:

```text
Không có event-specific data field bắt buộc ngoài common fields và userId.
```

`context.phase`, `context.monsterType`, `context.position` và `data.downCount` là context/snapshot conditional theo nguồn gameplay. Down Count metric không sum `data.downCount`.

Possible `reasonCode`:

```text
STALKER_ATTACK
LISTENER_ATTACK
WARDEN_ATTACK
```

Nếu damage source khác được GDD/spec bổ sung sau này, thêm reason code bằng version-compatible extension.

---

## 9.9. `PLAYER_REVIVED`

```json
{
  "eventType": "PLAYER_REVIVED",
  "userId": "player_03",
  "valueJson": {
    "context": {
      "phase": "FINAL_HUNT"
    },
    "data": {
      "reviverPlayerId": "player_01",
      "reviveCount": 2,
      "usedFirstAidKit": false
    }
  },
  "reasonCode": "TEAMMATE_REVIVE",
  "schemaVersion": "1.0"
}
```

Required payload:

```text
data.reviverPlayerId
```

`data.reviveCount` và `data.usedFirstAidKit` là snapshot/context của revive outcome; Revive Count metric được tính từ số `PLAYER_REVIVED` event hợp lệ.

---

## 9.10. `PLAYER_ELIMINATED`

```json
{
  "eventType": "PLAYER_ELIMINATED",
  "userId": "player_03",
  "valueJson": {
    "context": {
      "phase": "FINAL_HUNT"
    },
    "data": {
      "reviveCount": 2
    }
  },
  "reasonCode": "REVIVE_LIMIT_REACHED",
  "schemaVersion": "1.0"
}
```

Required payload:

```text
Không có event-specific data field bắt buộc ngoài common fields và userId.
```

`data.reviveCount` là snapshot/context và không phải source để tính Revive Count.

`reasonCode` khác chỉ được thêm nếu gameplay/spec chính thức có nguyên nhân Eliminated khác.

---

## 9.11. `PLAYER_ESCAPED`

`PLAYER_ESCAPED` chỉ biểu diễn Player đạt điều kiện thoát khỏi match. Event này không đồng nghĩa với `PLAYER_RESCUED`.

```json
{
  "eventType": "PLAYER_ESCAPED",
  "userId": "player_01",
  "valueJson": {
    "context": {
      "phase": "FINAL_HUNT"
    },
    "data": {
      "rescuedTeammate": true
    }
  },
  "reasonCode": "EXIT_REACHED",
  "schemaVersion": "1.0"
}
```

Required payload:

```text
Không có event-specific data field bắt buộc ngoài common fields và userId.
```

`data.rescuedTeammate` là field conditional của escape payload hiện tại; field này không tự tạo một `PLAYER_RESCUED` event và không được dùng làm Rescue Count cho đến khi rescue definition được freeze.

---

## 9.12. `TEAM_TOOL_USED`

```json
{
  "eventType": "TEAM_TOOL_USED",
  "userId": "player_01",
  "valueJson": {
    "context": {
      "phase": "CORE_COLLECTION"
    },
    "data": {
      "toolType": "NOISE_MAKER",
      "targetId": null
    }
  },
  "reasonCode": "PLAYER_ACTIVATED_TOOL",
  "schemaVersion": "1.0"
}
```

Required payload:

```text
data.toolType
```

`data.targetId` là conditional và có thể `null` khi tool use không có target ID theo gameplay contract.

Possible `toolType`:

```text
FIELD_SCANNER
NOISE_MAKER
FIRST_AID_KIT
DOOR_JAMMER
```

---

## 9.13. `CORE_PLACED`

`CORE_PLACED` được emit khi một Energy Core được đặt đúng objective theo gameplay flow đã chốt.

```json
{
  "eventType": "CORE_PLACED",
  "userId": "player_02",
  "valueJson": {
    "context": {
      "phase": "CORE_COLLECTION",
      "position": {
        "x": 18.4,
        "y": 0.0,
        "z": 6.2
      }
    },
    "data": {
      "coreId": "core_01"
    }
  },
  "reasonCode": "CORE_OBJECTIVE_PLACED",
  "schemaVersion": "1.0"
}
```

Required payload:

```text
data.coreId
```

`context.position` chỉ cần gửi khi vị trí đặt Core có ý nghĩa cho phân tích/debug; không biến event này thành position sampling.

---

## 9.14. `PUZZLE_COMPLETED`

`PUZZLE_COMPLETED` ghi nhận objective Power Puzzle hoàn thành. Đây là objective semantic, không thay thế phase lifecycle event.

```json
{
  "eventType": "PUZZLE_COMPLETED",
  "userId": null,
  "valueJson": {
    "context": {
      "phase": "POWER_PUZZLE"
    },
    "data": {}
  },
  "reasonCode": null,
  "schemaVersion": "1.0"
}
```

Required payload:

```text
context.phase
```

Nếu việc hoàn thành Power Puzzle đồng thời hoàn thành một gameplay phase, emit `PUZZLE_COMPLETED` cho objective semantic và `PHASE_COMPLETED` cho phase lifecycle. Không dùng `PUZZLE_COMPLETED` thay thế `PHASE_COMPLETED` khi tính Phase Duration.

---

## 9.15. `PUZZLE_FAILED`

**Status in schemaVersion 1.0: RESERVED / CONDITIONAL**  
**NOT EMITTED IN CURRENT v1.0 BASELINE**

`PUZZLE_FAILED` không được emit chỉ vì Player nhập sai. Event này chỉ được activate khi Gameplay/GDD/Implementation Spec freeze một failure consequence cụ thể có ý nghĩa telemetry.

```text
wrong input nhưng gameplay không có consequence
→ không emit PUZZLE_FAILED
```

Không tự tạo damage, monster aggro, timer penalty, reset mechanic, noise consequence hoặc mechanic khác chỉ để justify telemetry.

Payload dưới đây được giữ làm **contract dự kiến khi event được activate**, không phải event active của current v1.0 baseline:

```json
{
  "eventType": "PUZZLE_FAILED",
  "userId": "player_02",
  "valueJson": {
    "context": {
      "phase": "POWER_PUZZLE"
    },
    "data": {}
  },
  "reasonCode": "INVALID_PUZZLE_INPUT",
  "schemaVersion": "1.0"
}
```

Required payload khi được activate:

```text
context.phase
```

`userId` khi activate tuân theo rule tại Section 4.3: failure player-specific thì bắt buộc; failure system/team-level thì `null`, và ownership phải được gameplay/spec chốt theo failure condition/reasonCode trước implementation.

Không log nội dung puzzle tự do vào `reasonCode`. `INVALID_PUZZLE_INPUT` chỉ hợp lệ nếu future frozen gameplay contract xác định input đó thực sự tạo consequence đủ điều kiện telemetry. Nếu implementation sau này cần payload chi tiết hơn cho failure cụ thể, phần mở rộng phải bám gameplay/spec đã được chốt và tuân theo schema versioning.

---

## 9.16. `PLAYER_RESCUED`

**Status in schemaVersion 1.0: RESERVED / NOT EMITTED**

Semantic boundary:

```text
PLAYER_REVIVED
= Player đang Downed được teammate revive thành công.

PLAYER_ESCAPED
= Player đạt điều kiện thoát khỏi match.

PLAYER_RESCUED
= chỉ emit nếu gameplay specification có một rescue outcome riêng biệt,
  không đồng nghĩa với Revive hoặc Escape.
```

Baseline hiện tại chưa freeze một rescue mechanic/outcome riêng, vì vậy `PLAYER_RESCUED` không được emit trong schemaVersion `1.0`.

Không sử dụng `PLAYER_RESCUED` để duplicate một `PLAYER_REVIVED`.

Không sử dụng `PLAYER_RESCUED` để duplicate một `PLAYER_ESCAPED`.

Payload dưới đây được giữ làm **contract dự kiến nếu event được activate ở version sau**. `userId` là Player được rescue; nếu contract cần xác định người thực hiện rescue thì dùng `data.rescuerPlayerId`.

```json
{
  "eventType": "PLAYER_RESCUED",
  "userId": "player_03",
  "valueJson": {
    "context": {
      "phase": "FINAL_HUNT"
    },
    "data": {
      "rescuerPlayerId": "player_01"
    }
  },
  "reasonCode": "TEAMMATE_RESCUE",
  "schemaVersion": "1.0"
}
```

Required payload khi được activate:

```text
data.rescuerPlayerId
```

Việc giữ payload dự kiến không làm event trở thành active trong current v1.0 baseline.

---

## 9.17. `HELP_PING_USED`

`HELP_PING_USED` được emit khi Player đang Downed sử dụng Need Help ping theo gameplay baseline.

```json
{
  "eventType": "HELP_PING_USED",
  "userId": "player_03",
  "valueJson": {
    "context": {
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
  "schemaVersion": "1.0"
}
```

Required payload:

```text
context.phase
```

`context.position` là conditional: chỉ gửi khi cần phân tích vị trí yêu cầu hỗ trợ. Event này không cho phép Spectator/Soul cung cấp thông tin mà Player còn sống không thể biết.

---

# 10. Noise Telemetry Contract

## 10.1. `NOISE_EMITTED`

```json
{
  "eventType": "NOISE_EMITTED",
  "userId": "player_02",
  "valueJson": {
    "context": {
      "phase": "CORE_COLLECTION",
      "position": {
        "x": 12.5,
        "y": 0.0,
        "z": 8.3
      }
    },
    "data": {
      "noiseType": "SPRINT",
      "loudness": 0.7,
      "hearingRadius": 12.0
    }
  },
  "reasonCode": "PLAYER_SPRINT",
  "schemaVersion": "1.0"
}
```

### Required payload

```text
context.phase
context.position
data.noiseType
data.loudness
```

`hearingRadius` có thể được emit nếu runtime noise contract sử dụng radius rõ ràng.

---

## 10.2. Noise Type v0

```text
SPRINT
INTERACTION
CORE_CARRY
CORE_DROP
NOISE_MAKER
```

---

## 10.3. Noise reasonCode v0

| noiseType | reasonCode |
|---|---|
| `SPRINT` | `PLAYER_SPRINT` |
| `INTERACTION` | `OBJECT_INTERACTION` |
| `CORE_CARRY` | `CORE_CARRY_MOVEMENT` |
| `CORE_DROP` | `CORE_DROP` |
| `NOISE_MAKER` | `NOISE_MAKER_USED` |

---

# 11. Runtime NoiseEvent vs TelemetryEvent

Hai contract phải được tách riêng.

## Runtime `NoiseEvent`

Mục tiêu:

> Cho monster hearing system phản ứng real-time.

Ví dụ:

```text
position
noiseType
loudness
hearingRadius
source
timestamp
```

Luồng:

```text
Player / Environment
        ↓
Runtime NoiseEvent
        ↓
Noise System
        ↓
Hearing Sensor
        ↓
Listener AI
```

## Telemetry `NOISE_EMITTED`

Mục tiêu:

> Lưu dữ liệu để phân tích, aggregate metric, Player/Team Profile và research.

Luồng:

```text
Runtime NoiseEvent
        ↓
Telemetry Emitter
        ↓
NOISE_EMITTED
        ↓
Backend / Storage
```

Không dùng:

```text
Telemetry DB
    ↓
Listener Hearing
```

---

# 12. Telemetry Metrics Mapping

Planning baseline yêu cầu telemetry hỗ trợ các nhóm metric:

- phase;
- co-op / survival;
- navigation;
- risk;
- resource;
- noise.

Metric mapping chỉ dùng event active trong current v1.0 baseline. Event có status `RESERVED / NOT EMITTED` hoặc `RESERVED / CONDITIONAL` không được dùng làm source cho metric hiện tại.

## 12.1. Phase Metrics

| Metric | Source Event |
|---|---|
| Match Duration | `MATCH_STARTED` + `MATCH_ENDED` |
| Phase Duration | `PHASE_STARTED` + `PHASE_COMPLETED` |
| Objective Time | `PHASE_STARTED` + `PHASE_COMPLETED` đối với objective được biểu diễn bằng gameplay phase |

`PHASE_STARTED` / `PHASE_COMPLETED` là source-of-truth cho phase lifecycle. Không cộng thêm reserved specialized start/completed events vào cùng metric.

---

## 12.2. Co-op / Survival Metrics

| Metric | Source Event |
|---|---|
| Down Count | `PLAYER_DOWNED` |
| Revive Count | `PLAYER_REVIVED` |
| Eliminated Count | `PLAYER_ELIMINATED` |
| Rescue Count | **Deferred until rescue definition is frozen** |
| Tool Assist | **Deferred until assist outcome definition is frozen**; `TEAM_TOOL_USED` chỉ là source cho tool usage |
| Escape / Survival | `PLAYER_ESCAPED` cho player-level escape; `MATCH_ENDED` cho match-level outcome/survivor count |

Down Count được tính từ số `PLAYER_DOWNED` event hợp lệ; không sum `data.downCount`.

Revive Count được tính từ số `PLAYER_REVIVED` event hợp lệ; không sum `data.reviveCount` từ payload snapshot/debug.

`PLAYER_RESCUED` không phải metric source trong current v1.0 baseline vì event không được emit. `PLAYER_ESCAPED.data.rescuedTeammate` cũng không được tự suy diễn thành Rescue Count khi rescue definition chưa được freeze.

---

## 12.3. Noise Metrics

| Metric | Source Event |
|---|---|
| Noise Count | `NOISE_EMITTED` |
| Noise by Type | `NOISE_EMITTED.data.noiseType` |
| Noise by Phase | `NOISE_EMITTED.context.phase` |
| High-noise Action Count | `NOISE_EMITTED` filtered by configured rule |

---

## 12.4. Navigation / Risk / Resource Metrics

Planning baseline có các metric như:

```text
average distance
split time
backtrack
wrong-route
item use/waste
solo time
noise
risk/rescue
```

Các metric này thuộc telemetry pipeline nhưng **không cần freeze toàn bộ event chi tiết trong M1-008** nếu gameplay contract tương ứng chưa được chốt.

Trong current v1.0 baseline, metric chưa có source gameplay/event và công thức được freeze phải được xem là deferred; không tự map sang reserved event hoặc invent event mới để có dữ liệu.

Quy tắc mở rộng:

> Chỉ thêm event mới khi có gameplay source rõ ràng và metric có công thức xác định.

Không tự suy diễn thêm mechanic chỉ để có telemetry.

---

# 13. Position Logging Rule

Không gửi Player transform mỗi frame.

Không dùng:

```text
PLAYER_POSITION
PLAYER_POSITION
PLAYER_POSITION
PLAYER_POSITION
...
```

Telemetry không phải network replication.

Position chỉ đi kèm event khi có ý nghĩa phân tích.

Ví dụ:

```text
CORE_DROPPED.position
PLAYER_DOWNED.position
NOISE_EMITTED.position
MONSTER_TARGET_LOST.position
```

Nếu M4 cần tính `averageDistance`, `splitTime`, `soloTime` bằng sampling thì phải định nghĩa sampling policy riêng trước khi implementation.

---

# 14. reasonCode Rules

## 14.1. Purpose

`reasonCode` giải thích **vì sao** event xảy ra.

Ví dụ:

```text
PLAYER_DOWNED
reasonCode = STALKER_ATTACK
```

```text
NOISE_EMITTED
reasonCode = CORE_DROP
```

```text
MATCH_ENDED
reasonCode = TEAM_ESCAPED
```

---

## 14.2. Rule

`reasonCode` phải:

- là enum/code có kiểm soát;
- dùng `UPPER_SNAKE_CASE`;
- ổn định để query/aggregate;
- không chứa câu văn tự do;
- không dùng để thay thế `eventType`.

---

## 14.3. Conditional Null

Một số event có thể không cần reason riêng.

Nếu không có reason hợp lệ:

```json
"reasonCode": null
```

Không dùng giá trị giả như:

```text
UNKNOWN_REASON_123
OTHER_RANDOM
```

trừ khi enum đó được schema chính thức định nghĩa.

---

# 15. Validation Rules

Backend/validator sau này phải reject event khi thiếu field bắt buộc hoặc vi phạm active contract của schemaVersion.

Minimum validation:

```text
id != null
matchId != null
eventType != null
ts != null
valueJson != null
valueJson.context là JSON object
valueJson.data là JSON object
schemaVersion != null
```

Ngoài ra:

```text
eventType phải thuộc catalog/version được hỗ trợ và phải ở trạng thái active để được emit
schemaVersion phải được hỗ trợ
timestamp phải parse được
userId phải tuân theo Section 4.3
payload required của event phải đầy đủ
reasonCode phải thuộc allowed set nếu event yêu cầu reason
MATCH_STARTED phải có context.scenarioConfigVersion
scenarioConfigVersion đặt dưới valueJson.data là invalid trong schemaVersion 1.0
RESERVED / NOT EMITTED event phải bị reject trong current v1.0 baseline
PUZZLE_FAILED phải bị reject trong current v1.0 baseline cho đến khi gameplay/spec và schema contract chính thức activate event
```

Ví dụ invalid:

```json
{
  "eventType": "PLAYER_DOWNED"
}
```

Lý do:

```text
missing id
missing matchId
missing ts
missing valueJson
missing schemaVersion
missing userId
```

Ví dụ `MATCH_STARTED` invalid trong schemaVersion `1.0`:

```json
{
  "eventType": "MATCH_STARTED",
  "valueJson": {
    "context": {
      "teamSize": 4
    },
    "data": {
      "mapId": "RESEARCH_FACILITY"
    }
  }
}
```

Lý do contract:

```text
missing valueJson.context.scenarioConfigVersion
scenarioConfigVersion không được đặt dưới valueJson.data
```

---

# 16. Duplicate / Idempotency Rule

Mỗi event có `id` duy nhất.

Nếu cùng `id` được gửi lại do retry/batch retry:

```text
same event id
→ không được tạo hai telemetry records logic giống nhau
```

Chi tiết implementation thuộc Backend milestone sau, nhưng contract v0 yêu cầu `id` ổn định cho một event đã phát sinh.

---

# 17. Authority Rule

Gameplay source của event phải đến từ authoritative gameplay flow theo architecture của dự án.

```text
Gameplay / Host
      ↓
Telemetry Emitter
      ↓
Backend Storage
```

Backend là authoritative với dữ liệu đã lưu.

Telemetry không được dùng để client tự khai báo kết quả quan trọng mà không có validation phù hợp.

---

# 18. Batch Compatibility

Implementation Spec có REST baseline:

```text
POST /telemetry/batch
```

Input:

```text
events[]
```

Output baseline:

```text
accepted
rejected
```

M1-008 chỉ bảo đảm mỗi item trong `events[]` tuân theo `TelemetryEvent schemaVersion = 1.0`.

Batch size, retry policy và transport implementation được để cho milestone Backend/Telemetry sau.

---

# 19. Example Batch

```json
{
  "events": [
    {
      "id": "evt_001",
      "matchId": "match_001",
      "userId": "player_02",
      "eventType": "NOISE_EMITTED",
      "ts": "2026-08-20T09:15:32.125Z",
      "valueJson": {
        "context": {
          "phase": "CORE_COLLECTION",
          "position": {
            "x": 12.5,
            "y": 0.0,
            "z": 8.3
          }
        },
        "data": {
          "noiseType": "SPRINT",
          "loudness": 0.7
        }
      },
      "reasonCode": "PLAYER_SPRINT",
      "schemaVersion": "1.0"
    },
    {
      "id": "evt_002",
      "matchId": "match_001",
      "userId": "player_03",
      "eventType": "PLAYER_DOWNED",
      "ts": "2026-08-20T09:16:04.511Z",
      "valueJson": {
        "context": {
          "phase": "CORE_COLLECTION",
          "monsterType": "STALKER"
        },
        "data": {
          "downCount": 1
        }
      },
      "reasonCode": "STALKER_ATTACK",
      "schemaVersion": "1.0"
    }
  ]
}
```

---

# 20. Telemetry → Profile / AED Boundary

Gameplay Design quy định:

```text
Kết quả trận
+
Dữ liệu trong trận
        ↓
Player / Team Profile
        ↓
AED
        ↓
Scenario Configuration
```

TelemetryEvent là raw structured event.

Không dùng raw event trực tiếp để tùy tiện thay đổi gameplay.

Luồng đúng:

```text
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

AED chỉ được sử dụng dữ liệu đã được xử lý theo contract/model được chốt ở các task tiếp theo.

---

# 21. Out of Scope for M1-008

Các phần sau **không thuộc M1-008**:

- implement Unity telemetry emitter hoàn chỉnh;
- implement network upload;
- implement `/telemetry/batch`;
- database migration;
- telemetry retry queue;
- metric normalization 0..100;
- Player/Team Profile formulas;
- AED scoring;
- Adaptive Decision Log;
- Fixed vs Adaptive experiment;
- dashboard;
- analytics visualization;
- continuous Player position logging;
- emotion detection;
- GenAI telemetry decision.

Các phần này được xử lý ở task/milestone tương ứng sau.

---

# 22. Implementation Constraints

1. Không thêm telemetry event cho mechanic chưa được GDD/spec chốt.
2. `TelemetryEvent` phải giữ các field của data model baseline.
3. `schemaVersion` bắt buộc.
4. `reasonCode` phải có kiểm soát và giải thích được.
5. `valueJson` chứa payload linh hoạt theo event.
6. `context` nằm trong `valueJson.context` ở schema v0; `scenarioConfigVersion` của match nằm tại `valueJson.context.scenarioConfigVersion`.
7. `PHASE_STARTED` / `PHASE_COMPLETED` là source-of-truth cho phase lifecycle; không double-count bằng specialized start/completed event.
8. Event `RESERVED / NOT EMITTED` không được Unity/Host emit trong current v1.0 baseline.
9. `PUZZLE_FAILED` không được activate chỉ vì wrong input; phải có gameplay consequence đã được spec freeze.
10. `PLAYER_RESCUED` không được dùng để duplicate `PLAYER_REVIVED` hoặc `PLAYER_ESCAPED`.
11. Không log Player transform mỗi frame.
12. Runtime `NoiseEvent` và telemetry `NOISE_EMITTED` là hai trách nhiệm khác nhau.
13. Telemetry không điều khiển Traditional Monster AI.
14. Telemetry là input cho aggregation/Profile/AED ở các bước sau.
15. Event quan trọng phải có khả năng tái tính metric liên quan từ log/sample đã lưu.
16. Không hard-code công thức metric chưa được task Profile/AED chốt.
17. Không implement transport, retry queue, database migration, aggregation service, MatchScore/Profile/AED formula hoặc analytics ngoài compatibility contract đã nêu trong M1-008.

---

# 23. M1-008 Completion Criteria

Task M1-008 được xem là hoàn thành khi có:

- [x] common event contract rõ ràng với `eventType`, `userId`, `ts`, `valueJson`, `reasonCode`, `schemaVersion`;
- [x] `context` / `data` convention rõ ràng và `scenarioConfigVersion` thống nhất tại `valueJson.context`;
- [x] event naming convention;
- [x] event catalog v1.0 không ambiguity;
- [x] generic phase lifecycle là source-of-truth và không duplicate lifecycle event;
- [x] reserved/conditional event được đánh dấu rõ và không emit trong current baseline;
- [x] `userId` semantics rõ cho player-level, system-level và reserved event khi activate;
- [x] payload definition cho event P0 chính;
- [x] `PUZZLE_FAILED` có activation rule không invent gameplay consequence;
- [x] `PLAYER_RESCUED` có semantic boundary rõ với Revive/Escape;
- [x] noise telemetry contract;
- [x] tách Runtime `NoiseEvent` khỏi TelemetryEvent;
- [x] mapping từ active event sang metric không double-count và không phụ thuộc event NOT EMITTED;
- [x] validation baseline;
- [x] quy tắc không log position mỗi frame;
- [x] compatibility với `/telemetry/batch` mà không implement transport;
- [x] naming `Telemetry Event Schema v0` và serialized `schemaVersion = "1.0"` được giữ nguyên;
- [x] Unity/Host và Backend có thể implement frozen baseline mà không phải tự suy đoán contract.

**Final Status: DONE / FROZEN**

---

# 24. Frozen Baseline Summary

```text
TelemetryEvent v1.0
=
id
+ matchId
+ userId
+ eventType
+ ts
+ valueJson
    ├─ context
    └─ data
+ reasonCode
+ schemaVersion
```

Frozen contract decisions:

```text
Telemetry Event Schema v0
→ tên design iteration / milestone M1-008

schemaVersion = "1.0"
→ serialized contract version đầu tiên được freeze

scenarioConfigVersion
→ valueJson.context.scenarioConfigVersion

Gameplay phase lifecycle
→ PHASE_STARTED / PHASE_COMPLETED

SECURITY_HOLD_INTERRUPTED
→ active specialized semantic cho interruption/progress pause

SECURITY_HOLD_STARTED
SECURITY_HOLD_COMPLETED
FINAL_HUNT_STARTED
→ RESERVED / NOT EMITTED IN schemaVersion 1.0

PUZZLE_FAILED
→ RESERVED / CONDITIONAL
→ NOT EMITTED IN CURRENT v1.0 BASELINE

PLAYER_RESCUED
→ RESERVED / NOT EMITTED IN schemaVersion 1.0
→ không duplicate PLAYER_REVIVED hoặc PLAYER_ESCAPED

Rescue Count
→ Deferred until rescue definition is frozen
```

Runtime Noise và Telemetry tiếp tục tách biệt:

```text
Runtime NoiseEvent
→ Noise System
→ Hearing Sensor
→ Listener AI
```

Song song:

```text
Runtime NoiseEvent
→ Telemetry Emitter
→ NOISE_EMITTED
→ Backend
```

Telemetry không điều khiển Listener hoặc Traditional Monster AI.

Luồng dữ liệu:

```text
Gameplay Event
     ↓
TelemetryEvent v1.0
     ↓
MatchTelemetry
     ↓
MatchScore
     ↓
Player / Team Profile
     ↓
AED
```

Đây là contract baseline để Unity/Host, Backend và các task Telemetry, Profile, AED tiếp tục triển khai mà không thay đổi gameplay và không phải tự suy đoán contract.
