# AED v2 fixed fallback — LOCAL ONLY

Source used: `KLTN/Assets/Scripts/AI/Common/AED/FixedDirector.cs`, `ScenarioConfigValidator.cs`, `ScenarioConfigRegistry.cs` from the prior `KLTN_AED_LOCAL_REVIEW.zip`. These files predate the user's latest coordinator changes. Inspect actual Unity content and current source before local approval. No claim is made that scenes/assets were independently verified.

1. Ensure the DB is **local** and back it up; verify `SELECT current_database(), inet_server_addr(), inet_server_port();`. Inspect any existing fallback rows before inserting.
2. Stage via psql, passing `-v expected_db=<actual local DB name>` and `-f 01_stage_fixed_baseline.sql`. No entry becomes approved or active.
3. Copy `ScenarioLocalSeedValidationTests.cs` temporarily into `EchoProtocol.Backend/tests/EchoProtocol.Api.Tests/`, and set `AED_LOCAL_DB_CONNECTION` to the same local connection string. Run:
   `dotnet test EchoProtocol.Backend/tests/EchoProtocol.Api.Tests/EchoProtocol.Api.Tests.csproj --filter "FullyQualifiedName~StagedCandidatePassesScenarioConfigValidator"`
4. In Unity verify the map, Stalker, objectives and route referenced by the four IDs in the intended scene/build and that Unity compatibility `0.1.0` is accurate. If not, **do not approve**.
5. Only if both checks pass, use psql with `-v expected_db=<local DB name> -v validator_passed=yes -f 02_approve_fixed_baseline.sql`.
6. Run the second test filtered by `FullyQualifiedName~ApprovedFallbackResolvesThroughProductionRegistry`, then remove the temporary validation test file before the normal CI suite (or adapt into a properly isolated opt-in integration test).
7. Test a real lobby -> POST /api/matches/{matchId}/scenario/resolve as authenticated host; endpoint requires actual match and host identity.

The `validator_passed=yes` psql variable is an **operator attestation**, not independently cryptographic proof of validation. Use only on a development PostgreSQL database, never a shared or production DB. The SQL guards exact DB name and staged values, not database host; the operator must also verify the connection destination.

Because `psql`/`dotnet` are not available in the review runtime, scripts have not been executed against PostgreSQL or compiled. Validation still required on the user's machine.
