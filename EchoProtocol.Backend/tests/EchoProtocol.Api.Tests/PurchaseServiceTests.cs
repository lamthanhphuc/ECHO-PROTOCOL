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
    public async Task PurchaseSeededMercenaryChargesCreditsAndGrantsOwnership()
    {
        await using var harness = await PurchaseHarness.CreateAsync(balance: 1750);
        await ShopCatalogSeeder.SeedProductionCatalogAsync(harness.Db, TimeProvider.System, NullLogger.Instance);
        var itemId = Guid.Parse("13000000-0000-0000-0000-000000000003");
        (await harness.Db.PlayerProfiles.SingleAsync()).Level = 10;
        await harness.Db.SaveChangesAsync();

        var purchase = await harness.Service.PurchaseAsync(harness.UserId, Request(itemId, "mercenary"));
        var duplicate = await harness.Service.PurchaseAsync(harness.UserId, Request(itemId, "mercenary-again"));

        Assert.True(purchase.IsSuccess);
        Assert.Equal(1500, purchase.Data!.PricePaid);
        Assert.Equal(250, (await harness.Db.Wallets.SingleAsync()).Balance);
        Assert.Equal(itemId, (await harness.Db.InventoryItems.SingleAsync()).ShopItemId);
        Assert.Equal(ErrorCodes.ShopItemAlreadyOwned, duplicate.ErrorCode);
    }

    [Fact, Trait("Category", "M4PurchaseUnit")]
    public async Task InsufficientBalanceDoesNotMutateWallet()
    {
        await using var harness = await PurchaseHarness.CreateAsync(balance: 50);
        var before = await harness.SnapshotEconomyAsync();

        var result = await harness.Service.PurchaseAsync(
            harness.UserId,
            Request(harness.FirstItemId, "insufficient"));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.InsufficientWalletBalance, result.ErrorCode);
        Assert.Equal(50, (await harness.Db.Wallets.SingleAsync()).Balance);
        Assert.Empty(await harness.Db.WalletTransactions.ToListAsync());
        Assert.Empty(await harness.Db.InventoryItems.ToListAsync());
        Assert.Equal(before, await harness.SnapshotEconomyAsync());
    }

    [Fact, Trait("Category", "M4PurchaseUnit")]
    public async Task MissingItemIsRejected()
    {
        await using var harness = await PurchaseHarness.CreateAsync();
        var before = await harness.SnapshotEconomyAsync();

        var result = await harness.Service.PurchaseAsync(
            harness.UserId,
            Request(Guid.NewGuid(), "missing-item"));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ShopItemNotFound, result.ErrorCode);
        Assert.Equal(before, await harness.SnapshotEconomyAsync());
    }

    [Fact, Trait("Category", "M4PurchaseUnit")]
    public async Task DisabledItemIsRejected()
    {
        await using var harness = await PurchaseHarness.CreateAsync(firstItemActive: false);
        var before = await harness.SnapshotEconomyAsync();

        var result = await harness.Service.PurchaseAsync(
            harness.UserId,
            Request(harness.FirstItemId, "disabled-item"));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ShopItemInactive, result.ErrorCode);
        Assert.Equal(500, (await harness.Db.Wallets.SingleAsync()).Balance);
        Assert.Equal(before, await harness.SnapshotEconomyAsync());
    }

    [Fact, Trait("Category", "M4PurchaseUnit")]
    public async Task AlreadyOwnedCosmeticIsRejected()
    {
        await using var harness = await PurchaseHarness.CreateAsync(alreadyOwnsFirstItem: true);
        var before = await harness.SnapshotEconomyAsync();

        var result = await harness.Service.PurchaseAsync(
            harness.UserId,
            Request(harness.FirstItemId, "already-owned"));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ShopItemAlreadyOwned, result.ErrorCode);
        Assert.Equal(500, (await harness.Db.Wallets.SingleAsync()).Balance);
        Assert.Equal(before, await harness.SnapshotEconomyAsync());
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
    public async Task PurchaseHistoryIsOwnerScopedAndPaginated()
    {
        await using var harness = await PurchaseHarness.CreateAsync();
        var purchase = await harness.Service.PurchaseAsync(
            harness.UserId, Request(harness.FirstItemId, "history-first"));
        Assert.True(purchase.IsSuccess);
        var secondPurchase = await harness.Service.PurchaseAsync(
            harness.UserId, Request(harness.SecondItemId, "history-second"));
        Assert.True(secondPurchase.IsSuccess);

        var otherUserId = Guid.NewGuid();
        harness.Db.Users.Add(new User
        {
            Id = otherUserId,
            Email = $"purchase-{otherUserId:N}@echo.invalid",
            Username = $"purchase-{otherUserId:N}",
            PasswordHash = "not-used",
            Role = UserRole.PLAYER,
            Status = UserStatus.ACTIVE,
            CreatedAt = Now.UtcDateTime,
            UpdatedAt = Now.UtcDateTime,
            Wallet = new Wallet { Id = Guid.NewGuid(), Balance = 500, UpdatedAt = Now.UtcDateTime }
        });
        harness.Db.PlayerProfiles.Add(new PlayerProfile
        {
            Id = Guid.NewGuid(), UserId = otherUserId, DisplayName = "Other buyer", Level = 1,
            LevelCurveVersion = "LEVEL_V3", CreatedAt = Now.UtcDateTime, UpdatedAt = Now.UtcDateTime
        });
        await harness.Db.SaveChangesAsync();
        Assert.True((await harness.Service.PurchaseAsync(
            otherUserId, Request(harness.FirstItemId, "history-other"))).IsSuccess);

        var controller = new ShopPurchaseController(
            harness.Service,
            NullLogger<ShopPurchaseController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, harness.UserId.ToString())], "Test"))
                }
            }
        };
        var action = await controller.GetOwnedHistory(page: 2, pageSize: 1);
        var response = Assert.IsAssignableFrom<ObjectResult>(action);
        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        var body = Assert.IsType<ApiResponse<PurchaseHistoryResponse>>(response.Value);
        Assert.Single(body.Data!.Items);
        Assert.Equal(2, body.Data.TotalItems);
        Assert.Equal(2, body.Data.TotalPages);
        var pageTwoItem = Assert.Single(body.Data.Items);
        var firstPageAction = await controller.GetOwnedHistory(page: 1, pageSize: 1);
        var firstPageBody = Assert.IsType<ApiResponse<PurchaseHistoryResponse>>(
            Assert.IsAssignableFrom<ObjectResult>(firstPageAction).Value);
        Assert.Equal(1, firstPageBody.Data!.Page);
        var pageOneItem = Assert.Single(firstPageBody.Data.Items);
        Assert.Contains(pageOneItem.PurchaseId, new[] { purchase.Data!.PurchaseId, secondPurchase.Data!.PurchaseId });
        Assert.Contains(pageTwoItem.PurchaseId, new[] { purchase.Data.PurchaseId, secondPurchase.Data.PurchaseId });
        Assert.NotEqual(pageOneItem.PurchaseId, pageTwoItem.PurchaseId);
        var otherHistory = await harness.Service.GetOwnedHistoryAsync(otherUserId, 1, 20);
        Assert.Equal(1, otherHistory.Data!.TotalItems);
        var otherPurchaseId = await harness.Db.PurchaseTransactions
            .Where(x => x.UserId == otherUserId).Select(x => x.PurchaseId).SingleAsync();
        Assert.NotEqual(otherPurchaseId, pageOneItem.PurchaseId);
        Assert.NotEqual(otherPurchaseId, pageTwoItem.PurchaseId);
    }

    [Fact, Trait("Category", "M4PurchaseUnit")]
    public async Task UnauthorizedPurchaseHistoryIsRejected()
    {
        await using var harness = await PurchaseHarness.CreateAsync();
        var controller = new ShopPurchaseController(
            harness.Service,
            NullLogger<ShopPurchaseController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal() }
            }
        };

        var action = await controller.GetOwnedHistory();

        Assert.IsType<UnauthorizedObjectResult>(action);
    }

    [Fact, Trait("Category", "M4PurchaseUnit")]
    public async Task PurchaseHistoryOutOfRangeAndExtremePagesDoNotWrapOffsets()
    {
        await using var harness = await PurchaseHarness.CreateAsync();
        Assert.True((await harness.Service.PurchaseAsync(
            harness.UserId, Request(harness.FirstItemId, "history-boundary"))).IsSuccess);

        var ordinaryOutOfRange = await harness.Service.GetOwnedHistoryAsync(harness.UserId, 2, 1);
        Assert.True(ordinaryOutOfRange.IsSuccess);
        Assert.Empty(ordinaryOutOfRange.Data!.Items);
        Assert.Equal(2, ordinaryOutOfRange.Data.Page);
        Assert.Equal(1, ordinaryOutOfRange.Data.TotalPages);

        var controller = new ShopPurchaseController(
            harness.Service,
            NullLogger<ShopPurchaseController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, harness.UserId.ToString())], "Test"))
                }
            }
        };
        var outOfRangeAction = await controller.GetOwnedHistory(page: 2, pageSize: 1);
        var outOfRangeResponse = Assert.IsType<OkObjectResult>(outOfRangeAction);
        var outOfRangeBody = Assert.IsType<ApiResponse<PurchaseHistoryResponse>>(outOfRangeResponse.Value);
        Assert.Empty(outOfRangeBody.Data!.Items);
        Assert.Equal(1, outOfRangeBody.Data.TotalPages);

        var invalidPageSizeAction = await controller.GetOwnedHistory(page: 1, pageSize: 101);
        Assert.IsType<BadRequestObjectResult>(invalidPageSizeAction);

        var maximumPage = await harness.Service.GetOwnedHistoryAsync(
            harness.UserId, int.MaxValue, 1);
        Assert.True(maximumPage.IsSuccess);
        Assert.Empty(maximumPage.Data!.Items);
        Assert.Equal(int.MaxValue, maximumPage.Data.Page);
        Assert.Equal(1, maximumPage.Data.TotalPages);

        var overflowingOffset = await harness.Service.GetOwnedHistoryAsync(
            harness.UserId, int.MaxValue, 2);
        Assert.False(overflowingOffset.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationError, overflowingOffset.ErrorCode);
    }

    [Theory, Trait("Category", "M4PurchaseUnit")]
    [InlineData(0, 20)]
    [InlineData(-1, 20)]
    [InlineData(1, 0)]
    [InlineData(1, -1)]
    [InlineData(1, 101)]
    [InlineData(1, int.MaxValue)]
    public async Task PurchaseHistoryRejectsInvalidPageAndPageSizeValues(int page, int pageSize)
    {
        await using var harness = await PurchaseHarness.CreateAsync();

        var result = await harness.Service.GetOwnedHistoryAsync(harness.UserId, page, pageSize);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationError, result.ErrorCode);
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

    [Fact]
    public async Task LevelGateRejectsWithoutSpendingAndUnlocksAtRequiredLevel()
    {
        await using var harness = await PurchaseHarness.CreateAsync();
        var item = await harness.Db.ShopItems.SingleAsync(x => x.ItemId == harness.FirstItemId);
        item.RequiredLevel = 3;
        await harness.Db.SaveChangesAsync();
        var result = await harness.Service.PurchaseAsync(harness.UserId, Request(item.ItemId, "level-gate"));
        Assert.Equal("SHOP_LEVEL_REQUIRED", result.ErrorCode);
        Assert.Equal(500, (await harness.Db.Wallets.SingleAsync()).Balance);
        Assert.Empty(await harness.Db.PurchaseTransactions.ToListAsync());
        (await harness.Db.PlayerProfiles.SingleAsync()).Level = 3;
        await harness.Db.SaveChangesAsync();
        Assert.True((await harness.Service.PurchaseAsync(harness.UserId, Request(item.ItemId, "level-gate"))).IsSuccess);
        item.RequiredLevel = 10;
        await harness.Db.SaveChangesAsync();
        Assert.True((await harness.Service.PurchaseAsync(harness.UserId, Request(item.ItemId, "level-gate"))).Data!.IsReplay);
    }

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

        public async Task<string> SnapshotEconomyAsync()
        {
            var wallet = await Db.Wallets.SingleAsync(x => x.UserId == UserId);
            var ledger = await Db.WalletTransactions.AsNoTracking()
                .Where(x => x.WalletId == wallet.Id).OrderBy(x => x.Id)
                .Select(x => $"{x.Id:N}:{x.Type}:{x.Amount}:{x.BalanceBefore}:{x.BalanceAfter}:{x.ReferenceId:N}")
                .ToArrayAsync();
            var purchases = await Db.PurchaseTransactions.AsNoTracking().Where(x => x.UserId == UserId)
                .OrderBy(x => x.PurchaseId).Select(x => $"{x.PurchaseId:N}:{x.ShopItemId:N}:{x.PriceAtPurchase}:{x.Status}")
                .ToArrayAsync();
            var inventory = await Db.InventoryItems.AsNoTracking().Where(x => x.UserId == UserId)
                .OrderBy(x => x.InventoryItemId).Select(x => $"{x.InventoryItemId:N}:{x.ShopItemId:N}:{x.PurchaseId}")
                .ToArrayAsync();
            return $"{wallet.Balance}|{string.Join(';', ledger)}|{string.Join(';', purchases)}|{string.Join(';', inventory)}";
        }

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
            db.PlayerProfiles.Add(new PlayerProfile { Id = Guid.NewGuid(), UserId = userId,
                DisplayName = "Buyer", Level = 1, LevelCurveVersion = "LEVEL_V3",
                CreatedAt = Now.UtcDateTime, UpdatedAt = Now.UtcDateTime });
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
