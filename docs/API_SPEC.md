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

> **Note:** SRS may reference `GET /api/player/me`. Auth Foundation phase uses `GET /api/auth/me`. Player profile API expansion is deferred.

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
**Status:** Planned / Not implemented

**Response data:** Player profile + wallet summary (SRS target; use `GET /api/auth/me` until this endpoint ships)

---

## Shop

### `GET /api/shop/items`

**Auth:** Optional (public catalog)  
**Status:** Planned / Not implemented

**Query:** `category`, `page`, `pageSize`

### `POST /api/shop/purchase`

**Auth:** Bearer JWT  
**Status:** Planned / Not implemented

**Body:**

```json
{ "shopItemId": "guid" }
```

---

## Inventory

### `GET /api/inventory/me`

**Auth:** Bearer JWT  
**Status:** Planned / Not implemented

### `POST /api/inventory/equip`

**Auth:** Bearer JWT  
**Status:** Planned / Not implemented

**Body:**

```json
{
  "shopItemId": "uuid"
}
```

**Rules:**

- Player may only equip items they already own.
- Backend resolves category from the shop item definition.
- Only one equipped item per category at a time.

---

## Matches

### `POST /api/matches/logs`

**Auth:** Bearer JWT (host or server)  
**Status:** Planned / Not implemented

**Planned request body:**

```json
{
  "matchCode": "ROOM123",
  "startedAt": "2026-07-10T10:00:00Z",
  "endedAt": "2026-07-10T10:15:00Z",
  "durationSeconds": 900,
  "result": "WIN",
  "playerCount": 2,
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
  ],
  "aiLogs": [
    {
      "targetUserId": "uuid",
      "aiState": "CHASE",
      "triggerReason": "HIGH_NOISE_PRIORITY",
      "targetScore": 0.82,
      "noiseScore": 0.9,
      "isolationScore": 0.4,
      "objectiveItemScore": 0.3,
      "recentDetectionScore": 0.7,
      "hidingPatternScore": 0.1
    }
  ]
}
```

**Rules:**

- Unity/host sends raw match stats only.
- Backend validates payload and calculates rewards.
- Backend rejects duplicate submissions for the same match.
- Backend is the sole authority for wallet updates.

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
| POST | `/api/matches/{matchId}/start` | Bound Host | Start after at least two verified players |
| POST | `/api/matches/{matchId}/end` | Bound Host | End the authority binding idempotently |

Join proofs are HMAC-signed, expire after 120 seconds by default, and bind `matchId`, backend
`userId`, Fusion session name, and actor number. The proof secret is separate from the JWT secret.
After binding, the Host may submit telemetry whose `userId` belongs to that match; unrelated users
still receive `TELEMETRY_USER_MISMATCH`.

---

## Admin

**Auth:** Bearer JWT, role `ADMIN`  
**Status:** Planned / Not implemented

| Method | Path | Description |
|---|---|---|
| GET | `/api/admin/shop/items` | List all shop items (including disabled/archived) |
| POST | `/api/admin/shop/items` | Create shop item |
| PUT | `/api/admin/shop/items/{id}` | Update shop item, including `isEnabled` and `isArchived` status |
| GET | `/api/admin/matches/logs` | Match logs |
| GET | `/api/admin/ai/logs` | AI behavior logs |

### `PUT /api/admin/shop/items/{id}`

**Status:** Planned / Not implemented

Update shop item, including enabled and archived status. Items are never hard-deleted when referenced by inventory or transactions.

**Body:**

```json
{
  "name": "Red Flashlight",
  "description": "Red flashlight skin",
  "category": "FLASHLIGHT_SKIN",
  "price": 100,
  "imageUrl": "/images/shop/red_flashlight.png",
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

`VALIDATION_ERROR`, `UNAUTHORIZED`, `FORBIDDEN`, `NOT_FOUND`, `CONFLICT`, `INTERNAL_SERVER_ERROR`, `USERNAME_ALREADY_EXISTS`, `INVALID_CREDENTIALS`, `ACCOUNT_LOCKED`, `PASSWORD_CONFIRMATION_MISMATCH`, `PASSWORD_TOO_LONG`, `TOKEN_INVALID`, `MATCH_NOT_FOUND`, `MATCH_AUTHORITY_FORBIDDEN`, `MATCH_LEASE_EXPIRED`, `MATCH_ALREADY_ENDED`, `MATCH_SESSION_CONFLICT`, `MATCH_CAPACITY_REACHED`, `JOIN_PROOF_INVALID`, `MATCH_PLAYER_BINDING_CONFLICT`

See backend `ErrorCodes.cs` for the canonical list.
