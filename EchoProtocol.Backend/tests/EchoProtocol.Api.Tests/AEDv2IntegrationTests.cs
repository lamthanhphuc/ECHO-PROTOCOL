using EchoProtocol.Api.Common;
using EchoProtocol.Api.DTOs.Scenarios;
using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EchoProtocol.Api.Tests;

public sealed partial class ScenarioServiceTests
{
    [Fact, Trait("Category", "AEDv2")]
    public void AEDv2_BackendFingerprintMatchesUnityCatalog()
    {
        var normal = ScenarioAdaptivePlanV2Service.NormalPlanValues();
        Assert.Equal("d725f010f4f456aaaecc8a74def8cecd",
            ScenarioAdaptivePlanV2Service.ComputePlanFingerprint(normal));
        normal[0] = 2;
        Assert.Equal("644d57e5f4ec57e7b4c265d21dc338df",
            ScenarioAdaptivePlanV2Service.ComputePlanFingerprint(normal));
        normal[0] = 0;
        normal[2] = 1.5;
        Assert.Equal("b492dc7e6ea456b187bdc80c7967fd1b",
            ScenarioAdaptivePlanV2Service.ComputePlanFingerprint(normal));
    }

    [Fact, Trait("Category", "AEDv2")]
    public async Task AEDv2_HostCommitsOneBoundedKeyAndReplaysWithoutExtraRevision()
    {
        await using var f = await ValidAEDv2Fixture();
        var scenario = (await f.Service.ResolvePreMatchAsync(f.HostId, f.MatchId, AdaptiveRequest())).Data!;
        var service = V2(f);
        var request = V2Request(scenario, 0, "PRE_MATCH", "SupportBonus", 0, 2, "RELIEVE", "COMMITTED");
        var first = await service.SubmitAsync(f.HostId, f.MatchId, request);
        var stored = await f.Db.ScenarioAdaptivePlansV2.SingleAsync();
        stored.PlanValuesJson = stored.PlanValuesJson.Replace(",", ", ");
        await f.Db.SaveChangesAsync();
        var replay = await service.SubmitAsync(f.HostId, f.MatchId, request);
        Assert.True(first.IsSuccess, $"{first.ErrorCode}: {first.Message}");
        Assert.Equal("COMMITTED", first.Data!.CommitStatus);
        Assert.True(replay.Data!.IsReplay);
        Assert.Single(await f.Db.ScenarioAdaptivePlansV2.ToArrayAsync());
        Assert.Equal("PENDING", first.Data.ApplyStatus);
        var applied = await service.ConfirmAppliedAsync(f.HostId, f.MatchId,
            request.DecisionId, request.ResultingPlanFingerprint);
        var applyReplay = await service.ConfirmAppliedAsync(f.HostId, f.MatchId,
            request.DecisionId, request.ResultingPlanFingerprint);
        Assert.Equal("APPLIED", applied.Data!.ApplyStatus);
        Assert.True(applyReplay.Data!.IsReplay);
        f.Db.PlayerAIProfiles.Single(x => x.UserId == f.PlayerId).ProfileRevision++;
        await f.Db.SaveChangesAsync();
        Assert.Equal(ErrorCodes.ScenarioDecisionStaleRoster,
            (await service.SubmitAsync(f.HostId, f.MatchId, request)).ErrorCode);
    }

    [Fact, Trait("Category", "AEDv2")]
    public async Task AEDv2_ShadowCannotBeConfirmedApplied()
    {
        await using var f = await ValidAEDv2Fixture();
        var scenario = (await f.Service.ResolvePreMatchAsync(f.HostId, f.MatchId, AdaptiveRequest())).Data!;
        var service = V2(f);
        var request = V2Request(scenario, 0, "PRE_MATCH", "SupportBonus", 0, 2, "RELIEVE", "SHADOW_ONLY");
        var shadow = await service.SubmitAsync(f.HostId, f.MatchId, request);
        Assert.True(shadow.IsSuccess);
        Assert.Equal("NOT_APPLICABLE", shadow.Data!.ApplyStatus);
        var applied = await service.ConfirmAppliedAsync(f.HostId, f.MatchId,
            request.DecisionId, request.ResultingPlanFingerprint);
        Assert.Equal(ErrorCodes.ScenarioApplyConflict, applied.ErrorCode);
        Assert.Null((await f.Db.ScenarioAdaptivePlansV2.SingleAsync()).AppliedAtUtc);
    }

    [Fact, Trait("Category", "AEDv2")]
    public async Task AEDv2_RejectsNonHostStaleProfileAndTwoKeyChange()
    {
        await using var f = await ValidAEDv2Fixture();
        var scenario = (await f.Service.ResolvePreMatchAsync(f.HostId, f.MatchId, AdaptiveRequest())).Data!;
        var service = V2(f);
        var request = V2Request(scenario, 0, "PRE_MATCH", "SupportBonus", 0, 2, "RELIEVE", "COMMITTED");
        Assert.Equal(ErrorCodes.MatchAuthorityForbidden,
            (await service.SubmitAsync(f.PlayerId, f.MatchId, request)).ErrorCode);
        request.PlanValues[4] = 8;
        request.ResultingPlanFingerprint = ScenarioAdaptivePlanV2Service.ComputePlanFingerprint(request.PlanValues);
        Assert.Equal(ErrorCodes.ScenarioApplyConflict,
            (await service.SubmitAsync(f.HostId, f.MatchId, request)).ErrorCode);
        request.PlanValues[4] = 7.5;
        request.ResultingPlanFingerprint = ScenarioAdaptivePlanV2Service.ComputePlanFingerprint(request.PlanValues);
        f.Db.PlayerAIProfiles.Single(x => x.UserId == f.PlayerId).ProfileRevision++;
        await f.Db.SaveChangesAsync();
        Assert.Equal(ErrorCodes.ScenarioDecisionStaleRoster,
            (await service.SubmitAsync(f.HostId, f.MatchId, request)).ErrorCode);
        Assert.Empty(await f.Db.ScenarioAdaptivePlansV2.ToArrayAsync());
    }

    [Fact, Trait("Category", "AEDv2")]
    public async Task AEDv2_BoundaryAccumulatesOneNewKeyAndRejectsStaleOrdinal()
    {
        await using var f = await ValidAEDv2Fixture();
        var scenario = (await f.Service.ResolvePreMatchAsync(f.HostId, f.MatchId, AdaptiveRequest())).Data!;
        var service = V2(f);
        var pre = V2Request(scenario, 0, "PRE_MATCH", "SupportBonus", 0, 2, "RELIEVE", "COMMITTED");
        Assert.True((await service.SubmitAsync(f.HostId, f.MatchId, pre)).IsSuccess);
        var preApplied = await service.ConfirmAppliedAsync(
            f.HostId, f.MatchId,
            pre.DecisionId, pre.ResultingPlanFingerprint);
        Assert.True(preApplied.IsSuccess);
        f.Db.MatchAuthorityBindings.Single().Status = MatchAuthorityStatus.InMatch;
        await f.Db.SaveChangesAsync();
        var boundary = V2Request(scenario, 1, "ALLOWED_PHASE_BOUNDARY", "DetectionAcquireSeconds",
            1.25, 1.5, "RELIEVE", "COMMITTED", pre.PlanValues);
        Assert.True((await service.SubmitAsync(f.HostId, f.MatchId, boundary)).IsSuccess);
        Assert.Equal(2, (await f.Db.ScenarioAdaptivePlansV2.ToArrayAsync()).Length);
        Assert.Equal(2, boundary.PlanValues[0]);
        var boundaryApplied = await service.ConfirmAppliedAsync(
            f.HostId, f.MatchId,
            boundary.DecisionId, boundary.ResultingPlanFingerprint);
        Assert.True(boundaryApplied.IsSuccess);
        var stale = V2Request(scenario, 1, "ALLOWED_PHASE_BOUNDARY", "ChaseSpeed",
            7.5, 8, "INCREASE_PRESSURE", "COMMITTED", boundary.PlanValues);
        Assert.Equal(ErrorCodes.ScenarioDecisionConflict,
            (await service.SubmitAsync(f.HostId, f.MatchId, stale)).ErrorCode);
    }

    [Fact, Trait("Category", "AEDv2")]
    public async Task AEDv2_PendingPlanBlocksNextDecision()
    {
        await using var f = await ValidAEDv2Fixture();
        var scenario = (await f.Service.ResolvePreMatchAsync(
            f.HostId, f.MatchId, AdaptiveRequest())).Data!;
        var service = V2(f);
        var pre = V2Request(scenario, 0, "PRE_MATCH", "SupportBonus", 0, 2, "RELIEVE", "COMMITTED");
        Assert.True((await service.SubmitAsync(f.HostId, f.MatchId, pre)).IsSuccess);
        f.Db.MatchAuthorityBindings.Single().Status = MatchAuthorityStatus.InMatch;
        await f.Db.SaveChangesAsync();

        var boundary = V2Request(scenario, 1, "ALLOWED_PHASE_BOUNDARY",
            "DetectionAcquireSeconds", 1.25, 1.5, "RELIEVE", "COMMITTED", pre.PlanValues);
        var result = await service.SubmitAsync(f.HostId, f.MatchId, boundary);

        Assert.Equal(ErrorCodes.ScenarioApplyConflict, result.ErrorCode);
        Assert.Single(await f.Db.ScenarioAdaptivePlansV2.ToArrayAsync());
    }

    [Fact, Trait("Category", "AEDv2")]
    public async Task AEDv2_AbortedPlanCannotBeApplied()
    {
        await using var f = await ValidAEDv2Fixture();
        var scenario = (await f.Service.ResolvePreMatchAsync(
            f.HostId, f.MatchId, AdaptiveRequest())).Data!;
        var service = V2(f);
        var request = V2Request(scenario, 0, "PRE_MATCH",
            "SupportBonus", 0, 2, "RELIEVE", "COMMITTED");
        Assert.True((await service.SubmitAsync(f.HostId, f.MatchId, request)).IsSuccess);
        Assert.True((await service.AbortPendingAsync(
            f.HostId, f.MatchId, request.DecisionId)).IsSuccess);

        var result = await service.ConfirmAppliedAsync(f.HostId, f.MatchId,
            request.DecisionId, request.ResultingPlanFingerprint);

        Assert.Equal(ErrorCodes.ScenarioApplyConflict, result.ErrorCode);
        Assert.Equal("ABORTED", (await f.Db.ScenarioAdaptivePlansV2.SingleAsync()).ApplyStatus);
    }

    [Fact, Trait("Category", "AEDv2")]
    public async Task AEDv2_AbortedPlanIsNotPreviousPlan()
    {
        await using var f = await ValidAEDv2Fixture();
        var scenario = (await f.Service.ResolvePreMatchAsync(
            f.HostId, f.MatchId, AdaptiveRequest())).Data!;
        var service = V2(f);
        var pre = V2Request(scenario, 0, "PRE_MATCH",
            "SupportBonus", 0, 2, "RELIEVE", "COMMITTED");
        Assert.True((await service.SubmitAsync(f.HostId, f.MatchId, pre)).IsSuccess);
        Assert.True((await service.AbortPendingAsync(
            f.HostId, f.MatchId, pre.DecisionId)).IsSuccess);
        f.Db.MatchAuthorityBindings.Single().Status = MatchAuthorityStatus.InMatch;
        await f.Db.SaveChangesAsync();

        var boundary = V2Request(scenario, 1, "ALLOWED_PHASE_BOUNDARY",
            "DetectionAcquireSeconds", 1.25, 1.5, "RELIEVE", "COMMITTED");
        var result = await service.SubmitAsync(f.HostId, f.MatchId, boundary);

        Assert.True(result.IsSuccess, $"{result.ErrorCode}: {result.Message}");
        Assert.Equal(ScenarioAdaptivePlanV2Service.ComputePlanFingerprint(
            ScenarioAdaptivePlanV2Service.NormalPlanValues()),
            result.Data!.PreviousPlanFingerprint);
    }

    [Fact, Trait("Category", "AEDv2")]
    public async Task AEDv2_AbortReplayIsIdempotent()
    {
        await using var f = await ValidAEDv2Fixture();
        var scenario = (await f.Service.ResolvePreMatchAsync(
            f.HostId, f.MatchId, AdaptiveRequest())).Data!;
        var service = V2(f);
        var request = V2Request(scenario, 0, "PRE_MATCH",
            "SupportBonus", 0, 2, "RELIEVE", "COMMITTED");
        Assert.True((await service.SubmitAsync(f.HostId, f.MatchId, request)).IsSuccess);

        var aborted = await service.AbortPendingAsync(f.HostId, f.MatchId, request.DecisionId);
        var replay = await service.AbortPendingAsync(f.HostId, f.MatchId, request.DecisionId);

        Assert.True(aborted.IsSuccess);
        Assert.True(replay.IsSuccess);
        Assert.True(replay.Data!.IsReplay);
        Assert.Equal("ABORTED", replay.Data.ApplyStatus);
        Assert.Single(await f.Db.ScenarioAdaptivePlansV2.ToArrayAsync());
    }

    [Fact, Trait("Category", "AEDv2")]
    public async Task AEDv2_AppliedPlanCannotBeAborted()
    {
        await using var f = await ValidAEDv2Fixture();
        var scenario = (await f.Service.ResolvePreMatchAsync(
            f.HostId, f.MatchId, AdaptiveRequest())).Data!;
        var service = V2(f);
        var request = V2Request(scenario, 0, "PRE_MATCH",
            "SupportBonus", 0, 2, "RELIEVE", "COMMITTED");
        Assert.True((await service.SubmitAsync(f.HostId, f.MatchId, request)).IsSuccess);
        Assert.True((await service.ConfirmAppliedAsync(f.HostId, f.MatchId,
            request.DecisionId, request.ResultingPlanFingerprint)).IsSuccess);

        var result = await service.AbortPendingAsync(f.HostId, f.MatchId, request.DecisionId);

        Assert.Equal(ErrorCodes.ScenarioApplyConflict, result.ErrorCode);
        Assert.Equal("APPLIED", (await f.Db.ScenarioAdaptivePlansV2.SingleAsync()).ApplyStatus);
    }

    [Fact, Trait("Category", "AEDv2")]
    public async Task AEDv2_FixedDecisionCannotAuthorizePlan()
    {
        await using var f = await ValidAEDv2Fixture();
        var scenario = (await f.Service.ResolvePreMatchAsync(f.HostId, f.MatchId, Request())).Data!;
        var request = V2Request(scenario, 0, "PRE_MATCH", "SupportBonus", 0, 2, "RELIEVE", "COMMITTED");
        Assert.Equal(ErrorCodes.ScenarioDecisionStaleRoster,
            (await V2(f).SubmitAsync(f.HostId, f.MatchId, request)).ErrorCode);
        Assert.Empty(await f.Db.ScenarioAdaptivePlansV2.ToArrayAsync());
    }

    private static async Task<Fixture> ValidAEDv2Fixture()
    {
        var f = await Fixture.CreateAsync();
        var player = f.Db.PlayerAIProfiles.Single(x => x.UserId == f.PlayerId);
        player.SurvivalStatus = ProfileDimensionStatus.Active;
        player.NoiseStatus = ProfileDimensionStatus.Active;
        player.SurvivalSampleCount = 2;
        player.NoiseSampleCount = 2;
        await f.Db.SaveChangesAsync();
        return f;
    }

    private static ScenarioAdaptivePlanV2Service V2(Fixture f) =>
        new(f.Db, new ScenarioSnapshotReadService(f.Db, new FixedClock(Now)), new FixedClock(Now));

    private static ResolveScenarioRequest AdaptiveRequest()
    {
        var request = Request();
        request.ResolutionMode = ScenarioResolutionMode.Adaptive;
        return request;
    }

    private static SubmitScenarioAdaptivePlanV2Request V2Request(
        ScenarioDecisionResponse scenario, int ordinal, string point, string key,
        double before, double after, string intent, string status, double[]? previous = null)
    {
        var old = previous ?? ScenarioAdaptivePlanV2Service.NormalPlanValues();
        var values = (double[])old.Clone();
        var index = key switch
        {
            "SupportBonus" => 0,
            "DetectionAcquireSeconds" => 2,
            "ChaseSpeed" => 4,
            _ => throw new ArgumentOutOfRangeException(nameof(key))
        };
        values[index] = after;
        return new SubmitScenarioAdaptivePlanV2Request
        {
            DecisionId = Guid.NewGuid(), PhaseOrdinal = ordinal, DecisionPoint = point,
            PolicyVersion = ScenarioAdaptivePlanV2Service.PolicyVersion,
            BaselineVersion = ScenarioAdaptivePlanV2Service.BaselineVersion,
            PreviousPlanFingerprint = ScenarioAdaptivePlanV2Service.ComputePlanFingerprint(old),
            ResultingPlanFingerprint = ScenarioAdaptivePlanV2Service.ComputePlanFingerprint(values),
            ChangedKey = key, PreviousValue = before, AppliedValue = after,
            AdaptationIntent = intent, DecisionReason = "TEST_EVIDENCE",
            SnapshotId = scenario.SnapshotId, SnapshotFingerprint = scenario.SnapshotContentFingerprint,
            RosterIdentity = scenario.RosterIdentity,
            EvidenceFingerprint = ordinal == 0 ? string.Empty : new string('a', 64),
            CommitStatus = status, PlanValues = values
        };
    }
}
