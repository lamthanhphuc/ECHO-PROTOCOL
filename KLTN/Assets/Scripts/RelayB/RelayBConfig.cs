using System;
using System.Collections.Generic;
using UnityEngine;

namespace EchoProtocol.RelayB
{
    [CreateAssetMenu(menuName = "ECHO Protocol/Relay B/Signal Synchronization Config", fileName = "RelayBConfig")]
    public sealed class RelayBConfig : ScriptableObject
    {
        [Header("Tolerances (HARD Difficulty)")]
        [SerializeField, Range(0.5f, 10f)] private float frequencyTolerancePercent = 3f;
        [SerializeField, Range(1f, 30f)] private float phaseToleranceDegrees = 12f;
        [SerializeField, Min(1f)] private float holdRequiredSeconds = 8f;
        [SerializeField, Min(0f)] private float instabilityGraceSeconds = 0.5f;
        [SerializeField, Min(0f)] private float instabilityDecaySecondsPerSecond = 2f;

        [Header("Control Ranges")]
        [SerializeField, Min(1f)] private float minFrequency = 10f;
        [SerializeField, Min(10f)] private float maxFrequency = 100f;

        [Header("Ionospheric Drift (HARD Difficulty)")]
        [SerializeField] private bool enableDrift = true;
        [SerializeField, Min(0.5f)] private float driftWarningSeconds = 3f;
        [SerializeField, Min(1f)] private float driftTriggerHoldSeconds = 3.5f;
        [SerializeField] private float driftPhaseOffset = 30f;
        [SerializeField] private float driftFrequencyOffsetPercent = 1.2f;

        [Header("Presets")]
        [SerializeField] private RelayBPreset[] presets;

        public float FrequencyTolerancePercent => frequencyTolerancePercent;
        public float PhaseToleranceDegrees => phaseToleranceDegrees;
        public float HoldRequiredSeconds => holdRequiredSeconds;
        public float InstabilityGraceSeconds => instabilityGraceSeconds;
        public float InstabilityDecaySecondsPerSecond => instabilityDecaySecondsPerSecond;
        public float MinFrequency => minFrequency;
        public float MaxFrequency => maxFrequency;
        public bool EnableDrift => enableDrift;
        public float DriftWarningSeconds => driftWarningSeconds;
        public float DriftTriggerHoldSeconds => driftTriggerHoldSeconds;
        public float DriftPhaseOffset => driftPhaseOffset;
        public float DriftFrequencyOffsetPercent => driftFrequencyOffsetPercent;
        public IReadOnlyList<RelayBPreset> Presets => presets;

        public RelayBPreset GetPreset(int index)
        {
            InitializeDefaultPresetsIfEmpty();
            if (presets == null || presets.Length == 0)
            {
                return CreateDefaultPresets()[0];
            }

            int clamped = Mathf.Clamp(index, 0, presets.Length - 1);
            return presets[clamped];
        }

        public void InitializeDefaultPresetsIfEmpty()
        {
            if (presets != null && presets.Length >= 4 && presets[0].Candidates != null && presets[0].Candidates.Length > 0)
            {
                return;
            }

            presets = CreateDefaultPresets();
        }

        public bool AreAllPresetsSolvable()
        {
            InitializeDefaultPresetsIfEmpty();
            if (presets == null || presets.Length == 0) return false;

            for (int i = 0; i < presets.Length; i++)
            {
                RelayBPreset p = presets[i];
                if (p == null) return false;

                // 1. Correct channel index valid
                if (p.CorrectChannelIndex < 0 || p.CorrectChannelIndex >= p.Channels.Length) return false;

                // 2. Candidate for correct channel meets technical reference profile
                RelayBCandidate correctCand = p.GetCandidate(p.CorrectChannelIndex);
                if (correctCand == null) return false;
                if (correctCand.Peaks.Length == 0 || correctCand.Peaks[0] < p.ReferenceProfile.FundamentalMinKhz
                    || correctCand.Peaks[0] > p.ReferenceProfile.FundamentalMaxKhz) return false;
                if (correctCand.PilotFrame != p.ReferenceProfile.ExpectedPilot) return false;
                if (correctCand.Waveform != p.ReferenceWaveform) return false;

                int matchingCandidates = 0;
                for (int c = 0; c < p.Candidates.Length; c++)
                {
                    RelayBCandidate dist = p.Candidates[c];
                    if (dist != null && dist.Peaks.Length > 0
                        && dist.Peaks[0] >= p.ReferenceProfile.FundamentalMinKhz
                        && dist.Peaks[0] <= p.ReferenceProfile.FundamentalMaxKhz
                        && dist.PilotFrame == p.ReferenceProfile.ExpectedPilot
                        && dist.Waveform == p.ReferenceWaveform) matchingCandidates++;
                }
                if (matchingCandidates != 1) return false;
            }

            return true;
        }

        private static RelayBPreset[] CreateDefaultPresets()
        {
            return new[]
            {
                // Preset 0: Carrier Alpha (Echo-01) - 42 kHz target, SINE, pilot 11001010
                new RelayBPreset(
                    "Carrier Alpha (Echo-01)",
                    2, // Channel 03 is correct
                    WaveformType.Sine,
                    42.0f,
                    90.0f,
                    new RelayBChannelDef[]
                    {
                        new RelayBChannelDef("CHANNEL 01", WaveformType.Sine, 0.05f, 1f),
                        new RelayBChannelDef("CHANNEL 02", WaveformType.Sine, 0.045f, 1f),
                        new RelayBChannelDef("CHANNEL 03", WaveformType.Sine, 0.045f, 1f),
                        new RelayBChannelDef("CHANNEL 04", WaveformType.Triangle, 0.06f, 1f)
                    },
                    new RelayBReferenceProfile(39f, 45f, 42.0f, 90.0f, true, 10f, 8f, 10f, "11001010"),
                    new RelayBCandidate[]
                    {
                        new RelayBCandidate(0, new[] { 46f, 92f }, 5.5f, 9.0f, WaveformType.Sine, "11001010", false, 0f, "Frequency outside range"),
                        new RelayBCandidate(1, new[] { 42f, 84f }, 4.5f, 9.0f, WaveformType.Sine, "01010101", false, 0f, "Pilot mismatch (01010101)"),
                        new RelayBCandidate(2, new[] { 42f, 84f }, 4.5f, 9.0f, WaveformType.Sine, "11001010", false, 0f, "Nominal carrier lock"),
                        new RelayBCandidate(3, new[] { 41f, 82f }, 6.0f, 9.0f, WaveformType.Triangle, "11001010", false, 0f, "Waveform mismatch (triangle wave)")
                    }),

                // Preset 1: Pulse Beta (Echo-02) - 68 kHz target, SINE, pilot 10110100
                new RelayBPreset(
                    "Pulse Beta (Echo-02)",
                    2, // Channel 03 is correct
                    WaveformType.Sine,
                    68.0f,
                    240.0f,
                    new RelayBChannelDef[]
                    {
                        new RelayBChannelDef("CHANNEL 01", WaveformType.Sine, 0.164f, 1f),
                        new RelayBChannelDef("CHANNEL 02", WaveformType.Sine, 0.058f, 1f),
                        new RelayBChannelDef("CHANNEL 03", WaveformType.Sine, 0.068f, 1f),
                        new RelayBChannelDef("CHANNEL 04", WaveformType.Triangle, 0.182f, 0.985f)
                    },
                    new RelayBReferenceProfile(65f, 71f, 68.0f, 240.0f, true, 10f, 8f, 10f, "10110100"),
                    new RelayBCandidate[]
                    {
                        new RelayBCandidate(0, new[] { 73f, 146f }, 6.4f, 9.1f, WaveformType.Sine, "10110100", false, 0f, "Frequency outside range"),
                        new RelayBCandidate(1, new[] { 68f, 136f }, 5.8f, 9.0f, WaveformType.Sine, "01010101", false, 0f, "Pilot pattern mismatch"),
                        new RelayBCandidate(2, new[] { 68f, 136f }, 6.8f, 9.0f, WaveformType.Sine, "10110100", false, 0f, "Nominal harmonic spacing"),
                        new RelayBCandidate(3, new[] { 68f, 136f }, 6.2f, 9.0f, WaveformType.Triangle, "10110100", false, 0f, "Waveform mismatch (triangle wave)")
                    }),

                // Preset 2: Sub-Harmonic Gamma (Echo-03) - 28.5 kHz target, COMPOSITE, pilot 10011101
                new RelayBPreset(
                    "Sub-Harmonic Gamma (Echo-03)",
                    0, // Channel 01 is correct
                    WaveformType.CompositeHarmonic,
                    28.5f,
                    180.0f,
                    new RelayBChannelDef[]
                    {
                        new RelayBChannelDef("CHANNEL 01", WaveformType.CompositeHarmonic, 0.042f, 1f),
                        new RelayBChannelDef("CHANNEL 02", WaveformType.CompositeHarmonic, 0.26f, 1.01f),
                        new RelayBChannelDef("CHANNEL 03", WaveformType.Triangle, 0.06f, 0.98f),
                        new RelayBChannelDef("CHANNEL 04", WaveformType.Square, 0.04f, 0.74f)
                    },
                    new RelayBReferenceProfile(25f, 32f, 28.5f, 180.0f, true, 10f, 8.5f, 10.5f, "10011101"),
                    new RelayBCandidate[]
                    {
                        new RelayBCandidate(0, new[] { 28.5f, 57f }, 4.2f, 9.5f, WaveformType.CompositeHarmonic, "10011101", true, 0f, "Interference detected"),
                        new RelayBCandidate(1, new[] { 28.5f, 57f }, 5.0f, 9.5f, WaveformType.CompositeHarmonic, "10011111", false, 0f, "Pilot pattern mismatch (last 2 bits)"),
                        new RelayBCandidate(2, new[] { 28.5f, 57f }, 6.0f, 9.5f, WaveformType.Triangle, "10011101", false, 0f, "Waveform mismatch (triangle wave)"),
                        new RelayBCandidate(3, new[] { 34f, 68f }, 4.0f, 9.5f, WaveformType.CompositeHarmonic, "10011101", false, 0f, "Frequency outside range")
                    }),

                // Preset 3: Flux Delta (Echo-04) - 85 kHz target, SQUARE, pilot 11100100
                new RelayBPreset(
                    "Flux Delta (Echo-04)",
                    3, // Channel 04 is correct
                    WaveformType.Square,
                    85.0f,
                    315.0f,
                    new RelayBChannelDef[]
                    {
                        new RelayBChannelDef("CHANNEL 01", WaveformType.Square, 0.28f, 0.99f),
                        new RelayBChannelDef("CHANNEL 02", WaveformType.Triangle, 0.05f, 1.01f),
                        new RelayBChannelDef("CHANNEL 03", WaveformType.CompositeHarmonic, 0.05f, 1f),
                        new RelayBChannelDef("CHANNEL 04", WaveformType.Square, 0.035f, 1f)
                    },
                    new RelayBReferenceProfile(80f, 90f, 85.0f, 315.0f, true, 10f, 8f, 10f, "11100100"),
                    new RelayBCandidate[]
                    {
                        new RelayBCandidate(0, new[] { 92f, 184f }, 4.0f, 9.2f, WaveformType.Square, "11100100", false, 0f, "Frequency outside range"),
                        new RelayBCandidate(1, new[] { 85f, 170f }, 5.0f, 9.2f, WaveformType.Triangle, "11100100", false, 0f, "Waveform mismatch (triangle wave)"),
                        new RelayBCandidate(2, new[] { 85f, 170f }, 4.8f, 9.2f, WaveformType.Square, "01010101", false, 0f, "Pilot mismatch"),
                        new RelayBCandidate(3, new[] { 85f, 170f }, 18f, 9.2f, WaveformType.Square, "11100100", false, 0f, "Weak signal")
                    })
            };
        }

        private void OnValidate()
        {
            frequencyTolerancePercent = Mathf.Max(0.5f, frequencyTolerancePercent);
            phaseToleranceDegrees = Mathf.Max(1f, phaseToleranceDegrees);
            holdRequiredSeconds = Mathf.Max(1f, holdRequiredSeconds);
            instabilityGraceSeconds = Mathf.Max(0f, instabilityGraceSeconds);
            instabilityDecaySecondsPerSecond = Mathf.Max(0f, instabilityDecaySecondsPerSecond);
            minFrequency = Mathf.Max(1f, minFrequency);
            maxFrequency = Mathf.Max(minFrequency + 1f, maxFrequency);
            driftWarningSeconds = Mathf.Max(0.1f, driftWarningSeconds);
            driftTriggerHoldSeconds = Mathf.Max(0.5f, driftTriggerHoldSeconds);
            InitializeDefaultPresetsIfEmpty();
        }
    }
}
