using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EchoProtocol.Api.Common;
using EchoProtocol.Api.Configurations;
using EchoProtocol.Api.Data;
using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services;
using EchoProtocol.Api.Services.Interfaces;
using EchoProtocol.Api.Services.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace EchoProtocol.Api.Tests;

public sealed class PaymentProcessingTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 21, 15, 0, 0, TimeSpan.Zero);

    [Fact, Trait("Category", "M4PaymentProcessingUnit")]
    public async Task ValidCheckoutPersistsProviderIdentityAndPendingState()
    {
        await using var harness = await Harness.CreateAsync();
        var provider = new FakeProvider();
        var service = harness.CheckoutService(provider);

        var result = await service.CreateCheckoutAsync(harness.UserId, harness.OrderId);

        Assert.True(result.IsSuccess);
        Assert.Equal(PaymentOrderStatus.PENDING_PAYMENT, result.Data!.Status);
        Assert.StartsWith("https://pay.test/", result.Data.CheckoutUrl);
        Assert.Equal(1, provider.CreateCalls);
        Assert.Equal(125_000, provider.LastRequest!.Amount);
        Assert.Equal(PaymentOrderStatus.PENDING_PAYMENT,
            (await harness.Db.PaymentOrders.SingleAsync()).Status);
    }

    [Fact, Trait("Category", "M4PaymentProcessingUnit")]
    public async Task MissingProviderConfigurationDoesNotCreateCheckout()
    {
        await using var harness = await Harness.CreateAsync();
        var result = await harness.CheckoutService(new FakeProvider { Configured = false })
            .CreateCheckoutAsync(harness.UserId, harness.OrderId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.PaymentProviderConfigurationInvalid, result.ErrorCode);
        Assert.Equal(PaymentOrderStatus.CREATED,
            (await harness.Db.PaymentOrders.SingleAsync()).Status);
    }

    [Fact, Trait("Category", "M4PaymentProcessingUnit")]
    public async Task ProviderHttpFailureLeavesRetrySafeReservation()
    {
        await using var harness = await Harness.CreateAsync();
        var provider = new FakeProvider { ThrowOnCreate = true };

        var result = await harness.CheckoutService(provider)
            .CreateCheckoutAsync(harness.UserId, harness.OrderId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.PaymentProviderUnavailable, result.ErrorCode);
        Assert.Equal(PaymentOrderStatus.CREATED,
            (await harness.Db.PaymentOrders.SingleAsync()).Status);
        Assert.Single(await harness.Db.PaymentCheckouts.ToListAsync());
    }

    [Fact, Trait("Category", "M4PaymentProcessingUnit")]
    public async Task CheckoutRetryReturnsStoredLinkWithoutSecondProviderCreate()
    {
        await using var harness = await Harness.CreateAsync();
        var provider = new FakeProvider();
        var service = harness.CheckoutService(provider);
        await service.CreateCheckoutAsync(harness.UserId, harness.OrderId);

        var retry = await service.CreateCheckoutAsync(harness.UserId, harness.OrderId);

        Assert.True(retry.IsSuccess);
        Assert.True(retry.Data!.IsReplay);
        Assert.Equal(1, provider.CreateCalls);
    }

    [Fact, Trait("Category", "M4PaymentProcessingUnit")]
    public async Task NonOwnerCannotCreateCheckout()
    {
        await using var harness = await Harness.CreateAsync();
        var result = await harness.CheckoutService(new FakeProvider())
            .CreateCheckoutAsync(Guid.NewGuid(), harness.OrderId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.PaymentOrderForbidden, result.ErrorCode);
    }

    [Fact, Trait("Category", "M4PaymentProcessingUnit")]
    public async Task CheckoutAlwaysUsesStoredAuthoritativeAmount()
    {
        await using var harness = await Harness.CreateAsync(amount: 777_000m);
        var provider = new FakeProvider();

        await harness.CheckoutService(provider).CreateCheckoutAsync(harness.UserId, harness.OrderId);

        Assert.Equal(777_000, provider.LastRequest!.Amount);
    }

    [Fact, Trait("Category", "M4PaymentProcessingUnit")]
    public void PayOSValidSignatureProducesNormalizedEvent()
    {
        var provider = CreatePayOSProvider(new StubHttpHandler(
            _ => Task.FromException<HttpResponseMessage>(new NotSupportedException())));
        using var payload = PayOSWebhook("TX-1", 125_000, "1");

        var result = provider.VerifyAndParseWebhook(payload.RootElement);

        Assert.True(result.IsValid);
        Assert.Equal("TX-1", result.Event!.ProviderEventId);
        Assert.Equal(PaymentProviderEventStatus.PAID, result.Event.Status);
    }

    [Fact, Trait("Category", "M4PaymentProcessingUnit")]
    public void PayOSInvalidSignatureIsRejected()
    {
        var provider = CreatePayOSProvider(new StubHttpHandler(
            _ => Task.FromException<HttpResponseMessage>(new NotSupportedException())));
        using var payload = PayOSWebhook("TX-1", 125_000, "1", validSignature: false);

        var result = provider.VerifyAndParseWebhook(payload.RootElement);

        Assert.False(result.IsValid);
        Assert.Equal(ErrorCodes.PaymentWebhookSignatureInvalid, result.ErrorCode);
    }

    [Fact, Trait("Category", "M4PaymentProcessingUnit")]
    public async Task PayOSHttpCheckoutUsesHeadersAndSignedAuthoritativeRequest()
    {
        string? capturedBody = null;
        var hadClientHeader = false;
        var hadApiKeyHeader = false;
        var handler = new StubHttpHandler(async request =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            hadClientHeader = request.Headers.Contains("x-client-id");
            hadApiKeyHeader = request.Headers.Contains("x-api-key");
            var responseData = new Dictionary<string, object>
            {
                ["amount"] = 125000, ["checkoutUrl"] = "https://pay.payos.vn/web/link-1",
                ["currency"] = "VND", ["orderCode"] = 12L,
                ["paymentLinkId"] = "link-1", ["status"] = "PENDING"
            };
            return JsonResponse(responseData);
        });
        var provider = CreatePayOSProvider(handler);

        var result = await provider.CreateCheckoutAsync(new PaymentProviderCheckoutRequest(
            12, 125_000, "EP0000012", Now.AddMinutes(30).UtcDateTime));

        Assert.Equal(125_000, result.Amount);
        Assert.True(hadClientHeader);
        Assert.True(hadApiKeyHeader);
        Assert.Contains("\"amount\":125000", capturedBody);
        Assert.Contains("\"signature\"", capturedBody);
    }

    [Fact, Trait("Category", "M4PaymentProcessingUnit")]
    public async Task UnknownWebhookOrderCodeIsAuditedWithoutMutation()
    {
        await using var harness = await Harness.CreateAsync();
        var result = await harness.WebhookService().ProcessVerifiedAsync(
            Event("unknown-event", "999999", 125_000));

        Assert.True(result.IsSuccess);
        Assert.Equal("UNKNOWN_ORDER", result.Data!.Outcome);
        Assert.Equal(PaymentOrderStatus.CREATED,
            (await harness.Db.PaymentOrders.SingleAsync()).Status);
        Assert.Equal(PaymentProviderEventOutcome.UNKNOWN_ORDER,
            (await harness.Db.PaymentProviderEvents.SingleAsync()).ProcessingOutcome);
    }

    [Fact, Trait("Category", "M4PaymentProcessingUnit")]
    public async Task AmountMismatchDoesNotMarkOrderPaid()
    {
        await using var harness = await Harness.CreateAsync(pending: true);
        var result = await harness.WebhookService().ProcessVerifiedAsync(
            Event("mismatch", harness.ProviderOrderId, 1));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.PaymentProviderEvidenceMismatch, result.ErrorCode);
        Assert.Equal(PaymentOrderStatus.PENDING_PAYMENT,
            (await harness.Db.PaymentOrders.SingleAsync()).Status);
    }

    [Fact, Trait("Category", "M4PaymentProcessingUnit")]
    public async Task DuplicateWebhookIsIdempotent()
    {
        await using var harness = await Harness.CreateAsync(pending: true);
        var paymentEvent = Event("duplicate-event", harness.ProviderOrderId, 125_000);
        await harness.WebhookService().ProcessVerifiedAsync(paymentEvent);

        var replay = await harness.WebhookService().ProcessVerifiedAsync(paymentEvent);

        Assert.True(replay.IsSuccess);
        Assert.True(replay.Data!.IsReplay);
        Assert.Single(await harness.Db.PaymentProviderEvents.ToListAsync());
    }

    [Fact, Trait("Category", "M4PaymentProcessingUnit")]
    public async Task ConflictingDuplicateWebhookIsRejected()
    {
        await using var harness = await Harness.CreateAsync(pending: true);
        await harness.WebhookService().ProcessVerifiedAsync(
            Event("conflict-event", harness.ProviderOrderId, 125_000, "A"));

        var conflict = await harness.WebhookService().ProcessVerifiedAsync(
            Event("conflict-event", harness.ProviderOrderId, 125_000, "B"));

        Assert.False(conflict.IsSuccess);
        Assert.Equal(ErrorCodes.PaymentWebhookConflict, conflict.ErrorCode);
    }

    [Fact, Trait("Category", "M4PaymentProcessingUnit")]
    public async Task ValidWebhookTransitionsPendingOrderToPaid()
    {
        await using var harness = await Harness.CreateAsync(pending: true);
        await harness.WebhookService().ProcessVerifiedAsync(
            Event("paid-event", harness.ProviderOrderId, 125_000));

        var order = await harness.Db.PaymentOrders.SingleAsync();
        Assert.Equal(PaymentOrderStatus.PAID, order.Status);
        Assert.Equal("paid-event", order.ProviderTransactionId);
    }

    [Fact, Trait("Category", "M4PaymentProcessingUnit")]
    public async Task LateWebhookDoesNotReverseTerminalState()
    {
        await using var harness = await Harness.CreateAsync(
            pending: true, initialStatus: PaymentOrderStatus.EXPIRED);
        var result = await harness.WebhookService().ProcessVerifiedAsync(
            Event("late-event", harness.ProviderOrderId, 125_000));

        Assert.True(result.IsSuccess);
        Assert.Equal(PaymentOrderStatus.EXPIRED,
            (await harness.Db.PaymentOrders.SingleAsync()).Status);
        Assert.Equal(PaymentProviderEventOutcome.LATE_TERMINAL_IGNORED,
            (await harness.Db.PaymentProviderEvents.SingleAsync()).ProcessingOutcome);
    }

    [Fact, Trait("Category", "M4PaymentProcessingUnit")]
    public async Task WebhookBeforeCheckoutFinalizationIsDeferredAndRetryable()
    {
        await using var harness = await Harness.CreateAsync();
        var order = await harness.Db.PaymentOrders.SingleAsync();
        var reservation = new PaymentCheckout
        {
            PaymentOrderId = order.PaymentOrderId, Provider = "PAYOS",
            ProviderOrderId = string.Empty, Status = PaymentCheckoutStatus.RESERVED,
            ReservedAtUtc = Now.UtcDateTime
        };
        harness.Db.PaymentCheckouts.Add(reservation);
        await harness.Db.SaveChangesAsync();
        reservation.ProviderOrderId = reservation.CheckoutSequenceId.ToString();
        order.ProviderOrderId = reservation.ProviderOrderId;
        await harness.Db.SaveChangesAsync();
        var paymentEvent = Event(
            "early-event", reservation.ProviderOrderId, 125_000);

        var early = await harness.WebhookService().ProcessVerifiedAsync(paymentEvent);
        Assert.False(early.IsSuccess);
        Assert.Equal(ErrorCodes.PaymentCheckoutRecoveryPending, early.ErrorCode);

        await harness.CheckoutService(new FakeProvider())
            .CreateCheckoutAsync(harness.UserId, harness.OrderId);
        var retry = await harness.WebhookService().ProcessVerifiedAsync(paymentEvent);

        Assert.True(retry.IsSuccess);
        Assert.True(retry.Data!.IsReplay);
        Assert.Equal(PaymentOrderStatus.PAID,
            (await harness.Db.PaymentOrders.SingleAsync()).Status);
        Assert.Single(await harness.Db.PaymentProviderEvents.ToListAsync());
    }

    [Fact, Trait("Category", "M4PaymentProcessingUnit")]
    public async Task PaidOrderIsFulfilledAtomicallyToWallet()
    {
        await using var harness = await Harness.CreateAsync(
            initialStatus: PaymentOrderStatus.PAID, fulfillmentConfigured: true);

        var result = await harness.FulfillmentService().FulfillAsync(harness.OrderId);

        Assert.True(result.IsSuccess);
        Assert.Equal(PaymentOrderStatus.FULFILLED, result.Data!.Status);
        Assert.Equal(250, (await harness.Db.Wallets.SingleAsync()).Balance);
        Assert.Single(await harness.Db.WalletTransactions.ToListAsync());
        Assert.Single(await harness.Db.PaymentFulfillments.ToListAsync());
    }

    [Fact, Trait("Category", "M4PaymentProcessingUnit")]
    public async Task DuplicateFulfillmentDoesNotGrantTwice()
    {
        await using var harness = await Harness.CreateAsync(
            initialStatus: PaymentOrderStatus.PAID, fulfillmentConfigured: true);
        var service = harness.FulfillmentService();
        await service.FulfillAsync(harness.OrderId);

        var retry = await service.FulfillAsync(harness.OrderId);

        Assert.True(retry.IsSuccess);
        Assert.True(retry.Data!.IsReplay);
        Assert.Equal(250, (await harness.Db.Wallets.SingleAsync()).Balance);
        Assert.Single(await harness.Db.WalletTransactions.ToListAsync());
    }

    [Fact, Trait("Category", "M4PaymentProcessingUnit")]
    public async Task GrantFailureRollsBackAndPaidOrderRemainsRetryable()
    {
        await using var harness = await Harness.CreateAsync(
            initialStatus: PaymentOrderStatus.PAID, fulfillmentConfigured: true);
        await harness.Db.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER reject_payment_wallet_grant
            BEFORE INSERT ON WalletTransactions
            BEGIN SELECT RAISE(ABORT, 'forced fulfillment failure'); END;
            """);

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            harness.FulfillmentService().FulfillAsync(harness.OrderId));

        harness.Db.ChangeTracker.Clear();
        Assert.Equal(PaymentOrderStatus.PAID,
            (await harness.Db.PaymentOrders.SingleAsync()).Status);
        Assert.Equal(100, (await harness.Db.Wallets.SingleAsync()).Balance);
        Assert.Empty(await harness.Db.PaymentFulfillments.ToListAsync());
    }

    private static NormalizedPaymentProviderEvent Event(
        string eventId, string orderId, decimal amount, string fingerprintSeed = "A") => new(
        "PAYOS", eventId, orderId, amount, "VND", PaymentProviderEventStatus.PAID,
        Now.UtcDateTime, new string(fingerprintSeed[0], 64));

    private static PayOSPaymentProvider CreatePayOSProvider(HttpMessageHandler handler) => new(
        new HttpClient(handler),
        Options.Create(new PayOSSettings
        {
            ClientId = "test-client", ApiKey = "test-api", ChecksumKey = "test-checksum",
            BaseUrl = "https://api-merchant.payos.test",
            ReturnUrl = "https://game.test/payment/success",
            CancelUrl = "https://game.test/payment/cancel"
        }));

    private static JsonDocument PayOSWebhook(
        string reference, long amount, string orderCode, bool validSignature = true)
    {
        var data = new Dictionary<string, object>
        {
            ["amount"] = amount, ["code"] = "00", ["currency"] = "VND",
            ["description"] = "EP0000001", ["orderCode"] = long.Parse(orderCode),
            ["paymentLinkId"] = "link-1", ["reference"] = reference,
            ["transactionDateTime"] = "2026-09-21T15:00:00Z"
        };
        var signature = Sign(Canonical(data));
        if (!validSignature) signature = new string('0', 64);
        return JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            code = "00", desc = "success", success = true, data, signature
        }));
    }

    private static HttpResponseMessage JsonResponse(Dictionary<string, object> data)
    {
        var signature = Sign(Canonical(data));
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(new
            {
                code = "00", desc = "success", data, signature
            }), Encoding.UTF8, "application/json")
        };
    }

    private static string Canonical(Dictionary<string, object> data) => string.Join("&",
        data.OrderBy(item => item.Key, StringComparer.Ordinal)
            .Select(item => $"{item.Key}={Convert.ToString(item.Value, System.Globalization.CultureInfo.InvariantCulture)}"));

    private static string Sign(string value)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes("test-checksum"));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }

    private sealed class StubHttpHandler(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        public StubHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
            : this(request => Task.FromResult(handler(request))) { }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) => handler(request);
    }

    private sealed class FakeProvider : IPaymentProvider
    {
        public string ProviderKey => "PAYOS";
        public bool Configured { get; init; } = true;
        public bool IsConfigured => Configured;
        public bool ThrowOnCreate { get; init; }
        public int CreateCalls { get; private set; }
        public PaymentProviderCheckoutRequest? LastRequest { get; private set; }
        public PaymentProviderCheckoutResult? Stored { get; private set; }

        public Task<PaymentProviderCheckoutResult> CreateCheckoutAsync(
            PaymentProviderCheckoutRequest request, CancellationToken cancellationToken = default)
        {
            CreateCalls++;
            LastRequest = request;
            if (ThrowOnCreate) throw new PaymentProviderException("simulated provider failure");
            Stored = new PaymentProviderCheckoutResult(
                request.OrderCode.ToString(), $"link-{request.OrderCode}",
                new Uri($"https://pay.test/{request.OrderCode}"), "PENDING",
                request.Amount, "VND");
            return Task.FromResult(Stored);
        }

        public Task<PaymentProviderCheckoutResult?> QueryPaymentAsync(
            string providerOrderId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Stored);

        public PaymentWebhookVerificationResult VerifyAndParseWebhook(JsonElement payload) =>
            throw new NotSupportedException();
    }

    private sealed class Harness : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly IOptions<PaymentCatalogSettings> _catalog;

        private Harness(
            SqliteConnection connection, AppDbContext db, Guid userId,
            Guid orderId, string providerOrderId, IOptions<PaymentCatalogSettings> catalog)
        {
            _connection = connection;
            Db = db;
            UserId = userId;
            OrderId = orderId;
            ProviderOrderId = providerOrderId;
            _catalog = catalog;
        }

        public AppDbContext Db { get; }
        public Guid UserId { get; }
        public Guid OrderId { get; }
        public string ProviderOrderId { get; }

        public static async Task<Harness> CreateAsync(
            decimal amount = 125_000m,
            bool pending = false,
            PaymentOrderStatus? initialStatus = null,
            bool fulfillmentConfigured = false)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync();
            var userId = Guid.NewGuid();
            var orderId = Guid.NewGuid();
            var providerOrderId = "101";
            db.Users.Add(new User
            {
                Id = userId, Email = $"pay-{userId:N}@test.local", Username = $"pay-{userId:N}",
                PasswordHash = "test", Role = UserRole.PLAYER, Status = UserStatus.ACTIVE,
                CreatedAt = Now.UtcDateTime, UpdatedAt = Now.UtcDateTime
            });
            db.Wallets.Add(new Wallet
            {
                Id = Guid.NewGuid(), UserId = userId, Balance = 100, UpdatedAt = Now.UtcDateTime
            });
            var status = initialStatus ?? (pending ? PaymentOrderStatus.PENDING_PAYMENT : PaymentOrderStatus.CREATED);
            db.PaymentOrders.Add(new PaymentOrder
            {
                PaymentOrderId = orderId, UserId = userId, Provider = "PAYOS",
                ProviderOrderId = pending || initialStatus is not null ? providerOrderId : null,
                Purpose = "AUTOMATED_TEST_ONLY", ProductReference = "TEST_PRODUCT",
                Amount = amount, Currency = "VND", Status = status,
                IdempotencyKey = $"key-{orderId:N}", RequestFingerprint = new string('A', 64),
                CreatedAtUtc = Now.AddMinutes(-1).UtcDateTime, UpdatedAtUtc = Now.UtcDateTime,
                ExpiresAtUtc = Now.AddMinutes(30).UtcDateTime,
                ProviderTransactionId = status == PaymentOrderStatus.PAID ? "seed-paid" : null,
                PaidAtUtc = status == PaymentOrderStatus.PAID ? Now.UtcDateTime : null
            });
            if (pending || initialStatus is not null)
            {
                db.PaymentCheckouts.Add(new PaymentCheckout
                {
                    PaymentOrderId = orderId, Provider = "PAYOS", ProviderOrderId = providerOrderId,
                    ProviderPaymentLinkId = "seed-link", CheckoutUrl = "https://pay.test/seed-link",
                    Status = PaymentCheckoutStatus.READY, ReservedAtUtc = Now.AddMinutes(-1).UtcDateTime,
                    ReadyAtUtc = Now.UtcDateTime
                });
            }
            await db.SaveChangesAsync();
            var product = new PaymentProductSettings
            {
                ProductReference = "TEST_PRODUCT", Purpose = "AUTOMATED_TEST_ONLY",
                Amount = amount, Currency = "VND", IsActive = true, ExpiresAfterMinutes = 30,
                FulfillmentKind = fulfillmentConfigured ? "WALLET_CREDIT" : null,
                WalletCreditAmount = fulfillmentConfigured ? 150 : null
            };
            return new Harness(connection, db, userId, orderId, providerOrderId,
                Options.Create(new PaymentCatalogSettings
                {
                    AllowedProviders = ["PAYOS"], Products = [product]
                }));
        }

        public PaymentCheckoutService CheckoutService(IPaymentProvider provider) => new(
            Db, new PaymentProviderRegistry([provider]), new FixedTimeProvider(Now));

        public PaymentFulfillmentService FulfillmentService() => new(
            Db, _catalog, new FixedTimeProvider(Now));

        public PaymentWebhookService WebhookService() => new(
            Db, FulfillmentService(), new FixedTimeProvider(Now));

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
