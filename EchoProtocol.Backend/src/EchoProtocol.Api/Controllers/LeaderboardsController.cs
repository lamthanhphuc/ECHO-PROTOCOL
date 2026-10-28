using EchoProtocol.Api.Common;
using EchoProtocol.Api.DTOs.Leaderboards;
using EchoProtocol.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EchoProtocol.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/leaderboards")]
public sealed class LeaderboardsController : ControllerBase
{
    private readonly ILeaderboardService _service;

    public LeaderboardsController(ILeaderboardService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] string mode = "SOLO",
        [FromQuery] string? mapId = null,
        [FromQuery] string? difficulty = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<LeaderboardMode>(mode, ignoreCase: true, out var parsedMode)
            || !Enum.IsDefined(parsedMode)
            || !LeaderboardCatalog.IsKnownMap(mapId)
            || !LeaderboardCatalog.IsKnownDifficulty(difficulty?.ToUpperInvariant())
            || page is < 1 or > 1_000_000
            || pageSize is < 1 or > 50)
        {
            return BadRequest(ApiResponse<object>.Fail(
                "Leaderboard mode, map, difficulty, or page is invalid",
                ErrorCodes.ValidationError));
        }

        var result = await _service.GetAsync(
            parsedMode,
            mapId!,
            difficulty!.ToUpperInvariant(),
            page,
            pageSize,
            cancellationToken);
        return Ok(ApiResponse<LeaderboardPageResponse>.Ok(result));
    }
}
