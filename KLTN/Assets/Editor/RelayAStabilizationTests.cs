using System.Reflection;
using EchoProtocol.RelayA;
using NUnit.Framework;
using UnityEngine;

namespace EchoProtocol.Tests
{
    public sealed class RelayAStabilizationTests
    {
        private RelayAConfig _config;
        private RelayAStabilizationSimulation _simulation;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<RelayAConfig>();
            Set("enableFault", false);
            _simulation = new RelayAStabilizationSimulation();
            _simulation.Initialize(_config);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_config);

        private void Set(string name, object value) => typeof(RelayAConfig)
            .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(_config, value);

        private void Tick(float seconds)
        {
            for (int i = 0; i < Mathf.RoundToInt(seconds / 0.05f); i++) _simulation.Tick(0.05f);
        }

        private void SettleSafe()
        {
            var controls = _config.SolvedControls;
            _simulation.SetControls(controls.x, controls.y, controls.z);
            Tick(10f);
            Assert.IsTrue(_simulation.Snapshot.IsStable);
        }

        [Test]
        public void SafeOutputs_OnlyProgressWhileRunning_CompleteOnce()
        {
            Assert.IsFalse(_simulation.Snapshot.IsStable);
            SettleSafe();
            Assert.That(_simulation.Snapshot.StabilitySeconds, Is.Zero);
            int completions = 0;
            _simulation.Completed += () => completions++;
            _simulation.Start();
            Tick(_config.StabilityRequiredSeconds - 0.5f);
            Assert.IsFalse(_simulation.Snapshot.IsOnline);
            Tick(1f);
            Assert.IsTrue(_simulation.Snapshot.IsOnline);
            Tick(10f);
            Assert.That(completions, Is.EqualTo(1));
        }

        [Test]
        public void UnsafeOutputs_DoNotGainStability()
        {
            _simulation.SetControls(0f, 100f, 0f);
            Tick(10f);
            Assert.IsFalse(_simulation.Snapshot.IsStable);
            _simulation.Start();
            Tick(2f);
            Assert.That(_simulation.Snapshot.StabilitySeconds, Is.Zero);
            Assert.IsFalse(_simulation.Snapshot.IsOnline);
        }

        [Test]
        public void Instability_UsesGraceThenDecay_DangerDecaysFaster()
        {
            SettleSafe();
            _simulation.Start();
            Tick(3f);
            _simulation.EmergencyStop();
            float initial = _simulation.Snapshot.StabilitySeconds;
            _simulation.SetControls(70f, 50f, 50f);
            Tick(10f);
            Assert.IsFalse(_simulation.Snapshot.IsStable);
            Assert.IsFalse(_simulation.Snapshot.IsDangerous);
            _simulation.Start();
            Tick(0.2f);
            Assert.That(_simulation.Snapshot.StabilitySeconds, Is.EqualTo(initial).Within(0.001f));
            Tick(0.8f);
            Assert.That(_simulation.Snapshot.StabilitySeconds, Is.LessThan(initial));
            _simulation.EmergencyStop();
            _simulation.SetControls(0f, 100f, 0f);
            Tick(10f);
            float beforeDanger = _simulation.Snapshot.StabilitySeconds;
            Assert.IsTrue(_simulation.Snapshot.IsDangerous);
            _simulation.Start();
            Tick(0.2f);
            Assert.That(_simulation.Snapshot.StabilitySeconds,
                Is.EqualTo(Mathf.Max(0f, beforeDanger - 0.2f * _config.DangerDecaySecondsPerSecond)).Within(0.001f));
        }

        [Test]
        public void EmergencyStop_PreservesProgressAndAllowsAdjustment()
        {
            SettleSafe();
            _simulation.Start();
            Tick(1f);
            _simulation.EmergencyStop();
            float progress = _simulation.Snapshot.StabilitySeconds;
            _simulation.SetControls(20f, 30f, 40f);
            Tick(1f);
            Assert.IsFalse(_simulation.Snapshot.IsRunning);
            Assert.That(_simulation.Snapshot.StabilitySeconds, Is.EqualTo(progress));
            Assert.That(_simulation.Snapshot.Controls.x, Is.EqualTo(20f));
        }

        [Test]
        public void EveryControl_IsCrossCoupledAcrossAllOutputs()
        {
            var neutral = new Vector3(50f, 50f, 50f);
            var baseline = _config.EvaluateTarget(neutral, RelayAFaultType.None, 0f);
            var settings = new[] { new Vector3(60f, 50f, 50f), new Vector3(50f, 60f, 50f), new Vector3(50f, 50f, 60f) };
            foreach (var controls in settings)
            {
                var outputs = _config.EvaluateTarget(controls, RelayAFaultType.None, 0f);
                Assert.That(outputs.Voltage, Is.Not.EqualTo(baseline.Voltage));
                Assert.That(outputs.Frequency, Is.Not.EqualTo(baseline.Frequency));
                Assert.That(outputs.LoadBalance, Is.Not.EqualTo(baseline.LoadBalance));
            }
        }

        [Test]
        public void Fault_WarnsActivatesChangesOutputsAndRecovers()
        {
            Set("requireFaultRecovery", false);
            Set("enableFault", true);
            Set("earliestFaultAtSeconds", 0.1f);
            Set("faultWarningSeconds", 0.2f);
            Set("faultDurationSeconds", 0.3f);
            Set("stabilityRequiredSeconds", 100f);
            _simulation.Initialize(_config);
            SettleSafe();
            int warnings = 0, faults = 0;
            _simulation.FaultWarningStarted += _ => warnings++;
            _simulation.FaultActivated += _ => faults++;
            _simulation.Start();
            Tick(0.15f);
            Assert.That(warnings, Is.EqualTo(1));
            Assert.That(_simulation.Snapshot.WarningFault, Is.Not.EqualTo(RelayAFaultType.None));
            Tick(0.25f);
            Assert.That(faults, Is.EqualTo(1));
            Assert.That(_simulation.Snapshot.ActiveFault, Is.Not.EqualTo(RelayAFaultType.None));
            var target = _config.EvaluateTarget(_simulation.Snapshot.Controls, RelayAFaultType.None, 0.4f);
            Assert.That(_simulation.Snapshot.TargetOutputs.Voltage, Is.Not.EqualTo(target.Voltage));
            Tick(0.4f);
            Assert.That(_simulation.Snapshot.ActiveFault, Is.EqualTo(RelayAFaultType.None));
            Assert.IsFalse(_simulation.Snapshot.IsOnline);
        }

        [Test]
        public void EveryFault_HasCompensatingControlsWithinBounds()
        {
            foreach (var fault in new[] { RelayAFaultType.Overvoltage, RelayAFaultType.FrequencyDesynchronization, RelayAFaultType.LoadImbalance })
            {
                bool found = false;
                for (int g = 0; g <= 100 && !found; g += 5)
                    for (int f = 0; f <= 100 && !found; f += 5)
                        for (int l = 0; l <= 100 && !found; l += 5)
                            found = _config.IsOutputStable(_config.EvaluateTarget(new Vector3(g, f, l), fault, 0f));
                Assert.IsTrue(found, fault.ToString());
            }
        }

        [Test]
        public void ExpertFaults_CannotBeWaitedOutOrBypassedByStopping()
        {
            Set("enableFault", true);
            Set("faultChance", 0f);
            _simulation.Initialize(_config, true, 123);

            var safeControls =
                FindCompensation(RelayAFaultType.None);

            _simulation.SetControls(
                safeControls.x,
                safeControls.y,
                safeControls.z);

            Tick(10f);

            Assert.That(
                _simulation.Snapshot.IsStable,
                Is.True);

            _simulation.Start();
            Tick(40f);
            Assert.IsFalse(_simulation.Snapshot.IsOnline);
            Assert.That(_simulation.Snapshot.ActiveFault, Is.Not.EqualTo(RelayAFaultType.None));
            Assert.That(_simulation.Snapshot.RecoveredFaults, Is.Zero);
            Assert.That(_simulation.Snapshot.StabilitySeconds, Is.Zero);
            var fault = _simulation.Snapshot.ActiveFault;
            _simulation.EmergencyStop();
            Tick(40f);
            Assert.That(_simulation.Snapshot.ActiveFault, Is.EqualTo(fault));
            _simulation.Start();
            Tick(5f);
            Assert.IsFalse(_simulation.Snapshot.IsOnline);
        }

        [Test]
        public void ExpertAttempt_RequiresOneRecoveryThenFinalHold()
        {
            Set("enableFault", true);
            _simulation.Initialize(_config, true, 123);
            _simulation.Start();
            var seen = new System.Collections.Generic.HashSet<RelayAFaultType>();
            int completed = 0;
            _simulation.Completed += () => completed++;
            for (int tick = 0; tick < 6000 && !_simulation.Snapshot.IsOnline; tick++)
            {
                var fault = _simulation.Snapshot.ActiveFault;
                if (fault != RelayAFaultType.None) seen.Add(fault);
                Vector3 settings = FindCompensation(fault);
                _simulation.SetControls(settings.x, settings.y, settings.z);
                _simulation.Tick(0.05f);
            }
            Assert.That(seen.Count, Is.EqualTo(1));
            Assert.That(_simulation.Snapshot.RecoveredFaults, Is.EqualTo(1));
            Assert.IsTrue(_simulation.Snapshot.IsOnline);
            Assert.That(completed, Is.EqualTo(1));
            _simulation.Initialize(_config, true, 456);
            Assert.That(_simulation.Snapshot.RecoveredFaults, Is.Zero);
        }

        private Vector3 FindCompensation(RelayAFaultType fault)
        {
            // Choose the most centered reachable setting so drift cannot invalidate a boundary solution.
            Vector3 best = Vector3.zero;
            float score = float.PositiveInfinity;
            for (int g = 0; g <= 100; g += 5)
                for (int f = 0; f <= 100; f += 5)
                    for (int l = 0; l <= 100; l += 5)
                    {
                        var candidate = new Vector3(g, f, l);
                        var output = _simulation.EvaluateControlTarget(candidate, fault, 0f);
                        float v = (output.Voltage - 225f) / 5f;
                        float hz = output.Frequency - 50f;
                        float load = (output.LoadBalance - 50f) / 3f;
                        float distance = v * v + hz * hz + load * load;
                        if (distance >= score) continue;
                        score = distance;
                        best = candidate;
                    }
            return best;
        }

        [Test]
        public void SeededAttempt_StartsUnsolvedAndIsReproducible()
        {
            var second = new RelayAStabilizationSimulation();
            _simulation.Initialize(_config, true, 123);
            second.Initialize(_config, true, 123);
            Assert.That(second.Snapshot.Controls.x, Is.EqualTo(_simulation.Snapshot.Controls.x));
            Assert.IsFalse(_simulation.Snapshot.IsStable);
        }

        [Test]
        public void AuthoritativeTelemetry_DoesNotCompleteOrRunClientTimer()
        {
            int completions = 0;
            _simulation.Completed += () => completions++;
            _simulation.ApplyAuthoritative(new Vector3(30f, 40f, 50f), new Vector3(224f, 50f, 49f), true,
                3f, RelayAFaultType.Overvoltage, RelayAFaultType.None, new Vector2(2f, 0f), 2);
            Assert.That(_simulation.Snapshot.StabilitySeconds, Is.EqualTo(3f));
            Assert.That(_simulation.Snapshot.Outputs.Voltage, Is.EqualTo(224f));
            Assert.That(_simulation.Snapshot.WarningFault, Is.EqualTo(RelayAFaultType.Overvoltage));
            Assert.That(completions, Is.Zero);
            Assert.That(_simulation.Snapshot.RecoveredFaults, Is.EqualTo(2));
        }
    }
}
