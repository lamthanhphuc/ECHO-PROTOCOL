using System.Text.Json;
using EchoProtocol.AI.Common.AED;
using EchoProtocol.Api.Services;
using EchoProtocol.Api.Configurations;
using EchoProtocol.Api.Enums;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using Xunit;

namespace EchoProtocol.Api.Tests;

public sealed partial class ScenarioServiceTests
{
    [Fact, Trait("Category", "AEDV02")]
    public async Task V02_CancelBeforeApprovalIsIdempotentAndPreventsDelayedSubmission()
    {
        await using var f = await ValidV02Fixture();
        var scenario = (await f.Service.ResolvePreMatchAsync(f.HostId, f.MatchId, AdaptiveRequest())).Data!;
        var (events, source) = await V02WorldEvidence(f, geometry: true);
        var storage = new AcceptedEvidenceRepository(events);
        var service = new AEDV02DecisionService(f.Db, new AEDV02SourceVerifier(f.Db, storage, new FixedClock(Now)), storage,
            new FixedClock(Now), Options.Create(new AEDV02PilotSettings { Enabled = true }));
        f.Db.MatchAuthorityBindings.Single().Status = MatchAuthorityStatus.Lobby;
        await f.Db.SaveChangesAsync();
        var request = V02Request(f, scenario, source.SceneManifestFingerprint, AEDV02Plan.Normal(), 0, null, 0);
        Assert.False((await service.AbortAsync(f.PlayerId, f.MatchId, request.DecisionId)).IsSuccess);
        Assert.Equal("HOLD_HOST_COMMIT_NOT_ACCEPTED", (await service.AbortAsync(f.HostId, f.MatchId, request.DecisionId, mayHaveCommitted: true)).Message);
        Assert.Empty(f.Db.Set<EchoProtocol.Api.Entities.AEDV02Cancellation>());
        var cancelled = await service.AbortAsync(f.HostId, f.MatchId, request.DecisionId);
        Assert.True(cancelled.IsSuccess, cancelled.Message);
        Assert.Equal("CANCELLED", cancelled.Data!.ApprovalStatus);
        Assert.Equal("ABORTED", cancelled.Data.ApplyStatus);
        Assert.True((await service.AbortAsync(f.HostId, f.MatchId, request.DecisionId)).Data!.IsReplay);
        Assert.Equal("ABORT_DECISION_CANCELLED", (await service.SubmitAsync(f.HostId, f.MatchId, request)).Message);
        Assert.Empty(f.Db.AEDV02Decisions);
        Assert.Single(f.Db.Set<EchoProtocol.Api.Entities.AEDV02Cancellation>());
    }

    [Theory, Trait("Category", "AEDV02")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task V02_TerminalReconciliationRetainsCommittedRevisionWithoutInventingApply(bool committed)
    {
        await using var f = await ValidV02Fixture();
        var scenario = (await f.Service.ResolvePreMatchAsync(f.HostId, f.MatchId, AdaptiveRequest())).Data!;
        var (original, request) = await V02WorldEvidence(f, geometry: true);
        var events = original.ToList();
        var repository = new MutableV02Repository(events);
        var service = new AEDV02DecisionService(f.Db, new AEDV02SourceVerifier(f.Db, repository, new FixedClock(Now)), repository,
            new FixedClock(Now), Options.Create(new AEDV02PilotSettings { Enabled = true }));
        f.Db.MatchAuthorityBindings.Single().Status = MatchAuthorityStatus.Lobby;
        await f.Db.SaveChangesAsync();
        var result = await service.SubmitAsync(f.HostId, f.MatchId, V02Request(f, scenario, request.SceneManifestFingerprint, AEDV02Plan.Normal(), 0, null, 0));
        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal("HOLD_HOST_COMMIT_NOT_ACCEPTED", (await service.AbortAsync(f.HostId, f.MatchId, result.Data!.DecisionId, mayHaveCommitted: true)).Message);
        Assert.Equal("PENDING", f.Db.AEDV02Decisions.Single().ApplyStatus);
        if (committed) events.Add(V02Effect(f, events, result.Data!, "AEDPolicyEnvelope", AEDV02Key.CabinetAmbushEnabled, 2400, events.Count + 1, Now));
        var binding = f.Db.MatchAuthorityBindings.Single(); binding.Status = MatchAuthorityStatus.Ended; binding.LeaseExpiresAtUtc = Now.AddSeconds(-1);
        await f.Db.SaveChangesAsync();
        Assert.Equal("HOLD_MATCH_TERMINAL_PROOF_MISSING", (await service.AbortAsync(f.HostId, f.MatchId, result.Data!.DecisionId)).Message);
        events.Add(V02Event(f.MatchId, "MATCH_ENDED", events.Count + 1, Now, "1.1", new BsonDocument {
            ["context"] = new BsonDocument { ["phase"] = "MATCH_END", ["authorityTick"] = 2401 },
            ["data"] = new BsonDocument { ["outcome"] = "SUCCESS", ["durationSeconds"] = 61, ["survivorCount"] = 2 } }));
        binding.Status = MatchAuthorityStatus.InMatch; binding.LeaseExpiresAtUtc = Now.AddMinutes(5); await f.Db.SaveChangesAsync();
        var accepted = await TelemetryServiceTests.IngestV02Trace(f.MatchId, events, Now, V02TelemetryAuthority(f), f.HostId);
        events.Clear(); events.AddRange(accepted);
        binding.Status = MatchAuthorityStatus.Ended; binding.LeaseExpiresAtUtc = Now.AddSeconds(-1); await f.Db.SaveChangesAsync();
        var reconciled = await service.AbortAsync(f.HostId, f.MatchId, result.Data.DecisionId);
        Assert.True(reconciled.IsSuccess, reconciled.Message);
        Assert.Equal(committed ? "FAILED_EFFECT" : "ABORTED", reconciled.Data!.ApplyStatus);
        Assert.Null(f.Db.AEDV02Decisions.Single().ReceiptId);
        Assert.True((await service.AbortAsync(f.HostId, f.MatchId, result.Data.DecisionId)).Data!.IsReplay);
    }

    [Theory, Trait("Category", "AEDV02")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task V02_ColdStartCanCommitNeutralEnvelopeWithoutCertifyingMissingHistoricalSkill(bool adverse)
    {
        await using var f = await ValidV02Fixture();
        f.Db.PlayerAIProfiles.RemoveRange(f.Db.PlayerAIProfiles);
        await f.Db.SaveChangesAsync();
        var scenario = (await f.Service.ResolvePreMatchAsync(f.HostId, f.MatchId, AdaptiveRequest())).Data!;
        var snapshot = f.Db.AdaptiveInputSnapshots.Single(x => x.SnapshotId == scenario.SnapshotId);
        Assert.Equal(AdaptiveSnapshotValidity.Partial, snapshot.Validity);
        var (events, source) = await V02WorldEvidence(f, geometry: true);
        f.Db.MatchAuthorityBindings.Single().Status = MatchAuthorityStatus.Lobby;
        await f.Db.SaveChangesAsync();
        var storage = new AcceptedEvidenceRepository(events);
        var service = new AEDV02DecisionService(f.Db, new AEDV02SourceVerifier(f.Db, storage, new FixedClock(Now)), storage,
            new FixedClock(Now), Options.Create(new AEDV02PilotSettings { Enabled = true }));
        var plan = adverse ? AEDV02Plan.Normal().With(AEDV02Key.DetectionAcquireSeconds, 1) : AEDV02Plan.Normal();
        var result = await service.SubmitAsync(f.HostId, f.MatchId, V02Request(f, scenario, source.SceneManifestFingerprint, plan, 0, null, 0));
        Assert.Equal(!adverse, result.IsSuccess);
        if (adverse) Assert.Equal("HOLD_INVALID_SNAPSHOT", result.Message);
        else Assert.Equal("APPROVED", result.Data!.ApprovalStatus);
        Assert.Equal(AdaptiveSnapshotValidity.Partial, snapshot.Validity);
        Assert.Empty(f.Db.AEDV02SkillObservations);
    }

    [Theory, Trait("Category", "AEDV02")]
    [InlineData("complete")]
    [InlineData("old-contract")]
    [InlineData("missing-impact")]
    [InlineData("unobserved-onset")]
    [InlineData("detection-during-recovery")]
    [InlineData("chase-restarts")]
    [InlineData("caught-during-recovery")]
    public async Task V02_PilotOpportunityCoverageRequiresRealProtocolAndConsequences(string mode)
    {
        await using var f = await ValidV02Fixture();
        var (original, request) = await V02WorldEvidence(f, geometry: true);
        var user = f.Db.MatchPlayerBindings.First(x => x.MatchId == f.MatchId).UserId;
        var trace = original.Select(x => V02Event(x.MatchId, x.EventType, x.EventSequence, x.Ts, x.SchemaVersion,
            (BsonDocument)x.ValueJson.DeepClone(), x.UserId)).ToArray();
        foreach (var item in trace.Where(x => x.EventType == "AED_V02_WORLD_SAMPLE"))
        {
            var world = JsonSerializer.Deserialize<AEDV02WorldSample>(item.ValueJson["data"]["world"].ToJson())!;
            world.OpportunityContractVersion = mode == "old-contract" ? string.Empty : AEDV02PilotOpportunities.Version;
            int second = (int)(world.Tick / 60 - 10);
            if (second is >= 1 and <= 10 || mode == "unobserved-onset" && second == 0)
                world.Stalkers = [new AEDV02ThreatSource { NetworkId = "stalker", Zone = 1, State = "CHASE", EpisodeId = "real-episode", TargetUserId = user }];
            else world.Stalkers = [new AEDV02ThreatSource { NetworkId = "stalker", Zone = 1, State = "SEARCH" }];
            if (mode == "detection-during-recovery" && second is >= 11 and <= 20)
                world.Stalkers[0] = new AEDV02ThreatSource { NetworkId = "stalker", Zone = 1, State = "DETECTION", EpisodeId = "next-detection", TargetUserId = user };
            if (mode == "chase-restarts" && second >= 11)
                world.Stalkers[0] = new AEDV02ThreatSource { NetworkId = "stalker", Zone = 1, State = "CHASE", EpisodeId = "next-chase", TargetUserId = user };
            if (mode == "caught-during-recovery" && second == 15) world.Players.Single(x => x.UserId == user).IsCaught = true;
            if (mode == "missing-impact" && second >= 8) world.Players.Single(x => x.UserId == user).SlowUntilTick = 5000;
            item.ValueJson["data"]["world"] = BsonDocument.Parse(JsonSerializer.Serialize(world));
        }
        var accepted = await TelemetryServiceTests.IngestV02Trace(f.MatchId, trace, Now, V02TelemetryAuthority(f), f.HostId);
        var result = await new AEDV02SourceVerifier(f.Db, new AcceptedEvidenceRepository(accepted), new FixedClock(Now)).VerifyAsync(f.MatchId, request);
        if (mode == "missing-impact")
        { Assert.False(result.IsSuccess); Assert.Equal("HOLD_CAUSAL_IMPACT_LINEAGE_INVALID", result.Message); return; }
        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(mode != "old-contract", result.Data!.PressureComplete);
        if (mode == "old-contract")
        { Assert.Equal("Unsupported", result.Data.TeamPressureStatus); Assert.Empty(result.Data.ResolvedOpportunities!); return; }
        Assert.Equal("Verified", result.Data.TeamPressureStatus);
        Assert.Equal(AEDV02PilotOpportunities.Version, result.Data.OpportunityContractVersion);
        if (mode == "chase-restarts")
        {
            Assert.Equal(2, result.Data.ResolvedOpportunities!.Length);
            Assert.All(result.Data.ResolvedOpportunities, x => Assert.Equal("Censored", x.Resolution));
            return;
        }
        var opportunity = Assert.Single(result.Data.ResolvedOpportunities!);
        Assert.Equal(user, opportunity.UserId);
        Assert.Equal(mode is "unobserved-onset" or "detection-during-recovery" or "caught-during-recovery" ? "Censored" : "Success", opportunity.Resolution);
        Assert.Equal("Stalker", opportunity.Axis);
    }
}

public sealed class AEDV02PilotDefaultsTests
{
    [Theory]
    [InlineData("all", true)]
    [InlineData("aid-only", false)]
    [InlineData("weak-stabilizer", false)]
    [InlineData("missing-scanner", false)]
    public void ToolCompetenceCannotGeneralizeFirstAidSuccessToOtherToolFamilies(string mode, bool ready)
    {
        var user = Guid.NewGuid(); var now = DateTime.UtcNow; var context = AEDV02Plan.Hash("tool-context");
        var matches = Enumerable.Range(0, 3).Select(_ => Guid.NewGuid()).ToArray();
        var rows = new[] { 1, 2, 3, 4, 6 }.Where(type => mode != "aid-only" || type == 3)
            .Where(type => mode != "missing-scanner" || type != 1).SelectMany(type => Enumerable.Range(0, 8).Select(i => new AEDV02HistoricalOpportunity {
                MatchId = matches[i % 3], Context = context, ContractVersion = AEDV02PilotOpportunities.Version,
                SourceFingerprint = AEDV02Plan.Hash("tool:" + type + ":" + i), EndedAtUtc = now.AddDays(-i % 3),
                Opportunity = new AEDV02ResolvedOpportunity { Id = "tool:" + type + ":" + i, UserId = user, Axis = "Tool", ToolId = type,
                    StartTick = 1, EndTick = 100, Resolution = mode == "weak-stabilizer" && type == 6 ? "Failure" : "Success" }
            })).ToArray();
        var result = AEDV02PilotOpportunities.Competence(user, "Tool", context, now, rows);
        Assert.Equal(ready, result.ReadyForIncrease);
        if (mode is "aid-only" or "missing-scanner") { Assert.Equal("Insufficient", result.Confidence); Assert.Null(result.Score); }
        if (mode == "weak-stabilizer") Assert.Equal(0, result.Score);
    }

    [Theory]
    [InlineData(0d, 4)]
    [InlineData(.25, 4)]
    [InlineData(.45, 3)]
    [InlineData(.65, 1)]
    [InlineData(.85, 1)]
    public void TeamPressureDoesNotHideWeakestPlayerBehindTeamAverage(double worst, int maximumTier)
    {
        var result = AEDV02PilotOpportunities.TeamPressure([
            new(new double[5], worst, true, "test"), new(new double[5], 0, true, "test")], 0, 0);
        Assert.True(result.Complete);
        Assert.Equal(maximumTier, result.Tier);
        var unknown = AEDV02PilotOpportunities.TeamPressure([new(new double[5], 0, false, "missing")], 0, 0);
        Assert.False(unknown.Complete); Assert.Equal(-1, unknown.Tier);
    }

    [Theory]
    [InlineData("valid", true)]
    [InlineData("old", false)]
    [InlineData("context", false)]
    [InlineData("too-few-matches", false)]
    [InlineData("censored", false)]
    [InlineData("weak", false)]
    [InlineData("conflicting", false)]
    public void CompetenceNeedsResolvedComparableRecentIndependentOpportunities(string mode, bool ready)
    {
        var user = Guid.NewGuid(); var context = AEDV02Plan.Hash("context"); var now = DateTime.UtcNow;
        var matches = Enumerable.Range(0, 3).Select(_ => Guid.NewGuid()).ToArray();
        var rows = Enumerable.Range(0, 9).Select(i => new AEDV02HistoricalOpportunity {
            MatchId = mode == "too-few-matches" ? matches[0] : matches[i / 3],
            Context = mode == "context" ? AEDV02Plan.Hash("other") : context,
            ContractVersion = AEDV02PilotOpportunities.Version, SourceFingerprint = AEDV02Plan.Hash("accepted:" + i),
            EndedAtUtc = mode == "old" ? now.AddDays(-31) : now.AddDays(-i / 3),
            Opportunity = new AEDV02ResolvedOpportunity { Id = "episode:" + i, UserId = user, Axis = "Stalker", StartTick = 1, EndTick = 100,
                Resolution = mode == "censored" ? "Censored" : mode == "weak" ? "Failure" : "Success" } }).ToList();
        if (mode == "conflicting") rows.Add(new AEDV02HistoricalOpportunity { MatchId = rows[0].MatchId, Context = context,
            ContractVersion = AEDV02PilotOpportunities.Version, SourceFingerprint = rows[0].SourceFingerprint, EndedAtUtc = now,
            Opportunity = new AEDV02ResolvedOpportunity { Id = rows[0].Opportunity.Id, UserId = user, Axis = "Stalker", StartTick = 1, EndTick = 100, Resolution = "Failure" } });
        var result = AEDV02PilotOpportunities.Competence(user, "Stalker", context, now, rows.Concat(rows));
        Assert.Equal(ready, result.ReadyForIncrease);
        if (ready) { Assert.Equal(3, result.Matches); Assert.Equal(9, result.Resolved); Assert.Equal(1, result.Score); }
        if (mode == "conflicting") Assert.Equal("Invalid", result.Confidence);
        Assert.False(AEDV02PilotOpportunities.Competence(Guid.NewGuid(), "Stalker", context, now, rows).ReadyForIncrease);
    }
}
