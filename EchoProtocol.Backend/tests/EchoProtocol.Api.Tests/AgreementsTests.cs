using System.Security.Claims;
using EchoProtocol.Api.Controllers;
using EchoProtocol.Api.Data;
using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Legal;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EchoProtocol.Api.Tests;

public sealed class AgreementsTests
{
    private static AppDbContext Database() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static AgreementsController Controller(AppDbContext db, Guid user) => new(db) {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(
            new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, user.ToString()) }, "test")) } } };
    private static AcceptAgreementRequest Valid() => new(AgreementCatalog.Version, AgreementCatalog.Hash, "vi", true,true,true,true,true,true);

    [Fact]
    public async Task AcceptanceIsAccountScopedAndIdempotent()
    {
        await using var db = Database();
        var user = Guid.NewGuid();
        var controller = Controller(db,user);
        Assert.IsType<OkObjectResult>(await controller.Accept(Valid(),default));
        Assert.IsType<OkObjectResult>(await controller.Accept(Valid(),default));
        var saved = Assert.Single(await db.UserAgreementAcceptances.ToListAsync());
        Assert.Equal(user,saved.UserId);
        Assert.Equal(64,saved.ContentSha256.Length);
        Assert.Contains("transactions=",saved.DocumentVersions);
        Assert.True(saved.AdultConfirmed);
        Assert.Equal(DateTimeKind.Utc,saved.AcceptedAtUtc.Kind);
        Assert.False(await db.UserAgreementAcceptances.AnyAsync(a=>a.UserId==Guid.NewGuid()));
    }
    [Fact]
    public async Task RejectsStaleDocumentsAndUncheckedDeclarations()
    {
        await using var db = Database();
        var controller = Controller(db,Guid.NewGuid());
        Assert.IsType<ConflictObjectResult>(await controller.Accept(Valid() with {ContentSha256="old"},default));
        Assert.IsType<BadRequestObjectResult>(await controller.Accept(Valid() with {AdultConfirmed=false},default));
        Assert.IsType<BadRequestObjectResult>(await controller.Accept(Valid() with {PrivacyNoticeAcknowledged=false},default));
        Assert.IsType<BadRequestObjectResult>(await controller.Accept(Valid() with {TransactionsAccepted=false},default));
        Assert.Empty(db.UserAgreementAcceptances);
    }
    [Theory]
    [InlineData("/api/matches/new", "POST", true)]
    [InlineData("/api/telemetry/batch", "POST", true)]
    [InlineData("/api/payments/orders", "POST", true)]
    [InlineData("/api/inventory/equip", "POST", true)]
    [InlineData("/api/payments/orders/123", "GET", false)]
    [InlineData("/api/payments/webhooks/payos", "POST", false)]
    [InlineData("/api/auth/logout", "POST", false)]
    [InlineData("/api/agreements/accept", "POST", false)]
    public void ProtectsGameplayAndPurchasesWithoutBlockingSupportRoutes(string path,string method,bool expected)
        => Assert.Equal(expected, AgreementGateMiddleware.RequiresAcceptance(path,method));

    [Fact]
    public async Task GateDeniesUnacceptedAccountAndAllowsAcceptedAccount()
    {
        await using var db=Database();
        bool nextCalled=false;
        var middleware=new AgreementGateMiddleware(_=> { nextCalled=true; return Task.CompletedTask; });
        var user=Guid.NewGuid();
        var context=Controller(db,user).HttpContext;
        context.Request.Path="/api/payments/orders";context.Request.Method="POST";
        context.Response.Body=new MemoryStream();
        await middleware.InvokeAsync(context,db);
        Assert.Equal(403,context.Response.StatusCode);Assert.False(nextCalled);
        await Controller(db,user).Accept(Valid(),default);
        await middleware.InvokeAsync(context,db);
        Assert.True(nextCalled);
    }
}
