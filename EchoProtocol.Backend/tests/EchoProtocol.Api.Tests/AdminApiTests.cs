using System.Security.Claims;
using System.Text.Json;
using EchoProtocol.Api.Common;
using EchoProtocol.Api.Controllers;
using EchoProtocol.Api.Data;
using EchoProtocol.Api.DTOs.Admin;
using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EchoProtocol.Api.Tests;

public sealed class AdminApiTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 22, 12, 0, 0, TimeSpan.Zero);

    [Fact, Trait("Category", "AdminApiUnit")]
    public async Task UnauthenticatedPrincipalIsRejectedByAdminPolicy()
    {
        var result = await EvaluateAdminPolicy(new ClaimsPrincipal());
        Assert.True(result.Challenged); // Authorization middleware emits HTTP 401.
        AssertAdminControllerRequiresAdminRole();
    }

    [Fact, Trait("Category", "AdminApiUnit")]
    public async Task PlayerPrincipalIsForbiddenByAdminPolicy()
    {
        var result = await EvaluateAdminPolicy(Principal(UserRole.PLAYER));
        Assert.True(result.Forbidden); // Authorization middleware emits HTTP 403.
        AssertAdminControllerRequiresAdminRole();
    }

    [Fact, Trait("Category", "AdminApiUnit")]
    public async Task AdminPrincipalCanReadUsers()
    {
        Assert.True((await EvaluateAdminPolicy(Principal(UserRole.ADMIN))).Succeeded);
        await using var harness = await AdminHarness.CreateAsync();
        var controller = new AdminController(
            harness.Service, NullLogger<AdminController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
                {
                    User = Principal(UserRole.ADMIN)
                }
            }
        };
        Assert.IsType<OkObjectResult>(await controller.Users(
            new AdminUsersQuery(), CancellationToken.None));
    }

    [Fact, Trait("Category", "AdminApiUnit")]
    public async Task UsersPaginationIsStable()
    {
        await using var harness = await AdminHarness.CreateAsync();
        var result = await harness.Service.GetUsersAsync(new AdminUsersQuery
        {
            Page = 1,
            PageSize = 1
        });
        Assert.True(result.IsSuccess);
        Assert.Single(result.Data!.Items);
        Assert.Equal(2, result.Data.TotalItems);
        Assert.Equal(2, result.Data.TotalPages);
    }

    [Theory, Trait("Category", "AdminApiUnit")]
    [InlineData("Admin Display")]
    [InlineData("admin@echo.invalid")]
    public async Task UsersSearchesDisplayNameAndEmail(string search)
    {
        await using var harness = await AdminHarness.CreateAsync();
        var result = await harness.Service.GetUsersAsync(new AdminUsersQuery { Search = search });
        Assert.Equal(harness.AdminUserId, Assert.Single(result.Data!.Items).UserId);
    }

    [Fact, Trait("Category", "AdminApiUnit")]
    public async Task PaymentsSupportCombinedFilters()
    {
        await using var harness = await AdminHarness.CreateAsync();
        var result = await harness.Service.GetPaymentsAsync(new AdminPaymentsQuery
        {
            Status = PaymentOrderStatus.FULFILLED,
            Provider = " payos ",
            UserId = harness.AdminUserId,
            ProductReference = "TEST_WALLET_PACK",
            FromUtc = Now.AddMinutes(-2).UtcDateTime,
            ToUtc = Now.AddMinutes(2).UtcDateTime
        });
        Assert.Equal(harness.PaymentOrderId, Assert.Single(result.Data!.Items).PaymentOrderId);
    }

    [Fact, Trait("Category", "AdminApiUnit")]
    public async Task PaymentDetailReturnsAggregateRelationships()
    {
        await using var harness = await AdminHarness.CreateAsync();
        var result = await harness.Service.GetPaymentAsync(harness.PaymentOrderId);
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data!.Checkout);
        Assert.Single(result.Data.ProviderEvents);
        Assert.NotNull(result.Data.Fulfillment);
        Assert.NotNull(result.Data.WalletTransaction);
    }

    [Fact, Trait("Category", "AdminApiUnit")]
    public async Task UnknownPaymentReturnsNotFound()
    {
        await using var harness = await AdminHarness.CreateAsync();
        var result = await harness.Service.GetPaymentAsync(Guid.NewGuid());
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.PaymentOrderNotFound, result.ErrorCode);
    }

    [Fact, Trait("Category", "AdminApiUnit")]
    public async Task WalletTransactionsSupportUserTypeAndReferenceFilters()
    {
        await using var harness = await AdminHarness.CreateAsync();
        var result = await harness.Service.GetWalletTransactionsAsync(
            new AdminWalletTransactionsQuery
            {
                UserId = harness.AdminUserId,
                Type = WalletTransactionType.PAYMENT_FULFILLMENT,
                Reference = harness.PaymentOrderId
            });
        var transaction = Assert.Single(result.Data!.Items);
        Assert.Equal(100, transaction.Amount);
        Assert.Equal(550, transaction.BalanceAfter);
    }

    [Fact, Trait("Category", "AdminApiUnit")]
    public async Task PurchasesSupportUserShopItemAndDateFilters()
    {
        await using var harness = await AdminHarness.CreateAsync();
        var result = await harness.Service.GetPurchasesAsync(new AdminPurchasesQuery
        {
            UserId = harness.AdminUserId,
            ShopItemId = harness.ShopItemId,
            FromUtc = Now.AddMinutes(-2).UtcDateTime,
            ToUtc = Now.AddMinutes(2).UtcDateTime
        });
        var purchase = Assert.Single(result.Data!.Items);
        Assert.Equal(50, purchase.PriceAtPurchase);
        Assert.Equal("Test Character", purchase.ShopItemName);
    }

    [Fact, Trait("Category", "AdminApiUnit")]
    public async Task AdminContractsDoNotExposeSensitiveFields()
    {
        await using var harness = await AdminHarness.CreateAsync();
        var detail = await harness.Service.GetPaymentAsync(harness.PaymentOrderId);
        var users = await harness.Service.GetUsersAsync(new AdminUsersQuery());
        var json = JsonSerializer.Serialize(new
        {
            Payment = detail.Data,
            Users = users.Data
        }).ToLowerInvariant();
        Assert.DoesNotContain("passwordhash", json);
        Assert.DoesNotContain("requestfingerprint", json);
        Assert.DoesNotContain("semanticfingerprint", json);
        Assert.DoesNotContain("idempotencykey", json);
        Assert.DoesNotContain("checksumkey", json);
        Assert.DoesNotContain("apikey", json);
    }

    private static void AssertAdminControllerRequiresAdminRole()
    {
        var attribute = Assert.Single(typeof(AdminController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>());
        Assert.Equal(nameof(UserRole.ADMIN), attribute.Roles);
    }

    private static async Task<PolicyAuthorizationResult> EvaluateAdminPolicy(
        ClaimsPrincipal principal)
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddAuthorizationCore()
            .BuildServiceProvider();
        var authorization = services.GetRequiredService<IAuthorizationService>();
        var policy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .RequireRole(nameof(UserRole.ADMIN))
            .Build();
        var evaluator = new PolicyEvaluator(authorization);
        var context = new DefaultHttpContext { User = principal };
        var authentication = principal.Identity?.IsAuthenticated == true
            ? AuthenticateResult.Success(new AuthenticationTicket(principal, "Test"))
            : AuthenticateResult.NoResult();
        return await evaluator.AuthorizeAsync(policy, authentication, context, null);
    }

    private static ClaimsPrincipal Principal(UserRole role) => new(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, role.ToString())
        ], "Test"));

    private sealed class AdminHarness : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private AdminHarness(
            SqliteConnection connection,
            AppDbContext db,
            Guid adminUserId,
            Guid paymentOrderId,
            Guid shopItemId)
        {
            _connection = connection;
            Db = db;
            Service = new AdminQueryService(db);
            AdminUserId = adminUserId;
            PaymentOrderId = paymentOrderId;
            ShopItemId = shopItemId;
        }

        public AppDbContext Db { get; }
        public AdminQueryService Service { get; }
        public Guid AdminUserId { get; }
        public Guid PaymentOrderId { get; }
        public Guid ShopItemId { get; }

        public static async Task<AdminHarness> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync();

            var admin = CreateUser("admin", UserRole.ADMIN, "Admin Display", Now.AddMinutes(-2));
            admin.Email = "admin@echo.invalid";
            var player = CreateUser("player", UserRole.PLAYER, "Player Display", Now.AddMinutes(-1));
            var shopItem = new ShopItem
            {
                ItemId = Guid.NewGuid(), ItemName = "Test Character",
                Description = "Admin API test fixture", Category = "CHARACTER", Price = 50,
                AssetReference = "test://admin/character", IsActive = true,
                CreatedAtUtc = Now.UtcDateTime, UpdatedAtUtc = Now.UtcDateTime
            };
            var purchaseId = Guid.NewGuid();
            var purchaseWalletTransactionId = Guid.NewGuid();
            var paymentOrderId = Guid.NewGuid();
            var fulfillmentWalletTransactionId = Guid.NewGuid();
            admin.Wallet!.Transactions.Add(new WalletTransaction
            {
                Id = purchaseWalletTransactionId,
                Type = WalletTransactionType.PURCHASE,
                Amount = -50, BalanceBefore = 500, BalanceAfter = 450,
                ReferenceId = purchaseId, Description = "Test purchase",
                CreatedAtUtc = Now.UtcDateTime
            });
            admin.Wallet.Transactions.Add(new WalletTransaction
            {
                Id = fulfillmentWalletTransactionId,
                Type = WalletTransactionType.PAYMENT_FULFILLMENT,
                Amount = 100, BalanceBefore = 450, BalanceAfter = 550,
                ReferenceId = paymentOrderId, Description = "Test payment fulfillment",
                CreatedAtUtc = Now.AddSeconds(1).UtcDateTime
            });
            admin.PurchaseTransactions.Add(new PurchaseTransaction
            {
                PurchaseId = purchaseId,
                ShopItem = shopItem,
                IdempotencyKey = "sensitive-purchase-key",
                PriceAtPurchase = 50,
                WalletTransactionId = purchaseWalletTransactionId,
                Status = PurchaseTransactionStatus.COMPLETED,
                CreatedAtUtc = Now.UtcDateTime,
                InventoryItem = new InventoryItem
                {
                    InventoryItemId = Guid.NewGuid(),
                    UserId = admin.Id,
                    ShopItem = shopItem,
                    Source = InventoryAcquisitionSource.PURCHASE,
                    AcquiredAtUtc = Now.UtcDateTime
                }
            });
            admin.PaymentOrders.Add(new PaymentOrder
            {
                PaymentOrderId = paymentOrderId,
                Provider = "PAYOS", ProviderOrderId = "provider-order",
                ProviderTransactionId = "provider-transaction",
                Purpose = "WALLET_CREDIT", ProductReference = "TEST_WALLET_PACK",
                Amount = 100, Currency = "VND", Status = PaymentOrderStatus.FULFILLED,
                IdempotencyKey = "sensitive-payment-key",
                RequestFingerprint = new string('A', 64),
                CreatedAtUtc = Now.UtcDateTime, UpdatedAtUtc = Now.AddSeconds(2).UtcDateTime,
                PaidAtUtc = Now.AddSeconds(1).UtcDateTime,
                FulfilledAtUtc = Now.AddSeconds(2).UtcDateTime,
                FulfillmentReference = "fulfillment-reference",
                Checkout = new PaymentCheckout
                {
                    Provider = "PAYOS", ProviderOrderId = "provider-order",
                    ProviderPaymentLinkId = "payment-link",
                    CheckoutUrl = "https://pay.test/order",
                    Status = PaymentCheckoutStatus.READY,
                    ReservedAtUtc = Now.UtcDateTime, ReadyAtUtc = Now.UtcDateTime
                },
                ProviderEvents =
                [
                    new PaymentProviderEvent
                    {
                        PaymentProviderEventId = Guid.NewGuid(), Provider = "PAYOS",
                        ProviderEventId = "event-1", ProviderOrderId = "provider-order",
                        Amount = 100, Currency = "VND",
                        NormalizedStatus = PaymentProviderEventStatus.PAID,
                        SemanticFingerprint = new string('B', 64),
                        VerificationStatus = "VERIFIED",
                        ReceivedAtUtc = Now.AddSeconds(1).UtcDateTime,
                        ProcessedAtUtc = Now.AddSeconds(2).UtcDateTime,
                        ProcessingOutcome = PaymentProviderEventOutcome.FULFILLED
                    }
                ],
                Fulfillment = new PaymentFulfillment
                {
                    FulfillmentReference = "fulfillment-reference",
                    Kind = PaymentFulfillmentKind.WALLET_CREDIT,
                    WalletTransactionId = fulfillmentWalletTransactionId,
                    CompletedAtUtc = Now.AddSeconds(2).UtcDateTime
                }
            });
            player.PaymentOrders.Add(new PaymentOrder
            {
                PaymentOrderId = Guid.NewGuid(), Provider = "PAYOS",
                Purpose = "WALLET_CREDIT", ProductReference = "OTHER_PRODUCT",
                Amount = 200, Currency = "VND", Status = PaymentOrderStatus.CREATED,
                IdempotencyKey = "other-key", RequestFingerprint = new string('C', 64),
                CreatedAtUtc = Now.AddMinutes(-1).UtcDateTime,
                UpdatedAtUtc = Now.AddMinutes(-1).UtcDateTime
            });

            db.Users.AddRange(admin, player);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            return new AdminHarness(connection, db, admin.Id, paymentOrderId, shopItem.ItemId);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }

        private static User CreateUser(
            string username,
            UserRole role,
            string displayName,
            DateTimeOffset createdAt)
        {
            var userId = Guid.NewGuid();
            return new User
            {
                Id = userId,
                Email = $"{username}@echo.invalid",
                Username = username,
                PasswordHash = "sensitive-password-hash",
                Role = role,
                Status = UserStatus.ACTIVE,
                CreatedAt = createdAt.UtcDateTime,
                UpdatedAt = createdAt.UtcDateTime,
                PlayerProfile = new PlayerProfile
                {
                    Id = Guid.NewGuid(), DisplayName = displayName,
                    Level = 1, CreatedAt = createdAt.UtcDateTime,
                    UpdatedAt = createdAt.UtcDateTime
                },
                Wallet = new Wallet
                {
                    Id = Guid.NewGuid(), Balance = username == "admin" ? 550 : 100,
                    UpdatedAt = createdAt.UtcDateTime
                }
            };
        }
    }
}
