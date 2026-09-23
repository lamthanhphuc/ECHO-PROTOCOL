using EchoProtocol.Api.Common;
using EchoProtocol.Api.Data;
using EchoProtocol.Api.DTOs.Profiles;
using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EchoProtocol.Api.Services;

public sealed class AIProfileReadService(AppDbContext db) : IAIProfileReadService
{
    public async Task<ServiceResult<PlayerAIProfileResponse>> GetOwnPlayerProfileAsync(
        Guid callerUserId, CancellationToken cancellationToken = default)
    {
        var profile = await db.PlayerAIProfiles.AsNoTracking()
            .SingleOrDefaultAsync(item => item.UserId == callerUserId, cancellationToken);
        return profile is null
            ? ServiceResult<PlayerAIProfileResponse>.Failure("Player AI profile was not found", ErrorCodes.AIProfileNotFound)
            : ServiceResult<PlayerAIProfileResponse>.Success(Map(profile));
    }

    public async Task<ServiceResult<RosterAIProfileResponse>> GetMatchRosterProfilesAsync(
        Guid matchId, Guid callerUserId, CancellationToken cancellationToken = default)
    {
        var match = await db.MatchAuthorityBindings.AsNoTracking()
            .SingleOrDefaultAsync(item => item.MatchId == matchId, cancellationToken);
        if (match is null)
            return ServiceResult<RosterAIProfileResponse>.Failure("Match was not found", ErrorCodes.MatchNotFound);
        if (match.HostUserId != callerUserId)
            return ServiceResult<RosterAIProfileResponse>.Failure("Only the verified match host may read roster profiles", ErrorCodes.ProfileReadForbidden);

        var roster = await db.MatchPlayerBindings.AsNoTracking()
            .Where(item => item.MatchId == matchId)
            .OrderBy(item => item.FusionActorNumber)
            .Select(item => item.UserId)
            .ToArrayAsync(cancellationToken);
        var profiles = await db.PlayerAIProfiles.AsNoTracking()
            .Where(item => roster.Contains(item.UserId))
            .ToDictionaryAsync(item => item.UserId, cancellationToken);
        var result = roster.Select(userId => new RosterAIProfileItemResponse(
            userId, profiles.TryGetValue(userId, out var profile) ? Map(profile) : null)).ToArray();
        return ServiceResult<RosterAIProfileResponse>.Success(new(matchId, result));
    }

    public async Task<ServiceResult<TeamProfileResponse>> GetTeamProfileAsync(
        Guid matchId, Guid callerUserId, CancellationToken cancellationToken = default)
    {
        var authorized = await db.MatchAuthorityBindings.AsNoTracking().AnyAsync(item =>
            item.MatchId == matchId
            && (item.HostUserId == callerUserId || item.Players.Any(player => player.UserId == callerUserId)),
            cancellationToken);
        if (!authorized)
        {
            var exists = await db.MatchAuthorityBindings.AsNoTracking()
                .AnyAsync(item => item.MatchId == matchId, cancellationToken);
            return ServiceResult<TeamProfileResponse>.Failure(
                exists ? "Caller is not a member of this match" : "Match was not found",
                exists ? ErrorCodes.ProfileReadForbidden : ErrorCodes.MatchNotFound);
        }

        var profile = await db.TeamProfiles.AsNoTracking()
            .SingleOrDefaultAsync(item => item.MatchId == matchId, cancellationToken);
        return profile is null
            ? ServiceResult<TeamProfileResponse>.Failure("Team profile was not found", ErrorCodes.TeamProfileNotFound)
            : ServiceResult<TeamProfileResponse>.Success(Map(profile));
    }

    private static PlayerAIProfileResponse Map(PlayerAIProfile item) => new(
        item.UserId, item.ProfileLineageId, item.ProfileRevision,
        item.ProfileFormulaVersion, item.MatchScoreFormulaVersion,
        item.NormalizationConfigVersion, item.ProfileNoiseFilterVersion,
        item.AlphaConfigVersion,
        Observed(item.SurvivalScore, item.SurvivalStatus, item.SurvivalSampleCount, item.SurvivalLastMatchId, item.SurvivalLastUpdatedAtUtc),
        Observed(item.NoiseScore, item.NoiseStatus, item.NoiseSampleCount, item.NoiseLastMatchId, item.NoiseLastUpdatedAtUtc),
        Deferred(), Deferred(), Deferred(), Deferred(), Deferred(), Deferred(), Deferred(), item.UpdatedAtUtc);

    private static AIProfileDimensionResponse Observed(
        decimal score, ProfileDimensionStatus status, int sampleCount, Guid? matchId, DateTime? updatedAt) =>
        new(score, DimensionStatus(status), sampleCount, matchId, updatedAt);

    private static AIProfileDimensionResponse Deferred() =>
        new(null, "DEFERRED", null, null, null);

    private static TeamProfileResponse Map(TeamProfile item) => new(
        item.MatchId, item.ProcessingRevision, item.ProcessingStatus.ToString().ToUpperInvariant(),
        item.ProcessingReason, item.TelemetryCompleteness,
        Metric(item.ObjectiveTimeSeconds, item.ObjectiveTimeStatus),
        Metric(item.SplitTime, item.SplitTimeStatus),
        Metric(item.AvgDistance, item.AvgDistanceStatus),
        Metric(item.ReviveSuccess, item.ReviveSuccessStatus),
        Metric(item.ResourceEfficiency, item.ResourceEfficiencyStatus),
        Metric(item.Communication, item.CommunicationStatus),
        Metric(item.WipeRecovery, item.WipeRecoveryStatus),
        Metric(item.ObjectiveSpeedScore, item.ObjectiveSpeedStatus),
        Metric(item.SurvivalScore, item.SurvivalStatus),
        Metric(item.TeamworkScore, item.TeamworkStatus),
        Metric(item.ResourceEfficiencyScore, item.ResourceEfficiencyScoreStatus),
        item.TeamPerformanceScore, item.TeamPerformanceStatus.ToString().ToUpperInvariant(),
        item.ProfileFormulaVersion, item.TeamPerformanceFormulaVersion,
        item.PhaseRegistryVersion, item.NormalizationConfigVersion,
        item.SourceTelemetrySchemaVersion, item.SourceFingerprint,
        item.ProjectionFingerprint, item.UpdatedAtUtc);

    private static TeamMetricResponse Metric(decimal? value, TeamMetricStatus status) =>
        new(value, status.ToString().ToUpperInvariant());

    private static string DimensionStatus(ProfileDimensionStatus status) => status switch
    {
        ProfileDimensionStatus.ColdStart => "COLD_START",
        ProfileDimensionStatus.Active => "ACTIVE",
        ProfileDimensionStatus.Deferred => "DEFERRED",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };
}
