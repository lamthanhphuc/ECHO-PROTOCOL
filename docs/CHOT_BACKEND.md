<!-- Converted from Chốt Backend.docx. Source formatting is simplified; content order is preserved. -->

Chốt Backend, Database và dữ liệu cần lưu

<br>1.Backend Techical

| Thành phần | Chốt Công nghệ |
| --- | --- |
| Backend | ASP.NET Core Web API |
| Ngôn ngữ | C# |
| API | REST + JSON |
| ORM | Entity Framework Core |
| Database | PostgreSQL |
| Authentication | JWT Access Token |
| API documentation | OpenAPI / Swagger |
| Migration | EF Core Migration |
| Deployment | Docker container trên Linux |
| Dev DB | PostgreSQL local/Docker |
| Test/Release DB | PostgreSQL riêng |
| Config | Environment variables |
| Secret | Chỉ backend giữ, không đưa vào Unity/Git |

2.Database và dữ liệu cần lưu

| Nhóm | Dữ liệu chính | Mục đích | Lưu DB |
| --- | --- | --- | --- |
| Account | User, Session | Đăng ký, đăng nhập và quản lý người chơi | Có |
| Progress | PlayerProgress | XP, level, số trận, số trận thắng | Có |
| Balance | Wallet, WalletTransaction | Quản lý currency và lịch sử cộng/trừ tiền | Có |
| Shop | ShopItem, Inventory, PlayerLoadout | Quản lý cosmetic đã mua và đang sử dụng | Có |
| Payment | PaymentOrder, PaymentTransaction | Theo dõi giao dịch thanh toán sandbox | Có |
| Match | Match, MatchMember, MatchResult | Lưu thông tin người tham gia và kết quả trận | Có |
| Telemetry | TelemetryEvent, MatchTelemetry | Lưu các chỉ số hành vi trong trận phục vụ phân tích | Có |
| AED | MatchScore, PlayerAIProfile, TeamProfile | Tổng hợp hành vi người chơi và đội | Có |
| Scenario | ScenarioConfig, AdaptiveDecision | Lưu cấu hình và quyết định của AED | Có |
| GenAI | GeneratedContent | Lưu/cache Mission Briefing đã tạo | Có hoặc cache |

- Sơ đồ ERD <br>

Table "users" {

"id" BIGSERIAL [pk, increment]

"email" VARCHAR(255) [unique, not null]

"username" VARCHAR(100) [unique, not null]

"password_hash" VARCHAR(255) [not null]

"status" VARCHAR(30) [not null, default: 'ACTIVE']

"created_at" TIMESTAMP [not null, default: `CURRENT_TIMESTAMP`]

}

Table "sessions" {

"id" BIGSERIAL [pk, increment]

"user_id" BIGINT [not null]

"issued_at" TIMESTAMP [not null, default: `CURRENT_TIMESTAMP`]

"expires_at" TIMESTAMP [not null]

"revoked_at" TIMESTAMP

}

Table "player_progress" {

"user_id" BIGINT [pk]

"xp" BIGINT [not null, default: 0]

"level" INT [not null, default: 1]

"total_matches" INT [not null, default: 0]

"total_wins" INT [not null, default: 0]

}

Table "wallets" {

"user_id" BIGINT [pk]

"soft_balance" BIGINT [not null, default: 0]

"premium_balance" BIGINT [not null, default: 0]

"version" INT [not null, default: 0]

}

Table "wallet_transactions" {

"id" BIGSERIAL [pk, increment]

"user_id" BIGINT [not null]

"transaction_type" VARCHAR(50) [not null]

"amount" BIGINT [not null]

"currency" VARCHAR(30) [not null]

"source_ref" VARCHAR(255)

"created_at" TIMESTAMP [not null, default: `CURRENT_TIMESTAMP`]

}

Table "shop_items" {

"id" BIGSERIAL [pk, increment]

"sku" VARCHAR(100) [unique, not null]

"name" VARCHAR(150) [not null]

"category" VARCHAR(50) [not null]

"price" BIGINT [not null]

"currency" VARCHAR(30) [not null]

"active" BOOLEAN [not null, default: true]

"asset_key" VARCHAR(255)

}

Table "inventory" {

"id" BIGSERIAL [pk, increment]

"user_id" BIGINT [not null]

"item_id" BIGINT [not null]

"acquired_at" TIMESTAMP [not null, default: `CURRENT_TIMESTAMP`]

"source" VARCHAR(50)

Indexes {

(user_id, item_id) [unique, name: "uq_inventory_user_item"]

}

}

Table "player_loadouts" {

"user_id" BIGINT [pk]

"outfit_id" BIGINT

"flashlight_skin_id" BIGINT

"nameplate_id" BIGINT

"badge_id" BIGINT

"emote_id" BIGINT

"spray_id" BIGINT

}

Table "payment_orders" {

"id" BIGSERIAL [pk, increment]

"user_id" BIGINT [not null]

"package_id" VARCHAR(100) [not null]

"amount" NUMERIC(12,2) [not null]

"currency" VARCHAR(20) [not null]

"status" VARCHAR(30) [not null]

"provider_ref" VARCHAR(255)

"created_at" TIMESTAMP [not null, default: `CURRENT_TIMESTAMP`]

"expires_at" TIMESTAMP

}

Table "payment_transactions" {

"id" BIGSERIAL [pk, increment]

"order_id" BIGINT [not null]

"provider_txn_id" VARCHAR(255) [unique, not null]

"status" VARCHAR(30) [not null]

"raw_ref" TEXT

"processed_at" TIMESTAMP

}

Table "scenario_configs" {

"id" BIGSERIAL [pk, increment]

"map" VARCHAR(100) [not null]

"monster" VARCHAR(100)

"spawn_set" VARCHAR(100)

"event" VARCHAR(100)

"support_budget" INT

"route_modifier" VARCHAR(100)

"final_hunt_modifier" VARCHAR(100)

"version" INT [not null, default: 1]

"created_at" TIMESTAMP [not null, default: `CURRENT_TIMESTAMP`]

}

Table "matches" {

"id" BIGSERIAL [pk, increment]

"scenario_id" BIGINT

"map_id" VARCHAR(100) [not null, default: 'RESEARCH_FACILITY']

"status" VARCHAR(30) [not null]

"experiment_condition" VARCHAR(30)

"started_at" TIMESTAMP

"ended_at" TIMESTAMP

}

Table "match_members" {

"match_id" BIGINT [not null]

"user_id" BIGINT [not null]

"slot" INT

"join_state" VARCHAR(30)

"escape_state" VARCHAR(30)

Indexes {

(match_id, user_id) [pk]

}

}

Table "match_results" {

"match_id" BIGINT [pk]

"outcome" VARCHAR(30) [not null]

"duration_seconds" INT

"survivors" INT [default: 0]

"rescue_bonus" INT [default: 0]

"rating" NUMERIC(5,2)

}

Table "telemetry_events" {

"id" BIGSERIAL [pk, increment]

"match_id" BIGINT [not null]

"user_id" BIGINT

"event_type" VARCHAR(100) [not null]

"event_time" TIMESTAMP [not null, default: `CURRENT_TIMESTAMP`]

"value_json" JSONB

"reason_code" VARCHAR(100)

"schema_version" INT [not null, default: 1]

}

Table "match_telemetry" {

"id" BIGSERIAL [pk, increment]

"match_id" BIGINT [not null]

"user_id" BIGINT

"metric_key" VARCHAR(100) [not null]

"metric_value" NUMERIC(15,4)

}

Table "match_scores" {

"id" BIGSERIAL [pk, increment]

"match_id" BIGINT [not null]

"user_id" BIGINT [not null]

"survival_score" NUMERIC(5,2)

"objective_score" NUMERIC(5,2)

"teamwork_score" NUMERIC(5,2)

"exploration_score" NUMERIC(5,2)

"navigation_score" NUMERIC(5,2)

"tool_usage_score" NUMERIC(5,2)

"risk_score" NUMERIC(5,2)

"noise_score" NUMERIC(5,2)

"revive_score" NUMERIC(5,2)

Indexes {

(match_id, user_id) [unique, name: "uq_match_score"]

}

}

Table "player_ai_profiles" {

"user_id" BIGINT [pk]

"survival_score" NUMERIC(5,2) [default: 0]

"objective_score" NUMERIC(5,2) [default: 0]

"teamwork_score" NUMERIC(5,2) [default: 0]

"exploration_score" NUMERIC(5,2) [default: 0]

"navigation_score" NUMERIC(5,2) [default: 0]

"tool_usage_score" NUMERIC(5,2) [default: 0]

"risk_score" NUMERIC(5,2) [default: 0]

"noise_score" NUMERIC(5,2) [default: 0]

"revive_score" NUMERIC(5,2) [default: 0]

"updated_at" TIMESTAMP [not null, default: `CURRENT_TIMESTAMP`]

}

Table "team_profiles" {

"id" BIGSERIAL [pk, increment]

"member_hash" VARCHAR(255)

"objective_time" NUMERIC(10,2)

"split_time" NUMERIC(10,2)

"avg_distance" NUMERIC(10,2)

"revive_success" NUMERIC(5,2)

"resource_efficiency" NUMERIC(5,2)

"communication_score" NUMERIC(5,2)

"wipe_recovery_score" NUMERIC(5,2)

"updated_at" TIMESTAMP [not null, default: `CURRENT_TIMESTAMP`]

}

Table "adaptive_decisions" {

"id" BIGSERIAL [pk, increment]

"match_id" BIGINT [not null]

"phase" VARCHAR(50)

"metric_snapshot" JSONB

"before_config" JSONB

"after_config" JSONB

"reason_code" VARCHAR(100)

"created_at" TIMESTAMP [not null, default: `CURRENT_TIMESTAMP`]

}

Table "generated_contents" {

"id" BIGSERIAL [pk, increment]

"scenario_id" BIGINT [not null]

"content_type" VARCHAR(50) [not null]

"content_text" TEXT [not null]

"status" VARCHAR(30)

"model_ref" VARCHAR(100)

"created_at" TIMESTAMP [not null, default: `CURRENT_TIMESTAMP`]

}

Ref "fk_sessions_user":"users"."id" <? "sessions"."user_id" [delete: cascade]

Ref "fk_player_progress_user":"users"."id" <? "player_progress"."user_id" [delete: cascade]

Ref "fk_wallet_user":"users"."id" <? "wallets"."user_id" [delete: cascade]

Ref "fk_wallet_transactions_user":"users"."id" <? "wallet_transactions"."user_id" [delete: cascade]

Ref "fk_inventory_user":"users"."id" <? "inventory"."user_id" [delete: cascade]

Ref "fk_inventory_item":"shop_items"."id" <? "inventory"."item_id" [delete: restrict]

Ref "fk_loadout_user":"users"."id" <? "player_loadouts"."user_id" [delete: cascade]

Ref "fk_loadout_outfit":"shop_items"."id" ?<? "player_loadouts"."outfit_id"

Ref "fk_loadout_flashlight":"shop_items"."id" ?<? "player_loadouts"."flashlight_skin_id"

Ref "fk_loadout_nameplate":"shop_items"."id" ?<? "player_loadouts"."nameplate_id"

Ref "fk_loadout_badge":"shop_items"."id" ?<? "player_loadouts"."badge_id"

Ref "fk_loadout_emote":"shop_items"."id" ?<? "player_loadouts"."emote_id"

Ref "fk_loadout_spray":"shop_items"."id" ?<? "player_loadouts"."spray_id"

Ref "fk_payment_order_user":"users"."id" <? "payment_orders"."user_id" [delete: cascade]

Ref "fk_payment_transaction_order":"payment_orders"."id" <? "payment_transactions"."order_id" [delete: cascade]

Ref "fk_match_scenario":"scenario_configs"."id" ?<? "matches"."scenario_id" [delete: set null]

Ref "fk_match_member_match":"matches"."id" <? "match_members"."match_id" [delete: cascade]

Ref "fk_match_member_user":"users"."id" <? "match_members"."user_id" [delete: cascade]

Ref "fk_match_result_match":"matches"."id" <? "match_results"."match_id" [delete: cascade]

Ref "fk_telemetry_match":"matches"."id" <? "telemetry_events"."match_id" [delete: cascade]

Ref "fk_telemetry_user":"users"."id" ?<? "telemetry_events"."user_id" [delete: set null]

Ref "fk_match_telemetry_match":"matches"."id" <? "match_telemetry"."match_id" [delete: cascade]

Ref "fk_match_telemetry_user":"users"."id" ?<? "match_telemetry"."user_id" [delete: set null]

Ref "fk_match_score_match":"matches"."id" <? "match_scores"."match_id" [delete: cascade]

Ref "fk_match_score_user":"users"."id" <? "match_scores"."user_id" [delete: cascade]

Ref "fk_ai_profile_user":"users"."id" <? "player_ai_profiles"."user_id" [delete: cascade]

Ref "fk_adaptive_decision_match":"matches"."id" <? "adaptive_decisions"."match_id" [delete: cascade]

Ref "fk_generated_content_scenario":"scenario_configs"."id" <? "generated_contents"."scenario_id" [delete: cascade]

- <br><br>4. Các API project

| Nhóm | Method | Endpoint | Mục đích chính | DB liên quan |
| --- | --- | --- | --- | --- |
| Auth | POST | /auth/register | Đăng ký tài khoản | User |
| Auth | POST | /auth/login | Đăng nhập | User, Session |
| Auth | POST | /auth/logout | Đăng xuất | Session |
| Profile | GET | /me/profile | Lấy thông tin người chơi | User, Progress, Wallet |
| Shop | GET | /shop/catalog | Lấy danh sách cosmetic | ShopItem |
| Shop | POST | /shop/purchase | Mua cosmetic | Wallet, Transaction, Inventory |
| Inventory | GET | /inventory | Xem vật phẩm đang sở hữu | Inventory |
| Inventory | POST | /inventory/equip | Trang bị cosmetic | Inventory, PlayerLoadout |
| Payment | POST | /payments/orders | Tạo đơn thanh toán | PaymentOrder |
| Payment | GET | /payments/orders/{id} | Kiểm tra trạng thái thanh toán | PaymentOrder |
| Payment | POST | /payments/verify | Xác nhận thanh toán | PaymentOrder, PaymentTransaction |
| Match | POST | /matches | Tạo Match và lấy Scenario | Match, ScenarioConfig |
| Match | POST | /matches/{id}/result | Lưu kết quả trận | MatchResult, Wallet, Progress |
| Telemetry | POST | /telemetry/batch | Gửi dữ liệu gameplay | TelemetryEvent |
| AED | GET | /experiments/config | Lấy cấu hình Fixed/Adaptive | Scenario/AED |
| GenAI | POST | /ai/briefing | Tạo Mission Briefing | GeneratedContent |
| System | GET | /health | Kiểm tra Backend | Không cần DB |

<br>5. Các rule Database

User/Auth: password chỉ lưu dưới dạng hash.

Wallet: Backend là nguồn sự thật; Unity không được tự thay đổi balance.

WalletTransaction: dùng transaction log/ledger để truy vết mọi thay đổi currency.

Shop: chỉ cosmetic, không pay-to-win.

Match Result: Host/Server tạo authoritative result; Backend chỉ cấp reward từ result hợp lệ.

Duplicate Match Result: retry request không được cấp reward hai lần.

Payment: client không được tự báo “payment success”; Backend phải verify provider.

Payment idempotency: callback provider gửi nhiều lần vẫn chỉ fulfill một lần.

Telemetry: phải có matchId, player context khi phù hợp, event type, timestamp, schemaVersion, reasonCode.

PlayerAIProfile: chỉ được cập nhật từ telemetry/score được Backend chấp nhận.

AdaptiveDecision: phải lưu reasonCode và before/after config để giải thích AED

Runtime data: movement, monster position, puzzle progress từng frame không đưa vào persistent DB.
