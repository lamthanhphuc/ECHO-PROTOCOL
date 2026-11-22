using EchoProtocol.Api.Common;
using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EchoProtocol.Api.Tests;

public sealed partial class ScenarioServiceTests
{
    [Fact]
    public async Task AdminExperienceIsAuditedIdempotentAndDoesNotChangeMatchCounts()
    {
        await using var f = await Fixture.CreateAsync();
        f.Db.PlayerProfiles.Add(new PlayerProfile { Id = Guid.NewGuid(), UserId = f.PlayerId, DisplayName = "XP test",
            Level = 1, LevelCurveVersion = "LEVEL_V3", ExperiencePoints = 240, LifetimeExperiencePoints = 240,
            TotalMatches = 4, TotalWins = 2, CreatedAt = Now, UpdatedAt = Now });
        await f.Db.SaveChangesAsync();
        var service = new AdminQueryService(f.Db);
        var request = new EchoProtocol.Api.DTOs.Admin.AdminGrantExperienceRequest { RequestId = Guid.NewGuid(), Amount = 100, Reason = "Support progression test" };
        Assert.True((await service.GrantExperienceAsync(f.HostId, f.PlayerId, request)).IsSuccess);
        Assert.True((await service.GrantExperienceAsync(f.HostId, f.PlayerId, request)).IsSuccess);
        var profile = await f.Db.PlayerProfiles.SingleAsync();
        Assert.Equal(340, profile.ExperiencePoints); Assert.Equal(340, profile.LifetimeExperiencePoints); Assert.Equal(2, profile.Level);
        Assert.Equal(4, profile.TotalMatches); Assert.Equal(2, profile.TotalWins);
        var grant = Assert.Single(await f.Db.AdminExperienceGrants.ToListAsync());
        Assert.Equal(f.HostId, grant.AdminUserId); Assert.Equal(1, grant.LevelBefore); Assert.Equal(2, grant.LevelAfter);
        Assert.Equal(ErrorCodes.Conflict, (await service.GrantExperienceAsync(f.HostId, f.PlayerId,
            new() { RequestId = request.RequestId, Amount = 101, Reason = request.Reason })).ErrorCode);
        Assert.True((await service.GetProgressionAsync(f.PlayerId)).IsSuccess);
    }

    [Theory]
    [InlineData(0)] [InlineData(-1)] [InlineData(100001)]
    public async Task AdminExperienceRejectsInvalidAmount(int amount)
    {
        await using var f = await Fixture.CreateAsync();
        var result = await new AdminQueryService(f.Db).GrantExperienceAsync(f.HostId, f.PlayerId,
            new() { RequestId = Guid.NewGuid(), Amount = amount, Reason = "test" });
        Assert.Equal(ErrorCodes.ValidationError, result.ErrorCode);
        Assert.Empty(await f.Db.AdminExperienceGrants.ToListAsync());
    }

    [Fact]
    public async Task UnlockMilestonesUseCurrentCatalogAndPreserveOwnedItems()
    {
        await using var f = await Fixture.CreateAsync();
        await EchoProtocol.Api.Data.ShopCatalogSeeder.SeedProductionCatalogAsync(f.Db, TimeProvider.System,
            Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);
        var scanner = await f.Db.ShopItems.SingleAsync(x => x.ItemName == "Field Scanner");
        var stabilizer = await f.Db.ShopItems.SingleAsync(x => x.ItemName == "Core Stabilizer");
        Assert.Equal(6, scanner.RequiredLevel); Assert.Equal(4, stabilizer.RequiredLevel);
        Assert.Equal(12, (await f.Db.ShopItems.SingleAsync(x => x.ItemName == "Soul Eater")).RequiredLevel);
        Assert.Equal(5, (await f.Db.ShopItems.SingleAsync(x => x.ItemName == "Usurper")).RequiredLevel);
        f.Db.InventoryItems.Add(new InventoryItem { InventoryItemId = Guid.NewGuid(), UserId = f.PlayerId,
            ShopItemId = scanner.ItemId, AcquiredAtUtc = Now });
        await f.Db.SaveChangesAsync();
        var milestones = await LevelUnlockService.ReadAsync(f.Db, f.PlayerId, 2, 300, "LEVEL_V3", default);
        Assert.True(milestones.Single(x => x.ItemId == scanner.ItemId).Owned);
        Assert.Equal(575, milestones.Single(x => x.ItemId == stabilizer.ItemId).ExperienceRemaining);
        var change = await new EconomyAdminService(f.Db, new FixedClock(Now)).ApplyPriceAsync(f.HostId,
            new(scanner.ItemId, scanner.Price, scanner.Price, "Test scanner level change", 7, 6), default);
        Assert.True(change.IsSuccess, change.Message);
    }

    [Fact]
    public async Task RewardAdminActivatesV3WithNeutralReferencesAndRejectsChangedContribution()
    {
        await using var f = await Fixture.CreateAsync();
        var service = new EconomyAdminService(f.Db, new FixedClock(Now));
        var request = new RewardConfiguration { PolicyKey = "REWARD_V3" };
        var result = await service.SaveRewardsAsync(f.HostId, request, default);
        Assert.True(result.IsSuccess, result.Message);
        f.Db.ChangeTracker.Clear();
        var saved = await f.Db.RewardConfigurations.SingleAsync();
        Assert.True(saved.HasValidV3Settings());
        Assert.Equal(20, saved.SciFiReferenceMinutes);
        Assert.Equal(20, saved.FloodedReferenceMinutes);
        Assert.Equal(20, saved.PurgeReferenceMinutes);
        saved.OneActionTeamPercent = 51;
        Assert.Equal(ErrorCodes.ValidationError, (await service.SaveRewardsAsync(f.HostId, saved, default)).ErrorCode);
        Assert.True((await service.GetAsync(f.HostId, default)).IsSuccess);
    }

    [Fact]
    public async Task EssentialToolsCannotBeLockedBehindLevel()
    {
        await using var f = await Fixture.CreateAsync();
        var item = new ShopItem { ItemId = Guid.NewGuid(), ItemName = "Tool", Category = ShopItemCategories.TeamTool,
            Price = 100, RequiredLevel = 1, CreatedAtUtc = Now, UpdatedAtUtc = Now };
        f.Db.ShopItems.Add(item); await f.Db.SaveChangesAsync();
        var result = await new EconomyAdminService(f.Db, new FixedClock(Now))
            .ApplyPriceAsync(f.HostId, new(item.ItemId, 100, 100, "Test essential tool lock", 2, 1), default);
        Assert.Equal(ErrorCodes.ValidationError, result.ErrorCode);
        Assert.Empty(await f.Db.Set<ShopPriceProposal>().ToListAsync());
    }

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
