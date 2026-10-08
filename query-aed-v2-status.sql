-- Read-only, local PostgreSQL. Run after each match.
-- Replace one or more conditions with a specific MatchId if desired.
-- Does not expose HostUserId or roster identity.
SELECT
    "MatchId",
    "DecisionId",
    "PhaseOrdinal",
    "DecisionPoint",
    "ChangedKey",
    "PreviousValue",
    "AppliedValue",
    "CommitStatus",
    "ApplyStatus",
    "ResultingPlanFingerprint",
    "CommittedAtUtc",
    "AppliedAtUtc"
FROM "ScenarioAdaptivePlansV2"
ORDER BY "CommittedAtUtc" DESC NULLS LAST
LIMIT 100;
