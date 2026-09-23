using System.Security.Claims;
using System.Text.Json;
using EchoProtocol.Api.Common;
using EchoProtocol.Api.Controllers;
using EchoProtocol.Api.Data;
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

public sealed class PurchaseServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 20, 16, 0, 0, TimeSpan.Zero);

    [Fact, Trait("Category", "M4PurchaseUnit")]
    public async Task PurchaseSucceedsAndPersistsAtomicRecords()
    {
        await using var harness = await PurchaseHarness.CreateAsync();

        var result = await harness.Service.PurchaseAsync(
            harness.UserId,
            Request(harness.FirstItemId, "purchase-success"));

        Assert.True(result.IsSuccess);
        Assert.False(result.Data!.IsReplay);
        Assert.Equal(100, result.Data.PricePaid);
        Assert.Equal(400, result.Data.WalletBalance);
        Assert.Equal(400, (await harness.Db.Wallets.SingleAsync()).Balance);
        Assert.Single(await harness.Db.WalletTransactions.ToListAsync());
        Assert.Single(await harness.Db.PurchaseTransactions.ToListAsync());
        Assert.Single(await harness.Db.InventoryItems.ToListAsync());
    }

    [Fact, Trait("Category", "M4PurchaseUnit")]
    public async Task InsufficientBalanceDoesNotMutateWallet()
    {
        await using var harness = await PurchaseHarness.CreateAsync(balance: 50);

        var result = await harness.Service.PurchaseAsync(
            harness.UserId,
            Request(harness.FirstItemId, "insufficient"));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.InsufficientWalletBalance, result.ErrorCode);
        Assert.Equal(50, (await harness.Db.Wallets.SingleAsync()).Balance);
        Assert.Empty(await harness.Db.WalletTransactions.ToListAsync());
        Assert.Empty(await harness.Db.InventoryItems.ToListAsync());
    }

    [Fact, Trait("Category", "M4PurchaseUnit")]
    public async Task MissingItemIsRejected()
    {
        await using var harness = await PurchaseHarness.CreateAsync();

        var result = await harness.Service.PurchaseAsync(
            harness.UserId,
            Request(Guid.NewGuid(), "missing-item"));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ShopItemNotFound, result.ErrorCode);
    }

    [Fact, Trait("Category", "M4PurchaseUnit")]
    public async Task DisabledItemIsRejected()
    {
        await using var harness = await PurchaseHarness.CreateAsync(firstItemActive: false);

        var result = await harness.Service.PurchaseAsync(
            harness.UserId,
            Request(harness.FirstItemId, "disabled-item"));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ShopItemInactive, result.ErrorCode);
        Assert.Equal(500, (await harness.Db.Wallets.SingleAsync()).Balance);
    }

    [Fact, Trait("Category", "M4PurchaseUnit")]
    public async Task AlreadyOwnedCosmeticIsRejected()
    {
        await using var harness = await PurchaseHarness.CreateAsync(alreadyOwnsFirstItem: true);

        var result = await harness.Service.PurchaseAsync(
            harness.UserId,
            Request(harness.FirstItemId, "already-owned"));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ShopItemAlreadyOwned, result.ErrorCode);
        Assert.Equal(500, (await harness.Db.Wallets.SingleAsync()).Balance);
    }

    [Fact, Trait("Category", "M4PurchaseUnit")]
    public async Task IdenticalIdempotencyRetryReturnsStoredPurchase()
    {
        await using var harness = await PurchaseHarness.CreateAsync();
        var first = await harness.Service.PurchaseAsync(
            harness.UserId,
            Request(harness.FirstItemId, "retry-key"));

        var retry = await harness.Service.PurchaseAsync(
            harness.UserId,
            Request(harness.FirstItemId, "retry-key"));

        Assert.True(first.IsSuccess);
        Assert.True(retry.IsSuccess);
        Assert.True(retry.Data!.IsReplay);
        Assert.Equal(first.Data!.PurchaseId, retry.Data.PurchaseId);
        Assert.Equal(400, retry.Data.WalletBalance);
        Assert.Equal(400, (await harness.Db.Wallets.SingleAsync()).Balance);
        Assert.Single(await harness.Db.PurchaseTransactions.ToListAsync());
        Assert.Single(await harness.Db.InventoryItems.ToListAsync());
    }

    [Fact, Trait("Category", "M4PurchaseUnit")]
    public async Task SameIdempotencyKeyWithDifferentItemReturnsConflict()
    {
        await using var harness = await PurchaseHarness.CreateAsync();
        var first = await harness.Service.PurchaseAsync(
            harness.UserId,
            Request(harness.FirstItemId, "conflicting-key"));

        var conflict = await harness.Service.PurchaseAsync(
            harness.UserId,
            Request(harness.SecondItemId, "conflicting-key"));

        Assert.True(first.IsSuccess);
        Assert.False(conflict.IsSuccess);
        Assert.Equal(ErrorCodes.PurchaseIdempotencyConflict, conflict.ErrorCode);
        Assert.Equal(400, (await harness.Db.Wallets.SingleAsync()).Balance);
        Assert.Single(await harness.Db.PurchaseTransactions.ToListAsync());
    }

    [Fact, Trait("Category", "M4PurchaseUnit")]
    public async Task UnauthorizedControllerRequestIsRejected()
    {
        await using var harness = await PurchaseHarness.CreateAsync();
        var controller = new ShopPurchaseController(
            harness.Service,
            NullLogger<ShopPurchaseController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal()
                }
            }
        };

        var action = await controller.Purchase(
            Request(harness.FirstItemId, "unauthorized"),
            CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(action);
        var response = Assert.IsType<ApiResponse<object>>(unauthorized.Value);
        Assert.Equal(ErrorCodes.TokenInvalid, response.ErrorCode);
        Assert.Empty(await harness.Db.PurchaseTransactions.ToListAsync());
    }

    [Fact, Trait("Category", "M4PurchaseUnit")]
    public async Task ClientSuppliedAuthoritativeFieldsAreRejected()
    {
        await using var harness = await PurchaseHarness.CreateAsync();
        var request = Request(harness.FirstItemId, "unsupported-fields");
        request.ExtensionData = new Dictionary<string, JsonElement>
        {
            ["price"] = JsonDocument.Parse("1").RootElement.Clone(),
            ["walletBalance"] = JsonDocument.Parse("999999").RootElement.Clone()
        };

        var result = await harness.Service.PurchaseAsync(harness.UserId, request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationError, result.ErrorCode);
        Assert.Equal(500, (await harness.Db.Wallets.SingleAsync()).Balance);
    }

    private static PurchaseRequest Request(Guid itemId, string key) => new()
    {
        ItemId = itemId,
        IdempotencyKey = key
    };

    private sealed class PurchaseHarness : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private PurchaseHarness(
            SqliteConnection connection,
            AppDbContext db,
            Guid userId,
            Guid firstItemId,
            Guid secondItemId)
        {
            _connection = connection;
            Db = db;
            Service = new PurchaseService(db, new FixedTimeProvider(Now));
            UserId = userId;
            FirstItemId = firstItemId;
            SecondItemId = secondItemId;
        }

        public AppDbContext Db { get; }
        public PurchaseService Service { get; }
        public Guid UserId { get; }
        public Guid FirstItemId { get; }
        public Guid SecondItemId { get; }

        public static async Task<PurchaseHarness> CreateAsync(
            int balance = 500,
            bool firstItemActive = true,
            bool alreadyOwnsFirstItem = false)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options);
            await db.Database.EnsureCreatedAsync();

            var userId = Guid.NewGuid();
            var firstItem = CreateItem("First Cosmetic", 100, firstItemActive);
            var secondItem = CreateItem("Second Cosmetic", 150, true);
            db.Users.Add(new User
            {
                Id = userId,
                Email = $"purchase-{userId:N}@echo.invalid",
                Username = $"purchase-{userId:N}",
                PasswordHash = "not-used",
                Role = UserRole.PLAYER,
                Status = UserStatus.ACTIVE,
                CreatedAt = Now.UtcDateTime,
                UpdatedAt = Now.UtcDateTime,
                Wallet = new Wallet
                {
                    Id = Guid.NewGuid(),
                    Balance = balance,
                    UpdatedAt = Now.UtcDateTime
                }
            });
            db.ShopItems.AddRange(firstItem, secondItem);
            if (alreadyOwnsFirstItem)
            {
                db.InventoryItems.Add(new InventoryItem
                {
                    InventoryItemId = Guid.NewGuid(),
                    UserId = userId,
                    ShopItemId = firstItem.ItemId,
                    Source = InventoryAcquisitionSource.PURCHASE,
                    AcquiredAtUtc = Now.UtcDateTime
                });
            }

            await db.SaveChangesAsync();
            return new PurchaseHarness(
                connection,
                db,
                userId,
                firstItem.ItemId,
                secondItem.ItemId);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }

        private static ShopItem CreateItem(string name, int price, bool active) => new()
        {
            ItemId = Guid.NewGuid(),
            ItemName = name,
            Description = $"{name} description",
            Category = "CHARACTER",
            Price = price,
            AssetReference = $"test://purchase/{name.Replace(' ', '-').ToLowerInvariant()}",
            IsActive = active,
            CreatedAtUtc = Now.UtcDateTime,
            UpdatedAtUtc = Now.UtcDateTime
        };
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
