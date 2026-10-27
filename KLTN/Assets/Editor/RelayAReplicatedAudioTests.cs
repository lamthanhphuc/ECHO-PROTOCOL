using System;
using System.Reflection;
using EchoProtocol.RelayA;
using NUnit.Framework;
using UnityEngine;

namespace EchoProtocol.Tests
{
    public sealed class RelayAReplicatedAudioTests
    {
        private GameObject _owner;
        private RelayAController _controller;
        private MethodInfo _observe;

        [SetUp]
        public void SetUp()
        {
            _owner = new GameObject("RelayAReplicatedAudioTest");
            _controller = _owner.AddComponent<RelayAController>();
            _observe = typeof(RelayAController).GetMethod("ObserveReplicatedAudio", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(_observe, Is.Not.Null, "Relay A needs presentation-only replicated cue observation.");
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(_owner);

        private string Observe(RelayACircuitPhase phase, bool fault = false, bool trip = false,
            int sequence = 0, int scenario = 0)
        {
            var snapshot = new RelayACircuitSnapshot(null, scenario, null, phase, 0, 0, trip, sequence, fault);
            return _observe.Invoke(_controller, new object[] { snapshot, default(RelayAStabilizationSnapshot) }).ToString();
        }

        [TestCase(RelayACircuitPhase.Online, true, false)]
        [TestCase(RelayACircuitPhase.Faulted, true, false)]
        [TestCase(RelayACircuitPhase.Failed, true, true)]
        public void FirstSnapshot_DoesNotReplayHistoricalCues(RelayACircuitPhase phase, bool fault, bool trip)
        {
            Assert.That(Observe(phase, fault, trip, 3), Is.EqualTo("None"));
            Assert.That(Observe(phase, fault, trip, 3), Is.EqualTo("None"));
        }

        [Test]
        public void LiveSnapshots_PlayFaultTripAndCompletionOnce_WithoutGameplayEvents()
        {
            var config = ScriptableObject.CreateInstance<RelayAConfig>();
            try
            {
                config.InitializeDefaultCircuitScenariosIfEmpty();
                var simulation = new RelayACircuitSimulation();
                simulation.Initialize(config, 0, 42);
                int gameplayEvents = 0;
                simulation.FaultActivated += () => gameplayEvents++;
                simulation.ProtectionTripped += () => gameplayEvents++;
                simulation.Completed += () => gameplayEvents++;
                Assert.That(_observe.Invoke(_controller, new object[] { simulation.Snapshot, simulation.Stabilization.Snapshot }).ToString(), Is.EqualTo("None"));
                Assert.That(Observe(RelayACircuitPhase.Testing, sequence: 1), Is.EqualTo("None"));
                Assert.That(Observe(RelayACircuitPhase.Faulted, true, sequence: 1), Is.EqualTo("Warning"));
                Assert.That(Observe(RelayACircuitPhase.Faulted, true, sequence: 1), Is.EqualTo("None"));
                Assert.That(Observe(RelayACircuitPhase.Testing, true, sequence: 2), Is.EqualTo("None"));
                Assert.That(Observe(RelayACircuitPhase.Failed, true, true, 2), Is.EqualTo("Overload"));
                Assert.That(Observe(RelayACircuitPhase.Failed, true, true, 2), Is.EqualTo("None"));
                Assert.That(Observe(RelayACircuitPhase.Online, true, sequence: 3), Is.EqualTo("Complete"));
                Assert.That(Observe(RelayACircuitPhase.Online, true, sequence: 3), Is.EqualTo("None"));
                Assert.That(simulation.Snapshot.Phase, Is.EqualTo(RelayACircuitPhase.Editing));
                Assert.That(gameplayEvents, Is.Zero, "Presentation must not replay authority/gameplay callbacks.");
            }
            finally { UnityEngine.Object.DestroyImmediate(config); }
        }

        private string ObserveStabilization(RelayAStabilizationSnapshot stabilization)
        {
            var snapshot = new RelayACircuitSnapshot(null, 0, null, RelayACircuitPhase.StabilizeOutput,
                0, 0, false, 2, true);
            return _observe.Invoke(_controller, new object[] { snapshot, stabilization }).ToString();
        }

        private static RelayAStabilizationSnapshot Stabilization(RelayAFaultType warning = RelayAFaultType.None,
            RelayAFaultType active = RelayAFaultType.None, bool dangerous = false, bool running = true, float progress = 3f)
            => new RelayAStabilizationSnapshot(default, default, default, default, default, default, default,
                progress, 12f, 0f, warning, active, 1f, 1f, !dangerous, dangerous, running, false);

        [Test]
        public void Stabilization_WarningActivationAndDangerEachPlayOnce_AndRecoveryRearms()
        {
            Assert.That(ObserveStabilization(Stabilization()), Is.EqualTo("None"));
            Assert.That(ObserveStabilization(Stabilization(warning: RelayAFaultType.Overvoltage)), Is.EqualTo("Warning"));
            Assert.That(ObserveStabilization(Stabilization(warning: RelayAFaultType.Overvoltage)), Is.EqualTo("None"));
            Assert.That(ObserveStabilization(Stabilization(active: RelayAFaultType.Overvoltage)), Is.EqualTo("Overload"));
            Assert.That(ObserveStabilization(Stabilization(active: RelayAFaultType.Overvoltage)), Is.EqualTo("None"));
            Assert.That(ObserveStabilization(Stabilization()), Is.EqualTo("None"));
            Assert.That(ObserveStabilization(Stabilization(dangerous: true)), Is.EqualTo("Overload"));
            Assert.That(ObserveStabilization(Stabilization(dangerous: true)), Is.EqualTo("None"));
            Assert.That(ObserveStabilization(Stabilization()), Is.EqualTo("None"));
            Assert.That(ObserveStabilization(Stabilization(dangerous: true)), Is.EqualTo("Overload"));
        }

        [Test]
        public void InitialStabilizationFault_IsSilent_AndMatrixCompletionPlaysOnce()
        {
            Assert.That(ObserveStabilization(Stabilization(active: RelayAFaultType.LoadImbalance, dangerous: true)), Is.EqualTo("None"));
            _controller.ResetForRetry();
            Observe(RelayACircuitPhase.BreakerMatrix, true, sequence: 2);
            Assert.That(ObserveStabilization(Stabilization()), Is.EqualTo("MatrixBalanced"));
            Assert.That(ObserveStabilization(Stabilization()), Is.EqualTo("None"));
            Assert.That(Observe(RelayACircuitPhase.Online, true, sequence: 2), Is.EqualTo("Complete"));
            Assert.That(Observe(RelayACircuitPhase.Online, true, sequence: 2), Is.EqualTo("None"));
        }

        [Test]
        public void ApplyingReplicatedStates_DoesNotReplayAuthorityCallbacks()
        {
            var config = ScriptableObject.CreateInstance<RelayAConfig>();
            try
            {
                config.InitializeDefaultCircuitScenariosIfEmpty();
                typeof(RelayAController).GetField("config", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_controller, config);
                _controller.Circuit.Initialize(config, 0, 42);
                int gameplayEvents = 0;
                _controller.RelayAOnline += () => gameplayEvents++;
                _controller.Circuit.Completed += () => gameplayEvents++;
                _controller.Circuit.FaultActivated += () => gameplayEvents++;
                _controller.Circuit.ProtectionTripped += () => gameplayEvents++;
                _controller.ApplyAuthoritativeCircuitState(0, 0, RelayACircuitPhase.Editing, 0, 0, false, 0, false);
                _controller.ApplyAuthoritativeCircuitState(0, 0, RelayACircuitPhase.Faulted, 0, 0, false, 1, true);
                _controller.ApplyAuthoritativeCircuitState(0, 0, RelayACircuitPhase.Failed, 0, 0, true, 2, true);
                _controller.ApplyAuthoritativeCircuitState(0, 0, RelayACircuitPhase.Online, 0, 0, false, 3, true);
                _controller.ApplyOnlineFromAuthority();
                _controller.ApplyOnlineFromAuthority();
                typeof(RelayAController).GetMethod("PresentReplicatedAudio", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(_controller, new object[] { _controller.Circuit.Snapshot, _controller.Circuit.Stabilization.Snapshot });
                Assert.That(_controller.IsOnline, Is.True);
                Assert.That(gameplayEvents, Is.Zero, "Replicated presentation must not run callbacks that submit gameplay noise.");
            }
            finally { UnityEngine.Object.DestroyImmediate(config); }
        }

        [Test]
        public void ANewFailedTest_PlayOverloadEvenWhenIntermediateSnapshotsWereSkipped()
        {
            Observe(RelayACircuitPhase.Editing);
            Assert.That(Observe(RelayACircuitPhase.Failed, trip: true, sequence: 1), Is.EqualTo("Overload"));
            Assert.That(Observe(RelayACircuitPhase.Failed, trip: true, sequence: 2), Is.EqualTo("Overload"));
            Assert.That(Observe(RelayACircuitPhase.Failed, trip: true, sequence: 2), Is.EqualTo("None"));
        }

        [Test]
        public void RetryAndScenarioChange_SeedNewBaseline_ThenAcceptNewLiveCues()
        {
            Observe(RelayACircuitPhase.Editing);
            Observe(RelayACircuitPhase.Online, true, sequence: 3);
            _controller.ResetForRetry();
            Assert.That(Observe(RelayACircuitPhase.Faulted, true, sequence: 1), Is.EqualTo("None"));
            Assert.That(Observe(RelayACircuitPhase.Online, true, sequence: 2), Is.EqualTo("Complete"));
            Assert.That(Observe(RelayACircuitPhase.Faulted, true, sequence: 1, scenario: 1), Is.EqualTo("None"));
            Assert.That(Observe(RelayACircuitPhase.Online, true, sequence: 2, scenario: 1), Is.EqualTo("Complete"));
        }
    }
}
