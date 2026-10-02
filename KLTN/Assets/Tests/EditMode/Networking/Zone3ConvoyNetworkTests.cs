using System.IO;
using NUnit.Framework;

namespace EchoProtocol.Networking.Tests
{
    /// <summary>
    /// Structural source-scan tests for Zone 3 convoy gameplay additions.
    /// Tests verify that the expected code patterns are present in the authoritative
    /// NetworkMatchState source, following the same approach as NetworkMatchStateTests.
    /// </summary>
    public sealed class Zone3ConvoyNetworkTests
    {
        private const string MatchSourcePath =
            "Assets/_Project/Scripts/Networking/Match/NetworkMatchState.cs";
        private const string ConvoyControllerPath =
            "Assets/Scripts/MatchFlow/Zone3ConvoyController.cs";
        private const string RouteGraphPath =
            "Assets/Scripts/MatchFlow/Zone3ConvoyRouteGraph.cs";
        private const string HudPath =
            "Assets/Scripts/UI/HUD/HUDObjectiveTracker.cs";
        private const string MissionDirectorPath =
            "Assets/Scripts/MatchFlow/Zone3MissionDirector.cs";

        // ─── Zone Boundary Fix (PART 30) ─────────────────────────────────────────

        [Test]
        public void ZONE3_BoundaryIncludesZone2ToZone3FindFrigate()
        {
            var source = File.ReadAllText(MatchSourcePath);

            // The zone boundary used to only cover Zone2 → FinalHunt.
            // It must now also cover Zone2 → Zone3FindFrigate so revive budget
            // resets when Zone 3 begins, not at FinalHunt.
            StringAssert.Contains(
                "next == NetworkMatchPhase.Zone3FindFrigate",
                source,
                "IsZoneBoundary must include Zone2Objective → Zone3FindFrigate " +
                "so revive budgets reset at Zone 3 entry, not at FinalHunt.");
        }

        [Test]
        public void ZONE3_BoundaryStillIncludesZone2ToFinalHuntForLegacyPath()
        {
            var source = File.ReadAllText(MatchSourcePath);

            // Legacy guard: Zone2 → FinalHunt is still a boundary case.
            StringAssert.Contains(
                "next == NetworkMatchPhase.FinalHunt",
                source,
                "IsZoneBoundary must still include Zone2Objective → FinalHunt for backward-compat.");
        }

        // ─── Convoy RPC surface (PART 26) ─────────────────────────────────────────

        [Test]
        public void ZONE3_ConvoyRouteChoiceRpcExists()
        {
            var source = File.ReadAllText(MatchSourcePath);

            StringAssert.Contains("RpcZone3ConvoyRouteChoice", source,
                "NetworkMatchState must contain RPC for convoy route choice.");
            StringAssert.Contains("RequestZone3ConvoyRouteChoice", source,
                "NetworkMatchState must expose public RequestZone3ConvoyRouteChoice.");
        }

        [Test]
        public void ZONE3_ConvoyRefuelRpcExists()
        {
            var source = File.ReadAllText(MatchSourcePath);

            StringAssert.Contains("RpcZone3ConvoyRefuel", source,
                "NetworkMatchState must contain RPC for convoy refuel.");
            StringAssert.Contains("RequestZone3ConvoyRefuel", source,
                "NetworkMatchState must expose public RequestZone3ConvoyRefuel.");
        }

        [Test]
        public void ZONE3_ConvoyRouteSelectionValidatedOnHost()
        {
            var source = File.ReadAllText(MatchSourcePath);

            // The authoritative method must check HasStateAuthority, phase, and player alive.
            StringAssert.Contains("TrySelectZone3ConvoyRouteAuthoritative", source,
                "Route selection must go through host-authoritative validation method.");
            StringAssert.Contains(
                "CurrentPhase != NetworkMatchPhase.Zone3PushFrigate",
                source,
                "Route selection authoritative guard must check current phase is Zone3PushFrigate.");
        }

        [Test]
        public void ZONE3_ConvoyTickCalledWithRunnerDeltaTime()
        {
            var source = File.ReadAllText(MatchSourcePath);

            StringAssert.Contains("zone3.TickConvoyAuthoritative(Runner.DeltaTime)", source,
                "Convoy authoritative tick must use Runner.DeltaTime, not Time.deltaTime.");
        }

        [Test]
        public void ZONE3_FrigatePoseReplicatedEachFrame()
        {
            var source = File.ReadAllText(MatchSourcePath);

            StringAssert.Contains("UpdateZone3FrigatePoseAuthoritative", source,
                "Frigate position/rotation must be replicated each authoritative frame.");
            StringAssert.Contains("[Networked] public Vector3 Zone3FrigatePosition", source,
                "Zone3FrigatePosition must be a [Networked] field.");
            StringAssert.Contains("[Networked] public Quaternion Zone3FrigateRotation", source,
                "Zone3FrigateRotation must be a [Networked] field.");
        }

        // ─── Convoy controller structure (PART 2/3/4/8) ───────────────────────────

        [Test]
        public void ZONE3_ConvoyControllerHasEscortRadiusField()
        {
            var source = File.ReadAllText(ConvoyControllerPath);

            StringAssert.Contains("escortRadius", source,
                "Zone3ConvoyController must have configurable escort radius.");
        }

        [Test]
        public void ZONE3_ConvoyControllerHasFuelFields()
        {
            var source = File.ReadAllText(ConvoyControllerPath);

            StringAssert.Contains("maxFuel", source);
            StringAssert.Contains("fuelDrainPerMeter", source);
            StringAssert.Contains("refuelAmount01", source);
            StringAssert.Contains("lowFuelThreshold01", source);
        }

        [Test]
        public void ZONE3_ConvoyControllerSoloEscortUsesReducedSpeed()
        {
            var source = File.ReadAllText(ConvoyControllerPath);

            StringAssert.Contains("soloEscortSpeedMultiplier", source,
                "Convoy must use reduced speed multiplier when only 1 player escorts.");
            StringAssert.Contains("escortCount == 1 ? soloEscortSpeedMultiplier : 1f", source,
                "Escort speed must be scaled by soloEscortSpeedMultiplier for single player.");
        }

        [Test]
        public void ZONE3_ConvoyControllerFuelDrainOnlyWhileMoving()
        {
            var source = File.ReadAllText(ConvoyControllerPath);

            // Fuel drains by step * fuelDrainPerMeter, inside MoveTowardTarget.
            // It must NOT drain while stopped.
            StringAssert.Contains("_fuel = Mathf.Max(0f, _fuel - step * fuelDrainPerMeter)", source,
                "Fuel must drain proportional to distance moved, not by time.");
        }

        [Test]
        public void ZONE3_ConvoyControllerUsesHostAuthoritativeTick()
        {
            var source = File.ReadAllText(ConvoyControllerPath);

            StringAssert.Contains("public void TickAuthoritative(float deltaTime)", source,
                "ConvoyController must expose TickAuthoritative, not use Unity Update for movement.");
        }

        [Test]
        public void ZONE3_ClientAppliesReplicatedPoseOnly()
        {
            var source = File.ReadAllText(ConvoyControllerPath);

            StringAssert.Contains("public void ApplyReplicatedPose(Vector3 position, Quaternion rotation)", source,
                "Clients must apply replicated pose only, never run authoritative tick.");
        }

        [Test]
        public void ZONE3_ConvoyDoesNotUseFindObjectEveryTick()
        {
            // CountNearbyAlivePlayers uses FindObjectsByType — acceptable for host-only path
            // but the rest of movement must not use scene-wide search per tick.
            var source = File.ReadAllText(ConvoyControllerPath);

            // Waypoints are cached in _points dictionary, not found every tick.
            StringAssert.Contains("private readonly Dictionary", source,
                "Waypoints should be cached in a dictionary, not searched every tick.");
        }

        // ─── Route graph (PART 2) ─────────────────────────────────────────────────

        [Test]
        public void ZONE3_RouteGraphHasJunctions()
        {
            var source = File.ReadAllText(RouteGraphPath);

            // Junctions have multiple next points — verify static arrays with 2 entries
            StringAssert.Contains("Zone3ConvoyRoutePoint.Point02, Zone3ConvoyRoutePoint.Point03", source,
                "Route graph must define junctions (points with multiple next options).");
        }

        [Test]
        public void ZONE3_RouteGraphFinalHasNoSuccessors()
        {
            var source = File.ReadAllText(RouteGraphPath);

            StringAssert.Contains("Zone3ConvoyRoutePoint.Final => EmptyNext", source,
                "Final route point must have no successors in the route graph.");
        }

        // ─── Manual push retired (PART 1) ─────────────────────────────────────────

        [Test]
        public void ZONE3_PushableObjectRedirectsToConvoyWhenAvailable()
        {
            // PushableObject.Interact and BeginHoldInteract must delegate to Convoy if present.
            var source = File.ReadAllText("Assets/Scripts/Gameplay/Vehicles/PushableObject.cs");

            StringAssert.Contains("zone3.Convoy.Interact(interactor)", source,
                "PushableObject must redirect Interact to convoy when Zone3ConvoyController is present.");
        }

        [Test]
        public void ZONE3_PushableObjectHoldNotRequiredForFrigate()
        {
            var source = File.ReadAllText("Assets/Scripts/Gameplay/Vehicles/PushableObject.cs");

            // RequiresHold returns false when Convoy is present → single tap instead of hold
            StringAssert.Contains("zone3 != null && zone3.Frigate == this && zone3.Convoy != null", source,
                "PushableObject.RequiresHold must return false when convoy controller exists.");
        }

        // ─── HUD (PART 6) ─────────────────────────────────────────────────────────

        [Test]
        public void ZONE3_HudShowsEscortObjective()
        {
            var source = File.ReadAllText(HudPath);

            StringAssert.Contains("ESCORT SPACEFRIGATE", source,
                "HUD must display ESCORT SPACEFRIGATE objective text.");
        }

        [Test]
        public void ZONE3_HudShowsRefuelObjective()
        {
            var source = File.ReadAllText(HudPath);

            StringAssert.Contains("REFUEL SPACEFRIGATE", source,
                "HUD must display refuel objective when fuel is empty.");
        }

        [Test]
        public void ZONE3_HudShowsRouteChoiceObjective()
        {
            var source = File.ReadAllText(HudPath);

            StringAssert.Contains("CHOOSE CONVOY ROUTE", source,
                "HUD must display route choice objective at junctions.");
        }

        [Test]
        public void ZONE3_HudShowsFuelProgress()
        {
            var source = File.ReadAllText(HudPath);

            // Convoy fuel is used as the progress bar fill at convoy phase
            StringAssert.Contains("convoy.Fuel01", source,
                "HUD must use convoy.Fuel01 as progress indicator during escort phase.");
        }

        [Test]
        public void ZONE3_HudEmergencyPowerRemainingWording()
        {
            var source = File.ReadAllText(HudPath);

            StringAssert.Contains("Emergency power remaining", source,
                "HUD FinalHunt/Escape wording must include 'Emergency power remaining'.");
            StringAssert.DoesNotContain("CHARGE SPACEFRIGATE", source,
                "Old 'CHARGE SPACEFRIGATE' wording must not appear in HUD.");
        }

        // ─── Mission Director (PART 5 / convoy binding) ───────────────────────────

        [Test]
        public void ZONE3_MissionDirectorBindsConvoyController()
        {
            var source = File.ReadAllText(MissionDirectorPath);

            StringAssert.Contains("public Zone3ConvoyController Convoy { get; private set; }", source,
                "Zone3MissionDirector must expose Zone3ConvoyController as Convoy.");
            StringAssert.Contains("Convoy.BindForZone3()", source,
                "Zone3MissionDirector must call BindForZone3() on the convoy.");
        }

        [Test]
        public void ZONE3_MissionDirectorExposesConvoyRoute()
        {
            var source = File.ReadAllText(MissionDirectorPath);

            StringAssert.Contains("public bool SelectConvoyRoute(Zone3ConvoyRoutePoint nextPoint)", source,
                "Zone3MissionDirector must expose SelectConvoyRoute for host validation.");
        }

        // ─── RuntimeNoise (PART 14) ───────────────────────────────────────────────

        [Test]
        public void ZONE3_ConvoyNoiseUsesConvoyIsMoving()
        {
            var source = File.ReadAllText(MatchSourcePath);

            StringAssert.Contains("zone3.Convoy.IsMoving", source,
                "Convoy noise emission must check Convoy.IsMoving, not old IsBeingPushed alone.");
        }

        [Test]
        public void ZONE3_ConvoyNoiseEmittedViaVehiclePushType()
        {
            var source = File.ReadAllText(MatchSourcePath);

            StringAssert.Contains("RuntimeNoiseType.VEHICLE_PUSH", source,
                "Convoy movement noise must use VEHICLE_PUSH RuntimeNoiseType.");
        }

        // ─── Fuel Cell & Fuel Port (PART 10-12) ───────────────────────────────────

        [Test]
        public void ZONE3_FuelCellImplementsInteractable()
        {
            var source = File.ReadAllText("Assets/Scripts/MatchFlow/Zone3FuelCell.cs");

            StringAssert.Contains("IInteractable", source,
                "Zone3FuelCell must implement IInteractable.");
            StringAssert.Contains("public bool IsCarried", source,
                "Zone3FuelCell must track carried status.");
        }

        [Test]
        public void ZONE3_FuelPortImplementsInteractableAndValidatesEmpty()
        {
            var source = File.ReadAllText("Assets/Scripts/MatchFlow/Zone3FuelPort.cs");

            StringAssert.Contains("IInteractable", source,
                "Zone3FuelPort must implement IInteractable.");
            StringAssert.Contains("convoy.IsFuelEmpty", source,
                "Zone3FuelPort must check convoy.IsFuelEmpty before accepting interaction.");
            StringAssert.Contains("RequestZone3ConvoyRefuel", source,
                "Zone3FuelPort must request refuel on NetworkMatchState.");
        }
    }
}
