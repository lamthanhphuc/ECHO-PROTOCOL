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
    public sealed class StalkerNavigationRuntimePlayModeTests
    {
        private const string NavigationControllerTypeName = "EchoProtocol.AI.Stalker.StalkerNavigationController";
        private const float PathSettleTimeoutSeconds = 2f;
        private const float ArrivalTimeoutSeconds = 8f;
        private const int PathSettleFrameCap = 300;
        private const int ArrivalFrameCap = 10000;

        private static readonly Vector3 AgentStart = new Vector3(-3f, 0f, 0f);
        private static readonly Vector3 Destination = new Vector3(3f, 0f, 0f);

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
        public IEnumerator NAV_3_UsableAgentWithoutDestination_IsIdle()
        {
            BuildRuntimeNavMesh();
            var agent = CreateInactiveConfiguredAgent(AgentStart);
            agent.gameObject.SetActive(true);
            yield return null;
            AssertUsableAgent(agent);
            var controller = CreateController(agent);

            Assert.That(GetBoolProperty(controller, "IsUsable"), Is.True);
            Assert.That(GetBoolProperty(controller, "HasActiveDestination"), Is.False);
            Assert.That(GetPathStatusName(controller), Is.EqualTo("NoDestination"));
            Assert.That(GetExecutionStatusName(controller), Is.EqualTo("Idle"));
        }

        [UnityTest]
        public IEnumerator NAV_3_RequestDestination_SettlesToCompletePath()
        {
            var fixture = CreateUsableNavigationFixture(AgentStart);
            yield return fixture.ActivateAndWait();

            var result = RequestDestination(fixture.Controller, Destination);

            AssertPlanResult(result, "Accepted", Destination, true);
            Assert.That(GetBoolProperty(fixture.Controller, "HasActiveDestination"), Is.True);

            yield return WaitUntilPathSettled(fixture.Agent, fixture.Controller, PathSettleTimeoutSeconds, PathSettleFrameCap);

            Assert.That(GetPathStatusName(fixture.Controller), Is.EqualTo("Complete"));
        }

        [UnityTest]
        public IEnumerator NAV_3_CompletePath_ReportsMovingBeforeArrival()
        {
            var fixture = CreateUsableNavigationFixture(AgentStart);
            yield return fixture.ActivateAndWait();

            AssertPlanResult(RequestDestination(fixture.Controller, Destination), "Accepted", Destination, true);
            yield return WaitUntilPathCompleteBeforeArrival(fixture.Controller, PathSettleTimeoutSeconds, PathSettleFrameCap);

            Assert.That(GetExecutionStatusName(fixture.Controller), Is.EqualTo("Moving"));
        }

        [UnityTest]
        public IEnumerator NAV_3_CompletePath_AgentEventuallyArrives()
        {
            var fixture = CreateUsableNavigationFixture(AgentStart);
            yield return fixture.ActivateAndWait();

            AssertPlanResult(RequestDestination(fixture.Controller, Destination), "Accepted", Destination, true);
            yield return WaitUntilArrived(fixture.Controller, ArrivalTimeoutSeconds, ArrivalFrameCap);

            Assert.That(GetPathStatusName(fixture.Controller), Is.EqualTo("Complete"));
            Assert.That(GetExecutionStatusName(fixture.Controller), Is.EqualTo("Arrived"));
            Assert.That(GetBoolProperty(fixture.Controller, "HasActiveDestination"), Is.True);
        }

        [UnityTest]
        public IEnumerator NAV_3_ForceRepath_SameDestinationReturnsAcceptedRequest()
        {
            var fixture = CreateUsableNavigationFixture(AgentStart);
            yield return fixture.ActivateAndWait();

            AssertPlanResult(RequestDestination(fixture.Controller, Destination), "Accepted", Destination, true);
            AssertPlanResult(RequestDestination(fixture.Controller, Destination), "AlreadyActive", Destination, true);
            AssertPlanResult(RequestDestination(fixture.Controller, Destination, true), "Accepted", Destination, true);
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

        private NavMeshAgent CreateInactiveConfiguredAgent(Vector3 position)
        {
            var agentRoot = new GameObject("STK_Test_NavigationAgent");
            agentRoot.SetActive(false);
            agentRoot.transform.position = position;
            _createdObjects.Add(agentRoot);

            var agent = agentRoot.AddComponent<NavMeshAgent>();
            agent.radius = 0.25f;
            agent.height = 1.8f;
            agent.speed = 1f;
            agent.acceleration = 20f;
            agent.angularSpeed = 720f;
            agent.stoppingDistance = 0.2f;
            agent.autoBraking = true;
            agent.updatePosition = true;
            agent.updateRotation = true;
            return agent;
        }

        private NavigationFixture CreateUsableNavigationFixture(Vector3 startPosition)
        {
            BuildRuntimeNavMesh();
            var agent = CreateInactiveConfiguredAgent(startPosition);
            return new NavigationFixture(agent, CreateController(agent));
        }

        private static void AssertUsableAgent(NavMeshAgent agent)
        {
            Assert.That(agent.enabled, Is.True, "Runtime test NavMeshAgent must be enabled.");
            Assert.That(agent.isOnNavMesh, Is.True, "Runtime test NavMeshAgent must be placed on the generated NavMesh.");
        }

        private static object CreateController(NavMeshAgent agent)
        {
            var controllerType = ResolveType(NavigationControllerTypeName);
            var constructor = controllerType.GetConstructor(
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(NavMeshAgent) },
                null);
            Assert.That(constructor, Is.Not.Null, $"Missing public constructor '{NavigationControllerTypeName}(NavMeshAgent)'.");

            return constructor.Invoke(new object[] { agent });
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

        private static IEnumerator WaitUntilPathSettled(NavMeshAgent agent, object controller, float timeoutSeconds, int frameCap)
        {
            var elapsed = 0f;
            var frames = 0;
            while ((agent.pathPending || GetPathStatusName(controller) == "Pending")
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
                Is.Not.EqualTo("Pending"),
                $"Expected navigation path status to settle within {timeoutSeconds:0.###} gameplay seconds and {frameCap} frames.");
        }

        private static IEnumerator WaitUntilPathCompleteBeforeArrival(object controller, float timeoutSeconds, int frameCap)
        {
            var elapsed = 0f;
            var frames = 0;
            while ((GetPathStatusName(controller) != "Complete" || HasArrived(controller))
                && elapsed < timeoutSeconds
                && frames < frameCap)
            {
                yield return null;
                elapsed += Time.deltaTime;
                frames++;
            }

            Assert.That(
                GetPathStatusName(controller),
                Is.EqualTo("Complete"),
                $"Expected Complete path before arrival within {timeoutSeconds:0.###} gameplay seconds and {frameCap} frames.");
            Assert.That(
                HasArrived(controller),
                Is.False,
                $"Expected to observe Complete path before arrival within {timeoutSeconds:0.###} gameplay seconds and {frameCap} frames.");
        }

        private static IEnumerator WaitUntilArrived(object controller, float timeoutSeconds, int frameCap)
        {
            var elapsed = 0f;
            var frames = 0;
            while (!HasArrived(controller)
                && elapsed < timeoutSeconds
                && frames < frameCap)
            {
                yield return null;
                elapsed += Time.deltaTime;
                frames++;
            }

            Assert.That(
                HasArrived(controller),
                Is.True,
                $"Expected current HasArrived semantics to become true within {timeoutSeconds:0.###} gameplay seconds and {frameCap} frames.");
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

        private static bool GetBoolProperty(object target, string propertyName)
        {
            var value = GetProperty(target, propertyName);
            Assert.That(value, Is.TypeOf<bool>(), $"Property '{propertyName}' must return bool.");
            return (bool)value;
        }

        private static void AssertPlanResult(object result, string expectedStatusName, Vector3 expectedDestination, bool expectedAccepted)
        {
            Assert.That(result, Is.Not.Null, "NavigationPlanResult invocation returned null.");
            Assert.That(GetEnumPropertyName(result, "Status"), Is.EqualTo(expectedStatusName));
            Assert.That(GetVector3Property(result, "RequestedDestination"), Is.EqualTo(expectedDestination));
            Assert.That(GetBoolProperty(result, "IsAccepted"), Is.EqualTo(expectedAccepted));
        }

        private static string GetEnumPropertyName(object target, string propertyName)
        {
            var value = GetProperty(target, propertyName);
            Assert.That(value, Is.Not.Null, $"Property '{propertyName}' returned null.");
            Assert.That(value.GetType().IsEnum, Is.True, $"Property '{propertyName}' must return an enum value.");
            return value.ToString();
        }

        private static Vector3 GetVector3Property(object target, string propertyName)
        {
            var value = GetProperty(target, propertyName);
            Assert.That(value, Is.TypeOf<Vector3>(), $"Property '{propertyName}' must return Vector3.");
            return (Vector3)value;
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
                AssertUsableAgent(Agent);
            }
        }
    }
}
