START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261125161606_AddSalvageCoreContext') THEN
    ALTER TABLE "MatchAuthorityBindings" DROP CONSTRAINT "CK_MatchAuthorityBindings_LeaderboardContext";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261125161606_AddSalvageCoreContext') THEN
    ALTER TABLE "MatchAuthorityBindings" ADD CONSTRAINT "CK_MatchAuthorityBindings_LeaderboardContext" CHECK (("MapId" IS NULL AND "Difficulty" IS NULL) OR ("MapId" IS NOT NULL AND "Difficulty" IS NOT NULL AND "MapId" IN ('SciFi', 'Scene_A', 'Scene_B', 'Salvage_SciFi') AND "Difficulty" IN ('EASY', 'NORMAL', 'HARD')));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261125161606_AddSalvageCoreContext') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261125161606_AddSalvageCoreContext', '8.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261126182035_AddSalvageAccountRewards') THEN
    ALTER TABLE "MatchResults" ADD "SalvageCompletedDays" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261126182035_AddSalvageAccountRewards') THEN
    ALTER TABLE "MatchResults" ADD "SalvageCompletedLevels" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261126182035_AddSalvageAccountRewards') THEN
    ALTER TABLE "MatchResults" ADD "SalvageUsedDebugCheats" boolean;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261126182035_AddSalvageAccountRewards') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261126182035_AddSalvageAccountRewards', '8.0.11');
    END IF;
END $EF$;
COMMIT;

