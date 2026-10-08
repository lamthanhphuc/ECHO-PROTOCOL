using System;
using System.Collections.Generic;
using UnityEngine;

namespace EchoProtocol.RelayB
{
    public sealed class RelayBSignalSimulation
    {
        private readonly List<string> _systemLog = new List<string>();
        public RelayBDecoder Decoder { get; } = new RelayBDecoder();
        private RelayBConfig _config;
        private int _presetIndex;
        private RelayBPreset _attemptPreset;
        private int _cleanProblems;
        private int _cleanScenarioSeed;
        private int _selectedChannelIndex = -1;
        private float _currentFrequency = 50f;
        private float _currentPhase = 0f;
        private int _timingOffsetBaud = 0;
        private int _activeTab = 0;

        private RelayBModuleType[] _pipelineModules = new RelayBModuleType[4];
        private RelayBOutputDiagnostic _outputDiagnostic;
        private bool _falseLockDetected;

        // Legacy compatibility fields
        private RelayBFilterMode _filterMode = RelayBFilterMode.None;
        private RelayBGainMode _gainMode = RelayBGainMode.Low;
        private RelayBPilotMode _pilotMode = RelayBPilotMode.Unchecked;

        private float _syncTimer;
        private float _instabilityGraceTimer;
        private bool _isSyncing;
        private bool _isOnline;
        private bool _isScanning;
        private bool _hasScanned;

        private bool _driftTriggered;
        private float _attemptDriftPhaseOffset;
        private float _attemptDriftFrequencyPercent;
        private float _attemptDriftTriggerSeconds;
        public float FrequencyTolerancePercent => (_config != null ? _config.FrequencyTolerancePercent : 3f);
        public float PhaseToleranceDegrees => (_config != null ? _config.PhaseToleranceDegrees : 12f);
        private bool _driftWarningTriggered;
        private bool _mismatchPenaltyNotified;
        private float _scanDurationRemaining;

        public event Action<RelayBSnapshot> Changed;
        public event Action Completed;
        public event Action DriftWarning;
        public event Action DriftTriggered;
        public event Action SignalMismatchOccurred;
        public event Action InstabilityReset;
        public event Action OutputAnalyzed;

        public bool IsOnline => _isOnline;
        public bool IsSynchronizing => _isSyncing;
        public int SelectedChannelIndex => _selectedChannelIndex;
        public float CurrentFrequency => _currentFrequency;
        public float CurrentPhase => _currentPhase;
        public int TimingOffsetBaud => _timingOffsetBaud;
        public int ActiveTab => _activeTab;
        public RelayBModuleType[] PipelineModules => _pipelineModules;
        public RelayBOutputDiagnostic OutputDiagnostic => _outputDiagnostic;
        public bool IsSignalFound => _hasScanned && _selectedChannelIndex >= 0;
        public bool IsSignalClean => IsSignalFound && Decoder.IsComplete;
        public int CleanProblems => _cleanProblems;
        public RelayBFilterMode FilterMode => _filterMode;
        public RelayBGainMode GainMode => _gainMode;
        public RelayBPilotMode PilotMode => _pilotMode;
        public RelayBSnapshot Snapshot => BuildSnapshot();

        public void Initialize(RelayBConfig config, int presetIndex = 0, bool randomizeAttempt = false, int attemptSeed = 0)
        {
            _config = config;
            Decoder.Initialize(attemptSeed != 0 ? attemptSeed : 1337);
            _presetIndex = presetIndex;
            System.Random attemptRandom = new System.Random(attemptSeed != 0 ? attemptSeed : 1337);
            _attemptDriftPhaseOffset = config != null ? config.DriftPhaseOffset : 30f;
            _attemptDriftFrequencyPercent = 0f;
            _attemptDriftTriggerSeconds = config != null ? config.DriftTriggerHoldSeconds : 3.5f;
            if (randomizeAttempt)
            {
                _attemptDriftPhaseOffset *= Range(attemptRandom, 0.8f, 1.4f) * (attemptRandom.Next(2) == 0 ? -1f : 1f);
                if (attemptRandom.Next(2) == 0)
                {
                    _attemptDriftPhaseOffset = 0f;
                    _attemptDriftFrequencyPercent = Range(attemptRandom, 3.5f, 6f) * (attemptRandom.Next(2) == 0 ? -1f : 1f);
                }
                float hold = config != null ? config.HoldRequiredSeconds : 8f;
                float warning = config != null ? config.DriftWarningSeconds : 3f;
                _attemptDriftTriggerSeconds = Range(attemptRandom, Mathf.Min(warning + 0.5f, hold * 0.5f), Mathf.Max(warning + 0.5f, hold * 0.65f));
                _attemptDriftTriggerSeconds = Mathf.Min(_attemptDriftTriggerSeconds, hold - 0.5f);
            }
            _attemptPreset = randomizeAttempt && config != null
                ? BuildAttemptPreset(config.GetPreset(presetIndex), attemptRandom) : null;
            RelayBPreset preset = GetCurrentPreset();
            RelayBCandidate correct = preset != null ? preset.GetCandidate(preset.CorrectChannelIndex) : null;
            _cleanProblems = 1 | (correct != null && correct.HasSpur ? 2 : 0)
                | (correct != null && correct.DistortionPercent > 15f ? 4 : 0);
            _cleanScenarioSeed = 0;
            bool harder = presetIndex >= 2;
            float frequencyOffset = preset != null ? preset.TargetFrequency
                * Range(attemptRandom, harder ? 0.18f : 0.08f, harder ? 0.25f : 0.14f) : 0f;
            float phaseOffset = Range(attemptRandom, harder ? 85f : 35f, harder ? 130f : 65f);

            _selectedChannelIndex = -1;
            _currentFrequency = config != null && randomizeAttempt && preset != null
                ? Mathf.Clamp(preset.TargetFrequency + (preset.TargetFrequency + frequencyOffset <= config.MaxFrequency
                    && attemptRandom.Next(2) == 0 ? frequencyOffset : -frequencyOffset), config.MinFrequency, config.MaxFrequency)
                : config != null ? (config.MinFrequency + config.MaxFrequency) * 0.5f : 50f;
            _currentPhase = randomizeAttempt && preset != null
                ? (preset.TargetPhase + (attemptRandom.Next(2) == 0 ? phaseOffset : -phaseOffset) + 360f) % 360f : 0f;
            _timingOffsetBaud = 0;
            _activeTab = 0;

            _pipelineModules = new RelayBModuleType[4];
            _outputDiagnostic = default;
            _falseLockDetected = false;

            _filterMode = RelayBFilterMode.None;
            _gainMode = RelayBGainMode.Low;
            _pilotMode = RelayBPilotMode.Unchecked;

            _syncTimer = 0f;
            _instabilityGraceTimer = 0f;
            _isSyncing = false;
            _isOnline = false;
            _isScanning = false;
            _hasScanned = false;
            _driftTriggered = false;
            _driftWarningTriggered = false;
            _mismatchPenaltyNotified = false;

            _systemLog.Clear();
            AddSystemLog("RECEIVER INITIALIZED - READY FOR SPECTRUM SCAN");

            NotifyChanged();
        }

        public void SetActiveTab(int tabIndex)
        {
            _activeTab = Mathf.Clamp(tabIndex, 0, IsSignalClean ? 2 : IsSignalFound ? 1 : 0);
            NotifyChanged();
        }

        public void ScanSpectrum()
        {
            if (_isOnline || _hasScanned) return;
            _isScanning = false;
            _hasScanned = true;
            AddSystemLog("SPECTRUM SCAN COMPLETE: CANDIDATES DETECTED");
            NotifyChanged();
        }

        public void SelectChannel(int channelIndex)
        {
            RelayBPreset preset = GetCurrentPreset();
            if (_isOnline || _isSyncing || _selectedChannelIndex >= 0 || !_hasScanned || preset == null || channelIndex < 0 || channelIndex >= preset.Candidates.Length) return;
            RelayBCandidate candidate = preset.GetCandidate(channelIndex);
            RelayBReferenceProfile profile = preset.ReferenceProfile;
            if (candidate == null || candidate.Peaks.Length == 0) return;
            bool matches = candidate.Peaks[0] >= profile.FundamentalMinKhz
                && candidate.Peaks[0] <= profile.FundamentalMaxKhz
                && candidate.Waveform == preset.ReferenceWaveform
                && candidate.PilotFrame == profile.ExpectedPilot;
            if (!matches)
            {
                _falseLockDetected = _selectedChannelIndex < 0;
                AddSystemLog("SIGNAL MISMATCH. REVIEW FREQUENCY, WAVEFORM AND PILOT.");
                SignalMismatchOccurred?.Invoke();
                NotifyChanged();
                return;
            }
            _selectedChannelIndex = channelIndex;
            _activeTab = 0;
            _falseLockDetected = false;
            _outputDiagnostic = default;

            if (_isSyncing)
            {
                CancelSynchronization();
            }

            AddSystemLog($"CHANNEL {channelIndex + 1:00} ROUTED TO RECEIVER");
            NotifyChanged();
        }

        public void SetPipelineSlot(int slotIndex, RelayBModuleType module)
        {
            if (_isOnline || _isSyncing || !IsSignalFound || slotIndex < 0 || slotIndex >= 2
                || (module != RelayBModuleType.None && module != RelayBModuleType.NoiseSuppressor
                    && module != RelayBModuleType.Notch && module != RelayBModuleType.Gain)) return;
            _pipelineModules[slotIndex] = module;
            _activeTab = 1;
            _outputDiagnostic = default;
            NotifyChanged();
        }

        public RelayBOutputDiagnostic AnalyzeOutput()
        {
            RelayBPreset preset = GetCurrentPreset();
            RelayBCandidate candidate = IsSignalFound && preset != null ? preset.GetCandidate(_selectedChannelIndex) : null;

            if (candidate == null)
            {
                _outputDiagnostic = new RelayBOutputDiagnostic(-40f, -40f, 0f, 0f, false, "NO CHANNEL SELECTED", 0f, "", false, "Select a channel before analyzing output.");
                NotifyChanged();
                return _outputDiagnostic;
            }

            // Active problems for this channel
            bool hasNoiseProblem = (_cleanProblems & 1) != 0;
            bool hasInterferenceProblem = (_cleanProblems & 2) != 0;
            bool hasWeakSignal = (_cleanProblems & 4) != 0;

            // Tools placed by player
            bool hasNoiseFilter = false;
            bool hasInterferenceFilter = false;
            bool hasAmplifier = false;
            int amplifierSlot = -1;
            int noiseFilterSlot = -1;
            int interferenceFilterSlot = -1;

            for (int i = 0; i < 2; i++)
            {
                RelayBModuleType m = (i < _pipelineModules.Length) ? _pipelineModules[i] : RelayBModuleType.None;
                if (m == RelayBModuleType.NoiseSuppressor)
                {
                    hasNoiseFilter = true;
                    if (noiseFilterSlot < 0) noiseFilterSlot = i;
                }
                else if (m == RelayBModuleType.Notch)
                {
                    hasInterferenceFilter = true;
                    if (interferenceFilterSlot < 0) interferenceFilterSlot = i;
                }
                else if (m == RelayBModuleType.Gain)
                {
                    hasAmplifier = true;
                    if (amplifierSlot < 0) amplifierSlot = i;
                }
            }

            // Clipping check: Gain amplifies unfiltered interference
            bool clipping = hasAmplifier && ((hasNoiseProblem && (noiseFilterSlot < 0 || amplifierSlot < noiseFilterSlot))
                || (hasInterferenceProblem && (interferenceFilterSlot < 0 || amplifierSlot < interferenceFilterSlot)));
            string clippingMsg = "CLEAR";
            if (clipping) clippingMsg = "FILTER BEFORE AMPLIFIER";

            // Semantic validity: all active problems must be fixed, and no clipping
            bool noiseFixed = !hasNoiseProblem || hasNoiseFilter;
            bool interferenceFixed = !hasInterferenceProblem || hasInterferenceFilter;
            bool strengthFixed = !hasWeakSignal || hasAmplifier;
            int selectedTools = (_pipelineModules[0] != RelayBModuleType.None ? 1 : 0)
                + (_pipelineModules[1] != RelayBModuleType.None ? 1 : 0);
            int requiredTools = (hasNoiseProblem ? 1 : 0) + (hasInterferenceProblem ? 1 : 0)
                + (hasWeakSignal ? 1 : 0);
            bool semanticValid = noiseFixed && interferenceFixed && strengthFixed && !clipping
                && selectedTools == requiredTools;

            bool pilotMatch = candidate.PilotFrame == preset.ReferenceProfile.ExpectedPilot;
            bool pilotVerified = semanticValid && pilotMatch && _selectedChannelIndex == preset.CorrectChannelIndex;

            float snr = semanticValid ? 25f : 10f;
            float thd = candidate.DistortionPercent <= 10f ? candidate.DistortionPercent : (semanticValid ? 5f : candidate.DistortionPercent);
            float correlation = (semanticValid && pilotMatch) ? 0.98f : 0.35f;

            string noiseStatus = noiseFixed ? "LOW" : "HIGH";
            string intfStatus = hasInterferenceProblem ? (hasInterferenceFilter ? "CLEAR" : "DETECTED") : "CLEAR";
            string strStatus = hasWeakSignal ? (hasAmplifier && !clipping ? "GOOD" : "WEAK") : "GOOD";

            string summary = $"NOISE: {noiseStatus} | INTERFERENCE: {intfStatus} | STRENGTH: {strStatus}";

            _outputDiagnostic = new RelayBOutputDiagnostic(
                -12f,
                -30f,
                snr,
                thd,
                clipping,
                clippingMsg,
                correlation,
                candidate.PilotFrame,
                pilotVerified,
                summary);

            string resultLabel = semanticValid ? "SIGNAL CLEAN" : "SIGNAL NOT CLEAN";
            AddSystemLog($"TEST SIGNAL: {resultLabel} | {summary}");
            OutputAnalyzed?.Invoke();
            NotifyChanged();
            return _outputDiagnostic;
        }

        public void ClearOutputDiagnostic()
        {
            if (string.IsNullOrEmpty(_outputDiagnostic.Summary)) return;
            _outputDiagnostic = default;
            _activeTab = IsSignalFound ? 1 : 0;
            NotifyChanged();
        }

        public void RerollCleanScenario(int seed)
        {
            if (seed == 0 || seed == _cleanScenarioSeed || !IsSignalFound || _isSyncing || _isOnline) return;
            _cleanProblems = GetCleanProblemsForSeed(_presetIndex, seed);
            _cleanScenarioSeed = seed;
            _pipelineModules[0] = RelayBModuleType.None;
            _pipelineModules[1] = RelayBModuleType.None;
            _outputDiagnostic = default;
            _activeTab = 1;
            AddSystemLog("SIGNAL NOT CLEAN. NEW CONDITION GENERATED; CHECK IT BEFORE TESTING.");
            NotifyChanged();
        }

        public static int GetCleanProblemsForSeed(int presetIndex, int seed)
        {
            int choice = (int)((uint)seed % 3u);
            return presetIndex >= 2 ? new[] { 3, 5, 6 }[choice] : new[] { 1, 2, 4 }[choice];
        }

        public void MarkFindRerolled()
        {
            AddSystemLog("SIGNAL MISMATCH. NEW TARGET GENERATED; SCAN AGAIN.");
            NotifyChanged();
        }

        public void ApplyPhaseCorrection(float deltaDegrees)
        {
            if (_isOnline) return;
            SetPhase(_currentPhase + deltaDegrees);
        }

        public void ApplyTimingOffset(int deltaBaud)
        {
            if (_isOnline) return;
            _timingOffsetBaud = Mathf.Clamp(_timingOffsetBaud + deltaBaud, -2, 2);
            NotifyChanged();
        }

        public void ApplyPhaseTrim(float deltaDegrees)
        {
            ApplyPhaseCorrection(deltaDegrees);
        }

        public void ApplyClockTrim(int deltaBaud)
        {
            ApplyTimingOffset(deltaBaud);
        }

        public void SetFrequency(float frequency)
        {
            if (_isOnline) return;
            _currentFrequency = Mathf.Clamp(frequency, _config != null ? _config.MinFrequency : 10f, _config != null ? _config.MaxFrequency : 100f);
            NotifyChanged();
        }

        public void SetPhase(float phaseDegrees)
        {
            if (_isOnline) return;
            _currentPhase = phaseDegrees % 360f;
            if (_currentPhase < 0f) _currentPhase += 360f;
            NotifyChanged();
        }

        public void StartSynchronization()
        {
            if (_isOnline || !IsSignalClean) return;

            RelayBPreset preset = GetCurrentPreset();
            if (preset == null || _selectedChannelIndex < 0) return;

            RelayBCandidate candidate = preset.GetCandidate(_selectedChannelIndex);
            if (candidate == null) return;

            if (candidate.ChannelIndex != preset.CorrectChannelIndex)
            {
                // Wrong candidate or false lock
                _falseLockDetected = true;
                _isSyncing = false;
                _syncTimer = 0f;
                AddSystemLog("LINK ACQUISITION FAILED: FALSE CARRIER LOCK / PILOT MISMATCH");
                SignalMismatchOccurred?.Invoke();
                NotifyChanged();
                return;
            }

            _isSyncing = true;
            _falseLockDetected = false;
            _syncTimer = 0f;
            _instabilityGraceTimer = 0f;
            AddSystemLog("LINK ACQUISITION INITIATED - SYNCHRONIZING");
            NotifyChanged();
        }

        public void CancelSynchronization()
        {
            if (_isOnline) return;
            _isSyncing = false;
            _syncTimer = 0f;
            _instabilityGraceTimer = 0f;
            _mismatchPenaltyNotified = false;
            AddSystemLog("SYNCHRONIZATION ABORTED");
            NotifyChanged();
        }

        public void ForceCompleteForAuthoritativeSync()
        {
            _isOnline = true;
            _isSyncing = false;
            _syncTimer = _config != null ? _config.HoldRequiredSeconds : 8f;
            AddSystemLog("AUTHORITATIVE OVERRIDE: RELAY B ONLINE");
            NotifyChanged();
            Completed?.Invoke();
        }

        public void ApplyAuthoritativeSyncTelemetry(float progressSeconds, bool driftActive, bool driftWarning)
        {
            if (_isOnline) return;
            float progress = Mathf.Clamp(progressSeconds, 0f, _config != null ? _config.HoldRequiredSeconds : 8f);
            if (Mathf.Approximately(_syncTimer, progress) && _driftTriggered == driftActive
                && _driftWarningTriggered == driftWarning) return;
            _syncTimer = progress;
            _driftTriggered = driftActive;
            _driftWarningTriggered = driftWarning;
            NotifyChanged();
        }

        public void Tick(float deltaTime)
        {
            if (_isOnline) return;
            if (IsSignalFound && !Decoder.IsComplete && !Decoder.CanEdit)
            {
                Decoder.Tick(deltaTime);
                NotifyChanged();
            }

            if (_isScanning)
            {
                _scanDurationRemaining -= deltaTime;
                if (_scanDurationRemaining <= 0f)
                {
                    _isScanning = false;
                    _hasScanned = true;
                    AddSystemLog("SPECTRUM SCAN COMPLETE: CANDIDATES DETECTED");
                    NotifyChanged();
                }
            }

            if (_isSyncing)
            {
                UpdateSynchronization(deltaTime);
            }
        }

        private void UpdateSynchronization(float deltaTime)
        {
            bool isSync = CheckIsSynchronized(out _, out _);

            // Handle Ionospheric Drift during link acquisition
            if (_config != null && _config.EnableDrift && !_driftTriggered)
            {
                float holdForDrift = _attemptDriftTriggerSeconds;
                float warningTime = holdForDrift - _config.DriftWarningSeconds;

                if (_syncTimer >= warningTime - 0.001f && !_driftWarningTriggered)
                {
                    _driftWarningTriggered = true;
                    AddSystemLog("WARNING: SIGNAL DRIFT APPROACHING");
                    DriftWarning?.Invoke();
                }

                if (_syncTimer >= holdForDrift - 0.001f)
                {
                    _driftTriggered = true;
                    AddSystemLog(_attemptDriftFrequencyPercent != 0f ? "FREQUENCY DRIFT: RE-TUNE FREQUENCY." : "PHASE DRIFT: RE-ALIGN PHASE.");
                    DriftTriggered?.Invoke();
                }
            }

            if (isSync)
            {
                _instabilityGraceTimer = 0f;
                _mismatchPenaltyNotified = false;
                _syncTimer += deltaTime;

                float required = _config != null ? _config.HoldRequiredSeconds : 8f;
                if (_syncTimer >= required)
                {
                    _syncTimer = required;
                    _isOnline = true;
                    _isSyncing = false;
                    AddSystemLog("CARRIER LINK LOCKED - RELAY B ONLINE");
                    NotifyChanged();
                    Completed?.Invoke();
                    return;
                }
            }
            else
            {
                float grace = _config != null ? _config.InstabilityGraceSeconds : 0.5f;
                _instabilityGraceTimer += deltaTime;
                if (_instabilityGraceTimer > grace)
                {
                    float decay = _config != null ? _config.InstabilityDecaySecondsPerSecond : 2f;
                    _syncTimer = Mathf.Max(0f, _syncTimer - deltaTime * decay);
                    if (!_mismatchPenaltyNotified)
                    {
                        _mismatchPenaltyNotified = true;
                        InstabilityReset?.Invoke();
                        SignalMismatchOccurred?.Invoke();
                    }
                }
            }

            NotifyChanged();
        }

        public bool CheckIsSynchronized(out float freqErrorPercent, out float phaseErrorDegrees)
        {
            RelayBPreset preset = GetCurrentPreset();
            if (preset == null || _selectedChannelIndex < 0)
            {
                freqErrorPercent = 100f;
                phaseErrorDegrees = 180f;
                return false;
            }

            float targetFreq = GetEffectiveTargetFrequency();
            float targetPhase = GetEffectiveTargetPhase();

            freqErrorPercent = CalculateFrequencyErrorPercent(_currentFrequency, targetFreq);
            phaseErrorDegrees = CalculatePhaseErrorDegrees(_currentPhase, targetPhase);

            float freqTol = FrequencyTolerancePercent;
            float phaseTol = PhaseToleranceDegrees;

            bool isChannelCorrect = _selectedChannelIndex == preset.CorrectChannelIndex;
            bool phaseAligned = phaseErrorDegrees <= phaseTol;
            bool freqAligned = freqErrorPercent <= freqTol;

            // Must also satisfy pipeline quality and pilot if analyzed
            bool pipelineReady = IsSignalClean;

            return isChannelCorrect && phaseAligned && freqAligned && pipelineReady;
        }

        public float EvaluateSignalMatch(float freqErrorPercent, float phaseErrorDegrees)
        {
            RelayBPreset preset = GetCurrentPreset();
            if (preset == null || _selectedChannelIndex < 0) return 0f;

            float freqScore = Mathf.Clamp01(1f - (freqErrorPercent / 20f));
            float phaseScore = Mathf.Clamp01(1f - (phaseErrorDegrees / 75f));

            if (_selectedChannelIndex != preset.CorrectChannelIndex)
            {
                return Mathf.Min(65f, (freqScore * 0.5f + phaseScore * 0.5f) * 65f);
            }

            return (freqScore * 0.5f + phaseScore * 0.5f) * 100f;
        }

        public static float CalculateFrequencyErrorPercent(float current, float target)
        {
            if (target <= 0f) return 100f;
            return Mathf.Abs(current - target) / target * 100f;
        }

        public static float CalculatePhaseErrorDegrees(float current, float target)
        {
            float diff = Mathf.Abs((current % 360f) - (target % 360f));
            return diff > 180f ? 360f - diff : diff;
        }

        public RelayBPreset GetCurrentPreset()
        {
            return _attemptPreset ?? (_config != null ? _config.GetPreset(_presetIndex) : null);
        }

        private static RelayBPreset BuildAttemptPreset(RelayBPreset source, System.Random random)
        {
            if (source == null || source.Candidates.Length != 4) return source;
            float frequency = Range(random, 20f, 90f);
            float phase = random.Next(0, 360);
            var wave = (WaveformType)random.Next(0, 5);
            string pilot = MaskPilot(source.ReferenceProfile.ExpectedPilot, random.Next(1, 256));
            var profile = new RelayBReferenceProfile(frequency - 2f, frequency + 2f, frequency, phase,
                false, source.ReferenceProfile.MaxDistortionPercent,
                source.ReferenceProfile.SymbolRateMin, source.ReferenceProfile.SymbolRateMax, pilot);
            var order = new[] { 0, 1, 2, 3 };
            for (int i = 3; i > 0; i--)
            {
                int other = random.Next(i + 1);
                (order[i], order[other]) = (order[other], order[i]);
            }
            var candidates = new RelayBCandidate[4];
            var channels = new RelayBChannelDef[4];
            var nominal = source.GetCandidate(source.CorrectChannelIndex);
            int correct = -1;
            for (int i = 0; i < 4; i++)
            {
                int defect = order[i];
                float fundamental = frequency + Range(random, -1.5f, 1.5f);
                var candidateWave = wave;
                string candidatePilot = pilot;
                if (defect == 0) { correct = i; fundamental = frequency; }
                else if (defect == 1) candidatePilot = MaskPilot(pilot, 1 << random.Next(8));
                else if (defect == 2) candidateWave = (WaveformType)(((int)wave + random.Next(1, 5)) % 5);
                else if (random.Next(2) == 0) candidateWave = (WaveformType)(((int)wave + random.Next(1, 5)) % 5);
                else fundamental = random.Next(2) == 0
                    ? profile.FundamentalMinKhz - Range(random, 0.3f, 0.9f)
                    : profile.FundamentalMaxKhz + Range(random, 0.3f, 0.9f);
                candidates[i] = new RelayBCandidate(i, new[] { fundamental, fundamental * 2f },
                    nominal.DistortionPercent, nominal.SymbolRateKbaud, candidateWave, candidatePilot,
                    nominal.HasSpur, nominal.SpurFrequencyKhz, "");
                channels[i] = new RelayBChannelDef("CHANNEL " + (i + 1).ToString("00"), candidateWave, nominal.DistortionPercent / 100f);
            }
            return new RelayBPreset(source.PresetName, correct, wave, frequency, phase, channels, profile, candidates);
        }

        private static string MaskPilot(string pilot, int mask)
        {
            if (string.IsNullOrEmpty(pilot)) return pilot;
            char[] bits = pilot.ToCharArray();
            for (int i = 0; i < bits.Length && i < 8; i++)
                if ((mask & (1 << i)) != 0) bits[i] = bits[i] == '1' ? '0' : '1';
            return new string(bits);
        }

        public float GetEffectiveTargetPhase()
        {
            RelayBPreset preset = GetCurrentPreset();
            return preset != null
                ? (preset.TargetPhase + (_driftTriggered ? _attemptDriftPhaseOffset : 0f) + 360f) % 360f
                : 0f;
        }

        public float GetEffectiveTargetFrequency()
        {
            float frequency = GetCurrentPreset()?.TargetFrequency ?? 50f;
            return frequency * (1f + (_driftTriggered ? _attemptDriftFrequencyPercent : 0f) / 100f);
        }

        // Backward compatibility methods
        public void ApplyFrequencyCorrection(float deltaKhz) => SetFrequency(_currentFrequency + deltaKhz);
        public void ScanChannels(float scanDuration = 1.0f)
        {
            _isScanning = true;
            _scanDurationRemaining = scanDuration;
            NotifyChanged();
        }
        public void SetProcessing(RelayBFilterMode filter, RelayBGainMode gain, RelayBPilotMode pilot)
        {
            _filterMode = filter;
            _gainMode = gain;
            _pilotMode = pilot;
            NotifyChanged();
        }
        public RelayBChannelDiagnostic EvaluateSelectedChannel()
        {
            RelayBPreset preset = GetCurrentPreset();
            bool valid = preset != null && _selectedChannelIndex == preset.CorrectChannelIndex;
            return new RelayBChannelDiagnostic(valid, valid, valid, valid ? "NOMINAL" : "MISMATCH");
        }

        private RelayBSnapshot BuildSnapshot()
        {
            RelayBPreset preset = GetCurrentPreset();
            WaveformType refWave = preset != null ? preset.ReferenceWaveform : WaveformType.Sine;
            RelayBChannelDef chDef = preset != null && _selectedChannelIndex >= 0 ? preset.GetChannel(_selectedChannelIndex) : null;
            WaveformType curWave = chDef != null ? chDef.Waveform : WaveformType.Sine;

            float targetFreq = GetEffectiveTargetFrequency();
            float targetPhase = GetEffectiveTargetPhase();
            bool isSync = CheckIsSynchronized(out float fErr, out float pErr);
            float match = EvaluateSignalMatch(fErr, pErr);
            bool correctChan = preset != null && _selectedChannelIndex == preset.CorrectChannelIndex;

            return new RelayBSnapshot(
                DetermineStatus(isSync),
                _selectedChannelIndex,
                _currentFrequency,
                _currentPhase,
                targetFreq,
                targetPhase,
                refWave,
                curWave,
                fErr,
                pErr,
                match,
                _syncTimer,
                _config != null ? _config.HoldRequiredSeconds : 8f,
                _instabilityGraceTimer,
                isSync,
                _driftTriggered,
                _driftWarningTriggered,
                _isOnline,
                _isScanning,
                _hasScanned,
                correctChan,
                _filterMode,
                _gainMode,
                _pilotMode,
                new RelayBChannelDiagnostic(correctChan, correctChan, correctChan, correctChan ? "OPTIMAL" : "DISTORTED"),
                _outputDiagnostic,
                preset != null ? preset.ReferenceProfile : null,
                preset != null ? preset.Candidates : null,
                _pipelineModules,
                _activeTab,
                _timingOffsetBaud,
                _falseLockDetected,
                _systemLog.ToArray(),
                _cleanProblems,
                Decoder.Snapshot);
        }

        private RelayBStatus DetermineStatus(bool isSync)
        {
            if (_isOnline) return RelayBStatus.Online;
            if (_falseLockDetected) return RelayBStatus.SignalMismatch;
            if (_isScanning) return RelayBStatus.Scanning;
            if (_isSyncing)
            {
                if (_driftWarningTriggered && !_driftTriggered) return RelayBStatus.DriftWarning;
                if (!isSync) return RelayBStatus.ConnectionLost;
                return RelayBStatus.Synchronizing;
            }
            if (_selectedChannelIndex >= 0) return RelayBStatus.ChannelSelected;
            return RelayBStatus.Offline;
        }

        private void AddSystemLog(string msg)
        {
            string time = DateTime.Now.ToString("HH:mm:ss");
            _systemLog.Add($"[{time}] {msg}");
            if (_systemLog.Count > 10) _systemLog.RemoveAt(0);
        }

        private static float Range(System.Random rand, float min, float max)
        {
            return min + (float)rand.NextDouble() * (max - min);
        }

        private void NotifyChanged()
        {
            Changed?.Invoke(BuildSnapshot());
        }
    }
}
