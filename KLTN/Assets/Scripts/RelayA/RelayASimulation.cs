using System;
using UnityEngine;

namespace EchoProtocol.RelayA
{
    [Serializable]
    public sealed class RelayASimulation
    {
        private RelayAConfig _config;
        private Vector3 _controls;
        private RelayAOutputs _outputs;
        private RelayAOutputs _previousOutputs;
        private float _elapsedRunningSeconds;
        private float _stabilitySeconds;
        private float _instabilitySeconds;
        private float _faultWarningRemaining;
        private float _faultActiveRemaining;
        private bool _running;
        private bool _online;
        private bool _faultConsumed;
        private RelayAFaultType _warningFault;
        private RelayAFaultType _activeFault;

        public event Action<RelayASnapshot> Changed;
        public event Action Completed;
        public event Action<RelayAFaultType> FaultWarningStarted;
        public event Action<RelayAFaultType> FaultActivated;
        public event Action OverloadStarted;

        public RelayASnapshot Snapshot => BuildSnapshot();

        public void Initialize(RelayAConfig config)
        {
            _config = config;
            _controls = config != null ? config.InitialControls : new Vector3(50f, 50f, 50f);
            _outputs = config != null
                ? config.EvaluateTarget(_controls, RelayAFaultType.None, 0f)
                : new RelayAOutputs(225f, 50f, 50f);
            _previousOutputs = _outputs;
            _elapsedRunningSeconds = 0f;
            _stabilitySeconds = 0f;
            _instabilitySeconds = 0f;
            _faultWarningRemaining = 0f;
            _faultActiveRemaining = 0f;
            _running = false;
            _online = false;
            _faultConsumed = false;
            _warningFault = RelayAFaultType.None;
            _activeFault = RelayAFaultType.None;
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
            _stabilitySeconds = 0f;
            _instabilitySeconds = 0f;
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

            RelayAOutputs target = _config.EvaluateTarget(_controls, _activeFault, _elapsedRunningSeconds);
            float response = 1f - Mathf.Exp(-deltaTime / Mathf.Max(0.05f, _config.ResponseDelaySeconds));
            _outputs = new RelayAOutputs(
                Mathf.Lerp(_outputs.Voltage, target.Voltage, response),
                Mathf.Lerp(_outputs.Frequency, target.Frequency, response),
                Mathf.Lerp(_outputs.LoadBalance, target.LoadBalance, response));

            bool stable = _config.IsOutputStable(_outputs);
            bool dangerous = _config.IsOutputDangerous(_outputs);

            if (_running && dangerous)
            {
                if (_stabilitySeconds > 0f)
                {
                    OverloadStarted?.Invoke();
                }

                _stabilitySeconds = 0f;
                _instabilitySeconds = 0f;
            }
            else if (_running && stable)
            {
                _instabilitySeconds = 0f;
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
                _instabilitySeconds += deltaTime;
                if (_instabilitySeconds > _config.InstabilityToleranceSeconds)
                {
                    _stabilitySeconds = 0f;
                }
            }

            NotifyChanged();
        }

        public void ForceCompleteForAuthoritativeSync()
        {
            _online = true;
            _running = false;
            _stabilitySeconds = _config != null ? _config.StabilityRequiredSeconds : 12f;
            _warningFault = RelayAFaultType.None;
            _activeFault = RelayAFaultType.None;
            NotifyChanged();
        }

        private void UpdateFault(float deltaTime)
        {
            if (!_config.EnableFault || _faultConsumed)
            {
                return;
            }

            if (_activeFault != RelayAFaultType.None)
            {
                _faultActiveRemaining = Mathf.Max(0f, _faultActiveRemaining - deltaTime);
                if (_faultActiveRemaining <= 0f)
                {
                    _activeFault = RelayAFaultType.None;
                    _faultConsumed = true;
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
                    _faultActiveRemaining = _config.FaultDurationSeconds;
                    FaultActivated?.Invoke(_activeFault);
                }

                return;
            }

            if (_elapsedRunningSeconds >= _config.EarliestFaultAtSeconds)
            {
                _warningFault = SelectFaultType();
                _faultWarningRemaining = _config.FaultWarningSeconds;
                FaultWarningStarted?.Invoke(_warningFault);
            }
        }

        private RelayAFaultType SelectFaultType()
        {
            int seed = Mathf.RoundToInt(_controls.x * 13f + _controls.y * 7f + _controls.z * 5f);
            switch (Mathf.Abs(seed) % 3)
            {
                case 0:
                    return RelayAFaultType.Overvoltage;
                case 1:
                    return RelayAFaultType.FrequencyDesynchronization;
                default:
                    return RelayAFaultType.LoadImbalance;
            }
        }

        private RelayASnapshot BuildSnapshot()
        {
            RelayAOutputs target = _config != null
                ? _config.EvaluateTarget(_controls, _activeFault, _elapsedRunningSeconds)
                : _outputs;
            bool stable = _config != null && _config.IsOutputStable(_outputs);
            bool dangerous = _config != null && _config.IsOutputDangerous(_outputs);
            RelayAStatus status = DetermineStatus(stable, dangerous);
            return new RelayASnapshot(
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
                _online);
        }

        private RelayAStatus DetermineStatus(bool stable, bool dangerous)
        {
            if (_online) return RelayAStatus.Online;
            if (!_running) return RelayAStatus.Offline;
            if (dangerous) return RelayAStatus.Overload;
            if (_warningFault != RelayAFaultType.None || _activeFault != RelayAFaultType.None) return RelayAStatus.FaultWarning;
            return stable ? RelayAStatus.Stabilizing : RelayAStatus.Calibrating;
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
