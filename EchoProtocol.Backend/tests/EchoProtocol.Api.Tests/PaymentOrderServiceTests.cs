using EchoProtocol.Api.Common;
using EchoProtocol.Api.Configurations;
using EchoProtocol.Api.Data;
using EchoProtocol.Api.DTOs.Payments;
using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services;
using EchoProtocol.Api.Services.Models;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace EchoProtocol.Api.Tests;

public sealed class PaymentOrderServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 21, 14, 0, 0, TimeSpan.Zero);

    [Fact, Trait("Category", "M4PaymentOrderUnit")]
    public async Task CreateOrderUsesAuthoritativeProductConfiguration()
    {
        await using var harness = await Harness.CreateAsync();

        var result = await harness.Service.CreateAsync(
            harness.OwnerId,
            Request("create-success", "TEST_PRODUCT"));

        Assert.True(result.IsSuccess);
        Assert.False(result.Data!.IsReplay);
        Assert.Equal(PaymentOrderStatus.CREATED, result.Data.Status);
        Assert.Equal(125_000m, result.Data.Amount);
        Assert.Equal("VND", result.Data.Currency);
        Assert.Equal("AUTOMATED_TEST_ONLY", result.Data.Purpose);
        var stored = await harness.Db.PaymentOrders.SingleAsync();
        Assert.Equal(PaymentFulfillmentKind.WALLET_CREDIT, stored.FulfillmentKind);
        Assert.Equal(150, stored.WalletCreditAmount);
        Assert.Single(await harness.Db.PaymentOrders.ToListAsync());
    }

    [Fact, Trait("Category", "M4PaymentOrderUnit")]
    public async Task IdenticalRetryReturnsStoredOrder()
    {
        await using var harness = await Harness.CreateAsync();
        var first = await harness.Service.CreateAsync(
            harness.OwnerId, Request("same-key", "TEST_PRODUCT"));

        var retry = await harness.Service.CreateAsync(
            harness.OwnerId, Request("same-key", "TEST_PRODUCT"));

        Assert.True(retry.IsSuccess);
        Assert.True(retry.Data!.IsReplay);
        Assert.Equal(first.Data!.PaymentOrderId, retry.Data.PaymentOrderId);
        Assert.Single(await harness.Db.PaymentOrders.ToListAsync());
    }

    [Fact, Trait("Category", "M4PaymentOrderUnit")]
    public async Task SameKeyWithDifferentSemanticPayloadReturnsConflict()
    {
        await using var harness = await Harness.CreateAsync();
        await harness.Service.CreateAsync(
            harness.OwnerId, Request("conflict-key", "TEST_PRODUCT"));

        var conflict = await harness.Service.CreateAsync(
            harness.OwnerId, Request("conflict-key", "SECOND_TEST_PRODUCT"));

        Assert.False(conflict.IsSuccess);
        Assert.Equal(ErrorCodes.PaymentIdempotencyConflict, conflict.ErrorCode);
        Assert.Single(await harness.Db.PaymentOrders.ToListAsync());
    }

    [Theory, Trait("Category", "M4PaymentOrderUnit")]
    [InlineData("UNKNOWN_PRODUCT", "TEST_PROVIDER", "PAYMENT_PRODUCT_INVALID")]
    [InlineData("TEST_PRODUCT", "UNKNOWN_PROVIDER", "PAYMENT_PROVIDER_INVALID")]
    public async Task InvalidProductOrProviderIsRejected(
        string productReference,
        string provider,
        string expectedCode)
    {
        await using var harness = await Harness.CreateAsync();
        var request = Request("invalid-catalog", productReference);
        request.Provider = provider;

        var result = await harness.Service.CreateAsync(harness.OwnerId, request);

        Assert.False(result.IsSuccess);
        Assert.Equal(expectedCode, result.ErrorCode);
        Assert.Empty(await harness.Db.PaymentOrders.ToListAsync());
    }

    [Fact, Trait("Category", "M4PaymentOrderUnit")]
    public async Task InvalidStateTransitionIsRejected()
    {
        await using var harness = await Harness.CreateAsync();
        var order = await harness.CreateOrderAsync("invalid-transition");

        var result = await harness.Service.TransitionAsync(
            order.PaymentOrderId,
            new PaymentOrderTransition(
                PaymentOrderStatus.PAID,
                ProviderTransactionId: "transaction-without-pending"));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.PaymentInvalidStateTransition, result.ErrorCode);
        Assert.Equal(PaymentOrderStatus.CREATED,
            (await harness.Db.PaymentOrders.SingleAsync()).Status);
    }

    [Fact, Trait("Category", "M4PaymentOrderUnit")]
    public async Task CreatedTransitionsToPendingPayment()
    {
        await using var harness = await Harness.CreateAsync();
        var order = await harness.CreateOrderAsync("to-pending");

        var result = await harness.Service.TransitionAsync(
            order.PaymentOrderId,
            new PaymentOrderTransition(
                PaymentOrderStatus.PENDING_PAYMENT,
                ProviderOrderId: "provider-order-1"));

        Assert.True(result.IsSuccess);
        Assert.Equal(PaymentOrderStatus.PENDING_PAYMENT, result.Data!.Status);
        Assert.Equal("provider-order-1", result.Data.ProviderOrderId);
    }

    [Fact, Trait("Category", "M4PaymentOrderUnit")]
    public async Task PendingPaymentCannotBypassVerifiedWebhookBoundary()
    {
        await using var harness = await Harness.CreateAsync();
        var order = await harness.CreatePendingOrderAsync("to-paid");

        var result = await harness.Service.TransitionAsync(
            order.PaymentOrderId,
            new PaymentOrderTransition(
                PaymentOrderStatus.PAID,
                ProviderTransactionId: "provider-transaction-1"));

        Assert.True(PaymentOrderStateMachine.CanTransition(
            PaymentOrderStatus.PENDING_PAYMENT, PaymentOrderStatus.PAID));
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.PaymentInvalidStateTransition, result.ErrorCode);
    }

    [Fact, Trait("Category", "M4PaymentOrderUnit")]
    public async Task PaidCannotBypassFulfillmentService()
    {
        await using var harness = await Harness.CreateAsync();
        var order = await harness.CreatePendingOrderAsync("to-fulfilled");
        var stored = await harness.Db.PaymentOrders.SingleAsync();
        stored.Status = PaymentOrderStatus.PAID;
        stored.ProviderTransactionId = "provider-transaction-fulfilled";
        stored.PaidAtUtc = Now.UtcDateTime;
        await harness.Db.SaveChangesAsync();

        var result = await harness.Service.TransitionAsync(
            order.PaymentOrderId,
            new PaymentOrderTransition(
                PaymentOrderStatus.FULFILLED,
                FulfillmentReference: "fulfillment-1"));

        Assert.True(PaymentOrderStateMachine.CanTransition(
            PaymentOrderStatus.PAID, PaymentOrderStatus.FULFILLED));
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.PaymentInvalidStateTransition, result.ErrorCode);
    }

    [Theory, Trait("Category", "M4PaymentOrderUnit")]
    [InlineData(PaymentOrderStatus.FULFILLED)]
    [InlineData(PaymentOrderStatus.FAILED)]
    [InlineData(PaymentOrderStatus.CANCELLED)]
    [InlineData(PaymentOrderStatus.EXPIRED)]
    public void TerminalStateCannotTransition(PaymentOrderStatus terminalStatus)
    {
        foreach (var target in Enum.GetValues<PaymentOrderStatus>())
            Assert.False(PaymentOrderStateMachine.CanTransition(terminalStatus, target));
    }

    [Fact, Trait("Category", "M4PaymentOrderUnit")]
    public async Task UserCannotReadAnotherUsersOrder()
    {
        await using var harness = await Harness.CreateAsync();
        var order = await harness.CreateOrderAsync("private-order");

        var result = await harness.Service.GetOwnedAsync(harness.OtherUserId, order.PaymentOrderId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.PaymentOrderForbidden, result.ErrorCode);
        Assert.Null(result.Data);
    }

    [Fact, Trait("Category", "M4PaymentOrderUnit")]
    public async Task HistoryReturnsOnlyOwnedOrdersWithStoredStatusesNewestFirst()
    {
        await using var harness = await Harness.CreateAsync();
        var oldest = HistoryOrder(harness.OwnerId, "history-oldest", Now.AddHours(-2).UtcDateTime,
            PaymentOrderStatus.FAILED);
        var newest = HistoryOrder(harness.OwnerId, "history-newest", Now.UtcDateTime,
            PaymentOrderStatus.FULFILLED);
        var foreign = HistoryOrder(harness.OtherUserId, "history-foreign", Now.AddHours(1).UtcDateTime,
            PaymentOrderStatus.PAID);
        harness.Db.PaymentOrders.AddRange(oldest, newest, foreign);
        await harness.Db.SaveChangesAsync();

        var firstPage = await harness.Service.GetOwnedHistoryAsync(harness.OwnerId, 1, 1);
        var secondPage = await harness.Service.GetOwnedHistoryAsync(harness.OwnerId, 2, 1);

        Assert.True(firstPage.IsSuccess, firstPage.Message);
        Assert.Equal(2, firstPage.Data!.TotalItems);
        Assert.Equal(2, firstPage.Data.TotalPages);
        var first = Assert.Single(firstPage.Data.Items);
        Assert.Equal(newest.PaymentOrderId, first.PaymentOrderId);
        Assert.Equal(PaymentOrderStatus.FULFILLED, first.Status);
        var second = Assert.Single(secondPage.Data!.Items);
        Assert.Equal(oldest.PaymentOrderId, second.PaymentOrderId);
        Assert.Equal(PaymentOrderStatus.FAILED, second.Status);
        Assert.DoesNotContain(firstPage.Data.Items, item => item.PaymentOrderId == foreign.PaymentOrderId);
    }

    [Theory, Trait("Category", "M4PaymentOrderUnit")]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    [InlineData(int.MaxValue, 100)]
    public async Task HistoryRejectsInvalidPagination(int page, int pageSize)
    {
        await using var harness = await Harness.CreateAsync();

        var result = await harness.Service.GetOwnedHistoryAsync(harness.OwnerId, page, pageSize);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationError, result.ErrorCode);
    }

    [Fact, Trait("Category", "M4PaymentOrderUnit")]
    public async Task ClientSuppliedAuthoritativePaymentFieldsAreRejected()
    {
        await using var harness = await Harness.CreateAsync();
        var request = Request("client-authority", "TEST_PRODUCT");
        request.ExtensionData = new Dictionary<string, JsonElement>
        {
            ["amount"] = JsonDocument.Parse("1").RootElement.Clone(),
            ["status"] = JsonDocument.Parse("\"PAID\"").RootElement.Clone(),
            ["providerTransactionId"] = JsonDocument.Parse("\"forged\"").RootElement.Clone(),
            ["recipientUserId"] = JsonDocument.Parse("\"00000000-0000-0000-0000-000000000001\"").RootElement.Clone()
        };

        var result = await harness.Service.CreateAsync(harness.OwnerId, request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationError, result.ErrorCode);
        Assert.Empty(await harness.Db.PaymentOrders.ToListAsync());
    }

    [Fact]
    public async Task CosmeticCheckoutValidatesLevelOwnershipAndReservesOnePurchase()
    {
        await using var h = await Harness.CreateAsync();
        var item = await PrepareCosmeticAsync(h);
        var service = CosmeticService(h.Db, item.ItemId);
        var profile = await h.Db.PlayerProfiles.SingleAsync();
        profile.Level = 2; await h.Db.SaveChangesAsync();
        Assert.Equal(ErrorCodes.ShopLevelRequired, (await service.CreateAsync(h.OwnerId, Request("low-level", "COSMETIC_TEST"))).ErrorCode);
        profile.Level = 10; await h.Db.SaveChangesAsync();
        var order = await service.CreateAsync(h.OwnerId, Request("valid-cosmetic", "COSMETIC_TEST"));
        Assert.True(order.IsSuccess, order.Message);
        Assert.Equal(6, order.Data!.RequiredLevelAtPurchase);
        Assert.NotNull(order.Data.EntitlementValidatedAtUtc);
        Assert.True((await service.CreateAsync(h.OwnerId, Request("valid-cosmetic", "COSMETIC_TEST"))).Data!.IsReplay);
        Assert.Equal(ErrorCodes.PaymentOrderConflict, (await service.CreateAsync(h.OwnerId, Request("second-cosmetic", "COSMETIC_TEST"))).ErrorCode);
        var purchase = await new PurchaseService(h.Db, new FixedTimeProvider(Now)).PurchaseAsync(h.OwnerId,
            new EchoProtocol.Api.DTOs.Shop.PurchaseRequest { ItemId = item.ItemId, IdempotencyKey = "credits-during-checkout" });
        Assert.Equal(ErrorCodes.PaymentOrderConflict, purchase.ErrorCode);
        Assert.Equal(10000, (await h.Db.Wallets.SingleAsync()).Balance);
    }

    [Theory]
    [InlineData("TEAM_TOOL", true, "PAYMENT_CONFIGURATION_INVALID")]
    [InlineData("CHARACTER", true, "PAYMENT_CONFIGURATION_INVALID")]
    [InlineData("CHARACTER_OUTFIT", false, "SHOP_ITEM_INACTIVE")]
    public async Task DirectPaymentCannotSellGameplayItemsOrInactiveCosmetics(string category, bool active, string expected)
    {
        await using var h = await Harness.CreateAsync();
        var item = await PrepareCosmeticAsync(h); item.Category = category; item.IsActive = active;
        await h.Db.SaveChangesAsync();
        var result = await CosmeticService(h.Db, item.ItemId).CreateAsync(h.OwnerId, Request("invalid-cosmetic", "COSMETIC_TEST"));
        Assert.Equal(expected, result.ErrorCode);
        Assert.Empty(await h.Db.PaymentOrders.ToListAsync());
    }

    [Fact]
    public async Task PaidCosmeticHonorsStoredTermsAndLegacyDuplicatePaymentsReuseTheEntitlement()
    {
        await using var h = await Harness.CreateAsync();
        var item = await PrepareCosmeticAsync(h);
        var order = await CosmeticService(h.Db, item.ItemId).CreateAsync(h.OwnerId, Request("paid-cosmetic", "COSMETIC_TEST"));
        Assert.True(order.IsSuccess, order.Message);
        var stored = await h.Db.PaymentOrders.SingleAsync(); stored.Status = PaymentOrderStatus.PAID;
        stored.PaidAtUtc = Now.UtcDateTime; stored.ProviderOrderId = "test-order"; stored.ProviderTransactionId = "test-payment";
        item.IsActive = false; item.RequiredLevel = 30;
        (await h.Db.PlayerProfiles.SingleAsync()).Level = 1;
        var inventory = new InventoryItem { InventoryItemId = Guid.NewGuid(), UserId = h.OwnerId, ShopItemId = item.ItemId,
            Source = InventoryAcquisitionSource.PURCHASE, AcquiredAtUtc = Now.UtcDateTime };
        h.Db.InventoryItems.Add(inventory);
        await h.Db.SaveChangesAsync();
        var fulfillment = new PaymentFulfillmentService(h.Db, Options.Create(new PaymentCatalogSettings()), new FixedTimeProvider(Now));
        Assert.True((await fulfillment.FulfillAsync(stored.PaymentOrderId)).IsSuccess);
        Assert.True((await fulfillment.FulfillAsync(stored.PaymentOrderId)).Data!.IsReplay);
        var legacyDuplicate = new PaymentOrder { PaymentOrderId = Guid.NewGuid(), UserId = h.OwnerId, Provider = "TEST_PROVIDER",
            Purpose = "LEGACY_DUPLICATE", ProductReference = "OLD_COSMETIC", FulfillmentKind = PaymentFulfillmentKind.INVENTORY_ITEM,
            FulfillmentShopItemId = item.ItemId, Amount = 100, Currency = "VND", Status = PaymentOrderStatus.PAID,
            ProviderOrderId = "legacy-order", ProviderTransactionId = "legacy-payment", PaidAtUtc = Now.UtcDateTime,
            IdempotencyKey = "old-duplicate-paid", RequestFingerprint = new string('a', 64), CreatedAtUtc = Now.UtcDateTime, UpdatedAtUtc = Now.UtcDateTime };
        h.Db.PaymentOrders.Add(legacyDuplicate); await h.Db.SaveChangesAsync();
        Assert.True((await fulfillment.FulfillAsync(legacyDuplicate.PaymentOrderId)).IsSuccess);
        Assert.Single(await h.Db.InventoryItems.ToListAsync());
        Assert.Equal(2, await h.Db.PaymentFulfillments.CountAsync());
        Assert.All(await h.Db.PaymentFulfillments.ToListAsync(), f => Assert.Equal(inventory.InventoryItemId, f.InventoryItemId));
        Assert.Empty(await h.Db.WalletTransactions.ToListAsync());
    }

    [Fact]
    public async Task PaidPetSkinRequiresTheOwnedBasePetAndCannotBePurchasedTwice()
    {
        await using var h = await Harness.CreateAsync();
        var skin = await PrepareCosmeticAsync(h); skin.Category = "PET_SKIN";
        var pet = new ShopItem { ItemId = Guid.NewGuid(), ItemName = "Base pet", Category = "PET", RequiredLevel = 1,
            Price = 700, IsActive = true, CreatedAtUtc = Now.UtcDateTime, UpdatedAtUtc = Now.UtcDateTime };
        h.Db.ShopItems.Add(pet); await h.Db.SaveChangesAsync();
        var service = CosmeticService(h.Db, skin.ItemId, pet.ItemId);
        Assert.Equal(ErrorCodes.PaymentFulfillmentTargetInvalid,
            (await service.CreateAsync(h.OwnerId, Request("missing-base", "COSMETIC_TEST"))).ErrorCode);
        h.Db.InventoryItems.Add(new InventoryItem { InventoryItemId = Guid.NewGuid(), UserId = h.OwnerId, ShopItemId = pet.ItemId,
            Source = InventoryAcquisitionSource.PURCHASE, AcquiredAtUtc = Now.UtcDateTime });
        await h.Db.SaveChangesAsync();
        var checkout = await service.CreateAsync(h.OwnerId, Request("with-base", "COSMETIC_TEST"));
        Assert.True(checkout.IsSuccess, checkout.Message);
        Assert.Equal(pet.ItemId, checkout.Data!.RequiredOwnedItemId);
        h.Db.InventoryItems.Add(new InventoryItem { InventoryItemId = Guid.NewGuid(), UserId = h.OwnerId, ShopItemId = skin.ItemId,
            Source = InventoryAcquisitionSource.PURCHASE, AcquiredAtUtc = Now.UtcDateTime });
        await h.Db.SaveChangesAsync();
        Assert.Equal(ErrorCodes.ShopItemAlreadyOwned,
            (await service.CreateAsync(h.OwnerId, Request("already-owned-skin", "COSMETIC_TEST"))).ErrorCode);
    }

    private static async Task<ShopItem> PrepareCosmeticAsync(Harness h)
    {
        var item = new ShopItem { ItemId = Guid.NewGuid(), ItemName = "Test-only cosmetic", Category = "CHARACTER_OUTFIT",
            Price = 100, RequiredLevel = 6, IsActive = true, AssetReference = "test-only", CreatedAtUtc = Now.UtcDateTime, UpdatedAtUtc = Now.UtcDateTime };
        h.Db.ShopItems.Add(item);
        h.Db.Wallets.Add(new Wallet { Id = Guid.NewGuid(), UserId = h.OwnerId, Balance = 10000, UpdatedAt = Now.UtcDateTime });
        h.Db.PlayerProfiles.Add(new PlayerProfile { Id = Guid.NewGuid(), UserId = h.OwnerId, Level = 10, DisplayName = "Test", CreatedAt = Now.UtcDateTime, UpdatedAt = Now.UtcDateTime });
        await h.Db.SaveChangesAsync(); return item;
    }

    private static PaymentOrderService CosmeticService(AppDbContext db, Guid itemId, Guid? parentId = null) => new(db,
        Options.Create(new PaymentCatalogSettings { AllowedProviders = ["TEST_PROVIDER"], Products = [new PaymentProductSettings {
            ProductReference = "COSMETIC_TEST", Purpose = "COSMETIC", IsActive = true, Amount = 100, Currency = "VND", ExpiresAfterMinutes = 30,
            FulfillmentKind = "INVENTORY_ITEM", ShopItemId = itemId, RequiredOwnedItemId = parentId, UsesDefaultCharacter = parentId is null }] }), new FixedTimeProvider(Now));

    private static CreatePaymentOrderRequest Request(string key, string productReference) => new()
    {
        ProductReference = productReference,
        Provider = "TEST_PROVIDER",
        IdempotencyKey = key
    };

    private static PaymentOrder HistoryOrder(
        Guid userId,
        string idempotencyKey,
        DateTime createdAtUtc,
        PaymentOrderStatus status) => new()
    {
        PaymentOrderId = Guid.NewGuid(),
        UserId = userId,
        Provider = "PAYOS",
        Purpose = "WALLET_CREDIT",
        ProductReference = "WALLET_COIN_150",
        FulfillmentKind = PaymentFulfillmentKind.WALLET_CREDIT,
        WalletCreditAmount = 150,
        Amount = 125_000,
        Currency = "VND",
        Status = status,
        ProviderOrderId = status is PaymentOrderStatus.PENDING_PAYMENT or PaymentOrderStatus.PAID or PaymentOrderStatus.FULFILLED
            ? $"provider-order-{idempotencyKey}"
            : null,
        ProviderTransactionId = status is PaymentOrderStatus.PAID or PaymentOrderStatus.FULFILLED
            ? $"provider-transaction-{idempotencyKey}"
            : null,
        PaidAtUtc = status is PaymentOrderStatus.PAID or PaymentOrderStatus.FULFILLED
            ? createdAtUtc
            : null,
        FulfillmentReference = status == PaymentOrderStatus.FULFILLED
            ? $"fulfillment-{idempotencyKey}"
            : null,
        FulfilledAtUtc = status == PaymentOrderStatus.FULFILLED
            ? createdAtUtc
            : null,
        IdempotencyKey = idempotencyKey,
        RequestFingerprint = new string('A', 64),
        CreatedAtUtc = createdAtUtc,
        UpdatedAtUtc = createdAtUtc
    };

    private sealed class Harness : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private Harness(
            SqliteConnection connection,
            AppDbContext db,
            Guid ownerId,
            Guid otherUserId)
        {
            _connection = connection;
            Db = db;
            OwnerId = ownerId;
            OtherUserId = otherUserId;
            Service = CreateService(db);
        }

        public AppDbContext Db { get; }
        public PaymentOrderService Service { get; }
        public Guid OwnerId { get; }
        public Guid OtherUserId { get; }

        public static async Task<Harness> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options);
            await db.Database.EnsureCreatedAsync();

            var ownerId = Guid.NewGuid();
            var otherUserId = Guid.NewGuid();
            db.Users.AddRange(User(ownerId, "payment-owner"), User(otherUserId, "payment-other"));
            await db.SaveChangesAsync();
            return new Harness(connection, db, ownerId, otherUserId);
        }

        public async Task<PaymentOrderResponse> CreateOrderAsync(string key)
        {
            var result = await Service.CreateAsync(OwnerId, Request(key, "TEST_PRODUCT"));
            return result.Data!;
        }

        public async Task<PaymentOrderResponse> CreatePendingOrderAsync(string key)
        {
            var order = await CreateOrderAsync(key);
            return (await Service.TransitionAsync(
                order.PaymentOrderId,
                new PaymentOrderTransition(
                    PaymentOrderStatus.PENDING_PAYMENT,
                    ProviderOrderId: $"provider-{key}"))).Data!;
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }

        private static PaymentOrderService CreateService(AppDbContext db) => new(
            db,
            Options.Create(new PaymentCatalogSettings
            {
                AllowedProviders = ["TEST_PROVIDER"],
                Products =
                [
                    Product("TEST_PRODUCT", 125_000m),
                    Product("SECOND_TEST_PRODUCT", 250_000m)
                ]
            }),
            new FixedTimeProvider(Now));

        private static PaymentProductSettings Product(string reference, decimal amount) => new()
        {
            ProductReference = reference,
            Purpose = "AUTOMATED_TEST_ONLY",
            Amount = amount,
            Currency = "VND",
            IsActive = true,
            ExpiresAfterMinutes = 30,
            FulfillmentKind = "WALLET_CREDIT",
            WalletCreditAmount = 150
        };

        private static User User(Guid id, string name) => new()
        {
            Id = id,
            Email = $"{name}-{id:N}@test.local",
            Username = $"{name}-{id:N}",
            PasswordHash = "not-used",
            Role = UserRole.PLAYER,
            Status = UserStatus.ACTIVE,
            CreatedAt = Now.UtcDateTime,
            UpdatedAt = Now.UtcDateTime
        };
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
