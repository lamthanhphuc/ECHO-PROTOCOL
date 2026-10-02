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
            "test://team-tools/signal-scanner"),
        new(Guid.Parse("12000000-0000-0000-0000-000000000001"), "Field Scanner", "TEAM_TOOL", 300,
            "Scan Energy Cores and moving threats.", "Assets/Prefabs/Gameplay/Imported/PF_Scanner_Imported.prefab"),
        new(Guid.Parse("12000000-0000-0000-0000-000000000002"), "Noise Maker", "TEAM_TOOL", 250,
            "Place a beacon that draws the Stalker.", "Assets/Prefabs/Gameplay/Imported/PF_TeamToolPickup_NoiseMaker.prefab"),
        new(Guid.Parse("12000000-0000-0000-0000-000000000003"), "First Aid", "TEAM_TOOL", 250,
            "Revive a downed teammate.", "Assets/Prefabs/Gameplay/Imported/PF_TeamToolPickup_FirstAid.prefab"),
        new(Guid.Parse("12000000-0000-0000-0000-000000000005"), "Distress Beacon", "TEAM_TOOL", 200,
            "Distress Beacon device for the Noise Maker.", "Assets/Prefabs/Gameplay/Imported/DistressBeaconClosed.prefab")
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
            "Seeded {Count} development shop items.",
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
