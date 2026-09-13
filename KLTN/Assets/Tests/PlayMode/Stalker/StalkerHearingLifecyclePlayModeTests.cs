using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace EchoProtocol.AI.Stalker.Tests
{
    public sealed class StalkerHearingLifecyclePlayModeTests
    {
        private const string ControllerTypeName = "EchoProtocol.AI.Stalker.StalkerController";
        private const string InputTypeName = "EchoProtocol.AI.Stalker.StalkerSimulationInput";
        private const string StepTypeName = "EchoProtocol.AI.Common.AiSimulationStep";
        private const string TimeTypeName = "EchoProtocol.AI.Common.AiSimulationTime";
        private const string ObservationTypeName = "EchoProtocol.AI.Listener.Perception.HearingObservation";
        private const string OrderKeyTypeName = "EchoProtocol.AI.Listener.Noise.RuntimeNoiseEventOrderKey";
        private const string NoiseTypeName = "EchoProtocol.AI.Listener.Noise.RuntimeNoiseType";
        private const string OcclusionTypeName = "EchoProtocol.AI.Listener.Perception.ListenerOcclusionClass";

        [UnityTest]
        public IEnumerator STK_HearingLifecycle_DisableEnableClearsPreviousNoiseMemory()
        {
            var gameObject = new GameObject("STK_HearingLifecyclePlayMode");
            var component = gameObject.AddComponent(ResolveType(ControllerTypeName));
            var behaviour = (Behaviour)component;

            try
            {
                var heardAtUtc = DateTime.UtcNow;
                var hearingStep = CreateStep(1L, 0d, 0f);
                var hearingInput = CreateHearingInput(hearingStep, CreateObservation(heardAtUtc), heardAtUtc);

                Assert.That(InvokeSimulate(component, hearingInput), Is.True);
                Assert.That(Read(component, "CurrentState").ToString(), Is.EqualTo("SEARCH"));

                behaviour.enabled = false;
                yield return null;
                behaviour.enabled = true;
                yield return null;

                var postResetInput = CreateInputWithoutHearing(CreateStep(2L, 0.1d, 0.1f));
                Assert.That(InvokeSimulate(component, postResetInput), Is.True);
                Assert.That(Read(component, "CurrentState").ToString(), Is.EqualTo("PATROL"));
                Assert.That(Read(component, "ActiveSearchContext"), Is.Null);
            }
            finally
            {
                UnityEngine.Object.Destroy(gameObject);
            }
        }

        private static object CreateObservation(DateTime heardAtUtc)
        {
            var orderKey = Activator.CreateInstance(ResolveType(OrderKeyTypeName), new object[] { 1L, 1UL });
            var noiseType = Enum.Parse(ResolveType(NoiseTypeName), "INTERACTION");
            var occlusion = Enum.Parse(ResolveType(OcclusionTypeName), "CLEAR");
            var position = new Vector3(4f, 0f, 2f);
            return Activator.CreateInstance(ResolveType(ObservationTypeName), new object[]
            {
                "lifecycle-noise", orderKey, noiseType, position, heardAtUtc,
                heardAtUtc, heardAtUtc.AddSeconds(5), (double)position.magnitude,
                1d, 0.8d, occlusion
            });
        }

        private static object CreateStep(long tick, double seconds, float delta)
        {
            var simulationTime = Activator.CreateInstance(ResolveType(TimeTypeName), new object[] { tick, seconds });
            return Activator.CreateInstance(ResolveType(StepTypeName), simulationTime, delta);
        }

        private static object CreateHearingInput(object step, object observation, DateTime evaluationTimeUtc)
        {
            var observations = Array.CreateInstance(ResolveType(ObservationTypeName), 1);
            observations.SetValue(observation, 0);
            return Activator.CreateInstance(ResolveType(InputTypeName), step, null, null, null, observations, evaluationTimeUtc);
        }

        private static object CreateInputWithoutHearing(object step)
        {
            return Activator.CreateInstance(ResolveType(InputTypeName), step, null);
        }

        private static bool InvokeSimulate(object target, object input)
        {
            var method = target.GetType().GetMethod("Simulate", BindingFlags.Public | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null, "Missing public Simulate method.");
            try
            {
                return (bool)method.Invoke(target, new[] { input });
            }
            catch (TargetInvocationException exception)
            {
                throw exception.InnerException ?? exception;
            }
        }

        private static object Read(object target, string propertyName)
        {
            var property = target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(property, Is.Not.Null, $"Missing property '{propertyName}'.");
            return property.GetValue(target);
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

            Assert.Fail($"Could not resolve type '{fullTypeName}'.");
            return null;
        }
    }
}
