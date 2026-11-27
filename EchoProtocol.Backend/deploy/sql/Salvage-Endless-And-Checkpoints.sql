START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261127041821_AllowEndlessSalvageDuration') THEN
    ALTER TABLE "MatchResults" DROP CONSTRAINT "CK_MatchResults_DurationSeconds_Range";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261127041821_AllowEndlessSalvageDuration') THEN
    ALTER TABLE "MatchResults" ADD CONSTRAINT "CK_MatchResults_DurationSeconds_Range" CHECK ("DurationSeconds" >= 60 AND ("DurationSeconds" <= 7260 OR "SalvageCompletedDays" IS NOT NULL));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261127041821_AllowEndlessSalvageDuration') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261127041821_AllowEndlessSalvageDuration', '8.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261127043652_AddSalvageServerCheckpoints') THEN
    CREATE TABLE "SalvageCheckpoints" (
        "MatchId" uuid NOT NULL,
        "OwnerId" uuid NOT NULL,
        "Revision" bigint NOT NULL,
        "StateJson" character varying(262144) NOT NULL,
        "ResultJson" character varying(65536) NOT NULL,
        "SavedAtUtc" timestamp with time zone NOT NULL,
        "Failed" boolean NOT NULL,
        CONSTRAINT "PK_SalvageCheckpoints" PRIMARY KEY ("MatchId"),
        CONSTRAINT "FK_SalvageCheckpoints_MatchAuthorityBindings_MatchId" FOREIGN KEY ("MatchId") REFERENCES "MatchAuthorityBindings" ("MatchId") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261127043652_AddSalvageServerCheckpoints') THEN
    CREATE INDEX "IX_SalvageCheckpoints_OwnerId_SavedAtUtc" ON "SalvageCheckpoints" ("OwnerId", "SavedAtUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261127043652_AddSalvageServerCheckpoints') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261127043652_AddSalvageServerCheckpoints', '8.0.11');
    END IF;
END $EF$;
COMMIT;

