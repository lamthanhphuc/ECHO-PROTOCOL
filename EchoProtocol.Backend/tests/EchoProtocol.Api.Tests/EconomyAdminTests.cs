using EchoProtocol.Api.Common;
using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EchoProtocol.Api.Tests;

public sealed partial class ScenarioServiceTests
{
    [Fact]
    public async Task RewardAdminRejectsStaleConfigurationAndInvalidV2Parameters()
    {
        await using var f = await Fixture.CreateAsync();
        var service = new EconomyAdminService(f.Db, new FixedClock(Now));
        f.Db.RewardConfigurations.Add(new RewardConfiguration
        {
            Id = RewardConfiguration.SharedId, EffectiveAtUtc = Now.AddMinutes(-1), PolicyKey = "REWARD_V1"
        });
        await f.Db.SaveChangesAsync();
        var stale = new RewardConfiguration { PolicyKey = "REWARD_V1", EffectiveAtUtc = Now.AddMinutes(-2) };
        Assert.Equal(ErrorCodes.Conflict, (await service.SaveRewardsAsync(f.HostId, stale, default)).ErrorCode);
        var invalid = new RewardConfiguration
        {
            PolicyKey = "REWARD_V2", EffectiveAtUtc = Now.AddMinutes(-1), Participation = 20
        };
        Assert.Equal(ErrorCodes.ValidationError, (await service.SaveRewardsAsync(f.HostId, invalid, default)).ErrorCode);
        invalid.Participation = 0;
        invalid.RescueBonusCredits = -1;
        Assert.Equal(ErrorCodes.ValidationError, (await service.SaveRewardsAsync(f.HostId, invalid, default)).ErrorCode);
        invalid.RescueBonusCredits = 10;
        invalid.TwoActionTeamPercent = 40;
        Assert.Equal(ErrorCodes.ValidationError, (await service.SaveRewardsAsync(f.HostId, invalid, default)).ErrorCode);
    }

    [Fact]
    public async Task RewardAdminPinsV1SettingsBeforeActivatingV2WithLegacyActiveMatch()
    {
        await using var f = await Fixture.CreateAsync();
        var match = await f.Db.MatchAuthorityBindings.SingleAsync();
        match.Status = EchoProtocol.Api.Enums.MatchAuthorityStatus.InMatch;
        await f.Db.SaveChangesAsync();
        var service = new EconomyAdminService(f.Db, new FixedClock(Now));
        var request = new RewardConfiguration
        {
            PolicyKey = "REWARD_V2", Participation = 0, Disconnected = 0, Maximum = 300,
            LossCapCreditsPerMinute = 2m
        };
        Assert.True((await service.SaveRewardsAsync(f.HostId, request, default)).IsSuccess);
        f.Db.ChangeTracker.Clear();
        var stored = await f.Db.MatchAuthorityBindings.SingleAsync();
        Assert.Equal("REWARD_V1", stored.RewardPolicyVersion);
        Assert.Equal("PROGRESSION_V1", stored.ProgressionPolicyVersion);
        Assert.Equal("REWARD_V1", System.Text.Json.JsonSerializer.Deserialize<RewardConfiguration>(
            stored.RewardConfigurationJson!)!.PolicyKey);
    }
    [Fact]
    public async Task RewardAdminSavesConfiguredRescueBonus()
    {
        await using var f = await Fixture.CreateAsync();
        var service = new EconomyAdminService(f.Db, new FixedClock(Now));
        Assert.True((await service.SaveRewardsAsync(f.HostId,
            new RewardConfiguration { RescueBonusCredits = 25, ObjectiveActionCap = 5,
                RescueActionCap = 3, OneActionTeamPercent = 40, TwoActionTeamPercent = 70,
                FullTeamPercent = 90, WinExperience = 90, LossExperiencePerMinute = 2 }, default)).IsSuccess);
        f.Db.ChangeTracker.Clear();
        var saved = await f.Db.RewardConfigurations.SingleAsync();
        Assert.Equal(25, saved.RescueBonusCredits);
        Assert.Equal(5, saved.ObjectiveActionCap);
        Assert.Equal(3, saved.RescueActionCap);
        Assert.Equal(90, saved.WinExperience);
        Assert.Equal(2, saved.LossExperiencePerMinute);
    }
    [Fact]
    public async Task EconomyPrice_AllowsAdminToApproveOwnProposalAndRejectsStaleProposals()
    {
        await using var f = await Fixture.CreateAsync();
        var proposer = f.HostId;
        var item = new ShopItem { ItemId = Guid.NewGuid(), ItemName = "Test", Category = ShopItemCategories.Character,
            Price = 100, CreatedAtUtc = Now.AddMinutes(-1), UpdatedAtUtc = Now.AddMinutes(-1) };
        f.Db.ShopItems.Add(item);
        await f.Db.SaveChangesAsync();
        var service = new EconomyAdminService(f.Db, new FixedClock(Now));
        var first = (ShopPriceProposal)(await service.ProposeAsync(proposer, new(item.ItemId, 100, 110, "Balance test price"), default)).Data!;
        var stale = (ShopPriceProposal)(await service.ProposeAsync(proposer, new(item.ItemId, 100, 120, "Balance test price"), default)).Data!;
        Assert.Equal(100, await f.Db.ShopItems.AsNoTracking().Select(x => x.Price).SingleAsync());
        Assert.True((await service.ReviewAsync(proposer, first.Id, new(true, "Review test price"), default)).IsSuccess);
        Assert.Equal(ErrorCodes.Conflict, (await service.ReviewAsync(proposer, stale.Id, new(true, "Review test price"), default)).ErrorCode);
        Assert.Equal(110, await f.Db.ShopItems.AsNoTracking().Select(x => x.Price).SingleAsync());
    }
    [Fact]
    public async Task EconomyPrice_AppliesImmediatelyAndRecordsTheChange()
    {
        await using var f = await Fixture.CreateAsync();
        var item = new ShopItem { ItemId = Guid.NewGuid(), ItemName = "Test", Category = ShopItemCategories.Character,
            Price = 100, CreatedAtUtc = Now.AddMinutes(-1), UpdatedAtUtc = Now.AddMinutes(-1) };
        f.Db.ShopItems.Add(item);
        await f.Db.SaveChangesAsync();
        var service = new EconomyAdminService(f.Db, new FixedClock(Now));

        Assert.True((await service.ApplyPriceAsync(f.HostId, new(item.ItemId, 100, 125, "Cân chỉnh bảng giá"), default)).IsSuccess);
        Assert.Equal(ErrorCodes.Conflict, (await service.ApplyPriceAsync(f.HostId, new(item.ItemId, 100, 150, "Cân chỉnh bảng giá"), default)).ErrorCode);
        Assert.Equal(125, await f.Db.ShopItems.AsNoTracking().Select(x => x.Price).SingleAsync());
        var change = await f.Db.Set<ShopPriceProposal>().AsNoTracking().SingleAsync();
        Assert.Equal(100, change.PreviousPrice);
        Assert.Equal(125, change.ProposedPrice);
        Assert.Equal(f.HostId, change.ReviewedByUserId);
        Assert.Equal("APPROVED", change.Status);
    }
}
