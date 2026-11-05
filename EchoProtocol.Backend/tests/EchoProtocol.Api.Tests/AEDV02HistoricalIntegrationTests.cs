using System.Text.Json;
using EchoProtocol.AI.Common.AED;
using EchoProtocol.Api.Data.Telemetry;
using EchoProtocol.Api.DTOs.Scenarios;
using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services;
using EchoProtocol.Api.Configurations;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using Xunit;

namespace EchoProtocol.Api.Tests;

public sealed partial class ScenarioServiceTests
{
    [Theory, Trait("Category", "AEDV02")]
    [InlineData("valid", true)]
    [InlineData("prematch-apply", true)]
    [InlineData("overlap", true)]
    [InlineData("stored-outcomes-forged", true)]
    [InlineData("source-mutated", false)]
    [InlineData("missing-terminal", false)]
    [InlineData("wrong-context", false)]
    [InlineData("expired", false)]
    [InlineData("missing-seal", false)]
    [InlineData("debug-after-window", false)]
    [InlineData("reconnected", false)]
    [InlineData("aborted", false)]
    [InlineData("source-null-world", false)]
    [InlineData("source-malformed-world", false)]
    [InlineData("seal-wrong-type", false)]
    [InlineData("terminal-wrong-type", false)]
    [InlineData("stored-null-plan", false)]
    [InlineData("stored-malformed-json", false)]
    public async Task V02_HistoryReconstructsIngestedEndedMatchesAndDoesNotTrustStoredOutcomeClaims(string mode, bool ready)
    {
        await using var current = await ValidV02Fixture();
        var all = new List<TelemetryEventDocument>();
        string scene = string.Empty;
        for (int index = 0; index < 3; index++)
        {
            // Shared database, real users and independent match identities; the parent owns the connection.
            var match = await current.ForkMatchAsync();
            var (baseline, source) = await V02WorldEvidence(match, geometry: true);
            scene = source.SceneManifestFingerprint;
            var template = JsonSerializer.Deserialize<AEDV02WorldSample>(baseline.Last().ValueJson["data"]["world"].ToJson())!;
            var trace = baseline.Where(x => x.EventType != "AED_V02_WORLD_SAMPLE").Select(x => V02Event(match.MatchId, x.EventType, 0,
                Now.AddSeconds(x.EventType == "MATCH_STARTED" ? -121 : x.EventType == "PHASE_STARTED" ? -120 : -101),
                x.SchemaVersion, (BsonDocument)x.ValueJson.DeepClone(), x.UserId)).ToList();
            for (int second = 0; second <= 100; second++)
            {
                var world = JsonSerializer.Deserialize<AEDV02WorldSample>(JsonSerializer.Serialize(template))!;
                world.Tick = 600 + second * 60; world.OpportunityContractVersion = AEDV02PilotOpportunities.Version;
                int encounter = (second - 1) / 16;
                bool chase = second > 0 && encounter < 6 && (second - 1) % 16 < 5;
                world.Stalkers = [new AEDV02ThreatSource { NetworkId = "stalker", Zone = 1, State = chase ? "CHASE" : "SEARCH",
                    EpisodeId = chase ? "encounter:" + encounter : string.Empty,
                    TargetUserId = chase ? encounter % 2 == 0 ? current.HostId : current.PlayerId : null }];
                trace.Add(V02Event(match.MatchId, "AED_V02_WORLD_SAMPLE", 0, Now.AddSeconds(second - 100), "1.3",
                    BsonDocument.Parse(JsonSerializer.Serialize(new { context = new { phase = "CORE_COLLECTION", authorityTick = world.Tick }, data = new { world } }))));
            }
            trace.Add(V02Event(match.MatchId, "AED_V02_CAPTURE_SEAL", 0, Now.AddMilliseconds(500), "1.3", new BsonDocument {
                ["context"] = new BsonDocument { ["phase"] = "CORE_COLLECTION", ["authorityTick"] = 6601 },
                ["data"] = new BsonDocument { ["exclusion"] = mode == "debug-after-window" && index == 2 ? "DEBUG_GAMEPLAY" : "NONE" } }));
            bool aborted = mode == "aborted" && index == 2;
            trace.Add(V02Event(match.MatchId, "MATCH_ENDED", 0, Now.AddSeconds(1), "1.1", new BsonDocument {
                ["context"] = new BsonDocument { ["phase"] = "MATCH_END", ["authorityTick"] = 6660 },
                ["data"] = new BsonDocument { ["outcome"] = aborted ? "ABORTED" : "SUCCESS", ["durationSeconds"] = 122, ["survivorCount"] = 2 } },
                reasonCode: aborted ? "MATCH_ABORTED" : "TEAM_ESCAPED"));
            var ordered = trace.OrderBy(x => x.Ts).Select((x, ordinal) => V02Event(match.MatchId, x.EventType, ordinal + 1,
                x.Ts, x.SchemaVersion, x.ValueJson, x.UserId, x.ReasonCode)).ToArray();
            var accepted = await TelemetryServiceTests.IngestV02Trace(match.MatchId, ordered, Now.AddSeconds(1), V02TelemetryAuthority(match), current.HostId);
            var sealSequence = accepted.Single(x => x.EventType == "AED_V02_CAPTURE_SEAL").EventSequence;
            var verifier = new AEDV02SourceVerifier(current.Db, new AcceptedEvidenceRepository(accepted.Where(x => x.EventSequence < sealSequence).ToArray()), new FixedClock(Now));
            var request = new AEDV02SourceRequest(1, 600, 6600, scene);
            var proof = await verifier.VerifyAsync(match.MatchId, request);
            Assert.True(proof.IsSuccess, proof.Message);
            Assert.True(proof.Data!.PressureComplete);
            Assert.Equal(6, proof.Data.ResolvedOpportunities!.Count(x => x.Axis == "Stalker" && x.Resolution == "Success"));
            if (mode == "overlap") Assert.True((await verifier.VerifyAsync(match.MatchId, request with { StartTick = 1200 })).IsSuccess);
            if (mode == "debug-after-window" && index == 2)
                Assert.Equal("HOLD_SOURCE_CAPTURE_EXCLUDED", (await new AEDV02SourceVerifier(current.Db, new AcceptedEvidenceRepository(accepted),
                    new FixedClock(Now.AddSeconds(1))).VerifyAsync(match.MatchId, request)).Message);
            if (mode == "source-mutated" && index == 2)
                accepted.First(x => x.EventType == "AED_V02_WORLD_SAMPLE").ValueJson["data"]["world"]["EvidenceExclusion"] = "DEBUG";
            if (index == 2 && mode is "source-null-world" or "source-malformed-world")
                accepted.First(x => x.EventType == "AED_V02_WORLD_SAMPLE").ValueJson["data"]["world"] =
                    mode == "source-null-world" ? BsonNull.Value : new BsonString("not-a-world");
            if (mode == "seal-wrong-type" && index == 2)
                accepted.Single(x => x.EventType == "AED_V02_CAPTURE_SEAL").ValueJson["context"]["authorityTick"] = "not-a-tick";
            if (mode == "terminal-wrong-type" && index == 2)
                accepted.Single(x => x.EventType == "MATCH_ENDED").ValueJson["data"]["outcome"] = 123;
            all.AddRange(accepted.Where(x => !(index == 2 && (mode == "missing-terminal" && x.EventType == "MATCH_ENDED" ||
                mode == "missing-seal" && x.EventType == "AED_V02_CAPTURE_SEAL"))));
            if (mode == "reconnected" && index == 2)
            { var player = current.Db.MatchPlayerBindings.First(x => x.MatchId == match.MatchId); player.HadGameplayDisconnect = true; player.DisconnectedAtUtc = null; }
            current.Db.MatchAuthorityBindings.Single(x => x.MatchId == match.MatchId).Status = MatchAuthorityStatus.Ended;
        }
        foreach (var proof in current.Db.AEDV02VerifiedSources)
        {
            var reconstructed = JsonSerializer.Deserialize<AEDV02SourceResult>(proof.ReconstructedJson)!;
            if (mode == "stored-null-plan") reconstructed = reconstructed with { PlanFingerprint = null! };
            if (mode == "wrong-context") reconstructed = reconstructed with { ComparisonContext = AEDV02Plan.Hash("another-context") };
            if (mode == "stored-outcomes-forged") reconstructed = reconstructed with { ResolvedOpportunities = Enumerable.Range(0, 100)
                .Select(i => new AEDV02ResolvedOpportunity { Id = "forged:" + i, UserId = current.HostId, Axis = "Stalker",
                    StartTick = 1, EndTick = 100, Resolution = "Success" }).ToArray() };
            proof.ReconstructedJson = JsonSerializer.Serialize(reconstructed);
            if (mode == "stored-malformed-json") proof.ReconstructedJson = "{bad-json";
        }
        await current.Db.SaveChangesAsync();
        var history = await new AEDV02HistoricalCompetenceService(current.Db, new AcceptedEvidenceRepository(all),
            new FixedClock(mode == "expired" ? Now.AddDays(31) : Now.AddSeconds(2))).ReadAsync(current.MatchId,
                [current.HostId, current.PlayerId], scene, "Stalker");
        Assert.All(history.Values, value => Assert.Equal(ready, value.ReadyForIncrease));
        if (ready) Assert.All(history.Values, value => { Assert.Equal(3, value.Matches); Assert.Equal(9, value.Resolved); Assert.Equal(1, value.Score); });
        if (mode == "prematch-apply")
        {
            var scenario = (await current.Service.ResolvePreMatchAsync(current.HostId, current.MatchId, AdaptiveRequest())).Data!;
            var (world, source) = await V02WorldEvidence(current, geometry: true, planRevision: 1);
            Assert.Equal(scene, source.SceneManifestFingerprint);
            all.AddRange(world);
            var repository = new MutableV02Repository(all);
            var clock = new FixedClock(Now.AddSeconds(2));
            var service = new AEDV02DecisionService(current.Db, new AEDV02SourceVerifier(current.Db, repository, clock), repository,
                clock, Options.Create(new AEDV02PilotSettings { Enabled = true, CabinetAmbushEnabled = true }));
            current.Db.MatchAuthorityBindings.Single(x => x.MatchId == current.MatchId).Status = MatchAuthorityStatus.Lobby;
            await current.Db.SaveChangesAsync();
            var plan = AEDV02Plan.Normal().With(AEDV02Key.DetectionAcquireSeconds, 1).With(AEDV02Key.CabinetAmbushEnabled, 1);
            var request = V02Request(current, scenario, scene, plan, 0, null, 0);
            var approval = await service.SubmitAsync(current.HostId, current.MatchId, request);
            Assert.True(approval.IsSuccess, approval.Message);
            Assert.True(approval.Data!.ApprovalStatus == "APPROVED", approval.Data.ReasonCode);
            Assert.Equal(1, approval.Data.PlanRevision);
            current.Db.MatchAuthorityBindings.Single(x => x.MatchId == current.MatchId).Status = MatchAuthorityStatus.InMatch;
            await current.Db.SaveChangesAsync();
            // This exercises Backend reconstruction and commit/effect contracts, not native Unity execution.
            var currentEvents = world.ToList();
            var effect = V02Effect(current, currentEvents, approval.Data, "AEDPolicyEnvelope", AEDV02Key.CabinetAmbushEnabled,
                2400, currentEvents.Count + 1, Now.AddSeconds(2));
            currentEvents.Add(effect);
            V02AppendWindow(current, currentEvents, plan, 1, 2401, Now.AddSeconds(2), 1);
            var accepted = await TelemetryServiceTests.IngestV02Trace(current.MatchId, currentEvents, Now.AddSeconds(2), V02TelemetryAuthority(current), current.HostId);
            all.RemoveAll(x => x.MatchId == current.MatchId); all.AddRange(accepted);
            var receipt = new AEDV02ReceiptRequest(Guid.NewGuid(), effect.Id, 1, plan.Fingerprint(), 2400);
            var applied = await service.ConfirmAsync(current.HostId, current.MatchId, approval.Data.DecisionId, receipt);
            Assert.True(applied.IsSuccess, applied.Message);
            Assert.Equal("APPLIED", applied.Data!.ApplyStatus);
            Assert.True((await service.ConfirmAsync(current.HostId, current.MatchId, approval.Data.DecisionId, receipt)).Data!.IsReplay);
        }
    }
}
