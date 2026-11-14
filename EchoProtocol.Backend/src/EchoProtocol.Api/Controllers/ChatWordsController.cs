using System.Security.Claims;
using System.Text.Json;
using EchoProtocol.Api.Common;
using EchoProtocol.Api.Data;
using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EchoProtocol.Api.Controllers;

public sealed record ChatWordsResponse(string[] Words, DateTime? UpdatedAtUtc);
public sealed record UpdateChatWordsRequest(string[]? Words);

internal static class ChatWordDefaults
{
    public static readonly string[] Words =
    [
        "địt", "đụ", "đéo", "lồn", "cặc", "buồi", "dái", "đĩ", "phò", "cứt",
        "địt mẹ", "đụ má", "đụ mẹ", "vãi lồn", "vãi cặc", "chó đẻ", "óc chó",
        "súc vật", "súc sinh", "khốn nạn", "mất dạy", "ngu như chó",
        "dit", "djt", "dis", "d1t", "d!t", "dit me", "ditme", "djtme", "duma", "du ma", "du me",
        "loz", "l0n", "l0z", "c4c", "cak", "bu0i", "cho de", "oc cho", "suc vat", "khon nan", "mat day",
        "fuck", "fucking", "fucker", "fucked", "motherfucker", "motherfucking", "clusterfuck",
        "fck", "fuk", "fuq", "phuck", "phuk", "f0ck", "f*ck", "f.u.c.k",
        "shit", "shitty", "bullshit", "horseshit", "dipshit", "sh1t", "sh!t", "shyt", "s.h.i.t",
        "bitch", "bitchy", "b1tch", "b!tch", "b*tch", "biatch", "bastard", "asshole", "asswipe", "dumbass", "jackass",
        "dick", "dickhead", "d1ck", "d!ck", "d*ck", "cock", "c0ck", "c*ck", "cunt", "cun7", "c*nt",
        "pussy", "pu55y", "p*ssy", "twat", "prick", "wanker", "tosser", "bollocks", "whore", "slut", "skank", "douche", "douchebag", "scumbag",
        "stfu", "gtfo", "kys", "kill yourself", "nigger", "nigga", "faggot", "mothafucka", "m0therfucker", "mthrfckr", "mofo", "mfer"
    ];
}

[ApiController]
[Authorize]
public sealed class ChatWordsController(AppDbContext db) : ControllerBase
{
    [HttpGet("api/chat/words")]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var policy = await db.ChatWordPolicies.AsNoTracking().SingleOrDefaultAsync(x => x.Id == 1, ct);
        return Ok(ApiResponse<ChatWordsResponse>.Ok(new(
            policy == null ? ChatWordDefaults.Words : JsonSerializer.Deserialize<string[]>(policy.WordsJson) ?? [],
            policy?.UpdatedAtUtc)));
    }

    [HttpGet("api/admin/chat-words")]
    [Authorize(Roles = nameof(UserRole.ADMIN))]
    public Task<IActionResult> AdminGet(CancellationToken ct) => Get(ct);

    [HttpPost("api/admin/chat-words")]
    [Authorize(Roles = nameof(UserRole.ADMIN))]
    public async Task<IActionResult> Update(UpdateChatWordsRequest request, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var adminId))
            return Unauthorized();
        if (request.Words == null || request.Words.Length > 256
            || request.Words.Any(word => string.IsNullOrWhiteSpace(word) || word.Length > 50
                || !word.Any(char.IsLetterOrDigit) || word.Any(char.IsControl) || word.Any(char.IsSurrogate)))
            return BadRequest(ApiResponse<object>.Fail("Invalid chat word list", ErrorCodes.ValidationError));

        var words = request.Words.Select(word => word.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var policy = await db.ChatWordPolicies.SingleOrDefaultAsync(x => x.Id == 1, ct);
        if (policy == null)
        {
            policy = new ChatWordPolicy();
            db.ChatWordPolicies.Add(policy);
        }
        policy.WordsJson = JsonSerializer.Serialize(words);
        policy.UpdatedAtUtc = DateTime.UtcNow;
        policy.UpdatedByUserId = adminId;
        await db.SaveChangesAsync(ct);
        return Ok(ApiResponse<ChatWordsResponse>.Ok(new(words, policy.UpdatedAtUtc)));
    }
}
