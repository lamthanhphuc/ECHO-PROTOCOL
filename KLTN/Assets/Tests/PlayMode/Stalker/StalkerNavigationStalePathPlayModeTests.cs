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
    public sealed class StalkerNavigationStalePathPlayModeTests
    {
        private const string NavigationControllerTypeName = "EchoProtocol.AI.Stalker.StalkerNavigationController";
        private const float PathSettleTimeoutSeconds = 2f;
        private const int PathSettleFrameCap = 1000;
        private const int ObservationFrameCount = 5;

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
        public IEnumerator NAV_3_StalePath_AreaMaskChange_IsReportedAsFailedAndNotArrival()
        {
            BuildRuntimeNavMesh();
            var agent = CreateInactiveConfiguredAgent(AgentStart);
            agent.areaMask = NavMesh.AllAreas;
            agent.gameObject.SetActive(true);
            yield return null;
            Assert.That(agent.enabled, Is.True, "Runtime stale-path NavMeshAgent must be enabled.");
            Assert.That(agent.isOnNavMesh, Is.True, "Runtime stale-path NavMeshAgent must be placed on the generated NavMesh.");

            var controller = CreateController(agent);
            var requestResult = RequestDestination(controller, Destination);
            var requestStatus = GetEnumPropertyName(requestResult, "Status");
            var requestAccepted = GetBoolProperty(requestResult, "IsAccepted");
            Assert.That(requestStatus, Is.EqualTo("Accepted"), "Stale-path fixture did not accept the destination request.");
            Assert.That(requestAccepted, Is.True, "Stale-path fixture did not report accepted request.");
            Assert.That(agent.isOnNavMesh, Is.True, "Stale-path agent left the NavMesh after request.");

            yield return WaitUntilPathSettled(agent, controller, PathSettleTimeoutSeconds, PathSettleFrameCap);

            Assert.That(agent.pathPending, Is.False, "Stale-path fixture path did not settle.");
            Assert.That(agent.hasPath, Is.True, "Stale-path fixture must have a path before areaMask change.");
            Assert.That(agent.isPathStale, Is.False, "Stale-path fixture must not be stale before areaMask change.");
            Assert.That(agent.pathStatus, Is.EqualTo(NavMeshPathStatus.PathComplete), "Stale-path fixture did not settle to PathComplete before areaMask change.");
            Assert.That(GetEnumMethodResultName(controller, "GetPathStatus"), Is.EqualTo("Complete"), "Controller did not report Complete before areaMask change.");
            Assert.That(GetEnumMethodResultName(controller, "GetExecutionStatus"), Is.EqualTo("Moving"), "Controller did not report Moving before areaMask change.");
            Assert.That(InvokeBoolMethod(controller, "HasArrived"), Is.False, "Controller should not report arrival before areaMask change.");
            Assert.That(GetBoolProperty(controller, "HasActiveDestination"), Is.True, "Controller should retain active destination before areaMask change.");

            agent.areaMask = 1 << 0;

            AssertStalePathFacts(agent, controller, "immediately after areaMask change");

            for (var frame = 0; frame < ObservationFrameCount; frame++)
            {
                yield return null;
            }

            AssertStalePathFacts(agent, controller, "after five yielded frames");
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
            var agentRoot = new GameObject("STK_Test_StalePathAgent");
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
            agent.autoRepath = false;
            return agent;
        }

        private static IEnumerator WaitUntilPathSettled(NavMeshAgent agent, object controller, float timeoutSeconds, int frameCap)
        {
            var elapsed = 0f;
            var frames = 0;
            while ((agent.pathPending || GetEnumMethodResultName(controller, "GetPathStatus") == "Pending")
                && elapsed < timeoutSeconds
                && frames < frameCap)
            {
                yield return null;
                elapsed += Time.deltaTime;
                frames++;
            }
        }

        private static void AssertStalePathFacts(NavMeshAgent agent, object controller, string phase)
        {
            Assert.That(agent.isOnNavMesh, Is.True, $"Agent should remain on NavMesh {phase}.");
            Assert.That(agent.isPathStale, Is.True, $"Agent path should be stale {phase}.");
            Assert.That(agent.pathPending, Is.False, $"Agent path should not be pending {phase}.");
            Assert.That(agent.hasPath, Is.True, $"Agent should retain a path {phase}.");
            Assert.That(agent.pathStatus, Is.EqualTo(NavMeshPathStatus.PathComplete), $"Underlying Unity pathStatus should remain PathComplete {phase}.");
            Assert.That(GetEnumMethodResultName(controller, "GetPathStatus"), Is.EqualTo("Stale"), $"Controller should report Stale {phase}.");
            Assert.That(GetEnumMethodResultName(controller, "GetExecutionStatus"), Is.EqualTo("Failed"), $"Controller should map stale path to Failed {phase}.");
            Assert.That(InvokeBoolMethod(controller, "HasArrived"), Is.False, $"Controller should not report arrival for stale path {phase}.");
            Assert.That(GetBoolProperty(controller, "HasActiveDestination"), Is.True, $"Controller should retain active destination cache {phase}.");
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

        private static string GetEnumMethodResultName(object controller, string methodName)
        {
            var value = InvokeMethod(controller, methodName, Type.EmptyTypes, Array.Empty<object>());
            Assert.That(value, Is.Not.Null, $"Method '{methodName}' returned null.");
            Assert.That(value.GetType().IsEnum, Is.True, $"Method '{methodName}' must return an enum.");
            return value.ToString();
        }

        private static bool InvokeBoolMethod(object controller, string methodName)
        {
            var value = InvokeMethod(controller, methodName, Type.EmptyTypes, Array.Empty<object>());
            Assert.That(value, Is.TypeOf<bool>(), $"Method '{methodName}' must return bool.");
            return (bool)value;
        }

        private static bool GetBoolProperty(object target, string propertyName)
        {
            var value = GetProperty(target, propertyName);
            Assert.That(value, Is.TypeOf<bool>(), $"Property '{propertyName}' must return bool.");
            return (bool)value;
        }

        private static string GetEnumPropertyName(object target, string propertyName)
        {
            var value = GetProperty(target, propertyName);
            Assert.That(value, Is.Not.Null, $"Property '{propertyName}' returned null.");
            Assert.That(value.GetType().IsEnum, Is.True, $"Property '{propertyName}' must return an enum value.");
            return value.ToString();
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

    }
}
