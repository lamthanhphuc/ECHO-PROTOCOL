using System.Text.Json;
using EchoProtocol.AI.Common.AED;
using EchoProtocol.Api.Common;
using EchoProtocol.Api.Configurations;
using EchoProtocol.Api.Data;
using EchoProtocol.Api.DTOs.Admin;
using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EchoProtocol.Api.Services;

public sealed class AdaptiveConfigurationService(AppDbContext db, IScenarioConfigRegistry registry,
    IOptions<AEDPreMatchPolicySettings> defaults, IOptions<AEDV02PilotSettings> pilotSettings, TimeProvider clock)
{
    public const string VersionPrefix = "AED_ADMIN_";
    public static readonly AdaptivePilotPolicy DefaultPilot = new(
        AEDV02PilotPolicy.ObservationSeconds, AEDV02PilotPolicy.WindowSeconds, AEDV02PilotPolicy.EvidenceMaxAgeSeconds,
        AEDV02PressureNormalizer.DefaultWeights.ToArray(), .65, .25, AEDV02PilotPolicy.DownRecoverySeconds,
        AEDV02PilotOpportunities.OrdinarySupportQuota, AEDV02PilotOpportunities.EmergencySupportQuota,
        AEDV02PilotOpportunities.SupportSeparationSeconds);

    public static async Task<AdaptiveConfigurationVersion?> LatestAsync(AppDbContext db, string compatibility, CancellationToken ct) =>
        await db.AdaptiveConfigurationVersions.AsNoTracking().Where(x => x.Compatibility == compatibility)
            .OrderByDescending(x => x.Revision).FirstOrDefaultAsync(ct);

    public static AdaptiveConfiguration Read(AdaptiveConfigurationVersion version) =>
        JsonSerializer.Deserialize<AdaptiveConfiguration>(version.ConfigurationJson)!;

    public static async Task<AdaptiveConfiguration?> ForVersionAsync(AppDbContext db, string? version, CancellationToken ct)
    {
        if (version?.StartsWith(VersionPrefix, StringComparison.Ordinal) != true) return null;
        var row = await db.AdaptiveConfigurationVersions.AsNoTracking().SingleAsync(x => x.Version == version, ct);
        return Read(row);
    }

    public static async Task<AdaptiveConfiguration?> ForMatchAsync(AppDbContext db, Guid matchId, CancellationToken ct) =>
        await ForVersionAsync(db, await db.ScenarioDecisions.AsNoTracking()
            .Where(x => x.MatchId == matchId && x.IsCurrent).Select(x => x.PolicyConfigVersion).SingleOrDefaultAsync(ct), ct);

    public static AEDV02Plan Baseline(AdaptiveConfiguration? config) => config is null ? AEDV02Plan.Normal()
        : new(Enum.GetValues<AEDV02Key>().Select(key => config.Parameters[key.ToString()].Baseline).ToArray());

    public async Task<ServiceResult<object>> GetAsync(string? compatibility, CancellationToken ct)
    {
        var configs = await db.ScenarioConfigs.AsNoTracking()
            .Where(x => x.IsActive && x.IsProductionApproved)
            .OrderBy(x => x.UnityCompatibilityVersion).ThenBy(x => x.ScenarioConfigId).ThenBy(x => x.ScenarioConfigVersion)
            .ToArrayAsync(ct);
        var options = configs.Where(x => x.ConfigSource == ScenarioConfigSource.Adaptive || IsApprovedFixed(x))
            .Select(x => new { x.UnityCompatibilityVersion, Id = x.ScenarioConfigId, Version = x.ScenarioConfigVersion,
                Source = x.ConfigSource, x.IsFixedFallback, x.FallbackConfigId, x.FallbackConfigVersion, x.SupportItemBudget, x.ChaseSpeed })
            .ToArray();
        var compatibilities = options.Select(x => x.UnityCompatibilityVersion).Distinct().ToArray();
        if (compatibilities.Contains("0.1.0") && !compatibilities.Contains("0.1.1"))
            compatibilities = [.. compatibilities, "0.1.1"];
        compatibility ??= compatibilities.LastOrDefault();
        var registryCompatibility = compatibility is null ? null : ScenarioConfigRegistry.RegistryCompatibility(compatibility);
        bool chaseVerified = compatibility is not null && ChaseSpeedVerified(compatibility);
        var recentVersions = compatibility is null ? [] : await db.AdaptiveConfigurationVersions.AsNoTracking()
            .Where(x => x.Compatibility == compatibility).OrderByDescending(x => x.Revision).Take(10).ToArrayAsync(ct);
        var row = recentVersions.FirstOrDefault();
        var normal = AEDV02Plan.Normal();
        var catalog = Enum.GetValues<AEDV02Key>().ToDictionary(key => key.ToString(), key => new
        {
            Baseline = normal.Get(key), Tiers = AEDV02Plan.AllowedValues(key),
            BaselineLocked = key is AEDV02Key.Zone3AdaptiveMinionCap or AEDV02Key.ResourceGrade or AEDV02Key.OptionalToolBudget
                || key == AEDV02Key.ChaseSpeed && !chaseVerified
        });
        var s = defaults.Value;
        var fixedChoices = options.Where(x => x.UnityCompatibilityVersion == registryCompatibility && x.Source == ScenarioConfigSource.Fixed).ToArray();
        var defaultFixed = fixedChoices.FirstOrDefault(x => x.IsFixedFallback) ?? (fixedChoices.Length == 1 ? fixedChoices[0] : null);
        var configuration = row is not null ? Read(row) : new AdaptiveConfiguration(s.Enabled, s.SurvivalLowThreshold, s.SurvivalHighThreshold,
            s.NoiseLowThreshold, s.NoiseHighThreshold, s.MinimumSurvivalSampleCountPerObservedPlayer,
            s.MinimumNoiseSampleCountPerObservedPlayer, s.RequireFullRosterObservedCoverage,
            defaultFixed is null ? null : new(defaultFixed.Id, defaultFixed.Version), null, null,
            catalog.ToDictionary(x => x.Key, x => new AdaptiveParameter(x.Value.Baseline, x.Value.Tiers.ToArray())), DefaultPilot);
        return ServiceResult<object>.Success(new
        {
            compatibility, compatibilities, Version = row?.Version, row?.PublishedAtUtc, configuration, catalog,
            History = recentVersions.Select(version => new { version.Version, version.Revision, version.PublishedAtUtc }),
            options = options.Where(x => x.UnityCompatibilityVersion == registryCompatibility),
            ChaseSpeedAdminVerified = chaseVerified, PilotEnabled = pilotSettings.Value.Enabled,
            safety = new
            {
                AEDV02PilotPolicy.TierHysteresis, AEDV02PilotPolicy.ProtectionGraceSeconds, AEDV02PilotPolicy.SeekRecoverySeconds,
                AEDV02PilotPolicy.AmbushCooldownSeconds, AEDV02PilotPolicy.SharedSpecialSeparationSeconds
            }
        });
    }

    public async Task<ServiceResult<object>> PublishAsync(Guid adminId, PublishAdaptiveConfiguration request, CancellationToken ct)
    {
        var validation = await ValidateAsync(request.Compatibility, request.Configuration, ct);
        if (!validation.IsSuccess) return Invalid(validation.Message);
        var current = await LatestAsync(db, request.Compatibility, ct);
        if (request.ExpectedVersion != current?.Version)
            return ServiceResult<object>.Failure("ADAPTIVE_CONFIGURATION_STALE", ErrorCodes.Conflict);
        if (request.PreviewFingerprint != PreviewFingerprint(request.Compatibility, request.Configuration, current?.Version))
            return Invalid("ADAPTIVE_PREVIEW_REQUIRED");
        var row = new AdaptiveConfigurationVersion
        {
            Version = VersionPrefix + Guid.NewGuid().ToString("N"), Compatibility = request.Compatibility,
            Revision = (current?.Revision ?? 0) + 1, PublishedByUserId = adminId,
            PublishedAtUtc = clock.GetUtcNow().UtcDateTime, ConfigurationJson = JsonSerializer.Serialize(request.Configuration)
        };
        db.AdaptiveConfigurationVersions.Add(row);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation })
        {
            return ServiceResult<object>.Failure("ADAPTIVE_CONFIGURATION_STALE", ErrorCodes.Conflict);
        }
        return ServiceResult<object>.Success(new { row.Version, row.PublishedAtUtc });
    }

    public async Task<ServiceResult<object>> PreviewAsync(PreviewAdaptiveConfiguration request, CancellationToken ct)
    {
        if (request.Players is not { Length: >= 1 and <= 4 } || request.Players.Any(x => x is null ||
            x.Survival is < 0 or > 100 || x.Noise is < 0 or > 100 || x.SurvivalSamples < 0 || x.NoiseSamples < 0))
            return Invalid("ADAPTIVE_SAMPLE_INVALID");
        var validation = await ValidateAsync(request.Compatibility, request.Configuration, ct);
        if (!validation.IsSuccess) return Invalid(validation.Message);
        var fixedConfig = validation.Data!;
        var (rule, choice, reason) = Evaluate(request.Configuration, request.Players);
        var selected = choice ?? new(fixedConfig.ScenarioConfigId, fixedConfig.ScenarioConfigVersion);
        double? reliefChaseFactor = choice is not null && rule == AEDPreMatchPolicyV1.SurvivalRelief
            ? Baseline(request.Configuration).PreMatchReliefChase((key, tier) =>
                (key != AEDV02Key.ChaseSpeed || ChaseSpeedVerified(request.Compatibility)) &&
                request.Configuration.Parameters[key.ToString()].Tiers.Contains(tier)) == AEDV02Plan.PreMatchReliefChaseTier ? .9 : 1
            : null;
        var current = await LatestAsync(db, request.Compatibility, ct);
        return ServiceResult<object>.Success(new
        {
            rule, selected, UsedFixedFallback = choice is null, reason, reliefChaseFactor,
            PreviewFingerprint = PreviewFingerprint(request.Compatibility, request.Configuration, current?.Version)
        });
    }

    private static string PreviewFingerprint(string compatibility, AdaptiveConfiguration configuration, string? version) =>
        ScenarioFingerprint.Hash(compatibility, version, JsonSerializer.Serialize(configuration));

    public static (string? Rule, AdaptiveConfigRef? Choice, string? Reason) Evaluate(
        AdaptiveConfiguration config, IReadOnlyCollection<AdaptiveSample> players)
    {
        if (!config.Enabled) return (null, null, "POLICY_DISABLED");
        var qualified = players.Where(x => x.Survival.HasValue && x.Noise.HasValue &&
            x.SurvivalSamples >= config.MinimumSurvivalSamples && x.NoiseSamples >= config.MinimumNoiseSamples).ToArray();
        var full = qualified.Length == players.Count;
        if (qualified.Length == 0 || config.RequireFullRoster && !full) return (null, null, "INPUT_INVALID");
        var rule = AEDPreMatchPolicyV1.SelectRule(qualified.Average(x => x.Survival!.Value),
            qualified.Average(x => x.Noise!.Value), config.SurvivalLow, config.SurvivalHigh, config.NoiseLow, config.NoiseHigh);
        if (rule == AEDPreMatchPolicyV1.Hold) return (rule, null, "HOLD_RULE");
        if (rule == AEDPreMatchPolicyV1.Increase && !full) return (rule, null, "HOLD_INCREASE_REQUIRES_FULL_ROSTER");
        var selected = rule == AEDPreMatchPolicyV1.Increase ? config.Increase : config.Relief;
        return (rule, selected, selected is null ? "NO_APPROVED_NEXT_VALUE" : null);
    }

    public static async Task<ServiceResult<ScenarioConfigDefinition>> LoadAsync(IScenarioConfigRegistry registry,
        AdaptiveConfigRef? reference, string compatibility, CancellationToken ct) =>
        reference is null || string.IsNullOrWhiteSpace(reference.Id) || reference.Id.Length > 128 ||
        string.IsNullOrWhiteSpace(reference.Version) || reference.Version.Length > 80
            ? ServiceResult<ScenarioConfigDefinition>.Failure("ADAPTIVE_CONFIG_REFERENCE_REQUIRED", ErrorCodes.ValidationError)
            : await registry.GetActiveAsync(reference.Id, reference.Version, compatibility, ct);

    private async Task<ServiceResult<ScenarioConfigDefinition>> ValidateAsync(string compatibility, AdaptiveConfiguration c, CancellationToken ct)
    {
        ServiceResult<ScenarioConfigDefinition> Fail(string reason) =>
            ServiceResult<ScenarioConfigDefinition>.Failure(reason, ErrorCodes.ValidationError);
        if (c is null || !Thresholds(c.SurvivalLow, c.SurvivalHigh) || !Thresholds(c.NoiseLow, c.NoiseHigh) ||
            c.MinimumSurvivalSamples is < 1 or > 1000 || c.MinimumNoiseSamples is < 1 or > 1000)
            return Fail("ADAPTIVE_PREMATCH_THRESHOLDS_INVALID");
        if (!ValidPilot(c.Pilot)) return Fail("ADAPTIVE_PILOT_POLICY_INVALID");
        var keys = Enum.GetValues<AEDV02Key>();
        if (c.Parameters is null || c.Parameters.Count != keys.Length) return Fail("ADAPTIVE_CATALOG_INVALID");
        var normal = AEDV02Plan.Normal();
        foreach (var key in keys)
        {
            var approved = AEDV02Plan.AllowedValues(key).ToArray();
            if (!c.Parameters.TryGetValue(key.ToString(), out var p) || p?.Tiers is not { Length: > 0 } ||
                !p.Tiers.Contains(p.Baseline) || !p.Tiers.Contains(normal.Get(key)) ||
                !approved.Where(p.Tiers.Contains).SequenceEqual(p.Tiers) ||
                Array.IndexOf(approved, p.Tiers[^1]) - Array.IndexOf(approved, p.Tiers[0]) + 1 != p.Tiers.Length)
                return Fail($"ADAPTIVE_TIERS_INVALID_{key}");
            if (key == AEDV02Key.ChaseSpeed && !ChaseSpeedVerified(compatibility) &&
                (p.Baseline != normal.Get(key) || !p.Tiers.SequenceEqual(approved)))
                return Fail("ADAPTIVE_CHASE_SPEED_BUILD_VERIFICATION_REQUIRED");
            if (key is AEDV02Key.Zone3AdaptiveMinionCap or AEDV02Key.ResourceGrade or AEDV02Key.OptionalToolBudget && p.Baseline != normal.Get(key))
                return Fail($"ADAPTIVE_BASELINE_LOCKED_{key}");
        }
        if (!Baseline(c).TryValidateChange(normal, AEDV02DecisionPoint.PRE_MATCH, false, 0, out _, out var reason))
            return Fail(reason);
        if (c.Parameters[nameof(AEDV02Key.CabinetAmbushEnabled)].Baseline == 1 && !pilotSettings.Value.CabinetAmbushEnabled)
            return Fail("HOLD_CABINET_AMBUSH_DISABLED");
        var fixedResult = await LoadAsync(registry, c.Fixed, compatibility, ct);
        if (fixedResult.Data is not { } fixedConfig || !IsApprovedFixed(fixedConfig))
            return Fail("ADAPTIVE_APPROVED_FIXED_REQUIRED");
        foreach (var (reference, rule) in new[] { (c.Relief, AEDPreMatchPolicyV1.SurvivalRelief), (c.Increase, AEDPreMatchPolicyV1.Increase) })
        {
            if (reference is null) continue;
            var candidate = (await LoadAsync(registry, reference, compatibility, ct)).Data;
            if (candidate is null || !IsApprovedCandidate(rule, fixedConfig, candidate))
                return Fail("ADAPTIVE_APPROVED_CANDIDATE_REQUIRED_" + rule);
        }
        if (new[] { c.Relief, c.Increase }.Any(x => x is not null) &&
            !c.Parameters[nameof(AEDV02Key.OptionalToolBudget)].Tiers.Contains(fixedConfig.SupportItemBudget))
            return Fail("ADAPTIVE_FIXED_SUPPLY_OUTSIDE_TIERS");
        if (c.Relief is not null && !c.Parameters[nameof(AEDV02Key.OptionalToolBudget)].Tiers.Contains(fixedConfig.SupportItemBudget + 1))
            return Fail("ADAPTIVE_RELIEF_SUPPLY_OUTSIDE_TIERS");
        if (c.Increase is not null && (!c.Parameters[nameof(AEDV02Key.ChaseSpeed)].Tiers.Contains(8d) ||
            !Baseline(c).With(AEDV02Key.ChaseSpeed, 8d).TryValidateChange(normal, AEDV02DecisionPoint.PRE_MATCH, false, 0, out _, out _)))
            return Fail("ADAPTIVE_INCREASE_EXCEEDS_PREMATCH_BUDGET");
        return fixedResult;
    }

    public static bool IsApprovedFixed(ScenarioConfigDefinition config) =>
        config.ConfigSource == ScenarioConfigSource.Fixed && config.IsActive && config.IsProductionApproved &&
        config.FallbackConfigId == config.ScenarioConfigId && config.FallbackConfigVersion == config.ScenarioConfigVersion &&
        config.MapId == "M2-MAP-1" && config.MonsterType == "STALKER" && config.ObjectiveSpawnSetId == "DEFAULT_OBJECTIVES" &&
        config.RouteModifier == "DEFAULT_ROUTE" && config.ContentWhitelistVersion == "M2-WHITELIST-1" &&
        config.ScenarioConfigVersion.Length <= 64 && config.FallbackConfigId.Length <= 64 &&
        config.DetectionFillRate == .5 && config.DetectionDecayRate == 1 && config.SearchDuration == 5 &&
        config.EscapeDoorTimerSeconds == 45 && config.ChaseSpeed is 7 or 7.5 or 8 or 9 && config.SupportItemBudget is >= 0 and <= 2;

    public static bool IsApprovedCandidate(string rule, ScenarioConfigDefinition baseline, ScenarioConfigDefinition candidate) =>
        candidate.ScenarioConfigVersion.Length <= 64 &&
        (rule != AEDPreMatchPolicyV1.Increase || AEDV02Plan.TryMapPreMatchChaseIncrease(baseline.ChaseSpeed, candidate.ChaseSpeed, out _)) &&
        AEDPreMatchCandidateV1.TrySelect(rule, baseline, [candidate],
            rule == AEDPreMatchPolicyV1.Increase ? [baseline.ChaseSpeed, candidate.ChaseSpeed] : [0d, 1d, 2d],
            out _, out _, requireDefaultFallback: false);

    private bool ChaseSpeedVerified(string compatibility) =>
        pilotSettings.Value.ChaseSpeedVerifiedCompatibilityVersions.Contains(compatibility, StringComparer.Ordinal);

    private static bool Thresholds(decimal low, decimal high) => low > 0 && low < high && high < 100;

    private static bool ValidPilot(AdaptivePilotPolicy? p) => p is not null &&
        p.ObservationSeconds >= 30 && p.WindowSeconds >= p.ObservationSeconds + 5 && p.WindowSeconds <= 120 &&
        p.EvidenceMaxAgeSeconds is >= 5 and <= 10 && p.ReliefThreshold is >= .45 and <= .65 &&
        p.IncreasePressureCeiling is >= .10 and <= .25 && p.DownRecoverySeconds is >= 20 and <= 60 &&
        p.OrdinarySupportQuota >= 0 && p.OrdinarySupportQuota <= DefaultPilot.OrdinarySupportQuota &&
        p.EmergencySupportQuota >= 0 && p.EmergencySupportQuota <= DefaultPilot.EmergencySupportQuota &&
        p.SupportCooldownSeconds >= DefaultPilot.SupportCooldownSeconds && p.SupportCooldownSeconds <= 600 &&
        p.ReliefCooldownSeconds is >= 90 and <= 600 && p.IncreaseCooldownSeconds >= Math.Max(180, p.ReliefCooldownSeconds) &&
        p.IncreaseCooldownSeconds <= 900 &&
        p.TierDwellSeconds is >= 12 and <= 30 && p.ProtectionSeconds is >= 10 and <= 15 &&
        p.ProtectionCooldownSeconds is >= 120 and <= 300 && p.ProtectionQuota is >= 0 and <= 2 &&
        p.PressureWeights is { Length: 5 } && p.PressureWeights.All(x => x is >= .05 and <= .50) &&
        Math.Abs(p.PressureWeights.Sum() - 1) < .000001;

    private static ServiceResult<object> Invalid(string reason) => ServiceResult<object>.Failure(reason, ErrorCodes.ValidationError);
}
