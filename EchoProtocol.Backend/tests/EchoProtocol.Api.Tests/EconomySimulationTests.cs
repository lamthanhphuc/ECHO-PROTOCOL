using EchoProtocol.Api.Data;
using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services;
using EchoProtocol.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EchoProtocol.Api.Tests;

public sealed class EconomySimulationTests
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task GameplayAndFivePurchaseStrategiesKeepWalletBooksAndFiniteCatalogConsistent(int cohort)
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        await ShopCatalogSeeder.SeedProductionCatalogAsync(db, TimeProvider.System, NullLogger.Instance);
        var items = await db.ShopItems.OrderBy(i => i.ItemId).ToArrayAsync();
        Assert.Equal(29, items.Length); Assert.Equal(10375, items.Sum(i => i.Price));
        var config = new RewardConfiguration { PolicyKey = "REWARD_V3" };
        var credits = new RewardPolicyV3(config); var experience = new ProgressionPolicyV3(config);
        int wins = cohort + 2, actions = cohort == 0 ? 2 : cohort == 1 ? 3 : 4, rescues = cohort == 2 ? 2 : 0;
        string difficulty = cohort == 0 ? "EASY" : cohort == 1 ? "NORMAL" : "HARD";
        var user = Guid.NewGuid();
        var balancesAt10 = new Dictionary<string, long>();
        foreach (var strategy in new[] { "save", "all", "characters", "petskins", "favorite" })
        {
            long earned = 0, spent = 0, xp = 0;
            int minutes = 0; var owned = new HashSet<Guid>();
            for (int n = 0; minutes < 6000; n++)
            {
                bool win = n % 5 < wins;
                int duration = cohort == 0 ? win ? 25 : 15 : cohort == 2 && win ? 15 : 20;
                if (minutes + duration + 5 > 6000) break;
                minutes += duration + 5;
                var match = new RewardMatchSnapshot(Guid.NewGuid(), win ? MatchOutcome.WIN : MatchOutcome.LOSE,
                    win ? 1m : cohort == 0 ? .25m : cohort == 1 ? .5m : .75m, duration * 60,
                    [new RewardPlayerSnapshot(user, win, false, 0, 0, 0, 0, actions, rescues, true)], "SciFi", difficulty);
                earned += credits.Calculate(match).Single().CurrencyAmount;
                xp += experience.Calculate(match).Single().ExperiencePoints;
                int level = ProgressionCurveV3.GetLevel(xp);
                if (level >= 10) balancesAt10.TryAdd(strategy, earned - spent);
                foreach (var item in items)
                {
                    bool wanted = strategy == "all" || strategy == "characters" && item.Category == "CHARACTER"
                        || strategy == "petskins" && item.Category is "PET" or "PET_SKIN"
                        || strategy == "favorite" && item.ItemName is "Field Scanner" or "Core Stabilizer" or "Mercenary" or "Soul Eater";
                    var parent = item.Category == "PET_SKIN"
                        ? items.Single(p => p.Category == "PET" && item.ItemName.StartsWith(p.ItemName + " ", StringComparison.Ordinal))
                        : item.Category == "CHARACTER_OUTFIT" && item.ItemName.StartsWith("Jammo ", StringComparison.Ordinal)
                            ? items.Single(p => p.ItemName == "Jammo") : null;
                    if (wanted && !owned.Contains(item.ItemId) && level >= item.RequiredLevel && earned - spent >= item.Price
                        && (parent is null || owned.Contains(parent.ItemId)))
                    { owned.Add(item.ItemId); spent += item.Price; }
                }
                Assert.InRange(earned - spent, 0, long.MaxValue);
                Assert.Equal(spent, items.Where(i => owned.Contains(i.ItemId)).Sum(i => (long)i.Price));
            }
            Assert.Equal(strategy switch { "all" => 29, "characters" => 3, "petskins" => 16, "favorite" => 4, _ => 0 }, owned.Count);
        }
        Assert.True(balancesAt10["favorite"] > balancesAt10["all"]);
        Assert.True(balancesAt10["save"] > balancesAt10["favorite"]);
    }
}
