CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260711144210_InitialAuthSchema') THEN
    CREATE TABLE "Users" (
        "Id" uuid NOT NULL,
        "Username" character varying(100) NOT NULL,
        "PasswordHash" character varying(255) NOT NULL,
        "Role" character varying(20) NOT NULL,
        "Status" character varying(20) NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_Users" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260711144210_InitialAuthSchema') THEN
    CREATE TABLE "PlayerProfiles" (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "DisplayName" character varying(100) NOT NULL,
        "TotalMatches" integer NOT NULL DEFAULT 0,
        "TotalWins" integer NOT NULL DEFAULT 0,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_PlayerProfiles" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_PlayerProfiles_TotalMatches_NonNegative" CHECK ("TotalMatches" >= 0),
        CONSTRAINT "CK_PlayerProfiles_TotalWins_Lte_Matches" CHECK ("TotalWins" <= "TotalMatches"),
        CONSTRAINT "CK_PlayerProfiles_TotalWins_NonNegative" CHECK ("TotalWins" >= 0),
        CONSTRAINT "FK_PlayerProfiles_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260711144210_InitialAuthSchema') THEN
    CREATE TABLE "Wallets" (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "Balance" integer NOT NULL DEFAULT 0,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_Wallets" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_Wallets_Balance_NonNegative" CHECK ("Balance" >= 0),
        CONSTRAINT "FK_Wallets_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260711144210_InitialAuthSchema') THEN
    CREATE UNIQUE INDEX "IX_PlayerProfiles_UserId" ON "PlayerProfiles" ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260711144210_InitialAuthSchema') THEN
    CREATE UNIQUE INDEX "IX_Users_Username" ON "Users" ("Username");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260711144210_InitialAuthSchema') THEN
    CREATE UNIQUE INDEX "IX_Wallets_UserId" ON "Wallets" ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260711144210_InitialAuthSchema') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260711144210_InitialAuthSchema', '8.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260822064348_AddUserEmail') THEN
    ALTER TABLE "Users" ADD "Email" character varying(255);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260822064348_AddUserEmail') THEN
    UPDATE "Users" SET "Email" = LOWER("Id"::text) || '@legacy.echo.invalid' WHERE "Email" IS NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260822064348_AddUserEmail') THEN
    ALTER TABLE "Users" ALTER COLUMN "Email" SET NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260822064348_AddUserEmail') THEN
    CREATE UNIQUE INDEX "IX_Users_Email" ON "Users" ("Email");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260822064348_AddUserEmail') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260822064348_AddUserEmail', '8.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260901040612_AddMatchAuthorityBindings') THEN
    CREATE TABLE "MatchAuthorityBindings" (
        "MatchId" uuid NOT NULL,
        "FusionSessionName" character varying(128) NOT NULL,
        "HostUserId" uuid NOT NULL,
        "MaxPlayers" integer NOT NULL,
        "Status" character varying(20) NOT NULL,
        "LeaseExpiresAtUtc" timestamp with time zone NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "UpdatedAtUtc" timestamp with time zone NOT NULL,
        "EndedAtUtc" timestamp with time zone,
        CONSTRAINT "PK_MatchAuthorityBindings" PRIMARY KEY ("MatchId"),
        CONSTRAINT "FK_MatchAuthorityBindings_Users_HostUserId" FOREIGN KEY ("HostUserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260901040612_AddMatchAuthorityBindings') THEN
    CREATE TABLE "MatchPlayerBindings" (
        "Id" uuid NOT NULL,
        "MatchId" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "FusionActorNumber" integer NOT NULL,
        "JoinProofId" uuid NOT NULL,
        "BoundAtUtc" timestamp with time zone NOT NULL,
        "LastSeenAtUtc" timestamp with time zone NOT NULL,
        "DisconnectedAtUtc" timestamp with time zone,
        CONSTRAINT "PK_MatchPlayerBindings" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_MatchPlayerBindings_MatchAuthorityBindings_MatchId" FOREIGN KEY ("MatchId") REFERENCES "MatchAuthorityBindings" ("MatchId") ON DELETE CASCADE,
        CONSTRAINT "FK_MatchPlayerBindings_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260901040612_AddMatchAuthorityBindings') THEN
    CREATE INDEX "IX_MatchAuthorityBindings_FusionSessionName_Status" ON "MatchAuthorityBindings" ("FusionSessionName", "Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260901040612_AddMatchAuthorityBindings') THEN
    CREATE INDEX "IX_MatchAuthorityBindings_HostUserId" ON "MatchAuthorityBindings" ("HostUserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260901040612_AddMatchAuthorityBindings') THEN
    CREATE UNIQUE INDEX "IX_MatchPlayerBindings_JoinProofId" ON "MatchPlayerBindings" ("JoinProofId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260901040612_AddMatchAuthorityBindings') THEN
    CREATE UNIQUE INDEX "IX_MatchPlayerBindings_MatchId_FusionActorNumber" ON "MatchPlayerBindings" ("MatchId", "FusionActorNumber");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260901040612_AddMatchAuthorityBindings') THEN
    CREATE UNIQUE INDEX "IX_MatchPlayerBindings_MatchId_UserId" ON "MatchPlayerBindings" ("MatchId", "UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260901040612_AddMatchAuthorityBindings') THEN
    CREATE INDEX "IX_MatchPlayerBindings_UserId" ON "MatchPlayerBindings" ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260901040612_AddMatchAuthorityBindings') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260901040612_AddMatchAuthorityBindings', '8.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920093819_AddMatchResultsM4009') THEN
    DROP INDEX "IX_MatchPlayerBindings_MatchId_UserId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920093819_AddMatchResultsM4009') THEN
    ALTER TABLE "MatchAuthorityBindings" ADD "StartedAtUtc" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920093819_AddMatchResultsM4009') THEN
    ALTER TABLE "MatchPlayerBindings" ADD CONSTRAINT "AK_MatchPlayerBindings_MatchId_UserId" UNIQUE ("MatchId", "UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920093819_AddMatchResultsM4009') THEN
    CREATE TABLE "MatchResults" (
        "MatchId" uuid NOT NULL,
        "SubmittedByUserId" uuid NOT NULL,
        "Outcome" character varying(20) NOT NULL,
        "StartedAtUtc" timestamp with time zone NOT NULL,
        "EndedAtUtc" timestamp with time zone NOT NULL,
        "DurationSeconds" integer NOT NULL,
        "ObjectiveCompletion" numeric(5,4) NOT NULL,
        "PlayerCount" integer NOT NULL,
        "PayloadHash" character(64) NOT NULL,
        "RewardStatus" character varying(20) NOT NULL,
        "SubmittedAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_MatchResults" PRIMARY KEY ("MatchId"),
        CONSTRAINT "CK_MatchResults_DurationSeconds_Range" CHECK ("DurationSeconds" >= 60 AND "DurationSeconds" <= 900),
        CONSTRAINT "CK_MatchResults_Outcome_Allowed" CHECK ("Outcome" IN ('WIN', 'LOSE')),
        CONSTRAINT "CK_MatchResults_ObjectiveCompletion_Range" CHECK (CAST("ObjectiveCompletion" AS NUMERIC) >= 0 AND CAST("ObjectiveCompletion" AS NUMERIC) <= 1),
        CONSTRAINT "CK_MatchResults_PlayerCount_Range" CHECK ("PlayerCount" >= 1 AND "PlayerCount" <= 4),
        CONSTRAINT "CK_MatchResults_RewardStatus_Pending" CHECK ("RewardStatus" = 'Pending'),
        CONSTRAINT "CK_MatchResults_Timestamp_Order" CHECK ("EndedAtUtc" >= "StartedAtUtc"),
        CONSTRAINT "FK_MatchResults_MatchAuthorityBindings_MatchId" FOREIGN KEY ("MatchId") REFERENCES "MatchAuthorityBindings" ("MatchId") ON DELETE RESTRICT,
        CONSTRAINT "FK_MatchResults_Users_SubmittedByUserId" FOREIGN KEY ("SubmittedByUserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920093819_AddMatchResultsM4009') THEN
    CREATE TABLE "MatchResultPlayers" (
        "MatchId" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "Survived" boolean NOT NULL,
        "Disconnected" boolean NOT NULL,
        "DetectionCount" integer NOT NULL,
        "DownedCount" integer NOT NULL,
        "ReviveCount" integer NOT NULL,
        "ObjectiveContribution" integer NOT NULL,
        CONSTRAINT "PK_MatchResultPlayers" PRIMARY KEY ("MatchId", "UserId"),
        CONSTRAINT "CK_MatchResultPlayers_DetectionCount_NonNegative" CHECK ("DetectionCount" >= 0),
        CONSTRAINT "CK_MatchResultPlayers_DownedCount_NonNegative" CHECK ("DownedCount" >= 0),
        CONSTRAINT "CK_MatchResultPlayers_ObjectiveContribution_NonNegative" CHECK ("ObjectiveContribution" >= 0),
        CONSTRAINT "CK_MatchResultPlayers_ReviveCount_NonNegative" CHECK ("ReviveCount" >= 0),
        CONSTRAINT "FK_MatchResultPlayers_MatchPlayerBindings_MatchId_UserId" FOREIGN KEY ("MatchId", "UserId") REFERENCES "MatchPlayerBindings" ("MatchId", "UserId") ON DELETE RESTRICT,
        CONSTRAINT "FK_MatchResultPlayers_MatchResults_MatchId" FOREIGN KEY ("MatchId") REFERENCES "MatchResults" ("MatchId") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920093819_AddMatchResultsM4009') THEN
    CREATE INDEX "IX_MatchResults_SubmittedAtUtc" ON "MatchResults" ("SubmittedAtUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920093819_AddMatchResultsM4009') THEN
    CREATE INDEX "IX_MatchResults_SubmittedByUserId" ON "MatchResults" ("SubmittedByUserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920093819_AddMatchResultsM4009') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260920093819_AddMatchResultsM4009', '8.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920102130_AddRewardWalletLedgerM4010') THEN
    ALTER TABLE "MatchResults" DROP CONSTRAINT "CK_MatchResults_RewardStatus_Pending";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920102130_AddRewardWalletLedgerM4010') THEN
    CREATE TABLE "MatchRewardGrants" (
        "Id" uuid NOT NULL,
        "MatchId" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "WalletId" uuid NOT NULL,
        "CurrencyAmount" integer NOT NULL,
        "PolicyVersion" character varying(50) NOT NULL,
        "ProcessedAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_MatchRewardGrants" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_MatchRewardGrants_CurrencyAmount_NonNegative" CHECK ("CurrencyAmount" >= 0),
        CONSTRAINT "FK_MatchRewardGrants_MatchResultPlayers_MatchId_UserId" FOREIGN KEY ("MatchId", "UserId") REFERENCES "MatchResultPlayers" ("MatchId", "UserId") ON DELETE RESTRICT,
        CONSTRAINT "FK_MatchRewardGrants_Wallets_WalletId" FOREIGN KEY ("WalletId") REFERENCES "Wallets" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920102130_AddRewardWalletLedgerM4010') THEN
    CREATE TABLE "WalletTransactions" (
        "Id" uuid NOT NULL,
        "WalletId" uuid NOT NULL,
        "Type" character varying(30) NOT NULL,
        "Amount" integer NOT NULL,
        "BalanceBefore" integer NOT NULL,
        "BalanceAfter" integer NOT NULL,
        "ReferenceId" uuid NOT NULL,
        "Description" character varying(255) NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_WalletTransactions" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_WalletTransactions_Amount_NonNegative" CHECK ("Amount" >= 0),
        CONSTRAINT "CK_WalletTransactions_BalanceEquation" CHECK ("BalanceAfter" = "BalanceBefore" + "Amount"),
        CONSTRAINT "CK_WalletTransactions_Balances_NonNegative" CHECK ("BalanceBefore" >= 0 AND "BalanceAfter" >= 0),
        CONSTRAINT "FK_WalletTransactions_Wallets_WalletId" FOREIGN KEY ("WalletId") REFERENCES "Wallets" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920102130_AddRewardWalletLedgerM4010') THEN
    ALTER TABLE "MatchResults" ADD CONSTRAINT "CK_MatchResults_RewardStatus_Allowed" CHECK ("RewardStatus" IN ('Pending', 'Completed'));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920102130_AddRewardWalletLedgerM4010') THEN
    CREATE UNIQUE INDEX "IX_MatchRewardGrants_MatchId_UserId" ON "MatchRewardGrants" ("MatchId", "UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920102130_AddRewardWalletLedgerM4010') THEN
    CREATE INDEX "IX_MatchRewardGrants_WalletId" ON "MatchRewardGrants" ("WalletId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920102130_AddRewardWalletLedgerM4010') THEN
    CREATE INDEX "IX_WalletTransactions_ReferenceId" ON "WalletTransactions" ("ReferenceId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920102130_AddRewardWalletLedgerM4010') THEN
    CREATE UNIQUE INDEX "IX_WalletTransactions_WalletId_Type_ReferenceId" ON "WalletTransactions" ("WalletId", "Type", "ReferenceId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920102130_AddRewardWalletLedgerM4010') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260920102130_AddRewardWalletLedgerM4010', '8.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920104639_AddPlayerProgressionM4011') THEN
    ALTER TABLE "PlayerProfiles" ADD "ExperiencePoints" bigint NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920104639_AddPlayerProgressionM4011') THEN
    ALTER TABLE "PlayerProfiles" ADD "Level" integer NOT NULL DEFAULT 1;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920104639_AddPlayerProgressionM4011') THEN
    ALTER TABLE "MatchRewardGrants" ADD "ExperiencePointsAwarded" bigint NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920104639_AddPlayerProgressionM4011') THEN
    ALTER TABLE "MatchRewardGrants" ADD "ProgressionPolicyVersion" character varying(50) NOT NULL DEFAULT 'LEGACY_UNVERSIONED';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920104639_AddPlayerProgressionM4011') THEN
    ALTER TABLE "PlayerProfiles" ADD CONSTRAINT "CK_PlayerProfiles_ExperiencePoints_NonNegative" CHECK ("ExperiencePoints" >= 0);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920104639_AddPlayerProgressionM4011') THEN
    ALTER TABLE "PlayerProfiles" ADD CONSTRAINT "CK_PlayerProfiles_Level_Positive" CHECK ("Level" >= 1);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920104639_AddPlayerProgressionM4011') THEN
    ALTER TABLE "MatchRewardGrants" ADD CONSTRAINT "CK_MatchRewardGrants_ExperiencePointsAwarded_NonNegative" CHECK ("ExperiencePointsAwarded" >= 0);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920104639_AddPlayerProgressionM4011') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260920104639_AddPlayerProgressionM4011', '8.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920110211_AddShopCatalogM4012M4013') THEN
    CREATE TABLE "ShopItems" (
        "ItemId" uuid NOT NULL,
        "ItemName" character varying(100) NOT NULL,
        "Description" character varying(1000) NOT NULL,
        "Category" character varying(50) NOT NULL,
        "Price" integer NOT NULL,
        "AssetReference" character varying(500) NOT NULL,
        "IsActive" boolean NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "UpdatedAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_ShopItems" PRIMARY KEY ("ItemId"),
        CONSTRAINT "CK_ShopItems_Price_NonNegative" CHECK ("Price" >= 0)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920110211_AddShopCatalogM4012M4013') THEN
    CREATE INDEX "IX_ShopItems_Active_Category_Name" ON "ShopItems" ("IsActive", "Category", "ItemName", "ItemId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920110211_AddShopCatalogM4012M4013') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260920110211_AddShopCatalogM4012M4013', '8.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920111342_AddPurchaseInventoryM4014M4015') THEN
    ALTER TABLE "WalletTransactions" DROP CONSTRAINT "CK_WalletTransactions_Amount_NonNegative";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920111342_AddPurchaseInventoryM4014M4015') THEN
    CREATE TABLE "PurchaseTransactions" (
        "PurchaseId" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "ShopItemId" uuid NOT NULL,
        "IdempotencyKey" character varying(100) NOT NULL,
        "PriceAtPurchase" integer NOT NULL,
        "WalletTransactionId" uuid NOT NULL,
        "Status" character varying(20) NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_PurchaseTransactions" PRIMARY KEY ("PurchaseId"),
        CONSTRAINT "CK_PurchaseTransactions_PriceAtPurchase_NonNegative" CHECK ("PriceAtPurchase" >= 0),
        CONSTRAINT "CK_PurchaseTransactions_Status_Completed" CHECK ("Status" = 'COMPLETED'),
        CONSTRAINT "FK_PurchaseTransactions_ShopItems_ShopItemId" FOREIGN KEY ("ShopItemId") REFERENCES "ShopItems" ("ItemId") ON DELETE RESTRICT,
        CONSTRAINT "FK_PurchaseTransactions_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_PurchaseTransactions_WalletTransactions_WalletTransactionId" FOREIGN KEY ("WalletTransactionId") REFERENCES "WalletTransactions" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920111342_AddPurchaseInventoryM4014M4015') THEN
    CREATE TABLE "InventoryItems" (
        "InventoryItemId" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "ShopItemId" uuid NOT NULL,
        "Source" character varying(50) NOT NULL,
        "PurchaseId" uuid,
        "AcquiredAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_InventoryItems" PRIMARY KEY ("InventoryItemId"),
        CONSTRAINT "FK_InventoryItems_PurchaseTransactions_PurchaseId" FOREIGN KEY ("PurchaseId") REFERENCES "PurchaseTransactions" ("PurchaseId") ON DELETE RESTRICT,
        CONSTRAINT "FK_InventoryItems_ShopItems_ShopItemId" FOREIGN KEY ("ShopItemId") REFERENCES "ShopItems" ("ItemId") ON DELETE RESTRICT,
        CONSTRAINT "FK_InventoryItems_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920111342_AddPurchaseInventoryM4014M4015') THEN
    ALTER TABLE "WalletTransactions" ADD CONSTRAINT "CK_WalletTransactions_Amount_ByType" CHECK (("Type" = 'MATCH_REWARD' AND "Amount" >= 0) OR ("Type" = 'PURCHASE' AND "Amount" <= 0));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920111342_AddPurchaseInventoryM4014M4015') THEN
    CREATE UNIQUE INDEX "IX_InventoryItems_PurchaseId" ON "InventoryItems" ("PurchaseId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920111342_AddPurchaseInventoryM4014M4015') THEN
    CREATE INDEX "IX_InventoryItems_ShopItemId" ON "InventoryItems" ("ShopItemId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920111342_AddPurchaseInventoryM4014M4015') THEN
    CREATE UNIQUE INDEX "IX_InventoryItems_UserId_ShopItemId" ON "InventoryItems" ("UserId", "ShopItemId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920111342_AddPurchaseInventoryM4014M4015') THEN
    CREATE INDEX "IX_PurchaseTransactions_ShopItemId" ON "PurchaseTransactions" ("ShopItemId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920111342_AddPurchaseInventoryM4014M4015') THEN
    CREATE UNIQUE INDEX "IX_PurchaseTransactions_UserId_IdempotencyKey" ON "PurchaseTransactions" ("UserId", "IdempotencyKey");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920111342_AddPurchaseInventoryM4014M4015') THEN
    CREATE UNIQUE INDEX "IX_PurchaseTransactions_WalletTransactionId" ON "PurchaseTransactions" ("WalletTransactionId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920111342_AddPurchaseInventoryM4014M4015') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260920111342_AddPurchaseInventoryM4014M4015', '8.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921061308_AddPlayerAIProfileMatchScoreM4027M4038M4039') THEN
    CREATE TABLE "PlayerAIProfiles" (
        "UserId" uuid NOT NULL,
        "ProfileLineageId" uuid NOT NULL,
        "ProfileRevision" bigint NOT NULL,
        "ProfileFormulaVersion" character varying(80) NOT NULL,
        "MatchScoreFormulaVersion" character varying(80) NOT NULL,
        "NormalizationConfigVersion" character varying(80),
        "ProfileNoiseFilterVersion" character varying(80),
        "AlphaConfigVersion" character varying(80) NOT NULL,
        "SurvivalScore" numeric(9,6) NOT NULL DEFAULT 50.0,
        "SurvivalStatus" character varying(20) NOT NULL,
        "SurvivalSampleCount" integer NOT NULL,
        "SurvivalLastMatchEndTs" timestamp with time zone,
        "SurvivalLastMatchId" uuid,
        "SurvivalLastUpdatedAtUtc" timestamp with time zone,
        "NoiseScore" numeric(9,6) NOT NULL DEFAULT 50.0,
        "NoiseStatus" character varying(20) NOT NULL,
        "NoiseSampleCount" integer NOT NULL,
        "NoiseLastMatchEndTs" timestamp with time zone,
        "NoiseLastMatchId" uuid,
        "NoiseLastUpdatedAtUtc" timestamp with time zone,
        "ObjectiveScore" numeric,
        "TeamworkScore" numeric,
        "ExplorationScore" numeric,
        "NavigationScore" numeric,
        "ToolUsageScore" numeric,
        "RiskScore" numeric,
        "ReviveScore" numeric,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "UpdatedAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_PlayerAIProfiles" PRIMARY KEY ("UserId"),
        CONSTRAINT "AK_PlayerAIProfiles_ProfileLineageId" UNIQUE ("ProfileLineageId"),
        CONSTRAINT "CK_PlayerAIProfiles_Deferred_Null" CHECK ("ObjectiveScore" IS NULL AND "TeamworkScore" IS NULL AND "ExplorationScore" IS NULL AND "NavigationScore" IS NULL AND "ToolUsageScore" IS NULL AND "RiskScore" IS NULL AND "ReviveScore" IS NULL),
        CONSTRAINT "CK_PlayerAIProfiles_NoiseScore_Range" CHECK (CAST("NoiseScore" AS NUMERIC) >= 0 AND CAST("NoiseScore" AS NUMERIC) <= 100),
        CONSTRAINT "CK_PlayerAIProfiles_Revision_NonNegative" CHECK ("ProfileRevision" >= 0),
        CONSTRAINT "CK_PlayerAIProfiles_SampleCounts_NonNegative" CHECK ("SurvivalSampleCount" >= 0 AND "NoiseSampleCount" >= 0),
        CONSTRAINT "CK_PlayerAIProfiles_SurvivalScore_Range" CHECK (CAST("SurvivalScore" AS NUMERIC) >= 0 AND CAST("SurvivalScore" AS NUMERIC) <= 100),
        CONSTRAINT "FK_PlayerAIProfiles_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921061308_AddPlayerAIProfileMatchScoreM4027M4038M4039') THEN
    CREATE TABLE "MatchScores" (
        "Id" uuid NOT NULL,
        "MatchId" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "ProfileLineageId" uuid NOT NULL,
        "Dimension" character varying(20) NOT NULL,
        "Score" numeric(9,6) NOT NULL,
        "MatchEndTs" timestamp with time zone NOT NULL,
        "MatchScoreFormulaVersion" character varying(80) NOT NULL,
        "NormalizationConfigVersion" character varying(80) NOT NULL,
        "ProfileNoiseFilterVersion" character varying(80),
        "AlphaConfigVersion" character varying(80) NOT NULL,
        "SourceTelemetrySchemaVersion" character varying(20) NOT NULL,
        "SourceEvidenceFingerprint" character(64) NOT NULL,
        "SemanticFingerprint" character(64) NOT NULL,
        "ContributionStatus" character varying(20) NOT NULL,
        "RetractionReason" character varying(50),
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "RetractedAtUtc" timestamp with time zone,
        CONSTRAINT "PK_MatchScores" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_MatchScores_Score_Range" CHECK (CAST("Score" AS NUMERIC) >= 0 AND CAST("Score" AS NUMERIC) <= 100),
        CONSTRAINT "FK_MatchScores_MatchResultPlayers_MatchId_UserId" FOREIGN KEY ("MatchId", "UserId") REFERENCES "MatchResultPlayers" ("MatchId", "UserId") ON DELETE RESTRICT,
        CONSTRAINT "FK_MatchScores_PlayerAIProfiles_ProfileLineageId" FOREIGN KEY ("ProfileLineageId") REFERENCES "PlayerAIProfiles" ("ProfileLineageId") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921061308_AddPlayerAIProfileMatchScoreM4027M4038M4039') THEN
    CREATE INDEX "IX_MatchScores_MatchId_UserId" ON "MatchScores" ("MatchId", "UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921061308_AddPlayerAIProfileMatchScoreM4027M4038M4039') THEN
    CREATE INDEX "IX_MatchScores_ProfileLineageId" ON "MatchScores" ("ProfileLineageId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921061308_AddPlayerAIProfileMatchScoreM4027M4038M4039') THEN
    CREATE INDEX "IX_MatchScores_ReplayOrder" ON "MatchScores" ("UserId", "ProfileLineageId", "Dimension", "MatchEndTs", "MatchId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921061308_AddPlayerAIProfileMatchScoreM4027M4038M4039') THEN
    CREATE UNIQUE INDEX "UX_MatchScores_ApplyKey" ON "MatchScores" ("UserId", "MatchId", "ProfileLineageId", "Dimension");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921061308_AddPlayerAIProfileMatchScoreM4027M4038M4039') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260921061308_AddPlayerAIProfileMatchScoreM4027M4038M4039', '8.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921063635_AddTeamProfileM4040') THEN
    CREATE TABLE "TeamProfiles" (
        "MatchId" uuid NOT NULL,
        "ProcessingRevision" bigint NOT NULL,
        "ProcessingStatus" character varying(20) NOT NULL,
        "ProcessingReason" character varying(100) NOT NULL,
        "TelemetryCompleteness" character varying(20) NOT NULL,
        "ObjectiveTimeSeconds" numeric(14,6),
        "ObjectiveTimeStatus" character varying(20) NOT NULL,
        "SplitTime" numeric,
        "SplitTimeStatus" character varying(20) NOT NULL,
        "AvgDistance" numeric,
        "AvgDistanceStatus" character varying(20) NOT NULL,
        "ReviveSuccess" numeric,
        "ReviveSuccessStatus" character varying(20) NOT NULL,
        "ResourceEfficiency" numeric,
        "ResourceEfficiencyStatus" character varying(20) NOT NULL,
        "Communication" numeric,
        "CommunicationStatus" character varying(20) NOT NULL,
        "WipeRecovery" numeric,
        "WipeRecoveryStatus" character varying(20) NOT NULL,
        "ObjectiveSpeedScore" numeric(9,6),
        "ObjectiveSpeedStatus" character varying(20) NOT NULL,
        "SurvivalScore" numeric(9,6),
        "SurvivalStatus" character varying(20) NOT NULL,
        "TeamworkScore" numeric,
        "TeamworkStatus" character varying(20) NOT NULL,
        "ResourceEfficiencyScore" numeric,
        "ResourceEfficiencyScoreStatus" character varying(20) NOT NULL,
        "TeamPerformanceScore" numeric,
        "TeamPerformanceStatus" character varying(20) NOT NULL,
        "ProfileFormulaVersion" character varying(80) NOT NULL,
        "TeamPerformanceFormulaVersion" character varying(80),
        "PhaseRegistryVersion" character varying(80),
        "NormalizationConfigVersion" character varying(80),
        "SourceTelemetrySchemaVersion" character varying(20) NOT NULL,
        "SourceFingerprint" character(64) NOT NULL,
        "ProjectionFingerprint" character(64) NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "UpdatedAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_TeamProfiles" PRIMARY KEY ("MatchId"),
        CONSTRAINT "CK_TeamProfiles_Deferred_Null" CHECK ("SplitTime" IS NULL AND "SplitTimeStatus" = 'Deferred' AND "AvgDistance" IS NULL AND "AvgDistanceStatus" = 'Deferred' AND "ReviveSuccess" IS NULL AND "ReviveSuccessStatus" = 'Deferred' AND "ResourceEfficiency" IS NULL AND "ResourceEfficiencyStatus" = 'Deferred' AND "Communication" IS NULL AND "CommunicationStatus" = 'Deferred' AND "WipeRecovery" IS NULL AND "WipeRecoveryStatus" = 'Deferred' AND "TeamworkScore" IS NULL AND "TeamworkStatus" = 'Deferred' AND "ResourceEfficiencyScore" IS NULL AND "ResourceEfficiencyScoreStatus" = 'Deferred'),
        CONSTRAINT "CK_TeamProfiles_ObjectiveSpeed_StatusValue" CHECK (("ObjectiveSpeedStatus" = 'Available' AND "ObjectiveSpeedScore" IS NOT NULL) OR ("ObjectiveSpeedStatus" <> 'Available' AND "ObjectiveSpeedScore" IS NULL)),
        CONSTRAINT "CK_TeamProfiles_ObjectiveTime_NonNegative" CHECK ("ObjectiveTimeSeconds" IS NULL OR CAST("ObjectiveTimeSeconds" AS NUMERIC) >= 0),
        CONSTRAINT "CK_TeamProfiles_ObjectiveTime_StatusValue" CHECK (("ObjectiveTimeStatus" = 'Available' AND "ObjectiveTimeSeconds" IS NOT NULL) OR ("ObjectiveTimeStatus" <> 'Available' AND "ObjectiveTimeSeconds" IS NULL)),
        CONSTRAINT "CK_TeamProfiles_Performance_Incomplete" CHECK ("TeamPerformanceStatus" = 'Incomplete' AND "TeamPerformanceScore" IS NULL),
        CONSTRAINT "CK_TeamProfiles_Revision_NonNegative" CHECK ("ProcessingRevision" >= 0),
        CONSTRAINT "CK_TeamProfiles_Scores_Range" CHECK (("ObjectiveSpeedScore" IS NULL OR (CAST("ObjectiveSpeedScore" AS NUMERIC) >= 0 AND CAST("ObjectiveSpeedScore" AS NUMERIC) <= 100)) AND ("SurvivalScore" IS NULL OR (CAST("SurvivalScore" AS NUMERIC) >= 0 AND CAST("SurvivalScore" AS NUMERIC) <= 100))),
        CONSTRAINT "CK_TeamProfiles_Survival_StatusValue" CHECK (("SurvivalStatus" = 'Available' AND "SurvivalScore" IS NOT NULL) OR ("SurvivalStatus" <> 'Available' AND "SurvivalScore" IS NULL)),
        CONSTRAINT "FK_TeamProfiles_MatchResults_MatchId" FOREIGN KEY ("MatchId") REFERENCES "MatchResults" ("MatchId") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921063635_AddTeamProfileM4040') THEN
    CREATE INDEX "IX_TeamProfiles_ProcessingStatus" ON "TeamProfiles" ("ProcessingStatus");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921063635_AddTeamProfileM4040') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260921063635_AddTeamProfileM4040', '8.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921065243_AddScenarioConfigRegistryM4043M4045') THEN
    CREATE TABLE "ScenarioConfigs" (
        "ScenarioConfigId" character varying(128) NOT NULL,
        "ScenarioConfigVersion" character varying(80) NOT NULL,
        "SchemaVersion" character varying(20) NOT NULL,
        "PolicyVersion" character varying(80) NOT NULL,
        "ConfigSource" character varying(20) NOT NULL,
        "MapId" character varying(128) NOT NULL,
        "MonsterType" character varying(128) NOT NULL,
        "ObjectiveSpawnSetId" character varying(128) NOT NULL,
        "SupportItemBudget" integer NOT NULL,
        "DetectionFillRate" double precision NOT NULL,
        "DetectionDecayRate" double precision NOT NULL,
        "ChaseSpeed" double precision NOT NULL,
        "SearchDuration" double precision NOT NULL,
        "RouteModifier" character varying(128) NOT NULL,
        "EscapeDoorTimerSeconds" double precision NOT NULL,
        "FallbackConfigId" character varying(128) NOT NULL,
        "FallbackConfigVersion" character varying(80) NOT NULL,
        "ContentWhitelistVersion" character varying(80) NOT NULL,
        "UnityCompatibilityVersion" character varying(80) NOT NULL,
        "IsActive" boolean NOT NULL,
        "IsProductionApproved" boolean NOT NULL,
        "IsFixedFallback" boolean NOT NULL,
        "Provenance" character varying(500) NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "UpdatedAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_ScenarioConfigs" PRIMARY KEY ("ScenarioConfigId", "ScenarioConfigVersion"),
        CONSTRAINT "CK_ScenarioConfigs_EscapeTimer_Range" CHECK ("EscapeDoorTimerSeconds" >= 45 AND "EscapeDoorTimerSeconds" <= 60),
        CONSTRAINT "CK_ScenarioConfigs_Fallback_IsFixed" CHECK (NOT "IsFixedFallback" OR "ConfigSource" = 'Fixed'),
        CONSTRAINT "CK_ScenarioConfigs_Numerics_NonNegative" CHECK ("DetectionFillRate" >= 0 AND "DetectionDecayRate" >= 0 AND "ChaseSpeed" >= 0 AND "SearchDuration" >= 0),
        CONSTRAINT "CK_ScenarioConfigs_Source_Allowed" CHECK ("ConfigSource" IN ('Fixed', 'Adaptive')),
        CONSTRAINT "CK_ScenarioConfigs_SupportBudget_NonNegative" CHECK ("SupportItemBudget" >= 0)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921065243_AddScenarioConfigRegistryM4043M4045') THEN
    CREATE TABLE "ScenarioContentDefinitions" (
        "Id" uuid NOT NULL,
        "ContentType" character varying(30) NOT NULL,
        "ContentId" character varying(128) NOT NULL,
        "ContentWhitelistVersion" character varying(80) NOT NULL,
        "UnityCompatibilityVersion" character varying(80) NOT NULL,
        "IsActive" boolean NOT NULL,
        "IsProductionApproved" boolean NOT NULL,
        "Provenance" character varying(500) NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "UpdatedAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_ScenarioContentDefinitions" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_ScenarioContentDefinitions_Type_Allowed" CHECK ("ContentType" IN ('Map', 'Monster', 'ObjectiveSpawnSet', 'RouteModifier'))
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921065243_AddScenarioConfigRegistryM4043M4045') THEN
    CREATE UNIQUE INDEX "IX_ScenarioConfigs_ScenarioConfigVersion" ON "ScenarioConfigs" ("ScenarioConfigVersion");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921065243_AddScenarioConfigRegistryM4043M4045') THEN
    CREATE UNIQUE INDEX "UX_ScenarioConfigs_ProductionFallbackCompatibility" ON "ScenarioConfigs" ("UnityCompatibilityVersion") WHERE "IsActive" AND "IsProductionApproved" AND "IsFixedFallback";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921065243_AddScenarioConfigRegistryM4043M4045') THEN
    CREATE INDEX "IX_ScenarioContentDefinitions_IsActive_IsProductionApproved_Un~" ON "ScenarioContentDefinitions" ("IsActive", "IsProductionApproved", "UnityCompatibilityVersion");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921065243_AddScenarioConfigRegistryM4043M4045') THEN
    CREATE UNIQUE INDEX "UX_ScenarioContent_IdentityVersion" ON "ScenarioContentDefinitions" ("ContentType", "ContentId", "ContentWhitelistVersion", "UnityCompatibilityVersion");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921065243_AddScenarioConfigRegistryM4043M4045') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260921065243_AddScenarioConfigRegistryM4043M4045', '8.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921125415_AddScenarioDecisionsM4051M4049') THEN
    DROP INDEX "IX_ScenarioConfigs_ScenarioConfigVersion";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921125415_AddScenarioDecisionsM4051M4049') THEN
    CREATE TABLE "AdaptiveInputSnapshots" (
        "SnapshotId" uuid NOT NULL,
        "MatchId" uuid NOT NULL,
        "DecisionPoint" character varying(30) NOT NULL,
        "SnapshotContentFingerprint" character(64) NOT NULL,
        "RosterIdentity" character(64) NOT NULL,
        "TeamSize" integer NOT NULL,
        "Validity" character varying(20) NOT NULL,
        "ReasonCodesJson" jsonb NOT NULL,
        "ProfileFormulaSemanticId" character varying(80),
        "SurvivalComparisonKey" character(64),
        "NoiseComparisonKey" character(64),
        "SurvivalAggregationStatus" character varying(20) NOT NULL,
        "NoiseAggregationStatus" character varying(20) NOT NULL,
        "SurvivalMeanObservedScore" numeric(9,6),
        "NoiseMeanObservedScore" numeric(9,6),
        "SurvivalObservedActiveCount" integer NOT NULL,
        "NoiseObservedActiveCount" integer NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_AdaptiveInputSnapshots" PRIMARY KEY ("SnapshotId"),
        CONSTRAINT "CK_AdaptiveSnapshots_Means_Range" CHECK (("SurvivalMeanObservedScore" IS NULL OR (CAST("SurvivalMeanObservedScore" AS NUMERIC) >= 0 AND CAST("SurvivalMeanObservedScore" AS NUMERIC) <= 100)) AND ("NoiseMeanObservedScore" IS NULL OR (CAST("NoiseMeanObservedScore" AS NUMERIC) >= 0 AND CAST("NoiseMeanObservedScore" AS NUMERIC) <= 100))),
        CONSTRAINT "CK_AdaptiveSnapshots_TeamSize_Positive" CHECK ("TeamSize" >= 1 AND "TeamSize" <= 4),
        CONSTRAINT "FK_AdaptiveInputSnapshots_MatchAuthorityBindings_MatchId" FOREIGN KEY ("MatchId") REFERENCES "MatchAuthorityBindings" ("MatchId") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921125415_AddScenarioDecisionsM4051M4049') THEN
    CREATE TABLE "AdaptiveInputSnapshotPlayers" (
        "SnapshotId" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "ProfileAvailable" boolean NOT NULL,
        "ProfileLineageId" uuid,
        "ProfileRevision" bigint,
        "ProfileFormulaVersion" character varying(80),
        "MatchScoreFormulaVersion" character varying(80),
        "NormalizationConfigVersion" character varying(80),
        "ProfileNoiseFilterVersion" character varying(80),
        "AlphaConfigVersion" character varying(80),
        "SurvivalScore" numeric(9,6),
        "SurvivalStatus" character varying(20) NOT NULL,
        "SurvivalSampleCount" integer,
        "SurvivalComparisonKey" character(64),
        "NoiseScore" numeric(9,6),
        "NoiseStatus" character varying(20) NOT NULL,
        "NoiseSampleCount" integer,
        "NoiseComparisonKey" character(64),
        "DeferredDimensionsJson" jsonb NOT NULL,
        "CapturedAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_AdaptiveInputSnapshotPlayers" PRIMARY KEY ("SnapshotId", "UserId"),
        CONSTRAINT "CK_AdaptiveSnapshotPlayers_DeferredCanonical" CHECK ("DeferredDimensionsJson" IS NOT NULL),
        CONSTRAINT "CK_AdaptiveSnapshotPlayers_Scores_Range" CHECK (("SurvivalScore" IS NULL OR (CAST("SurvivalScore" AS NUMERIC) >= 0 AND CAST("SurvivalScore" AS NUMERIC) <= 100)) AND ("NoiseScore" IS NULL OR (CAST("NoiseScore" AS NUMERIC) >= 0 AND CAST("NoiseScore" AS NUMERIC) <= 100))),
        CONSTRAINT "FK_AdaptiveInputSnapshotPlayers_AdaptiveInputSnapshots_Snapsho~" FOREIGN KEY ("SnapshotId") REFERENCES "AdaptiveInputSnapshots" ("SnapshotId") ON DELETE CASCADE,
        CONSTRAINT "FK_AdaptiveInputSnapshotPlayers_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921125415_AddScenarioDecisionsM4051M4049') THEN
    CREATE TABLE "ScenarioDecisions" (
        "DecisionId" uuid NOT NULL,
        "MatchId" uuid NOT NULL,
        "HostUserId" uuid NOT NULL,
        "SnapshotId" uuid NOT NULL,
        "ResolutionMode" character varying(20) NOT NULL,
        "DecisionPoint" character varying(30) NOT NULL,
        "ExperimentCondition" character varying(80),
        "RequestFingerprint" character(64) NOT NULL,
        "DecisionSemanticFingerprint" character(64) NOT NULL,
        "ScenarioConfigId" character varying(128) NOT NULL,
        "ScenarioConfigVersion" character varying(80) NOT NULL,
        "ScenarioConfigFingerprint" character(64) NOT NULL,
        "PolicyVersion" character varying(80) NOT NULL,
        "PolicyConfigVersion" character varying(80),
        "EvidencePolicyVersion" character varying(80),
        "ParameterRegistryVersion" character varying(80),
        "ContentWhitelistVersion" character varying(80) NOT NULL,
        "FallbackConfigId" character varying(128) NOT NULL,
        "FallbackConfigVersion" character varying(80) NOT NULL,
        "UsedFixedFallback" boolean NOT NULL,
        "FallbackReasonCode" character varying(50),
        "CandidateValidationStatus" character varying(20),
        "ResolutionResult" character varying(30) NOT NULL,
        "Status" character varying(20) NOT NULL,
        "IsCurrent" boolean NOT NULL,
        "CommittedAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_ScenarioDecisions" PRIMARY KEY ("DecisionId"),
        CONSTRAINT "FK_ScenarioDecisions_AdaptiveInputSnapshots_SnapshotId" FOREIGN KEY ("SnapshotId") REFERENCES "AdaptiveInputSnapshots" ("SnapshotId") ON DELETE RESTRICT,
        CONSTRAINT "FK_ScenarioDecisions_MatchAuthorityBindings_MatchId" FOREIGN KEY ("MatchId") REFERENCES "MatchAuthorityBindings" ("MatchId") ON DELETE RESTRICT,
        CONSTRAINT "FK_ScenarioDecisions_ScenarioConfigs_ScenarioConfigId_Scenario~" FOREIGN KEY ("ScenarioConfigId", "ScenarioConfigVersion") REFERENCES "ScenarioConfigs" ("ScenarioConfigId", "ScenarioConfigVersion") ON DELETE RESTRICT,
        CONSTRAINT "FK_ScenarioDecisions_Users_HostUserId" FOREIGN KEY ("HostUserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921125415_AddScenarioDecisionsM4051M4049') THEN
    CREATE TABLE "ScenarioApplyReceipts" (
        "DecisionId" uuid NOT NULL,
        "MatchId" uuid NOT NULL,
        "ReportedByUserId" uuid NOT NULL,
        "AppliedScenarioConfigId" character varying(128) NOT NULL,
        "AppliedScenarioConfigVersion" character varying(80) NOT NULL,
        "AppliedScenarioConfigFingerprint" character(64) NOT NULL,
        "AppliedAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_ScenarioApplyReceipts" PRIMARY KEY ("DecisionId"),
        CONSTRAINT "FK_ScenarioApplyReceipts_ScenarioDecisions_DecisionId" FOREIGN KEY ("DecisionId") REFERENCES "ScenarioDecisions" ("DecisionId") ON DELETE RESTRICT,
        CONSTRAINT "FK_ScenarioApplyReceipts_Users_ReportedByUserId" FOREIGN KEY ("ReportedByUserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921125415_AddScenarioDecisionsM4051M4049') THEN
    CREATE INDEX "IX_ScenarioConfigs_ScenarioConfigVersion" ON "ScenarioConfigs" ("ScenarioConfigVersion");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921125415_AddScenarioDecisionsM4051M4049') THEN
    CREATE INDEX "IX_AdaptiveInputSnapshotPlayers_UserId" ON "AdaptiveInputSnapshotPlayers" ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921125415_AddScenarioDecisionsM4051M4049') THEN
    CREATE INDEX "IX_AdaptiveInputSnapshots_MatchId_CreatedAtUtc" ON "AdaptiveInputSnapshots" ("MatchId", "CreatedAtUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921125415_AddScenarioDecisionsM4051M4049') THEN
    CREATE UNIQUE INDEX "IX_ScenarioApplyReceipts_MatchId" ON "ScenarioApplyReceipts" ("MatchId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921125415_AddScenarioDecisionsM4051M4049') THEN
    CREATE INDEX "IX_ScenarioApplyReceipts_ReportedByUserId" ON "ScenarioApplyReceipts" ("ReportedByUserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921125415_AddScenarioDecisionsM4051M4049') THEN
    CREATE INDEX "IX_ScenarioDecisions_HostUserId" ON "ScenarioDecisions" ("HostUserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921125415_AddScenarioDecisionsM4051M4049') THEN
    CREATE INDEX "IX_ScenarioDecisions_ScenarioConfigId_ScenarioConfigVersion" ON "ScenarioDecisions" ("ScenarioConfigId", "ScenarioConfigVersion");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921125415_AddScenarioDecisionsM4051M4049') THEN
    CREATE UNIQUE INDEX "IX_ScenarioDecisions_SnapshotId" ON "ScenarioDecisions" ("SnapshotId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921125415_AddScenarioDecisionsM4051M4049') THEN
    CREATE UNIQUE INDEX "UX_ScenarioDecisions_CurrentMatch" ON "ScenarioDecisions" ("MatchId") WHERE "IsCurrent";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921125415_AddScenarioDecisionsM4051M4049') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260921125415_AddScenarioDecisionsM4051M4049', '8.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921140000_AddPaymentOrdersM4054') THEN
    CREATE TABLE "PaymentOrders" (
        "PaymentOrderId" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "Provider" character varying(50) NOT NULL,
        "ProviderOrderId" character varying(200),
        "Purpose" character varying(100) NOT NULL,
        "ProductReference" character varying(128) NOT NULL,
        "Amount" numeric(18,2) NOT NULL,
        "Currency" character(3) NOT NULL,
        "Status" character varying(30) NOT NULL,
        "IdempotencyKey" character varying(100) NOT NULL,
        "RequestFingerprint" character(64) NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "UpdatedAtUtc" timestamp with time zone NOT NULL,
        "ExpiresAtUtc" timestamp with time zone,
        "PaidAtUtc" timestamp with time zone,
        "FulfilledAtUtc" timestamp with time zone,
        "ProviderTransactionId" character varying(200),
        "FulfillmentReference" character varying(200),
        CONSTRAINT "PK_PaymentOrders" PRIMARY KEY ("PaymentOrderId"),
        CONSTRAINT "CK_PaymentOrders_Amount_Positive" CHECK ("Amount" > 0),
        CONSTRAINT "CK_PaymentOrders_Currency_Format" CHECK (length("Currency") = 3 AND "Currency" = upper("Currency")),
        CONSTRAINT "CK_PaymentOrders_State_Data" CHECK (("Status" <> 'PENDING_PAYMENT' OR "ProviderOrderId" IS NOT NULL) AND ("Status" NOT IN ('PAID', 'FULFILLED') OR ("ProviderOrderId" IS NOT NULL AND "ProviderTransactionId" IS NOT NULL AND "PaidAtUtc" IS NOT NULL)) AND ("Status" <> 'FULFILLED' OR ("FulfillmentReference" IS NOT NULL AND "FulfilledAtUtc" IS NOT NULL))),
        CONSTRAINT "CK_PaymentOrders_Status_Allowed" CHECK ("Status" IN ('CREATED', 'PENDING_PAYMENT', 'PAID', 'FULFILLED', 'FAILED', 'CANCELLED', 'EXPIRED')),
        CONSTRAINT "CK_PaymentOrders_Timestamps" CHECK ("UpdatedAtUtc" >= "CreatedAtUtc" AND ("ExpiresAtUtc" IS NULL OR "ExpiresAtUtc" > "CreatedAtUtc") AND ("PaidAtUtc" IS NULL OR "PaidAtUtc" >= "CreatedAtUtc") AND ("FulfilledAtUtc" IS NULL OR ("PaidAtUtc" IS NOT NULL AND "FulfilledAtUtc" >= "PaidAtUtc"))),
        CONSTRAINT "FK_PaymentOrders_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921140000_AddPaymentOrdersM4054') THEN
    CREATE UNIQUE INDEX "IX_PaymentOrders_FulfillmentReference" ON "PaymentOrders" ("FulfillmentReference") WHERE "FulfillmentReference" IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921140000_AddPaymentOrdersM4054') THEN
    CREATE UNIQUE INDEX "IX_PaymentOrders_Provider_ProviderOrderId" ON "PaymentOrders" ("Provider", "ProviderOrderId") WHERE "ProviderOrderId" IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921140000_AddPaymentOrdersM4054') THEN
    CREATE UNIQUE INDEX "IX_PaymentOrders_Provider_ProviderTransactionId" ON "PaymentOrders" ("Provider", "ProviderTransactionId") WHERE "ProviderTransactionId" IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921140000_AddPaymentOrdersM4054') THEN
    CREATE INDEX "IX_PaymentOrders_UserId_CreatedAtUtc" ON "PaymentOrders" ("UserId", "CreatedAtUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921140000_AddPaymentOrdersM4054') THEN
    CREATE UNIQUE INDEX "IX_PaymentOrders_UserId_IdempotencyKey" ON "PaymentOrders" ("UserId", "IdempotencyKey");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921140000_AddPaymentOrdersM4054') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260921140000_AddPaymentOrdersM4054', '8.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921150000_AddPayOSCheckoutWebhookFulfillmentM4055M4056M4057') THEN
    ALTER TABLE "WalletTransactions" DROP CONSTRAINT "CK_WalletTransactions_Amount_ByType";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921150000_AddPayOSCheckoutWebhookFulfillmentM4055M4056M4057') THEN
    ALTER TABLE "WalletTransactions" ADD CONSTRAINT "CK_WalletTransactions_Amount_ByType" CHECK (("Type" IN ('MATCH_REWARD', 'PAYMENT_FULFILLMENT') AND "Amount" >= 0) OR ("Type" = 'PURCHASE' AND "Amount" <= 0));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921150000_AddPayOSCheckoutWebhookFulfillmentM4055M4056M4057') THEN
    CREATE TABLE "PaymentCheckouts" (
        "CheckoutSequenceId" bigint GENERATED BY DEFAULT AS IDENTITY,
        "PaymentOrderId" uuid NOT NULL,
        "Provider" character varying(50) NOT NULL,
        "ProviderOrderId" character varying(200) NOT NULL,
        "ProviderPaymentLinkId" character varying(200),
        "CheckoutUrl" character varying(2000),
        "Status" character varying(20) NOT NULL,
        "ReservedAtUtc" timestamp with time zone NOT NULL,
        "ReadyAtUtc" timestamp with time zone,
        CONSTRAINT "PK_PaymentCheckouts" PRIMARY KEY ("CheckoutSequenceId"),
        CONSTRAINT "CK_PaymentCheckouts_Ready_Data" CHECK ("Status" <> 'READY' OR ("ProviderPaymentLinkId" IS NOT NULL AND "CheckoutUrl" IS NOT NULL AND "ReadyAtUtc" IS NOT NULL)),
        CONSTRAINT "CK_PaymentCheckouts_Status_Allowed" CHECK ("Status" IN ('RESERVED', 'READY')),
        CONSTRAINT "FK_PaymentCheckouts_PaymentOrders_PaymentOrderId" FOREIGN KEY ("PaymentOrderId") REFERENCES "PaymentOrders" ("PaymentOrderId") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921150000_AddPayOSCheckoutWebhookFulfillmentM4055M4056M4057') THEN
    CREATE TABLE "PaymentProviderEvents" (
        "PaymentProviderEventId" uuid NOT NULL,
        "Provider" character varying(50) NOT NULL,
        "ProviderEventId" character varying(200) NOT NULL,
        "PaymentOrderId" uuid,
        "ProviderOrderId" character varying(200) NOT NULL,
        "Amount" numeric(18,2) NOT NULL,
        "Currency" character(3),
        "NormalizedStatus" character varying(30) NOT NULL,
        "SemanticFingerprint" character(64) NOT NULL,
        "VerificationStatus" character varying(20) NOT NULL,
        "ReceivedAtUtc" timestamp with time zone NOT NULL,
        "ProcessedAtUtc" timestamp with time zone,
        "ProcessingOutcome" character varying(40) NOT NULL,
        CONSTRAINT "PK_PaymentProviderEvents" PRIMARY KEY ("PaymentProviderEventId"),
        CONSTRAINT "CK_PaymentProviderEvents_Amount_Positive" CHECK ("Amount" > 0),
        CONSTRAINT "CK_PaymentProviderEvents_Verified" CHECK ("VerificationStatus" = 'VERIFIED'),
        CONSTRAINT "FK_PaymentProviderEvents_PaymentOrders_PaymentOrderId" FOREIGN KEY ("PaymentOrderId") REFERENCES "PaymentOrders" ("PaymentOrderId") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921150000_AddPayOSCheckoutWebhookFulfillmentM4055M4056M4057') THEN
    CREATE TABLE "PaymentFulfillments" (
        "PaymentOrderId" uuid NOT NULL,
        "FulfillmentReference" character varying(200) NOT NULL,
        "Kind" character varying(30) NOT NULL,
        "WalletTransactionId" uuid,
        "InventoryItemId" uuid,
        "CompletedAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_PaymentFulfillments" PRIMARY KEY ("PaymentOrderId"),
        CONSTRAINT "CK_PaymentFulfillments_Target" CHECK (("Kind" = 'WALLET_CREDIT' AND "WalletTransactionId" IS NOT NULL AND "InventoryItemId" IS NULL) OR ("Kind" = 'INVENTORY_ITEM' AND "WalletTransactionId" IS NULL AND "InventoryItemId" IS NOT NULL)),
        CONSTRAINT "FK_PaymentFulfillments_InventoryItems_InventoryItemId" FOREIGN KEY ("InventoryItemId") REFERENCES "InventoryItems" ("InventoryItemId") ON DELETE RESTRICT,
        CONSTRAINT "FK_PaymentFulfillments_PaymentOrders_PaymentOrderId" FOREIGN KEY ("PaymentOrderId") REFERENCES "PaymentOrders" ("PaymentOrderId") ON DELETE RESTRICT,
        CONSTRAINT "FK_PaymentFulfillments_WalletTransactions_WalletTransactionId" FOREIGN KEY ("WalletTransactionId") REFERENCES "WalletTransactions" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921150000_AddPayOSCheckoutWebhookFulfillmentM4055M4056M4057') THEN
    CREATE UNIQUE INDEX "IX_PaymentCheckouts_PaymentOrderId" ON "PaymentCheckouts" ("PaymentOrderId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921150000_AddPayOSCheckoutWebhookFulfillmentM4055M4056M4057') THEN
    CREATE UNIQUE INDEX "IX_PaymentCheckouts_Provider_ProviderOrderId" ON "PaymentCheckouts" ("Provider", "ProviderOrderId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921150000_AddPayOSCheckoutWebhookFulfillmentM4055M4056M4057') THEN
    CREATE INDEX "IX_PaymentProviderEvents_PaymentOrderId" ON "PaymentProviderEvents" ("PaymentOrderId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921150000_AddPayOSCheckoutWebhookFulfillmentM4055M4056M4057') THEN
    CREATE UNIQUE INDEX "IX_PaymentProviderEvents_Provider_ProviderEventId" ON "PaymentProviderEvents" ("Provider", "ProviderEventId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921150000_AddPayOSCheckoutWebhookFulfillmentM4055M4056M4057') THEN
    CREATE UNIQUE INDEX "IX_PaymentFulfillments_FulfillmentReference" ON "PaymentFulfillments" ("FulfillmentReference");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921150000_AddPayOSCheckoutWebhookFulfillmentM4055M4056M4057') THEN
    CREATE UNIQUE INDEX "IX_PaymentFulfillments_InventoryItemId" ON "PaymentFulfillments" ("InventoryItemId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921150000_AddPayOSCheckoutWebhookFulfillmentM4055M4056M4057') THEN
    CREATE UNIQUE INDEX "IX_PaymentFulfillments_WalletTransactionId" ON "PaymentFulfillments" ("WalletTransactionId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921150000_AddPayOSCheckoutWebhookFulfillmentM4055M4056M4057') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260921150000_AddPayOSCheckoutWebhookFulfillmentM4055M4056M4057', '8.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260922090000_AddPlayerLoadoutM4052') THEN
    ALTER TABLE "InventoryItems" ADD CONSTRAINT "AK_InventoryItems_UserId_InventoryItemId" UNIQUE ("UserId", "InventoryItemId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260922090000_AddPlayerLoadoutM4052') THEN
    CREATE TABLE "PlayerLoadoutItems" (
        "UserId" uuid NOT NULL,
        "SlotId" character varying(50) NOT NULL,
        "InventoryItemId" uuid NOT NULL,
        "EquippedAtUtc" timestamp with time zone NOT NULL,
        "UpdatedAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_PlayerLoadoutItems" PRIMARY KEY ("UserId", "SlotId"),
        CONSTRAINT "CK_PlayerLoadoutItems_Timestamps" CHECK ("UpdatedAtUtc" >= "EquippedAtUtc"),
        CONSTRAINT "FK_PlayerLoadoutItems_InventoryItems_UserId_InventoryItemId" FOREIGN KEY ("UserId", "InventoryItemId") REFERENCES "InventoryItems" ("UserId", "InventoryItemId") ON DELETE RESTRICT,
        CONSTRAINT "FK_PlayerLoadoutItems_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260922090000_AddPlayerLoadoutM4052') THEN
    CREATE UNIQUE INDEX "IX_PlayerLoadoutItems_UserId_InventoryItemId" ON "PlayerLoadoutItems" ("UserId", "InventoryItemId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260922090000_AddPlayerLoadoutM4052') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260922090000_AddPlayerLoadoutM4052', '8.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260922110000_AddAdminQueryIndexes') THEN
    CREATE INDEX "IX_Admin_PaymentOrders_Created" ON "PaymentOrders" ("CreatedAtUtc" DESC, "PaymentOrderId" DESC);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260922110000_AddAdminQueryIndexes') THEN
    CREATE INDEX "IX_Admin_PaymentOrders_Status_Created" ON "PaymentOrders" ("Status", "CreatedAtUtc" DESC, "PaymentOrderId" DESC);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260922110000_AddAdminQueryIndexes') THEN
    CREATE INDEX "IX_Admin_PaymentOrders_Provider_Created" ON "PaymentOrders" ("Provider", "CreatedAtUtc" DESC, "PaymentOrderId" DESC);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260922110000_AddAdminQueryIndexes') THEN
    CREATE INDEX "IX_Admin_PaymentOrders_Product_Created" ON "PaymentOrders" ("ProductReference", "CreatedAtUtc" DESC, "PaymentOrderId" DESC);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260922110000_AddAdminQueryIndexes') THEN
    CREATE INDEX "IX_Admin_WalletTransactions_Created" ON "WalletTransactions" ("CreatedAtUtc" DESC, "Id" DESC);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260922110000_AddAdminQueryIndexes') THEN
    CREATE INDEX "IX_Admin_WalletTransactions_Wallet_Created" ON "WalletTransactions" ("WalletId", "CreatedAtUtc" DESC, "Id" DESC);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260922110000_AddAdminQueryIndexes') THEN
    CREATE INDEX "IX_Admin_WalletTransactions_Type_Created" ON "WalletTransactions" ("Type", "CreatedAtUtc" DESC, "Id" DESC);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260922110000_AddAdminQueryIndexes') THEN
    CREATE INDEX "IX_Admin_Purchases_Created" ON "PurchaseTransactions" ("CreatedAtUtc" DESC, "PurchaseId" DESC);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260922110000_AddAdminQueryIndexes') THEN
    CREATE INDEX "IX_Admin_Purchases_User_Created" ON "PurchaseTransactions" ("UserId", "CreatedAtUtc" DESC, "PurchaseId" DESC);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260922110000_AddAdminQueryIndexes') THEN
    CREATE INDEX "IX_Admin_Purchases_Item_Created" ON "PurchaseTransactions" ("ShopItemId", "CreatedAtUtc" DESC, "PurchaseId" DESC);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260922110000_AddAdminQueryIndexes') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260922110000_AddAdminQueryIndexes', '8.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261002093340_AddRefreshSessions') THEN
    CREATE TABLE "RefreshSessions" (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "FamilyId" uuid NOT NULL,
        "TokenHash" character(64) NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "ExpiresAtUtc" timestamp with time zone NOT NULL,
        "LastUsedAtUtc" timestamp with time zone,
        "RevokedAtUtc" timestamp with time zone,
        "ReplacedBySessionId" uuid,
        "ConcurrencyToken" uuid NOT NULL,
        CONSTRAINT "PK_RefreshSessions" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_RefreshSessions_ExpiryAfterCreated" CHECK ("ExpiresAtUtc" > "CreatedAtUtc"),
        CONSTRAINT "FK_RefreshSessions_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261002093340_AddRefreshSessions') THEN
    CREATE INDEX "IX_RefreshSessions_FamilyId" ON "RefreshSessions" ("FamilyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261002093340_AddRefreshSessions') THEN
    CREATE UNIQUE INDEX "IX_RefreshSessions_TokenHash" ON "RefreshSessions" ("TokenHash");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261002093340_AddRefreshSessions') THEN
    CREATE INDEX "IX_RefreshSessions_UserId" ON "RefreshSessions" ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261002093340_AddRefreshSessions') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261002093340_AddRefreshSessions', '8.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261002123000_ExtendMatchDurationTo45Minutes') THEN
    ALTER TABLE "MatchResults" DROP CONSTRAINT "CK_MatchResults_DurationSeconds_Range";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261002123000_ExtendMatchDurationTo45Minutes') THEN
    ALTER TABLE "MatchResults" ADD CONSTRAINT "CK_MatchResults_DurationSeconds_Range" CHECK ("DurationSeconds" >= 60 AND "DurationSeconds" <= 2760);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261002123000_ExtendMatchDurationTo45Minutes') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261002123000_ExtendMatchDurationTo45Minutes', '8.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261008031522_AddScenarioAdaptivePlansV2') THEN
    CREATE TABLE "ScenarioAdaptivePlansV2" (
        "DecisionId" uuid NOT NULL,
        "MatchId" uuid NOT NULL,
        "HostUserId" uuid NOT NULL,
        "PhaseOrdinal" integer NOT NULL,
        "DecisionPoint" character varying(30) NOT NULL,
        "PolicyVersion" character varying(80) NOT NULL,
        "BaselineVersion" character varying(80) NOT NULL,
        "PreviousPlanFingerprint" character varying(64) NOT NULL,
        "ResultingPlanFingerprint" character varying(64) NOT NULL,
        "ChangedKey" character varying(80) NOT NULL,
        "PreviousValue" double precision NOT NULL,
        "AppliedValue" double precision NOT NULL,
        "AdaptationIntent" character varying(30) NOT NULL,
        "DecisionReason" character varying(100) NOT NULL,
        "SnapshotId" uuid NOT NULL,
        "SnapshotFingerprint" character varying(64) NOT NULL,
        "EvidenceFingerprint" character varying(64) NOT NULL,
        "RosterIdentity" character varying(64) NOT NULL,
        "PlanValuesJson" jsonb NOT NULL,
        "CommitStatus" character varying(20) NOT NULL,
        "ApplyStatus" character varying(20) NOT NULL,
        "CommittedAtUtc" timestamp with time zone,
        "AppliedAtUtc" timestamp with time zone,
        CONSTRAINT "PK_ScenarioAdaptivePlansV2" PRIMARY KEY ("DecisionId"),
        CONSTRAINT "FK_ScenarioAdaptivePlansV2_AdaptiveInputSnapshots_SnapshotId" FOREIGN KEY ("SnapshotId") REFERENCES "AdaptiveInputSnapshots" ("SnapshotId") ON DELETE RESTRICT,
        CONSTRAINT "FK_ScenarioAdaptivePlansV2_MatchAuthorityBindings_MatchId" FOREIGN KEY ("MatchId") REFERENCES "MatchAuthorityBindings" ("MatchId") ON DELETE RESTRICT,
        CONSTRAINT "FK_ScenarioAdaptivePlansV2_Users_HostUserId" FOREIGN KEY ("HostUserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261008031522_AddScenarioAdaptivePlansV2') THEN
    CREATE INDEX "IX_ScenarioAdaptivePlansV2_HostUserId" ON "ScenarioAdaptivePlansV2" ("HostUserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261008031522_AddScenarioAdaptivePlansV2') THEN
    CREATE UNIQUE INDEX "IX_ScenarioAdaptivePlansV2_MatchId_PhaseOrdinal" ON "ScenarioAdaptivePlansV2" ("MatchId", "PhaseOrdinal");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261008031522_AddScenarioAdaptivePlansV2') THEN
    CREATE INDEX "IX_ScenarioAdaptivePlansV2_SnapshotId" ON "ScenarioAdaptivePlansV2" ("SnapshotId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261008031522_AddScenarioAdaptivePlansV2') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261008031522_AddScenarioAdaptivePlansV2', '8.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261008091001_AddExpandedPlayerAIDimensions') THEN
    ALTER TABLE "PlayerAIProfiles" DROP CONSTRAINT "CK_PlayerAIProfiles_Deferred_Null";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261008091001_AddExpandedPlayerAIDimensions') THEN
    ALTER TABLE "PlayerAIProfiles" ALTER COLUMN "ToolUsageScore" TYPE numeric(9,6);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261008091001_AddExpandedPlayerAIDimensions') THEN
    ALTER TABLE "PlayerAIProfiles" ALTER COLUMN "ObjectiveScore" TYPE numeric(9,6);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261008091001_AddExpandedPlayerAIDimensions') THEN
    ALTER TABLE "PlayerAIProfiles" ADD "ObjectiveSampleCount" integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261008091001_AddExpandedPlayerAIDimensions') THEN
    ALTER TABLE "PlayerAIProfiles" ADD "ObjectiveStatus" character varying(20) NOT NULL DEFAULT 'ColdStart';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261008091001_AddExpandedPlayerAIDimensions') THEN
    ALTER TABLE "PlayerAIProfiles" ADD "ToolUsageNormalizationVersion" character varying(80);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261008091001_AddExpandedPlayerAIDimensions') THEN
    ALTER TABLE "PlayerAIProfiles" ADD "ToolUsageSampleCount" integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261008091001_AddExpandedPlayerAIDimensions') THEN
    ALTER TABLE "PlayerAIProfiles" ADD "ToolUsageStatus" character varying(20) NOT NULL DEFAULT 'ColdStart';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261008091001_AddExpandedPlayerAIDimensions') THEN
    ALTER TABLE "AdaptiveInputSnapshots" ADD "FingerprintVersion" character varying(10) NOT NULL DEFAULT 'V1';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261008091001_AddExpandedPlayerAIDimensions') THEN
    ALTER TABLE "AdaptiveInputSnapshots" ADD "ObjectiveAggregationStatus" character varying(20) NOT NULL DEFAULT 'UNAVAILABLE';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261008091001_AddExpandedPlayerAIDimensions') THEN
    ALTER TABLE "AdaptiveInputSnapshots" ADD "ObjectiveComparisonKey" character(64);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261008091001_AddExpandedPlayerAIDimensions') THEN
    ALTER TABLE "AdaptiveInputSnapshots" ADD "ObjectiveMeanObservedScore" numeric(9,6);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261008091001_AddExpandedPlayerAIDimensions') THEN
    ALTER TABLE "AdaptiveInputSnapshots" ADD "ObjectiveObservedActiveCount" integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261008091001_AddExpandedPlayerAIDimensions') THEN
    ALTER TABLE "AdaptiveInputSnapshots" ADD "ToolUsageAggregationStatus" character varying(20) NOT NULL DEFAULT 'UNAVAILABLE';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261008091001_AddExpandedPlayerAIDimensions') THEN
    ALTER TABLE "AdaptiveInputSnapshots" ADD "ToolUsageComparisonKey" character(64);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261008091001_AddExpandedPlayerAIDimensions') THEN
    ALTER TABLE "AdaptiveInputSnapshots" ADD "ToolUsageMeanObservedScore" numeric(9,6);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261008091001_AddExpandedPlayerAIDimensions') THEN
    ALTER TABLE "AdaptiveInputSnapshots" ADD "ToolUsageObservedActiveCount" integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261008091001_AddExpandedPlayerAIDimensions') THEN
    ALTER TABLE "PlayerAIProfiles" ADD CONSTRAINT "CK_PlayerAIProfiles_Deferred_Null" CHECK ("TeamworkScore" IS NULL AND "ExplorationScore" IS NULL AND "NavigationScore" IS NULL AND "RiskScore" IS NULL AND "ReviveScore" IS NULL);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261008091001_AddExpandedPlayerAIDimensions') THEN
    ALTER TABLE "PlayerAIProfiles" ADD CONSTRAINT "CK_PlayerAIProfiles_ExpandedSamples_NonNegative" CHECK ("ObjectiveSampleCount" >= 0 AND "ToolUsageSampleCount" >= 0);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261008091001_AddExpandedPlayerAIDimensions') THEN
    ALTER TABLE "PlayerAIProfiles" ADD CONSTRAINT "CK_PlayerAIProfiles_ExpandedScores_Range" CHECK (("ObjectiveScore" IS NULL OR (CAST("ObjectiveScore" AS NUMERIC) >= 0 AND CAST("ObjectiveScore" AS NUMERIC) <= 100)) AND ("ToolUsageScore" IS NULL OR (CAST("ToolUsageScore" AS NUMERIC) >= 0 AND CAST("ToolUsageScore" AS NUMERIC) <= 100)));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261008091001_AddExpandedPlayerAIDimensions') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261008091001_AddExpandedPlayerAIDimensions', '8.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261009100000_AddMatchProfileProcessingJobs') THEN
    CREATE TABLE "MatchProfileProcessingJobs" (
        "MatchId" uuid NOT NULL,
        "Status" character varying(20) NOT NULL,
        "Attempts" integer NOT NULL,
        "NextAttemptAtUtc" timestamp with time zone NOT NULL,
        "LeaseExpiresAtUtc" timestamp with time zone,
        "LastError" character varying(200) NOT NULL,
        CONSTRAINT "PK_MatchProfileProcessingJobs" PRIMARY KEY ("MatchId")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261009100000_AddMatchProfileProcessingJobs') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261009100000_AddMatchProfileProcessingJobs', '8.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261009134521_AddAdminGrantWalletTransaction') THEN
    ALTER TABLE "WalletTransactions" DROP CONSTRAINT "CK_WalletTransactions_Amount_ByType";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261009134521_AddAdminGrantWalletTransaction') THEN
    ALTER TABLE "WalletTransactions" ADD CONSTRAINT "CK_WalletTransactions_Amount_ByType" CHECK (("Type" IN ('MATCH_REWARD', 'PAYMENT_FULFILLMENT', 'ADMIN_GRANT') AND "Amount" >= 0) OR ("Type" = 'PURCHASE' AND "Amount" <= 0));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261009134521_AddAdminGrantWalletTransaction') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261009134521_AddAdminGrantWalletTransaction', '8.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261011053603_AddAEDPhase8DirectorApprovals') THEN
    CREATE TABLE "AEDDirectorDecisionsV1" (
        "DecisionId" uuid NOT NULL,
        "MatchId" uuid NOT NULL,
        "HostUserId" uuid NOT NULL,
        "CompletedPhaseOrdinal" integer NOT NULL,
        "DirectorKind" character varying(30) NOT NULL,
        "PolicyVersion" character varying(80) NOT NULL,
        "RosterIdentity" character varying(64) NOT NULL,
        "SnapshotFingerprint" character varying(64) NOT NULL,
        "EvidenceFingerprint" character varying(64) NOT NULL,
        "SourceFingerprint" character varying(64) NOT NULL,
        "PayloadFingerprint" character varying(64) NOT NULL,
        "PayloadJson" jsonb NOT NULL,
        "ApprovalScope" character varying(20) NOT NULL,
        "Status" character varying(20) NOT NULL,
        "ReasonCode" character varying(100) NOT NULL,
        "ExpectedRevision" bigint NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "ApprovedAtUtc" timestamp with time zone,
        "AppliedAtUtc" timestamp with time zone,
        "AbortedAtUtc" timestamp with time zone,
        CONSTRAINT "PK_AEDDirectorDecisionsV1" PRIMARY KEY ("DecisionId")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261011053603_AddAEDPhase8DirectorApprovals') THEN
    CREATE TABLE "AEDPhase8VerifiedSourcesV1" (
        "Id" uuid NOT NULL,
        "MatchId" uuid NOT NULL,
        "CompletedPhaseOrdinal" integer NOT NULL,
        "SourceVersion" character varying(80) NOT NULL,
        "EvidenceFingerprint" character varying(64) NOT NULL,
        "SourceFingerprint" character varying(64) NOT NULL,
        "RosterIdentity" character varying(64) NOT NULL,
        "CanonicalVerified" boolean NOT NULL,
        "HistoricalSkillVerified" boolean NOT NULL,
        "PressureVerified" boolean NOT NULL,
        "SupplyLedgerVerified" boolean NOT NULL,
        "MinionPopulationVerified" boolean NOT NULL,
        "ReasonCode" character varying(100) NOT NULL,
        "VerifiedContentJson" jsonb NOT NULL,
        "VerifiedAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_AEDPhase8VerifiedSourcesV1" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261011053603_AddAEDPhase8DirectorApprovals') THEN
    CREATE UNIQUE INDEX "IX_AEDDirectorDecisionsV1_MatchId_CompletedPhaseOrdinal_Direct~" ON "AEDDirectorDecisionsV1" ("MatchId", "CompletedPhaseOrdinal", "DirectorKind");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261011053603_AddAEDPhase8DirectorApprovals') THEN
    CREATE INDEX "IX_AEDDirectorDecisionsV1_MatchId_DirectorKind_Status" ON "AEDDirectorDecisionsV1" ("MatchId", "DirectorKind", "Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261011053603_AddAEDPhase8DirectorApprovals') THEN
    CREATE UNIQUE INDEX "IX_AEDPhase8VerifiedSourcesV1_MatchId_CompletedPhaseOrdinal_So~" ON "AEDPhase8VerifiedSourcesV1" ("MatchId", "CompletedPhaseOrdinal", "SourceVersion");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261011053603_AddAEDPhase8DirectorApprovals') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261011053603_AddAEDPhase8DirectorApprovals', '8.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014102100_AddAEDV02PilotLedger') THEN
    CREATE TABLE "AEDV02Decisions" (
        "DecisionId" uuid NOT NULL,
        "MatchId" uuid NOT NULL,
        "HostUserId" uuid NOT NULL,
        "PhaseOrdinal" integer NOT NULL,
        "DecisionPoint" character varying(24) NOT NULL,
        "TargetPhaseName" text NOT NULL,
        "WindowSequence" integer NOT NULL,
        "WindowId" character varying(64) NOT NULL,
        "PlanRevision" integer NOT NULL,
        "ExpectedPlanRevision" integer NOT NULL,
        "PreviousPlanFingerprint" text NOT NULL,
        "PlanFingerprint" text NOT NULL,
        "RosterIdentity" text NOT NULL,
        "SnapshotFingerprint" text NOT NULL,
        "SnapshotId" uuid NOT NULL,
        "SourceFingerprint" text NOT NULL,
        "SceneManifestFingerprint" text NOT NULL,
        "RequestFingerprint" text NOT NULL,
        "PolicyVersion" text NOT NULL,
        "CatalogVersion" text NOT NULL,
        "PlanValuesJson" text NOT NULL,
        "InitialOptionalBudget" integer NOT NULL,
        "ChangedKey" text NOT NULL,
        "ReasonCode" text NOT NULL,
        "ApprovalStatus" text NOT NULL,
        "ApplyStatus" text NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "ExpiresAtUtc" timestamp with time zone NOT NULL,
        "ReceiptId" uuid,
        "EffectEventId" uuid,
        "AppliedTick" bigint,
        "AppliedAtUtc" timestamp with time zone,
        CONSTRAINT "PK_AEDV02Decisions" PRIMARY KEY ("DecisionId"),
        CONSTRAINT "FK_AEDV02Decisions_MatchAuthorityBindings_MatchId" FOREIGN KEY ("MatchId") REFERENCES "MatchAuthorityBindings" ("MatchId") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014102100_AddAEDV02PilotLedger') THEN
    CREATE TABLE "AEDV02SceneContents" (
        "Fingerprint" character varying(64) NOT NULL,
        "SceneVersion" character varying(128) NOT NULL,
        "ManifestJson" text NOT NULL,
        "RegisteredAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_AEDV02SceneContents" PRIMARY KEY ("Fingerprint")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014102100_AddAEDV02PilotLedger') THEN
    CREATE TABLE "AEDV02SkillObservations" (
        "Id" uuid NOT NULL,
        "MatchId" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "Dimension" character varying(32) NOT NULL,
        "ComparisonContextKey" character varying(128) NOT NULL,
        "SourceFingerprint" text NOT NULL,
        "Eligible" integer NOT NULL,
        "ResolvedSuccess" integer NOT NULL,
        "ResolvedFailure" integer NOT NULL,
        "Censored" integer NOT NULL,
        "VerifiedAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_AEDV02SkillObservations" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_AEDV02SkillObservations_MatchAuthorityBindings_MatchId" FOREIGN KEY ("MatchId") REFERENCES "MatchAuthorityBindings" ("MatchId") ON DELETE RESTRICT,
        CONSTRAINT "FK_AEDV02SkillObservations_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014102100_AddAEDV02PilotLedger') THEN
    CREATE TABLE "AEDV02VerifiedSources" (
        "Fingerprint" character varying(64) NOT NULL,
        "MatchId" uuid NOT NULL,
        "PhaseOrdinal" integer NOT NULL,
        "StartTick" bigint NOT NULL,
        "EndTick" bigint NOT NULL,
        "EndEventSequence" bigint NOT NULL,
        "RosterIdentity" text NOT NULL,
        "SceneManifestFingerprint" text NOT NULL,
        "ReconstructedJson" text NOT NULL,
        "VerifiedAtUtc" timestamp with time zone NOT NULL,
        "SourceEndedAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_AEDV02VerifiedSources" PRIMARY KEY ("Fingerprint"),
        CONSTRAINT "FK_AEDV02VerifiedSources_MatchAuthorityBindings_MatchId" FOREIGN KEY ("MatchId") REFERENCES "MatchAuthorityBindings" ("MatchId") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014102100_AddAEDV02PilotLedger') THEN
    CREATE UNIQUE INDEX "IX_AEDV02Decisions_MatchId_PhaseOrdinal_DecisionPoint_WindowSe~" ON "AEDV02Decisions" ("MatchId", "PhaseOrdinal", "DecisionPoint", "WindowSequence");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014102100_AddAEDV02PilotLedger') THEN
    CREATE UNIQUE INDEX "IX_AEDV02Decisions_MatchId_PlanRevision" ON "AEDV02Decisions" ("MatchId", "PlanRevision");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014102100_AddAEDV02PilotLedger') THEN
    CREATE UNIQUE INDEX "IX_AEDV02Decisions_ReceiptId" ON "AEDV02Decisions" ("ReceiptId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014102100_AddAEDV02PilotLedger') THEN
    CREATE UNIQUE INDEX "IX_AEDV02SkillObservations_MatchId_UserId_Dimension_Comparison~" ON "AEDV02SkillObservations" ("MatchId", "UserId", "Dimension", "ComparisonContextKey");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014102100_AddAEDV02PilotLedger') THEN
    CREATE INDEX "IX_AEDV02SkillObservations_UserId" ON "AEDV02SkillObservations" ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014102100_AddAEDV02PilotLedger') THEN
    CREATE INDEX "IX_AEDV02VerifiedSources_MatchId_PhaseOrdinal_EndTick" ON "AEDV02VerifiedSources" ("MatchId", "PhaseOrdinal", "EndTick");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014102100_AddAEDV02PilotLedger') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261014102100_AddAEDV02PilotLedger', '8.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261015030000_AddAEDV02ActionEnvelope') THEN
    ALTER TABLE "AEDV02Decisions" ADD "ProposalJson" text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261015030000_AddAEDV02ActionEnvelope') THEN
    ALTER TABLE "MatchPlayerBindings" ADD "HadGameplayDisconnect" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261015030000_AddAEDV02ActionEnvelope') THEN
    ALTER TABLE "AEDV02Decisions" ADD "ActionFingerprint" character varying(64) NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261015030000_AddAEDV02ActionEnvelope') THEN
    CREATE TABLE "AEDV02Cancellations" (
        "DecisionId" uuid NOT NULL,
        "MatchId" uuid NOT NULL,
        "HostUserId" uuid NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_AEDV02Cancellations" PRIMARY KEY ("DecisionId"),
        CONSTRAINT "FK_AEDV02Cancellations_MatchAuthorityBindings_MatchId" FOREIGN KEY ("MatchId") REFERENCES "MatchAuthorityBindings" ("MatchId") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261015030000_AddAEDV02ActionEnvelope') THEN
    CREATE INDEX "IX_AEDV02Cancellations_MatchId" ON "AEDV02Cancellations" ("MatchId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261015030000_AddAEDV02ActionEnvelope') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261015030000_AddAEDV02ActionEnvelope', '8.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261015095309_AllowHostMigrationActorReuse') THEN
    DROP INDEX "IX_MatchPlayerBindings_MatchId_FusionActorNumber";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261015095309_AllowHostMigrationActorReuse') THEN
    CREATE UNIQUE INDEX "IX_MatchPlayerBindings_MatchId_FusionActorNumber" ON "MatchPlayerBindings" ("MatchId", "FusionActorNumber") WHERE "DisconnectedAtUtc" IS NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261015095309_AllowHostMigrationActorReuse') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261015095309_AllowHostMigrationActorReuse', '8.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261019000000_AddLeaderboards') THEN
    ALTER TABLE "MatchAuthorityBindings" ADD "MapId" character varying(64);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261019000000_AddLeaderboards') THEN
    ALTER TABLE "MatchAuthorityBindings" ADD "Difficulty" character varying(16);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261019000000_AddLeaderboards') THEN
    ALTER TABLE "MatchResults" ADD "RosterKey" character varying(131);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261019000000_AddLeaderboards') THEN
    ALTER TABLE "MatchAuthorityBindings" ADD CONSTRAINT "CK_MatchAuthorityBindings_LeaderboardContext" CHECK (("MapId" IS NULL AND "Difficulty" IS NULL) OR ("MapId" IS NOT NULL AND "Difficulty" IS NOT NULL AND "MapId" IN ('SciFi', 'Scene_A', 'Scene_B') AND "Difficulty" IN ('EASY', 'NORMAL', 'HARD')));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261019000000_AddLeaderboards') THEN
    CREATE INDEX "IX_MatchAuthorityBindings_MapId_Difficulty" ON "MatchAuthorityBindings" ("MapId", "Difficulty");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261019000000_AddLeaderboards') THEN
    CREATE INDEX "IX_MatchResults_Outcome_PlayerCount_DurationSeconds_EndedAtUtc" ON "MatchResults" ("Outcome", "PlayerCount", "DurationSeconds", "EndedAtUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261019000000_AddLeaderboards') THEN
    CREATE INDEX "IX_MatchResults_RosterKey" ON "MatchResults" ("RosterKey");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261019000000_AddLeaderboards') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261019000000_AddLeaderboards', '8.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261026130000_ExtendMatchDurationToTwoHours') THEN
    ALTER TABLE "MatchResults" DROP CONSTRAINT "CK_MatchResults_DurationSeconds_Range";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261026130000_ExtendMatchDurationToTwoHours') THEN
    ALTER TABLE "MatchResults" ADD CONSTRAINT "CK_MatchResults_DurationSeconds_Range" CHECK ("DurationSeconds" >= 60 AND "DurationSeconds" <= 7260);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261026130000_ExtendMatchDurationToTwoHours') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261026130000_ExtendMatchDurationToTwoHours', '8.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027024405_SnapshotPaymentEntitlements') THEN
    ALTER TABLE "PaymentOrders" ADD "FulfillmentKind" character varying(30);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027024405_SnapshotPaymentEntitlements') THEN
    ALTER TABLE "PaymentOrders" ADD "FulfillmentShopItemId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027024405_SnapshotPaymentEntitlements') THEN
    ALTER TABLE "PaymentOrders" ADD "WalletCreditAmount" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027024405_SnapshotPaymentEntitlements') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261027024405_SnapshotPaymentEntitlements', '8.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261029080021_FreezeStartedRosterAndAuditRecovery') THEN
    ALTER TABLE "MatchResults" ADD "RecoveryAudit" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261029080021_FreezeStartedRosterAndAuditRecovery') THEN
    ALTER TABLE "MatchPlayerBindings" ADD "Participated" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261029080021_FreezeStartedRosterAndAuditRecovery') THEN
    UPDATE "MatchPlayerBindings" AS p SET "Participated" = TRUE FROM "MatchAuthorityBindings" AS m WHERE p."MatchId" = m."MatchId" AND m."StartedAtUtc" IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261029080021_FreezeStartedRosterAndAuditRecovery') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261029080021_FreezeStartedRosterAndAuditRecovery', '8.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030090842_AddUserAgreementAcceptance') THEN
    CREATE TABLE "UserAgreementAcceptances" (
        "UserId" uuid NOT NULL,
        "BundleVersion" character varying(40) NOT NULL,
        "ContentSha256" character varying(64) NOT NULL,
        "DocumentVersions" character varying(500) NOT NULL,
        "AcceptedAtUtc" timestamp with time zone NOT NULL,
        "Locale" character varying(2) NOT NULL,
        "AdultConfirmed" boolean NOT NULL,
        "WarningAcknowledged" boolean NOT NULL,
        "TermsAccepted" boolean NOT NULL,
        "PrivacyNoticeAcknowledged" boolean NOT NULL,
        "CommunityAccepted" boolean NOT NULL,
        "TransactionsAccepted" boolean NOT NULL,
        CONSTRAINT "PK_UserAgreementAcceptances" PRIMARY KEY ("UserId", "BundleVersion"),
        CONSTRAINT "FK_UserAgreementAcceptances_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030090842_AddUserAgreementAcceptance') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261030090842_AddUserAgreementAcceptance', '8.0.11');
    END IF;
END $EF$;
COMMIT;

