using System;
using UnityEngine;

namespace EchoProtocol.RelayA
{
    [Serializable]
    public sealed class RelayAStabilizationSimulation
    {
        private RelayAConfig _config;
        private Vector3 _controls;
        private RelayAStabilizationOutputs _outputs;
        private RelayAStabilizationOutputs _previousOutputs;
        private float _elapsedRunningSeconds;
        private float _stabilitySeconds;
        private float _instabilitySeconds;
        private float _faultWarningRemaining;
        private float _faultActiveRemaining;
        private float _faultTriggerAtSeconds;
        private System.Random _attemptRandom;
        private bool _running;
        private bool _online;
        private bool _randomizeAttempt;
        private bool _dangerPenaltyNotified;
        private RelayAFaultType _warningFault;
        private RelayAFaultType _activeFault;
        private RelayAFaultType _scheduledFault;
        private int _recoveredFaults;
        private float _recoverySeconds;
        private int _faultOrderOffset;
        private Vector3 _calibrationOffset;
        private float _faultStrength = 1f;

        public RelayAStabilizationOutputs EvaluateControlTarget(Vector3 controls, RelayAFaultType fault, float elapsedSeconds)
        {
            if (_config == null) return new RelayAStabilizationOutputs(225f, 50f, 50f);
            var output = _config.EvaluateTarget(controls + _calibrationOffset, fault, elapsedSeconds);
            Vector3 extra = _config.GetFaultOffset(fault) * (_faultStrength - 1f);
            return new RelayAStabilizationOutputs(output.Voltage + extra.x, output.Frequency + extra.y, output.LoadBalance + extra.z);
        }

        public event Action<RelayAStabilizationSnapshot> Changed;
        public event Action Completed;
        public event Action<RelayAFaultType> FaultWarningStarted;
        public event Action<RelayAFaultType> FaultActivated;
        public event Action OverloadStarted;

        public RelayAStabilizationSnapshot Snapshot => BuildSnapshot();

        public void Initialize(RelayAConfig config, bool randomizeAttempt = false, int attemptSeed = 0)
        {
            _config = config;
            _attemptRandom = randomizeAttempt
                ? new System.Random(attemptSeed != 0 ? attemptSeed : Environment.TickCount)
                : null;
            _calibrationOffset = randomizeAttempt
                ? new Vector3(Range(_attemptRandom, -10f, 10f), Range(_attemptRandom, -10f, 10f), Range(_attemptRandom, -10f, 10f))
                : Vector3.zero;
            _faultStrength = randomizeAttempt ? Range(_attemptRandom, 0.85f, 1.15f) : 1f;
            _controls = config != null && randomizeAttempt
                ? RandomizeControls(config.InitialControls, _attemptRandom)
                : config != null ? config.InitialControls : new Vector3(50f, 50f, 50f);
            _outputs = config != null
                ? EvaluateControlTarget(_controls, RelayAFaultType.None, 0f)
                : new RelayAStabilizationOutputs(225f, 50f, 50f);
            for (int retry = 0; config != null && config.IsOutputStable(_outputs) && retry < 32; retry++)
            {
                _controls = randomizeAttempt ? RandomizeControls(config.InitialControls, _attemptRandom) : config.InitialControls;
                _outputs = EvaluateControlTarget(_controls, RelayAFaultType.None, 0f);
            }
            _previousOutputs = _outputs;
            _elapsedRunningSeconds = 0f;
            _stabilitySeconds = 0f;
            _instabilitySeconds = 0f;
            _faultWarningRemaining = 0f;
            _faultActiveRemaining = 0f;
            _randomizeAttempt = randomizeAttempt;
            _recoveredFaults = 0;
            _recoverySeconds = 0f;
            _faultOrderOffset = _attemptRandom != null ? _attemptRandom.Next(0, 3) : 0;
            _faultTriggerAtSeconds = BuildNextFaultTime(
                config != null ? config.EarliestFaultAtSeconds : 8f,
                config != null ? 7f : 0f);
            _running = false;
            _online = false;
            _dangerPenaltyNotified = false;
            _warningFault = RelayAFaultType.None;
            _activeFault = RelayAFaultType.None;
            _scheduledFault = randomizeAttempt ? SelectRandomFault(_attemptRandom) : RelayAFaultType.None;
            if (config != null && config.RequireFaultRecovery)
                _faultTriggerAtSeconds = randomizeAttempt ? Range(_attemptRandom, 2f, 5f) : 2f;
            NotifyChanged();
        }

        public void SetControls(float generatorOutput, float frequencyRegulator, float loadDistribution)
        {
            if (_online)
            {
                return;
            }

            _controls = new Vector3(
                Mathf.Clamp(generatorOutput, 0f, 100f),
                Mathf.Clamp(frequencyRegulator, 0f, 100f),
                Mathf.Clamp(loadDistribution, 0f, 100f));
            NotifyChanged();
        }

        public void Start()
        {
            if (_online)
            {
                return;
            }

            _running = true;
            NotifyChanged();
        }

        public void EmergencyStop()
        {
            if (_online)
            {
                return;
            }

            _running = false;
            _instabilitySeconds = 0f;
            _recoverySeconds = 0f;
            if (_config != null && _config.RequireFaultRecovery && _activeFault != RelayAFaultType.None)
                _faultActiveRemaining = _config.FaultRecoveryHoldSeconds;
            _dangerPenaltyNotified = false;
            NotifyChanged();
        }

        public void Tick(float deltaTime)
        {
            if (_config == null || deltaTime <= 0f || _online)
            {
                return;
            }

            _previousOutputs = _outputs;
            if (_running)
            {
                _elapsedRunningSeconds += deltaTime;
                UpdateFault(deltaTime);
            }

            RelayAStabilizationOutputs target = EvaluateControlTarget(_controls, _activeFault, _elapsedRunningSeconds);
            float response = 1f - Mathf.Exp(-deltaTime / Mathf.Max(0.05f, _config.ResponseDelaySeconds));
            _outputs = new RelayAStabilizationOutputs(
                Mathf.Lerp(_outputs.Voltage, target.Voltage, response),
                Mathf.Lerp(_outputs.Frequency, target.Frequency, response),
                Mathf.Lerp(_outputs.LoadBalance, target.LoadBalance, response));

            bool stable = _config.IsOutputStable(_outputs);
            bool dangerous = _config.IsOutputDangerous(_outputs);

            // Expert faults persist until the operator holds compensated outputs, not until a timer expires.
            if (_running && _config.RequireFaultRecovery && _activeFault != RelayAFaultType.None)
            {
                _recoverySeconds = stable ? _recoverySeconds + deltaTime : 0f;
                _faultActiveRemaining = Mathf.Max(0f, _config.FaultRecoveryHoldSeconds - _recoverySeconds);
                if (_recoverySeconds >= _config.FaultRecoveryHoldSeconds)
                {
                    _recoveredFaults++;
                    _recoverySeconds = 0f;
                    _activeFault = RelayAFaultType.None;
                    _faultActiveRemaining = 0f;
                    _faultTriggerAtSeconds = float.PositiveInfinity;
                    _stabilitySeconds = 0f;
                    stable = false;
                }
            }

            if (_running && dangerous)
            {
                if (!_dangerPenaltyNotified && _stabilitySeconds > 0f)
                {
                    OverloadStarted?.Invoke();
                }

                _dangerPenaltyNotified = true;
                float decayRate = _config.DangerDecaySecondsPerSecond;
                _stabilitySeconds = Mathf.Max(0f, _stabilitySeconds - deltaTime * decayRate);
                _instabilitySeconds = 0f;
            }
            else if (_running && stable)
            {
                _dangerPenaltyNotified = false;
                _instabilitySeconds = 0f;
                if (!_config.RequireFaultRecovery || _recoveredFaults >= 1)
                    _stabilitySeconds += deltaTime;
                if (_stabilitySeconds >= _config.StabilityRequiredSeconds)
                {
                    _stabilitySeconds = _config.StabilityRequiredSeconds;
                    _running = false;
                    _online = true;
                    _warningFault = RelayAFaultType.None;
                    _activeFault = RelayAFaultType.None;
                    Completed?.Invoke();
                }
            }
            else if (_running)
            {
                _dangerPenaltyNotified = false;
                _instabilitySeconds += deltaTime;
                if (_instabilitySeconds > _config.InstabilityToleranceSeconds)
                {
                    float decayRate = _config.InstabilityDecaySecondsPerSecond;
                    _stabilitySeconds = Mathf.Max(0f, _stabilitySeconds - deltaTime * decayRate);
                }
            }

            NotifyChanged();
        }

        public void ForceCompleteForAuthoritativeSync()
        {
            _recoveredFaults = 1;
            _online = true;
            _running = false;
            _stabilitySeconds = _config != null ? _config.StabilityRequiredSeconds : 12f;
            _dangerPenaltyNotified = false;
            _warningFault = RelayAFaultType.None;
            _activeFault = RelayAFaultType.None;
            NotifyChanged();
        }

        public void ApplyAuthoritative(Vector3 controls, Vector3 readings, bool running,
            float progress, RelayAFaultType warning, RelayAFaultType active, Vector2 faultTimers, int recoveredFaults = 0)
        {
            if (_controls.x == controls.x && _controls.y == controls.y && _controls.z == controls.z
                && _outputs.Voltage == readings.x && _outputs.Frequency == readings.y && _outputs.LoadBalance == readings.z
                && _running == running && _stabilitySeconds == progress && _warningFault == warning && _activeFault == active
                && _faultWarningRemaining == faultTimers.x && _faultActiveRemaining == faultTimers.y
                && _recoveredFaults == recoveredFaults) return;
            _controls = controls;
            _outputs = new RelayAStabilizationOutputs(readings.x, readings.y, readings.z);
            _previousOutputs = _outputs;
            _running = running;
            _stabilitySeconds = progress;
            _warningFault = warning;
            _activeFault = active;
            _faultWarningRemaining = faultTimers.x;
            _faultActiveRemaining = faultTimers.y;
            _recoveredFaults = recoveredFaults;
            NotifyChanged();
        }

        private void UpdateFault(float deltaTime)
        {
            if (!_config.EnableFault)
            {
                return;
            }

            if (_activeFault != RelayAFaultType.None)
            {
                if (_config.RequireFaultRecovery) return;
                _faultActiveRemaining = Mathf.Max(0f, _faultActiveRemaining - deltaTime);
                if (_faultActiveRemaining <= 0f)
                {
                    _activeFault = RelayAFaultType.None;
                    _scheduledFault = SelectRandomFault(_attemptRandom);
                    _faultTriggerAtSeconds = BuildNextFaultTime(
                        _elapsedRunningSeconds + _config.RepeatFaultMinDelaySeconds,
                        Mathf.Max(0f, _config.RepeatFaultMaxDelaySeconds - _config.RepeatFaultMinDelaySeconds));
                }

                return;
            }

            if (_warningFault != RelayAFaultType.None)
            {
                _faultWarningRemaining = Mathf.Max(0f, _faultWarningRemaining - deltaTime);
                if (_faultWarningRemaining <= 0f)
                {
                    _activeFault = _warningFault;
                    _warningFault = RelayAFaultType.None;
                    _faultActiveRemaining = _config.RequireFaultRecovery ? _config.FaultRecoveryHoldSeconds : _config.FaultDurationSeconds;
                    _recoverySeconds = 0f;
                    FaultActivated?.Invoke(_activeFault);
                }

                return;
            }

            if (_elapsedRunningSeconds >= _faultTriggerAtSeconds)
            {
                _warningFault = _config.RequireFaultRecovery
                    ? (RelayAFaultType)(1 + _faultOrderOffset)
                    : SelectFaultType(_randomizeAttempt);
                _faultWarningRemaining = _config.FaultWarningSeconds;
                FaultWarningStarted?.Invoke(_warningFault);
            }
        }

        private float BuildNextFaultTime(float baseTime, float randomWindow)
        {
            if (_config == null || !_config.EnableFault)
            {
                return float.PositiveInfinity;
            }

            if (_randomizeAttempt && _attemptRandom != null && _attemptRandom.NextDouble() > _config.FaultChance)
            {
                return float.PositiveInfinity;
            }

            return baseTime + (_randomizeAttempt ? Range(_attemptRandom, 0f, randomWindow) : 0f);
        }

        private RelayAFaultType SelectFaultType(bool randomizeAttempt)
        {
            int choice = randomizeAttempt
                ? (int)_scheduledFault - 1
                : Mathf.Abs(Mathf.RoundToInt(_controls.x * 13f + _controls.y * 7f + _controls.z * 5f)) % 3;
            switch (choice)
            {
                case 0:
                    return RelayAFaultType.Overvoltage;
                case 1:
                    return RelayAFaultType.FrequencyDesynchronization;
                default:
                    return RelayAFaultType.LoadImbalance;
            }
        }

        private static Vector3 RandomizeControls(Vector3 baseControls, System.Random random)
        {
            return new Vector3(
                Mathf.Clamp(baseControls.x + Range(random, -22f, 22f), 0f, 100f),
                Mathf.Clamp(baseControls.y + Range(random, -22f, 22f), 0f, 100f),
                Mathf.Clamp(baseControls.z + Range(random, -22f, 22f), 0f, 100f));
        }

        private static RelayAFaultType SelectRandomFault(System.Random random)
        {
            if (random == null)
            {
                random = new System.Random(Environment.TickCount);
            }

            switch (random.Next(0, 3))
            {
                case 0:
                    return RelayAFaultType.Overvoltage;
                case 1:
                    return RelayAFaultType.FrequencyDesynchronization;
                default:
                    return RelayAFaultType.LoadImbalance;
            }
        }

        private static float Range(System.Random random, float min, float max)
        {
            return min + (float)random.NextDouble() * (max - min);
        }

        private RelayAStabilizationSnapshot BuildSnapshot()
        {
            RelayAStabilizationOutputs target = _config != null
                ? EvaluateControlTarget(_controls, _activeFault, _elapsedRunningSeconds)
                : _outputs;
            bool stable = _config != null && _config.IsOutputStable(_outputs);
            bool dangerous = _config != null && _config.IsOutputDangerous(_outputs);
            RelayAStabilizationStatus status = DetermineStatus(stable, dangerous);
            return new RelayAStabilizationSnapshot(
                status,
                _controls,
                _outputs,
                target,
                Trend(_previousOutputs.Voltage, _outputs.Voltage),
                Trend(_previousOutputs.Frequency, _outputs.Frequency),
                Trend(_previousOutputs.LoadBalance, _outputs.LoadBalance),
                _stabilitySeconds,
                _config != null ? _config.StabilityRequiredSeconds : 12f,
                Mathf.Max(0f, (_config != null ? _config.InstabilityToleranceSeconds : 0.5f) - _instabilitySeconds),
                _warningFault,
                _activeFault,
                _faultWarningRemaining,
                _faultActiveRemaining,
                stable,
                dangerous,
                _running,
                _online,
                _recoveredFaults);
        }

        private RelayAStabilizationStatus DetermineStatus(bool stable, bool dangerous)
        {
            if (_online) return RelayAStabilizationStatus.Online;
            if (!_running) return RelayAStabilizationStatus.Offline;
            if (dangerous) return RelayAStabilizationStatus.Overload;
            if (_warningFault != RelayAFaultType.None || _activeFault != RelayAFaultType.None) return RelayAStabilizationStatus.FaultWarning;
            return stable ? RelayAStabilizationStatus.Stabilizing : RelayAStabilizationStatus.Calibrating;
        }

        private static RelayAReadingTrend Trend(float previous, float current)
        {
            float delta = current - previous;
            if (delta > 0.02f) return RelayAReadingTrend.Rising;
            if (delta < -0.02f) return RelayAReadingTrend.Falling;
            return RelayAReadingTrend.Stable;
        }

        private void NotifyChanged()
        {
            Changed?.Invoke(BuildSnapshot());
        }
    }
}
