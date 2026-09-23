using System;
using UnityEngine;

namespace EchoProtocol.RelayB
{
    [Serializable]
    public sealed class RelayBSignalSimulation
    {
        private RelayBConfig _config;
        private int _presetIndex;
        private int _selectedChannelIndex = -1;
        private float _currentFrequency = 50f;
        private float _currentPhase = 0f;

        private float _syncTimer;
        private float _instabilityGraceTimer;
        private bool _isSyncing;
        private bool _online;
        private bool _isScanning;
        private float _scanningTimeRemaining;

        private bool _driftTriggered;
        private bool _isDriftActive;
        private bool _isDriftWarning;
        private bool _driftWarningFired;

        public event Action<RelayBSnapshot> Changed;
        public event Action Completed;
        public event Action DriftWarning;
        public event Action DriftTriggered;
        public event Action SignalMismatchOccurred;
        public event Action InstabilityReset;

        public RelayBSnapshot Snapshot => BuildSnapshot();
        public bool IsOnline => _online;
        public int PresetIndex => _presetIndex;
        public int SelectedChannelIndex => _selectedChannelIndex;
        public float CurrentFrequency => _currentFrequency;
        public float CurrentPhase => _currentPhase;

        public void Initialize(RelayBConfig config, int presetIndex = 0)
        {
            _config = config;
            _presetIndex = Mathf.Max(0, presetIndex);
            _selectedChannelIndex = -1;
            _currentFrequency = config != null ? (config.MinFrequency + config.MaxFrequency) * 0.5f : 50f;
            _currentPhase = 0f;

            _syncTimer = 0f;
            _instabilityGraceTimer = 0f;
            _isSyncing = false;
            _online = false;
            _isScanning = false;
            _scanningTimeRemaining = 0f;

            _driftTriggered = false;
            _isDriftActive = false;
            _isDriftWarning = false;
            _driftWarningFired = false;

            NotifyChanged();
        }

        public void SelectChannel(int channelIndex)
        {
            if (_online)
            {
                return;
            }

            int clamped = Mathf.Clamp(channelIndex, 0, 3);
            if (_selectedChannelIndex != clamped)
            {
                _selectedChannelIndex = clamped;
                // Changing channel cancels current ongoing sync run
                if (_isSyncing)
                {
                    _isSyncing = false;
                    _syncTimer = 0f;
                    _instabilityGraceTimer = 0f;
                }

                NotifyChanged();
            }
        }

        public void SetFrequency(float frequency)
        {
            if (_online)
            {
                return;
            }

            float min = _config != null ? _config.MinFrequency : 10f;
            float max = _config != null ? _config.MaxFrequency : 100f;
            _currentFrequency = Mathf.Clamp(frequency, min, max);
            NotifyChanged();
        }

        public void SetPhase(float phaseDegrees)
        {
            if (_online)
            {
                return;
            }

            _currentPhase = NormalizeAngle(phaseDegrees);
            NotifyChanged();
        }

        public void ScanChannels(float scanDuration = 1.0f)
        {
            if (_online)
            {
                return;
            }

            _isScanning = true;
            _scanningTimeRemaining = Mathf.Max(0.2f, scanDuration);
            NotifyChanged();
        }

        public void StartSynchronization()
        {
            if (_online || _selectedChannelIndex < 0)
            {
                return;
            }

            _isSyncing = true;
            _instabilityGraceTimer = 0f;

            bool isSync = CheckIsSynchronized(out _, out _);
            if (!isSync)
            {
                SignalMismatchOccurred?.Invoke();
            }

            NotifyChanged();
        }

        public void CancelSynchronization()
        {
            if (_online)
            {
                return;
            }

            _isSyncing = false;
            _syncTimer = 0f;
            _instabilityGraceTimer = 0f;
            _isDriftWarning = false;
            NotifyChanged();
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f || _online)
            {
                return;
            }

            if (_isScanning)
            {
                _scanningTimeRemaining -= deltaTime;
                if (_scanningTimeRemaining <= 0f)
                {
                    _isScanning = false;
                    NotifyChanged();
                }
            }

            if (!_isSyncing)
            {
                return;
            }

            bool synchronized = CheckIsSynchronized(out _, out _);

            if (synchronized)
            {
                _instabilityGraceTimer = 0f;
                _syncTimer += deltaTime;

                UpdateDriftProgress();

                float holdRequired = _config != null ? _config.HoldRequiredSeconds : 8f;
                if (_syncTimer >= holdRequired)
                {
                    _syncTimer = holdRequired;
                    _online = true;
                    _isSyncing = false;
                    _isDriftWarning = false;
                    Completed?.Invoke();
                }
            }
            else
            {
                _instabilityGraceTimer += deltaTime;
                float graceLimit = _config != null ? _config.InstabilityGraceSeconds : 0.5f;
                if (_instabilityGraceTimer > graceLimit)
                {
                    if (_syncTimer > 0f)
                    {
                        _syncTimer = 0f;
                        _isDriftWarning = false;
                        InstabilityReset?.Invoke();
                        SignalMismatchOccurred?.Invoke();
                    }
                }
            }

            NotifyChanged();
        }

        public void ForceCompleteForAuthoritativeSync()
        {
            _online = true;
            _isSyncing = false;
            _syncTimer = _config != null ? _config.HoldRequiredSeconds : 8f;
            _isDriftWarning = false;
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

            float effectiveTargetFreq = GetEffectiveTargetFrequency(preset);
            float effectiveTargetPhase = GetEffectiveTargetPhase(preset);

            freqErrorPercent = CalculateFrequencyErrorPercent(_currentFrequency, effectiveTargetFreq);
            phaseErrorDegrees = CalculatePhaseErrorDegrees(_currentPhase, effectiveTargetPhase);

            float freqTol = _config != null ? _config.FrequencyTolerancePercent : 3f;
            float phaseTol = _config != null ? _config.PhaseToleranceDegrees : 12f;

            bool isChannelCorrect = (_selectedChannelIndex == preset.CorrectChannelIndex);
            RelayBChannelDef channelDef = preset.GetChannel(_selectedChannelIndex);
            bool isWaveformCorrect = channelDef != null && channelDef.Waveform == preset.ReferenceWaveform;

            return isChannelCorrect && isWaveformCorrect && freqErrorPercent <= freqTol && phaseErrorDegrees <= phaseTol;
        }

        public static float CalculateFrequencyErrorPercent(float current, float target)
        {
            if (target <= 0.0001f)
            {
                return 100f;
            }

            return (Mathf.Abs(current - target) / target) * 100f;
        }

        public static float CalculatePhaseErrorDegrees(float current, float target)
        {
            float normCur = NormalizeAngle(current);
            float normTarget = NormalizeAngle(target);
            float delta = Mathf.Abs(normCur - normTarget);
            return Mathf.Min(delta, 360f - delta);
        }

        public static float NormalizeAngle(float degrees)
        {
            float result = degrees % 360f;
            if (result < 0f)
            {
                result += 360f;
            }

            return result;
        }

        public float EvaluateSignalMatch(float freqErrorPercent, float phaseErrorDegrees)
        {
            RelayBPreset preset = GetCurrentPreset();
            if (preset == null || _selectedChannelIndex < 0)
            {
                return 0f;
            }

            bool isChannelCorrect = (_selectedChannelIndex == preset.CorrectChannelIndex);
            RelayBChannelDef channelDef = preset.GetChannel(_selectedChannelIndex);
            bool isWaveformCorrect = channelDef != null && channelDef.Waveform == preset.ReferenceWaveform;

            float freqScore = Mathf.Clamp01(1f - (freqErrorPercent / 20f));
            float phaseScore = Mathf.Clamp01(1f - (phaseErrorDegrees / 75f));

            float waveShapeFactor;
            if (isChannelCorrect && isWaveformCorrect)
            {
                waveShapeFactor = 1.0f;
            }
            else
            {
                // Wrong channel waveforms never correlate above 65%
                float distortion = channelDef != null ? channelDef.HarmonicDistortion : 0.3f;
                waveShapeFactor = Mathf.Clamp(0.55f - distortion * 0.25f, 0.2f, 0.62f);
            }

            float match = waveShapeFactor * freqScore * phaseScore * 100f;
            return Mathf.Clamp(match, 0f, 100f);
        }

        private void UpdateDriftProgress()
        {
            if (_config == null || !_config.EnableDrift || _driftTriggered)
            {
                return;
            }

            float triggerAt = _config.DriftTriggerHoldSeconds;
            float warnAt = Mathf.Max(0.5f, triggerAt - _config.DriftWarningSeconds);

            if (_syncTimer >= warnAt && !_isDriftWarning && !_driftWarningFired)
            {
                _isDriftWarning = true;
                _driftWarningFired = true;
                DriftWarning?.Invoke();
            }

            if (_syncTimer >= triggerAt)
            {
                _isDriftWarning = false;
                _isDriftActive = true;
                _driftTriggered = true;
                DriftTriggered?.Invoke();
            }
        }

        private float GetEffectiveTargetPhase(RelayBPreset preset)
        {
            float phase = preset.TargetPhase;
            if (_isDriftActive && _config != null)
            {
                phase = NormalizeAngle(phase + _config.DriftPhaseOffset);
            }

            return phase;
        }

        private float GetEffectiveTargetFrequency(RelayBPreset preset)
        {
            float freq = preset.TargetFrequency;
            if (_isDriftActive && _config != null)
            {
                freq *= (1f + _config.DriftFrequencyOffsetPercent / 100f);
            }

            return freq;
        }

        private RelayBPreset GetCurrentPreset()
        {
            if (_config == null)
            {
                return null;
            }

            return _config.GetPreset(_presetIndex);
        }

        private RelayBSnapshot BuildSnapshot()
        {
            RelayBPreset preset = GetCurrentPreset();
            float effectiveTargetFreq = preset != null ? GetEffectiveTargetFrequency(preset) : 50f;
            float effectiveTargetPhase = preset != null ? GetEffectiveTargetPhase(preset) : 180f;
            WaveformType refWave = preset != null ? preset.ReferenceWaveform : WaveformType.Sine;

            RelayBChannelDef currentChannelDef = preset != null && _selectedChannelIndex >= 0
                ? preset.GetChannel(_selectedChannelIndex)
                : null;
            WaveformType curWave = currentChannelDef != null ? currentChannelDef.Waveform : WaveformType.Sine;

            float freqError = CalculateFrequencyErrorPercent(_currentFrequency, effectiveTargetFreq);
            float phaseError = CalculatePhaseErrorDegrees(_currentPhase, effectiveTargetPhase);
            float signalMatch = EvaluateSignalMatch(freqError, phaseError);
            bool isSync = CheckIsSynchronized(out _, out _);

            float graceLimit = _config != null ? _config.InstabilityGraceSeconds : 0.5f;
            float graceRemaining = Mathf.Max(0f, graceLimit - _instabilityGraceTimer);
            float holdRequired = _config != null ? _config.HoldRequiredSeconds : 8f;

            RelayBStatus status = DetermineStatus(isSync);

            return new RelayBSnapshot(
                status,
                _selectedChannelIndex,
                _currentFrequency,
                _currentPhase,
                effectiveTargetFreq,
                effectiveTargetPhase,
                refWave,
                curWave,
                freqError,
                phaseError,
                signalMatch,
                _syncTimer,
                holdRequired,
                graceRemaining,
                isSync,
                _isDriftActive,
                _isDriftWarning,
                _online,
                _isScanning);
        }

        private RelayBStatus DetermineStatus(bool isSync)
        {
            if (_online) return RelayBStatus.Online;
            if (_isScanning) return RelayBStatus.Scanning;
            if (_isSyncing)
            {
                if (_isDriftWarning) return RelayBStatus.DriftWarning;
                if (isSync) return RelayBStatus.Synchronizing;
                return RelayBStatus.ConnectionLost;
            }

            if (_selectedChannelIndex >= 0)
            {
                return RelayBStatus.ChannelSelected;
            }

            return RelayBStatus.Offline;
        }

        private void NotifyChanged()
        {
            Changed?.Invoke(BuildSnapshot());
        }
    }
}

