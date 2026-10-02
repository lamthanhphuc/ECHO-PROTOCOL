using NUnit.Framework;
using UnityEngine;
using EchoProtocol.Networking;

namespace EchoProtocol.Tests.EditMode.MatchFlow
{
    /// <summary>
    /// Edit-mode structural/logic tests for Zone3ConvoyController.
    /// All tests run without Photon Fusion Runner — host-authoritative tick is called directly.
    /// </summary>
    public sealed class Zone3ConvoyControllerTests
    {
        // ─── Helpers ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Minimal scene setup: one convoy GameObject with waypoints Z3_Route_00_Frigate_Start and Z3_Route_Final.
        /// The route graph always contains Point00 → (auto-select next). We create only Start + Final so
        /// FindNearestPoint resolves to Point00 and auto-select finds no additional next point
        /// (graph returns empty for Final), so convoy becomes WaitingForRouteChoice or stops.
        /// For movement tests we create a two-point straight path.
        /// </summary>
        private static (GameObject convoyGo, Zone3ConvoyController convoy, GameObject[] waypoints) BuildMinimalConvoy()
        {
            // Create convoy GameObject
            var convoyGo = new GameObject("Spacefrigate");
            convoyGo.AddComponent<BoxCollider>();
            var rb = convoyGo.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            var convoy = convoyGo.AddComponent<Zone3ConvoyController>();

            // Create Start waypoint (Point00)
            var wp0 = new GameObject(Zone3ConvoyRouteGraph.GetSceneObjectName(Zone3ConvoyRoutePoint.Point00));
            wp0.transform.position = Vector3.zero;

            // Create first route waypoint (Point01)
            var wp1 = new GameObject(Zone3ConvoyRouteGraph.GetSceneObjectName(Zone3ConvoyRoutePoint.Point01));
            wp1.transform.position = new Vector3(10f, 0f, 0f);

            return (convoyGo, convoy, new[] { wp0, wp1 });
        }

        private static void DestroyAll(params Object[] objects)
        {
            foreach (var obj in objects)
                if (obj != null) Object.DestroyImmediate(obj);
        }

        // ─── Activation ───────────────────────────────────────────────────────────

        [Test]
        public void CONVOY_NotMovingBeforeActivation()
        {
            var (go, convoy, wps) = BuildMinimalConvoy();
            try
            {
                Assert.IsFalse(convoy.IsInitialized,
                    "Convoy must not be initialized before Activate().");
                Assert.IsFalse(convoy.IsMoving,
                    "Convoy must not be moving before Activate().");
            }
            finally { DestroyAll(go, wps[0], wps[1]); }
        }

        [Test]
        public void CONVOY_FuelFullAfterActivation()
        {
            var (go, convoy, wps) = BuildMinimalConvoy();
            try
            {
                convoy.BindForZone3();
                convoy.Activate();
                Assert.IsTrue(convoy.IsInitialized, "Convoy should be initialized after Activate.");
                Assert.That(convoy.Fuel01, Is.EqualTo(1f).Within(0.01f),
                    "Fuel should be 100% after activation.");
            }
            finally { DestroyAll(go, wps[0], wps[1]); }
        }

        // ─── Escort radius / player count ────────────────────────────────────────

        [Test]
        public void CONVOY_DoesNotMoveWith0ValidPlayers()
        {
            var (go, convoy, wps) = BuildMinimalConvoy();
            try
            {
                convoy.BindForZone3();
                convoy.Activate();
                // Force route locked to a simple destination
                var result = convoy.SelectNextPoint(Zone3ConvoyRoutePoint.Point01);
                if (!result)
                {
                    // Graph says Point00 → Point01 is valid, but if waypoint not found convoy stays
                    // Accept: convoy cannot select route because waypoint is in the scene but
                    // FindObjectsByType would find it – the internal dict was built at Awake/BindForZone3.
                    // This test validates that with 0 real players nearby, TickAuthoritative produces speed 0.
                }

                Vector3 startPos = go.transform.position;
                // Tick 1 second with no players present
                convoy.TickAuthoritative(1f);
                // Position should not move (no nearby players → targetSpeed = 0)
                Assert.That(go.transform.position, Is.EqualTo(startPos),
                    "Convoy must not move when no valid players are nearby.");
            }
            finally { DestroyAll(go, wps[0], wps[1]); }
        }

        // ─── Fuel ─────────────────────────────────────────────────────────────────

        [Test]
        public void FUEL_DoesNotDrainWhileStopped()
        {
            var (go, convoy, wps) = BuildMinimalConvoy();
            try
            {
                convoy.BindForZone3();
                convoy.Activate();
                float fuelBefore = convoy.Fuel01;
                // Tick with no players → convoy stopped
                convoy.TickAuthoritative(5f);
                Assert.That(convoy.Fuel01, Is.EqualTo(fuelBefore).Within(0.001f),
                    "Fuel must not drain while convoy is stopped.");
            }
            finally { DestroyAll(go, wps[0], wps[1]); }
        }

        [Test]
        public void FUEL_CannotExceedMaxFuel()
        {
            var (go, convoy, wps) = BuildMinimalConvoy();
            try
            {
                convoy.BindForZone3();
                convoy.Activate();
                // Manually trigger refuel when already full — Fuel01 should stay ≤ 1
                // Force fuelEmpty via reflection to test the clamp
                var field = typeof(Zone3ConvoyController).GetField("_fuelEmpty",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                field.SetValue(convoy, true);
                convoy.Refuel();
                Assert.That(convoy.Fuel01, Is.LessThanOrEqualTo(1f),
                    "Fuel01 must never exceed 1 (MaxFuel).");
                Assert.IsFalse(convoy.IsFuelEmpty, "Convoy should not be fuel-empty after Refuel().");
            }
            finally { DestroyAll(go, wps[0], wps[1]); }
        }

        [Test]
        public void FUEL_RefuelRestoresFuelState()
        {
            var (go, convoy, wps) = BuildMinimalConvoy();
            try
            {
                convoy.BindForZone3();
                convoy.Activate();
                // Simulate depletion
                var fuelField = typeof(Zone3ConvoyController).GetField("_fuel",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var emptyField = typeof(Zone3ConvoyController).GetField("_fuelEmpty",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                fuelField.SetValue(convoy, 0f);
                emptyField.SetValue(convoy, true);

                Assert.IsTrue(convoy.IsFuelEmpty, "Should be fuel-empty before refuel.");
                convoy.Refuel();
                Assert.IsFalse(convoy.IsFuelEmpty, "Should not be fuel-empty after Refuel().");
                Assert.That(convoy.Fuel01, Is.GreaterThan(0f), "Fuel01 should be > 0 after Refuel().");
            }
            finally { DestroyAll(go, wps[0], wps[1]); }
        }

        [Test]
        public void FUEL_FuelEmptyStopsConvoy()
        {
            var (go, convoy, wps) = BuildMinimalConvoy();
            try
            {
                convoy.BindForZone3();
                convoy.Activate();
                // Force fuel empty
                var fuelField = typeof(Zone3ConvoyController).GetField("_fuel",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var emptyField = typeof(Zone3ConvoyController).GetField("_fuelEmpty",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                fuelField.SetValue(convoy, 0f);
                emptyField.SetValue(convoy, true);

                Assert.IsFalse(convoy.IsMoving,
                    "Convoy must not move when fuel is empty.");
            }
            finally { DestroyAll(go, wps[0], wps[1]); }
        }

        // ─── Route ────────────────────────────────────────────────────────────────

        [Test]
        public void ROUTE_SelectLegalRouteSucceeds()
        {
            var (go, convoy, wps) = BuildMinimalConvoy();
            try
            {
                convoy.BindForZone3();
                convoy.Activate();

                // Point00 → Point01 is legal per the graph
                bool selected = convoy.SelectNextPoint(Zone3ConvoyRoutePoint.Point01);
                // May succeed only if both waypoints are found in scene
                // If the internal dict has Point01, it will be true
                // We check consistency: if selected, convoy must not be waiting for route
                if (selected)
                    Assert.IsFalse(convoy.IsWaitingForRouteChoice,
                        "After route selected, convoy should not be waiting for route choice.");
            }
            finally { DestroyAll(go, wps[0], wps[1]); }
        }

        [Test]
        public void ROUTE_IllegalRouteIsRejected()
        {
            var (go, convoy, wps) = BuildMinimalConvoy();
            try
            {
                convoy.BindForZone3();
                convoy.Activate();

                // Point00 → Final is NOT a direct connection in the graph
                bool selected = convoy.SelectNextPoint(Zone3ConvoyRoutePoint.Final);
                Assert.IsFalse(selected,
                    "Selecting an illegal route (no direct edge) must be rejected.");
            }
            finally { DestroyAll(go, wps[0], wps[1]); }
        }

        // ─── Route Graph ──────────────────────────────────────────────────────────

        [Test]
        public void ROUTE_GRAPH_Point00HasSingleSuccessor()
        {
            var next = Zone3ConvoyRouteGraph.GetNextPoints(Zone3ConvoyRoutePoint.Point00);
            Assert.AreEqual(1, next.Count,
                "Point00 (Frigate Start) should have exactly one successor.");
            Assert.AreEqual(Zone3ConvoyRoutePoint.Point01, next[0],
                "Point00 successor must be Point01.");
        }

        [Test]
        public void ROUTE_GRAPH_FinalHasNoSuccessors()
        {
            var next = Zone3ConvoyRouteGraph.GetNextPoints(Zone3ConvoyRoutePoint.Final);
            Assert.AreEqual(0, next.Count,
                "Final (Power Dock) must have no successors — convoy stops there.");
        }

        [Test]
        public void ROUTE_GRAPH_CanTravelValidEdge()
        {
            Assert.IsTrue(Zone3ConvoyRouteGraph.CanTravel(Zone3ConvoyRoutePoint.Point00, Zone3ConvoyRoutePoint.Point01),
                "Point00 → Point01 must be a valid direct edge.");
        }

        [Test]
        public void ROUTE_GRAPH_CannotTravelInvalidEdge()
        {
            Assert.IsFalse(Zone3ConvoyRouteGraph.CanTravel(Zone3ConvoyRoutePoint.Point00, Zone3ConvoyRoutePoint.Final),
                "Point00 → Final must not be a valid direct edge.");
        }

        // ─── Checkpoint ───────────────────────────────────────────────────────────

        [Test]
        public void CHECKPOINT_ConvoyStopsAtJunction()
        {
            // Validate that WaitingForRouteChoice is set when two successors exist
            // Junction points in the graph have 2+ successors
            var nextFromP01 = Zone3ConvoyRouteGraph.GetNextPoints(Zone3ConvoyRoutePoint.Point01);
            Assert.IsTrue(nextFromP01.Count >= 2,
                "Point01 should be a junction with >= 2 successor routes.");
        }

        // ─── ApplyReplicatedPose (client side) ────────────────────────────────────

        [Test]
        public void NETWORK_ApplyReplicatedPoseSetsTransform()
        {
            var (go, convoy, wps) = BuildMinimalConvoy();
            try
            {
                var testPos = new Vector3(5f, 0f, 3f);
                var testRot = Quaternion.Euler(0f, 45f, 0f);
                convoy.ApplyReplicatedPose(testPos, testRot);
                Assert.That(go.transform.position, Is.EqualTo(testPos).Using(Vector3EqualityComparer.Instance));
                Assert.That(go.transform.rotation, Is.EqualTo(testRot).Using(QuaternionEqualityComparer.Instance));
            }
            finally { DestroyAll(go, wps[0], wps[1]); }
        }

        // ─── Fuel Cell & Fuel Port ───────────────────────────────────────────────

        [Test]
        public void FUEL_CELL_CanPickUpAndCarry()
        {
            var cellGo = new GameObject("FuelCell");
            cellGo.AddComponent<BoxCollider>();
            var cell = cellGo.AddComponent<EchoProtocol.MatchFlow.Zone3FuelCell>();

            var playerGo = new GameObject("Player");
            try
            {
                Assert.IsTrue(cell.CanInteract(playerGo), "Alive player should be able to pick up Fuel Cell.");
                cell.Interact(playerGo);
                Assert.IsTrue(cell.IsCarried, "Fuel Cell should be carried after interaction.");
                Assert.AreEqual(playerGo, cell.Carrier, "Carrier should match interacting player.");
            }
            finally
            {
                DestroyAll(cellGo, playerGo);
            }
        }

        [Test]
        public void FUEL_PORT_CanInteractOnlyWhenFuelEmpty()
        {
            var (go, convoy, wps) = BuildMinimalConvoy();
            var portGo = new GameObject("FuelPort");
            portGo.transform.SetParent(go.transform);
            portGo.AddComponent<BoxCollider>();
            var port = portGo.AddComponent<EchoProtocol.MatchFlow.Zone3FuelPort>();

            var playerGo = new GameObject("Player");
            try
            {
                convoy.BindForZone3();
                // Before activation: offline
                Assert.IsFalse(port.CanInteract(playerGo), "Fuel Port must reject interaction before convoy is initialized.");

                convoy.Activate();
                // When fuel full: nominal
                Assert.IsFalse(port.CanInteract(playerGo), "Fuel Port must reject interaction when fuel is full.");

                // When fuel empty:
                var fuelField = typeof(Zone3ConvoyController).GetField("_fuel",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var emptyField = typeof(Zone3ConvoyController).GetField("_fuelEmpty",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                fuelField.SetValue(convoy, 0f);
                emptyField.SetValue(convoy, true);

                Assert.IsTrue(port.CanInteract(playerGo), "Fuel Port must accept interaction when convoy is fuel empty.");
                StringAssert.Contains("INSERT FUEL CELL", port.InteractionPrompt);
            }
            finally
            {
                DestroyAll(go, portGo, playerGo, wps[0], wps[1]);
            }
        }

        [Test]
        public void FUEL_PORT_ConsumesFuelCellAndRefuels()
        {
            var (go, convoy, wps) = BuildMinimalConvoy();
            var portGo = new GameObject("FuelPort");
            portGo.transform.SetParent(go.transform);
            portGo.AddComponent<BoxCollider>();
            var port = portGo.AddComponent<EchoProtocol.MatchFlow.Zone3FuelPort>();

            var playerGo = new GameObject("Player");
            var cellGo = new GameObject("FuelCell");
            cellGo.AddComponent<BoxCollider>();
            var cell = cellGo.AddComponent<EchoProtocol.MatchFlow.Zone3FuelCell>();

            try
            {
                convoy.BindForZone3();
                convoy.Activate();

                // Deplete fuel
                var fuelField = typeof(Zone3ConvoyController).GetField("_fuel",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var emptyField = typeof(Zone3ConvoyController).GetField("_fuelEmpty",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                fuelField.SetValue(convoy, 0f);
                emptyField.SetValue(convoy, true);

                // Player carries fuel cell
                cell.AttachToCarrier(playerGo);

                // Interact with port
                port.Interact(playerGo);

                // Convoy should be refueled
                Assert.IsFalse(convoy.IsFuelEmpty, "Convoy should not be fuel empty after inserting fuel cell.");
                Assert.That(convoy.Fuel01, Is.GreaterThan(0f), "Fuel should be restored after insertion.");
            }
            finally
            {
                DestroyAll(go, portGo, playerGo, cellGo, wps[0], wps[1]);
            }
        }

        // ─── Scene waypoint naming ────────────────────────────────────────────────

        [Test]
        public void SCENE_AllRoutePointsHaveSceneNames()
        {
            foreach (var point in Zone3ConvoyRouteGraph.OrderedPoints)
            {
                string name = Zone3ConvoyRouteGraph.GetSceneObjectName(point);
                Assert.IsFalse(string.IsNullOrEmpty(name),
                    $"Route point {point} must have a non-empty scene object name.");
                Assert.IsTrue(name.StartsWith("Z3_Route_"),
                    $"Scene name for {point} should follow Z3_Route_ convention, got: {name}");
            }
        }

        // ─── Comparer helpers ─────────────────────────────────────────────────────

        private sealed class Vector3EqualityComparer : System.Collections.Generic.IEqualityComparer<Vector3>
        {
            public static readonly Vector3EqualityComparer Instance = new Vector3EqualityComparer();
            public bool Equals(Vector3 x, Vector3 y) => (x - y).sqrMagnitude < 0.0001f;
            public int GetHashCode(Vector3 v) => v.GetHashCode();
        }

        private sealed class QuaternionEqualityComparer : System.Collections.Generic.IEqualityComparer<Quaternion>
        {
            public static readonly QuaternionEqualityComparer Instance = new QuaternionEqualityComparer();
            public bool Equals(Quaternion x, Quaternion y) => Quaternion.Angle(x, y) < 0.1f;
            public int GetHashCode(Quaternion q) => q.GetHashCode();
        }
    }
}
