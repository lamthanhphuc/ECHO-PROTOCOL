-- ECHO PROTOCOL / Report 4 / Figure 04 - Account and Economy ERD
-- PostgreSQL DDL for ERD visualization (SELECTED TABLES, not a production migration).
-- Selected EF Core entities validated against main (7a0d741) and LTP.
-- Includes former Figure 4a Legal Agreements in Figure 04 (no additional numbered Figure).
-- Import this file ALONE into dbdiagram.io (Import SQL / PostgreSQL).
-- Visualization DDL only: do not run against the application database.
-- Names are quoted to preserve EF Core PascalCase identifiers.

CREATE TABLE "Users" (
    "Id" uuid PRIMARY KEY,
    "Email" varchar(255) NOT NULL UNIQUE,
    "Username" varchar(100) NOT NULL UNIQUE,
    "PasswordHash" varchar(255) NOT NULL,
    "Role" varchar(20) NOT NULL,
    "Status" varchar(20) NOT NULL,
    "CreatedAt" timestamptz NOT NULL,
    "UpdatedAt" timestamptz NOT NULL
);

-- Authentication refresh-session records (used by Figure 06/07).
CREATE TABLE "RefreshSessions" (
    "Id" uuid PRIMARY KEY,
    "UserId" uuid NOT NULL REFERENCES "Users" ("Id"),
    "FamilyId" uuid NOT NULL,
    "TokenHash" varchar(64) NOT NULL UNIQUE,
    "CreatedAtUtc" timestamptz NOT NULL,
    "ExpiresAtUtc" timestamptz NOT NULL,
    "LastUsedAtUtc" timestamptz,
    "RevokedAtUtc" timestamptz,
    "ReplacedBySessionId" uuid,
    "ConcurrencyToken" uuid NOT NULL
);

-- Figure 4a integrated: first-time Content Warning and Legal Agreements acceptance.
-- PK (UserId, BundleVersion), not just UserId: new document versions can require acceptance.
CREATE TABLE "UserAgreementAcceptances" (
    "UserId" uuid NOT NULL REFERENCES "Users" ("Id") ON DELETE CASCADE,
    "BundleVersion" varchar(40) NOT NULL,
    "ContentSha256" varchar(64) NOT NULL,
    "DocumentVersions" varchar(500) NOT NULL,
    "AcceptedAtUtc" timestamptz NOT NULL,
    "Locale" varchar(2) NOT NULL,
    "AdultConfirmed" boolean NOT NULL,
    "WarningAcknowledged" boolean NOT NULL,
    "TermsAccepted" boolean NOT NULL,
    "PrivacyNoticeAcknowledged" boolean NOT NULL,
    "CommunityAccepted" boolean NOT NULL,
    "TransactionsAccepted" boolean NOT NULL,
    PRIMARY KEY ("UserId", "BundleVersion")
);

CREATE TABLE "PlayerProfiles" (
    "Id" uuid PRIMARY KEY,
    "UserId" uuid NOT NULL UNIQUE REFERENCES "Users" ("Id"),
    "DisplayName" varchar(100) NOT NULL,
    "TotalMatches" integer NOT NULL DEFAULT 0,
    "TotalWins" integer NOT NULL DEFAULT 0,
    "ExperiencePoints" bigint NOT NULL DEFAULT 0,
    "Level" integer NOT NULL DEFAULT 1,
    "CreatedAt" timestamptz NOT NULL,
    "UpdatedAt" timestamptz NOT NULL,
    CONSTRAINT "CK_PlayerProfiles_ValidCounters" CHECK (
        "TotalMatches" >= 0 AND "TotalWins" BETWEEN 0 AND "TotalMatches"
        AND "ExperiencePoints" >= 0 AND "Level" >= 1
    )
);

CREATE TABLE "Wallets" (
    "Id" uuid PRIMARY KEY,
    "UserId" uuid NOT NULL UNIQUE REFERENCES "Users" ("Id"),
    "Balance" integer NOT NULL DEFAULT 0,
    "UpdatedAt" timestamptz NOT NULL,
    CONSTRAINT "CK_Wallets_Balance_NonNegative" CHECK ("Balance" >= 0)
);

CREATE TABLE "WalletTransactions" (
    "Id" uuid PRIMARY KEY,
    "WalletId" uuid NOT NULL REFERENCES "Wallets" ("Id"),
    "Type" varchar(30) NOT NULL,
    "Amount" integer NOT NULL,
    "BalanceBefore" integer NOT NULL,
    "BalanceAfter" integer NOT NULL,
    "ReferenceId" uuid NOT NULL,
    "Description" varchar(255) NOT NULL,
    "CreatedAtUtc" timestamptz NOT NULL,
    UNIQUE ("WalletId", "Type", "ReferenceId"),
    CONSTRAINT "CK_WalletTransactions_BalanceEquation"
        CHECK ("BalanceAfter" = "BalanceBefore" + "Amount"),
    CONSTRAINT "CK_WalletTransactions_NonNegative"
        CHECK ("BalanceBefore" >= 0 AND "BalanceAfter" >= 0)
);

CREATE TABLE "ShopItems" (
    "ItemId" uuid PRIMARY KEY,
    "ItemName" varchar(100) NOT NULL,
    "Description" varchar(1000) NOT NULL,
    "Category" varchar(50) NOT NULL,
    "Price" integer NOT NULL,
    "AssetReference" varchar(500) NOT NULL,
    "IsActive" boolean NOT NULL,
    "CreatedAtUtc" timestamptz NOT NULL,
    "UpdatedAtUtc" timestamptz NOT NULL,
    CONSTRAINT "CK_ShopItems_Price_NonNegative" CHECK ("Price" >= 0)
);

CREATE TABLE "PurchaseTransactions" (
    "PurchaseId" uuid PRIMARY KEY,
    "UserId" uuid NOT NULL REFERENCES "Users" ("Id"),
    "ShopItemId" uuid NOT NULL REFERENCES "ShopItems" ("ItemId"),
    "IdempotencyKey" varchar(100) NOT NULL,
    "PriceAtPurchase" integer NOT NULL,
    "WalletTransactionId" uuid NOT NULL UNIQUE REFERENCES "WalletTransactions" ("Id"),
    "Status" varchar(20) NOT NULL,
    "CreatedAtUtc" timestamptz NOT NULL,
    UNIQUE ("UserId", "IdempotencyKey"),
    CONSTRAINT "CK_PurchaseTransactions_Price_NonNegative" CHECK ("PriceAtPurchase" >= 0),
    CONSTRAINT "CK_PurchaseTransactions_Completed" CHECK ("Status" = 'COMPLETED')
);

CREATE TABLE "InventoryItems" (
    "InventoryItemId" uuid PRIMARY KEY,
    "UserId" uuid NOT NULL REFERENCES "Users" ("Id"),
    "ShopItemId" uuid NOT NULL REFERENCES "ShopItems" ("ItemId"),
    "Source" varchar(50) NOT NULL,
    "PurchaseId" uuid UNIQUE REFERENCES "PurchaseTransactions" ("PurchaseId"),
    "AcquiredAtUtc" timestamptz NOT NULL,
    UNIQUE ("UserId", "ShopItemId"),
    -- Alternate key required by PlayerLoadoutItems composite foreign key.
    UNIQUE ("UserId", "InventoryItemId")
);

CREATE TABLE "PlayerLoadoutItems" (
    "UserId" uuid NOT NULL REFERENCES "Users" ("Id"),
    "SlotId" varchar(50) NOT NULL,
    "InventoryItemId" uuid NOT NULL,
    "EquippedAtUtc" timestamptz NOT NULL,
    "UpdatedAtUtc" timestamptz NOT NULL,
    PRIMARY KEY ("UserId", "SlotId"),
    UNIQUE ("UserId", "InventoryItemId"),
    FOREIGN KEY ("UserId", "InventoryItemId")
        REFERENCES "InventoryItems" ("UserId", "InventoryItemId"),
    CONSTRAINT "CK_PlayerLoadoutItems_Timestamps"
        CHECK ("UpdatedAtUtc" >= "EquippedAtUtc")
);

-- Important: WalletTransactions.ReferenceId is a business reference, NOT a general FK.
-- InventoryItems.PurchaseId is nullable: inventory may originate outside a purchase.
-- This is the selected Report 4 ERD, NOT the complete EF migration schema.

-- Figure 04 caption: Account, Economy and First-time Legal Acceptance ERD (selected entities).
