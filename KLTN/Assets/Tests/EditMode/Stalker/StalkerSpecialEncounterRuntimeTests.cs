using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace EchoProtocol.AI.Stalker.Tests
{
    public sealed class StalkerSpecialEncounterRuntimeTests
    {
        private const string RuntimeTypeName =
            "EchoProtocol.AI.Stalker.Special.StalkerSpecialEncounterRuntime";

        private const string SettingsTypeName =
            "EchoProtocol.AI.Stalker.Special.StalkerSpecialEncounterSettings";

        private const string PhaseTypeName =
            "EchoProtocol.AI.Stalker.Special.StalkerSpecialEncounterPhase";

        private const string SimulationTimeTypeName =
            "AiSimulationTime";

        private GameObject _runtimeObject;
        private Component _runtime;

        private Type _runtimeType;
        private Type _settingsType;
        private Type _phaseType;
        private Type _simulationTimeType;

        private readonly List<GameObject> _createdObjects =
            new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            _runtimeType =
                ResolveProductionType(
                    RuntimeTypeName);

            _settingsType =
                ResolveProductionType(
                    SettingsTypeName);

            _phaseType =
                ResolveProductionType(
                    PhaseTypeName);

            _simulationTimeType =
                ResolveProductionTypeBySimpleName(
                    SimulationTimeTypeName);

            _runtimeObject =
                new GameObject(
                    "StalkerSpecialEncounterRuntime_Test");

            _runtime =
                _runtimeObject.AddComponent(
                    _runtimeType);

            Assert.That(
                _runtime,
                Is.Not.Null);
        }

        [TearDown]
        public void TearDown()
        {
            for (var i = _createdObjects.Count - 1;
                 i >= 0;
                 i--)
            {
                if (_createdObjects[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(
                        _createdObjects[i]);
                }
            }

            _createdObjects.Clear();

            if (_runtimeObject != null)
            {
                UnityEngine.Object.DestroyImmediate(
                    _runtimeObject);
            }
        }

        [Test]
        public void STK_SPECIAL_RUNTIME_001_DefaultSettings_AreCanonical()
        {
            var settings =
                Activator.CreateInstance(
                    _settingsType);

            Assert.That(
                GetPublicProperty<float>(
                    settings,
                    "CooldownSeconds"),
                Is.EqualTo(600f));

            Assert.That(
                GetPublicProperty<float>(
                    settings,
                    "FailedAttemptBackoffSeconds"),
                Is.EqualTo(15f));

            Assert.That(
                GetPublicProperty<float>(
                    settings,
                    "HiddenTransferDelaySeconds"),
                Is.EqualTo(0.05f));

            Assert.That(
                GetPublicProperty<float>(
                    settings,
                    "HiddenTransferVirtualSpeed"),
                Is.EqualTo(8f));

            Assert.That(
                GetPublicProperty<float>(
                    settings,
                    "HiddenTransferMinSeconds"),
                Is.EqualTo(1f));

            Assert.That(
                GetPublicProperty<float>(
                    settings,
                    "HiddenTransferMaxSeconds"),
                Is.EqualTo(8f));

            Assert.That(
                GetPublicProperty<float>(
                    settings,
                    "JumpInDurationSeconds"),
                Is.EqualTo(1f));

            Assert.That(
                GetPublicProperty<float>(
                    settings,
                    "ReactionLockSeconds"),
                Is.EqualTo(1.5f));

            Assert.That(
                GetPublicProperty<int>(
                    settings,
                    "MinimumOtherAlivePlayers"),
                Is.EqualTo(2));

            Assert.That(
                GetPublicProperty<float>(
                    settings,
                    "JumpInMinDistance"),
                Is.EqualTo(7f));

            Assert.That(
                GetPublicProperty<float>(
                    settings,
                    "JumpInMaxDistance"),
                Is.EqualTo(10f));

            Assert.That(
                GetPublicProperty<float>(
                    settings,
                    "PreferredJumpInDistance"),
                Is.EqualTo(8.5f));
        }

        [Test]
        public void STK_SPECIAL_RUNTIME_002_HiddenTransfer_UsesDistanceFormula()
        {
            _runtime.transform.position =
                Vector3.zero;

            AddEligiblePlayer(
                new Vector3(
                    16f,
                    0f,
                    0f));

            var duration =
                InvokeResolveHiddenTransferDuration(
                    out var travelDistance);

            Assert.That(
                travelDistance,
                Is.EqualTo(16f)
                    .Within(0.001f));

            //
            // 0.05 + 16 / 8 = 2.05 seconds.
            //
            Assert.That(
                duration,
                Is.EqualTo(2.05f)
                    .Within(0.001f));
        }

        [Test]
        public void STK_SPECIAL_RUNTIME_003_HiddenTransfer_ClampsToMinimum()
        {
            _runtime.transform.position =
                Vector3.zero;

            AddEligiblePlayer(
                new Vector3(
                    1f,
                    0f,
                    0f));

            var duration =
                InvokeResolveHiddenTransferDuration(
                    out var travelDistance);

            Assert.That(
                travelDistance,
                Is.EqualTo(1f)
                    .Within(0.001f));

            //
            // Raw:
            // 0.05 + 1 / 8 = 0.175
            //
            // Clamp:
            // 1 second.
            //
            Assert.That(
                duration,
                Is.EqualTo(1f)
                    .Within(0.001f));
        }

        [Test]
        public void STK_SPECIAL_RUNTIME_004_HiddenTransfer_ClampsToMaximum()
        {
            _runtime.transform.position =
                Vector3.zero;

            AddEligiblePlayer(
                new Vector3(
                    100f,
                    0f,
                    0f));

            var duration =
                InvokeResolveHiddenTransferDuration(
                    out var travelDistance);

            Assert.That(
                travelDistance,
                Is.EqualTo(100f)
                    .Within(0.001f));

            //
            // Raw:
            // 0.05 + 100 / 8 = 12.55
            //
            // Clamp:
            // 8 seconds.
            //
            Assert.That(
                duration,
                Is.EqualTo(8f)
                    .Within(0.001f));
        }

        [Test]
        public void STK_SPECIAL_RUNTIME_005_PhaseDurations_AreCanonical()
        {
            Assert.That(
                InvokeGetPhaseDuration(
                    "Sniff"),
                Is.EqualTo(1.5f)
                    .Within(0.001f));

            Assert.That(
                InvokeGetPhaseDuration(
                    "JumpOut"),
                Is.EqualTo(1f)
                    .Within(0.001f));

            Assert.That(
                InvokeGetPhaseDuration(
                    "JumpIn"),
                Is.EqualTo(1f)
                    .Within(0.001f));

            Assert.That(
                InvokeGetPhaseDuration(
                    "ReactionLock"),
                Is.EqualTo(1.5f)
                    .Within(0.001f));

            SetPrivateField(
                "_hiddenTransferDurationSeconds",
                4.25f);

            Assert.That(
                InvokeGetPhaseDuration(
                    "HiddenTransfer"),
                Is.EqualTo(4.25f)
                    .Within(0.001f));
        }

        [Test]
        public void STK_SPECIAL_RUNTIME_006_ReactionLockProgress_IsNormalized()
        {
            InvokeSetPhase(
                "ReactionLock");

            SetPrivateField(
                "_phaseElapsed",
                0.75f);

            var phase =
                GetPublicProperty<object>(
                    _runtime,
                    "Phase");

            Assert.That(
                phase.ToString(),
                Is.EqualTo(
                    "ReactionLock"));

            Assert.That(
                GetPublicProperty<float>(
                    _runtime,
                    "PhaseProgress01"),
                Is.EqualTo(0.5f)
                    .Within(0.001f));
        }

        [Test]
        public void STK_SPECIAL_RUNTIME_007_GlobalCooldownBoundary_Is600Seconds()
        {
            var settings =
                GetPrivateField<object>(
                    "settings");

            var cooldownSeconds =
                GetPublicProperty<float>(
                    settings,
                    "CooldownSeconds");

            Assert.That(
                cooldownSeconds,
                Is.EqualTo(600f));

            var start =
                CreateSimulationTime(
                    100,
                    100d);

            var cooldownUntil =
                InvokeAddSeconds(
                    start,
                    cooldownSeconds);

            SetPrivateField(
                "_cooldownUntil",
                cooldownUntil);

            var beforeExpiry =
                CreateSimulationTime(
                    101,
                    699.99d);

            var atExpiry =
                CreateSimulationTime(
                    102,
                    700d);

            Assert.That(
                InvokeIsCoolingDown(
                    beforeExpiry),
                Is.True);

            Assert.That(
                InvokeIsCoolingDown(
                    atExpiry),
                Is.False);
        }

        [Test]
        public void STK_SPECIAL_RUNTIME_008_FailedAttemptBackoffBoundary_Is15Seconds()
        {
            var settings =
                GetPrivateField<object>(
                    "settings");

            var backoffSeconds =
                GetPublicProperty<float>(
                    settings,
                    "FailedAttemptBackoffSeconds");

            Assert.That(
                backoffSeconds,
                Is.EqualTo(15f));

            var start =
                CreateSimulationTime(
                    200,
                    50d);

            var cooldownUntil =
                InvokeAddSeconds(
                    start,
                    backoffSeconds);

            SetPrivateField(
                "_cooldownUntil",
                cooldownUntil);

            var beforeExpiry =
                CreateSimulationTime(
                    201,
                    64.99d);

            var atExpiry =
                CreateSimulationTime(
                    202,
                    65d);

            Assert.That(
                InvokeIsCoolingDown(
                    beforeExpiry),
                Is.True);

            Assert.That(
                InvokeIsCoolingDown(
                    atExpiry),
                Is.False);
        }

        private void AddEligiblePlayer(
            Vector3 position)
        {
            var playerObject =
                new GameObject(
                    "EligiblePlayer");

            _createdObjects.Add(
                playerObject);

            playerObject.transform.position =
                position;

            var players =
                GetPrivateField<IList>(
                    "_eligibleAlivePlayerRoots");

            players.Add(
                playerObject.transform);
        }

        private float
            InvokeResolveHiddenTransferDuration(
                out float travelDistance)
        {
            var method =
                GetPrivateMethod(
                    "ResolveHiddenTransferDuration");

            var arguments =
                new object[]
                {
                    0f
                };

            var result =
                method.Invoke(
                    _runtime,
                    arguments);

            travelDistance =
                (float)arguments[0];

            return (float)result;
        }

        private float InvokeGetPhaseDuration(
            string phaseName)
        {
            var phase =
                CreatePhase(
                    phaseName);

            var result =
                GetPrivateMethod(
                        "GetPhaseDuration")
                    .Invoke(
                        _runtime,
                        new[]
                        {
                            phase
                        });

            return (float)result;
        }

        private void InvokeSetPhase(
            string phaseName)
        {
            var phase =
                CreatePhase(
                    phaseName);

            GetPrivateMethod(
                    "SetPhase")
                .Invoke(
                    _runtime,
                    new[]
                    {
                        phase
                    });
        }

        private bool InvokeIsCoolingDown(
            object simulationTime)
        {
            var result =
                GetPrivateMethod(
                        "IsCoolingDown")
                    .Invoke(
                        _runtime,
                        new[]
                        {
                            simulationTime
                        });

            return (bool)result;
        }

        private object InvokeAddSeconds(
            object simulationTime,
            float seconds)
        {
            var method =
                _runtimeType.GetMethod(
                    "AddSeconds",
                    BindingFlags.Static
                    | BindingFlags.NonPublic);

            Assert.That(
                method,
                Is.Not.Null,
                "Missing private static method 'AddSeconds'.");

            return method.Invoke(
                null,
                new object[]
                {
                    simulationTime,
                    seconds
                });
        }

        private object CreatePhase(
            string phaseName)
        {
            return Enum.Parse(
                _phaseType,
                phaseName);
        }

        private object CreateSimulationTime(
            long tick,
            double seconds)
        {
            var constructors =
                _simulationTimeType
                    .GetConstructors(
                        BindingFlags.Instance
                        | BindingFlags.Public
                        | BindingFlags.NonPublic);

            for (var i = 0;
                 i < constructors.Length;
                 i++)
            {
                var constructor =
                    constructors[i];

                var parameters =
                    constructor.GetParameters();

                if (parameters.Length != 2)
                {
                    continue;
                }

                try
                {
                    var convertedTick =
                        Convert.ChangeType(
                            tick,
                            parameters[0].ParameterType);

                    var convertedSeconds =
                        Convert.ChangeType(
                            seconds,
                            parameters[1].ParameterType);

                    return constructor.Invoke(
                        new[]
                        {
                            convertedTick,
                            convertedSeconds
                        });
                }
                catch
                {
                    //
                    // Try the next two-argument constructor.
                    //
                }
            }

            Assert.Fail(
                "Could not construct AiSimulationTime.");

            return null;
        }

        private MethodInfo GetPrivateMethod(
            string methodName)
        {
            var method =
                _runtimeType.GetMethod(
                    methodName,
                    BindingFlags.Instance
                    | BindingFlags.NonPublic);

            Assert.That(
                method,
                Is.Not.Null,
                $"Missing private method '{methodName}'.");

            return method;
        }

        private T GetPrivateField<T>(
            string fieldName)
        {
            var field =
                _runtimeType.GetField(
                    fieldName,
                    BindingFlags.Instance
                    | BindingFlags.NonPublic);

            Assert.That(
                field,
                Is.Not.Null,
                $"Missing private field '{fieldName}'.");

            return (T)field.GetValue(
                _runtime);
        }

        private void SetPrivateField(
            string fieldName,
            object value)
        {
            var field =
                _runtimeType.GetField(
                    fieldName,
                    BindingFlags.Instance
                    | BindingFlags.NonPublic);

            Assert.That(
                field,
                Is.Not.Null,
                $"Missing private field '{fieldName}'.");

            field.SetValue(
                _runtime,
                value);
        }

        private static T GetPublicProperty<T>(
            object instance,
            string propertyName)
        {
            Assert.That(
                instance,
                Is.Not.Null);

            var property =
                instance.GetType()
                    .GetProperty(
                        propertyName,
                        BindingFlags.Instance
                        | BindingFlags.Public);

            Assert.That(
                property,
                Is.Not.Null,
                $"Missing public property '{propertyName}'.");

            return (T)property.GetValue(
                instance);
        }

        private static Type ResolveProductionType(
            string fullName)
        {
            var assemblies =
                AppDomain.CurrentDomain
                    .GetAssemblies();

            for (var i = 0;
                 i < assemblies.Length;
                 i++)
            {
                var type =
                    assemblies[i]
                        .GetType(
                            fullName,
                            false);

                if (type != null)
                {
                    return type;
                }
            }

            Assert.Fail(
                $"Missing production type '{fullName}'.");

            return null;
        }

        private static Type
            ResolveProductionTypeBySimpleName(
                string simpleName)
        {
            var assemblies =
                AppDomain.CurrentDomain
                    .GetAssemblies();

            for (var assemblyIndex = 0;
                 assemblyIndex < assemblies.Length;
                 assemblyIndex++)
            {
                Type[] types;

                try
                {
                    types =
                        assemblies[assemblyIndex]
                            .GetTypes();
                }
                catch (ReflectionTypeLoadException exception)
                {
                    types =
                        exception.Types;
                }

                if (types == null)
                {
                    continue;
                }

                for (var typeIndex = 0;
                     typeIndex < types.Length;
                     typeIndex++)
                {
                    var type =
                        types[typeIndex];

                    if (type != null
                        && type.Name == simpleName)
                    {
                        return type;
                    }
                }
            }

            Assert.Fail(
                $"Missing production type '{simpleName}'.");

            return null;
        }
    }
}