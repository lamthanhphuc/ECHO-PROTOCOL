using System;
using System.Reflection;
using NUnit.Framework;

namespace EchoProtocol.Networking.Tests
{
    public sealed class FusionAiSimulationStepAdapterTests
    {
        private const string AdapterTypeName = "EchoProtocol.Networking.FusionAiSimulationStepAdapter";

        [Test]
        public void FND_NET_TIME_KnownAuthoritativeValues_MapExactly()
        {
            var tick = 42L;
            var simulationTime = 0.7d;
            var deltaTime = 1f / 60f;

            var created = TryCreateFromValues(tick, simulationTime, deltaTime, out var step);

            Assert.That(created, Is.True);
            Assert.That(GetBoolProperty(step, "IsValid"), Is.True);
            Assert.That(GetLongProperty(GetProperty(step, "Time"), "Tick"), Is.EqualTo(tick));
            Assert.That(GetDoubleProperty(GetProperty(step, "Time"), "Seconds"), Is.EqualTo(simulationTime));
            Assert.That(GetFloatProperty(step, "DeltaSeconds"), Is.EqualTo(deltaTime));
        }

        [Test]
        public void FND_NET_TIME_ZeroOriginValues_AreValid()
        {
            var created = TryCreateFromValues(0L, 0d, 0f, out var step);

            Assert.That(created, Is.True);
            Assert.That(GetBoolProperty(step, "IsValid"), Is.True);
            Assert.That(GetLongProperty(GetProperty(step, "Time"), "Tick"), Is.EqualTo(0L));
            Assert.That(GetDoubleProperty(GetProperty(step, "Time"), "Seconds"), Is.EqualTo(0d));
            Assert.That(GetFloatProperty(step, "DeltaSeconds"), Is.EqualTo(0f));
        }

        [Test]
        public void FND_NET_TIME_NegativeTick_FailsClosed()
        {
            var created = TryCreateFromValues(-1L, 0.1d, 0.02f, out var step);

            Assert.That(created, Is.False);
            Assert.That(GetBoolProperty(step, "IsValid"), Is.False);
        }

        [Test]
        public void FND_NET_TIME_InvalidSimulationTime_FailsClosed()
        {
            AssertInvalidSimulationTime(-0.1d);
            AssertInvalidSimulationTime(double.NaN);
            AssertInvalidSimulationTime(double.PositiveInfinity);
        }

        [Test]
        public void FND_NET_TIME_InvalidDelta_FailsClosed()
        {
            AssertInvalidDelta(-0.01f);
            AssertInvalidDelta(float.NaN);
            AssertInvalidDelta(float.PositiveInfinity);

            var publicResult = TryCreateWithNullRunner(out var publicStep);
            Assert.That(publicResult, Is.False);
            Assert.That(GetBoolProperty(publicStep, "IsValid"), Is.False);
        }

        private static void AssertInvalidSimulationTime(double simulationTime)
        {
            var created = TryCreateFromValues(1L, simulationTime, 0.02f, out var step);

            Assert.That(created, Is.False);
            Assert.That(GetBoolProperty(step, "IsValid"), Is.False);
        }

        private static void AssertInvalidDelta(float deltaTime)
        {
            var created = TryCreateFromValues(1L, 0.1d, deltaTime, out var step);

            Assert.That(created, Is.False);
            Assert.That(GetBoolProperty(step, "IsValid"), Is.False);
        }

        private static bool TryCreateFromValues(long tick, double simulationTime, float deltaTime, out object step)
        {
            var method = ResolveType(AdapterTypeName).GetMethod(
                "TryCreateFromValues",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "Missing non-public FusionAiSimulationStepAdapter.TryCreateFromValues seam.");

            var args = new object[] { tick, simulationTime, deltaTime, null };
            var result = method.Invoke(null, args);
            Assert.That(result, Is.TypeOf<bool>());

            step = args[3];
            return (bool)result;
        }

        private static bool TryCreateWithNullRunner(out object step)
        {
            var method = ResolveType(AdapterTypeName).GetMethod(
                "TryCreate",
                BindingFlags.Static | BindingFlags.Public);
            Assert.That(method, Is.Not.Null, "Missing public FusionAiSimulationStepAdapter.TryCreate.");

            var args = new object[] { null, null };
            var result = method.Invoke(null, args);
            Assert.That(result, Is.TypeOf<bool>());

            step = args[1];
            return (bool)result;
        }

        private static bool GetBoolProperty(object target, string propertyName)
        {
            var value = GetProperty(target, propertyName);
            Assert.That(value, Is.TypeOf<bool>(), $"Property '{propertyName}' must return bool.");
            return (bool)value;
        }

        private static long GetLongProperty(object target, string propertyName)
        {
            var value = GetProperty(target, propertyName);
            Assert.That(value, Is.TypeOf<long>(), $"Property '{propertyName}' must return long.");
            return (long)value;
        }

        private static double GetDoubleProperty(object target, string propertyName)
        {
            var value = GetProperty(target, propertyName);
            Assert.That(value, Is.TypeOf<double>(), $"Property '{propertyName}' must return double.");
            return (double)value;
        }

        private static float GetFloatProperty(object target, string propertyName)
        {
            var value = GetProperty(target, propertyName);
            Assert.That(value, Is.TypeOf<float>(), $"Property '{propertyName}' must return float.");
            return (float)value;
        }

        private static object GetProperty(object target, string propertyName)
        {
            var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, $"Missing public property '{propertyName}' on '{target.GetType().FullName}'.");

            return property.GetValue(target);
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
