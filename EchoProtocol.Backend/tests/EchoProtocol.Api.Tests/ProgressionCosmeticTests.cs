using System.Text.Json;
using EchoProtocol.Api.DTOs.Player;
using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EchoProtocol.Api.Tests;

public sealed partial class ScenarioServiceTests
{
    [Fact]
    public async Task CosmeticCatchupIsIdempotentPreservesWalletAndEquippingRequiresTheCorrectMilestone()
    {
        await using var f = await Fixture.CreateAsync();
        f.Db.PlayerProfiles.Add(new PlayerProfile { Id = Guid.NewGuid(), UserId = f.PlayerId,
            Level = 19, LevelCurveVersion = "LEVEL_V3", ExperiencePoints = ProgressionCurveV3.Threshold(19), CreatedAt = Now, UpdatedAt = Now });
        await f.Db.SaveChangesAsync();
        var first = await ProgressionCosmeticService.ReconcileAsync(f.Db, f.PlayerId, Now, default);
        Assert.True(first.IsSuccess, first.Message);
        Assert.Single(first.Data!, c => c.Owned);
        var equipped = await ProgressionCosmeticService.ReconcileAsync(f.Db, f.PlayerId, Now, default,
            new("BADGE", "level15_badge"));
        Assert.True(equipped.IsSuccess, equipped.Message);
        Assert.True(equipped.Data!.Single(c => c.RewardKey == "level15_badge").Equipped);
        Assert.False((await ProgressionCosmeticService.ReconcileAsync(f.Db, f.PlayerId, Now, default,
            new("PROFILE_FRAME", "level20_frame"))).IsSuccess);
        Assert.False((await ProgressionCosmeticService.ReconcileAsync(f.Db, f.PlayerId, Now, default,
            new("BADGE", "level30_outfit"))).IsSuccess);
        var profile = await f.Db.PlayerProfiles.SingleAsync();
        profile.Level = 30; profile.ExperiencePoints = ProgressionCurveV3.Threshold(30);
        await f.Db.SaveChangesAsync();
        Assert.True((await ProgressionCosmeticService.ReconcileAsync(f.Db, f.PlayerId, Now.AddHours(1), default)).IsSuccess);
        Assert.True((await ProgressionCosmeticService.ReconcileAsync(f.Db, f.PlayerId, Now.AddHours(2), default)).IsSuccess);
        Assert.Equal(4, await f.Db.ProgressionCosmeticGrants.CountAsync());
        Assert.True((await f.Db.ProgressionCosmeticGrants.SingleAsync(g => g.RewardKey == "level15_badge")).Equipped);
        Assert.Equal(Now, (await f.Db.ProgressionCosmeticGrants.SingleAsync(g => g.RewardKey == "level15_badge")).GrantedAtUtc);
        Assert.Empty(await f.Db.WalletTransactions.ToListAsync());
        Assert.True((await ProgressionCosmeticService.ReconcileAsync(f.Db, f.PlayerId, Now, default, new("BADGE", null))).IsSuccess);
        Assert.False((await f.Db.ProgressionCosmeticGrants.SingleAsync(g => g.RewardKey == "level15_badge")).Equipped);
        Assert.True((await ProgressionCosmeticService.ReconcileAsync(f.Db, f.PlayerId, Now, default,
            new("BADGE", "level15_badge"))).IsSuccess);
        Assert.True((await ProgressionCosmeticService.ReconcileAsync(f.Db, f.PlayerId, Now, default, new("BADGE", ""))).IsSuccess);
        Assert.False((await f.Db.ProgressionCosmeticGrants.SingleAsync(g => g.RewardKey == "level15_badge")).Equipped);
    }

    [Fact]
    public async Task WalletTelemetrySeparatesPaidCreditsAndSpendingFromGameplayIncome()
    {
        await using var f = await Fixture.CreateAsync();
        var wallet = new Wallet { Id = Guid.NewGuid(), UserId = f.PlayerId, Balance = 250, UpdatedAt = Now };
        f.Db.Wallets.Add(wallet);
        f.Db.WalletTransactions.AddRange(
            new WalletTransaction { Id = Guid.NewGuid(), WalletId = wallet.Id, Type = WalletTransactionType.MATCH_REWARD,
                Amount = 300, BalanceBefore = 0, BalanceAfter = 300, ReferenceId = Guid.NewGuid(), CreatedAtUtc = Now.AddMinutes(-3) },
            new WalletTransaction { Id = Guid.NewGuid(), WalletId = wallet.Id, Type = WalletTransactionType.PAYMENT_FULFILLMENT,
                Amount = 500, BalanceBefore = 300, BalanceAfter = 800, ReferenceId = Guid.NewGuid(), CreatedAtUtc = Now.AddMinutes(-2) },
            new WalletTransaction { Id = Guid.NewGuid(), WalletId = wallet.Id, Type = WalletTransactionType.PURCHASE,
                Amount = -550, BalanceBefore = 800, BalanceAfter = 250, ReferenceId = Guid.NewGuid(), CreatedAtUtc = Now.AddMinutes(-1) });
        await f.Db.SaveChangesAsync();
        var result = await new EconomyAdminService(f.Db, new FixedClock(Now)).GetAsync(f.HostId, default);
        Assert.True(result.IsSuccess, result.Message);
        var json = JsonSerializer.SerializeToElement(result.Data).GetProperty("WalletTelemetry");
        Assert.Equal(250, json.GetProperty("WalletP50").GetDecimal());
        var flows = json.GetProperty("Flows").EnumerateArray().ToDictionary(x => x.GetProperty("Source").GetString()!, x => x.GetProperty("Credits").GetInt64());
        Assert.Equal(300, flows["MATCH_REWARD"]); Assert.Equal(500, flows["PAYMENT_FULFILLMENT"]);
        Assert.Equal(-550, flows["PURCHASE"]);
    }
}
