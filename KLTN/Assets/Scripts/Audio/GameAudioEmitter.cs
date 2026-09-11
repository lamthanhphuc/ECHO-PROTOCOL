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
        private AudioSource _effects, _loop;
        private NetworkObject _network;
        private NetworkPlayerMovement _player;
        private NetworkPlayerLifeState _life;
        private NetworkPlayerFlashlight _flashlight;
        private LobbyPlayerState _lobby;
        private PlayerHidingController _hiding;
        private StalkerFusionRuntime _stalker;
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
            _effects = GameAudioRuntime.CreateSource(gameObject, true);
            _loop = GameAudioRuntime.CreateSource(gameObject, true);
            _network = GetComponent<NetworkObject>();
            _player = GetComponent<NetworkPlayerMovement>();
            _life = GetComponent<NetworkPlayerLifeState>();
            _flashlight = GetComponent<NetworkPlayerFlashlight>();
            _lobby = GetComponent<LobbyPlayerState>();
            _hiding = GetComponent<PlayerHidingController>();
            _stalker = GetComponent<StalkerFusionRuntime>();
            _slidingDoor = GetComponent<NetworkSlidingDoor>();
            _door = GetComponent<NetworkDoor>();
            _core = GetComponent<NetworkPickupItem>();
            _sector = GetComponent<NetworkSectorBox>();
            _puzzle = GetComponent<NetworkPowerPuzzle>();
            _match = GetComponent<NetworkMatchState>();
            _terminal = GetComponent<SecurityTerminalDownload>();
            _beacon = GetComponent<NoiseMakerBeacon>();
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

        private void Update()
        {
            if (_network != null && !_network.IsValid)
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
            var state = _stalker.GetReplicatedPresentationState();
            if (Changed("stalker", state.SemanticState))
            {
                switch (state.SemanticState)
                {
                    case StalkerState.DETECT: Play("stalker/detect_cue"); break;
                    case StalkerState.CHASE: Play("stalker/chase_start"); break;
                    case StalkerState.SEARCH: Play("stalker/search_vocal"); break;
                    case StalkerState.RECOVER: Play("stalker/recover_exhale"); break;
                }
            }
            if (Changed("attackPhase", state.AttackPhase) && state.AttackPhase == StalkerNetworkAttackPhase.Windup)
                Play("stalker/attack_swing");
            if (Changed("attackResolution", state.AttackResolvedTick) && state.AttackHitMomentResolved)
                Play(state.AttackOutcome == StalkerAttackOutcome.Hit ? "stalker/attack_hit" : "stalker/miss_attack");
            var chasing = state.SemanticState == StalkerState.CHASE;
            GameAudioRuntime.Loop(_loop, chasing ? "stalker/chase_loop_vocal_loop" : "stalker/idle_breathing_growl_loop", 0.3f);
            Footstep(speed, chasing ? "stalker/chase_footstep_" : "stalker/patrol_footstep_", chasing ? 0.3f : 0.65f, 0.8f);
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
            if (_effects != null) _effects.Stop();
            _previous.Clear();
            _lastPosition = transform.position;
            _lastSample = Time.time;
        }
    }
}
