using System;
using System.Linq;
using EchoProtocol.AI.AED;
using EchoProtocol.Telemetry;
using NUnit.Framework;

public sealed class AEDPhase8SourceUploadBuilderTests
{
    [Test]
    public void CompleteLocalReceipts_BuildTypedUpload()
    {
        var (matchId, snapshot, references) = Fixture();
        Assert.True(AEDPhase8SourceUploadBuilderV1.TryBuild(matchId, 1, new string('a', 64), new string('b', 64), references, snapshot, out var request, out var reason));
        Assert.AreEqual(string.Empty, reason);
        Assert.AreEqual(AEDPhase8SourceUploadBuilderV1.SourceVersion, request.sourceVersion);
        Assert.AreEqual(3, request.sourceEvents.Length);
        Assert.AreEqual(2, request.supply.Length);
        Assert.AreEqual(1, request.minions.Length);
        Assert.False(snapshot.CoverageComplete);
        Assert.True(request.supply.All(x => !x.reachable));
    }

    [TestCase("missing-tool")]
    [TestCase("missing-minion")]
    [TestCase("forged-receipt")]
    public void IncompleteLocalReceipts_RejectUpload(string corruption)
    {
        var (matchId, snapshot, references) = Fixture();
        switch (corruption)
        {
            case "missing-tool": references = references.Where(x => x.OccurrenceKey != "pickup").ToArray(); break;
            case "missing-minion": references = references.Where(x => x.OccurrenceKey != "minion-spawn").ToArray(); break;
            case "forged-receipt": references = references.Concat(new[] { new TelemetrySourceIdentity(Guid.NewGuid(), 9, TelemetryEventTypes.TeamToolLifecycle, "forged") }).ToArray(); break;
        }
        Assert.False(AEDPhase8SourceUploadBuilderV1.TryBuild(matchId, 1, new string('a', 64), new string('b', 64), references, snapshot, out _, out var reason));
        Assert.AreEqual("P8_SOURCE_LOCAL_COVERAGE_MISMATCH", reason);
    }

    [Test]
    public void WrongPhaseSnapshot_RejectUpload()
    {
        var (matchId, snapshot, references) = Fixture();
        Assert.False(AEDPhase8SourceUploadBuilderV1.TryBuild(matchId, 2, new string('a', 64), new string('b', 64), references, snapshot, out _, out _));
    }

    private static (Guid MatchId, AEDPhase8AuthoritySourceSnapshotV1 Snapshot, TelemetrySourceIdentity[] References) Fixture()
    {
        var matchId = Guid.NewGuid();
        var owner = Guid.NewGuid().ToString("D");
        var collector = new AEDPhase8AuthoritySourceCollectorV1();
        collector.BeginMatch(matchId);
        collector.RecordToolSpawn("tool-1", 2, 1, 10, "spawn");
        collector.RecordMinionSpawn("minion-1", 1, 15, "minion-spawn");
        collector.RecordToolTransition("tool-1", "Player", owner, 3, 20, "pickup");
        Assert.True(collector.TryFreeze(matchId, 1, out var snapshot));
        var references = new[]
        {
            new TelemetrySourceIdentity(Guid.NewGuid(), 2, TelemetryEventTypes.TeamToolLifecycle, "spawn"),
            new TelemetrySourceIdentity(Guid.NewGuid(), 3, TelemetryEventTypes.MinionLifecycle, "minion-spawn"),
            new TelemetrySourceIdentity(Guid.NewGuid(), 4, TelemetryEventTypes.TeamToolLifecycle, "pickup")
        };
        return (matchId, snapshot, references);
    }
}
