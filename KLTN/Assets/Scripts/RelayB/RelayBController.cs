using System;
using EchoProtocol.AI.Listener.Noise;
using EchoProtocol.Networking.Authority;
using Fusion;
using UnityEngine;

namespace EchoProtocol.RelayB
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class RelayBController : MonoBehaviour
    {
        [SerializeField] private RelayBConfig config;
        [SerializeField] private RelayBUIController ui;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip startupClip;
        [SerializeField] private AudioClip adjustClip;
        [SerializeField] private AudioClip warningClip;
        [SerializeField] private AudioClip mismatchClip;
        [SerializeField] private AudioClip completeClip;
        [SerializeField, Min(0f)] private float adjustSoundCooldown = 0.12f;
        [SerializeField, Min(0f)] private float mismatchSoundCooldown = 1.0f;
        [SerializeField, Min(0f)] private float noiseEmissionCooldown = 2.0f;

        private readonly RelayBSignalSimulation _simulation = new RelayBSignalSimulation();
        private float _nextAdjustSoundAt;
        private float _nextMismatchSoundAt;
        private float _nextNoiseEmissionAt;
        private int _presetIndex = 0;

        public event Action<RelayBSnapshot> StateChanged;
        public event Action RelayBOnline;
        public event Action<int, float, float> ControlsChanged;
        public event Action StartSyncRequested;
        public event Action CancelSyncRequested;

        public bool IsOnline => _simulation.Snapshot.IsOnline;
        public RelayBSnapshot Snapshot => _simulation.Snapshot;
        public RelayBConfig Config => config;
        public RelayBSignalSimulation Simulation => _simulation;

        private void Awake()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }

            if (ui == null)
            {
                ui = GetComponentInChildren<RelayBUIController>(true);
            }

            if (ui != null)
            {
                ui.Bind(this);
            }

            _simulation.Changed += HandleSimulationChanged;
            _simulation.Completed += HandleCompleted;
            _simulation.DriftWarning += HandleDriftWarning;
            _simulation.DriftTriggered += HandleDriftTriggered;
            _simulation.SignalMismatchOccurred += HandleSignalMismatch;
            _simulation.InstabilityReset += HandleInstabilityReset;

            _simulation.Initialize(config, _presetIndex);
        }

        private void OnDestroy()
        {
            _simulation.Changed -= HandleSimulationChanged;
            _simulation.Completed -= HandleCompleted;
            _simulation.DriftWarning -= HandleDriftWarning;
            _simulation.DriftTriggered -= HandleDriftTriggered;
            _simulation.SignalMismatchOccurred -= HandleSignalMismatch;
            _simulation.InstabilityReset -= HandleInstabilityReset;
        }

        private void Update()
        {
            _simulation.Tick(Time.deltaTime);
        }

        public void SetPresetIndex(int presetIndex)
        {
            _presetIndex = Mathf.Max(0, presetIndex);
            if (!_simulation.IsOnline)
            {
                _simulation.Initialize(config, _presetIndex);
            }
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

        public void SelectChannel(int channelIndex)
        {
            _simulation.SelectChannel(channelIndex);
            PlayAdjustSound();
            ControlsChanged?.Invoke(_simulation.SelectedChannelIndex, _simulation.CurrentFrequency, _simulation.CurrentPhase);
        }

        public void SetFrequency(float frequency)
        {
            _simulation.SetFrequency(frequency);
            PlayAdjustSound();
            ControlsChanged?.Invoke(_simulation.SelectedChannelIndex, _simulation.CurrentFrequency, _simulation.CurrentPhase);
        }

        public void SetPhase(float phase)
        {
            _simulation.SetPhase(phase);
            PlayAdjustSound();
            ControlsChanged?.Invoke(_simulation.SelectedChannelIndex, _simulation.CurrentFrequency, _simulation.CurrentPhase);
        }

        public void ScanChannels()
        {
            _simulation.ScanChannels(1.0f);
            PlayOneShot(startupClip, 0.75f);
        }

        public void StartSynchronization()
        {
            if (IsOnline)
            {
                return;
            }

            _simulation.StartSynchronization();
            PlayOneShot(startupClip, 0.85f);
            StartSyncRequested?.Invoke();
        }

        public void CancelSynchronization()
        {
            _simulation.CancelSynchronization();
            PlayOneShot(warningClip, 0.5f);
            CancelSyncRequested?.Invoke();
        }

        public void ApplyAuthoritativeControls(int channelIndex, float frequency, float phase)
        {
            if (channelIndex != _simulation.SelectedChannelIndex)
            {
                _simulation.SelectChannel(channelIndex);
            }

            _simulation.SetFrequency(frequency);
            _simulation.SetPhase(phase);
        }

        public void ApplyOnlineFromAuthority()
        {
            _simulation.ForceCompleteForAuthoritativeSync();
        }

        private void HandleSimulationChanged(RelayBSnapshot snapshot)
        {
            ui?.Refresh(snapshot);
            StateChanged?.Invoke(snapshot);
        }

        private void HandleCompleted()
        {
            PlayOneShot(completeClip, 0.95f);
            RelayBOnline?.Invoke();
        }

        private void HandleDriftWarning()
        {
            PlayOneShot(warningClip, 0.8f);
        }

        private void HandleDriftTriggered()
        {
            PlayOneShot(warningClip, 0.9f);
        }

        private void HandleSignalMismatch()
        {
            if (Time.unscaledTime >= _nextMismatchSoundAt)
            {
                _nextMismatchSoundAt = Time.unscaledTime + mismatchSoundCooldown;
                PlayOneShot(mismatchClip != null ? mismatchClip : warningClip, 0.75f);
            }

            TryEmitNoiseEvent();
        }

        private void HandleInstabilityReset()
        {
            PlayOneShot(warningClip, 0.8f);
            TryEmitNoiseEvent();
        }

        private void PlayAdjustSound()
        {
            if (Time.unscaledTime >= _nextAdjustSoundAt)
            {
                _nextAdjustSoundAt = Time.unscaledTime + adjustSoundCooldown;
                PlayOneShot(adjustClip, 0.35f);
            }
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

            var noiseService = FindAnyObjectByType<HostRuntimeNoiseService>();
            if (noiseService != null)
            {
                var key = RuntimeNoiseSourceOccurrenceKey.ForInteraction(
                    "RELAY_B",
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
