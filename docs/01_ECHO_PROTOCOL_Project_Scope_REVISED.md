<!-- Converted from 01_ECHO_PROTOCOL_Project_Scope_REVISED.docx. Source formatting is simplified; content order is preserved. -->

ECHO PROTOCOL

01 — PROJECT SCOPE BASELINE

Nguồn chính: ECHO PROTO (Gameplay Source of Truth hiện tại) + yêu cầu System/KLTN bắt buộc; ECHO_PROTOCOL_Gameplay_KLTN_Hoan_Chinh dùng làm Design Rationale / Historical Reference

| Project | ECHO PROTOCOL |
| --- | --- |
| Baseline date | 19/08/2026 |
| Team size | 4 thành viên |
| Target feature-complete | 31/10/2026 |
| Final target | 06/12/2026 |
| Gameplay source | ECHO PROTO (current gameplay source of truth) |
| Document role | Khóa phạm vi trước khi thiết kế kiến trúc, spec và chia task; tài liệu gameplay hoàn chỉnh cũ chỉ dùng làm rationale/reference |

| Quy tắc nguồn yêu cầu<br>ECHO PROTO là Gameplay Source of Truth hiện tại. Khi mechanic/scope trong tài liệu này khác ECHO_PROTOCOL_Gameplay_KLTN_Hoan_Chinh, ưu tiên ECHO PROTO. Tài liệu gameplay hoàn chỉnh cũ vẫn được giữ để tham khảo competitor analysis, MDA/SDT, AED rationale, telemetry và playtest. Payment Sandbox và Generative AI Mission Briefing được giữ là System/KLTN Mandatory Scope; chúng không phải core gameplay mechanic và không được thay đổi luật gameplay. |
| --- |

## 1. Mục tiêu dự án

Xây dựng một game Online Cooperative Survival Horror 2-4 người, thời lượng mục tiêu 15-20 phút/trận, tập trung khám phá, tìm đường, giải đố, sinh tồn, phối hợp và chạy thoát. Hệ thống phải thể hiện rõ ba nhóm AI: Traditional Monster AI, Modern Adaptive Experience Director (AED) và Generative AI cho nội dung text có kiểm soát.

## 2. Scope bắt buộc (Mandatory / P0)

Baseline scope rule: Gameplay P0 = 1 Research Facility + core loop + 3 monsters + 4 Team Tools + Player/Team Profile + AED. System/KLTN P0 = Backend/API/DB + Auth/Profile/Match/Telemetry/Reward + Shop/Inventory/Wallet + Payment Sandbox + GenAI Mission Briefing. Map 2 là P1/Conditional; Optional Evidence là P2/Future Work.

| Scope Area | Nội dung bắt buộc | Điều kiện đạt |
| --- | --- | --- |
| Gameplay Core | Movement, stamina, crouch, interaction, inventory, noise, Down/Revive/Eliminated, reward/result. | Một trận hoàn chỉnh từ Lobby đến Result không soft-lock. |
| Objectives & Co-op | Energy Core/Sector Box, two-room puzzle, Security Hold, Final Hunt, Escape/Rescue, 4 Team Tools. | Co-op tốt hơn solo; không soft-lock khi số người còn sống giảm; Final Hunt không phụ thuộc Optional Evidence. |
| Maps | Research Facility (P0 bắt buộc). Underground Station là P1/Conditional, chỉ triển khai nếu P0 ổn định. | P0 pass khi Research Facility chạy full loop ổn định; Map 2 không phải điều kiện pass milestone P0. |
| Monsters | Stalker, Listener, Warden. | 3 trục counterplay: sight, sound, route. |
| Multiplayer | Lobby 2-4, create/join/ready/tool selection, runtime replication, match state, disconnect baseline. | 2/3/4 người hoàn thành full match. |
| Traditional AI | Patrol, Investigate, Chase, Search, Attack, Recover, Return/Final Hunt; vision/noise/NavMesh/last-known-position. | Monster không omniscient; hành vi test được. |
| Telemetry | Match/phase time, survival, co-op, navigation, resource, risk, monster, AED decision logs. | Dữ liệu đủ tính Player/Team Profile và nghiên cứu. |
| Modern AI - Player/Team Modeling | Persistent Player Profile + Team Behavior Analysis + TeamPerformance. | Score cập nhật qua nhiều trận, normalize và log được. |
| Modern AI - AED | Scenario selection, bounded difficulty adjustment, pacing events, fairness/min-max/cooldown/fallback. | Có Fixed baseline và Adaptive condition để so sánh. |
| Generative AI | Mission briefing text có backend validation, timeout/cache/fallback. Lore variation nâng cao không bắt buộc. | Provider lỗi vẫn vào trận bằng template/cache; GenAI không sinh gameplay rule/stat/map/code. |
| Backend & DB | Auth/profile, match, result, reward, wallet, inventory, telemetry, AI profiles, shop, payment. | API/data đủ hỗ trợ client và nghiên cứu. |
| Shop FE/BE | Catalog, cosmetic-only, wallet, inventory, equip. | Không pay-to-win; purchase end-to-end. |
| Payment | Sandbox order, provider callback/verify, idempotent fulfillment, transaction log. Đây là System/KLTN requirement ngoài core gameplay. | Một giao dịch chỉ fulfill đúng một lần. |
| UI/UX | Login, Lobby, Tool Select, HUD, Result, Shop, Inventory, error/loading states. | User flow end-to-end không cần thao tác dev. |
| Audio/Visual | Gameplay cue, monster cue, Final Hunt presentation, map lighting/VFX, usable assets. | Beta đủ âm thanh/hình ảnh để mentor đánh giá. |
| QA/Research | Core playtest, co-op test, monster differentiation, Fixed vs Adaptive, balance/final validation. | Có telemetry + questionnaire + bug evidence. |
| Documents | Architecture, implementation spec, project plan, test evidence, final report/demo guide. | Final package tái hiện được hệ thống và kết quả. |

## 3. Scope ưu tiên thấp / được giảm độ phức tạp

| Hạng mục | Chiến lược giảm scope |
| --- | --- |
| Cosmetic richness | Giảm số outfit/nameplate/badge/spray; giữ luồng catalog-purchase-inventory-equip. |
| GenAI richness | Giữ 1 use case mission briefing + fallback; lore variation nâng cao có thể giảm. |
| Advanced reconnect | Giữ disconnect baseline; host migration/reconnect phức tạp chỉ làm nếu networking solution hỗ trợ ổn. |
| Art polish | Dùng asset phù hợp/placeholder chất lượng chấp nhận được; ưu tiên readability và gameplay cue. |
| AED complexity | Giữ rule-based, bounded adaptation; không cần ML training pipeline. |
| Map 2 — Underground Station | P1/Conditional. Chỉ triển khai sau khi Research Facility P0, multiplayer và core loop ổn định; cắt trước khi ảnh hưởng M4. |
| Optional Evidence | P2/Future Work. Không nằm trong core loop, reward hay milestone exit bắt buộc; chỉ mở lại khi mentor yêu cầu và có effort budget. |

## 4. Out of Scope / Cut-first

Map 3 Abandoned Hospital.

Dual-monster Hard Mode nếu gây rủi ro tích hợp.

Combat system hoặc vũ khí chiến đấu.

Pay-to-win item/stat.

Runtime AI tự sinh map/model/code/gameplay rule.

Machine Learning training pipeline cho monster/AED.

Production real-money payment ngoài sandbox nếu không được yêu cầu.

Matchmaking/ranking/competitive ladder quy mô production.

Dedicated-server scale production nếu KLTN chỉ cần demo/test environment.

## 5. Product Breakdown Structure

| PBS | Phạm vi |
| --- | --- |
| P1 Client/Foundation | Bootstrap, config, scenes, input, UI framework, API/network client. |
| P2 Gameplay | Player, interaction, inventory, objective, puzzle, terminal, hunt, down/revive, tools. |
| P3 Multiplayer | Lobby, replication, authority, scene/match state, disconnect. |
| P4 Traditional AI | Perception, noise, nav, FSM/BT, 3 monster variants. |
| P5 Modern AI | Telemetry, aggregation, Player/Team Profile, Fixed Director, AED, decision logs. |
| P6 Content AI | GenAI prompt/template, validation, cache/fallback. |
| P7 Backend/Data | Auth, profile, match, reward, telemetry, AI profiles, DB/migrations. |
| P8 Economy | Shop, wallet, inventory, equip, payment sandbox. |
| P9 Content/Presentation | Research Facility P0; Map 2 Conditional/P1; audio, lighting, VFX, UI polish. |
| P10 QA/Research/Documents | Automated/manual tests, playtest, Fixed-vs-Adaptive, bug fixing, docs. |

## 6. Scope theo Milestone

| Milestone | Scope phải đạt |
| --- | --- |
| M1 Design + Plan — 05/08–24/08 | Chốt ECHO PROTO, Project Scope, Architecture, Implementation Spec baseline, ownership/dependency/DoD/risk, WBS, networking decision và Backend/DB baseline. |
| M2 Prototype — 25/08–20/09 | Research Facility greybox + Stalker + 2-4 multiplayer + Core/Puzzle/Security Hold/Final Hunt + Down/Revive; full match playable, không soft-lock. |
| M3 Improve Prototype — 21/09–25/09 | Chỉ test nội bộ, sửa bug, network/core stability, soft-lock fixes và tuning prototype. Không dùng M3 để mở feature scope lớn. |
| M4 Beta — 26/09–31/10 | Feature-complete P0: Research Facility, 3 monsters, 4 Team Tools, Player/Team Modeling, AED, backend/DB, shop/inventory, Payment Sandbox, GenAI briefing+fallback, UI/audio/visual beta. Map 2 chỉ P1/Conditional. |
| M5 Beta Improve — 01/11–01/12 | Mentor feedback, P0/P1 fixes, balance/performance, 2/3/4P regression, Fixed-vs-Adaptive playtest/evidence và release candidate. |
| M6 Final & Documents — 02/12–06/12 | Final regression/build/deploy package, source tag, technical documents, research result, demo/deployment guide và rehearsal. |

## 7. Quality Gates

Không có P0 soft-lock trong core loop.

2/3/4-player full-match regression pass trước Beta.

Economy/payment do backend authoritative và có transaction log.

Monster không biết vị trí player nếu không có perception/event hợp lệ.

AED có min/max, cooldown, reason code và fallback.

GenAI provider lỗi không chặn gameplay.

M4 kết thúc ngày 31/10/2026 và là feature freeze cho scope P0. Từ 01/11 chỉ sửa lỗi, balance, performance, mentor feedback và research evidence; không mở feature mới nếu không có quyết định scope change.

## 8. Open Decisions / TBD trước khi code sâu

| Decision | Baseline | Yêu cầu |
| --- | --- | --- |
| Networking framework | TBD | Phải spike và chốt trong M1. |
| Host vs dedicated | Proposed: host/server authoritative cho match KLTN | Không khóa framework cụ thể. |
| Backend framework/DB | TBD theo năng lực nhóm | Contract và ownership quan trọng hơn framework. |
| Payment provider | TBD sandbox | Phải hỗ trợ verify/callback/idempotency. |
| GenAI provider/model | TBD | Chỉ text; có timeout/cache/fallback. |
| Hosting/deployment | TBD | Dev/Test/Release config riêng. |
| AED alpha/threshold | Tuning via playtest | Không hard-code như “công thức chuẩn”. |

## 9. Scope Change Control

Mọi feature mới sau khi baseline được mentor/nhóm chấp thuận phải trả lời 4 câu hỏi: (1) có bắt buộc cho KLTN không, (2) dependency nào bị ảnh hưởng, (3) milestone nào trượt, (4) feature nào sẽ bị cắt để bù effort. Sau feature freeze 31/10/2026 không thêm feature mới trừ yêu cầu sửa trực tiếp của mentor và phải có impact analysis.
