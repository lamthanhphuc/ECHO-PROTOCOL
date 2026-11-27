using EchoProtocol.AI.Stalker;
using EchoProtocol.Networking;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EchoProtocol.Audio
{
    public sealed partial class GameAudioRuntime
    {
        private readonly HorrorPresentationRules _pacing = new HorrorPresentationRules();
        private readonly AudioSource[] _oneShots = new AudioSource[8];
        private readonly AudioSource[] _echoSteps = new AudioSource[3];
        private readonly float[] _echoStepAt = new float[3];
        private float _nextAtmosphereProbe, _scannerUntil;
        private bool _safeForCosmetics;
        private float _quietWeight = 1f;
        private AudioSource _rareAmbient;
        private float _dangerBaseVolume;
        private float _nextScannerEchoAt;

        private NetworkMatchState _worldCueMatch;
        private uint _lastWorldCue;
        private AudioSource _horn, _outdoorWind;
        private NetworkMatchState _hornMatch;
        private uint _lastHorn;
        public static void PresentMapHorn(NetworkMatchState match, uint ordinal)
        {
            var runtime = _instance;
            if (runtime == null || !runtime.isActiveAndEnabled) return;
            if (runtime._hornMatch != match) { runtime._hornMatch = match; runtime._lastHorn = 0; }
            if (!HorrorPresentationRules.ConsumeOrdinal(ordinal, ref runtime._lastHorn)
                || match == null || match.IsEnded) return;
            var clip = runtime._catalog.Find("flooded/Horn");
            if (clip == null) return;
            runtime.StopPersonalEffects(); runtime._rareAmbient.Stop();
            runtime._horn.Stop(); runtime._horn.clip = clip; runtime._horn.loop = false;
            runtime._horn.volume = runtime._catalog.effectsVolume * .35f;
            runtime._horn.Play();
            runtime._pacing.Presented(21, Time.unscaledTime, 65, false);
        }
        public static void PresentEnvironmentCue(NetworkMatchState match, uint ordinal, string cue,
            Vector3 position, float gain, float range, float duration)
        {
            var runtime = _instance;
            if (runtime == null) return;
            if (runtime._worldCueMatch != match) { runtime._worldCueMatch = match; runtime._lastWorldCue = 0; }
            if (!HorrorPresentationRules.ConsumeOrdinal(ordinal, ref runtime._lastWorldCue)) return;
            // Consume the cue even when its presentation is blocked.
            var camera = Camera.main;
            if (runtime.CosmeticBlocked(camera)
                || runtime._horn.isPlaying
                || Time.unscaledTime < runtime._pacing.RecoveryUntil
                || (camera.transform.position - position).sqrMagnitude > range * range) return;
            var clip = runtime._catalog.Find(cue);
            if (clip == null) return;
            runtime.StopPersonalEffects();
            var source = runtime._rareAmbient;
            source.Stop(); source.transform.position = position; source.clip = clip;
            source.pitch = 1f; source.loop = false; source.maxDistance = range;
            source.rolloffMode = AudioRolloffMode.Linear; source.minDistance = 2f;
            source.volume = Mathf.Clamp(gain, 0, .5f) * runtime._catalog.effectsVolume;
            source.Play(); runtime._ambientStopAt = Time.unscaledTime + Mathf.Min(clip.length, duration);
            runtime._pacing.Presented(20, Time.unscaledTime, 65, false);
        }

        public static float ScannerInterference => _instance != null && _instance.isActiveAndEnabled && _instance._safeForCosmetics
            && !PlayerCameraSettings.ReduceMotion
                ? Mathf.Clamp01((_instance._scannerUntil - Time.unscaledTime) / .5f) : 0f;

        public static void TryScannerInterference()
        {
            var runtime = _instance;
            float now = Time.unscaledTime;
            if (runtime == null || !runtime._safeForCosmetics
                || PlayerCameraSettings.ReduceMotion || now < runtime._nextScannerEchoAt
                || now < runtime._pacing.RecoveryUntil) return;
            runtime._nextScannerEchoAt = now + 240f;
            runtime._scannerUntil = now + .5f;
        }

        private void InitializeAtmosphere()
        {
            _horn = ChildSource("Shared map horn"); _horn.spatialBlend = 0f;
            _outdoorWind = ChildSource("Outdoor wind bed"); _outdoorWind.spatialBlend = 0f;
            _rareAmbient = ChildSource("Rare local ambience");
            for (int i = 0; i < _oneShots.Length; i++)
                _oneShots[i] = ChildSource("Pooled cue " + i);
            for (int i = 0; i < _echoSteps.Length; i++)
                _echoSteps[i] = ChildSource("Personal ECHO " + i);
            ResetAtmosphere();
        }

        private AudioSource ChildSource(string label)
        {
            var child = new GameObject(label);
            child.transform.SetParent(transform, false);
            return CreateSource(child, true);
        }

        private void PlayPooled(AudioClip clip, Vector3 position, float volume)
        {
            foreach (var source in _oneShots)
            {
                if (source == null || source.isPlaying) continue; // Never steal a gameplay cue.
                source.transform.position = position;
                source.clip = clip; source.loop = false; source.pitch = 1f;
                source.volume = Mathf.Clamp01(volume) * _catalog.effectsVolume;
                source.Play(); return;
            }
        }

        private void ResetAtmosphere()
        {
            if (_horn != null) _horn.Stop();
            if (_outdoorWind != null) _outdoorWind.Stop();
            _pacing.Reset(Time.unscaledTime);
            _nextAtmosphereProbe = 0;
            _scannerUntil = 0;
            _nextScannerEchoAt = Time.unscaledTime + 180f;
            _safeForCosmetics = false;
            StopPersonalEffects();
            if (_rareAmbient != null) _rareAmbient.Stop();
            foreach (var source in _oneShots) if (source != null) source.Stop();
        }

        private void OnDisable()
        {
            if (_horn != null) _horn.Stop();
            if (_outdoorWind != null) _outdoorWind.Stop();
            _safeForCosmetics = false;
            StopPersonalEffects();
            if (_rareAmbient != null) _rareAmbient.Stop();
            foreach (var source in _oneShots) if (source != null) source.Stop();
        }

        private void StopPersonalEffects()
        {
            _scannerUntil = 0;
            for (int i = 0; i < _echoSteps.Length; i++)
            { _echoStepAt[i] = -1f; if (_echoSteps[i] != null) _echoSteps[i].Stop(); }
        }

        private bool CosmeticBlocked(Camera camera)
        {
            var match = NetworkMatchState.Instance;
            if (camera == null || match == null || match.Object == null || !match.Object.IsValid
                || match.Runner == null || !match.Runner.IsRunning || match.IsEnded
                || PlayerInteractionControlLock.HasModal || Time.timeScale == 0f
                || !match.Runner.TryGetPlayerObject(match.Runner.LocalPlayer, out var player)
                || player == null || !player.IsValid || !player.HasInputAuthority) return true;
            if (match.CurrentPhase == NetworkMatchPhase.FinalHunt || match.CurrentPhase == NetworkMatchPhase.Escape
                || match.CurrentPhase == NetworkMatchPhase.SecurityHold
                || match.PurgeMood == PurgeBossMood.Hunting || match.PurgeMood == PurgeBossMood.Warning) return true;
            if (player.TryGetComponent<NetworkPlayerLifeState>(out var life) && life.Status != NetworkPlayerLifeStatus.Alive) return true;
            if (player.TryGetComponent<NetworkPlayerInteractor>(out var interaction) && interaction.CurrentReviveTarget != null) return true;
            if (player.TryGetComponent<PlayerInteraction>(out var hold) && hold.IsHoldingInteraction) return true;
            if (player.TryGetComponent<PlayerJumpscareController>(out var scare) && scare.IsPlaying) return true;
            foreach (var owner in _environmentSources)
            {
                if (owner == null || !owner.isActiveAndEnabled) continue;
                var stalker = owner.PresentationStalker;
                if (stalker == null || stalker.Object == null || !stalker.Object.IsValid) continue;
                var state = stalker.GetReplicatedPresentationState();
                if (state.PresentationVisible && (stalker.transform.position - camera.transform.position).sqrMagnitude < 45f * 45f
                    && (state.SemanticState == StalkerState.CHASE || state.SemanticState == StalkerState.DETECT
                        || state.AttackPhase != EchoProtocol.AI.Stalker.Networking.StalkerNetworkAttackPhase.None
                        || state.SpecialPhase != EchoProtocol.AI.Stalker.Special.StalkerSpecialEncounterPhase.None)) return true;
            }
            return EchoProtocol.Voice.VoicePlayerBinding.AnyRemoteSpeaking;
        }

        private void UpdateAtmosphere()
        {
            float now = Time.unscaledTime;
            if (now >= _nextAtmosphereProbe)
            {
                _nextAtmosphereProbe = now + .25f;
                var camera = Camera.main;
                bool gameplay = LobbyMapCatalog.IsGameplayScene(SceneManager.GetActiveScene().name);
                bool blocked = !gameplay || CosmeticBlocked(camera) || _horn.isPlaying;
                bool outdoor = SceneManager.GetActiveScene().name == "Scene_A" || SceneManager.GetActiveScene().name == "Scene_B";
                Loop(_outdoorWind, outdoor ? "flooded/Wind" : null, .055f);
                var liveMatch = NetworkMatchState.Instance;
                bool validMatch = liveMatch != null && liveMatch.Object != null && liveMatch.Object.IsValid
                    && liveMatch.Runner != null && liveMatch.Runner.IsRunning;
                if (!gameplay || !validMatch || liveMatch.IsEnded) _horn.Stop();
                _safeForCosmetics = !blocked;
                if (blocked)
                {
                    StopPersonalEffects();
                    _rareAmbient.Stop();
                }
                if (_pacing.Ready(now, blocked)) TryAtmosphereCue(camera, now);
            }
            if (_rareAmbient != null && _rareAmbient.isPlaying)
            {
                _rareAmbient.volume = Mathf.Min(_rareAmbient.volume, Mathf.Clamp01((_ambientStopAt - now) / .5f) * .22f * _catalog.effectsVolume);
                if (now >= _ambientStopAt) _rareAmbient.Stop();
            }
            for (int i = 0; i < _echoSteps.Length; i++)
            {
                if (_echoStepAt[i] < 0f || now < _echoStepAt[i] || !_safeForCosmetics) continue;
                _echoStepAt[i] = -1f;
                if (_echoSteps[i].clip != null) _echoSteps[i].Play();
            }
            // Only ambience/music is ducked; never lower warning, interaction or voice buses.
            float desired = EchoProtocol.Voice.VoicePlayerBinding.AnyRemoteSpeaking ? .5f
                : _safeForCosmetics && now < _pacing.RecoveryUntil ? .55f : _safeForCosmetics ? 1f : .7f;
            float next = Mathf.MoveTowards(_quietWeight, desired, Time.unscaledDeltaTime * 2f);
            if (_ambience != null) _ambience.volume = _catalog.ambienceVolume * _catalog.effectsVolume * next;
            if (_dangerMusic != null) _dangerMusic.volume = _dangerBaseVolume * next;
            _quietWeight = next;
            if (_outdoorWind != null) _outdoorWind.volume = .055f * _catalog.effectsVolume * next;
            if (_horn != null) _horn.volume = _catalog.effectsVolume * (EchoProtocol.Voice.VoicePlayerBinding.AnyRemoteSpeaking ? .18f : .35f);
        }

        private void TryAtmosphereCue(Camera camera, float now)
        {
            if (now < _pacing.NextEchoAt || Random.value >= .4f) return;
            int first = Random.Range(0, 3);
            for (int attempt = 0; attempt < 3; attempt++)
            {
                int index = (first + attempt) % 3;
                int cueId = 10 + index;
                if (!_pacing.CanChoose(cueId)) continue;
                var clip = _catalog.Find("player/concrete_footstep_01");
                if (clip == null || !TryCuePosition(camera, out var position)) continue;
                for (int i = 0; i < _echoSteps.Length; i++)
                {
                    var source = _echoSteps[i];
                    source.transform.position = position + camera.transform.right * i * .65f;
                    source.clip = _catalog.Find("player/concrete_footstep_0" + ((index + i) % 4 + 1));
                    source.volume = _catalog.effectsVolume * .28f;
                    source.pitch = .82f; source.loop = false;
                    _echoStepAt[i] = now + i * .55f;
                }
                _pacing.Presented(cueId, now, Random.Range(65f, 100f), true);
                return;
            }
        }

        private float _ambientStopAt;
        private bool TryCuePosition(Camera camera, out Vector3 position)
        {
            // Local acoustic accent only: no spawn, navmesh query, collider or AI noise.
            var direction = Quaternion.AngleAxis(Random.Range(100f, 260f), Vector3.up) * camera.transform.forward;
            direction.y = 0;
            if (direction.sqrMagnitude < .01f) direction = camera.transform.right;
            direction.Normalize();
            var origin = camera.transform.position;
            position = origin + direction * Random.Range(7f, 13f);
            if (Physics.Raycast(origin, direction, out var wall, 13f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                position = wall.point - direction * .2f;
            return (position - origin).sqrMagnitude >= 4f;
        }
    }
}
