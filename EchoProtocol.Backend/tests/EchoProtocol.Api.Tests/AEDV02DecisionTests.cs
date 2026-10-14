using System.Text.Json;
using EchoProtocol.AI.Common.AED;
using EchoProtocol.Api.Common;
using EchoProtocol.Api.Configurations;
using EchoProtocol.Api.Data.Telemetry;
using EchoProtocol.Api.DTOs.Scenarios;
using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using Xunit;

namespace EchoProtocol.Api.Tests;

public sealed partial class ScenarioServiceTests
{
    [Theory, Trait("Category", "AEDV02")]
    [InlineData("valid", null)]
    [InlineData("missing-terminal", "HOLD_MATCH_TERMINAL_PROOF_MISSING")]
    [InlineData("effect-after-terminal", "HOLD_EFFECT_AFTER_MATCH_END")]
    [InlineData("wrong-host", "AED_V02_HOST_REQUIRED")]
    public async Task V02_LateReceiptRequiresPreTerminalWorldProof(string mode, string? rejection)
    {
        await using var f = await ValidAEDv2Fixture();
        var scenario = (await f.Service.ResolvePreMatchAsync(f.HostId, f.MatchId, AdaptiveRequest())).Data!;
        var (world, request) = await V02WorldEvidence(f);
        var events = world.ToList(); var repository = new MutableV02Repository(events);
        var service = new AEDV02DecisionService(f.Db, new AEDV02SourceVerifier(f.Db, repository, new FixedClock(Now)), repository,
            new FixedClock(Now), Options.Create(new AEDV02PilotSettings { Enabled = true }));
        f.Db.MatchAuthorityBindings.Single().Status = MatchAuthorityStatus.Lobby; await f.Db.SaveChangesAsync();
        var initial = (await service.SubmitAsync(f.HostId, f.MatchId, V02Request(f, scenario, request.SceneManifestFingerprint, AEDV02Plan.Normal(), 0, null, 0))).Data!;
        f.Db.MatchAuthorityBindings.Single().Status = MatchAuthorityStatus.InMatch; await f.Db.SaveChangesAsync();
        var effect = V02Effect(f, initial, "AEDPolicyEnvelope", AEDV02Key.CabinetAmbushEnabled, 2400, events.Count + 1, Now); events.Add(effect);
        V02AppendWindow(f, events, AEDV02Plan.Normal(), 1, 2401, Now, 1);
        if (mode != "missing-terminal") events.Add(V02Event(f.MatchId, "MATCH_ENDED", events.Count + 1, Now.AddSeconds(1), "1.1", new BsonDocument {
            ["context"] = new BsonDocument { ["phase"] = "MATCH_END", ["authorityTick"] = 2460 },
            ["data"] = new BsonDocument { ["outcome"] = "SUCCESS", ["durationSeconds"] = 61, ["survivorCount"] = 2 } }));
        var accepted = await TelemetryServiceTests.IngestV02Trace(f.MatchId, events, Now.AddSeconds(1), V02TelemetryAuthority(f), f.HostId);
        events.Clear(); events.AddRange(accepted);
        if (mode == "effect-after-terminal")
        {
            events.RemoveAll(x => x.Id == effect.Id);
            effect = V02Effect(f, initial, "AEDPolicyEnvelope", AEDV02Key.CabinetAmbushEnabled, 2461, events.Last().EventSequence + 1, Now.AddSeconds(2));
            events.Add(effect);
        }
        f.Db.MatchAuthorityBindings.Single().Status = MatchAuthorityStatus.Ended;
        f.Db.MatchAuthorityBindings.Single().LeaseExpiresAtUtc = Now; await f.Db.SaveChangesAsync();
        var receipt = new AEDV02ReceiptRequest(Guid.NewGuid(), effect.Id, 1, initial.PlanFingerprint, 2400);
        var result = await service.ConfirmAsync(mode == "wrong-host" ? Guid.NewGuid() : f.HostId, f.MatchId, initial.DecisionId, receipt);
        if (rejection != null) { Assert.False(result.IsSuccess); Assert.Equal(rejection, result.Message); return; }
        Assert.True(result.IsSuccess, result.Message); Assert.Equal("APPLIED", result.Data!.ApplyStatus);
        Assert.True((await service.ConfirmAsync(f.HostId, f.MatchId, initial.DecisionId, receipt)).Data!.IsReplay);
    }

    [Fact, Trait("Category", "AEDV02")]
    public async Task V02_VerifiedPressureAndClaimedSkillRows_CannotCertifyUnsupportedHistoricalCompetence()
    {
        await using var f = await ValidAEDv2Fixture();
        var scenario = (await f.Service.ResolvePreMatchAsync(f.HostId, f.MatchId, AdaptiveRequest())).Data!;
        var (world, sourceRequest) = await V02WorldEvidence(f);
        var events = world.ToList(); var repository = new MutableV02Repository(events);
        var verifier = new AEDV02SourceVerifier(f.Db, repository, new FixedClock(Now));
        var service = new AEDV02DecisionService(f.Db, verifier, repository, new FixedClock(Now), Options.Create(new AEDV02PilotSettings { Enabled = true }));
        f.Db.MatchAuthorityBindings.Single().Status = MatchAuthorityStatus.Lobby; await f.Db.SaveChangesAsync();
        var initialRequest = V02Request(f, scenario, sourceRequest.SceneManifestFingerprint, AEDV02Plan.Normal(), 0, null, 0);
        var initial = (await service.SubmitAsync(f.HostId, f.MatchId, initialRequest)).Data!;
        f.Db.MatchAuthorityBindings.Single().Status = MatchAuthorityStatus.InMatch; await f.Db.SaveChangesAsync();
        var effect = V02Effect(f, initial, "AEDPolicyEnvelope", AEDV02Key.CabinetAmbushEnabled, 2400, events.Count + 1, Now);
        events.Add(effect);
        var source = await verifier.VerifyAsync(f.MatchId, sourceRequest);
        Assert.True(source.IsSuccess, source.Message);
        var users = source.Data!.PlayerPressure.Keys.ToArray();
        var context = source.Data.SceneManifestFingerprint + "|Normal|" + AEDV02Plan.CatalogVersion;
        for (int i = 0; i < 3; i++)
        {
            var prior = Guid.NewGuid();
            f.Db.MatchAuthorityBindings.Add(new MatchAuthorityBinding { MatchId = prior, HostUserId = f.HostId,
                FusionSessionName = "hist-" + prior.ToString("N"), MaxPlayers = 4, Status = MatchAuthorityStatus.InMatch, LeaseExpiresAtUtc = Now.AddMinutes(1), CreatedAtUtc = Now, UpdatedAtUtc = Now });
            foreach (var user in users) f.Db.MatchPlayerBindings.Add(new MatchPlayerBinding { Id = Guid.NewGuid(), MatchId = prior, UserId = user,
                JoinProofId = Guid.NewGuid(), FusionActorNumber = Array.IndexOf(users, user) + 1, BoundAtUtc = Now, LastSeenAtUtc = Now });
            await f.Db.SaveChangesAsync();
            var priorRoster = AEDV02Plan.Hash(prior.ToString("D") + "|" + string.Join(',', users.Order().Select(x => x.ToString("D"))));
            var priorEvents = world.Select(item =>
            {
                var json = item.ValueJson.DeepClone().AsBsonDocument;
                if (item.EventType == "AED_V02_WORLD_SAMPLE") json["data"]["world"]["RosterIdentity"] = priorRoster;
                return V02Event(prior, item.EventType, item.EventSequence, item.Ts, item.SchemaVersion, json, item.UserId);
            }).ToArray();
            var reconstructed = await new AEDV02SourceVerifier(f.Db, new AcceptedEvidenceRepository(priorEvents), new FixedClock(Now)).VerifyAsync(prior, sourceRequest);
            Assert.True(reconstructed.IsSuccess, reconstructed.Message);
            foreach (var user in users) foreach (var dimension in Enum.GetValues<AEDSkillDimensionV1>())
                f.Db.AEDV02SkillObservations.Add(new AEDV02SkillObservation { Id = Guid.NewGuid(), MatchId = prior, UserId = user,
                    Dimension = dimension.ToString(), ComparisonContextKey = context, SourceFingerprint = reconstructed.Data!.Fingerprint,
                    Eligible = 8, ResolvedSuccess = 8, VerifiedAtUtc = Now });
        }
        await f.Db.SaveChangesAsync();
        var observed = JsonSerializer.Deserialize<AEDV02WorldSample>(world.Last().ValueJson["data"]["world"].ToJson())!;
        observed.Tick = 2401;
        events.Add(V02Event(f.MatchId, "AED_V02_WORLD_SAMPLE", events.Count + 1, Now, "1.3", BsonDocument.Parse(JsonSerializer.Serialize(new
            { context = new { phase = "CORE_COLLECTION", authorityTick = 2401, occurrenceKey = "world:2401" }, data = new { world = observed } }))));
        Assert.True((await service.ConfirmAsync(f.HostId, f.MatchId, initial.DecisionId, new AEDV02ReceiptRequest(Guid.NewGuid(), effect.Id, 1, initial.PlanFingerprint, 2400))).IsSuccess);
        var increase = await service.SubmitAsync(f.HostId, f.MatchId, V02Request(f, scenario, sourceRequest.SceneManifestFingerprint,
            AEDV02Plan.Normal().With(AEDV02Key.ChaseSpeed, 8), 1, source.Data, 1));
        Assert.True(increase.IsSuccess, increase.Message);
        Assert.Equal("HOLD", increase.Data!.ApprovalStatus);
        Assert.Equal("HOLD_HISTORICAL_OPPORTUNITIES_REQUIRED", increase.Data.ReasonCode);
        Assert.Equal("NOT_APPLICABLE", increase.Data.ApplyStatus);
    }

    [Theory, Trait("Category", "AEDV02")]
    [InlineData("fixed", "AED_V02_REQUEST_INVALID")]
    [InlineData("difficulty", "AED_V02_REQUEST_INVALID")]
    [InlineData("disabled", "HOLD_GAMEPLAY_DISABLED")]
    [InlineData("client", "AED_V02_HOST_REQUIRED")]
    [InlineData("lease", "AED_V02_LEASE_EXPIRED")]
    [InlineData("revision", "ABORT_STALE_REVISION")]
    [InlineData("fingerprint", "ABORT_STALE_REVISION")]
    [InlineData("roster", "HOLD_STALE_SNAPSHOT")]
    [InlineData("snapshot", "HOLD_STALE_SNAPSHOT")]
    [InlineData("window", "AED_V02_WINDOW_ID_INVALID")]
    [InlineData("cross-match", "AED_V02_HOST_REQUIRED")]
    [InlineData("scene", "HOLD_UNTRUSTED_SCENE_MANIFEST")]
    [InlineData("phase", "HOLD_DECISION_WINDOW_CLOSED")]
    [InlineData("disconnect", "HOLD_ROSTER_NOT_CURRENT")]
    [InlineData("unsupported-history", "HOLD_HISTORICAL_OPPORTUNITIES_REQUIRED")]
    public async Task V02_RealApproval_FailsClosedBeforeAnyPendingApply(string failure, string reason)
    {
        await using var f = await ValidAEDv2Fixture();
        var scenario = (await f.Service.ResolvePreMatchAsync(f.HostId, f.MatchId, AdaptiveRequest())).Data!;
        var (events, source) = await V02WorldEvidence(f);
        f.Db.MatchAuthorityBindings.Single().Status = MatchAuthorityStatus.Lobby;
        var request = V02Request(f, scenario, source.SceneManifestFingerprint, AEDV02Plan.Normal(), 0, null, 0);
        switch (failure)
        {
            case "fixed": request = request with { ResolutionMode = "Fixed" }; break;
            case "difficulty": request = request with { Difficulty = "Hard" }; break;
            case "lease": f.Db.MatchAuthorityBindings.Single().LeaseExpiresAtUtc = Now; break;
            case "revision": request = request with { ExpectedPlanRevision = 1 }; break;
            case "fingerprint": request = request with { PreviousPlanFingerprint = AEDV02Plan.Hash("wrong") }; break;
            case "roster": request = request with { RosterIdentity = AEDV02Plan.Hash("wrong") }; break;
            case "snapshot": request = request with { SnapshotFingerprint = AEDV02Plan.Hash("wrong") }; break;
            case "window": request = request with { WindowId = AEDV02Plan.Hash("wrong") }; break;
            case "scene": request = request with { SceneManifestFingerprint = AEDV02Plan.Hash("wrong") }; break;
            case "phase": f.Db.MatchAuthorityBindings.Single().Status = MatchAuthorityStatus.InMatch; break;
            case "disconnect": f.Db.MatchPlayerBindings.First().DisconnectedAtUtc = Now; break;
            case "unsupported-history": request = request with { PlanValues = AEDV02Plan.Normal().With(AEDV02Key.ChaseSpeed, 8).ToArray() }; break;
        }
        await f.Db.SaveChangesAsync();
        var repository = new AcceptedEvidenceRepository(events);
        var verifier = new AEDV02SourceVerifier(f.Db, repository, new FixedClock(Now));
        var service = new AEDV02DecisionService(f.Db, verifier, repository, new FixedClock(Now),
            Options.Create(new AEDV02PilotSettings { Enabled = failure != "disabled" }));
        var result = await service.SubmitAsync(failure == "client" ? Guid.NewGuid() : f.HostId,
            failure == "cross-match" ? Guid.NewGuid() : f.MatchId, request);
        Assert.Equal(reason, result.IsSuccess ? result.Data!.ReasonCode : result.Message);
        Assert.DoesNotContain(f.Db.AEDV02Decisions, x => x.ApplyStatus is "PENDING" or "APPLIED");
    }

    [Fact, Trait("Category", "AEDV02")]
    public async Task V02_RealApproval_PreMatchTwoSamePhaseWindowsAndObservedReceipts()
    {
        await using var f = await ValidAEDv2Fixture();
        var scenario = (await f.Service.ResolvePreMatchAsync(f.HostId, f.MatchId, AdaptiveRequest())).Data!;
        var (world, sourceRequest) = await V02WorldEvidence(f, geometry: true);
        var events = (await TelemetryServiceTests.IngestV02Trace(f.MatchId, V02PressureTrace(f, world), Now, V02TelemetryAuthority(f), f.HostId)).ToList();
        var storage = new MutableV02Repository(events);
        var clock = new MutableV02Clock(Now);
        var verifier = new AEDV02SourceVerifier(f.Db, storage, clock);
        var service = new AEDV02DecisionService(f.Db, verifier, storage, clock, Options.Create(new AEDV02PilotSettings { Enabled = true }));
        f.Db.MatchAuthorityBindings.Single().Status = MatchAuthorityStatus.Lobby;
        await f.Db.SaveChangesAsync();
        var initialRequest = V02Request(f, scenario, sourceRequest.SceneManifestFingerprint, AEDV02Plan.Normal(), 0, null, 0);
        var initial = await service.SubmitAsync(f.HostId, f.MatchId, initialRequest);
        Assert.True(initial.IsSuccess, initial.Message);
        Assert.Equal("APPROVED", initial.Data!.ApprovalStatus);
        Assert.Equal(1, initial.Data.PlanRevision);
        Assert.True((await service.SubmitAsync(f.HostId, f.MatchId, initialRequest)).Data!.IsReplay);
        f.Db.MatchAuthorityBindings.Single().Status = MatchAuthorityStatus.InMatch;
        await f.Db.SaveChangesAsync();
        var envelope = V02Effect(f, initial.Data, "AEDPolicyEnvelope", AEDV02Key.CabinetAmbushEnabled, 2400, events.Count + 1, Now);
        events.Add(envelope);
        var initialReceipt = new AEDV02ReceiptRequest(Guid.NewGuid(), envelope.Id, 1, initial.Data.PlanFingerprint, 2400);
        Assert.Equal("HOLD_WORLD_EFFECT_NOT_OBSERVED", (await service.ConfirmAsync(f.HostId, f.MatchId, initial.Data.DecisionId, initialReceipt)).Message);
        V02AppendWindow(f, events, AEDV02Plan.Normal(), 1, 2401, Now, 1);
        Assert.True((await service.ConfirmAsync(f.HostId, f.MatchId, initial.Data.DecisionId, initialReceipt)).IsSuccess);

        var source = await verifier.VerifyAsync(f.MatchId, sourceRequest);
        Assert.True(source.IsSuccess, source.Message);
        Assert.NotEmpty(source.Data!.RecentDownUsers);
        var plan2 = AEDV02Plan.Normal().With(AEDV02Key.DetectionAcquireSeconds, 1.5);
        var request2 = V02Request(f, scenario, sourceRequest.SceneManifestFingerprint, plan2, 1, source.Data, 1);
        var approval2 = await service.SubmitAsync(f.HostId, f.MatchId, request2);
        Assert.True(approval2.IsSuccess, approval2.Message);
        Assert.Equal("APPROVED", approval2.Data!.ApprovalStatus);
        Assert.Equal(2, approval2.Data.PlanRevision);
        var effect2 = V02Effect(f, approval2.Data, "StalkerFusionRuntime", AEDV02Key.DetectionAcquireSeconds, 2401, events.Count + 1, Now);
        events.Add(effect2);
        var receipt2 = new AEDV02ReceiptRequest(Guid.NewGuid(), effect2.Id, 2, plan2.Fingerprint(), 2401);
        Assert.Equal("HOLD_WORLD_EFFECT_NOT_OBSERVED", (await service.ConfirmAsync(f.HostId, f.MatchId, request2.DecisionId, receipt2)).Message);
        V02AppendWindow(f, events, plan2, 2, 2401, Now, 1);
        Assert.True((await service.ConfirmAsync(f.HostId, f.MatchId, request2.DecisionId, receipt2)).IsSuccess);
        Assert.True((await service.ConfirmAsync(f.HostId, f.MatchId, request2.DecisionId, receipt2)).Data!.IsReplay);
        Assert.Equal("AED_V02_RECEIPT_IDENTITY_CONFLICT", (await service.ConfirmAsync(f.HostId, f.MatchId, request2.DecisionId, receipt2 with { ReceiptId = Guid.NewGuid() })).Message);

        clock.UtcNow = Now.AddSeconds(31);
        V02AppendWindow(f, events, plan2, 2, 2460, Now.AddSeconds(1), 31);
        var secondSource = await verifier.VerifyAsync(f.MatchId, sourceRequest with { StartTick = 2460, EndTick = 4260 });
        Assert.True(secondSource.IsSuccess, secondSource.Message);
        var plan3 = plan2.With(AEDV02Key.PostChaseCooldownSeconds, 28);
        var request3 = V02Request(f, scenario, sourceRequest.SceneManifestFingerprint, plan3, 2, secondSource.Data!, 2);
        var approval3 = await service.SubmitAsync(f.HostId, f.MatchId, request3);
        Assert.True(approval3.IsSuccess, approval3.Message);
        Assert.Equal("APPROVED", approval3.Data!.ApprovalStatus);
        Assert.Equal(3, approval3.Data.PlanRevision);
        Assert.Equal(2, approval3.Data.WindowSequence);
        Assert.Equal(approval2.Data.PhaseOrdinal, approval3.Data.PhaseOrdinal);
        Assert.Equal("HOLD_PREVIOUS_RECEIPT_PENDING", (await service.SubmitAsync(f.HostId, f.MatchId,
            request3 with { DecisionId = Guid.NewGuid(), WindowSequence = 3 })).Message);
        var effect3 = V02Effect(f, approval3.Data, "StalkerFusionRuntime", AEDV02Key.PostChaseCooldownSeconds, 4261, events.Count + 1, clock.UtcNow);
        events.Add(effect3);
        V02AppendWindow(f, events, plan3, 3, 4261, clock.UtcNow, 1);
        var receipt3 = new AEDV02ReceiptRequest(Guid.NewGuid(), effect3.Id, 3, plan3.Fingerprint(), 4261);
        Assert.True((await service.ConfirmAsync(f.HostId, f.MatchId, request3.DecisionId, receipt3)).IsSuccess);
        Assert.Equal("ABORT_STALE_SOURCE_REVISION", (await service.SubmitAsync(f.HostId, f.MatchId,
            request3 with { DecisionId = Guid.NewGuid(), ExpectedPlanRevision = 3, PreviousPlanFingerprint = plan3.Fingerprint(), WindowSequence = 3 })).Message);
    }

    private static AEDV02DecisionRequest V02Request(Fixture f, ScenarioDecisionResponse scenario, string manifest, AEDV02Plan plan,
        int revision, AEDV02SourceResult? source, int sequence)
    {
        var point = source == null ? AEDV02DecisionPoint.PRE_MATCH : AEDV02DecisionPoint.SAFE_WINDOW;
        return new AEDV02DecisionRequest(Guid.NewGuid(), source?.PhaseOrdinal ?? 0, point.ToString(), sequence,
            AEDV02Plan.WindowId(f.MatchId, source?.PhaseOrdinal ?? 0, point, sequence, source?.StartTick ?? 0, source?.EndTick ?? 0, source?.Fingerprint ?? string.Empty),
            revision, source?.PlanFingerprint ?? AEDV02Plan.Normal().Fingerprint(), plan.ToArray(), scenario.SnapshotId,
            scenario.SnapshotContentFingerprint, scenario.RosterIdentity, source?.Fingerprint ?? string.Empty,
            source?.StartTick ?? 0, source?.EndTick ?? 0, manifest, AEDV02PilotPolicy.Version, AEDV02Plan.CatalogVersion, "Adaptive", "Normal", source?.PhaseName ?? "CORE_COLLECTION");
    }
    private static TelemetryEventDocument[] V02PressureTrace(Fixture f, TelemetryEventDocument[] events)
    {
        var user = f.Db.MatchPlayerBindings.First(x => x.MatchId == f.MatchId).UserId;
        var reviver = f.Db.MatchPlayerBindings.First(x => x.MatchId == f.MatchId && x.UserId != user).UserId;
        var trace = events.ToList();
        foreach (var item in trace.Where(x => x.EventType == "AED_V02_WORLD_SAMPLE"))
        {
            var sample = JsonSerializer.Deserialize<AEDV02WorldSample>(item.ValueJson["data"]["world"].ToJson())!;
            sample.Stalkers = new[] { new AEDV02ThreatSource { NetworkId = "stalker:1", Zone = 1,
                State = item.Ts < Now.AddSeconds(-10) ? "CHASE" : "SEARCH", EpisodeId = "chase:1",
                TargetUserId = item.Ts < Now.AddSeconds(-10) ? user : null } };
            sample.Players.Single(x => x.UserId == user).LifeState = item.Ts >= Now.AddSeconds(-10) && item.Ts < Now.AddSeconds(-5) ? "DOWNED" : "ALIVE";
            if (item.Ts < Now.AddSeconds(-5))
            {
                sample.Players.Single(x => x.UserId == reviver).HeldToolInstanceId = "loadout:aid";
                sample.Tools = sample.Tools.Append(new AEDV02ToolSource { InstanceId = "loadout:aid", ToolId = 3, Zone = 0, Location = "Player", Origin = "LobbyLoadout", OwnerUserId = reviver }).ToArray();
            }
            item.ValueJson["data"]["world"] = BsonDocument.Parse(JsonSerializer.Serialize(sample));
        }
        trace.Add(V02Event(f.MatchId, "TEAM_TOOL_LIFECYCLE", 0, Now.AddSeconds(-32), "1.2", new BsonDocument {
            ["context"] = new BsonDocument { ["phase"] = "CORE_COLLECTION", ["authorityTick"] = 598 },
            ["data"] = new BsonDocument { ["action"] = "LOADOUT", ["instanceId"] = "loadout:aid", ["toolId"] = 3, ["zone"] = 0, ["origin"] = "LobbyLoadout", ["ownerUserId"] = reviver.ToString("D") } }));
        trace.Add(V02Event(f.MatchId, "PLAYER_DOWNED", 0, Now.AddSeconds(-10), "1.1", new BsonDocument { ["context"] = new BsonDocument { ["phase"] = "CORE_COLLECTION", ["authorityTick"] = 1800 }, ["data"] = new BsonDocument() }, user));
        trace.Add(V02Event(f.MatchId, "TEAM_TOOL_LIFECYCLE", 0, Now.AddSeconds(-5), "1.2", new BsonDocument {
            ["context"] = new BsonDocument { ["phase"] = "CORE_COLLECTION", ["authorityTick"] = 2100 },
            ["data"] = new BsonDocument { ["action"] = "CONSUME", ["instanceId"] = "loadout:aid", ["toolId"] = 3, ["zone"] = 0, ["remainingCharges"] = 0, ["ownerUserId"] = reviver.ToString("D") } }));
        trace.Add(V02Event(f.MatchId, "PLAYER_REVIVED", 0, Now.AddSeconds(-5), "1.1", new BsonDocument { ["context"] = new BsonDocument { ["phase"] = "CORE_COLLECTION", ["authorityTick"] = 2100 },
            ["data"] = new BsonDocument { ["reviverPlayerId"] = f.Db.MatchPlayerBindings.First(x => x.MatchId == f.MatchId && x.UserId != user).UserId.ToString("D"), ["usedFirstAidKit"] = true } }, user));
        return trace.OrderBy(x => x.Ts).ThenBy(x => x.EventType == "AED_V02_WORLD_SAMPLE" ? 1 : 0).Select((x, i) => V02Event(f.MatchId, x.EventType, i + 1, x.Ts, x.SchemaVersion, x.ValueJson, x.UserId)).ToArray();
    }
    private static TelemetryEventDocument V02Effect(Fixture f, AEDV02DecisionResult approval, string consumer, AEDV02Key key, long tick, long sequence, DateTime time)
    {
        var effect = new AEDV02EffectSource { DecisionId = approval.DecisionId, PlanRevision = approval.PlanRevision,
            PlanFingerprint = approval.PlanFingerprint, Consumer = consumer, Key = key.ToString(), ObservedValue = new AEDV02Plan(approval.PlanValues).Get(key),
            ObservedTick = tick, NetworkObjectIds = consumer == "StalkerFusionRuntime" ? new[] { "stalker:1" } : Array.Empty<string>() };
        return V02Event(f.MatchId, "AED_V02_ACTION_EFFECT", sequence, time, "1.3", BsonDocument.Parse(JsonSerializer.Serialize(new
            { context = new { phase = "CORE_COLLECTION", authorityTick = tick }, data = new { effect } })));
    }
    private static void V02AppendWindow(Fixture f, List<TelemetryEventDocument> events, AEDV02Plan plan, int revision, long startTick, DateTime started, int count)
    {
        var previous = events.Last(x => x.EventType == "AED_V02_WORLD_SAMPLE");
        var user = f.Db.MatchPlayerBindings.First(x => x.MatchId == f.MatchId).UserId;
        var reviver = f.Db.MatchPlayerBindings.First(x => x.MatchId == f.MatchId && x.UserId != user).UserId;
        if (count > 1) events.Add(V02Event(f.MatchId, "TEAM_TOOL_LIFECYCLE", events.Count + 1, started, "1.2", new BsonDocument {
            ["context"] = new BsonDocument { ["phase"] = "CORE_COLLECTION", ["authorityTick"] = startTick },
            ["data"] = new BsonDocument { ["action"] = "PICKUP", ["instanceId"] = "tool:3", ["toolId"] = 3, ["zone"] = 1, ["ownerUserId"] = reviver.ToString("D") } }));
        for (int i = 0; i < count; i++)
        {
            var sample = JsonSerializer.Deserialize<AEDV02WorldSample>(previous.ValueJson["data"]["world"].ToJson())!;
            sample.Tick = startTick + i * 60; sample.PlanRevision = revision; sample.PlanFingerprint = plan.Fingerprint();
            sample.Stalkers[0].State = count > 1 && i < 20 ? "CHASE" : "SEARCH";
            sample.Stalkers[0].EpisodeId = "chase:2"; sample.Stalkers[0].TargetUserId = count > 1 && i < 20 ? user : null;
            if (count > 1)
            {
                sample.Players.Single(x => x.UserId == reviver).HeldToolInstanceId = i < 25 ? "tool:3" : string.Empty;
                if (i < 25)
                {
                    var aid = sample.Tools.Single(x => x.InstanceId == "tool:3"); aid.Location = "Player"; aid.OwnerUserId = reviver; aid.MarkerId = string.Empty;
                }
                else sample.Tools = sample.Tools.Where(x => x.InstanceId != "tool:3").ToArray();
            }
            if (i == 25) events.Add(V02Event(f.MatchId, "TEAM_TOOL_LIFECYCLE", events.Count + 1, started.AddSeconds(i), "1.2", new BsonDocument {
                ["context"] = new BsonDocument { ["phase"] = "CORE_COLLECTION", ["authorityTick"] = sample.Tick },
                ["data"] = new BsonDocument { ["action"] = "CONSUME", ["instanceId"] = "tool:3", ["toolId"] = 3, ["zone"] = 1, ["remainingCharges"] = 0, ["ownerUserId"] = reviver.ToString("D") } }));
            if (i == 20 || i == 25) events.Add(V02Event(f.MatchId, i == 20 ? "PLAYER_DOWNED" : "PLAYER_REVIVED", events.Count + 1,
                started.AddSeconds(i), "1.1", new BsonDocument { ["context"] = new BsonDocument { ["phase"] = "CORE_COLLECTION", ["authorityTick"] = sample.Tick },
                    ["data"] = i == 25 ? new BsonDocument { ["reviverPlayerId"] = f.Db.MatchPlayerBindings.First(x => x.MatchId == f.MatchId && x.UserId != user).UserId.ToString("D"), ["usedFirstAidKit"] = true } : new BsonDocument() }, user));
            sample.Players.Single(x => x.UserId == user).LifeState = i >= 20 && i < 25 ? "DOWNED" : "ALIVE";
            events.Add(V02Event(f.MatchId, "AED_V02_WORLD_SAMPLE", events.Count + 1, started.AddSeconds(i), "1.3", BsonDocument.Parse(JsonSerializer.Serialize(new
                { context = new { phase = "CORE_COLLECTION", authorityTick = sample.Tick, occurrenceKey = "world:" + sample.Tick }, data = new { world = sample } }))));
        }
    }
    private sealed class MutableV02Clock(DateTime value) : TimeProvider
    { public DateTime UtcNow = value; public override DateTimeOffset GetUtcNow() => new(UtcNow); }
    private sealed class MutableV02Repository(List<TelemetryEventDocument> events) : ITelemetryEventRepository
    {
        public Task<IReadOnlyList<TelemetryEventDocument>> LoadAcceptedMatchEventsAsync(Guid matchId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<TelemetryEventDocument>>(events.Where(x => x.MatchId == matchId).ToArray());
        public Task EnsureIndexesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<TelemetryWriteResult> AtomicCommitBatchAsync(IReadOnlyCollection<TelemetryEventDocument> items, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TelemetryWriteResult> InsertBatchAsync(IReadOnlyCollection<TelemetryEventDocument> items, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyDictionary<Guid, TelemetryWriteItemResult>> LoadConflictsAsync(IReadOnlyCollection<TelemetryEventDocument> items, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyDictionary<Guid, TelemetryMatchBoundary>> LoadMatchBoundariesAsync(IReadOnlyCollection<Guid> matchIds, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
