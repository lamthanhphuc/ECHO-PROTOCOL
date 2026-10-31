-- ECHO PROTOCOL / Report 4 / Figure 05 - Match, Rewards, Adaptive AI ERD
-- PostgreSQL DDL for ERD visualization (SELECTED TABLES / COLUMNS).
-- Selected schema: main base + LTP adaptive pre-match candidate / V2 plan and profile jobs.
-- LTP differs from main: candidate selection needs Enabled=true and approved configurations.
-- Import this file ALONE into dbdiagram.io (Import SQL / PostgreSQL).
-- Do not run it as an EF migration. Standalone Users/Wallets stubs are deliberate.
-- The stub tables are NOT an accurate full replacement for Figure 04!
-- Corrects LTP PlayerAIProfiles: ObjectiveScore is NOT forced NULL.
-- Adds active sample statuses needed for pre-match selection.
-- Omitted profile/telemetry columns are intentional to keep the ERD readable on A4.

CREATE TABLE "Users" (
    "Id" uuid PRIMARY KEY
);

CREATE TABLE "Wallets" (
    "Id" uuid PRIMARY KEY,
    "UserId" uuid NOT NULL UNIQUE REFERENCES "Users" ("Id")
);

CREATE TABLE "MatchAuthorityBindings" (
    "MatchId" uuid PRIMARY KEY,
    "HostUserId" uuid NOT NULL REFERENCES "Users" ("Id"),
    "FusionSessionName" varchar(128) NOT NULL,
    "MaxPlayers" integer NOT NULL,
    "MapId" varchar(64),
    "Difficulty" varchar(16),
    "Status" varchar(20) NOT NULL,
    "LeaseExpiresAtUtc" timestamptz NOT NULL,
    "CreatedAtUtc" timestamptz NOT NULL,
    "UpdatedAtUtc" timestamptz NOT NULL,
    "StartedAtUtc" timestamptz,
    "EndedAtUtc" timestamptz
);

CREATE TABLE "MatchPlayerBindings" (
    "Id" uuid PRIMARY KEY,
    "MatchId" uuid NOT NULL REFERENCES "MatchAuthorityBindings" ("MatchId"),
    "UserId" uuid NOT NULL REFERENCES "Users" ("Id"),
    "FusionActorNumber" integer NOT NULL,
    "JoinProofId" uuid NOT NULL UNIQUE,
    "BoundAtUtc" timestamptz NOT NULL,
    "LastSeenAtUtc" timestamptz NOT NULL,
    "DisconnectedAtUtc" timestamptz,
    UNIQUE ("MatchId", "UserId")
);

CREATE TABLE "MatchResults" (
    "MatchId" uuid PRIMARY KEY REFERENCES "MatchAuthorityBindings" ("MatchId"),
    "SubmittedByUserId" uuid NOT NULL REFERENCES "Users" ("Id"),
    "Outcome" varchar(20) NOT NULL,
    "StartedAtUtc" timestamptz NOT NULL,
    "EndedAtUtc" timestamptz NOT NULL,
    "DurationSeconds" integer NOT NULL,
    "ObjectiveCompletion" numeric(5,4) NOT NULL,
    "PlayerCount" integer NOT NULL,
    "RosterKey" varchar(131),
    "PayloadHash" char(64) NOT NULL,
    "RewardStatus" varchar(20) NOT NULL,
    "SubmittedAtUtc" timestamptz NOT NULL,
    CONSTRAINT "CK_MatchResults_PlayerCount_Range" CHECK ("PlayerCount" BETWEEN 1 AND 4),
    CONSTRAINT "CK_MatchResults_Outcome" CHECK ("Outcome" IN ('WIN','LOSE')),
    CONSTRAINT "CK_MatchResults_RewardStatus" CHECK ("RewardStatus" IN ('Pending','Completed'))
);

CREATE TABLE "MatchResultPlayers" (
    "MatchId" uuid NOT NULL REFERENCES "MatchResults" ("MatchId"),
    "UserId" uuid NOT NULL,
    "Survived" boolean NOT NULL,
    "Disconnected" boolean NOT NULL,
    "DetectionCount" integer NOT NULL,
    "DownedCount" integer NOT NULL,
    "ReviveCount" integer NOT NULL,
    "ObjectiveContribution" integer NOT NULL,
    PRIMARY KEY ("MatchId", "UserId"),
    FOREIGN KEY ("MatchId", "UserId")
        REFERENCES "MatchPlayerBindings" ("MatchId", "UserId")
);

CREATE TABLE "MatchRewardGrants" (
    "Id" uuid PRIMARY KEY,
    "MatchId" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "WalletId" uuid NOT NULL REFERENCES "Wallets" ("Id"),
    "CurrencyAmount" integer NOT NULL,
    "PolicyVersion" varchar(50) NOT NULL,
    "ExperiencePointsAwarded" bigint NOT NULL,
    "ProgressionPolicyVersion" varchar(50) NOT NULL,
    "ProcessedAtUtc" timestamptz NOT NULL,
    UNIQUE ("MatchId", "UserId"),
    FOREIGN KEY ("MatchId", "UserId")
        REFERENCES "MatchResultPlayers" ("MatchId", "UserId"),
    CONSTRAINT "CK_MatchRewardGrants_NonNegative"
        CHECK ("CurrencyAmount" >= 0 AND "ExperiencePointsAwarded" >= 0)
);

CREATE TABLE "TeamProfiles" (
    "MatchId" uuid PRIMARY KEY REFERENCES "MatchResults" ("MatchId"),
    "ProcessingRevision" bigint NOT NULL,
    "ProcessingStatus" varchar(20) NOT NULL,
    "ProcessingReason" varchar(100) NOT NULL,
    "TelemetryCompleteness" varchar(20) NOT NULL,
    "ObjectiveTimeSeconds" numeric(14,6),
    "ObjectiveSpeedScore" numeric(9,6),
    "SurvivalScore" numeric(9,6),
    "TeamPerformanceScore" numeric(9,6),
    "TeamPerformanceStatus" varchar(20) NOT NULL,
    "ProfileFormulaVersion" varchar(80) NOT NULL,
    "SourceTelemetrySchemaVersion" varchar(20) NOT NULL,
    "SourceFingerprint" char(64) NOT NULL,
    "ProjectionFingerprint" char(64) NOT NULL,
    "CreatedAtUtc" timestamptz NOT NULL,
    "UpdatedAtUtc" timestamptz NOT NULL,
    CONSTRAINT "CK_TeamProfiles_SelectedPerformanceIncomplete" CHECK (
        "TeamPerformanceScore" IS NULL AND "TeamPerformanceStatus" = 'Incomplete'
    )
);

CREATE TABLE "PlayerAIProfiles" (
    "UserId" uuid PRIMARY KEY REFERENCES "Users" ("Id"),
    "ProfileLineageId" uuid NOT NULL,
    "ProfileRevision" bigint NOT NULL,
    "ProfileFormulaVersion" varchar(80) NOT NULL,
    "MatchScoreFormulaVersion" varchar(80) NOT NULL,
    "SurvivalScore" numeric(9,6) NOT NULL,
    "SurvivalStatus" varchar(20) NOT NULL,
    "SurvivalSampleCount" integer NOT NULL,
    "NoiseScore" numeric(9,6) NOT NULL,
    "NoiseStatus" varchar(20) NOT NULL,
    "NoiseSampleCount" integer NOT NULL,
    "ObjectiveScore" numeric(9,6),
    "ObjectiveStatus" varchar(20) NOT NULL,
    "ObjectiveSampleCount" integer NOT NULL,
    "ToolUsageScore" numeric(9,6),
    "ToolUsageStatus" varchar(20) NOT NULL,
    "ToolUsageSampleCount" integer NOT NULL,
    "TeamworkScore" numeric(9,6),
    "UpdatedAtUtc" timestamptz NOT NULL,
    -- In LTP ObjectiveScore and ToolUsageScore are supported, optional dimensions.
    -- TeamworkScore and other deferred dimensions must remain NULL.
    CONSTRAINT "CK_PlayerAIProfiles_SelectedDeferred" CHECK ("TeamworkScore" IS NULL),
    CONSTRAINT "CK_PlayerAIProfiles_ExpandedScoreRange" CHECK (
        ("ObjectiveScore" IS NULL OR "ObjectiveScore" BETWEEN 0 AND 100)
        AND ("ToolUsageScore" IS NULL OR "ToolUsageScore" BETWEEN 0 AND 100)
    ),
    CONSTRAINT "CK_PlayerAIProfiles_ExpandedCounts" CHECK (
        "ObjectiveSampleCount" >= 0 AND "ToolUsageSampleCount" >= 0
    )
);

CREATE TABLE "AdaptiveInputSnapshots" (
    "SnapshotId" uuid PRIMARY KEY,
    "MatchId" uuid NOT NULL REFERENCES "MatchAuthorityBindings" ("MatchId"),
    "DecisionPoint" varchar(30) NOT NULL,
    "SnapshotContentFingerprint" char(64) NOT NULL,
    "RosterIdentity" char(64) NOT NULL,
    "TeamSize" integer NOT NULL,
    "Validity" varchar(20) NOT NULL,
    "SurvivalAggregationStatus" varchar(20) NOT NULL,
    "NoiseAggregationStatus" varchar(20) NOT NULL,
    "SurvivalObservedActiveCount" integer NOT NULL,
    "NoiseObservedActiveCount" integer NOT NULL,
    "ReasonCodesJson" jsonb NOT NULL,
    "SurvivalMeanObservedScore" numeric(9,6),
    "NoiseMeanObservedScore" numeric(9,6),
    "CreatedAtUtc" timestamptz NOT NULL,
    CONSTRAINT "CK_AdaptiveSnapshots_TeamSize" CHECK ("TeamSize" BETWEEN 1 AND 4)
);

CREATE TABLE "AdaptiveInputSnapshotPlayers" (
    "SnapshotId" uuid NOT NULL REFERENCES "AdaptiveInputSnapshots" ("SnapshotId"),
    "UserId" uuid NOT NULL REFERENCES "Users" ("Id"),
    "ProfileAvailable" boolean NOT NULL,
    "ProfileLineageId" uuid,
    "ProfileRevision" bigint,
    "SurvivalScore" numeric(9,6),
    "SurvivalStatus" varchar(20) NOT NULL,
    "SurvivalSampleCount" integer,
    "NoiseScore" numeric(9,6),
    "NoiseStatus" varchar(20) NOT NULL,
    "NoiseSampleCount" integer,
    "DeferredDimensionsJson" jsonb NOT NULL,
    "CapturedAtUtc" timestamptz NOT NULL,
    PRIMARY KEY ("SnapshotId", "UserId")
);

CREATE TABLE "ScenarioConfigDefinitions" (
    "ScenarioConfigId" varchar(128) NOT NULL,
    "ScenarioConfigVersion" varchar(80) NOT NULL,
    "SchemaVersion" varchar(20) NOT NULL,
    "PolicyVersion" varchar(80) NOT NULL,
    "ConfigSource" varchar(20) NOT NULL,
    "MapId" varchar(128) NOT NULL,
    "MonsterType" varchar(128) NOT NULL,
    "ObjectiveSpawnSetId" varchar(128) NOT NULL,
    "SupportItemBudget" integer NOT NULL,
    "ChaseSpeed" double precision NOT NULL,
    "SearchDuration" double precision NOT NULL,
    "EscapeDoorTimerSeconds" double precision NOT NULL,
    "FallbackConfigId" varchar(128) NOT NULL,
    "FallbackConfigVersion" varchar(80) NOT NULL,
    "ContentWhitelistVersion" varchar(80) NOT NULL,
    "UnityCompatibilityVersion" varchar(80) NOT NULL,
    "IsActive" boolean NOT NULL,
    "IsProductionApproved" boolean NOT NULL,
    "IsFixedFallback" boolean NOT NULL,
    "CreatedAtUtc" timestamptz NOT NULL,
    "UpdatedAtUtc" timestamptz NOT NULL,
    PRIMARY KEY ("ScenarioConfigId", "ScenarioConfigVersion")
);

CREATE TABLE "ScenarioDecisions" (
    "DecisionId" uuid PRIMARY KEY,
    "MatchId" uuid NOT NULL REFERENCES "MatchAuthorityBindings" ("MatchId"),
    "HostUserId" uuid NOT NULL REFERENCES "Users" ("Id"),
    "SnapshotId" uuid NOT NULL UNIQUE REFERENCES "AdaptiveInputSnapshots" ("SnapshotId"),
    "ScenarioConfigId" varchar(128) NOT NULL,
    "ScenarioConfigVersion" varchar(80) NOT NULL,
    "PolicyConfigVersion" varchar(80),
    "EvidencePolicyVersion" varchar(80),
    "ParameterRegistryVersion" varchar(80),
    "CandidateValidationStatus" varchar(20),
    "ResolutionMode" varchar(20) NOT NULL,
    "DecisionPoint" varchar(30) NOT NULL,
    "ExperimentCondition" varchar(80),
    "UsedFixedFallback" boolean NOT NULL,
    "FallbackReasonCode" varchar(50),
    "ResolutionResult" varchar(30) NOT NULL,
    "Status" varchar(20) NOT NULL,
    "IsCurrent" boolean NOT NULL,
    "CommittedAtUtc" timestamptz NOT NULL,
    FOREIGN KEY ("ScenarioConfigId", "ScenarioConfigVersion")
        REFERENCES "ScenarioConfigDefinitions" ("ScenarioConfigId", "ScenarioConfigVersion")
);

-- One current scenario decision per match; index in source is filtered on IsCurrent.
CREATE UNIQUE INDEX "UX_ScenarioDecisions_CurrentMatch"
    ON "ScenarioDecisions" ("MatchId") WHERE "IsCurrent" = TRUE;

CREATE TABLE "ScenarioApplyReceipts" (
    "DecisionId" uuid PRIMARY KEY REFERENCES "ScenarioDecisions" ("DecisionId"),
    "MatchId" uuid NOT NULL UNIQUE,
    "ReportedByUserId" uuid NOT NULL REFERENCES "Users" ("Id"),
    "AppliedScenarioConfigId" varchar(128) NOT NULL,
    "AppliedScenarioConfigVersion" varchar(80) NOT NULL,
    "AppliedScenarioConfigFingerprint" char(64) NOT NULL,
    "AppliedAtUtc" timestamptz NOT NULL
);

-- Note: ScenarioApplyReceipts.MatchId is NOT configured as FK in AppDbContext.
-- Note: PlayerAIProfiles has further deferred nullable dimensions not drawn here.
-- Note: TeamProfiles also has additional metrics and status columns intentionally omitted.
-- Note: MongoDB gameplay telemetry is document storage and is NOT a PostgreSQL ERD entity.

-- LTP match-profile processing job. MatchId is the PK in EF mapping;
-- a separate database FK is not configured, so this visual DDL does not invent one.
CREATE TABLE "MatchProfileProcessingJobs" (
    "MatchId" uuid PRIMARY KEY,
    "Status" varchar(20) NOT NULL,
    "Attempts" integer NOT NULL,
    "NextAttemptAtUtc" timestamptz NOT NULL,
    "LeaseExpiresAtUtc" timestamptz,
    "LastError" varchar(200) NOT NULL
);

-- Additional LTP AED V2 planning ledger (not the same operation as PRE_MATCH resolve).
-- DecisionId is the PK; AppDbContext does not map it to ScenarioDecisions as an FK.
CREATE TABLE "ScenarioAdaptivePlansV2" (
    "DecisionId" uuid PRIMARY KEY,
    "MatchId" uuid NOT NULL REFERENCES "MatchAuthorityBindings" ("MatchId"),
    "HostUserId" uuid NOT NULL REFERENCES "Users" ("Id"),
    "SnapshotId" uuid NOT NULL REFERENCES "AdaptiveInputSnapshots" ("SnapshotId"),
    "PhaseOrdinal" integer NOT NULL,
    "DecisionPoint" varchar(30) NOT NULL,
    "PolicyVersion" varchar(80) NOT NULL,
    "BaselineVersion" varchar(80) NOT NULL,
    "ChangedKey" varchar(80) NOT NULL,
    "PreviousValue" double precision NOT NULL,
    "AppliedValue" double precision NOT NULL,
    "PlanValuesJson" jsonb NOT NULL,
    "CommitStatus" varchar(20) NOT NULL,
    "ApplyStatus" varchar(20) NOT NULL,
    "CommittedAtUtc" timestamptz,
    "AppliedAtUtc" timestamptz,
    UNIQUE ("MatchId", "PhaseOrdinal")
);

-- Selected relational tables; additional LTP research/pilot ledgers are excluded for readability.
-- NOTE (LTP): AEDPreMatchPolicy.Enabled defaults to false in appsettings.json,
-- and is true in appsettings.Development.json. Database entities do not prove
-- that an adaptive candidate is selected for every request.
-- NOTE: ScenarioAdaptivePlansV2 is a separate V2 planning ledger, not the same
-- table as the pre-match ScenarioDecisions candidate/fallback record.
-- Figure 05: Match, Reward and Adaptive Records (main baseline + LTP additions).
