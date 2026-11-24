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
        private NetworkPlayerInteractor _tools;
        private AudioSource _stabilizerLoop;
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
        private Zone3ChargeStation _charge;
        private FloodedChargeInteraction _floodedCharge;
        private EchoProtocol.Gameplay.PushableObject _frigate;
        private NoiseMakerBeacon _beacon;
        private Vector3 _lastPosition;
        private float _lastSample, _nextStep;
        private float _lastRelayWindowRemaining = -1f;
        private int _lastTerminalHolderCount;
        private int _step;
        private float _previousPlayerHealth = -1f;
        private bool _exhausted;

        private void Awake()
        {
            GameAudioRuntime.EnsureInitialized();
            _effects = GameAudioRuntime.CreateSource(gameObject, true);
            _loop = GameAudioRuntime.CreateSource(gameObject, true);
            _proximity = GameAudioRuntime.CreateSource(gameObject, true);
            _network = GetComponent<NetworkObject>();
            _player = GetComponent<NetworkPlayerMovement>();
            _tools = GetComponent<NetworkPlayerInteractor>();
            if (_tools != null) _stabilizerLoop = GameAudioRuntime.CreateSource(gameObject, true);
            _life = GetComponent<NetworkPlayerLifeState>();
            _flashlight = GetComponent<NetworkPlayerFlashlight>();
            _lobby = GetComponent<LobbyPlayerState>();
            _hiding = GetComponent<PlayerHidingController>();
            _stalker = GetComponent<StalkerFusionRuntime>();
            _stalkerController = GetComponent<StalkerController>();
            if (GetComponent<EchoProtocol.AI.Stalker.Presentation.StalkerAudioController>() != null)
            {
                // When StalkerAudioController is present, it handles all Stalker audio with dedicated 3D sources & animation events.
                // Neutralize legacy handling here to prevent duplicate AudioSources, unwanted Cave reverb filter, and audio conflicts.
                _stalker = null;
                _stalkerController = null;
            }
            _slidingDoor = GetComponent<NetworkSlidingDoor>();
            _door = GetComponent<NetworkDoor>();
            _core = GetComponent<NetworkPickupItem>();
            _sector = GetComponent<NetworkSectorBox>();
            _puzzle = GetComponent<NetworkPowerPuzzle>();
            _match = GetComponent<NetworkMatchState>();
            _terminal = GetComponent<SecurityTerminalDownload>();
            _charge = GetComponent<Zone3ChargeStation>();
            _floodedCharge = GetComponent<FloodedChargeInteraction>();
            _frigate = GetComponent<EchoProtocol.Gameplay.PushableObject>();
            if (_charge != null || _floodedCharge != null)
            {
                _effects.rolloffMode = AudioRolloffMode.Linear;
                _loop.rolloffMode = AudioRolloffMode.Linear;
                _effects.minDistance = 8f;
                _effects.maxDistance = 90f;
                _loop.minDistance = 8f;
                _loop.maxDistance = 90f;
            }
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
                if (_stabilizerLoop != null) _stabilizerLoop.Stop();
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
                    Play(_sector.IsCoreObjectiveComplete ? "sector_box_power_hub/fully_powered" : "sector_box_power_hub/core_insert_partial", .65f);
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
                int holders = NetworkMatchState.Instance != null && NetworkMatchState.Instance.Object != null
                    && NetworkMatchState.Instance.Object.IsValid
                    ? NetworkMatchState.Instance.SecurityHoldParticipantCount
                    : _terminal.IsDownloading ? 1 : 0;
                if (Changed("terminal_holders", holders) && holders > _lastTerminalHolderCount && holders > 1)
                    Play("power_puzzle/terminal_interaction", 0.65f);
                _lastTerminalHolderCount = holders;
                GameAudioRuntime.Loop(_loop, _terminal.IsDownloading ? "security_terminal/download_progress_loop" : null, 0.3f);
                UpdateTerminalDeadlineWarning();
            }
            if (_charge != null || _floodedCharge != null) UpdateCharge();
            if (_frigate != null) UpdateFrigate(speed);
            if (_beacon != null) GameAudioRuntime.Loop(_loop, "noise_maker/beacon_loop_loop", 0.5f);
        }

        private void UpdateCharge()
        {
            var match = NetworkMatchState.Instance;
            bool flooded = _floodedCharge != null;
            if (flooded && (match == null || match.Object == null || !match.Object.IsValid || match.IsEnded))
            { GameAudioRuntime.Loop(_loop, null); return; }
            bool charging = flooded ? match.IsZone3Charging : _charge.IsCharging;
            float progress = flooded ? _floodedCharge.Progress01 : _charge.Progress01;
            bool completed = progress >= 1f && (flooded ? match.CurrentPhase == NetworkMatchPhase.Zone3PushFrigate
                : match == null || match.Object == null || !match.Object.IsValid
                    || match.CurrentPhase == NetworkMatchPhase.FinalHunt);
            if (Changed("zone3ChargeActive", charging) && (charging || !completed))
                Play(charging ? "objectives/charge_engage" : "objectives/charge_interrupt", 0.9f);
            GameAudioRuntime.Loop(_loop,
                charging ? "objectives/charge_transfer_loop" : null, 0.95f);
            if (Changed("zone3ChargeComplete", completed) && completed)
                Play("sector_box_power_hub/fully_powered", 0.9f);
        }

        private void UpdateFrigate(float speed)
        {
            var match = NetworkMatchState.Instance;
            var zone3 = EchoProtocol.Networking.Zone3MissionDirector.Instance;
            bool pushPhase = match != null && match.Object != null && match.Object.IsValid
                ? match.CurrentPhase == NetworkMatchPhase.Zone3PushFrigate
                : zone3 != null && zone3.IsPushAvailable;
            bool floodedEscort = FloodedMapMission.Instance != null
                && FloodedMapMission.Instance.Escort == _frigate.transform;
            if (floodedEscort) return; // FloodedMapMission presents the replicated escort-moving flag.
            bool moving = zone3 != null && zone3.Frigate == _frigate
                && pushPhase && speed > 0.08f && speed < 4f;
            if (Changed("frigateMoving", moving))
                Play(moving ? "power_puzzle/breaker_toggle" : "map_ambience/pipe_creak", 0.55f);
            GameAudioRuntime.Loop(_loop, moving ? "objectives/frigate_grind_loop" : null, 0.8f);
        }

        private void UpdateTerminalDeadlineWarning()
        {
            var match = NetworkMatchState.Instance;
            float remaining;
            bool completed;
            if (match != null && match.Object != null && match.Object.IsValid)
            {
                remaining = match.RelayRepairWindowRemainingSeconds;
                completed = match.SecurityHoldCompleted;
            }
            else
            {
                var director = EchoProtocol.MatchFlow.Zone2MissionDirector.Instance;
                remaining = director != null ? director.RelayRepairRemainingSeconds : 0f;
                completed = director != null && director.IsSecurityHoldComplete;
            }

            if (remaining > 0f && _lastRelayWindowRemaining > remaining)
            {
                if ((_lastRelayWindowRemaining > 60f && remaining <= 60f)
                    || (_lastRelayWindowRemaining > 30f && remaining <= 30f)
                    || (_lastRelayWindowRemaining > 10f && remaining <= 10f))
                    Play("security_terminal/access_denied", 0.8f);
            }
            else if (_lastRelayWindowRemaining > 0f && remaining <= 0f && !completed)
                Play("ui/error", 0.8f);
            _lastRelayWindowRemaining = remaining;
        }

        private void UpdatePlayer(float speed)
        {
            var local = _network == null || _network.HasInputAuthority;
            _effects.spatialBlend = local ? 0f : 1f;
            _loop.spatialBlend = local ? 0f : 1f;
            if (_lobby != null && Changed("ready", (bool)_lobby.IsReady) && _lobby.IsReady)
                GameAudioRuntime.UI("ui/lobby_ready");
            var gameplay = _lobby == null || _lobby.IsGameplayPlayer;
            bool stabilizing = gameplay && _tools != null && _tools.Object != null && _tools.Object.IsValid
                && _tools.IsCoreStabilizerActive;
            GameAudioRuntime.Loop(_stabilizerLoop, stabilizing ? "energy_core/ambient_hum_loop" : null, .10f);
            if (Changed("stabilizerActive", stabilizing) && !stabilizing)
                Play("objectives/charge_interrupt", .22f);
            if (!gameplay) { GameAudioRuntime.Loop(_loop, null); return; }
            if (_flashlight != null && Changed("flashlight", _flashlight.IsOn))
                Play(_flashlight.IsOn ? "player/flashlight_on" : "player/flashlight_off", 0.55f);
            var hidden = _hiding != null && _hiding.IsHidden;
            if (local && Changed("hidden", hidden))
                Play(hidden ? "locker_hiding/enter_hiding" : "locker_hiding/locker_open");
            var alive = _life == null || _life.Status == NetworkPlayerLifeStatus.Alive;
            // Life transition ordinals do not advance for non-fatal damage.
            if (_life != null)
            {
                if (alive && PlayerCameraFeedback.IsDamage(_previousPlayerHealth, _life.Health))
                    Play("player/damage_grunt", .85f);
                _previousPlayerHealth = _life.Health;
            }
            if (_life != null)
            {
                if (Changed("lifeOrdinal", _life.TransitionOrdinal))
                {
                    switch (_life.LastTransitionCause)
                    {
                        case NetworkPlayerLifeTransitionCause.Damage:
                            if (_life.Status == NetworkPlayerLifeStatus.Downed) Play("player/death_downed");
                            break;
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
            float stamina = _player.CurrentStamina / Mathf.Max(1, _player.MaxStamina);
            _exhausted = alive && (stamina < .22f || _exhausted && stamina < .55f);
            bool modal = local && PlayerInteractionControlLock.HasModal
                || NetworkMatchState.Instance != null && NetworkMatchState.Instance.CurrentPhase == NetworkMatchPhase.MatchEnded;
            GameAudioRuntime.Loop(_loop, modal ? null : reviving ? "first_aid/revive_progress_loop" :
                local && downed ? "player/downed_heartbeat_loop" :
                local && alive && hidden ? "locker_hiding/player_breathing_inside_loop" :
                local && alive && _exhausted ? "player/low_stamina_breathing_loop" :
                local && alive && speed < .2f ? "player/breathing_loop" : null,
                downed || reviving || hidden || _exhausted ? .3f : .09f);
            if (alive && !hidden && !modal && _player.IsPresentationGrounded)
            {
                bool crouch = _player.IsAnimationCrouching;
                Footstep(speed, PlayerStepPrefix(crouch, _player.IsAnimationSprinting),
                    crouch ? .8f : _player.IsAnimationSprinting ? .4f : .56f,
                    crouch ? (local ? .25f : .35f) : local ? .45f : .65f);
            }
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
            _step = (_step + Random.Range(1, 4)) % 4;
            Play(prefix + (_step + 1).ToString("00"), volume);
        }

        private readonly RaycastHit[] _groundHits = new RaycastHit[8];
        private float _nextSurfaceProbe;
        private bool _metalGround, _softGround;
        private Collider _lastGround;
        private string PlayerStepPrefix(bool crouch, bool sprint)
        {
            if (Time.time >= _nextSurfaceProbe)
            {
                _nextSurfaceProbe = Time.time + .4f;
                int count = Physics.RaycastNonAlloc(transform.position + Vector3.up * .3f, Vector3.down,
                    _groundHits, 1.4f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                float nearest = float.MaxValue;
                Collider ground = null;
                for (int i = 0; i < count; i++)
                {
                    var hit = _groundHits[i];
                    if (hit.collider == null || hit.collider.transform.IsChildOf(transform)
                        || hit.distance >= nearest || !GameplayEnvironmentPhysics.IsEnvironment(hit.collider)) continue;
                    nearest = hit.distance;
                    ground = hit.collider;
                }
                if (ground != _lastGround)
                {
                    _lastGround = ground;
                    var renderer = ground != null ? ground.GetComponent<Renderer>() : null;
                    string material = ground == null ? "" : ground.sharedMaterial != null ? ground.sharedMaterial.name
                        : renderer != null && renderer.sharedMaterial != null ? renderer.sharedMaterial.name : ground.name;
                    _metalGround = material.IndexOf("metal", System.StringComparison.OrdinalIgnoreCase) >= 0
                        || material.IndexOf("grate", System.StringComparison.OrdinalIgnoreCase) >= 0
                        || material.IndexOf("steel", System.StringComparison.OrdinalIgnoreCase) >= 0;
                    _softGround = ground is TerrainCollider || material.IndexOf("grass", System.StringComparison.OrdinalIgnoreCase) >= 0
                        || material.IndexOf("mud", System.StringComparison.OrdinalIgnoreCase) >= 0;
                }
            }
            return HorrorPresentationRules.StepPrefix(_metalGround, _softGround, crouch, sprint);
        }

        private void UpdateMatch()
        {
            bool climax = !_match.IsEnded && (_match.CurrentPhase == NetworkMatchPhase.FinalHunt
                || _match.CurrentPhase == NetworkMatchPhase.Escape);
            _proximity.spatialBlend = 0f;
            GameAudioSettings.RouteMusic(_proximity);
            GameAudioRuntime.Loop(_proximity, climax ? "horror_ambience/low_frequency_rumble_loop" : null,
                EchoProtocol.Voice.VoicePlayerBinding.AnyRemoteSpeaking ? .035f : .07f);
            if (Changed("phase", _match.CurrentPhase))
            {
                switch (_match.CurrentPhase)
                {
                    case NetworkMatchPhase.SecurityHold: GameAudioRuntime.UI("security_terminal/terminal_boot"); break;
                    case NetworkMatchPhase.FinalHunt:
                        GameAudioRuntime.UI("escape_endgame/final_hunt_start");
                        GameAudioRuntime.UI("escape_endgame/countdown_start");
                        break;
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
            _lastGround = null;
            _metalGround = _softGround = false;
            _nextSurfaceProbe = 0f;
            if (_stabilizerLoop != null) _stabilizerLoop.Stop();
            if (_loop != null) _loop.Stop();
            if (_proximity != null) _proximity.Stop();
            if (_effects != null) _effects.Stop();
            _previous.Clear();
            _lastPosition = transform.position;
            _lastSample = Time.time;
            _lastRelayWindowRemaining = -1f;
            _lastTerminalHolderCount = 0;
        }
    }
}
