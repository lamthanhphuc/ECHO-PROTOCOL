using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace EchoProtocol.AI.Stalker.Tests
{
    public sealed class StalkerNavigationProgressPlayModeTests
    {
        private const string NavigationControllerTypeName = "EchoProtocol.AI.Stalker.StalkerNavigationController";
        private const string NavigationProgressSettingsTypeName = "EchoProtocol.AI.Stalker.NavigationProgressSettings";
        private const string NavigationRequestIntentTypeName = "EchoProtocol.AI.Stalker.NavigationRequestIntent";
        private const string StalkerControllerTypeName = "EchoProtocol.AI.Stalker.StalkerController";
        private const string PatrolRouteTypeName = "EchoProtocol.AI.Stalker.PatrolRoute";

        private const float PathSettleTimeoutSeconds = 2f;
        private const int PathSettleFrameCap = 1000;
        private const float ProgressTickDeltaTime = 0.11f;
        private const float UpdateDrivenNoProgressTimeoutSeconds = 2f;

        private static readonly Vector3 AgentStart = new Vector3(-3f, 0f, 0f);
        private static readonly Vector3 Destination = new Vector3(3f, 0f, 0f);
        private static readonly Vector3 IslandACenter = new Vector3(-3f, -0.05f, 0f);
        private static readonly Vector3 IslandBCenter = new Vector3(3f, -0.05f, 0f);
        private static readonly Vector3 IslandSize = new Vector3(3f, 0.1f, 4f);
        private static readonly Vector3 TrackedDestination = new Vector3(2.5f, 0f, 1f);
        private static readonly Vector3 NewGoalDestination = new Vector3(2.5f, 0f, -1f);
        private static readonly Vector3 ChaseDestinationA = new Vector3(1f, 0f, 0f);
        private static readonly Vector3 ChaseDestinationB = new Vector3(1.1f, 0f, 0f);
        private static readonly Vector3 ChaseDestinationC = new Vector3(1.2f, 0f, 0f);
        private static readonly Vector3 ChaseDestinationD = new Vector3(1.3f, 0f, 0f);
        private static readonly Vector3 ChaseDestinationE = new Vector3(1.4f, 0f, 0f);
        private static readonly Vector3 ChaseDestinationF = new Vector3(1.5f, 0f, 0f);

        private readonly List<GameObject> _createdObjects = new List<GameObject>();
        private NavMeshDataInstance _navMeshDataInstance;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            for (var i = _createdObjects.Count - 1; i >= 0; i--)
            {
                if (_createdObjects[i] != null)
                {
                    UnityEngine.Object.Destroy(_createdObjects[i]);
                }
            }

            _createdObjects.Clear();
            yield return null;

            if (_navMeshDataInstance.valid)
            {
                _navMeshDataInstance.Remove();
                _navMeshDataInstance = default;
            }
        }

        [UnityTest]
        public IEnumerator NAV_4B_CompleteStoppedAgent_BecomesNoProgress()
        {
            var fixture = CreateFixture();
            yield return fixture.ActivateAndWait();
            yield return RequestAndSettleCompletePath(fixture);

            StopAgent(fixture.Agent);
            yield return null;
            AssertStoppedCompletePath(fixture.Agent, fixture.Controller);

            TickProgress(fixture.Controller, ProgressTickDeltaTime);
            Assert.That(GetExecutionStatusName(fixture.Controller), Is.EqualTo("Moving"));

            TickProgress(fixture.Controller, ProgressTickDeltaTime);
            Assert.That(GetExecutionStatusName(fixture.Controller), Is.EqualTo("Moving"));

            TickProgress(fixture.Controller, ProgressTickDeltaTime);

            Assert.That(GetExecutionStatusName(fixture.Controller), Is.EqualTo("NoProgress"));
            Assert.That(GetPathStatusName(fixture.Controller), Is.EqualTo("Complete"));
            Assert.That(HasArrived(fixture.Controller), Is.False);
        }

        [UnityTest]
        public IEnumerator NAV_4B_CompleteStoppedAgent_BecomesStuck()
        {
            var fixture = CreateFixture();
            yield return fixture.ActivateAndWait();
            yield return RequestAndSettleCompletePath(fixture);

            StopAgent(fixture.Agent);
            yield return null;
            AssertStoppedCompletePath(fixture.Agent, fixture.Controller);

            TickProgress(fixture.Controller, ProgressTickDeltaTime);
            for (var i = 0; i < 5; i++)
            {
                TickProgress(fixture.Controller, ProgressTickDeltaTime);
            }

            Assert.That(GetExecutionStatusName(fixture.Controller), Is.EqualTo("Stuck"));
            Assert.That(GetPathStatusName(fixture.Controller), Is.EqualTo("Complete"));
            Assert.That(HasArrived(fixture.Controller), Is.False);
        }

        [UnityTest]
        public IEnumerator NAV_4B_ForceRepathAccepted_ResetsProgressToMoving()
        {
            var fixture = CreateFixture();
            yield return fixture.ActivateAndWait();
            yield return RequestAndSettleCompletePath(fixture);

            StopAgent(fixture.Agent);
            yield return null;
            AssertStoppedCompletePath(fixture.Agent, fixture.Controller);

            TickProgress(fixture.Controller, ProgressTickDeltaTime);
            TickProgress(fixture.Controller, ProgressTickDeltaTime);
            TickProgress(fixture.Controller, ProgressTickDeltaTime);
            Assert.That(GetExecutionStatusName(fixture.Controller), Is.EqualTo("NoProgress"));

            AssertPlanResult(RequestDestination(fixture.Controller, Destination, true), "Accepted", true);
            yield return WaitUntilComplete(fixture.Agent, fixture.Controller, PathSettleTimeoutSeconds, PathSettleFrameCap);

            Assert.That(GetExecutionStatusName(fixture.Controller), Is.EqualTo("Moving"));

            TickProgress(fixture.Controller, ProgressTickDeltaTime);
            Assert.That(GetExecutionStatusName(fixture.Controller), Is.EqualTo("Moving"));
        }

        [UnityTest]
        public IEnumerator NAV_4C_TrackMovingGoalAccepted_PreservesNoProgressHistory()
        {
            var fixture = CreateFixture();
            yield return fixture.ActivateAndWait();
            yield return RequestAndSettleCompletePath(fixture);

            StopAgent(fixture.Agent);
            yield return null;
            AssertStoppedCompletePath(fixture.Agent, fixture.Controller);
            TickUntilNoProgress(fixture.Controller);

            Assert.That(GetPathStatusName(fixture.Controller), Is.EqualTo("Complete"));
            Assert.That(HasArrived(fixture.Controller), Is.False);
            Assert.That(GetExecutionStatusName(fixture.Controller), Is.EqualTo("NoProgress"));
            AssertPointOnNavMesh(TrackedDestination);

            AssertPlanResult(
                RequestDestination(fixture.Controller, TrackedDestination, "TrackMovingGoal"),
                "Accepted",
                true);
            yield return WaitUntilComplete(fixture.Agent, fixture.Controller, PathSettleTimeoutSeconds, PathSettleFrameCap);

            Assert.That(GetExecutionStatusName(fixture.Controller), Is.EqualTo("NoProgress"));
            Assert.That(HasArrived(fixture.Controller), Is.False);
            Assert.That(fixture.Agent.isStopped, Is.True);
        }

        [UnityTest]
        public IEnumerator NAV_4C_NewGoalAccepted_ResetsNoProgressHistory()
        {
            var fixture = CreateFixture();
            yield return fixture.ActivateAndWait();
            yield return RequestAndSettleCompletePath(fixture);

            StopAgent(fixture.Agent);
            yield return null;
            AssertStoppedCompletePath(fixture.Agent, fixture.Controller);
            TickUntilNoProgress(fixture.Controller);
            Assert.That(GetExecutionStatusName(fixture.Controller), Is.EqualTo("NoProgress"));
            AssertPointOnNavMesh(NewGoalDestination);

            AssertPlanResult(
                RequestDestination(fixture.Controller, NewGoalDestination, "NewGoal"),
                "Accepted",
                true);
            yield return WaitUntilComplete(fixture.Agent, fixture.Controller, PathSettleTimeoutSeconds, PathSettleFrameCap);

            Assert.That(GetExecutionStatusName(fixture.Controller), Is.EqualTo("Moving"));

            TickProgress(fixture.Controller, ProgressTickDeltaTime);
            Assert.That(GetExecutionStatusName(fixture.Controller), Is.EqualTo("Moving"));
        }

        [UnityTest]
        public IEnumerator NAV_4C2_ChaseWithoutRecordedDestination_RequestsImmediately()
        {
            var fixture = CreateChaseCadenceFixture(0.5f, 100f);
            yield return fixture.ActivateInitializeAndDisable();
            ResetChaseCadence(fixture.StalkerController);

            Assert.That(GetHasLastChaseRequestedDestination(fixture.StalkerController), Is.False);
            var destination = SampleChaseDestinationPointOnNavMesh(Destination);

            InvokeSetChaseDestination(fixture.StalkerController, destination);

            Assert.That(GetHasLastChaseRequestedDestination(fixture.StalkerController), Is.True);
            AssertVectorApproximately(GetLastChaseRequestedDestination(fixture.StalkerController), destination);
            Assert.That(GetNavigationHasActiveDestination(fixture.StalkerController), Is.True);
        }

        [UnityTest]
        public IEnumerator NAV_4C2_ChaseSmallCumulativeMotion_SuppressedUntilThreshold()
        {
            var fixture = CreateChaseCadenceFixture(0.5f, 100f);
            yield return fixture.ActivateInitializeAndDisable();

            var destinationA = SampleChaseDestinationPointOnNavMesh(ChaseDestinationA);
            var destinationB = SampleChaseDestinationPointOnNavMesh(ChaseDestinationB);
            var destinationC = SampleChaseDestinationPointOnNavMesh(ChaseDestinationC);
            var destinationD = SampleChaseDestinationPointOnNavMesh(ChaseDestinationD);
            var destinationE = SampleChaseDestinationPointOnNavMesh(ChaseDestinationE);
            var destinationF = SampleChaseDestinationPointOnNavMesh(ChaseDestinationF);

            Assert.That(Vector3.Distance(destinationA, destinationB), Is.LessThan(0.5f));
            Assert.That(Vector3.Distance(destinationA, destinationC), Is.LessThan(0.5f));
            Assert.That(Vector3.Distance(destinationA, destinationD), Is.LessThan(0.5f));
            Assert.That(Vector3.Distance(destinationA, destinationE), Is.LessThan(0.5f));
            Assert.That(Vector3.Distance(destinationA, destinationF), Is.GreaterThanOrEqualTo(0.5f));

            InvokeSetChaseDestination(fixture.StalkerController, destinationA);
            Assert.That(GetHasLastChaseRequestedDestination(fixture.StalkerController), Is.True);
            AssertVectorApproximately(GetLastChaseRequestedDestination(fixture.StalkerController), destinationA);

            InvokeSetChaseDestination(fixture.StalkerController, destinationB);
            AssertVectorApproximately(GetLastChaseRequestedDestination(fixture.StalkerController), destinationA);

            InvokeSetChaseDestination(fixture.StalkerController, destinationC);
            AssertVectorApproximately(GetLastChaseRequestedDestination(fixture.StalkerController), destinationA);

            InvokeSetChaseDestination(fixture.StalkerController, destinationD);
            AssertVectorApproximately(GetLastChaseRequestedDestination(fixture.StalkerController), destinationA);

            InvokeSetChaseDestination(fixture.StalkerController, destinationE);
            AssertVectorApproximately(GetLastChaseRequestedDestination(fixture.StalkerController), destinationA);

            InvokeSetChaseDestination(fixture.StalkerController, destinationF);

            Assert.That(GetHasLastChaseRequestedDestination(fixture.StalkerController), Is.True);
            AssertVectorApproximately(GetLastChaseRequestedDestination(fixture.StalkerController), destinationF);
        }

        [UnityTest]
        public IEnumerator NAV_4C2_ChaseSmallMotion_RefreshesAtMaxInterval()
        {
            const float RefreshInterval = 0.15f;
            const float TimeoutSeconds = 2f;

            var fixture = CreateChaseCadenceFixture(100f, RefreshInterval);
            yield return fixture.ActivateInitializeAndDisable();

            var destinationA = SampleChaseDestinationPointOnNavMesh(ChaseDestinationA);
            var destinationB = SampleChaseDestinationPointOnNavMesh(ChaseDestinationB);
            var sampledDistance = Vector3.Distance(destinationA, destinationB);
            Assert.That(sampledDistance, Is.GreaterThan(0.001f));
            Assert.That(sampledDistance, Is.LessThan(100f));

            InvokeSetChaseDestination(fixture.StalkerController, destinationA);
            Assert.That(GetHasLastChaseRequestedDestination(fixture.StalkerController), Is.True);
            AssertVectorApproximately(GetLastChaseRequestedDestination(fixture.StalkerController), destinationA);

            var elapsed = 0f;
            while (Vector3.Distance(GetLastChaseRequestedDestination(fixture.StalkerController), destinationA) <= 0.01f
                && elapsed < TimeoutSeconds)
            {
                yield return null;
                elapsed += Time.deltaTime;
                InvokeSetChaseDestination(fixture.StalkerController, destinationB);
            }

            Assert.That(
                elapsed,
                Is.LessThan(TimeoutSeconds),
                $"Expected CHASE max refresh interval {RefreshInterval:0.###}s to refresh within {TimeoutSeconds:0.###} gameplay seconds.");
            Assert.That(GetHasLastChaseRequestedDestination(fixture.StalkerController), Is.True);
            AssertVectorApproximately(GetLastChaseRequestedDestination(fixture.StalkerController), destinationB);
        }

        [UnityTest]
        public IEnumerator NAV_4C3_StalkerControllerUpdate_DrivesNavigationProgressToNoProgress()
        {
            var fixture = CreateUpdateDrivenProgressFixture();
            yield return fixture.ActivateAndWait();

            var navigation = GetInternalNavigation(fixture.StalkerController);
            Assert.That(GetEnumPropertyName(fixture.StalkerController, "CurrentState"), Is.EqualTo("PATROL"));
            Assert.That(((Behaviour)fixture.StalkerController).enabled, Is.True);
            Assert.That(fixture.Agent.isOnNavMesh, Is.True);
            Assert.That(GetBoolProperty(navigation, "HasActiveDestination"), Is.True);

            AssertPlanResultAccepted(RequestDestination(navigation, fixture.Destination));
            yield return WaitUntilComplete(fixture.Agent, navigation, PathSettleTimeoutSeconds, PathSettleFrameCap);

            fixture.Agent.isStopped = true;
            yield return null;

            Assert.That(GetEnumPropertyName(fixture.StalkerController, "CurrentState"), Is.EqualTo("PATROL"));
            Assert.That(((Behaviour)fixture.StalkerController).enabled, Is.True);
            Assert.That(fixture.Agent.isOnNavMesh, Is.True);
            Assert.That(GetBoolProperty(navigation, "HasActiveDestination"), Is.True);
            Assert.That(GetPathStatusName(navigation), Is.EqualTo("Complete"));
            Assert.That(HasArrived(navigation), Is.False);
            Assert.That(fixture.Agent.isStopped, Is.True);

            var elapsed = 0f;
            while (GetExecutionStatusName(navigation) != "NoProgress"
                && elapsed < UpdateDrivenNoProgressTimeoutSeconds)
            {
                yield return null;
                elapsed += Time.deltaTime;
            }

            Assert.That(
                GetExecutionStatusName(navigation),
                Is.EqualTo("NoProgress"),
                $"Expected enabled StalkerController.Update to drive navigation progress to NoProgress within {UpdateDrivenNoProgressTimeoutSeconds:0.###} gameplay seconds.");
            Assert.That(GetEnumPropertyName(fixture.StalkerController, "CurrentState"), Is.EqualTo("PATROL"));
        }

        [UnityTest]
        public IEnumerator NAV_REC_StuckCompletePath_RequestsSingleRecoveryRepath()
        {
            var fixture = CreateRecoveryPolicyFixture();
            yield return fixture.ActivateInitializeReplaceNavigationAndDisable();
            yield return PrepareStoppedStuckRecoveryPath(fixture);

            Assert.That(GetNavigationRecoveryAttemptUsed(fixture.StalkerController), Is.False);
            Assert.That(GetExecutionStatusName(fixture.Navigation), Is.EqualTo("Stuck"));

            InvokeTickNavigationRecovery(fixture.StalkerController);

            Assert.That(GetNavigationRecoveryAttemptUsed(fixture.StalkerController), Is.True);
            yield return WaitUntilComplete(fixture.Agent, fixture.Navigation, PathSettleTimeoutSeconds, PathSettleFrameCap);

            Assert.That(GetExecutionStatusName(fixture.Navigation), Is.EqualTo("Moving"));
            Assert.That(GetPathStatusName(fixture.Navigation), Is.EqualTo("Complete"));
            Assert.That(GetBoolProperty(fixture.Navigation, "HasActiveDestination"), Is.True);
            AssertVectorApproximately(fixture.Agent.destination, fixture.Destination);
        }

        [UnityTest]
        public IEnumerator NAV_REC_RepeatedStuckAfterRecovery_DoesNotIssueSecondRepath()
        {
            var fixture = CreateRecoveryPolicyFixture();
            yield return fixture.ActivateInitializeReplaceNavigationAndDisable();
            yield return PrepareStoppedStuckRecoveryPath(fixture);

            InvokeTickNavigationRecovery(fixture.StalkerController);
            Assert.That(GetNavigationRecoveryAttemptUsed(fixture.StalkerController), Is.True);
            yield return WaitUntilComplete(fixture.Agent, fixture.Navigation, PathSettleTimeoutSeconds, PathSettleFrameCap);
            Assert.That(GetExecutionStatusName(fixture.Navigation), Is.EqualTo("Moving"));

            TickUntilStuck(fixture.Navigation);
            Assert.That(GetExecutionStatusName(fixture.Navigation), Is.EqualTo("Stuck"));

            InvokeTickNavigationRecovery(fixture.StalkerController);

            Assert.That(GetNavigationRecoveryAttemptUsed(fixture.StalkerController), Is.True);
            Assert.That(GetExecutionStatusName(fixture.Navigation), Is.EqualTo("Stuck"));
            Assert.That(GetPathStatusName(fixture.Navigation), Is.EqualTo("Complete"));
            AssertVectorApproximately(fixture.Agent.destination, fixture.Destination);
        }

        [UnityTest]
        public IEnumerator NAV_REC_StalePath_UsesSingleRecoveryBudget()
        {
            var fixture = CreateRecoveryPolicyFixture();
            yield return fixture.ActivateInitializeReplaceNavigationAndDisable();
            yield return PrepareStaleRecoveryPath(fixture);

            Assert.That(GetNavigationRecoveryAttemptUsed(fixture.StalkerController), Is.False);
            Assert.That(GetPathStatusName(fixture.Navigation), Is.EqualTo("Stale"));
            Assert.That(GetExecutionStatusName(fixture.Navigation), Is.EqualTo("Failed"));

            InvokeTickNavigationRecovery(fixture.StalkerController);

            Assert.That(GetNavigationRecoveryAttemptUsed(fixture.StalkerController), Is.True);
            yield return WaitUntilComplete(fixture.Agent, fixture.Navigation, PathSettleTimeoutSeconds, PathSettleFrameCap);

            Assert.That(GetExecutionStatusName(fixture.Navigation), Is.EqualTo("Moving"));
            Assert.That(GetPathStatusName(fixture.Navigation), Is.EqualTo("Complete"));
            Assert.That(GetBoolProperty(fixture.Navigation, "HasActiveDestination"), Is.True);
            AssertVectorApproximately(fixture.Agent.destination, fixture.Destination);
        }

        [UnityTest]
        public IEnumerator NAV_REC_StaleAfterBudgetUsed_DoesNotIssueSecondRepath()
        {
            var fixture = CreateRecoveryPolicyFixture();
            yield return fixture.ActivateInitializeReplaceNavigationAndDisable();
            yield return PrepareStaleRecoveryPath(fixture);

            InvokeTickNavigationRecovery(fixture.StalkerController);
            Assert.That(GetNavigationRecoveryAttemptUsed(fixture.StalkerController), Is.True);
            yield return WaitUntilComplete(fixture.Agent, fixture.Navigation, PathSettleTimeoutSeconds, PathSettleFrameCap);
            Assert.That(GetExecutionStatusName(fixture.Navigation), Is.EqualTo("Moving"));

            InduceStalePath(fixture);
            Assert.That(GetPathStatusName(fixture.Navigation), Is.EqualTo("Stale"));
            Assert.That(GetExecutionStatusName(fixture.Navigation), Is.EqualTo("Failed"));
            Assert.That(GetNavigationRecoveryAttemptUsed(fixture.StalkerController), Is.True);

            InvokeTickNavigationRecovery(fixture.StalkerController);

            Assert.That(GetNavigationRecoveryAttemptUsed(fixture.StalkerController), Is.True);
            Assert.That(GetPathStatusName(fixture.Navigation), Is.EqualTo("Stale"));
            Assert.That(GetExecutionStatusName(fixture.Navigation), Is.EqualTo("Failed"));
            AssertVectorApproximately(fixture.Agent.destination, fixture.Destination);
        }

        [UnityTest]
        public IEnumerator NAV_REC_PartialPath_DoesNotConsumeRecoveryBudget()
        {
            var fixture = CreatePartialRecoveryPolicyFixture();
            yield return fixture.ActivateInitializeReplaceNavigationAndDisable();

            Assert.That(GetEnumPropertyName(fixture.StalkerController, "CurrentState"), Is.EqualTo("PATROL"));
            AssertPlanResult(RequestDestination(fixture.Navigation, fixture.Destination), "Accepted", true);
            yield return WaitUntilPathStatus(fixture.Agent, fixture.Navigation, "Partial", PathSettleTimeoutSeconds, PathSettleFrameCap);

            Assert.That(GetPathStatusName(fixture.Navigation), Is.EqualTo("Partial"));
            Assert.That(GetExecutionStatusName(fixture.Navigation), Is.EqualTo("Failed"));
            Assert.That(HasArrived(fixture.Navigation), Is.False);
            Assert.That(GetNavigationRecoveryAttemptUsed(fixture.StalkerController), Is.False);

            InvokeTickNavigationRecovery(fixture.StalkerController);

            Assert.That(GetNavigationRecoveryAttemptUsed(fixture.StalkerController), Is.False);
            Assert.That(GetPathStatusName(fixture.Navigation), Is.EqualTo("Partial"));
            Assert.That(GetExecutionStatusName(fixture.Navigation), Is.EqualTo("Failed"));
            Assert.That(HasArrived(fixture.Navigation), Is.False);
        }

        private NavigationFixture CreateFixture()
        {
            BuildRuntimeNavMesh();
            var agent = CreateInactiveConfiguredAgent(AgentStart);
            return new NavigationFixture(agent, CreateController(agent));
        }

        private ChaseCadenceFixture CreateChaseCadenceFixture(float refreshDistance, float refreshInterval)
        {
            BuildRuntimeNavMesh();

            var stalkerRoot = new GameObject("STK_Test_ChaseCadenceStalker");
            stalkerRoot.SetActive(false);
            stalkerRoot.transform.position = AgentStart;
            _createdObjects.Add(stalkerRoot);

            var agent = stalkerRoot.AddComponent<NavMeshAgent>();
            ConfigureAgent(agent);

            var stalkerController = stalkerRoot.AddComponent(ResolveType(StalkerControllerTypeName));
            SetPrivateFloatField(stalkerController, "chaseDestinationRefreshDistance", refreshDistance);
            SetPrivateFloatField(stalkerController, "chaseDestinationRefreshInterval", refreshInterval);
            return new ChaseCadenceFixture(agent, stalkerController);
        }

        private UpdateDrivenProgressFixture CreateUpdateDrivenProgressFixture()
        {
            BuildRuntimeNavMesh();

            var destination = SampleChaseDestinationPointOnNavMesh(Destination);
            var stalkerRoot = new GameObject("STK_Test_UpdateDrivenProgressStalker");
            stalkerRoot.SetActive(false);
            stalkerRoot.transform.position = AgentStart;
            _createdObjects.Add(stalkerRoot);

            var agent = stalkerRoot.AddComponent<NavMeshAgent>();
            ConfigureAgent(agent);

            var patrolRouteObject = new GameObject("STK_Test_UpdateDrivenProgressPatrolRoute");
            _createdObjects.Add(patrolRouteObject);

            var waypoint = new GameObject("STK_Test_UpdateDrivenProgressWaypoint");
            waypoint.transform.SetParent(patrolRouteObject.transform, false);
            waypoint.transform.position = destination;

            var patrolRoute = patrolRouteObject.AddComponent(ResolveType(PatrolRouteTypeName));
            var stalkerController = stalkerRoot.AddComponent(ResolveType(StalkerControllerTypeName));
            SetPrivateField(stalkerController, "patrolRoute", patrolRoute);

            return new UpdateDrivenProgressFixture(agent, stalkerController, destination);
        }

        private RecoveryPolicyFixture CreateRecoveryPolicyFixture()
        {
            BuildRuntimeNavMesh();

            var destination = SampleChaseDestinationPointOnNavMesh(Destination);
            var stalkerRoot = new GameObject("STK_Test_RecoveryPolicyStalker");
            stalkerRoot.SetActive(false);
            stalkerRoot.transform.position = AgentStart;
            _createdObjects.Add(stalkerRoot);

            var agent = stalkerRoot.AddComponent<NavMeshAgent>();
            ConfigureAgent(agent);

            var patrolRouteObject = new GameObject("STK_Test_RecoveryPolicyPatrolRoute");
            _createdObjects.Add(patrolRouteObject);

            var waypoint = new GameObject("STK_Test_RecoveryPolicyWaypoint");
            waypoint.transform.SetParent(patrolRouteObject.transform, false);
            waypoint.transform.position = destination;

            var patrolRoute = patrolRouteObject.AddComponent(ResolveType(PatrolRouteTypeName));
            var stalkerController = stalkerRoot.AddComponent(ResolveType(StalkerControllerTypeName));
            SetPrivateField(stalkerController, "patrolRoute", patrolRoute);

            return new RecoveryPolicyFixture(agent, stalkerController, destination);
        }

        private RecoveryPolicyFixture CreatePartialRecoveryPolicyFixture()
        {
            BuildDisconnectedIslandNavMesh();

            var destination = SampleChaseDestinationPointOnNavMesh(Destination);
            var stalkerRoot = new GameObject("STK_Test_PartialRecoveryPolicyStalker");
            stalkerRoot.SetActive(false);
            stalkerRoot.transform.position = AgentStart;
            _createdObjects.Add(stalkerRoot);

            var agent = stalkerRoot.AddComponent<NavMeshAgent>();
            ConfigureAgent(agent);

            var patrolRouteObject = new GameObject("STK_Test_PartialRecoveryPolicyPatrolRoute");
            _createdObjects.Add(patrolRouteObject);

            var waypoint = new GameObject("STK_Test_PartialRecoveryPolicyWaypoint");
            waypoint.transform.SetParent(patrolRouteObject.transform, false);
            waypoint.transform.position = destination;

            var patrolRoute = patrolRouteObject.AddComponent(ResolveType(PatrolRouteTypeName));
            var stalkerController = stalkerRoot.AddComponent(ResolveType(StalkerControllerTypeName));
            SetPrivateField(stalkerController, "patrolRoute", patrolRoute);

            return new RecoveryPolicyFixture(agent, stalkerController, destination);
        }

        private void BuildRuntimeNavMesh()
        {
            var buildSettings = NavMesh.GetSettingsByID(0);
            if (buildSettings.agentTypeID == -1)
            {
                Assert.Fail("Default NavMesh agent build settings for agentTypeID 0 are unavailable.");
            }

            var sources = new List<NavMeshBuildSource>
            {
                new NavMeshBuildSource
                {
                    shape = NavMeshBuildSourceShape.Box,
                    transform = Matrix4x4.TRS(
                        new Vector3(0f, -0.05f, 0f),
                        Quaternion.identity,
                        Vector3.one),
                    size = new Vector3(8f, 0.1f, 8f),
                    area = 0
                }
            };
            var bounds = new Bounds(Vector3.zero, new Vector3(10f, 4f, 10f));
            var navMeshData = NavMeshBuilder.BuildNavMeshData(
                buildSettings,
                sources,
                bounds,
                Vector3.zero,
                Quaternion.identity);

            Assert.That(navMeshData, Is.Not.Null, "Runtime NavMeshBuilder.BuildNavMeshData returned null.");

            _navMeshDataInstance = NavMesh.AddNavMeshData(navMeshData);
            Assert.That(_navMeshDataInstance.valid, Is.True, "Runtime NavMeshDataInstance was not valid after AddNavMeshData.");
        }

        private void BuildDisconnectedIslandNavMesh()
        {
            var buildSettings = NavMesh.GetSettingsByID(0);
            if (buildSettings.agentTypeID == -1)
            {
                Assert.Fail("Default NavMesh agent build settings for agentTypeID 0 are unavailable.");
            }

            var sources = new List<NavMeshBuildSource>
            {
                CreateBoxSource(IslandACenter, IslandSize),
                CreateBoxSource(IslandBCenter, IslandSize)
            };
            var bounds = new Bounds(Vector3.zero, new Vector3(12f, 4f, 8f));
            var navMeshData = NavMeshBuilder.BuildNavMeshData(
                buildSettings,
                sources,
                bounds,
                Vector3.zero,
                Quaternion.identity);

            Assert.That(navMeshData, Is.Not.Null, "Runtime disconnected-island NavMeshBuilder.BuildNavMeshData returned null.");

            _navMeshDataInstance = NavMesh.AddNavMeshData(navMeshData);
            Assert.That(_navMeshDataInstance.valid, Is.True, "Runtime disconnected-island NavMeshDataInstance was not valid after AddNavMeshData.");
        }

        private static NavMeshBuildSource CreateBoxSource(Vector3 center, Vector3 size)
        {
            return new NavMeshBuildSource
            {
                shape = NavMeshBuildSourceShape.Box,
                transform = Matrix4x4.TRS(center, Quaternion.identity, Vector3.one),
                size = size,
                area = 0
            };
        }

        private NavMeshAgent CreateInactiveConfiguredAgent(Vector3 position)
        {
            var agentRoot = new GameObject("STK_Test_NavigationProgressAgent");
            agentRoot.SetActive(false);
            agentRoot.transform.position = position;
            _createdObjects.Add(agentRoot);

            var agent = agentRoot.AddComponent<NavMeshAgent>();
            ConfigureAgent(agent);
            return agent;
        }

        private static void ConfigureAgent(NavMeshAgent agent)
        {
            agent.radius = 0.25f;
            agent.height = 1.8f;
            agent.speed = 1f;
            agent.acceleration = 20f;
            agent.angularSpeed = 720f;
            agent.stoppingDistance = 0.2f;
            agent.autoBraking = true;
            agent.updatePosition = true;
            agent.updateRotation = true;
        }

        private static IEnumerator RequestAndSettleCompletePath(NavigationFixture fixture)
        {
            AssertPlanResult(RequestDestination(fixture.Controller, Destination), "Accepted", true);
            yield return WaitUntilComplete(fixture.Agent, fixture.Controller, PathSettleTimeoutSeconds, PathSettleFrameCap);
        }

        private static IEnumerator PrepareStoppedStuckRecoveryPath(RecoveryPolicyFixture fixture)
        {
            Assert.That(GetEnumPropertyName(fixture.StalkerController, "CurrentState"), Is.EqualTo("PATROL"));
            AssertPlanResult(RequestDestination(fixture.Navigation, fixture.Destination), "Accepted", true);
            yield return WaitUntilComplete(fixture.Agent, fixture.Navigation, PathSettleTimeoutSeconds, PathSettleFrameCap);

            StopAgent(fixture.Agent);
            yield return null;
            AssertStoppedCompletePath(fixture.Agent, fixture.Navigation);

            TickUntilStuck(fixture.Navigation);
            Assert.That(GetExecutionStatusName(fixture.Navigation), Is.EqualTo("Stuck"));
        }

        private static IEnumerator PrepareStaleRecoveryPath(RecoveryPolicyFixture fixture)
        {
            fixture.Agent.autoRepath = false;
            fixture.Agent.areaMask = NavMesh.AllAreas;

            Assert.That(GetEnumPropertyName(fixture.StalkerController, "CurrentState"), Is.EqualTo("PATROL"));
            AssertPlanResult(RequestDestination(fixture.Navigation, fixture.Destination), "Accepted", true);
            yield return WaitUntilComplete(fixture.Agent, fixture.Navigation, PathSettleTimeoutSeconds, PathSettleFrameCap);

            StopAgent(fixture.Agent);
            yield return null;
            Assert.That(fixture.Agent.pathStatus, Is.EqualTo(NavMeshPathStatus.PathComplete));
            Assert.That(fixture.Agent.isPathStale, Is.False);
            Assert.That(GetPathStatusName(fixture.Navigation), Is.EqualTo("Complete"));
            Assert.That(GetExecutionStatusName(fixture.Navigation), Is.EqualTo("Moving"));
            Assert.That(HasArrived(fixture.Navigation), Is.False);

            InduceStalePath(fixture);
        }

        private static void InduceStalePath(RecoveryPolicyFixture fixture)
        {
            fixture.Agent.areaMask = fixture.Agent.areaMask == NavMesh.AllAreas
                ? 1 << 0
                : NavMesh.AllAreas;

            Assert.That(fixture.Agent.isOnNavMesh, Is.True, "Stale recovery fixture agent should remain on NavMesh after areaMask change.");
            Assert.That(fixture.Agent.isPathStale, Is.True, "Stale recovery fixture path should become stale after areaMask change.");
            Assert.That(fixture.Agent.pathPending, Is.False, "Stale recovery fixture path should not be pending after areaMask change.");
            Assert.That(fixture.Agent.hasPath, Is.True, "Stale recovery fixture should retain a path after areaMask change.");
            Assert.That(fixture.Agent.pathStatus, Is.EqualTo(NavMeshPathStatus.PathComplete), "Stale recovery fixture underlying Unity path should remain complete.");
            Assert.That(GetPathStatusName(fixture.Navigation), Is.EqualTo("Stale"));
            Assert.That(GetExecutionStatusName(fixture.Navigation), Is.EqualTo("Failed"));
            Assert.That(HasArrived(fixture.Navigation), Is.False);
            Assert.That(GetBoolProperty(fixture.Navigation, "HasActiveDestination"), Is.True);
        }

        private static IEnumerator WaitUntilComplete(NavMeshAgent agent, object controller, float timeoutSeconds, int frameCap)
        {
            var elapsed = 0f;
            var frames = 0;
            while ((agent.pathPending || GetPathStatusName(controller) != "Complete")
                && elapsed < timeoutSeconds
                && frames < frameCap)
            {
                yield return null;
                elapsed += Time.deltaTime;
                frames++;
            }

            Assert.That(
                agent.pathPending,
                Is.False,
                $"Expected NavMeshAgent.pathPending to clear within {timeoutSeconds:0.###} gameplay seconds and {frameCap} frames.");
            Assert.That(
                GetPathStatusName(controller),
                Is.EqualTo("Complete"),
                $"Expected Complete path within {timeoutSeconds:0.###} gameplay seconds and {frameCap} frames.");
            Assert.That(
                HasArrived(controller),
                Is.False,
                "Progress integration tests require the Complete path to remain non-arrived before progress sampling.");
        }

        private static IEnumerator WaitUntilPathStatus(NavMeshAgent agent, object controller, string expectedStatus, float timeoutSeconds, int frameCap)
        {
            var elapsed = 0f;
            var frames = 0;
            while ((agent.pathPending || GetPathStatusName(controller) != expectedStatus)
                && elapsed < timeoutSeconds
                && frames < frameCap)
            {
                yield return null;
                elapsed += Time.deltaTime;
                frames++;
            }

            Assert.That(
                agent.pathPending,
                Is.False,
                $"Expected NavMeshAgent.pathPending to clear within {timeoutSeconds:0.###} gameplay seconds and {frameCap} frames.");
            Assert.That(
                GetPathStatusName(controller),
                Is.EqualTo(expectedStatus),
                $"Expected {expectedStatus} path within {timeoutSeconds:0.###} gameplay seconds and {frameCap} frames.");
        }

        private static void StopAgent(NavMeshAgent agent)
        {
            agent.isStopped = true;
        }

        private static void TickUntilNoProgress(object controller)
        {
            TickProgress(controller, ProgressTickDeltaTime);
            TickProgress(controller, ProgressTickDeltaTime);
            TickProgress(controller, ProgressTickDeltaTime);
            Assert.That(GetExecutionStatusName(controller), Is.EqualTo("NoProgress"));
        }

        private static void TickUntilStuck(object controller)
        {
            TickProgress(controller, ProgressTickDeltaTime);
            for (var i = 0; i < 5; i++)
            {
                TickProgress(controller, ProgressTickDeltaTime);
            }

            Assert.That(GetExecutionStatusName(controller), Is.EqualTo("Stuck"));
        }

        private static void AssertStoppedCompletePath(NavMeshAgent agent, object controller)
        {
            Assert.That(agent.isStopped, Is.True);
            Assert.That(agent.pathStatus, Is.EqualTo(NavMeshPathStatus.PathComplete));
            Assert.That(GetPathStatusName(controller), Is.EqualTo("Complete"));
            Assert.That(HasArrived(controller), Is.False);
        }

        private static void AssertPointOnNavMesh(Vector3 point)
        {
            Assert.That(
                NavMesh.SamplePosition(point, out _, 0.5f, NavMesh.AllAreas),
                Is.True,
                $"Expected test destination {point} to sample onto the runtime NavMesh.");
        }

        private static Vector3 SampleChaseDestinationPointOnNavMesh(Vector3 requestedPoint)
        {
            Assert.That(
                NavMesh.SamplePosition(requestedPoint, out var hit, 0.25f, NavMesh.AllAreas),
                Is.True,
                $"Expected CHASE cadence destination {requestedPoint} to sample onto the runtime NavMesh.");

            return hit.position;
        }

        private static object CreateController(NavMeshAgent agent)
        {
            var controllerType = ResolveType(NavigationControllerTypeName);
            var settings = CreateProgressSettings();
            var constructor = controllerType.GetConstructor(
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(NavMeshAgent), ResolveType(NavigationProgressSettingsTypeName) },
                null);
            Assert.That(constructor, Is.Not.Null, $"Missing public constructor '{NavigationControllerTypeName}(NavMeshAgent, NavigationProgressSettings)'.");

            return constructor.Invoke(new[] { agent, settings });
        }

        private static object CreateProgressSettings()
        {
            var settingsType = ResolveType(NavigationProgressSettingsTypeName);
            var constructor = settingsType.GetConstructor(
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(float), typeof(float), typeof(float), typeof(float), typeof(float) },
                null);
            Assert.That(constructor, Is.Not.Null, $"Missing public constructor '{NavigationProgressSettingsTypeName}(float, float, float, float, float)'.");

            return constructor.Invoke(new object[] { 0.10f, 0.05f, 0.05f, 0.20f, 0.50f });
        }

        private static object RequestDestination(object controller, Vector3 destination)
        {
            return InvokeMethod(
                controller,
                "RequestDestination",
                new[] { typeof(Vector3) },
                new object[] { destination });
        }

        private static object RequestDestination(object controller, Vector3 destination, bool forceRepath)
        {
            return InvokeMethod(
                controller,
                "RequestDestination",
                new[] { typeof(Vector3), typeof(bool) },
                new object[] { destination, forceRepath });
        }

        private static object RequestDestination(object controller, Vector3 destination, string intentName)
        {
            var intentType = ResolveType(NavigationRequestIntentTypeName);
            var intent = Enum.Parse(intentType, intentName);
            return InvokeMethod(
                controller,
                "RequestDestination",
                new[] { typeof(Vector3), intentType },
                new[] { destination, intent });
        }

        private static void TickProgress(object controller, float deltaTime)
        {
            InvokeMethod(
                controller,
                "TickProgress",
                new[] { typeof(float) },
                new object[] { deltaTime });
        }

        private static string GetPathStatusName(object controller)
        {
            return GetEnumMethodResultName(controller, "GetPathStatus");
        }

        private static string GetExecutionStatusName(object controller)
        {
            return GetEnumMethodResultName(controller, "GetExecutionStatus");
        }

        private static string GetEnumMethodResultName(object controller, string methodName)
        {
            var value = InvokeMethod(controller, methodName, Type.EmptyTypes, Array.Empty<object>());
            Assert.That(value, Is.Not.Null, $"Method '{methodName}' returned null.");
            Assert.That(value.GetType().IsEnum, Is.True, $"Method '{methodName}' must return an enum.");
            return value.ToString();
        }

        private static bool HasArrived(object controller)
        {
            var value = InvokeMethod(controller, "HasArrived", Type.EmptyTypes, Array.Empty<object>());
            Assert.That(value, Is.TypeOf<bool>(), "StalkerNavigationController.HasArrived must return bool.");
            return (bool)value;
        }

        private static void InvokeSetChaseDestination(object stalkerController, Vector3 observedPosition)
        {
            InvokePrivateMethod(
                stalkerController,
                "SetChaseDestination",
                new[] { typeof(Vector3) },
                new object[] { observedPosition });
        }

        private static void ResetChaseCadence(object stalkerController)
        {
            InvokePrivateMethod(stalkerController, "ResetChaseDestinationTracking", Type.EmptyTypes, Array.Empty<object>());
        }

        private static void InvokeTickNavigationRecovery(object stalkerController)
        {
            InvokePrivateMethod(stalkerController, "TickNavigationRecovery", Type.EmptyTypes, Array.Empty<object>());
        }

        private static bool GetHasLastChaseRequestedDestination(object stalkerController)
        {
            return GetPrivateField<bool>(stalkerController, "_hasLastChaseRequestedDestination");
        }

        private static bool GetNavigationRecoveryAttemptUsed(object stalkerController)
        {
            return GetPrivateField<bool>(stalkerController, "_navigationRecoveryAttemptUsed");
        }

        private static Vector3 GetLastChaseRequestedDestination(object stalkerController)
        {
            return GetPrivateField<Vector3>(stalkerController, "_lastChaseRequestedDestination");
        }

        private static bool GetNavigationHasActiveDestination(object stalkerController)
        {
            return GetBoolProperty(GetPrivateField<object>(stalkerController, "_navigation"), "HasActiveDestination");
        }

        private static object GetInternalNavigation(object stalkerController)
        {
            return GetPrivateField<object>(stalkerController, "_navigation");
        }

        private static void AssertVectorApproximately(Vector3 actual, Vector3 expected)
        {
            Assert.That(
                Vector3.Distance(actual, expected),
                Is.LessThanOrEqualTo(0.01f),
                $"Expected Vector3 {actual} to match {expected} within tolerance.");
        }

        private static void AssertPlanResult(object result, string expectedStatusName, bool expectedAccepted)
        {
            Assert.That(result, Is.Not.Null, "NavigationPlanResult invocation returned null.");
            Assert.That(GetEnumPropertyName(result, "Status"), Is.EqualTo(expectedStatusName));
            Assert.That(GetBoolProperty(result, "IsAccepted"), Is.EqualTo(expectedAccepted));
        }

        private static void AssertPlanResultAccepted(object result)
        {
            Assert.That(result, Is.Not.Null, "NavigationPlanResult invocation returned null.");
            Assert.That(GetBoolProperty(result, "IsAccepted"), Is.True);
            var statusName = GetEnumPropertyName(result, "Status");
            Assert.That(
                statusName == "Accepted" || statusName == "AlreadyActive",
                Is.True,
                "Update-driven progress setup accepts either a fresh NewGoal request or an already-active matching patrol destination.");
        }

        private static string GetEnumPropertyName(object target, string propertyName)
        {
            var value = GetProperty(target, propertyName);
            Assert.That(value, Is.Not.Null, $"Property '{propertyName}' returned null.");
            Assert.That(value.GetType().IsEnum, Is.True, $"Property '{propertyName}' must return an enum value.");
            return value.ToString();
        }

        private static bool GetBoolProperty(object target, string propertyName)
        {
            var value = GetProperty(target, propertyName);
            Assert.That(value, Is.TypeOf<bool>(), $"Property '{propertyName}' must return bool.");
            return (bool)value;
        }

        private static object GetProperty(object target, string propertyName)
        {
            var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, $"Missing public property '{propertyName}' on '{target.GetType().FullName}'.");

            return property.GetValue(target);
        }

        private static object InvokeMethod(object target, string methodName, Type[] parameterTypes, object[] args)
        {
            var method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public,
                null,
                parameterTypes,
                null);
            Assert.That(method, Is.Not.Null, $"Missing public method '{methodName}' on '{target.GetType().FullName}'.");

            return method.Invoke(target, args);
        }

        private static object InvokePrivateMethod(object target, string methodName, Type[] parameterTypes, object[] args)
        {
            var method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                parameterTypes,
                null);
            Assert.That(method, Is.Not.Null, $"Missing private method '{methodName}' on '{target.GetType().FullName}'.");

            return method.Invoke(target, args);
        }

        private static T GetPrivateField<T>(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing private field '{fieldName}' on '{target.GetType().FullName}'.");

            return (T)field.GetValue(target);
        }

        private static void SetPrivateFloatField(object target, string fieldName, float value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing private float field '{fieldName}' on '{target.GetType().FullName}'.");
            Assert.That(field.FieldType, Is.EqualTo(typeof(float)), $"Private field '{fieldName}' must be float.");

            field.SetValue(target, value);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing private field '{fieldName}' on '{target.GetType().FullName}'.");

            field.SetValue(target, value);
        }

        private static Type ResolveType(string fullTypeName)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < assemblies.Length; i++)
            {
                var type = assemblies[i].GetType(fullTypeName, false);
                if (type != null)
                {
                    return type;
                }
            }

            Assert.Fail($"Could not find production type '{fullTypeName}' in the loaded Unity AppDomain.");
            return null;
        }

        private readonly struct NavigationFixture
        {
            public NavigationFixture(NavMeshAgent agent, object controller)
            {
                Agent = agent;
                Controller = controller;
            }

            public NavMeshAgent Agent { get; }

            public object Controller { get; }

            public IEnumerator ActivateAndWait()
            {
                Agent.gameObject.SetActive(true);
                yield return null;

                Assert.That(Agent.enabled, Is.True, "Runtime test NavMeshAgent must be enabled.");
                Assert.That(Agent.isOnNavMesh, Is.True, "Runtime test NavMeshAgent must be placed on the generated NavMesh.");
            }
        }

        private readonly struct ChaseCadenceFixture
        {
            public ChaseCadenceFixture(NavMeshAgent agent, object stalkerController)
            {
                Agent = agent;
                StalkerController = stalkerController;
            }

            public NavMeshAgent Agent { get; }

            public object StalkerController { get; }

            public IEnumerator ActivateInitializeAndDisable()
            {
                Agent.gameObject.SetActive(true);
                yield return null;

                Assert.That(Agent.enabled, Is.True, "Runtime CHASE cadence test NavMeshAgent must be enabled.");
                Assert.That(Agent.isOnNavMesh, Is.True, "Runtime CHASE cadence test NavMeshAgent must be placed on the generated NavMesh.");

                var behaviour = (Behaviour)StalkerController;
                behaviour.enabled = false;
                Assert.That(behaviour.enabled, Is.False, "StalkerController must be disabled before manual SetChaseDestination invocation.");
            }
        }

        private readonly struct UpdateDrivenProgressFixture
        {
            public UpdateDrivenProgressFixture(NavMeshAgent agent, object stalkerController, Vector3 destination)
            {
                Agent = agent;
                StalkerController = stalkerController;
                Destination = destination;
            }

            public NavMeshAgent Agent { get; }

            public object StalkerController { get; }

            public Vector3 Destination { get; }

            public IEnumerator ActivateAndWait()
            {
                Agent.gameObject.SetActive(true);
                yield return null;

                Assert.That(Agent.enabled, Is.True, "Runtime Update-driven progress test NavMeshAgent must be enabled.");
                Assert.That(Agent.isOnNavMesh, Is.True, "Runtime Update-driven progress test NavMeshAgent must be placed on the generated NavMesh.");
                Assert.That(((Behaviour)StalkerController).enabled, Is.True, "StalkerController must remain enabled so Update can drive TickProgress.");
            }
        }

        private sealed class RecoveryPolicyFixture
        {
            public RecoveryPolicyFixture(NavMeshAgent agent, object stalkerController, Vector3 destination)
            {
                Agent = agent;
                StalkerController = stalkerController;
                Destination = destination;
            }

            public NavMeshAgent Agent { get; }

            public object StalkerController { get; }

            public Vector3 Destination { get; }

            public object Navigation { get; private set; }

            public IEnumerator ActivateInitializeReplaceNavigationAndDisable()
            {
                Agent.gameObject.SetActive(true);
                yield return null;

                Assert.That(Agent.enabled, Is.True, "Runtime recovery policy test NavMeshAgent must be enabled.");
                Assert.That(Agent.isOnNavMesh, Is.True, "Runtime recovery policy test NavMeshAgent must be placed on the generated NavMesh.");

                var navigation = CreateController(Agent);
                SetPrivateField(StalkerController, "_navigation", navigation);
                SetPrivateField(StalkerController, "_navigationRecoveryAttemptUsed", false);
                Navigation = navigation;

                var behaviour = (Behaviour)StalkerController;
                behaviour.enabled = false;
                Assert.That(behaviour.enabled, Is.False, "StalkerController must be disabled before manual TickNavigationRecovery invocation.");

                Assert.That(Navigation, Is.Not.Null, "Recovery policy fixture must install a short-threshold navigation controller.");
            }
        }
    }
}
