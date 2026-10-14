# AED DYNAMIC PACING DESIGN — V0.2 · STALKER POLICY REVISION 02 (BẢN ĐỀ XUẤT ĐẦY ĐỦ ĐỂ DUYỆT)

**Dự án:** ECHO PROTOCOL — Adaptive Enemy Director (AED)
**Phạm vi:** PreMatch + In-Match (`Phase Boundary` **và** `Safe Window` trong cùng đợt triển khai)
**Cơ sở đối chiếu:** GitHub `lamthanhphuc/ECHO-PROTOCOL`, nhánh `LTP`, commit `10391b1d820a9f25d2352d925dbf5bfc39962e50` (14/10/2026)
**Ngày phiên bản:** 14/10/2026 (revision Stalker 02)
**Trạng thái:** **Ba nguyên tắc Stalker đã chốt. Đã điền toàn bộ candidate parameters Rev02 để duyệt; CHƯA chốt calibration, CHƯA phê duyệt triển khai/bật AED Gameplay.**
**Bảo vệ bất biến:** Giữ nguyên Fixed/Normal baseline và mọi chế độ Fixed, không sửa baseline của `main`.

> **Cách đọc:** `Đã xác minh từ code` = hành vi truy vết được trên commit nêu trên; `ĐÃ CHỐT` = chủ dự án phê duyệt nguyên tắc; `ĐỀ XUẤT REV02` = số mặc định có giải thích, *chỉ để duyệt*, chưa phải kết quả đo; `GATE` = bắt buộc đạt mới có thể bật gameplay. Không có Unity PlayMode/Backend integration/E2E thực thi trong lần đánh giá này. **Mục XV–XVIII là bộ đề xuất đầy đủ**, được ưu tiên nếu bảng candidate cũ còn ghi trống hoặc nêu khoảng rộng.

## 0. TÓM TẮT THAY ĐỔI TỪ V0.1 VÀ CÁC BẤT BIẾN

| Nhóm | v0.1 / hiện trạng | Quyết định v0.2 |
|---|---|---|
| Zone 3 Minion | Tài liệu v0.1 nhầm Normal Zone 3 cap = 1; `MatchDifficultyProfiles.Normal` thực tế = 0 | **Fixed Normal Zone 3 = 0 không đổi**. Chỉ mode Adaptive được phép 0→1 thông qua policy `Zone3AdaptiveMinionCap`, đầy đủ approval, thời điểm spawn an toàn; relief 1→0. |
| TeamTool | Mỗi Zone đảm bảo 5 loại cơ bản; extra mang tính hỗ trợ | **Được giảm số lượng Tool *tùy chọn*** (extra/bonus chưa spawn) theo `OptionalToolBudget`; **không được giảm 5 Tool bắt buộc** hoặc xóa item đã spawn/sở hữu. |
| In-Match | Phase boundary production-oriented; giữa phase chưa có contract độc lập | **Triển khai hai loại decision point trong cùng release**; tách ledger/identity/safety cho `PHASE_BOUNDARY` và `SAFE_WINDOW`, dùng chung policy + budget. |
| Historical Skill | Các chi tiết research-only | Không dùng noise score / ít Down làm proxy kỹ năng. Cần context, opportunity đã resolve và source Backend verified. |
| Pressure | Nghiên cứu `Quiet/Elevated/Critical/Unknown` | Tách `TeamPacingPressureBand` (`TP0` Critical → `TP4` Very Low) khỏi `PlayerPressure01` (`PP0` Very Low → `PP4` Critical), không dùng như API production khi chưa version hóa. |
| Spawn Tool | `TeamToolWorldSpawn.TrySpawnInitial` spawn 3 Zone từ đầu match | Đề xuất tách planning/reservation, spawn khi Zone khởi động; support giữa Zone chỉ thêm item chưa tồn tại. |
| Minion tuning | V1 research-only, Zone3 research baseline 1 mâu thuẫn với Normal | Tái sử dụng Director nhưng sửa baseline production, thêm key/contract riêng Zone3; không mở gameplay chỉ bằng cách lật flag. |
| Approval | Backend v2 chặn COMMITTED PreMatch, giới hạn 1 decision/phase | Cần version contract/DB/endpoint cho PreMatch và nhiều safe-window decisions trong cùng phase. Không tái sử dụng `PhaseOrdinal` làm ID duy nhất của window. |
| **Stalker Policy Rev 01** | Source chỉ có Stalker-focused pressure, Hide inspection force-exit và Special Down-triggered | **Chốt P4 Hidden protection (kể cả đèn); Cabinet Ambush cảnh báo→Jump gần→đi tới→inspection; PlayerPressure gồm 5 nguồn.** Thông số và enablement chưa duyệt. |

**Bất biến bắt buộc:**

1. Adaptive chỉ chạy khi Host có authority, mode Adaptive được bật có kiểm soát, match/roster/phase/plan revision còn hợp lệ; Fixed không đổi.
2. HOLD là quyết định hợp lệ; thiếu evidence, attribution, supply/census, trust hoặc safe application window thì không được tự tăng khó.
3. Không thay đổi trực tiếp AI state trong CHASE/ATTACK/RECOVER/Special, không rút tài nguyên đã sở hữu, không spawn quái vi phạm NavMesh/door/Safe Room/route.
4. Số lượng Minion / Tool / Stalker không được tăng chỉ vì cooldown hết; cooldown là **điều kiện cần, không đủ**.
5. Mỗi **in-match** transaction tối đa một thay đổi gameplay có thể kiểm chứng; PreMatch dùng initial composite plan được phê duyệt như một cấu hình nguyên tử. Cả hai đều tính ngân sách dựa trên **tổng ảnh hưởng**, không chỉ số lượng key.
6. Trường hợp timeout/stale/reconnect phải rõ fail-closed cho *adaptation* nhưng fail-open cho *gameplay progression*, trừ khi hành động nền tảng vốn cần Backend để hợp lệ.
7. Không có bằng chứng runtime-effect thì không được tuyên bố thành công chỉ vì Backend trả `APPLIED`.

---

## I. MỤC TIÊU TRẢI NGHIỆM

AED không có nhiệm vụ giữ tỷ lệ thắng cố định hoặc trừng phạt người chơi vì chơi giỏi.

AED cần tạo cảm giác:

- Người chơi yếu vẫn gặp nguy hiểm nhưng có cơ hội vượt qua.
- Người chơi trung bình phải phối hợp và sử dụng tài nguyên hợp lý.
- Người chơi giỏi gặp thử thách đa dạng hơn, không đơn giản là quái chạy nhanh hơn.
- Khi người chơi liên tục thất bại, AED giảm đúng nguyên nhân gây khó thay vì giảm mọi thứ.
- Khi người chơi đang vượt qua thử thách tốt, AED không được lập tức phá vỡ lợi thế họ vừa tạo ra.
- Người chơi không nhận thấy rõ việc game đang tự thay đổi độ khó.

### 1. Bốn giai đoạn logic

| Giai đoạn AED | Gameplay tương ứng | Zone |
|---|---|---|
| Phase 1 | Core Objective, tìm và vận chuyển Core | Zone 1 |
| Phase 2 | Zone 2 Objective, terminal, relay, security hold | Zone 2 |
| Phase 3 | Tìm Frigate, di chuyển, tiếp nhiên liệu và hoàn thành nhiệm vụ | Zone 3 |
| Phase 4 | Final Hunt và Escape | Kết thúc |

Đây là **bốn giai đoạn logic dùng để báo cáo AED**, không phải enum thay thế `NetworkMatchPhase`. Các subphase thực tế như `Zone3FindFrigate`, `Zone3PushFrigate` và `SecurityHold` phải giữ identity riêng khi thu thập và freeze evidence.

### 2. Ba trục điều chỉnh chính

| Trục | Tăng thử thách | Giảm thử thách |
|---|---|---|
| Stalker | Phát hiện sớm hơn, áp lực tìm kiếm hoặc truy đuổi tăng nhẹ | Phát hiện chậm hơn, thời gian nghỉ giữa encounter dài hơn |
| Minion | Tăng nhẹ số lượng hoặc tần suất gây rối trong giới hạn | Giảm số Minion sinh mới hoặc giảm tần suất gây rối |
| TeamTool | Vị trí phân tán hơn, ít bonus hỗ trợ, đòi hỏi khám phá | Tool hữu dụng gần lối vào/nhiệm vụ hơn, cấp bonus có điều kiện |

AED được phép chọn **HOLD** — giữ nguyên — ngay cả khi phát hiện một đội chơi giỏi.

---

**Điều chỉnh v0.2:** Rất quan trọng là `Zone3FindFrigate` và `Zone3PushFrigate` có safe-window khác nhau; `FinalHunt` và `Escape` không được tăng áp lực. Hỗ trợ đội yếu khác với bù phần thưởng chơi giỏi: không dựng biến thiên áp lực kiểu rubber-banding tức thì.

## II. HỆ THỐNG ĐÁNH GIÁ ĐỘ KHÓ VÀ KỸ NĂNG

### 1. Historical Skill — đủ opportunity, không suy từ proxy

Chỉ dùng các observation đa trận được Backend chứng thực với `ComparisonContextKey` tương đồng, episode/opportunity đã resolve và các điều kiện chống bias. Các chiều đã có trong `AEDHistoricalSkillV1`: `Survival`, `Evasion`, `Objective`, `MinionCounterplay`, `ToolEffectiveness`, `ResourceManagement`, `Teamwork`, `RiskStyle`. `RiskStyle` để mô tả chiến thuật, không dùng như điểm competence.

Research projector trên `LTP` đang dùng **≥3 trận khác biệt và ≥8 resolved opportunities** làm ngưỡng confidence (chưa calibrate). Nếu đủ dữ liệu nhưng sai context, lệch version hoặc provenance thì vẫn `Insufficient`/`Invalid`.

`AEDTeamSkillV1` đã có mean, weakest, variance và bảo vệ unknown member. Không tăng pressure nếu có người mới, roster chưa verified, hoặc weakest-player skill chưa đủ tin cậy. Có thể relief dựa trên bằng chứng hiện tại ngay cả khi historical skill là ColdStart; không được nhầm đây là kết quả profiling.

**Vấn đề source đã xác minh:** `AEDv2Policy.Evaluate()` hiện trả `AED_V2_FIRST_PHASE_BASELINE` cho PreMatch, còn Backend `ScenarioAdaptivePlanV2Service` từ chối `COMMITTED` PreMatch; đây là **blocker** phải nâng contract nếu mong v0.2 Adaptive PreMatch tạo tác động thật. Cần giữ đường PreMatch v1.1 đang tồn tại độc lập, không gộp lẫn hoặc sửa sai baseline.

### 2. Current Match Pressure — cửa sổ quan sát có bằng chứng

Nguồn tối thiểu: Stalker episode (CHASE / ATTACK / special + consequence), Minion episode (harass / slow / core/tool disruption), down/revive/eliminate, objective progress, TeamTool world + inventory + charges, active encounter, alive roster, debug/connection flags.

`AEDCurrentMatchPressureV1` hiện nghiên cứu sử dụng các mức `Unknown/Quiet/Elevated/Critical`. V0.2 đề xuất **policy mapping team `TP0…TP4`** với threshold tính từ dữ liệu calibration; **không đổi enum runtime nếu chưa thống nhất contract và test**.

| Mức policy | Diễn giải | Quyết định mặc định |
|---|---|---|
| Unknown | Source thiếu/không verified/đang debug | HOLD |
| TP0 Critical | Có nguy cơ đội không tiếp tục được | Cân nhắc relief khẩn có kiểm chứng |
| TP1 High | Encounter gây gián đoạn đáng kể | RELIEVE một trục đã xác minh |
| TP2 Balanced | Thử thách phù hợp | HOLD |
| TP3 Low | Đội xử lý encounter tốt | HOLD hoặc candidate nhỏ, phải qua confidence gate |
| TP4 Very Low | Liên tục dễ, đủ cơ hội quan sát và confidence | Có thể INCREASE nhẹ, phải qua budget/fairness/cooldown |

**Quan trọng:** Công thức team pressure hiện tại và `TP0–TP4` chưa có calibration; không thể coi `Quiet` tương đương chắc chắn TP3/TP4 hoặc diễn giải không gặp encounter là skill cao.

### 3. Causal attribution và cơ hội quan sát

- Phân biệt `CombatDifficulty`, `ObjectiveDifficulty`, `ResourceAccessDifficulty`, `Exploration/Knowledge`, `Connectivity/Bug`.
- Có `VerifiedCause` khi event hành vi threat/cản trở gắn với objective window và consequence. Nếu chỉ tương quan thời gian: `Suspected`, chỉ dùng shadow; không cấp quyền tăng khó.
- Causal chain đa tác nhân: Minion slow → Stalker down phải lưu cả hai nguồn và không nhân đôi consequence trong pressure score.
- Không tính phase-ending, disconnection, AFK hoặc debug skip thành resolved success/failure; censored opportunity cần xử lý riêng.

### 4. Kịch bản đánh giá PreMatch

| ID | Tình huống | Quyết định đề xuất |
|---|---|---|
| PM-01 | Đội hoàn toàn mới | Bắt đầu gần baseline Normal, ưu tiên safety và quan sát |
| PM-02 | Người chơi từng bị Down nhiều vì Stalker | Cho Stalker mức dễ hơn một nấc hoặc tăng recovery window |
| PM-03 | Người chơi bị Minion cướp Tool nhiều | Giữ Stalker, giảm áp lực Minion hoặc hỗ trợ Tool phù hợp |
| PM-04 | Người chơi có kỹ năng tốt, hoàn thành nhiệm vụ đều | Có thể rải tài nguyên khó tìm hơn một mức |
| PM-05 | Người chơi rất giỏi đối phó Stalker nhưng kém đối phó Minion | Giữ Stalker ở mức phù hợp, không tăng Minion |
| PM-06 | Người chơi giỏi Minion nhưng kém Stalker | Không tăng Stalker; chỉ cân nhắc thay đổi nhẹ ở trục khác |
| PM-07 | Đội nhiều người, kỹ năng chênh lệch | Ưu tiên fairness cho thành viên yếu nhất |
| PM-08 | Có người mới vào đội nhiều người chơi giỏi | Không lấy skill trung bình làm lý do tăng mạnh |
| PM-09 | Lịch sử có quá ít opportunity | Giữ các trục chưa đủ confidence tại baseline |
| PM-10 | Đội có lịch sử hoàn thành nhiệm vụ chậm do thiếu vật phẩm | Ưu tiên cải thiện khả năng tìm TeamTool, không mặc định giảm Stalker |
| PM-11 | Đội thắng nhanh nhưng tránh hầu hết encounter | Không đánh giá Stalker Evasion cao khi chưa có encounter hợp lệ |
| PM-12 | Đội quay lại sau thời gian dài | Hạ độ tin cậy của historical skill cũ, tăng trọng số quan sát trận hiện tại |

---

### 5. Chính sách quyết định PreMatch v0.2

1. Chọn baseline Adaptive-Normal giống Fixed-Normal, ngoại trừ các **delta** đã approved/versioned. Không tự động chọn Easy/Hard nguyên trạng.
2. Tối đa một **stalker tuning delta** tại PreMatch; `OptionalToolBudget` và placement manifest là các phần khác của một **initial composite plan** được Backend phê duyệt atomically. PreMatch có thể chốt policy Zone3 nhưng **không mặc định bật Minion 1 ở Zone3** khi chưa có pressure window trong trận. Composite plan có validation riêng, không sử dụng quy tắc single-key của các delta In-Match.
3. Nếu đội chưa đủ skill confidence, baseline không tăng Stalker/Minion; được bố trí Tool gần hơn theo một relief có căn cứ.
4. PreMatch Tool phải materialize/reserve trước khi các object của Zone 1 xuất hiện; nếu approval chưa có ở thời điểm spawn, dùng baseline bình thường và không backfill ngầm.
5. PreMatch approval phải ghi planned-vs-applied manifest, source identity, snapshot/roster fingerprint, config fingerprint, revision và receipt; Backend reject thì fallback baseline, không bỏ qua gate.

## III. ĐIỀU PHỐI NHỊP ĐỘ TOÀN TRẬN: PHASE BOUNDARY + SAFE WINDOW

### 1. Hai thời điểm quyết định — cùng release, cùng bảo vệ

| Loại | Trigger | Evidence window | Điều được làm | Điều cấm |
|---|---|---|---|---|
| `PHASE_BOUNDARY` | Host hoàn tất phase/subphase hợp lệ | Frozen completed-phase evidence, phase ordinal canonical | Chỉnh một thông số Stalker, target Minion cap, reserve/spawn Zone chưa bắt đầu, resource relief | Áp dụng nếu active encounter, stale proof hoặc đã advance phase |
| `SAFE_WINDOW` | Host xác nhận trong phase không có active threat/interaction nhạy cảm | Sliding bounded window `[startTick,endTick]`, current phase/zone/roster, event coverage | Bounded parameter delta, Minion target cap, **thêm** support Tool tại điểm chưa dùng | Tái bố trí Tool đã spawn, tăng pressure trong CHASE/ATTACK, spawn giữa objective critical |

**Safe-window không có nghĩa là timer đơn thuần.** Có thể là Host quan sát: Stalker không CHASE/ATTACK/RECOVER/Special, không Minion Harass/Track có thể gây dồn áp lực, không player đang Down/Revive nhạy cảm, không đang tương tác mission quan trọng, route/spawn hợp lệ, telemetry đủ. Các tình huống Down có thể cần relief nhưng việc apply vẫn đợi safe window; không dùng Down như điều kiện buộc gameplay đứng lại.

**Vấn đề bắt buộc sửa từ source:** Backend V2 hiện chỉ chấp nhận `PRE_MATCH` hoặc `ALLOWED_PHASE_BOUNDARY` / `FINAL_HUNT_SETUP`, lưu một decision cho mỗi `PhaseOrdinal`. Nếu thêm safe-window bằng cách đổi chuỗi decision point nhưng giữ database uniqueness/verification cũ sẽ bị reject hoặc conflict. Phải nâng schema + DTO + verifier, **không giả vờ rằng V2 đã hỗ trợ**.

### 2. Plan, budget và hysteresis

- `StalkerPressureTier ∈ {-1,0,+1}` trong đợt đầu. +1 nghĩa tăng một hiệu ứng được whitelist, không phải được phép tăng liên tiếp mọi key. *Handoff* `+1 → 0` là một thay đổi **giảm áp lực/return-to-baseline** riêng, phải có Backend approval và dwell; không dùng để reset budget liên tục.
- `MinionPressure`: Zone1 cap target `0..1`; Zone2 `1..2` (chỉ relief dưới Normal); Zone3 Adaptive `0..1`, Fixed Normal luôn 0. Minion spawn áp dụng riêng với global pressure budget.
- `ResourcePressure`: `RequiredToolSet = 5 loại/Zone` bất biến; OptionalBudget tùy chọn `0..2 /match` ở mức thử nghiệm; placement grade R-1..R3. Hạn mức support relief được theo dõi tách nhưng vẫn ảnh hưởng tổng pressure.
- `GlobalPressureBudget` là **ngân sách delta độ khó**, khác với `PlayerPressure01` là **áp lực thực tế trên từng người**; giới hạn tổng ảnh hưởng của Stalker, Minion và Resource; **chỉ một modifier tăng áp lực mạnh hoạt động cùng lúc trong bản thử nghiệm đầu**. Cho phép chuyển trục theo quy tắc handoff (bên dưới); đặc biệt **không đồng thời Stalker +1 và Zone3Minion 1** khi chưa có calibration. Đây là giới hạn pilot, không phải kết quả đo được.
- Khi tăng xong cần observation/dwell trước tăng tiếp; khi vừa relief không được đảo chiều ngay. Không dùng cooldown để qua mặt fairness/evidence gate.
- `HOLD` phải tồn tại cùng lý do/receipt, không ép chọn trục khác khi thiếu bằng chứng.

### 3. Cooldown và điều kiện hoàn trả (giá trị thử nghiệm)

| Điều kiện | Candidate v0.2 | Ghi chú |
|---|---|---|
| Increase cooldown | 120–180 giây **và** ≥1 cửa sổ evidence hợp lệ sau apply | Đề xuất chưa calibration |
| Relieve cooldown | 60–90 giây nếu không phải emergency | Không áp dụng lúc active encounter |
| Dwell sau relief | Ít nhất một observation window / objective checkpoint | Không đảo từ RELIEVE sang INCREASE liên tiếp |
| Một commit In-Match | Tối đa một delta/tier có thể kiểm chứng | PreMatch có initial composite plan; resource placement manifest có nhiều item nhưng là một transaction theo axis |
| Cumulative budget | Không vượt giới hạn áp lực tổng của release | Theo lịch sử revision, không reset mỗi phase |
| Missing/stale source | HOLD | Giữ gameplay tiến triển nếu backend lỗi |
| Escape/Final Hunt | Chỉ HOLD/approved relief, **không increase** trong bản đầu | Giữ cao trào baseline |

### 4. Trình tự điều phối

`Collect authoritative events → Freeze verified snapshot → Compute pressure/causal attribution → Candidate per axis → Select priority by cause + budget/fairness → Backend approval → Host revalidate phase/roster/revision/scene/safety → Commit → Fusion replication → World effect + receipt → Observe dwell`.

- Đội quá dễ: thử Stalker nhẹ một nấc. Nếu Stalker đã +1, **không tăng Stalker nữa**; xét placement của **Tool tùy chọn chưa spawn** nếu budget cho phép, hoặc thực hiện *handoff* sang Minion khi đủ bằng chứng. Nếu handoff sang Zone3Minion 0→1 trong pilot, phải đưa Stalker +1 về 0 ở **một transaction riêng**, quan sát một window an toàn, sau đó mới duyệt Minion 0→1; không đổi hai trục trong cùng commit. Nếu tổng áp lực đã đạt trần hoặc chưa đủ opportunity, HOLD.
- Đội quá khó: giảm đúng nguồn gây khó (Minion/Stalker/resource). Đội vừa mất Tool không đồng nghĩa combat yếu. Khi nguyên nhân không rõ, HOLD hoặc relief an toàn đã được whitelist, không tự hạ tất cả key.
- `SafeWindow` có thể xảy ra nhiều lần trong một `NetworkMatchPhase`; mỗi window **cần identity và revision riêng**.

### 5. Kịch bản điều phối tổng hợp

| ID | Tình huống | Quyết định |
|---|---|---|
| DP-01 | Stalker hơi dễ, Minion bình thường, nhiệm vụ ổn định | Tăng nhẹ một tham số Stalker |
| DP-02 | Stalker đã tăng một nấc, đội vẫn chơi quá dễ | Giữ Stalker, cân nhắc Tool placement của Zone kế tiếp |
| DP-03 | Stalker đã tăng, Tool placement đã khó hơn, đội vẫn dễ | Có thể handoff sang Minion sau khi đưa Stalker về 0, qua dwell/approval/budget; không stack buff mạnh |
| DP-04 | Stalker và Minion đều gây áp lực cao | Không tăng thêm; HOLD |
| DP-05 | Đội gặp khó do Stalker, Minion ít ảnh hưởng | Giảm một tham số Stalker |
| DP-06 | Đội gặp khó do Minion, Stalker đang bình thường | Giảm áp lực Minion trước |
| DP-07 | Đội vừa bị Down liên tiếp | Không tăng trục nào, tạo recovery opportunity |
| DP-08 | Đội mất nhiều Tool nhưng chưa bị Down | Ưu tiên cứu tài nguyên, không tự kết luận kỹ năng combat yếu |
| DP-09 | Nhiệm vụ bị chậm do cả Stalker lẫn Minion | Giảm trục gây hậu quả nhiều hơn; trục còn lại HOLD |
| DP-10 | Đội liên tục tiến triển nhanh sau một lần tăng | Chờ đủ observation/cooldown, không tăng liên tục |
| DP-11 | Đội vượt khó sau khi AED giảm áp lực | Giữ một thời gian, không tăng lại ngay |
| DP-12 | Người chơi đang CHASE hoặc ATTACK | Không thay đổi hành vi Stalker trong encounter |
| DP-13 | Đội đang trong Safe Room | Có thể chuẩn bị thay đổi cho sau khi ra phòng, không ép quái tấn công Safe Room |
| DP-14 | Dữ liệu các trục mâu thuẫn | HOLD hoặc thực hiện một thay đổi giảm áp lực đã được xác minh |
| DP-15 | Một người trong đội liên tục chịu toàn bộ encounter | Ưu tiên weakest-player fairness, tránh tăng áp lực theo thành tích người còn lại |
| DP-16 | AED không thể xác định tác dụng của thay đổi trước | Không cộng thêm thay đổi mới cho đến khi đánh giá được |

---

## IV. TEAMTOOL ADAPTATION — GIỮ 5 TOOL BẮT BUỘC, CÓ THỂ GIẢM TOOL TÙY CHỌN

### 1. Ngân sách phân phối v0.2

**Quyết định đã chốt:** Không giảm 5 Tool bắt buộc của baseline (`Scanner`, `NoiseMaker`, `FirstAid`, `DoorJammer`, `CoreStabilizer`). Cho phép giảm số Tool *tùy chọn* (`OptionalToolBudget`) nhưng chỉ với supply **chưa materialize**. Đây là giảm extra bonus, **không phải** giảm bộ Tool cơ bản trong scene hay inventory.

| Loại | Chính sách | Khi nào được quyết định |
|---|---|---|
| `RequiredSupply` | 5 loại/Zone, có điểm spawn hợp lệ; không trừ vì lobby loadout trừ khi contract baseline mới cho phép | PreMatch/Zone reserve |
| `OptionalToolBudget` | Tối đa 0–2 Tool extra cho cả match ở candidate ban đầu; có thể 2→1→0 trước khi spawn | PreMatch, trước Zone chưa mở |
| `SupportSpawnBudget` | Tối đa +1 Tool phù hợp / relief transaction; tổng budget riêng để tránh spam | Safe Window/Boundary, qua approval |
| `EmergencySupport` | Tối đa một event bổ sung có điều kiện trong bản đầu, không vượt cấm vị trí/ownership | Safe Window có threat clearance |
| Already spawned/carried/dropped | **Không được xóa, teleport, đổi loại, hồi charge hay tịch thu** | Mọi thời điểm |

`SupportBonus` của AEDv2 hiện có giá trị 0→2 và `PreMatchOnly = true` **không đủ để thể hiện OptionalToolBudget, proximity tier, support-on-demand**. **Không được giảm OptionalToolBudget nếu ban đầu chưa cấp extras**: trường hợp `OptionalToolBudget=0` thì "giảm optional" phải là HOLD, không chuyển thành cắt Tool bắt buộc. Nâng version catalog/backend contract thay vì ép trường cũ mang nhiều nghĩa.

### 2. Mức vị trí và khoảng cách

| Grade | Mục tiêu |
|---|---|
| R-1 — Exploration | Tool *tùy chọn* xa hơn, phòng phụ có đường đi mở hợp lệ; không che Tool thiết yếu |
| R0 — Normal | Default spawn planner bình thường |
| R1 — Light Assist | Gần tuyến chính/phòng đầu |
| R2 — Strong Assist | Gần Zone entry, dễ phát hiện |
| R3 — Emergency | Tại điểm có marker an toàn trên tuyến bắt buộc gần đầu Zone |

Khoảng cách dùng **NavMesh path distance từ entry production marker**, cộng điều kiện đồ thị cửa/objective đang mở và điểm player thực tế. `TryGetReachablePosition`/`AEDResourcePlacementV1` trên `LTP` là nền tảng tái sử dụng, nhưng *NavMesh PathComplete* không chứng minh một cửa tương tác đang khóa có thể đi qua.

### 3. Entry marker cho từng Zone

- **Z1**: `Z1_ENTRY`, `Z1_CORRIDOR` (hành lang đầu tới khu trung tâm), `Z1_CENTRAL`, `Z1_SIDE_ROOMS`, `Z1_OBJECTIVE`.
- **Z2**: `Z2_ENTRY`, `Z2_FIRST_ROUTE`, `Z2_TERMINAL`, `Z2_RELAY`, `Z2_SAFE_SIDE_ROOMS`. Debug skip hiện dùng `StalkerZone2EntryTrigger` làm teleport target; chỉ dùng làm **tham chiếu**, không dùng debug state làm source production.
- **Z3**: `Z3_ENTRY`, `Z3_FIRST_ROUTE`, `Z3_FRIGATE`, `Z3_SUPPLY_ROUTE`, `Z3_SIDE_ROOMS`. Debug skip Zone 3 hiện hard-code tọa độ `(88.66215, 2.080028, -399.027)` trong `NetworkMatchState`; production cần **Scene marker** thay cho tọa độ magic, không coi điểm debug là bằng chứng.

### 4. Spawn lifecycle phải thay đổi, nhưng tái sử dụng code

**Đã xác minh:** `NetworkMatchState.Spawned()` gọi `TeamToolWorldSpawn.TrySpawnInitial()` và planner lập/spawn đủ 3 Zone từ đầu match. Để thay placement Zone 2 theo Zone 1, phải tách:

1. `Plan/Reserve` cho Zone chưa khởi động (fingerprint + scene marker manifest).
2. `CommitZoneSpawn` chỉ một lần khi Zone được khởi động/được phép truy cập; Host `Runner.Spawn`, ownership/receipt.
3. `TrySpawnSupport` tại safe-window trong Zone đang chơi, chỉ điểm còn trống và không nằm sau cửa khóa/objective block.
4. Với world items đã xuất hiện: chỉ ghi census, **không replace batch và không rollback Tool player đã thấy/nhặt**.
5. Failure nếu không đủ 5 slot bắt buộc → lỗi content/baseline, không được che bằng AED. Failure extra → giữ 5 và ghi `OPTIONAL_SLOT_UNAVAILABLE`.
6. Chỉ chọn điểm thực sự thuộc player path hiện tại, tránh spawn bên trong player, Stalker/Minion hoặc safe room threat overlap.

**Tính nhất quán Fixed:** Giữ `TeamToolWorldSpawn.TrySpawnInitial()` cũ cho Fixed hoặc bọc feature flag; chỉ Adaptive opt-in dùng deferred mode. Nếu dùng deferred cho cả Fixed thì phải chứng minh Fixed giống baseline về vị trí, số lượng và thời điểm nhìn thấy Tool.

### 5. Kịch bản TeamTool

| ID | Tình huống | Hành động |
|---|---|---|
| TT-01 | Đội khó ngay Zone 1 | Ưu tiên First Aid tại `Z1_ENTRY` hoặc tuyến đầu |
| TT-02 | Đội gặp khó nhiều hơn | Cho Tool hỗ trợ tại `Z1_CORRIDOR` nếu marker hợp lệ |
| TT-03 | Đội có ít tài nguyên nhưng chưa nguy cấp | Ưu tiên một Tool hữu ích gần `Z1_CENTRAL` |
| TT-04 | Đội mạnh | Giữ Tool bắt buộc, chọn điểm xa hơn một mức |
| TT-05 | Đội rất mạnh | Ưu tiên phòng phụ để khuyến khích khám phá, không khóa Tool sau objective |
| TT-06 | Zone 1 mất nhiều Tool vì Minion | Zone 2 ưu tiên khả năng phòng vệ/thu hồi tài nguyên |
| TT-07 | Đội bị Down nhiều | Ưu tiên First Aid, không tự spawn nhiều loại Tool cùng lúc |
| TT-08 | Đội gặp khó vì không xác định được vị trí | Cân nhắc Scanner gần tuyến chính |
| TT-09 | Đội gặp nhiều Stalker encounter | Cân nhắc DoorJammer/NoiseMaker ở vị trí tiếp cận hợp lệ |
| TT-10 | Zone 2 khó khăn | Đặt Tool hỗ trợ ở `Z2_ENTRY` hoặc `Z2_FIRST_ROUTE` |
| TT-11 | Zone 2 vẫn khó sau hỗ trợ nhẹ | Đặt một Tool bổ sung gần khu terminal/relay nhưng không spawn ngay trong objective |
| TT-12 | Zone 2 quá dễ | Đẩy Tool tùy chọn sang phòng phụ, giữ Tool thiết yếu accessible |
| TT-13 | Zone 3 khó vì thiếu tài nguyên | Ưu tiên hỗ trợ từ `Z3_ENTRY` hoặc `Z3_FIRST_ROUTE` |
| TT-14 | Zone 3 khó trong giai đoạn Frigate | Chọn Tool hỗ trợ tương ứng với threat thực tế, không thay objective |
| TT-15 | Zone 3 quá dễ | Phân bố Tool tùy chọn xa hơn, không bắt quay lại vùng nguy hiểm không hợp lệ |
| TT-16 | Đội đã có nhiều First Aid trong inventory | Không spawn thêm chỉ vì số lần Down cao |
| TT-17 | Đội có Tool nhưng chưa dùng | Không coi đó là thiếu nguồn cung |
| TT-18 | Đội bị cướp Tool nhưng Tool vẫn có thể thu hồi | Cân nhắc khả năng recovery trước khi cấp thêm |
| TT-19 | Spawn point mục tiêu không có đường NavMesh đầy đủ | Bỏ điểm đó và chọn fallback hợp lệ |
| TT-20 | Không đủ điểm để spawn Tool bonus | Giữ 5 Tool bắt buộc và ghi lý do không cấp bonus |
| TT-21 | Player đã nhặt Tool | Không teleport, đổi loại hay xóa Tool đó |
| TT-22 | Player mua/chọn Tool từ Lobby | Tính loadout vào tổng cung trước khi quyết định thêm |
| TT-23 | Player đang ở Zone 2 khi AED quyết định giảm khó | Chỉ spawn hỗ trợ bổ sung ở vị trí hợp lệ chưa dùng, không bố trí lại toàn bộ vật phẩm |
| TT-24 | Đội tiếp tục chơi tốt sau hỗ trợ | Không thu hồi Tool từng cấp |
| TT-25 | Bắt đầu Phase 4 Final Hunt | Không tự tạo đợt phân phối Tool lớn, trừ emergency support đã được phê duyệt |

## V. STALKER ADAPTATION — NHẸ, CÓ TRẦN TỔNG

### 1. Thứ tự ưu tiên và bounds

| Mức ưu tiên | Tham số | Normal / Relief / Pressure (candidate từ `AEDv2Catalog`) | Ghi chú |
|---|---|---|---|
| 1 | `DetectionAcquireSeconds` | 1.25 / 1.50 / 1.00 | Điều chỉnh khả năng phát hiện; tránh làm khó thiếu cảnh báo |
| 2 | `PostChaseCooldownSeconds` | 18 / 28 / 12 | Recovery gap giữa chase |
| 3 | `HearingMultiplier` | 1.00 / 0.85 / 1.15 | Không phủ định tác dụng NoiseMaker |
| 4 | `SearchSeconds` | 2.25 / 1.50 / 3.00 | Chỉ tăng khi có đủ evidence search encounter |
| 5 | `ChaseSpeed` | 7.50 / 7.00 / 8.00 | Không dùng như tăng thử thách mặc định |
| 6 | `SpecialCooldownSeconds` | 420 / 540 / 360 | Không tạo special bất ngờ trên route mất đường thoát |

Không đổi trực tiếp hitbox, windup, attack recovery, ánh sáng vật lý, teleport/Safe Room rules hoặc LOS. **Stalker Policy Rev 01 chỉ có quyền bỏ qua clue đèn *gắn với occupant PP4* trong hide protection và chặn force-exit tại action gate; không tắt đèn thật.** Phải đối chiếu `AEDv2GameplayBridge` (Normal-compatible) và `StalkerFusionRuntime.RefreshAEDv2ProfileAuthoritative()` để bảo đảm đúng revision thật sự được áp dụng.

### 2. Trần và fairness

- Một commit chỉ thay đổi một Stalker key; nhiều commit **không được tích lũy** nhiều Stalker buff tới tổng >+1.
- Đã +1 thì mọi Stalker increase sau đều HOLD; chuyển trục khác phải được global budget cho phép, không mặc nhiên.
- Không reapply/rollback một tham số giữa active chase/attack/recover/special.
- Nếu đã relief, chỉ đảo lại Normal sau đủ dwell, evidence, Backend approval; không swing khó liên tục.
- Thứ tự can thiệp khi đội khó: xác minh nguyên nhân Stalker; khi Minion slow dẫn đến bị bắt, ưu tiên xem Minion.

### 3. Kịch bản Stalker

| ID | Tình huống | Hành động |
|---|---|---|
| ST-01 | Người chơi thường bị phát hiện trước khi kịp phản ứng | Tăng nhẹ thời gian acquire |
| ST-02 | Người chơi liên tục thoát Chase và không chịu hậu quả | Có thể giảm nhẹ thời gian acquire hoặc tăng SearchSeconds, chọn một |
| ST-03 | Stalker liên tục bắt kịp và gây Down | Giảm ChaseSpeed một nấc nếu được phép |
| ST-04 | Stalker nhanh nhưng không gây áp lực quá lớn | Giữ speed, không tăng chỉ vì chưa có Down |
| ST-05 | Người chơi liên tục bị truy đuổi không có thời gian hồi phục | Tăng PostChaseCooldownSeconds |
| ST-06 | Đội sử dụng NoiseMaker hiệu quả | Không ngay lập tức vô hiệu hóa chiến thuật NoiseMaker |
| ST-07 | Người chơi quá dễ né Stalker bằng đường vòng | Cân nhắc Search/Hearing, không tự sửa NavMesh hoặc teleport |
| ST-08 | Stalker đã tăng áp lực một nấc | Khóa tăng Stalker tiếp; xem xét trục khác sau cooldown |
| ST-09 | Người chơi bị Minion làm chậm rồi Stalker bắt | Ưu tiên giảm Minion, không đổ toàn bộ lỗi cho Stalker |
| ST-10 | Stalker đang CHASE | Hoãn thay đổi tham số đến safe window |
| ST-11 | Stalker đang ATTACK/RECOVER/Special | HOLD đối với thay đổi gameplay |
| ST-12 | Player đi vào Safe Room | Không tăng khả năng Stalker vượt qua cơ chế Safe Room |
| ST-13 | Stalker không có đường NavMesh hợp lệ | Không sửa bằng cách tăng speed hoặc teleport |
| ST-14 | Đội yếu ở Zone 1 nhưng mạnh hơn ở Zone 2 | Chờ đủ evidence trước khi đảo chiều difficulty |
| ST-15 | Đội rất mạnh ở Zone 3 | Chỉ điều chỉnh giới hạn đã phê duyệt, không tạo spike ngay trước Final Hunt |

### 4. AED STALKER PLAYER PRESSURE FAIRNESS V0.2 — POLICY CHỐT, THÔNG SỐ CHƯA CHỐT

> **Trạng thái phê duyệt:** Đã chốt **(a)** P4 Hidden Box miễn kéo ra trong cửa sổ bảo vệ kể cả khi đang bật đèn; **(b)** Cabinet Ambush phải Jump In *gần tủ* → phát cảnh báo → tiếp cận hợp lệ → kiểm tra tủ → kéo ra; **(c)** Player Pressure phải tính **Stalker + Minion + Down + Resource + Objective**. Không suy ra đã chốt ngưỡng 0.85, multiplier, cooldown, khoảng cách, trọng số hay tần suất. Đây là **Stalker Policy v0.2** thuộc AED pacing v0.2, chưa bật Gameplay.

#### 4.1. Tách ba khái niệm để không lệch contract

| Tên | Cấp độ / chiều | Công dụng | Quy tắc |
|---|---|---|---|
| `PlayerPressure01[player]` / `PP0…PP4` | Per-player; **0 ít → 4 nghiêm trọng** | Điều tiết target selection, sensory, Hidden Box, grace, Jump In fairness | Host-authoritative, không làm phép so sánh trực tiếp với `AEDCurrentMatchPressureV1` hoặc `playerPressureStopThreshold` hiện có |
| `TeamPacingPressureBand` / `TP0…TP4` | Toàn đội; **0 critical → 4 rất thấp** trong policy document | Quyết định trục Stalker/Minion/Resource có cần relief hoặc tăng khó | Dùng aggregate với worst-player guard; không lấy trung bình che một người đang P4 |
| `GlobalPressureBudget` | Cumulative **difficulty delta**, không phải PlayerPressure score | Chặn stacking Stalker buff + Minion + optional supply restriction + Cabinet Ambush | Không reset khi thay target/phase/cooldown; kiểm tra trước Backend approval và Host apply |

`PP4` nghĩa Player chịu áp lực **cao**, còn `TP4` nghĩa toàn đội chịu áp lực **thấp**; trường/enum/payload phải dùng tên đầy đủ. **Không được dùng `PP4` để tự nâng khó**, và không dùng TeamPressure thấp làm bằng chứng tự động rằng từng Player đều chịu ít áp lực.

**Contract phân tầng:** Backend phê duyệt *policy envelope*, version/bounds và persistent tuning ở PreMatch/Phase Boundary/Safe Window. Host thực thi quyết định vi mô theo envelope đã duyệt (target score, kiểm tra tủ, cue hợp lệ, freeze per-encounter, warning/abort) **không gửi mỗi frame/inspection lên Backend**; telemetry chứa reason/version/tick để replay. Chỉ đổi parameter dài hạn/đổi enablement Cabinet Ambush mới tạo transaction; bảo vệ P4 được kiểm tra ngay tại action gate theo policy đã hoạt động, không trì hoãn để chờ phase boundary. Nếu chưa có policy hoặc authoritative pressure hợp lệ để **cấp token mới**, giữ hành vi baseline của tình huống *không bị khởi tạo bởi adaptive*; **Cabinet Ambush mới** fail-closed và không dùng thông tin private để suy đoán. **Ngoại lệ lifecycle:** một `HideInspectionProtection` token P4 đã được Host cấp hợp lệ **không bị thu hồi giữa cửa sổ** chỉ vì telemetry tạm Unknown/reconnect; phải áp dụng token đã ký với TTL, revision và grace, sau đó mới chuyển trạng thái an toàn. Không tự kéo Player khi dữ liệu mới bị trễ.

#### 4.2. Mô hình Player Pressure tổng hợp (quyết định đã chốt: đủ năm nguồn)

`HostPlayerPressureSnapshot` lưu tối thiểu `matchId, phase/zone, playerId, rosterRevision, sourceWindow/tick, coverage/confidence, episodeIds, component scores, total, stable tier, policyVersion, lastProtection/ambush timestamps`.

| Thành phần | Observation hợp lệ | Chống sai lệch |
|---|---|---|
| `S_i` — Stalker | Chase duration + target recurrence + attack/hide-reveal + interrupted action | Phải có encounter thực, dedup target-acquire/CHASE trong cùng episode; không farm bằng vòng lặp ngắn |
| `M_i` — Minion | Slow, harass, Tool steal, forced Core drop, objective interrupt gắn actor | Chỉ tính consequence có verified event; không nhân đôi Stalker-caused Down |
| `D_i` — Down / recovery | Down, revive stress, revive bị ngắt, failed recovery, victim vulnerability | Dùng **mức hậu quả dư** của encounter, không cộng nguyên vẹn một Down lặp ở ba mục S/M/D |
| `R_i` — Resource | Tool/charge thực sự thiếu hoặc không thể tới được lúc cần, thiếu khả năng hồi phục | Inventory trống không tự động là khó; phải kiểm tra world census, supply access, opportunity và tool usage |
| `O_i` — Objective | Bị threat chặn interaction, objective stall verified, carrier fatigue/sustained exposure | Không phạt người làm nhiều objective; không coi tiến độ thấp do khám phá/bug/AFK là pressure do AI |

**Ứng viên công thức, chưa duyệt trọng số:** `RawPP_i = Clamp01(wS*S_i + wM*M_i + wD*D_i + wR*R_i + wO*O_i − recoveryCredit_i)`, với các thành phần normalize `[0,1]` trên opportunity/episode và `Σw=1`. **Không hardcode `w` khi chưa có replay/calibration**. Snapshot phải giữ component breakdown, provenance, độ mới, `Unknown`, và episode consequence caps để ngăn double count. Missing source ≠ zero: nếu thiếu nguồn vật phẩm hoặc objective thì score có confidence thấp/Unknown, **không được kết luận PP0 hoặc tự mở Cabinet Ambush**.

Về team: dùng `TeamPacingPressure = aggregation(verified PlayerPressure snapshots, active encounter severity, weakest-player guard, team capability)` và lưu cả phân phối `min/max/spread`, không chỉ trung bình. `PlayerPressure` là **áp lực vừa chịu**, không phải Historical Skill hay năng lực; pressure P4 không có nghĩa người chơi kém.

**Tier ứng viên cần duyệt:** `PP0 0.00–0.24`, `PP1 0.25–0.44`, `PP2 0.45–0.64`, `PP3 0.65–0.84`, `PP4 0.85–1.00`. Áp dụng **hysteresis + dwell + time decay + anti-farm** (chi tiết ở bảng chờ duyệt), không nhảy tier qua lại vì một frame. `StalkerTargetHistoryMemory` đang có pressure decay lịch sử Stalker; đây là **nguồn thành phần S**, không tự thay thế tổng PP. `playerPressureStopThreshold=0.85` hiện có cũng **không tự động** là production threshold của PP4.

#### 4.3. Fair target selection, Vision/Hearing và Core/Seek

Stalker vẫn tuân thủ **LOS, field of view, khoảng cách, NavMesh, phòng an toàn, nguồn âm và danh sách target hợp lệ**. Player áp lực thấp được cộng **ưu tiên có giới hạn** trong nhóm *đã eligible*; không bị lộ vị trí, tạo tiếng động giả hoặc bị Stalker teleport tới chỉ vì PP thấp. Khi tất cả Player PP cao, **giảm global encounter demand**, không chuyển toàn bộ sang người PP thấp nhất. Không buộc 25% số Chase/người mà xét opportunity, vị trí và thời gian hồi phục.

| PP | Vision multiplier (ứng viên) | Hearing multiplier (ứng viên) | Ý nghĩa |
|---|---:|---:|---|
| PP0 | 1.10 | 1.10 | Tăng nhận biết nhẹ nếu target/cue hợp lệ |
| PP1 | 1.05 | 1.05 | Tăng rất nhẹ |
| PP2 | 1.00 | 1.00 | Baseline |
| PP3 | 0.90 | 0.90 | Bớt tập trung |
| PP4 | 0.80 | 0.80 | Bảo vệ áp lực cao; không xóa mục tiêu đang bị Chase hoặc target visible gần |

Các multiplier chỉ là **ứng viên**; cần định nghĩa áp vào *effective detection range* hay *acquire sensitivity* cho Vision, và *attributable player sound attenuation/range* cho Hearing. Không làm mất tác dụng các tool phát noise độc lập (NoiseMaker), không sửa âm thanh vật lý của scene. Freeze multiplier liên quan encounter ở lúc acquire, thay đổi tier chỉ áp sau `soft-reacquire`/safe transition; không làm Player đột ngột biến mất trong LOS hay huỷ attack animation.

- **Core Carrier:** giữ bonus `IsObjectiveCarrier`; PP3/PP4 chỉ bớt *tái ưu tiên* hoặc tăng recovery, không xóa cơ chế Core Pursuit. `Minion forced-drop` + `Stalker target` phải chia causal credit thay vì cùng kích hoạt hai buff.
- **Seek:** tiếp tục `SeekPlayersAfterSeconds`, dựa trên room evidence, patrol và last-known information hợp lệ; PP0 không được làm lộ tủ/vị trí ẩn, PP4 đang hồi phục không bị seek đúng tọa độ. Safe Room là no-go.
- **Reveal:** khi Player đã bị kéo **hợp lệ**, `hideSpotRevealTrackSeconds`/`hideSpotRevealGraceSeconds` có thể map theo PP tại thời điểm reveal, nhưng P4 đang bảo vệ thì **không có reveal event giả**.

#### 4.4. Hidden Box — P4 miễn kéo ra dù đang bật đèn (ĐÃ CHỐT)

**Quyết định bắt buộc:** Với Player `PP4` có `ActiveHideInspectionProtection` hợp lệ, Stalker **không được force-exit** từ tủ trong cửa sổ bảo vệ, **bất kể** Stalker đã thấy Player bước vào tủ hoặc người đó bật đèn. Stalker vẫn có thể nghi ngờ/đến điều tra nếu có clue hợp lệ, nhưng phải chấm dứt *inspection → force exit* đối với Player được bảo vệ; có thể chuyển sang patrol/search tại chỗ khác sau reaction thích hợp.

**Điểm đặt guard bắt buộc:** Tại Host `StalkerUnityHideSpotAdapter.TryResolveInspection()` (hoặc một authoritative action gate chung **trước mọi** `NetworkPlayerMovement.TryForceExitHidingAuthoritative()`), kiểm tra `PlayerId + HideSpotStableId + protectionEpoch + windowRemaining + pressure source identity` **trước force-exit và trước side effect**. Nếu được bảo vệ: return `Protected/Skipped` theo contract mới hoặc lý do rõ; **không force exit, không lockout, không phát reveal, không cộng hide-reveal penalty**. `StalkerHidingInvestigation` phải xử lý Protected khác với Empty/Failed nhằm tránh inspect loop. Client visual là render của server decision, không tự định đoạt.

**Đèn bật:** chỉ bỏ qua/giảm *flashlight clue gắn trực tiếp với occupant PP4* cho quyết định kiểm tra/kéo trong protection; **không xoá scene light, visibility của những người khác, tool cue hay nguồn âm riêng**. Ngay cả khi một clue độc lập đưa Stalker tới tủ, `force-exit guard` vẫn phải ngăn kéo occupant PP4. Trường hợp nhiều occupant hoặc target khác không được tự hưởng exemption; contract hiện tại có thể chỉ hỗ trợ một occupant/tủ, phải test theo thực tế.

**Cửa sổ bảo vệ:** trigger là host xác nhận PP4 + hide-enter/hide-stay hợp lệ; dùng `ProtectionToken` và `start/endTick`, `minHold`, per-player/per-hide-spot cooldown, giới hạn episode/phase. Token **snapshot-lock** đến `endTick` hoặc exit hợp lệ; không bị tự rút khi PP4 tạm tụt xuống PP3 trong lúc tủ đang bị điều tra. **Không duy trì miễn kéo vô thời hạn** bằng cách farm encounter hay toggling đèn. P4 bảo vệ ngay cả khi vào tủ trong LOS; không đợi Backend approve tại giây đó. Khi hết thời hạn, **không tự bật cưỡng chế kéo ngay** nếu Stalker vẫn đứng sát tủ: áp `post-protection grace`, bỏ stale investigation target, chỉ inspection mới sau reacquisition với clue hợp lệ. Khi Player chủ động rời tủ, bảo vệ không còn áp vào world/outside hiding; recovery target fairness vẫn có thể tiếp tục.

**Tier khác:** `PP0–PP2` theo baseline; `PP3` có thể hưởng protection nhẹ có hạn nếu sau này được duyệt, **không đảm bảo miễn kéo**. Không dùng PP4 để sửa hậu quả combat đã committed trước lúc token bắt đầu hoặc để miễn mọi damage. Không được bỏ qua Safe Room/hide spot lockout của baseline theo hướng gây bất lợi mới.

**Post-reveal candidate (chưa duyệt):**

| Tier lúc reveal | Track time | Chase/reacquire grace | Ghi chú |
|---|---:|---:|---|
| PP0 | 0.60 s | 1.00 s | Tăng tracking nhẹ so với baseline |
| PP1 | 0.45 s | 1.25 s | Tăng nhẹ |
| PP2 | **0.35 s** | **1.50 s** | Baseline serialized đã xác minh |
| PP3 | 0.20 s | 2.00 s | Relief |
| PP4 | Không reveal khi token active | ≥ baseline nếu reveal đã diễn ra hợp lệ ngoài window | Không huỷ attack/reveal đã committed |

#### 4.5. Jump In/Jump Out và Cabinet Ambush — trình tự ĐÃ CHỐT

**Không gộp ba đường gameplay:** (A) Special Encounter hiện hữu khởi phát từ `StalkerDownedPlayerFact`; (B) Fair Jump In chọn entry ít dồn pressure hơn khi Special hợp lệ; (C) **Cabinet Ambush mới** là một encounter có trigger độc lập, action/state riêng và shared global special-safety budget. Tránh tái sử dụng `Arm(downedFact)` bằng fact giả để gọi Ambush.

**Cabinet Ambush state flow (Host-authoritative):**

```text
Verified cabinet clue + occupant eligible PP0/PP1 + team non-critical
 -> Candidate from CabinetAmbushPolicy (NO omniscient position lookup)
 -> Global pressure/cooldown/cap/one-special-at-a-time gates
 -> Validate authored JumpIn entry (min/max distance + complete NavMesh path
    + door/escape/SafeRoom + no overlap + target legitimacy)
 -> Reserve spot + cue plan (visual/audio + reaction window)
 -> Jump In NEAR cabinet; play warning / allow reaction
 -> Stalker physically approaches cabinet, with interruptible search state
 -> Fresh authoritative inspection: occupant still present? still low PP?
    HideInspectionProtection active? clue still valid? door/route still valid?
 -> If YES: regular inspection -> force exit -> post-reveal grace / chase
 -> If NO: abort, backoff, patrol/search using only evidence actually available
 -> Audit outcome/cooldown/telemetry and release reserved entry
```

**Bắt buộc:** `PP0/PP1` là *điều kiện chưa đủ*; phải có **occupancy evidence hoặc clue hợp lệ** từ gameplay (quan sát vào tủ, ánh đèn liên quan LOS/occlusion, noise/footstep có provenance, hide-spot search memory). Host giữ occupant state để *revalidate* sau khi đã chọn một tủ từ clue; **không được scan tất cả tủ tìm ai PP thấp rồi teleporter tới**. Chỉ Jump In tại entry gần tủ, **không** xuất hiện trong tủ/hitbox/player; có **warning nghe/nhìn rõ, window đủ để phản ứng và đường rút thật**. Stalker phải đi tới tủ; nếu Player rời tủ, đổi tier lên PP3/4, hoặc vào protected state trong lúc Stalker tiếp cận thì **huỷ cưỡng chế kéo**, tiếp tục tình huống hợp lệ khác hoặc bỏ encounter. Không chồng CHASE/ATTACK/RECOVER/Special/Down-sensitive objective/Final Hunt pressure spike. Nếu đang ở Zone 3 Adaptive có Minion 1, không bật Cabinet Ambush mới gây pressure spike cùng thời điểm trừ khi cross-axis budget đã được hiệu chuẩn và approval nêu rõ; pilot khuyến nghị khoá stacking.

**Cooldown và warning:** `StalkerSpecialEncounterSettings.CooldownSeconds` hiện ép **≥300 s**; `JumpOutDurationSeconds ≥5 s`, `JumpInDurationSeconds ≥3 s`. Các giá trị này thuộc Special hiện hữu và **không được giảm ngầm** vì policy mới. Ambush phải có cooldown/cap/timing độc lập hoặc shared gate nêu rõ (chưa chốt số), tính toàn trận và per-player để chặn liên tiếp; `warningLead`, `entryMin/maxDistance`, `ambushApproachTimeout`, `postAmbushRecovery`, `failedAttemptBackoff` phải version hóa. Nếu cảnh báo/đường rút bị block trên Host hoặc Client không thể nhìn/nghe warning theo điều kiện kiểm thử, từ chối Ambush; không coi status backend Approved là đã bảo đảm escape route.

#### 4.6. Kịch bản Stalker v0.2 — bộ test/review chính thức

**Phân phối áp lực từng Player (`PF`)**

| ID | Khi nào | Expected policy |
|---|---|---|
| PF-01 | Một Player chịu Chase liên tục | Hạ retarget priority và dành recovery opportunity |
| PF-02 | Player không bị gặp lâu | Chỉ tăng nhẹ lựa chọn khi target hợp lệ |
| PF-03 | Hai Player đều trong LOS | Ưu tiên PP thấp hơn khi các tín hiệu khác tương đương |
| PF-04 | Vừa Down/Revive | Recovery grace; tránh chọn lại ngay |
| PF-05 | Player đang bị Minion gây rối | Không chồng Stalker pressure lên người đó |
| PF-06 | PP4 nhưng giữ Core | Relief ưu tiên có giới hạn, không xóa Core gameplay |
| PF-07 | Chỉ còn một người Alive | Không nâng aggression để bù số người |
| PF-08 | Một người làm hầu hết objective | Không coi engagement làm lý do nhắm liên tục |
| PF-09 | Tất cả người chơi PP3–PP4 | Đề xuất relief cấp toàn đội / HOLD tăng khó |
| PF-10 | Tất cả PP0–PP1 | Chỉ có thể đề xuất tăng nhẹ sau opportunity/skill/budget gate |
| PF-11 | Người chơi farm target/chase/đèn/tủ | Dedup + cap + hysteresis, không buff PP vô hạn |
| PF-12 | Chase exposure cao nhưng không chịu hậu quả | Không suy tự động thành Critical, tách stress/severity |

**Vision/Hearing (`VH`)**

| ID | Khi nào | Expected policy |
|---|---|---|
| VH-01 | PP0 nằm trong LOS/eligibility | Candidate detection sớm hơn nhẹ |
| VH-02 | PP4 ở xa | Candidate effective range ngắn hơn |
| VH-03 | PP4 chạy có âm hợp lệ | Giảm nhẹ attributable hearing range, không xoá âm vật lý |
| VH-04 | PP0 chạy | Có thể nghe xa hơn nhưng không xuyên vật cản |
| VH-05 | PP4 dùng NoiseMaker | Tool noise độc lập vẫn có hiệu lực |
| VH-06 | Chase đã bắt đầu | Không thay multiplier đột ngột/hủy Chase |
| VH-07 | Pressure chuyển PP2→PP3 | Hysteresis, không target flip-flop |
| VH-08 | Stalker nhìn rõ Player ở cự ly sát | Không tạo tàng hình tức thời ngoài Hide protection |
| VH-09 | Cue âm thanh nhiều Player | Không tự gán âm cho PP thấp nhất |
| VH-10 | Protection/time decay kết thúc | Hồi dần hệ số, không instant spike |

**Hidden Box (`HB`)**

| ID | Khi nào | Expected policy |
|---|---|---|
| HB-01 | PP0 bị thấy vào tủ | Inspection như baseline |
| HB-02 | **PP4 bị thấy vào tủ**, token active | **Không force exit**, có thể nghi ngờ/đến gần |
| HB-03 | **PP4 bật đèn trong tủ**, token active | **Không force exit**, suppress player-linked clue theo policy; giữ independent cues |
| HB-04 | PP1 bật đèn | Clue bình thường |
| HB-05 | PP3 bật đèn | Có thể giảm điều tra, không đảm bảo miễn kéo |
| HB-06 | PP4 từng bị kéo khỏi tủ | Không lặp force exit trong recovery/protection |
| HB-07 | PP0 lặp cùng tủ | Hide-spot memory hoạt động nếu clue hợp lệ |
| HB-08 | PP4 có đồng đội gây noise ở cạnh | Không miễn detection cho đồng đội |
| HB-09 | PP4 rời tủ | Kết thúc hide inspection protection, vẫn có grace |
| HB-10 | Tủ không có valid clue | Không lộ tọa độ occupant từ PP score |
| HB-11 | Token P4 hết lúc Stalker đứng trước tủ | Không tự force exit; grace + evidence mới |
| HB-12 | Final Hunt, PP4 Critical | Chỉ bảo vệ theo quota/window đã duyệt, không vô hạn |
| HB-13 | Protected inspection bị nhiều runtime path gọi | Mọi path đều chặn force-exit, không lockout/reveal/false pressure gain |
| HB-14 | Player vừa PP3→PP4 khi nằm tủ | Token bắt đầu theo Host rule; không sửa force exit đã committed |
| HB-15 | Source PP Unknown / mất authority | Không cấp token mới, không bật Ambush; **giữ token P4 hợp lệ còn TTL**, log và đồng bộ lifecycle |

**Reveal Tracking (`HT`)**

| ID | Khi nào | Expected policy |
|---|---|---|
| HT-01 | PP0 bị kéo hợp lệ | Track candidate 0.60 s, grace 1.00 s |
| HT-02 | PP2 bị kéo hợp lệ | Baseline 0.35 s/1.50 s |
| HT-03 | PP3 bị kéo hợp lệ | Track 0.20 s, grace 2.00 s |
| HT-04 | PP4 miễn kéo | Không phát fake reveal/chase/lockout |
| HT-05 | PP4 mới đạt ngưỡng sau reveal | Không retroactive undo combat, safe transition |
| HT-06 | Player Down giữa reveal | Không stack negative bonus |
| HT-07 | Target disconnect | Clear authoritative target và stale hide inspection |

**Core/Seek (`CC`, `SK`)**

| ID | Khi nào | Expected policy |
|---|---|---|
| CC-01 | Core Carrier PP thấp | Cho ưu tiên carrier bình thường |
| CC-02 | Core Carrier PP2 | Baseline |
| CC-03 | Carrier bị Chase liên tục | Giảm tái ưu tiên |
| CC-04 | Minion vừa forced-drop Core | Không cộng Stalker aggression ngay |
| CC-05 | Carrier PP4 | Relief window nhưng không xoá Core system |
| CC-06 | Chuyển Core cho người khác | PP riêng không chuyển theo Core |
| CC-07 | Các thành viên luân phiên cầm Core | Fairness theo encounter opportunity |
| CC-08 | Chỉ một người giữ Core, người khác chưa gặp AI | Chọn người khác khi eligible, không cưỡng chế |
| SK-01 | Cả đội lâu không gặp Stalker | Seek theo verified room evidence/patrol |
| SK-02 | PP0 ở khu có evidence hợp lệ | Có thể nâng hướng tuần tra một chút |
| SK-03 | PP4 đang hồi phục trong tủ | Không seek đúng tọa độ tủ từ pressure |
| SK-04 | A bị gặp nhiều, B chưa gặp | Cân nhắc B nếu evidence tương đương |
| SK-05 | Đội vừa thoát Chase dài | Hoãn Seek trong recovery |
| SK-06 | Player ở Safe Room | Không seek vào no-go area |
| SK-07 | Không có occupancy/evidence hợp lệ | Patrol bình thường |

**Jump In/Out (`JP`) và Cabinet Ambush (`CAB`)**

| ID | Khi nào | Expected policy |
|---|---|---|
| JP-01 | Team PP cao | Không tạo special mới |
| JP-02 | Một Player PP4, người khác PP0 | Không nhắm entry gây thêm shock lên PP4 |
| JP-03 | PP0 ở khu entry hợp lệ | Ưu tiên entry nhưng không phá fairness |
| JP-04 | PP4 vừa trải qua special | Per-player cooldown |
| JP-05 | Jump gần A nhiều lần | Penalize recent pressure to A |
| JP-06 | Entry không có escape route | Reject |
| JP-07 | Entry quá gần occupant | Reject |
| JP-08 | Entry sát Safe Room | Respect no-go/entry rules |
| JP-09 | Jump In | Warning + reaction window ≥ min đã calibrate |
| JP-10 | Global pressure Critical | HOLD special |
| JP-11 | Có Down mới trong special | Không overlap hai active encounters |
| JP-12 | Cooldown hết, evidence thiếu | HOLD |
| CAB-01 | PP0 trong tủ, Stalker có clue hợp lệ | Candidate Ambush |
| CAB-02 | PP0 trong tủ, không clue | Reject; không scan hidden occupant theo PP |
| CAB-03 | PP1 bật đèn, source clue hợp lệ | Candidate nhưng vẫn LOS/occlusion + warning |
| CAB-04 | PP3/PP4 đang trong tủ | Không chọn Cabinet Ambush |
| CAB-05 | PP4 bật đèn, protection active | **Reject Ambush + force exit** |
| CAB-06 | Player rời tủ trước inspection | Abort force-exit, không teleport bám target |
| CAB-07 | Player vừa bị kéo khỏi tủ | Cooldown chống repeat ambush |
| CAB-08 | Tủ sát Safe Room | Reject khi route/zone cấm |
| CAB-09 | Player Down trong wind-up Ambush | Abort, lifecycle ưu tiên |
| CAB-10 | Hết cooldown nhưng team pressure cao | HOLD |
| CAB-11 | Không có entry/route/escape hợp lệ | Abort, không teleport trực tiếp sát tủ |
| CAB-12 | Sau Ambush Player từ PP0 thành PP3/4 | Recovery và chống retarget |
| CAB-13 | Có cue nhưng Client không quan sát được warning theo test policy | Reject/abort, không kéo bất ngờ |
| CAB-14 | Đến sát tủ thì protection mới active | Guard trước force exit → abort reveal |
| CAB-15 | Ambush đồng thời với Zone3 Minion spawn/special khác | Reject theo shared global pressure + exclusivity gate |
| CAB-16 | Host migration/reconnect/retry | Idempotent encounter ID, không double-teleport/double-force-exit |

#### 4.7. Parameter baseline — ĐỀ XUẤT REV02, ĐANG CHỜ DUYỆT

Các giá trị từng được để mở ở Revision 01 nay đã được **điền một cấu hình thử nghiệm cụ thể**, có bound, điều kiện kích hoạt và cách rollback. **Không được hiểu là các giá trị đã được người dùng chốt.** Mục XV có bảng key đầy đủ; XVI là policy logic và exception; XVII là tình huống end-to-end; XVIII là ma trận duyệt/triển khai.

| Nhóm | Rev02 đề xuất mặc định | Ghi chú |
|---|---|---|
| Player Pressure | `S=.30, M=.20, D=.25, R=.15, O=.10`, tổng 1; PP4 từ `0.85` | Chỉ tính event verified, không có dữ liệu = `UNKNOWN` |
| Thời gian | rolling 120s; episode history 300s; minimum observation 30s | Ổn định tier bằng hysteresis/dwell; urgent relief theo down riêng |
| Sensory | Vision và Hearing PP0–4: `1.08 / 1.04 / 1.00 / 0.92 / 0.85` | Chỉ trong acquisition mới; không xuyên LOS/noise rules |
| Hidden Box P4 | Token 10s; tối đa 1 lần/120s/player; post-protection grace 3s | **ĐÃ CHỐT** miễn kéo kể cả bật đèn; số giây chỉ là đề xuất |
| Cabinet Ambush | PP0–1; warning ≥2.5s; entry route 8–12m; own cooldown 420s; 1/zone, 2/match | **ĐÃ CHỐT** flow cảnh báo→Jump gần→tiếp cận→kéo; số chỉ đề xuất |
| Special hiện hữu | Không hạ floor cooldown 300s, JumpOut 5s, JumpIn 3s | Contract source đang tồn tại, không mở khóa ngầm |
| Cross-axis budget | 1 positive pressure token đồng thời; không stack Stalker + Minion + Tool squeeze | Mức thử nghiệm bảo thủ cho v0.2 |

**Thứ tự áp dụng khi có mâu thuẫn:** bất biến ở mục 0 → các quyết định **ĐÃ CHỐT** → policy Rev02 ở XV–XVIII (*nếu được phê duyệt*) → mô tả candidate trước đó. Mọi thay đổi được version hóa thành `AED_STALKER_POLICY_V0_2_REV02_CANDIDATE`, chỉ nâng sang `APPROVED` bằng quyết định riêng.


## VI. MINION ADAPTATION — ZONE 3 ADAPTIVE POLICY RIÊNG

### 1. Baseline production đã xác minh (không sửa Fixed)

| Zone | `MatchDifficultyProfiles.Normal` | Adaptive v0.2 target cap | Hướng được duyệt ban đầu |
|---|---:|---:|---|
| Zone 1 | **1** | 0 hoặc 1 | Chỉ relief từ 1→0; không vượt Normal |
| Zone 2 | **2** | 1 hoặc 2 | Chỉ relief từ 2→1; không vượt Normal |
| Zone 3 | **0** | **0 hoặc 1** | Adaptive-only increase 0→1 khi đủ chứng cứ, relief 1→0 |

**Đã xác minh và cần sửa research code:** `AEDMinionDirectorV1.NormalBaselineCap(3)` hiện trả `1`, nhưng `MatchDifficultyProfiles.Get(Normal).Zone3MinionCap = 0`. `AEDv2Catalog` mới có Zone1/Zone2 MinionCap và `PreMatchOnly=true`; `AEDv2GameplayBridge` giữ `n.Zone3MinionCap`, do đó Zone3 Adaptive cap 1 **chưa thể apply**. `PlayerSpawner.MaintainCreepMinionPopulation()` có thể đọc applied v2 plan nhưng hiện chỉ lấy Zone3 cap từ profile. Phải nâng đúng **catalog/client/backend/bridge/spawner** đồng bộ theo contract mới.

### 2. Zone 3 policy cụ thể

`Zone3AdaptiveMinionPolicyV1` là policy riêng, không đổi baseline config:

- Chỉ sử dụng trong `Adaptive` + gameplay flag + current match verified; `Fixed`, `Easy`, `Hard` không nhận delta này.
- Default = 0 kể cả Adaptive cold start; chỉ lên 1 nếu team pressure vẫn Low/VeryLow qua đủ window, historical confidence đầy đủ, không có weakest struggling, objective vẫn tiến triển, trong global budget còn một increment.
- Chỉ cho spawn lần đầu ở safe-window sau Zone 3 entry, có tối thiểu khoảng buffer thời gian/không gian để player định hướng (ngưỡng thử nghiệm, chưa khóa). Ưu tiên `Zone3FindFrigate`; khi `Zone3PushFrigate` phải có kiểm tra đặc biệt để tránh push/charge/refuel.
- Khi cap target từ 1→0: không force-despawn Minion đang sống, đang Harass/Track hay đang giữ Tool/Core. Chỉ chặn replacement sau despawn, hoặc giảm cơ chế encounter tương thích sau lần resolve.
- Nếu không có verified zero-population census, không được approve cap change hoặc claim đã thực hiện; số 0 phải là census thật, không phải thiếu event.
- Kết thúc Zone3 hoặc vào Final Hunt/Escape: không spawn Minion mới bất ngờ; giữ nguyên chính sách baseline transition.
- Tất cả spawn phải tuân theo min distance player/Stalker, route, Safe Room, NavMesh, population/ownership census và `MinionSpawnCooldown` độc lập. `PlayerSpawner` hiện kiểm tra spawn mỗi 3 giây là runtime maintenance, **không phải** pacing approval cooldown.

**Lưu ý quan trọng về trải nghiệm:** Thêm Minion đầu tiên ở Zone 3 (0→1) là thay đổi đáng kể hơn một "nấc nhỏ". Vì vậy pilot **không cho đồng thời Stalker +1**, nhưng vẫn cho phép *handoff* qua hai transaction riêng (Stalker +1→0; sau observation window, Zone3 Minion 0→1). Điều này có trade-off là chậm tăng thử thách hơn nhưng tránh spike.

### 3. Minion Behavior

Các `VisionRange`, `TrackSpeed`, `HarassSpeed`, `AttackCooldown`, `AlertCooldown` hiện nằm trong `AEDMinionDirectorV1` **research-only**. Đề xuất **chưa kích hoạt đồng thời cả behavior tuning và cap tuning** ở bản đầu; ưu tiên cap/respawn interval vì đơn giản, dễ đo và tránh nhiều tác dụng chồng lên nhau. Vẫn thu telemetry behavior để calibration.

### 4. Kịch bản Minion

| ID | Tình huống | Hành động |
|---|---|---|
| MN-01 | Người chơi bị Minion tấn công liên tục | Giảm tần suất encounter hoặc cap sinh mới |
| MN-02 | Minion liên tục cướp TeamTool | Giảm áp lực Minion đối với Tool và ưu tiên hỗ trợ recovery |
| MN-03 | Minion gây forced-drop Core nhiều lần | Giảm áp lực Minion, không mặc định giảm Stalker |
| MN-04 | Minion slow khiến Player bị Stalker bắt | Giảm Minion trước, giữ Stalker |
| MN-05 | Nhiệm vụ không tiến triển vì Minion liên tục cản trở | Tạm giảm số Minion sinh thêm hoặc thời gian harass nếu contract cho phép |
| MN-06 | Người chơi xử lý Minion bằng flashlight tốt | Giữ một thời gian, sau đó mới cân nhắc tăng nhẹ challenge |
| MN-07 | Đội thường né Minion thành công mà không có threat thực tế | Có thể tăng nhẹ encounter pressure sau khi xác minh skill và pressure |
| MN-08 | Zone 1 đang có 1 Minion nhưng đội rất khó khăn | Đặt target cap 0 cho những lần spawn tiếp theo; không force-despawn quái đang chiến đấu |
| MN-09 | Zone 2 đang có 2 Minion, đội thường xuyên bị gián đoạn | Giảm target cap xuống 1 |
| MN-10 | Adaptive Zone 3 đã kích hoạt 1 Minion, đội kiệt sức | Đặt target cap về 0; không force-despawn khi đang encounter |
| MN-11 | Đội xử lý Stalker rất giỏi nhưng Minion kém | Không tăng Minion chỉ vì kết quả Stalker tốt |
| MN-12 | Stalker đạt +1, đội vẫn quá dễ | Chỉ xét Minion nếu tổng budget còn, được xác minh và safe window hợp lệ |
| MN-13 | Đội đang có active Minion encounter | Không spawn thêm Minion để dồn áp lực |
| MN-14 | Nhiều Minion bị tiêu diệt nhanh | Không respawn lập tức; giữ khoảng nghỉ |
| MN-15 | Người chơi còn một người Alive | Ưu tiên Relief/Hold, không tăng Minion |
| MN-16 | Minion tồn tại nhưng không tương tác người chơi | Không coi đó là bằng chứng đội đã khắc chế Minion |
| MN-17 | Thiếu source event về số Minion thực tế | HOLD; không áp dụng population adjustment |
| MN-18 | Player đang thoát sau Final Hunt | Không đột ngột sinh Minion ngay tuyến escape |
| MN-19 | Stalker đã gây Critical pressure | Minion không được đồng thời tăng pressure |
| MN-20 | Minion gây nhiều áp lực nhưng Stalker ít gặp | Giảm trục Minion, không buộc phải tăng Stalker để cân bằng |

### 5. Kịch bản Minion bổ sung v0.2

| ID | Tình huống | Phản ứng AED |
|---|---|---|
| MN-21 | Zone 3 Adaptive cap 0, đội mạnh và pressure rất thấp sau nhiều cửa sổ đã verified | Có thể nâng target cap lên 1 tại safe window, nếu Stalker đã trở về 0 khi cần handoff và không vượt budget |
| MN-22 | Zone 3 Fixed/Normal hoặc mode không hợp lệ | Giữ cap 0, từ chối quyết định Adaptive Minion |
| MN-23 | Đang refuel/push/charge, Minion/Stalker active encounter hoặc ngay tuyến escape | HOLD, không spawn Minion bất ngờ |
| MN-24 | Thiếu zero-population world census hoặc evidence stale | HOLD; không suy 0 Minion từ việc thiếu event |
| MN-25 | Plan cap 1 đã commit nhưng spawn cadence/đường NavMesh/khoảng cách chưa hợp lệ | Chưa spawn; theo dõi pending placement và ghi lý do, không tự bỏ qua safety gate |

### 6. Nguyên tắc giảm/tăng population chung

- Giảm: thay `target cap` cho future spawn, tăng respawn gap, không force-despawn đang encounter, không làm mất Tool/Core, có thể kết hợp support Tool sau một observation window.
- Tăng: chỉ với current pressure thấp có evidence; không spawn ngay sau player/entry, trong Safe Room hoặc tuyến escape; không vượt global cap và phải có ledger + receipt.
- Không dùng số Minion bị tiêu diệt đơn thuần làm skill score. Đội tránh Minion vì không gặp encounter thì `NoOpportunity`.
- Không để `PlayerSpawner` tự đổi target cap dựa trên Unity `Time.time`; timer chỉ phục vụ spawn maintenance sau khi Host đã commit cấu hình có approval.

## VII. NHẬN DIỆN NGUYÊN NHÂN NHIỆM VỤ HOÀN THÀNH CHẬM

Đây là phần quan trọng để AED không chỉnh sai.

### 1. Phân loại nguyên nhân

| ID | Nguyên nhân thực tế | Bằng chứng cần có | AED nên làm |
|---|---|---|---|
| CA-01 | Chậm do Minion | Harass/slow/tool steal làm gián đoạn objective có causal link | Giảm Minion pressure |
| CA-02 | Chậm do Stalker | Confirmed chase/attack gây gián đoạn thao tác | Giảm Stalker pressure |
| CA-03 | Chậm do thiếu Tool | World/inventory census và đường tiếp cận cho thấy Tool thiếu | Cấp hoặc chuyển hỗ trợ hợp lệ |
| CA-04 | Chậm vì Tool quá xa | Đường NavMesh dài, người chơi chưa thể tiếp cận tài nguyên cần thiết | Bố trí Tool hỗ trợ gần hơn ở lượt/Zone phù hợp |
| CA-05 | Chậm vì người chơi đang khám phá | Không có threat và objective không bị chặn | HOLD |
| CA-06 | Chậm vì chưa hiểu puzzle | Không có đủ evidence xác định threat/resource là nguyên nhân | HOLD; có thể đề xuất hint system riêng |
| CA-07 | Chậm vì nhiều Player Down | LifeState/Revive evidence rõ ràng | Recovery/support, không tăng encounter |
| CA-08 | Chậm vì bug tương tác hoặc đường bị chặn | Runtime errors/invalid objective state | Báo lỗi, không dùng AED che bug |
| CA-09 | Chậm do mạng lag/disconnect | Fusion/session evidence | HOLD và chờ khôi phục |
| CA-10 | Không phân biệt được nguyên nhân | Source incomplete hoặc attribution không chắc chắn | HOLD |
| CA-11 | Nhiều nguyên nhân cùng xảy ra | Có evidence riêng theo từng nguồn | Ưu tiên nguyên nhân gây hậu quả lớn nhất, một thay đổi |
| CA-12 | Người chơi cố ý trì hoãn hoặc đứng AFK | Không có eligible gameplay opportunity | Không tính là kỹ năng yếu |

### 2. Ví dụ thực tế

**Tình huống A:**

Player đang mang Core. Minion làm rơi Core hai lần và Stalker chỉ truy đuổi một lần.

AED xác định Minion gây gián đoạn chính, nếu event có liên kết nguyên nhân hợp lệ.

Hành động:

- Giảm target cap hoặc hoạt động Minion ở safe window.
- Giữ Stalker.
- Cân nhắc hỗ trợ Tool recovery/defense.
- Không giảm tất cả difficulty.

**Tình huống B:**

Player không hoàn thành relay vì Stalker liên tục CHASE, trong khi Minion gần như không tham gia.

Hành động:

- Tăng recovery window hoặc giảm một nấc Stalker.
- Giữ Minion.
- Không đặt thêm First Aid nếu inventory vẫn đủ và chưa có nhu cầu thực.

**Tình huống C:**

Player đã khám phá nhiều nhưng chưa tìm Scanner cần dùng.

Hành động:

- Nếu đã xác minh Tool thiếu/không thể tiếp cận, cấp support tại điểm hợp lệ được Host kiểm tra và Backend duyệt.
- Nếu Scanner vẫn có thể tiếp cận và người chơi chỉ chưa tìm thấy, có thể áp dụng chính sách vị trí hỗ trợ cho Zone sau.
- Không tự kết luận người chơi yếu về combat.

**Tình huống D:**

Player hoàn thành objective nhanh nhưng bị Down nhiều lần.

Hành động:

- Không coi team là quá giỏi chỉ vì objective nhanh.
- Giữ hoặc giảm combat pressure.
- Có thể giữ Tool placement bình thường.

---

### 3. Quy tắc attribution mới v0.2

Cần phân biệt **tương quan** và **nguyên nhân có chứng cứ**. `objective_stall` không tự suy ra do Minion hay Stalker. Chỉ gán `VerifiedCause` khi có `ObjectiveUnitId`, khoảng thời gian gián đoạn, source actor/network ID, event sequence/tick và tác động (slow, drop, interrupt, down, resource loss) liên kết nhất quán; nếu không đủ, dùng `Unknown`.

- Nếu Minion slow sau đó Stalker gây Down, ghi `Compound(Minion,Stalker)` và hậu quả riêng của từng tác nhân, không gán 100% cho một trục.
- Ưu tiên tác động giảm khó không đảo ngược khi bằng chứng thiếu, nhưng chỉ sau safety + approval gate; thiếu căn cứ cho tăng khó luôn HOLD.
- `AFK`, disconnect, debug skip, bug map, puzzle chưa hiểu và người chơi khám phá không được biến thành skill failure.
- Event được dedup theo canonical occurrence/episode ID. Cần denominator là opportunity đã resolve, không lấy số lần bị attack/đánh rơi Tool đơn lẻ làm tỷ lệ kỹ năng.
- Chỉ phê duyệt gameplay khi Backend có thể xác minh source linkage hoặc phiên bản hợp đồng evidence cho phép kiểm chứng tương ứng. Các scorer nghiên cứu hiện có không được mặc nhiên coi là source đã chứng thực.

## VIII. KỊCH BẢN THEO BỐN GIAI ĐOẠN

### Phase 1 — Zone 1 / Core Objective

Mục tiêu: Giới thiệu threat và đánh giá khả năng thực tế của đội.

| ID | Tình huống | Phản ứng AED |
|---|---|---|
| Z1-01 | Đội mới, không có historical skill đáng tin | Baseline hoặc hỗ trợ nhẹ có kiểm chứng |
| Z1-02 | Đội liên tục bị Stalker phát hiện | Giảm phát hiện một nấc tại safe window |
| Z1-03 | Minion liên tục làm rơi Core | Giảm Minion pressure |
| Z1-04 | Đội thiếu Tool ngay đầu Zone | Ưu tiên hỗ trợ gần Entry/Corridor |
| Z1-05 | Đội rất mạnh từ các trận trước | Đặt Tool tùy chọn xa hơn một mức |
| Z1-06 | Đội hoàn thành Core rất nhanh mà không có encounter | Chưa tăng Stalker/Minion nếu opportunity chưa đủ |
| Z1-07 | Đội vừa vượt được encounter khó | Giữ một khoảng nghỉ trước khi tăng lại |
| Z1-08 | Chuẩn bị sang Zone 2 | Chốt evidence và đề xuất cấu hình Zone 2 |

### Phase 2 — Zone 2 / Terminal, Relay, Security Hold

Mục tiêu: Điều phối áp lực trong lúc đội cần thực hiện nhiều thao tác.

| ID | Tình huống | Phản ứng AED |
|---|---|---|
| Z2-01 | Đội bước vào Zone 2 với pressure cao | Ưu tiên Tool hỗ trợ gần `Z2_ENTRY` |
| Z2-02 | Đội bị Minion ngắt thao tác relay nhiều lần | Giảm Minion pressure |
| Z2-03 | Đội bị Stalker truy đuổi liên tục quanh terminal | Giảm Stalker pressure |
| Z2-04 | Đội hoàn thành relay tốt, inventory nhiều | HOLD; chưa tự tăng nếu encounter evidence thấp |
| Z2-05 | Đội rất mạnh và áp lực hiện tại thấp | Có thể rải Tool tùy chọn xa hơn |
| Z2-06 | Player Down trong security hold | Không tăng difficulty và không sửa trạng thái encounter đang active |
| Z2-07 | Đội thiếu First Aid nhưng còn Tool phòng vệ | Cân nhắc một hỗ trợ recovery có điều kiện |
| Z2-08 | Chuẩn bị sang Zone 3 | Đánh giá nguồn gây khó chính trước khi quyết định |

### Phase 3 — Zone 3 / Frigate Mission

Mục tiêu: Tăng căng thẳng tự nhiên mà không dồn áp lực gây mất khả năng hoàn thành nhiệm vụ.

| ID | Tình huống | Phản ứng AED |
|---|---|---|
| Z3-01 | Đội vào Zone 3 với nhiều người vừa bị Down | Ưu tiên hỗ trợ ở `Z3_ENTRY` |
| Z3-02 | Đội yếu và thiếu Tool | Cấp một support item phù hợp |
| Z3-03 | Đội khó tìm Frigate | Không tăng monster vì objective progress thấp |
| Z3-04 | Đội bị Minion cướp Tool khi đang tìm tài nguyên | Giảm Minion pressure |
| Z3-05 | Stalker liên tục phá việc di chuyển/tiếp nhiên liệu | Tăng recovery window an toàn |
| Z3-06 | Đội thực hiện nhiệm vụ quá nhanh với đủ evidence | Chỉ tăng trong ngân sách tổng; Zone 3 Minion 0→1 là ứng viên nếu đủ gate |
| Z3-07 | Đội đang push Frigate hoặc thực hiện charge | Không apply hay spawn Minion trong interaction nhạy cảm |
| Z3-08 | Chuẩn bị Final Hunt | Chốt cấu hình Phase 4, không tạo difficulty spike |

### Phase 4 — Final Hunt và Escape

Mục tiêu: Tạo cao trào dựa trên trạng thái hiện tại, không biến thành hình phạt bất ngờ.

| ID | Tình huống | Phản ứng AED |
|---|---|---|
| F4-01 | Đội đã suy yếu từ Zone 3 | Giữ hoặc giảm nhẹ áp lực trước Final Hunt |
| F4-02 | Đội mạnh, đủ tài nguyên | Giữ thử thách cao trào baseline, không cần tăng liên tục |
| F4-03 | Có Player Down ngay trước Escape | Ưu tiên survival fairness |
| F4-04 | Đội đang CHASE | Không áp dụng thay đổi trực tiếp Stalker |
| F4-05 | Người chơi đang ở tuyến Escape bắt buộc | Không đặt Minion hoặc Tool chắn tuyến |
| F4-06 | TeamTool gần như hết | Chỉ cấp emergency support nếu policy và vị trí cho phép |
| F4-07 | Người chơi đang rất gần chiến thắng | Không tăng áp lực bất thường để ngăn thắng |
| F4-08 | Match kết thúc | Dừng adaptation, chốt telemetry và cập nhật profile |

---

### Kịch bản Stalker policy Rev 01 theo từng phase (chưa chốt thông số)

| ID | Phase / tình huống | Expected AED behavior |
|---|---|---|
| Z1-STK-01 | Zone 1: Player mang Core PP4 trốn tủ ngay trong LOS và bật đèn | P4 token → không kéo trong window, nhưng giữ Core objective và clue của Player khác |
| Z1-STK-02 | Zone 1: PP0 người khác có Core clue rõ, PP4 đang hồi phục | Chọn target bằng eligibility + Core + PP fairness, không có teleport do PP0 |
| Z2-STK-01 | Zone 2: Player PP4 vừa bị Minion slow khi làm Relay | Không stack Stalker target buff/ambush khi người đó đang bị gián đoạn |
| Z2-STK-02 | Zone 2: PP0 trốn tủ có verified light clue, area có path và warning | Cabinet Ambush candidate chỉ trong policy enabled và budget còn; Approach → fresh inspect |
| Z3-STK-01 | Zone 3: Adaptive Minion target 1 đang hoạt động | Không tạo Cabinet Ambush spike chồng Minion encounter trong pilot; áp shared budget |
| Z3-STK-02 | Zone 3: tủ gần Frigate objective có door locked không qua được | Không nhảy xuyên door/route; Ambush bị hủy |
| F4-STK-01 | Final Hunt / Escape: PP4 vào tủ đang protection | Vẫn honor P4 token; không tăng Stalker aggression để bù |
| F4-STK-02 | Final Hunt / Escape: Cabinet Ambush muốn khởi tạo | Pilot HOLD: không phát động pressure-increase encounter mới |

---

### Kịch bản bổ sung v0.2 theo giai đoạn

| ID | Tình huống | Phản ứng AED |
|---|---|---|
| Z1-09 | PreMatch đã đặt Tool ở hành lang | Không spawn lại bản sao Tool đó khi bắt đầu Zone |
| Z2-09 | Hoàn thành Zone 1 nhưng còn pending approval | Abort/HOLD theo timeout policy; phase transition không kẹt vô hạn |
| Z3-09 | Normal cap 0, Adaptive plan đã duyệt cap 1 | Chỉ duy trì Minion theo safe-window sau Zone entry và Host safety check |
| Z3-10 | Adaptive cap 1→0 sau relief | Chặn replacement, không despawn Minion đang giữ Tool/Core |
| F4-09 | Bắt đầu Final Hunt hoặc Escape | Khóa toàn bộ tăng pressure, không spawn thêm Minion bất ngờ |
| F4-10 | Emergency relief trong Final Hunt | Chỉ dùng policy support đã phê duyệt và vị trí hợp lệ |

## IX. CÁC KỊCH BẢN AN TOÀN VÀ NGOẠI LỆ

| ID | Trường hợp | Quy định |
|---|---|---|
| SAFE-01 | Backend không thể truy cập | Không apply thay đổi mới; giữ cấu hình hợp lệ hoặc fallback theo contract |
| SAFE-02 | Evidence thiếu hoặc fingerprint sai | HOLD |
| SAFE-03 | Roster thay đổi | Đánh giá lại và chặn approval stale |
| SAFE-04 | Player disconnect | Không suy thành yếu/kém kỹ năng |
| SAFE-05 | Host disconnect | Không thực hiện quyết định không còn authority |
| SAFE-06 | Player reconnect | Khôi phục config revision, state và vật phẩm đã sở hữu |
| SAFE-07 | Double event | Dedup, không nhân số opportunity |
| SAFE-08 | Apply receipt trùng | Idempotent |
| SAFE-09 | Backend approval đến muộn | Kiểm tra phase/revision/snapshot hiện hành trước apply |
| SAFE-10 | Minion/Tool census không khớp | HOLD trục liên quan |
| SAFE-11 | NavMesh không có complete path | Không spawn tại điểm đó |
| SAFE-12 | Tool spawn trùng với scene-placed hoặc object đang tồn tại | Bỏ điểm và chọn lại |
| SAFE-13 | Đã đạt giới hạn tăng | HOLD, không vòng sang trục khác chỉ để tiếp tục tăng |
| SAFE-14 | Có debug skip, God Mode, inventory override | Đánh dấu evidence không phù hợp để xét skill/gameplay approval |
| SAFE-15 | Không có encounter | NoOpportunity, không coi là skill cao |
| SAFE-16 | Opportunity bị cắt do phase end/disconnect | Censored theo contract, không biến thành success/failure |
| SAFE-17 | Adaptive đang giảm độ khó thì team phục hồi | Không lập tức đảo chiều |
| SAFE-18 | Người chơi đang giữ Tool được hỗ trợ | Không thu hồi hoặc thay đổi remaining charges |
| SAFE-19 | Một người chơi yếu trong đội mạnh | Không tăng vượt weakest-player fairness |
| SAFE-20 | Có dấu hiệu game bug hoặc objective không thể hoàn thành | Báo lỗi, không dùng adaptation che lỗi |

---

### Các kịch bản an toàn bổ sung v0.2

| ID | Trường hợp | Quy định |
|---|---|---|
| SAFE-21 | Backend timeout tại safe-window | Không đổi config, không dừng gameplay |
| SAFE-22 | Một phase có nhiều safe windows | Ledger/DB phải phân biệt window, revision; không dùng uniqueness một decision mỗi `PhaseOrdinal` |
| SAFE-23 | Receipt được gửi trước khi có gameplay effect | Không báo effect-success; lưu trạng thái pending/reconcile |
| SAFE-24 | Host disconnect/migration/reconnect mất ledger | Khôi phục revision/manifest hoặc HOLD, không duplicate spawn |
| SAFE-25 | Zone3 Minion target 1 trong Fixed | Reject, giữ Fixed cap 0 |
| SAFE-26 | Optional budget giảm khi item đã spawn | Chỉ áp dụng cho unspawned reservation; không xóa item |
| SAFE-27 | Map/door state đổi khiến NavMesh path không hợp lệ | Chọn fallback đã kiểm tra hoặc HOLD |
| SAFE-28 | TeamTool placement manifest khác scene version | Reject, không apply |

## X. KIẾN TRÚC & IMPLEMENTATION CONTRACT V0.2

### 1. Luồng tổng thể

```text
BACKEND HISTORICAL OPPORTUNITIES + CURRENT MATCH SOURCE (HOST/FUSION)
       │
       ├─ skill/confidence verifier (cross-match comparable context)
       └─ phase/window verifier (identity, source coverage, cause attribution)
                         │
                  Pacing Coordinator
       ┌──────────────┬──┴──────────────┐
       │              │                 │
 StalkerDirector  MinionDirector   ResourceDirector / Placement
       └──────────────┴──┬──────────────┘
                 ONE PROPOSED CHANGE
                         │
    global budget + fairness + effect/no-op + cooldown/dwell
                         │
    Backend APPROVE/REJECT (current snapshot, plan revision)
                         │
 Host revalidate (phase, WindowId, roster, active threat, world state)
                         │
    Host commit (exactly once) → Fusion sync → actual world effect
                         │
            APPLIED receipt + effect observation
                         │
       next evidence window, no oscillation
```

**Stalker policy bổ sung:** authoritative per-player five-source pressure evidence → `PlayerPressureFairnessDirector` / existing Stalker target pressure integration → *host local bounded micro-decisions* (target scoring, LOS/hearing multiplier frozen per encounter, Hide P4 guard, Cabinet Ambush eligibility/warning) → event audit/replay. Backend phê duyệt **policy envelope/version** và persistent parameters, không duyệt mỗi frame hay mỗi clue; Cabinet Ambush activation vẫn phải qua feature-gate + safety budget. Tách PlayerPressure source score, TeamPressure aggregation và GlobalPressureBudget.

Không thay thế Director nghiên cứu đang có bằng module tương đương mới. `PacingCoordinator` chỉ là orchestration/decision policy; tạo class mới **khi** xác định không thể dùng chung `AEDv2BoundaryPolicy` + proposal selection mà không gây coupling khó bảo trì.

### 2. Decision envelope thống nhất (PHASE và WINDOW)

`DecisionContextV02` (DTO/contract đề xuất, **chưa tồn tại trên LTP**):

```text
matchId
hostAuthorityLeaseId / host identity
mode = Adaptive; difficulty = Normal
phaseName; phaseOrdinal (gameplay phase counter)
decisionPoint = PRE_MATCH | PHASE_BOUNDARY | SAFE_WINDOW
windowSequence (0 for boundary/preMatch; >0 for safe windows)
windowId (deterministic by match+phase+sequence+source range)
sourceTickStart, sourceTickEnd, sourceVersion, coverageFingerprint
rosterIdentity, snapshotId, snapshotFingerprint
expectedPlanRevision, expectedPlanFingerprint
sceneVersion/markerManifestFingerprint (resource or spawn changes)
proposedAxis, proposedKey, previousValue, targetValue
pressureSnapshot/attributionSummary + `PlayerPressureSnapshotVersion`/`PlayerPressurePolicyVersion`/fairness proof (Stalker policy) + confidence/budget/cooldown proofs
policyVersion, catalogVersion, safetyFlags, expiryWindow
```

Các trường nhạy cảm với runtime (NavMesh/door/active states) phải được Host xác minh ở thời điểm apply. Backend phải phê duyệt **bằng chứng có thể kiểm chứng độc lập** hoặc whitelist/manifest đã phê duyệt; không coi boolean `reachable=true` tự khai là bằng chứng độc lập.

**Identity và uniqueness bắt buộc:** `(MatchId, PlanRevision)` xác định thứ tự transaction; `(MatchId, PhaseOrdinal, DecisionPoint, WindowSequence)` xác định cửa sổ logic; `DecisionId` idempotent; `ReceiptId` idempotent. Một `PhaseOrdinal` có thể có nhiều safe-window decisions, nên query backend theo revision thay vì `OrderByDescending(PhaseOrdinal)` đơn thuần. Các migrations/unique index được sửa nhất quán; không phá dữ liệu AED v2 V1 đang có.

### 3. Luồng PreMatch và compatibility

1. Chụp roster + historical evidence trước spawn; xác minh `SnapshotId`, roster/current profile, comparison context và full coverage.
2. Backend phê duyệt initial adaptive plan; bản cũ **từ chối `COMMITTED PRE_MATCH`** nên cần endpoint/contract mới hoặc phiên bản approved riêng. Không bypass bằng client-only local plan.
3. Host stage plan, revalidate và áp dụng trước khi spawn Stalker/Minion/TeamTool đầu tiên, ghi init manifest+receipt; initial composite plan có các giới hạn riêng, không áp dụng V2 single-key validator nguyên trạng.
4. Nếu thiếu approval: fallback **Fixed-Normal-derived gameplay baseline**, đồng thời giữ mode Adaptive cho việc thu evidence (không giả nhận đã adjust).
5. `ScenarioConfig` v1.1, `AEDv2Plan`, `NetworkMatchState` plan revision phải rõ quyền sở hữu; không double-apply `SupportItemBudget` và `SupportBonus` hoặc tăng ChaseSpeed hai lần.

### 4. Phase Boundary commit

- Freeze evidence phase hoàn thành theo canonical ordinal contract hiện có: initial `NetworkMatchState.PhaseOrdinal=0`, evidence phase thứ nhất có ordinal 1; không tự ±1 khi chuyển Backend.
- Propose kế hoạch cho phase kế; Host revalidate active Stalker/Minion, player down/revive, roster, snapshot, scene, expected revision.
- Backend phải validate proposed key, source fingerprint và policy. Host commit đúng một lần rồi transition/respawn logic theo FSM thật.
- Nếu timeout/rejected/stale: HOLD và cho phase transition hợp lệ tiếp tục; không bỏ mất objective progress. Transaction chờ receipt phải có cơ chế reconcile tách khỏi gameplay transition.

### 5. Safe Window commit trong cùng release

- Collector ghi episode theo tick/phase, `windowSequence` tăng tuần tự **chỉ ở Host**; trigger tại safe state do game xác định, không lấy timer làm quyền approve.
- `SAFE_WINDOW` không đổi `NetworkMatchPhase` hoặc `PhaseOrdinal`; mọi thay đổi bổ sung tăng `PlanRevision` và ghi ledger.
- Backend phải biết window source slice để chống replay/cùng một episode dùng cho nhiều increases; `windowId` không được bị tái sử dụng với nguồn evidence khác.
- Host freeze window rồi xin approval **asynchronously**; khi reply tới, Host kiểm tra window còn mở. Nếu thay đổi state/roster/door, hủy proposal, không apply giữa encounter.
- Không để backend timeout khóa input/objective. Pending/abort/APPLIED phải reconcile, không tạo duplicate item/minion khi retry.
- `SAFE_WINDOW` áp dụng Tool support chỉ **spawn mới**; đối với vị trí/resource còn reservation cho Zone sau, commit thay đổi reservation trước khi Zone spawn.

### 6. Tác động Gameplay thực tế, Authority và Receipt

| Hạng mục | Integration point được xác minh | Việc cần bổ sung |
|---|---|---|
| Stalker | `StalkerFusionRuntime.RefreshAEDv2ProfileAuthoritative` gọi `ApplyMatchDifficulty` khi revision đổi | Áp dụng delta có budget và safe-window; test revision Host/Client |
| Minion | `PlayerSpawner.MaintainCreepMinionPopulation` đọc effective profile; maintenance mỗi 3s | Zone3 Adaptive key/target cap, spawn gating, census, no force despawn |
| TeamTool | `NetworkMatchState.Spawned → TeamToolWorldSpawn.TrySpawnInitial` | Deferred reserve + spawn per Zone; OptionalBudget + support safe-window |
| Phase | `NetworkMatchState.TryAdvancePhase`, `AEDv2BoundaryCoordinator` | Không ghép safe-window vào API `TryAdvancePhase`; tách coordinator/transaction cho cùng ledger |
| Backend | `ScenarioAdaptivePlanV2Service` / `AEDPhase8SourceVerifierV1` | New version gate: preMatch commit, window verification, multi-revision/phase, real coverage |
| Evidence | `AEDPhase8AuthoritySourceCollectorV1` freeze coverage=false; lifecycle verifier có đối chiếu event | Complete world census, source coverage/reachability and metric independent proof |

`ConfirmApplied` chỉ chứng minh Backend lưu trạng thái claim từ Host theo contract, không tự chứng minh World effect. Receipt tối thiểu phải liên kết `decisionId`, `revision`, `hostTick`, `before/after`, `networked object IDs`, `spawn/despawn event`, `scene marker ID` nếu có và `EffectObserved` sau một window hợp lệ. Nếu effect thất bại, dùng compensating action an toàn hoặc HOLD + ghi incident; không giả mạo rollback những vật phẩm đã được Player nhặt.

### 7. State machine và hỗ trợ kỹ thuật

```text
OBSERVING → CANDIDATE → PENDING_BACKEND → APPROVED → REVALIDATING
             └→ HOLD          └→ EXPIRED/HOLD            └→ ABORTED/HOLD
APPROVED → COMMITTED_LOCAL → REPLICATED → RECEIPT_PENDING → OBSERVING
                                             └→ RECONCILE_PENDING
```

Duy nhất Host tạo world objects. Cần đồng bộ `PlanRevision`, fingerprint và các network entity thực tế; khi reconnect/client join không tự spawn lần thứ hai. Receipt retries phải idempotent. Config/ledger của một match không dùng static persisted state vượt quá match khi reset.

### 8. Bảo vệ khi thay scene/debug

- Debug Zone2 tham chiếu `StalkerZone2EntryTrigger` và Zone3 hiện hard-code transform: tạo production `ZoneEntryMarker` trong Unity Scene, map với NavMesh và door/access graph.
- `DebugSkipToZone3()` hiện set `CurrentPhase` trực tiếp. Tất cả debug operations phải set `EvidenceExcludedFromAdaptive` đến khi được phục hồi theo rule rõ ràng; không chỉ dựa vào chuỗi `DEBUG_SKIP_` nếu luồng đó không dùng `TryAdvancePhase`.
- Không dùng debug inventory, God Mode hoặc teleport skip để học skill hay xét gameplay approval. Chạy editor skip test riêng.

---

## XI. BỘ GIÁ TRỊ CẤU HÌNH DỰ KIẾN (CẦN CALIBRATION)

| Quyết định | v0.2 chốt / đề xuất | Trạng thái |
|---|---|---|
| Fixed Normal Minion caps | Zone1=1 / Zone2=2 / **Zone3=0** | **Khóa theo production source** |
| Adaptive Zone3 Minion | 0 hoặc 1, mặc định 0; chỉ 0→1 khi đủ skill/pressure/budget/safety | **Đã chốt khả năng; ngưỡng chưa calibrate** |
| Adaptive Zone1,2 Minion | Không vượt Normal trong bản đầu; Zone1 1→0, Zone2 2→1 | Đề xuất khóa |
| Stalker positive budget | Tối đa +1 tổng, không cộng nhiều key buff | Đề xuất khóa |
| Multi-axis global budget | Không đồng thời Stalker +1 và Zone3Minion +1 trong bản đầu | Đề xuất khóa theo an toàn |
| Required TeamTool | 5 loại/Zone; không bị cắt bởi Adaptive | **Bất biến gameplay** |
| OptionalToolBudget | 0–2 Tool optional/match, được giảm với phần chưa spawn | **Đã chốt khả năng; quota calibration** |
| Resource grade | R-1 / R0 / R1 / R2 / R3 | Đề xuất khóa semantic, cần markers scene |
| Support Tool | +1 / ordinary relief, 1 emergency event có điều kiện | Candidate policy |
| Increase cooldown | 120–180s + observation dwell | Chưa calibrate |
| Relieve cooldown | 60–90s, emergency có rule riêng | Chưa calibrate |
| Minion spawn maintenance | Theo source ~3s check; KHÁC pacing cooldown | Giữ semantics, bổ sung approval gate |
| Decision points in-match | `PHASE_BOUNDARY` và `SAFE_WINDOW` **cùng release** | **Đã chốt** |
| Active chase/attack/recover/special | Không apply gameplay delta | Đề xuất khóa |
| Sensitive push/refuel/charge/escape | Không tăng Minion / không spawn blocking object | Đề xuất khóa |
| Fixed gameplay baseline của `main` | Không thay đổi | **Bắt buộc** |
| Debug skip như skill evidence | Không | **Bắt buộc** |
| Backend independent approval / verified source | Không bypass | **Bắt buộc** |

**Ngưỡng chưa đủ căn cứ:** mốc meter "near/far", Buffer Zone3 entry, `WeakestSkill` threshold, opportunity minimum theo threat, cooldown và weighted pressure. Không điền số production tùy ý. Gán version policy và thử nghiệm A/B Fixed vs Adaptive sau khi thu pilot data.

---

## XII. NGHIỆM THU VÀ TIÊU CHÍ ĐÁNH GIÁ KHÓA LUẬN

### 1. Bằng chứng bắt buộc trước bật gameplay

| ID | Điều cần chứng minh | Loại kiểm thử |
|---|---|---|
| ACC-01 | Fixed modes giữ nguyên Normal/scene baseline (đặc biệt Zone3 cap=0) | EditMode + Host/Client PlayMode |
| ACC-02 | Adaptive PreMatch cold start HOLD/relief hợp lý, không tăng khi thiếu confidence | Backend integration |
| ACC-03 | Cả `PHASE_BOUNDARY` và `SAFE_WINDOW` có approval, revision, receipt độc lập | Backend + Unity integration |
| ACC-04 | 2+ safe-window changes trong một phase không conflict `PhaseOrdinal` | DB uniqueness/idempotency integration |
| ACC-05 | Thay đổi Stalker trong safe state, không chỉnh active chase/attack/special | PlayMode state transition |
| ACC-06 | Global budget ngăn stacking Stalker+Minion quá giới hạn | Policy property-based tests |
| ACC-07 | Zone3 Fixed Normal không spawn Minion, Zone3 Adaptive approved 0→1, relief 1→0 | Host/Client E2E |
| ACC-08 | Target cap 0 không xóa Minion đang giữ Core/Tool hoặc đang encounter | Minion lifecycle E2E |
| ACC-09 | 5 Tool bắt buộc mỗi Zone luôn còn, optional 2→1→0 chỉ giảm unspawned | Planner tests + runtime E2E |
| ACC-10 | Tool gần Zone entry/corridor theo NavMesh+door/access, không duplicate spawn | Scene marker + Fusion E2E |
| ACC-11 | Cause Minion/Stalker/resource đúng; causal chain không double-count | Event replay/simulation |
| ACC-12 | Backend timeout/stale/rejected không khóa phase/objective, và không apply unauthorized | Backend fault injection |
| ACC-13 | Apply/receipt retry, reconnect, resend idempotent; Host-Client state đồng nhất | Integration E2E |
| ACC-14 | Final Hunt / Escape không spawn Minion/tăng pressure bất thường | Mission PlayMode |
| ACC-15 | Scene invalid/navmesh blocked/door locked → reject hoặc safe fallback | Scene + route E2E |
| ACC-16 | Debug skip, God Mode, stale roster, incomplete evidence → HOLD | Contract + PlayMode |
| ACC-17 | Trước/ sau adaptation có gameplay effect có thể đo | Telemetry outcome report |
| ACC-STK-01 | `PP0…PP4` từ 5 nguồn có provenance/Unknown/dedup; không trùng team `TP0…TP4` | Event replay + policy unit |
| ACC-STK-02 | PP4 protected khi vào tủ trong LOS hoặc bật đèn: **zero force exit / reveal / lockout** trong token window | Host/Client hide PlayMode + property tests |
| ACC-STK-03 | Timeout protection không tạo immediate forced reveal; **valid token vẫn có hiệu lực qua telemetry Unknown/reconnect**, duplicate inspection không lặp effect | Lifecycle / stale authorization tests |
| ACC-STK-04 | PP multipliers giữ LOS/audio provenance và stable encounter; Fixed không đổi | Vision/hearing + AI state tests |
| ACC-STK-05 | Cabinet Ambush chỉ khi PP0/PP1, clue verified, entry+path+warning+reaction+inspection hợp lệ | Scene path + Network PlayMode |
| ACC-STK-06 | Ambush abort nếu mất occupant/clue, PP tăng, protection bật, blocked route hoặc simultaneous special | Fault injection + AI state replay |
| ACC-STK-07 | GlobalPressureBudget chặn Cabinet Ambush+Zone3 Minion+Special stacking, mọi cửa sổ approved đều không tăng quá mức | Policy / integration / receipt |
| ACC-STK-08 | Per-player target fairness không tạo omniscience và không chuyển toàn bộ áp lực sang một Player khác | Multi-player replay + gameplay pilot |


### 2. Ma trận hành vi phải mô phỏng

- **PreMatch:** nhóm PM-01…PM-12.
- **Global pacing:** DP-01…DP-16 + same-phase windows, hysteresis, budget exhaustion.
- **TeamTool:** TT-01…TT-25 + optional budget revision, deferred spawn, ownership and scene marker validity.
- **Stalker:** ST-01…ST-15 + **PF-01…12, VH-01…10, HB-01…15, HT-01…07, CC-01…08, SK-01…07, JP-01…12, CAB-01…16**; cumulative key, protection at force-exit, Cabinet Ambush abort.
- **Minion:** MN-01…MN-25 (MN-21…25 bổ sung v0.2), đặc biệt Zone3 Adaptive-only.
- **Causal attribution:** CA-01…CA-12 + compound Minion slow → Stalker down.
- **Phase:** Z1-01…09, Z2-01…09, Z3-01…10, F4-01…10 và **Z1-STK-01…02, Z2-STK-01…02, Z3-STK-01…02, F4-STK-01…02**.
- **Safety:** SAFE-01…28, bao gồm preMatch/safe-window multi-revision và apply/effect mismatches.

Các ID trong v0.1 được giữ; chỉ cập nhật nội dung khi có sai khác theo source. Với một ID, test cần chứng minh `(input evidence, expected decision, reason code, affected axis, safety invariant, actual effect)`; không chỉ assert candidate object khác null.

### 3. Đánh giá khoa học Fixed vs Adaptive

**Giả thuyết:** AED có thể giảm tỷ lệ mất khả năng tiếp tục vì quá tải và cải thiện nhịp nghỉ giữa encounters, **không** làm mất độ căng thẳng tổng thể; đây là giả thuyết cần kiểm thử, không phải kết luận.

**Thiết kế thí nghiệm:** cùng map/scenario, cân bằng roster/team size và người chơi theo skill band; ghi seed, version, phase, zone, match duration, host. Tránh điều kiện Adaptive nhận thêm nội dung/AI bugfix khiến kết quả bị nhiễu so với Fixed. Nên có chạy replay/simulation dùng cùng event input trước khi mở human study.

**Outcome measures:** objective completion rate, objective time, downs/revive per opportunity, Stalker chase duration + recovery gaps, Minion-caused interruptions / resolved opportunities, Tool usage and reachable supply, fairness across weakest player, abort/HOLD rates, observer-rated frustration/tension (survey). Tất cả denominator phải định nghĩa và version hóa; không suy ra "tốt hơn" nếu không có sample/CI hoặc nghiên cứu người dùng đủ mạnh.

**Điều kiện chấp nhận:** tất cả P0/policy safety gates pass; không regression Fixed; E2E Host-Client và approval path được chứng minh; negative cases fail closed. Trước đó chỉ chạy shadow hoặc test flag local.

---

## XIII. TRACEABILITY — SOURCE LTP, BLOCKER VÀ TRIỂN KHAI TỐI THIỂU

### 1. Căn cứ mã nguồn (đã đọc, không phải runtime test)

| Khẳng định đã xác minh | File/đường dẫn |
|---|---|
| Normal Minion caps 1/2/0 | `KLTN/Assets/Scripts/Gameplay/MatchDifficulty.cs` |
| PlayerSpawner dùng effective profile, Zone3 lấy profile.Zone3MinionCap | `KLTN/Assets/_Project/Scripts/Networking/Player/PlayerSpawner.cs` |
| AEDv2Catalog chỉ có Zone1/Zone2 cap và PreMatchOnly | `KLTN/Assets/Scripts/AI/Common/AED/AEDv2Tuning.cs` |
| AEDv2GameplayBridge giữ Normal Zone3 cap khi chuyển plan | `KLTN/Assets/Scripts/AI/AED/AEDv2GameplayBridge.cs` |
| Phase boundary qua `TryAdvancePhase`; toàn bộ Tool spawn ở `Spawned()` | `KLTN/Assets/_Project/Scripts/Networking/Match/NetworkMatchState.cs` |
| World spawn plan nhiều Zone ngay lúc khởi tạo; NavMesh research preview riêng | `KLTN/Assets/Scripts/TeamTools/TeamToolWorldSpawn.cs` |
| Spawnpoint có NavMesh path complete checker | `KLTN/Assets/Scripts/TeamTools/TeamToolSpawnPoint.cs` |
| Resource/Minion Director vẫn ResearchOnly | `KLTN/Assets/Scripts/AI/Common/AED/AEDResourceDirectorV1.cs`, `AEDMinionDirectorV1.cs` |
| Source collector frozen snapshot `CoverageComplete=false` | `KLTN/Assets/Scripts/AI/AED/AEDPhase8AuthoritySourceCollectorV1.cs` |
| Backend SourceVerifier returns coverage incomplete; preMatch commit disabled, one plan/phase | `EchoProtocol.Backend/src/EchoProtocol.Api/Services/AEDPhase8SourceVerifierV1.cs`, `ScenarioAdaptivePlanV2Service.cs` |
| Debug Zone2 target `StalkerZone2EntryTrigger`, Zone3 hard-coded coords; Zone3 skip direct FSM mutation | `KLTN/Assets/_Project/Scripts/Networking/Match/NetworkMatchState.cs` |
| Stalker profile refresh theo AED revision | `KLTN/Assets/Scripts/AI/Stalker/Networking/StalkerFusionRuntime.cs` |
| Stalker target ranking currently uses core/distance/isolation/recent/history; Stalker-only pressure and decay exist | `KLTN/Assets/Scripts/AI/Stalker/AdaptiveStalkerTargetPolicy.cs`, `StalkerTargetHistoryMemory.cs`, `StalkerController.cs` |
| Hide inspection calls `TryForceExitHidingAuthoritative` before post-reveal eligibility; P4 guard must be inserted **before** force-exit | `KLTN/Assets/Scripts/AI/Stalker/StalkerUnityHideSpotAdapter.cs` |
| Special existing is armed from `StalkerDownedPlayerFact`, Jump entry fairness exists; current settings enforce ≥300s cooldown | `KLTN/Assets/Scripts/AI/Stalker/Special/StalkerSpecialEncounterRuntime.cs`, `StalkerJumpEntrySelector.cs`, `StalkerSpecialEncounterSettings.cs` |

Tất cả nguồn dẫn trên thuộc repo `https://github.com/lamthanhphuc/ECHO-PROTOCOL`, nhánh LTP, commit `10391b1d820a9f25d2352d925dbf5bfc39962e50`.

### 2. Blocking work theo thứ tự dependency — cùng một release về mặt sản phẩm

| Ưu tiên | Thay đổi tối thiểu | Vì sao |
|---|---|---|
| P0 | Đồng bộ Normal baseline zone3=0 trong proposal/testing + thêm Zone3 Adaptive-only key ở contract mới | Ngăn sai baseline và cap không thực thi |
| P0 | Backend verified coverage for skill/pressure/census/reachability; production version verifier | Hiện approval gameplay bị chặn hợp lệ |
| P0 | V2+ decision ledger/DB hỗ trợ PreMatch commit và nhiều SafeWindow/revision trên một phase | Tránh conflict + unauthorized apply |
| P0 | Host safewindow collector/trigger, revalidate và async non-blocking fail semantics | Cho phép giữa phase mà không tác động gameplay sai thời điểm |
| P1 | Effective cumulative budget + optional budget (chỉ extra) + idempotent effect receipt | Ngăn overbuff/double-spawn |
| P1 | Sửa `AEDResourceDirectorV1.ConstrainSupply` để không cắt `CoreStabilizer` hoặc bất kỳ RequiredTool nào | Research code hiện mâu thuẫn với quyết định 5 Tool/Zone |
| P1 | `TeamToolWorldSpawn` reserve/deferred/safe support path và Scene production markers | Có tác động placement Zone2/Zone3 đúng lúc |
| P1 | `PlayerSpawner` nhận Zone3 target cap 0/1 + authoritative census, cooldown | Có Minion Adaptive Zone3 thực sự |
| P1 | Causal attribution evidence producer/verifier và reason-coded policy | Điều chỉnh theo nguyên nhân chứ không theo raw counts |
| P0 | Stalker 5-source per-player authoritative snapshot, Unknown/episode dedup, distinct PP/TP/GlobalBudget contract | Tránh PP4 false grant, PP0 false ambush và nhầm team-vs-player pressure |
| P0 | P4 force-exit action gate trong Hide Spot Adapter + HidingInvestigation protected result + lifecycle protection token | Nếu chỉ sửa target scoring thì Player vẫn bị kéo ra khỏi tủ |
| P1 | Cabinet Ambush state/entry/cue/approach/inspection/abort + shared special exclusivity, anti-omniscience | Không mô phỏng bằng DownedFact giả hoặc teleport sát tủ |
| P1 | Stalker sensory per-player bounded modifiers, Core/Seek fairness và audit/replay | Những đề xuất sensory hiện chưa có đầy đủ integration verified |
| P1 | Host/Client tests, Backend integration, rollout shadow → local gated → E2E | Chứng minh effect và Fixed regression |
| P2 | Calibration và evaluation Fixed vs Adaptive trên dữ liệu pilot | Căn cứ lựa chọn ngưỡng cuối |

### 3. Ngoài phạm vi v0.2

- Không thay đổi AI navigation algorithm, combat hitbox, windup, Safe Room rules, **hệ thống ánh sáng vật lý** hoặc objective logic để "hợp thức hóa" adaptation; Stalker PP4 protection được phép lọc clue gắn occupant *theo policy*, không thay ánh sáng thế giới.
- Không thay đổi bản Fixed qua việc chỉnh `MatchDifficultyProfiles.Normal`.
- Không dùng LLM/ML online inference, không thêm AI black-box để thay thế rule-based system.
- Không tạo thêm Director song song trùng chức năng, không sửa `main` khi chưa có regression evidence.

### 4. Quy định rollout

**Giai đoạn implementation:** Các contract/transaction cho phase boundary và safe window được thiết kế, code và test trong cùng release; triển khai tuần tự theo dependency nhưng không tách thành hai gói tính năng khác nhau. **Activation:** có thể bật shadow/local test trước, chỉ bật gameplay khi cả hai decision point đã đạt gate. Nếu safe-window chưa chứng minh an toàn, không được ghi release v0.2 gameplay là hoàn tất.

**Kết luận:** AED v0.2 là director điều tiết nhịp độ có chủ đích; đẩy thử thách qua nhiều trục trong giới hạn cumulative budget, cấp/giảm *Tool optional chưa xuất hiện*, và cho phép Minion Zone3 Adaptive 0→1 khi thật sự đủ điều kiện. Mọi điều chỉnh dựa trên evidence, được Backend approval và Host revalidate, có Fusion world effect và receipt; Fixed baseline không bị tác động.


## XIV. CHECKLIST DUYỆT RIÊNG STALKER POLICY V0.2 (THAM CHIẾU ĐỀ XUẤT CHI TIẾT XV–XVIII)

| Hạng mục | Chính sách chốt | Nội dung cần duyệt ở lượt tiếp |
|---|---|---|
| **A — Hidden Box P4** | **MIỄN FORCE-EXIT trong protection window, dù bật đèn, kể cả vào tủ khi có LOS** | Ngưỡng PP4, thời gian token, quota/cooldown, post-protection grace, PP3 có relief không |
| **B — Cabinet Ambush** | **Jump In gần tủ → warning → đi tới → inspect → kéo nếu vẫn hợp lệ** | Allowed phases/zones, reaction window, entry distance, cooldown, ambush frequency, clue eligibility, escape route validation |
| **C — Player Pressure** | **5 nguồn Stalker/Minion/Down/Resource/Objective** | Weights, episode normalization, recovery/decay, missing-source behavior, hysteresis, weakest-player protection |
| D — Sensory | Hỗ trợ điều chỉnh Vision/Hearing theo **PP** và giữ LOS/noise fidelity | Các multiplier PP0…PP4, phạm vi sensor thực, encounter snapshot freeze |
| E — Core/Seek | Giữ logic Core Carrier/Seek và fairness | Trọng số target, recovery grace, seek evidence, no-omniscience assertions |
| F — Reveal/Jump | Không làm sai state machine Special/Hide hiện có | Track/grace per PP, warning, cooldown, shared special budget, minimum 300/5/3 seconds invariants |
| G — Rollout | Shadow + local gated + integration; Fixed bất biến | Pilot sample, safety pass criteria, release activation flag/version |

**Quy ước phản hồi để chốt tiếp:** có thể duyệt từng nhóm `A…G`; mọi con số trong mục `V.4.7` vẫn là **candidate** cho tới khi được ghi vào policy registry đã version hóa. Giữ tài liệu này ở trạng thái **Design Review**; không giao agent thay code production chỉ từ một thông số chưa duyệt.

---

## XV. STALKER REVISION 02 — BỘ THÔNG SỐ ĐẦY ĐỦ DO ASSISTANT ĐỀ XUẤT

> **Phân loại:** tất cả con số và logic mới trong mục XV–XVIII là **ĐỀ XUẤT REV02 — CHỜ NGƯỜI DÙNG DUYỆT**, trừ ba nguyên tắc Stalker đã chốt và các bất biến source. Không áp dụng các giá trị này lên `LTP` hoặc `main` khi chưa phê duyệt. Giá trị được chọn **để tạo điểm khởi đầu nhất quán cho pilot**, không khẳng định là tối ưu hoặc đã được đo bằng telemetry.

### XV.1. Quy ước, độ tin cậy và thang áp lực

- `PP` = mức áp lực riêng người chơi: PP0 (0–0.24), PP1 (0.25–0.44), PP2 (0.45–0.64), PP3 (0.65–0.84), PP4 (0.85–1.00). Trong code, dùng so sánh số thực theo ngưỡng tăng `0.25/0.45/0.65/0.85` thay vì làm tròn hai số thập phân để tránh khoảng trống giữa tier.
- `PP_UNKNOWN` = nguồn bắt buộc không đầy đủ, player vừa join, stale/unknown occupancy hoặc chưa đủ cơ hội quan sát. **PP_UNKNOWN không phải PP0** và **không đủ điều kiện để Cabinet Ambush**; đang thiếu nguồn thì HOLD với tăng áp lực.
- `TP` = áp lực toàn đội (`TP0` critical → `TP4` very low). `GlobalPressureBudget` = số token khả dụng để tăng độ khó. **Không lấy trung bình PP rồi xem đó là TP**; TP còn phụ thuộc alive/down, objective blockage, encounter overlap và verified team outcome.
- Fairness không yêu cầu chia đều số lần Chase cho 4 người; yêu cầu tránh dồn liên tục khi có nhiều mục tiêu **hợp lệ về LOS, noise, route, phase và trạng thái**.

### XV.2. Player Pressure: công thức, trọng số, đơn vị và thời gian

**ĐỀ XUẤT REV02:** mỗi player `p` có năm kênh tách biệt và chuẩn hóa về `[0,1]` trong cửa sổ quan sát, tính:

`RawPP(p,t) = clamp01(0.30*S + 0.20*M + 0.25*D + 0.15*R + 0.10*O)`.

| Kênh | Trọng số | Chuẩn hóa mẫu (candidate) | Điều *không* được làm |
|---|---:|---|---|
| `S` — Stalker | **0.30** | Exposure từ Chase hợp lệ, acquisition, hit, forced reveal, Special; ước lượng theo **một episode ID** và thời lượng đã cap | Một Chase dài không được cộng vô hạn theo frame; hit và reveal cùng episode không nhân đôi consequence |
| `M` — Minion | **0.20** | Harass/Slow/Steal/ForcedDrop đã xác minh, thời lượng gián đoạn và cơ hội chống trả | Không suy minion pressure chỉ từ số Minion đang sống |
| `D` — Down | **0.25** | Down/Revive/Elimination authoritative trong cửa sổ, phân biệt lần mới với callback lặp | Không tính cùng một Down hai lần qua Stalker và Minion; Recovery grace là luật độc lập |
| `R` — Resource | **0.15** | Thiếu Tool phù hợp khi có verified need, resource inaccessible/consumed, revive capacity | Không coi người chơi tự chưa nhặt Tool là bị thiếu tài nguyên do AED; chỉ đánh giá Tool world + inventory + reachable/usable |
| `O` — Objective | **0.10** | Phần objective bị *threat-caused interruption* theo active opportunity | Puzzle khó, đi lạc, AFK, networking hoặc thời gian tìm hiểu nhiệm vụ **không** mặc định cộng áp lực |

**Mẫu normalization để bắt đầu test, không phải phép đo thực tế:** `S = min(1, weightedExposureSeconds/60)`; `M = min(1, weightedHarassSeconds/45)`; `D = min(1, verifiedDownEvents/2)`; `R = min(1, verifiedScarcityWindows/2)`; `O = min(1, verifiedThreatBlockedSeconds/60)`. Các hệ số ưu tiên/chồng lấn nằm trong `EvidenceNormalizerV1`, được xác minh bằng replay. Hành động Stalker gây Down có thể góp cho S exposure và D consequence, **nhưng không cộng lại cùng một Down như một `S-hit` và một `D-down` nếu chúng biểu diễn đúng một consequence**; quy định attribution event IDs và caps rõ ràng.

**Đề xuất thời gian và confidence:**

| Parameter | Mặc định Rev02 | Giới hạn an toàn / lý do |
|---|---|---|
| `PressureRollingWindowSeconds` | **120 s** | Lịch sử episode **300 s** để xét repeat targeting; đã tách hai khái niệm |
| `PressureMinObservationSeconds` | **30 s** | Nếu chưa đủ opportunity thì `PP_UNKNOWN`, không hạ thành PP0 chỉ vì im lặng |
| `PressureEvidenceMaxAgeSeconds` | **10 s** | Stale → không ra quyết định *bất lợi* mới, nhưng giữ token bảo vệ P4 hiện hữu tới TTL |
| `PressureDecayHalfLifeSeconds` | **90 s** | Decay event đã kết thúc; **không decay down recovery grace** bằng cách nội suy score |
| `TierMinimumDwellSeconds` | **12 s** | Hạn chế thrash khi PP sát ranh giới; PP4 bảo vệ đã cấp không bị revoke khi score giảm |
| `TierHysteresis` | **0.05** | Tier tăng qua ngưỡng chính; muốn giảm cần thấp hơn ngưỡng `0.05` liên tục 12s |
| `EpisodeDedupWindowSeconds` | **3 s** | Trùng callback cùng episode/action ID phải loại tuyệt đối; 3s chỉ là fallback khi event thiếu identity, **không thay ID** |
| `RecentDownRecoverySeconds` | **20 s** | Grace riêng sau Down/Revive; bảo vệ ngay cả khi phép tính có PP chưa tới P4 |
| `PressureUnknownFallback` | **Hold + baseline sensory** | Không tăng detection/targeting và không Ambush; không thu hồi active protection |

**Đặc tả xử lý thiếu nguồn:** Missing `Stalker`/`Down`/`PlayerLifecycle` authoritative → `PP_UNKNOWN`; thiếu `Resource` hoặc `Objective` → tiếp tục tính snapshot hiển thị *partial* cho debug nhưng `Confidence=INSUFFICIENT` đối với **tăng khó/Ambush**. Nếu đã có proof Down hoặc pressure Critical, hệ thống vẫn có thể xét **relief** qua `EmergencyRelief` độc lập với một PP aggregate đầy đủ. Không tạo PP4 chỉ bằng cách đưa mọi missing channel về 1.0.

### XV.3. Per-player target fairness và Vision/Hearing

**Modifier selection đề xuất:** trong tập target đã `Eligible`, cộng `FairnessScoreAdjustment` theo PP: PP0 `+0.15`, PP1 `+0.08`, PP2 `0`, PP3 `-0.15`, PP4 `-0.25`, PP_UNKNOWN `0`. Trần tuyệt đối `|adjustment|≤0.25` để không vô hiệu hóa `IsObjectiveCarrier` weight hiện có **0.90** hoặc các ưu tiên combat bắt buộc. Nếu đối tượng được tấn công/chase đã cam kết, không đổi target chỉ vì score vừa cập nhật; nếu có hai target tương đương, ưu tiên ai được gặp ít hơn khi cả hai hợp lệ.

| Tier | Vision multiplier | Hearing multiplier | Fairness score delta | Hide inspection |
|---|---:|---:|---:|---|
| PP0 | **1.08** | **1.08** | **+0.15** | Baseline, có thể Ambush nếu clue verified |
| PP1 | **1.04** | **1.04** | **+0.08** | Baseline, có thể Ambush nếu clue verified |
| PP2 | **1.00** | **1.00** | **0.00** | Baseline |
| PP3 | **0.92** | **0.92** | **−0.15** | Không Ambush; giảm điều tra lặp, *không* miễn force-exit |
| PP4 | **0.85** | **0.85** | **−0.25** | **Miễn force-exit trong token active, kể cả đèn** |
| UNKNOWN | **1.00** | **1.00** | **0.00** | Không Ambush; safety grace hiện hữu vẫn hợp lệ |

**Application:** chỉ modifier cho **new acquire / new sound attribution / new target scoring**, freeze multiplier tại thời điểm bắt đầu detection/chase/inspection cho đến khi encounter kết thúc; thay đổi PP không tạo hiệu ứng Stalker “quên” một Player trước mắt hoặc bắt lại người đó tức thì. Sensor vẫn cần FOV/LOS/occlusion, audio provenance, threat eligibility và map bounds. Không tăng physical light range, không “gán” một âm thanh không có người gây ra cho PP0. Đề xuất tối đa 1 lần cập nhật sensory snapshot/5s ở safe AI tick; controller có smoothing độc lập nếu cần.

**Core priority / Seek:** Giữ carrier weight baseline nếu player carrying Core. Nếu carrier PP4, áp dụng fairness penalty trong target scoring hoặc recover window **không xóa gameplay Core pursuit**; không giảm score cho PP0 đồng đội không có sensor clue. `SeekPlayersAfterSeconds` tiếp tục yêu cầu verified room/position evidence. Sau Chase dài: **15 s** không khởi tạo chủ động Seek gây áp lực lên cùng player; không áp dụng nếu player tạo threat mới hợp lệ và gameplay combat bắt buộc.

### XV.4. Hidden Box P4 — thời gian/token, clue/đèn, expiry, force-exit

**Nguyên tắc đã chốt:** Player PP4 được **miễn kéo khỏi tủ trong cửa sổ bảo vệ**, bất kể Stalker thấy họ chạy vào tủ hay đèn trong tủ còn bật. Không yêu cầu giảm/che bóng ánh sáng toàn thế giới: chỉ vô hiệu hóa việc dùng **clue gắn riêng với occupant được bảo vệ** để mở tủ hoặc force-exit trong token.

| Key | ĐỀ XUẤT REV02 | Mục đích |
|---|---|---|
| `P4HideProtectionWindowSeconds` | **10 s** | Đủ thời gian hồi nhịp, không tạo miễn vĩnh viễn |
| `P4HideProtectionPerPlayerCooldownSeconds` | **120 s** | Một Player không farm bảo vệ qua ra/vào liên tục |
| `P4HideProtectionQuota` | **1 token mỗi encounter + tối đa 2 token/match/player** | Không spam; nếu đã exhausted vẫn có recovery grace từ Down riêng |
| `P4HideProtectionGrantRule` | **Vào tủ khi PP4** hoặc **Host phát hiện PP4 trước khi resolve inspection** | Token cấp *trước* force-exit, kể cả vừa chuyển PP4 |
| `P4HideProtectionPersistAcrossScoreDrop` | **Đúng, tới TTL** | Không thu hồi khi PP4→PP3 giữa thời gian bảo vệ |
| `P4HideReinspectionGraceSeconds` | **3 s** sau token hết/Player rời tủ | Không lập tức giật Player ra vì Stalker đứng sát cửa tủ chờ timeout |
| `P4HideSpotCampingRadiusMeters` | **3 m** | Nếu Stalker đang đứng sát tủ lúc TTL hết thì ngắt inspection hiện tại, đổi sang search/patrol, không timer-trigger force-exit |
| `P4HideProtectionOnUnknown` | **Honor existing token; reject new grant** | Mất telemetry không được lập tức tước bảo vệ đã cấp, cũng không tạo unlimited protection |
| `PP3HideRelief` | **Giảm 25% điểm ưu tiên inspect/reinspect**, không miễn | Giữ khác biệt PP3 / PP4 |

**State/authority:** `None → Eligible → Active(tokenId, playerId, hideSpotId, grantedAt, expiresAt, policyVersion, planRevision) → Grace → Cooldown/Exhausted`. Token authoritative gắn cả `playerId` và `hideSpotId`, chỉ owner Host được tạo; force-exit guard xem cả PP tier và token **ngay trước** `NetworkPlayerMovement.TryForceExitHidingAuthoritative()`. Client nhận protected result/cue nhưng không tự định đoạt. Nếu người đang protected rời tủ tự nguyện, token kết thúc và grace áp dụng; không ép họ trở lại tủ. Hide memory vẫn có thể ghi “suspected occupied” nhưng không được chuyển sang cưỡng chế kéo khi token active.

**Invariant chống khai thác:** không cho 1 player lặp `enter/exit` nhận nhiều token cùng cooldown; không được làm Stalker tự biết hide spot từ PP. Nếu player PP4 thật sự liên tục Critical trong khi quota đã hết, áp dụng **global pressure relief** chứ không cấp token vô hạn. Nếu UI/animation đã bắt đầu force-exit trước khi PP4 được xác thực, xử lý theo action authority cutoff/timeline (không rewind animation); test phải bảo đảm snapshot guard đúng trước hành động.

### XV.5. Hide reveal tracking / recovery sau khi kéo hợp lệ

| Tier tại thời điểm reveal | Track memory seconds | Grace trước Chase | Ghi chú |
|---|---:|---:|---|
| PP0 | **0.45** | **1.25** | Tăng nhẹ pressure, không phá animation |
| PP1 | **0.40** | **1.35** | Gần baseline |
| PP2 | **0.35** | **1.50** | Trùng serialized baseline hiện có |
| PP3 | **0.25** | **1.80** | Ưu tiên recovery |
| PP4 | **0.20** | **2.20** | Chỉ khi reveal hợp lệ ngoài protection window, không tạo force-exit mới |
| UNKNOWN | **0.35** | **1.50** | Baseline + no new pressure |

Freeze giá trị tại lúc inspection/reveal đã được phép. Không xóa Stalker attack commit; không apply target speed multiplier trong CHASE. Trường hợp Down/revive hoặc state disconnect dùng grace mạnh hơn nếu rule lifecycle yêu cầu.

### XV.6. Cabinet Ambush — config, điều kiện, cảnh báo và state machine

**Nguyên tắc đã chốt:** clue hợp lệ → Jump In **gần** tủ với cảnh báo → Stalker **đi tới** → revalidate → inspection/kéo nếu vẫn hợp lệ. Không teleport vào tủ hoặc ngay sát người chơi; không arm qua DownedFact giả.

| Key | ĐỀ XUẤT REV02 | Điều kiện |
|---|---|---|
| `CabinetAmbushEnabled` | **false ở production; true riêng Shadow/Local Test** | Chỉ bật sau E2E + review |
| `CabinetAmbushTargetTiers` | **PP0, PP1** | `PP_UNKNOWN` không eligible |
| `CabinetAmbushAllowedPhases` | **Phase 1, Phase 2** ở pilot | Phase 3 chỉ sau test Zone3 Minion exclusivity; Phase 4 **cấm** |
| `CabinetAmbushMaxPerZone` | **1** | Pilot kiểm soát tình huống |
| `CabinetAmbushMaxPerMatch` | **2** | Ngăn spam |
| `CabinetAmbushMaxPerPlayerPerMatch` | **1** | Fairness |
| `CabinetAmbushOwnCooldownSeconds` | **420 s** | Ambush riêng, không sửa global special 300s |
| `CabinetAmbushSharedSpecialSeparationSeconds` | **300 s** | Không khởi động Ambush gần Special khác; shared exclusivity |
| `CabinetAmbushValidClueMaxAgeSeconds` | **12 s** | Clue gameplay verified không quá cũ |
| `CabinetAmbushEntryPathDistanceMeters` | **8–12 m** từ tủ | Dùng complete NavMesh path; thêm minimum player distance ≥7m |
| `CabinetAmbushMinPlayerDistanceMeters` | **7 m** | Không Jump ngay sát player/đầu tủ |
| `CabinetAmbushWarningLeadSeconds` | **≥2.5 s** *sau khi cue xác nhận presentable tới relevant clients* | Thời gian thực phản ứng, không chỉ server timer |
| `CabinetAmbushApproachTimeoutSeconds` | **12 s** | Hết thời gian đi đến tủ → abort |
| `CabinetAmbushFailedAttemptBackoffSeconds` | **90 s** | Không thử liên tục khi không có route/cue |
| `CabinetAmbushPostEncounterRecoverySeconds` | **25 s** | Target bị tấn công/cưỡng chế có recovery chống focus |
| `CabinetAmbushNoStackWithZone3MinionIncrease` | **true** | Khóa pilot; chỉ mở bằng policy revision riêng |

**Clue allowlist đề xuất:** (1) Stalker nhìn hợp lệ thấy Player đi vào tủ; (2) light clue có đúng nguồn từ tủ / occupant không protected, LOS/occlusion thỏa; (3) footstep/interaction noise có `sourcePlayerId` + `hideSpotId` hoặc khu vực nghi vấn hợp lệ; (4) memory của Stalker từ một inspection trước đó trong ngưỡng TTL. **Không đủ:** PP0/PP1 đơn độc, raw network `IsHidden`, query tất cả hide spots từ occupancy, debug teleport location, tiếng động không rõ nguồn. Host chỉ được dùng occupant ground-truth để *kiểm tra lại* tủ đã được chọn bằng clue hợp lệ.

**Ràng buộc cue/reaction:** entry authored hoặc fallback đã được kiểm chứng; thể hiện audio + visual warning trước khi áp sát; yêu cầu cả server và relevant client không báo cue unavailable (audio muted của người dùng không phải tự động lỗi hệ thống, nhưng cơ chế cue phải có cả đường visual); tính warning từ mốc cue sẵn sàng hiển thị trên client theo test contract, cộng latency slack. Stalker không force-exit trong lead window. Route từ cabinet đến ít nhất một nhánh thoát hợp lệ có NavMesh+door snapshot; nếu phòng khóa tạo no-win situation → HOLD.

**Host state machine đề xuất:**

```text
Idle -> Candidate(verified clue, PP0/1, team non-critical)
 -> Reserved(spotId, entryId, encounterId, policyRevision)
 -> CuePending -> CueVisible/WarningLead >= 2.5s
 -> JumpInNearCabinet -> ApproachWithNavMesh <= 12s
 -> Revalidate(occupant, PP tier, P4 token, LOS/clue validity, route, special exclusivity)
 -> Inspect -> ForceExitEligible? -> SuccessfulAmbush -> Recovery -> Cooldown
                 \-> NotEligible / PlayerLeft / Protected / stale -> Abort -> Backoff
```

**Hủy bắt buộc** khi: clue đã sai nguồn; target rời tủ; tier lên PP3/PP4 hoặc unknown; P4 token active; door/nav path mất; objective sensitive; Player Down/revive; bất kỳ Special/CHASE/ATTACK/RECOVER không tương thích; cue không trình bày được; shared pressure budget hết; Host authority thay đổi. Idempotency theo `encounterId`, `decisionId`, `spotId` và `planRevision`; rollback chỉ giải phóng reservation và ghi abort, **không xóa state thực sự của player**.

### XV.7. Global pressure budget, interaction với Minion / TeamTool

**ĐỀ XUẤT REV02:** cấp tối đa **1 `PositivePressureToken` đang hoạt động trong toàn match**; tăng Stalker (sensory/cooldown), Minion Zone3 0→1, giảm optional Tool hoặc đẩy optional Tool ra xa đều **tiêu** một token. Giảm/relief có receipt và safe gates riêng nhưng không sử dụng token tăng khó. Cộng gộp `+1 Stalker` rồi `+1 Minion` là **không hợp lệ**, ngay cả khi mỗi Director riêng vẫn trong cap. Sau khi đổi trục chỉ cho tăng mới nếu token cũ **đã revert hoặc hết hiệu lực có world proof**, chờ cooldown và đủ cơ hội quan sát lại.

**Cabinet Ambush là burst pressure event**: trong pilot yêu cầu budget positive slot trống, không có tăng Stalker/Minion/Tool squeeze active, và riêng `CabinetAmbushQuota`; bản policy nâng cấp có thể dùng weighted burst budget nhưng **không tự mở**. Việc thay Stalker target fairness hay bảo vệ P4 là *safety response*, không tiêu token; việc tăng sensory cho PP0/PP1 do fairness không được biến thành cumulative pressure spike cùng Ambush. Khi team TP0/TP1, có PP3/PP4 đang active, có recent Down/Revive hoặc source unknown → **không tăng khó, không Ambush**.

**Zone3 Minion đã chốt:** Fixed/Normal vẫn 0; Adaptive policy riêng có thể 0→1 khi Zone3 evidence verified và safe window hợp lệ. **Pilot Rev02:** nếu Zone3Minion 1 thì không trigger Cabinet Ambush; Phase 4 không thêm Minion mới và không tăng Stalker. **TeamTool đã chốt:** giảm optional Tool **chưa spawn** trong ngân sách đã duyệt, không bao giờ thu 5 required Tool/zone, không xóa world item đã spawn hoặc inventory.

### XV.8. Phân biệt policy tuning và runtime commit

| Loại hành vi | Quy tắc quyết định | Backend approval và receipt |
|---|---|---|
| Per-player fairness khi chọn target mới | Host dùng snapshot đã xác minh, ngay tại action-eligible tick | Cần `policyVersion` và config approved trước trận/phase; **không gửi round trip mỗi frame**; telemetry quyết định/target |
| PP4 force-exit protection | Authoritative guard tại action boundary; token không bị thu hồi sớm | Approved feature gate + token audit; không chờ network round trip tại cửa tủ |
| Cabinet Ambush | Host quyết định encounter ngắn hạn dựa trên approved policy lease + global budget | Policy revision/feature gate phải approved từ trước; record encounter ID, cue, abort/effect receipt |
| Thay đổi kéo dài sensory profile hoặc global Stalker config | PreMatch, Boundary hoặc SafeWindow transaction | Backend verified evidence → approval → host safe apply → Fusion revision → applied **effect** receipt |
| Zone3 Minion và Tool spawn allocation | Cần world state census, stable spawn boundary | Backend approval + authoritative world effect + idempotent spawn/abort |

**Quan trọng:** source LTP ở commit đối chiếu **chưa có** đầy đủ contract này. Bảng trên là mục tiêu triển khai, **không phải tuyên bố hệ thống đã hoạt động**. Nếu Backend không reachable, hold mọi adaptation bất lợi mới; nhiệm vụ nền vẫn được tiến hành qua fail-open progression; token bảo vệ P4 đã cấp vẫn honor tới TTL.

---

## XVI. BẢNG KỊCH BẢN QUYẾT ĐỊNH MẪU (GIẢ LẬP — CHỜ REPLAY VALIDATION)

Mỗi ví dụ có kết quả dự kiến, không phải log thực tế. `HOLD` là lựa chọn an toàn. Kết quả phải xuất `reasonCode`, `evidenceFingerprint`, `playerPressureSnapshotVersion`, `decisionId`, `applicability`, `effectReceipt`.

| ID | Bằng chứng đầu vào | AED chọn | Điều tuyệt đối không được xảy ra |
|---|---|---|---|
| `SIM-01` | A PP4, đang trốn tủ, Stalker có LOS khi A vào, đèn bật, token P4 còn 6s | `PROTECT_HIDE_FORCE_EXIT`; Stalker có thể nghi ngờ nhưng không kéo A | Adapter gọi `TryForceExitHidingAuthoritative` |
| `SIM-02` | A PP4 token còn 1s, Stalker đang cách tủ 1m | Token kết thúc, chuyển `GRACE=3s`; Stalker rời inspection rồi patrol/reacquire hợp lệ | Instant pull vì timer hết |
| `SIM-03` | A PP4, đã dùng 2 token/match, vẫn bị Down liên tục | Không cấp token mới trái quota; thử `TEAM_RELIEF` theo evidence | Bật miễn kéo vô hạn |
| `SIM-04` | A PP0; cue Stalker đã thấy vào tủ cách 5s; team low; route 9m; mọi gate pass | Reserve Ambush; warning ≥2.5s; Jump In gần tủ; đi bộ tới; inspect/revalidate | Teleport trực tiếp vào tủ; kéo ngay sau Jump |
| `SIM-05` | A PP0 đang trong tủ; không có clue, `IsHidden=true` từ network | `HOLD_NO_VERIFIED_CLUE` | Query map tủ để săn A |
| `SIM-06` | A PP1 trốn; clue hợp lệ; bắt đầu warning, A chạy ra trước inspection | Abort/normal seek theo clue còn hợp lệ | Stalker vẫn kéo ra từ tủ rỗng |
| `SIM-07` | A PP1, nhận Minion slow→Down và chuyển PP4 khi Stalker đang tới tủ | `ABORT_TARGET_NOW_HIGH_PRESSURE`; bảo vệ/recovery | Kéo A vì snapshot từ lúc reserve vẫn PP1 |
| `SIM-08` | A PP0, nhưng team member B PP4, team TP1 do Down liên tục | `HOLD_TEAM_OVERLOADED` | Dồn Ambush vào A để “cân bằng” thống kê |
| `SIM-09` | PP_UNKNOWN vì mới join hoặc source Resource mất, không có recent exposure | Baseline sensory, không Ambush, chưa xét +pressure | Mặc định PP0 rồi ưu tiên săn |
| `SIM-10` | Chỉ A mang Core, PP4; B PP1 không visible, không clue | Giữ LOS/Core eligibility của A với fairness relief; không tạo B target | Stalker biết B xuyên tường |
| `SIM-11` | PP0 và PP3 cùng visible hợp lệ, Stalker không trong committed attack | Bounded fairness ưu tiên PP0 nếu base scores gần nhau | Luôn nhảy sang PP0 bất kể khoảng cách/route |
| `SIM-12` | Zone3 Adaptive Minion cap 1 và event Minion spawn approved | `HOLD_CABINET_AMBUSH_BUDGET`; không stack; Stalker fairness/relief vẫn chạy | Ambush cùng Minion +1 |
| `SIM-13` | Phase4 FinalHunt/Escape, A PP0, tủ có clue | Không kích hoạt Cabinet Ambush/pressure increase | Spike cuối trận |
| `SIM-14` | Tool optional ở Zone2 đã spawn; team TP4 | Không xóa Tool; có thể giảm optional *chưa spawn* ở Zone3 sau approval | Thu hồi Tool inventory/world |
| `SIM-15` | Backend approved old revision; scene/roster/phase đổi trước Host apply | `ABORT_STALE_REVISION`, không apply | Double effects sau retry |
| `SIM-16` | PP4 token còn 8s, backend disconnected; Host vẫn authoritative | Honor current token tới expiresAt, HOLD new adverse changes | Protection tự tắt chỉ vì mất mạng Backend |
| `SIM-17` | Cabinet cue không bảo đảm observable ở Client; entry hợp lệ | `HOLD_WARNING_NOT_READY`, no force-exit | Client bị kéo bất ngờ |
| `SIM-18` | Down callback gửi lại 2 lần cùng `eventId` | Một pressure fact, một recovery grace lifecycle event | PP tăng gấp đôi |
| `SIM-19` | Stalker Chase vừa kết thúc; PP thấp vì decay; cùng player vừa bị focus | Respect recent-target history + 15s seek pause | Focus lại ngay vì PP tụt |
| `SIM-20` | PP4 bước ra tủ tự nguyện trong token, Stalker đang ở gần | End protection token + grace; normal legitimate sensing sau grace | Vừa ra khỏi tủ bị instant forced-reveal |

**Reason codes tối thiểu:** `HOLD_INSUFFICIENT_EVIDENCE`, `HOLD_TEAM_OVERLOADED`, `HOLD_GLOBAL_PRESSURE_BUDGET`, `HOLD_NO_VERIFIED_CLUE`, `HOLD_SAFE_WINDOW_CLOSED`, `HOLD_WARNING_NOT_READY`, `HOLD_ROUTE_INVALID`, `HOLD_SPECIAL_CONFLICT`, `ABORT_TARGET_NOW_HIGH_PRESSURE`, `ABORT_TARGET_LEFT_HIDE`, `ABORT_STALE_REVISION`, `PROTECT_P4_HIDE_WINDOW`, `GRACE_AFTER_PROTECTION`, `COMMIT_CABINET_AMBUSH`, `ABORT_AMBUSH_REVALIDATION`, `HOLD_FIXED_MODE`. Các mã là **đề xuất registry**, chưa có sẵn trong source.

---

## XVII. KẾ HOẠCH KIỂM THỬ VÀ CHUẨN NGHIỆM THU REV02

### XVII.1. Unit/property tests (bắt buộc)

1. Mọi source verified → `0≤PP≤1`; cân nặng đúng tổng 1; tier monotonic; boundary values `0.24/0.25/0.44/0.45/0.64/0.65/0.84/0.85` kiểm tra bằng ngưỡng số thực.
2. Missing/stale/corrupt source → `PP_UNKNOWN` cho increase/Ambush; existing P4 token được honor; verified emergency Down relief không bị bỏ qua.
3. Một episode có nhiều facts nhưng duy nhất event IDs; Down/Revive retry không tăng áp lực gấp đôi; decay và hysteresis không thrash tier tại biên.
4. Fair target scoring chỉ sửa ranking trên eligible targets; target không LOS/noise/route không được được chọn dù PP0.
5. Hide protection guard test bằng mock ghi số lần force-exit: token P4 active → **0 authoritative calls**; P4 bật đèn vẫn 0; token TTL → grace → no instant re-inspect.
6. Cabinet Ambush gate exhaustively: verified clue, PP0/1, allowable phase, cost budget, special exclusivity, warning cue, NavMesh/door/escape path, occupied spot, current protection.
7. Idempotency property: replay command có cùng `encounterId`/`decisionId` không tăng quota hai lần; abort không để stuck reservation.
8. Global budget invariant: no more than 1 positive pressure token, không bật Zone3Minion +1 và Ambush cùng lúc; no increase Phase4.
9. Fixed mode diff: cùng seed/event → baseline vision/hearing, normal Zone3 cap 0, original hide/special behavior unchanged.

### XVII.2. Integration Host/Client và scene tests

| Mã | Test end-to-end | Pass condition |
|---|---|---|
| `E2E-P4-01` | PP4 vào tủ khi Stalker nhìn; flashlight bật | 0 force exit trong token; protected state replicate đúng |
| `E2E-P4-02` | Protection sắp hết và Stalker chờ sát cửa | Grace được áp dụng, không kéo instant |
| `E2E-P4-03` | Disconnect Backend trong token | Bảo vệ tiếp tục tới TTL; không cấp mới khi evidence unknown |
| `E2E-CAB-01` | PP0, verified clue, valid authored entry, client warning | Cảnh báo hiện trước; path đi đủ; mới inspect/kéo |
| `E2E-CAB-02` | Client missed cue / mất route / player rời tủ | Encounter abort, world/player state nhất quán |
| `E2E-CAB-03` | Host retry/lag/authority migration scenario | Không double Jump/force-exit, quota/receipt idempotent |
| `E2E-PP-01` | 4 players áp lực khác nhau, cùng lúc valid LOS | Ranking biến thiên có giới hạn; chase đã commit không đổi giữa chừng |
| `E2E-PP-02` | Một player bị Minion slow rồi Stalker Down | Provenance hai tác nhân, Down không bị đếm hai lần |
| `E2E-Z3-01` | Adaptive Zone3 Minion policy 0→1 | No Cabinet Ambush stacking trong pilot; Fixed Zone3 vẫn 0 |
| `E2E-TT-01` | Zone2 Tool optional spawned nhưng TeamTool policy muốn giảm | Không despawn; chỉ ảnh hưởng optional chưa spawn |
| `E2E-F4-01` | Final Hunt/Escape, cả team low PP | No new Stalker/Minion/Ambush pressure spike |
| `E2E-AED-01` | PreMatch+Boundary+SafeWindow sequence + Backend timeout | Fingerprint/revision/receipt chuẩn; gameplay tiến hành nếu AED approval timeout |

**Acceptance gates đo được (đề xuất):** 100% unit/property safety assertions và 100% P0 E2E pass trong suite repeatable; **0 force-exit trái P4 token** trong automated replay; **0 Ambush không có cue/route**; 0 regressions Fixed. Dùng đo telemetry human pilot cho target share, perceived unfairness, completion rate, recovery opportunities và ambush reaction success; **chưa đặt ngưỡng human-study thành kết luận khi chưa có số mẫu**.

### XVII.3. Rollout

1. `Shadow`: tính PP/TP, candidate Hide/Ambush, reason-codes và telemetry, **không làm thay đổi gameplay**.
2. `Local/Gated`: bật PP4 guard & Cabinet Ambush bằng test-only flag trong host controlled builds; kiểm tra client cues và force-exit timeline.
3. `Integration`: hoàn thành verified Backend source, multi-safe-window ledger, authoritative world effect receipts, Zone3 Minion và TeamTool reservation.
4. `Production Candidate`: chỉ chuyển sang `APPROVED` khi user duyệt bảng XV, safety suite pass, calib replay + human pilot xác nhận. Việc thiết kế xong **không có nghĩa đã bật sản phẩm**.

---

## XVIII. PHIẾU DUYỆT CUỐI VÀ DANH SÁCH CÒN MỞ

| Nhóm | Giá trị mặc định Rev02 *để duyệt* | Trạng thái |
|---|---|---|
| **A. PP4 Hidden Box** | 10s token, 120s cooldown, ≤2 token/player/match; 3s expiry grace; PP3 chỉ relief 25% | **Nguyên tắc đã chốt; số CHỜ DUYỆT** |
| **B. Cabinet Ambush** | PP0/1; 2.5s warning; 8–12m route; 420s own cooldown; 1/zone, 2/match, 1/player; pilot Z1/Z2 | **Flow đã chốt; số/phase CHỜ DUYỆT** |
| **C. Player Pressure** | `(S,M,D,R,O)=(.30,.20,.25,.15,.10)`, window 120s, half-life 90s, dwell 12s, hysteresis .05 | **5 nguồn đã chốt; trọng số CHỜ DUYỆT** |
| **D. Vision/Hearing** | PP0–4 `1.08/1.04/1/.92/.85`, score modifier `+.15/+.08/0/−.15/−.25` | **CHỜ DUYỆT** |
| **E. Core/Seek** | Carrier score gốc còn hiệu lực; seek recovery 15s, không omniscient | **CHỜ DUYỆT số** |
| **F. Hide reveal & Jump** | Reveal table XV.5; floor Special 300s / 5s / 3s **giữ nguyên** | **CHỜ DUYỆT per-tier** |
| **G. Budget/Rollout** | 1 positive token at any time; no Ambush + Zone3Minion; shadow→local→integration→pilot | **CHỜ DUYỆT các giới hạn mới** |

**Những điểm chưa được source xác nhận:** source hiện hành chưa có 5-source PP aggregation, PP4 authoritative protection token, Cabinet Ambush riêng, multi-window Backend ledger và full source verifier. Rev02 là **spec đề xuất**, không phải mô tả chức năng đã chạy; implementation phải bắt đầu từ các blocker XIII.2. Các tham số liên quan độ chính xác cảm giác (*warning lead, detection range, forced reveal reaction*) cần QA trong Unity trước khi khóa giá trị production.

**Gợi ý cho vòng duyệt kế tiếp:** trả lời “duyệt A–G theo Rev02” hoặc chỉ chỉnh các dòng chưa phù hợp như `P4=12s`, `CAB cooldown=600s`, `Vision PP4=.90`, `PP weights=.25/.20/.30/.15/.10`. Không cần chốt lại ba nguyên tắc đã quyết định; chúng là bất biến của thiết kế này.
