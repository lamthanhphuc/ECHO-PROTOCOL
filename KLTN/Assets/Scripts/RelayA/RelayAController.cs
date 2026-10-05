using System;
using EchoProtocol.AI.Listener.Noise;
using EchoProtocol.Networking;
using EchoProtocol.Networking.Authority;
using Fusion;
using UnityEngine;

namespace EchoProtocol.RelayA
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class RelayAController : MonoBehaviour
    {
        [SerializeField] private RelayAConfig config;
        [SerializeField] private RelayAUIController ui;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip startupClip;
        [SerializeField] private AudioClip adjustClip;
        [SerializeField] private AudioClip warningClip;
        [SerializeField] private AudioClip overloadClip;
        [SerializeField] private AudioClip completeClip;
        [SerializeField, Min(0f)] private float adjustSoundCooldown = 0.12f;

        private readonly RelayACircuitSimulation _circuit = new RelayACircuitSimulation();
        private AudioSource _statusLoop;
        private int _attemptSeed;
        private float _nextAdjustSoundAt;
        private float _nextOverloadSoundAt;
        private int _lastBreakerPulseSequence = -1;

        public event Action RelayAOnline;

        public bool IsOnline => _circuit.IsOnline;
        public RelayACircuitSnapshot CircuitSnapshot => _circuit.Snapshot;
        public RelayACircuitSimulation Circuit => _circuit;
        public RelayAConfig Config => config;

        private void Awake()
        {
            if (GetComponent<EchoProtocol.Visuals.ObjectiveGlowHighlight>() == null)
                gameObject.AddComponent<EchoProtocol.Visuals.ObjectiveGlowHighlight>();
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
            EchoProtocol.Audio.GameAudioRuntime.EnsureInitialized();
            EchoProtocol.Audio.GameAudioSettings.RouteEffects(audioSource);
            if (audioSource != null) audioSource.spatialBlend = 1f;
            _statusLoop = EchoProtocol.Audio.GameAudioRuntime.CreateSource(gameObject, true);
            _statusLoop.maxDistance = 28f;
            if (ui == null) ui = GetComponentInChildren<RelayAUIController>(true);
            ui?.Bind(this);
            _circuit.Changed += HandleCircuitChanged;
            _circuit.Completed += HandleCompleted;
            _circuit.ProtectionTripped += HandleCircuitTrip;
            _circuit.FaultActivated += HandleCircuitFault;
            _circuit.Breakers.Completed += HandleMatrixBalanced;
            _circuit.Breakers.Changed += HandleBreakerChanged;
            _circuit.Stabilization.FaultWarningStarted += HandleStabilizationWarning;
            _circuit.Stabilization.FaultActivated += HandleStabilizationFault;
            _circuit.Stabilization.OverloadStarted += HandleCircuitTrip;
            _circuit.Initialize(config, 0);
        }

        private void OnDestroy()
        {
            if (_statusLoop != null) _statusLoop.Stop();
            _circuit.Changed -= HandleCircuitChanged;
            _circuit.Completed -= HandleCompleted;
            _circuit.ProtectionTripped -= HandleCircuitTrip;
            _circuit.FaultActivated -= HandleCircuitFault;
            _circuit.Breakers.Completed -= HandleMatrixBalanced;
            _circuit.Breakers.Changed -= HandleBreakerChanged;
            _circuit.Stabilization.FaultWarningStarted -= HandleStabilizationWarning;
            _circuit.Stabilization.FaultActivated -= HandleStabilizationFault;
            _circuit.Stabilization.OverloadStarted -= HandleCircuitTrip;
        }

        private void Update()
        {
            var match = NetworkMatchState.Instance;
            if (match != null && match.Object != null && match.Object.IsValid && !match.Object.HasStateAuthority) return;
            _circuit.Tick(Time.deltaTime);
        }

        public void OpenUI(GameObject interactor)
        {
            ui?.Open(interactor);
            ui?.RefreshCircuit(_circuit.Snapshot);
        }

        public void CloseUI() => ui?.Close();

        public bool RotateCircuitTile(int cellIndex)
        {
            bool changed = _circuit.Rotate(cellIndex);
            if (changed) PlayAdjustSound();
            return changed;
        }

        public bool TestCircuit()
        {
            bool accepted = _circuit.TestCircuit();
            if (accepted)
            {
                PlayOneShot(startupClip, 0.9f, "power_puzzle/breaker_toggle");
                EmitAuthoritativeNoise(RuntimeNoiseType.MACHINE_REPAIR);
            }
            return accepted;
        }

        public bool PressBreaker(int cell)
        {
            return _circuit.PressBreaker(cell);
        }

        private void HandleBreakerChanged()
        {
            var state = _circuit.Breakers.Snapshot;
            if (state.Sequence == 0) _lastBreakerPulseSequence = -1;
            if (state.Phase != RelayABreakerPhase.Pulsing || state.Sequence == _lastBreakerPulseSequence) return;
            _lastBreakerPulseSequence = state.Sequence;
            PlayOneShot(adjustClip, 0.35f, "power_puzzle/breaker_toggle");
        }

        public void PlayBreakerDenied() => PlayOneShot(warningClip, 0.25f, "security_terminal/access_denied");

        private void HandleMatrixBalanced()
        {
            PlayOneShot(completeClip, 0.6f, "sector_box_power_hub/fully_powered");
            EmitAuthoritativeNoise(RuntimeNoiseType.POWER_SURGE);
        }

        public bool ResetBreakers()
        {
            if (!_circuit.ResetBreakers()) return false;
            PlayOneShot(startupClip, 0.5f, "power_puzzle/breaker_toggle");
            EmitAuthoritativeNoise(RuntimeNoiseType.MACHINE_REPAIR);
            return true;
        }

        public void ApplyAuthoritativeCircuitScenario(int scenarioIndex, int attemptSeed)
        {
            if (attemptSeed == 0 || _attemptSeed == attemptSeed || IsOnline) return;
            _attemptSeed = attemptSeed;
            _circuit.Initialize(config, scenarioIndex, attemptSeed);
        }

        public void ApplyAuthoritativeCircuitState(int scenarioIndex, ulong rotations,
            RelayACircuitPhase phase, ulong powered, int missingTargets, bool faultPowered,
            int testSequence, bool faultActive)
        {
            _circuit.ApplyAuthoritative(scenarioIndex, rotations, phase, powered,
                missingTargets, faultPowered, testSequence, faultActive);
        }

        public void ApplyOnlineFromAuthority() => _circuit.ForceOnline();

        public bool SetControls(float generator, float frequency, float load)
        {
            bool accepted = _circuit.SetStabilizationControls(generator, frequency, load);
            if (accepted) PlayAdjustSound();
            return accepted;
        }

        public bool StartStabilization()
        {
            if (!_circuit.SetStabilizationRunning(true)) return false;
            PlayOneShot(startupClip, 0.9f, "power_puzzle/breaker_toggle");
            EmitAuthoritativeNoise(RuntimeNoiseType.MACHINE_REPAIR);
            return true;
        }

        public bool EmergencyStop()
        {
            if (!_circuit.SetStabilizationRunning(false)) return false;
            PlayOneShot(warningClip, 0.5f, "security_terminal/download_pause");
            return true;
        }

        private void HandleStabilizationWarning(RelayAFaultType fault) => HandleCircuitFault();
        private void HandleStabilizationFault(RelayAFaultType fault) => HandleCircuitTrip();

        public void ResetForRetry(int attemptSeed = 0)
        {
            ui?.Close();
            _attemptSeed = 0;
            _circuit.Initialize(config, _circuit.Snapshot.ScenarioIndex);
        }

        private void HandleCircuitChanged(RelayACircuitSnapshot snapshot)
        {
            EchoProtocol.Audio.GameAudioRuntime.Loop(_statusLoop,
                snapshot.Phase == RelayACircuitPhase.Testing || (snapshot.Phase == RelayACircuitPhase.StabilizeOutput
                    && _circuit.Stabilization.Snapshot.IsRunning) ? "objectives/relay_a_motor_loop" : null, 0.72f);
            ui?.RefreshCircuit(snapshot);
        }

        private void HandleCompleted()
        {
            PlayOneShot(completeClip, 0.9f, "sector_box_power_hub/fully_powered");
            EmitAuthoritativeNoise(RuntimeNoiseType.POWER_SURGE);
            ui?.Close();
            RelayAOnline?.Invoke();
        }

        private void HandleCircuitTrip()
        {
            PlayOverloadSound();
            EmitAuthoritativeNoise(RuntimeNoiseType.MACHINE_OVERLOAD);
        }

        private void HandleCircuitFault()
        {
            PlayOneShot(warningClip, 0.8f, "map_ambience/electrical_flicker");
            EmitAuthoritativeNoise(RuntimeNoiseType.POWER_SURGE);
        }

        private void PlayOverloadSound()
        {
            if (Time.unscaledTime < _nextOverloadSoundAt) return;
            _nextOverloadSoundAt = Time.unscaledTime + 0.45f;
            PlayOneShot(overloadClip, 0.85f, "objectives/relay_overload");
        }

        private void PlayAdjustSound()
        {
            if (Time.unscaledTime < _nextAdjustSoundAt) return;
            _nextAdjustSoundAt = Time.unscaledTime + adjustSoundCooldown;
            PlayOneShot(adjustClip, 0.35f, "power_puzzle/rotary_switch");
        }

        private void PlayOneShot(AudioClip clip, float volume, string fallbackKey)
        {
            if (audioSource != null && clip != null) audioSource.PlayOneShot(clip, volume);
            else EchoProtocol.Audio.GameAudioRuntime.Play(audioSource, fallbackKey, volume);
        }

        private void EmitAuthoritativeNoise(RuntimeNoiseType type)
        {
            try
            {
                var authority = MatchAuthorityRuntime.Instance;
                if (authority == null || !authority.HasStateAuthority) return;
                var key = new RuntimeNoiseSourceOccurrenceKey($"RelayA_{gameObject.name}", Time.frameCount);
                HostRuntimeNoiseService.EnsureExists(authority)
                    .TryAccept(PlayerRef.None, type, key, transform.position, out _);
            }
            catch
            {
                // Offline EditMode tests do not have network authority.
            }
        }
    }
}
