# ECHO PROTOCOL — API Specification

Base URL (local dev): `http://localhost:5042/api`

All responses use wrapper:

```json
{
  "success": true,
  "message": "string",
  "data": {},
  "errorCode": null
}
```

Error responses set `success: false` and include `errorCode`.

> **Note:** `GET /api/auth/me` remains unchanged for M2 compatibility. The expanded player profile is available from `GET /api/player/me`.

---

## Health

### `GET /health`

**Auth:** None  
**Status:** Implemented

Compatibility route: `GET /api/health`

The endpoint uses ASP.NET Core Health Checks and verifies PostgreSQL and MongoDB connectivity.
It returns HTTP 200 only when the API can connect to both databases, otherwise HTTP 503.

**Healthy response:**

```json
{
  "status": "Healthy",
  "service": "EchoProtocol.Api",
  "checks": {
    "postgresql": "Healthy",
    "mongodb": "Healthy"
  }
}
```

---

## Auth

### `POST /api/auth/register`

**Auth:** `[AllowAnonymous]`  
**Status:** Implemented

**Body:**

```json
{
  "email": "player01@echo.invalid",
  "username": "player01",
  "password": "123456",
  "confirmPassword": "123456"
}
```

**Success (201):**

```json
{
  "success": true,
  "message": "Register successfully",
  "data": {
    "id": "uuid",
    "username": "player01",
    "role": "PLAYER"
  }
}
```

**Errors:**

| HTTP | errorCode | When |
|---|---|---|
| 400 | `VALIDATION_ERROR` | Invalid input, whitespace-only fields, password &lt; 6 chars |
| 400 | `PASSWORD_TOO_LONG` | Password exceeds 72 UTF-8 bytes |
| 400 | `PASSWORD_CONFIRMATION_MISMATCH` | Password ≠ confirmPassword |
| 409 | `USERNAME_ALREADY_EXISTS` | Duplicate username (case-insensitive) |

---

### `POST /api/auth/login`

**Auth:** `[AllowAnonymous]`  
**Status:** Implemented

**Body:**

```json
{
  "username": "player01",
  "password": "123456"
}
```

**Success (200):**

```json
{
  "success": true,
  "message": "Login successfully",
  "data": {
    "accessToken": "jwt",
    "expiresAt": "2026-07-11T10:00:00Z",
    "user": {
      "id": "uuid",
      "username": "player01",
      "role": "PLAYER"
    },
    "wallet": {
      "balance": 500
    }
  }
}
```

**Errors:**

| HTTP | errorCode | When |
|---|---|---|
| 400 | `VALIDATION_ERROR` | Missing/whitespace fields |
| 400 | `PASSWORD_TOO_LONG` | Password exceeds 72 UTF-8 bytes |
| 401 | `INVALID_CREDENTIALS` | Wrong username or password |
| 403 | `ACCOUNT_LOCKED` | Valid credentials but account locked |

---

### `GET /api/auth/me`

**Auth:** `[Authorize]` Bearer JWT  
**Status:** Implemented

**Success (200):**

```json
{
  "success": true,
  "message": "Current user loaded",
  "data": {
    "id": "uuid",
    "username": "player01",
    "role": "PLAYER",
    "displayName": "player01",
    "walletBalance": 500
  }
}
```

**Errors:**

| HTTP | errorCode | When |
|---|---|---|
| 401 | `UNAUTHORIZED` | No token |
| 401 | `TOKEN_INVALID` | Invalid/expired token or bad claim |
| 403 | `ACCOUNT_LOCKED` | User status is LOCKED (even with valid JWT) |
| 403 | `FORBIDDEN` | Authorization failure |
| 404 | `NOT_FOUND` | Token valid but user deleted |

**Role values:** `"PLAYER"`, `"ADMIN"` (strings)

---

## Telemetry

### `POST /api/telemetry/batch`

**Auth:** Bearer JWT
**Status:** Implemented and verified for canonical wire schema `"1.1"`

Maximum batch size is configured by `MongoDb:MaxBatchSize` (default 500). Raw events are
stored in MongoDB; business state and aggregate profiles remain in PostgreSQL.
For the M2 client-auth flow, a non-null event `userId` must match the authenticated JWT user.
Team/system events may omit `userId`; trusted-host delegation is deferred until the multiplayer
authority contract provides a verifiable host identity.

```json
{
  "events": [
    {
      "id": "uuid",
      "matchId": "uuid",
      "userId": null,
      "eventType": "MATCH_STARTED",
      "ts": "2026-08-27T10:15:30Z",
      "valueJson": {
        "context": {
          "eventSequence": 1,
          "authorityTick": null,
          "scenarioConfigVersion": "SCENARIO-1",
          "policyVersion": "M1-015-v0",
          "configSource": "FIXED",
          "teamSize": 4,
          "buildVersion": "BUILD-1",
          "mapContentVersion": "RF-1",
          "contentWhitelistVersion": "WL-1",
          "researchCaptureEnabled": false
        },
        "data": { "mapId": "RESEARCH_FACILITY" }
      },
      "reasonCode": "MATCH_READY",
      "schemaVersion": "1.1"
    }
  ]
}
```

The endpoint returns a semantic acknowledgement for every submitted event. Valid events in a
mixed batch may be accepted while invalid events are permanently rejected. Transport/storage
failures are transient so the Unity buffer retries the same immutable event.

```json
{
  "success": true,
  "message": "Telemetry batch processed",
  "data": {
    "items": [
      { "id": "uuid", "status": "ACCEPTED", "rejectReason": null },
      { "id": "uuid", "status": "DUPLICATE_ALREADY_ACCEPTED", "rejectReason": null },
      { "id": "uuid", "status": "PERMANENTLY_REJECTED", "rejectReason": "TELEMETRY_SCHEMA_UNSUPPORTED" }
    ]
  },
  "errorCode": null
}
```

Allowed item statuses are `ACCEPTED`, `DUPLICATE_ALREADY_ACCEPTED`,
`PERMANENTLY_REJECTED`, and `TRANSIENT_FAILURE`. Successful retries do not create duplicate
documents. MongoDB enforces unique logical event ID and unique `(matchId,eventSequence)`.

Configuration defaults:

| Key | Default |
|---|---:|
| `MongoDb:MaxBatchSize` | 500 events |
| `MongoDb:SupportedSchemaVersion` | `"1.1"` |
| `MongoDb:MaxValueJsonBytes` | 32768 bytes/event |
| `MongoDb:MaxFutureSkewMinutes` | 5 minutes |
| `MongoDb:MaxEventAgeDays` | 7 days |

| HTTP | Error code | Meaning |
|---:|---|---|
| 400 | `VALIDATION_ERROR` | Empty batch, oversized batch, or malformed request envelope |
| 401 | `UNAUTHORIZED` / `TOKEN_INVALID` | Missing, invalid, or expired JWT |
| 503 | `TELEMETRY_UNAVAILABLE` | MongoDB is temporarily unavailable |

Schema, payload, timestamp, identity, sequence, and user-attribution errors are normally returned
as per-item `PERMANENTLY_REJECTED` acknowledgements rather than failing the whole batch.

MongoDB unavailability does not stop the API process or PostgreSQL-backed Auth endpoints. The
database-aware health endpoint reports HTTP 503 until MongoDB recovers.

---

## Player

### `GET /api/player/me`

**Auth:** Bearer JWT
**Status:** Implemented (M4-011)

The user identity comes only from the validated JWT subject claim.

```json
{
  "success": true,
  "message": "Player profile retrieved",
  "data": {
    "userId": "00000000-0000-0000-0000-000000000000",
    "displayName": "EchoPlayer",
    "totalMatches": 0,
    "totalWins": 0,
    "experiencePoints": 0,
    "level": 1,
    "walletBalance": 500
  },
  "errorCode": null
}
```

Inventory, loadout, and `PlayerAIProfile` are intentionally excluded.

---

## AI Profiles (M4-050)

These routes are implemented for M4-050 but remain **proposed contracts pending route freeze with Networking/AI**.
All use Bearer JWT; identity/host authority comes only from validated claims and backend match bindings.

### `GET /api/profiles/ai/me`

Returns only the caller's cross-match `PlayerAIProfile`. `SURVIVAL` and `NOISE` include score,
status, sample count, and provenance versions. `COLD_START` is explicit; DEFERRED dimensions
serialize with `score: null`, `status: "DEFERRED"`, and no fabricated sample count.

### `GET /api/matches/{matchId}/player-ai-profiles`

Host-only roster read. The caller must equal the backend-bound `HostUserId`; the returned users
come from `MatchPlayerBinding`, including disconnected bindings. A roster member with no processed
profile is returned with `profile: null` rather than synthetic evidence.

### `GET /api/matches/{matchId}/team-profile`

Returns only the requested match-scoped TeamProfile to its verified host or bound roster members.
It never substitutes a previous match. In v1.1, `teamPerformanceScore` remains `null` with
`teamPerformanceStatus: "INCOMPLETE"` while required components are DEFERRED.

---

## Shop

### `GET /api/shop/items`

**Auth:** Optional (public catalog)  
**Status:** Implemented (M4-012/M4-013)

**Query:** `category`, `page`, `pageSize`

- `page` defaults to `1`.
- `pageSize` defaults to `20` and must be between `1` and `100`.
- `category` is trimmed and matched case-insensitively against the normalized catalog category.
- Only active items are returned.

```json
{
  "success": true,
  "message": "Shop catalog retrieved",
  "data": {
    "items": [
      {
        "itemId": "11000000-0000-0000-0000-000000000004",
        "name": "Test Explorer Character",
        "category": "CHARACTER",
        "price": 100,
        "description": "Test-only Character unlock.",
        "assetReference": "test://characters/explorer"
      }
    ],
    "page": 1,
    "pageSize": 20,
    "totalItems": 1,
    "totalPages": 1
  },
  "errorCode": null
}
```

### `POST /api/shop/purchase`

**Auth:** Bearer JWT
**Status:** Implemented (M4-014)

**Body:**

```json
{
  "itemId": "11000000-0000-0000-0000-000000000001",
  "idempotencyKey": "unity-request-4bffd8c9"
}
```

The backend resolves `userId`, price, and wallet balance. Unknown fields such as client-supplied `price`, `userId`, or `walletBalance` are rejected. First success returns HTTP 201; an identical retry returns HTTP 200 with `isReplay: true`.

```json
{
  "success": true,
  "message": "Purchase completed",
  "data": {
    "purchaseId": "00000000-0000-0000-0000-000000000001",
    "walletTransactionId": "00000000-0000-0000-0000-000000000002",
    "itemId": "11000000-0000-0000-0000-000000000001",
    "pricePaid": 100,
    "walletBalance": 400,
    "purchasedAtUtc": "2026-09-20T16:00:00Z",
    "isReplay": false
  },
  "errorCode": null
}
```

---

## Inventory

### `GET /api/inventory/me`

**Auth:** Bearer JWT  
**Status:** Implemented (M4-015)

Only inventory belonging to the JWT subject is returned.

```json
{
  "success": true,
  "message": "Inventory retrieved",
  "data": {
    "items": [
      {
        "inventoryItemId": "00000000-0000-0000-0000-000000000003",
        "itemId": "11000000-0000-0000-0000-000000000001",
        "itemName": "Test Explorer Character",
        "category": "CHARACTER",
        "assetReference": "test://characters/explorer",
        "source": "PURCHASE",
        "purchaseId": "00000000-0000-0000-0000-000000000001",
        "acquiredAtUtc": "2026-09-20T16:00:00Z"
      }
    ]
  },
  "errorCode": null
}
```

### `POST /api/inventory/equip`

**Auth:** Bearer JWT  
**Status:** Implemented (M4-052)

**Body:**

```json
{
  "slotId": "TEAM_TOOL_1",
  "itemId": "uuid"
}
```

**Rules:**

- Player may only equip items they already own.
- Backend resolves category from the shop item definition.
- The current gameplay contract exposes exactly `CHARACTER` and `TEAM_TOOL_1`.
- `CHARACTER` accepts only Character items; `TEAM_TOOL_1` accepts only TeamTool items.
- `TEAM_TOOL_2` and every other `TEAM_TOOL_n` value are rejected with `LOADOUT_SLOT_INVALID`.
- A slot contains at most one item.
- Retrying the same slot/item is idempotent.

### `GET /api/inventory/loadout`

**Auth:** Bearer JWT
**Status:** Implemented (M4-052)

Returns the JWT player's current Character and at most one TeamTool selection in `teamTools`.
Empty slots are omitted; an empty Character is represented by `character: null`. Rows from legacy,
disabled slots such as `TEAM_TOOL_2` are not returned and therefore must not enter gameplay mapping;
the rows and their Inventory/Purchase records are retained for explicit operational cleanup.

Networking must resolve the `TEAM_TOOL_1` ShopItem identity through the approved content mapping
before assigning `LobbyPlayerState.ToolId`. A ShopItem GUID must never be cast or otherwise mapped
directly to a gameplay `ToolId`. `CHARACTER` remains a separate slot.

### `POST /api/inventory/unequip`

**Auth:** Bearer JWT
**Status:** Implemented (M4-052)

```json
{ "slotId": "TEAM_TOOL_1" }
```

TeamTool unequip is idempotent. M4-052 rejects Character unequip with
`LOADOUT_CHARACTER_REQUIRED`; the pre-match Character requirement still needs Gameplay/Networking freeze.

---

## Matches

### `PUT /api/matches/{matchId}/result`

**Auth:** Bearer JWT of the bound Host
**Status:** Implemented for M4-009

**Request body:**

```json
{
  "outcome": "WIN",
  "objectiveCompletion": 1.0,
  "players": [
    {
      "userId": "uuid",
      "survived": true,
      "disconnected": false,
      "detectionCount": 3,
      "downedCount": 1,
      "reviveCount": 2,
      "objectiveContribution": 1
    }
  ]
}
```

**Rules:**

- `matchId` is the existing backend-created `MatchAuthorityBinding.MatchId`.
- Only the persisted Host may submit. The match must be `InMatch`, started, and have a valid lease.
- Request players must exactly match all persisted bindings, including disconnected bindings.
- Allowed normal Host outcomes are `WIN` and `LOSE`. M4-009 does not synthesize `HOST_DISCONNECTED`.
- Start/end timestamps and duration are server-derived. Duration must be 60-900 seconds and
  `objectiveCompletion` must be 0-1. Counts must be non-negative.
- Unknown request fields are rejected. The contract has no reward, XP, wallet, Host identity,
  client timestamps, or AI-log field.
- First submission returns HTTP 201, stores Result + players, and transitions the match to `Ended`
  in the same PostgreSQL transaction.
- An identical retry by the same authorized Host returns HTTP 200 and the stored result with
  `isReplay: true`. Different content for the same match returns HTTP 409
  `MATCH_RESULT_CONFLICT`.
- A match already ended through `/end` without a Result cannot accept a late Result.
- `rewardStatus` is always `Pending` in M4-009. No wallet/profile/progression mutation occurs.

**Success data:**

```json
{
  "matchId": "uuid",
  "outcome": "WIN",
  "startedAtUtc": "2026-09-20T10:00:00Z",
  "endedAtUtc": "2026-09-20T10:12:00Z",
  "durationSeconds": 720,
  "objectiveCompletion": 1.0,
  "playerCount": 2,
  "rewardStatus": "Pending",
  "submittedAtUtc": "2026-09-20T10:12:00Z",
  "isReplay": false,
  "players": []
}
```

| HTTP | Error code | Meaning |
|---:|---|---|
| 400 | `MATCH_RESULT_INVALID_PAYLOAD` | Unsupported field, outcome, objective value, or player stat |
| 400 | `MATCH_RESULT_INVALID_ROSTER` | Request roster/disconnect state does not match bindings |
| 400 | `MATCH_RESULT_DUPLICATE_PLAYER` | A user appears more than once |
| 400 | `MATCH_RESULT_INVALID_DURATION` | Server-derived duration is outside 60-900 seconds |
| 403 | `MATCH_AUTHORITY_FORBIDDEN` | Caller is not the bound Host |
| 404 | `MATCH_NOT_FOUND` | Match binding does not exist |
| 409 | `MATCH_RESULT_INVALID_STATE` | Match is not an eligible `InMatch` match |
| 409 | `MATCH_RESULT_CONFLICT` | A different final result already exists |
| 410 | `MATCH_LEASE_EXPIRED` | Host authority lease expired before first submission |

The older SRS/API draft `POST /api/matches/logs` is superseded for M4-009. It combined AI logs,
reward calculation, and client timestamps; those responsibilities conflict with the canonical
Telemetry v1.1 pipeline and the staged M4-009/M4-010 delivery.

---

## Match authority binding (M2 implemented)

All endpoints require the normal Bearer JWT. The backend user ID always comes from the token.

| Method | Path | Caller | Purpose |
|---|---|---|---|
| POST | `/api/matches/authority` | Fusion Host | Create a 2–4 player binding and initial lease |
| POST | `/api/matches/{matchId}/join-proofs` | Each player | Issue a short-lived signed proof for its Fusion actor |
| POST | `/api/matches/{matchId}/players/bind` | Bound Host | Verify the proof and persist actor-to-user identity |
| POST | `/api/matches/{matchId}/players/{actor}/disconnect` | Bound Host | Mark a player disconnected |
| POST | `/api/matches/{matchId}/lease` | Bound Host | Renew the Host lease |
| POST | `/api/matches/{matchId}/start` | Bound Host | Start under the current M2 minimum-player rule (one bound player) |
| POST | `/api/matches/{matchId}/end` | Bound Host | End the authority binding idempotently |

Join proofs are HMAC-signed, expire after 120 seconds by default, and bind `matchId`, backend
`userId`, Fusion session name, and actor number. The proof secret is separate from the JWT secret.
After binding, the Host may submit telemetry whose `userId` belongs to that match; unrelated users
still receive `TELEMETRY_USER_MISMATCH`.

---

## Scenario resolution and decision logging (M4-051/M4-049)

These routes are implemented as a **proposed contract pending freeze with Networking/AI**.
Both require the current backend-bound Host JWT and a valid authority lease.

### `POST /api/matches/{matchId}/scenario/resolve`

Valid only during the PRE_MATCH Lobby window. The request contains `decisionId`,
`resolutionMode` (`FIXED` or `ADAPTIVE`), `unityCompatibilityVersion`, and optional
`experimentCondition`. HTTP 201 commits a new decision; an identical retry returns HTTP 200.

The response contains the immutable snapshot identity/fingerprint, roster identity,
resolution/fallback reason, full validated ScenarioConfig and its content fingerprint.
`unityApplyStatus` is `PENDING`; returning this response does not claim Unity applied it.

### `PUT /api/matches/{matchId}/scenario/decisions/{decisionId}/applied`

Unity Host confirms the exact `scenarioConfigId`, `scenarioConfigVersion`, and
`scenarioConfigFingerprint` actually applied. The receipt is idempotent and stored separately
from immutable decision evidence. A conflicting receipt returns `SCENARIO_APPLY_CONFLICT`.

Adaptive production policy/configuration is not approved. ADAPTIVE requests therefore resolve
to the approved deterministic fixed baseline with `FIXED_FALLBACK`, `NOT_EVALUATED`, and
`POLICY_CONFIG_INVALID`. If no valid production fallback exists, the API returns
`SCENARIO_FIXED_FALLBACK_NOT_CONFIGURED`.

---

## Admin

**Auth:** Bearer JWT, role `ADMIN`  
**Status:** Read-only dashboard APIs implemented; shop mutation APIs remain planned

| Method | Path | Description |
|---|---|---|
| GET | `/api/admin/users` | Paginated users; optional `search` |
| GET | `/api/admin/users/{userId}` | User detail |
| GET | `/api/admin/payments` | Paginated/filterable payment orders |
| GET | `/api/admin/payments/{paymentOrderId}` | Payment/checkout/event/fulfillment aggregate |
| GET | `/api/admin/wallet-transactions` | Paginated/filterable wallet ledger |
| GET | `/api/admin/purchases` | Paginated/filterable purchase history |
| GET | `/api/admin/shop/items` | List all shop items (including disabled/archived) |
| POST | `/api/admin/shop/items` | Create shop item |
| PUT | `/api/admin/shop/items/{id}` | Update shop item, including `isEnabled` and `isArchived` status |
| GET | `/api/admin/matches/logs` | Match logs |
| GET | `/api/admin/ai/logs` | AI behavior logs |

All list routes accept `page` (default `1`) and `pageSize` (default `20`, maximum `100`).
Payments additionally accept `status`, `provider`, `userId`, `productReference`, `fromUtc`, `toUtc`.
Wallet transactions accept `userId`, `type`, `reference`, `fromUtc`, `toUtc`.
Purchases accept `userId`, `shopItemId`, `fromUtc`, `toUtc`.

These endpoints never return password hashes, JWT/provider secrets, request fingerprints,
semantic fingerprints or idempotency keys. They do not mutate payments, wallets or purchases.

### `PUT /api/admin/shop/items/{id}`

**Status:** Planned / Not implemented

Update shop item, including enabled and archived status. Items are never hard-deleted when referenced by inventory or transactions.

**Body:**

```json
{
  "name": "Explorer Character",
  "description": "Approved Character unlock",
  "category": "CHARACTER",
  "price": 100,
  "imageUrl": "/images/shop/character_explorer.png",
  "isEnabled": false,
  "isArchived": true
}
```

**Rules:**

- Disabled items (`isEnabled: false`) cannot be purchased.
- Archived items (`isArchived: true`) are hidden from the public shop catalog.
- No hard delete for items that may be referenced by inventory or purchase history.

---

## Error codes

`VALIDATION_ERROR`, `UNAUTHORIZED`, `FORBIDDEN`, `NOT_FOUND`, `CONFLICT`, `INTERNAL_SERVER_ERROR`, `USERNAME_ALREADY_EXISTS`, `INVALID_CREDENTIALS`, `ACCOUNT_LOCKED`, `PASSWORD_CONFIRMATION_MISMATCH`, `PASSWORD_TOO_LONG`, `TOKEN_INVALID`, `MATCH_NOT_FOUND`, `MATCH_AUTHORITY_FORBIDDEN`, `MATCH_LEASE_EXPIRED`, `MATCH_ALREADY_ENDED`, `MATCH_SESSION_CONFLICT`, `MATCH_CAPACITY_REACHED`, `JOIN_PROOF_INVALID`, `MATCH_PLAYER_BINDING_CONFLICT`, `MATCH_RESULT_CONFLICT`, `MATCH_RESULT_INVALID_STATE`, `MATCH_RESULT_INVALID_ROSTER`, `MATCH_RESULT_DUPLICATE_PLAYER`, `MATCH_RESULT_INVALID_PAYLOAD`, `MATCH_RESULT_INVALID_DURATION`

See backend `ErrorCodes.cs` for the canonical list.
## Payment Orders — M4-054 (PROPOSED)

The route below is proposed until the Payment/Networking contract is frozen.
No provider checkout, callback, fulfillment, or refund endpoint is part of M4-054.

### `POST /api/payments/orders`

Requires JWT Bearer authentication. `userId`, amount, currency, purpose, status,
provider transaction identifiers, and fulfillment state are never accepted from
the client. Amount, currency, and purpose come from the authoritative
`PaymentCatalog` backend configuration.

```json
{
  "productReference": "approved-product-reference",
  "provider": "approved-provider-key",
  "idempotencyKey": "client-generated-stable-key"
}
```

The first successful request returns HTTP 201. An identical retry returns HTTP
200 with `isReplay: true`. Reusing the key with different semantic input returns
`PAYMENT_IDEMPOTENCY_CONFLICT`. If no production provider/product catalog is
configured, the API returns `PAYMENT_CONFIGURATION_MISSING` and creates no order.

### `GET /api/payments/catalog`

Requires JWT Bearer authentication. Returns active wallet-credit products from
the same authoritative `PaymentCatalog` configuration used for order creation
and fulfillment. The response exposes only safe display data:

```json
{
  "items": [
    {
      "productReference": "WALLET_COIN_500",
      "displayName": "Gói 500 Coins",
      "amount": 20000,
      "currency": "VND",
      "walletCredit": 500,
      "provider": "PAYOS"
    }
  ]
}
```

### `GET /api/payments/orders/{paymentOrderId}`

Requires JWT Bearer authentication and returns the stored order only when it
belongs to the authenticated user. It is the authoritative polling source for
the browser return page; payOS return query values never determine payment or
fulfillment state.

### `POST /api/payments/orders/{paymentOrderId}/checkout` (PROPOSED)

Requires the JWT owner of the order. The body is empty. Backend uses the stored
amount, currency, product and provider. For `PAYOS`, a database-generated bigint
is reserved as the stable `orderCode` before contacting the provider. A successful
checkout changes `CREATED` to `PENDING_PAYMENT` and returns only the order ID,
provider, provider order identity, HTTPS checkout URL, expiry and status.

### `POST /api/payments/webhooks/payos` (PROPOSED)

Anonymous by JWT design; authenticated cryptographically with the payOS webhook
signature. Invalid signatures are rejected before database mutation. Verified
payloads are normalized and deduplicated by `(provider, providerEventId)`.
Only matching amount/currency/order evidence can transition
`PENDING_PAYMENT -> PAID`. Browser return/cancel URLs never change payment state.

After a verified payment is committed as `PAID`, fulfillment is attempted using
the authoritative backend product catalog. Wallet ledger/inventory grant,
fulfillment receipt and `PAID -> FULFILLED` commit atomically. A failed grant
leaves the order `PAID` for webhook retry or recovery processing.
