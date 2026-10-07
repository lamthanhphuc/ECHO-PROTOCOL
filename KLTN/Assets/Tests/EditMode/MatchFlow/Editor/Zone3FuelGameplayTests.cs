using EchoProtocol.MatchFlow;
using EchoProtocol.Networking;
using NUnit.Framework;
using UnityEngine;

public sealed class Zone3FuelGameplayTests
{
    [Test]
    public void SupplyBudgetCoversLongestNonRepeatingRoutePlusBackup()
    {
        int segments = Zone3FuelRules.LongestSimpleRouteSegments();
        Assert.That(segments, Is.EqualTo(11));
        int cells = Zone3FuelRules.RequiredCells(segments);
        Assert.That(cells, Is.EqualTo(6));
        Assert.That(2 + (cells - 1) * 2, Is.GreaterThanOrEqualTo(segments));
    }

    [TestCase(0, true, true, true, true)]
    [TestCase(1, true, true, true, false)]
    [TestCase(2, true, true, true, false)]
    [TestCase(0, false, true, true, false)]
    [TestCase(0, true, false, true, false)]
    [TestCase(0, true, true, false, false)]
    public void RefuelRequiresEmptyReserveCellAlivePlayerAndRange(int fuel, bool cell, bool alive, bool near, bool expected)
    {
        Assert.That(Zone3FuelRules.CanRefuel(fuel, cell, alive, near), Is.EqualTo(expected));
    }

    [Test]
    public void FuelOnlyDecreasesOnArrivalAndStopsAtZero()
    {
        var existingRoute = GameObject.Find("Zone3ConvoyRoute");
        if (existingRoute != null) existingRoute.SetActive(false);
        var root = new GameObject("FuelTestRoute");
        var ship = new GameObject("FuelTestShip");
        try
        {
            foreach (var point in Zone3ConvoyRouteGraph.OrderedPoints)
            {
                var go = new GameObject(Zone3ConvoyRouteGraph.GetSceneObjectName(point));
                go.transform.SetParent(root.transform);
                go.transform.position = Vector3.right * ((int)point * 10f);
            }
            var convoy = ship.AddComponent<Zone3ConvoyController>();
            convoy.BindForZone3();
            convoy.ApplyReplicatedState(true, 2, Zone3ConvoyRoutePoint.Point00, Zone3ConvoyRoutePoint.Point01, true, false);
            convoy.TickAuthoritative(2f);
            Assert.That(convoy.FuelPointsRemaining, Is.EqualTo(2), "Pausing without an escort must not consume fuel.");
            ship.transform.position = Vector3.right * 10f;
            convoy.TickAuthoritative(0.1f);
            Assert.That(convoy.FuelPointsRemaining, Is.EqualTo(1));
            convoy.TickAuthoritative(0.1f);
            Assert.That(convoy.FuelPointsRemaining, Is.EqualTo(1), "Same arrival must not be charged twice.");
            Assert.That(convoy.SelectNextPoint(Zone3ConvoyRoutePoint.Point02), Is.True);
            ship.transform.position = Vector3.right * 20f;
            convoy.TickAuthoritative(0.1f);
            Assert.That(convoy.FuelPointsRemaining, Is.Zero);
            Assert.That(convoy.SelectNextPoint(Zone3ConvoyRoutePoint.Point04), Is.False);
            var stoppedPosition = ship.transform.position;
            convoy.TickAuthoritative(10f);
            Assert.That(ship.transform.position, Is.EqualTo(stoppedPosition));
            convoy.Refuel();
            Assert.That(convoy.FuelPointsRemaining, Is.EqualTo(2));
            convoy.ApplyReplicatedState(true, 1, Zone3ConvoyRoutePoint.Point02, Zone3ConvoyRoutePoint.Point04, true, false);
            convoy.Refuel();
            Assert.That(convoy.FuelPointsRemaining, Is.EqualTo(1), "Nonempty reserve cannot stockpile cells.");
        }
        finally
        {
            Object.DestroyImmediate(ship);
            Object.DestroyImmediate(root);
            if (existingRoute != null) existingRoute.SetActive(true);
        }
    }
}
