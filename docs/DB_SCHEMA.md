# ECHO PROTOCOL — Database Schema (Skeleton)

Polyglot persistence baseline:

- PostgreSQL is the source of truth for transactional/relational business data.
- MongoDB stores raw, versioned telemetry and detailed AI/gameplay event context.
- Cross-database references use the same UUID values (`matchId`, `userId`) but are not foreign keys.
- No business operation uses a distributed transaction across both databases.

PostgreSQL naming uses `PascalCase` tables (matching backend entities).

## Entity relationship overview

```text
Users 1──1 PlayerProfiles
Users 1──1 Wallets
Users 1──* Inventories
Users 1──* EquippedItems
Users 1──* PurchaseTransactions
Users 1──* PlayerMatchLogs
ShopItems 1──* Inventories
ShopItems 1──* PurchaseTransactions
MatchLogs 1──* PlayerMatchLogs
Wallets 1──* WalletTransactions
```

---

## Users

| Column | Type | Notes |
|---|---|---|
| Id | UUID PK | |
| Username | VARCHAR(100) UNIQUE | Stored normalized lowercase |
| PasswordHash | VARCHAR(255) | BCrypt |
| Role | VARCHAR(20) | `PLAYER`, `ADMIN` (string) |
| Status | VARCHAR(20) | `ACTIVE`, `LOCKED` (string) |
| CreatedAt | TIMESTAMPTZ | UTC |
| UpdatedAt | TIMESTAMPTZ | UTC |

**No Email column in Auth Foundation phase.**

---

## PlayerProfiles

| Column | Type | Notes |
|---|---|---|
| Id | UUID PK | |
| UserId | UUID FK → Users | UNIQUE, ON DELETE RESTRICT |
| DisplayName | VARCHAR(100) | |
| TotalMatches | INT | Default 0, CHECK >= 0 |
| TotalWins | INT | Default 0, CHECK >= 0, CHECK <= TotalMatches |
| CreatedAt | TIMESTAMPTZ | UTC |
| UpdatedAt | TIMESTAMPTZ | UTC |

---

## Wallets

| Column | Type | Notes |
|---|---|---|
| Id | UUID PK | |
| UserId | UUID FK → Users | UNIQUE, ON DELETE RESTRICT |
| Balance | INT | CHECK >= 0; default 500 for new players |
| UpdatedAt | TIMESTAMPTZ | UTC |

---

## ShopItems

| Column | Type | Notes |
|---|---|---|
| ItemId | UUID PK | Public immutable catalog identity |
| ItemName | VARCHAR(100) | |
| Description | VARCHAR(1000) | |
| Price | INT | Check `>= 0` |
| AssetReference | VARCHAR(500) | Unity cosmetic asset/address reference |
| Category | VARCHAR(50) | Normalized uppercase |
| IsActive | BOOLEAN | Disabled items are hidden from public catalog |
| CreatedAtUtc | TIMESTAMPTZ | UTC |
| UpdatedAtUtc | TIMESTAMPTZ | UTC |

---

## InventoryItems

| Column | Type | Notes |
|---|---|---|
| InventoryItemId | UUID PK | |
| UserId | UUID FK → Users | |
| ShopItemId | UUID FK → ShopItems | |
| Source | VARCHAR(50) | `PURCHASE` in M4-015 |
| PurchaseId | UUID NULL UNIQUE FK | References PurchaseTransactions for purchase grants |
| AcquiredAtUtc | TIMESTAMPTZ | UTC |

Unique `(UserId, ShopItemId)` enforces one owned copy per cosmetic.

---

## PlayerLoadoutItems (M4-052)

| Column | Type | Notes |
|---|---|---|
| UserId | UUID PK/FK → Users | JWT owner |
| SlotId | VARCHAR(50) PK | Current contract: `CHARACTER` or `TEAM_TOOL_1` |
| InventoryItemId | UUID | Composite FK `(UserId, InventoryItemId)` enforces ownership |
| EquippedAtUtc | TIMESTAMPTZ | Current selection timestamp |
| UpdatedAtUtc | TIMESTAMPTZ | Must be >= EquippedAtUtc |

Unique `(UserId, SlotId)` permits one item per slot. Unique `(UserId, InventoryItemId)` prevents
the same owned item from occupying multiple slots. Slot availability is configuration/service
validation rather than a database enum so legacy `TEAM_TOOL_2` rows can remain recoverable.
Such disabled-slot rows are excluded from loadout responses and gameplay mapping; Inventory and
Purchase ownership is unchanged until an explicit cleanup is approved.
Category/slot compatibility is validated inside the transactional service because it spans the
InventoryItem and ShopItem records.

---

## PurchaseTransactions

| Column | Type | Notes |
|---|---|---|
| Id | UUID PK | |
| UserId | UUID FK → Users | |
| ShopItemId | UUID FK → ShopItems | |
| IdempotencyKey | VARCHAR(100) | Unique with UserId |
| PriceAtPurchase | INT | Server-resolved price, check `>= 0` |
| WalletTransactionId | UUID UNIQUE FK | References WalletTransactions |
| Status | VARCHAR(20) | `COMPLETED` |
| CreatedAtUtc | TIMESTAMPTZ | UTC |

---

## WalletTransactions

| Column | Type | Notes |
|---|---|---|
| Id | UUID PK | |
| WalletId | UUID FK → Wallets | |
| Amount | INT | Reward `>= 0`; purchase `<= 0` |
| Type | VARCHAR(30) | `MATCH_REWARD`, `PURCHASE` |
| BalanceBefore | INT | Non-negative |
| BalanceAfter | INT | Non-negative; after = before + amount |
| ReferenceId | UUID | MatchId or PurchaseId |
| Description | VARCHAR(255) | Audit description |
| CreatedAtUtc | TIMESTAMPTZ | UTC |

---

## MatchLogs

| Column | Type | Notes |
|---|---|---|
| Id | UUID PK | |
| RoomCode | VARCHAR(20) | |
| HostUserId | UUID FK → Users | |
| MapId | VARCHAR(50) | |
| StartedAt | TIMESTAMPTZ | |
| EndedAt | TIMESTAMPTZ NULL | |
| Result | VARCHAR(50) | escaped, failed |
| ObjectiveData | JSONB | fuses, power, etc. |

---

## PlayerMatchLogs

| Column | Type | Notes |
|---|---|---|
| Id | UUID PK | |
| MatchLogId | UUID FK → MatchLogs | |
| UserId | UUID FK → Users | |
| Escaped | BOOLEAN | |
| Deaths | INT | |
| StatsJson | JSONB NULL | |

---

## MongoDB: `telemetry_events`

Raw gameplay and AI behavior events are documents in MongoDB. The `_id` field is the
client-generated canonical `id`, making retries idempotent.

| Field | BSON type | Notes |
|---|---|---|
| `_id` | UUID | Unique event ID / idempotency key |
| `matchId` | UUID | Logical reference to PostgreSQL match ID |
| `userId` | UUID, optional | Logical reference to PostgreSQL user ID |
| `eventType` | string | Canonical upper snake case event type |
| `ts` | date | Authoritative UTC occurrence time |
| `eventSequence` | int64 | Host-owned monotonic sequence within the match |
| `valueJson` | document | Canonical `{ context, data }` snapshot |
| `reasonCode` | string, optional | AI/gameplay reason code |
| `schemaVersion` | string | Canonical wire version, currently `"1.1"` |
| `semanticFingerprint` | string | SHA-256 identity-conflict evidence |
| `ingestedAt` | date | Backend UTC ingestion time |

Indexes: `_id` unique, unique `(matchId,eventSequence)`, `(matchId,ts)`, `(userId,ts)`, and
`eventType`.

M2 ingestion accepts schema version `"1.1"`. A non-null `userId` must match the authenticated JWT
user, unless that JWT belongs to the bound Fusion Host and the target user exists in
`MatchPlayerBindings`. System/team events may omit it. Events outside the configured time window or with an
oversized `valueJson` document receive a permanent per-item rejection. Retention/TTL remains a later
milestone decision and is not implied by the ingestion-age validation.

---

## MatchAuthorityBindings (M2 implemented)

| Column | Type | Notes |
|---|---|---|
| MatchId | UUID PK | Published into the Fusion session property |
| FusionSessionName | VARCHAR(128) | Exact room binding |
| HostUserId | UUID FK → Users | JWT identity allowed to renew/start/end/delegate |
| MaxPlayers | INT | API restricts to 2–4 |
| Status | VARCHAR(20) | Lobby, InMatch, Ended |
| LeaseExpiresAtUtc | TIMESTAMPTZ | Expired leases cannot mutate authority state |
| CreatedAtUtc / UpdatedAtUtc | TIMESTAMPTZ | Audit timestamps |
| EndedAtUtc | TIMESTAMPTZ NULL | Telemetry delegation retention anchor |

## MatchPlayerBindings (M2 implemented)

| Column | Type | Notes |
|---|---|---|
| Id | UUID PK | Binding identity |
| MatchId | UUID FK → MatchAuthorityBindings | Cascades on match deletion |
| UserId | UUID FK → Users | Backend JWT identity |
| FusionActorNumber | INT | Verified by Host against the RPC sender |
| JoinProofId | UUID UNIQUE | Signed proof identity |
| BoundAtUtc / LastSeenAtUtc | TIMESTAMPTZ | Audit timestamps |
| DisconnectedAtUtc | TIMESTAMPTZ NULL | Null means currently connected |

Unique indexes enforce one backend user and one Fusion actor per match.

The M4-009 migration promotes `(MatchId, UserId)` to an alternate unique key so
`MatchResultPlayers` can reference the authoritative roster at database level.

## MatchResults (M4-009 implemented)

| Column | Type | Notes |
|---|---|---|
| MatchId | UUID PK/FK -> MatchAuthorityBindings | One final Result per authoritative match; ON DELETE RESTRICT |
| SubmittedByUserId | UUID FK -> Users | Authenticated bound Host; ON DELETE RESTRICT |
| Outcome | VARCHAR(20) | Normal Host flow accepts `WIN`, `LOSE` |
| StartedAtUtc / EndedAtUtc | TIMESTAMPTZ | Server timestamps; Ended >= Started |
| DurationSeconds | INT | Server-derived, CHECK 60-900 |
| ObjectiveCompletion | NUMERIC(5,4) | CHECK 0-1 |
| PlayerCount | INT | CHECK 1-4; retains the current M2 minimum-player behavior |
| PayloadHash | CHAR(64) | Canonical SHA-256 used for retry/conflict detection |
| RewardStatus | VARCHAR(20) | `Pending` in M4-009 |
| SubmittedAtUtc | TIMESTAMPTZ | Server audit timestamp |

`MatchAuthorityBindings.StartedAtUtc` is nullable for compatibility with existing rows and is set
once by `StartAsync`. A new result requires this value.

## MatchResultPlayers (M4-009 implemented)

| Column | Type | Notes |
|---|---|---|
| MatchId + UserId | Composite PK | One player Result per match |
| MatchId | FK -> MatchResults | ON DELETE CASCADE |
| MatchId + UserId | Composite FK -> MatchPlayerBindings | Rejects unbound users; ON DELETE RESTRICT |
| Survived / Disconnected | BOOLEAN | Disconnected must match the stored binding state |
| DetectionCount / DownedCount / ReviveCount | INT | CHECK >= 0 |
| ObjectiveContribution | INT | CHECK >= 0 |

The Result transaction inserts both tables and transitions the authority binding to `Ended`.
M4-009 creates no wallet ledger and does not update wallet/profile/progression values.

---

## Seed data (future phase)

- Admin user (hashed password via env/seed script — not in repo)
- Development-only idempotent seed of 3 test cosmetic `ShopItems`; replace with an approved catalog before release
