using NUnit.Framework;
using UnityEngine;
using EchoProtocol.RelayB;

namespace EchoProtocol.RelayB.Tests
{
    [TestFixture]
    public sealed class RelayBSimulationTests
    {
        private RelayBConfig _config;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<RelayBConfig>();
            _config.InitializeDefaultPresetsIfEmpty();
        }

        [TearDown]
        public void TearDown()
        {
            if (_config != null)
            {
                Object.DestroyImmediate(_config);
            }
        }

        [Test]
        public void Presets_AllFourPresetsHaveValidSolvableSolutions()
        {
            Assert.That(_config.Presets.Count, Is.GreaterThanOrEqualTo(4));

            for (int i = 0; i < _config.Presets.Count; i++)
            {
                RelayBPreset preset = _config.Presets[i];
                RelayBSignalSimulation sim = new RelayBSignalSimulation();
                sim.Initialize(_config, i);

                sim.SelectChannel(preset.CorrectChannelIndex);
                sim.SetFrequency(preset.TargetFrequency);
                sim.SetPhase(preset.TargetPhase);

                bool isSync = sim.CheckIsSynchronized(out float freqErr, out float phaseErr);
                Assert.IsTrue(isSync, $"Preset {i} ({preset.PresetName}) failed to synchronize.");

                float match = sim.EvaluateSignalMatch(freqErr, phaseErr);
                Assert.That(match, Is.GreaterThanOrEqualTo(95f), $"Preset {i} match score was {match}%.");
            }
        }

        [Test]
        public void PhaseMath_CyclicAngularDifferenceCalculatesCorrectly()
        {
            // 359° and 1° -> 2°
            float diff1 = RelayBSignalSimulation.CalculatePhaseErrorDegrees(359f, 1f);
            Assert.That(diff1, Is.EqualTo(2f).Within(0.01f));

            // 10° and 350° -> 20°
            float diff2 = RelayBSignalSimulation.CalculatePhaseErrorDegrees(10f, 350f);
            Assert.That(diff2, Is.EqualTo(20f).Within(0.01f));

            // 0° and 360° -> 0°
            float diff3 = RelayBSignalSimulation.CalculatePhaseErrorDegrees(0f, 360f);
            Assert.That(diff3, Is.EqualTo(0f).Within(0.01f));

            // 180° and 0° -> 180°
            float diff4 = RelayBSignalSimulation.CalculatePhaseErrorDegrees(180f, 0f);
            Assert.That(diff4, Is.EqualTo(180f).Within(0.01f));
        }

        [Test]
        public void FrequencyMath_PercentageToleranceAccurate()
        {
            float target = 50f;
            // 51.5 kHz is exactly 3% of 50 kHz
            float err3 = RelayBSignalSimulation.CalculateFrequencyErrorPercent(51.5f, target);
            Assert.That(err3, Is.EqualTo(3f).Within(0.01f));

            // 48.5 kHz is exactly 3% of 50 kHz
            float errMinus3 = RelayBSignalSimulation.CalculateFrequencyErrorPercent(48.5f, target);
            Assert.That(errMinus3, Is.EqualTo(3f).Within(0.01f));

            // 52.0 kHz is 4% (exceeds tolerance)
            float err4 = RelayBSignalSimulation.CalculateFrequencyErrorPercent(52f, target);
            Assert.That(err4, Is.GreaterThan(3f));
        }

        [Test]
        public void WrongChannels_CannotAchieveSyncEvenWithMatchedFreqAndPhase()
        {
            for (int p = 0; p < _config.Presets.Count; p++)
            {
                RelayBPreset preset = _config.Presets[p];
                for (int c = 0; c < 4; c++)
                {
                    if (c == preset.CorrectChannelIndex)
                    {
                        continue;
                    }

                    RelayBSignalSimulation sim = new RelayBSignalSimulation();
                    sim.Initialize(_config, p);

                    sim.SelectChannel(c);
                    sim.SetFrequency(preset.TargetFrequency);
                    sim.SetPhase(preset.TargetPhase);

                    bool isSync = sim.CheckIsSynchronized(out float freqErr, out float phaseErr);
                    Assert.IsFalse(isSync, $"Preset {p} allowed wrong channel {c} to synchronize.");

                    float match = sim.EvaluateSignalMatch(freqErr, phaseErr);
                    Assert.That(match, Is.LessThanOrEqualTo(65f), $"Preset {p} wrong channel {c} achieved {match}% match.");
                }
            }
        }

        [Test]
        public void WaveformMismatch_CannotAchieveSyncOrCorrelation()
        {
            RelayBPreset preset = _config.Presets[0];
            RelayBSignalSimulation sim = new RelayBSignalSimulation();
            sim.Initialize(_config, 0);

            // Channel 0 has Triangle waveform, preset 0 expects Sine
            sim.SelectChannel(0);
            sim.SetFrequency(preset.TargetFrequency);
            sim.SetPhase(preset.TargetPhase);

            bool isSync = sim.CheckIsSynchronized(out float freqErr, out float phaseErr);
            Assert.IsFalse(isSync, "Waveform mismatch was accepted as synchronized.");

            float match = sim.EvaluateSignalMatch(freqErr, phaseErr);
            Assert.That(match, Is.LessThanOrEqualTo(65f));
        }

        [Test]
        public void HoldTimer_ProgressesOnlyWhenSynchronized()
        {
            RelayBSignalSimulation sim = new RelayBSignalSimulation();
            sim.Initialize(_config, 0);

            // Select correct channel but wrong frequency
            sim.SelectChannel(1);
            sim.SetFrequency(15f);
            sim.StartSynchronization();

            sim.Tick(1f);
            Assert.That(sim.Snapshot.SyncProgressSeconds, Is.EqualTo(0f));

            // Now set correct frequency and phase
            sim.SetFrequency(42f);
            sim.SetPhase(90f);
            sim.Tick(1f);

            Assert.That(sim.Snapshot.SyncProgressSeconds, Is.GreaterThan(0.9f));
        }

        [Test]
        public void ProgressBar_FillAmountStrictlyMatchesRatio()
        {
            RelayBSignalSimulation sim = new RelayBSignalSimulation();
            sim.Initialize(_config, 0);

            Assert.That(sim.Snapshot.Progress01, Is.EqualTo(0f));

            sim.SelectChannel(1);
            sim.SetFrequency(42f);
            sim.SetPhase(90f);
            sim.StartSynchronization();

            Assert.That(sim.Snapshot.Progress01, Is.EqualTo(0f));

            // 2s / 8s = 0.25
            for (int i = 0; i < 20; i++)
            {
                if (sim.Snapshot.IsDriftActive) sim.SetPhase(90f + _config.DriftPhaseOffset);
                sim.Tick(0.1f);
            }
            Assert.That(sim.Snapshot.Progress01, Is.EqualTo(0.25f).Within(0.02f));

            // 4s / 8s = 0.50
            for (int i = 0; i < 20; i++)
            {
                if (sim.Snapshot.IsDriftActive) sim.SetPhase(90f + _config.DriftPhaseOffset);
                sim.Tick(0.1f);
            }
            Assert.That(sim.Snapshot.Progress01, Is.EqualTo(0.50f).Within(0.02f));

            // 6s / 8s = 0.75
            for (int i = 0; i < 20; i++)
            {
                if (sim.Snapshot.IsDriftActive) sim.SetPhase(90f + _config.DriftPhaseOffset);
                sim.Tick(0.1f);
            }
            Assert.That(sim.Snapshot.Progress01, Is.EqualTo(0.75f).Within(0.02f));

            // Cancel
            sim.CancelSynchronization();
            Assert.That(sim.Snapshot.Progress01, Is.EqualTo(0f));
        }

        [Test]
        public void InstabilityGrace_RecoversWithinTolerance()
        {
            RelayBSignalSimulation sim = new RelayBSignalSimulation();
            sim.Initialize(_config, 0);

            sim.SelectChannel(1);
            sim.SetFrequency(42f);
            sim.SetPhase(90f);
            sim.StartSynchronization();

            for (int i = 0; i < 20; i++) sim.Tick(0.1f);
            float progressBefore = sim.Snapshot.SyncProgressSeconds;
            Assert.That(progressBefore, Is.GreaterThan(1.9f));

            // Deviate for 0.3s (within 0.5s grace)
            sim.SetFrequency(20f);
            sim.Tick(0.3f);
            Assert.That(sim.Snapshot.SyncProgressSeconds, Is.GreaterThan(0f));

            // Recover
            sim.SetFrequency(42f);
            sim.Tick(0.2f);
            Assert.That(sim.Snapshot.SyncProgressSeconds, Is.GreaterThan(progressBefore));
        }

        [Test]
        public void InstabilityGrace_ExceedingGracePeriodResets()
        {
            RelayBSignalSimulation sim = new RelayBSignalSimulation();
            sim.Initialize(_config, 0);

            sim.SelectChannel(1);
            sim.SetFrequency(42f);
            sim.SetPhase(90f);
            sim.StartSynchronization();

            for (int i = 0; i < 20; i++) sim.Tick(0.1f);

            // Deviate for 0.6s (exceeds 0.5s grace)
            sim.SetFrequency(20f);
            sim.Tick(0.6f);

            Assert.That(sim.Snapshot.SyncProgressSeconds, Is.EqualTo(0f));
            Assert.That(sim.Snapshot.Progress01, Is.EqualTo(0f));
        }

        [Test]
        public void SignalDrift_FiresWarningThreeSecondsPrior()
        {
            RelayBSignalSimulation sim = new RelayBSignalSimulation();
            sim.Initialize(_config, 0);

            bool warningFired = false;
            sim.DriftWarning += () => warningFired = true;

            sim.SelectChannel(1);
            sim.SetFrequency(42f);
            sim.SetPhase(90f);
            sim.StartSynchronization();

            // Run for 0.6s (3.5s - 3.0s = 0.5s trigger)
            for (int i = 0; i < 6; i++) sim.Tick(0.1f);

            Assert.IsTrue(warningFired);
            Assert.IsTrue(sim.Snapshot.IsDriftWarning);
        }

        [Test]
        public void SignalDrift_ActivationShiftsEffectiveTarget()
        {
            RelayBSignalSimulation sim = new RelayBSignalSimulation();
            sim.Initialize(_config, 0);

            bool driftFired = false;
            sim.DriftTriggered += () => driftFired = true;

            sim.SelectChannel(1);
            sim.SetFrequency(42f);
            sim.SetPhase(90f);
            sim.StartSynchronization();

            // Run past 3.5s
            for (int i = 0; i < 36; i++) sim.Tick(0.1f);

            Assert.IsTrue(driftFired);
            Assert.IsTrue(sim.Snapshot.IsDriftActive);
            Assert.That(sim.Snapshot.TargetPhase, Is.EqualTo(90f + _config.DriftPhaseOffset).Within(0.01f));
        }

        [Test]
        public void SignalDrift_OccursOnlyOncePerSession()
        {
            RelayBSignalSimulation sim = new RelayBSignalSimulation();
            sim.Initialize(_config, 0);

            int count = 0;
            sim.DriftTriggered += () => count++;

            sim.SelectChannel(1);
            sim.SetFrequency(42f);
            sim.SetPhase(90f);
            sim.StartSynchronization();

            // Trigger drift at 3.5s
            for (int i = 0; i < 36; i++) sim.Tick(0.1f);
            Assert.That(count, Is.EqualTo(1));

            // Cause a reset
            sim.Tick(0.6f);
            Assert.That(sim.Snapshot.SyncProgressSeconds, Is.EqualTo(0f));

            // Re-sync to shifted target
            sim.SetPhase(90f + _config.DriftPhaseOffset);
            for (int i = 0; i < 40; i++) sim.Tick(0.1f);

            Assert.That(count, Is.EqualTo(1), "Drift should only occur once per solve session.");
        }

        [Test]
        public void ChannelSwitch_CancelsOngoingSyncAndResetsTimer()
        {
            RelayBSignalSimulation sim = new RelayBSignalSimulation();
            sim.Initialize(_config, 0);

            sim.SelectChannel(1);
            sim.SetFrequency(42f);
            sim.SetPhase(90f);
            sim.StartSynchronization();

            for (int i = 0; i < 20; i++) sim.Tick(0.1f);
            Assert.That(sim.Snapshot.SyncProgressSeconds, Is.GreaterThan(1.9f));

            // Switch to Channel 0
            sim.SelectChannel(0);

            Assert.That(sim.Snapshot.Status, Is.Not.EqualTo(RelayBStatus.Synchronizing));
            Assert.That(sim.Snapshot.SyncProgressSeconds, Is.EqualTo(0f));
        }

        [Test]
        public void FullRun_ReachesRequiredSecondsAndLocksOnline()
        {
            RelayBSignalSimulation sim = new RelayBSignalSimulation();
            sim.Initialize(_config, 0);

            bool completed = false;
            sim.Completed += () => completed = true;

            sim.SelectChannel(1);
            sim.SetFrequency(42f);
            sim.SetPhase(90f);
            sim.StartSynchronization();

            for (int i = 0; i < 150 && !sim.Snapshot.IsOnline; i++)
            {
                if (sim.Snapshot.IsDriftActive)
                {
                    sim.SetPhase(90f + _config.DriftPhaseOffset);
                }

                sim.Tick(0.1f);
            }

            Assert.IsTrue(sim.Snapshot.IsOnline, "Relay B should reach Online status.");
            Assert.IsTrue(completed, "Completed event should have fired.");
            Assert.That(sim.Snapshot.SyncProgressSeconds, Is.EqualTo(_config.HoldRequiredSeconds).Within(0.01f));
        }

        [Test]
        public void OnlineState_LocksInteractiveControls()
        {
            RelayBSignalSimulation sim = new RelayBSignalSimulation();
            sim.Initialize(_config, 0);

            sim.ForceCompleteForAuthoritativeSync();
            Assert.IsTrue(sim.Snapshot.IsOnline);

            int chBefore = sim.SelectedChannelIndex;
            float freqBefore = sim.CurrentFrequency;
            float phaseBefore = sim.CurrentPhase;

            sim.SelectChannel(3);
            sim.SetFrequency(12.5f);
            sim.SetPhase(333f);
            sim.StartSynchronization();

            Assert.That(sim.SelectedChannelIndex, Is.EqualTo(chBefore));
            Assert.That(sim.CurrentFrequency, Is.EqualTo(freqBefore));
            Assert.That(sim.CurrentPhase, Is.EqualTo(phaseBefore));
        }
    }
}
