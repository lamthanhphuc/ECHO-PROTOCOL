using System;
using EchoProtocol.AI.AED;
using NUnit.Framework;

public sealed class AEDPhase8AuthoritySourceCollectorTests
{
    [Test]
    public void UnknownTool_IsRejected()
    {
        var collector = new AEDPhase8AuthoritySourceCollectorV1();
        collector.BeginMatch(Guid.NewGuid());
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            collector.RecordToolSpawn("tool-1", 99, 1, 1, "spawn-1"));
    }

    [Test]
    public void MinionCannotDespawnFromWrongZone()
    {
        var collector = new AEDPhase8AuthoritySourceCollectorV1();
        collector.BeginMatch(Guid.NewGuid());
        collector.RecordMinionSpawn("minion-1", 1, 10, "minion-spawn");
        Assert.Throws<InvalidOperationException>(() =>
            collector.RecordMinionDespawn("minion-1", 2, 11, "minion-despawn"));
    }

    [Test]
    public void MinionCannotSpawnTwice()
    {
        var collector = new AEDPhase8AuthoritySourceCollectorV1();
        collector.BeginMatch(Guid.NewGuid());
        collector.RecordMinionSpawn("minion-1", 1, 10, "spawn-1");
        collector.RecordMinionDespawn("minion-1", 1, 20, "despawn-1");
        Assert.Throws<InvalidOperationException>(() =>
            collector.RecordMinionSpawn("minion-1", 1, 30, "spawn-2"));
    }

    [Test]
    public void IncompleteCoverage_StaysFailClosed()
    {
        var collector = new AEDPhase8AuthoritySourceCollectorV1();
        var matchId = Guid.NewGuid();
        collector.BeginMatch(matchId);
        Assert.True(collector.TryFreeze(matchId, 1, out var snapshot));
        Assert.False(snapshot.CoverageComplete);
        Assert.AreEqual(matchId, snapshot.MatchId);
        Assert.AreEqual(1u, snapshot.PhaseOrdinal);
    }
}
