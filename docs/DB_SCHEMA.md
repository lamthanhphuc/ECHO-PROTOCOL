# ECHO PROTOCOL — Database Schema (Skeleton)

PostgreSQL. Naming: `PascalCase` tables, `snake_case` columns optional per team convention (backend uses PascalCase entities).

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
MatchLogs 1──* AIBehaviorLogs
Wallets 1──* WalletTransactions
```

---

## Users

| Column | Type | Notes |
|---|---|---|
| Id | UUID PK | |
| Username | VARCHAR(50) UNIQUE | |
| Email | VARCHAR(255) UNIQUE | |
| PasswordHash | VARCHAR(255) | BCrypt |
| Role | VARCHAR(20) | Player, Admin |
| CreatedAt | TIMESTAMPTZ | |
| UpdatedAt | TIMESTAMPTZ | |

---

## PlayerProfiles

| Column | Type | Notes |
|---|---|---|
| Id | UUID PK | |
| UserId | UUID FK → Users | UNIQUE |
| DisplayName | VARCHAR(50) | |
| AvatarUrl | VARCHAR(500) NULL | |
| CreatedAt | TIMESTAMPTZ | |

---

## Wallets

| Column | Type | Notes |
|---|---|---|
| Id | UUID PK | |
| UserId | UUID FK → Users | UNIQUE |
| Balance | INT | In-game currency |
| UpdatedAt | TIMESTAMPTZ | |

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

## AIBehaviorLogs

| Column | Type | Notes |
|---|---|---|
| Id | UUID PK | |
| MatchLogId | UUID FK → MatchLogs | |
| Timestamp | TIMESTAMPTZ | |
| BehaviorType | VARCHAR(50) | chase, patrol, adaptive |
| ContextJson | JSONB | player positions, noise |
| Decision | VARCHAR(100) | |

---

## Seed data (future phase)

- Admin user (hashed password via env/seed script — not in repo)
- 15 cosmetic `ShopItems` with `ImageUrl`
