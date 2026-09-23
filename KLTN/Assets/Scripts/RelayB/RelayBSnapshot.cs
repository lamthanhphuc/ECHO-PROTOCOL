using UnityEngine;

namespace EchoProtocol.RelayB
{
    public readonly struct RelayBSnapshot
    {
        public RelayBSnapshot(
            RelayBStatus status,
            int selectedChannelIndex,
            float currentFrequency,
            float currentPhase,
            float targetFrequency,
            float targetPhase,
            WaveformType referenceWaveform,
            WaveformType currentWaveform,
            float frequencyErrorPercent,
            float phaseErrorDegrees,
            float signalMatchPercent,
            float syncProgressSeconds,
            float holdRequiredSeconds,
            float instabilityGraceRemaining,
            bool isSynchronized,
            bool isDriftActive,
            bool isDriftWarning,
            bool isOnline,
            bool isScanning)
        {
            Status = status;
            SelectedChannelIndex = selectedChannelIndex;
            CurrentFrequency = currentFrequency;
            CurrentPhase = currentPhase;
            TargetFrequency = targetFrequency;
            TargetPhase = targetPhase;
            ReferenceWaveform = referenceWaveform;
            CurrentWaveform = currentWaveform;
            FrequencyErrorPercent = frequencyErrorPercent;
            PhaseErrorDegrees = phaseErrorDegrees;
            SignalMatchPercent = signalMatchPercent;
            SyncProgressSeconds = syncProgressSeconds;
            HoldRequiredSeconds = holdRequiredSeconds;
            InstabilityGraceRemaining = instabilityGraceRemaining;
            IsSynchronized = isSynchronized;
            IsDriftActive = isDriftActive;
            IsDriftWarning = isDriftWarning;
            IsOnline = isOnline;
            IsScanning = isScanning;
        }

        public RelayBStatus Status { get; }
        public int SelectedChannelIndex { get; }
        public float CurrentFrequency { get; }
        public float CurrentPhase { get; }
        public float TargetFrequency { get; }
        public float TargetPhase { get; }
        public WaveformType ReferenceWaveform { get; }
        public WaveformType CurrentWaveform { get; }
        public float FrequencyErrorPercent { get; }
        public float PhaseErrorDegrees { get; }
        public float SignalMatchPercent { get; }
        public float SyncProgressSeconds { get; }
        public float HoldRequiredSeconds { get; }
        public float InstabilityGraceRemaining { get; }
        public bool IsSynchronized { get; }
        public bool IsDriftActive { get; }
        public bool IsDriftWarning { get; }
        public bool IsOnline { get; }
        public bool IsScanning { get; }

        public float Progress01 => HoldRequiredSeconds <= 0f
            ? (IsOnline ? 1f : 0f)
            : Mathf.Clamp01(SyncProgressSeconds / HoldRequiredSeconds);
    }
}

