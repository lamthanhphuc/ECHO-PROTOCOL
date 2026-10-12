using EchoProtocol.Api.Common;
using EchoProtocol.Api.Data;
using EchoProtocol.Api.DTOs.Shop;
using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EchoProtocol.Api.Tests;

// CHARACTER_OUTFIT_CONTRACT_TESTS
public sealed class CharacterOutfitCommerceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 10, 12, 4, 0, 0, TimeSpan.Zero);

    private static readonly Guid OutfitItemId =
        Guid.Parse("13100000-0000-0000-0000-000000000001");

    [Fact, Trait("Category", "M4ShopUnit")]
    public void CharacterOutfitCategoryIsSupported()
    {
        Assert.Equal(
            "CHARACTER_OUTFIT",
            ShopItemCategories.CharacterOutfit);

        Assert.True(
            ShopItemCategories.IsSupported(
                ShopItemCategories.CharacterOutfit));
    }

    [Fact, Trait("Category", "M4ShopUnit")]
    public async Task CharacterOutfitAppearsInCatalogFilter()
    {
        await using var connection =
            new SqliteConnection("Data Source=:memory:");

        await connection.OpenAsync();

        await using var db =
            new AppDbContext(
                new DbContextOptionsBuilder<AppDbContext>()
                    .UseSqlite(connection)
                    .Options);

        await db.Database.EnsureCreatedAsync();

        db.ShopItems.Add(
            CreateOutfitItem());

        await db.SaveChangesAsync();

        var service =
            new ShopCatalogService(db);

        var result =
            await service.GetItemsAsync(
                ShopItemCategories.CharacterOutfit,
                1,
                20);

        Assert.True(result.IsSuccess);

        var item =
            Assert.Single(
                result.Data!.Items);

        Assert.Equal(
            OutfitItemId,
            item.ItemId);

        Assert.Equal(
            ShopItemCategories.CharacterOutfit,
            item.Category);

        Assert.Equal(
            150,
            item.Price);
    }

    [Fact, Trait("Category", "M4PurchaseUnit")]
    public async Task CharacterOutfitPurchaseDebitsWalletAndAppearsInInventory()
    {
        await using var connection =
            new SqliteConnection("Data Source=:memory:");

        await connection.OpenAsync();

        await using var db =
            new AppDbContext(
                new DbContextOptionsBuilder<AppDbContext>()
                    .UseSqlite(connection)
                    .Options);

        await db.Database.EnsureCreatedAsync();

        var userId =
            Guid.NewGuid();

        db.Users.Add(
            new User
            {
                Id = userId,
                Email = $"outfit-{userId:N}@echo.invalid",
                Username = $"outfit-{userId:N}",
                PasswordHash = "not-used",
                Role = UserRole.PLAYER,
                Status = UserStatus.ACTIVE,
                CreatedAt = Now.UtcDateTime,
                UpdatedAt = Now.UtcDateTime,
                Wallet = new Wallet
                {
                    Id = Guid.NewGuid(),
                    Balance = 500,
                    UpdatedAt = Now.UtcDateTime
                }
            });

        db.ShopItems.Add(
            CreateOutfitItem());

        await db.SaveChangesAsync();

        var purchaseService =
            new PurchaseService(
                db,
                new FixedTimeProvider(Now));

        var purchase =
            await purchaseService.PurchaseAsync(
                userId,
                new PurchaseRequest
                {
                    ItemId = OutfitItemId,
                    IdempotencyKey =
                        "character-outfit-contract"
                });

        Assert.True(purchase.IsSuccess);

        Assert.Equal(
            150,
            purchase.Data!.PricePaid);

        Assert.Equal(
            350,
            purchase.Data.WalletBalance);

        Assert.Equal(
            350,
            (await db.Wallets.SingleAsync()).Balance);

        var inventoryService =
            new InventoryService(db);

        var inventory =
            await inventoryService.GetCurrentAsync(
                userId);

        Assert.True(inventory.IsSuccess);

        var owned =
            Assert.Single(
                inventory.Data!.Items);

        Assert.Equal(
            OutfitItemId,
            owned.ItemId);

        Assert.Equal(
            ShopItemCategories.CharacterOutfit,
            owned.Category);

        Assert.Single(
            await db.PurchaseTransactions.ToListAsync());

        Assert.Single(
            await db.InventoryItems.ToListAsync());

        Assert.Single(
            await db.WalletTransactions.ToListAsync());
    }

    private static ShopItem CreateOutfitItem() =>
        new()
        {
            ItemId = OutfitItemId,
            ItemName = "Astronaut Orange",
            Description =
                "Orange cosmetic outfit for Astronaut.",
            Category =
                ShopItemCategories.CharacterOutfit,
            Price = 150,
            AssetReference =
                "Assets/Resources/Shop/LobbyVariants/Characters/character-0-outfit-1.png",
            IsActive = true,
            CreatedAtUtc = Now.UtcDateTime,
            UpdatedAtUtc = Now.UtcDateTime
        };

    private sealed class FixedTimeProvider(
        DateTimeOffset now)
        : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() =>
            now;
    }
}