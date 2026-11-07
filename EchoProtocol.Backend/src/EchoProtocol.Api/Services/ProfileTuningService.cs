using System.Text.Json;
using EchoProtocol.AI.Common.AED;
using EchoProtocol.Api.Common;
using EchoProtocol.Api.Configurations;
using EchoProtocol.Api.Data;
using EchoProtocol.Api.DTOs.Admin;
using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace EchoProtocol.Api.Services;

public sealed class ProfileTuningService(AppDbContext db, IOptions<PlayerAIProfileSettings> player,
    IOptions<TeamProfileSettings> team, TimeProvider clock)
{
    private const string Prefix = "AI_PROFILE_ADMIN_";
    private static readonly string[] NoiseTypes =
    [
        "SPRINT", "INTERACTION", "CORE_CARRY", "CORE_DROP", "NOISE_MAKER", "FIELD_SCANNER", "CROUCH", "WALK",
        "DOOR", "CORE_INSERT", "MACHINE_REPAIR", "MINION_ALERT", "MACHINE_OVERLOAD", "TERMINAL_DOWNLOAD",
        "VEHICLE_PUSH", "CHARGE_TRANSFER", "POWER_SURGE"
    ];

    public static Task<ProfileTuningVersion?> LatestAsync(AppDbContext db, CancellationToken ct) =>
        db.ProfileTuningVersions.AsNoTracking().OrderByDescending(x => x.Revision).FirstOrDefaultAsync(ct);

    public static async Task<ProfileTuningVersion?> ReadAsync(AppDbContext db, string? version, CancellationToken ct) =>
        version?.StartsWith(Prefix, StringComparison.Ordinal) == true
            ? await db.ProfileTuningVersions.AsNoTracking().SingleAsync(x => x.Version == version, ct)
            : null;

    public static ProfileTuning Read(ProfileTuningVersion version) => JsonSerializer.Deserialize<ProfileTuning>(version.SettingsJson)!;

    private ProfileTuning Defaults => new(player.Value.SurvivalAlpha ?? .5m, player.Value.NoiseAlpha ?? .5m,
        player.Value.ObjectiveAlpha ?? .3m, player.Value.ToolUsageAlpha ?? .3m, player.Value.ProfileNoiseCountMin ?? 0,
        player.Value.ProfileNoiseCountMax ?? 200, player.Value.ToolUsageCountMax ?? 5,
        player.Value.NoisePenaltyTypes.Length == 0 ? ["SPRINT"] : player.Value.NoisePenaltyTypes,
        team.Value.ObjectiveTimeMin ?? 120, team.Value.ObjectiveTimeMax ?? 900,
        AEDV02PilotOpportunities.HistoryMatches, AEDV02PilotOpportunities.HistoryResolved,
        AEDV02PilotOpportunities.HistoryMaxAgeDays, AEDV02PilotOpportunities.HistoryHalfLifeDays);

    public async Task<ServiceResult<object>> GetAsync(CancellationToken ct)
    {
        var current = await LatestAsync(db, ct);
        return ServiceResult<object>.Success(new
        {
            Version = current?.Version, PublishedAtUtc = current?.PublishedAtUtc, Settings = current is null ? Defaults : Read(current),
            AllowedNoiseTypes = NoiseTypes
        });
    }

    public async Task<ServiceResult<object>> PublishAsync(Guid adminId, PublishProfileTuning request, CancellationToken ct)
    {
        if (!Valid(request.Settings)) return Invalid("AI_PROFILE_TUNING_INVALID");
        var current = await LatestAsync(db, ct);
        if (current?.Version != request.ExpectedVersion) return Conflict();
        if (request.PreviewFingerprint != PreviewFingerprint(request.Settings, current?.Version))
            return Invalid("AI_PROFILE_PREVIEW_REQUIRED");
        var row = new ProfileTuningVersion
        {
            Version = Prefix + Guid.NewGuid().ToString("N"), Revision = (current?.Revision ?? 0) + 1,
            SettingsJson = JsonSerializer.Serialize(request.Settings), PublishedByUserId = adminId,
            PublishedAtUtc = clock.GetUtcNow().UtcDateTime
        };
        db.ProfileTuningVersions.Add(row);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        { return Conflict(); }
        return ServiceResult<object>.Success(new { row.Version, row.PublishedAtUtc });
    }

    public async Task<ServiceResult<object>> PreviewAsync(PreviewProfileTuning request, CancellationToken ct)
    {
        if (!Valid(request.Settings) || request.NoiseCounts is null ||
            request.NoiseCounts.Any(x => !NoiseTypes.Contains(x.Key) || x.Value is < 0 or > 100000) ||
            request.ToolsUsed is < 0 or > 100000 || request.PreviousScore is < 0 or > 100 ||
            request.ObjectiveSeconds is < 0 or > 7200)
            return Invalid("AI_PROFILE_SAMPLE_INVALID");
        var current = await LatestAsync(db, ct);
        var before = current is null ? Defaults : Read(current);
        return ServiceResult<object>.Success(new
        {
            Before = Project(before, request), After = Project(request.Settings, request),
            PreviewFingerprint = PreviewFingerprint(request.Settings, current?.Version)
        });
    }

    public async Task<ServiceResult<object>> RequeueAsync(Guid matchId, CancellationToken ct)
    {
        if (!await db.MatchResults.AnyAsync(x => x.MatchId == matchId && x.Match.Status == MatchAuthorityStatus.Ended &&
            x.Match.MatchDataPolicy != RemoteFixedBalanceContract.TestOnly, ct))
            return ServiceResult<object>.Failure("Không tìm thấy trận đủ điều kiện xử lý hồ sơ.", ErrorCodes.NotFound);
        var changed = await db.MatchProfileProcessingJobs.Where(x => x.MatchId == matchId &&
            (x.Status == "FAILED" || x.Status == "COMPLETED") && x.LeaseExpiresAtUtc == null)
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.Status, "PENDING").SetProperty(x => x.Attempts, 0)
                .SetProperty(x => x.LastError, "").SetProperty(x => x.NextAttemptAtUtc, clock.GetUtcNow().UtcDateTime), ct);
        return changed == 1 ? ServiceResult<object>.Success(new { matchId, Status = "PENDING" })
            : ServiceResult<object>.Failure("Tác vụ chưa thể đưa lại vào hàng đợi.", ErrorCodes.Conflict);
    }

    public ConfiguredPlayerAIProfilePolicy PlayerPolicy(ProfileTuningVersion version) => PlayerPolicy(Read(version), version.Version);

    private ConfiguredPlayerAIProfilePolicy PlayerPolicy(ProfileTuning value, string version) =>
        new(Options.Create(new PlayerAIProfileSettings
        {
            ProfileFormulaVersion = player.Value.ProfileFormulaVersion, MatchScoreFormulaVersion = player.Value.MatchScoreFormulaVersion,
            AlphaConfigVersion = version, NormalizationConfigVersion = version, ProfileNoiseFilterVersion = version,
            SurvivalAlpha = value.SurvivalAlpha, NoiseAlpha = value.NoiseAlpha, ObjectiveAlpha = value.ObjectiveAlpha,
            ToolUsageAlpha = value.ToolUsageAlpha, ToolUsageCountMax = value.ToolUsageMax,
            ProfileNoiseCountMin = value.NoiseMin, ProfileNoiseCountMax = value.NoiseMax, NoisePenaltyTypes = value.NoiseTypes
        }));

    public ConfiguredTeamProfilePolicy TeamPolicy(ProfileTuningVersion version)
    {
        var value = Read(version);
        return new(Options.Create(new TeamProfileSettings
        {
            ProfileFormulaVersion = team.Value.ProfileFormulaVersion, TeamPerformanceFormulaVersion = team.Value.TeamPerformanceFormulaVersion,
            PhaseRegistryVersion = team.Value.PhaseRegistryVersion, ObjectiveBearingPhases = team.Value.ObjectiveBearingPhases,
            AllowedOverlapPairs = team.Value.AllowedOverlapPairs, NormalizationConfigVersion = version.Version,
            ObjectiveTimeMin = value.ObjectiveTimeMin, ObjectiveTimeMax = value.ObjectiveTimeMax,
            ObjectiveWeight = team.Value.ObjectiveWeight, SurvivalWeight = team.Value.SurvivalWeight,
            TeamworkWeight = team.Value.TeamworkWeight, ResourceWeight = team.Value.ResourceWeight
        }));
    }

    private object Project(ProfileTuning value, PreviewProfileTuning sample)
    {
        var policy = PlayerPolicy(value, "PREVIEW");
        var count = value.NoiseTypes.Sum(type => sample.NoiseCounts.GetValueOrDefault(type));
        var inputs = new (PlayerAIDimension Dimension, decimal Raw)[]
        {
            (PlayerAIDimension.Survival, sample.Survived ? 100 : 0), (PlayerAIDimension.Noise, count),
            (PlayerAIDimension.Objective, sample.CompletedObjective ? 100 : 0), (PlayerAIDimension.ToolUsage, sample.ToolsUsed)
        };
        var scores = inputs.Select(x =>
        {
            var available = MatchScoreNormalizer.TryNormalize(new(x.Dimension, x.Raw, MetricAvailability.Available, "", ""), policy, out var score, out _);
            var matchScore = available ? score!.Score : (decimal?)null;
            var alpha = available ? policy.GetAlpha(x.Dimension) : 0;
            return new { x.Dimension, MatchScore = matchScore,
                UpdatedScore = sample.PreviousScore is decimal previous ? (1 - alpha) * previous + alpha * matchScore : matchScore };
        });
        return new
        {
            Scores = scores, CountedNoise = count,
            ObjectiveSpeed = value.ObjectiveTimeMax <= value.ObjectiveTimeMin ? (decimal?)null :
                100 * (1 - Math.Clamp((sample.ObjectiveSeconds - value.ObjectiveTimeMin) /
                    (value.ObjectiveTimeMax - value.ObjectiveTimeMin), 0, 1)),
            value.HistoryMatches, value.HistoryOpportunities, value.HistoryMaxAgeDays, value.HistoryHalfLifeDays,
            ThirtyDayHistoryWeight = value.HistoryMaxAgeDays < 30 ? 0 : Math.Pow(.5, 30 / value.HistoryHalfLifeDays)
        };
    }

    private static bool Valid(ProfileTuning? value) => value is not null &&
        new[] { value.SurvivalAlpha, value.NoiseAlpha, value.ObjectiveAlpha, value.ToolUsageAlpha }.All(x => x is > 0 and <= 1) &&
        value.NoiseMin >= 0 && value.NoiseMax > value.NoiseMin && value.NoiseMax <= 100000 &&
        value.ToolUsageMax is > 0 and <= 100000 && value.NoiseTypes is { Length: > 0 } &&
        value.NoiseTypes.All(NoiseTypes.Contains) && value.NoiseTypes.Distinct(StringComparer.Ordinal).Count() == value.NoiseTypes.Length &&
        value.ObjectiveTimeMin >= 0 && value.ObjectiveTimeMax > value.ObjectiveTimeMin && value.ObjectiveTimeMax <= 7200 &&
        value.HistoryMatches is >= 3 and <= 30 && value.HistoryOpportunities is >= 8 and <= 200 &&
        value.HistoryMaxAgeDays is >= 7 and <= 30 && value.HistoryHalfLifeDays >= 1 && value.HistoryHalfLifeDays <= value.HistoryMaxAgeDays;

    private static ServiceResult<object> Invalid(string message) => ServiceResult<object>.Failure(message, ErrorCodes.ValidationError);
    private static ServiceResult<object> Conflict() => ServiceResult<object>.Failure("AI_PROFILE_VERSION_STALE", ErrorCodes.Conflict);
    private static string PreviewFingerprint(ProfileTuning settings, string? version) =>
        ScenarioFingerprint.Hash(version, JsonSerializer.Serialize(settings));
}
