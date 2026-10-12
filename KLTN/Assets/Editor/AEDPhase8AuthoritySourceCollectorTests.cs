using System;
using EchoProtocol.AI.AED;
using NUnit.Framework;

public sealed class AEDPhase8AuthoritySourceCollectorTests
{
    [Test]
    public void LatestTool_PreservesSpawnMetadata()
    {
        var collector = new AEDPhase8AuthoritySourceCollectorV1();
        var matchId = Guid.NewGuid();
        var owner = Guid.NewGuid().ToString("D");
        collector.BeginMatch(matchId);
        collector.RecordToolSpawn("tool-1", 2, 1, 10, "spawn-1");
        collector.RecordToolTransition("tool-1", "Player", owner, 2, 20, "pickup-20");

        Assert.True(collector.TryGetLatestTool("tool-1", out var tool));
        Assert.AreEqual(2, tool.ToolId);
        Assert.AreEqual(1, tool.Zone);
        Assert.AreEqual("Player", tool.Location);
        Assert.AreEqual(owner, tool.OwnerUserId);
        Assert.AreEqual(2, tool.RemainingCharges);
        Assert.AreEqual("pickup-20", tool.SourceOccurrenceKey);
    }

    [Test]
    public void DropAndRepickup_PreservesLineage()
    {
        var collector = new AEDPhase8AuthoritySourceCollectorV1();
        var matchId = Guid.NewGuid();
        var owner = Guid.NewGuid().ToString("D");
        collector.BeginMatch(matchId);
        collector.RecordToolSpawn("tool-original", 2, 1, 10, "spawn");
        collector.RecordToolTransition("tool-original", "Player", owner, 2, 20, "pickup");
        collector.RecordToolDrop("tool-dropped", 2, owner, 2, 30, "drop");

        Assert.True(collector.TryGetLatestTool("tool-dropped", out var dropped));
        Assert.AreEqual("PlayerDrop", dropped.Origin);
        Assert.AreEqual("tool-original", dropped.ParentInstanceId);
        Assert.AreEqual("World", dropped.Location);
        Assert.AreEqual(2, dropped.RemainingCharges);
        collector.RecordToolTransition("tool-dropped", "Player", owner, 2, 40, "repickup");
        Assert.True(collector.TryGetLatestTool("tool-dropped", out var repicked));
        Assert.AreEqual("tool-original", repicked.ParentInstanceId);
        Assert.True(collector.TryFreeze(matchId, 1, out var snapshot));
        Assert.False(snapshot.CoverageComplete);
    }

    [Test]
    public void DropWithoutVerifiedParent_IsRejected()
    {
        var collector = new AEDPhase8AuthoritySourceCollectorV1();
        collector.BeginMatch(Guid.NewGuid());
        Assert.Throws<InvalidOperationException>(() => collector.RecordToolDrop(
            "new-tool", 2, Guid.NewGuid().ToString("D"), 1, 30, "drop"));
    }

    [Test]
    public void TransferredParent_CannotBeReactivated()
    {
        var collector = new AEDPhase8AuthoritySourceCollectorV1();
        var owner = Guid.NewGuid().ToString("D");
        collector.BeginMatch(Guid.NewGuid());
        collector.RecordToolSpawn("parent", 2, 1, 10, "spawn");
        collector.RecordToolTransition("parent", "Player", owner, 3, 20, "pickup");
        collector.RecordToolDrop("child", 2, owner, 3, 30, "drop");

        var error = Assert.Throws<InvalidOperationException>(() => collector.RecordToolTransition(
            "parent", "Player", owner, 3, 40, "invalid-repickup"));
        Assert.AreEqual("AED_P8_TOOL_INSTANCE_TRANSFERRED", error.Message);

        collector.RecordToolTransition("child", "Player", owner, 3, 50, "valid-repickup");
        Assert.True(collector.TryGetLatestTool("child", out var source));
        Assert.AreEqual("parent", source.ParentInstanceId);
    }

    [Test]
    public void ToolUse_DecrementsChargesAndConsumesTerminal()
    {
        var collector = new AEDPhase8AuthoritySourceCollectorV1();
        var owner = Guid.NewGuid().ToString("D");
        collector.BeginMatch(Guid.NewGuid());
        collector.RecordToolSpawn("tool-1", 2, 1, 10, "spawn");
        collector.RecordToolTransition("tool-1", "Player", owner, 3, 20, "pickup");
        collector.RecordToolUse("tool-1", owner, 2, 2, false, 30, "use-1");
        collector.RecordToolUse("tool-1", owner, 2, 1, false, 40, "use-2");
        collector.RecordToolUse("tool-1", owner, 2, 0, true, 50, "consume");
        Assert.True(collector.TryGetLatestTool("tool-1", out var source));
        Assert.AreEqual("Consumed", source.Location);
        Assert.AreEqual(0, source.RemainingCharges);
        Assert.False(collector.TryGetHeldTool(owner, out _));
        collector.RecordToolSpawn("tool-2", 3, 1, 60, "spawn-2");
        collector.RecordToolTransition("tool-2", "Player", owner, null, 70, "pickup-2");
        Assert.True(collector.TryGetHeldTool(owner, out var next));
        Assert.AreEqual("tool-2", next.InstanceId);
    }

    [Test]
    public void ToolUse_RejectsChargeIncreaseAndWrongOwner()
    {
        var collector = new AEDPhase8AuthoritySourceCollectorV1();
        var owner = Guid.NewGuid().ToString("D");
        collector.BeginMatch(Guid.NewGuid());
        collector.RecordToolSpawn("tool-1", 4, 1, 10, "spawn");
        collector.RecordToolTransition("tool-1", "Player", owner, 3, 20, "pickup");
        Assert.Throws<InvalidOperationException>(() => collector.RecordToolUse("tool-1", owner, 4, 3, false, 30, "bad-use"));
        Assert.Throws<InvalidOperationException>(() => collector.RecordToolUse("tool-1", Guid.NewGuid().ToString("D"), 4, 2, false, 30, "wrong-owner"));
        Assert.True(collector.TryGetHeldTool(owner, out var source));
        Assert.AreEqual(3, source.RemainingCharges);
    }
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

    [Test]
    public void ValidMinionLifecycle_PreservesNetworkIdentity()
    {
        var collector = new AEDPhase8AuthoritySourceCollectorV1();
        var matchId = Guid.NewGuid();
        collector.BeginMatch(matchId);
        collector.RecordMinionSpawn("minion-1", 2, 10, "spawn-1");
        collector.RecordMinionDespawn("minion-1", 2, 20, "despawn-1");
        Assert.True(collector.TryFreeze(matchId, 1, out var snapshot));
        Assert.AreEqual(2, snapshot.Minions.Count);
        Assert.AreEqual("minion-1", snapshot.Minions[0].NetworkInstanceId);
        Assert.AreEqual("SPAWNED", snapshot.Minions[0].State);
        Assert.AreEqual("DESPAWNED", snapshot.Minions[1].State);
        Assert.False(snapshot.CoverageComplete);
    }

    [Test]
    public void DuplicateOccurrenceAcrossInstances_IsRejected()
    {
        var collector = new AEDPhase8AuthoritySourceCollectorV1();
        collector.BeginMatch(Guid.NewGuid());
        collector.RecordMinionSpawn("minion-1", 1, 10, "shared-key");
        Assert.Throws<InvalidOperationException>(() =>
            collector.RecordMinionSpawn("minion-2", 1, 11, "shared-key"));
    }

    [Test]
    public void ToolSpawnAndPickup_PreserveIdentityAndOwner()
    {
        var collector = new AEDPhase8AuthoritySourceCollectorV1();
        var matchId = Guid.NewGuid();
        var owner = Guid.NewGuid().ToString("D");
        collector.BeginMatch(matchId);
        collector.RecordToolSpawn("tool-1", 2, 1, 10, "tool-spawn-1");
        collector.RecordToolTransition(
            "tool-1", "Player", owner, 2, 20, "tool-pickup-1");
        Assert.True(collector.TryFreeze(matchId, 1, out var snapshot));
        Assert.AreEqual(2, snapshot.Tools.Count);
        Assert.AreEqual("tool-1", snapshot.Tools[0].InstanceId);
        Assert.AreEqual("World", snapshot.Tools[0].Location);
        Assert.AreEqual("Player", snapshot.Tools[1].Location);
        Assert.AreEqual(owner, snapshot.Tools[1].OwnerUserId);
        Assert.AreEqual(2, snapshot.Tools[1].RemainingCharges);
        Assert.False(snapshot.CoverageComplete);
    }

    [Test]
    public void ToolPickup_RejectsUnverifiedOwner()
    {
        var collector = new AEDPhase8AuthoritySourceCollectorV1();
        collector.BeginMatch(Guid.NewGuid());
        collector.RecordToolSpawn("tool-1", 1, 1, 10, "spawn-1");
        Assert.Throws<InvalidOperationException>(() =>
            collector.RecordToolTransition(
                "tool-1", "Player", "invalid", null, 20, "pickup-1"));
    }

    [Test]
    public void ToolPickup_RejectsMissingSpawn()
    {
        var collector = new AEDPhase8AuthoritySourceCollectorV1();
        collector.BeginMatch(Guid.NewGuid());
        Assert.Throws<InvalidOperationException>(() =>
            collector.RecordToolTransition(
                "missing-tool", "Player", Guid.NewGuid().ToString("D"),
                null, 20, "pickup-1"));
    }

    [Test]
    public void ToolOccurrence_CannotBeReused()
    {
        var collector = new AEDPhase8AuthoritySourceCollectorV1();
        collector.BeginMatch(Guid.NewGuid());
        collector.RecordToolSpawn("tool-1", 1, 1, 10, "shared-key");
        Assert.Throws<InvalidOperationException>(() =>
            collector.RecordToolSpawn("tool-2", 2, 1, 11, "shared-key"));
    }

    [Test]
    public void SceneToolSpawn_CanBePickedUpAndConsumed()
    {
        var collector = new AEDPhase8AuthoritySourceCollectorV1();
        var owner = Guid.NewGuid().ToString("D");
        collector.BeginMatch(Guid.NewGuid());
        collector.RecordSceneToolSpawn("scene-tool", 4, 10, "scene-spawn");
        Assert.True(collector.TryGetLatestTool("scene-tool", out var spawned));
        Assert.AreEqual("ScenePlaced", spawned.Origin);
        Assert.AreEqual(0, spawned.Zone);
        collector.RecordToolTransition("scene-tool", "Player", owner, 3, 20, "pickup");
        collector.RecordToolUse("scene-tool", owner, 4, 2, false, 30, "use-1");
        collector.RecordToolUse("scene-tool", owner, 4, 1, false, 40, "use-2");
        collector.RecordToolUse("scene-tool", owner, 4, 0, true, 50, "consume");
        Assert.False(collector.TryGetHeldTool(owner, out _));
    }

    [Test]
    public void SceneToolSpawn_DuplicateInstanceRejected()
    {
        var collector = new AEDPhase8AuthoritySourceCollectorV1();
        collector.BeginMatch(Guid.NewGuid());
        collector.RecordSceneToolSpawn("scene-tool", 2, 10, "scene-spawn");
        Assert.Throws<InvalidOperationException>(() =>
            collector.RecordToolSpawn("scene-tool", 2, 1, 20, "world-spawn"));
    }

    [Test]
    public void ScenePlacedJammer_PickupWithoutKnownCharges_IsRejected()
    {
        var collector = new AEDPhase8AuthoritySourceCollectorV1();
        var owner = Guid.NewGuid().ToString("D");
        collector.BeginMatch(Guid.NewGuid());
        collector.RecordSceneToolSpawn("scene-jammer", 4, 10, "spawn");
        var error = Assert.Throws<InvalidOperationException>(() => collector.RecordToolTransition(
            "scene-jammer", "Player", owner, null, 20, "pickup"));
        Assert.AreEqual("AED_P8_TOOL_PICKUP_CHARGES_INVALID", error.Message);
        Assert.False(collector.TryGetHeldTool(owner, out _));
    }

    [Test]
    public void MultiUseTool_RejectsSkippedChargeReceipt()
    {
        var collector = new AEDPhase8AuthoritySourceCollectorV1();
        var owner = Guid.NewGuid().ToString("D");
        collector.BeginMatch(Guid.NewGuid());
        collector.RecordToolSpawn("tool-1", 4, 1, 10, "spawn");
        collector.RecordToolTransition("tool-1", "Player", owner, 3, 20, "pickup");
        var error = Assert.Throws<InvalidOperationException>(() => collector.RecordToolUse(
            "tool-1", owner, 4, 1, false, 30, "skip-charge"));
        Assert.AreEqual("AED_P8_TOOL_USE_CHARGES_INVALID", error.Message);
        Assert.True(collector.TryGetHeldTool(owner, out var held));
        Assert.AreEqual(3, held.RemainingCharges);
    }

    [Test]
    public void LobbyLoadout_UseDropPickup_PreservesIdentity()
    {
        var collector = new AEDPhase8AuthoritySourceCollectorV1();
        var owner = Guid.NewGuid().ToString("D");
        collector.BeginMatch(Guid.NewGuid());
        collector.RecordLobbyToolLoadout("loadout-1", 2, owner, 3, 10, "loadout");
        Assert.True(collector.TryGetHeldTool(owner, out var held));
        Assert.AreEqual("LobbyLoadout", held.Origin);
        Assert.AreEqual("Player", held.Location);
        Assert.AreEqual(0, held.Zone);
        collector.RecordToolUse("loadout-1", owner, 2, 2, false, 20, "use");
        collector.RecordToolDrop("dropped-1", 2, owner, 2, 30, "drop");
        collector.RecordToolTransition("dropped-1", "Player", owner, 2, 40, "pickup");
        Assert.True(collector.TryGetHeldTool(owner, out var repicked));
        Assert.AreEqual("loadout-1", repicked.ParentInstanceId);
        Assert.AreEqual(2, repicked.RemainingCharges);
    }

    [Test]
    public void LobbyLoadout_DuplicateOwner_IsRejected()
    {
        var collector = new AEDPhase8AuthoritySourceCollectorV1();
        var owner = Guid.NewGuid().ToString("D");
        collector.BeginMatch(Guid.NewGuid());
        collector.RecordLobbyToolLoadout("loadout-1", 2, owner, 3, 10, "loadout-1");
        Assert.Throws<InvalidOperationException>(() => collector.RecordLobbyToolLoadout(
            "loadout-2", 4, owner, 3, 20, "loadout-2"));
        Assert.True(collector.TryGetHeldTool(owner, out var held));
        Assert.AreEqual("loadout-1", held.InstanceId);
    }

    [Test]
    public void Drop_RejectsChargeLossWithoutUse()
    {
        var collector = new AEDPhase8AuthoritySourceCollectorV1();
        var owner = Guid.NewGuid().ToString("D");
        collector.BeginMatch(Guid.NewGuid());
        collector.RecordToolSpawn("parent", 2, 1, 10, "spawn");
        collector.RecordToolTransition("parent", "Player", owner, 3, 20, "pickup");
        var error = Assert.Throws<InvalidOperationException>(() => collector.RecordToolDrop("child", 2, owner, 2, 30, "drop"));
        Assert.AreEqual("AED_P8_DROP_CHARGES_INVALID", error.Message);
        Assert.False(collector.TryGetLatestTool("child", out _));
        Assert.True(collector.TryGetHeldTool(owner, out _));
    }

    [Test]
    public void Drop_RejectsChargeIncrease()
    {
        var collector = new AEDPhase8AuthoritySourceCollectorV1();
        var owner = Guid.NewGuid().ToString("D");
        collector.BeginMatch(Guid.NewGuid());
        collector.RecordToolSpawn("parent", 4, 1, 10, "spawn");
        collector.RecordToolTransition("parent", "Player", owner, 2, 20, "pickup");
        Assert.Throws<InvalidOperationException>(() => collector.RecordToolDrop("child", 4, owner, 3, 30, "drop"));
    }

    [Test]
    public void Repickup_RejectsChangedCharges()
    {
        var collector = new AEDPhase8AuthoritySourceCollectorV1();
        var owner = Guid.NewGuid().ToString("D");
        collector.BeginMatch(Guid.NewGuid());
        collector.RecordToolSpawn("parent", 2, 1, 10, "spawn");
        collector.RecordToolTransition("parent", "Player", owner, 3, 20, "pickup");
        collector.RecordToolDrop("child", 2, owner, 3, 30, "drop");
        var error = Assert.Throws<InvalidOperationException>(() => collector.RecordToolTransition("child", "Player", owner, 2, 40, "repickup"));
        Assert.AreEqual("AED_P8_TOOL_PICKUP_CHARGES_INVALID", error.Message);
    }

    [Test]
    public void MultiUsePickup_RequiresCharges()
    {
        var collector = new AEDPhase8AuthoritySourceCollectorV1();
        var owner = Guid.NewGuid().ToString("D");
        collector.BeginMatch(Guid.NewGuid());
        collector.RecordToolSpawn("tool", 4, 1, 10, "spawn");
        Assert.Throws<InvalidOperationException>(() => collector.RecordToolTransition("tool", "Player", owner, null, 20, "pickup"));
    }

    [Test]
    public void SingleUsePickup_RejectsExtraCharges()
    {
        var collector = new AEDPhase8AuthoritySourceCollectorV1();
        var owner = Guid.NewGuid().ToString("D");
        collector.BeginMatch(Guid.NewGuid());
        collector.RecordToolSpawn("first-aid", 3, 1, 10, "spawn");
        Assert.Throws<InvalidOperationException>(() => collector.RecordToolTransition("first-aid", "Player", owner, 3, 20, "pickup"));
        collector.RecordToolTransition("first-aid", "Player", owner, null, 30, "valid-pickup");
        Assert.True(collector.TryGetHeldTool(owner, out _));
    }

    [Test]
    public void PhaseWindows_FilterRowsAndPreserveCrossPhaseLineage()
    {
        var collector = new AEDPhase8AuthoritySourceCollectorV1();
        var matchId = Guid.NewGuid();
        var owner = Guid.NewGuid().ToString("D");
        collector.BeginMatch(matchId);
        collector.RecordToolSpawn("tool-1", 2, 1, 10, "spawn-1");
        collector.RecordMinionSpawn("minion-1", 1, 11, "minion-spawn");
        collector.StartPhase(2);
        collector.RecordToolTransition("tool-1", "Player", owner, 3, 20, "pickup-2");
        collector.RecordMinionDespawn("minion-1", 1, 21, "minion-despawn");
        Assert.True(collector.TryFreeze(matchId, 1, out var first));
        Assert.True(collector.TryFreeze(matchId, 2, out var second));
        Assert.AreEqual(1, first.Tools.Count);
        Assert.AreEqual("spawn-1", first.Tools[0].SourceOccurrenceKey);
        Assert.AreEqual(1, first.Minions.Count);
        Assert.AreEqual(1, second.Tools.Count);
        Assert.AreEqual("pickup-2", second.Tools[0].SourceOccurrenceKey);
        Assert.AreEqual("Player", second.Tools[0].Location);
        Assert.AreEqual(1, second.Minions.Count);
        Assert.AreEqual("DESPAWNED", second.Minions[0].State);
        Assert.False(first.CoverageComplete);
        Assert.False(second.CoverageComplete);
    }

    [Test]
    public void PhaseWindows_RejectSkippedAndFutureOrdinals()
    {
        var collector = new AEDPhase8AuthoritySourceCollectorV1();
        var matchId = Guid.NewGuid();
        collector.BeginMatch(matchId);
        Assert.False(collector.TryFreeze(matchId, 2, out _));
        Assert.Throws<InvalidOperationException>(() => collector.StartPhase(3));
        collector.StartPhase(2);
        collector.StartPhase(2);
        Assert.Throws<InvalidOperationException>(() => collector.StartPhase(1));
        Assert.True(collector.TryFreeze(matchId, 2, out var snapshot));
        Assert.False(snapshot.CoverageComplete);
    }
}
