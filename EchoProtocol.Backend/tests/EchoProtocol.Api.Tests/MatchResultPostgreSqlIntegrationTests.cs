using System.Security.Claims;
using System.Collections.Concurrent;
using System.Text.Json;
using EchoProtocol.Api.Common;
using EchoProtocol.Api.Configurations;
using EchoProtocol.Api.Controllers;
using EchoProtocol.Api.Data;
using EchoProtocol.Api.Data.Telemetry;
using EchoProtocol.Api.DTOs.MatchResults;
using EchoProtocol.Api.DTOs.Inventory;
using EchoProtocol.Api.DTOs.Admin;
using EchoProtocol.Api.DTOs.Payments;
using EchoProtocol.Api.DTOs.Shop;
using EchoProtocol.Api.DTOs.Scenarios;
using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services;
using EchoProtocol.Api.Services.Interfaces;
using EchoProtocol.Api.Services.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Options;
using Npgsql;
using MongoDB.Bson;
using Xunit;

namespace EchoProtocol.Api.Tests;

[CollectionDefinition("M4 PostgreSQL", DisableParallelization = true)]
public sealed class M4PostgreSqlCollectionDefinition;

[Collection("M4 PostgreSQL")]
public sealed class MatchResultPostgreSqlIntegrationTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact, Trait("Category", "M4PostgreSqlIntegration")]
    public async Task MigrationAppliesToRealPostgreSql()
    {
        await using var scope = await PostgresScope.CreateAsync();
        await using var db = scope.CreateDbContext();

        var applied = await db.Database.GetAppliedMigrationsAsync();
        var pending = await db.Database.GetPendingMigrationsAsync();

        Assert.Contains(applied, name => name.EndsWith("AddMatchResultsM4009"));
        Assert.Contains(applied, name => name.EndsWith("AddRewardWalletLedgerM4010"));
        Assert.Contains(applied, name => name.EndsWith("AddPlayerProgressionM4011"));
        Assert.Contains(applied, name => name.EndsWith("AddShopCatalogM4012M4013"));
        Assert.Contains(applied, name => name.EndsWith("AddPurchaseInventoryM4014M4015"));
        Assert.Contains(applied, name => name.EndsWith("AddPlayerAIProfileMatchScoreM4027M4038M4039"));
        Assert.Contains(applied, name => name.EndsWith("AddTeamProfileM4040"));
        Assert.Contains(applied, name => name.EndsWith("AddScenarioConfigRegistryM4043M4045"));
        Assert.Contains(applied, name => name.EndsWith("AddScenarioDecisionsM4051M4049"));
        Assert.Contains(applied, name => name.EndsWith("AddPaymentOrdersM4054"));
        Assert.Contains(applied, name => name.EndsWith("AddPayOSCheckoutWebhookFulfillmentM4055M4056M4057"));
        Assert.Empty(pending);
    }

    [Fact, Trait("Category", "M4ScenarioPostgreSqlIntegration")]
    public async Task ScenarioRegistry_PersistsAndResolvesApprovedFixedFallback()
    {
        await using var scope = await PostgresScope.CreateAsync();
        await using var db = scope.CreateDbContext();
        db.ScenarioContentDefinitions.AddRange(CreateScenarioContent());
        db.ScenarioConfigs.Add(CreateScenarioFallback());
        await db.SaveChangesAsync();

        var result = await new ScenarioConfigRegistry(db).ResolveWithFixedFallbackAsync(
            "unknown", "unknown", "TEST_UNITY_CONTRACT_V1");

        Assert.True(result.IsSuccess);
        Assert.True(result.Data!.UsedFixedFallback);
        Assert.Equal("TEST_FIXED", result.Data.Config.ScenarioConfigId);
    }

    [Fact, Trait("Category", "M4ScenarioPostgreSqlIntegration")]
    public async Task ScenarioRegistry_AllowsOnlyOneProductionFallbackPerCompatibilityVersion()
    {
        await using var scope = await PostgresScope.CreateAsync();
        await using var db = scope.CreateDbContext();
        db.ScenarioConfigs.Add(CreateScenarioFallback());
        var duplicate = CreateScenarioFallback();
        duplicate.ScenarioConfigId = "TEST_FIXED_DUPLICATE";
        duplicate.ScenarioConfigVersion = "TEST_FIXED_DUPLICATE_V1";
        db.ScenarioConfigs.Add(duplicate);

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact, Trait("Category", "M4ScenarioDecisionPostgreSqlIntegration")]
    public async Task ScenarioDecision_ConcurrentIdenticalRequests_CommitExactlyOnce()
    {
        await using var scope = await PostgresScope.CreateAsync();
        var seed = await SeedScenarioDecisionAsync(scope);
        await using var firstDb = scope.CreateDbContext();
        await using var secondDb = scope.CreateDbContext();
        var first = CreateScenarioService(firstDb);
        var second = CreateScenarioService(secondDb);
        var request = new ResolveScenarioRequest
        { DecisionId = Guid.NewGuid(), ResolutionMode = ScenarioResolutionMode.Fixed, UnityCompatibilityVersion = "TEST_UNITY_CONTRACT_V1" };

        var results = await Task.WhenAll(
            first.ResolvePreMatchAsync(seed.HostId, seed.MatchId, request),
            second.ResolvePreMatchAsync(seed.HostId, seed.MatchId, request));

        Assert.All(results, item => Assert.True(item.IsSuccess));
        Assert.Single(results, item => item.Data!.IsReplay);
        await using var verify = scope.CreateDbContext();
        Assert.Equal(1, await verify.ScenarioDecisions.CountAsync());
        Assert.Equal(1, await verify.AdaptiveInputSnapshots.CountAsync());
        Assert.Empty(await verify.ScenarioApplyReceipts.ToArrayAsync());
    }

    [Fact, Trait("Category", "M4ScenarioDecisionPostgreSqlIntegration")]
    public async Task ScenarioDecision_ConcurrentConflictingRequests_HasOneCurrentWinner()
    {
        await using var scope = await PostgresScope.CreateAsync();
        var seed = await SeedScenarioDecisionAsync(scope);
        await using var firstDb = scope.CreateDbContext();
        await using var secondDb = scope.CreateDbContext();
        var first = CreateScenarioService(firstDb);
        var second = CreateScenarioService(secondDb);

        var results = await Task.WhenAll(
            first.ResolvePreMatchAsync(seed.HostId, seed.MatchId, new ResolveScenarioRequest
            { DecisionId = Guid.NewGuid(), ResolutionMode = ScenarioResolutionMode.Fixed, UnityCompatibilityVersion = "TEST_UNITY_CONTRACT_V1" }),
            second.ResolvePreMatchAsync(seed.HostId, seed.MatchId, new ResolveScenarioRequest
            { DecisionId = Guid.NewGuid(), ResolutionMode = ScenarioResolutionMode.Adaptive, UnityCompatibilityVersion = "TEST_UNITY_CONTRACT_V1" }));

        Assert.Single(results, item => item.IsSuccess);
        Assert.Single(results, item => !item.IsSuccess && item.ErrorCode == ErrorCodes.ScenarioDecisionConflict);
        await using var verify = scope.CreateDbContext();
        Assert.Equal(1, await verify.ScenarioDecisions.CountAsync(item => item.IsCurrent));
    }

    [Fact, Trait("Category", "M4PlayerAIProfilePostgreSqlIntegration")]
    public async Task PlayerAIProfile_PersistsProfileAndDimensionReceipts()
    {
        await using var scope = await PostgresScope.CreateAsync();
        var seed = await scope.SeedRewardMatchAsync();
        await using var db = scope.CreateDbContext();
        var aggregation = CreateProfileAggregation(seed.MatchId, seed.HostId, Now.UtcDateTime);
        var updater = new PlayerAIProfileUpdater(
            db, new StaticProfileAggregator(aggregation), CreateProfilePolicy(),
            new FixedTimeProvider(Now));

        var result = await updater.ProcessAsync(seed.MatchId, seed.HostId);

        Assert.True(result.IsSuccess);
        db.ChangeTracker.Clear();
        var profile = await db.PlayerAIProfiles.Include(item => item.MatchScores)
            .SingleAsync(item => item.UserId == seed.HostId);
        Assert.Equal(1, profile.ProfileRevision);
        Assert.Equal(2, profile.MatchScores.Count);
        Assert.Equal(100m, profile.SurvivalScore);
        Assert.Null(profile.ObjectiveScore);
    }

    [Fact, Trait("Category", "M4PlayerAIProfilePostgreSqlIntegration")]
    public async Task PlayerAIProfile_ConcurrentDuplicate_IsAppliedExactlyOnce()
    {
        await using var scope = await PostgresScope.CreateAsync();
        var seed = await scope.SeedRewardMatchAsync();
        var aggregation = CreateProfileAggregation(seed.MatchId, seed.HostId, Now.UtcDateTime);
        await using var firstDb = scope.CreateDbContext();
        await using var secondDb = scope.CreateDbContext();
        var first = new PlayerAIProfileUpdater(firstDb, new StaticProfileAggregator(aggregation),
            CreateProfilePolicy(), new FixedTimeProvider(Now));
        var second = new PlayerAIProfileUpdater(secondDb, new StaticProfileAggregator(aggregation),
            CreateProfilePolicy(), new FixedTimeProvider(Now));

        var results = await Task.WhenAll(
            first.ProcessAsync(seed.MatchId, seed.HostId),
            second.ProcessAsync(seed.MatchId, seed.HostId));

        Assert.All(results, item => Assert.True(item.IsSuccess));
        Assert.Single(results, item => item.Data!.IsDuplicate);
        await using var verify = scope.CreateDbContext();
        Assert.Equal(2, await verify.MatchScores.CountAsync(item => item.UserId == seed.HostId));
        Assert.Equal(1, (await verify.PlayerAIProfiles.SingleAsync(item => item.UserId == seed.HostId)).ProfileRevision);
    }

    [Fact, Trait("Category", "M4PlayerAIProfilePostgreSqlIntegration")]
    public async Task PlayerAIProfile_SourceChange_RollsBackProfileAndScores()
    {
        await using var scope = await PostgresScope.CreateAsync();
        var seed = await scope.SeedRewardMatchAsync();
        await using var db = scope.CreateDbContext();
        var initial = CreateProfileAggregation(seed.MatchId, seed.HostId, Now.UtcDateTime);
        var changed = initial with { SourceFingerprint = new string('D', 64) };
        var updater = new PlayerAIProfileUpdater(db,
            new SequencedProfileAggregator(initial, changed), CreateProfilePolicy(),
            new FixedTimeProvider(Now));

        var result = await updater.ProcessAsync(seed.MatchId, seed.HostId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.AIProfileSourceChanged, result.ErrorCode);
        db.ChangeTracker.Clear();
        Assert.Empty(await db.PlayerAIProfiles.ToArrayAsync());
        Assert.Empty(await db.MatchScores.ToArrayAsync());
    }

    [Fact, Trait("Category", "M4TeamProfilePostgreSqlIntegration")]
    public async Task TeamProfile_MigrationAndPersistence_UseMatchScopedPrimaryKey()
    {
        await using var scope = await PostgresScope.CreateAsync();
        var seed = await scope.SeedRewardMatchAsync();
        await using var db = scope.CreateDbContext();
        var service = new TeamProfileService(db,
            new StaticTeamTelemetryRepository(CreateTeamTelemetry(seed.MatchId)),
            CreateTeamProfilePolicy(), new FixedTimeProvider(Now));

        var result = await service.ProcessAsync(seed.MatchId);

        Assert.True(result.IsSuccess);
        db.ChangeTracker.Clear();
        var stored = await db.TeamProfiles.SingleAsync();
        Assert.Equal(seed.MatchId, stored.MatchId);
        Assert.Equal(30m, stored.ObjectiveTimeSeconds);
        Assert.Equal(TeamMetricStatus.Deferred, stored.TeamworkStatus);
        Assert.Null(stored.TeamPerformanceScore);
        Assert.Equal(TeamPerformanceStatus.Incomplete, stored.TeamPerformanceStatus);
    }

    [Fact, Trait("Category", "M4TeamProfilePostgreSqlIntegration")]
    public async Task TeamProfile_ConcurrentProcessing_ConvergesToOneRevision()
    {
        await using var scope = await PostgresScope.CreateAsync();
        var seed = await scope.SeedRewardMatchAsync();
        var repository = new StaticTeamTelemetryRepository(CreateTeamTelemetry(seed.MatchId));
        await using var firstDb = scope.CreateDbContext();
        await using var secondDb = scope.CreateDbContext();
        var first = new TeamProfileService(firstDb, repository,
            CreateTeamProfilePolicy(), new FixedTimeProvider(Now));
        var second = new TeamProfileService(secondDb, repository,
            CreateTeamProfilePolicy(), new FixedTimeProvider(Now));

        var results = await Task.WhenAll(
            first.ProcessAsync(seed.MatchId), second.ProcessAsync(seed.MatchId));

        Assert.All(results, item => Assert.True(item.IsSuccess));
        Assert.Single(results, item => item.Data!.IsDuplicate);
        await using var verify = scope.CreateDbContext();
        Assert.Equal(1, await verify.TeamProfiles.CountAsync());
        Assert.Equal(1, (await verify.TeamProfiles.SingleAsync()).ProcessingRevision);
    }

    [Fact, Trait("Category", "M4PurchasePostgreSqlIntegration")]
    public async Task PurchaseAtomicallyDebitsWalletAndPersistsLedgerPurchaseAndInventory()
    {
        await using var scope = await PostgresScope.CreateAsync();
        var seed = await scope.SeedPurchaseAsync();

        var result = await ProcessPurchaseAsync(
            scope, seed.UserId, seed.FirstItemId, "atomic-purchase");

        Assert.True(result.IsSuccess);
        Assert.False(result.Data!.IsReplay);
        await using var verify = scope.CreateDbContext();
        Assert.Equal(400, (await verify.Wallets.AsNoTracking().SingleAsync()).Balance);
        var ledger = await verify.WalletTransactions.AsNoTracking().SingleAsync();
        Assert.Equal(WalletTransactionType.PURCHASE, ledger.Type);
        Assert.Equal(-100, ledger.Amount);
        Assert.Equal(500, ledger.BalanceBefore);
        Assert.Equal(400, ledger.BalanceAfter);
        var purchase = await verify.PurchaseTransactions.AsNoTracking().SingleAsync();
        Assert.Equal(ledger.Id, purchase.WalletTransactionId);
        var inventory = await verify.InventoryItems.AsNoTracking().SingleAsync();
        Assert.Equal(purchase.PurchaseId, inventory.PurchaseId);
    }

    [Fact, Trait("Category", "M4PurchasePostgreSqlIntegration")]
    public async Task InventoryGrantDatabaseFailureRollsBackEntirePurchase()
    {
        await using var scope = await PostgresScope.CreateAsync();
        var seed = await scope.SeedPurchaseAsync();
        await using (var setup = scope.CreateDbContext())
        {
            await setup.Database.ExecuteSqlRawAsync("""
                CREATE FUNCTION reject_inventory_grant() RETURNS trigger AS $$
                BEGIN
                    RAISE EXCEPTION 'forced inventory grant failure' USING ERRCODE = '23514';
                END;
                $$ LANGUAGE plpgsql;
                CREATE TRIGGER reject_inventory_grant_trigger
                BEFORE INSERT ON "InventoryItems"
                FOR EACH ROW EXECUTE FUNCTION reject_inventory_grant();
                """);
        }

        await Assert.ThrowsAsync<DbUpdateException>(async () =>
        {
            await using var db = scope.CreateDbContext();
            await new PurchaseService(db, new FixedTimeProvider(Now)).PurchaseAsync(
                seed.UserId,
                new PurchaseRequest
                {
                    ItemId = seed.FirstItemId,
                    IdempotencyKey = "forced-rollback"
                });
        });

        await using var verify = scope.CreateDbContext();
        Assert.Equal(500, (await verify.Wallets.AsNoTracking().SingleAsync()).Balance);
        Assert.Empty(await verify.WalletTransactions.AsNoTracking().ToListAsync());
        Assert.Empty(await verify.PurchaseTransactions.AsNoTracking().ToListAsync());
        Assert.Empty(await verify.InventoryItems.AsNoTracking().ToListAsync());
    }

    [Fact, Trait("Category", "M4PurchasePostgreSqlIntegration")]
    public async Task ConcurrentIdenticalIdempotencyRequestsDebitOnceAndReplayOnce()
    {
        await using var scope = await PostgresScope.CreateAsync();
        var seed = await scope.SeedPurchaseAsync();

        var results = await Task.WhenAll(
            ProcessPurchaseAsync(scope, seed.UserId, seed.FirstItemId, "same-key"),
            ProcessPurchaseAsync(scope, seed.UserId, seed.FirstItemId, "same-key"));

        Assert.All(results, result => Assert.True(result.IsSuccess));
        Assert.Single(results, result => !result.Data!.IsReplay);
        Assert.Single(results, result => result.Data!.IsReplay);
        await using var verify = scope.CreateDbContext();
        Assert.Equal(400, (await verify.Wallets.AsNoTracking().SingleAsync()).Balance);
        Assert.Equal(1, await verify.WalletTransactions.CountAsync());
        Assert.Equal(1, await verify.PurchaseTransactions.CountAsync());
        Assert.Equal(1, await verify.InventoryItems.CountAsync());
    }

    [Fact, Trait("Category", "M4PurchasePostgreSqlIntegration")]
    public async Task ConcurrentDifferentKeysForSameItemGrantAndDebitOnce()
    {
        await using var scope = await PostgresScope.CreateAsync();
        var seed = await scope.SeedPurchaseAsync();

        var results = await Task.WhenAll(
            ProcessPurchaseAsync(scope, seed.UserId, seed.FirstItemId, "same-item-a"),
            ProcessPurchaseAsync(scope, seed.UserId, seed.FirstItemId, "same-item-b"));

        Assert.Single(results, result => result.IsSuccess);
        var rejected = Assert.Single(results, result => !result.IsSuccess);
        Assert.Equal(ErrorCodes.ShopItemAlreadyOwned, rejected.ErrorCode);
        await using var verify = scope.CreateDbContext();
        Assert.Equal(400, (await verify.Wallets.AsNoTracking().SingleAsync()).Balance);
        Assert.Equal(1, await verify.PurchaseTransactions.CountAsync());
        Assert.Equal(1, await verify.InventoryItems.CountAsync());
    }

    [Fact, Trait("Category", "M4PurchasePostgreSqlIntegration")]
    public async Task ConcurrentDifferentItemsOnSameWalletDoNotLoseDebits()
    {
        await using var scope = await PostgresScope.CreateAsync();
        var seed = await scope.SeedPurchaseAsync();

        var results = await Task.WhenAll(
            ProcessPurchaseAsync(scope, seed.UserId, seed.FirstItemId, "different-item-a"),
            ProcessPurchaseAsync(scope, seed.UserId, seed.SecondItemId, "different-item-b"));

        Assert.All(results, result => Assert.True(result.IsSuccess));
        await using var verify = scope.CreateDbContext();
        Assert.Equal(250, (await verify.Wallets.AsNoTracking().SingleAsync()).Balance);
        Assert.Equal(2, await verify.PurchaseTransactions.CountAsync());
        Assert.Equal(2, await verify.InventoryItems.CountAsync());
    }

    [Fact, Trait("Category", "M4PurchasePostgreSqlIntegration")]
    public async Task ConcurrentRewardCreditAndPurchaseDebitDoNotLoseWalletUpdate()
    {
        await using var scope = await PostgresScope.CreateAsync();
        var rewardSeed = await scope.SeedRewardMatchAsync();
        var itemId = await scope.SeedShopItemAsync(30);

        var rewardTask = ProcessRewardAsync(scope, rewardSeed.MatchId);
        var purchaseTask = ProcessPurchaseAsync(
            scope, rewardSeed.HostId, itemId, "reward-purchase-race");
        await Task.WhenAll(rewardTask, purchaseTask);

        var rewardResult = await rewardTask;
        var purchaseResult = await purchaseTask;

        Assert.True(rewardResult.IsSuccess);
        Assert.True(purchaseResult.IsSuccess);
        await using var verify = scope.CreateDbContext();
        var hostWallet = await verify.Wallets.AsNoTracking().SingleAsync(
            wallet => wallet.UserId == rewardSeed.HostId);
        Assert.Equal(95, hostWallet.Balance);
        Assert.Equal(1, await verify.PurchaseTransactions.CountAsync());
        Assert.Equal(1, await verify.InventoryItems.CountAsync());
        Assert.Equal(3, await verify.WalletTransactions.CountAsync());
    }

    [Fact, Trait("Category", "M4PurchasePostgreSqlIntegration")]
    public async Task PurchaseDatabaseUniqueConstraintsRejectDuplicateOwnershipAndKey()
    {
        await using var scope = await PostgresScope.CreateAsync();
        var seed = await scope.SeedPurchaseAsync();
        var first = await ProcessPurchaseAsync(
            scope, seed.UserId, seed.FirstItemId, "database-unique-key");
        Assert.True(first.IsSuccess);

        await using (var inventoryDb = scope.CreateDbContext())
        {
            inventoryDb.InventoryItems.Add(new InventoryItem
            {
                InventoryItemId = Guid.NewGuid(),
                UserId = seed.UserId,
                ShopItemId = seed.FirstItemId,
                Source = InventoryAcquisitionSource.PURCHASE,
                AcquiredAtUtc = Now.UtcDateTime
            });
            var exception = await Assert.ThrowsAsync<DbUpdateException>(
                () => inventoryDb.SaveChangesAsync());
            Assert.Equal(
                PostgresErrorCodes.UniqueViolation,
                Assert.IsType<PostgresException>(exception.InnerException).SqlState);
        }

        await using (var purchaseDb = scope.CreateDbContext())
        {
            var wallet = await purchaseDb.Wallets.SingleAsync();
            var walletTransactionId = Guid.NewGuid();
            var purchaseId = Guid.NewGuid();
            purchaseDb.WalletTransactions.Add(new WalletTransaction
            {
                Id = walletTransactionId,
                WalletId = wallet.Id,
                Type = WalletTransactionType.PURCHASE,
                Amount = 0,
                BalanceBefore = wallet.Balance,
                BalanceAfter = wallet.Balance,
                ReferenceId = purchaseId,
                Description = "duplicate idempotency constraint test",
                CreatedAtUtc = Now.UtcDateTime
            });
            purchaseDb.PurchaseTransactions.Add(new PurchaseTransaction
            {
                PurchaseId = purchaseId,
                UserId = seed.UserId,
                ShopItemId = seed.SecondItemId,
                IdempotencyKey = "database-unique-key",
                PriceAtPurchase = 0,
                WalletTransactionId = walletTransactionId,
                Status = PurchaseTransactionStatus.COMPLETED,
                CreatedAtUtc = Now.UtcDateTime
            });
            var exception = await Assert.ThrowsAsync<DbUpdateException>(
                () => purchaseDb.SaveChangesAsync());
            Assert.Equal(
                PostgresErrorCodes.UniqueViolation,
                Assert.IsType<PostgresException>(exception.InnerException).SqlState);
        }
    }

    [Fact, Trait("Category", "M4ShopPostgreSqlIntegration")]
    public async Task ShopCatalogDatabaseEnforcesItemIdAndNonNegativePrice()
    {
        await using var scope = await PostgresScope.CreateAsync();
        var itemId = Guid.NewGuid();
        await using (var validDb = scope.CreateDbContext())
        {
            validDb.ShopItems.Add(CreateShopItem(itemId, 100));
            await validDb.SaveChangesAsync();
        }

        await using (var duplicateDb = scope.CreateDbContext())
        {
            duplicateDb.ShopItems.Add(CreateShopItem(itemId, 200));
            var exception = await Assert.ThrowsAsync<DbUpdateException>(
                () => duplicateDb.SaveChangesAsync());
            Assert.Equal(
                PostgresErrorCodes.UniqueViolation,
                Assert.IsType<PostgresException>(exception.InnerException).SqlState);
        }

        await using (var invalidPriceDb = scope.CreateDbContext())
        {
            invalidPriceDb.ShopItems.Add(CreateShopItem(Guid.NewGuid(), -1));
            var exception = await Assert.ThrowsAsync<DbUpdateException>(
                () => invalidPriceDb.SaveChangesAsync());
            Assert.Equal(
                PostgresErrorCodes.CheckViolation,
                Assert.IsType<PostgresException>(exception.InnerException).SqlState);
        }
    }

    [Fact, Trait("Category", "M4ProfilePostgreSqlIntegration")]
    public async Task ExistingPlayerProfileMigrationPreservesRowAndAddsSafeDefaults()
    {
        const string rewardMigration = "20260920102130_AddRewardWalletLedgerM4010";
        await using var scope = await PostgresScope.CreateAsync(rewardMigration);
        var userId = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        await using (var beforeMigration = scope.CreateDbContext())
        {
            await beforeMigration.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "Users"
                    ("Id", "Email", "Username", "PasswordHash", "Role", "Status", "CreatedAt", "UpdatedAt")
                VALUES
                    ({userId}, {"existing@echo.invalid"}, {"existing-user"}, {"not-used"}, {"PLAYER"}, {"ACTIVE"}, {Now.UtcDateTime}, {Now.UtcDateTime})
                """);
            await beforeMigration.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "PlayerProfiles"
                    ("Id", "UserId", "DisplayName", "TotalMatches", "TotalWins", "CreatedAt", "UpdatedAt")
                VALUES
                    ({profileId}, {userId}, {"Existing Player"}, {7}, {3}, {Now.UtcDateTime}, {Now.UtcDateTime})
                """);
            await beforeMigration.GetService<IMigrator>().MigrateAsync();
        }

        await using var verify = scope.CreateDbContext();
        var profile = await verify.PlayerProfiles.AsNoTracking().SingleAsync();
        Assert.Equal(profileId, profile.Id);
        Assert.Equal(7, profile.TotalMatches);
        Assert.Equal(3, profile.TotalWins);
        Assert.Equal(0, profile.ExperiencePoints);
        Assert.Equal(1, profile.Level);
    }

    [Fact, Trait("Category", "M4RewardPostgreSqlIntegration")]
    public async Task ConcurrentRewardRequestsCreditEachWalletExactlyOnce()
    {
        await using var scope = await PostgresScope.CreateAsync();
        var seed = await scope.SeedRewardMatchAsync();

        var results = await Task.WhenAll(
            ProcessRewardAsync(scope, seed.MatchId),
            ProcessRewardAsync(scope, seed.MatchId));

        Assert.All(results, result => Assert.True(result.IsSuccess));
        Assert.Single(results, result => !result.Data!.IsReplay);
        Assert.Single(results, result => result.Data!.IsReplay);
        await using var verify = scope.CreateDbContext();
        Assert.Equal(2, await verify.MatchRewardGrants.CountAsync());
        Assert.Equal(2, await verify.WalletTransactions.CountAsync());
        Assert.All(await verify.Wallets.AsNoTracking().ToListAsync(), wallet =>
            Assert.Equal(125, wallet.Balance));
        Assert.All(await verify.PlayerProfiles.AsNoTracking().ToListAsync(), profile =>
        {
            Assert.Equal(1, profile.TotalMatches);
            Assert.Equal(1, profile.TotalWins);
            Assert.Equal(10, profile.ExperiencePoints);
            Assert.Equal(2, profile.Level);
        });
        Assert.Equal(
            MatchRewardStatus.Completed,
            (await verify.MatchResults.AsNoTracking().SingleAsync()).RewardStatus);
    }

    [Fact, Trait("Category", "M4RewardPostgreSqlIntegration")]
    public async Task ConcurrentDifferentMatchesDoNotLoseWalletUpdates()
    {
        await using var scope = await PostgresScope.CreateAsync();
        var first = await scope.SeedRewardMatchAsync();
        var second = await scope.SeedAdditionalRewardMatchAsync(first);

        var results = await Task.WhenAll(
            ProcessRewardAsync(scope, first.MatchId),
            ProcessRewardAsync(scope, second.MatchId));

        Assert.All(results, result =>
        {
            Assert.True(result.IsSuccess);
            Assert.False(result.Data!.IsReplay);
        });
        await using var verify = scope.CreateDbContext();
        Assert.Equal(4, await verify.MatchRewardGrants.CountAsync());
        Assert.Equal(4, await verify.WalletTransactions.CountAsync());
        Assert.All(await verify.Wallets.AsNoTracking().ToListAsync(), wallet =>
            Assert.Equal(150, wallet.Balance));
        Assert.All(await verify.PlayerProfiles.AsNoTracking().ToListAsync(), profile =>
        {
            Assert.Equal(2, profile.TotalMatches);
            Assert.Equal(2, profile.TotalWins);
            Assert.Equal(20, profile.ExperiencePoints);
        });
    }

    [Fact, Trait("Category", "M4RewardPostgreSqlIntegration")]
    public async Task LedgerConflictRollsBackGrantBalanceAndRewardStatus()
    {
        await using var scope = await PostgresScope.CreateAsync();
        var seed = await scope.SeedRewardMatchAsync();
        await using (var arrange = scope.CreateDbContext())
        {
            var wallet = await arrange.Wallets.SingleAsync(item => item.UserId == seed.HostId);
            arrange.WalletTransactions.Add(new WalletTransaction
            {
                Id = Guid.NewGuid(),
                WalletId = wallet.Id,
                Type = WalletTransactionType.MATCH_REWARD,
                Amount = 0,
                BalanceBefore = wallet.Balance,
                BalanceAfter = wallet.Balance,
                ReferenceId = seed.MatchId,
                Description = "pre-existing conflict",
                CreatedAtUtc = Now.UtcDateTime
            });
            await arrange.SaveChangesAsync();
        }

        var result = await ProcessRewardAsync(scope, seed.MatchId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.RewardConflict, result.ErrorCode);
        await using var verify = scope.CreateDbContext();
        Assert.Empty(await verify.MatchRewardGrants.AsNoTracking().ToListAsync());
        Assert.Single(await verify.WalletTransactions.AsNoTracking().ToListAsync());
        Assert.All(await verify.Wallets.AsNoTracking().ToListAsync(), wallet =>
            Assert.Equal(100, wallet.Balance));
        Assert.All(await verify.PlayerProfiles.AsNoTracking().ToListAsync(), profile =>
        {
            Assert.Equal(0, profile.TotalMatches);
            Assert.Equal(0, profile.ExperiencePoints);
            Assert.Equal(1, profile.Level);
        });
        Assert.Equal(
            MatchRewardStatus.Pending,
            (await verify.MatchResults.AsNoTracking().SingleAsync()).RewardStatus);
    }

    [Fact, Trait("Category", "M4PostgreSqlIntegration")]
    public async Task MatchResultPrimaryKeyRejectsSecondRowForSameMatch()
    {
        await using var scope = await PostgresScope.CreateAsync();
        var seed = await scope.SeedMatchAsync();
        await using (var firstDb = scope.CreateDbContext())
        {
            var service = new MatchResultService(firstDb, new FixedTimeProvider(Now));
            var first = await service.SubmitAsync(
                seed.HostId, seed.MatchId, CreateRequest(seed), CancellationToken.None);
            Assert.True(first.IsSuccess);
        }

        await using var secondDb = scope.CreateDbContext();
        secondDb.MatchResults.Add(CreateStoredResult(seed));
        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => secondDb.SaveChangesAsync());
        var postgres = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgres.SqlState);
    }

    [Fact, Trait("Category", "M4PostgreSqlIntegration")]
    public async Task MatchResultPlayerForeignKeyRejectsUnboundUser()
    {
        await using var scope = await PostgresScope.CreateAsync();
        var seed = await scope.SeedMatchAsync();
        await using var db = scope.CreateDbContext();
        var result = CreateStoredResult(seed);
        result.Players.Add(new MatchResultPlayer
        {
            MatchId = seed.MatchId,
            UserId = Guid.NewGuid(),
            Survived = true
        });
        db.MatchResults.Add(result);

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        var postgres = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, postgres.SqlState);
    }

    [Fact, Trait("Category", "M4PostgreSqlIntegration")]
    public async Task PostgreSqlCheckConstraintsRejectInvalidOutcomeAndGameplayStats()
    {
        await using var scope = await PostgresScope.CreateAsync();
        var seed = await scope.SeedMatchAsync();
        await using (var outcomeDb = scope.CreateDbContext())
        {
            var invalidOutcome = CreateStoredResult(seed);
            invalidOutcome.Outcome = (MatchOutcome)999;
            outcomeDb.MatchResults.Add(invalidOutcome);
            var exception = await Assert.ThrowsAsync<DbUpdateException>(
                () => outcomeDb.SaveChangesAsync());
            Assert.Equal(
                PostgresErrorCodes.CheckViolation,
                Assert.IsType<PostgresException>(exception.InnerException).SqlState);
        }

        await using (var statsDb = scope.CreateDbContext())
        {
            var invalidStats = CreateStoredResult(seed);
            invalidStats.Players.Add(new MatchResultPlayer
            {
                MatchId = seed.MatchId,
                UserId = seed.HostId,
                Survived = true,
                DetectionCount = -1
            });
            statsDb.MatchResults.Add(invalidStats);
            var exception = await Assert.ThrowsAsync<DbUpdateException>(
                () => statsDb.SaveChangesAsync());
            Assert.Equal(
                PostgresErrorCodes.CheckViolation,
                Assert.IsType<PostgresException>(exception.InnerException).SqlState);
        }
    }

    [Fact, Trait("Category", "M4PostgreSqlIntegration")]
    public async Task PostgreSqlTransactionRollbackLeavesMatchUnchanged()
    {
        await using var scope = await PostgresScope.CreateAsync();
        var seed = await scope.SeedMatchAsync();
        await using (var db = scope.CreateDbContext())
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            var match = await db.MatchAuthorityBindings.SingleAsync(
                item => item.MatchId == seed.MatchId);
            match.Status = MatchAuthorityStatus.Ended;
            match.EndedAtUtc = Now.UtcDateTime;
            db.MatchResults.Add(CreateStoredResult(seed));
            await db.SaveChangesAsync();
            await transaction.RollbackAsync();
        }

        await using var verify = scope.CreateDbContext();
        Assert.Empty(verify.MatchResults);
        var storedMatch = await verify.MatchAuthorityBindings.AsNoTracking().SingleAsync();
        Assert.Equal(MatchAuthorityStatus.InMatch, storedMatch.Status);
        Assert.Null(storedMatch.EndedAtUtc);
    }

    [Fact, Trait("Category", "M4PostgreSqlIntegration")]
    public async Task IdenticalConcurrentSubmissionsPersistOnceAndReplayOnce()
    {
        await using var scope = await PostgresScope.CreateAsync();
        var seed = await scope.SeedMatchAsync();

        var results = await Task.WhenAll(
            SubmitAsync(scope, seed, CreateRequest(seed)),
            SubmitAsync(scope, seed, CreateRequest(seed)));

        Assert.All(results, result => Assert.True(result.IsSuccess));
        Assert.Single(results, result => !result.Data!.IsReplay);
        Assert.Single(results, result => result.Data!.IsReplay);
        await using var verify = scope.CreateDbContext();
        Assert.Equal(1, await verify.MatchResults.CountAsync());
        Assert.Equal(2, await verify.MatchResultPlayers.CountAsync());
    }

    [Fact, Trait("Category", "M4PostgreSqlIntegration")]
    public async Task ConflictingConcurrentSubmissionsAcceptOneAndConflictOne()
    {
        await using var scope = await PostgresScope.CreateAsync();
        var seed = await scope.SeedMatchAsync();
        var win = CreateRequest(seed);
        var lose = CreateRequest(seed);
        lose.Outcome = MatchOutcome.LOSE;
        lose.ObjectiveCompletion = 0.5m;

        var results = await Task.WhenAll(
            SubmitAsync(scope, seed, win),
            SubmitAsync(scope, seed, lose));

        Assert.Single(results, result => result.IsSuccess);
        var conflict = Assert.Single(results, result => !result.IsSuccess);
        Assert.Equal(ErrorCodes.MatchResultConflict, conflict.ErrorCode);
        await using var verify = scope.CreateDbContext();
        Assert.Equal(1, await verify.MatchResults.CountAsync());
    }

    [Fact, Trait("Category", "M4PostgreSqlIntegration")]
    public async Task ControllerSubmissionReturns201AndPersistsInPostgreSql()
    {
        await using var scope = await PostgresScope.CreateAsync();
        var seed = await scope.SeedMatchAsync();
        await using var db = scope.CreateDbContext();
        var controller = new MatchResultsController(
            new MatchResultService(db, new FixedTimeProvider(Now)))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, seed.HostId.ToString())],
                        "Test"))
                }
            }
        };

        var action = await controller.Submit(
            seed.MatchId, CreateRequest(seed), CancellationToken.None);

        var response = Assert.IsType<ObjectResult>(action);
        Assert.Equal(StatusCodes.Status201Created, response.StatusCode);
        await using var verify = scope.CreateDbContext();
        var stored = await verify.MatchResults.AsNoTracking()
            .Include(item => item.Players)
            .SingleAsync(item => item.MatchId == seed.MatchId);
        Assert.Equal(MatchRewardStatus.Pending, stored.RewardStatus);
        Assert.Equal(2, stored.Players.Count);
        Assert.Equal(MatchAuthorityStatus.Ended,
            (await verify.MatchAuthorityBindings.AsNoTracking().SingleAsync()).Status);
    }

    [Fact, Trait("Category", "M4PaymentOrderPostgreSqlIntegration")]
    public async Task PaymentOrderDatabaseRejectsDuplicateUserIdAndIdempotencyKey()
    {
        await using var scope = await PostgresScope.CreateAsync();
        var seed = await scope.SeedPurchaseAsync();
        await using var db = scope.CreateDbContext();
        db.PaymentOrders.AddRange(
            DirectPaymentOrder(seed.UserId, "duplicate-key", "A"),
            DirectPaymentOrder(seed.UserId, "duplicate-key", "B"));

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact, Trait("Category", "M4PaymentOrderPostgreSqlIntegration")]
    public async Task ConcurrentIdenticalPaymentOrderCreationConvergesToOneOrder()
    {
        await using var scope = await PostgresScope.CreateAsync();
        var seed = await scope.SeedPurchaseAsync();

        var results = await Task.WhenAll(
            ProcessPaymentCreateAsync(scope, seed.UserId, "concurrent-identical", "TEST_PRODUCT"),
            ProcessPaymentCreateAsync(scope, seed.UserId, "concurrent-identical", "TEST_PRODUCT"));

        Assert.All(results, result => Assert.True(result.IsSuccess));
        Assert.Single(results, result => result.Data!.IsReplay);
        Assert.Single(results, result => !result.Data!.IsReplay);
        await using var verify = scope.CreateDbContext();
        Assert.Equal(1, await verify.PaymentOrders.CountAsync());
    }

    [Fact, Trait("Category", "M4PaymentOrderPostgreSqlIntegration")]
    public async Task ConcurrentConflictingPaymentOrderCreationAcceptsOneAndConflictsOne()
    {
        await using var scope = await PostgresScope.CreateAsync();
        var seed = await scope.SeedPurchaseAsync();

        var results = await Task.WhenAll(
            ProcessPaymentCreateAsync(scope, seed.UserId, "concurrent-conflict", "TEST_PRODUCT"),
            ProcessPaymentCreateAsync(scope, seed.UserId, "concurrent-conflict", "SECOND_TEST_PRODUCT"));

        Assert.Single(results, result => result.IsSuccess);
        Assert.Equal(ErrorCodes.PaymentIdempotencyConflict,
            Assert.Single(results, result => !result.IsSuccess).ErrorCode);
        await using var verify = scope.CreateDbContext();
        Assert.Equal(1, await verify.PaymentOrders.CountAsync());
    }

    [Fact, Trait("Category", "M4PaymentOrderPostgreSqlIntegration")]
    public async Task PaymentOrderDatabaseRejectsInvalidAmountAndStatus()
    {
        await using var scope = await PostgresScope.CreateAsync();
        var seed = await scope.SeedPurchaseAsync();
        await using (var amountDb = scope.CreateDbContext())
        {
            var invalidAmount = DirectPaymentOrder(seed.UserId, "invalid-amount", "A");
            invalidAmount.Amount = 0;
            amountDb.PaymentOrders.Add(invalidAmount);
            await Assert.ThrowsAsync<DbUpdateException>(() => amountDb.SaveChangesAsync());
        }

        await using (var statusDb = scope.CreateDbContext())
        {
            var invalidStatus = DirectPaymentOrder(seed.UserId, "invalid-status", "B");
            invalidStatus.Status = (PaymentOrderStatus)999;
            statusDb.PaymentOrders.Add(invalidStatus);
            await Assert.ThrowsAsync<DbUpdateException>(() => statusDb.SaveChangesAsync());
        }
    }

    [Fact, Trait("Category", "M4PaymentOrderPostgreSqlIntegration")]
    public async Task FailedPaymentTransitionRollsBackStatusAndProviderMetadata()
    {
        await using var scope = await PostgresScope.CreateAsync();
        var seed = await scope.SeedPurchaseAsync();
        var created = await ProcessPaymentCreateAsync(
            scope, seed.UserId, "transition-rollback", "TEST_PRODUCT");

        await using (var setup = scope.CreateDbContext())
        {
            await setup.Database.ExecuteSqlRawAsync("""
                CREATE FUNCTION reject_pending_payment_order() RETURNS trigger AS $$
                BEGIN
                    IF NEW."Status" = 'PENDING_PAYMENT' THEN
                        RAISE EXCEPTION 'forced payment transition failure' USING ERRCODE = '23514';
                    END IF;
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;
                CREATE TRIGGER reject_pending_payment_order_trigger
                BEFORE UPDATE ON "PaymentOrders"
                FOR EACH ROW EXECUTE FUNCTION reject_pending_payment_order();
                """);
        }

        await Assert.ThrowsAsync<DbUpdateException>(() => ProcessPaymentTransitionAsync(
            scope,
            created.Data!.PaymentOrderId,
            new PaymentOrderTransition(
                PaymentOrderStatus.PENDING_PAYMENT,
                ProviderOrderId: "must-roll-back")));

        await using var verify = scope.CreateDbContext();
        var stored = await verify.PaymentOrders.AsNoTracking().SingleAsync();
        Assert.Equal(PaymentOrderStatus.CREATED, stored.Status);
        Assert.Null(stored.ProviderOrderId);
        Assert.Null(stored.PaidAtUtc);
    }

    [Fact, Trait("Category", "M4PaymentProcessingPostgreSqlIntegration")]
    public async Task PaymentProviderEventIdentityIsDatabaseUnique()
    {
        await using var scope = await PostgresScope.CreateAsync();
        await using var db = scope.CreateDbContext();
        db.PaymentProviderEvents.AddRange(
            DirectProviderEvent("duplicate-provider-event", "A"),
            DirectProviderEvent("duplicate-provider-event", "B"));

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact, Trait("Category", "M4PaymentProcessingPostgreSqlIntegration")]
    public async Task ConcurrentCheckoutRetriesUseOneStableProviderOrder()
    {
        await using var scope = await PostgresScope.CreateAsync();
        var seed = await SeedCheckoutPaymentAsync(scope);
        var provider = new IdempotentCheckoutProvider();

        var results = await Task.WhenAll(
            ProcessCheckoutAsync(scope, seed, provider),
            ProcessCheckoutAsync(scope, seed, provider));

        Assert.All(results, result => Assert.True(result.IsSuccess));
        Assert.Equal(1, provider.UniqueOrderCount);
        await using var verify = scope.CreateDbContext();
        Assert.Equal(1, await verify.PaymentCheckouts.CountAsync());
        Assert.Equal(PaymentOrderStatus.PENDING_PAYMENT,
            (await verify.PaymentOrders.SingleAsync()).Status);
    }

    [Fact, Trait("Category", "M4PaymentProcessingPostgreSqlIntegration")]
    public async Task ConcurrentIdenticalWebhooksPayAndFulfillExactlyOnce()
    {
        await using var scope = await PostgresScope.CreateAsync();
        var seed = await SeedProcessingPaymentAsync(scope, PaymentOrderStatus.PENDING_PAYMENT);
        var paymentEvent = ProcessingEvent("concurrent-webhook", seed.ProviderOrderId);

        var results = await Task.WhenAll(
            ProcessWebhookAsync(scope, seed, paymentEvent),
            ProcessWebhookAsync(scope, seed, paymentEvent));

        Assert.All(results, result => Assert.True(result.IsSuccess));
        await using var verify = scope.CreateDbContext();
        Assert.Equal(1, await verify.PaymentProviderEvents.CountAsync());
        Assert.Equal(1, await verify.PaymentFulfillments.CountAsync());
        Assert.Equal(1, await verify.WalletTransactions.CountAsync());
        Assert.Equal(250, (await verify.Wallets.SingleAsync()).Balance);
        Assert.Equal(PaymentOrderStatus.FULFILLED,
            (await verify.PaymentOrders.SingleAsync()).Status);
    }

    [Fact, Trait("Category", "M4PaymentProcessingPostgreSqlIntegration")]
    public async Task ConcurrentFulfillmentAttemptsGrantWalletExactlyOnce()
    {
        await using var scope = await PostgresScope.CreateAsync();
        var seed = await SeedProcessingPaymentAsync(scope, PaymentOrderStatus.PAID);

        var results = await Task.WhenAll(
            ProcessFulfillmentAsync(scope, seed, PaymentCatalogFor(seed)),
            ProcessFulfillmentAsync(scope, seed, PaymentCatalogFor(seed)));

        Assert.All(results, result => Assert.True(result.IsSuccess));
        Assert.Single(results, result => result.Data!.IsReplay);
        await using var verify = scope.CreateDbContext();
        Assert.Equal(1, await verify.PaymentFulfillments.CountAsync());
        Assert.Equal(1, await verify.WalletTransactions.CountAsync());
        Assert.Equal(250, (await verify.Wallets.SingleAsync()).Balance);
    }

    [Fact, Trait("Category", "M4PaymentProcessingPostgreSqlIntegration")]
    public async Task InventoryFulfillmentPersistsGrantAndOrderAtomically()
    {
        await using var scope = await PostgresScope.CreateAsync();
        var seed = await SeedProcessingPaymentAsync(
            scope, PaymentOrderStatus.PAID, includeInventoryTarget: true);

        var result = await ProcessFulfillmentAsync(
            scope, seed, PaymentCatalogFor(seed, inventory: true));

        Assert.True(result.IsSuccess);
        await using var verify = scope.CreateDbContext();
        Assert.Equal(1, await verify.InventoryItems.CountAsync());
        Assert.Equal(1, await verify.PaymentFulfillments.CountAsync());
        Assert.Equal(PaymentOrderStatus.FULFILLED,
            (await verify.PaymentOrders.SingleAsync()).Status);
    }

    [Fact, Trait("Category", "M4PaymentProcessingPostgreSqlIntegration")]
    public async Task FulfillmentDatabaseFailureRollsBackWalletGrantAndOrderState()
    {
        await using var scope = await PostgresScope.CreateAsync();
        var seed = await SeedProcessingPaymentAsync(scope, PaymentOrderStatus.PAID);
        await using (var setup = scope.CreateDbContext())
        {
            await setup.Database.ExecuteSqlRawAsync("""
                CREATE FUNCTION reject_payment_fulfillment_ledger() RETURNS trigger AS $$
                BEGIN
                    IF NEW."Type" = 'PAYMENT_FULFILLMENT' THEN
                        RAISE EXCEPTION 'forced payment fulfillment failure' USING ERRCODE = '23514';
                    END IF;
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;
                CREATE TRIGGER reject_payment_fulfillment_ledger_trigger
                BEFORE INSERT ON "WalletTransactions"
                FOR EACH ROW EXECUTE FUNCTION reject_payment_fulfillment_ledger();
                """);
        }

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            ProcessFulfillmentAsync(scope, seed, PaymentCatalogFor(seed)));

        await using var verify = scope.CreateDbContext();
        Assert.Equal(PaymentOrderStatus.PAID,
            (await verify.PaymentOrders.SingleAsync()).Status);
        Assert.Equal(100, (await verify.Wallets.SingleAsync()).Balance);
        Assert.Empty(await verify.WalletTransactions.ToListAsync());
        Assert.Empty(await verify.PaymentFulfillments.ToListAsync());
    }

    [Fact, Trait("Category", "M4LoadoutPostgreSqlIntegration")]
    public async Task LoadoutMigrationAppliesToRealPostgreSql()
    {
        await using var scope = await PostgresScope.CreateAsync();
        await using var db = scope.CreateDbContext();
        var applied = await db.Database.GetAppliedMigrationsAsync();
        Assert.Contains("20260922090000_AddPlayerLoadoutM4052", applied);
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
    }

    [Fact, Trait("Category", "M4LoadoutPostgreSqlIntegration")]
    public async Task LoadoutUniqueUserAndSlotConstraintRejectsDuplicate()
    {
        await using var scope = await PostgresScope.CreateAsync();
        var seed = await scope.SeedLoadoutAsync();
        await using var db = scope.CreateDbContext();
        var now = Now.UtcDateTime;
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "PlayerLoadoutItems"
                ("UserId", "SlotId", "InventoryItemId", "EquippedAtUtc", "UpdatedAtUtc")
            VALUES ({seed.UserId}, 'CHARACTER', {seed.CharacterOneInventoryId}, {now}, {now})
            """);

        var exception = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "PlayerLoadoutItems"
                    ("UserId", "SlotId", "InventoryItemId", "EquippedAtUtc", "UpdatedAtUtc")
                VALUES ({seed.UserId}, 'CHARACTER', {seed.CharacterTwoInventoryId}, {now}, {now})
                """));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, exception.SqlState);
    }

    [Fact, Trait("Category", "M4LoadoutPostgreSqlIntegration")]
    public async Task CharacterReplacementIsAtomic()
    {
        await using var scope = await PostgresScope.CreateAsync();
        var seed = await scope.SeedLoadoutAsync();
        Assert.True((await EquipLoadoutAsync(
            scope, seed.UserId, "CHARACTER", seed.CharacterOneId)).IsSuccess);
        Assert.True((await EquipLoadoutAsync(
            scope, seed.UserId, "CHARACTER", seed.CharacterTwoId)).IsSuccess);

        await using var verify = scope.CreateDbContext();
        var row = Assert.Single(await verify.PlayerLoadoutItems.ToArrayAsync());
        Assert.Equal(seed.CharacterTwoInventoryId, row.InventoryItemId);
    }

    [Fact, Trait("Category", "M4LoadoutPostgreSqlIntegration")]
    public async Task ConcurrentEquipSameSlotLeavesExactlyOneValidItem()
    {
        await using var scope = await PostgresScope.CreateAsync();
        var seed = await scope.SeedLoadoutAsync();

        var results = await Task.WhenAll(
            EquipLoadoutAsync(scope, seed.UserId, "CHARACTER", seed.CharacterOneId),
            EquipLoadoutAsync(scope, seed.UserId, "CHARACTER", seed.CharacterTwoId));

        Assert.All(results, result => Assert.True(result.IsSuccess));
        await using var verify = scope.CreateDbContext();
        var row = Assert.Single(await verify.PlayerLoadoutItems.ToArrayAsync());
        Assert.Contains(row.InventoryItemId,
            new[] { seed.CharacterOneInventoryId, seed.CharacterTwoInventoryId });
    }

    [Fact, Trait("Category", "M4LoadoutPostgreSqlIntegration")]
    public async Task InventoryOwnershipForeignKeyRejectsCrossUserLoadout()
    {
        await using var scope = await PostgresScope.CreateAsync();
        var seed = await scope.SeedLoadoutAsync();
        await using var db = scope.CreateDbContext();
        var now = Now.UtcDateTime;
        var exception = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "PlayerLoadoutItems"
                    ("UserId", "SlotId", "InventoryItemId", "EquippedAtUtc", "UpdatedAtUtc")
                VALUES ({seed.OtherUserId}, 'CHARACTER', {seed.CharacterOneInventoryId}, {now}, {now})
                """));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, exception.SqlState);
    }

    [Fact, Trait("Category", "M4LoadoutPostgreSqlIntegration")]
    public async Task EquipPersistenceFailureRollsBackEntireChange()
    {
        await using var scope = await PostgresScope.CreateAsync();
        var seed = await scope.SeedLoadoutAsync();
        await using (var setup = scope.CreateDbContext())
        {
            await setup.Database.ExecuteSqlRawAsync("""
                CREATE FUNCTION reject_loadout_write() RETURNS trigger AS $$
                BEGIN
                    RAISE EXCEPTION 'forced loadout failure' USING ERRCODE = '23514';
                END;
                $$ LANGUAGE plpgsql;
                CREATE TRIGGER reject_loadout_write_trigger
                BEFORE INSERT OR UPDATE ON "PlayerLoadoutItems"
                FOR EACH ROW EXECUTE FUNCTION reject_loadout_write();
                """);
        }

        var result = await EquipLoadoutAsync(
            scope, seed.UserId, "CHARACTER", seed.CharacterOneId);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.LoadoutConflict, result.ErrorCode);
        await using var verify = scope.CreateDbContext();
        Assert.Empty(await verify.PlayerLoadoutItems.ToArrayAsync());
    }

    [Fact, Trait("Category", "M4LoadoutPostgreSqlIntegration")]
    public async Task LoadoutPersistsAfterDbContextReload()
    {
        await using var scope = await PostgresScope.CreateAsync();
        var seed = await scope.SeedLoadoutAsync();
        Assert.True((await EquipLoadoutAsync(
            scope, seed.UserId, "TEAM_TOOL_1", seed.TeamToolId)).IsSuccess);

        await using var reloaded = scope.CreateDbContext();
        var result = await CreateLoadoutService(reloaded).GetCurrentAsync(seed.UserId);
        Assert.Equal(seed.TeamToolId, Assert.Single(result.Data!.TeamTools).ItemId);
    }

    [Fact, Trait("Category", "AdminApiPostgreSqlIntegration")]
    public async Task AdminPaymentPaginationUsesDeterministicTieBreak()
    {
        await using var scope = await PostgresScope.CreateAsync();
        var seed = await SeedAdminQueryAsync(scope);
        await using var db = scope.CreateDbContext();
        var service = new AdminQueryService(db);
        var first = await service.GetPaymentsAsync(new AdminPaymentsQuery
        {
            Page = 1, PageSize = 1
        });
        var second = await service.GetPaymentsAsync(new AdminPaymentsQuery
        {
            Page = 2, PageSize = 1
        });
        var expected = new[] { seed.PaymentOrderId, seed.SecondPaymentOrderId }
            .OrderByDescending(id => id).ToArray();
        Assert.Equal(expected[0], Assert.Single(first.Data!.Items).PaymentOrderId);
        Assert.Equal(expected[1], Assert.Single(second.Data!.Items).PaymentOrderId);
    }

    [Fact, Trait("Category", "AdminApiPostgreSqlIntegration")]
    public async Task AdminPaymentDetailLoadsAggregateRelationships()
    {
        await using var scope = await PostgresScope.CreateAsync();
        var seed = await SeedAdminQueryAsync(scope);
        await using var db = scope.CreateDbContext();
        var detail = await new AdminQueryService(db).GetPaymentAsync(seed.PaymentOrderId);
        Assert.True(detail.IsSuccess);
        Assert.NotNull(detail.Data!.Checkout);
        Assert.Single(detail.Data.ProviderEvents);
        Assert.NotNull(detail.Data.Fulfillment);
        Assert.NotNull(detail.Data.WalletTransaction);
    }

    [Fact, Trait("Category", "AdminApiPostgreSqlIntegration")]
    public async Task AdminFiltersReturnOnlyMatchingRows()
    {
        await using var scope = await PostgresScope.CreateAsync();
        var seed = await SeedAdminQueryAsync(scope);
        await using var db = scope.CreateDbContext();
        var service = new AdminQueryService(db);
        var payments = await service.GetPaymentsAsync(new AdminPaymentsQuery
        {
            Status = PaymentOrderStatus.FULFILLED,
            UserId = seed.UserId,
            ProductReference = "ADMIN_TEST_PRODUCT"
        });
        var wallets = await service.GetWalletTransactionsAsync(
            new AdminWalletTransactionsQuery
            {
                UserId = seed.UserId,
                Type = WalletTransactionType.PAYMENT_FULFILLMENT,
                Reference = seed.PaymentOrderId
            });
        var purchases = await service.GetPurchasesAsync(new AdminPurchasesQuery
        {
            UserId = seed.UserId,
            ShopItemId = seed.ShopItemId
        });
        Assert.Equal(seed.PaymentOrderId, Assert.Single(payments.Data!.Items).PaymentOrderId);
        Assert.Equal(seed.PaymentOrderId, Assert.Single(wallets.Data!.Items).Reference);
        Assert.Equal(seed.ShopItemId, Assert.Single(purchases.Data!.Items).ShopItemId);
    }

    [Fact, Trait("Category", "AdminApiPostgreSqlIntegration")]
    public async Task AdminQueryIndexMigrationIsApplied()
    {
        await using var scope = await PostgresScope.CreateAsync();
        await using var db = scope.CreateDbContext();
        var applied = await db.Database.GetAppliedMigrationsAsync();
        Assert.Contains("20260922110000_AddAdminQueryIndexes", applied);
        var indexCount = await db.Database.SqlQueryRaw<int>("""
            SELECT CAST(count(*) AS integer) AS "Value"
            FROM pg_indexes
            WHERE schemaname = current_schema()
              AND indexname LIKE 'IX_Admin_%'
            """).SingleAsync();
        Assert.Equal(10, indexCount);
    }

    private static async Task<AdminQuerySeed> SeedAdminQueryAsync(PostgresScope scope)
    {
        var userId = Guid.NewGuid();
        var shopItemId = Guid.NewGuid();
        var purchaseId = Guid.NewGuid();
        var purchaseWalletTransactionId = Guid.NewGuid();
        var paymentOrderId = Guid.NewGuid();
        var secondPaymentOrderId = Guid.NewGuid();
        var fulfillmentWalletTransactionId = Guid.NewGuid();
        await using var db = scope.CreateDbContext();
        db.Users.Add(new User
        {
            Id = userId, Email = $"admin-query-{userId:N}@echo.invalid",
            Username = $"admin-query-{userId:N}", PasswordHash = "not-used",
            Role = UserRole.ADMIN, Status = UserStatus.ACTIVE,
            CreatedAt = Now.UtcDateTime, UpdatedAt = Now.UtcDateTime,
            PlayerProfile = new PlayerProfile
            {
                Id = Guid.NewGuid(), DisplayName = "Admin Query User", Level = 1,
                CreatedAt = Now.UtcDateTime, UpdatedAt = Now.UtcDateTime
            },
            Wallet = new Wallet
            {
                Id = Guid.NewGuid(), Balance = 150, UpdatedAt = Now.UtcDateTime,
                Transactions =
                [
                    new WalletTransaction
                    {
                        Id = purchaseWalletTransactionId,
                        Type = WalletTransactionType.PURCHASE,
                        Amount = -50, BalanceBefore = 100, BalanceAfter = 50,
                        ReferenceId = purchaseId, Description = "Admin query purchase",
                        CreatedAtUtc = Now.UtcDateTime
                    },
                    new WalletTransaction
                    {
                        Id = fulfillmentWalletTransactionId,
                        Type = WalletTransactionType.PAYMENT_FULFILLMENT,
                        Amount = 100, BalanceBefore = 50, BalanceAfter = 150,
                        ReferenceId = paymentOrderId, Description = "Admin query fulfillment",
                        CreatedAtUtc = Now.UtcDateTime
                    }
                ]
            },
            PurchaseTransactions =
            [
                new PurchaseTransaction
                {
                    PurchaseId = purchaseId, ShopItemId = shopItemId,
                    IdempotencyKey = "admin-query-purchase", PriceAtPurchase = 50,
                    WalletTransactionId = purchaseWalletTransactionId,
                    Status = PurchaseTransactionStatus.COMPLETED,
                    CreatedAtUtc = Now.UtcDateTime,
                    InventoryItem = new InventoryItem
                    {
                        InventoryItemId = Guid.NewGuid(), UserId = userId,
                        ShopItemId = shopItemId, Source = InventoryAcquisitionSource.PURCHASE,
                        AcquiredAtUtc = Now.UtcDateTime
                    }
                }
            ],
            PaymentOrders =
            [
                new PaymentOrder
                {
                    PaymentOrderId = paymentOrderId, Provider = "PAYOS",
                    ProviderOrderId = $"admin-{paymentOrderId:N}",
                    ProviderTransactionId = $"txn-{paymentOrderId:N}",
                    Purpose = "WALLET_CREDIT", ProductReference = "ADMIN_TEST_PRODUCT",
                    Amount = 100, Currency = "VND", Status = PaymentOrderStatus.FULFILLED,
                    IdempotencyKey = "admin-query-payment-1", RequestFingerprint = new string('D', 64),
                    CreatedAtUtc = Now.UtcDateTime, UpdatedAtUtc = Now.UtcDateTime,
                    PaidAtUtc = Now.UtcDateTime, FulfilledAtUtc = Now.UtcDateTime,
                    FulfillmentReference = $"fulfill-{paymentOrderId:N}",
                    Checkout = new PaymentCheckout
                    {
                        Provider = "PAYOS", ProviderOrderId = $"admin-{paymentOrderId:N}",
                        ProviderPaymentLinkId = $"link-{paymentOrderId:N}",
                        CheckoutUrl = $"https://pay.test/{paymentOrderId:N}",
                        Status = PaymentCheckoutStatus.READY,
                        ReservedAtUtc = Now.UtcDateTime, ReadyAtUtc = Now.UtcDateTime
                    },
                    ProviderEvents =
                    [
                        new PaymentProviderEvent
                        {
                            PaymentProviderEventId = Guid.NewGuid(), Provider = "PAYOS",
                            ProviderEventId = $"event-{paymentOrderId:N}",
                            ProviderOrderId = $"admin-{paymentOrderId:N}", Amount = 100,
                            Currency = "VND", NormalizedStatus = PaymentProviderEventStatus.PAID,
                            SemanticFingerprint = new string('E', 64), VerificationStatus = "VERIFIED",
                            ReceivedAtUtc = Now.UtcDateTime, ProcessedAtUtc = Now.UtcDateTime,
                            ProcessingOutcome = PaymentProviderEventOutcome.FULFILLED
                        }
                    ],
                    Fulfillment = new PaymentFulfillment
                    {
                        FulfillmentReference = $"fulfill-{paymentOrderId:N}",
                        Kind = PaymentFulfillmentKind.WALLET_CREDIT,
                        WalletTransactionId = fulfillmentWalletTransactionId,
                        CompletedAtUtc = Now.UtcDateTime
                    }
                },
                new PaymentOrder
                {
                    PaymentOrderId = secondPaymentOrderId, Provider = "PAYOS",
                    Purpose = "WALLET_CREDIT", ProductReference = "OTHER_PRODUCT",
                    Amount = 200, Currency = "VND", Status = PaymentOrderStatus.CREATED,
                    IdempotencyKey = "admin-query-payment-2", RequestFingerprint = new string('F', 64),
                    CreatedAtUtc = Now.UtcDateTime, UpdatedAtUtc = Now.UtcDateTime
                }
            ]
        });
        db.ShopItems.Add(new ShopItem
        {
            ItemId = shopItemId, ItemName = "Admin Query Character",
            Description = "PostgreSQL admin fixture", Category = "CHARACTER",
            Price = 50, AssetReference = "test://admin-query/character", IsActive = true,
            CreatedAtUtc = Now.UtcDateTime, UpdatedAtUtc = Now.UtcDateTime
        });
        await db.SaveChangesAsync();
        return new AdminQuerySeed(
            userId, paymentOrderId, secondPaymentOrderId, shopItemId);
    }

    private static async Task<ServiceResult<LoadoutItemResponse>> EquipLoadoutAsync(
        PostgresScope scope,
        Guid userId,
        string slotId,
        Guid itemId)
    {
        await using var db = scope.CreateDbContext();
        return await CreateLoadoutService(db).EquipAsync(userId, new EquipLoadoutRequest
        {
            SlotId = slotId,
            ItemId = itemId
        });
    }

    private static LoadoutService CreateLoadoutService(AppDbContext db) => new(
        db,
        new FixedTimeProvider(Now),
        Options.Create(new LoadoutSettings { TeamToolSlotCount = 1 }));

    private static async Task<ServiceResult<MatchResultResponse>> SubmitAsync(
        PostgresScope scope,
        MatchSeed seed,
        SubmitMatchResultRequest request)
    {
        await using var db = scope.CreateDbContext();
        return await new MatchResultService(db, new FixedTimeProvider(Now)).SubmitAsync(
            seed.HostId, seed.MatchId, request, CancellationToken.None);
    }

    private static async Task<ServiceResult<EchoProtocol.Api.DTOs.Rewards.RewardProcessingResponse>>
        ProcessRewardAsync(PostgresScope scope, Guid matchId)
    {
        await using var db = scope.CreateDbContext();
        return await new RewardService(
                db,
                new FixedTestRewardPolicy(),
                new ProgressionService(new FixedTestProgressionPolicy()),
                new FixedTimeProvider(Now))
            .ProcessAsync(matchId, CancellationToken.None);
    }

    private static async Task<ServiceResult<PurchaseResponse>> ProcessPurchaseAsync(
        PostgresScope scope,
        Guid userId,
        Guid itemId,
        string idempotencyKey)
    {
        await using var db = scope.CreateDbContext();
        return await new PurchaseService(db, new FixedTimeProvider(Now)).PurchaseAsync(
            userId,
            new PurchaseRequest
            {
                ItemId = itemId,
                IdempotencyKey = idempotencyKey
            },
            CancellationToken.None);
    }

    private static async Task<ServiceResult<PaymentOrderResponse>> ProcessPaymentCreateAsync(
        PostgresScope scope,
        Guid userId,
        string idempotencyKey,
        string productReference)
    {
        await using var db = scope.CreateDbContext();
        return await CreatePaymentService(db).CreateAsync(
            userId,
            new CreatePaymentOrderRequest
            {
                ProductReference = productReference,
                Provider = "TEST_PROVIDER",
                IdempotencyKey = idempotencyKey
            });
    }

    private static async Task<ServiceResult<PaymentOrderResponse>> ProcessPaymentTransitionAsync(
        PostgresScope scope,
        Guid paymentOrderId,
        PaymentOrderTransition transition)
    {
        await using var db = scope.CreateDbContext();
        return await CreatePaymentService(db).TransitionAsync(paymentOrderId, transition);
    }

    private static PaymentOrderService CreatePaymentService(AppDbContext db) => new(
        db,
        Options.Create(PaymentCatalog()),
        new FixedTimeProvider(Now));

    private static PaymentCatalogSettings PaymentCatalog() => new()
    {
        AllowedProviders = ["TEST_PROVIDER"],
        Products =
        [
            new PaymentProductSettings
            {
                ProductReference = "TEST_PRODUCT", Purpose = "AUTOMATED_TEST_ONLY",
                Amount = 125_000m, Currency = "VND", IsActive = true, ExpiresAfterMinutes = 30
            },
            new PaymentProductSettings
            {
                ProductReference = "SECOND_TEST_PRODUCT", Purpose = "AUTOMATED_TEST_ONLY",
                Amount = 250_000m, Currency = "VND", IsActive = true, ExpiresAfterMinutes = 30
            }
        ]
    };

    private static PaymentOrder DirectPaymentOrder(Guid userId, string key, string discriminator) => new()
    {
        PaymentOrderId = Guid.NewGuid(), UserId = userId, Provider = "TEST_PROVIDER",
        Purpose = "AUTOMATED_TEST_ONLY", ProductReference = "TEST_PRODUCT",
        Amount = 125_000m, Currency = "VND", Status = PaymentOrderStatus.CREATED,
        IdempotencyKey = key, RequestFingerprint = new string(discriminator[0], 64),
        CreatedAtUtc = Now.UtcDateTime, UpdatedAtUtc = Now.UtcDateTime,
        ExpiresAtUtc = Now.AddMinutes(30).UtcDateTime
    };

    private static EchoProtocol.Api.Entities.PaymentProviderEvent DirectProviderEvent(
        string eventId, string discriminator) => new()
    {
        PaymentProviderEventId = Guid.NewGuid(), Provider = "PAYOS",
        ProviderEventId = eventId, ProviderOrderId = "unknown-order", Amount = 125_000,
        Currency = "VND", NormalizedStatus = PaymentProviderEventStatus.PAID,
        SemanticFingerprint = new string(discriminator[0], 64), VerificationStatus = "VERIFIED",
        ReceivedAtUtc = Now.UtcDateTime, ProcessedAtUtc = Now.UtcDateTime,
        ProcessingOutcome = PaymentProviderEventOutcome.UNKNOWN_ORDER
    };

    private static async Task<PaymentProcessingSeed> SeedProcessingPaymentAsync(
        PostgresScope scope,
        PaymentOrderStatus status,
        bool includeInventoryTarget = false)
    {
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var itemId = includeInventoryTarget ? Guid.NewGuid() : (Guid?)null;
        await using var db = scope.CreateDbContext();
        db.Users.Add(CreateScenarioUser(userId, "payment-processing"));
        db.Wallets.Add(new Wallet
        {
            Id = Guid.NewGuid(), UserId = userId, Balance = 100, UpdatedAt = Now.UtcDateTime
        });
        if (itemId is not null)
            db.ShopItems.Add(CreateShopItem(itemId.Value, 0));
        db.PaymentOrders.Add(new PaymentOrder
        {
            PaymentOrderId = orderId, UserId = userId, Provider = "PAYOS",
            ProviderOrderId = "processing-501", Purpose = "AUTOMATED_TEST_ONLY",
            ProductReference = includeInventoryTarget ? "TEST_PAYMENT_ITEM" : "TEST_PAYMENT_WALLET",
            Amount = 125_000, Currency = "VND", Status = status,
            IdempotencyKey = $"processing-{orderId:N}", RequestFingerprint = new string('D', 64),
            CreatedAtUtc = Now.AddMinutes(-5).UtcDateTime, UpdatedAtUtc = Now.UtcDateTime,
            ExpiresAtUtc = Now.AddMinutes(30).UtcDateTime,
            ProviderTransactionId = status == PaymentOrderStatus.PAID ? "seed-provider-tx" : null,
            PaidAtUtc = status == PaymentOrderStatus.PAID ? Now.UtcDateTime : null
        });
        db.PaymentCheckouts.Add(new PaymentCheckout
        {
            PaymentOrderId = orderId, Provider = "PAYOS", ProviderOrderId = "processing-501",
            ProviderPaymentLinkId = "processing-link", CheckoutUrl = "https://pay.test/processing-link",
            Status = PaymentCheckoutStatus.READY, ReservedAtUtc = Now.AddMinutes(-5).UtcDateTime,
            ReadyAtUtc = Now.AddMinutes(-4).UtcDateTime
        });
        await db.SaveChangesAsync();
        return new PaymentProcessingSeed(orderId, userId, "processing-501", itemId);
    }

    private static async Task<PaymentProcessingSeed> SeedCheckoutPaymentAsync(PostgresScope scope)
    {
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        await using var db = scope.CreateDbContext();
        db.Users.Add(CreateScenarioUser(userId, "checkout-processing"));
        db.PaymentOrders.Add(new PaymentOrder
        {
            PaymentOrderId = orderId, UserId = userId, Provider = "PAYOS",
            Purpose = "AUTOMATED_TEST_ONLY", ProductReference = "TEST_PAYMENT_WALLET",
            Amount = 125_000, Currency = "VND", Status = PaymentOrderStatus.CREATED,
            IdempotencyKey = $"checkout-{orderId:N}", RequestFingerprint = new string('F', 64),
            CreatedAtUtc = Now.UtcDateTime, UpdatedAtUtc = Now.UtcDateTime,
            ExpiresAtUtc = Now.AddMinutes(30).UtcDateTime
        });
        await db.SaveChangesAsync();
        return new PaymentProcessingSeed(orderId, userId, string.Empty, null);
    }

    private static PaymentCatalogSettings PaymentCatalogFor(
        PaymentProcessingSeed seed, bool inventory = false) => new()
    {
        AllowedProviders = ["PAYOS"],
        Products =
        [
            new PaymentProductSettings
            {
                ProductReference = inventory ? "TEST_PAYMENT_ITEM" : "TEST_PAYMENT_WALLET",
                Purpose = "AUTOMATED_TEST_ONLY", Amount = 125_000, Currency = "VND",
                IsActive = true, ExpiresAfterMinutes = 30,
                FulfillmentKind = inventory ? "INVENTORY_ITEM" : "WALLET_CREDIT",
                WalletCreditAmount = inventory ? null : 150,
                ShopItemId = inventory ? seed.ShopItemId : null
            }
        ]
    };

    private static NormalizedPaymentProviderEvent ProcessingEvent(
        string eventId, string providerOrderId) => new(
        "PAYOS", eventId, providerOrderId, 125_000, "VND",
        PaymentProviderEventStatus.PAID, Now.UtcDateTime, new string('E', 64));

    private static async Task<ServiceResult<PaymentWebhookResponse>> ProcessWebhookAsync(
        PostgresScope scope,
        PaymentProcessingSeed seed,
        NormalizedPaymentProviderEvent paymentEvent)
    {
        await using var db = scope.CreateDbContext();
        var fulfillment = new PaymentFulfillmentService(
            db, Options.Create(PaymentCatalogFor(seed)), new FixedTimeProvider(Now));
        return await new PaymentWebhookService(db, fulfillment, new FixedTimeProvider(Now))
            .ProcessVerifiedAsync(paymentEvent);
    }

    private static async Task<ServiceResult<PaymentCheckoutResponse>> ProcessCheckoutAsync(
        PostgresScope scope,
        PaymentProcessingSeed seed,
        IPaymentProvider provider)
    {
        await using var db = scope.CreateDbContext();
        return await new PaymentCheckoutService(
            db, new PaymentProviderRegistry([provider]), new FixedTimeProvider(Now))
            .CreateCheckoutAsync(seed.UserId, seed.OrderId);
    }

    private static async Task<ServiceResult<PaymentFulfillmentResponse>> ProcessFulfillmentAsync(
        PostgresScope scope,
        PaymentProcessingSeed seed,
        PaymentCatalogSettings catalog)
    {
        await using var db = scope.CreateDbContext();
        return await new PaymentFulfillmentService(
            db, Options.Create(catalog), new FixedTimeProvider(Now))
            .FulfillAsync(seed.OrderId);
    }

    private static SubmitMatchResultRequest CreateRequest(MatchSeed seed) => new()
    {
        Outcome = MatchOutcome.WIN,
        ObjectiveCompletion = 1m,
        Players =
        [
            new SubmitMatchResultPlayerRequest
            {
                UserId = seed.HostId,
                Survived = true,
                DetectionCount = 1,
                ReviveCount = 1,
                ObjectiveContribution = 1
            },
            new SubmitMatchResultPlayerRequest
            {
                UserId = seed.PlayerId,
                Survived = true,
                DetectionCount = 2,
                DownedCount = 1,
                ObjectiveContribution = 1
            }
        ]
    };

    private static MatchResult CreateStoredResult(MatchSeed seed) => new()
    {
        MatchId = seed.MatchId,
        SubmittedByUserId = seed.HostId,
        Outcome = MatchOutcome.WIN,
        StartedAtUtc = Now.AddMinutes(-2).UtcDateTime,
        EndedAtUtc = Now.UtcDateTime,
        DurationSeconds = 120,
        ObjectiveCompletion = 1m,
        PlayerCount = 2,
        PayloadHash = new string('A', 64),
        RewardStatus = MatchRewardStatus.Pending,
        SubmittedAtUtc = Now.UtcDateTime
    };

    private static IPlayerAIProfilePolicy CreateProfilePolicy() =>
        new ConfiguredPlayerAIProfilePolicy(Options.Create(new PlayerAIProfileSettings
        {
            NormalizationConfigVersion = "TEST_NORM_V1",
            ProfileNoiseFilterVersion = "TEST_FILTER_V1",
            AlphaConfigVersion = "TEST_ALPHA_V1",
            SurvivalAlpha = 0.5m,
            NoiseAlpha = 0.5m,
            ProfileNoiseCountMin = 0m,
            ProfileNoiseCountMax = 10m,
            NoisePenaltyTypes = ["SPRINT"]
        }));

    private static ITeamProfilePolicy CreateTeamProfilePolicy() =>
        new ConfiguredTeamProfilePolicy(Options.Create(new TeamProfileSettings
        {
            TeamPerformanceFormulaVersion = "TEST_TEAM_PERFORMANCE_FOUR_COMPONENT_V0",
            PhaseRegistryVersion = "TEST_PHASE_REGISTRY_V1",
            NormalizationConfigVersion = "TEST_OBJECTIVE_NORMALIZATION_V1",
            ObjectiveBearingPhases = ["TEST_OBJECTIVE"],
            ObjectiveTimeMin = 0,
            ObjectiveTimeMax = 60,
            ObjectiveWeight = 0.25m,
            SurvivalWeight = 0.25m,
            TeamworkWeight = 0.25m,
            ResourceWeight = 0.25m
        }));

    private static TelemetryEventDocument[] CreateTeamTelemetry(Guid matchId) =>
    [
        TeamEvent(matchId, "MATCH_STARTED", 1, Now.AddMinutes(-2).UtcDateTime,
            new BsonDocument
            {
                ["context"] = new BsonDocument { ["teamSize"] = 2 },
                ["data"] = new BsonDocument { ["mapId"] = "test" }
            }, "MATCH_READY"),
        TeamEvent(matchId, "PHASE_STARTED", 2, Now.AddSeconds(-60).UtcDateTime,
            new BsonDocument
            {
                ["context"] = new BsonDocument { ["phase"] = "TEST_OBJECTIVE" },
                ["data"] = new BsonDocument()
            }, null),
        TeamEvent(matchId, "PHASE_COMPLETED", 3, Now.AddSeconds(-30).UtcDateTime,
            new BsonDocument
            {
                ["context"] = new BsonDocument { ["phase"] = "TEST_OBJECTIVE" },
                ["data"] = new BsonDocument()
            }, "OBJECTIVE_COMPLETED"),
        TeamEvent(matchId, "MATCH_ENDED", 4, Now.UtcDateTime,
            new BsonDocument
            {
                ["context"] = new BsonDocument { ["phase"] = "MATCH_END" },
                ["data"] = new BsonDocument { ["outcome"] = "SUCCESS", ["durationSeconds"] = 120, ["survivorCount"] = 2 }
            }, "TEAM_ESCAPED")
    ];

    private static TelemetryEventDocument TeamEvent(
        Guid matchId, string type, long sequence, DateTime timestamp,
        BsonDocument value, string? reason) => new()
    {
        Id = Guid.NewGuid(), MatchId = matchId, EventType = type,
        EventSequence = sequence, Ts = timestamp, ValueJson = value,
        ReasonCode = reason, SchemaVersion = "1.1",
        SemanticFingerprint = new string((char)('0' + sequence), 64),
        IngestedAt = Now.UtcDateTime
    };

    private sealed class StaticTeamTelemetryRepository(
        IReadOnlyList<TelemetryEventDocument> events) : ITelemetryEventRepository
    {
        public Task<IReadOnlyList<TelemetryEventDocument>> LoadAcceptedMatchEventsAsync(
            Guid matchId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TelemetryEventDocument>>(
                events.Where(item => item.MatchId == matchId).ToArray());
        public Task EnsureIndexesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<TelemetryWriteResult> AtomicCommitBatchAsync(IReadOnlyCollection<TelemetryEventDocument> documents, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TelemetryWriteResult> InsertBatchAsync(IReadOnlyCollection<TelemetryEventDocument> documents, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyDictionary<Guid, TelemetryWriteItemResult>> LoadConflictsAsync(IReadOnlyCollection<TelemetryEventDocument> documents, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyDictionary<Guid, TelemetryMatchBoundary>> LoadMatchBoundariesAsync(IReadOnlyCollection<Guid> matchIds, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private static MatchTelemetryAggregation CreateProfileAggregation(
        Guid matchId, Guid userId, DateTime endedAt)
    {
        IReadOnlyDictionary<PlayerAIDimension, AggregatedMetric> metrics =
            new Dictionary<PlayerAIDimension, AggregatedMetric>
            {
                [PlayerAIDimension.Survival] = new(PlayerAIDimension.Survival, 100m,
                    MetricAvailability.Available, "test", new string('A', 64)),
                [PlayerAIDimension.Noise] = new(PlayerAIDimension.Noise, 2m,
                    MetricAvailability.Available, "test", new string('B', 64))
            };
        return new(matchId, userId, endedAt, MatchProfileEligibilityStatus.Eligible,
            TelemetryCompleteness.Complete, [], "1.1", new string('C', 64), true, null, metrics);
    }

    private sealed class StaticProfileAggregator(MatchTelemetryAggregation value) : IMatchTelemetryAggregator
    {
        public Task<MatchTelemetryAggregation> AggregateAsync(
            Guid matchId, Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(value);
    }

    private sealed class SequencedProfileAggregator(params MatchTelemetryAggregation[] values) : IMatchTelemetryAggregator
    {
        private int _index;
        public Task<MatchTelemetryAggregation> AggregateAsync(
            Guid matchId, Guid userId, CancellationToken cancellationToken = default)
        {
            var index = Math.Min(Interlocked.Increment(ref _index) - 1, values.Length - 1);
            return Task.FromResult(values[index]);
        }
    }

    private static ShopItem CreateShopItem(Guid itemId, int price) => new()
    {
        ItemId = itemId,
        ItemName = $"Test Item {itemId:N}",
        Description = "PostgreSQL catalog constraint test item",
        Category = "TEST_COSMETIC",
        Price = price,
        AssetReference = $"test://catalog/{itemId:N}",
        IsActive = true,
        CreatedAtUtc = Now.UtcDateTime,
        UpdatedAtUtc = Now.UtcDateTime
    };

    private static ScenarioConfigDefinition CreateScenarioFallback() => new()
    {
        ScenarioConfigId = "TEST_FIXED", ScenarioConfigVersion = "TEST_FIXED_V1",
        SchemaVersion = "1.1", PolicyVersion = ScenarioConfigValidator.SupportedPolicyVersion,
        ConfigSource = ScenarioConfigSource.Fixed, MapId = "test-map", MonsterType = "test-monster",
        ObjectiveSpawnSetId = "test-objectives", SupportItemBudget = 1,
        DetectionFillRate = 1, DetectionDecayRate = 1, ChaseSpeed = 1, SearchDuration = 1,
        RouteModifier = "test-route", EscapeDoorTimerSeconds = 45,
        FallbackConfigId = "TEST_FIXED", FallbackConfigVersion = "TEST_FIXED_V1",
        ContentWhitelistVersion = "TEST_WHITELIST_V1", UnityCompatibilityVersion = "TEST_UNITY_CONTRACT_V1",
        IsActive = true, IsProductionApproved = true, IsFixedFallback = true,
        Provenance = "AUTOMATED_TEST_FIXTURE", CreatedAtUtc = Now.UtcDateTime, UpdatedAtUtc = Now.UtcDateTime
    };

    private static ScenarioService CreateScenarioService(AppDbContext db)
    {
        var clock = new FixedTimeProvider(Now);
        var builder = new AdaptiveInputSnapshotBuilder(db, clock);
        return new ScenarioService(db, new ScenarioConfigRegistry(db), builder, clock);
    }

    private static async Task<MatchSeed> SeedScenarioDecisionAsync(PostgresScope scope)
    {
        var hostId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var matchId = Guid.NewGuid();
        await using var db = scope.CreateDbContext();
        db.Users.AddRange(CreateScenarioUser(hostId, "scenario-host"), CreateScenarioUser(playerId, "scenario-player"));
        db.MatchAuthorityBindings.Add(new MatchAuthorityBinding
        {
            MatchId = matchId, FusionSessionName = $"scenario-{matchId:N}", HostUserId = hostId,
            MaxPlayers = 4, Status = MatchAuthorityStatus.Lobby,
            LeaseExpiresAtUtc = Now.AddMinutes(5).UtcDateTime,
            CreatedAtUtc = Now.UtcDateTime, UpdatedAtUtc = Now.UtcDateTime,
            Players = [CreateScenarioBinding(matchId, hostId, 1), CreateScenarioBinding(matchId, playerId, 2)]
        });
        db.PlayerAIProfiles.AddRange(
            CreateScenarioAIProfile(hostId, 80m), CreateScenarioAIProfile(playerId, 70m));
        db.ScenarioContentDefinitions.AddRange(CreateScenarioContent());
        db.ScenarioConfigs.Add(CreateScenarioFallback());
        await db.SaveChangesAsync();
        return new MatchSeed(matchId, hostId, playerId);
    }

    private static PlayerAIProfile CreateScenarioAIProfile(Guid userId, decimal score) => new()
    {
        UserId = userId, ProfileLineageId = Guid.NewGuid(), ProfileRevision = 1,
        ProfileFormulaVersion = "PROFILE_FORMULA_V1_1", MatchScoreFormulaVersion = "TEST_SCORE_V1",
        NormalizationConfigVersion = "TEST_NORM_V1", ProfileNoiseFilterVersion = "TEST_FILTER_V1",
        AlphaConfigVersion = "TEST_ALPHA_V1", SurvivalScore = score,
        SurvivalStatus = ProfileDimensionStatus.Active, SurvivalSampleCount = 2,
        NoiseScore = score, NoiseStatus = ProfileDimensionStatus.Active, NoiseSampleCount = 2,
        CreatedAtUtc = Now.UtcDateTime, UpdatedAtUtc = Now.UtcDateTime
    };

    private static User CreateScenarioUser(Guid id, string name) => new()
    {
        Id = id, Email = $"{name}-{id:N}@test.local", Username = $"{name}-{id:N}",
        PasswordHash = "hash", Role = UserRole.PLAYER, Status = UserStatus.ACTIVE,
        CreatedAt = Now.UtcDateTime, UpdatedAt = Now.UtcDateTime
    };

    private static MatchPlayerBinding CreateScenarioBinding(Guid matchId, Guid userId, int actor) => new()
    {
        Id = Guid.NewGuid(), MatchId = matchId, UserId = userId, FusionActorNumber = actor,
        JoinProofId = Guid.NewGuid(), BoundAtUtc = Now.UtcDateTime, LastSeenAtUtc = Now.UtcDateTime
    };

    private static ScenarioContentDefinition[] CreateScenarioContent() =>
    [
        CreateScenarioContent(ScenarioContentType.Map, "test-map"),
        CreateScenarioContent(ScenarioContentType.Monster, "test-monster"),
        CreateScenarioContent(ScenarioContentType.ObjectiveSpawnSet, "test-objectives"),
        CreateScenarioContent(ScenarioContentType.RouteModifier, "test-route")
    ];

    private static ScenarioContentDefinition CreateScenarioContent(ScenarioContentType type, string id) => new()
    {
        Id = Guid.NewGuid(), ContentType = type, ContentId = id,
        ContentWhitelistVersion = "TEST_WHITELIST_V1", UnityCompatibilityVersion = "TEST_UNITY_CONTRACT_V1",
        IsActive = true, IsProductionApproved = true, Provenance = "AUTOMATED_TEST_FIXTURE",
        CreatedAtUtc = Now.UtcDateTime, UpdatedAtUtc = Now.UtcDateTime
    };

    private sealed class PostgresScope : IAsyncDisposable
    {
        private const string ConnectionEnvironmentVariable =
            "ECHO_M4_POSTGRES_CONNECTION";
        private readonly string _adminConnectionString;
        private readonly string _schema;
        private readonly DbContextOptions<AppDbContext> _options;

        private PostgresScope(
            string adminConnectionString,
            string schema,
            DbContextOptions<AppDbContext> options)
        {
            _adminConnectionString = adminConnectionString;
            _schema = schema;
            _options = options;
        }

        public static async Task<PostgresScope> CreateAsync(string? targetMigration = null)
        {
            var connectionString = Environment.GetEnvironmentVariable(
                ConnectionEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    $"PostgreSQL integration tests are NOT RUN. Set {ConnectionEnvironmentVariable} first.");
            }

            var schema = $"m4_{Guid.NewGuid():N}";
            await using (var connection = new NpgsqlConnection(connectionString))
            {
                await connection.OpenAsync();
                await using var command = new NpgsqlCommand(
                    $"CREATE SCHEMA \"{schema}\"", connection);
                await command.ExecuteNonQueryAsync();
            }

            var scopedConnection = new NpgsqlConnectionStringBuilder(connectionString)
            {
                SearchPath = schema
            }.ConnectionString;
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(scopedConnection, postgres =>
                    postgres.MigrationsHistoryTable("__EFMigrationsHistory", schema))
                .Options;
            var scope = new PostgresScope(connectionString, schema, options);
            await using var db = scope.CreateDbContext();
            await db.GetService<IMigrator>().MigrateAsync(targetMigration);
            return scope;
        }

        public AppDbContext CreateDbContext() => new(_options);

        public async Task<MatchSeed> SeedMatchAsync()
        {
            var hostId = Guid.NewGuid();
            var playerId = Guid.NewGuid();
            var matchId = Guid.NewGuid();
            await using var db = CreateDbContext();
            db.Users.AddRange(CreateUser(hostId, "host"), CreateUser(playerId, "player"));
            db.MatchAuthorityBindings.Add(new MatchAuthorityBinding
            {
                MatchId = matchId,
                FusionSessionName = $"integration-{matchId:N}",
                HostUserId = hostId,
                MaxPlayers = 4,
                Status = MatchAuthorityStatus.InMatch,
                LeaseExpiresAtUtc = Now.AddMinutes(1).UtcDateTime,
                CreatedAtUtc = Now.AddMinutes(-3).UtcDateTime,
                UpdatedAtUtc = Now.AddMinutes(-2).UtcDateTime,
                StartedAtUtc = Now.AddMinutes(-2).UtcDateTime,
                Players =
                [
                    CreateBinding(matchId, hostId, 1),
                    CreateBinding(matchId, playerId, 2)
                ]
            });
            await db.SaveChangesAsync();
            return new MatchSeed(matchId, hostId, playerId);
        }

        public async Task<MatchSeed> SeedRewardMatchAsync()
        {
            var seed = await SeedMatchAsync();
            await using var db = CreateDbContext();
            var match = await db.MatchAuthorityBindings.SingleAsync(
                item => item.MatchId == seed.MatchId);
            match.Status = MatchAuthorityStatus.Ended;
            match.EndedAtUtc = Now.UtcDateTime;
            match.UpdatedAtUtc = Now.UtcDateTime;
            db.Wallets.AddRange(
                new Wallet
                {
                    Id = Guid.NewGuid(),
                    UserId = seed.HostId,
                    Balance = 100,
                    UpdatedAt = Now.UtcDateTime
                },
                new Wallet
                {
                    Id = Guid.NewGuid(),
                    UserId = seed.PlayerId,
                    Balance = 100,
                    UpdatedAt = Now.UtcDateTime
                });
            db.PlayerProfiles.AddRange(
                CreateProfile(seed.HostId, "Host"),
                CreateProfile(seed.PlayerId, "Player"));
            var result = CreateStoredResult(seed);
            result.Players =
            [
                new MatchResultPlayer
                {
                    MatchId = seed.MatchId,
                    UserId = seed.HostId,
                    Survived = true
                },
                new MatchResultPlayer
                {
                    MatchId = seed.MatchId,
                    UserId = seed.PlayerId,
                    Survived = true
                }
            ];
            db.MatchResults.Add(result);
            await db.SaveChangesAsync();
            return seed;
        }

        public async Task<MatchSeed> SeedAdditionalRewardMatchAsync(MatchSeed existingPlayers)
        {
            var seed = new MatchSeed(
                Guid.NewGuid(),
                existingPlayers.HostId,
                existingPlayers.PlayerId);
            await using var db = CreateDbContext();
            db.MatchAuthorityBindings.Add(new MatchAuthorityBinding
            {
                MatchId = seed.MatchId,
                FusionSessionName = $"reward-{seed.MatchId:N}",
                HostUserId = seed.HostId,
                MaxPlayers = 4,
                Status = MatchAuthorityStatus.Ended,
                LeaseExpiresAtUtc = Now.UtcDateTime,
                CreatedAtUtc = Now.AddMinutes(-3).UtcDateTime,
                UpdatedAtUtc = Now.UtcDateTime,
                StartedAtUtc = Now.AddMinutes(-2).UtcDateTime,
                EndedAtUtc = Now.UtcDateTime,
                Players =
                [
                    CreateBinding(seed.MatchId, seed.HostId, 1),
                    CreateBinding(seed.MatchId, seed.PlayerId, 2)
                ],
                Result = new MatchResult
                {
                    MatchId = seed.MatchId,
                    SubmittedByUserId = seed.HostId,
                    Outcome = MatchOutcome.WIN,
                    StartedAtUtc = Now.AddMinutes(-2).UtcDateTime,
                    EndedAtUtc = Now.UtcDateTime,
                    DurationSeconds = 120,
                    ObjectiveCompletion = 1m,
                    PlayerCount = 2,
                    PayloadHash = new string('C', 64),
                    RewardStatus = MatchRewardStatus.Pending,
                    SubmittedAtUtc = Now.UtcDateTime,
                    Players =
                    [
                        new MatchResultPlayer
                        {
                            MatchId = seed.MatchId,
                            UserId = seed.HostId,
                            Survived = true
                        },
                        new MatchResultPlayer
                        {
                            MatchId = seed.MatchId,
                            UserId = seed.PlayerId,
                            Survived = true
                        }
                    ]
                }
            });
            await db.SaveChangesAsync();
            return seed;
        }

        public async Task<PurchaseSeed> SeedPurchaseAsync(int balance = 500)
        {
            var userId = Guid.NewGuid();
            var firstItemId = Guid.NewGuid();
            var secondItemId = Guid.NewGuid();
            await using var db = CreateDbContext();
            db.Users.Add(CreateUser(userId, "buyer"));
            db.Wallets.Add(new Wallet
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Balance = balance,
                UpdatedAt = Now.UtcDateTime
            });
            db.ShopItems.AddRange(
                CreatePurchaseShopItem(firstItemId, "First Purchase Cosmetic", 100),
                CreatePurchaseShopItem(secondItemId, "Second Purchase Cosmetic", 150));
            await db.SaveChangesAsync();
            return new PurchaseSeed(userId, firstItemId, secondItemId);
        }

        public async Task<Guid> SeedShopItemAsync(int price)
        {
            var itemId = Guid.NewGuid();
            await using var db = CreateDbContext();
            db.ShopItems.Add(CreatePurchaseShopItem(itemId, "Reward Race Cosmetic", price));
            await db.SaveChangesAsync();
            return itemId;
        }

        public async Task<LoadoutSeed> SeedLoadoutAsync()
        {
            var userId = Guid.NewGuid();
            var otherUserId = Guid.NewGuid();
            var characterOne = CreateLoadoutShopItem("Character One", "CHARACTER");
            var characterTwo = CreateLoadoutShopItem("Character Two", "CHARACTER");
            var teamTool = CreateLoadoutShopItem("Team Tool", "TEAM_TOOL");
            var characterOneInventory = CreateLoadoutInventory(userId, characterOne.ItemId);
            var characterTwoInventory = CreateLoadoutInventory(userId, characterTwo.ItemId);
            var teamToolInventory = CreateLoadoutInventory(userId, teamTool.ItemId);

            await using var db = CreateDbContext();
            db.Users.AddRange(
                CreateUser(userId, "loadout-owner"),
                CreateUser(otherUserId, "loadout-other"));
            db.ShopItems.AddRange(characterOne, characterTwo, teamTool);
            db.InventoryItems.AddRange(
                characterOneInventory, characterTwoInventory, teamToolInventory);
            await db.SaveChangesAsync();

            return new LoadoutSeed(
                userId,
                otherUserId,
                characterOne.ItemId,
                characterTwo.ItemId,
                teamTool.ItemId,
                characterOneInventory.InventoryItemId,
                characterTwoInventory.InventoryItemId);
        }

        public async ValueTask DisposeAsync()
        {
            await using var connection = new NpgsqlConnection(_adminConnectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(
                $"DROP SCHEMA IF EXISTS \"{_schema}\" CASCADE", connection);
            await command.ExecuteNonQueryAsync();
        }

        private static User CreateUser(Guid id, string name) => new()
        {
            Id = id,
            Email = $"{name}-{id:N}@echo.invalid",
            Username = $"{name}-{id:N}",
            PasswordHash = "not-used",
            Role = UserRole.PLAYER,
            Status = UserStatus.ACTIVE,
            CreatedAt = Now.UtcDateTime,
            UpdatedAt = Now.UtcDateTime
        };

        private static MatchPlayerBinding CreateBinding(Guid matchId, Guid userId, int actor) => new()
        {
            Id = Guid.NewGuid(),
            MatchId = matchId,
            UserId = userId,
            FusionActorNumber = actor,
            JoinProofId = Guid.NewGuid(),
            BoundAtUtc = Now.AddMinutes(-3).UtcDateTime,
            LastSeenAtUtc = Now.AddMinutes(-1).UtcDateTime
        };

        private static PlayerProfile CreateProfile(Guid userId, string displayName) => new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            DisplayName = displayName,
            Level = 1,
            CreatedAt = Now.UtcDateTime,
            UpdatedAt = Now.UtcDateTime
        };

        private static ShopItem CreatePurchaseShopItem(
            Guid itemId,
            string name,
            int price) => new()
        {
            ItemId = itemId,
            ItemName = name,
            Description = $"{name} description",
            Category = "CHARACTER",
            Price = price,
            AssetReference = $"test://purchase/{itemId:N}",
            IsActive = true,
            CreatedAtUtc = Now.UtcDateTime,
            UpdatedAtUtc = Now.UtcDateTime
        };

        private static ShopItem CreateLoadoutShopItem(string name, string category) => new()
        {
            ItemId = Guid.NewGuid(),
            ItemName = name,
            Description = $"{name} PostgreSQL loadout fixture",
            Category = category,
            Price = 100,
            AssetReference = $"test://loadout/{Guid.NewGuid():N}",
            IsActive = true,
            CreatedAtUtc = Now.UtcDateTime,
            UpdatedAtUtc = Now.UtcDateTime
        };

        private static InventoryItem CreateLoadoutInventory(Guid userId, Guid shopItemId) => new()
        {
            InventoryItemId = Guid.NewGuid(),
            UserId = userId,
            ShopItemId = shopItemId,
            Source = InventoryAcquisitionSource.PURCHASE,
            AcquiredAtUtc = Now.UtcDateTime
        };
    }

    private sealed record MatchSeed(Guid MatchId, Guid HostId, Guid PlayerId);
    private sealed record PurchaseSeed(Guid UserId, Guid FirstItemId, Guid SecondItemId);
    private sealed record LoadoutSeed(
        Guid UserId,
        Guid OtherUserId,
        Guid CharacterOneId,
        Guid CharacterTwoId,
        Guid TeamToolId,
        Guid CharacterOneInventoryId,
        Guid CharacterTwoInventoryId);
    private sealed record AdminQuerySeed(
        Guid UserId,
        Guid PaymentOrderId,
        Guid SecondPaymentOrderId,
        Guid ShopItemId);
    private sealed record PaymentProcessingSeed(
        Guid OrderId,
        Guid UserId,
        string ProviderOrderId,
        Guid? ShopItemId);

    private sealed class IdempotentCheckoutProvider : IPaymentProvider
    {
        private readonly ConcurrentDictionary<long, PaymentProviderCheckoutResult> _orders = new();
        public string ProviderKey => "PAYOS";
        public bool IsConfigured => true;
        public int UniqueOrderCount => _orders.Count;

        public Task<PaymentProviderCheckoutResult> CreateCheckoutAsync(
            PaymentProviderCheckoutRequest request,
            CancellationToken cancellationToken = default) => Task.FromResult(
            _orders.GetOrAdd(request.OrderCode, code => new PaymentProviderCheckoutResult(
                code.ToString(), $"integration-{code}",
                new Uri($"https://pay.test/integration-{code}"), "PENDING",
                request.Amount, "VND")));

        public Task<PaymentProviderCheckoutResult?> QueryPaymentAsync(
            string providerOrderId,
            CancellationToken cancellationToken = default)
        {
            var found = long.TryParse(providerOrderId, out var code) &&
                        _orders.TryGetValue(code, out var result)
                ? result
                : null;
            return Task.FromResult<PaymentProviderCheckoutResult?>(found);
        }

        public PaymentWebhookVerificationResult VerifyAndParseWebhook(JsonElement payload) =>
            throw new NotSupportedException();
    }

    private sealed class FixedTestRewardPolicy : IRewardPolicy
    {
        public string Version => "TEST-POLICY-v1";
        public bool IsConfigured => true;

        public IReadOnlyList<RewardAllocation> Calculate(RewardMatchSnapshot match) =>
            match.Players.Select(item => new RewardAllocation(item.UserId, 25)).ToArray();
    }

    private sealed class FixedTestProgressionPolicy : IProgressionPolicy
    {
        public string Version => "TEST-PROGRESSION-v1";
        public bool IsConfigured => true;

        public IReadOnlyList<ProgressionAllocation> Calculate(RewardMatchSnapshot match) =>
            match.Players.Select(item => new ProgressionAllocation(item.UserId, 10, true)).ToArray();

        public int GetLevel(long totalExperiencePoints) => totalExperiencePoints >= 10 ? 2 : 1;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
