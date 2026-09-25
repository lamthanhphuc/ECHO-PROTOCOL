using System;
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

        private readonly RelayASimulation _simulation = new RelayASimulation();
        private AudioSource _statusLoop;
        private float _nextAdjustSoundAt;
        private int _attemptSeed;

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
            EchoProtocol.Audio.GameAudioRuntime.EnsureInitialized();
            if (audioSource != null) audioSource.spatialBlend = 1f;
            _statusLoop = EchoProtocol.Audio.GameAudioRuntime.CreateSource(gameObject, true);
            _statusLoop.maxDistance = 28f;

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
            _attemptSeed = NewAttemptSeed();
            _simulation.Initialize(config, true, _attemptSeed);
        }

        private void OnDestroy()
        {
            if (_statusLoop != null) _statusLoop.Stop();
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
            PlayOneShot(startupClip, 0.85f, "power_puzzle/breaker_toggle");
        }

        public void EmergencyStop()
        {
            _simulation.EmergencyStop();
            PlayOneShot(warningClip, 0.5f, "security_terminal/download_pause");
        }

        public void SetControls(float generatorOutput, float frequencyRegulator, float loadDistribution)
        {
            _simulation.SetControls(generatorOutput, frequencyRegulator, loadDistribution);
            if (Time.unscaledTime >= _nextAdjustSoundAt)
            {
                _nextAdjustSoundAt = Time.unscaledTime + adjustSoundCooldown;
                PlayOneShot(adjustClip, 0.35f, "power_puzzle/rotary_switch");
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

        public void ApplyAuthoritativeAttemptSeed(int attemptSeed)
        {
            if (attemptSeed == 0 || _attemptSeed == attemptSeed || _simulation.Snapshot.IsOnline)
            {
                return;
            }

            _attemptSeed = attemptSeed;
            _simulation.Initialize(config, true, _attemptSeed);
            ui?.Refresh(_simulation.Snapshot);
        }

        public void ResetForRetry(int attemptSeed = 0)
        {
            ui?.Close();
            _attemptSeed = attemptSeed != 0 ? attemptSeed : NewAttemptSeed();
            _simulation.Initialize(config, true, _attemptSeed);
            ui?.Refresh(_simulation.Snapshot);
        }

        private static int NewAttemptSeed()
        {
            int seed = UnityEngine.Random.Range(1, int.MaxValue);
            return seed == 0 ? 1 : seed;
        }

        private void HandleSimulationChanged(RelayASnapshot snapshot)
        {
            string loop = snapshot.Status == RelayAStatus.Online ? "sector_box_power_hub/idle_machinery_loop"
                : snapshot.Status == RelayAStatus.FaultWarning || snapshot.Status == RelayAStatus.Overload
                    ? "map_ambience/alarm_ambience_loop"
                : snapshot.IsRunning ? "map_ambience/generator_loop"
                : "map_ambience/electrical_room_loop";
            float volume = snapshot.IsRunning && !snapshot.IsOnline ? 0.72f : 0.1f;
            EchoProtocol.Audio.GameAudioRuntime.Loop(_statusLoop, loop, volume);
            ui?.Refresh(snapshot);
            StateChanged?.Invoke(snapshot);
        }

        private void HandleCompleted()
        {
            PlayOneShot(completeClip, 0.9f, "sector_box_power_hub/fully_powered");
            ui?.Close();
            RelayAOnline?.Invoke();
        }

        private void HandleFaultWarningStarted(RelayAFaultType faultType)
        {
            PlayOneShot(warningClip, 0.8f, "map_ambience/electrical_flicker");
        }

        private void HandleFaultActivated(RelayAFaultType faultType)
        {
            PlayOneShot(overloadClip, 0.85f, "power_puzzle/electrical_sparks");
        }

        private void HandleOverloadStarted()
        {
            PlayOneShot(overloadClip, 0.85f, "power_puzzle/electrical_sparks");
        }

        private void PlayOneShot(AudioClip clip, float volume, string fallbackKey)
        {
            if (audioSource != null && clip != null)
            {
                audioSource.PlayOneShot(clip, volume);
            }
            else EchoProtocol.Audio.GameAudioRuntime.Play(audioSource, fallbackKey, volume);
        }

    }
}
