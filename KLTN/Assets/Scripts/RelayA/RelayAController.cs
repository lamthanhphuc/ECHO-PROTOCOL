using System;
using EchoProtocol.AI.Listener.Noise;
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
        [SerializeField, Min(0f)] private float noiseEmissionCooldown = 2.0f;

        private readonly RelayASimulation _simulation = new RelayASimulation();
        private float _nextAdjustSoundAt;
        private float _nextNoiseEmissionAt;

        public event Action<RelayASnapshot> StateChanged;
        public event Action RelayAOnline;

        public bool IsOnline => _simulation.Snapshot.IsOnline;
        public RelayASnapshot Snapshot => _simulation.Snapshot;
        public RelayAConfig Config => config;
        public RelayASimulation Simulation => _simulation;

        private void Awake()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }

            if (ui == null)
            {
                ui = GetComponentInChildren<RelayAUIController>(true);
            }

            if (ui != null)
            {
                ui.Bind(this);
            }

            _simulation.Changed += HandleSimulationChanged;
            _simulation.Completed += HandleCompleted;
            _simulation.FaultWarningStarted += HandleFaultWarningStarted;
            _simulation.FaultActivated += HandleFaultActivated;
            _simulation.OverloadStarted += HandleOverloadStarted;
            _simulation.Initialize(config);
        }

        private void OnDestroy()
        {
            _simulation.Changed -= HandleSimulationChanged;
            _simulation.Completed -= HandleCompleted;
            _simulation.FaultWarningStarted -= HandleFaultWarningStarted;
            _simulation.FaultActivated -= HandleFaultActivated;
            _simulation.OverloadStarted -= HandleOverloadStarted;
        }

        private void Update()
        {
            _simulation.Tick(Time.deltaTime);
        }

        public void OpenUI(GameObject interactor)
        {
            if (ui == null)
            {
                return;
            }

            ui.Open(interactor);
            ui.Refresh(_simulation.Snapshot);
        }

        public void CloseUI()
        {
            if (ui != null)
            {
                ui.Close();
            }
        }

        public void StartStabilization()
        {
            if (IsOnline)
            {
                return;
            }

            _simulation.Start();
            PlayOneShot(startupClip, 0.85f);
            TryEmitNoiseEvent();
        }

        public void EmergencyStop()
        {
            _simulation.EmergencyStop();
            PlayOneShot(warningClip, 0.5f);
        }

        public void SetControls(float generatorOutput, float frequencyRegulator, float loadDistribution)
        {
            _simulation.SetControls(generatorOutput, frequencyRegulator, loadDistribution);
            if (Time.unscaledTime >= _nextAdjustSoundAt)
            {
                _nextAdjustSoundAt = Time.unscaledTime + adjustSoundCooldown;
                PlayOneShot(adjustClip, 0.35f);
            }
        }

        public void ApplyAuthoritativeControls(
            float generatorOutput,
            float frequencyRegulator,
            float loadDistribution)
        {
            var controls = new Vector3(generatorOutput, frequencyRegulator, loadDistribution);
            if ((_simulation.Snapshot.Controls - controls).sqrMagnitude <= 0.000001f)
            {
                return;
            }

            _simulation.SetControls(controls.x, controls.y, controls.z);
        }

        public void ApplyAuthoritativeRunningState(bool running)
        {
            var snapshot = _simulation.Snapshot;
            if (snapshot.IsOnline || snapshot.IsRunning == running)
            {
                return;
            }

            if (running) _simulation.Start();
            else _simulation.EmergencyStop();
        }

        public void ApplyOnlineFromAuthority()
        {
            if (_simulation.Snapshot.IsOnline) return;
            _simulation.ForceCompleteForAuthoritativeSync();
        }

        private void HandleSimulationChanged(RelayASnapshot snapshot)
        {
            ui?.Refresh(snapshot);
            StateChanged?.Invoke(snapshot);
        }

        private void HandleCompleted()
        {
            PlayOneShot(completeClip, 0.9f);
            TryEmitNoiseEvent();
            RelayAOnline?.Invoke();
        }

        private void HandleFaultWarningStarted(RelayAFaultType faultType)
        {
            PlayOneShot(warningClip, 0.8f);
        }

        private void HandleFaultActivated(RelayAFaultType faultType)
        {
            PlayOneShot(overloadClip, 0.85f);
        }

        private void HandleOverloadStarted()
        {
            PlayOneShot(overloadClip, 0.85f);
            TryEmitNoiseEvent();
        }

        private void PlayOneShot(AudioClip clip, float volume)
        {
            if (audioSource != null && clip != null)
            {
                audioSource.PlayOneShot(clip, volume);
            }
        }

        private void TryEmitNoiseEvent()
        {
            if (Time.unscaledTime < _nextNoiseEmissionAt)
            {
                return;
            }

            _nextNoiseEmissionAt = Time.unscaledTime + noiseEmissionCooldown;

            var noiseService = UnityEngine.Object.FindAnyObjectByType<HostRuntimeNoiseService>();
            if (noiseService != null)
            {
                var key = RuntimeNoiseSourceOccurrenceKey.ForInteraction(
                    "RELAY_A",
                    (uint)Mathf.FloorToInt(Time.time * 10f));

                noiseService.TryAccept(
                    PlayerRef.None,
                    RuntimeNoiseType.INTERACTION,
                    key,
                    transform.position,
                    out _);
            }
        }
    }
}
