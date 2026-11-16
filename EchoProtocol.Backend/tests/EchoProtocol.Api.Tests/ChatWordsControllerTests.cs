using System.Security.Claims;
using EchoProtocol.Api.Controllers;
using EchoProtocol.Api.Common;
using EchoProtocol.Api.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EchoProtocol.Api.Tests;

public sealed class ChatWordsControllerTests
{
    [Fact]
    public async Task AdminCanReplaceAndClearWords()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var adminId = Guid.NewGuid();
        var controller = new ChatWordsController(db) { ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, adminId.ToString())], "test")) }
        } };

        var initial = Assert.IsType<ApiResponse<ChatWordsResponse>>(
            Assert.IsType<OkObjectResult>(await controller.Get(default)).Value);
        Assert.Null(initial.Data!.UpdatedAtUtc);
        Assert.Contains("fuck", initial.Data.Words);
        Assert.InRange(initial.Data.Words.Length, 1, 256);

        Assert.IsType<OkObjectResult>(await controller.Update(new([" bad word ", "bad word"]), default));
        var policy = Assert.Single(await db.ChatWordPolicies.ToListAsync());
        Assert.Equal("[\"bad word\"]", policy.WordsJson);
        Assert.Equal(adminId, policy.UpdatedByUserId);
        Assert.IsType<OkObjectResult>(await controller.Update(new([]), default));
        Assert.Equal("[]", policy.WordsJson);
    }

    [Fact]
    public async Task RejectsInvalidWords()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var controller = new ChatWordsController(db) { ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())], "test")) }
        } };

        Assert.IsType<BadRequestObjectResult>(await controller.Update(new(["bad\nword"]), default));
        Assert.IsType<BadRequestObjectResult>(await controller.Update(new(["!!!"]), default));
        Assert.Empty(await db.ChatWordPolicies.ToListAsync());
    }
}
