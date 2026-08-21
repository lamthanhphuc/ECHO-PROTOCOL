<!-- Converted from 04_ECHO_PROTOCOL_Project_Management_Baseline_REVISED.xlsx. Each worksheet is represented as a coordinate-preserving Markdown table. -->

# 04_ECHO_PROTOCOL_Project_Management_Baseline_REVISED

## 00_OVERVIEW

| Row | A | B |
| --- | --- | --- |
| 1 | ECHO PROTOCOL — DEPENDENCY + OWNERSHIP + MILESTONE BASELINE |  |
| 3 | Purpose | Khóa dependency, module ownership, Definition of Done, milestone exit criteria và risk trước khi chia WBS. |
| 4 | Source | ECHO PROTO (current gameplay source) + revised Project Scope + revised System Architecture |
| 5 | Team | 4 thành viên; owner bên dưới là role đề xuất, cần đổi theo skill thực tế. |
| 6 | Feature complete | 31/10/2026 |
| 7 | Mentor review/fix | 01/11/2026–01/12/2026 |
| 8 | Final | 06/12/2026 |
| 9 | Ownership rule | Mỗi module có 1 Primary + 1 Backup; integration task có thể All 4. |
| 10 | Scope rule | P0 bắt buộc; P1/Conditional giảm/cắt trước khi ảnh hưởng deadline; P2/Future không nằm trong milestone gate. Research Facility P0; Map 2 P1; Optional Evidence P2. |

## 01_MODULE_OWNERSHIP

| Row | A | B | C | D | E | F | G | H |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | Module | Primary Owner (proposed) | Backup | Main responsibilities | Key dependencies | Integration partners | Handoff / contract | Priority |
| 2 | Gameplay/Maps/UI | A - Unity Gameplay/UI/Map | B - Multiplayer | Player, interaction, inventory, Core, puzzle, terminal, hunt, tools, maps, HUD/audio/VFX | Foundation; network contracts | B,C,D | Gameplay state/events + config + UI data | P0 |
| 3 | Multiplayer/Networking | B - Multiplayer/Networking | A - Unity Gameplay | Lobby, ready/tool selection, authority, replication, scene/match sync, disconnect | Gameplay state contracts | A,C,D | Commands/events/snapshots/authority matrix | P0 |
| 4 | Traditional Monster AI | C - AI/Telemetry/Research | A - Unity Gameplay | Perception, noise, NavMesh, FSM/BT, Stalker/Listener/Warden, adaptation rules | Map/NavMesh; network authority | A,B | Monster state/perception/noise contracts | P0 |
| 5 | Telemetry + Player/Team Modeling | C - AI/Telemetry/Research | D - Backend | Event schema, instrumentation, normalization, EMA, team metrics | Gameplay events; backend ingest | A,B,D | Versioned telemetry DTO + profile schema | P0 |
| 6 | Adaptive Experience Director | C - AI/Telemetry/Research | D - Backend | Fixed baseline, scenario selection, bounded DDA/pacing, reason logs, experiments | Telemetry + profiles + scenario data | A,B,D | ScenarioConfig/AEDDecision contract | P0 |
| 7 | Backend/API/Database | D - Backend/DB/Shop/Payment | C - AI | Auth, profile, match, telemetry persistence, reward, config, migrations | Architecture/contracts | A,B,C | REST DTO/error/version/data schema | P0 |
| 8 | Shop/Inventory/Reward | D - Backend/DB/Shop/Payment | A - UI | Catalog, wallet, purchase, inventory, equip, reward persistence | Auth + match result | A,C | Economy transaction APIs/view models | P0 |
| 9 | Payment Sandbox | D - Backend/DB/Shop/Payment | B - Integration | Order, provider verify, callback/poll, idempotent fulfillment, history minimum | Shop/economy stable | A,B | Payment state machine + API status | P0 |
| 10 | Generative AI | D - Backend adapter + C - AI prompt/validation | C/D shared | Mission briefing, validation/sanitize, cache/timeout/fallback | ScenarioConfig; external provider | A,C,D | GeneratedContent contract | P0 |
| 11 | QA/Integration/Release | All 4 | All 4 | Regression, playtest, performance, mentor fixes, release package | All modules | All | Build + test evidence + bug list | P0 |

## 02_DEPENDENCY_MAP

| Row | A | B | C | D | E | F |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | From / Prerequisite | To / Dependent | Dependency type | Why it matters | Earliest milestone | Critical path? |
| 2 | Architecture decisions | Unity/Network/Backend skeleton | Technical | Tránh rework framework/authority/contracts. | M1 | Yes |
| 3 | Player movement + interaction | Inventory/Objectives/Tools | Functional | Objective và tools dùng chung interaction/state. | M2 | Yes |
| 4 | Map1 greybox + NavMesh | Stalker AI | Content/AI | AI cần route/nav/perception test space. | M2 | Yes |
| 5 | Noise event bus | Stalker/Listener + telemetry | AI/Data | Một contract chung cho sound detection và measurement. | M2 | Yes |
| 6 | Core/Puzzle/Terminal/Final Hunt local | Multiplayer replication | Integration | Phải hiểu state machine trước khi network sync. | M2 | Yes |
| 7 | Lobby + runtime authority | Full multiplayer match | Network | Tất cả state phải có single source of truth. | M2 | Yes |
| 8 | Full match result | Reward/Progression | Business | Reward chỉ tính từ authoritative result. | M4 | Yes |
| 9 | Auth/Profile + DB | Shop/Inventory/Payment | Backend | Economy cần user identity và persistence. | M4 | Yes |
| 10 | Telemetry instrumentation | Player Modeling | Data/AI | Không có dữ liệu thì không tính profile. | M4 | Yes |
| 11 | Player + Team Profile | AED | AI | AED cần skill/team context. | M4 | Yes |
| 12 | ScenarioConfig + validated spawn/routes | AED runtime | AI/Content | AED chỉ chọn content hợp lệ. | M4 | Yes |
| 13 | Stalker base AI | Listener/Warden | AI reuse | Tái dùng AI framework/perception/nav. | M4 | No |
| 14 | Map1 proven core loop | Map2 | Content | Map 2 là P1/Conditional; chỉ bắt đầu sau khi Map1/core loop/multiplayer P0 ổn định. | M4 | No |
| 15 | Shop/economy stable | Payment sandbox | Business/Security | Payment fulfill vào wallet/inventory có sẵn. | M4 | Yes |
| 16 | ScenarioConfig | GenAI briefing | AI content | Briefing dựa trên scenario đã được chốt. | M4 | No |
| 17 | Feature-complete Beta | Mentor review/bug fix | Process | Review có ý nghĩa khi không còn mock bắt buộc. | M5 | Yes |
| 18 | Fixed Director + Adaptive AED | Research comparison | Research | Cần baseline cùng content để chứng minh. | M5 | Yes |
| 19 | P0 regression + research evidence | Final docs/build | Release | Final phải khớp code và kết quả. | M6 | Yes |

## 03_TEAM_SKILL_MATRIX

| Row | A | B | C | D | E | F | G | H | I | J | K | L |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | Member | Unity Gameplay | UI/UX | Networking | Backend/API | Database | Traditional AI | Modern AI/Data | DevOps/QA |  | Scoring guide | Meaning |
| 2 | Member A |  |  |  |  |  |  |  |  |  | 0 | Chưa làm |
| 3 | Member B |  |  |  |  |  |  |  |  |  | 1 | Có thể làm khi có hướng dẫn |
| 4 | Member C |  |  |  |  |  |  |  |  |  | 2 | Làm độc lập |
| 5 | Member D |  |  |  |  |  |  |  |  |  | 3 | Mạnh/có thể review người khác |

## 04_MILESTONE_EXIT

| Row | A | B | C | D | E |
| --- | --- | --- | --- | --- | --- |
| 1 | Milestone | Deadline | Exit criteria — tất cả phải đạt | Mentor review evidence | If not met |
| 2 | M1 | 24/08/2026 | Chốt ECHO PROTO, Project Scope, Architecture, Implementation Spec baseline, ownership/dependency/DoD/risk, WBS; networking decision/spike; Backend/DB/ERD/API baseline. | Gameplay pitch + revised 01–05 baseline + ADR/spike evidence + ERD/API/network contracts. | Không mở M2 nếu authority/networking và core contracts chưa chốt. |
| 3 | M2 | 20/09/2026 | 2-4 người create/join lobby và chơi full Research Facility greybox với Stalker: Core -> Puzzle -> Security Hold -> Final Hunt -> Result; Down/Revive; không soft-lock. | Live prototype demo + 2/3/4P smoke + bug list. | Giảm polish; ưu tiên end-to-end core loop và network stability. |
| 4 | M3 | 25/09/2026 | Improve Prototype only: test nội bộ, sửa P0 bug/soft-lock/desync, tuning core mechanic và ổn định build. Không thêm feature scope lớn. | Bug burn-down + regression lại M2 flow + tuning notes. | Feature chưa thuộc prototype chuyển M4; không dùng M3 để mở rộng scope. |
| 5 | M4 | 31/10/2026 | Feature-complete P0 Beta: Research Facility, 3 monsters, 4 Team Tools, full multiplayer, Player/Team Modeling, AED scenario+pacing+fallback, backend/DB, shop/inventory, Payment Sandbox, GenAI briefing+fallback, UI/audio/visual beta. Map 2 chỉ P1/Conditional. | Single beta build + backend + payment/GenAI fallback demo + P0 checklist. | Feature freeze 31/10; thiếu P0 phải triage ngay. Map 2 bị cắt trước khi giảm P0. |
| 6 | M5 | 01/12/2026 | Mentor feedback closed; P0 bugs=0; 2/3/4P regression; performance/balance; payment idempotency; Fixed vs Adaptive playtest data; Release Candidate clean-machine run. | Mentor feedback log, regression report, telemetry/experiment report, RC build. | Không mở scope; chỉ fixes/evidence/performance/balance. |
| 7 | M6 | 06/12/2026 | Final build/backend/source tag; docs khớp code; deployment/user/demo guide; final research result; rehearsal và fallback demo. | Final package + report + demo script + setup verification. | Chỉ critical hotfix; tài liệu phải được cập nhật xuyên M1–M5 để M6 chỉ final hóa. |

## 05_DEFINITION_OF_DONE

| Row | A | B |
| --- | --- | --- |
| 1 | Area | Definition of Done |
| 2 | General | Code merged to agreed integration branch; no compile/runtime blocker; task acceptance criteria pass. |
| 3 | Code quality | Naming/conventions followed; no secret hard-code; config/tuning exposed where needed. |
| 4 | Review | At least one teammate review for P0 or cross-module change. |
| 5 | Unity gameplay | Playable in target scene/build; no obvious soft-lock; relevant config documented. |
| 6 | Multiplayer | Test >=2 clients; authority/duplicate interaction verified; relevant state synchronized. |
| 7 | Backend/API | Validation + error code + auth/authorization as applicable; DB migration reproducible. |
| 8 | Persistent transaction | Retry/idempotency behavior defined for reward/shop/payment. |
| 9 | Traditional AI | Controlled test for state transition/perception; no omniscience beyond documented events. |
| 10 | Modern AI/AED | Input/output/bounds/reasonCode logged; deterministic baseline test available. |
| 11 | GenAI | Validation + timeout + fallback tested; provider outage does not block core gameplay. |
| 12 | Payment | Sandbox verify path; fake client success cannot grant; repeated callback cannot double grant. |
| 13 | Telemetry | Event has version/correlation/match/player context as applicable; ingestion verified. |
| 14 | Bug fix | Regression test added or clear reproduction/verification recorded. |
| 15 | Documentation | Relevant spec/API/config/ADR updated if behavior/contract changes. |

## 06_RISK_REGISTER

| Row | A | B | C | D | E | F | G | H |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | ID | Risk | Probability | Impact | Owner | Early mitigation | Trigger | Fallback / scope action |
| 2 | R-01 | Multiplayer desync/authority bug | High | Very High | B | Spike M1; authority matrix; 2/3/4P test every milestone. | Duplicate item/state divergence. | Simplify reconnect/advanced features; preserve basic 2-4P. |
| 3 | R-02 | Integration gameplay-network late | High | Very High | A+B | Sync vertical slice in M2, không đợi gameplay hoàn thiện 100%. | Local works but network rewrite needed. | Freeze interfaces; cut polish. |
| 4 | R-03 | AED không đủ dữ liệu/không thuyết phục | Medium | Very High | C | Telemetry contract từ M1, instrumentation sớm trong M2/M4; Fixed baseline; offline simulator; reason log. | Sang M4 vẫn chưa có sample telemetry/profile deterministic. | Keep rule-based minimal AED + stronger evaluation. |
| 5 | R-04 | AI monster khó debug qua network | Medium | High | B+C | Single authoritative AI; test scene; state debug overlay. | Different monster states per client. | Replicate state/cues; simplify adaptation. |
| 6 | R-05 | Payment integration trễ/provider issue | Medium | High | D | Choose sandbox M1; implement state machine before provider specifics. | Callback/verify unavailable. | Use supported sandbox poll/verify; document provider constraints. |
| 7 | R-06 | GenAI provider unstable/cost/timeout | Medium | Medium | C+D | Adapter + timeout/cache/template from start. | Provider fail/slow. | Fallback briefing mandatory. |
| 8 | R-07 | Map 2 conditional + 3 monster content overload | High | High | A+C | Reuse frameworks; Research Facility P0 trước; Map 2 chỉ bắt đầu khi core/MP ổn định. | P0 Beta M4 slipping vì content mở rộng. | Cut Map 2 trước; giữ 3 monster P0. Giảm art richness trước mechanics. |
| 9 | R-08 | Backend/economy transaction bugs | Medium | High | D | Atomic transactions, idempotency, tests. | Double reward/payment grant. | Lock economy writes; repair script/dev reset. |
| 10 | R-09 | Art/audio assets late | Medium | Medium | A | Placeholder-first; asset list by M3. | Beta missing cues. | Use licensed/available assets with functional polish. |
| 11 | R-10 | Team skill mismatch | Medium | High | All | Fill Skill Matrix before final assignment; backup owner for each module. | Owner blocked >2 days. | Pairing/reassign ownership. |
| 12 | R-11 | Mentor requests major change after M4 | Medium | High | All | Review scope mỗi milestone; feature freeze 31/10 explicit. | New core mechanic requested. | Impact analysis + trade/cut existing feature. |
| 13 | R-12 | Final docs left too late | High | Medium | All | Update docs/evidence xuyên M1–M5; M6 chỉ final hóa/đóng gói. | Code complete but docs missing. | Không để dồn viết mới vào 02–06/12; nếu thiếu, ưu tiên docs bắt buộc và evidence tái hiện hệ thống. |

## 07_SCOPE_CUT_RULES

| Row | A | B |
| --- | --- | --- |
| 1 | Rule | Contents |
| 2 | Keep at all costs (P0) | Core match loop; Research Facility; 2-4 multiplayer; Stalker/Listener/Warden; 4 Team Tools; telemetry; Player/Team Modeling; minimal AED; backend/DB; shop FE/BE; payment sandbox core; GenAI briefing+fallback; test/research evidence. |
| 3 | Reduce complexity first | Art richness; số cosmetic; UI animation; payment history/refund simulation; reconnect sophistication; monster adaptation sophistication; Map 2 chỉ làm nếu còn capacity. |
| 4 | Cut first | Map 2 Underground Station; Optional Evidence; Map3; dual-monster hard mode; GenAI lore richness; advanced procedural content; production-scale hosting; combat; ranking; extra cosmetic systems. |
| 5 | Never cut silently | Không cắt ngầm System/KLTN P0. Nếu P0 có rủi ro, phải impact analysis + mentor decision. Map 2/Optional Evidence không được dùng để trì hoãn P0. |
