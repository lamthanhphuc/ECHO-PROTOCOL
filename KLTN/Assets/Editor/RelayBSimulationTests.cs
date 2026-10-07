using NUnit.Framework;
using UnityEngine;
using UnityEditor;
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
        public void SeededExpertSignal_RejectsDecoysAndRecoversBothDriftAxes()
        {
            bool frequencyDrift = false, phaseDrift = false;
            var waves = new System.Collections.Generic.HashSet<WaveformType>();
            for (int seed = 1; seed <= 40; seed++)
            {
                var sim = new RelayBSignalSimulation();
                sim.Initialize(_config, 2, true, seed);
                var preset = sim.GetCurrentPreset();
                waves.Add(preset.ReferenceWaveform);
                sim.ScanSpectrum();
                foreach (var candidate in preset.Candidates)
                {
                    if (candidate.ChannelIndex == preset.CorrectChannelIndex) continue;
                    sim.SelectChannel(candidate.ChannelIndex);
                    Assert.That(sim.SelectedChannelIndex, Is.EqualTo(-1));
                }
                sim.SelectChannel(preset.CorrectChannelIndex);
                SolveDecoder(sim);
                sim.SetFrequency(preset.TargetFrequency);
                sim.SetPhase(preset.TargetPhase);
                sim.StartSynchronization();
                for (int tick = 0; tick < 400 && !sim.IsOnline; tick++)
                {
                    sim.Tick(0.05f);
                    if (!sim.Snapshot.IsDriftActive) continue;
                    frequencyDrift |= Mathf.Abs(sim.GetEffectiveTargetFrequency() - preset.TargetFrequency) > 0.01f;
                    phaseDrift |= Mathf.Abs(Mathf.DeltaAngle(sim.GetEffectiveTargetPhase(), preset.TargetPhase)) > 0.01f;
                    sim.SetFrequency(sim.GetEffectiveTargetFrequency());
                    sim.SetPhase(sim.GetEffectiveTargetPhase());
                }
                Assert.IsTrue(sim.IsOnline, "Seed " + seed);
                Assert.That(sim.FrequencyTolerancePercent, Is.LessThan(_config.FrequencyTolerancePercent));
            }
            Assert.IsTrue(frequencyDrift && phaseDrift);
            Assert.That(waves.Count, Is.EqualTo(5));
        }

        private void PrepareStage3(RelayBSignalSimulation sim)
        {
            sim.ScanSpectrum();
            sim.SelectChannel(_config.Presets[0].CorrectChannelIndex);
            sim.SetPipelineSlot(0, RelayBModuleType.NoiseSuppressor);
            Assert.IsTrue(sim.AnalyzeOutput().IsValid);
            SolveDecoder(sim);
        }

        private static void SolveDecoder(RelayBSignalSimulation sim)
        {
            int secret = (int)typeof(RelayBDecoder).GetField("_secret", System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic).GetValue(sim.Decoder);
            Assert.IsTrue(sim.Decoder.Transmit(secret));
            sim.Tick(3.4f);
            Assert.IsTrue(sim.Decoder.Snapshot.IsComplete);
        }

        [Test]
        public void FindSignal_RequiresScanAndRejectsWrongCandidate()
        {
            var sim = new RelayBSignalSimulation();
            sim.Initialize(_config, 0);
            sim.SelectChannel(_config.Presets[0].CorrectChannelIndex);
            Assert.That(sim.Snapshot.SelectedChannelIndex, Is.EqualTo(-1));
            sim.SetActiveTab(1);
            Assert.That(sim.Snapshot.ActiveTab, Is.EqualTo(0));

            sim.ScanSpectrum();
            sim.SelectChannel(0);
            Assert.That(sim.Snapshot.SelectedChannelIndex, Is.EqualTo(-1));
            sim.SetActiveTab(1);
            Assert.That(sim.Snapshot.ActiveTab, Is.EqualTo(0));
        }

        [Test]
        public void FindSignal_RoutingCorrectCandidateKeepsFindTabUntilPlayerAdvances()
        {
            var sim = new RelayBSignalSimulation();
            sim.Initialize(_config, 0);
            sim.ScanSpectrum();
            sim.SelectChannel(_config.Presets[0].CorrectChannelIndex);

            Assert.That(sim.Snapshot.SelectedChannelIndex, Is.EqualTo(_config.Presets[0].CorrectChannelIndex));
            Assert.That(sim.Snapshot.ActiveTab, Is.EqualTo(0));

            sim.SetActiveTab(1);
            Assert.That(sim.Snapshot.ActiveTab, Is.EqualTo(1));
        }

        [Test]
        public void FindSignal_NewAttemptShufflesCluesAndStillHasOneAnswer()
        {
            var signatures = new System.Collections.Generic.HashSet<string>();
            var channels = new System.Collections.Generic.HashSet<int>();
            for (int seed = 1; seed <= 12; seed++)
            {
                var sim = new RelayBSignalSimulation();
                sim.Initialize(_config, 0, true, seed);
                var preset = sim.GetCurrentPreset();
                int matches = 0;
                for (int i = 0; i < preset.Candidates.Length; i++)
                {
                    var candidate = preset.Candidates[i];
                    if (candidate.Peaks[0] >= preset.ReferenceProfile.FundamentalMinKhz
                        && candidate.Peaks[0] <= preset.ReferenceProfile.FundamentalMaxKhz
                        && candidate.Waveform == preset.ReferenceWaveform
                        && candidate.PilotFrame == preset.ReferenceProfile.ExpectedPilot
                        && RelayBSignalSimulation.HasMatchingHarmonic(candidate, preset.ReferenceProfile)) matches++;
                }
                Assert.That(matches, Is.EqualTo(1));
                signatures.Add($"{preset.TargetFrequency:0.0}:{preset.ReferenceProfile.ExpectedPilot}:{preset.CorrectChannelIndex}");
                channels.Add(preset.CorrectChannelIndex);
            }
            Assert.That(signatures.Count, Is.GreaterThan(2));
            Assert.That(channels.Count, Is.GreaterThan(1));
        }

        [Test]
        public void CleanSignal_FailedAttemptChangesProblemsButPreservesFind()
        {
            foreach (int presetIndex in new[] { 0, 2 })
            {
                var sim = new RelayBSignalSimulation();
                sim.Initialize(_config, presetIndex, true, 123);
                sim.ScanSpectrum();
                sim.SelectChannel(sim.GetCurrentPreset().CorrectChannelIndex);
                int selected = sim.SelectedChannelIndex;
                int oldProblems = sim.Snapshot.CleanProblems;
                sim.SetPipelineSlot(0, RelayBModuleType.Gain);
                Assert.IsFalse(sim.AnalyzeOutput().IsValid);

                sim.RerollCleanScenario(457);

                Assert.That(sim.SelectedChannelIndex, Is.EqualTo(selected));
                Assert.That(sim.Snapshot.HasScanned, Is.True);
                Assert.That(sim.Snapshot.ActiveTab, Is.EqualTo(1));
                Assert.That(sim.Snapshot.CleanProblems, Is.Not.EqualTo(oldProblems));
                Assert.That(sim.Snapshot.PipelineModules[0], Is.EqualTo(RelayBModuleType.None));
                Assert.That(sim.Snapshot.PipelineModules[1], Is.EqualTo(RelayBModuleType.None));
                Assert.That(sim.Snapshot.OutputDiagnostic.Summary, Is.Null.Or.Empty);
            }
        }

        [Test]
        public void CleanSignal_RerolledConditionsRemainSolvable()
        {
            foreach (int presetIndex in new[] { 0, 2 })
            {
                var sim = new RelayBSignalSimulation();
                sim.Initialize(_config, presetIndex, true, 123);
                sim.ScanSpectrum();
                sim.SelectChannel(sim.GetCurrentPreset().CorrectChannelIndex);
                var seen = new System.Collections.Generic.HashSet<int>();
                for (int seed = 100; seed < 112; seed++)
                {
                    sim.RerollCleanScenario(seed);
                    int problems = sim.Snapshot.CleanProblems;
                    seen.Add(problems);
                    RelayBModuleType first = (problems & 1) != 0 ? RelayBModuleType.NoiseSuppressor
                        : (problems & 2) != 0 ? RelayBModuleType.Notch : RelayBModuleType.Gain;
                    RelayBModuleType second = (problems & 4) != 0 && first != RelayBModuleType.Gain
                        ? RelayBModuleType.Gain : (problems & 2) != 0 && first != RelayBModuleType.Notch
                        ? RelayBModuleType.Notch : RelayBModuleType.None;
                    sim.SetPipelineSlot(0, first);
                    sim.SetPipelineSlot(1, second);
                    Assert.IsTrue(sim.AnalyzeOutput().IsValid, $"Preset {presetIndex}, problems {problems}");
                }
                Assert.That(seen.Count, Is.EqualTo(3));
            }
        }

        [Test]
        public void CleanSignal_LatestSeedIsEnoughToReconstructCondition()
        {
            var host = new RelayBSignalSimulation();
            var client = new RelayBSignalSimulation();
            host.Initialize(_config, 2, true, 123);
            client.Initialize(_config, 2, true, 123);
            host.ScanSpectrum();
            client.ScanSpectrum();
            int channel = host.GetCurrentPreset().CorrectChannelIndex;
            host.SelectChannel(channel);
            client.SelectChannel(channel);
            host.RerollCleanScenario(101);
            host.RerollCleanScenario(102);
            client.RerollCleanScenario(102);

            Assert.That(client.Snapshot.CleanProblems, Is.EqualTo(host.Snapshot.CleanProblems));
        }

        [Test]
        public void FindSignal_EachPresetHasExactlyOneThreeClueMatch()
        {
            foreach (var preset in _config.Presets)
            {
                int matches = 0;
                foreach (var candidate in preset.Candidates)
                {
                    if (candidate.Peaks.Length > 0
                        && candidate.Peaks[0] >= preset.ReferenceProfile.FundamentalMinKhz
                        && candidate.Peaks[0] <= preset.ReferenceProfile.FundamentalMaxKhz
                        && candidate.Waveform == preset.ReferenceWaveform
                        && candidate.PilotFrame == preset.ReferenceProfile.ExpectedPilot) matches++;
                }
                Assert.That(matches, Is.EqualTo(1), preset.PresetName);
            }
        }

        [Test]
        public void SerializedPresetAsset_HasOneAnswerAndExpectedCleanDifficulty()
        {
            var asset = AssetDatabase.LoadAssetAtPath<RelayBConfig>(
                "Assets/ScriptableObjects/RelayB/RelayB_Hard_Config.asset");
            Assert.IsNotNull(asset);
            Assert.IsTrue(asset.AreAllPresetsSolvable());
            Assert.That(asset.Presets.Count, Is.EqualTo(4));

            for (int i = 0; i < asset.Presets.Count; i++)
            {
                var preset = asset.Presets[i];
                var candidate = preset.GetCandidate(preset.CorrectChannelIndex);
                int problems = 1 + (candidate.HasSpur ? 1 : 0)
                    + (candidate.DistortionPercent > 15f ? 1 : 0);
                Assert.That(problems, Is.EqualTo(i < 2 ? 1 : 2), preset.PresetName);
                Assert.That(preset.ReferenceProfile.TargetFundamentalKhz - preset.ReferenceProfile.FundamentalMinKhz,
                    Is.EqualTo(preset.ReferenceProfile.FundamentalMaxKhz
                        - preset.ReferenceProfile.TargetFundamentalKhz).Within(0.01f));
            }
        }

        [Test]
        public void CleanSignal_RequiresCorrectToolsAndOrder()
        {
            for (int presetIndex = 0; presetIndex < _config.Presets.Count; presetIndex++)
            {
                var preset = _config.Presets[presetIndex];
                var signal = preset.GetCandidate(preset.CorrectChannelIndex);
                var sim = new RelayBSignalSimulation();
                sim.Initialize(_config, presetIndex);
                sim.ScanSpectrum();
                sim.SelectChannel(preset.CorrectChannelIndex);
                sim.SetFrequency(preset.TargetFrequency);
                sim.SetPhase(preset.TargetPhase);

                sim.StartSynchronization();
                Assert.That(sim.Snapshot.Status, Is.Not.EqualTo(RelayBStatus.Synchronizing));
                sim.SetActiveTab(2);
                Assert.That(sim.Snapshot.ActiveTab, Is.EqualTo(1));

                sim.SetPipelineSlot(0, RelayBModuleType.NoiseSuppressor);
                if (signal.HasSpur || signal.DistortionPercent > 15f)
                {
                    var second = signal.HasSpur ? RelayBModuleType.Notch : RelayBModuleType.Gain;
                    sim.SetPipelineSlot(1, RelayBModuleType.None);
                    Assert.IsFalse(sim.AnalyzeOutput().IsValid);
                    sim.SetPipelineSlot(0, second);
                    sim.SetPipelineSlot(1, RelayBModuleType.NoiseSuppressor);
                    if (second == RelayBModuleType.Gain) Assert.IsFalse(sim.AnalyzeOutput().IsValid);
                    sim.SetPipelineSlot(0, RelayBModuleType.NoiseSuppressor);
                    sim.SetPipelineSlot(1, second);
                }
                Assert.IsTrue(sim.AnalyzeOutput().IsValid, $"Preset {presetIndex} should clean with required tools.");
                sim.SetActiveTab(2);
                Assert.That(sim.Snapshot.ActiveTab, Is.EqualTo(1), "Legacy DSP must not bypass decoding.");
                SolveDecoder(sim);
                sim.SetActiveTab(2);
                Assert.That(sim.Snapshot.ActiveTab, Is.EqualTo(2));
                sim.StartSynchronization();
                Assert.That(sim.Snapshot.Status, Is.EqualTo(RelayBStatus.Synchronizing));
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

                sim.ScanSpectrum();
                sim.SelectChannel(preset.CorrectChannelIndex);
                sim.SetPipelineSlot(0, RelayBModuleType.NoiseSuppressor);
                RelayBCandidate candidate = preset.GetCandidate(preset.CorrectChannelIndex);
                if (candidate.HasSpur) sim.SetPipelineSlot(1, RelayBModuleType.Notch);
                else if (candidate.DistortionPercent > 15f) sim.SetPipelineSlot(1, RelayBModuleType.Gain);
                Assert.IsTrue(sim.AnalyzeOutput().IsValid);
                SolveDecoder(sim);
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

                    sim.ScanSpectrum();
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

            // Channel 3 has Triangle waveform, preset 0 expects Sine
            sim.ScanSpectrum();
            sim.SelectChannel(3);
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
            PrepareStage3(sim);
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

            PrepareStage3(sim);
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

            PrepareStage3(sim);
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
        public void InstabilityGrace_ExceedingGracePeriodDecaysProgress()
        {
            RelayBSignalSimulation sim = new RelayBSignalSimulation();
            sim.Initialize(_config, 0);

            PrepareStage3(sim);
            sim.SetFrequency(42f);
            sim.SetPhase(90f);
            sim.StartSynchronization();

            for (int i = 0; i < 20; i++) sim.Tick(0.1f);

            // Deviate for 0.6s (exceeds 0.5s grace)
            sim.SetFrequency(20f);
            sim.Tick(0.6f);

            Assert.That(sim.Snapshot.SyncProgressSeconds, Is.GreaterThan(0f).And.LessThan(2f));
        }

        [Test]
        public void SignalDrift_FiresWarningThreeSecondsPrior()
        {
            RelayBSignalSimulation sim = new RelayBSignalSimulation();
            sim.Initialize(_config, 0);

            bool warningFired = false;
            sim.DriftWarning += () => warningFired = true;

            PrepareStage3(sim);
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

            PrepareStage3(sim);
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

            PrepareStage3(sim);
            sim.SetFrequency(42f);
            sim.SetPhase(90f);
            sim.StartSynchronization();

            // Trigger drift at 3.5s
            for (int i = 0; i < 36; i++) sim.Tick(0.1f);
            Assert.That(count, Is.EqualTo(1));

            // Progress decays after the grace window.
            sim.Tick(0.6f);
            Assert.That(sim.Snapshot.SyncProgressSeconds, Is.GreaterThan(0f));

            // Re-sync to shifted target
            sim.SetPhase(90f + _config.DriftPhaseOffset);
            for (int i = 0; i < 40; i++) sim.Tick(0.1f);

            Assert.That(count, Is.EqualTo(1), "Drift should only occur once per solve session.");
        }

        [Test]
        public void WrongChannelSelection_DoesNotInterruptActiveSync()
        {
            RelayBSignalSimulation sim = new RelayBSignalSimulation();
            sim.Initialize(_config, 0);

            PrepareStage3(sim);
            sim.SetFrequency(42f);
            sim.SetPhase(90f);
            sim.StartSynchronization();

            for (int i = 0; i < 20; i++) sim.Tick(0.1f);
            Assert.That(sim.Snapshot.SyncProgressSeconds, Is.GreaterThan(1.9f));

            float progress = sim.Snapshot.SyncProgressSeconds;
            sim.SelectChannel(0);

            Assert.That(sim.Snapshot.SelectedChannelIndex, Is.EqualTo(_config.Presets[0].CorrectChannelIndex));
            Assert.That(sim.Snapshot.SyncProgressSeconds, Is.EqualTo(progress));
        }

        [Test]
        public void FullRun_ReachesRequiredSecondsAndLocksOnline()
        {
            RelayBSignalSimulation sim = new RelayBSignalSimulation();
            sim.Initialize(_config, 0);

            bool completed = false;
            sim.Completed += () => completed = true;

            PrepareStage3(sim);
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
