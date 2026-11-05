using System;
using EchoProtocol.AI.Common.AED;
using NUnit.Framework;

public sealed class AEDV02NavGeometryTests
{
    [TestCase("OPEN", true)]
    [TestCase("LOCKED", false)]
    [TestCase("MISSING", false)]
    public void AEDV02_DoorFootprintInsideOneTriangleCannotBypassDoorState(string state, bool expected)
    {
        var manifest = Manifest();
        manifest.DoorIds = new[] { "gate" };
        manifest.NavCells[0].DoorIds = new[] { "gate" };
        var doors = state == "MISSING" ? new[] { new AEDV02DoorSource { DoorId = "door", State = "OPEN" } } :
            new[] { new AEDV02DoorSource { DoorId = "door", State = "OPEN" }, new AEDV02DoorSource { DoorId = "gate", State = state } };
        Assert.That(manifest.HasReachableGeometry(new[] { 1d, 0, 1 }, new[] { 2d, 0, 1 }, doors), Is.EqualTo(expected));
    }

    [Test]
    public void AEDV02_TriangleClippingPreservesRealDoorFootprintAndRejectsOtherFloorOrBoundingBoxOnlyOverlap()
    {
        var cell = Manifest().NavCells[0];
        Assert.That(cell.IntersectsBounds(new[] { 1d, -.1, 1 }, new[] { 2d, .1, 2 }), Is.True);
        Assert.That(cell.IntersectsBounds(new[] { 1d, 2, 1 }, new[] { 2d, 3, 2 }), Is.False);
        Assert.That(cell.IntersectsBounds(new[] { 8d, -.1, 8 }, new[] { 9d, .1, 9 }), Is.False);
        Assert.That(cell.IntersectsBounds(new[] { double.NaN, 0, 0 }, new[] { 1d, 1, 1 }), Is.False);
    }
    private static AEDV02SceneManifest Manifest() => new AEDV02SceneManifest
    {
        SceneVersion = "NAV_TEST", Markers = new[] { new AEDV02SceneMarker { Id = "Z1_ENTRY", Zone = 1, Kind = "ENTRY" } },
        NavCells = new[] {
            new AEDV02NavCell { Id = 0, A = new[] { 0d, 0, 0 }, B = new[] { 10d, 0, 0 }, C = new[] { 0d, 0, 10 } },
            new AEDV02NavCell { Id = 1, A = new[] { 10d, 0, 0 }, B = new[] { 10d, 0, 10 }, C = new[] { 0d, 0, 10 } }
        },
        NavLinks = new[] { new AEDV02NavLink { From = 0, To = 1, DoorIds = new[] { "door" } }, new AEDV02NavLink { From = 1, To = 0, DoorIds = new[] { "door" } } }
    };

    [TestCase("OPEN", true)]
    [TestCase("CLOSED_UNLOCKED", true)]
    [TestCase("BROKEN", true)]
    [TestCase("LOCKED", false)]
    [TestCase("MISSING", false)]
    public void DoorStateChangesIndependentReachability(string state, bool expected)
    {
        var manifest = Manifest(); Assert.That(manifest.IsValid(), Is.True);
        var doors = state == "MISSING" ? Array.Empty<AEDV02DoorSource>() : new[] { new AEDV02DoorSource { DoorId = "door", State = state } };
        Assert.That(manifest.HasReachableGeometry(new[] { 1d, 0, 1 }, new[] { 9d, .5, 9 }, doors), Is.EqualTo(expected));
    }

    [Test]
    public void ClaimedPositionOutsideMeshOrOnAnotherFloorIsNotReachable()
    {
        var manifest = Manifest(); var doors = new[] { new AEDV02DoorSource { DoorId = "door", State = "OPEN" } };
        Assert.That(manifest.HasReachableGeometry(new[] { 1d, 0, 1 }, new[] { 15d, 0, 9 }, doors), Is.False);
        Assert.That(manifest.HasReachableGeometry(new[] { 1d, 0, 1 }, new[] { 9d, 3, 9 }, doors), Is.False);
        Assert.That(manifest.HasReachableGeometry(new[] { 1d, 0, 1 }, new[] { double.NaN, 0, 9 }, doors), Is.False);
    }

    [Test]
    public void KnownBlockedRouteIsDifferentFromMissingProof()
    {
        var manifest = Manifest(); var from = new[] { 1d, 0, 1 }; var to = new[] { 9d, 0, 9 };
        Assert.That(manifest.TryReachableGeometry(from, to, new[] { new AEDV02DoorSource { DoorId = "door", State = "LOCKED" } }, out var reachable), Is.True);
        Assert.That(reachable, Is.False);
        Assert.That(manifest.TryReachableGeometry(from, to, Array.Empty<AEDV02DoorSource>(), out _), Is.False);
        Assert.That(manifest.TryReachableGeometry(from, new[] { 9d, 3, 9 }, new[] { new AEDV02DoorSource { DoorId = "door", State = "OPEN" } }, out _), Is.False);
    }

    [Test]
    public void ForgedPortalBetweenDisconnectedTrianglesMakesManifestInvalid()
    {
        var manifest = Manifest(); manifest.NavCells[1].A[0] = 100;
        Assert.That(manifest.IsValid(), Is.False);
    }
}
