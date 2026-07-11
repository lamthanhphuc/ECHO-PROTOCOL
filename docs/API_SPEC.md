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

**Response data:**

```json
{ "service": "EchoProtocol.Api" }
```

---

## Auth

### `POST /api/auth/register`

**Auth:** `[AllowAnonymous]`

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
| 400 | `VALIDATION_ERROR` | Invalid input, whitespace-only fields |
| 400 | `PASSWORD_CONFIRMATION_MISMATCH` | Password ≠ confirmPassword |
| 409 | `USERNAME_ALREADY_EXISTS` | Duplicate username (case-insensitive) |

---

### `POST /api/auth/login`

**Auth:** `[AllowAnonymous]`

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
| 401 | `INVALID_CREDENTIALS` | Wrong username or password |
| 403 | `ACCOUNT_LOCKED` | Valid credentials but account locked |

---

### `GET /api/auth/me`

**Auth:** `[Authorize]` Bearer JWT

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
| 403 | `FORBIDDEN` | Authorization failure |
| 404 | `NOT_FOUND` | Token valid but user deleted |

**Role values:** `"PLAYER"`, `"ADMIN"` (strings)

---

## Shop (placeholder — not implemented)

### `GET /api/shop/items`

**Auth:** Optional (public catalog)

### `POST /api/shop/purchase`

**Auth:** Bearer JWT

---

## Inventory (placeholder)

### `GET /api/inventory/me`

**Auth:** Bearer JWT

---

## Admin (placeholder)

**Auth:** Bearer JWT, role `ADMIN`

---

## Error codes

`VALIDATION_ERROR`, `UNAUTHORIZED`, `FORBIDDEN`, `NOT_FOUND`, `CONFLICT`, `INTERNAL_SERVER_ERROR`, `USERNAME_ALREADY_EXISTS`, `INVALID_CREDENTIALS`, `ACCOUNT_LOCKED`, `PASSWORD_CONFIRMATION_MISMATCH`, `TOKEN_INVALID`
