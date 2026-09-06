using System;
using System.Collections.Generic;
using System.Reflection;
using EchoProtocol.AI.Common.Spatial;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace EchoProtocol.AI.Stalker.Tests
{
    public sealed class RoomSweepControllerIntegrationTests
    {
        private readonly List<GameObject> _createdObjects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (var i = _createdObjects.Count - 1; i >= 0; i--)
            {
                if (_createdObjects[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(_createdObjects[i]);
                }
            }

            _createdObjects.Clear();
        }

        [Test]
        public void STK_RoomSweepIntegration_EnumValuesAreAppended()
        {
            Assert.That(EnumValue(StalkerPatrolModeType, "FixedWaypoint"), Is.EqualTo(0));
            Assert.That(EnumValue(StalkerPatrolModeType, "DynamicSpatial"), Is.EqualTo(1));
            Assert.That(EnumValue(StalkerPatrolModeType, "ConfidenceSpatial"), Is.EqualTo(2));
            Assert.That(EnumValue(StalkerPatrolModeType, "RoomSweepSpatial"), Is.EqualTo(3));

            var chaseTarget = EnumValue(StalkerNavigationObjectiveKindType, "ChaseTarget");
            var roomSweepTransit = EnumValue(StalkerNavigationObjectiveKindType, "RoomSweepTransit");
            var roomSweepProbe = EnumValue(StalkerNavigationObjectiveKindType, "RoomSweepProbe");
            Assert.That(roomSweepTransit, Is.GreaterThan(chaseTarget));
            Assert.That(roomSweepProbe, Is.EqualTo(roomSweepTransit + 1));
        }

        [Test]
        public void STK_RoomSweepIntegration_VisibleProbeIsObservedAndBlockedProbeRemainsUnobserved()
        {
            var controller = CreateController();
            var room = new RegionId(1);
            var spatialGraph = CreateSpatialGraph(
                Node(0, new Vector3(0f, 0f, 0f)),
                Node(1, new Vector3(2f, 0f, 0f)));
            var regionGraph = CreateRegionGraph(
                spatialGraph,
                new[] { room, room },
                RoomRegion(room, 1, "Zone01/Room"));
            var memory = CreateMemory();
            RegisterRegion(memory, room, 0, 1);
            AttachRoomSweepState(controller, spatialGraph, regionGraph, memory);
            AttachVision(controller, Vector3.zero, Vector3.right, 10f, 90f);
            CreateBlocker(new Vector3(1f, 0f, 0f), new Vector3(0.25f, 2f, 2f));

            InvokePrivate(controller, "UpdateRoomSweepVisualCoverage", new[] { typeof(RegionId) }, room);

            Assert.That(IsObserved(memory, room, 0), Is.True);
            Assert.That(IsObserved(memory, room, 1), Is.False);
        }

        [Test]
        public void STK_RoomSweepIntegration_CurrentFloorProbeUsesDerivedObservationPointForVisualCoverage()
        {
            var controller = CreateController();
            var room = new RegionId(1);
            var spatialGraph = CreateSpatialGraph(Node(0, Vector3.zero));
            var regionGraph = CreateRegionGraph(
                spatialGraph,
                new[] { room },
                RoomRegion(room, 1, "Zone01/Room"));
            var memory = CreateMemory();
            RegisterRegion(memory, room, 0);
            AttachRoomSweepState(controller, spatialGraph, regionGraph, memory);
            AttachVisionWithLocalOrigin(controller, Vector3.zero, new Vector3(0f, 1.6f, 0f), Vector3.forward, 20f, 120f);

            InvokePrivate(controller, "UpdateRoomSweepVisualCoverage", new[] { typeof(RegionId) }, room);

            Assert.That(IsObserved(memory, room, 0), Is.True);
        }

        [Test]
        public void STK_RoomSweepIntegration_DerivedObservationPointBehindFacingRemainsUnobserved()
        {
            var controller = CreateController();
            var room = new RegionId(1);
            var spatialGraph = CreateSpatialGraph(Node(0, new Vector3(0f, 0f, -2f)));
            var regionGraph = CreateRegionGraph(
                spatialGraph,
                new[] { room },
                RoomRegion(room, 1, "Zone01/Room"));
            var memory = CreateMemory();
            RegisterRegion(memory, room, 0);
            AttachRoomSweepState(controller, spatialGraph, regionGraph, memory);
            AttachVisionWithLocalOrigin(controller, Vector3.zero, new Vector3(0f, 1.6f, 0f), Vector3.forward, 20f, 120f);

            InvokePrivate(controller, "UpdateRoomSweepVisualCoverage", new[] { typeof(RegionId) }, room);

            Assert.That(IsObserved(memory, room, 0), Is.False);
        }

        [Test]
        public void STK_RoomSweepIntegration_BlockerBetweenVisionOriginAndDerivedObservationPointRemainsUnobserved()
        {
            var controller = CreateController();
            var room = new RegionId(1);
            var spatialGraph = CreateSpatialGraph(Node(0, new Vector3(0f, 0f, 5f)));
            var regionGraph = CreateRegionGraph(
                spatialGraph,
                new[] { room },
                RoomRegion(room, 1, "Zone01/Room"));
            var memory = CreateMemory();
            RegisterRegion(memory, room, 0);
            AttachRoomSweepState(controller, spatialGraph, regionGraph, memory);
            AttachVisionWithLocalOrigin(controller, Vector3.zero, new Vector3(0f, 1.6f, 0f), Vector3.forward, 20f, 120f);
            CreateBlocker(new Vector3(0f, 1.6f, 2.5f), new Vector3(0.5f, 0.5f, 0.5f));

            InvokePrivate(controller, "UpdateRoomSweepVisualCoverage", new[] { typeof(RegionId) }, room);

            Assert.That(IsObserved(memory, room, 0), Is.False);
        }

        [Test]
        public void STK_RoomSweepIntegration_PhysicalArrivalDoesNotMarkProbeObserved()
        {
            var controller = CreateController();
            var room = new RegionId(1);
            var spatialGraph = CreateSpatialGraph(
                Node(0, new Vector3(0f, 0f, 0f)),
                Node(1, new Vector3(1f, 0f, 0f)));
            var regionGraph = CreateRegionGraph(
                spatialGraph,
                new[] { room, room },
                RoomRegion(room, 1, "Zone01/Room"));
            var memory = CreateMemory();
            RegisterRegion(memory, room, 1);
            AttachRoomSweepState(controller, spatialGraph, regionGraph, memory);
            SetBlackboardNode(controller, "CurrentSpatialNodeId", 0);
            SetBlackboardNode(controller, "DestinationSpatialNodeId", 1);

            InvokePrivate(controller, "MarkRoomSweepDestinationReached", Type.EmptyTypes);

            Assert.That(IsObserved(memory, room, 1), Is.False);
            Assert.That(GetBlackboardNode(controller, "PreviousSpatialNodeId"), Is.EqualTo(0));
            Assert.That(GetBlackboardNode(controller, "CurrentSpatialNodeId"), Is.EqualTo(1));
            Assert.That(GetBlackboardNode(controller, "DestinationSpatialNodeId"), Is.EqualTo(-1));
        }

        [Test]
        public void STK_RoomSweepIntegration_SelfProbeWithAlternateStartsInPlaceScanWithoutImmediateReject()
        {
            var controller = CreateController();
            var room = new RegionId(1);
            var spatialGraph = CreateSpatialGraph(
                Node(0, Vector3.zero, 1),
                Node(1, Vector3.right, 0));
            var regionGraph = CreateRegionGraph(
                spatialGraph,
                new[] { room, room },
                RoomRegion(room, 1, "Zone01/Room"));
            var memory = CreateMemory();
            AttachRoomSweepState(controller, spatialGraph, regionGraph, memory);
            AttachNavigationAndLocalSelector(controller, spatialGraph, regionGraph);

            var result = InvokePrivate(
                controller,
                "TryBeginOrContinueCurrentRoomSweep",
                new[] { typeof(int), typeof(RegionId) },
                0,
                room);

            Assert.That(result.ToString(), Is.EqualTo("DestinationSet"));
            Assert.That(IsObserved(memory, room, 0), Is.False);
            Assert.That(IsRegionCleared(memory, room), Is.False);
            Assert.That(GetBlackboardNode(controller, "DestinationSpatialNodeId"), Is.EqualTo(-1));
            Assert.That(GetNavigationObjectiveKindName(controller), Is.EqualTo("None"));
            Assert.That(GetPlannerInt(controller, "RejectedProbeCount"), Is.EqualTo(0));
            Assert.That((bool)GetPrivateField(controller, "_roomSweepSelfProbeScanActive"), Is.True);
            Assert.That((int)GetPrivateField(controller, "_roomSweepSelfProbeScanNodeId"), Is.EqualTo(0));
        }

        [Test]
        public void STK_RoomSweepIntegration_BeginSelfProbeScanDisablesAgentUpdateRotationWhenOriginallyTrue()
        {
            var controller = CreateController();
            var agent = ((Component)controller).GetComponent<NavMeshAgent>();
            agent.updateRotation = true;

            InvokePrivate(controller, "BeginRoomSweepSelfProbeScan", new[] { typeof(int), typeof(RegionId) }, 0, new RegionId(1));

            Assert.That(agent.updateRotation, Is.False);
        }

        [Test]
        public void STK_RoomSweepIntegration_ClearSelfProbeScanRestoresAgentUpdateRotationTrue()
        {
            var controller = CreateController();
            var agent = ((Component)controller).GetComponent<NavMeshAgent>();
            agent.updateRotation = true;
            InvokePrivate(controller, "BeginRoomSweepSelfProbeScan", new[] { typeof(int), typeof(RegionId) }, 0, new RegionId(1));

            InvokePrivate(controller, "ClearRoomSweepSelfProbeScan", Type.EmptyTypes);
            InvokePrivate(controller, "ClearRoomSweepSelfProbeScan", Type.EmptyTypes);

            Assert.That(agent.updateRotation, Is.True);
        }

        [Test]
        public void STK_RoomSweepIntegration_ClearSelfProbeScanPreservesOriginallyFalseUpdateRotation()
        {
            var controller = CreateController();
            var agent = ((Component)controller).GetComponent<NavMeshAgent>();
            agent.updateRotation = false;
            InvokePrivate(controller, "BeginRoomSweepSelfProbeScan", new[] { typeof(int), typeof(RegionId) }, 0, new RegionId(1));

            InvokePrivate(controller, "ClearRoomSweepSelfProbeScan", Type.EmptyTypes);

            Assert.That(agent.updateRotation, Is.False);
        }

        [Test]
        public void STK_RoomSweepIntegration_SelfProbeOnlyRemainingStartsScanWithoutClearingOrLooping()
        {
            var controller = CreateController();
            var room = new RegionId(1);
            var spatialGraph = CreateSpatialGraph(Node(0, Vector3.zero));
            var regionGraph = CreateRegionGraph(
                spatialGraph,
                new[] { room },
                RoomRegion(room, 1, "Zone01/Room"));
            var memory = CreateMemory();
            AttachRoomSweepState(controller, spatialGraph, regionGraph, memory);
            AttachNavigationAndLocalSelector(controller, spatialGraph, regionGraph);

            var result = InvokePrivate(
                controller,
                "TryBeginOrContinueCurrentRoomSweep",
                new[] { typeof(int), typeof(RegionId) },
                0,
                room);

            Assert.That(result.ToString(), Is.EqualTo("DestinationSet"));
            Assert.That(IsObserved(memory, room, 0), Is.False);
            Assert.That(IsRegionCleared(memory, room), Is.False);
            Assert.That(GetPlannerInt(controller, "RejectedProbeCount"), Is.EqualTo(0));
            Assert.That(GetBlackboardNode(controller, "DestinationSpatialNodeId"), Is.EqualTo(-1));
            Assert.That(GetNavigationObjectiveKindName(controller), Is.EqualTo("None"));
            Assert.That((bool)GetPrivateField(controller, "_roomSweepSelfProbeScanActive"), Is.True);
        }

        [Test]
        public void STK_RoomSweepIntegration_PhysicalSelfArrivalDoesNotIncreaseObservedCount()
        {
            var controller = CreateController();
            var room = new RegionId(1);
            var spatialGraph = CreateSpatialGraph(Node(0, Vector3.zero));
            var regionGraph = CreateRegionGraph(
                spatialGraph,
                new[] { room },
                RoomRegion(room, 1, "Zone01/Room"));
            var memory = CreateMemory();
            RegisterRegion(memory, room, 0);
            AttachRoomSweepState(controller, spatialGraph, regionGraph, memory);
            SetBlackboardNode(controller, "CurrentSpatialNodeId", 0);
            SetBlackboardNode(controller, "DestinationSpatialNodeId", 0);

            InvokePrivate(controller, "MarkRoomSweepDestinationReached", Type.EmptyTypes);

            Assert.That(GetMemoryInt(memory, "GetObservedCount", room), Is.EqualTo(0));
            Assert.That(IsObserved(memory, room, 0), Is.False);
            Assert.That(IsRegionCleared(memory, room), Is.False);
        }

        [Test]
        public void STK_RoomSweepIntegration_SelfProbeScanAccumulatedDegreesUsesActualTransformYawDelta()
        {
            var controller = CreateController();
            var room = new RegionId(1);
            var spatialGraph = CreateSpatialGraph(Node(0, Vector3.zero));
            var regionGraph = CreateRegionGraph(
                spatialGraph,
                new[] { room },
                RoomRegion(room, 1, "Zone01/Room"));
            AttachRoomSweepState(controller, spatialGraph, regionGraph, CreateMemory());
            Invoke(GetPrivateField(controller, "_roomSweepPlanner"), "TryBeginRegion", new[] { typeof(RegionId) }, room);
            SetPrivateField(controller, "_currentSimulationDeltaSeconds", 1f);
            var agent = ((Component)controller).GetComponent<NavMeshAgent>();
            agent.angularSpeed = 90f;

            InvokePrivate(controller, "BeginRoomSweepSelfProbeScan", new[] { typeof(int), typeof(RegionId) }, 0, room);
            InvokePrivate(controller, "TickRoomSweepSelfProbeScan", Type.EmptyTypes);

            Assert.That((float)GetPrivateField(controller, "_roomSweepSelfProbeScanAccumulatedDegrees"), Is.EqualTo(90f).Within(0.001f));
        }

        [Test]
        public void STK_RoomSweepIntegration_SelfProbeScanDoesNotReachFullScanWhenActualRotationDoesNotOccur()
        {
            var controller = CreateController();
            var room = new RegionId(1);
            var spatialGraph = CreateSpatialGraph(Node(0, Vector3.zero));
            var regionGraph = CreateRegionGraph(
                spatialGraph,
                new[] { room },
                RoomRegion(room, 1, "Zone01/Room"));
            AttachRoomSweepState(controller, spatialGraph, regionGraph, CreateMemory());
            Invoke(GetPrivateField(controller, "_roomSweepPlanner"), "TryBeginRegion", new[] { typeof(RegionId) }, room);
            SetPrivateField(controller, "_currentSimulationDeltaSeconds", 10f);
            var agent = ((Component)controller).GetComponent<NavMeshAgent>();
            agent.angularSpeed = 0f;

            InvokePrivate(controller, "BeginRoomSweepSelfProbeScan", new[] { typeof(int), typeof(RegionId) }, 0, room);
            InvokePrivate(controller, "TickRoomSweepSelfProbeScan", Type.EmptyTypes);

            Assert.That((float)GetPrivateField(controller, "_roomSweepSelfProbeScanAccumulatedDegrees"), Is.EqualTo(0f));
            Assert.That((bool)GetPrivateField(controller, "_roomSweepSelfProbeScanActive"), Is.True);
            Assert.That(GetPlannerInt(controller, "RejectedProbeCount"), Is.EqualTo(0));
        }

        [Test]
        public void STK_RoomSweepIntegration_SelfProbeBehindFovIsObservedAfterInPlaceRotation()
        {
            var controller = CreateController();
            var room = new RegionId(1);
            var spatialGraph = CreateSpatialGraph(Node(0, new Vector3(0f, 0f, -5f)));
            var regionGraph = CreateRegionGraph(
                spatialGraph,
                new[] { room },
                RoomRegion(room, 1, "Zone01/Room"));
            var memory = CreateMemory();
            AttachRoomSweepState(controller, spatialGraph, regionGraph, memory);
            AttachNavigationAndLocalSelector(controller, spatialGraph, regionGraph);
            AttachChildVisionWithLocalOrigin(controller, Vector3.zero, new Vector3(0f, 1.6f, 0f), Vector3.forward, 20f, 60f);
            SetPrivateField(controller, "_currentSimulationDeltaSeconds", 1f);
            ((Component)controller).GetComponent<NavMeshAgent>().angularSpeed = 180f;

            InvokePrivate(controller, "TryBeginOrContinueCurrentRoomSweep", new[] { typeof(int), typeof(RegionId) }, 0, room);
            InvokePrivate(controller, "TickRoomSweepSelfProbeScan", Type.EmptyTypes);

            Assert.That(IsObserved(memory, room, 0), Is.True);
            Assert.That(GetPlannerInt(controller, "RejectedProbeCount"), Is.EqualTo(0));
            Assert.That((bool)GetPrivateField(controller, "_roomSweepSelfProbeScanActive"), Is.False);
        }

        [Test]
        public void STK_RoomSweepIntegration_ObservedSelfProbeScanSuccessRestoresAgentUpdateRotation()
        {
            var controller = CreateController();
            var room = new RegionId(1);
            var spatialGraph = CreateSpatialGraph(Node(0, new Vector3(0f, 0f, -5f)));
            var regionGraph = CreateRegionGraph(
                spatialGraph,
                new[] { room },
                RoomRegion(room, 1, "Zone01/Room"));
            AttachRoomSweepState(controller, spatialGraph, regionGraph, CreateMemory());
            AttachNavigationAndLocalSelector(controller, spatialGraph, regionGraph);
            AttachChildVisionWithLocalOrigin(controller, Vector3.zero, new Vector3(0f, 1.6f, 0f), Vector3.forward, 20f, 60f);
            SetPrivateField(controller, "_currentSimulationDeltaSeconds", 1f);
            var agent = ((Component)controller).GetComponent<NavMeshAgent>();
            agent.updateRotation = true;
            agent.angularSpeed = 180f;

            InvokePrivate(controller, "TryBeginOrContinueCurrentRoomSweep", new[] { typeof(int), typeof(RegionId) }, 0, room);
            InvokePrivate(controller, "TickRoomSweepSelfProbeScan", Type.EmptyTypes);

            Assert.That(agent.updateRotation, Is.True);
        }

        [Test]
        public void STK_RoomSweepIntegration_SelfProbeFullScanBlockedRejectsAfterFullRotationOnly()
        {
            var controller = CreateController();
            var room = new RegionId(1);
            var spatialGraph = CreateSpatialGraph(Node(0, new Vector3(0f, 0f, 5f)));
            var regionGraph = CreateRegionGraph(
                spatialGraph,
                new[] { room },
                RoomRegion(room, 1, "Zone01/Room"));
            var memory = CreateMemory();
            AttachRoomSweepState(controller, spatialGraph, regionGraph, memory);
            AttachNavigationAndLocalSelector(controller, spatialGraph, regionGraph);
            AttachChildVisionWithLocalOrigin(controller, Vector3.zero, new Vector3(0f, 1.6f, 0f), Vector3.forward, 20f, 60f);
            CreateBlocker(new Vector3(0f, 1.6f, 2.5f), new Vector3(0.5f, 0.5f, 0.5f));
            SetPrivateField(controller, "_currentSimulationDeltaSeconds", 1f);
            ((Component)controller).GetComponent<NavMeshAgent>().angularSpeed = 180f;

            InvokePrivate(controller, "TryBeginOrContinueCurrentRoomSweep", new[] { typeof(int), typeof(RegionId) }, 0, room);
            InvokePrivate(controller, "TickRoomSweepSelfProbeScan", Type.EmptyTypes);
            Assert.That(GetPlannerInt(controller, "RejectedProbeCount"), Is.EqualTo(0));
            Assert.That(IsObserved(memory, room, 0), Is.False);

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("SELF_PROBE_FULL_SCAN_UNSEEN"));
            InvokePrivate(controller, "TickRoomSweepSelfProbeScan", Type.EmptyTypes);

            Assert.That(GetPlannerInt(controller, "RejectedProbeCount"), Is.EqualTo(1));
            Assert.That(IsObserved(memory, room, 0), Is.False);
            Assert.That(IsRegionCleared(memory, room), Is.False);
            Assert.That((bool)GetPrivateField(controller, "_roomSweepSelfProbeScanActive"), Is.False);
        }

        [Test]
        public void STK_RoomSweepIntegration_FullScanUnseenRestoresAgentUpdateRotation()
        {
            var controller = CreateController();
            var room = new RegionId(1);
            var spatialGraph = CreateSpatialGraph(Node(0, new Vector3(0f, 0f, 5f)));
            var regionGraph = CreateRegionGraph(
                spatialGraph,
                new[] { room },
                RoomRegion(room, 1, "Zone01/Room"));
            AttachRoomSweepState(controller, spatialGraph, regionGraph, CreateMemory());
            AttachNavigationAndLocalSelector(controller, spatialGraph, regionGraph);
            AttachChildVisionWithLocalOrigin(controller, Vector3.zero, new Vector3(0f, 1.6f, 0f), Vector3.forward, 20f, 60f);
            CreateBlocker(new Vector3(0f, 1.6f, 2.5f), new Vector3(0.5f, 0.5f, 0.5f));
            SetPrivateField(controller, "_currentSimulationDeltaSeconds", 1f);
            var agent = ((Component)controller).GetComponent<NavMeshAgent>();
            agent.updateRotation = true;
            agent.angularSpeed = 180f;

            InvokePrivate(controller, "TryBeginOrContinueCurrentRoomSweep", new[] { typeof(int), typeof(RegionId) }, 0, room);
            InvokePrivate(controller, "TickRoomSweepSelfProbeScan", Type.EmptyTypes);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("SELF_PROBE_FULL_SCAN_UNSEEN"));
            InvokePrivate(controller, "TickRoomSweepSelfProbeScan", Type.EmptyTypes);

            Assert.That(agent.updateRotation, Is.True);
        }

        [Test]
        public void STK_RoomSweepIntegration_ScanSuccessDoesNotIncreaseRejectedProbeCount()
        {
            var controller = CreateController();
            var room = new RegionId(1);
            var spatialGraph = CreateSpatialGraph(Node(0, new Vector3(0f, 0f, -5f)));
            var regionGraph = CreateRegionGraph(
                spatialGraph,
                new[] { room },
                RoomRegion(room, 1, "Zone01/Room"));
            var memory = CreateMemory();
            AttachRoomSweepState(controller, spatialGraph, regionGraph, memory);
            AttachNavigationAndLocalSelector(controller, spatialGraph, regionGraph);
            AttachChildVisionWithLocalOrigin(controller, Vector3.zero, new Vector3(0f, 1.6f, 0f), Vector3.forward, 20f, 60f);
            SetPrivateField(controller, "_currentSimulationDeltaSeconds", 1f);
            ((Component)controller).GetComponent<NavMeshAgent>().angularSpeed = 180f;

            InvokePrivate(controller, "TryBeginOrContinueCurrentRoomSweep", new[] { typeof(int), typeof(RegionId) }, 0, room);
            InvokePrivate(controller, "TickRoomSweepSelfProbeScan", Type.EmptyTypes);

            Assert.That(IsObserved(memory, room, 0), Is.True);
            Assert.That(GetPlannerInt(controller, "RejectedProbeCount"), Is.EqualTo(0));
        }

        [Test]
        public void STK_RoomSweepIntegration_FsmInterruptionClearsSelfProbeScanAndPreservesCoverage()
        {
            var controller = CreateController();
            var room = new RegionId(1);
            var spatialGraph = CreateSpatialGraph(
                Node(0, Vector3.zero),
                Node(1, Vector3.right));
            var regionGraph = CreateRegionGraph(
                spatialGraph,
                new[] { room, room },
                RoomRegion(room, 1, "Zone01/Room"));
            var memory = CreateMemory();
            RegisterRegion(memory, room, 0, 1);
            MarkObserved(memory, room, 1);
            AttachRoomSweepState(controller, spatialGraph, regionGraph, memory);
            AttachNavigationAndLocalSelector(controller, spatialGraph, regionGraph);
            var agent = ((Component)controller).GetComponent<NavMeshAgent>();
            agent.updateRotation = true;

            InvokePrivate(controller, "TryBeginOrContinueCurrentRoomSweep", new[] { typeof(int), typeof(RegionId) }, 0, room);
            Assert.That((bool)GetPrivateField(controller, "_roomSweepSelfProbeScanActive"), Is.True);

            InvokePrivate(controller, "StopAgentPath", Type.EmptyTypes);

            Assert.That((bool)GetPrivateField(controller, "_roomSweepSelfProbeScanActive"), Is.False);
            Assert.That(agent.updateRotation, Is.True);
            Assert.That(IsObserved(memory, room, 1), Is.True);
            Assert.That(IsObserved(memory, room, 0), Is.False);
        }

        [Test]
        public void STK_RoomSweepIntegration_FullVisualCoverageMarksRegionCleared()
        {
            var controller = CreateController();
            var room = new RegionId(1);
            var spatialGraph = CreateSpatialGraph(
                Node(0, new Vector3(0f, 0f, 0f)),
                Node(1, new Vector3(1f, 0f, 0f)));
            var regionGraph = CreateRegionGraph(
                spatialGraph,
                new[] { room, room },
                RoomRegion(room, 1, "Zone01/Room"));
            var memory = CreateMemory();
            AttachRoomSweepState(controller, spatialGraph, regionGraph, memory);
            AttachVision(controller, Vector3.zero, Vector3.right, 10f, 180f);

            InvokePrivate(controller, "TryBeginOrContinueCurrentRoomSweep", new[] { typeof(int), typeof(RegionId) }, 0, room);

            Assert.That(IsRegionCleared(memory, room), Is.True);
        }

        [Test]
        public void STK_RoomSweepIntegration_FullyObservedCurrentRoomInvalidatesCompletedObjectiveBeforeNextRoomPlanning()
        {
            var controller = CreateController();
            var completedRoom = new RegionId(18);
            var nextRoom = new RegionId(19);
            var spatialGraph = CreateSpatialGraph(
                Node(0, Vector3.zero, 1),
                Node(1, Vector3.right, 0));
            var regionGraph = CreateRegionGraph(
                spatialGraph,
                new[] { completedRoom, nextRoom },
                RoomRegion(completedRoom, 18, "Zone01/Completed", nextRoom),
                RoomRegion(nextRoom, 19, "Zone01/Next", completedRoom));
            var memory = CreateMemory();
            AttachRoomSweepState(controller, spatialGraph, regionGraph, memory);
            AttachNavigationAndLocalSelector(controller, spatialGraph, regionGraph);
            AttachVision(controller, Vector3.zero, Vector3.right, 10f, 180f);
            var globalPlanner = GetPrivateField(controller, "_roomSweepGlobalPlanner");
            Invoke(globalPlanner, "TryGetOrCreateObjective", new[] { typeof(RegionId), RoomSweepGlobalObjectiveType.MakeByRefType() }, new object[] { nextRoom, null });
            Invoke(globalPlanner, "TryGetOrCreateObjective", new[] { typeof(RegionId), RoomSweepGlobalObjectiveType.MakeByRefType() }, new object[] { completedRoom, null });

            var result = (bool)InvokePrivate(controller, "SetRoomSweepPatrolDestination", Type.EmptyTypes);

            Assert.That(result, Is.False);
            Assert.That(IsRegionCleared(memory, completedRoom), Is.True);
            Assert.That(GetProperty(globalPlanner, "LastInvalidationReason").ToString(), Is.EqualTo("TargetCleared"));
            Assert.That(GetPrivateCollectionCount(controller, "_rejectedRoomSweepGlobalRegionIds"), Is.EqualTo(1));
            Assert.That((bool)GetProperty(controller, "FixedFallbackActive"), Is.False);
        }

        [Test]
        public void STK_RoomSweepIntegration_FullyObservedCurrentRoomWithNoEligibleRoomsStillReturnsTerminalFailure()
        {
            var controller = CreateController();
            var completedRoom = new RegionId(18);
            var spatialGraph = CreateSpatialGraph(Node(0, Vector3.zero));
            var regionGraph = CreateRegionGraph(
                spatialGraph,
                new[] { completedRoom },
                RoomRegion(completedRoom, 18, "Zone01/Completed"));
            var memory = CreateMemory();
            AttachRoomSweepState(controller, spatialGraph, regionGraph, memory);
            AttachNavigationAndLocalSelector(controller, spatialGraph, regionGraph);
            AttachVision(controller, Vector3.zero, Vector3.right, 10f, 180f);

            var result = (bool)InvokePrivate(controller, "SetRoomSweepPatrolDestination", Type.EmptyTypes);

            Assert.That(result, Is.False);
            Assert.That(IsRegionCleared(memory, completedRoom), Is.True);
            Assert.That(GetProperty(controller, "RegionGraphFallbackReason").ToString(), Is.EqualTo("NoReachableRegionObjective"));
            Assert.That((bool)GetProperty(controller, "FixedFallbackActive"), Is.False);
        }

        [Test]
        public void STK_RoomSweepIntegration_RoomSweepFallbackStopsStaleRecoveryWithNoDestinationNode()
        {
            var controller = CreateController();
            var spatialGraph = CreateSpatialGraph(Node(0, Vector3.zero));
            var room = new RegionId(18);
            var regionGraph = CreateRegionGraph(
                spatialGraph,
                new[] { room },
                RoomRegion(room, 18, "Zone01/Completed"));
            AttachRoomSweepState(controller, spatialGraph, regionGraph, CreateMemory());
            AttachNavigationAndLocalSelector(controller, spatialGraph, regionGraph);
            SetPrivateField(controller, "patrolMode", Enum.Parse(StalkerPatrolModeType, "RoomSweepSpatial"));
            SetPrivateField(controller, "currentState", Enum.Parse(StalkerStateType, "PATROL"));
            SetBlackboardNode(controller, "DestinationSpatialNodeId", -1);
            var navigation = GetPrivateField(controller, "_navigation");
            Invoke(navigation, "RequestDestination", new[] { typeof(Vector3) }, Vector3.right);

            InvokePrivate(controller, "ActivateRoomSweepPatrolFallback", Type.EmptyTypes);
            InvokePrivate(controller, "TickNavigationRecovery", Type.EmptyTypes);

            Assert.That(GetBlackboardNode(controller, "DestinationSpatialNodeId"), Is.EqualTo(-1));
            Assert.That(GetNavigationObjectiveKindName(controller), Is.EqualTo("None"));
            Assert.That((bool)GetProperty(navigation, "HasActiveDestination"), Is.False);
            Assert.That((bool)GetProperty(controller, "FixedFallbackActive"), Is.True);
        }

        [Test]
        public void STK_RoomSweepIntegration_ExhaustedRoomDoesNotMarkCleared()
        {
            var controller = CreateController();
            var room = new RegionId(1);
            var spatialGraph = CreateSpatialGraph(
                Node(0, new Vector3(0f, 0f, 0f)),
                Node(1, new Vector3(1f, 0f, 0f)));
            var regionGraph = CreateRegionGraph(
                spatialGraph,
                new[] { room, room },
                RoomRegion(room, 1, "Zone01/Room"));
            var memory = CreateMemory();
            AttachRoomSweepState(controller, spatialGraph, regionGraph, memory);
            var planner = GetPrivateField(controller, "_roomSweepPlanner");
            Invoke(planner, "TryBeginRegion", new[] { typeof(RegionId) }, room);
            Invoke(planner, "RejectProbe", new[] { typeof(int) }, 0);
            Invoke(planner, "RejectProbe", new[] { typeof(int) }, 1);

            InvokePrivate(controller, "TryBeginOrContinueCurrentRoomSweep", new[] { typeof(int), typeof(RegionId) }, 0, room);

            Assert.That(IsRegionCleared(memory, room), Is.False);
        }

        [Test]
        public void STK_RoomSweepIntegration_TargetRoomReachedBeginsSweepInsteadOfSkipping()
        {
            var controller = CreateController();
            var route = new RegionId(1);
            var room = new RegionId(2);
            var spatialGraph = CreateSpatialGraph(
                Node(0, new Vector3(0f, 0f, 0f)),
                Node(1, new Vector3(1f, 0f, 0f)));
            var regionGraph = CreateRegionGraph(
                spatialGraph,
                new[] { route, room },
                RouteRegion(route, 1, "Zone01/Route", room),
                RoomRegion(room, 2, "Zone01/Room", route));
            var memory = CreateMemory();
            AttachRoomSweepState(controller, spatialGraph, regionGraph, memory);
            AttachVision(controller, new Vector3(1f, 0f, 0f), Vector3.right, 10f, 180f);
            ((Component)controller).transform.position = new Vector3(1f, 0f, 0f);
            var globalPlanner = GetPrivateField(controller, "_roomSweepGlobalPlanner");
            Invoke(globalPlanner, "TryGetOrCreateObjective", new[] { typeof(RegionId), RoomSweepGlobalObjectiveType.MakeByRefType() }, new object[] { route, null });

            var result = (bool)InvokePrivate(controller, "SetRoomSweepTransitDestination", Type.EmptyTypes);

            Assert.That(result, Is.True);
            Assert.That(IsRegionCleared(memory, room), Is.True);
        }

        [Test]
        public void STK_RoomSweepIntegration_SameSourceIndexSiblingRegionsRemainIndependent()
        {
            var controller = CreateController();
            var roomSeven = new RegionId(7);
            var roomEight = new RegionId(8);
            var spatialGraph = CreateSpatialGraph(
                Node(0, new Vector3(0f, 0f, 0f)),
                Node(1, new Vector3(1f, 0f, 0f)));
            var regionGraph = CreateRegionGraph(
                spatialGraph,
                new[] { roomSeven, roomEight },
                RoomRegion(roomSeven, 2, "Zone01/RoomA", roomEight),
                RoomRegion(roomEight, 2, "Zone01/RoomB", roomSeven));
            var memory = CreateMemory();
            AttachRoomSweepState(controller, spatialGraph, regionGraph, memory);
            AttachVision(controller, Vector3.zero, Vector3.right, 10f, 180f);

            InvokePrivate(controller, "TryBeginOrContinueCurrentRoomSweep", new[] { typeof(int), typeof(RegionId) }, 0, roomSeven);

            Assert.That(IsRegionCleared(memory, roomSeven), Is.True);
            Assert.That(IsRegionCleared(memory, roomEight), Is.False);
        }

        [Test]
        public void STK_RoomSweepIntegration_RecoveryDestinationUsesCurrentSpatialDestination()
        {
            var controller = CreateController();
            var room = new RegionId(1);
            var destination = new Vector3(3f, 0f, 4f);
            var spatialGraph = CreateSpatialGraph(Node(0, Vector3.zero), Node(1, destination));
            var regionGraph = CreateRegionGraph(
                spatialGraph,
                new[] { room, room },
                RoomRegion(room, 1, "Zone01/Room"));
            AttachRoomSweepState(controller, spatialGraph, regionGraph, CreateMemory());
            SetPrivateField(controller, "patrolMode", Enum.Parse(StalkerPatrolModeType, "RoomSweepSpatial"));
            SetBlackboardNode(controller, "DestinationSpatialNodeId", 1);

            var args = new object[] { default(Vector3) };
            var result = (bool)InvokePrivate(controller, "TryGetCurrentPatrolRecoveryDestination", new[] { typeof(Vector3).MakeByRefType() }, args);

            Assert.That(result, Is.True);
            Assert.That((Vector3)args[0], Is.EqualTo(destination));
        }

        [Test]
        public void STK_RoomSweepIntegration_UnclearedExhaustedRoomDoesNotTransitOrClear()
        {
            var controller = CreateController();
            var room = new RegionId(1);
            var nextRoom = new RegionId(2);
            var spatialGraph = CreateSpatialGraph(
                Node(0, Vector3.zero, 1),
                Node(1, Vector3.right, 0));
            var regionGraph = CreateRegionGraph(
                spatialGraph,
                new[] { room, nextRoom },
                RoomRegion(room, 1, "Zone01/Room", nextRoom),
                RoomRegion(nextRoom, 2, "Zone01/Next", room));
            var memory = CreateMemory();
            AttachRoomSweepState(controller, spatialGraph, regionGraph, memory);
            AttachNavigationAndLocalSelector(controller, spatialGraph, regionGraph);
            var planner = GetPrivateField(controller, "_roomSweepPlanner");
            Invoke(planner, "TryBeginRegion", new[] { typeof(RegionId) }, room);
            Invoke(planner, "RejectProbe", new[] { typeof(int) }, 0);

            var result = (bool)InvokePrivate(controller, "SetRoomSweepPatrolDestination", Type.EmptyTypes);

            Assert.That(result, Is.False);
            Assert.That(IsRegionCleared(memory, room), Is.False);
            Assert.That(GetNavigationObjectiveKindName(controller), Is.Not.EqualTo("RoomSweepTransit"));
            Assert.That(GetPrivateCollectionCount(controller, "_rejectedRoomSweepGlobalRegionIds"), Is.EqualTo(0));
        }

        [Test]
        public void STK_RoomSweepIntegration_ProbeRequestFailureRetriesAllProbesInSameRoomBeforeFallbackState()
        {
            var controller = CreateController();
            var room = new RegionId(1);
            var otherRoom = new RegionId(2);
            var spatialGraph = CreateSpatialGraph(
                Node(0, Vector3.zero, 1),
                Node(1, Vector3.right, 0, 2),
                Node(2, Vector3.right * 2f, 1));
            var regionGraph = CreateRegionGraph(
                spatialGraph,
                new[] { room, room, room },
                RoomRegion(room, 1, "Zone01/Room", otherRoom),
                RoomRegion(otherRoom, 2, "Zone01/Other", room));
            var memory = CreateMemory();
            RegisterRegion(memory, room, 0, 1, 2);
            MarkObserved(memory, room, 0);
            AttachRoomSweepState(controller, spatialGraph, regionGraph, memory);
            AttachNavigationAndLocalSelector(controller, spatialGraph, regionGraph);

            var result = (bool)InvokePrivate(controller, "SetRoomSweepPatrolDestination", Type.EmptyTypes);

            Assert.That(result, Is.False);
            Assert.That(IsRegionCleared(memory, room), Is.False);
            Assert.That(GetPlannerInt(controller, "RejectedProbeCount"), Is.EqualTo(2));
            Assert.That(GetPrivateCollectionCount(controller, "_rejectedRoomSweepGlobalRegionIds"), Is.EqualTo(0));
        }

        [Test]
        public void STK_RoomSweepIntegration_ProbeFailuresExhaustingRoomActivateFallbackWithoutClearing()
        {
            var controller = CreateController();
            var room = new RegionId(1);
            var spatialGraph = CreateSpatialGraph(
                Node(0, Vector3.zero, 1),
                Node(1, Vector3.right, 0));
            var regionGraph = CreateRegionGraph(
                spatialGraph,
                new[] { room, room },
                RoomRegion(room, 1, "Zone01/Room"));
            var memory = CreateMemory();
            AttachRoomSweepState(controller, spatialGraph, regionGraph, memory);
            AttachNavigationAndLocalSelector(controller, spatialGraph, regionGraph);
            Invoke(GetPrivateField(controller, "_roomSweepPlanner"), "TryBeginRegion", new[] { typeof(RegionId) }, room);
            MarkObserved(memory, room, 0);
            SetPrivateField(controller, "patrolMode", Enum.Parse(StalkerPatrolModeType, "RoomSweepSpatial"));
            SetBlackboardNode(controller, "CurrentSpatialNodeId", 0);
            SetBlackboardNode(controller, "DestinationSpatialNodeId", 1);
            SetNavigationObjective(controller, "RoomSweepProbe", 1, room.Value);
            SetPrivateField(controller, "_navigationRecoveryAttemptUsed", true);

            InvokePrivate(controller, "HandleRoomSweepNavigationFailure", new[] { NavigationFailureReasonType }, Enum.Parse(NavigationFailureReasonType, "PathInvalid"));

            Assert.That(IsRegionCleared(memory, room), Is.False);
            Assert.That((bool)GetProperty(controller, "FixedFallbackActive"), Is.True);
        }

        [Test]
        public void STK_RoomSweepIntegration_FsmResumeClearsProbeRejectsAndPreservesVisualProgressAndClearedRooms()
        {
            var controller = CreateController();
            var room = new RegionId(1);
            var spatialGraph = CreateSpatialGraph(Node(0, Vector3.zero), Node(1, Vector3.right));
            var regionGraph = CreateRegionGraph(
                spatialGraph,
                new[] { room, room },
                RoomRegion(room, 1, "Zone01/Room"));
            var memory = CreateMemory();
            RegisterRegion(memory, room, 0, 1);
            MarkObserved(memory, room, 0);
            AttachRoomSweepState(controller, spatialGraph, regionGraph, memory);
            AttachNavigationAndLocalSelector(controller, spatialGraph, regionGraph);
            AttachVision(controller, Vector3.zero, Vector3.right, 10f, 180f);
            var planner = GetPrivateField(controller, "_roomSweepPlanner");
            Invoke(planner, "TryBeginRegion", new[] { typeof(RegionId) }, room);
            Invoke(planner, "RejectProbe", new[] { typeof(int) }, 1);
            SetPrivateField(controller, "patrolMode", Enum.Parse(StalkerPatrolModeType, "RoomSweepSpatial"));

            InvokePrivate(controller, "SetCurrentPatrolDestination", Type.EmptyTypes);

            Assert.That(GetPlannerInt(controller, "RejectedProbeCount"), Is.EqualTo(0));
            Assert.That(IsObserved(memory, room, 0), Is.True);
            MarkRegionCleared(memory, room);
            InvokePrivate(controller, "SetCurrentPatrolDestination", Type.EmptyTypes);
            Assert.That(IsRegionCleared(memory, room), Is.True);
        }

        [Test]
        public void STK_RoomSweepIntegration_TransitRequestFailureExhaustsLocalCandidatesBeforeRejectingGlobalTarget()
        {
            var controller = CreateController();
            var route = new RegionId(1);
            var nextRoute = new RegionId(2);
            var targetRoom = new RegionId(3);
            var spatialGraph = CreateSpatialGraph(
                Node(0, Vector3.zero, 1, 2),
                Node(1, Vector3.right, 0),
                Node(2, Vector3.left, 0));
            var regionGraph = CreateRegionGraph(
                spatialGraph,
                new[] { route, nextRoute, nextRoute },
                RouteRegion(route, 1, "Zone01/Route", nextRoute),
                RouteRegion(nextRoute, 2, "Zone01/NextRoute", route, targetRoom),
                RoomRegion(targetRoom, 3, "Zone01/Target", nextRoute));
            AttachRoomSweepState(controller, spatialGraph, regionGraph, CreateMemory());
            AttachNavigationAndLocalSelector(controller, spatialGraph, regionGraph);

            var localOnlyResult = (bool)InvokePrivate(controller, "SetRoomSweepTransitDestination", Type.EmptyTypes);

            Assert.That(localOnlyResult, Is.False);
            Assert.That(GetPrivateCollectionCount(controller, "_rejectedRoomSweepTransitNodeIds"), Is.EqualTo(2));
            Assert.That(GetPrivateCollectionCount(controller, "_rejectedRoomSweepGlobalRegionIds"), Is.EqualTo(0));

            var args = new object[] { null };
            var globalResult = (bool)InvokePrivate(
                controller,
                "TrySetRoomSweepTransitDestinationWithGlobalAlternates",
                new[] { NavigationRecoveryReasonType.MakeByRefType() },
                args);

            Assert.That(globalResult, Is.False);
            Assert.That(GetPrivateCollectionCount(controller, "_rejectedRoomSweepGlobalRegionIds"), Is.EqualTo(1));
        }

        [Test]
        public void STK_RoomSweepIntegration_ActiveTransitCannotClearAnotherRoom()
        {
            var controller = CreateController();
            var roomA = new RegionId(1);
            var roomB = new RegionId(2);
            var spatialGraph = CreateSpatialGraph(
                Node(0, Vector3.zero),
                Node(1, new Vector3(10f, 0f, 0f)),
                Node(2, new Vector3(11f, 0f, 0f)));
            var regionGraph = CreateRegionGraph(
                spatialGraph,
                new[] { roomA, roomB, roomB },
                RoomRegion(roomA, 1, "Zone01/RoomA", roomB),
                RoomRegion(roomB, 2, "Zone01/RoomB", roomA));
            var memory = CreateMemory();
            RegisterRegion(memory, roomA, 0);
            RegisterRegion(memory, roomB, 1, 2);
            MarkObserved(memory, roomA, 0);
            AttachRoomSweepState(controller, spatialGraph, regionGraph, memory);
            AttachVision(controller, new Vector3(10f, 0f, 0f), Vector3.right, 10f, 180f);
            ((Component)controller).transform.position = new Vector3(10f, 0f, 0f);
            Invoke(GetPrivateField(controller, "_roomSweepPlanner"), "TryBeginRegion", new[] { typeof(RegionId) }, roomA);
            SetNavigationObjective(controller, "RoomSweepTransit", 1, roomB.Value);

            var processed = ProcessActiveProbeCoverage(controller, out var clearedRegionId);

            Assert.That(processed, Is.False);
            Assert.That(clearedRegionId, Is.EqualTo(RegionId.Invalid));
            Assert.That(IsRegionCleared(memory, roomB), Is.False);
            Assert.That(GetMemoryInt(memory, "GetObservedCount", roomB), Is.EqualTo(0));
        }

        [Test]
        public void STK_RoomSweepIntegration_ActiveProbeUsesOnlyItsOwnRoom()
        {
            var controller = CreateController();
            var roomB = new RegionId(2);
            var spatialGraph = CreateSpatialGraph(
                Node(0, new Vector3(10f, 0f, 0f)),
                Node(1, new Vector3(11f, 0f, 0f)));
            var regionGraph = CreateRegionGraph(
                spatialGraph,
                new[] { roomB, roomB },
                RoomRegion(roomB, 2, "Zone01/RoomB"));
            var memory = CreateMemory();
            AttachRoomSweepState(controller, spatialGraph, regionGraph, memory);
            AttachVision(controller, new Vector3(10f, 0f, 0f), Vector3.right, 10f, 180f);
            ((Component)controller).transform.position = new Vector3(10f, 0f, 0f);
            Invoke(GetPrivateField(controller, "_roomSweepPlanner"), "TryBeginRegion", new[] { typeof(RegionId) }, roomB);
            SetNavigationObjective(controller, "RoomSweepProbe", 1, roomB.Value);

            var processed = ProcessActiveProbeCoverage(controller, out var clearedRegionId);

            Assert.That(processed, Is.True);
            Assert.That(clearedRegionId, Is.EqualTo(roomB));
            Assert.That(GetMemoryInt(memory, "GetObservedCount", roomB), Is.EqualTo(2));
            Assert.That(IsRegionCleared(memory, roomB), Is.True);
        }

        [Test]
        public void STK_RoomSweepIntegration_OrdinaryActiveProbeTickPreservesRejections()
        {
            var controller = CreateController();
            var roomB = new RegionId(2);
            var spatialGraph = CreateSpatialGraph(
                Node(0, Vector3.zero),
                Node(1, Vector3.right));
            var regionGraph = CreateRegionGraph(
                spatialGraph,
                new[] { roomB, roomB },
                RoomRegion(roomB, 2, "Zone01/RoomB"));
            var memory = CreateMemory();
            AttachRoomSweepState(controller, spatialGraph, regionGraph, memory);
            ((Component)controller).transform.position = Vector3.zero;
            var planner = GetPrivateField(controller, "_roomSweepPlanner");
            Invoke(planner, "TryBeginRegion", new[] { typeof(RegionId) }, roomB);
            Invoke(planner, "RejectProbe", new[] { typeof(int) }, 1);
            SetNavigationObjective(controller, "RoomSweepProbe", 0, roomB.Value);

            var processed = ProcessActiveProbeCoverage(controller, out _);

            Assert.That(processed, Is.False);
            Assert.That(GetPlannerInt(controller, "RejectedProbeCount"), Is.EqualTo(1));
        }

        [Test]
        public void STK_RoomSweepIntegration_FullCoverageDuringActiveProbeCancelsStaleProbe()
        {
            var controller = CreateController();
            var roomB = new RegionId(2);
            var spatialGraph = CreateSpatialGraph(
                Node(0, Vector3.zero),
                Node(1, Vector3.right));
            var regionGraph = CreateRegionGraph(
                spatialGraph,
                new[] { roomB, roomB },
                RoomRegion(roomB, 2, "Zone01/RoomB"));
            var memory = CreateMemory();
            AttachRoomSweepState(controller, spatialGraph, regionGraph, memory);
            AttachNavigationAndLocalSelector(controller, spatialGraph, regionGraph);
            AttachVision(controller, Vector3.zero, Vector3.right, 10f, 180f);
            ((Component)controller).transform.position = Vector3.zero;
            Invoke(GetPrivateField(controller, "_roomSweepPlanner"), "TryBeginRegion", new[] { typeof(RegionId) }, roomB);
            SetBlackboardNode(controller, "DestinationSpatialNodeId", 1);
            SetNavigationObjective(controller, "RoomSweepProbe", 1, roomB.Value);

            var cancelled = (bool)InvokePrivate(controller, "TryCancelCompletedActiveRoomSweepProbe", Type.EmptyTypes);

            Assert.That(cancelled, Is.True);
            Assert.That(IsRegionCleared(memory, roomB), Is.True);
            Assert.That(GetBlackboardNode(controller, "DestinationSpatialNodeId"), Is.EqualTo(-1));
            Assert.That(GetNavigationObjectiveKindName(controller), Is.EqualTo("None"));
        }

        [Test]
        public void STK_RoomSweepIntegration_CancelledCompletedProbeInvalidatesClearedGlobalObjectiveBeforeNextRoomPlanning()
        {
            var controller = CreateController();
            var roomA = new RegionId(18);
            var roomB = new RegionId(19);
            var spatialGraph = CreateSpatialGraph(
                Node(0, Vector3.zero, 1),
                Node(1, Vector3.right, 0));
            var regionGraph = CreateRegionGraph(
                spatialGraph,
                new[] { roomA, roomB },
                RoomRegion(roomA, 18, "Zone01/RoomA", roomB),
                RoomRegion(roomB, 19, "Zone01/RoomB", roomA));
            var memory = CreateMemory();
            AttachRoomSweepState(controller, spatialGraph, regionGraph, memory);
            AttachNavigationAndLocalSelector(controller, spatialGraph, regionGraph);
            AttachVision(controller, Vector3.zero, Vector3.right, 10f, 180f);
            ((Component)controller).transform.position = Vector3.zero;

            var globalPlanner = GetPrivateField(controller, "_roomSweepGlobalPlanner");
            var objectiveArgs = new object[] { roomB, null };
            var hasInitialObjective = (bool)Invoke(
                globalPlanner,
                "TryGetOrCreateObjective",
                new[] { typeof(RegionId), RoomSweepGlobalObjectiveType.MakeByRefType() },
                objectiveArgs);
            Assert.That(hasInitialObjective, Is.True);
            Assert.That((RegionId)GetProperty(objectiveArgs[1], "TargetRoomRegionId"), Is.EqualTo(roomA));

            Invoke(GetPrivateField(controller, "_roomSweepPlanner"), "TryBeginRegion", new[] { typeof(RegionId) }, roomA);
            SetBlackboardNode(controller, "DestinationSpatialNodeId", 0);
            SetNavigationObjective(controller, "RoomSweepProbe", 0, roomA.Value);

            var cancelled = (bool)InvokePrivate(controller, "TryCancelCompletedActiveRoomSweepProbe", Type.EmptyTypes);

            Assert.That(cancelled, Is.True);
            Assert.That(IsRegionCleared(memory, roomA), Is.True);
            Assert.That(GetProperty(globalPlanner, "LastInvalidationReason").ToString(), Is.EqualTo("TargetCleared"));
            Assert.That((bool)GetProperty(GetProperty(globalPlanner, "CurrentObjective"), "IsValid"), Is.False);

            var nextObjectiveArgs = new object[] { roomA, null };
            var hasNextObjective = (bool)Invoke(
                globalPlanner,
                "TryGetOrCreateObjective",
                new[] { typeof(RegionId), RoomSweepGlobalObjectiveType.MakeByRefType() },
                nextObjectiveArgs);

            Assert.That(hasNextObjective, Is.True);
            Assert.That((RegionId)GetProperty(nextObjectiveArgs[1], "TargetRoomRegionId"), Is.EqualTo(roomB));
            Assert.That(IsRegionCleared(memory, roomB), Is.False);
            Assert.That(GetBlackboardNode(controller, "DestinationSpatialNodeId"), Is.EqualTo(-1));
            Assert.That(GetNavigationObjectiveKindName(controller), Is.EqualTo("None"));
        }

        private object CreateController()
        {
            var gameObject = new GameObject("STK_RoomSweepControllerTest");
            _createdObjects.Add(gameObject);
            return gameObject.AddComponent(StalkerControllerType);
        }

        private void AttachVision(object controller, Vector3 originPosition, Vector3 forward, float distance, float angle)
        {
            var visionObject = new GameObject("STK_RoomSweepVisionTest");
            _createdObjects.Add(visionObject);
            visionObject.transform.position = originPosition;
            visionObject.transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
            var sensor = visionObject.AddComponent(StalkerVisionSensorType);
            SetPrivateField(sensor, "visionOrigin", visionObject.transform);
            SetPrivateField(sensor, "visionDistance", distance);
            SetPrivateField(sensor, "visionAngle", angle);
            SetPrivateField(sensor, "losBlockerMask", new LayerMask { value = Physics.DefaultRaycastLayers });
            SetPrivateField(controller, "visionSensor", sensor);
        }

        private void AttachVisionWithLocalOrigin(
            object controller,
            Vector3 rootPosition,
            Vector3 originLocalPosition,
            Vector3 forward,
            float distance,
            float angle)
        {
            var visionObject = new GameObject("STK_RoomSweepVisionTest");
            _createdObjects.Add(visionObject);
            visionObject.transform.position = rootPosition;
            visionObject.transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);

            var originObject = new GameObject("STK_RoomSweepVisionOriginTest");
            _createdObjects.Add(originObject);
            originObject.transform.SetParent(visionObject.transform, false);
            originObject.transform.localPosition = originLocalPosition;
            originObject.transform.localRotation = Quaternion.identity;

            var sensor = visionObject.AddComponent(StalkerVisionSensorType);
            SetPrivateField(sensor, "visionOrigin", originObject.transform);
            SetPrivateField(sensor, "visionDistance", distance);
            SetPrivateField(sensor, "visionAngle", angle);
            SetPrivateField(sensor, "losBlockerMask", new LayerMask { value = Physics.DefaultRaycastLayers });
            SetPrivateField(controller, "visionSensor", sensor);
        }

        private void AttachChildVisionWithLocalOrigin(
            object controller,
            Vector3 localPosition,
            Vector3 originLocalPosition,
            Vector3 forward,
            float distance,
            float angle)
        {
            var controllerComponent = (Component)controller;
            var visionObject = new GameObject("STK_RoomSweepChildVisionTest");
            _createdObjects.Add(visionObject);
            visionObject.transform.SetParent(controllerComponent.transform, false);
            visionObject.transform.localPosition = localPosition;
            visionObject.transform.localRotation = Quaternion.LookRotation(forward.normalized, Vector3.up);

            var originObject = new GameObject("STK_RoomSweepChildVisionOriginTest");
            _createdObjects.Add(originObject);
            originObject.transform.SetParent(visionObject.transform, false);
            originObject.transform.localPosition = originLocalPosition;
            originObject.transform.localRotation = Quaternion.identity;

            var sensor = visionObject.AddComponent(StalkerVisionSensorType);
            SetPrivateField(sensor, "visionOrigin", originObject.transform);
            SetPrivateField(sensor, "visionDistance", distance);
            SetPrivateField(sensor, "visionAngle", angle);
            SetPrivateField(sensor, "losBlockerMask", new LayerMask { value = Physics.DefaultRaycastLayers });
            SetPrivateField(controller, "visionSensor", sensor);
        }

        private void CreateBlocker(Vector3 position, Vector3 scale)
        {
            var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _createdObjects.Add(blocker);
            blocker.transform.position = position;
            blocker.transform.localScale = scale;
            Physics.SyncTransforms();
        }

        private static void AttachRoomSweepState(object controller, object spatialGraph, object regionGraph, object memory)
        {
            SetPrivateField(controller, "_spatialPatrolGraph", spatialGraph);
            SetPrivateField(controller, "_regionGraph", regionGraph);
            SetPrivateField(controller, "_roomSweepCoverageMemory", memory);
            SetPrivateField(controller, "_roomSweepPlanner", Activator.CreateInstance(RoomSweepPlannerType, spatialGraph, regionGraph, memory));
            SetPrivateField(controller, "_roomSweepGlobalPlanner", Activator.CreateInstance(RoomSweepGlobalPlannerType, regionGraph, memory));
        }

        private static void AttachNavigationAndLocalSelector(object controller, object spatialGraph, object regionGraph)
        {
            var coverageMemory = Activator.CreateInstance(CoverageMemoryType, GetProperty(spatialGraph, "NodeCount"), regionGraph);
            var localSelector = Activator.CreateInstance(
                LocalPatrolSelectorType,
                spatialGraph,
                regionGraph,
                coverageMemory,
                8,
                null);
            SetPrivateField(controller, "_coverageMemory", coverageMemory);
            SetPrivateField(controller, "_localPatrolSelector", localSelector);
            SetPrivateField(controller, "_navigation", Activator.CreateInstance(StalkerNavigationControllerType, new object[] { null }));
        }

        private static int GetPlannerInt(object controller, string propertyName)
        {
            return (int)GetProperty(GetPrivateField(controller, "_roomSweepPlanner"), propertyName);
        }

        private static int GetPrivateCollectionCount(object target, string fieldName)
        {
            return (int)GetProperty(GetPrivateField(target, fieldName), "Count");
        }

        private static string GetNavigationObjectiveKindName(object controller)
        {
            return GetProperty(GetPrivateField(controller, "_navigationObjectiveKey"), "Kind").ToString();
        }

        private static bool ProcessActiveProbeCoverage(object controller, out RegionId clearedRegionId)
        {
            var args = new object[] { RegionId.Invalid };
            var result = (bool)InvokePrivate(
                controller,
                "TryProcessActiveRoomSweepProbeVisualCoverage",
                new[] { typeof(RegionId).MakeByRefType() },
                args);
            clearedRegionId = (RegionId)args[0];
            return result;
        }

        private static void SetNavigationObjective(object controller, string kindName, int localNodeId, int globalRegionId)
        {
            SetPrivateField(
                controller,
                "_navigationObjectiveKey",
                Activator.CreateInstance(
                    NavigationObjectiveKeyType,
                    Enum.Parse(StalkerNavigationObjectiveKindType, kindName),
                    localNodeId,
                    globalRegionId,
                    -1));
        }

        private static object CreateMemory()
        {
            return Activator.CreateInstance(RoomSweepCoverageMemoryType);
        }

        private static void RegisterRegion(object memory, RegionId regionId, params int[] nodeIds)
        {
            Invoke(memory, "RegisterRegion", new[] { typeof(RegionId), typeof(IReadOnlyCollection<int>) }, regionId, nodeIds);
        }

        private static bool MarkObserved(object memory, RegionId regionId, int nodeId)
        {
            return (bool)Invoke(memory, "MarkObserved", new[] { typeof(RegionId), typeof(int) }, regionId, nodeId);
        }

        private static int GetMemoryInt(object memory, string methodName, RegionId regionId)
        {
            return (int)Invoke(memory, methodName, new[] { typeof(RegionId) }, regionId);
        }

        private static void MarkRegionCleared(object memory, RegionId regionId)
        {
            Invoke(memory, "MarkRegionCleared", new[] { typeof(RegionId) }, regionId);
        }

        private static bool IsObserved(object memory, RegionId regionId, int nodeId)
        {
            return (bool)Invoke(memory, "IsObserved", new[] { typeof(RegionId), typeof(int) }, regionId, nodeId);
        }

        private static bool IsRegionCleared(object memory, RegionId regionId)
        {
            return (bool)Invoke(memory, "IsRegionCleared", new[] { typeof(RegionId) }, regionId);
        }

        private static object CreateSpatialGraph(params object[] nodes)
        {
            return Activator.CreateInstance(GraphType, ToArray(NodeType, nodes));
        }

        private static object Node(int id, Vector3 position, params int[] neighbors)
        {
            return Activator.CreateInstance(
                NodeType,
                id,
                position,
                0,
                id,
                id * 3,
                id * 3 + 1,
                id * 3 + 2,
                new List<int>(neighbors));
        }

        private static object CreateRegionGraph(object spatialGraph, IReadOnlyList<RegionId> nodeToRegionMap, params object[] regionNodes)
        {
            return Activator.CreateInstance(
                RegionGraphType,
                ToArray(RegionNodeType, regionNodes),
                nodeToRegionMap,
                GetProperty(spatialGraph, "CompatibilityIdentity"),
                1);
        }

        private static object RoomRegion(RegionId regionId, int sourceIndex, string sourcePath, params RegionId[] edges)
        {
            return SemanticRegion(regionId, sourceIndex, sourcePath, "Zone01", "Room", edges);
        }

        private static object RouteRegion(RegionId regionId, int sourceIndex, string sourcePath, params RegionId[] edges)
        {
            return SemanticRegion(regionId, sourceIndex, sourcePath, "Zone01", "Route", edges);
        }

        private static object SemanticRegion(
            RegionId regionId,
            int sourceIndex,
            string sourcePath,
            string zone,
            string kind,
            params RegionId[] edges)
        {
            return Activator.CreateInstance(
                RegionNodeType,
                regionId,
                EdgeArray(edges),
                Metadata(sourceIndex, sourcePath, zone, kind));
        }

        private static Array EdgeArray(params RegionId[] regionIds)
        {
            var array = Array.CreateInstance(RegionEdgeType, regionIds?.Length ?? 0);
            for (var i = 0; i < array.Length; i++)
            {
                array.SetValue(Activator.CreateInstance(RegionEdgeType, regionIds[i], DoorId.Invalid), i);
            }

            return array;
        }

        private static object Metadata(int sourceIndex, string sourcePath, string zone, string kind)
        {
            return Activator.CreateInstance(
                RegionSemanticMetadataType,
                sourceIndex,
                sourcePath,
                Enum.Parse(RegionSemanticZoneType, zone),
                Enum.Parse(RegionSemanticKindType, kind));
        }

        private static void SetBlackboardNode(object controller, string propertyName, int value)
        {
            var blackboard = GetProperty(controller, "Blackboard");
            var property = blackboard.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null);
            property.SetValue(blackboard, value);
        }

        private static int GetBlackboardNode(object controller, string propertyName)
        {
            var blackboard = GetProperty(controller, "Blackboard");
            var property = blackboard.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null);
            return (int)property.GetValue(blackboard);
        }

        private static object InvokePrivate(object target, string methodName, Type[] parameterTypes, params object[] args)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic, null, parameterTypes, null);
            Assert.That(method, Is.Not.Null, $"Missing private method '{methodName}' on '{target.GetType().FullName}'.");
            return method.Invoke(target, args);
        }

        private static object Invoke(object target, string methodName, Type[] parameterTypes, params object[] args)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public, null, parameterTypes, null);
            Assert.That(method, Is.Not.Null, $"Missing public method '{methodName}' on '{target.GetType().FullName}'.");
            return method.Invoke(target, args);
        }

        private static object GetPrivateField(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing private field '{fieldName}' on '{target.GetType().FullName}'.");
            return field.GetValue(target);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing private field '{fieldName}' on '{target.GetType().FullName}'.");
            field.SetValue(target, value);
        }

        private static object GetProperty(object target, string propertyName)
        {
            var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, $"Missing public property '{propertyName}' on '{target.GetType().FullName}'.");
            return property.GetValue(target);
        }

        private static int EnumValue(Type enumType, string name)
        {
            return (int)Enum.Parse(enumType, name);
        }

        private static Array ToArray(Type elementType, object[] values)
        {
            var array = Array.CreateInstance(elementType, values?.Length ?? 0);
            for (var i = 0; i < array.Length; i++)
            {
                array.SetValue(values[i], i);
            }

            return array;
        }

        private static Type ResolveType(string fullTypeName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(fullTypeName, false);
                if (type != null)
                {
                    return type;
                }
            }

            var assemblyCSharp = Assembly.Load("Assembly-CSharp");
            var loadedType = assemblyCSharp.GetType(fullTypeName, false);
            if (loadedType != null)
            {
                return loadedType;
            }

            Assert.Fail($"Could not find production type '{fullTypeName}'.");
            return null;
        }

        private static Type StalkerControllerType => ResolveType("EchoProtocol.AI.Stalker.StalkerController");
        private static Type StalkerStateType => ResolveType("EchoProtocol.AI.Stalker.StalkerState");
        private static Type StalkerNavigationControllerType => ResolveType("EchoProtocol.AI.Stalker.StalkerNavigationController");
        private static Type NavigationFailureReasonType => ResolveType("EchoProtocol.AI.Stalker.NavigationFailureReason");
        private static Type NavigationRecoveryReasonType => ResolveType("EchoProtocol.AI.Stalker.NavigationRecoveryReason");
        private static Type NavigationObjectiveKeyType => ResolveType("EchoProtocol.AI.Stalker.StalkerNavigationObjectiveKey");
        private static Type StalkerPatrolModeType => ResolveType("EchoProtocol.AI.Stalker.StalkerPatrolMode");
        private static Type StalkerNavigationObjectiveKindType => ResolveType("EchoProtocol.AI.Stalker.StalkerNavigationObjectiveKind");
        private static Type StalkerVisionSensorType => ResolveType("EchoProtocol.AI.Stalker.StalkerVisionSensor");
        private static Type GraphType => ResolveType("EchoProtocol.AI.Stalker.Spatial.NavMeshSpatialGraph");
        private static Type NodeType => ResolveType("EchoProtocol.AI.Stalker.Spatial.SpatialNode");
        private static Type RegionGraphType => ResolveType("EchoProtocol.AI.Stalker.Spatial.RegionGraph");
        private static Type RegionNodeType => ResolveType("EchoProtocol.AI.Stalker.Spatial.RegionNode");
        private static Type RegionEdgeType => ResolveType("EchoProtocol.AI.Stalker.Spatial.RegionEdge");
        private static Type RegionSemanticMetadataType => ResolveType("EchoProtocol.AI.Stalker.Spatial.RegionSemanticMetadata");
        private static Type RegionSemanticZoneType => ResolveType("EchoProtocol.AI.Stalker.Spatial.RegionSemanticZone");
        private static Type RegionSemanticKindType => ResolveType("EchoProtocol.AI.Stalker.Spatial.RegionSemanticKind");
        private static Type CoverageMemoryType => ResolveType("EchoProtocol.AI.Stalker.Spatial.CoverageMemory");
        private static Type LocalPatrolSelectorType => ResolveType("EchoProtocol.AI.Stalker.Spatial.LocalPatrolSelector");
        private static Type RoomSweepCoverageMemoryType => ResolveType("EchoProtocol.AI.Stalker.Spatial.RoomSweepCoverageMemory");
        private static Type RoomSweepPlannerType => ResolveType("EchoProtocol.AI.Stalker.Spatial.RoomSweepPlanner");
        private static Type RoomSweepGlobalPlannerType => ResolveType("EchoProtocol.AI.Stalker.Spatial.RoomSweepGlobalPlanner");
        private static Type RoomSweepGlobalObjectiveType => ResolveType("EchoProtocol.AI.Stalker.Spatial.RoomSweepGlobalObjective");
    }
}
