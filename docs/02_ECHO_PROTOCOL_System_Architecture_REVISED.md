<!-- Converted from 02_ECHO_PROTOCOL_System_Architecture_REVISED.docx. Source formatting is simplified; content order is preserved. -->

ECHO PROTOCOL

02 — SYSTEM ARCHITECTURE DESIGN

Kiến trúc đề xuất để chuyển Gameplay Scope thành các module có thể phân công và tích hợp

| Architecture status | Proposed baseline — framework-specific choices may remain TBD |
| --- | --- |
| Primary constraints | 2-4 multiplayer; Unity client; backend/shop/payment; Traditional AI; AED; GenAI |
| Authority principle | Gameplay state: host/server authority; account/economy/payment: backend authority |
| Feature-freeze target | 31/10/2026 |

| Ranh giới giữa yêu cầu và quyết định kỹ thuật<br>Kiến trúc này triển khai ECHO PROTO là Gameplay Source of Truth hiện tại. ECHO_PROTOCOL_Gameplay_KLTN_Hoan_Chinh được giữ làm Design Rationale / Historical Reference. Payment Sandbox và GenAI Mission Briefing là System/KLTN Mandatory Scope ngoài core gameplay. Networking framework, backend framework, database engine, payment provider và GenAI provider vẫn là ADR/TBD cho tới khi nhóm chốt. |
| --- |

## 1. Architecture Goals

Tách gameplay runtime khỏi backend/economy để gameplay vẫn test được khi dịch vụ phụ lỗi.

Tách Traditional Monster AI khỏi Modern AED để dễ giải thích, test và bảo vệ KLTN.

Dùng authority rõ để giảm desync/duplicate interaction/payment exploit.

Telemetry là contract dùng chung giữa gameplay, research và AED.

Mọi external AI/payment service đều có timeout, validation, logging và fallback.

Cho phép 4 thành viên ownership subsystem tương đối độc lập nhưng có integration contract sớm.

## 2. System Context

## 3. Component Responsibilities

| Component | Trách nhiệm | Không làm |
| --- | --- | --- |
| Unity Client | UI/HUD, player mechanics, interaction, inventory, map presentation, puzzle/terminal visuals, audio/VFX, local input, network/API adapters. | Không tự xác nhận reward/payment; không tự quyết định persistent profile. |
| Multiplayer Session | Lobby 2-4, member/ready/tool state, authoritative gameplay commands, replication player/item/objective/monster/match phase. | Không sở hữu account/wallet/payment truth. |
| Traditional Monster AI | Perception (vision/noise), navigation, target selection, FSM/BT states, monster-specific counterplay. | Không tự tính team difficulty/profile; không omniscient. |
| Backend API | Auth/profile, match/result, reward, shop/inventory/wallet, payment verification, telemetry ingestion, AI profile/scenario endpoints. | Không điều khiển từng frame gameplay. |
| Database | Persistent data: user, economy, payment, match, telemetry, Player/Team AI profile, scenario/decision logs. | Không chứa runtime-only transient movement state. |
| Modern AED | Aggregate player/team data, Fixed baseline, scenario selection, bounded difficulty/pacing decision, reason log/fallback. | Không sinh map/model/code; không bypass fairness bounds. |
| Generative AI Adapter | Mission briefing/lore text, prompt/template, validate/sanitize/cache/fallback. | Không sinh gameplay stat/rules/runtime content tự do. |
| Payment Adapter | Create provider request, verify callback/signature/status, idempotent fulfillment. | Client không được tự cộng currency/item. |

## 4. Authority Model

| State | Rule | Authority |
| --- | --- | --- |
| Player input | Client sends intent; host/server validates applicable interactions. | Client + Host |
| Player transform/state | Network solution authoritative model; interpolate on clients. | Host/Server proposed |
| Item pickup/drop | Validate one owner, position/state replicated. | Host/Server |
| Core/Sector/Puzzle/Terminal | State transition và progress authoritative. Security Terminal bị ngắt/Down thì pause tại progress hiện tại; player hợp lệ khác resume từ đúng progress đó, không reset và không ép về 60%. | Host/Server |
| Monster decision | One authoritative AI instance; clients receive state/transform/cues. | Host/Server |
| Match phase/win-loss | Single authoritative match state machine. | Host/Server |
| Account/Profile | Token/session and persistent profile. | Backend |
| Reward/Wallet/Inventory | Calculate and persist authoritative economy. | Backend |
| Payment fulfillment | Provider verification + idempotent transaction. | Backend |
| Player/Team Profile | Aggregate verified/accepted telemetry and persist. | Backend/AED |
| Scenario config | AED proposes; validation service approves; runtime applies. | Backend/AED + Host apply |

## 5. End-to-End Data Flows

| Flow | Sequence |
| --- | --- |
| Login | Unity -> Auth API -> DB -> token/profile -> Unity. |
| Create/Join Lobby | Unity -> Multiplayer session -> member/ready/tool state broadcast. |
| Start Match | Host requests/receives ScenarioConfig (fixed/adaptive) -> validates -> loads map -> spawns player/monster/objectives. |
| Runtime Interaction | Client intent -> host validation -> authoritative state mutation -> replicate event/snapshot. |
| Monster | Host Monster AI reads perception/noise/objective events -> transitions state -> replicates movement/state/cues. |
| Telemetry | Gameplay systems emit local/runtime events -> aggregate/batch -> Backend ingest -> DB. |
| Match End | Authoritative result (escape/rescue/outcome) -> Backend result/reward -> wallet/progress -> profile update -> result UI. |
| AED Update | Telemetry -> normalized MatchScore -> Player/Team Profile -> next scenario/pacing decision -> decision log. |
| Shop | Unity catalog -> Backend -> DB; purchase -> backend validates balance -> transaction -> inventory/equip. |
| Payment | Unity creates order -> Backend -> Sandbox Provider -> callback/verify -> idempotent fulfillment -> wallet/inventory -> Unity refresh. |
| GenAI | Backend receives scenario/profile context subset -> provider/template -> validate/sanitize -> cache -> briefing; fallback if failed. |

## 6. Gameplay Runtime Architecture

MatchStateMachine: Preparation -> CoreCollection -> PowerPuzzle -> SecurityHold -> FinalHunt -> Result.

Map baseline: Research Facility là P0 bắt buộc. Kiến trúc vẫn giữ mapId/content registry để không hard-code một map; Underground Station chỉ là P1/Conditional và không phải milestone gate P0.

Interaction framework dùng interface/component chung cho Item, SectorBox, Door, PuzzlePanel, Terminal và Exit. Optional Evidence không thuộc P0/P1 runtime contract hiện tại.

GameplayConfig/ScriptableObject chứa tuning values thay vì hard-code: stamina, carry penalty, revive, hunt timer, noise radius, tool cooldown.

TeamToolConfig baseline: Field Scanner chỉ trả khu vực/hướng tương đối (không exact position; cooldown TBD/tuning); Noise Maker cooldown 300 giây; First Aid ReviveSpeedMultiplier = 1.5 và không self-use; Door Jammer duration = 60 giây và chỉ áp dụng JamEligible door. Các giá trị này do host/server validate.

NoiseEventBus là contract trung tâm cho sprint, core, door, puzzle, terminal, Noise Maker và Listener.

TelemetryEventBus độc lập với gameplay result để dễ log/test và tránh gameplay phụ thuộc backend.

## 7. Multiplayer Architecture

| Area | Contract |
| --- | --- |
| Lobby commands | Create/Join/Leave/Ready/SelectTool/StartMatch. |
| Runtime commands | Interact, pickup/drop, use tool, puzzle input, terminal hold, revive, exit. |
| Authoritative events | MemberChanged, ToolSelected, ItemState, CoreInstalled, PuzzleResult, TerminalProgress, PlayerDown/Revived/Eliminated, MonsterState, MatchPhaseChanged, Result. |
| Snapshots | Player transform/state; monster transform/state; selected persistent runtime state depending framework. |
| Disconnect baseline | Remove/disable player, drop carried item, release tool slot if lobby; match continues if valid. |
| Anti-duplication | Pickup, reward and transaction paths use server/backend validation; idempotency where persistent side effect exists. |

## 8. Traditional Monster AI Architecture

Traditional AI chạy trong authoritative gameplay runtime. Công nghệ có thể là FSM hoặc Behavior Tree; Gameplay Document không bắt buộc một lựa chọn duy nhất.

| Module | Design |
| --- | --- |
| Perception | Vision cone, occlusion, hearing/noise age/priority, objective event, last-known-position. |
| Navigation | NavMesh/pathfinding + route/hiding data. |
| Decision | Patrol -> Investigate -> Chase -> Search -> Attack -> Recover -> Return/Final Hunt. |
| Stalker | Sight/line-of-sight dominant. |
| Listener | Noise dominant; vision weaker; diminishing reliability for fake noise. |
| Warden | Route/objective control; temporary route lock with telegraph and alternate route. |
| Adaptation layer | Rule-based probability/behavior bias; never reduce counterplay to zero. |

## 9. Modern AI / AED Architecture

| AED Stage | Responsibility |
| --- | --- |
| Telemetry Input | Survival, objective, teamwork, exploration/navigation, tool use, risk, noise, revive, team distance/split/resource. |
| Normalization | Convert match metrics to comparable bounded component scores. |
| Player Profile | Persistent vector; EMA update across matches. |
| Team Profile | Session/team aggregate: objective time, split time, distance, revive success, resource efficiency, wipe/recovery. |
| Team Performance | Initial model: 30% ObjectiveSpeed + 25% SurvivalRate + 25% Teamwork + 20% ResourceEfficiency; weights tuned by playtest. |
| Fixed Director | Reproducible baseline config for research comparison. |
| Adaptive Decision | Select ScenarioConfig and 1-2 bounded parameter changes at pre-match/phase boundary. |
| In-phase Pacing | Only designer-approved events with cooldown/budget. |
| Fairness | No speed+vision+hearing triple buff; no omniscience; min/max; no rigging; reason log; fallback. |

## 10. Generative AI Architecture

Mandatory KLTN use case baseline: mission briefing text; optional second use case: lore log variation.

Input chỉ gồm dữ liệu cần thiết của ScenarioConfig; không gửi secret/payment credential.

Backend adapter gọi provider với timeout; output được length/schema/content validation trước khi trả client.

Cache theo scenario/template key để giảm lỗi phụ thuộc provider.

Fallback: deterministic template/localized text. Provider failure không block lobby/start match.

## 11. Backend & Data Model Baseline

| Data Group | Entities |
| --- | --- |
| Identity | User, Session/RefreshToken (nếu dùng). |
| Progress | PlayerProgress, Wallet, WalletTransaction. |
| Shop | ShopItem, InventoryItem, PlayerLoadout. |
| Payment | PaymentOrder, PaymentTransaction, Fulfillment/Idempotency key. |
| Match | Match, MatchMember, MatchResult. |
| Telemetry | TelemetryBatch/Event hoặc aggregate table theo design. |
| AI | PlayerAIProfile, TeamProfile, ScenarioConfig/ScenarioDecisionLog. |
| Content | GeneratedContentCache/BriefingCache nếu cần. |

## 12. API Boundary Baseline

| API Area | Endpoints/Responsibilities |
| --- | --- |
| Auth | POST register/login/logout/refresh (nếu dùng), GET profile. |
| Match | POST create/start/finish; GET result/history tối thiểu nếu UI cần. |
| Telemetry | POST batch telemetry/aggregates. |
| AI | GET/POST player profile debug/admin nếu cần; POST scenario decision; POST/GET briefing. |
| Shop | GET catalog; POST purchase; GET inventory; POST equip. |
| Payment | POST order; callback/webhook endpoint; GET payment status. |
| Health | GET health/readiness for dev/test. |

## 13. Failure & Fallback Strategy

| Failure | Expected behavior |
| --- | --- |
| Backend unavailable | Block account/shop/economy operations; gameplay dev mode may use local config only if explicitly enabled. |
| AED unavailable | Use Fixed/last-known valid ScenarioConfig. |
| GenAI unavailable | Use template/cache; gameplay continues. |
| Payment callback repeated | Idempotency prevents double fulfillment. |
| Client disconnect | Authoritative state cleanup and objective item release. |
| Monster path failure | Repath/return/recover strategy; never teleport silently unless explicit debug recovery. |
| Invalid scenario | Reject and fallback to validated template. |

## 14. Suggested Code/Repository Boundaries

| Boundary | Contents |
| --- | --- |
| Unity/Game | Player, Interaction, Inventory, Objectives, Tools, MatchState, UI, Audio/VFX. |
| Unity/Networking | Lobby/session adapters, commands/events, authoritative bridges, sync components. |
| Unity/AI | Perception, Monster FSM/BT, Monster variants; AED runtime config application only. |
| Shared/Contracts | DTOs/enums/versioned messages where sharing is practical. |
| Backend/API | Controllers/endpoints, auth middleware, request validation. |
| Backend/Application | Match, economy, payment, telemetry, AI orchestration use cases. |
| Backend/Domain | Entities/rules/value objects. |
| Backend/Infrastructure | DB, payment adapter, GenAI adapter, logging. |
| Tests | Unit/integration/network/playtest support. |

## 15. Architecture Decisions to Lock in M1

ADR-001 Networking framework + authority/host policy.

ADR-002 Backend framework and database engine.

ADR-003 Contract serialization/versioning.

ADR-004 Telemetry granularity: raw events vs aggregate + retention.

ADR-005 Payment sandbox provider.

ADR-006 GenAI provider/model + timeout/cost limits.

ADR-007 Build/deployment target and environment secret management.

## 16. Integration Order

Foundation -> Local core gameplay -> Stalker AI -> Full local match -> Multiplayer core -> Backend auth/match -> Telemetry -> Player/Team Modeling -> Fixed Director -> AED -> Listener/Warden + 4 Team Tools -> Shop/economy -> Payment -> GenAI -> Audio/Visual -> QA/Research. Map 2 chỉ triển khai P1/Conditional sau khi P0 ổn định. Payment/GenAI được thiết kế contract sớm nhưng tích hợp sâu sau core loop để tránh chặn gameplay.
