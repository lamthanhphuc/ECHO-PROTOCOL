# ECHO PROTOCOL — API Specification (Skeleton)

Base URL (local dev): `http://localhost:5000/api` (see `launchSettings.json`)

All responses use wrapper:

```json
{
  "success": true,
  "message": "string",
  "data": {},
  "errorCode": null
}
```

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

**Body:**

```json
{
  "username": "string",
  "email": "string",
  "password": "string"
}
```

**Response data:** `{ "userId": "guid", "username": "string" }`

### `POST /api/auth/login`

**Body:**

```json
{
  "usernameOrEmail": "string",
  "password": "string"
}
```

**Response data:**

```json
{
  "accessToken": "jwt",
  "expiresAt": "datetime",
  "userId": "guid",
  "username": "string",
  "role": "Player|Admin"
}
```

---

## Player

### `GET /api/player/me`

**Auth:** Bearer JWT

**Response data:** Player profile + wallet summary

---

## Shop

### `GET /api/shop/items`

**Auth:** Optional (public catalog)

**Query:** `category`, `page`, `pageSize`

### `POST /api/shop/purchase`

**Auth:** Bearer JWT

**Body:**

```json
{ "shopItemId": "guid" }
```

---

## Inventory

### `GET /api/inventory/me`

**Auth:** Bearer JWT

### `POST /api/inventory/equip`

**Auth:** Bearer JWT

**Body:**

```json
{ "inventoryItemId": "guid", "slot": "string" }
```

---

## Matches

### `POST /api/matches/logs`

**Auth:** Bearer JWT (host or server)

**Body:** Match result, objectives, players, duration, escaped flag

---

## Admin (placeholder)

| Method | Path | Description |
|---|---|---|
| GET | `/api/admin/shop/items` | List all shop items |
| POST | `/api/admin/shop/items` | Create shop item |
| PUT | `/api/admin/shop/items/{id}` | Update shop item |
| DELETE | `/api/admin/shop/items/{id}` | Delete shop item |
| GET | `/api/admin/matches/logs` | Match logs |
| GET | `/api/admin/ai/logs` | AI behavior logs |

**Auth:** Bearer JWT, role `Admin`

---

## Error codes

See backend `ErrorCodes.cs`: `VALIDATION_ERROR`, `UNAUTHORIZED`, `FORBIDDEN`, `NOT_FOUND`, `CONFLICT`, `INTERNAL_SERVER_ERROR`
