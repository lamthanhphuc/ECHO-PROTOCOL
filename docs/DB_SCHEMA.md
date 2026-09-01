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
| Id | UUID PK | |
| Name | VARCHAR(100) | |
| Description | TEXT | |
| Price | INT | |
| ImageUrl | VARCHAR(500) | Cosmetic image |
| Category | VARCHAR(50) | |
| IsActive | BOOLEAN | |
| CreatedAt | TIMESTAMPTZ | |

---

## Inventories

| Column | Type | Notes |
|---|---|---|
| Id | UUID PK | |
| UserId | UUID FK → Users | |
| ShopItemId | UUID FK → ShopItems | |
| AcquiredAt | TIMESTAMPTZ | |
| Source | VARCHAR(50) | purchase, reward |

---

## EquippedItems

| Column | Type | Notes |
|---|---|---|
| Id | UUID PK | |
| UserId | UUID FK → Users | |
| InventoryItemId | UUID FK → Inventories | |
| Slot | VARCHAR(50) | hat, outfit, etc. |
| EquippedAt | TIMESTAMPTZ | |

---

## PurchaseTransactions

| Column | Type | Notes |
|---|---|---|
| Id | UUID PK | |
| UserId | UUID FK → Users | |
| ShopItemId | UUID FK → ShopItems | |
| Amount | INT | |
| Status | VARCHAR(20) | completed, failed |
| CreatedAt | TIMESTAMPTZ | |

---

## WalletTransactions

| Column | Type | Notes |
|---|---|---|
| Id | UUID PK | |
| WalletId | UUID FK → Wallets | |
| Amount | INT | +/- |
| Type | VARCHAR(50) | purchase, reward, admin |
| ReferenceId | UUID NULL | |
| CreatedAt | TIMESTAMPTZ | |

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

---

## Seed data (future phase)

- Admin user (hashed password via env/seed script — not in repo)
- 15 cosmetic `ShopItems` with `ImageUrl`
