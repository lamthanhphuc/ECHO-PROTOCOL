using EchoProtocol.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace EchoProtocol.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<PlayerProfile> PlayerProfiles => Set<PlayerProfile>();
    public DbSet<Wallet> Wallets => Set<Wallet>();
    public DbSet<MatchAuthorityBinding> MatchAuthorityBindings => Set<MatchAuthorityBinding>();
    public DbSet<MatchPlayerBinding> MatchPlayerBindings => Set<MatchPlayerBinding>();
    public DbSet<MatchResult> MatchResults => Set<MatchResult>();
    public DbSet<MatchResultPlayer> MatchResultPlayers => Set<MatchResultPlayer>();
    public DbSet<MatchRewardGrant> MatchRewardGrants => Set<MatchRewardGrant>();
    public DbSet<WalletTransaction> WalletTransactions => Set<WalletTransaction>();
    public DbSet<ShopItem> ShopItems => Set<ShopItem>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<PlayerLoadoutItem> PlayerLoadoutItems => Set<PlayerLoadoutItem>();
    public DbSet<PurchaseTransaction> PurchaseTransactions => Set<PurchaseTransaction>();
    public DbSet<MatchScore> MatchScores => Set<MatchScore>();
    public DbSet<PlayerAIProfile> PlayerAIProfiles => Set<PlayerAIProfile>();
    public DbSet<TeamProfile> TeamProfiles => Set<TeamProfile>();
    public DbSet<ScenarioConfigDefinition> ScenarioConfigs => Set<ScenarioConfigDefinition>();
    public DbSet<ScenarioContentDefinition> ScenarioContentDefinitions => Set<ScenarioContentDefinition>();
    public DbSet<AdaptiveInputSnapshot> AdaptiveInputSnapshots => Set<AdaptiveInputSnapshot>();
    public DbSet<AdaptiveInputSnapshotPlayer> AdaptiveInputSnapshotPlayers => Set<AdaptiveInputSnapshotPlayer>();
    public DbSet<ScenarioDecision> ScenarioDecisions => Set<ScenarioDecision>();
    public DbSet<ScenarioApplyReceipt> ScenarioApplyReceipts => Set<ScenarioApplyReceipt>();
    public DbSet<PaymentOrder> PaymentOrders => Set<PaymentOrder>();
    public DbSet<PaymentCheckout> PaymentCheckouts => Set<PaymentCheckout>();
    public DbSet<PaymentProviderEvent> PaymentProviderEvents => Set<PaymentProviderEvent>();
    public DbSet<PaymentFulfillment> PaymentFulfillments => Set<PaymentFulfillment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Email)
                .IsRequired()
                .HasMaxLength(255);

            entity.Property(e => e.Username)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.PasswordHash)
                .IsRequired()
                .HasMaxLength(255);

            entity.Property(e => e.Role)
                .HasConversion<string>()
                .HasMaxLength(20);

            entity.Property(e => e.Status)
                .HasConversion<string>()
                .HasMaxLength(20);

            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();

            entity.HasIndex(e => e.Username)
                .IsUnique()
                .HasDatabaseName("IX_Users_Username");

            entity.HasIndex(e => e.Email)
                .IsUnique()
                .HasDatabaseName("IX_Users_Email");

            entity.HasOne(e => e.PlayerProfile)
                .WithOne(e => e.User)
                .HasForeignKey<PlayerProfile>(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Wallet)
                .WithOne(e => e.User)
                .HasForeignKey<Wallet>(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.PlayerAIProfile)
                .WithOne(e => e.User)
                .HasForeignKey<PlayerAIProfile>(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PlayerProfile>(entity =>
        {
            entity.ToTable(t =>
            {
                t.HasCheckConstraint(
                    "CK_PlayerProfiles_TotalMatches_NonNegative",
                    "\"TotalMatches\" >= 0");
                t.HasCheckConstraint(
                    "CK_PlayerProfiles_TotalWins_NonNegative",
                    "\"TotalWins\" >= 0");
                t.HasCheckConstraint(
                    "CK_PlayerProfiles_TotalWins_Lte_Matches",
                    "\"TotalWins\" <= \"TotalMatches\"");
                t.HasCheckConstraint(
                    "CK_PlayerProfiles_ExperiencePoints_NonNegative",
                    "\"ExperiencePoints\" >= 0");
                t.HasCheckConstraint(
                    "CK_PlayerProfiles_Level_Positive",
                    "\"Level\" >= 1");
            });

            entity.HasKey(e => e.Id);

            entity.Property(e => e.DisplayName)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.TotalMatches).HasDefaultValue(0);
            entity.Property(e => e.TotalWins).HasDefaultValue(0);
            entity.Property(e => e.ExperiencePoints).HasDefaultValue(0L);
            entity.Property(e => e.Level).HasDefaultValue(1);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();

            entity.HasIndex(e => e.UserId)
                .IsUnique()
                .HasDatabaseName("IX_PlayerProfiles_UserId");
        });

        modelBuilder.Entity<Wallet>(entity =>
        {
            entity.ToTable(t =>
            {
                t.HasCheckConstraint(
                    "CK_Wallets_Balance_NonNegative",
                    "\"Balance\" >= 0");
            });

            entity.HasKey(e => e.Id);
            entity.Property(e => e.Balance).HasDefaultValue(0);
            entity.Property(e => e.UpdatedAt).IsRequired();

            entity.HasIndex(e => e.UserId)
                .IsUnique()
                .HasDatabaseName("IX_Wallets_UserId");
        });

        modelBuilder.Entity<MatchAuthorityBinding>(entity =>
        {
            entity.ToTable("MatchAuthorityBindings");
            entity.HasKey(e => e.MatchId);
            entity.Property(e => e.FusionSessionName).IsRequired().HasMaxLength(128);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
            entity.HasIndex(e => new { e.FusionSessionName, e.Status });
            entity.HasIndex(e => e.HostUserId);
            entity.HasOne(e => e.HostUser)
                .WithMany()
                .HasForeignKey(e => e.HostUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Result)
                .WithOne(e => e.Match)
                .HasForeignKey<MatchResult>(e => e.MatchId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MatchPlayerBinding>(entity =>
        {
            entity.ToTable("MatchPlayerBindings");
            entity.HasKey(e => e.Id);
            entity.HasAlternateKey(e => new { e.MatchId, e.UserId })
                .HasName("AK_MatchPlayerBindings_MatchId_UserId");
            entity.HasIndex(e => new { e.MatchId, e.FusionActorNumber }).IsUnique();
            entity.HasIndex(e => e.JoinProofId).IsUnique();
            entity.HasOne(e => e.Match)
                .WithMany(e => e.Players)
                .HasForeignKey(e => e.MatchId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MatchResult>(entity =>
        {
            entity.ToTable(t =>
            {
                t.HasCheckConstraint(
                    "CK_MatchResults_DurationSeconds_Range",
                    "\"DurationSeconds\" >= 60 AND \"DurationSeconds\" <= 900");
                t.HasCheckConstraint(
                    "CK_MatchResults_Outcome_Allowed",
                    "\"Outcome\" IN ('WIN', 'LOSE')");
                t.HasCheckConstraint(
                    "CK_MatchResults_ObjectiveCompletion_Range",
                    "CAST(\"ObjectiveCompletion\" AS NUMERIC) >= 0 AND CAST(\"ObjectiveCompletion\" AS NUMERIC) <= 1");
                t.HasCheckConstraint(
                    "CK_MatchResults_PlayerCount_Range",
                    "\"PlayerCount\" >= 1 AND \"PlayerCount\" <= 4");
                t.HasCheckConstraint(
                    "CK_MatchResults_Timestamp_Order",
                    "\"EndedAtUtc\" >= \"StartedAtUtc\"");
                t.HasCheckConstraint(
                    "CK_MatchResults_RewardStatus_Allowed",
                    "\"RewardStatus\" IN ('Pending', 'Completed')");
            });

            entity.HasKey(e => e.MatchId);
            entity.Property(e => e.Outcome).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.ObjectiveCompletion).HasPrecision(5, 4);
            entity.Property(e => e.PayloadHash).IsRequired().HasMaxLength(64).IsFixedLength();
            entity.Property(e => e.RewardStatus).HasConversion<string>().HasMaxLength(20);
            entity.HasIndex(e => e.SubmittedByUserId);
            entity.HasIndex(e => e.SubmittedAtUtc);
            entity.HasOne(e => e.SubmittedByUser)
                .WithMany()
                .HasForeignKey(e => e.SubmittedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MatchResultPlayer>(entity =>
        {
            entity.ToTable(t =>
            {
                t.HasCheckConstraint(
                    "CK_MatchResultPlayers_DetectionCount_NonNegative",
                    "\"DetectionCount\" >= 0");
                t.HasCheckConstraint(
                    "CK_MatchResultPlayers_DownedCount_NonNegative",
                    "\"DownedCount\" >= 0");
                t.HasCheckConstraint(
                    "CK_MatchResultPlayers_ReviveCount_NonNegative",
                    "\"ReviveCount\" >= 0");
                t.HasCheckConstraint(
                    "CK_MatchResultPlayers_ObjectiveContribution_NonNegative",
                    "\"ObjectiveContribution\" >= 0");
            });

            entity.HasKey(e => new { e.MatchId, e.UserId });
            entity.HasOne(e => e.MatchResult)
                .WithMany(e => e.Players)
                .HasForeignKey(e => e.MatchId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.PlayerBinding)
                .WithOne(e => e.ResultPlayer)
                .HasForeignKey<MatchResultPlayer>(e => new { e.MatchId, e.UserId })
                .HasPrincipalKey<MatchPlayerBinding>(e => new { e.MatchId, e.UserId })
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MatchRewardGrant>(entity =>
        {
            entity.ToTable(t =>
            {
                t.HasCheckConstraint(
                    "CK_MatchRewardGrants_CurrencyAmount_NonNegative",
                    "\"CurrencyAmount\" >= 0");
                t.HasCheckConstraint(
                    "CK_MatchRewardGrants_ExperiencePointsAwarded_NonNegative",
                    "\"ExperiencePointsAwarded\" >= 0");
            });

            entity.HasKey(e => e.Id);
            entity.Property(e => e.PolicyVersion).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ProgressionPolicyVersion).IsRequired().HasMaxLength(50);
            entity.HasIndex(e => new { e.MatchId, e.UserId })
                .IsUnique()
                .HasDatabaseName("IX_MatchRewardGrants_MatchId_UserId");
            entity.HasIndex(e => e.WalletId);
            entity.HasOne(e => e.ResultPlayer)
                .WithOne(e => e.RewardGrant)
                .HasForeignKey<MatchRewardGrant>(e => new { e.MatchId, e.UserId })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Wallet)
                .WithMany(e => e.MatchRewardGrants)
                .HasForeignKey(e => e.WalletId)
                .OnDelete(DeleteBehavior.Restrict);

        });

        modelBuilder.Entity<WalletTransaction>(entity =>
        {
            entity.ToTable(t =>
            {
                t.HasCheckConstraint(
                    "CK_WalletTransactions_Amount_ByType",
                    "(\"Type\" IN ('MATCH_REWARD', 'PAYMENT_FULFILLMENT') AND \"Amount\" >= 0) OR " +
                    "(\"Type\" = 'PURCHASE' AND \"Amount\" <= 0)");
                t.HasCheckConstraint(
                    "CK_WalletTransactions_Balances_NonNegative",
                    "\"BalanceBefore\" >= 0 AND \"BalanceAfter\" >= 0");
                t.HasCheckConstraint(
                    "CK_WalletTransactions_BalanceEquation",
                    "\"BalanceAfter\" = \"BalanceBefore\" + \"Amount\"");
            });

            entity.HasKey(e => e.Id);
            entity.Property(e => e.Type).HasConversion<string>().HasMaxLength(30);
            entity.Property(e => e.Description).IsRequired().HasMaxLength(255);
            entity.HasIndex(e => new { e.WalletId, e.Type, e.ReferenceId })
                .IsUnique()
                .HasDatabaseName("IX_WalletTransactions_WalletId_Type_ReferenceId");
            entity.HasIndex(e => e.ReferenceId);
            entity.HasIndex(e => new { e.CreatedAtUtc, e.Id })
                .IsDescending(true, true)
                .HasDatabaseName("IX_Admin_WalletTransactions_Created");
            entity.HasIndex(e => new { e.WalletId, e.CreatedAtUtc, e.Id })
                .IsDescending(false, true, true)
                .HasDatabaseName("IX_Admin_WalletTransactions_Wallet_Created");
            entity.HasIndex(e => new { e.Type, e.CreatedAtUtc, e.Id })
                .IsDescending(false, true, true)
                .HasDatabaseName("IX_Admin_WalletTransactions_Type_Created");
            entity.HasOne(e => e.Wallet)
                .WithMany(e => e.Transactions)
                .HasForeignKey(e => e.WalletId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ShopItem>(entity =>
        {
            entity.ToTable(t =>
            {
                t.HasCheckConstraint(
                    "CK_ShopItems_Price_NonNegative",
                    "\"Price\" >= 0");
            });

            entity.HasKey(e => e.ItemId);
            entity.Property(e => e.ItemName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Description).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.Category).IsRequired().HasMaxLength(50);
            entity.Property(e => e.AssetReference).IsRequired().HasMaxLength(500);
            entity.Property(e => e.CreatedAtUtc).IsRequired();
            entity.Property(e => e.UpdatedAtUtc).IsRequired();
            entity.HasIndex(e => new { e.IsActive, e.Category, e.ItemName, e.ItemId })
                .HasDatabaseName("IX_ShopItems_Active_Category_Name");
        });

        modelBuilder.Entity<InventoryItem>(entity =>
        {
            entity.ToTable("InventoryItems");
            entity.HasKey(e => e.InventoryItemId);
            entity.Property(e => e.Source).HasConversion<string>().HasMaxLength(50);
            entity.Property(e => e.AcquiredAtUtc).IsRequired();
            entity.HasIndex(e => new { e.UserId, e.ShopItemId })
                .IsUnique()
                .HasDatabaseName("IX_InventoryItems_UserId_ShopItemId");
            entity.HasIndex(e => e.ShopItemId);
            entity.HasIndex(e => e.PurchaseId).IsUnique();
            entity.HasAlternateKey(e => new { e.UserId, e.InventoryItemId });
            entity.HasOne(e => e.User)
                .WithMany(e => e.InventoryItems)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.ShopItem)
                .WithMany(e => e.InventoryItems)
                .HasForeignKey(e => e.ShopItemId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PlayerLoadoutItem>(entity =>
        {
            entity.ToTable(table => table.HasCheckConstraint(
                "CK_PlayerLoadoutItems_Timestamps",
                "\"UpdatedAtUtc\" >= \"EquippedAtUtc\""));
            entity.HasKey(item => new { item.UserId, item.SlotId });
            entity.Property(item => item.SlotId).HasMaxLength(50).IsRequired();
            entity.Property(item => item.EquippedAtUtc).IsRequired();
            entity.Property(item => item.UpdatedAtUtc).IsRequired();
            entity.HasIndex(item => new { item.UserId, item.InventoryItemId })
                .IsUnique()
                .HasDatabaseName("IX_PlayerLoadoutItems_UserId_InventoryItemId");
            entity.HasOne(item => item.User)
                .WithMany(user => user.LoadoutItems)
                .HasForeignKey(item => item.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.InventoryItem)
                .WithMany(inventory => inventory.LoadoutItems)
                .HasForeignKey(item => new { item.UserId, item.InventoryItemId })
                .HasPrincipalKey(inventory => new { inventory.UserId, inventory.InventoryItemId })
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PurchaseTransaction>(entity =>
        {
            entity.ToTable(t =>
            {
                t.HasCheckConstraint(
                    "CK_PurchaseTransactions_PriceAtPurchase_NonNegative",
                    "\"PriceAtPurchase\" >= 0");
                t.HasCheckConstraint(
                    "CK_PurchaseTransactions_Status_Completed",
                    "\"Status\" = 'COMPLETED'");
            });

            entity.HasKey(e => e.PurchaseId);
            entity.Property(e => e.IdempotencyKey).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.CreatedAtUtc).IsRequired();
            entity.HasIndex(e => new { e.UserId, e.IdempotencyKey })
                .IsUnique()
                .HasDatabaseName("IX_PurchaseTransactions_UserId_IdempotencyKey");
            entity.HasIndex(e => e.ShopItemId);
            entity.HasIndex(e => e.WalletTransactionId).IsUnique();
            entity.HasIndex(e => new { e.CreatedAtUtc, e.PurchaseId })
                .IsDescending(true, true)
                .HasDatabaseName("IX_Admin_Purchases_Created");
            entity.HasIndex(e => new { e.UserId, e.CreatedAtUtc, e.PurchaseId })
                .IsDescending(false, true, true)
                .HasDatabaseName("IX_Admin_Purchases_User_Created");
            entity.HasIndex(e => new { e.ShopItemId, e.CreatedAtUtc, e.PurchaseId })
                .IsDescending(false, true, true)
                .HasDatabaseName("IX_Admin_Purchases_Item_Created");
            entity.HasOne(e => e.User)
                .WithMany(e => e.PurchaseTransactions)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.ShopItem)
                .WithMany(e => e.PurchaseTransactions)
                .HasForeignKey(e => e.ShopItemId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.WalletTransaction)
                .WithOne(e => e.PurchaseTransaction)
                .HasForeignKey<PurchaseTransaction>(e => e.WalletTransactionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.InventoryItem)
                .WithOne(e => e.PurchaseTransaction)
                .HasForeignKey<InventoryItem>(e => e.PurchaseId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PaymentOrder>(entity =>
        {
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_PaymentOrders_Amount_Positive", "\"Amount\" > 0");
                table.HasCheckConstraint(
                    "CK_PaymentOrders_Currency_Format",
                    "length(\"Currency\") = 3 AND \"Currency\" = upper(\"Currency\")");
                table.HasCheckConstraint(
                    "CK_PaymentOrders_Status_Allowed",
                    "\"Status\" IN ('CREATED', 'PENDING_PAYMENT', 'PAID', 'FULFILLED', 'FAILED', 'CANCELLED', 'EXPIRED')");
                table.HasCheckConstraint(
                    "CK_PaymentOrders_Timestamps",
                    "\"UpdatedAtUtc\" >= \"CreatedAtUtc\" AND " +
                    "(\"ExpiresAtUtc\" IS NULL OR \"ExpiresAtUtc\" > \"CreatedAtUtc\") AND " +
                    "(\"PaidAtUtc\" IS NULL OR \"PaidAtUtc\" >= \"CreatedAtUtc\") AND " +
                    "(\"FulfilledAtUtc\" IS NULL OR (\"PaidAtUtc\" IS NOT NULL AND \"FulfilledAtUtc\" >= \"PaidAtUtc\"))");
                table.HasCheckConstraint(
                    "CK_PaymentOrders_State_Data",
                    "(\"Status\" <> 'PENDING_PAYMENT' OR \"ProviderOrderId\" IS NOT NULL) AND " +
                    "(\"Status\" NOT IN ('PAID', 'FULFILLED') OR (\"ProviderOrderId\" IS NOT NULL AND \"ProviderTransactionId\" IS NOT NULL AND \"PaidAtUtc\" IS NOT NULL)) AND " +
                    "(\"Status\" <> 'FULFILLED' OR (\"FulfillmentReference\" IS NOT NULL AND \"FulfilledAtUtc\" IS NOT NULL))");
            });

            entity.HasKey(item => item.PaymentOrderId);
            entity.Property(item => item.Provider).IsRequired().HasMaxLength(50);
            entity.Property(item => item.ProviderOrderId).HasMaxLength(200);
            entity.Property(item => item.Purpose).IsRequired().HasMaxLength(100);
            entity.Property(item => item.ProductReference).IsRequired().HasMaxLength(128);
            entity.Property(item => item.Amount).HasPrecision(18, 2);
            entity.Property(item => item.Currency).IsRequired().HasMaxLength(3).IsFixedLength();
            entity.Property(item => item.Status).HasConversion<string>().HasMaxLength(30);
            entity.Property(item => item.IdempotencyKey).IsRequired().HasMaxLength(100);
            entity.Property(item => item.RequestFingerprint).IsRequired().HasMaxLength(64).IsFixedLength();
            entity.Property(item => item.ProviderTransactionId).HasMaxLength(200);
            entity.Property(item => item.FulfillmentReference).HasMaxLength(200);
            entity.HasIndex(item => new { item.UserId, item.IdempotencyKey })
                .IsUnique()
                .HasDatabaseName("IX_PaymentOrders_UserId_IdempotencyKey");
            entity.HasIndex(item => new { item.Provider, item.ProviderOrderId })
                .IsUnique()
                .HasFilter("\"ProviderOrderId\" IS NOT NULL")
                .HasDatabaseName("IX_PaymentOrders_Provider_ProviderOrderId");
            entity.HasIndex(item => new { item.Provider, item.ProviderTransactionId })
                .IsUnique()
                .HasFilter("\"ProviderTransactionId\" IS NOT NULL")
                .HasDatabaseName("IX_PaymentOrders_Provider_ProviderTransactionId");
            entity.HasIndex(item => item.FulfillmentReference)
                .IsUnique()
                .HasFilter("\"FulfillmentReference\" IS NOT NULL")
                .HasDatabaseName("IX_PaymentOrders_FulfillmentReference");
            entity.HasIndex(item => new { item.UserId, item.CreatedAtUtc });
            entity.HasIndex(item => new { item.CreatedAtUtc, item.PaymentOrderId })
                .IsDescending(true, true)
                .HasDatabaseName("IX_Admin_PaymentOrders_Created");
            entity.HasIndex(item => new { item.Status, item.CreatedAtUtc, item.PaymentOrderId })
                .IsDescending(false, true, true)
                .HasDatabaseName("IX_Admin_PaymentOrders_Status_Created");
            entity.HasIndex(item => new { item.Provider, item.CreatedAtUtc, item.PaymentOrderId })
                .IsDescending(false, true, true)
                .HasDatabaseName("IX_Admin_PaymentOrders_Provider_Created");
            entity.HasIndex(item => new { item.ProductReference, item.CreatedAtUtc, item.PaymentOrderId })
                .IsDescending(false, true, true)
                .HasDatabaseName("IX_Admin_PaymentOrders_Product_Created");
            entity.HasOne(item => item.User)
                .WithMany(user => user.PaymentOrders)
                .HasForeignKey(item => item.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PaymentCheckout>(entity =>
        {
            entity.ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_PaymentCheckouts_Status_Allowed",
                    "\"Status\" IN ('RESERVED', 'READY')");
                table.HasCheckConstraint(
                    "CK_PaymentCheckouts_Ready_Data",
                    "\"Status\" <> 'READY' OR (\"ProviderPaymentLinkId\" IS NOT NULL AND \"CheckoutUrl\" IS NOT NULL AND \"ReadyAtUtc\" IS NOT NULL)");
            });
            entity.HasKey(item => item.CheckoutSequenceId);
            entity.Property(item => item.CheckoutSequenceId).ValueGeneratedOnAdd();
            entity.Property(item => item.Provider).IsRequired().HasMaxLength(50);
            entity.Property(item => item.ProviderOrderId).IsRequired().HasMaxLength(200);
            entity.Property(item => item.ProviderPaymentLinkId).HasMaxLength(200);
            entity.Property(item => item.CheckoutUrl).HasMaxLength(2000);
            entity.Property(item => item.Status).HasConversion<string>().HasMaxLength(20);
            entity.HasIndex(item => item.PaymentOrderId).IsUnique();
            entity.HasIndex(item => new { item.Provider, item.ProviderOrderId }).IsUnique();
            entity.HasOne(item => item.PaymentOrder)
                .WithOne(item => item.Checkout)
                .HasForeignKey<PaymentCheckout>(item => item.PaymentOrderId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PaymentProviderEvent>(entity =>
        {
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_PaymentProviderEvents_Amount_Positive", "\"Amount\" > 0");
                table.HasCheckConstraint("CK_PaymentProviderEvents_Verified", "\"VerificationStatus\" = 'VERIFIED'");
            });
            entity.HasKey(item => item.PaymentProviderEventId);
            entity.Property(item => item.Provider).IsRequired().HasMaxLength(50);
            entity.Property(item => item.ProviderEventId).IsRequired().HasMaxLength(200);
            entity.Property(item => item.ProviderOrderId).IsRequired().HasMaxLength(200);
            entity.Property(item => item.Amount).HasPrecision(18, 2);
            entity.Property(item => item.Currency).HasMaxLength(3).IsFixedLength();
            entity.Property(item => item.NormalizedStatus).HasConversion<string>().HasMaxLength(30);
            entity.Property(item => item.SemanticFingerprint).IsRequired().HasMaxLength(64).IsFixedLength();
            entity.Property(item => item.VerificationStatus).IsRequired().HasMaxLength(20);
            entity.Property(item => item.ProcessingOutcome).HasConversion<string>().HasMaxLength(40);
            entity.HasIndex(item => new { item.Provider, item.ProviderEventId }).IsUnique();
            entity.HasIndex(item => item.PaymentOrderId);
            entity.HasOne(item => item.PaymentOrder)
                .WithMany(item => item.ProviderEvents)
                .HasForeignKey(item => item.PaymentOrderId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PaymentFulfillment>(entity =>
        {
            entity.ToTable(table => table.HasCheckConstraint(
                "CK_PaymentFulfillments_Target",
                "(\"Kind\" = 'WALLET_CREDIT' AND \"WalletTransactionId\" IS NOT NULL AND \"InventoryItemId\" IS NULL) OR " +
                "(\"Kind\" = 'INVENTORY_ITEM' AND \"WalletTransactionId\" IS NULL AND \"InventoryItemId\" IS NOT NULL)"));
            entity.HasKey(item => item.PaymentOrderId);
            entity.Property(item => item.FulfillmentReference).IsRequired().HasMaxLength(200);
            entity.Property(item => item.Kind).HasConversion<string>().HasMaxLength(30);
            entity.HasIndex(item => item.FulfillmentReference).IsUnique();
            entity.HasIndex(item => item.WalletTransactionId).IsUnique();
            entity.HasIndex(item => item.InventoryItemId).IsUnique();
            entity.HasOne(item => item.PaymentOrder)
                .WithOne(item => item.Fulfillment)
                .HasForeignKey<PaymentFulfillment>(item => item.PaymentOrderId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.WalletTransaction)
                .WithMany()
                .HasForeignKey(item => item.WalletTransactionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.InventoryItem)
                .WithMany()
                .HasForeignKey(item => item.InventoryItemId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PlayerAIProfile>(entity =>
        {
            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_PlayerAIProfiles_Revision_NonNegative", "\"ProfileRevision\" >= 0");
                t.HasCheckConstraint("CK_PlayerAIProfiles_SurvivalScore_Range", "CAST(\"SurvivalScore\" AS NUMERIC) >= 0 AND CAST(\"SurvivalScore\" AS NUMERIC) <= 100");
                t.HasCheckConstraint("CK_PlayerAIProfiles_NoiseScore_Range", "CAST(\"NoiseScore\" AS NUMERIC) >= 0 AND CAST(\"NoiseScore\" AS NUMERIC) <= 100");
                t.HasCheckConstraint("CK_PlayerAIProfiles_SampleCounts_NonNegative", "\"SurvivalSampleCount\" >= 0 AND \"NoiseSampleCount\" >= 0");
                t.HasCheckConstraint("CK_PlayerAIProfiles_Deferred_Null", "\"ObjectiveScore\" IS NULL AND \"TeamworkScore\" IS NULL AND \"ExplorationScore\" IS NULL AND \"NavigationScore\" IS NULL AND \"ToolUsageScore\" IS NULL AND \"RiskScore\" IS NULL AND \"ReviveScore\" IS NULL");
            });

            entity.HasKey(e => e.UserId);
            entity.Property(e => e.ProfileFormulaVersion).IsRequired().HasMaxLength(80);
            entity.Property(e => e.MatchScoreFormulaVersion).IsRequired().HasMaxLength(80);
            entity.Property(e => e.NormalizationConfigVersion).HasMaxLength(80);
            entity.Property(e => e.ProfileNoiseFilterVersion).HasMaxLength(80);
            entity.Property(e => e.AlphaConfigVersion).IsRequired().HasMaxLength(80);
            entity.Property(e => e.SurvivalScore).HasPrecision(9, 6).HasDefaultValue(50m);
            entity.Property(e => e.NoiseScore).HasPrecision(9, 6).HasDefaultValue(50m);
            entity.Property(e => e.SurvivalStatus).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.NoiseStatus).HasConversion<string>().HasMaxLength(20);
        });

        modelBuilder.Entity<MatchScore>(entity =>
        {
            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_MatchScores_Score_Range", "CAST(\"Score\" AS NUMERIC) >= 0 AND CAST(\"Score\" AS NUMERIC) <= 100");
            });

            entity.HasKey(e => e.Id);
            entity.Property(e => e.Dimension).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.Score).HasPrecision(9, 6);
            entity.Property(e => e.MatchScoreFormulaVersion).IsRequired().HasMaxLength(80);
            entity.Property(e => e.NormalizationConfigVersion).IsRequired().HasMaxLength(80);
            entity.Property(e => e.ProfileNoiseFilterVersion).HasMaxLength(80);
            entity.Property(e => e.AlphaConfigVersion).IsRequired().HasMaxLength(80);
            entity.Property(e => e.SourceTelemetrySchemaVersion).IsRequired().HasMaxLength(20);
            entity.Property(e => e.SourceEvidenceFingerprint).IsRequired().HasMaxLength(64).IsFixedLength();
            entity.Property(e => e.SemanticFingerprint).IsRequired().HasMaxLength(64).IsFixedLength();
            entity.Property(e => e.ContributionStatus).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.RetractionReason).HasMaxLength(50);
            entity.HasIndex(e => new { e.UserId, e.MatchId, e.ProfileLineageId, e.Dimension })
                .IsUnique()
                .HasDatabaseName("UX_MatchScores_ApplyKey");
            entity.HasIndex(e => new { e.UserId, e.ProfileLineageId, e.Dimension, e.MatchEndTs, e.MatchId })
                .HasDatabaseName("IX_MatchScores_ReplayOrder");
            entity.HasOne(e => e.ResultPlayer)
                .WithMany(e => e.MatchScores)
                .HasForeignKey(e => new { e.MatchId, e.UserId })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.PlayerAIProfile)
                .WithMany(e => e.MatchScores)
                .HasForeignKey(e => e.ProfileLineageId)
                .HasPrincipalKey(e => e.ProfileLineageId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TeamProfile>(entity =>
        {
            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_TeamProfiles_Revision_NonNegative", "\"ProcessingRevision\" >= 0");
                t.HasCheckConstraint("CK_TeamProfiles_ObjectiveTime_NonNegative", "\"ObjectiveTimeSeconds\" IS NULL OR CAST(\"ObjectiveTimeSeconds\" AS NUMERIC) >= 0");
                t.HasCheckConstraint("CK_TeamProfiles_Scores_Range", "(\"ObjectiveSpeedScore\" IS NULL OR (CAST(\"ObjectiveSpeedScore\" AS NUMERIC) >= 0 AND CAST(\"ObjectiveSpeedScore\" AS NUMERIC) <= 100)) AND (\"SurvivalScore\" IS NULL OR (CAST(\"SurvivalScore\" AS NUMERIC) >= 0 AND CAST(\"SurvivalScore\" AS NUMERIC) <= 100))");
                t.HasCheckConstraint("CK_TeamProfiles_Deferred_Null", "\"SplitTime\" IS NULL AND \"SplitTimeStatus\" = 'Deferred' AND \"AvgDistance\" IS NULL AND \"AvgDistanceStatus\" = 'Deferred' AND \"ReviveSuccess\" IS NULL AND \"ReviveSuccessStatus\" = 'Deferred' AND \"ResourceEfficiency\" IS NULL AND \"ResourceEfficiencyStatus\" = 'Deferred' AND \"Communication\" IS NULL AND \"CommunicationStatus\" = 'Deferred' AND \"WipeRecovery\" IS NULL AND \"WipeRecoveryStatus\" = 'Deferred' AND \"TeamworkScore\" IS NULL AND \"TeamworkStatus\" = 'Deferred' AND \"ResourceEfficiencyScore\" IS NULL AND \"ResourceEfficiencyScoreStatus\" = 'Deferred'");
                t.HasCheckConstraint("CK_TeamProfiles_ObjectiveTime_StatusValue", "(\"ObjectiveTimeStatus\" = 'Available' AND \"ObjectiveTimeSeconds\" IS NOT NULL) OR (\"ObjectiveTimeStatus\" <> 'Available' AND \"ObjectiveTimeSeconds\" IS NULL)");
                t.HasCheckConstraint("CK_TeamProfiles_ObjectiveSpeed_StatusValue", "(\"ObjectiveSpeedStatus\" = 'Available' AND \"ObjectiveSpeedScore\" IS NOT NULL) OR (\"ObjectiveSpeedStatus\" <> 'Available' AND \"ObjectiveSpeedScore\" IS NULL)");
                t.HasCheckConstraint("CK_TeamProfiles_Survival_StatusValue", "(\"SurvivalStatus\" = 'Available' AND \"SurvivalScore\" IS NOT NULL) OR (\"SurvivalStatus\" <> 'Available' AND \"SurvivalScore\" IS NULL)");
                t.HasCheckConstraint("CK_TeamProfiles_Performance_Incomplete", "\"TeamPerformanceStatus\" = 'Incomplete' AND \"TeamPerformanceScore\" IS NULL");
            });

            entity.HasKey(e => e.MatchId);
            entity.Property(e => e.ProcessingStatus).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.ProcessingReason).IsRequired().HasMaxLength(100);
            entity.Property(e => e.TelemetryCompleteness).IsRequired().HasMaxLength(20);
            entity.Property(e => e.ObjectiveTimeSeconds).HasPrecision(14, 6);
            entity.Property(e => e.ObjectiveSpeedScore).HasPrecision(9, 6);
            entity.Property(e => e.SurvivalScore).HasPrecision(9, 6);
            entity.Property(e => e.ObjectiveTimeStatus).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.SplitTimeStatus).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.AvgDistanceStatus).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.ReviveSuccessStatus).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.ResourceEfficiencyStatus).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.CommunicationStatus).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.WipeRecoveryStatus).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.ObjectiveSpeedStatus).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.SurvivalStatus).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.TeamworkStatus).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.ResourceEfficiencyScoreStatus).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.TeamPerformanceStatus).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.ProfileFormulaVersion).IsRequired().HasMaxLength(80);
            entity.Property(e => e.TeamPerformanceFormulaVersion).HasMaxLength(80);
            entity.Property(e => e.PhaseRegistryVersion).HasMaxLength(80);
            entity.Property(e => e.NormalizationConfigVersion).HasMaxLength(80);
            entity.Property(e => e.SourceTelemetrySchemaVersion).IsRequired().HasMaxLength(20);
            entity.Property(e => e.SourceFingerprint).IsRequired().HasMaxLength(64).IsFixedLength();
            entity.Property(e => e.ProjectionFingerprint).IsRequired().HasMaxLength(64).IsFixedLength();
            entity.HasIndex(e => e.ProcessingStatus);
            entity.HasOne(e => e.MatchResult)
                .WithOne(e => e.TeamProfile)
                .HasForeignKey<TeamProfile>(e => e.MatchId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ScenarioContentDefinition>(entity =>
        {
            entity.ToTable(t => t.HasCheckConstraint(
                "CK_ScenarioContentDefinitions_Type_Allowed",
                "\"ContentType\" IN ('Map', 'Monster', 'ObjectiveSpawnSet', 'RouteModifier')"));
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ContentType).HasConversion<string>().HasMaxLength(30);
            entity.Property(e => e.ContentId).IsRequired().HasMaxLength(128);
            entity.Property(e => e.ContentWhitelistVersion).IsRequired().HasMaxLength(80);
            entity.Property(e => e.UnityCompatibilityVersion).IsRequired().HasMaxLength(80);
            entity.Property(e => e.Provenance).IsRequired().HasMaxLength(500);
            entity.HasIndex(e => new
                { e.ContentType, e.ContentId, e.ContentWhitelistVersion, e.UnityCompatibilityVersion })
                .IsUnique()
                .HasDatabaseName("UX_ScenarioContent_IdentityVersion");
            entity.HasIndex(e => new { e.IsActive, e.IsProductionApproved, e.UnityCompatibilityVersion });
        });

        modelBuilder.Entity<ScenarioConfigDefinition>(entity =>
        {
            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_ScenarioConfigs_SupportBudget_NonNegative", "\"SupportItemBudget\" >= 0");
                t.HasCheckConstraint("CK_ScenarioConfigs_Numerics_NonNegative", "\"DetectionFillRate\" >= 0 AND \"DetectionDecayRate\" >= 0 AND \"ChaseSpeed\" >= 0 AND \"SearchDuration\" >= 0");
                t.HasCheckConstraint("CK_ScenarioConfigs_EscapeTimer_Range", "\"EscapeDoorTimerSeconds\" >= 45 AND \"EscapeDoorTimerSeconds\" <= 60");
                t.HasCheckConstraint("CK_ScenarioConfigs_Fallback_IsFixed", "NOT \"IsFixedFallback\" OR \"ConfigSource\" = 'Fixed'");
                t.HasCheckConstraint("CK_ScenarioConfigs_Source_Allowed", "\"ConfigSource\" IN ('Fixed', 'Adaptive')");
            });
            entity.HasKey(e => new { e.ScenarioConfigId, e.ScenarioConfigVersion });
            entity.Property(e => e.ScenarioConfigId).HasMaxLength(128);
            entity.Property(e => e.ScenarioConfigVersion).HasMaxLength(80);
            entity.Property(e => e.SchemaVersion).IsRequired().HasMaxLength(20);
            entity.Property(e => e.PolicyVersion).IsRequired().HasMaxLength(80);
            entity.Property(e => e.ConfigSource).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.MapId).IsRequired().HasMaxLength(128);
            entity.Property(e => e.MonsterType).IsRequired().HasMaxLength(128);
            entity.Property(e => e.ObjectiveSpawnSetId).IsRequired().HasMaxLength(128);
            entity.Property(e => e.RouteModifier).IsRequired().HasMaxLength(128);
            entity.Property(e => e.FallbackConfigId).IsRequired().HasMaxLength(128);
            entity.Property(e => e.FallbackConfigVersion).IsRequired().HasMaxLength(80);
            entity.Property(e => e.ContentWhitelistVersion).IsRequired().HasMaxLength(80);
            entity.Property(e => e.UnityCompatibilityVersion).IsRequired().HasMaxLength(80);
            entity.Property(e => e.Provenance).IsRequired().HasMaxLength(500);
            entity.HasIndex(e => e.ScenarioConfigVersion)
                .HasDatabaseName("IX_ScenarioConfigs_ScenarioConfigVersion");
            entity.HasIndex(e => e.UnityCompatibilityVersion)
                .IsUnique()
                .HasFilter("\"IsActive\" AND \"IsProductionApproved\" AND \"IsFixedFallback\"")
                .HasDatabaseName("UX_ScenarioConfigs_ProductionFallbackCompatibility");
        });

        modelBuilder.Entity<AdaptiveInputSnapshot>(entity =>
        {
            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_AdaptiveSnapshots_TeamSize_Positive", "\"TeamSize\" >= 1 AND \"TeamSize\" <= 4");
                t.HasCheckConstraint("CK_AdaptiveSnapshots_Means_Range", "(\"SurvivalMeanObservedScore\" IS NULL OR (CAST(\"SurvivalMeanObservedScore\" AS NUMERIC) >= 0 AND CAST(\"SurvivalMeanObservedScore\" AS NUMERIC) <= 100)) AND (\"NoiseMeanObservedScore\" IS NULL OR (CAST(\"NoiseMeanObservedScore\" AS NUMERIC) >= 0 AND CAST(\"NoiseMeanObservedScore\" AS NUMERIC) <= 100))");
            });
            entity.HasKey(e => e.SnapshotId);
            entity.Property(e => e.DecisionPoint).IsRequired().HasMaxLength(30);
            entity.Property(e => e.SnapshotContentFingerprint).IsRequired().HasMaxLength(64).IsFixedLength();
            entity.Property(e => e.RosterIdentity).IsRequired().HasMaxLength(64).IsFixedLength();
            entity.Property(e => e.Validity).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.ReasonCodesJson).IsRequired().HasColumnType("jsonb");
            entity.Property(e => e.ProfileFormulaSemanticId).HasMaxLength(80);
            entity.Property(e => e.SurvivalComparisonKey).HasMaxLength(64).IsFixedLength();
            entity.Property(e => e.NoiseComparisonKey).HasMaxLength(64).IsFixedLength();
            entity.Property(e => e.SurvivalAggregationStatus).IsRequired().HasMaxLength(20);
            entity.Property(e => e.NoiseAggregationStatus).IsRequired().HasMaxLength(20);
            entity.Property(e => e.SurvivalMeanObservedScore).HasPrecision(9, 6);
            entity.Property(e => e.NoiseMeanObservedScore).HasPrecision(9, 6);
            entity.HasIndex(e => new { e.MatchId, e.CreatedAtUtc });
            entity.HasOne<MatchAuthorityBinding>().WithMany().HasForeignKey(e => e.MatchId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AdaptiveInputSnapshotPlayer>(entity =>
        {
            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_AdaptiveSnapshotPlayers_DeferredCanonical", "\"DeferredDimensionsJson\" IS NOT NULL");
                t.HasCheckConstraint("CK_AdaptiveSnapshotPlayers_Scores_Range", "(\"SurvivalScore\" IS NULL OR (CAST(\"SurvivalScore\" AS NUMERIC) >= 0 AND CAST(\"SurvivalScore\" AS NUMERIC) <= 100)) AND (\"NoiseScore\" IS NULL OR (CAST(\"NoiseScore\" AS NUMERIC) >= 0 AND CAST(\"NoiseScore\" AS NUMERIC) <= 100))");
            });
            entity.HasKey(e => new { e.SnapshotId, e.UserId });
            entity.Property(e => e.ProfileFormulaVersion).HasMaxLength(80);
            entity.Property(e => e.MatchScoreFormulaVersion).HasMaxLength(80);
            entity.Property(e => e.NormalizationConfigVersion).HasMaxLength(80);
            entity.Property(e => e.ProfileNoiseFilterVersion).HasMaxLength(80);
            entity.Property(e => e.AlphaConfigVersion).HasMaxLength(80);
            entity.Property(e => e.SurvivalScore).HasPrecision(9, 6);
            entity.Property(e => e.NoiseScore).HasPrecision(9, 6);
            entity.Property(e => e.SurvivalStatus).IsRequired().HasMaxLength(20);
            entity.Property(e => e.NoiseStatus).IsRequired().HasMaxLength(20);
            entity.Property(e => e.SurvivalComparisonKey).HasMaxLength(64).IsFixedLength();
            entity.Property(e => e.NoiseComparisonKey).HasMaxLength(64).IsFixedLength();
            entity.Property(e => e.DeferredDimensionsJson).IsRequired().HasColumnType("jsonb");
            entity.HasOne(e => e.Snapshot).WithMany(e => e.Players).HasForeignKey(e => e.SnapshotId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<User>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ScenarioDecision>(entity =>
        {
            entity.ToTable("ScenarioDecisions");
            entity.HasKey(e => e.DecisionId);
            entity.Property(e => e.ResolutionMode).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.DecisionPoint).IsRequired().HasMaxLength(30);
            entity.Property(e => e.ExperimentCondition).HasMaxLength(80);
            entity.Property(e => e.RequestFingerprint).IsRequired().HasMaxLength(64).IsFixedLength();
            entity.Property(e => e.DecisionSemanticFingerprint).IsRequired().HasMaxLength(64).IsFixedLength();
            entity.Property(e => e.ScenarioConfigId).IsRequired().HasMaxLength(128);
            entity.Property(e => e.ScenarioConfigVersion).IsRequired().HasMaxLength(80);
            entity.Property(e => e.ScenarioConfigFingerprint).IsRequired().HasMaxLength(64).IsFixedLength();
            entity.Property(e => e.PolicyVersion).IsRequired().HasMaxLength(80);
            entity.Property(e => e.PolicyConfigVersion).HasMaxLength(80);
            entity.Property(e => e.EvidencePolicyVersion).HasMaxLength(80);
            entity.Property(e => e.ParameterRegistryVersion).HasMaxLength(80);
            entity.Property(e => e.ContentWhitelistVersion).IsRequired().HasMaxLength(80);
            entity.Property(e => e.FallbackConfigId).IsRequired().HasMaxLength(128);
            entity.Property(e => e.FallbackConfigVersion).IsRequired().HasMaxLength(80);
            entity.Property(e => e.FallbackReasonCode).HasMaxLength(50);
            entity.Property(e => e.CandidateValidationStatus).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.ResolutionResult).IsRequired().HasMaxLength(30);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
            entity.HasIndex(e => e.MatchId).IsUnique().HasFilter("\"IsCurrent\"").HasDatabaseName("UX_ScenarioDecisions_CurrentMatch");
            entity.HasIndex(e => e.SnapshotId).IsUnique();
            entity.HasOne(e => e.Match).WithMany().HasForeignKey(e => e.MatchId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.HostUser).WithMany().HasForeignKey(e => e.HostUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Snapshot).WithOne(e => e.Decision).HasForeignKey<ScenarioDecision>(e => e.SnapshotId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.ScenarioConfig).WithMany().HasForeignKey(e => new { e.ScenarioConfigId, e.ScenarioConfigVersion }).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ScenarioApplyReceipt>(entity =>
        {
            entity.ToTable("ScenarioApplyReceipts");
            entity.HasKey(e => e.DecisionId);
            entity.Property(e => e.AppliedScenarioConfigId).IsRequired().HasMaxLength(128);
            entity.Property(e => e.AppliedScenarioConfigVersion).IsRequired().HasMaxLength(80);
            entity.Property(e => e.AppliedScenarioConfigFingerprint).IsRequired().HasMaxLength(64).IsFixedLength();
            entity.HasIndex(e => e.MatchId).IsUnique();
            entity.HasOne(e => e.Decision).WithOne(e => e.ApplyReceipt).HasForeignKey<ScenarioApplyReceipt>(e => e.DecisionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<User>().WithMany().HasForeignKey(e => e.ReportedByUserId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
