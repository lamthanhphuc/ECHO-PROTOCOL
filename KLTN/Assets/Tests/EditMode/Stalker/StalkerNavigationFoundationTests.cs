using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;

namespace EchoProtocol.AI.Stalker.Tests
{
    public sealed class StalkerNavigationFoundationTests
    {
        private const string NavigationControllerTypeName = "EchoProtocol.AI.Stalker.StalkerNavigationController";
        private const string NavigationPlanResultTypeName = "EchoProtocol.AI.Stalker.NavigationPlanResult";
        private const string NavigationPlanStatusTypeName = "EchoProtocol.AI.Stalker.NavigationPlanStatus";
        private const string NavigationExecutionStatusTypeName = "EchoProtocol.AI.Stalker.NavigationExecutionStatus";
        private const string NavigationPathStatusTypeName = "EchoProtocol.AI.Stalker.NavigationPathStatus";

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
        public void NAV_1_NullAgent_RequestDestination_ReturnsAgentUnavailable()
        {
            var destination = new Vector3(1f, 0f, 2f);
            var controller = CreateNavigationController(null);

            var result = RequestDestination(controller, destination);

            AssertNavigationPlanResult(result, "AgentUnavailable", destination, false);
            Assert.That(GetBoolProperty(controller, "HasActiveDestination"), Is.False);
            Assert.That(TrySetDestination(controller, destination), Is.False);
            Assert.That(GetExecutionStatusName(controller), Is.EqualTo("Failed"));
        }

        [Test]
        public void NAV_1_DisabledAgent_RequestDestination_ReturnsAgentUnavailable()
        {
            var destination = new Vector3(1f, 0f, 2f);
            var agent = CreateInactiveAgent("STK_Test_DisabledAgent");
            agent.enabled = false;
            var controller = CreateNavigationController(agent);

            var result = RequestDestination(controller, destination);

            AssertNavigationPlanResult(result, "AgentUnavailable", destination, false);
            Assert.That(GetBoolProperty(controller, "HasActiveDestination"), Is.False);
            Assert.That(TrySetDestination(controller, destination), Is.False);
            Assert.That(GetExecutionStatusName(controller), Is.EqualTo("Failed"));
        }

        [Test]
        public void NAV_1_EnabledOffNavMeshAgent_RequestDestination_ReturnsAgentNotOnNavMesh()
        {
            var destination = new Vector3(1f, 0f, 2f);
            var agent = CreateInactiveAgent("STK_Test_EnabledOffNavMeshAgent");
            agent.enabled = true;
            Assert.That(agent.isOnNavMesh, Is.False, "Inactive test agent should remain off NavMesh without a baked NavMesh fixture.");
            var controller = CreateNavigationController(agent);

            var result = RequestDestination(controller, destination);

            AssertNavigationPlanResult(result, "AgentNotOnNavMesh", destination, false);
            Assert.That(GetBoolProperty(controller, "HasActiveDestination"), Is.False);
            Assert.That(TrySetDestination(controller, destination), Is.False);
            Assert.That(GetExecutionStatusName(controller), Is.EqualTo("Failed"));
        }

        [Test]
        public void NAV_1_NavigationPlanResult_IsAccepted_MatchesRequestStatusContract()
        {
            var destination = new Vector3(1f, 0f, 2f);
            AssertPlanResultContract("Accepted", destination, true);
            AssertPlanResultContract("AlreadyActive", destination, true);
            AssertPlanResultContract("AgentUnavailable", destination, false);
            AssertPlanResultContract("AgentNotOnNavMesh", destination, false);
            AssertPlanResultContract("DestinationRequestFailed", destination, false);
        }

        [Test]
        public void NAV_2_NavigationPathStatus_ContainsExpectedFoundationMembers()
        {
            AssertExactEnumNames(
                NavigationPathStatusTypeName,
                new[]
                {
                    "NoDestination",
                    "Pending",
                    "Complete",
                    "Partial",
                    "Invalid",
                    "Stale",
                    "AgentUnavailable",
                    "AgentNotOnNavMesh"
                });
        }

        [Test]
        public void NAV_2_NullAgent_GetPathStatus_ReturnsAgentUnavailable()
        {
            var controller = CreateNavigationController(null);

            Assert.That(GetPathStatusName(controller), Is.EqualTo("AgentUnavailable"));
            Assert.That(GetExecutionStatusName(controller), Is.EqualTo("Failed"));
        }

        [Test]
        public void NAV_2_DisabledAgent_GetPathStatus_ReturnsAgentUnavailable()
        {
            var agent = CreateInactiveAgent("STK_Test_NAV2_DisabledAgent");
            agent.enabled = false;
            var controller = CreateNavigationController(agent);

            Assert.That(GetPathStatusName(controller), Is.EqualTo("AgentUnavailable"));
            Assert.That(GetExecutionStatusName(controller), Is.EqualTo("Failed"));
        }

        [Test]
        public void NAV_2_EnabledOffNavMeshAgent_GetPathStatus_ReturnsAgentNotOnNavMesh()
        {
            var agent = CreateInactiveAgent("STK_Test_NAV2_EnabledOffNavMeshAgent");
            agent.enabled = true;
            Assert.That(agent.isOnNavMesh, Is.False, "Inactive test agent should remain off NavMesh without a baked NavMesh fixture.");
            var controller = CreateNavigationController(agent);

            Assert.That(GetPathStatusName(controller), Is.EqualTo("AgentNotOnNavMesh"));
            Assert.That(GetExecutionStatusName(controller), Is.EqualTo("Failed"));
        }

        [Test]
        public void NAV_2_ForceRepathOverload_UnusableAgentPreservesTypedFailureSemantics()
        {
            var destination = new Vector3(1f, 0f, 2f);
            var controller = CreateNavigationController(null);

            var result = RequestDestination(controller, destination, true);

            AssertNavigationPlanResult(result, "AgentUnavailable", destination, false);
            Assert.That(GetBoolProperty(controller, "HasActiveDestination"), Is.False);
        }

        private NavMeshAgent CreateInactiveAgent(string name)
        {
            var root = new GameObject(name);
            root.SetActive(false);
            _createdObjects.Add(root);
            return root.AddComponent<NavMeshAgent>();
        }

        private static object CreateNavigationController(NavMeshAgent agent)
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

        private static bool TrySetDestination(object controller, Vector3 destination)
        {
            var value = InvokeMethod(controller, "TrySetDestination", new object[] { destination });
            Assert.That(value, Is.TypeOf<bool>(), "StalkerNavigationController.TrySetDestination must return bool.");
            return (bool)value;
        }

        private static string GetExecutionStatusName(object controller)
        {
            var value = InvokeMethod(controller, "GetExecutionStatus", Array.Empty<object>());
            return GetEnumName(value, NavigationExecutionStatusTypeName);
        }

        private static string GetPathStatusName(object controller)
        {
            var value = InvokeMethod(controller, "GetPathStatus", Array.Empty<object>());
            return GetEnumName(value, NavigationPathStatusTypeName);
        }

        private static void AssertPlanResultContract(string statusName, Vector3 destination, bool expectedAccepted)
        {
            var statusType = ResolveType(NavigationPlanStatusTypeName);
            var resultType = ResolveType(NavigationPlanResultTypeName);
            var status = Enum.Parse(statusType, statusName);
            var result = Activator.CreateInstance(resultType, status, destination);

            AssertNavigationPlanResult(result, statusName, destination, expectedAccepted);
        }

        private static void AssertNavigationPlanResult(object result, string expectedStatusName, Vector3 expectedDestination, bool expectedAccepted)
        {
            Assert.That(result, Is.Not.Null, "NavigationPlanResult invocation returned null.");
            Assert.That(result.GetType(), Is.EqualTo(ResolveType(NavigationPlanResultTypeName)), "Unexpected result type.");
            Assert.That(GetEnumPropertyName(result, "Status", NavigationPlanStatusTypeName), Is.EqualTo(expectedStatusName));
            Assert.That(GetVector3Property(result, "RequestedDestination"), Is.EqualTo(expectedDestination));
            Assert.That(GetBoolProperty(result, "IsAccepted"), Is.EqualTo(expectedAccepted));
        }

        private static object InvokeMethod(object target, string methodName, object[] args)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(method, Is.Not.Null, $"Missing public method '{methodName}' on '{target.GetType().FullName}'.");

            return method.Invoke(target, args);
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

        private static bool GetBoolProperty(object target, string propertyName)
        {
            var value = GetProperty(target, propertyName);
            Assert.That(value, Is.TypeOf<bool>(), $"Property '{propertyName}' must return bool.");
            return (bool)value;
        }

        private static Vector3 GetVector3Property(object target, string propertyName)
        {
            var value = GetProperty(target, propertyName);
            Assert.That(value, Is.TypeOf<Vector3>(), $"Property '{propertyName}' must return Vector3.");
            return (Vector3)value;
        }

        private static string GetEnumPropertyName(object target, string propertyName, string expectedTypeName)
        {
            return GetEnumName(GetProperty(target, propertyName), expectedTypeName);
        }

        private static string GetEnumName(object value, string expectedTypeName)
        {
            Assert.That(value, Is.Not.Null, "Expected enum value but got null.");
            Assert.That(value.GetType(), Is.EqualTo(ResolveType(expectedTypeName)), $"Enum value must be '{expectedTypeName}'.");
            Assert.That(value.GetType().IsEnum, Is.True, $"Type '{value.GetType().FullName}' must be an enum.");
            return value.ToString();
        }

        private static object GetProperty(object target, string propertyName)
        {
            var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, $"Missing public property '{propertyName}' on '{target.GetType().FullName}'.");

            return property.GetValue(target);
        }

        private static void AssertExactEnumNames(string fullTypeName, string[] expectedNames)
        {
            var enumType = ResolveType(fullTypeName);
            Assert.That(enumType.IsEnum, Is.True, $"Production type '{fullTypeName}' must be an enum.");

            var actualNames = Enum.GetNames(enumType);
            var expectedNameSet = new HashSet<string>(expectedNames);
            Assert.That(actualNames, Has.Length.EqualTo(expectedNames.Length), $"Enum '{fullTypeName}' must contain exactly the expected member count.");

            for (var i = 0; i < expectedNames.Length; i++)
            {
                Assert.That(actualNames, Does.Contain(expectedNames[i]), $"Missing enum member '{expectedNames[i]}' on '{fullTypeName}'.");
            }

            for (var i = 0; i < actualNames.Length; i++)
            {
                Assert.That(expectedNameSet.Contains(actualNames[i]), Is.True, $"Unexpected enum member '{actualNames[i]}' on '{fullTypeName}'.");
            }
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
