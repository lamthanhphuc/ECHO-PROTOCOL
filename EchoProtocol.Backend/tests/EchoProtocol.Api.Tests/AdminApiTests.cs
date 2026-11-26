using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using EchoProtocol.Api.Configurations;
using EchoProtocol.Api.Common;
using EchoProtocol.Api.Controllers;
using EchoProtocol.Api.Data;
using EchoProtocol.Api.DTOs.Admin;
using EchoProtocol.Api.DTOs.Payments;
using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services;
using EchoProtocol.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace EchoProtocol.Api.Tests;

public sealed class AdminApiTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 22, 12, 0, 0, TimeSpan.Zero);

    [Fact] public async Task AdminCanAccessRemoteFixedEndpoints() =>
        Assert.True((await EvaluateAdminPolicy(Principal(UserRole.ADMIN))).Succeeded);

    [Fact] public async Task PlayerCannotAccessRemoteFixedEndpoints() =>
        Assert.True((await EvaluateAdminPolicy(Principal(UserRole.PLAYER))).Forbidden);

    [Fact] public async Task UnauthenticatedCannotAccessRemoteFixedEndpoints() =>
        Assert.True((await EvaluateAdminPolicy(new ClaimsPrincipal())).Challenged);

    [Fact]
    public async Task ConflictingActivationReturns409()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var remote = new RemoteFixedBalanceAdminService(db, TimeProvider.System, Options.Create(new MatchAuthoritySettings()));
        var adminId = Guid.NewGuid();
        await remote.CreateVersionAsync(adminId, new CreateRemoteFixedVersionRequest
        { Version = "RFB-001", MapId = "SciFi", Difficulty = "NORMAL", SnapshotJson = RemoteFixedBalanceTests.ValidSnapshot }, default);
        var controller = new AdminController(new AdminQueryService(db), NullLogger<AdminController>.Instance)
        { ControllerContext = new() { HttpContext = new DefaultHttpContext { User = Principal(UserRole.ADMIN) } } };
        var result = await controller.ActivateRemoteFixedVersion("RFB-001", new()
        { ExpectedActiveVersion = "stale", Reason = "test conflict" }, remote, default);
        Assert.Equal(StatusCodes.Status409Conflict, Assert.IsType<ObjectResult>(result).StatusCode);
    }

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

    [Fact, Trait("Category", "AdminApiUnit")]
    public async Task MatchLogsOnlyIncludeStartedMatchesAndFilterParticipant()
    {
        await using var harness = await AdminHarness.CreateAsync();
        var startedId = Guid.NewGuid();
        var lobbyId = Guid.NewGuid();
        harness.Db.MatchAuthorityBindings.AddRange(
            new MatchAuthorityBinding
            {
                MatchId = startedId, HostUserId = harness.AdminUserId,
                FusionSessionName = "started-match", MaxPlayers = 4,
                Status = MatchAuthorityStatus.Ended,
                CreatedAtUtc = Now.UtcDateTime, UpdatedAtUtc = Now.UtcDateTime,
                StartedAtUtc = Now.UtcDateTime, EndedAtUtc = Now.AddMinutes(5).UtcDateTime,
                Players = [new MatchPlayerBinding
                {
                    Id = Guid.NewGuid(), UserId = harness.AdminUserId,
                    Participated = true, BoundAtUtc = Now.UtcDateTime, LastSeenAtUtc = Now.UtcDateTime
                }]
            },
            new MatchAuthorityBinding
            {
                MatchId = lobbyId, HostUserId = harness.AdminUserId,
                FusionSessionName = "lobby-match", MaxPlayers = 4,
                Status = MatchAuthorityStatus.Lobby,
                CreatedAtUtc = Now.UtcDateTime, UpdatedAtUtc = Now.UtcDateTime
            });
        await harness.Db.SaveChangesAsync();
        var controller = new AdminController(harness.Service, NullLogger<AdminController>.Instance);
        var response = Assert.IsType<OkObjectResult>(await controller.MatchLogs(
            new AdminMatchesQuery { UserId = harness.AdminUserId }, harness.Db, default));
        var page = Assert.IsType<ApiResponse<AdminPagedResponse<AdminMatchResponse>>>(response.Value);
        Assert.Equal(startedId, Assert.Single(page.Data!.Items).MatchId);
        Assert.Equal(1, page.Data.TotalItems);

        var lobby = await harness.Db.MatchAuthorityBindings.SingleAsync(item => item.MatchId == lobbyId);
        lobby.Status = MatchAuthorityStatus.Ended;
        lobby.EndedAtUtc = Now.UtcDateTime;
        await harness.Db.SaveChangesAsync();
        var all = Assert.IsType<OkObjectResult>(await controller.MatchLogs(new AdminMatchesQuery(), harness.Db, default));
        var allPage = Assert.IsType<ApiResponse<AdminPagedResponse<AdminMatchResponse>>>(all.Value);
        Assert.Equal(2, allPage.Data!.TotalItems);
        Assert.Null(allPage.Data.Items.Single(item => item.MatchId == lobbyId).StartedAtUtc);
        var active = Assert.IsType<OkObjectResult>(await controller.MatchLogs(
            new AdminMatchesQuery { Status = MatchAuthorityStatus.InMatch }, harness.Db, default));
        Assert.Empty(Assert.IsType<ApiResponse<AdminPagedResponse<AdminMatchResponse>>>(active.Value).Data!.Items);
        var detail = Assert.IsType<OkObjectResult>(await controller.MatchDetail(startedId, harness.Db, default));
        var roster = Assert.IsType<ApiResponse<AdminMatchDetailResponse>>(detail.Value).Data!;
        Assert.Equal(1, roster.Match.ConnectedCount);
        Assert.Equal(harness.AdminUserId, Assert.Single(roster.Players).UserId);
        harness.Db.AdminModerationActions.Add(new AdminModerationAction
        {
            Id = Guid.NewGuid(), TargetId = startedId, Action = "FLAG_MATCH",
            AdminUserId = harness.AdminUserId, Reason = "Review this match", CreatedAtUtc = Now.UtcDateTime
        });
        await harness.Db.SaveChangesAsync();
        var flagged = Assert.IsType<OkObjectResult>(await controller.MatchLogs(
            new AdminMatchesQuery { FlaggedOnly = true }, harness.Db, default));
        var flaggedPage = Assert.IsType<ApiResponse<AdminPagedResponse<AdminMatchResponse>>>(flagged.Value);
        Assert.Equal(startedId, Assert.Single(flaggedPage.Data!.Items).MatchId);
        Assert.True(flaggedPage.Data.Items[0].IsFlagged);
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
    }    [Fact, Trait("Category", "AdminApiUnit")]
    public async Task AdminCanGrantCreditsAndCreateLedgerEntry()
    {
        await using var harness =
            await AdminHarness.CreateAsync();

        var result =
            await harness.Service.GrantCreditsAsync(
                harness.AdminUserId,
                harness.AdminUserId,
                new AdminGrantCreditsRequest
                {
                    Amount = 1_000,
                    Reason = "Shop testing"
                });

        Assert.True(result.IsSuccess);

        Assert.Equal(
            550,
            result.Data!.BalanceBefore);

        Assert.Equal(
            1_550,
            result.Data.BalanceAfter);

        Assert.Equal(
            1_000,
            result.Data.Amount);
        Assert.Equal(harness.AdminUserId, result.Data.ActorAdminUserId);
        Assert.Equal(harness.AdminUserId, result.Data.UserId);

        var wallet =
            await harness.Db.Wallets
                .SingleAsync(
                    item =>
                        item.UserId ==
                        harness.AdminUserId);

        Assert.Equal(
            1_550,
            wallet.Balance);

        harness.Db.ChangeTracker.Clear();

        var ledger =
            await harness.Service
                .GetWalletTransactionsAsync(
                    new AdminWalletTransactionsQuery
                    {
                        UserId =
                            harness.AdminUserId,

                        Type =
                            WalletTransactionType.ADMIN_GRANT,

                        Reference =
                            result.Data.Reference
                    });

        var transaction =
            Assert.Single(
                ledger.Data!.Items);

        Assert.Equal(
            1_000,
            transaction.Amount);

        Assert.Equal(
            550,
            transaction.BalanceBefore);

        Assert.Equal(
            1_550,
            transaction.BalanceAfter);

        Assert.Contains(
            "Shop testing",
            transaction.Description);
        var storedTransaction = await harness.Db.WalletTransactions
            .SingleAsync(item => item.Id == result.Data.TransactionId);
        Assert.Equal(WalletTransactionType.ADMIN_GRANT, storedTransaction.Type);
        Assert.Equal(result.Data.Amount, storedTransaction.Amount);
        Assert.Equal(result.Data.CreatedAtUtc, storedTransaction.CreatedAtUtc);
        Assert.Equal(harness.AdminUserId, storedTransaction.ActorAdminUserId);
        Assert.Equal(harness.AdminUserId, transaction.ActorAdminUserId);
    }

    [Fact, Trait("Category", "AdminApiUnit")]
    public async Task AdminGrantUsesFreshBalanceWhenContextTrackedAnOlderWallet()
    {
        await using var harness = await AdminHarness.CreateAsync();
        var tracked = await harness.Db.Wallets.SingleAsync(item => item.UserId == harness.AdminUserId);
        Assert.Equal(550, tracked.Balance);

        await using (var concurrentWriter = harness.CreateSiblingDb())
        {
            var wallet = await concurrentWriter.Wallets.SingleAsync(item => item.UserId == harness.AdminUserId);
            wallet.Balance += 75;
            await concurrentWriter.SaveChangesAsync();
        }

        var result = await harness.Service.GrantCreditsAsync(harness.AdminUserId, harness.AdminUserId,
            new AdminGrantCreditsRequest { Amount = 25, Reason = "concurrent balance update" });

        Assert.True(result.IsSuccess);
        Assert.Equal(625, result.Data!.BalanceBefore);
        Assert.Equal(650, result.Data.BalanceAfter);
        harness.Db.ChangeTracker.Clear();
        Assert.Equal(650, (await harness.Db.Wallets.SingleAsync(item => item.UserId == harness.AdminUserId)).Balance);
        var grant = await harness.Db.WalletTransactions.SingleAsync(item => item.Id == result.Data.TransactionId);
        Assert.Equal(625, grant.BalanceBefore);
        Assert.Equal(650, grant.BalanceAfter);
    }

    [Theory, Trait("Category", "AdminApiUnit")]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(100001)]
    public async Task AdminGrantCreditsRejectsInvalidAmount(
        int amount)
    {
        await using var harness =
            await AdminHarness.CreateAsync();

        var result =
            await harness.Service.GrantCreditsAsync(
                harness.AdminUserId,
                harness.AdminUserId,
                new AdminGrantCreditsRequest
                {
                    Amount = amount
                });

        Assert.False(result.IsSuccess);

        Assert.Equal(
            ErrorCodes.ValidationError,
            result.ErrorCode);
    }

    [Fact, Trait("Category", "AdminApiUnit")]
    public async Task AdminGrantToUnknownTargetDoesNotChangeWalletsOrLedger()
    {
        await using var harness = await AdminHarness.CreateAsync();
        var balancesBefore = await harness.Db.Wallets.AsNoTracking()
            .ToDictionaryAsync(item => item.UserId, item => item.Balance);
        var ledgerCountBefore = await harness.Db.WalletTransactions.CountAsync();

        var result = await harness.Service.GrantCreditsAsync(
            harness.AdminUserId,
            Guid.NewGuid(),
            new AdminGrantCreditsRequest { Amount = 25, Reason = "unknown target" });

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.NotFound, result.ErrorCode);
        harness.Db.ChangeTracker.Clear();
        var balancesAfter = await harness.Db.Wallets.AsNoTracking()
            .ToDictionaryAsync(item => item.UserId, item => item.Balance);
        Assert.Equal(
            balancesBefore.OrderBy(item => item.Key),
            balancesAfter.OrderBy(item => item.Key));
        Assert.Equal(ledgerCountBefore, await harness.Db.WalletTransactions.CountAsync());
    }

    [Fact, Trait("Category", "AdminApiHttp")]
    public async Task AdminCreditAndRemoteFixedMutationsRequireHttpAuthorizationAndRecordClaimActor()
    {
        await using var harness = await AdminHarness.CreateAsync();
        var playerId = await harness.Db.Users
            .Where(item => item.Role == UserRole.PLAYER)
            .Select(item => item.Id)
            .SingleAsync();
        var player = await harness.Db.Users.SingleAsync(item => item.Id == playerId);
        var playerBalanceBefore = await harness.Db.Wallets.AsNoTracking()
            .Where(item => item.UserId == playerId)
            .Select(item => item.Balance)
            .SingleAsync();
        var ledgerCountBefore = await harness.Db.WalletTransactions.CountAsync();

        var remote = new RemoteFixedBalanceAdminService(
            harness.Db, TimeProvider.System, Options.Create(new MatchAuthoritySettings()));
        var oldVersion = await remote.CreateVersionAsync(harness.AdminUserId,
            new CreateRemoteFixedVersionRequest
            {
                Version = "RFB-HTTP-OLD",
                MapId = "SciFi",
                Difficulty = "NORMAL",
                SnapshotJson = RemoteFixedBalanceTests.ValidSnapshot
            }, default);
        Assert.True(oldVersion.IsSuccess, oldVersion.Message);
        Assert.True((await remote.ActivateVersionAsync(harness.AdminUserId, "RFB-HTTP-OLD",
            new ChangeRemoteFixedActivationRequest
            {
                ExpectedActiveVersion = string.Empty,
                Reason = "HTTP authorization fixture"
            }, default)).IsSuccess);
        var nextVersion = await remote.CreateVersionAsync(harness.AdminUserId,
            new CreateRemoteFixedVersionRequest
            {
                Version = "RFB-HTTP-NEXT",
                MapId = "SciFi",
                Difficulty = "NORMAL",
                SnapshotJson = RemoteFixedBalanceTests.ValidSnapshot
            }, default);
        Assert.True(nextVersion.IsSuccess, nextVersion.Message);
        var auditCountBefore = await harness.Db.RemoteFixedBalanceAuditEntries.CountAsync();

        using var server = CreateHttpServer(harness.Db);
        using var client = server.CreateClient();
        var creditPath = $"/api/admin/users/{playerId}/wallet/credit";
        var activatePath = "/api/admin/remote-fixed/versions/RFB-HTTP-NEXT/activate";
        var grantBody = new
        {
            amount = 25,
            reason = "support credit",
            actorAdminUserId = playerId
        };

        var anonymousGrant = await client.PostAsJsonAsync(creditPath, grantBody);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousGrant.StatusCode);

        client.DefaultRequestHeaders.Authorization = Bearer(CreateAccessToken(player));
        var playerGrant = await client.PostAsJsonAsync(creditPath, grantBody);
        Assert.Equal(HttpStatusCode.Forbidden, playerGrant.StatusCode);
        var playerAnalytics = await client.GetAsync("/api/admin/balance-analytics");
        Assert.Equal(HttpStatusCode.Forbidden, playerAnalytics.StatusCode);
        var playerActivation = await client.PostAsJsonAsync(activatePath,
            new ChangeRemoteFixedActivationRequest
            {
                ExpectedActiveVersion = "RFB-HTTP-OLD",
                Reason = "unauthorized activation"
            });
        Assert.Equal(HttpStatusCode.Forbidden, playerActivation.StatusCode);

        harness.Db.ChangeTracker.Clear();
        Assert.Equal(playerBalanceBefore, await harness.Db.Wallets.AsNoTracking()
            .Where(item => item.UserId == playerId)
            .Select(item => item.Balance)
            .SingleAsync());
        Assert.Equal(ledgerCountBefore, await harness.Db.WalletTransactions.CountAsync());
        Assert.Equal(auditCountBefore, await harness.Db.RemoteFixedBalanceAuditEntries.CountAsync());
        Assert.Equal("RFB-HTTP-OLD", await harness.Db.RemoteFixedBalanceVersions
            .Where(item => item.IsActive)
            .Select(item => item.Version)
            .SingleAsync());

        var admin = await harness.Db.Users.SingleAsync(item => item.Id == harness.AdminUserId);
        client.DefaultRequestHeaders.Authorization = Bearer(CreateAccessToken(admin));
        var adminGrant = await client.PostAsJsonAsync(creditPath, grantBody);
        Assert.Equal(HttpStatusCode.OK, adminGrant.StatusCode);
        using var grantJson = JsonDocument.Parse(await adminGrant.Content.ReadAsStringAsync());
        var grantData = grantJson.RootElement.GetProperty("data");
        Assert.Equal(harness.AdminUserId, grantData.GetProperty("actorAdminUserId").GetGuid());
        Assert.Equal(playerId, grantData.GetProperty("userId").GetGuid());
        Assert.Equal(25, grantData.GetProperty("amount").GetInt32());
        Assert.Equal("support credit", grantData.GetProperty("reason").GetString());

        var grantId = grantData.GetProperty("transactionId").GetGuid();
        var grantTime = grantData.GetProperty("createdAtUtc").GetDateTime();
        harness.Db.ChangeTracker.Clear();
        var walletTransaction = await harness.Db.WalletTransactions
            .SingleAsync(item => item.Id == grantId);
        Assert.Equal(harness.AdminUserId, walletTransaction.ActorAdminUserId);
        Assert.Equal(25, walletTransaction.Amount);
        Assert.Contains("support credit", walletTransaction.Description);
        Assert.Equal(grantTime, walletTransaction.CreatedAtUtc);

        var history = await client.GetAsync(
            $"/api/admin/wallet-transactions?userId={playerId}&type=ADMIN_GRANT");
        Assert.Equal(HttpStatusCode.OK, history.StatusCode);
        using var historyJson = JsonDocument.Parse(await history.Content.ReadAsStringAsync());
        var audit = Assert.Single(historyJson.RootElement.GetProperty("data")
            .GetProperty("items").EnumerateArray().ToArray());
        Assert.Equal(harness.AdminUserId, audit.GetProperty("actorAdminUserId").GetGuid());
        Assert.Equal("admin", audit.GetProperty("actorAdminUsername").GetString());
        Assert.Equal(playerId, audit.GetProperty("userId").GetGuid());
        Assert.Equal(25, audit.GetProperty("amount").GetInt32());
        Assert.Equal(grantId, audit.GetProperty("transactionId").GetGuid());
        Assert.Equal(grantTime, audit.GetProperty("createdAtUtc").GetDateTime());
    }

    [Fact, Trait("Category", "AdminApiHttp")]
    public async Task PaymentHistoryHttpEndpointUsesOnlyAuthenticatedOwner()
    {
        await using var harness = await AdminHarness.CreateAsync();
        var player = await harness.Db.Users
            .SingleAsync(item => item.Role == UserRole.PLAYER);
        var playerOrder = await harness.Db.PaymentOrders.AsNoTracking()
            .SingleAsync(item => item.UserId == player.Id);
        var adminOrder = await harness.Db.PaymentOrders.AsNoTracking()
            .SingleAsync(item => item.UserId == harness.AdminUserId);

        using var server = CreateHttpServer(harness.Db);
        using var client = server.CreateClient();
        client.DefaultRequestHeaders.Authorization = Bearer(CreateAccessToken(player));
        var playerResponse = await client.GetAsync("/api/payments/orders?page=1&pageSize=20");
        Assert.Equal(HttpStatusCode.OK, playerResponse.StatusCode);
        using var playerJson = JsonDocument.Parse(await playerResponse.Content.ReadAsStringAsync());
        var playerResult = playerJson.RootElement.GetProperty("data");
        var playerHistory = playerResult.GetProperty("items").EnumerateArray().ToArray();
        var playerItem = Assert.Single(playerHistory);
        Assert.Equal(playerOrder.PaymentOrderId,
            playerItem.GetProperty("paymentOrderId").GetGuid());
        Assert.Equal(playerOrder.Status.ToString(),
            playerItem.GetProperty("status").GetString());
        Assert.Equal(1, playerResult.GetProperty("totalItems").GetInt32());

        var admin = await harness.Db.Users.SingleAsync(item => item.Id == harness.AdminUserId);
        client.DefaultRequestHeaders.Authorization = Bearer(CreateAccessToken(admin));
        var adminResponse = await client.GetAsync("/api/payments/orders?page=1&pageSize=20");
        Assert.Equal(HttpStatusCode.OK, adminResponse.StatusCode);
        using var adminJson = JsonDocument.Parse(await adminResponse.Content.ReadAsStringAsync());
        var adminItem = Assert.Single(adminJson.RootElement.GetProperty("data")
            .GetProperty("items").EnumerateArray().ToArray());
        Assert.Equal(adminOrder.PaymentOrderId,
            adminItem.GetProperty("paymentOrderId").GetGuid());
        Assert.NotEqual(playerOrder.PaymentOrderId,
            adminItem.GetProperty("paymentOrderId").GetGuid());
    }

    [Fact, Trait("Category", "AdminApiHttp")]
    public async Task ExpiredAccessTokenIsRejectedByProtectedHttpEndpoint()
    {
        await using var harness = await AdminHarness.CreateAsync();
        var admin = await harness.Db.Users.SingleAsync(item => item.Id == harness.AdminUserId);
        using var server = CreateHttpServer(harness.Db);
        using var client = server.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            Bearer(CreateAccessToken(admin, expiryMinutes: -5));

        var response = await client.GetAsync("/api/admin/users");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact, Trait("Category", "AdminApiUnit")]
    public async Task AdminGrantRequiresReasonWithoutChangingWalletOrLedger()
    {
        await using var harness = await AdminHarness.CreateAsync();
        var balanceBefore = await harness.Db.Wallets.AsNoTracking()
            .Where(item => item.UserId == harness.AdminUserId)
            .Select(item => item.Balance)
            .SingleAsync();
        var ledgerCountBefore = await harness.Db.WalletTransactions.CountAsync();

        var result = await harness.Service.GrantCreditsAsync(
            harness.AdminUserId,
            harness.AdminUserId,
            new AdminGrantCreditsRequest { Amount = 25, Reason = "  " });

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationError, result.ErrorCode);
        harness.Db.ChangeTracker.Clear();
        Assert.Equal(balanceBefore, await harness.Db.Wallets.AsNoTracking()
            .Where(item => item.UserId == harness.AdminUserId)
            .Select(item => item.Balance)
            .SingleAsync());
        Assert.Equal(ledgerCountBefore, await harness.Db.WalletTransactions.CountAsync());
    }

    [Fact, Trait("Category", "AdminApiUnit")]
    public async Task FailedAdminGrantRollsBackLedgerAndBalanceAndCanRetry()
    {
        await using var harness = await AdminHarness.CreateAsync();
        const string trigger = "fail_admin_grant_insert";
        await harness.Db.Database.ExecuteSqlRawAsync($"""
            CREATE TRIGGER "{trigger}"
            BEFORE INSERT ON "WalletTransactions"
            WHEN NEW."Type" = 'ADMIN_GRANT'
            BEGIN
                SELECT RAISE(ABORT, 'test failure');
            END;
            """);
        var balanceBefore = await harness.Db.Wallets.AsNoTracking()
            .Where(item => item.UserId == harness.AdminUserId)
            .Select(item => item.Balance)
            .SingleAsync();
        var ledgerCountBefore = await harness.Db.WalletTransactions.CountAsync();

        await Assert.ThrowsAsync<DbUpdateException>(() => harness.Service.GrantCreditsAsync(
            harness.AdminUserId,
            harness.AdminUserId,
            new AdminGrantCreditsRequest { Amount = 25, Reason = "retry after storage failure" }));

        harness.Db.ChangeTracker.Clear();
        Assert.Equal(balanceBefore, await harness.Db.Wallets.AsNoTracking()
            .Where(item => item.UserId == harness.AdminUserId)
            .Select(item => item.Balance)
            .SingleAsync());
        Assert.Equal(ledgerCountBefore, await harness.Db.WalletTransactions.CountAsync());

        await harness.Db.Database.ExecuteSqlRawAsync($"""DROP TRIGGER "{trigger}";""");
        var retry = await harness.Service.GrantCreditsAsync(
            harness.AdminUserId,
            harness.AdminUserId,
            new AdminGrantCreditsRequest { Amount = 25, Reason = "retry after storage failure" });

        Assert.True(retry.IsSuccess, retry.Message);
        harness.Db.ChangeTracker.Clear();
        Assert.Equal(balanceBefore + 25, await harness.Db.Wallets.AsNoTracking()
            .Where(item => item.UserId == harness.AdminUserId)
            .Select(item => item.Balance)
            .SingleAsync());
        Assert.Equal(ledgerCountBefore + 1, await harness.Db.WalletTransactions.CountAsync());
        Assert.Equal(harness.AdminUserId,
            (await harness.Db.WalletTransactions.SingleAsync(item => item.Id == retry.Data!.TransactionId))
            .ActorAdminUserId);
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

    private static TestServer CreateHttpServer(AppDbContext db) =>
        new(new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddLogging();
                services.AddControllers()
                    .AddApplicationPart(typeof(AdminController).Assembly)
                    .AddJsonOptions(options =>
                        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
                services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                    .AddJwtBearer(options =>
                    {
                        options.TokenValidationParameters = new TokenValidationParameters
                        {
                            ValidateIssuerSigningKey = true,
                            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestJwtSecret)),
                            ValidateIssuer = true,
                            ValidIssuer = TestJwtIssuer,
                            ValidateAudience = true,
                            ValidAudience = TestJwtAudience,
                            ValidateLifetime = true,
                            ClockSkew = TimeSpan.FromSeconds(30),
                            NameClaimType = ClaimTypes.Name,
                            RoleClaimType = ClaimTypes.Role
                        };
                    });
                services.AddAuthorization();
                services.AddScoped<IAdminQueryService>(_ => new AdminQueryService(db));
                services.AddScoped<IPaymentOrderService>(_ =>
                    new PaymentOrderService(db, Options.Create(new PaymentCatalogSettings()), TimeProvider.System));
                services.AddScoped<IPaymentCheckoutService, UnusedPaymentCheckoutService>();
                services.AddScoped(_ => new AdminBalanceAnalyticsService(db));
                services.AddScoped(_ => new RemoteFixedBalanceAdminService(
                    db, TimeProvider.System, Options.Create(new MatchAuthoritySettings())));
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseAuthentication();
                app.UseAuthorization();
                app.UseEndpoints(endpoints => endpoints.MapControllers());
            }));

    private const string TestJwtIssuer = "echo-protocol-tests";
    private const string TestJwtAudience = "echo-protocol-test-client";
    private const string TestJwtSecret = "test-jwt-signing-key-for-backend-integration-at-least-32-bytes";

    private static string CreateAccessToken(User user, int expiryMinutes = 5) =>
        new JwtTokenService(Options.Create(new JwtSettings
        {
            Issuer = TestJwtIssuer,
            Audience = TestJwtAudience,
            SecretKey = TestJwtSecret,
            ExpiryMinutes = expiryMinutes
        })).GenerateToken(user).AccessToken;

    private static AuthenticationHeaderValue Bearer(string token) =>
        new(JwtBearerDefaults.AuthenticationScheme, token);

    private sealed class UnusedPaymentCheckoutService : IPaymentCheckoutService
    {
        public Task<ServiceResult<PaymentCheckoutResponse>> CreateCheckoutAsync(
            Guid userId,
            Guid paymentOrderId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ServiceResult<PaymentCheckoutResponse>.Failure(
                "Checkout is not used by this test host",
                ErrorCodes.PaymentOrderNotFound));
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

        public AppDbContext CreateSiblingDb() => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection).Options);

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
