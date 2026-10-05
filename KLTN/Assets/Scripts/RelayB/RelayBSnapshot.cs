using System;
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
            bool isScanning,
            bool hasScanned,
            bool isSelectedChannelCorrect,
            RelayBFilterMode filterMode,
            RelayBGainMode gainMode,
            RelayBPilotMode pilotMode,
            RelayBChannelDiagnostic selectedChannelDiagnostic,
            RelayBOutputDiagnostic outputDiagnostic = default,
            RelayBReferenceProfile referenceProfile = null,
            RelayBCandidate[] candidates = null,
            RelayBModuleType[] pipelineModules = null,
            int activeTab = 0,
            int timingOffsetBaud = 0,
            bool falseLockDetected = false,
            string[] systemLog = null,
            int cleanProblems = 0,
            RelayBDecodeSnapshot decoder = default)
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
            HasScanned = hasScanned;
            IsSelectedChannelCorrect = isSelectedChannelCorrect;
            FilterMode = filterMode;
            GainMode = gainMode;
            PilotMode = pilotMode;
            SelectedChannelDiagnostic = selectedChannelDiagnostic;
            OutputDiagnostic = outputDiagnostic;
            ReferenceProfile = referenceProfile;
            Candidates = candidates != null ? (RelayBCandidate[])candidates.Clone() : Array.Empty<RelayBCandidate>();
            PipelineModules = pipelineModules != null ? (RelayBModuleType[])pipelineModules.Clone() : new RelayBModuleType[4];
            ActiveTab = activeTab;
            TimingOffsetBaud = timingOffsetBaud;
            FalseLockDetected = falseLockDetected;
            SystemLog = systemLog != null ? (string[])systemLog.Clone() : Array.Empty<string>();
            CleanProblems = cleanProblems;
            Decoder = decoder;
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
        public bool HasScanned { get; }
        public bool IsSelectedChannelCorrect { get; }
        public RelayBFilterMode FilterMode { get; }
        public RelayBGainMode GainMode { get; }
        public RelayBPilotMode PilotMode { get; }
        public RelayBChannelDiagnostic SelectedChannelDiagnostic { get; }
        public RelayBOutputDiagnostic OutputDiagnostic { get; }
        public RelayBReferenceProfile ReferenceProfile { get; }
        public RelayBCandidate[] Candidates { get; }
        public RelayBModuleType[] PipelineModules { get; }
        public int ActiveTab { get; }
        public int TimingOffsetBaud { get; }
        public bool FalseLockDetected { get; }
        public string[] SystemLog { get; }
        public int CleanProblems { get; }
        public RelayBDecodeSnapshot Decoder { get; }

        public float Progress01 => HoldRequiredSeconds <= 0f
            ? (IsOnline ? 1f : 0f)
            : Mathf.Clamp01(SyncProgressSeconds / HoldRequiredSeconds);
    }
}
