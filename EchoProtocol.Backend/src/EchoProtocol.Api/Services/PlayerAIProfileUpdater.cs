using System.Security.Cryptography;
using System.Text;
using System.Globalization;
using EchoProtocol.Api.Common;
using EchoProtocol.Api.Data;
using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services.Interfaces;
using EchoProtocol.Api.Services.Models;
using Microsoft.EntityFrameworkCore;

namespace EchoProtocol.Api.Services;

public sealed class PlayerAIProfileUpdater : IPlayerAIProfileUpdater
{
    private readonly AppDbContext _db;
    private readonly IMatchTelemetryAggregator _aggregator;
    private readonly IPlayerAIProfilePolicy _policy;
    private readonly TimeProvider _clock;

    public PlayerAIProfileUpdater(
        AppDbContext db,
        IMatchTelemetryAggregator aggregator,
        IPlayerAIProfilePolicy policy,
        TimeProvider clock)
    {
        _db = db;
        _aggregator = aggregator;
        _policy = policy;
        _clock = clock;
    }

    public async Task<ServiceResult<PlayerAIProfileUpdateResult>> ProcessAsync(
        Guid matchId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var aggregation = await _aggregator.AggregateAsync(matchId, userId, cancellationToken);
        if (aggregation.Eligibility == MatchProfileEligibilityStatus.Pending)
        {
            return ServiceResult<PlayerAIProfileUpdateResult>.Failure(
                "Match telemetry is not finalized", ErrorCodes.AIProfileSourcePending);
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var user = await LockUserAsync(userId, cancellationToken);
        if (user is null)
        {
            return ServiceResult<PlayerAIProfileUpdateResult>.Failure("User not found", ErrorCodes.NotFound);
        }

        var profile = await _db.PlayerAIProfiles
            .SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);

        if (aggregation.Eligibility == MatchProfileEligibilityStatus.Ineligible)
        {
            if (profile is null)
            {
                return ServiceResult<PlayerAIProfileUpdateResult>.Failure(
                    $"Match is ineligible: {string.Join(',', aggregation.Reasons)}",
                    ErrorCodes.AIProfileSourceInvalid);
            }

            var contributingDimensions = await _db.MatchScores.AsNoTracking()
                .Where(item => item.UserId == userId && item.MatchId == matchId &&
                               item.ProfileLineageId == profile.ProfileLineageId &&
                               item.ContributionStatus == ProfileContributionStatus.Contributing)
                .Select(item => item.Dimension)
                .Distinct()
                .ToListAsync(cancellationToken);
            foreach (var dimension in contributingDimensions)
            {
                if (!_policy.TryValidate(dimension, out var reason))
                {
                    return ServiceResult<PlayerAIProfileUpdateResult>.Failure(
                        $"Cannot replay {dimension}: {reason}", ErrorCodes.AIProfilePolicyNotConfigured);
                }
            }

            var retracted = await RetractMatchAsync(profile, matchId, cancellationToken);
            if (retracted.Count == 0)
            {
                return ServiceResult<PlayerAIProfileUpdateResult>.Failure(
                    $"Match is ineligible: {string.Join(',', aggregation.Reasons)}",
                    ErrorCodes.AIProfileSourceInvalid);
            }

            await ReplayAsync(profile, retracted, [], cancellationToken);
            profile.ProfileRevision++;
            profile.UpdatedAtUtc = _clock.GetUtcNow().UtcDateTime;
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ServiceResult<PlayerAIProfileUpdateResult>.Success(
                new(userId, matchId, profile.ProfileLineageId, profile.ProfileRevision,
                    false, [], retracted));
        }

        var normalized = new List<NormalizedMatchScore>();
        var hasConfigurationFailure = false;
        foreach (var metric in aggregation.Metrics.Values)
        {
            if (MatchScoreNormalizer.TryNormalize(metric, _policy, out var score, out _))
            {
                normalized.Add(score!);
            }
            else if (metric.Availability == MetricAvailability.Available &&
                     !_policy.TryValidate(metric.Dimension, out _))
            {
                hasConfigurationFailure = true;
            }
        }

        if (profile is null && normalized.Count == 0)
        {
            return ServiceResult<PlayerAIProfileUpdateResult>.Failure(
                hasConfigurationFailure
                    ? "No AVAILABLE dimension has an approved production policy"
                    : "No eligible AVAILABLE Player MatchScore exists",
                hasConfigurationFailure
                    ? ErrorCodes.AIProfilePolicyNotConfigured
                    : ErrorCodes.AIProfileSourceInvalid);
        }

        profile ??= CreateColdStartProfile(userId);
        if (_db.Entry(profile).State == EntityState.Detached)
        {
            _db.PlayerAIProfiles.Add(profile);
        }

        if (!TryAlignVersions(profile, normalized.Select(item => item.Dimension)))
        {
            return ServiceResult<PlayerAIProfileUpdateResult>.Failure(
                "Configured formula versions do not match the existing profile lineage",
                ErrorCodes.AIProfileApplyConflict);
        }

        // MongoDB is deliberately outside the PostgreSQL transaction. Re-read immediately
        // before mutation and reject a stale plan rather than attempting a cross-store transaction.
        var current = await _aggregator.AggregateAsync(matchId, userId, cancellationToken);
        if (!EquivalentSource(aggregation, current))
        {
            return ServiceResult<PlayerAIProfileUpdateResult>.Failure(
                "Telemetry source changed while the profile update was being planned",
                ErrorCodes.AIProfileSourceChanged);
        }

        var existing = await _db.MatchScores
            .Where(item => item.UserId == userId && item.MatchId == matchId &&
                           item.ProfileLineageId == profile.ProfileLineageId)
            .ToListAsync(cancellationToken);
        var metricRetractions = new List<PlayerAIDimension>();
        foreach (var invalidDimension in aggregation.Metrics.Values
                     .Where(item => item.Availability == MetricAvailability.Invalid)
                     .Select(item => item.Dimension))
        {
            var receipt = existing.SingleOrDefault(item =>
                item.Dimension == invalidDimension &&
                item.ContributionStatus == ProfileContributionStatus.Contributing);
            if (receipt is null)
            {
                continue;
            }

            if (!_policy.TryValidate(invalidDimension, out var replayReason))
            {
                return ServiceResult<PlayerAIProfileUpdateResult>.Failure(
                    $"Cannot replay {invalidDimension}: {replayReason}",
                    ErrorCodes.AIProfilePolicyNotConfigured);
            }

            receipt.ContributionStatus = ProfileContributionStatus.Retracted;
            receipt.RetractionReason = "METRIC_BECAME_INVALID";
            receipt.RetractedAtUtc = _clock.GetUtcNow().UtcDateTime;
            metricRetractions.Add(invalidDimension);
        }

        if (normalized.Count == 0 && metricRetractions.Count == 0)
        {
            return ServiceResult<PlayerAIProfileUpdateResult>.Failure(
                hasConfigurationFailure
                    ? "No AVAILABLE dimension has an approved production policy"
                    : "No eligible AVAILABLE Player MatchScore exists",
                hasConfigurationFailure
                    ? ErrorCodes.AIProfilePolicyNotConfigured
                    : ErrorCodes.AIProfileSourceInvalid);
        }

        var added = new List<MatchScore>();
        foreach (var score in normalized)
        {
            var semanticFingerprint = ScoreFingerprint(aggregation, score, profile.ProfileLineageId);
            var receipt = existing.SingleOrDefault(item => item.Dimension == score.Dimension);
            if (receipt is not null)
            {
                if (!SameSemanticPayload(receipt, aggregation, score))
                {
                    return ServiceResult<PlayerAIProfileUpdateResult>.Failure(
                        "The dimension apply key already has a different immutable score",
                        ErrorCodes.AIProfileApplyConflict);
                }

                continue;
            }

            added.Add(new MatchScore
            {
                Id = Guid.NewGuid(),
                MatchId = matchId,
                UserId = userId,
                ProfileLineageId = profile.ProfileLineageId,
                Dimension = score.Dimension,
                Score = score.Score,
                MatchEndTs = aggregation.MatchEndTs!.Value,
                MatchScoreFormulaVersion = score.MatchScoreFormulaVersion,
                NormalizationConfigVersion = score.NormalizationConfigVersion,
                ProfileNoiseFilterVersion = score.ProfileNoiseFilterVersion,
                AlphaConfigVersion = _policy.AlphaConfigVersion!,
                SourceTelemetrySchemaVersion = aggregation.SourceSchemaVersion,
                SourceEvidenceFingerprint = score.EvidenceFingerprint,
                SemanticFingerprint = semanticFingerprint,
                ContributionStatus = ProfileContributionStatus.Contributing,
                CreatedAtUtc = _clock.GetUtcNow().UtcDateTime
            });
        }

        if (added.Count == 0 && metricRetractions.Count == 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return ServiceResult<PlayerAIProfileUpdateResult>.Success(
                new(userId, matchId, profile.ProfileLineageId, profile.ProfileRevision,
                    true, [], []), "Duplicate profile update was ignored");
        }

        _db.MatchScores.AddRange(added);
        var affectedDimensions = added.Select(item => item.Dimension)
            .Concat(metricRetractions)
            .Distinct()
            .ToArray();
        await ReplayAsync(profile, affectedDimensions, added, cancellationToken);
        profile.ProfileRevision++;
        profile.UpdatedAtUtc = _clock.GetUtcNow().UtcDateTime;
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ServiceResult<PlayerAIProfileUpdateResult>.Success(
            new(userId, matchId, profile.ProfileLineageId, profile.ProfileRevision,
                false, added.Select(item => item.Dimension).ToArray(), metricRetractions));
    }

    private async Task<User?> LockUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (_db.Database.IsNpgsql())
        {
            return await _db.Users
                .FromSqlInterpolated($"SELECT * FROM \"Users\" WHERE \"Id\" = {userId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);
        }

        return await _db.Users.SingleOrDefaultAsync(item => item.Id == userId, cancellationToken);
    }

    private PlayerAIProfile CreateColdStartProfile(Guid userId)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        return new PlayerAIProfile
        {
            UserId = userId,
            ProfileLineageId = Guid.NewGuid(),
            ProfileRevision = 0,
            ProfileFormulaVersion = _policy.ProfileFormulaVersion,
            MatchScoreFormulaVersion = _policy.MatchScoreFormulaVersion,
            NormalizationConfigVersion = null,
            ProfileNoiseFilterVersion = null,
            AlphaConfigVersion = _policy.AlphaConfigVersion!,
            SurvivalScore = 50m,
            SurvivalStatus = ProfileDimensionStatus.ColdStart,
            NoiseScore = 50m,
            NoiseStatus = ProfileDimensionStatus.ColdStart,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    private bool TryAlignVersions(PlayerAIProfile profile, IEnumerable<PlayerAIDimension> dimensions)
    {
        if (profile.ProfileFormulaVersion != _policy.ProfileFormulaVersion ||
            profile.MatchScoreFormulaVersion != _policy.MatchScoreFormulaVersion ||
            profile.AlphaConfigVersion != _policy.AlphaConfigVersion)
        {
            return false;
        }

        if (!dimensions.Contains(PlayerAIDimension.Noise))
        {
            return true;
        }

        if (profile.NoiseSampleCount == 0)
        {
            profile.NormalizationConfigVersion = _policy.NormalizationConfigVersion;
            profile.ProfileNoiseFilterVersion = _policy.ProfileNoiseFilterVersion;
            return true;
        }

        return profile.NormalizationConfigVersion == _policy.NormalizationConfigVersion &&
               profile.ProfileNoiseFilterVersion == _policy.ProfileNoiseFilterVersion;
    }

    private async Task<IReadOnlyList<PlayerAIDimension>> RetractMatchAsync(
        PlayerAIProfile profile, Guid matchId, CancellationToken cancellationToken)
    {
        var observations = await _db.MatchScores
            .Where(item => item.UserId == profile.UserId && item.MatchId == matchId &&
                           item.ProfileLineageId == profile.ProfileLineageId &&
                           item.ContributionStatus == ProfileContributionStatus.Contributing)
            .ToListAsync(cancellationToken);
        var now = _clock.GetUtcNow().UtcDateTime;
        foreach (var observation in observations)
        {
            observation.ContributionStatus = ProfileContributionStatus.Retracted;
            observation.RetractionReason = "MATCH_BECAME_INELIGIBLE";
            observation.RetractedAtUtc = now;
        }

        return observations.Select(item => item.Dimension).Distinct().ToArray();
    }

    private async Task ReplayAsync(
        PlayerAIProfile profile,
        IReadOnlyCollection<PlayerAIDimension> dimensions,
        IReadOnlyCollection<MatchScore> pending,
        CancellationToken cancellationToken)
    {
        var persisted = await _db.MatchScores.AsNoTracking()
            .Where(item => item.UserId == profile.UserId &&
                           item.ProfileLineageId == profile.ProfileLineageId &&
                           item.ContributionStatus == ProfileContributionStatus.Contributing)
            .ToListAsync(cancellationToken);
        var observations = persisted
            .Where(item => !_db.MatchScores.Local.Any(local => local.Id == item.Id &&
                local.ContributionStatus == ProfileContributionStatus.Retracted))
            .Concat(pending)
            .ToArray();

        foreach (var dimension in dimensions)
        {
            var ordered = observations.Where(item => item.Dimension == dimension)
                .OrderBy(item => item.MatchEndTs)
                .ThenBy(item => item.MatchId.ToString("D"), StringComparer.Ordinal)
                .ToArray();
            var alpha = dimension == PlayerAIDimension.Survival
                ? _policy.SurvivalAlpha!.Value
                : _policy.NoiseAlpha!.Value;
            var score = 50m;
            for (var index = 0; index < ordered.Length; index++)
            {
                score = index == 0
                    ? ordered[index].Score
                    : Math.Clamp((1m - alpha) * score + alpha * ordered[index].Score, 0m, 100m);
            }

            ApplyDimension(profile, dimension, ordered, score);
        }
    }

    private void ApplyDimension(
        PlayerAIProfile profile,
        PlayerAIDimension dimension,
        IReadOnlyList<MatchScore> ordered,
        decimal score)
    {
        var active = ordered.Count > 0;
        var last = ordered.LastOrDefault();
        var now = _clock.GetUtcNow().UtcDateTime;
        if (dimension == PlayerAIDimension.Survival)
        {
            profile.SurvivalScore = active ? score : 50m;
            profile.SurvivalStatus = active ? ProfileDimensionStatus.Active : ProfileDimensionStatus.ColdStart;
            profile.SurvivalSampleCount = ordered.Count;
            profile.SurvivalLastMatchEndTs = last?.MatchEndTs;
            profile.SurvivalLastMatchId = last?.MatchId;
            profile.SurvivalLastUpdatedAtUtc = now;
        }
        else
        {
            profile.NoiseScore = active ? score : 50m;
            profile.NoiseStatus = active ? ProfileDimensionStatus.Active : ProfileDimensionStatus.ColdStart;
            profile.NoiseSampleCount = ordered.Count;
            profile.NoiseLastMatchEndTs = last?.MatchEndTs;
            profile.NoiseLastMatchId = last?.MatchId;
            profile.NoiseLastUpdatedAtUtc = now;
        }
    }

    private static bool EquivalentSource(MatchTelemetryAggregation planned, MatchTelemetryAggregation current) =>
        planned.Eligibility == current.Eligibility &&
        planned.SourceFingerprint == current.SourceFingerprint &&
        planned.MatchEndTs == current.MatchEndTs;

    private string ScoreFingerprint(
        MatchTelemetryAggregation aggregation,
        NormalizedMatchScore score,
        Guid lineageId)
    {
        var canonical = string.Join('\n', aggregation.UserId.ToString("D"), aggregation.MatchId.ToString("D"), lineageId.ToString("D"),
            score.Dimension.ToString(), score.Score.ToString(CultureInfo.InvariantCulture), aggregation.MatchEndTs?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            score.MatchScoreFormulaVersion, score.NormalizationConfigVersion,
            score.ProfileNoiseFilterVersion ?? "null", score.EvidenceFingerprint,
            aggregation.SourceSchemaVersion, _policy.AlphaConfigVersion);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private bool SameSemanticPayload(
        MatchScore receipt,
        MatchTelemetryAggregation aggregation,
        NormalizedMatchScore score) =>
        receipt.Dimension == score.Dimension &&
        receipt.Score == score.Score &&
        receipt.MatchEndTs == aggregation.MatchEndTs &&
        receipt.MatchScoreFormulaVersion == score.MatchScoreFormulaVersion &&
        receipt.NormalizationConfigVersion == score.NormalizationConfigVersion &&
        receipt.ProfileNoiseFilterVersion == score.ProfileNoiseFilterVersion &&
        receipt.AlphaConfigVersion == _policy.AlphaConfigVersion &&
        receipt.SourceTelemetrySchemaVersion == aggregation.SourceSchemaVersion &&
        receipt.SourceEvidenceFingerprint == score.EvidenceFingerprint;
}
