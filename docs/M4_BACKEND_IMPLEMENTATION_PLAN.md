# M4 Backend Implementation Plan

> Audit date: 2026-09-20
> Scope: audit and implementation planning only. No M4 runtime code, M2 API, migration, data, or Unity scene was changed.

## 1. Executive summary

The backend has a usable M2 foundation for M4: JWT authentication, one-to-one `User` / `PlayerProfile` / `Wallet` records, a PostgreSQL-backed match-authority binding, verified player bindings, a three-state match lifecycle, and an idempotent MongoDB telemetry pipeline.

M4 Match Result, Reward, and Profile are not implemented. There is no relational match result, player result, reward grant, wallet ledger, XP/level state, profile API, or PostgreSQL transaction/concurrency test.

M4-009 should be implemented first as a match-scoped, Host-only, idempotent result write tied directly to `MatchAuthorityBinding.MatchId`. Reward and progression must be added afterward in the same PostgreSQL transaction boundary. No reward or XP formula should be coded until the team approves an authoritative, versioned policy.

Two existing contract conflicts must be resolved before coding M4-009:

1. `docs/NETWORKING_AUTHORITY_HANDOFF.md` says Unity creates the authoritative `matchId` and the backend registers that ID. Current `MatchAuthorityService.CreateAsync` creates a new backend GUID.
2. `docs/API_SPEC.md` describes a minimum of two bound players to start, while current code and `SingleBoundHost_CanStartMatch` explicitly allow one.

These are audit findings, not changes requested for M2 in this phase.

## 2. Audit scope and evidence

Reviewed:

- all backend controllers, services, entities, DTOs, configuration, health checks, `Data/AppDbContext.cs`, database initializer, migrations, `Program.cs`, project configuration, and tests under `EchoProtocol.Backend`;
- `docs/SRS.md`, `docs/API_SPEC.md`, `docs/DB_SCHEMA.md`, `docs/NETWORKING_AUTHORITY_HANDOFF.md`;
- `docs/03_ECHO_PROTOCOL_Implementation_Spec_REVISED.md` and `docs/05_ECHO_PROTOCOL_Project_Plan_4P_2026_REVISED.md`;
- M2 match-state and telemetry handoff material relevant to backend boundaries.

The requested CodeGraph and other project MCP servers were not available in this session, so source exploration used direct file reads and `rg`. The Unity project skill was inspected but does not apply because no Unity file or Editor operation is in scope.

### Build and test result

| Command | Actual result |
|---|---|
| `rtk dotnet restore EchoProtocol.sln` | Could not run: `rtk` is not installed or not in `PATH`. |
| `dotnet restore EchoProtocol.sln` | Passed; all projects up to date. |
| `dotnet build EchoProtocol.sln --no-restore` | Passed; 0 warnings, 0 errors. |
| `dotnet test EchoProtocol.sln --no-build --no-restore` | Passed; 70 passed, 0 failed, 0 skipped. |

Test caveat: Mongo repository integration tests exit early and are counted as passed when `ECHO_PHASE6R_MONGO_URI` is absent. This run therefore does not prove live MongoDB transactions/indexes. There are no PostgreSQL integration tests for migrations, unique constraints, row locking, rollback, or concurrent reward processing.

## 3. Backend foundation already completed

### 3.1 Authentication and JWT

- `AuthController` implements register, login, and `GET /api/auth/me` using the standard `ApiResponse<T>` wrapper.
- `AuthService` normalizes username/email, hashes passwords with BCrypt, creates `User`, `PlayerProfile`, and `Wallet` in one EF Core `SaveChangesAsync`, and maps unique PostgreSQL violations.
- `JwtTokenService` issues HMAC-SHA256 tokens with user ID, username, role, and JTI claims.
- JWT validation checks issuer, audience, lifetime, signing key, and uses a 30-second clock skew.
- JWT and match-proof secrets are configuration/environment inputs; they are not committed in appsettings.
- `Program.cs` produces consistent JSON responses for challenge and forbidden outcomes.

Relevant reuse for M4: authenticated `ClaimTypes.NameIdentifier`, `ApiResponse<T>`, `ServiceResult<T>`, error mapping style, and existing authorization middleware.

Known gaps relevant to quality: there are no controller/auth integration tests, refresh/revocation is absent, and controllers repeat claim parsing. These do not require changing M2 for M4-009.

### 3.2 User, PlayerProfile, and Wallet

- `User` has one required-by-application `PlayerProfile` and one `Wallet`, each protected by a unique `UserId` index and `ON DELETE RESTRICT` FK.
- `PlayerProfile` already stores `DisplayName`, `TotalMatches`, and `TotalWins`.
- Database checks enforce non-negative match/win totals and `TotalWins <= TotalMatches`.
- `Wallet` stores a non-negative integer `Balance`; new players receive the configured code default of 500.
- Registration creates all three records together. Login and `/api/auth/me` fail loudly if profile/wallet integrity is broken.

Reusable for M4: existing profile counters, wallet ownership relation, wallet non-negative check, and registration/seed creation path.

Missing for M4: XP, level, progression audit/idempotency, wallet transaction ledger, concurrency control, public profile endpoint, and reward history.

### 3.3 MatchAuthorityBinding and MatchPlayerBinding

- `MatchAuthorityBinding.MatchId` is the PostgreSQL primary key.
- Current backend creation uses `Guid.NewGuid()` and returns it to the caller.
- `HostUserId` is taken from the authenticated JWT subject and stored as an FK to `Users`.
- `FusionSessionName`, capacity, status, lease, created/updated timestamps, and optional end timestamp are persisted.
- `MatchPlayerBinding` persists `(MatchId, UserId, FusionActorNumber, JoinProofId)` and disconnect timestamps.
- Unique indexes prevent duplicate user or actor within one match and prevent reuse of a persisted join proof ID.
- Player identity is proven with a short-lived HMAC proof binding match, user, session, and actor; the Host performs the final bind.

Reusable for M4: `MatchId`, trusted Host identity, persisted roster, actor/user mapping, disconnect state, lease timestamps, and the relational FKs to `Users`.

### 3.4 Match lifecycle and Host authority

- States are `Lobby`, `InMatch`, and `Ended`.
- Create enters `Lobby` and issues a lease.
- Start changes to `InMatch` after checking the caller is the bound Host, the lease is valid, and at least one connected binding exists.
- End checks Host ownership and idempotently changes to `Ended`, setting `EndedAtUtc`; it currently accepts both Lobby and InMatch and ignores `EndMatchAuthorityRequest.Reason`.
- Lease renewal and player disconnect are Host-only while the match is not ended and the lease remains valid.
- No worker currently converts expired bindings to `Ended`/`HostDisconnected`.

Reusable for M4: Host validation and lifecycle lookup. M4 should centralize/reuse this validation instead of copying it into a new controller.

### 3.5 Telemetry and MongoDB

- `POST /api/telemetry/batch` is authenticated and validates the canonical `1.1` envelope and event semantics.
- A player may submit its own events; a bound Host may delegate events for users in its stored match roster and submit system events.
- MongoDB enforces event ID and `(matchId, eventSequence)` uniqueness.
- The repository uses Mongo transactions, semantic fingerprints, match boundary state, and retry handling for telemetry idempotency.
- Telemetry authorization remains available for ended matches for a configured retention window.
- Mongo startup failure is degraded: PostgreSQL/Auth can remain available while telemetry reports unavailable.

Reusable for M4: `matchId` correlation and telemetry as audit/corroboration evidence. Mongo telemetry must not be inside the PostgreSQL reward transaction, because the documented architecture explicitly has no cross-database transaction.

## 4. Match Result analysis

### 4.1 Answers from current implementation

| Question | Current answer |
|---|---|
| Where is Match ID created? | In `MatchAuthorityService.CreateAsync` with `Guid.NewGuid()`, then stored as `MatchAuthorityBinding.MatchId`. This conflicts with the networking handoff, which says Unity creates it. |
| How is Host identified? | JWT subject becomes `HostUserId`; every Host mutation compares the authenticated user ID with that value. No client `isHost` flag or shared Host header is trusted. |
| How is the player list managed? | `MatchPlayerBindings`, one unique user and actor per match. The Host binds a signed join proof and can mark a binding disconnected; disconnected rows are retained. |
| How does InMatch become Ended? | Host calls `POST /api/matches/{matchId}/end`; `EndAsync` writes `Ended` and `EndedAtUtc`. A graceful telemetry `MATCH_ENDED` does not update PostgreSQL lifecycle. Expired leases are not automatically closed. |
| Is MatchResult implemented? | No entity, service, controller, DTO, DbSet, migration, or test exists. `/api/matches/logs` remains planned documentation only. |
| How should it integrate? | One `MatchResult` per `MatchAuthorityBinding.MatchId`, plus player rows constrained to the persisted roster. Submission must use the binding's Host and lifecycle, not create a separate match identity. |
| How to reject duplicate result? | Primary/unique DB key on `MatchResults.MatchId`, normalized payload hash, transactional insert, and deterministic replay/conflict behavior. An application-only `AnyAsync` check is insufficient. |
| How to pay once? | Unique reward grant per `(MatchId, UserId)` and unique wallet ledger reference per wallet/match, all written with wallet/profile updates in one PostgreSQL transaction. |

### 4.2 Recommended relational schema

Names below follow current PascalCase PostgreSQL tables. Exact migration names should be generated when implementation starts.

#### `MatchResults`

| Column | Type / constraint | Purpose |
|---|---|---|
| `MatchId` | UUID PK, FK to `MatchAuthorityBindings.MatchId`, `ON DELETE RESTRICT` | One result per authoritative match; main idempotency key. |
| `SubmittedByUserId` | UUID FK to `Users`, required | Audit identity; must equal bound Host at submission. |
| `Outcome` | VARCHAR(30), required | Approved enum only, initially expected `WIN`, `LOSE`, `HOST_DISCONNECTED`. |
| `StartedAtUtc` | TIMESTAMPTZ, required | Prefer server lifecycle timestamp. Current binding needs `StartedAtUtc`. |
| `EndedAtUtc` | TIMESTAMPTZ, required | Server terminal timestamp. |
| `DurationSeconds` | INT, check non-negative | Compute from server timestamps; client value is evidence only. |
| `ObjectiveCompletion` | NUMERIC with check 0..1 | Raw authoritative summary input. |
| `PlayerCount` | INT, check 1..4 pending minimum-player decision | Must equal accepted result roster count. |
| `PayloadHash` | CHAR(64), required | SHA-256 of canonical result content for safe replay/conflict detection. |
| `RewardStatus` | VARCHAR(20), required | `Pending`, `Processing`, `Completed`, `Failed`; M4-009 starts as `Pending`. |
| `SubmittedAtUtc`, `RewardProcessedAtUtc` | TIMESTAMPTZ | Audit/recovery timestamps. |

Do not duplicate a separate generated `MatchLogId` unless an external requirement needs it; `MatchId` is already the authoritative identity and documented idempotency key.

#### `MatchResultPlayers`

| Column | Type / constraint | Purpose |
|---|---|---|
| `Id` | UUID PK | Row identity. |
| `MatchId`, `UserId` | UUID, unique pair | One result row per player per match. |
| roster FK | composite FK `(MatchId, UserId)` to the authoritative binding | DB-level rejection of an unbound player. |
| `Survived`, `Disconnected` | BOOLEAN | Raw result facts. |
| `DetectionCount`, `DownedCount`, `ReviveCount`, `ObjectiveContribution` | INT, checks >= 0 | Bounded raw stats; bounds require gameplay sign-off. |
| `RewardEarned`, `XpEarned` | INT/BIGINT nullable initially | Backend output only; never accepted from request. |

To support the composite FK cleanly, configure `(MatchId, UserId)` as an alternate key on `MatchPlayerBinding`, not only an application-visible unique index.

#### `MatchRewardGrants`

| Column | Type / constraint | Purpose |
|---|---|---|
| `Id` | UUID PK | Reward processing identity. |
| `MatchId`, `UserId` | UUID, unique pair | Hard guard against duplicate reward/progression. |
| `CurrencyAmount`, `XpAmount` | INT/BIGINT, checks >= 0 | Server-calculated outputs. |
| `PolicyVersion` | VARCHAR(50), required | Makes balance changes reproducible after tuning. |
| `BreakdownJson` | JSONB, required | Result-screen/audit breakdown without trusting the client. |
| `ProcessedAtUtc` | TIMESTAMPTZ | Audit timestamp. |

#### `WalletTransactions`

Use the current `docs/DB_SCHEMA.md` wallet relationship and the richer SRS audit fields:

| Column | Type / constraint |
|---|---|
| `Id` | UUID PK |
| `WalletId` | UUID FK to `Wallets`, `ON DELETE RESTRICT` |
| `Type` | VARCHAR(30), e.g. `MATCH_REWARD` |
| `Amount` | INT, non-zero for a posted ledger entry |
| `BalanceBefore`, `BalanceAfter` | INT, checks >= 0 |
| `ReferenceId` | UUID required for match rewards; use `MatchId` |
| `Description`, `CreatedAtUtc` | bounded text, TIMESTAMPTZ |

Add unique `(WalletId, Type, ReferenceId)`. One aggregate ledger row per player/match is preferable to multiple component rows; component detail belongs in `MatchRewardGrants.BreakdownJson`.

#### `PlayerProfiles` additions

- `ExperiencePoints BIGINT NOT NULL DEFAULT 0`, check >= 0.
- `Level INT NOT NULL DEFAULT 1`, check >= 1.
- Add a concurrency strategy (`Version` token or PostgreSQL row locking) before parallel reward processing.

`TotalMatches` and `TotalWins` already exist and should only be incremented as part of a newly inserted `MatchRewardGrant`. The account/economy `PlayerProfile` must remain distinct from the later AED `PlayerAIProfile` planned in M4-027/M4-039.

### 4.3 Recommended API contract

Preferred canonical endpoint:

```http
PUT /api/matches/{matchId}/result
Authorization: Bearer <Host JWT>
Content-Type: application/json
```

`PUT` makes the one-result-per-match resource and retry behavior explicit. If Unity has already committed to the planned `POST /api/matches/logs`, expose that only as a compatibility adapter to the same service; do not create two processing paths.

Request principles:

- path `matchId` is the idempotency/authority key;
- do not accept `rewardAmount`, `xp`, `walletBalance`, `submittedByUserId`, or a client-selected Host identity;
- include only raw Host-authoritative outcome and per-player facts;
- do not resend AI logs here because canonical telemetry already has its own Mongo pipeline;
- timestamps/duration in the request should be optional evidence where the server lifecycle can supply them.

Example shape:

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

Response should retain the current `ApiResponse<T>` envelope and return result identity/status plus reward/profile data when M4-010/M4-011 are available.

Recommended semantics:

| Case | HTTP / code | Side effect |
|---|---|---|
| First valid submission | 201 | Insert result once; M4-009 leaves reward `Pending`. |
| Exact retry, same canonical hash | 200 | Return stored result; no new rows or counters. |
| Same `matchId`, different content | 409 `MATCH_RESULT_CONFLICT` | No mutation. |
| Caller is not bound Host | 403 `MATCH_AUTHORITY_FORBIDDEN` | No mutation. |
| Player not in persisted roster | 400/409 `PLAYER_NOT_IN_MATCH` | No mutation. |
| Invalid lifecycle/outcome/stats | 409/400 with a specific code | No mutation. |

The SRS currently proposes returning `DUPLICATE_MATCH_SUBMISSION` for every duplicate. The team must choose whether an identical network retry returns that 409 or a replay-safe 200. A replay-safe 200 is recommended for Unity reliability; a changed duplicate must always be 409.

### 4.4 M4-009 service boundary

Create `IMatchResultService.SubmitAsync(hostUserId, matchId, request, ct)`. It should:

1. begin a PostgreSQL transaction;
2. lock/load `MatchAuthorityBinding` and roster;
3. verify caller, accepted lifecycle, result submission window, and roster equality/membership;
4. canonicalize and hash the payload;
5. return the existing result for an exact retry or conflict for changed content;
6. insert `MatchResult` and `MatchResultPlayers` using DB uniqueness as the final race guard;
7. transition the match terminal state in the same transaction, or reconcile an already-ended match under an explicitly approved rule;
8. commit and return the persisted server representation.

Do not use a pre-insert `AnyAsync` check as the sole duplicate defense. Catch the named PostgreSQL unique violation and reload/compare the stored hash so concurrent identical calls are deterministic.

## 5. Reward and profile plan

### 5.1 Reward calculation

Create a pure, versioned `IRewardPolicy` and an orchestration `IRewardService`:

- `IRewardPolicy.Calculate(MatchResultSnapshot)` returns currency, XP, and a component breakdown without database writes.
- `IRewardService.ProcessAsync(matchId, ct)` performs idempotency, locking, wallet/profile updates, ledgers, and status updates.
- Policy inputs must only come from the stored result/roster and approved server/Host-authoritative fields.
- The request must never contain final currency/XP amounts.

Do not implement numbers yet. The SRS contains an older proposed currency table, while the implementation spec lists completion, survivors, difficulty modifier, rescue, and rating without exact weights. There is no approved XP curve or level threshold table. These are design inputs, not safe assumptions.

### 5.2 Atomic processing algorithm

Use one PostgreSQL transaction for the whole match reward operation:

1. lock result and all affected wallet/profile rows in stable `UserId` order;
2. if every unique `MatchRewardGrant` already exists and status is completed, return stored output;
3. calculate with the approved, versioned policy;
4. insert one grant per `(MatchId, UserId)`;
5. update wallet balances and write matching `WalletTransactions`;
6. increment `TotalMatches`; increment `TotalWins` only under the approved team/player win rule;
7. add XP and derive level from the approved threshold policy;
8. mark result reward processing completed and commit.

For concurrent matches affecting the same wallet, use explicit PostgreSQL row locks or a serializable transaction with bounded retries. Reading a balance and later assigning `Balance + reward` under default read committed isolation can lose updates. Stable lock ordering avoids multi-player deadlocks. Keep arithmetic checked to prevent integer overflow.

If any player update, grant, ledger, or profile update fails, roll back the entire match operation. Do not mark `RewardStatus=Completed` outside that transaction.

### 5.3 Profile API

Add `GET /api/player/me` after result/reward persistence is stable. It should return:

- user/display identity;
- wallet balance;
- `totalMatches`, `totalWins`;
- approved `experiencePoints`, `level`, and optionally next-level progress;
- no AED behavioral profile unless a separate authorized contract explicitly requests it.

`GET /api/auth/me` should remain compatible for M2 clients. Do not silently change its existing response during M4-009.

## 6. Files to create

Proposed paths; split DTO files if that matches implementation size.

### M4-009 Match Result

- `src/EchoProtocol.Api/Entities/MatchResult.cs`
- `src/EchoProtocol.Api/Entities/MatchResultPlayer.cs`
- `src/EchoProtocol.Api/Enums/MatchOutcome.cs`
- `src/EchoProtocol.Api/Enums/RewardProcessingStatus.cs`
- `src/EchoProtocol.Api/DTOs/Matches/SubmitMatchResultRequest.cs`
- `src/EchoProtocol.Api/DTOs/Matches/MatchResultResponse.cs`
- `src/EchoProtocol.Api/Services/Interfaces/IMatchResultService.cs`
- `src/EchoProtocol.Api/Services/MatchResultService.cs`
- `src/EchoProtocol.Api/Controllers/MatchResultsController.cs`
- `tests/EchoProtocol.Api.Tests/MatchResultServiceTests.cs`
- PostgreSQL-backed match result integration/concurrency tests (new file or a dedicated integration-test project)
- one generated EF migration after the model is approved

### M4-010 Reward and wallet ledger

- `src/EchoProtocol.Api/Entities/MatchRewardGrant.cs`
- `src/EchoProtocol.Api/Entities/WalletTransaction.cs`
- `src/EchoProtocol.Api/Enums/WalletTransactionType.cs`
- `src/EchoProtocol.Api/Services/Interfaces/IRewardPolicy.cs`
- `src/EchoProtocol.Api/Services/Interfaces/IRewardService.cs`
- `src/EchoProtocol.Api/Services/RewardService.cs`
- a concrete versioned reward policy only after gameplay approval
- `tests/EchoProtocol.Api.Tests/RewardServiceTests.cs`
- PostgreSQL rollback/concurrency/idempotency integration tests
- one generated EF migration, or combine with M4-009 only if both models ship atomically

### M4-011 Profile/progression

- `src/EchoProtocol.Api/DTOs/Player/PlayerProfileResponse.cs`
- `src/EchoProtocol.Api/Services/Interfaces/IPlayerProfileService.cs`
- `src/EchoProtocol.Api/Services/PlayerProfileService.cs`
- `src/EchoProtocol.Api/Controllers/PlayerController.cs`
- progression policy/threshold type after design approval
- `tests/EchoProtocol.Api.Tests/PlayerProfileServiceTests.cs`
- API integration tests for JWT ownership and response compatibility

## 7. Files to modify

- `Data/AppDbContext.cs`: new DbSets, relationships, alternate/unique keys, checks, indexes, delete behavior, and concurrency configuration.
- `Entities/MatchAuthorityBinding.cs`: result navigation and recommended `StartedAtUtc`; consider a concurrency token.
- `Entities/MatchPlayerBinding.cs`: alternate-key/result-player navigation if using a composite roster FK.
- `Entities/Wallet.cs`: ledger navigation and concurrency strategy.
- `Entities/PlayerProfile.cs`: XP/level and concurrency strategy.
- `Entities/User.cs`: only required navigations; do not change auth behavior.
- `Services/MatchAuthorityService.cs`: record start time and coordinate terminal transition with result submission; preserve existing M2 routes/contracts.
- `Program.cs`: register M4 services/policies and validate any approved policy configuration.
- `Common/ErrorCodes.cs`: add result/reward-specific stable codes.
- `docs/API_SPEC.md` and `docs/DB_SCHEMA.md`: update after the contract is approved and implemented.
- `EchoProtocol.Api.Tests.csproj`: add only the packages required for real PostgreSQL/API integration tests.

Do not modify `AuthController`, `AuthService`, telemetry service/repository/schema, existing M2 migrations, or Unity scenes as part of M4-009 unless a separately approved compatibility defect requires it.

## 8. Implementation order

1. **Contract decisions:** settle Match ID ownership, minimum player rule, exact result route/retry semantics, result roster definition, terminal ordering, outcomes, and accepted stat bounds.
2. **M4-009 Match Result:** add server lifecycle start timestamp, relational result/player tables, Host/lifecycle/roster validation, payload fingerprint, DB uniqueness, and exact-retry tests. Do not calculate rewards yet.
3. **M4-010 Reward ledger:** approve/version formula, add reward grants and wallet transactions, then perform all rewards and wallet writes in one locked PostgreSQL transaction.
4. **M4-011 Profile:** update totals/XP/level only through the idempotent grant transaction and expose `GET /api/player/me`.
5. **Unity integration:** after backend contract tests pass, integrate one result submit/retry path and M4-008 result UI. This audit does not change Unity.
6. **Hardening:** real PostgreSQL parallel-submit tests, rollback/failure injection, API auth tests, migration test on a disposable database, and 2/3/4-player E2E evidence.

## 9. Quality and risk findings

### Database and migrations

- Current migrations match the model for auth and match authority; no M4 tables exist.
- Production does not auto-run migrations; only Development calls `MigrateAsync`. Deployment must have an explicit migration step.
- `docs/DB_SCHEMA.md` is stale about `Users.Email` and does not fully reflect current code.
- EF InMemory tests do not enforce PostgreSQL FKs, unique constraints, checks, isolation, or locking.
- Existing match/session conflict detection is check-then-insert and has no database constraint representing “only one unexpired active session name”; concurrent creates can both pass.
- `MatchAuthorityBinding` and wallet/profile rows have no optimistic concurrency token.

### Match authority security

- Host identity is strong relative to the current architecture because it is tied to a signed JWT and persisted binding.
- Any authenticated player can request a join proof for an open match/actor; the Host must still bind it. This proves backend identity but not independent Photon presence.
- A proof issued in Lobby can still be bound shortly after the match starts because bind validation does not require `Lobby` status.
- Start currently permits one player and conflicts with API documentation/SRS 2–4-player expectations.
- End can close a Lobby, ignores the supplied reason, and does not require a valid lease.
- Expired leases are not automatically persisted as `Ended`/`HostDisconnected`.
- Backend currently creates Match ID despite the networking handoff stating Unity owns it.

M4 result validation cannot be stronger than these trust boundaries. At minimum it must require the persisted Host, exact roster, correct lifecycle, bounded inputs, and immutable database evidence. Telemetry can detect inconsistencies but should not be a transactional dependency.

### Race conditions and idempotency

- `MatchAuthorityService` has no row-version checks; simultaneous start/end/renew requests are last-writer-wins.
- Application checks alone cannot protect Match Result or Reward. Use primary/unique constraints and handle the constraint race.
- Wallet balance and profile counters need row locks/serializable retry or atomic updates to prevent lost updates from different matches.
- Result insertion, player rows, reward grants, wallet updates, ledgers, and profile counters must have one clear transaction boundary.
- PostgreSQL and MongoDB must not be treated as one atomic unit. Persist business truth in PostgreSQL, then correlate telemetry by UUID.

### Unity/API compatibility

- Existing APIs use camelCase JSON, string enums, Bearer JWT, and an `ApiResponse<T>` wrapper; M4 must preserve these conventions.
- Current planned docs use `POST /api/matches/logs`; the recommended resource route is `PUT /api/matches/{matchId}/result`. Freeze one canonical route with Member B before Unity integration.
- Keep `/api/matches/{matchId}/end` compatibility. Define whether result submission ends the binding atomically or whether `/end` must occur first; leaving both unordered creates a race.
- Do not require Unity to calculate reward, XP, balance-after, or profile counters.

## 10. Gameplay/product decisions requiring team confirmation

No implementation should invent these values or rules:

1. Is authoritative `matchId` created by Unity or backend?
2. Is the supported result roster minimum 1 or 2 players, and must it exactly equal all bound players or the roster frozen at start?
3. Canonical route: planned `POST /api/matches/logs`, match-scoped POST, or recommended PUT?
4. Should an identical duplicate return replay-safe 200 or SRS `DUPLICATE_MATCH_SUBMISSION` 409?
5. Exact terminal order between result submission, `/end`, and telemetry `MATCH_ENDED`.
6. Approved outcome mapping among gameplay end reasons, telemetry reasons, and `WIN` / `LOSE` / `HOST_DISCONNECTED`.
7. Reward currency formula, per-component bounds, maximum, rounding, difficulty/rating/rescue rules, and version identifier.
8. Whether disconnected, eliminated, late-bound, or Host-disconnected players receive participation reward/XP and profile match credit.
9. Whether `TotalWins` is team-win based or only counts individual survival/escape.
10. XP formula, level thresholds, maximum level, overflow behavior, and whether level can ever decrease.
11. Valid bounds and provenance for duration, objective completion, detections, downs, revives, and contribution.
12. Host-loss timeout and whether backend may synthesize a `HOST_DISCONNECTED` result without a Host payload.
13. Retention and privacy policy for detailed per-player match statistics.

## 11. Recommended first implementation: M4-009

The first pull request should be intentionally narrow:

1. freeze the Match ID and endpoint/retry contracts above;
2. add `StartedAtUtc` to the authority lifecycle;
3. add `MatchResult` and `MatchResultPlayer` with PK/FK/check/unique constraints;
4. implement a Host-only match-scoped submit endpoint with no reward/XP input;
5. use canonical payload hashing plus `MatchId` uniqueness for replay-safe idempotency;
6. validate result users against `MatchPlayerBindings` and lifecycle against `MatchAuthorityBinding`;
7. persist the result and terminal lifecycle atomically, with `RewardStatus=Pending`;
8. add unit tests and real PostgreSQL tests for exact retry, changed retry, simultaneous duplicate calls, unauthorized Host, invalid roster, invalid lifecycle, and full rollback;
9. document the final wire contract for Member B/A before touching Unity;
10. leave Auth, Telemetry, Reward, Wallet balance, Profile counters, XP, and Level unchanged until their following work items.

This gives M4-010 one immutable, authoritative result record to consume and prevents reward logic from being coupled to controller retries or raw Unity payloads.
