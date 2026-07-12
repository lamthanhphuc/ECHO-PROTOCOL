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

### `GET /api/health`

**Auth:** None  
**Status:** Implemented

**Response data:**

```json
{ "service": "EchoProtocol.Api" }
```

---

## Auth

### `POST /api/auth/register`

**Auth:** `[AllowAnonymous]`  
**Status:** Implemented

**Body:**

```json
{
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

`VALIDATION_ERROR`, `UNAUTHORIZED`, `FORBIDDEN`, `NOT_FOUND`, `CONFLICT`, `INTERNAL_SERVER_ERROR`, `USERNAME_ALREADY_EXISTS`, `INVALID_CREDENTIALS`, `ACCOUNT_LOCKED`, `PASSWORD_CONFIRMATION_MISMATCH`, `PASSWORD_TOO_LONG`, `TOKEN_INVALID`

See backend `ErrorCodes.cs` for the canonical list.
