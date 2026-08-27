using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace EchoProtocol.AI.Stalker.Tests
{
    public sealed class StalkerNavigationProgressMonitorTests
    {
        private const string NavigationProgressMonitorTypeName = "EchoProtocol.AI.Stalker.NavigationProgressMonitor";
        private const string NavigationProgressSettingsTypeName = "EchoProtocol.AI.Stalker.NavigationProgressSettings";
        private const string NavigationProgressStateTypeName = "EchoProtocol.AI.Stalker.NavigationProgressState";

        private const float SampleInterval = 0.10f;
        private const float MinimumDisplacement = 0.05f;
        private const float MinimumRemainingDistanceImprovement = 0.05f;
        private const float NoProgressDuration = 0.20f;
        private const float StuckDuration = 0.50f;
        private const float SampleDeltaTime = 0.11f;

        [Test]
        public void NAV_4A_FirstObservation_EstablishesMovingBaseline()
        {
            var monitor = CreateMonitor();

            Observe(monitor, Vector3.zero, 10f, 10f);

            Assert.That(GetStateName(monitor), Is.EqualTo("Moving"));
        }

        [Test]
        public void NAV_4A_StationarySamples_BecomeNoProgress()
        {
            var monitor = CreateMonitor();
            Observe(monitor, Vector3.zero, 10f, SampleDeltaTime);

            Observe(monitor, Vector3.zero, 10f, SampleDeltaTime);
            Assert.That(GetStateName(monitor), Is.EqualTo("Moving"));

            Observe(monitor, Vector3.zero, 10f, SampleDeltaTime);
            Assert.That(GetStateName(monitor), Is.EqualTo("NoProgress"));
        }

        [Test]
        public void NAV_4A_SustainedStationarySamples_BecomeStuck()
        {
            var monitor = CreateMonitor();
            Observe(monitor, Vector3.zero, 10f, SampleDeltaTime);

            Observe(monitor, Vector3.zero, 10f, SampleDeltaTime);
            Observe(monitor, Vector3.zero, 10f, SampleDeltaTime);
            Observe(monitor, Vector3.zero, 10f, SampleDeltaTime);
            Observe(monitor, Vector3.zero, 10f, SampleDeltaTime);
            Observe(monitor, Vector3.zero, 10f, SampleDeltaTime);

            Assert.That(GetStateName(monitor), Is.EqualTo("Stuck"));
        }

        [Test]
        public void NAV_4A_MeaningfulDisplacement_ResetsNoProgressToMoving()
        {
            var monitor = CreateMonitor();
            Observe(monitor, Vector3.zero, 10f, SampleDeltaTime);
            Observe(monitor, Vector3.zero, 10f, SampleDeltaTime);
            Observe(monitor, Vector3.zero, 10f, SampleDeltaTime);
            Assert.That(GetStateName(monitor), Is.EqualTo("NoProgress"));

            Observe(monitor, new Vector3(0.10f, 0f, 0f), 10f, SampleDeltaTime);
            Assert.That(GetStateName(monitor), Is.EqualTo("Moving"));

            Observe(monitor, new Vector3(0.10f, 0f, 0f), 10f, SampleDeltaTime);
            Assert.That(GetStateName(monitor), Is.EqualTo("Moving"));
        }

        [Test]
        public void NAV_4A_RemainingDistanceImprovement_CountsAsMeaningfulProgress()
        {
            var monitor = CreateMonitor();
            Observe(monitor, Vector3.zero, 10f, SampleDeltaTime);
            Observe(monitor, Vector3.zero, 10f, SampleDeltaTime);
            Observe(monitor, Vector3.zero, 10f, SampleDeltaTime);
            Assert.That(GetStateName(monitor), Is.EqualTo("NoProgress"));

            Observe(monitor, Vector3.zero, 9.9f, SampleDeltaTime);
            Assert.That(GetStateName(monitor), Is.EqualTo("Moving"));
        }

        [Test]
        public void NAV_4A_Reset_ClearsProgressHistory()
        {
            var monitor = CreateMonitor();
            Observe(monitor, Vector3.zero, 10f, SampleDeltaTime);
            Observe(monitor, Vector3.zero, 10f, SampleDeltaTime);
            Observe(monitor, Vector3.zero, 10f, SampleDeltaTime);
            Assert.That(GetStateName(monitor), Is.EqualTo("NoProgress"));

            Reset(monitor);
            Assert.That(GetStateName(monitor), Is.EqualTo("Moving"));

            Observe(monitor, Vector3.zero, float.PositiveInfinity, 10f);
            Assert.That(GetStateName(monitor), Is.EqualTo("Moving"));
        }

        private static object CreateMonitor()
        {
            var monitorType = ResolveType(NavigationProgressMonitorTypeName);
            var settings = CreateSettings();
            var constructor = monitorType.GetConstructor(
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { ResolveType(NavigationProgressSettingsTypeName) },
                null);
            Assert.That(constructor, Is.Not.Null, $"Missing public constructor '{NavigationProgressMonitorTypeName}(NavigationProgressSettings)'.");

            return constructor.Invoke(new[] { settings });
        }

        private static object CreateSettings()
        {
            var settingsType = ResolveType(NavigationProgressSettingsTypeName);
            var constructor = settingsType.GetConstructor(
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(float), typeof(float), typeof(float), typeof(float), typeof(float) },
                null);
            Assert.That(constructor, Is.Not.Null, $"Missing public constructor '{NavigationProgressSettingsTypeName}(float, float, float, float, float)'.");

            return constructor.Invoke(new object[]
            {
                SampleInterval,
                MinimumDisplacement,
                MinimumRemainingDistanceImprovement,
                NoProgressDuration,
                StuckDuration
            });
        }

        private static void Observe(object monitor, Vector3 position, float remainingDistance, float deltaTime)
        {
            InvokeMethod(
                monitor,
                "Observe",
                new[] { typeof(Vector3), typeof(float), typeof(float) },
                new object[] { position, remainingDistance, deltaTime });
        }

        private static void Reset(object monitor)
        {
            InvokeMethod(monitor, "Reset", Type.EmptyTypes, Array.Empty<object>());
        }

        private static string GetStateName(object monitor)
        {
            var value = GetProperty(monitor, "State");
            Assert.That(value, Is.Not.Null, "NavigationProgressMonitor.State returned null.");
            Assert.That(value.GetType(), Is.EqualTo(ResolveType(NavigationProgressStateTypeName)));
            Assert.That(value.GetType().IsEnum, Is.True, "NavigationProgressMonitor.State must return an enum.");
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
