# AED v0.2 — Checklist nghiệm thu cuối cho chủ dự án

**Mục tiêu:** Sau khi coding agent đã chạy mọi bài kiểm thử tự động, chủ dự án chỉ phải chạy những trường hợp Host–Client–Scene dưới đây. Tất cả giá trị thời gian/ngưỡng là candidate A–G Rev02 trừ khi đã được duyệt sửa. Ghi trạng thái mỗi test **PASS / FAIL / NOT RUN**. Không đánh dấu PASS chỉ dựa vào video/UI nếu thiếu authoritative log.

## Điều kiện agent phải bàn giao **trước khi bạn mở game**

- [ ] Có PR vào `LTP`, branch hash, file changes, migrations, flags **production disabled** và changelog.
- [ ] Report cho .NET tests, Unity EditMode/PlayMode (test không chạy phải ghi NOT RUN), Backend integration, compile/build và Fixed regression. **Không còn lỗi P0 chưa giải quyết**.
- [ ] Database test được migrate, Backend test đang hoạt động, match Adaptive local/test profile bật có kiểm soát, source/approval verifier thật, logging/correlation ID đầy đủ.
- [ ] Build/Scene 3 Zone có production entry markers, authored cabinet Jump entry, NavMesh/door graph có kiểm thử; có hướng dẫn tạo tình huống PP0…PP4 bằng debug scenario **không làm bẩn adaptive production evidence**.
- [ ] Tool log/counters hoặc overlay test-only thể hiện `PP`, `TP`, budget token, protection token, source confidence, reason codes, plan revision, policy version, decision ID, expected/applied value, Fusion replication, world effect, receipt.
- [ ] Có cách lặp lại/seed/toggle từng tình huống bằng test controls để không phải cố chơi lâu chờ hệ thống xuất hiện ngẫu nhiên.

## 12 ca E2E cuối (theo Rev02 mục XVII)

| Test | Thiết lập và hành động cần thực hiện | PASS khi |
|---|---|---|
| **E2E-P4-01** | Host+Client, PP4 vào tủ ngay trong LOS, bật flashlight; Stalker tiến đến inspect | **0 force-exit** trong P4 token; token state/TTL replicate; không xóa vật thể/tủ |
| **E2E-P4-02** | Stalker đứng gần tủ trước khi token 10s hết | Không lập tức force-exit; có 3s grace và rời/đổi behavior không camping |
| **E2E-P4-03** | Ngắt Backend sau khi token P4 được cấp | Token vẫn có hiệu lực đúng TTL; không cấp token mới dựa trên unknown evidence; match không bị khóa |
| **E2E-CAB-01** | Zone1/2 PP0, clue hợp lệ, authored jump entry+escape route, clients quan sát | Audio+visual warning đủ ≥2.5s sau cue readiness; Jump cách tủ theo path 8–12m, tiến đến bằng path, revalidate rồi mới inspect/kéo |
| **E2E-CAB-02** | Trong khi warning/approach: player rời tủ hoặc door đóng/route invalid hoặc cue unavailable | Ambush abort an toàn, không force-exit/teleport vào tủ, no duplicate encounter, state đồng nhất |
| **E2E-CAB-03** | Retry/loss/reconnect/authority migration đúng kịch bản mà runner hỗ trợ | Không double-jump, double-force-exit hay double-quota; trace ID/receipt idempotent |
| **E2E-PP-01** | Bốn player PP khác nhau đều là target hợp lệ trong LOS | Target ranking fairness được áp dụng có giới hạn; không nhìn xuyên tường/đổi mục tiêu giữa attack; UNKNOWN không như PP0 |
| **E2E-PP-02** | Player bị Minion slow, sau đó Down bởi Stalker | Không cộng Down hai lần; provenance M/D rõ; relief theo đúng nguyên nhân và budget |
| **E2E-Z3-01** | Fixed-Normal Zone3 và Adaptive Zone3 với verified approval cho cap 0→1 rồi relief 1→0 | Fixed Zone3 luôn 0; Adaptive cho phép 1 đúng safe window, không force despawn khi active, không stack Cabinet Ambush |
| **E2E-TT-01** | Zone2 Tool optional đã spawn, AED đề xuất giảm optional; dùng thêm close-entrance relief scenario | Item đã spawn/nhặt không bị xóa; 5 required Tool giữ đủ; support mới được spawn đúng marker/path, no double spawn |
| **E2E-F4-01** | Final Hunt + Escape với đội áp lực thấp | Không có tăng Stalker/Minion/Ambush mới; baseline event diễn ra bình thường; mission hoàn thành được |
| **E2E-AED-01** | Một trận PreMatch → Phase Boundary → ít nhất 2 SafeWindows cùng phase; ngắt Backend/retry một bước | Mọi identity, revision, fingerprint, policy, receipt khớp; chỉ host apply once, client sync, objective chuyển phase không softlock |

## Điều kiện quyết định nghiệm thu

- **Chỉ duyệt merge/release khi:** 12/12 E2E PASS trên môi trường test đại diện; tất cả P0/Fixed regression PASS; 0 unauthorized P4 force-exit; 0 Cabinet Ambush thiếu cue/route; dữ liệu verification và world effect có log. Bất kỳ lỗi nghiêm trọng nào phải trả lại PR cho agent sửa và rerun test, sau đó bạn test lại ca lỗi.
- **Không tự bật production:** `CabinetAmbushEnabled` và extended AED gameplay gates phải OFF ở config production cho đến khi bạn duyệt E2E và bật cờ release riêng.
- Sau E2E, chỉ cần trả agent/bản PR: `PASS toàn bộ — cho phép merge` hoặc danh sách `FAIL: E2E-... + logs + reproduction`.

**Lưu ý:** Unity multiplayer scene E2E thường không thể đảm bảo hoàn toàn bằng unit tests. Nếu agent không có Unity Editor/license, agent **không thể hợp lệ báo 'chỉ còn E2E cuối'** khi chưa xác minh compile/PlayMode; yêu cầu agent đánh dấu NOT RUN và giao cách khắc phục.
