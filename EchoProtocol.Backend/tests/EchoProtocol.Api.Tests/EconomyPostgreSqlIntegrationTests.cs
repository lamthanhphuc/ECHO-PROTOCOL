using EchoProtocol.Api.Common;
using EchoProtocol.Api.Configurations;
using EchoProtocol.Api.DTOs.Payments;
using EchoProtocol.Api.DTOs.Player;
using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace EchoProtocol.Api.Tests;

public sealed partial class MatchResultPostgreSqlIntegrationTests
{
    [Fact, Trait("Category", "M4PostgreSqlIntegration")]
    public async Task ConcurrentCosmeticCatchupAndEquipGrantEveryMilestoneOnceWithoutChargingWallet()
    {
        await using var scope = await PostgresScope.CreateAsync();
        var seed = await scope.SeedRewardMatchAsync();
        await using (var db = scope.CreateDbContext())
        {
            var profile = await db.PlayerProfiles.SingleAsync(p => p.UserId == seed.HostId);
            profile.Level = 30; profile.ExperiencePoints = ProgressionCurveV3.Threshold(30);
            await db.SaveChangesAsync();
        }
        async Task<ServiceResult<ProgressionCosmeticResponse[]>> Grant()
        {
            await using var db = scope.CreateDbContext();
            return await ProgressionCosmeticService.ReconcileAsync(db, seed.HostId, Now.UtcDateTime, default, new("BADGE", "level15_badge"));
        }
        var replies = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Grant()));
        Assert.All(replies, reply => Assert.True(reply.IsSuccess, reply.Message));
        await using var verify = scope.CreateDbContext();
        Assert.Equal(4, await verify.ProgressionCosmeticGrants.CountAsync());
        Assert.Single(await verify.ProgressionCosmeticGrants.Where(g => g.Equipped).ToListAsync());
        Assert.Equal(100, (await verify.Wallets.SingleAsync(w => w.UserId == seed.HostId)).Balance);
        Assert.Empty(await verify.WalletTransactions.ToListAsync());
    }

    [Fact, Trait("Category", "M4PostgreSqlIntegration")]
    public async Task ConcurrentDirectCosmeticCheckoutsReserveOnlyOneOrder()
    {
        await using var scope = await PostgresScope.CreateAsync();
        var seed = await scope.SeedRewardMatchAsync();
        var id = Guid.NewGuid();
        await using (var db = scope.CreateDbContext())
        {
            db.ShopItems.Add(new ShopItem { ItemId = id, ItemName = "PG cosmetic test", Category = "CHARACTER_OUTFIT",
                Price = 100, IsActive = true, RequiredLevel = 1, CreatedAtUtc = Now.UtcDateTime, UpdatedAtUtc = Now.UtcDateTime });
            await db.SaveChangesAsync();
        }
        var options = Options.Create(new PaymentCatalogSettings { AllowedProviders = ["TEST"], Products = [new PaymentProductSettings {
            ProductReference = "PG_COSMETIC", Purpose = "COSMETIC", IsActive = true, Amount = 100, Currency = "VND", ExpiresAfterMinutes = 30,
            FulfillmentKind = "INVENTORY_ITEM", ShopItemId = id, UsesDefaultCharacter = true }] });
        async Task<bool> Checkout(string key)
        {
            await using var db = scope.CreateDbContext();
            var result = await new PaymentOrderService(db, options, TimeProvider.System).CreateAsync(seed.HostId,
                new CreatePaymentOrderRequest { Provider = "TEST", ProductReference = "PG_COSMETIC", IdempotencyKey = key });
            Assert.True(result.IsSuccess || result.ErrorCode == ErrorCodes.PaymentOrderConflict, result.Message);
            return result.IsSuccess;
        }
        var results = await Task.WhenAll(Checkout("cosmetic-a"), Checkout("cosmetic-b"));
        Assert.Single(results, success => success);
        await using var verify = scope.CreateDbContext();
        Assert.Single(await verify.PaymentOrders.ToListAsync());
        Assert.Empty(await verify.InventoryItems.ToListAsync());
    }
}
