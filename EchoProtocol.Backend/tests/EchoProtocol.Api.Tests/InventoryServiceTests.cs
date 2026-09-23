using System.Security.Claims;
using EchoProtocol.Api.Common;
using EchoProtocol.Api.Controllers;
using EchoProtocol.Api.Data;
using EchoProtocol.Api.DTOs.Inventory;
using EchoProtocol.Api.DTOs.Shop;
using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EchoProtocol.Api.Tests;

public sealed class InventoryServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 20, 17, 0, 0, TimeSpan.Zero);

    [Fact, Trait("Category", "M4InventoryUnit")]
    public async Task InventoryControllerReturnsOnlyItemsOwnedByJwtUser()
    {
        await using var harness = await InventoryHarness.CreateAsync();
        var controller = new InventoryController(
            new InventoryService(harness.Db),
            NullLogger<InventoryController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = Principal(harness.FirstUserId)
                }
            }
        };

        var action = await controller.Me(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(action.Result);
        var response = Assert.IsType<ApiResponse<InventoryResponse>>(ok.Value);
        var owned = Assert.Single(response.Data!.Items);
        Assert.Equal(harness.FirstUserItemId, owned.ItemId);
        Assert.DoesNotContain(response.Data.Items, item => item.ItemId == harness.SecondUserItemId);
    }

    [Fact, Trait("Category", "M4InventoryUnit")]
    public async Task PurchasedInventoryPersistsAndReturnsUnityFields()
    {
        await using var harness = await InventoryHarness.CreateAsync(includeInitialInventory: false);
        var purchase = await new PurchaseService(
                harness.Db,
                new FixedTimeProvider(Now))
            .PurchaseAsync(harness.FirstUserId, new PurchaseRequest
            {
                ItemId = harness.FirstUserItemId,
                IdempotencyKey = "inventory-persistence"
            });
        Assert.True(purchase.IsSuccess);
        harness.Db.ChangeTracker.Clear();

        var inventory = await new InventoryService(harness.Db)
            .GetCurrentAsync(harness.FirstUserId);

        Assert.True(inventory.IsSuccess);
        var item = Assert.Single(inventory.Data!.Items);
        Assert.Equal(harness.FirstUserItemId, item.ItemId);
        Assert.Equal("First User Cosmetic", item.ItemName);
        Assert.Equal("CHARACTER", item.Category);
        Assert.Equal("test://inventory/first", item.AssetReference);
        Assert.Equal(InventoryAcquisitionSource.PURCHASE, item.Source);
        Assert.Equal(purchase.Data!.PurchaseId, item.PurchaseId);
    }

    private static ClaimsPrincipal Principal(Guid userId) => new(new ClaimsIdentity(
        [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
        "Test"));

    private sealed class InventoryHarness : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private InventoryHarness(
            SqliteConnection connection,
            AppDbContext db,
            Guid firstUserId,
            Guid firstUserItemId,
            Guid secondUserItemId)
        {
            _connection = connection;
            Db = db;
            FirstUserId = firstUserId;
            FirstUserItemId = firstUserItemId;
            SecondUserItemId = secondUserItemId;
        }

        public AppDbContext Db { get; }
        public Guid FirstUserId { get; }
        public Guid FirstUserItemId { get; }
        public Guid SecondUserItemId { get; }

        public static async Task<InventoryHarness> CreateAsync(bool includeInitialInventory = true)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options);
            await db.Database.EnsureCreatedAsync();

            var firstUser = CreateUser("first");
            var secondUser = CreateUser("second");
            var firstItem = CreateItem("First User Cosmetic", "test://inventory/first");
            var secondItem = CreateItem("Second User Cosmetic", "test://inventory/second");
            db.Users.AddRange(firstUser, secondUser);
            db.ShopItems.AddRange(firstItem, secondItem);
            if (includeInitialInventory)
            {
                db.InventoryItems.AddRange(
                    CreateInventory(firstUser.Id, firstItem.ItemId),
                    CreateInventory(secondUser.Id, secondItem.ItemId));
            }

            await db.SaveChangesAsync();
            return new InventoryHarness(
                connection,
                db,
                firstUser.Id,
                firstItem.ItemId,
                secondItem.ItemId);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }

        private static User CreateUser(string name) => new()
        {
            Id = Guid.NewGuid(),
            Email = $"{name}-{Guid.NewGuid():N}@echo.invalid",
            Username = $"{name}-{Guid.NewGuid():N}",
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
        };

        private static ShopItem CreateItem(string name, string assetReference) => new()
        {
            ItemId = Guid.NewGuid(),
            ItemName = name,
            Description = $"{name} description",
            Category = "CHARACTER",
            Price = 100,
            AssetReference = assetReference,
            IsActive = true,
            CreatedAtUtc = Now.UtcDateTime,
            UpdatedAtUtc = Now.UtcDateTime
        };

        private static InventoryItem CreateInventory(Guid userId, Guid itemId) => new()
        {
            InventoryItemId = Guid.NewGuid(),
            UserId = userId,
            ShopItemId = itemId,
            Source = InventoryAcquisitionSource.PURCHASE,
            AcquiredAtUtc = Now.UtcDateTime
        };
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
