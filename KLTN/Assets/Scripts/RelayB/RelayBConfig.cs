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

        [Header("Control Ranges")]
        [SerializeField, Min(1f)] private float minFrequency = 10f;
        [SerializeField, Min(10f)] private float maxFrequency = 100f;

        [Header("Ionospheric Drift (HARD Difficulty)")]
        [SerializeField] private bool enableDrift = true;
        [SerializeField, Min(0.5f)] private float driftWarningSeconds = 3f;
        [SerializeField, Min(1f)] private float driftTriggerHoldSeconds = 3.5f;
        [SerializeField] private float driftPhaseOffset = 22f;
        [SerializeField] private float driftFrequencyOffsetPercent = 0f;

        [Header("Presets")]
        [SerializeField] private RelayBPreset[] presets;

        public float FrequencyTolerancePercent => frequencyTolerancePercent;
        public float PhaseToleranceDegrees => phaseToleranceDegrees;
        public float HoldRequiredSeconds => holdRequiredSeconds;
        public float InstabilityGraceSeconds => instabilityGraceSeconds;
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
            if (presets == null || presets.Length == 0)
            {
                return CreateDefaultPreset();
            }

            int clamped = Mathf.Clamp(index, 0, presets.Length - 1);
            return presets[clamped];
        }

        public void InitializeDefaultPresetsIfEmpty()
        {
            if (presets != null && presets.Length >= 4)
            {
                return;
            }

            presets = new RelayBPreset[]
            {
                new RelayBPreset(
                    "Carrier Alpha (Echo-01)",
                    1, // Channel 2
                    WaveformType.Sine,
                    42.0f,
                    90.0f,
                    new RelayBChannelDef[]
                    {
                        new RelayBChannelDef("CHANNEL 01", WaveformType.Triangle, 0.2f),
                        new RelayBChannelDef("CHANNEL 02", WaveformType.Sine, 0f),
                        new RelayBChannelDef("CHANNEL 03", WaveformType.Square, 0.15f),
                        new RelayBChannelDef("CHANNEL 04", WaveformType.CompositeHarmonic, 0.3f)
                    }),
                new RelayBPreset(
                    "Pulse Beta (Echo-02)",
                    2, // Channel 3
                    WaveformType.Triangle,
                    68.0f,
                    240.0f,
                    new RelayBChannelDef[]
                    {
                        new RelayBChannelDef("CHANNEL 01", WaveformType.Square, 0.25f),
                        new RelayBChannelDef("CHANNEL 02", WaveformType.CompositeHarmonic, 0.2f),
                        new RelayBChannelDef("CHANNEL 03", WaveformType.Triangle, 0f),
                        new RelayBChannelDef("CHANNEL 04", WaveformType.Sine, 0.1f)
                    }),
                new RelayBPreset(
                    "Sub-Harmonic Gamma (Echo-03)",
                    0, // Channel 1
                    WaveformType.CompositeHarmonic,
                    28.5f,
                    180.0f,
                    new RelayBChannelDef[]
                    {
                        new RelayBChannelDef("CHANNEL 01", WaveformType.CompositeHarmonic, 0f),
                        new RelayBChannelDef("CHANNEL 02", WaveformType.Sine, 0.2f),
                        new RelayBChannelDef("CHANNEL 03", WaveformType.Triangle, 0.35f),
                        new RelayBChannelDef("CHANNEL 04", WaveformType.Square, 0.4f)
                    }),
                new RelayBPreset(
                    "Flux Delta (Echo-04)",
                    3, // Channel 4
                    WaveformType.Square,
                    85.0f,
                    315.0f,
                    new RelayBChannelDef[]
                    {
                        new RelayBChannelDef("CHANNEL 01", WaveformType.Sine, 0.3f),
                        new RelayBChannelDef("CHANNEL 02", WaveformType.Triangle, 0.15f),
                        new RelayBChannelDef("CHANNEL 03", WaveformType.CompositeHarmonic, 0.25f),
                        new RelayBChannelDef("CHANNEL 04", WaveformType.Square, 0f)
                    })
            };
        }

        private static RelayBPreset CreateDefaultPreset()
        {
            return new RelayBPreset(
                "Carrier Alpha",
                1,
                WaveformType.Sine,
                42.0f,
                90.0f,
                new RelayBChannelDef[]
                {
                    new RelayBChannelDef("CHANNEL 01", WaveformType.Triangle),
                    new RelayBChannelDef("CHANNEL 02", WaveformType.Sine),
                    new RelayBChannelDef("CHANNEL 03", WaveformType.Square),
                    new RelayBChannelDef("CHANNEL 04", WaveformType.CompositeHarmonic)
                });
        }

        private void OnValidate()
        {
            frequencyTolerancePercent = Mathf.Max(0.5f, frequencyTolerancePercent);
            phaseToleranceDegrees = Mathf.Max(1f, phaseToleranceDegrees);
            holdRequiredSeconds = Mathf.Max(1f, holdRequiredSeconds);
            instabilityGraceSeconds = Mathf.Max(0f, instabilityGraceSeconds);
            minFrequency = Mathf.Max(1f, minFrequency);
            maxFrequency = Mathf.Max(minFrequency + 1f, maxFrequency);
            driftWarningSeconds = Mathf.Max(0.1f, driftWarningSeconds);
            driftTriggerHoldSeconds = Mathf.Max(0.5f, driftTriggerHoldSeconds);

            if (presets == null || presets.Length == 0)
            {
                InitializeDefaultPresetsIfEmpty();
            }
        }
    }
}

