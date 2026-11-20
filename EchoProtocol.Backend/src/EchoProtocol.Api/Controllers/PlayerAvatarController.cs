using System.Security.Claims;
using EchoProtocol.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;

namespace EchoProtocol.Api.Controllers;

// Fixed-size RGB pixels avoid accepting or serving executable files or image decoder payloads.
[ApiController, Authorize, Route("api/player/me/avatar")]
public sealed class PlayerAvatarController(IMongoDatabase database) : ControllerBase
{
    public const int Width = 256;
    public const int Height = 332;
    public const int PixelBytes = Width * Height * 3;
    private readonly IMongoCollection<BsonDocument> _avatars = database.GetCollection<BsonDocument>("player_avatars");

    public sealed record AvatarRequest(string? Pixels);
    public sealed record AvatarResponse(string Pixels);

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        if (!TryUser(out var user)) return Unauthorized();
        var document = await _avatars.Find(Builders<BsonDocument>.Filter.Eq("_id", user))
            .FirstOrDefaultAsync(cancellationToken);
        Response.Headers.CacheControl = "no-store";
        return Ok(ApiResponse<AvatarResponse>.Ok(new(document == null ? "" : Convert.ToBase64String(document["pixels"].AsByteArray))));
    }

    [HttpPut, RequestSizeLimit(400_000)]
    public async Task<IActionResult> Put(AvatarRequest request, CancellationToken cancellationToken)
    {
        if (!TryUser(out var user)) return Unauthorized();
        if (!TryDecode(request.Pixels, out var pixels))
            return BadRequest(ApiResponse<object>.Fail("Invalid avatar pixels", "AVATAR_INVALID"));
        await _avatars.ReplaceOneAsync(Builders<BsonDocument>.Filter.Eq("_id", user),
            new BsonDocument { ["_id"] = user, ["pixels"] = new BsonBinaryData(pixels), ["updatedAt"] = DateTime.UtcNow },
            new ReplaceOptions { IsUpsert = true }, cancellationToken);
        return Ok(ApiResponse<AvatarResponse>.Ok(new(request.Pixels!)));
    }

    [HttpDelete]
    public async Task<IActionResult> Delete(CancellationToken cancellationToken)
    {
        if (!TryUser(out var user)) return Unauthorized();
        await _avatars.DeleteOneAsync(Builders<BsonDocument>.Filter.Eq("_id", user), cancellationToken);
        return Ok(ApiResponse<AvatarResponse>.Ok(new("")));
    }

    public static bool TryDecode(string? encoded, out byte[] pixels)
    {
        pixels = [];
        if (encoded == null || encoded.Length != ((PixelBytes + 2) / 3) * 4) return false;
        var buffer = new byte[PixelBytes];
        if (!Convert.TryFromBase64String(encoded, buffer, out int count) || count != PixelBytes) return false;
        pixels = buffer;
        return true;
    }

    private bool TryUser(out string user)
    {
        bool valid = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id);
        user = id.ToString("D");
        return valid;
    }
}
