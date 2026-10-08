using EchoProtocol.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace EchoProtocol.Api.Data;

public static class ShopCatalogSeeder
{
    // Real shop items available in production.
    private static readonly ShopSeedDefinition[] ProductionCatalog =
    [
        new(
            Guid.Parse("12000000-0000-0000-0000-000000000001"),
            "Field Scanner",
            "TEAM_TOOL",
            300,
            "Scan Energy Cores and moving threats.",
            "Assets/Prefabs/Tools/PF_FieldScanner_Pickup.prefab"),

        new(
            Guid.Parse("12000000-0000-0000-0000-000000000002"),
            "Noise Maker",
            "TEAM_TOOL",
            250,
            "Place a beacon that draws the Stalker.",
            "Assets/Prefabs/Gameplay/Imported/PF_TeamToolPickup_NoiseMaker.prefab"),

        new(
            Guid.Parse("12000000-0000-0000-0000-000000000004"),
            "Door Jammer",
            "TEAM_TOOL",
            275,
            "Temporarily jam a compatible door.",
            "Assets/Prefabs/Gameplay/Imported/PF_Plank_Imported.prefab"),

        new(
            Guid.Parse("12000000-0000-0000-0000-000000000006"),
            "Core Stabilizer",
            "TEAM_TOOL",
            350,
            "Stabilize volatile energy-core handling.",
            "Assets/Prefabs/Environment/Teamtoools/Gameplay/PF_CoreStabilizer_Pickup.prefab"),

        new(
            Guid.Parse("13000000-0000-0000-0000-000000000001"),
            "Jammo",
            "CHARACTER",
            600,
            "Compact expedition unit configured for hazardous operations.",
            "Assets/Resources/Characters/PF_JammoVisual.prefab")
    ];

    // Development/test-only catalog entries.
    private static readonly ShopSeedDefinition[] DevelopmentOnlyCatalog =
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

    // These gameplay items must never be purchasable shop SKUs.
    private static readonly Guid FirstAidLegacyId =
        Guid.Parse("12000000-0000-0000-0000-000000000003");

    private static readonly Guid DistressBeaconLegacyId =
        Guid.Parse("12000000-0000-0000-0000-000000000005");

    public static async Task SeedProductionCatalogAsync(
        AppDbContext db,
        TimeProvider timeProvider,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        await EnsureCatalogAsync(
            db,
            ProductionCatalog,
            timeProvider,
            logger,
            cancellationToken);

        await DeactivateLegacyItemsAsync(
            db,
            timeProvider,
            logger,
            cancellationToken);
    }

    public static async Task SeedTestCatalogAsync(
        AppDbContext db,
        TimeProvider timeProvider,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        await EnsureCatalogAsync(
            db,
            ProductionCatalog,
            timeProvider,
            logger,
            cancellationToken);

        await EnsureCatalogAsync(
            db,
            DevelopmentOnlyCatalog,
            timeProvider,
            logger,
            cancellationToken);

        await DeactivateLegacyItemsAsync(
            db,
            timeProvider,
            logger,
            cancellationToken);
    }

    private static async Task EnsureCatalogAsync(
        AppDbContext db,
        IReadOnlyCollection<ShopSeedDefinition> catalog,
        TimeProvider timeProvider,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var ids =
            catalog
                .Select(item => item.ItemId)
                .ToArray();

        var existing =
            await db.ShopItems
                .Where(item => ids.Contains(item.ItemId))
                .ToDictionaryAsync(
                    item => item.ItemId,
                    cancellationToken);

        var now =
            timeProvider
                .GetUtcNow()
                .UtcDateTime;

        foreach (var definition in catalog)
        {
            if (!existing.TryGetValue(
                    definition.ItemId,
                    out var item))
            {
                db.ShopItems.Add(
                    new ShopItem
                    {
                        ItemId = definition.ItemId,
                        ItemName = definition.ItemName,
                        Category = definition.Category,
                        Price = definition.Price,
                        Description = definition.Description,
                        AssetReference = definition.AssetReference,
                        IsActive = true,
                        CreatedAtUtc = now,
                        UpdatedAtUtc = now
                    });

                continue;
            }

            var changed = false;

            if (item.ItemName != definition.ItemName)
            {
                item.ItemName = definition.ItemName;
                changed = true;
            }

            if (item.Category != definition.Category)
            {
                item.Category = definition.Category;
                changed = true;
            }

            if (item.Price != definition.Price)
            {
                item.Price = definition.Price;
                changed = true;
            }

            if (item.Description != definition.Description)
            {
                item.Description = definition.Description;
                changed = true;
            }

            if (item.AssetReference != definition.AssetReference)
            {
                item.AssetReference = definition.AssetReference;
                changed = true;
            }

            if (!item.IsActive)
            {
                item.IsActive = true;
                changed = true;
            }

            if (changed)
            {
                item.UpdatedAtUtc = now;
            }
        }

        await db.SaveChangesAsync(
            cancellationToken);

        logger.LogInformation(
            "Ensured {Count} shop catalog definitions.",
            catalog.Count);
    }

    private static async Task DeactivateLegacyItemsAsync(
        AppDbContext db,
        TimeProvider timeProvider,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var legacyIds =
            new[]
            {
                FirstAidLegacyId,
                DistressBeaconLegacyId
            };

        var legacyItems =
            await db.ShopItems
                .Where(
                    item =>
                        legacyIds.Contains(item.ItemId)
                        && item.IsActive)
                .ToListAsync(
                    cancellationToken);

        if (legacyItems.Count == 0)
        {
            return;
        }

        var now =
            timeProvider
                .GetUtcNow()
                .UtcDateTime;

        foreach (var item in legacyItems)
        {
            item.IsActive = false;
            item.UpdatedAtUtc = now;
        }

        await db.SaveChangesAsync(
            cancellationToken);

        logger.LogInformation(
            "Deactivated {Count} legacy shop items.",
            legacyItems.Count);
    }

    private sealed record ShopSeedDefinition(
        Guid ItemId,
        string ItemName,
        string Category,
        int Price,
        string Description,
        string AssetReference);
}
