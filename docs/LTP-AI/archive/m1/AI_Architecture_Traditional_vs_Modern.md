# ECHO PROTOCOL — AI Architecture: Traditional AI vs Modern AI

**Task:** M1-007 — Chốt Traditional AI vs Modern AI  
**Status:** DONE / FROZEN  
**Scope:** Kiến trúc AI cho monster runtime, Adaptive Experience Director (AED) và GenAI Mission Briefing.

---

## 1. Purpose

Tài liệu này xác định ranh giới trách nhiệm giữa:

- **Traditional AI** dùng cho hành vi monster trong trận;
- **Adaptive Experience Director (AED)** dùng để chuẩn bị và điều chỉnh `Scenario Configuration`;
- **GenAI** dùng cho `Mission Briefing`.

Mục tiêu chính là bảo đảm:

> **Monster AI, AED và GenAI không trộn trách nhiệm.**

---

## 2. Traditional AI Scope

Hành vi runtime của monster được triển khai bằng **Traditional AI**.

Traditional AI bao gồm:

- Finite State Machine (FSM);
- rule-based decision;
- perception sensor;
- target selection;
- pathfinding / NavMesh;
- configurable gameplay parameters.

### 2.1. The Stalker

The Stalker sử dụng:

- Vision / Line of Sight;
- Detection Meter;
- target selection;
- Last Known Position;
- Patrol;
- Detect;
- Chase;
- Search;
- Attack;
- Recover;
- NavMesh/pathfinding.

Luồng FSM đã chốt:

```text
PATROL
→ DETECT
→ CHASE
→ ATTACK
→ RECOVER
→ SEARCH
→ PATROL
```

The Stalker không sử dụng Machine Learning hoặc GenAI để quyết định hành vi runtime.

---

### 2.2. The Listener

The Listener sử dụng Traditional AI kết hợp hệ thống cảm nhận tiếng động.

Các nguồn tiếng động trong gameplay gồm:

- Sprint;
- tương tác với vật thể;
- mang Energy Core;
- làm rơi Energy Core;
- Noise Maker.

Luồng kiến trúc dự kiến:

```text
Player / Environment
        ↓
    Noise Event
        ↓
   Noise System
        ↓
 Hearing Sensor
        ↓
  Listener FSM
```

Listener phản ứng theo rule được thiết kế trước, không sử dụng GenAI để quyết định hành vi.

---

### 2.3. The Warden

The Warden sử dụng Traditional AI / rule-based logic cho:

- kiểm soát route;
- kiểm soát cửa;
- chọn route hợp lệ;
- phản ứng khi route bị chặn;
- đảm bảo gameplay vẫn còn đường hợp lệ để tiếp tục objective.

---

## 3. Traditional AI Responsibilities

| System | Responsibility |
|---|---|
| FSM | Điều khiển state của monster |
| Vision Sensor | Phát hiện Player bằng LOS |
| Hearing Sensor | Nhận và đánh giá Noise Event |
| Target Selection | Chọn target theo rule |
| Navigation | Di chuyển bằng NavMesh/pathfinding |
| Search Logic | Tìm kiếm quanh Last Known Position hoặc nguồn noise |
| Attack Logic | Kiểm tra range, wind-up, hit moment và recover |
| Configurable Parameters | Nhận các giá trị tuning từ configuration |

Traditional AI phải có hành vi **deterministic/rule-based trong phạm vi gameplay đã thiết kế**.

---

## 4. Adaptive Experience Director (AED)

AED không trực tiếp điều khiển monster theo từng frame hoặc từng hành động.

AED có nhiệm vụ:

```text
Match Result / Telemetry
        ↓
Player / Team Profile
        ↓
        AED
        ↓
Scenario Configuration
        ↓
Gameplay Systems / Traditional AI
```

AED có thể chuẩn bị hoặc điều chỉnh các tham số đã được thiết kế trước, ví dụ:

- tốc độ hoặc mức độ phản ứng của monster;
- một số tham số hành vi của monster;
- vị trí xuất hiện Energy Core trong tập vị trí hợp lệ;
- một số tham số điều tiết mức áp lực của trận.

AED:

- không tạo mechanic mới;
- không thay đổi core gameplay;
- không thay thế FSM;
- không trực tiếp quyết định `CHASE`, `ATTACK` hoặc `SEARCH`;
- chỉ cung cấp configuration trong giới hạn đã thiết kế và kiểm thử.

---

## 5. Player / Team Profile

`Player/Team Profile` là dữ liệu đầu vào cho AED.

Luồng:

```text
Kết quả trận
+ Telemetry trong trận
        ↓
Player / Team Profile
        ↓
        AED
        ↓
Scenario Configuration cho trận tiếp theo
```

Profile không trực tiếp điều khiển monster.

Traditional AI chỉ nhận các tham số cần thiết thông qua configuration/gameplay systems.

---

## 6. GenAI Scope

GenAI được tách khỏi Monster AI và AED gameplay decision.

Trong scope hiện tại:

```text
Backend
   ↓
GenAI Adapter
   ↓
Mission Briefing
```

GenAI có thể được dùng để hỗ trợ tạo hoặc trình bày nội dung `Mission Briefing`.

GenAI **không được dùng để**:

- điều khiển monster runtime;
- chọn target;
- quyết định Chase;
- quyết định Attack;
- quyết định Search;
- thay đổi FSM;
- sinh mechanic mới;
- thay đổi gameplay rule;
- tự thay đổi monster stat trong lúc chơi;
- thay thế AED.

---

## 7. Responsibility Boundary

| Thành phần | Loại | Trách nhiệm |
|---|---|---|
| Stalker AI | Traditional AI | Vision, Detect, Chase, Search, Attack |
| Listener AI | Traditional AI | Hearing, Noise Investigation, Chase |
| Warden AI | Traditional AI | Route / Door Control |
| NavMesh | Traditional AI | Pathfinding |
| Vision Sensor | Traditional AI | LOS perception |
| Hearing Sensor | Traditional AI | Sound perception |
| Target Selection | Traditional AI | Chọn target theo rule |
| Telemetry | Data Collection | Thu thập dữ liệu gameplay |
| Player/Team Profile | Data / Model | Tổng hợp dữ liệu người chơi và đội |
| AED | Adaptive AI Layer | Chuẩn bị Scenario Configuration |
| Scenario Configuration | Configuration | Cung cấp tham số đã được kiểm soát |
| GenAI Adapter | GenAI Integration | Kết nối GenAI cho Mission Briefing |
| GenAI | Content Support | Mission Briefing, không điều khiển gameplay runtime |

---

## 8. Architecture Boundary

```text
                    ┌─────────────────┐
                    │    Telemetry    │
                    └────────┬────────┘
                             ↓
                    ┌─────────────────┐
                    │ Player / Team   │
                    │     Profile     │
                    └────────┬────────┘
                             ↓
                    ┌─────────────────┐
                    │       AED       │
                    └────────┬────────┘
                             ↓
                    ┌─────────────────┐
                    │ Scenario Config │
                    └────────┬────────┘
                             ↓
             ┌────────────────────────────┐
             │   Traditional Game AI      │
             ├────────────────────────────┤
             │ Stalker                    │
             │ Listener                   │
             │ Warden                     │
             │ Sensors                    │
             │ FSM / Rules / Navigation   │
             └────────────────────────────┘
```

GenAI nằm ngoài luồng điều khiển monster:

```text
Backend
   ↓
GenAI Adapter
   ↓
Mission Briefing
```

Không tồn tại luồng:

```text
GenAI → Monster FSM
GenAI → Chase / Attack Decision
GenAI → Runtime Gameplay Rule
```

---

## 9. Architecture Decision

Kiến trúc AI của ECHO PROTOCOL được chốt như sau:

> **Monster runtime behavior = Traditional AI.**

> **AED = Adaptive layer sử dụng Player/Team Profile để chuẩn bị Scenario Configuration trong giới hạn đã thiết kế.**

> **GenAI = Mission Briefing / content support, không điều khiển gameplay runtime.**

Việc tách trách nhiệm này giúp:

- hành vi monster có thể kiểm thử;
- gameplay giữ tính ổn định;
- dễ debug;
- tránh phụ thuộc vào GenAI trong runtime;
- AED có thể điều chỉnh độ khó/áp lực mà không phá vỡ core gameplay;
- phù hợp với phạm vi triển khai của dự án.

---

## 10. Implementation Constraint

Implementation phải tuân theo các nguyên tắc:

1. Monster behavior chỉ sử dụng Traditional AI trong runtime.
2. FSM và gameplay rule phải bám specification đã được chốt.
3. AED chỉ được thay đổi các configurable parameter đã được cho phép.
4. AED không được tự tạo mechanic hoặc gameplay rule mới.
5. GenAI không được điều khiển Monster AI.
6. GenAI không được thay thế FSM, Sensor, Navigation hoặc Target Selection.
7. Các system phải giao tiếp qua contract/configuration rõ ràng để tránh phụ thuộc chéo.

---

## 11. M1-007 Completion Criteria

Task **M1-007** được xem là hoàn thành khi nhóm thống nhất:

- [x] Monster runtime dùng Traditional AI.
- [x] Stalker dùng Vision / LOS.
- [x] Listener dùng Sound / Hearing.
- [x] Warden dùng Route / Door Control.
- [x] AED chỉ chuẩn bị / điều chỉnh Scenario Configuration.
- [x] Player/Team Profile là input cho AED.
- [x] GenAI chỉ phục vụ Mission Briefing trong scope hiện tại.
- [x] Monster AI, AED và GenAI không trộn trách nhiệm.

**Final Status: DONE / FROZEN**
