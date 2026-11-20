using System.Security.Claims;
using EchoProtocol.Api.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using Xunit;

namespace EchoProtocol.Api.Tests;

public sealed class PlayerAvatarTests
{
    [Fact]
    public void CanonicalPixelsRoundTripWithoutImageDecoder()
    {
        var bytes = new byte[PlayerAvatarController.PixelBytes];
        new Random(42).NextBytes(bytes);
        Assert.True(PlayerAvatarController.TryDecode(Convert.ToBase64String(bytes), out var decoded));
        Assert.Equal(bytes, decoded);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not an image")]
    public void RejectsInvalidPayload(string? payload) => Assert.False(PlayerAvatarController.TryDecode(payload, out _));

    [Fact]
    public void RejectsOversizedTruncatedAndMalformedPixels()
    {
        Assert.False(PlayerAvatarController.TryDecode(Convert.ToBase64String(new byte[PlayerAvatarController.PixelBytes - 1]), out _));
        Assert.False(PlayerAvatarController.TryDecode(Convert.ToBase64String(new byte[PlayerAvatarController.PixelBytes + 1]), out _));
        Assert.False(PlayerAvatarController.TryDecode(new string('!', PlayerAvatarController.PixelBytes / 3 * 4), out _));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-user-id")]
    public async Task AllEndpointsRejectMissingOrInvalidAccountBeforeDatabaseAccess(string? subject)
    {
        var controller = new PlayerAvatarController(new MongoClient("mongodb://localhost:27017").GetDatabase("avatar_test"));
        var claims = subject == null ? Array.Empty<Claim>() : new[] { new Claim(ClaimTypes.NameIdentifier, subject) };
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims)) } };
        Assert.IsType<UnauthorizedResult>(await controller.Get(default));
        Assert.IsType<UnauthorizedResult>(await controller.Put(new(null), default));
        Assert.IsType<UnauthorizedResult>(await controller.Delete(default));
    }
}
