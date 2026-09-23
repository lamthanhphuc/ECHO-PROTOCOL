# M4-009 Match Result Test Report

> Final validation date: 2026-09-20
> Scope: Match Result persistence, authority, roster validation, idempotency, lifecycle transaction, and M2 regression.

## Result

Final automated run completed successfully through `scripts/test-m4-009.ps1`.

| Phase | Executed | Passed | Failed | Not Run |
|---|---:|---:|---:|---:|
| M4-009 unit tests (SQLite relational provider) | 16 | 16 | 0 | 0 |
| M4-009 PostgreSQL integration tests | 8 | 8 | 0 | 0 |
| Existing M2 regression tests | 70 | 70 | 0 | 0 |
| **Total test cases reported by the three runs** | **94** | **94** | **0** | **0** |

Build result: passed with 0 warnings and 0 errors.

The PostgreSQL tests were actually executed against an isolated `postgres:16-alpine` Docker
container. The script used a unique container name and random host port, then stopped the
`--rm` container in `finally`. It did not connect to or reset a development/production database.

## PostgreSQL evidence

The eight PostgreSQL tests verified:

1. all EF Core migrations, including `AddMatchResultsM4009`, apply to a clean PostgreSQL schema;
2. the `MatchResults` primary key rejects a second row for the same `MatchId`;
3. the composite roster FK rejects a `MatchResultPlayer` that was not bound to the match;
4. PostgreSQL check constraints reject an unsupported outcome and negative gameplay stats;
5. transaction rollback leaves both Result and match lifecycle unchanged;
6. two identical concurrent submissions store one Result and return one replay;
7. two conflicting concurrent submissions accept one payload and return
   `MATCH_RESULT_CONFLICT` for the other;
8. a controller submission returns HTTP 201 and persists Result, players, `Pending`, and
   `Ended` in PostgreSQL.

## Unit coverage

The 16 M4 unit tests cover:

- valid Host submission and first submission;
- non-Host rejection and match-not-found;
- invalid lifecycle and expired Host lease;
- exact roster validation, duplicate users, and a disconnected bound player;
- non-negative gameplay stats;
- persisted server timestamps, player rows, and `RewardStatus=Pending`;
- identical retry and conflicting retry;
- atomic transition to `Ended`;
- failure without partial match mutation;
- an already-ended match with no Result cannot receive a late Result.

## M2 regression caveat

The existing M2 test run reported 70/70 passed. Five pre-existing Mongo repository integration
tests are opt-in and return without Mongo operations when `ECHO_PHASE6R_MONGO_URI` is absent.
That behavior predates M4-009, so the 70 passing M2 tests do **not** claim live MongoDB integration
coverage. M4-009 does not alter Telemetry v1.1.

## Contract conflicts and resolved scope

- `NETWORKING_AUTHORITY_HANDOFF.md` says Unity creates the Match ID. The accepted M4-009 rule
  overrides that point: backend creation remains unchanged and Result uses
  `MatchAuthorityBinding.MatchId`.
- Older SRS/API material proposes `POST /api/matches/logs`; M4-009 implements only
  `PUT /api/matches/{matchId}/result`.
- Older docs mention a two-player start minimum. M4-009 keeps the current M2 one-bound-player
  start behavior unchanged.
- The SRS includes `HOST_DISCONNECTED`, but normal Host submission accepts only `WIN` and `LOSE`.
  No Host-disconnect outcome is synthesized without an approved policy.
- Older match-log drafts combine Result, AI logs, and reward calculation. M4-009 keeps telemetry
  in the existing Mongo pipeline and leaves all reward/profile work for M4-010/M4-011.

## Known lifecycle limitations

- Normal completion calls the Result endpoint, which writes Result and changes `InMatch` to
  `Ended` atomically.
- `/api/matches/{matchId}/end` remains available for abort/graceful-close compatibility. If it
  ends a match before a Result exists, M4-009 deliberately rejects any later Result.
- An expired lease/abrupt Host loss cannot submit Result. M4-009 does not create an aborted or
  `HOST_DISCONNECTED` Result and never grants a reward.
- Existing `InMatch` rows created before the migration have no `StartedAtUtc`; they cannot submit
  a Result until a separately approved data policy exists. The migration does not fabricate a
  start timestamp.
- Host authority proves who may submit, not that every gameplay fact is honest. M4-009 validates
  the stored roster/disconnect state and bounded fields; deeper telemetry reconciliation remains
  a separate policy decision.

## Re-run command

From the repository root on Windows:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\test-m4-009.ps1
```

Requirements: .NET SDK capable of targeting .NET 8 and a running Docker daemon. If Docker or the
PostgreSQL container cannot run, the script reports PostgreSQL integration as `NOT RUN` and exits
with a non-zero status. TRX files are written under `TestResults/M4-009/<run-id>/`.

Running the PostgreSQL category directly without `ECHO_M4_POSTGRES_CONNECTION` fails explicitly
with `PostgreSQL integration tests are NOT RUN`; it is never converted into a passing test.
