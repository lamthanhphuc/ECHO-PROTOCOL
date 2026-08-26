using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace EchoProtocol.AI.Stalker.Tests
{
    public sealed class PatrolRouteCanonicalTests
    {
        private const string PatrolRouteTypeName = "EchoProtocol.AI.Stalker.PatrolRoute";

        private GameObject _patrolRoot;

        [TearDown]
        public void TearDown()
        {
            if (_patrolRoot != null)
            {
                UnityEngine.Object.DestroyImmediate(_patrolRoot);
                _patrolRoot = null;
            }
        }

        [Test]
        public void STK_R_013_PatrolRoute_FollowsDirectChildSiblingOrderAndWraps()
        {
            var route = CreatePatrolRoute(out var p0, out var p1, out var p2, out var p3);

            Assert.That(GetPointCount(route), Is.EqualTo(4));
            AssertPoint(route, 0, p0);
            AssertPoint(route, 1, p1);
            AssertPoint(route, 2, p2);
            AssertPoint(route, 3, p3);
            AssertPoint(route, 4, p0);
            AssertPoint(route, 5, p1);
            AssertPoint(route, -1, p3);
        }

        private Component CreatePatrolRoute(out Transform p0, out Transform p1, out Transform p2, out Transform p3)
        {
            var patrolRouteType = ResolveType(PatrolRouteTypeName);
            _patrolRoot = new GameObject("STK_Test_PatrolRoot");
            var route = _patrolRoot.AddComponent(patrolRouteType);

            p0 = CreatePoint("P0", 0);
            p1 = CreatePoint("P1", 1);
            p2 = CreatePoint("P2", 2);
            p3 = CreatePoint("P3", 3);

            return route;
        }

        private Transform CreatePoint(string name, int siblingIndex)
        {
            var point = new GameObject(name);
            point.transform.SetParent(_patrolRoot.transform, false);
            point.transform.SetSiblingIndex(siblingIndex);
            return point.transform;
        }

        private static void AssertPoint(Component route, int index, Transform expectedPoint)
        {
            Assert.That(TryGetPoint(route, index, out var actualPoint), Is.True, $"Expected PatrolRoute point lookup at index {index} to succeed.");
            Assert.That(actualPoint, Is.SameAs(expectedPoint), $"Expected PatrolRoute index {index} to resolve to '{expectedPoint.name}'.");
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

        private static int GetPointCount(Component route)
        {
            var property = route.GetType().GetProperty("PointCount", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, $"Missing public property 'PointCount' on '{route.GetType().FullName}'.");

            var value = property.GetValue(route);
            Assert.That(value, Is.TypeOf<int>(), "PatrolRoute.PointCount must return int.");
            return (int)value;
        }

        private static bool TryGetPoint(Component route, int index, out Transform point)
        {
            var method = route.GetType().GetMethod(
                "TryGetPoint",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(int), typeof(Transform).MakeByRefType() },
                null);
            Assert.That(method, Is.Not.Null, $"Missing public method 'TryGetPoint(int, out Transform)' on '{route.GetType().FullName}'.");

            var args = new object[] { index, null };
            var result = method.Invoke(route, args);
            Assert.That(result, Is.TypeOf<bool>(), "PatrolRoute.TryGetPoint must return bool.");

            point = args[1] as Transform;
            return (bool)result;
        }
    }
}
