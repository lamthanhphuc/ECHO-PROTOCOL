using EchoProtocol.Api.Common;
using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace EchoProtocol.Api.Tests;

public sealed partial class ScenarioServiceTests
{
    [Fact]
    public async Task EconomyPrice_RequiresIndependentApprovalAndRejectsStaleProposals()
    {
        await using var f = await Fixture.CreateAsync();
        var proposer = f.HostId;
        var approver = f.PlayerId;
        var item = new ShopItem { ItemId = Guid.NewGuid(), ItemName = "Test", Category = ShopItemCategories.Character,
            Price = 100, CreatedAtUtc = Now.AddMinutes(-1), UpdatedAtUtc = Now.AddMinutes(-1) };
        f.Db.ShopItems.Add(item);
        await f.Db.SaveChangesAsync();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["EconomyAdmin:PriceApproverUserIds:0"] = proposer.ToString(),
            ["EconomyAdmin:PriceApproverUserIds:1"] = approver.ToString()
        }).Build();
        var service = new EconomyAdminService(f.Db, config, new FixedClock(Now));
        var first = (ShopPriceProposal)(await service.ProposeAsync(proposer, new(item.ItemId, 100, 110, "Balance test price"), default)).Data!;
        var stale = (ShopPriceProposal)(await service.ProposeAsync(proposer, new(item.ItemId, 100, 120, "Balance test price"), default)).Data!;
        Assert.Equal(100, await f.Db.ShopItems.AsNoTracking().Select(x => x.Price).SingleAsync());
        Assert.Equal("FORBIDDEN", (await service.ReviewAsync(proposer, first.Id, new(true, "Review test price"), default)).ErrorCode);
        Assert.Equal("FORBIDDEN", (await service.ReviewAsync(Guid.NewGuid(), first.Id, new(true, "Review test price"), default)).ErrorCode);
        Assert.True((await service.ReviewAsync(approver, first.Id, new(true, "Review test price"), default)).IsSuccess);
        Assert.Equal(ErrorCodes.Conflict, (await service.ReviewAsync(approver, stale.Id, new(true, "Review test price"), default)).ErrorCode);
        Assert.Equal(110, await f.Db.ShopItems.AsNoTracking().Select(x => x.Price).SingleAsync());
    }
}
