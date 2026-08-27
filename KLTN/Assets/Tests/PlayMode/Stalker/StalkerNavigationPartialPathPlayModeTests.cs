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
    public sealed class StalkerNavigationPartialPathPlayModeTests
    {
        private const string NavigationControllerTypeName = "EchoProtocol.AI.Stalker.StalkerNavigationController";
        private const float PathSettleTimeoutSeconds = 2f;
        private const int PathSettleFrameCap = 1000;
        private const float EndpointTimeoutSeconds = 4f;
        private const int EndpointFrameCap = 10000;
        private const float SampleRadius = 0.5f;

        private static readonly Vector3 IslandACenter = new Vector3(-3f, -0.05f, 0f);
        private static readonly Vector3 IslandBCenter = new Vector3(3f, -0.05f, 0f);
        private static readonly Vector3 IslandSize = new Vector3(3f, 0.1f, 4f);
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
        public IEnumerator NAV_3_PartialPath_ReachableEndpoint_DoesNotCountAsArrival()
        {
            BuildDisconnectedIslandNavMesh();

            var sourceSampleSucceeded = NavMesh.SamplePosition(AgentStart, out var sourceHit, SampleRadius, NavMesh.AllAreas);
            var destinationSampleSucceeded = NavMesh.SamplePosition(Destination, out var destinationHit, SampleRadius, NavMesh.AllAreas);
            Assert.That(sourceSampleSucceeded, Is.True, $"Source position {AgentStart} did not sample onto the runtime NavMesh.");
            Assert.That(destinationSampleSucceeded, Is.True, $"Destination position {Destination} did not sample onto the runtime NavMesh.");

            var agent = CreateInactiveConfiguredAgent(AgentStart);
            agent.gameObject.SetActive(true);
            yield return null;
            Assert.That(agent.enabled, Is.True, "Runtime partial-path NavMeshAgent must be enabled.");
            Assert.That(agent.isOnNavMesh, Is.True, "Runtime partial-path NavMeshAgent must be placed on island A.");

            var controller = CreateController(agent);
            var requestResult = RequestDestination(controller, Destination);
            var requestStatus = GetEnumPropertyName(requestResult, "Status");
            var requestAccepted = GetBoolProperty(requestResult, "IsAccepted");

            var elapsed = 0f;
            var frames = 0;
            while (agent.pathPending
                && elapsed < PathSettleTimeoutSeconds
                && frames < PathSettleFrameCap)
            {
                yield return null;
                elapsed += Time.deltaTime;
                frames++;
            }

            var settledControllerPathStatus = GetEnumMethodResultName(controller, "GetPathStatus");
            var settledControllerExecutionStatus = GetEnumMethodResultName(controller, "GetExecutionStatus");
            Assert.That(requestStatus, Is.EqualTo("Accepted"), "Disconnected-island fixture no longer accepts the destination request.");
            Assert.That(requestAccepted, Is.True, "Disconnected-island fixture no longer reports an accepted request.");
            Assert.That(agent.isOnNavMesh, Is.True, "Disconnected-island fixture agent left the NavMesh before endpoint observation.");
            Assert.That(agent.pathPending, Is.False, "Disconnected-island fixture path did not settle before endpoint observation.");
            Assert.That(agent.pathStatus, Is.EqualTo(NavMeshPathStatus.PathPartial), "Disconnected-island fixture no longer settles to PathPartial.");
            Assert.That(settledControllerPathStatus, Is.EqualTo("Partial"), "Controller no longer reports Partial for the disconnected-island fixture.");
            Assert.That(settledControllerExecutionStatus, Is.EqualTo("Failed"), "Controller no longer maps Partial to Failed for the disconnected-island fixture.");

            var endpointElapsed = 0f;
            var endpointFrames = 0;
            while (agent.remainingDistance > agent.stoppingDistance
                && endpointElapsed < EndpointTimeoutSeconds
                && endpointFrames < EndpointFrameCap)
            {
                yield return null;
                endpointElapsed += Time.deltaTime;
                endpointFrames++;
            }

            var reachedStoppingDistance = agent.remainingDistance <= agent.stoppingDistance;
            var controllerHasArrived = InvokeBoolMethod(controller, "HasArrived");
            var distanceToOriginalRequestedDestination = Vector3.Distance(agent.transform.position, Destination);

            Assert.That(
                reachedStoppingDistance,
                Is.True,
                $"Partial path did not reach its reachable endpoint within {EndpointTimeoutSeconds:0.###} gameplay seconds and {EndpointFrameCap} frames. RemainingDistance={agent.remainingDistance:0.###}, StoppingDistance={agent.stoppingDistance:0.###}, PathStatus={agent.pathStatus}, Position={agent.transform.position}, Destination={agent.destination}, ElapsedEndpointGameplayTime={endpointElapsed:0.###}, EndpointFrames={endpointFrames}.");
            Assert.That(agent.pathStatus, Is.EqualTo(NavMeshPathStatus.PathPartial));
            Assert.That(agent.remainingDistance, Is.LessThanOrEqualTo(agent.stoppingDistance));
            Assert.That(GetEnumMethodResultName(controller, "GetPathStatus"), Is.EqualTo("Partial"));
            Assert.That(GetEnumMethodResultName(controller, "GetExecutionStatus"), Is.EqualTo("Failed"));
            Assert.That(
                distanceToOriginalRequestedDestination,
                Is.GreaterThan(1f),
                $"Partial path endpoint should remain materially far from the original requested destination. Position={agent.transform.position}, OriginalDestination={Destination}, Distance={distanceToOriginalRequestedDestination:0.###}.");
            Assert.That(
                controllerHasArrived,
                Is.False,
                "PathPartial reached its reachable endpoint but must not count as arrival at the originally requested destination.");
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

            Assert.That(navMeshData, Is.Not.Null, "Runtime NavMeshBuilder.BuildNavMeshData returned null.");

            _navMeshDataInstance = NavMesh.AddNavMeshData(navMeshData);
            Assert.That(_navMeshDataInstance.valid, Is.True, "Runtime NavMeshDataInstance was not valid after AddNavMeshData.");
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
            var agentRoot = new GameObject("STK_Test_PathStatusSpikeAgent");
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
