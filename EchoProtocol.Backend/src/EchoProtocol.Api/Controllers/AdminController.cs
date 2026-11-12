using EchoProtocol.Api.Common;
using EchoProtocol.Api.DTOs.Admin;
using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EchoProtocol.Api.Services;
using EchoProtocol.Api.DTOs.MatchResults;
using System.Security.Claims;
using EchoProtocol.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace EchoProtocol.Api.Controllers;

[ApiController]
[Authorize(Roles = nameof(UserRole.ADMIN))]
[Route("api/admin")]
public sealed class AdminController(
    IAdminQueryService service,
    ILogger<AdminController> logger) : ControllerBase
{
    [HttpGet("remote-fixed/game-defaults")]
    public async Task<IActionResult> UnityBalanceDefaults(
        [FromServices] Microsoft.Extensions.Options.IOptions<Configurations.MatchAuthoritySettings> settings,
        CancellationToken ct)
    {
        var path = settings.Value.UnityBalanceDefaultsManifestPath;
        if (string.IsNullOrWhiteSpace(path))
            return Ok(ApiResponse<object>.Ok(new { available = false }));
        try
        {
            await using var stream = System.IO.File.OpenRead(path);
            if (stream.Length > 64 * 1024)
                return Ok(ApiResponse<object>.Ok(new { available = false }));
            using var document = await System.Text.Json.JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var root = document.RootElement;
            if (root.ValueKind != System.Text.Json.JsonValueKind.Object ||
                !root.TryGetProperty("schema", out var schema) || schema.ValueKind != System.Text.Json.JsonValueKind.String ||
                schema.GetString() != "UNITY_BALANCE_DEFAULTS_V1" ||
                !root.TryGetProperty("gameVersion", out var gameVersion) ||
                gameVersion.ValueKind != System.Text.Json.JsonValueKind.String || string.IsNullOrWhiteSpace(gameVersion.GetString()) ||
                !root.TryGetProperty("profiles", out var profiles) || profiles.ValueKind != System.Text.Json.JsonValueKind.Array)
                return Ok(ApiResponse<object>.Ok(new { available = false }));
            return Ok(ApiResponse<object>.Ok(new { available = true, gameVersion = gameVersion.GetString(), profiles = profiles.Clone() }));
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            logger.LogWarning(ex, "Unity balance defaults manifest could not be loaded");
            return Ok(ApiResponse<object>.Ok(new { available = false }));
        }
    }
    [HttpGet("balance-analytics")]
    public Task<IActionResult> BalanceAnalytics([FromServices] AdminBalanceAnalyticsService analytics, CancellationToken ct,
        int days = 30, string? map = null, string? difficulty = null, int? teamSize = null, string? baseline = null, bool testOnly = false) =>
        Execute(() => analytics.GetAsync(days, map, difficulty, teamSize, baseline, testOnly, ct), ct);

    [HttpGet("users/{userId:guid}/ai-history")]
    public Task<IActionResult> PlayerAIHistory(Guid userId, [FromServices] AdminBalanceAnalyticsService analytics,
        CancellationToken ct, int page = 1) =>
        Execute(() => analytics.PlayerAsync(userId, page, ct), ct);

    [HttpGet("economy")]
    public Task<IActionResult> Economy([FromServices] EconomyAdminService economy, CancellationToken ct) =>
        WithAdmin(id => economy.GetAsync(id, ct), ct);

    [HttpPost("economy/prices")]
    public Task<IActionResult> ProposePrice(ProposeShopPrice request, [FromServices] EconomyAdminService economy, CancellationToken ct) =>
        WithAdmin(id => economy.ProposeAsync(id, request, ct), ct);

    [HttpPost("economy/prices/{proposalId:guid}/review")]
    public Task<IActionResult> ReviewPrice(Guid proposalId, ReviewShopPrice request, [FromServices] EconomyAdminService economy, CancellationToken ct) =>
        WithAdmin(id => economy.ReviewAsync(id, proposalId, request, ct), ct);
    [HttpGet("adaptive")]
    public Task<IActionResult> AdaptiveConfiguration([FromQuery] string? compatibility,
        [FromServices] AdaptiveConfigurationService adaptive, CancellationToken ct) =>
        Execute(() => adaptive.GetAsync(compatibility, ct), ct);

    [HttpPost("adaptive")]
    public Task<IActionResult> PublishAdaptiveConfiguration(PublishAdaptiveConfiguration request,
        [FromServices] AdaptiveConfigurationService adaptive, CancellationToken ct) =>
        request.Compatibility == Configurations.MatchAuthoritySettings.CurrentGameVersion
            ? WithAdmin(id => adaptive.PublishAsync(id, request, ct), ct)
            : Task.FromResult<IActionResult>(BadRequest(ApiResponse<object>.Fail("Unsupported Unity build", ErrorCodes.ValidationError)));

    [HttpPost("adaptive/preview")]
    public Task<IActionResult> PreviewAdaptiveConfiguration(PreviewAdaptiveConfiguration request,
        [FromServices] AdaptiveConfigurationService adaptive, CancellationToken ct) =>
        request.Compatibility == Configurations.MatchAuthoritySettings.CurrentGameVersion
            ? Execute(() => adaptive.PreviewAsync(request, ct), ct)
            : Task.FromResult<IActionResult>(BadRequest(ApiResponse<object>.Fail("Unsupported Unity build", ErrorCodes.ValidationError)));

    [HttpGet("matches/{matchId:guid}/adaptive")]
    public Task<IActionResult> MatchAdaptive(Guid matchId, [FromServices] AdaptiveMatchAuditService audit, CancellationToken ct) =>
        Execute(() => audit.GetAsync(matchId, ct), ct);

    [HttpGet("ai-profile")]
    public Task<IActionResult> ProfileTuning([FromServices] ProfileTuningService tuning, CancellationToken ct) =>
        Execute(() => tuning.GetAsync(ct), ct);

    [HttpPost("ai-profile")]
    public Task<IActionResult> PublishProfileTuning(PublishProfileTuning request,
        [FromServices] ProfileTuningService tuning, CancellationToken ct) =>
        WithAdmin(id => tuning.PublishAsync(id, request, ct), ct);

    [HttpPost("ai-profile/preview")]
    public Task<IActionResult> PreviewProfileTuning(PreviewProfileTuning request,
        [FromServices] ProfileTuningService tuning, CancellationToken ct) =>
        Execute(() => tuning.PreviewAsync(request, ct), ct);

    [HttpPost("matches/{matchId:guid}/reprocess-profiles")]
    public Task<IActionResult> ReprocessProfiles(Guid matchId, [FromServices] ProfileTuningService tuning, CancellationToken ct) =>
        WithAdmin(_ => tuning.RequeueAsync(matchId, ct), ct);

    [HttpGet("remote-fixed/status")]
    public Task<IActionResult> RemoteFixedStatus([FromServices] RemoteFixedBalanceAdminService remoteFixed, CancellationToken ct) =>
        Execute(() => Task.FromResult(remoteFixed.GetStatus()), ct);

    [HttpGet("remote-fixed/gameplay-fields")]
    public IActionResult RemoteGameplayFields() => Ok(ApiResponse<RemoteGameplayTuning.Field[]>.Ok(RemoteGameplayTuning.EditableFields));

    [HttpGet("match-content")]
    public IActionResult MatchContent([FromServices] Microsoft.Extensions.Options.IOptions<EchoProtocol.Api.Configurations.MatchAuthoritySettings> settings) =>
        Ok(ApiResponse<object>.Ok(new { MatchContentCatalog.Entries, MatchContentCatalog.Difficulties, GameVersions = settings.Value.RemoteFixedGameVersions }));

    [HttpGet("remote-fixed/versions")]
    public Task<IActionResult> RemoteFixedVersions([FromQuery] RemoteFixedVersionsQuery query,
        [FromServices] RemoteFixedBalanceAdminService remoteFixed, CancellationToken ct) =>
        Execute(() => remoteFixed.GetVersionsAsync(query, ct), ct);

    [HttpGet("remote-fixed/versions/{version}")]
    public Task<IActionResult> RemoteFixedVersion(string version,
        [FromServices] RemoteFixedBalanceAdminService remoteFixed, CancellationToken ct) =>
        Execute(() => remoteFixed.GetVersionAsync(version, ct), ct);

    [HttpPost("remote-fixed/versions")]
    public Task<IActionResult> CreateRemoteFixedVersion([FromBody] CreateRemoteFixedVersionRequest request,
        [FromServices] RemoteFixedBalanceAdminService remoteFixed, CancellationToken ct) =>
        WithAdmin(adminId => remoteFixed.CreateVersionAsync(adminId, request, ct), ct);

    [HttpPost("remote-fixed/versions/{version}/activate")]
    public Task<IActionResult> ActivateRemoteFixedVersion(string version, [FromBody] ChangeRemoteFixedActivationRequest request,
        [FromServices] RemoteFixedBalanceAdminService remoteFixed, CancellationToken ct) =>
        WithAdmin(adminId => remoteFixed.ActivateVersionAsync(adminId, version, request, ct), ct);

    [HttpPost("remote-fixed/versions/{version}/deactivate")]
    public Task<IActionResult> DeactivateRemoteFixedVersion(string version, [FromBody] ChangeRemoteFixedActivationRequest request,
        [FromServices] RemoteFixedBalanceAdminService remoteFixed, CancellationToken ct) =>
        WithAdmin(adminId => remoteFixed.DeactivateVersionAsync(adminId, version, request, ct), ct);

    [HttpGet("remote-fixed/history")]
    public Task<IActionResult> RemoteFixedHistory([FromQuery] RemoteFixedVersionsQuery query,
        [FromServices] RemoteFixedBalanceAdminService remoteFixed, CancellationToken ct) =>
        Execute(() => remoteFixed.GetHistoryAsync(query, ct), ct);

    [HttpGet("matches/{matchId:guid}/recovery")]
    public async Task<IActionResult> InspectRecovery(Guid matchId, [FromServices] AppDbContext db, CancellationToken ct)
    {
        var match = await db.MatchAuthorityBindings.AsNoTracking().Include(item => item.Players)
            .Include(item => item.Result).SingleOrDefaultAsync(item => item.MatchId == matchId, ct);
        if (match == null) return NotFound(ApiResponse<object>.Fail("Match not found", ErrorCodes.MatchNotFound));
        return Ok(ApiResponse<object>.Ok(new
        {
            match.MatchId, match.HostUserId, match.Status, match.StartedAtUtc, match.LeaseExpiresAtUtc,
            players = match.Players.Where(player => player.Participated).Select(player => new
                { player.UserId, disconnected = player.DisconnectedAtUtc.HasValue }),
            result = match.Result == null ? null : new
                { match.Result.Outcome, match.Result.RewardStatus, match.Result.RecoveryAudit, match.Result.EndedAtUtc }
        }, "Recovery review context"));
    }
    [HttpGet("matches/logs")]
    public async Task<IActionResult> MatchLogs([FromQuery] AdminMatchesQuery query,
        [FromServices] AppDbContext db, CancellationToken ct)
    {
        if (query.Page < 1 || query.PageSize is < 1 or > 100 ||
            (long)(query.Page - 1) * query.PageSize > int.MaxValue)
            return BadRequest(ApiResponse<object>.Fail("Invalid pagination", ErrorCodes.ValidationError));

        var matches = db.MatchAuthorityBindings.AsNoTracking();
        if (query.MatchId is { } matchId)
            matches = matches.Where(item => item.MatchId == matchId);
        if (query.UserId is { } userId)
            matches = matches.Where(item => item.Players.Any(player => player.UserId == userId));
        if (query.Status is { } status)
            matches = matches.Where(item => item.Status == status);
        if (query.FlaggedOnly)
            matches = matches.Where(item => db.AdminModerationActions
                .Where(entry => entry.TargetId == item.MatchId && (entry.Action == "FLAG_MATCH" || entry.Action == "CLEAR_MATCH"))
                .OrderByDescending(entry => entry.CreatedAtUtc).ThenByDescending(entry => entry.Id)
                .Select(entry => entry.Action).FirstOrDefault() == "FLAG_MATCH");

        var totalItems = await matches.CountAsync(ct);
        var items = await matches.OrderByDescending(item => item.CreatedAtUtc)
            .ThenByDescending(item => item.MatchId)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(item => new AdminMatchResponse
            {
                MatchId = item.MatchId,
                HostUserId = item.HostUserId,
                HostUsername = item.HostUser == null ? "" : item.HostUser.Username,
                MapId = item.MapId,
                Difficulty = item.Difficulty,
                Status = item.Status,
                Outcome = item.Result == null ? null : item.Result.Outcome,
                PlayerCount = item.Result == null ? item.Players.Count() : item.Result.PlayerCount,
                ConnectedCount = item.Players.Count(player => player.DisconnectedAtUtc == null),
                IsFlagged = db.AdminModerationActions
                    .Where(entry => entry.TargetId == item.MatchId && (entry.Action == "FLAG_MATCH" || entry.Action == "CLEAR_MATCH"))
                    .OrderByDescending(entry => entry.CreatedAtUtc).ThenByDescending(entry => entry.Id)
                    .Select(entry => entry.Action).FirstOrDefault() == "FLAG_MATCH",
                LeaseExpiresAtUtc = item.LeaseExpiresAtUtc,
                StartedAtUtc = item.StartedAtUtc,
                EndedAtUtc = item.EndedAtUtc
            }).ToArrayAsync(ct);
        return Ok(ApiResponse<AdminPagedResponse<AdminMatchResponse>>.Ok(new()
        {
            Items = items, Page = query.Page, PageSize = query.PageSize,
            TotalItems = totalItems,
            TotalPages = (int)Math.Ceiling(totalItems / (double)query.PageSize)
        }));
    }
    [HttpGet("matches/{matchId:guid}")]
    public async Task<IActionResult> MatchDetail(Guid matchId, [FromServices] AppDbContext db, CancellationToken ct)
    {
        var match = await db.MatchAuthorityBindings.AsNoTracking()
            .Where(item => item.MatchId == matchId)
            .Select(item => new AdminMatchResponse
            {
                MatchId = item.MatchId,
                HostUserId = item.HostUserId,
                HostUsername = item.HostUser == null ? "" : item.HostUser.Username,
                MapId = item.MapId,
                Difficulty = item.Difficulty,
                Status = item.Status,
                Outcome = item.Result == null ? null : item.Result.Outcome,
                PlayerCount = item.Result == null ? item.Players.Count() : item.Result.PlayerCount,
                ConnectedCount = item.Players.Count(player => player.DisconnectedAtUtc == null),
                IsFlagged = db.AdminModerationActions
                    .Where(entry => entry.TargetId == item.MatchId && (entry.Action == "FLAG_MATCH" || entry.Action == "CLEAR_MATCH"))
                    .OrderByDescending(entry => entry.CreatedAtUtc).ThenByDescending(entry => entry.Id)
                    .Select(entry => entry.Action).FirstOrDefault() == "FLAG_MATCH",
                LeaseExpiresAtUtc = item.LeaseExpiresAtUtc,
                StartedAtUtc = item.StartedAtUtc,
                EndedAtUtc = item.EndedAtUtc
            }).SingleOrDefaultAsync(ct);
        if (match is null) return NotFound(ApiResponse<object>.Fail("Match not found", ErrorCodes.MatchNotFound));
        var players = await db.MatchPlayerBindings.AsNoTracking()
            .Where(item => item.MatchId == matchId)
            .OrderBy(item => item.BoundAtUtc)
            .Select(item => new AdminMatchPlayerResponse
            {
                UserId = item.UserId,
                Username = item.User == null ? "" : item.User.Username,
                DisplayName = item.User == null || item.User.PlayerProfile == null ? null : item.User.PlayerProfile.DisplayName,
                FusionActorNumber = item.FusionActorNumber,
                Participated = item.Participated,
                HadGameplayDisconnect = item.HadGameplayDisconnect,
                BoundAtUtc = item.BoundAtUtc,
                LastSeenAtUtc = item.LastSeenAtUtc,
                DisconnectedAtUtc = item.DisconnectedAtUtc
            }).ToArrayAsync(ct);
        return Ok(ApiResponse<AdminMatchDetailResponse>.Ok(new() { Match = match, Players = players }));
    }
    [HttpGet("moderation/{targetId:guid}")]
    public Task<IActionResult> ModerationHistory(Guid targetId,
        [FromServices] AdminModerationService moderation, CancellationToken ct) =>
        Execute(() => moderation.HistoryAsync(targetId, ct), ct);

    [HttpPost("moderation/{targetId:guid}")]
    public Task<IActionResult> Moderate(Guid targetId, [FromBody] AdminModerationRequest request,
        [FromServices] AdminModerationService moderation, CancellationToken ct) =>
        WithAdmin(adminId => moderation.ApplyAsync(adminId, targetId, request.Action, request.Reason, ct), ct);
    [HttpPost("matches/{matchId:guid}/recover-result")]
    public async Task<IActionResult> RecoverResult(Guid matchId, [FromBody] RecoverMatchResultRequest request,
        [FromServices] MatchResultService results, [FromServices] IRewardService rewards,
        CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var adminId)) return Unauthorized();
        return await Execute(async () =>
        {
            var result = await results.RecoverAsync(adminId, matchId, request.Result, request.EndedAtUtc, request.Reason, ct);
            if (result.IsSuccess)
            {
                var reward = await rewards.ProcessAsync(matchId, ct);
                if (reward.IsSuccess) result.Data!.RewardStatus = reward.Data!.RewardStatus;
                else
                {
                    logger.LogWarning("Recovered match {MatchId} saved, but reward processing failed: {ErrorCode}", matchId, reward.ErrorCode);
                    return ServiceResult<EchoProtocol.Api.DTOs.MatchResults.MatchResultResponse>.Success(
                        result.Data!, "Result saved; rewards are pending. Retry this recovery request to process rewards again.");
                }
            }
            return result;
        }, ct);
    }
    [HttpGet("users")]
    public Task<IActionResult> Users(
        [FromQuery] AdminUsersQuery query,
        CancellationToken cancellationToken) => Execute(
            () => service.GetUsersAsync(query, cancellationToken), cancellationToken);

    [HttpGet("users/{userId:guid}")]
    public Task<IActionResult> UserDetail(
        Guid userId,
        CancellationToken cancellationToken) => Execute(
            () => service.GetUserAsync(userId, cancellationToken), cancellationToken);    [HttpPost("users/{userId:guid}/wallet/credit")]
    public Task<IActionResult> GrantCredits(
        Guid userId,
        [FromBody] AdminGrantCreditsRequest request,
        CancellationToken cancellationToken) => Execute(
            () => service.GrantCreditsAsync(
                userId,
                request,
                cancellationToken),
            cancellationToken);


    [HttpGet("payments")]
    public Task<IActionResult> Payments(
        [FromQuery] AdminPaymentsQuery query,
        CancellationToken cancellationToken) => Execute(
            () => service.GetPaymentsAsync(query, cancellationToken), cancellationToken);

    [HttpGet("payments/{paymentOrderId:guid}")]
    public Task<IActionResult> PaymentDetail(
        Guid paymentOrderId,
        CancellationToken cancellationToken) => Execute(
            () => service.GetPaymentAsync(paymentOrderId, cancellationToken), cancellationToken);

    [HttpPost("payments/{paymentOrderId:guid}/retry-fulfillment")]
    public Task<IActionResult> RetryFulfillment(Guid paymentOrderId,
        [FromServices] PaymentRecoveryService recovery, CancellationToken cancellationToken) => Execute(
            () => recovery.RecoverAsync(paymentOrderId, cancellationToken), cancellationToken);

    [HttpGet("wallet-transactions")]
    public Task<IActionResult> WalletTransactions(
        [FromQuery] AdminWalletTransactionsQuery query,
        CancellationToken cancellationToken) => Execute(
            () => service.GetWalletTransactionsAsync(query, cancellationToken), cancellationToken);

    [HttpGet("purchases")]
    public Task<IActionResult> Purchases(
        [FromQuery] AdminPurchasesQuery query,
        CancellationToken cancellationToken) => Execute(
            () => service.GetPurchasesAsync(query, cancellationToken), cancellationToken);

    private async Task<IActionResult> Execute<T>(
        Func<Task<ServiceResult<T>>> action,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await action();
            return result.IsSuccess
                ? Ok(ApiResponse<T>.Ok(result.Data!, result.Message))
                : StatusCode(
                    result.ErrorCode is ErrorCodes.NotFound or ErrorCodes.PaymentOrderNotFound
                        ? StatusCodes.Status404NotFound
                        : result.ErrorCode == "FORBIDDEN" ? StatusCodes.Status403Forbidden
                        : result.ErrorCode == ErrorCodes.Conflict
                            ? StatusCodes.Status409Conflict
                            : StatusCodes.Status400BadRequest,
                    ApiResponse<object>.Fail(result.Message, result.ErrorCode!));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unexpected admin query failure");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                ApiResponse<object>.Fail(
                    "Internal server error", ErrorCodes.InternalServerError));
        }
    }

    private Task<IActionResult> WithAdmin<T>(Func<Guid, Task<ServiceResult<T>>> action, CancellationToken ct) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var adminId)
            ? Execute(() => action(adminId), ct)
            : Task.FromResult<IActionResult>(Unauthorized());
}

public sealed class RecoverMatchResultRequest
{
    [System.ComponentModel.DataAnnotations.Required]
    public SubmitMatchResultRequest Result { get; set; } = new();
    public DateTime EndedAtUtc { get; set; }
    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(1000, MinimumLength = 10)]
    public string Reason { get; set; } = string.Empty;
}

public sealed class AdminModerationRequest
{
    public string Action { get; set; } = "";
    public string Reason { get; set; } = "";
}
