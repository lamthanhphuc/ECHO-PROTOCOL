using EchoProtocol.Api.Common;
using EchoProtocol.Api.Data;
using EchoProtocol.Api.Data.Telemetry;
using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services.Interfaces;
using EchoProtocol.Api.Services.Models;
using Microsoft.EntityFrameworkCore;

namespace EchoProtocol.Api.Services;

public sealed class TeamProfileService : ITeamProfileService
{
    private readonly AppDbContext _db;
    private readonly ITelemetryEventRepository _repository;
    private readonly ITeamProfilePolicy _policy;
    private readonly TimeProvider _clock;

    public TeamProfileService(
        AppDbContext db,
        ITelemetryEventRepository repository,
        ITeamProfilePolicy policy,
        TimeProvider clock)
    {
        _db = db;
        _repository = repository;
        _policy = policy;
        _clock = clock;
    }

    public async Task<ServiceResult<TeamProfileProcessResult>> ProcessAsync(
        Guid matchId,
        CancellationToken cancellationToken = default)
    {
        var planned = await BuildProjectionAsync(matchId, cancellationToken);
        if (planned is null)
        {
            return ServiceResult<TeamProfileProcessResult>.Failure(
                "MatchResult not found", ErrorCodes.TeamProfileMatchNotFound);
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var match = await LockMatchAsync(matchId, cancellationToken);
        if (match is null)
        {
            return ServiceResult<TeamProfileProcessResult>.Failure(
                "Match not found", ErrorCodes.TeamProfileMatchNotFound);
        }

        _db.ChangeTracker.Clear();
        var current = await BuildProjectionAsync(matchId, cancellationToken);
        if (current is null || planned.SourceFingerprint != current.SourceFingerprint ||
            planned.ProjectionFingerprint != current.ProjectionFingerprint)
        {
            return ServiceResult<TeamProfileProcessResult>.Failure(
                "Telemetry or relational source changed before TeamProfile commit",
                ErrorCodes.TeamProfileSourceChanged);
        }

        var entity = await _db.TeamProfiles.SingleOrDefaultAsync(
            item => item.MatchId == matchId, cancellationToken);
        if (entity is not null && entity.ProjectionFingerprint == current.ProjectionFingerprint)
        {
            await transaction.CommitAsync(cancellationToken);
            return ServiceResult<TeamProfileProcessResult>.Success(
                new(matchId, entity.ProcessingRevision, true, entity.ProcessingStatus),
                "Duplicate TeamProfile processing was ignored");
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        if (entity is null)
        {
            entity = new TeamProfile
            {
                MatchId = matchId,
                ProcessingRevision = 1,
                CreatedAtUtc = now
            };
            _db.TeamProfiles.Add(entity);
        }
        else
        {
            entity.ProcessingRevision++;
        }

        Apply(entity, current, now);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ServiceResult<TeamProfileProcessResult>.Success(
            new(matchId, entity.ProcessingRevision, false, entity.ProcessingStatus));
    }

    private async Task<TeamProfileProjection?> BuildProjectionAsync(
        Guid matchId,
        CancellationToken cancellationToken)
    {
        var result = await _db.MatchResults.AsNoTracking()
            .Include(item => item.Match)
            .Include(item => item.Players)
            .SingleOrDefaultAsync(item => item.MatchId == matchId, cancellationToken);
        if (result is null)
        {
            return null;
        }

        var boundUsers = (await _db.MatchPlayerBindings.AsNoTracking()
            .Where(item => item.MatchId == matchId)
            .Select(item => item.UserId)
            .ToListAsync(cancellationToken)).ToHashSet();
        var events = await _repository.LoadAcceptedMatchEventsAsync(matchId, cancellationToken);
        var facts = new TeamMatchFacts(matchId, result.Outcome,
            result.Players.Count(item => item.Survived),
            result.Match.Status == MatchAuthorityStatus.Ended && result.Match.EndedAtUtc.HasValue);
        return TeamProfileProjector.Project(facts, events, boundUsers, _policy);
    }

    private async Task<MatchAuthorityBinding?> LockMatchAsync(Guid matchId, CancellationToken cancellationToken)
    {
        if (_db.Database.IsNpgsql())
        {
            return await _db.MatchAuthorityBindings
                .FromSqlInterpolated($"SELECT * FROM \"MatchAuthorityBindings\" WHERE \"MatchId\" = {matchId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);
        }
        return await _db.MatchAuthorityBindings.SingleOrDefaultAsync(
            item => item.MatchId == matchId, cancellationToken);
    }

    private void Apply(TeamProfile entity, TeamProfileProjection projection, DateTime now)
    {
        entity.ProcessingStatus = projection.ProcessingStatus;
        entity.ProcessingReason = projection.ProcessingReason;
        entity.TelemetryCompleteness = projection.Completeness.ToString();
        entity.ObjectiveTimeSeconds = projection.ObjectiveTimeSeconds;
        entity.ObjectiveTimeStatus = projection.ObjectiveTimeStatus;
        entity.ObjectiveSpeedScore = projection.ObjectiveSpeedScore;
        entity.ObjectiveSpeedStatus = projection.ObjectiveSpeedStatus;
        entity.SurvivalScore = projection.SurvivalScore;
        entity.SurvivalStatus = projection.SurvivalStatus;

        entity.SplitTime = null;
        entity.SplitTimeStatus = TeamMetricStatus.Deferred;
        entity.AvgDistance = null;
        entity.AvgDistanceStatus = TeamMetricStatus.Deferred;
        entity.ReviveSuccess = null;
        entity.ReviveSuccessStatus = TeamMetricStatus.Deferred;
        entity.ResourceEfficiency = null;
        entity.ResourceEfficiencyStatus = TeamMetricStatus.Deferred;
        entity.Communication = null;
        entity.CommunicationStatus = TeamMetricStatus.Deferred;
        entity.WipeRecovery = null;
        entity.WipeRecoveryStatus = TeamMetricStatus.Deferred;
        entity.TeamworkScore = null;
        entity.TeamworkStatus = TeamMetricStatus.Deferred;
        entity.ResourceEfficiencyScore = null;
        entity.ResourceEfficiencyScoreStatus = TeamMetricStatus.Deferred;
        entity.TeamPerformanceScore = null;
        entity.TeamPerformanceStatus = TeamPerformanceStatus.Incomplete;

        entity.ProfileFormulaVersion = _policy.ProfileFormulaVersion;
        entity.TeamPerformanceFormulaVersion = _policy.TeamPerformanceFormulaVersion;
        entity.PhaseRegistryVersion = _policy.PhaseRegistryVersion;
        entity.NormalizationConfigVersion = _policy.NormalizationConfigVersion;
        entity.SourceTelemetrySchemaVersion = projection.SourceTelemetrySchemaVersion;
        entity.SourceFingerprint = projection.SourceFingerprint;
        entity.ProjectionFingerprint = projection.ProjectionFingerprint;
        entity.UpdatedAtUtc = now;
    }
}
