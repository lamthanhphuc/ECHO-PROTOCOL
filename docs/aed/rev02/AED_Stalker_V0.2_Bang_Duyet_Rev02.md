# AED STALKER V0.2 — PHIẾU DUYỆT THÔNG SỐ ĐỀ XUẤT REV02

**Tài liệu gốc đầy đủ:** `AED_Dynamic_Pacing_Design_v0.2_Stalker_Rev02_Complete.md`
**Ngày:** 14/10/2026 · **Source kiểm tra:** LTP `10391b1`
**Tình trạng:** Đề xuất để phê duyệt, **chưa triển khai**. Bảng này không tự cho phép thay đổi code.

## 1. Ba nguyên tắc KHÔNG CẦN DUYỆT LẠI

1. **PP4 Hidden Box:** P4 không bị kéo khỏi tủ trong protection window, ngay cả bật đèn hoặc Stalker nhìn thấy lúc vào tủ.
2. **Cabinet Ambush:** Clue hợp lệ → Jump In gần tủ có warning → đi tới bằng route → inspection → kéo nếu vẫn hợp lệ.
3. **Player Pressure:** 5 nguồn **Stalker, Minion, Down, Resource, Objective**; khác với team TP và global budget.

## 2. Bảy nhóm giá trị đề xuất để chốt

| Nhóm | Đề xuất mặc định | Chốt / Sửa |
|---|---|---|
| **A — Hidden Box** | PP4 ≥0.85, token **10s**, cooldown **120s/player**, **2 token/match/player**, post-grace **3s**, PP3 -25% reinspection | ☐ Chốt ☐ Sửa |
| **B — Ambush** | PP0/1; warning **≥2.5s**, entry path **8–12m**, cooldown **420s**, **1/zone, 2/match, 1/player**; chỉ Z1/Z2 pilot | ☐ Chốt ☐ Sửa |
| **C — Pressure** | **S .30 / M .20 / D .25 / R .15 / O .10**, window **120s**, half-life **90s**, min-observation **30s**, hysteresis **.05** | ☐ Chốt ☐ Sửa |
| **D — Vision/Hearing** | PP0 **1.08** / PP1 **1.04** / PP2 **1.00** / PP3 **.92** / PP4 **.85**; unknown 1.00 | ☐ Chốt ☐ Sửa |
| **E — Target/Core/Seek** | Score modifier PP0..4 = **+.15/+.08/0/−.15/−.25**; Core pursuit baseline giữ; Seek sau chase dài hoãn **15s** | ☐ Chốt ☐ Sửa |
| **F — Reveal/Special** | PP0 track/grace **.45/1.25s**; PP2 **.35/1.50s**; PP4 ngoài window **.20/2.20s**; giữ Special min **300/5/3s** | ☐ Chốt ☐ Sửa |
| **G — Global Safety** | Tối đa **1 positive token** đồng thời; **cấm Ambush + Zone3 Minion increase** pilot; cấm pressure spike Phase4; Fixed bất biến | ☐ Chốt ☐ Sửa |

## 3. Các chính sách an toàn khóa cứng

- `PP_UNKNOWN` khác `PP0`: chưa đủ telemetry → **không chọn Ambush / không tăng detection**.
- Trong PP4 token, HideSpot Adapter **không được gọi force-exit**; token tồn tại tới TTL kể cả snapshot PP thay đổi hoặc Backend gián đoạn.
- Clue tủ phải đến từ gameplay; không lấy `IsHidden`/tọa độ ẩn làm trigger tìm mục tiêu.
- Bắt buộc cue visual/audio và reaction window; không có route thoát → hủy Cabinet Ambush.
- Không lấy PP thấp làm cớ tập trung một Player trong nhiều encounter liên tiếp.
- Không giảm required 5 Tool/Zone; chỉ giảm **optional chưa spawn**. Fixed/Normal Zone3 Minion **0**, Adaptive có policy riêng `0→1`.
- Mọi tính năng mới ở **proposal/shadow** tới khi hoàn tất Backend source verification, authority receipts và E2E.

## 4. 20 ví dụ quyết định đầy đủ để soát

Chi tiết `SIM-01`…`SIM-20` ở mục XVI tài liệu gốc, bao gồm: P4 bật đèn, token timeout sát tủ, quota exhausted, Cabinet Ambush PP0 có clue, thiếu clue, player rời tủ, Minion→Down→P4, team đang quá tải, missing source, Core carrier, target fairness, Zone3 stack, Phase4, Tool optional đã spawn, stale Backend revision, Backend disconnect, cue missing, duplicate Down event, recent chase và voluntary hide exit.

## 5. Kiểm thử không thể bỏ qua

`E2E-P4-01…03`, `E2E-CAB-01…03`, `E2E-PP-01…02`, `E2E-Z3-01`, `E2E-TT-01`, `E2E-F4-01`, `E2E-AED-01`. Mục XVII có pass condition. Unit regression Fixed phải 100% pass, không được có unauthorized force exit/cabinet no-warning trong replay.

## 6. Cách phản hồi

Chỉ cần trả lời: **“Chốt A–G theo Rev02”** hoặc ghi mục muốn sửa: ví dụ **“A token 12s, B cooldown 600s, D PP4 = .90”**. Các nhóm không nêu thay đổi sẽ vẫn ở trạng thái **đề xuất** cho tới khi người dùng phê duyệt rõ ràng.
