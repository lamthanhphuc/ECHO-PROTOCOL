using System.Text.Json;
using EchoProtocol.AI.Common.AED;
using EchoProtocol.Api.Common;
using EchoProtocol.Api.Configurations;
using EchoProtocol.Api.DTOs.Admin;
using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace EchoProtocol.Api.Tests;

public sealed partial class ScenarioServiceTests
{
    [Theory]
    [InlineData(true, false, 6.75, 1)]
    [InlineData(false, false, 7.5, 0)]
    [InlineData(true, true, 7.5, 1)]
    public async Task AdaptiveAdmin_PreviewAndResolveSharePolicyAndPinPublishedVersion(
        bool supportsRelativeRelief, bool configuredDetection, double expectedChase, int expectedTools)
    {
        await using var f = await Fixture.CreateAsync();
        await f.EnableApprovedReliefAsync();
        var configs = f.Db.ScenarioConfigs.ToArray();
        foreach (var config in configs)
        {
            config.MapId = "M2-MAP-1";
            config.ObjectiveSpawnSetId = "DEFAULT_OBJECTIVES";
            config.RouteModifier = "DEFAULT_ROUTE";
            config.DetectionFillRate = .5;
            config.DetectionDecayRate = 1;
            config.SearchDuration = 5;
        }
        var baseline = configs.Single(x => x.IsFixedFallback);
        var relief = configs.Single(x => !x.IsFixedFallback);
        var alternateFixed = new ScenarioConfigDefinition
        {
            ScenarioConfigId = "APPROVED_FIXED_ALTERNATE", ScenarioConfigVersion = "V1",
            SchemaVersion = baseline.SchemaVersion, PolicyVersion = baseline.PolicyVersion, ConfigSource = ScenarioConfigSource.Fixed,
            MapId = baseline.MapId, MonsterType = baseline.MonsterType, ObjectiveSpawnSetId = baseline.ObjectiveSpawnSetId,
            DetectionFillRate = .5, DetectionDecayRate = 1, ChaseSpeed = 9, SearchDuration = 5, RouteModifier = baseline.RouteModifier,
            EscapeDoorTimerSeconds = 45, FallbackConfigId = "APPROVED_FIXED_ALTERNATE", FallbackConfigVersion = "V1",
            ContentWhitelistVersion = baseline.ContentWhitelistVersion, UnityCompatibilityVersion = baseline.UnityCompatibilityVersion,
            IsActive = true, IsProductionApproved = true, Provenance = "TEST"
        };
        relief.FallbackConfigId = alternateFixed.ScenarioConfigId;
        relief.FallbackConfigVersion = alternateFixed.ScenarioConfigVersion;
        f.Db.ScenarioConfigs.Add(alternateFixed);
        foreach (var content in f.Db.ScenarioContentDefinitions)
            content.ContentId = content.ContentType switch
            {
                ScenarioContentType.Map => baseline.MapId,
                ScenarioContentType.ObjectiveSpawnSet => baseline.ObjectiveSpawnSetId,
                ScenarioContentType.RouteModifier => baseline.RouteModifier,
                _ => baseline.MonsterType
            };
        await f.Db.SaveChangesAsync();

        var registry = new ScenarioConfigRegistry(f.Db);
        var service = new AdaptiveConfigurationService(f.Db, registry, Options.Create(f.Policy),
            Options.Create(new AEDV02PilotSettings()), new FixedClock(Now));
        var normal = AEDV02Plan.Normal();
        var parameters = Enum.GetValues<AEDV02Key>().ToDictionary(key => key.ToString(),
            key => new AdaptiveParameter(normal.Get(key), AEDV02Plan.AllowedValues(key).ToArray()));
        if (configuredDetection)
            parameters[nameof(AEDV02Key.DetectionAcquireSeconds)] = new(1.5, [1, 1.25, 1.5]);
        var configToPublish = new AdaptiveConfiguration(true, 30, 70, 30, 70, 2, 2, true,
            new(alternateFixed.ScenarioConfigId, alternateFixed.ScenarioConfigVersion),
            new(relief.ScenarioConfigId, relief.ScenarioConfigVersion), null, parameters, AdaptiveConfigurationService.DefaultPilot);
        var compatibility = baseline.UnityCompatibilityVersion;

        var preview = await service.PreviewAsync(new(compatibility, configToPublish, [new(10, 20, 2, 2), new(10, 20, 2, 2)]), default);
        Assert.True(preview.IsSuccess, preview.Message);
        var shown = JsonSerializer.SerializeToElement(preview.Data);
        Assert.Equal(relief.ScenarioConfigId, shown.GetProperty("selected").GetProperty("Id").GetString());
        var fingerprint = shown.GetProperty("PreviewFingerprint").GetString()!;
        var partial = AdaptiveConfigurationService.Evaluate(configToPublish, [new(10, 20, 2, 2), new(null, null, 0, 0)]);
        Assert.Equal("INPUT_INVALID", partial.Reason);
        var partialRelief = AdaptiveConfigurationService.Evaluate(configToPublish with { RequireFullRoster = false },
            [new(10, 20, 2, 2), new(null, null, 0, 0)]);
        Assert.Equal(configToPublish.Relief, partialRelief.Choice);

        var lockedParameters = new Dictionary<string, AdaptiveParameter>(parameters) { ["ChaseSpeed"] = new(8, [7, 7.5, 8]) };
        var locked = await service.PublishAsync(f.HostId, new(compatibility, null, configToPublish with { Parameters = lockedParameters }, fingerprint), default);
        Assert.Equal("ADAPTIVE_CHASE_SPEED_BUILD_VERIFICATION_REQUIRED", locked.Message);

        var changed = await service.PublishAsync(f.HostId,
            new(compatibility, null, configToPublish with { Enabled = false }, fingerprint), default);
        Assert.Equal("ADAPTIVE_PREVIEW_REQUIRED", changed.Message);

        var published = await service.PublishAsync(f.HostId, new(compatibility, null, configToPublish, fingerprint), default);
        Assert.True(published.IsSuccess, published.Message);
        var version = (await AdaptiveConfigurationService.LatestAsync(f.Db, compatibility, default))!.Version;
        f.Db.MatchAuthorityBindings.Single(x => x.MatchId == f.MatchId).AdaptivePolicyConfigVersion = version;
        await f.Db.SaveChangesAsync();
        var stale = await service.PublishAsync(f.HostId, new(compatibility, null, configToPublish, fingerprint), default);
        Assert.Equal(ErrorCodes.Conflict, stale.ErrorCode);
        var disabledConfig = configToPublish with { Enabled = false };
        var disabledPreview = await service.PreviewAsync(new(compatibility, disabledConfig, [new(10, 20, 2, 2)]), default);
        Assert.True(disabledPreview.IsSuccess, disabledPreview.Message);
        var disabledFingerprint = JsonSerializer.SerializeToElement(disabledPreview.Data).GetProperty("PreviewFingerprint").GetString()!;
        var disabled = await service.PublishAsync(f.HostId, new(compatibility, version, disabledConfig, disabledFingerprint), default);
        Assert.True(disabled.IsSuccess, disabled.Message);

        var request = AdaptiveRequest();
        var resolved = await f.Service.ResolvePreMatchAsync(f.HostId, f.MatchId, request);
        Assert.True(resolved.IsSuccess, resolved.Message);
        Assert.Equal(relief.ScenarioConfigId, resolved.Data!.Config.ScenarioConfigId);
        Assert.Equal(AEDPreMatchPolicyV1.SurvivalRelief, resolved.Data.PreMatchRule);
        Assert.Equal(version, resolved.Data.PolicyConfigVersion);

        Assert.True((await AdaptiveConfigurationService.ForMatchAsync(f.Db, f.MatchId, default))!.Enabled);
        var replay = await f.Service.ResolvePreMatchAsync(f.HostId, f.MatchId, request);
        Assert.Equal(version, replay.Data!.PolicyConfigVersion);

        var (_, sourceRequest) = await V02WorldEvidence(f);
        var match = f.Db.MatchAuthorityBindings.Single(x => x.MatchId == f.MatchId);
        match.Status = MatchAuthorityStatus.Lobby;
        match.BalanceBuildVersion = compatibility;
        match.BalanceSource = "LOCAL";
        match.BalanceProtocolVersion = RemoteFixedBalanceContract.ProtocolVersion;
        match.MapId = "SciFi";
        match.Difficulty = "NORMAL";
        match.ResolutionMode = "ADAPTIVE";
        await f.Db.SaveChangesAsync();
        var evidence = new AcceptedEvidenceRepository([]);
        var clock = new FixedClock(Now);
        var decisions = new AEDV02DecisionService(f.Db, new AEDV02SourceVerifier(f.Db, evidence, clock), evidence, clock,
            Options.Create(new AEDV02PilotSettings { Enabled = true, ChaseSpeedVerifiedCompatibilityVersions = [compatibility] }));
        var reliefPlan = AdaptiveConfigurationService.Baseline(configToPublish)
            .With(AEDV02Key.ChaseSpeed, expectedChase < 7.5 ? AEDV02Plan.PreMatchReliefChaseTier : 7.5)
            .With(AEDV02Key.OptionalToolBudget, relief.SupportItemBudget);
        var approval = await decisions.SubmitAsync(f.HostId, f.MatchId,
            V02Request(f, resolved.Data, sourceRequest.SceneManifestFingerprint, reliefPlan, 0, null, 0) with { SupportsRelativePreMatchRelief = supportsRelativeRelief });
        Assert.True(approval.IsSuccess, approval.Message);
        Assert.Equal("APPROVED", approval.Data!.ApprovalStatus);
        var row = f.Db.AEDV02Decisions.Single();
        Assert.Equal(7.5, row.PreviousChaseSpeed);
        var values = JsonSerializer.Deserialize<double[]>(row.StalkerValuesJson!)!;
        Assert.Equal(expectedChase, values[(int)AEDV02Key.ChaseSpeed]);
        Assert.Equal(expectedTools, values[(int)AEDV02Key.OptionalToolBudget]);

        var fallbackRequest = AdaptiveRequest();
        fallbackRequest.FallbackFromDecisionId = request.DecisionId.ToString("D");
        fallbackRequest.DecisionId = AEDV02Plan.FixedFallbackDecisionId(request.DecisionId);
        var fallback = await f.Service.ResolvePreMatchAsync(f.HostId, f.MatchId, fallbackRequest);
        Assert.True(fallback.IsSuccess, fallback.Message);
        Assert.True(fallback.Data!.UsedFixedFallback);
        Assert.Equal("HOLD_PREMATCH_V02_UNAVAILABLE", fallback.Data.FallbackReasonCode);
        Assert.Equal(alternateFixed.ScenarioConfigId, fallback.Data.Config.ScenarioConfigId);
        Assert.Equal(version, fallback.Data.PolicyConfigVersion);
        Assert.Equal(fallbackRequest.DecisionId, (await f.Service.ResolvePreMatchAsync(f.HostId, f.MatchId, request)).Data!.DecisionId);
    }

    [Theory]
    [InlineData(9, 10, true)]
    [InlineData(7.5, 8, true)]
    [InlineData(9, 11, false)]
    public void AdaptiveAdmin_ChaseIncreaseMapsOnlyTheApprovedNextTier(double before, double after, bool allowed)
    {
        Assert.Equal(allowed, AEDV02Plan.TryMapPreMatchChaseIncrease(before, after, out var speed));
        if (allowed) Assert.Equal(8d, AEDV02Plan.Normal().With(AEDV02Key.ChaseSpeed, speed).Get(AEDV02Key.ChaseSpeed));
    }
}
