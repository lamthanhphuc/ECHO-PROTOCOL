using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EchoProtocol.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRewardProgressionV3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Debug runs may only lose reward eligibility; the balance snapshot stays immutable.
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION rfb_guard_match_context()
                RETURNS trigger AS $$
                BEGIN
                    IF OLD."StartedAtUtc" IS NOT NULL AND
                    (
                        NEW."StartedAtUtc" IS DISTINCT FROM OLD."StartedAtUtc"
                        OR NEW."MapId" IS DISTINCT FROM OLD."MapId"
                        OR NEW."Difficulty" IS DISTINCT FROM OLD."Difficulty"
                        OR NEW."ResolutionMode" IS DISTINCT FROM OLD."ResolutionMode"
                        OR NEW."BalanceSource" IS DISTINCT FROM OLD."BalanceSource"
                        OR NEW."BalanceProtocolVersion" IS DISTINCT FROM OLD."BalanceProtocolVersion"
                        OR NEW."BalanceVersion" IS DISTINCT FROM OLD."BalanceVersion"
                        OR NEW."BalanceHash" IS DISTINCT FROM OLD."BalanceHash"
                        OR (NEW."MatchDataPolicy" IS DISTINCT FROM OLD."MatchDataPolicy"
                            AND NOT (OLD."MatchDataPolicy" = 'STANDARD' AND NEW."MatchDataPolicy" = 'TEST_ONLY'))
                    ) THEN
                        RAISE EXCEPTION 'MATCH_BALANCE_CONTEXT_IMMUTABLE';
                    END IF;

                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;
                """);
            migrationBuilder.DropCheckConstraint(
                name: "CK_MatchAuthorityBindings_BalancePolicy",
                table: "MatchAuthorityBindings");

            migrationBuilder.AddColumn<int>(
                name: "PreviousRequiredLevel",
                table: "ShopPriceProposal",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProposedRequiredLevel",
                table: "ShopPriceProposal",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RequiredLevel",
                table: "ShopItems",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<decimal>(
                name: "BaseMinutes",
                table: "RewardConfigurations",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "EasyMultiplier",
                table: "RewardConfigurations",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "FloodedReferenceMinutes",
                table: "RewardConfigurations",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "HardMultiplier",
                table: "RewardConfigurations",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PurgeReferenceMinutes",
                table: "RewardConfigurations",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SciFiReferenceMinutes",
                table: "RewardConfigurations",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "V3LossCreditsPerMinute",
                table: "RewardConfigurations",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "V3LossExperiencePerMinute",
                table: "RewardConfigurations",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "LevelCurveVersion",
                table: "PlayerProfiles",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "LEVEL_V1");

            migrationBuilder.AddColumn<long>(
                name: "LifetimeExperiencePoints",
                table: "PlayerProfiles",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddCheckConstraint(
                name: "CK_MatchAuthorityBindings_BalancePolicy",
                table: "MatchAuthorityBindings",
                sql: "COALESCE((\r\n    \"ResolutionMode\" IS NULL\r\n    AND \"BalanceSource\" IS NULL\r\n    AND \"BalanceProtocolVersion\" IS NULL\r\n    AND \"BalanceVersion\" IS NULL\r\n    AND \"BalanceHash\" IS NULL\r\n    AND \"MatchDataPolicy\" IN ('TEST_ONLY', 'STANDARD')\r\n)\r\nOR\r\n(\r\n    \"BalanceSource\" = 'LOCAL'\r\n    AND \"ResolutionMode\" IN ('FIXED', 'ADAPTIVE')\r\n    AND \"BalanceProtocolVersion\" = 'RFB-1'\r\n    AND \"BalanceVersion\" IS NULL\r\n    AND \"BalanceHash\" IS NULL\r\n    AND \"MatchDataPolicy\" IN ('TEST_ONLY', 'STANDARD')\r\n    AND \"MapId\" IS NOT NULL\r\n    AND \"Difficulty\" IS NOT NULL\r\n)\r\nOR\r\n(\r\n    \"BalanceSource\" = 'REMOTE_FIXED'\r\n    AND (\"ResolutionMode\" = 'FIXED' OR\r\n        (\"ResolutionMode\" = 'ADAPTIVE' AND \"BalanceBuildVersion\" IS NOT NULL\r\n         AND \"MapId\" = 'SciFi' AND \"Difficulty\" = 'NORMAL'))\r\n    AND \"BalanceProtocolVersion\" = 'RFB-1'\r\n    AND \"BalanceVersion\" IS NOT NULL\r\n    AND \"BalanceHash\" IS NOT NULL\r\n    AND \"MatchDataPolicy\" IN ('TEST_ONLY', 'STANDARD')\r\n    AND \"MapId\" IS NOT NULL\r\n    AND \"Difficulty\" IS NOT NULL\r\n    AND \"MapId\" IN ('SciFi', 'Scene_A', 'Scene_B')\r\n    AND \"Difficulty\" IN ('EASY', 'NORMAL', 'HARD')\r\n), FALSE)");
            migrationBuilder.Sql("""
                UPDATE "RewardConfigurations" SET "BaseMinutes" = 20, "SciFiReferenceMinutes" = 20,
                    "FloodedReferenceMinutes" = 20, "PurgeReferenceMinutes" = 20,
                    "EasyMultiplier" = 0.90, "HardMultiplier" = 1.15,
                    "V3LossCreditsPerMinute" = 4, "V3LossExperiencePerMinute" = 2;
                UPDATE "RewardConfigurations" SET "PolicyKey" = 'REWARD_V3'
                    WHERE "PolicyKey" = 'REWARD_V2' AND "Win" = 150 AND "Objective" = 70
                    AND "Contribution" = 70 AND "Survival" = 10 AND "Maximum" = 320
                    AND "ObjectiveActionCap" = 4 AND "RescueActionCap" = 2
                    AND "RescueBonusCredits" = 10 AND "Participation" = 0 AND "Disconnected" = 0
                    AND "OneActionTeamPercent" = 50 AND "TwoActionTeamPercent" = 75 AND "FullTeamPercent" = 100
                    AND "WinExperience" = 75 AND "ObjectiveExperience" = 50
                    AND "ContributionExperience" = 25 AND "SurvivalExperience" = 25;
                UPDATE "ShopItems" SET "RequiredLevel" = CASE "ItemName"
                    WHEN 'Jammo' THEN 3 WHEN 'Nightmare' THEN 4 WHEN 'Soul Eater' THEN 5
                    WHEN 'Cursed Priest' THEN 6 WHEN 'Terror Bringer' THEN 8
                    WHEN 'Mercenary' THEN 10 WHEN 'Usurper' THEN 12 ELSE 1 END;
                WITH steps AS (
                    SELECT level, (25 * ceil((250 + 25 * (level-1) + 3 * (level-1) * (level-1)) / 25.0))::bigint AS needed
                    FROM generate_series(1, 30) AS level
                ), curve AS (
                    SELECT level, needed, COALESCE(sum(needed) OVER (ORDER BY level ROWS BETWEEN UNBOUNDED PRECEDING AND 1 PRECEDING),0)::bigint AS threshold
                    FROM steps
                )
                UPDATE "PlayerProfiles" p SET "LifetimeExperiencePoints" = p."ExperiencePoints",
                    "ExperiencePoints" = c.threshold + floor((p."ExperiencePoints" % 500) * c.needed / 500.0)::bigint,
                    "LevelCurveVersion" = 'LEVEL_V3'
                FROM curve c WHERE c.level = LEAST(GREATEST(p."Level",1),30);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION rfb_guard_match_context()
                RETURNS trigger AS $$
                BEGIN
                    IF OLD."StartedAtUtc" IS NOT NULL AND
                    (
                        NEW."StartedAtUtc" IS DISTINCT FROM OLD."StartedAtUtc"
                        OR NEW."MapId" IS DISTINCT FROM OLD."MapId"
                        OR NEW."Difficulty" IS DISTINCT FROM OLD."Difficulty"
                        OR NEW."ResolutionMode" IS DISTINCT FROM OLD."ResolutionMode"
                        OR NEW."BalanceSource" IS DISTINCT FROM OLD."BalanceSource"
                        OR NEW."BalanceProtocolVersion" IS DISTINCT FROM OLD."BalanceProtocolVersion"
                        OR NEW."BalanceVersion" IS DISTINCT FROM OLD."BalanceVersion"
                        OR NEW."BalanceHash" IS DISTINCT FROM OLD."BalanceHash"
                        OR NEW."MatchDataPolicy" IS DISTINCT FROM OLD."MatchDataPolicy"
                    ) THEN
                        RAISE EXCEPTION 'MATCH_BALANCE_CONTEXT_IMMUTABLE';
                    END IF;

                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;
                """);
            migrationBuilder.Sql("""
                UPDATE "RewardConfigurations" SET "PolicyKey" = 'REWARD_V2' WHERE "PolicyKey" = 'REWARD_V3';
                UPDATE "PlayerProfiles" SET "ExperiencePoints" = "LifetimeExperiencePoints",
                    "Level" = GREATEST("Level", LEAST(2147483647, "LifetimeExperiencePoints" / 500 + 1)::integer)
                    WHERE "LevelCurveVersion" = 'LEVEL_V3';
                """);
            migrationBuilder.DropCheckConstraint(
                name: "CK_MatchAuthorityBindings_BalancePolicy",
                table: "MatchAuthorityBindings");

            migrationBuilder.DropColumn(
                name: "PreviousRequiredLevel",
                table: "ShopPriceProposal");

            migrationBuilder.DropColumn(
                name: "ProposedRequiredLevel",
                table: "ShopPriceProposal");

            migrationBuilder.DropColumn(
                name: "RequiredLevel",
                table: "ShopItems");

            migrationBuilder.DropColumn(
                name: "BaseMinutes",
                table: "RewardConfigurations");

            migrationBuilder.DropColumn(
                name: "EasyMultiplier",
                table: "RewardConfigurations");

            migrationBuilder.DropColumn(
                name: "FloodedReferenceMinutes",
                table: "RewardConfigurations");

            migrationBuilder.DropColumn(
                name: "HardMultiplier",
                table: "RewardConfigurations");

            migrationBuilder.DropColumn(
                name: "PurgeReferenceMinutes",
                table: "RewardConfigurations");

            migrationBuilder.DropColumn(
                name: "SciFiReferenceMinutes",
                table: "RewardConfigurations");

            migrationBuilder.DropColumn(
                name: "V3LossCreditsPerMinute",
                table: "RewardConfigurations");

            migrationBuilder.DropColumn(
                name: "V3LossExperiencePerMinute",
                table: "RewardConfigurations");

            migrationBuilder.DropColumn(
                name: "LevelCurveVersion",
                table: "PlayerProfiles");

            migrationBuilder.DropColumn(
                name: "LifetimeExperiencePoints",
                table: "PlayerProfiles");

            // Keep TEST_ONLY compatible with local/legacy matches after rollback.
            migrationBuilder.AddCheckConstraint(
                name: "CK_MatchAuthorityBindings_BalancePolicy",
                table: "MatchAuthorityBindings",
                sql: "COALESCE((\r\n    \"ResolutionMode\" IS NULL\r\n    AND \"BalanceSource\" IS NULL\r\n    AND \"BalanceProtocolVersion\" IS NULL\r\n    AND \"BalanceVersion\" IS NULL\r\n    AND \"BalanceHash\" IS NULL\r\n    AND \"MatchDataPolicy\" IN ('TEST_ONLY', 'STANDARD')\r\n)\r\nOR\r\n(\r\n    \"BalanceSource\" = 'LOCAL'\r\n    AND \"ResolutionMode\" IN ('FIXED', 'ADAPTIVE')\r\n    AND \"BalanceProtocolVersion\" = 'RFB-1'\r\n    AND \"BalanceVersion\" IS NULL\r\n    AND \"BalanceHash\" IS NULL\r\n    AND \"MatchDataPolicy\" IN ('TEST_ONLY', 'STANDARD')\r\n    AND \"MapId\" IS NOT NULL\r\n    AND \"Difficulty\" IS NOT NULL\r\n)\r\nOR\r\n(\r\n    \"BalanceSource\" = 'REMOTE_FIXED'\r\n    AND (\"ResolutionMode\" = 'FIXED' OR\r\n        (\"ResolutionMode\" = 'ADAPTIVE' AND \"BalanceBuildVersion\" IS NOT NULL\r\n         AND \"MapId\" = 'SciFi' AND \"Difficulty\" = 'NORMAL'))\r\n    AND \"BalanceProtocolVersion\" = 'RFB-1'\r\n    AND \"BalanceVersion\" IS NOT NULL\r\n    AND \"BalanceHash\" IS NOT NULL\r\n    AND \"MatchDataPolicy\" IN ('TEST_ONLY', 'STANDARD')\r\n    AND \"MapId\" IS NOT NULL\r\n    AND \"Difficulty\" IS NOT NULL\r\n    AND \"MapId\" IN ('SciFi', 'Scene_A', 'Scene_B')\r\n    AND \"Difficulty\" IN ('EASY', 'NORMAL', 'HARD')\r\n), FALSE)");
        }
    }
}
