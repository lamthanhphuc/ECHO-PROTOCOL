using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EchoProtocol.AI.Listener.Noise;
using EchoProtocol.Networking.Authority;
using EchoProtocol.Networking;
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

        private readonly RelayBSignalSimulation _simulation = new RelayBSignalSimulation();
        private AudioSource _statusLoop;
        private float _nextAdjustSoundAt;
        private float _nextMismatchSoundAt;
        private int _presetIndex = 0;
        private int _attemptSeed;
        private readonly AudioSource[] _decoderFeedbackSources = new AudioSource[3];
        private int _surgeDifficulty=1, _surgeVariant;
        public RelayBSurgeSimulation Surge => _simulation.Surge;

        // Only pure C# board generation runs on workers; simulation/events stay on Unity's thread.
        private readonly Dictionary<int, Task<RelayBSurgeBoard>> _preparedBoards = new Dictionary<int, Task<RelayBSurgeBoard>>();
        private int _preparedSeed, _preparedDifficulty=-1, _preparedVariant=-1;
        private bool _preparingSurge;
        private Task<RelayBSurgeBoard> _reportedGenerationFailure;
        public bool IsPreparingSurge => _preparingSurge || (Surge.IsFailed
            && _preparedBoards.TryGetValue(Surge.Attempt+1,out var next) && !next.IsCompleted);

        private Task<RelayBSurgeBoard> PrepareSurge(int seed,int difficulty,int variant,int attempt)
        {
            if (_preparedSeed!=seed || _preparedDifficulty!=difficulty || _preparedVariant!=variant) {
                _preparedBoards.Clear(); _preparedSeed=seed; _preparedDifficulty=difficulty; _preparedVariant=variant;
            }
            if (!_preparedBoards.TryGetValue(attempt,out var task)) {
                task=Task.Run(()=>RelayBSurgeGenerator.Generate(seed,difficulty,variant,attempt));
                _preparedBoards.Add(attempt,task);
            }
            return task;
        }
        private bool EnsureSurge(int seed,int difficulty,int variant,int attempt)
        {
            var task=PrepareSurge(seed,difficulty,variant,attempt);
            _preparingSurge=!task.IsCompleted;
            if (!task.IsCompleted) return false;
            if (task.IsFaulted) {
                _preparingSurge=true;
                if (_reportedGenerationFailure!=task) {
                    _reportedGenerationFailure=task;Debug.LogException(task.Exception,this);
                }
                return false;
            }
            if (Surge.Board==null || Surge.Board.Seed!=seed || Surge.Attempt!=attempt
                || Surge.Board.Difficulty!=difficulty || Surge.Board.Variant!=variant) Surge.Reset(task.Result,attempt);
            // Keep only this board and one retry, not an ever-growing cache.
            _preparedBoards.Remove(attempt-1);
            PrepareSurge(seed,difficulty,variant,attempt+1);
            return true;
        }

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
            EchoProtocol.Audio.GameAudioRuntime.RegisterEnvironmentOwner(this);
            if (GetComponent<EchoProtocol.Visuals.ObjectiveGlowHighlight>() == null)
            {
                gameObject.AddComponent<EchoProtocol.Visuals.ObjectiveGlowHighlight>();
            }

            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }
            EchoProtocol.Audio.GameAudioRuntime.EnsureInitialized();
            EchoProtocol.Audio.GameAudioSettings.RouteEffects(audioSource);
            if (audioSource != null) audioSource.spatialBlend = 1f;
            _statusLoop = EchoProtocol.Audio.GameAudioRuntime.CreateSource(gameObject, true);
            _statusLoop.maxDistance = 28f;

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
            _simulation.Decoder.Failed += HandleDecoderFailed;
            _simulation.Decoder.Completed += HandleDecoderCompleted;
            Surge.Changed += HandleSurgeChanged;
            Surge.Pulsed += HandleSurgePulse;
            Surge.CriticalOverload += HandleSurgeCritical;
            Surge.Failed += HandleSurgeFailed;
            Surge.Completed += HandleSurgeCompleted;

            RandomizePresetIndex();
            _attemptSeed = NewAttemptSeed();
            InitializeAttempt();
        }

        private void OnDestroy()
        {
            if (_statusLoop != null) _statusLoop.Stop();
            _simulation.Changed -= HandleSimulationChanged;
            _simulation.Completed -= HandleCompleted;
            _simulation.DriftWarning -= HandleDriftWarning;
            _simulation.DriftTriggered -= HandleDriftTriggered;
            _simulation.SignalMismatchOccurred -= HandleSignalMismatch;
            _simulation.InstabilityReset -= HandleInstabilityReset;
            _simulation.Decoder.Failed -= HandleDecoderFailed;
            _simulation.Decoder.Completed -= HandleDecoderCompleted;
            Surge.Changed -= HandleSurgeChanged;
            Surge.Pulsed -= HandleSurgePulse;
            Surge.CriticalOverload -= HandleSurgeCritical;
            Surge.Failed -= HandleSurgeFailed;
            Surge.Completed -= HandleSurgeCompleted;
        }

        private void Update()
        {
            var matchState = NetworkMatchState.Instance;
            // Network simulation advances only in FixedUpdateNetwork.
            if (matchState != null && matchState.Object != null && matchState.Object.IsValid) return;
            EnsureSurge(_attemptSeed,_surgeDifficulty,_surgeVariant,Surge.Board==null ? 0 : Surge.Attempt);
            Surge.Tick(Time.deltaTime);
            _simulation.Tick(Time.deltaTime);
        }

        public void TickAuthoritative(float dt) { Surge.Tick(dt);_simulation.Tick(dt); }
        public void ConfigureSurge(int difficulty,int variant)
        {
            if(_surgeDifficulty==difficulty && _surgeVariant==variant) {
                EnsureSurge(_attemptSeed,difficulty,variant,Surge.Board==null ? 0 : Surge.Attempt);return;
            }
            _surgeDifficulty=difficulty;_surgeVariant=variant;
            EnsureSurge(_attemptSeed,difficulty,variant,0);
        }
        public bool StartSurge()
        {
            if(!EnsureSurge(_attemptSeed,_surgeDifficulty,_surgeVariant,Surge.IsFailed ? Surge.Attempt+1 : Surge.Attempt))return false;
            if(!Surge.Start())return false;
            PlayOneShot(startupClip,0.7f,"security_terminal/terminal_boot");
            EmitAuthoritativeNoise(RuntimeNoiseType.MACHINE_REPAIR);return true;
        }
        public bool PlaceSurgeInsulation(int cell,int attempt)
        {
            if(attempt!=Surge.Attempt || !Surge.Place(cell))return false;
            PlayAdjustSound();return true;
        }
        public void ApplyAuthoritativeSurge(RelayBSurgeTelemetry state)
        {
            if(state.Seed==0)return;
            _surgeDifficulty=state.Difficulty;_surgeVariant=state.Variant;
            if(Surge.Board==null || Surge.Board.Seed!=state.Seed || Surge.Attempt!=state.Attempt
                || Surge.Board.Difficulty!=state.Difficulty || Surge.Board.Variant!=state.Variant)
                if(!EnsureSurge(state.Seed,state.Difficulty,state.Variant,state.Attempt))return;
            if(state.PulseSequence>Surge.PulseSequence)HandleSurgePulse();
            if((RelayBSurgePhase)state.Phase>=RelayBSurgePhase.CoreLost && !Surge.IsFailed)
                PlayOneShot(mismatchClip,0.8f,"objectives/relay_b_desync");
            bool newlyContained=(RelayBSurgePhase)state.Phase==RelayBSurgePhase.Contained && Surge.Phase!=RelayBSurgePhase.Contained;
            Surge.ApplyRemote((RelayBSurgePhase)state.Phase,
                new RelayBSurgeMask{Low=state.InfectedLow,High=state.InfectedHigh},
                new RelayBSurgeMask{Low=state.InsulatedLow,High=state.InsulatedHigh},
                state.Elapsed,state.PulseRemaining,state.Cooldown,state.PulseSequence);
            _simulation.CompleteSurge();
            if(newlyContained)PresentSurgeContained();
        }
        private void HandleSurgeChanged()=>HandleSimulationChanged(_simulation.Snapshot);
        private void HandleSurgePulse()=>PlayOneShot(adjustClip,0.25f,"ui/click");
        private void HandleSurgeCritical()
        {
            PlayOneShot(warningClip,0.65f,"objectives/relay_b_desync");
            EmitAuthoritativeNoise(RuntimeNoiseType.MACHINE_OVERLOAD);
        }
        private void HandleSurgeFailed()
        {
            PlayOneShot(mismatchClip,0.8f,"objectives/relay_b_desync");
            EmitAuthoritativeNoise(RuntimeNoiseType.MACHINE_OVERLOAD);
        }
        private void HandleSurgeCompleted()
        {
            _simulation.CompleteSurge();PresentSurgeContained();
        }
        private void PresentSurgeContained()
        {
            PlayOneShot(completeClip,0.65f,"security_terminal/access_granted");
            if(ui!=null && ui.IsOpen)EchoProtocol.UI.GameUIFeedback.Instance.Toast(
                EchoProtocol.Settings.GameLanguage.Choose("Xung điện đã cô lập · Mở giải mã","Surge contained · Decoder unlocked"));
        }

        public void SetPresetIndex(int presetIndex)
        {
            _presetIndex = Mathf.Max(0, presetIndex);
            if (!_simulation.IsOnline)
            {
                _attemptSeed = NewAttemptSeed();
                InitializeAttempt();
            }
        }

        public void OpenUI(GameObject interactor)
        {
            if (ui == null) return;
            ui.Open(interactor);
            ui.Refresh(_simulation.Snapshot);
        }

        public void CloseUI()
        {
            if (ui != null) ui.Close();
        }

        public void SetActiveTab(int tabIndex)
        {
            _simulation.SetActiveTab(tabIndex);
            PlayAdjustSound();
        }

        public bool TransmitCode(int packed)
        {
            if (!_simulation.IsSignalFound || _simulation.IsOnline || _simulation.IsSynchronizing) return false;
            if (!_simulation.Decoder.Transmit(packed)) return false;
            PlayOneShot(startupClip, 0.65f, "security_terminal/terminal_boot");
            HandleSimulationChanged(_simulation.Snapshot);
            return true;
        }

        public void ApplyAuthoritativeDecoder(RelayBDecodeSnapshot state)
        {
            _simulation.Decoder.ApplyAuthoritative(state);
            ui?.Refresh(_simulation.Snapshot);
        }

        public void PlayDecoderTick(int feedback = -1)
        {
            if (!Application.isPlaying) return;
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
            int index = Mathf.Clamp(feedback, 0, 2);
            var source = _decoderFeedbackSources[index];
            if (source == null) source = _decoderFeedbackSources[index] = EchoProtocol.Audio.GameAudioRuntime.CreateSource(gameObject, false);
            source.pitch = feedback == 2 ? 1.35f : feedback == 1 ? 1f : feedback == 0 ? 0.7f : 1.1f;
            if (adjustClip != null) source.PlayOneShot(adjustClip, 0.3f);
            else EchoProtocol.Audio.GameAudioRuntime.Play(source, "ui/click", 0.3f);
        }

        private void HandleDecoderFailed()
        {
            PlayOneShot(mismatchClip, 0.8f, "objectives/relay_b_desync");
            EmitAuthoritativeNoise(RuntimeNoiseType.MACHINE_OVERLOAD);
        }

        private void HandleDecoderCompleted() => PlayOneShot(completeClip, 0.7f, "security_terminal/access_granted");

        public void ScanSpectrum()
        {
            _simulation.ScanSpectrum();
            PlayOneShot(startupClip, 0.85f, "security_terminal/terminal_boot");
            EmitAuthoritativeNoise(RuntimeNoiseType.MACHINE_REPAIR);
        }

        public void SelectChannel(int channelIndex)
        {
            _simulation.SelectChannel(channelIndex);
            PlayAdjustSound();
            ControlsChanged?.Invoke(_simulation.SelectedChannelIndex, _simulation.CurrentFrequency, _simulation.CurrentPhase);
        }

        public void SetPipelineSlot(int slotIndex, RelayBModuleType module)
        {
            _simulation.SetPipelineSlot(slotIndex, module);
            PlayAdjustSound();
        }

        public void AnalyzeOutput()
        {
            _simulation.AnalyzeOutput();
            PlayOneShot(adjustClip, 0.75f, "security_terminal/terminal_boot");
            EmitAuthoritativeNoise(RuntimeNoiseType.MACHINE_REPAIR);
        }

        public void ApplyPhaseShift(float deltaDegrees)
        {
            _simulation.ApplyPhaseCorrection(deltaDegrees);
            PlayAdjustSound();
            ControlsChanged?.Invoke(_simulation.SelectedChannelIndex, _simulation.CurrentFrequency, _simulation.CurrentPhase);
        }

        public void ApplyTimingOffset(int deltaBaud)
        {
            _simulation.ApplyTimingOffset(deltaBaud);
            PlayAdjustSound();
        }

        public void ApplyPhaseTrim(float deltaDegrees)
        {
            _simulation.ApplyPhaseTrim(deltaDegrees);
            PlayAdjustSound();
        }

        public void ApplyClockTrim(int deltaBaud)
        {
            _simulation.ApplyClockTrim(deltaBaud);
            PlayAdjustSound();
        }

        public void StartSynchronization()
        {
            if (IsOnline) return;

            _simulation.StartSynchronization();
            if (_simulation.IsSynchronizing)
            {
                PlayOneShot(startupClip, 0.9f, "security_terminal/terminal_boot");
                StartSyncRequested?.Invoke();
            }
            else
            {
                EmitAuthoritativeNoise(RuntimeNoiseType.MACHINE_OVERLOAD);
            }
        }

        public void CancelSynchronization()
        {
            _simulation.CancelSynchronization();
            PlayOneShot(warningClip, 0.5f, "security_terminal/download_pause");
            CancelSyncRequested?.Invoke();
        }

        // Backward compatibility
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

        public void ScanChannels() => ScanSpectrum();

        public void SetProcessing(RelayBFilterMode filterMode, RelayBGainMode gainMode, RelayBPilotMode pilotMode)
        {
            _simulation.SetProcessing(filterMode, gainMode, pilotMode);
            PlayAdjustSound();
        }

        public void ApplyAuthoritativeControls(int channelIndex, float frequency, float phase)
        {
            if (channelIndex >= 0 && channelIndex != _simulation.SelectedChannelIndex)
            {
                _simulation.SelectChannel(channelIndex);
            }

            if (!Mathf.Approximately(frequency, _simulation.CurrentFrequency))
            {
                _simulation.SetFrequency(frequency);
            }
            if (!Mathf.Approximately(phase, _simulation.CurrentPhase))
            {
                _simulation.SetPhase(phase);
            }
        }

        public void ApplyAuthoritativeProcessing(bool scanned, int firstSlot, int secondSlot, bool tested, bool applySlots)
        {
            if (scanned && !_simulation.Snapshot.HasScanned) _simulation.ScanSpectrum();
            if (!applySlots || !_simulation.IsSignalFound) return;

            var modules = _simulation.PipelineModules;
            if (modules[0] != (RelayBModuleType)firstSlot)
                _simulation.SetPipelineSlot(0, (RelayBModuleType)firstSlot);
            if (modules[1] != (RelayBModuleType)secondSlot)
                _simulation.SetPipelineSlot(1, (RelayBModuleType)secondSlot);
            if (tested && string.IsNullOrEmpty(_simulation.OutputDiagnostic.Summary))
                _simulation.AnalyzeOutput();
            else if (!tested)
                _simulation.ClearOutputDiagnostic();
        }

        public void ApplyAuthoritativePresetIndex(int presetIndex)
        {
            int normalized = Mathf.Max(0, presetIndex);
            if (_simulation.IsOnline || _presetIndex == normalized) return;

            _presetIndex = normalized;
            _attemptSeed = NewAttemptSeed();
            InitializeAttempt();
        }

        public void ApplyAuthoritativeSyncState(bool synchronizing)
        {
            if (_simulation.IsOnline || _simulation.IsSynchronizing == synchronizing) return;

            if (synchronizing) _simulation.StartSynchronization();
            else _simulation.CancelSynchronization();
        }

        public void ApplyAuthoritativeCleanScenario(int seed)
        {
            _simulation.RerollCleanScenario(seed);
        }

        public void RerollFindAfterMismatch(int failedChannel)
        {
            int count = config != null && config.Presets != null ? config.Presets.Count : 0;
            if (count == 0) return;
            int start = _presetIndex >= 2 ? 2 : 0;
            int end = Mathf.Min(start + 2, count);
            int next = end - start > 1 ? start + ((_presetIndex - start + 1) % (end - start)) : start;
            int seed = 0;
            var preview = new RelayBSignalSimulation();
            for (int attempt = 0; attempt < 64; attempt++)
            {
                seed = NewAttemptSeed();
                preview.Initialize(config, next, true, seed);
                if (preview.GetCurrentPreset().CorrectChannelIndex != failedChannel) break;
            }
            ApplyAuthoritativeAttempt(next, seed);
        }

        public void RerollCleanAfterFailedTest()
        {
            if (!_simulation.IsSignalClean)
            {
                int seed;
                do seed = NewAttemptSeed();
                while (RelayBSignalSimulation.GetCleanProblemsForSeed(_presetIndex, seed)
                    == _simulation.CleanProblems);
                ApplyAuthoritativeCleanScenario(seed);
            }
        }

        public void ApplyAuthoritativeSyncTelemetry(float progressSeconds, bool driftActive, bool driftWarning)
        {
            _simulation.ApplyAuthoritativeSyncTelemetry(progressSeconds, driftActive, driftWarning);
        }

        public void ApplyOnlineFromAuthority()
        {
            if (_simulation.IsOnline) return;
            _simulation.ForceCompleteForAuthoritativeSync();
        }

        public void ApplyAuthoritativeAttempt(int presetIndex, int attemptSeed)
        {
            if (attemptSeed == 0 || _simulation.IsOnline) return;

            int normalizedPreset = Mathf.Max(0, presetIndex);
            if (_presetIndex == normalizedPreset && _attemptSeed == attemptSeed) return;

            bool rerolledFind = _simulation.Snapshot.HasScanned && !_simulation.IsSignalFound;
            _presetIndex = normalizedPreset;
            _attemptSeed = attemptSeed;
            InitializeAttempt();
            if (rerolledFind) _simulation.MarkFindRerolled();
            ui?.Refresh(_simulation.Snapshot);
        }

        public void ResetForRetry(int presetIndex = -1, int attemptSeed = 0)
        {
            ui?.Close();
            _presetIndex = presetIndex >= 0 ? presetIndex : ChooseRandomPresetIndex();
            _attemptSeed = attemptSeed != 0 ? attemptSeed : NewAttemptSeed();
            InitializeAttempt();
            ui?.Refresh(_simulation.Snapshot);
        }

        private void RandomizePresetIndex() => _presetIndex = ChooseRandomPresetIndex();

        private void InitializeAttempt()
        {
            _simulation.Initialize(config, _presetIndex, true, _attemptSeed);
            EnsureSurge(_attemptSeed,_surgeDifficulty,_surgeVariant,0);
            // Spectrum seeds are replicated; decoder secrets must use an independent private seed.
            _simulation.Decoder.Initialize(NewAttemptSeed());
        }

        private int ChooseRandomPresetIndex()
        {
            int count = config != null && config.Presets != null ? config.Presets.Count : 0;
            return count > 0 ? UnityEngine.Random.Range(0, count) : 0;
        }

        private static int NewAttemptSeed()
        {
            int seed = UnityEngine.Random.Range(1, int.MaxValue);
            return seed == 0 ? 1 : seed;
        }

        private void HandleSimulationChanged(RelayBSnapshot snapshot)
        {
            bool repairing = snapshot.Status == RelayBStatus.Scanning
                || snapshot.Status == RelayBStatus.Synchronizing
                || snapshot.Status == RelayBStatus.SignalMismatch
                || snapshot.Status == RelayBStatus.ConnectionLost
                || snapshot.Status == RelayBStatus.DriftWarning;
            EchoProtocol.Audio.GameAudioRuntime.Loop(_statusLoop,
                repairing ? "objectives/relay_b_signal_loop" : null, 0.68f);
            ui?.Refresh(snapshot);
            StateChanged?.Invoke(snapshot);
        }

        private void HandleCompleted()
        {
            PlayOneShot(completeClip, 0.95f, "security_terminal/access_granted");
            ui?.Close();
            RelayBOnline?.Invoke();
        }

        private void HandleDriftWarning()
        {
            PlayOneShot(warningClip, 0.8f, "objectives/relay_b_desync");
        }

        private void HandleDriftTriggered()
        {
            PlayOneShot(warningClip, 0.9f, "objectives/relay_b_desync");
        }

        private void HandleSignalMismatch()
        {
            if (Time.unscaledTime >= _nextMismatchSoundAt)
            {
                _nextMismatchSoundAt = Time.unscaledTime + mismatchSoundCooldown;
                PlayOneShot(mismatchClip, 0.75f, "objectives/relay_b_desync");
            }
        }

        private void HandleInstabilityReset()
        {
            PlayOneShot(warningClip, 0.8f, "objectives/relay_b_desync");
        }

        private void PlayAdjustSound()
        {
            if (Time.unscaledTime >= _nextAdjustSoundAt)
            {
                _nextAdjustSoundAt = Time.unscaledTime + adjustSoundCooldown;
                PlayOneShot(adjustClip, 0.35f, "ui/click");
            }
        }

        private void PlayOneShot(AudioClip clip, float volume, string fallbackKey)
        {
            if (audioSource != null && clip != null)
            {
                audioSource.PlayOneShot(clip, volume);
            }
            else EchoProtocol.Audio.GameAudioRuntime.Play(audioSource, fallbackKey, volume);
        }

        private void EmitAuthoritativeNoise(RuntimeNoiseType type)
        {
            try
            {
                var authority = MatchAuthorityRuntime.Instance;
                if (authority != null && authority.HasStateAuthority)
                {
                    var key = new RuntimeNoiseSourceOccurrenceKey($"RelayB_{gameObject.name}", Time.frameCount);
                    HostRuntimeNoiseService.EnsureExists(authority)
                        .TryAccept(PlayerRef.None, type, key, transform.position, out _);
                }
            }
            catch
            {
                // Fallback safe in non-networked unit tests
            }
        }
    }
}
