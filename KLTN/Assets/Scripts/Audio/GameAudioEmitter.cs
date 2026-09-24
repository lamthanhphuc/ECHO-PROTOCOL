using System.Collections.Generic;
using EchoProtocol.AI.Stalker;
using EchoProtocol.AI.Stalker.Networking;
using EchoProtocol.Networking;
using Fusion;
using UnityEngine;

namespace EchoProtocol.Audio
{
    /// <summary>Observes rendered state on each peer, including proxies; skips initial snapshots.</summary>
    public sealed class GameAudioEmitter : MonoBehaviour
    {
        private readonly Dictionary<string, object> _previous = new Dictionary<string, object>();
        private AudioSource _effects, _loop, _proximity;
        private AudioReverbFilter _stalkerReverb;
        private NetworkObject _network;
        private NetworkPlayerMovement _player;
        private NetworkPlayerLifeState _life;
        private NetworkPlayerFlashlight _flashlight;
        private LobbyPlayerState _lobby;
        private PlayerHidingController _hiding;
        private StalkerFusionRuntime _stalker;
        private StalkerController _stalkerController;
        private NetworkSlidingDoor _slidingDoor;
        private NetworkDoor _door;
        private NetworkPickupItem _core;
        private NetworkSectorBox _sector;
        private NetworkPowerPuzzle _puzzle;
        private NetworkMatchState _match;
        private SecurityTerminalDownload _terminal;
        private NoiseMakerBeacon _beacon;
        private Vector3 _lastPosition;
        private float _lastSample, _nextStep;
        private int _step;

        private void Awake()
        {
            GameAudioRuntime.EnsureInitialized();
            _effects = GameAudioRuntime.CreateSource(gameObject, true);
            _loop = GameAudioRuntime.CreateSource(gameObject, true);
            _proximity = GameAudioRuntime.CreateSource(gameObject, true);
            _network = GetComponent<NetworkObject>();
            _player = GetComponent<NetworkPlayerMovement>();
            _life = GetComponent<NetworkPlayerLifeState>();
            _flashlight = GetComponent<NetworkPlayerFlashlight>();
            _lobby = GetComponent<LobbyPlayerState>();
            _hiding = GetComponent<PlayerHidingController>();
            _stalker = GetComponent<StalkerFusionRuntime>();
            _stalkerController = GetComponent<StalkerController>();
            _slidingDoor = GetComponent<NetworkSlidingDoor>();
            _door = GetComponent<NetworkDoor>();
            _core = GetComponent<NetworkPickupItem>();
            _sector = GetComponent<NetworkSectorBox>();
            _puzzle = GetComponent<NetworkPowerPuzzle>();
            _match = GetComponent<NetworkMatchState>();
            _terminal = GetComponent<SecurityTerminalDownload>();
            _beacon = GetComponent<NoiseMakerBeacon>();
            if (_stalker != null)
            {
                _effects.minDistance = 4f;
                _effects.maxDistance = 55f;
                _loop.minDistance = 6f;
                _loop.maxDistance = 60f;
                _proximity.minDistance = 8f;
                _proximity.maxDistance = 50f;
                _effects.spread = 25f;
                _loop.spread = 35f;
                _proximity.spread = 45f;
                _effects.reverbZoneMix = 1f;
                _loop.reverbZoneMix = 1f;
                _proximity.reverbZoneMix = 1f;
                _stalkerReverb = gameObject.AddComponent<AudioReverbFilter>();
                _stalkerReverb.reverbPreset = AudioReverbPreset.Cave;
            }
            _lastPosition = transform.position;
            _lastSample = Time.time;
        }

        private bool Changed<T>(string key, T value)
        {
            var known = _previous.TryGetValue(key, out var previous);
            _previous[key] = value;
            return known && !EqualityComparer<T>.Default.Equals((T)previous, value);
        }

        private void Play(string key, float volume = 1f) => GameAudioRuntime.Play(_effects, key, volume);

        private void PlayStalker(string key, float volume = 1f, float pitchMin = 0.88f, float pitchMax = 1.02f) =>
            GameAudioRuntime.Play(_effects, key, volume, pitchMin, pitchMax);

        private void Update()
        {
            if (_network != null && !_network.IsValid && _stalker == null)
            {
                _loop.Stop();
                return;
            }
            var elapsed = Time.time - _lastSample;
            if (elapsed < 0.05f) return;
            var delta = transform.position - _lastPosition;
            delta.y = 0f;
            var speed = delta.magnitude / elapsed;
            _lastPosition = transform.position;
            _lastSample = Time.time;

            if (_player != null) UpdatePlayer(speed);
            if (_stalker != null) UpdateStalker(speed);
            if (_slidingDoor != null)
            {
                if (Changed("door", _slidingDoor.CurrentState))
                    Play(_slidingDoor.CurrentState == NetworkDoorState.Open ? "door/sliding_open" : "door/sliding_close");
                // Break and jammer deploy already have replicated RPC presentation.
            }
            else if (_door != null && Changed("door", _door.State))
                Play(_door.State == NetworkDoorState.Open ? "door/sliding_open" : "door/sliding_close");

            if (_core != null)
            {
                if (Changed("core", _core.State))
                {
                    switch (_core.State)
                    {
                        case NetworkItemState.Carried: Play("energy_core/pickup"); break;
                        case NetworkItemState.Dropped: Play("energy_core/drop"); break;
                        case NetworkItemState.Placed: Play("energy_core/deposit_into_sector_box"); break;
                    }
                }
                GameAudioRuntime.Loop(_loop,
                    _core.State == NetworkItemState.Available || _core.State == NetworkItemState.Dropped
                        ? "energy_core/ambient_hum_loop" : null, 0.15f);
            }
            if (_sector != null)
            {
                if (Changed("cores", _sector.PlacedCoreCount))
                    Play(_sector.IsCoreObjectiveComplete ? "sector_box_power_hub/fully_powered" : "sector_box_power_hub/core_insert_partial");
                GameAudioRuntime.Loop(_loop, _sector.IsCoreObjectiveComplete ? "sector_box_power_hub/idle_machinery_loop" : null, 0.15f);
            }
            if (_puzzle != null)
            {
                var failed = Changed("failures", _puzzle.FailureCount);
                var progressed = Changed("step", _puzzle.CurrentSequenceIndex);
                var stateChanged = Changed("puzzle", _puzzle.State);
                if (stateChanged && _puzzle.State == NetworkPowerPuzzleState.Completed) Play("power_puzzle/puzzle_complete");
                else if (failed) Play("power_puzzle/wrong_input");
                else if (progressed && _puzzle.LastInputWasCorrect) Play("power_puzzle/correct_input");
            }
            if (_match != null) UpdateMatch();
            if (_terminal != null)
            {
                if (Changed("terminal", _terminal.State))
                    Play(_terminal.IsComplete ? "security_terminal/download_complete" :
                        _terminal.IsPaused ? "security_terminal/download_pause" :
                        _terminal.Progress01 > 0.01f ? "security_terminal/download_resume" : "security_terminal/download_start");
                GameAudioRuntime.Loop(_loop, _terminal.IsDownloading ? "security_terminal/download_progress_loop" : null, 0.3f);
            }
            if (_beacon != null) GameAudioRuntime.Loop(_loop, "noise_maker/beacon_loop_loop", 0.5f);
        }

        private void UpdatePlayer(float speed)
        {
            var local = _network == null || _network.HasInputAuthority;
            _effects.spatialBlend = local ? 0f : 1f;
            _loop.spatialBlend = local ? 0f : 1f;
            if (_lobby != null && Changed("ready", (bool)_lobby.IsReady) && _lobby.IsReady)
                GameAudioRuntime.UI("ui/lobby_ready");
            var gameplay = _lobby == null || _lobby.IsGameplayPlayer;
            if (!gameplay) { GameAudioRuntime.Loop(_loop, null); return; }
            if (_flashlight != null && Changed("flashlight", _flashlight.IsOn))
                Play(_flashlight.IsOn ? "player/flashlight_on" : "player/flashlight_off", 0.55f);
            var hidden = _hiding != null && _hiding.IsHidden;
            if (local && Changed("hidden", hidden))
                Play(hidden ? "locker_hiding/enter_hiding" : "locker_hiding/locker_open");
            var alive = _life == null || _life.Status == NetworkPlayerLifeStatus.Alive;
            if (_life != null)
            {
                if (Changed("lifeOrdinal", _life.TransitionOrdinal))
                {
                    switch (_life.LastTransitionCause)
                    {
                        case NetworkPlayerLifeTransitionCause.Damage:
                            Play(_life.Status == NetworkPlayerLifeStatus.Downed ? "player/death_downed" : "player/damage_grunt"); break;
                        case NetworkPlayerLifeTransitionCause.ReviveCompleted: Play("first_aid/revive_complete"); break;
                        case NetworkPlayerLifeTransitionCause.ReviveStarted: Play("first_aid/use_kit"); break;
                        case NetworkPlayerLifeTransitionCause.ReviveCancelled: Play("first_aid/revive_cancel"); break;
                        case NetworkPlayerLifeTransitionCause.Bleedout:
                        case NetworkPlayerLifeTransitionCause.ReviveLimit: Play("player/eliminated"); break;
                    }
                }
            }
            var downed = _life != null && _life.Status == NetworkPlayerLifeStatus.Downed;
            var reviving = downed && _life.Reviver != PlayerRef.None;
            GameAudioRuntime.Loop(_loop, reviving ? "first_aid/revive_progress_loop" :
                local && downed ? "player/downed_heartbeat_loop" :
                local && hidden ? "locker_hiding/player_breathing_inside_loop" : null, 0.3f);
            if (alive && !hidden) Footstep(speed, _player.IsAnimationSprinting ? "player/sprint_footstep_" : "player/concrete_footstep_",
                _player.IsAnimationSprinting ? 0.3f : 0.5f, local ? 0.45f : 0.65f);
        }

        private void UpdateStalker(float speed)
        {
            var state = _network != null && !_network.IsValid && _stalkerController != null
                ? new StalkerNetworkPresentationState(
                    _stalkerController.CurrentState,
                    StalkerAttackEpisodeId.Invalid,
                    StalkerNetworkAttackPhase.None,
                    0f,
                    false,
                    StalkerAttackOutcome.None,
                    -1L,
                    -1L)
                : _stalker.GetReplicatedPresentationState();
            if (!state.PresentationVisible)
            {
                GameAudioRuntime.Loop(_loop, null, 0f);
                GameAudioRuntime.Loop(_proximity, null, 0f);
                return;
            }

            if (Changed("stalker", state.SemanticState))
            {
                switch (state.SemanticState)
                {
                    case StalkerState.DETECT: PlayStalker("stalker/monster_growl_01", 1.0f, 0.78f, 0.94f); break;
                    case StalkerState.CHASE: PlayStalker("stalker/monster_growl_02", 1.05f, 0.82f, 0.98f); break;
                    case StalkerState.SEARCH: PlayStalker("stalker/monster_growl_02", 1.0f, 0.78f, 0.94f); break;
                    case StalkerState.RECOVER: PlayStalker("stalker/monster_growl_01", 0.9f, 0.82f, 0.94f); break;
                }
            }
            if (Changed("attackPhase", state.AttackPhase) && state.AttackPhase == StalkerNetworkAttackPhase.Windup)
                PlayStalker("stalker/monster_scream_attack", 1.05f, 0.86f, 0.98f);
            if (Changed("attackResolution", state.AttackResolvedTick) && state.AttackHitMomentResolved)
            {
                var hit = state.AttackOutcome == StalkerAttackOutcome.Hit;
                PlayStalker(hit ? "stalker/monster_scream_attack" : "stalker/miss_attack", hit ? 1.15f : 1.05f,
                    hit ? 0.78f : 0.88f, hit ? 0.92f : 1.01f);
                if (hit) PlayStalker("stalker/monster_growl_01", 0.8f, 0.72f, 0.86f);
            }
            var chasing = state.SemanticState == StalkerState.CHASE;
            var pursuing = chasing || state.SemanticState == StalkerState.DETECT;
            GameAudioRuntime.Loop(_loop,
                pursuing ? "stalker/monster_breathing_chase_loop" : "stalker/monster_breathing_idle_loop",
                pursuing ? 0.55f : 0.35f);
            GameAudioRuntime.Loop(_proximity,
                pursuing ? "stalker/monster_proximity_rumble_loop" : null,
                pursuing ? 0.24f : 0f);
            Footstep(speed, "stalker/monster_step_", chasing ? 0.3f : 0.65f, chasing ? 1.1f : 0.95f);
        }

        private void Footstep(float speed, string prefix, float interval, float volume)
        {
            // Ignore standing still and teleport corrections.
            if (speed < 0.2f || speed > 12f || Time.time < _nextStep) return;
            _nextStep = Time.time + interval;
            _step = (_step + 1) % 4;
            Play(prefix + (_step + 1).ToString("00"), volume);
        }

        private void UpdateMatch()
        {
            if (Changed("phase", _match.CurrentPhase))
            {
                switch (_match.CurrentPhase)
                {
                    case NetworkMatchPhase.SecurityHold: GameAudioRuntime.UI("security_terminal/terminal_boot"); break;
                    case NetworkMatchPhase.FinalHunt: GameAudioRuntime.UI("escape_endgame/final_hunt_start"); break;
                    case NetworkMatchPhase.Escape: GameAudioRuntime.UI("escape_endgame/escape_unlocked"); break;
                    case NetworkMatchPhase.MatchEnded:
                        GameAudioRuntime.UI(_match.Result == NetworkMatchResult.Win ? "escape_endgame/mission_success" : "escape_endgame/mission_failure"); break;
                    default: GameAudioRuntime.UI("ui/objective_update"); break;
                }
            }
            var remaining = Mathf.CeilToInt(_match.EscapeRemainingSeconds);
            if (Changed("countdown", remaining) && _match.IsEscapeTimerRunning && remaining > 0 && remaining <= 10)
                GameAudioRuntime.UI("escape_endgame/countdown_tick");
        }

        private void OnDisable()
        {
            if (_loop != null) _loop.Stop();
            if (_proximity != null) _proximity.Stop();
            if (_effects != null) _effects.Stop();
            _previous.Clear();
            _lastPosition = transform.position;
            _lastSample = Time.time;
        }
    }
}
