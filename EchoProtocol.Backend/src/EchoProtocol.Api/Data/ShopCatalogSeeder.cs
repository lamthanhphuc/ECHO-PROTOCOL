using EchoProtocol.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace EchoProtocol.Api.Data;

public static class ShopCatalogSeeder
{
    private static readonly ShopSeedDefinition[] TestCatalog =
    [
        new(
            Guid.Parse("11000000-0000-0000-0000-000000000004"),
            "Test Explorer Character",
            "CHARACTER",
            100,
            "Test-only Character unlock.",
            "test://characters/explorer"),
        new(
            Guid.Parse("11000000-0000-0000-0000-000000000005"),
            "Test Signal Scanner",
            "TEAM_TOOL",
            75,
            "Test-only permanent TeamTool unlock.",
            "test://team-tools/signal-scanner")
    ];

    public static async Task SeedTestCatalogAsync(
        AppDbContext db,
        TimeProvider timeProvider,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var seedIds = TestCatalog.Select(item => item.ItemId).ToArray();
        var existingIds = (await db.ShopItems.AsNoTracking()
            .Where(item => seedIds.Contains(item.ItemId))
            .Select(item => item.ItemId)
            .ToListAsync(cancellationToken))
            .ToHashSet();
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var missing = TestCatalog
            .Where(item => !existingIds.Contains(item.ItemId))
            .Select(item => new ShopItem
            {
                ItemId = item.ItemId,
                ItemName = item.ItemName,
                Category = item.Category,
                Price = item.Price,
                Description = item.Description,
                AssetReference = item.AssetReference,
                IsActive = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            })
            .ToArray();

        if (missing.Length == 0)
        {
            logger.LogInformation("Test shop catalog already seeded.");
            return;
        }

        db.ShopItems.AddRange(missing);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Seeded {Count} test-only cosmetic shop items.",
            missing.Length);
    }

    private sealed record ShopSeedDefinition(
        Guid ItemId,
        string ItemName,
        string Category,
        int Price,
        string Description,
        string AssetReference);
}
