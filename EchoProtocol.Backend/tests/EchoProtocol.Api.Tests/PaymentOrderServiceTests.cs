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
    public async Task ClientSuppliedAuthoritativePaymentFieldsAreRejected()
    {
        await using var harness = await Harness.CreateAsync();
        var request = Request("client-authority", "TEST_PRODUCT");
        request.ExtensionData = new Dictionary<string, JsonElement>
        {
            ["amount"] = JsonDocument.Parse("1").RootElement.Clone(),
            ["status"] = JsonDocument.Parse("\"PAID\"").RootElement.Clone(),
            ["providerTransactionId"] = JsonDocument.Parse("\"forged\"").RootElement.Clone()
        };

        var result = await harness.Service.CreateAsync(harness.OwnerId, request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationError, result.ErrorCode);
        Assert.Empty(await harness.Db.PaymentOrders.ToListAsync());
    }

    private static CreatePaymentOrderRequest Request(string key, string productReference) => new()
    {
        ProductReference = productReference,
        Provider = "TEST_PROVIDER",
        IdempotencyKey = key
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
            ExpiresAfterMinutes = 30
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
