using EchoProtocol.AI.Common.AED;
using Xunit;

namespace EchoProtocol.Api.Tests;

public sealed class AEDV02PlacementContractTests
{
    [Theory]
    [InlineData("valid", true)]
    [InlineData("carried-item", true)]
    [InlineData("unknown-spacing", false)]
    [InlineData("spacing", false)]
    [InlineData("existing-world-item", false)]
    [InlineData("locked-door", false)]
    [InlineData("missing-door", false)]
    [InlineData("other-floor", false)]
    [InlineData("wrong-role", false)]
    [InlineData("missing-required-type", false)]
    [InlineData("already-materialized-zone", false)]
    public void ProposedGradeMustIndependentlyPreserveTypesSpacingAndDoorAwareRoutes(string mode, bool accepted)
    {
        int[] types = [1, 2, 3, 4, 6];
        var markers = types.Select((type, index) => new AEDV02SceneMarker { Id = AEDV02Plan.Hash("placement:" + type),
            Zone = 2, Kind = "TOOL", ResourceRole = "MainRoute", AllowedToolIds = [type], X = 5 + index * 5, Z = 49 }).ToArray();
        var manifest = new AEDV02SceneManifest { SceneVersion = "placement-contract", MinimumToolSpacing = 4,
            Markers = markers.Append(new AEDV02SceneMarker { Id = "Z2_ENTRY", Zone = 2, Kind = "ENTRY", X = 1, Z = 1 }).ToArray(),
            NavCells = [new() { Id = 0, A = [0, 0, 0], B = [50, 0, 0], C = [0, 0, 50] },
                new() { Id = 1, A = [50, 0, 50], B = [0, 0, 50], C = [50, 0, 0] }],
            NavLinks = [new() { From = 0, To = 1, DoorIds = ["door"] }, new() { From = 1, To = 0, DoorIds = ["door"] }], DoorIds = ["door"] };
        var proposal = markers.Select(x => new AEDV02ProposedReservation { Zone = 2, ToolId = x.AllowedToolIds.Single(), MarkerId = x.Id }).ToArray();
        var world = new AEDV02WorldSample { Doors = [new() { DoorId = "door", State = "OPEN" }], Supply = new() {
            Reservations = markers.Select(x => new AEDV02SupplyReservation { Zone = 2, ToolId = x.AllowedToolIds.Single(), MarkerId = x.Id }).ToArray() } };
        switch (mode)
        {
            case "unknown-spacing": manifest.MinimumToolSpacing = -1; break;
            case "spacing": markers[1].X = markers[0].X + .1; break;
            case "existing-world-item": case "carried-item":
                world.Tools = [new() { InstanceId = "existing", ToolId = 3, Location = mode == "carried-item" ? "Player" : "World",
                    X = markers[0].X, Y = markers[0].Y, Z = markers[0].Z }]; break;
            case "locked-door": world.Doors[0].State = "LOCKED"; break;
            case "missing-door": world.Doors = []; break;
            case "other-floor": markers[0].Y = 100; break;
            case "wrong-role": markers[0].ResourceRole = "SideRoom"; break;
            case "missing-required-type": proposal[0].ToolId = 2; break;
            case "already-materialized-zone": world.Supply.SpawnedZones = [2]; break;
        }
        Assert.True(manifest.IsValid());
        Assert.Equal(accepted, AEDV02ResourcePlacement.ValidateProposed(manifest, world, proposal, 1));
    }

    [Theory]
    [InlineData(-1, "SideRoom", true)]
    [InlineData(-1, "MainRoute", false)]
    [InlineData(1, "MainRoute", true)]
    [InlineData(1, "SideRoom", false)]
    [InlineData(2, "SideRoom", true)]
    [InlineData(3, "FirstRoom", true)]
    [InlineData(3, "SideRoom", false)]
    public void PlacementUsesRegisteredRole(int grade, string role, bool allowed) =>
        Assert.Equal(allowed, AEDV02ResourcePlacement.AllowsRole(grade, role));

    [Fact]
    public void PlannerBacktracksToPreserveAllFiveRequiredTypes()
    {
        var candidates = Enumerable.Range(0, 5).Select(i => new AEDV02PlacementCandidate {
            Marker = new AEDV02SceneMarker { Id = AEDV02Plan.Hash("marker:" + i), Zone = 1, Kind = "TOOL", ResourceRole = "MainRoute",
                AllowedToolIds = i == 0 ? [1, 2] : i == 1 ? [1] : [i == 4 ? 6 : i + 1], X = i * 3 },
            Path = [[0, 0, 0], [i * 3, 0, 0]], PathMeters = i * 3
        }).ToArray();
        Assert.True(AEDV02ResourcePlacement.TryPlan(1, 1, [1, 2, 3, 4, 6], candidates, 2, out var plan));
        Assert.Equal(5, plan.Select(x => x.MarkerId).Distinct().Count());
        Assert.Equal(new[] { 1, 2, 3, 4, 6 }, plan.Select(x => x.ToolId).Order().ToArray());
        Assert.Equal(candidates[0].Marker.Id, plan.Single(x => x.ToolId == 2).MarkerId);
        Assert.False(AEDV02ResourcePlacement.TryPlan(1, 1, [1, 2, 3, 4, 6], candidates, 20, out _));
        Assert.False(AEDV02ResourcePlacement.TryPlan(1, -1, [1, 2, 3, 4, 6], candidates, 2, out _));
    }

    [Fact]
    public void SupportFingerprintBindsCauseRecipientMarkerTypeAndDecision()
    {
        var decision = Guid.NewGuid();
        var support = new AEDV02SupportAction { Kind = "EMERGENCY", UserId = Guid.NewGuid(), CauseEventId = Guid.NewGuid(),
            ToolId = 3, Zone = 1, MarkerId = AEDV02Plan.Hash("safe:marker") };
        Assert.True(support.IsValid());
        string expected = support.Fingerprint(decision);
        Assert.NotEqual(expected, support.Fingerprint(Guid.NewGuid()));
        support.CauseEventId = Guid.NewGuid(); Assert.NotEqual(expected, support.Fingerprint(decision));
        support.ToolId = 1; Assert.False(support.IsValid());
        support.ToolId = 3; support.MarkerId = "unknown"; Assert.False(support.IsValid());
    }

    [Fact]
    public void ShortObservedChaseQualifiesAtExitBoundaryAndDoesNotRepeatRecovery()
    {
        var recovery = new AEDV02LongChaseRecovery();
        recovery.Observe(true, 0); recovery.Observe(true, 19.9); recovery.Observe(false, 20);
        Assert.Equal(20 + AEDV02PilotPolicy.SeekRecoverySeconds, recovery.SeekPausedUntil);
        recovery.Observe(false, 30);
        Assert.Equal(20 + AEDV02PilotPolicy.SeekRecoverySeconds, recovery.SeekPausedUntil);
        recovery.Reset(); Assert.Equal(0, recovery.SeekPausedUntil);
    }
}
