using System.Collections.Generic;
using EchoProtocol.Audio;
using UnityEngine;

namespace EchoProtocol.Networking
{
    // Local presentation driven by the replicated charge progress and match phase.
    // The existing corridor lights remain the source of illumination and are restored on teardown.
    [DisallowMultipleComponent]
    public sealed class Zone3FacilityPresentation : MonoBehaviour
    {
        private struct LightState
        {
            public Light Light;
            public Color Color;
            public float Intensity;
            public bool Enabled;
            public float Delay;
            public bool IsExit;
        }

        [SerializeField] private Color chargingColor = new Color(0.22f, 0.75f, 1f);
        [SerializeField] private Color emergencyColor = new Color(1f, 0.08f, 0.04f);
        [SerializeField] private Color exitColor = new Color(0.1f, 1f, 0.45f);
        [SerializeField, Min(0.1f)] private float dockLightRadius = 16f;
        [SerializeField, Min(0.1f)] private float exitLightRadius = 12f;
        [SerializeField, Min(0.1f)] private float lightWaveSpeed = 38f;

        private readonly List<LightState> _lights = new List<LightState>();
        private readonly List<Transform> _effectSites = new List<Transform>();
        private readonly List<Transform> _doorSites = new List<Transform>();
        private Transform _dock;
        private Transform _exit;
        private MatchFlowController _offlineFlow;
        private AudioSource _alarm;
        private AudioSource _dockAudio;
        private float _huntStartedAt;
        private float _nextEffectAt;
        private int _highestChargeMilestone;
        private int _effectIndex;
        private bool _wasHunting;
        private bool _wasBound;
        private bool _hasPhaseSample;
        private bool _lightsModified;

        public void Bind(Transform dock, Transform exit)
        {
            RestoreLights();
            _dock = dock;
            _exit = exit;
            _offlineFlow = GetComponent<MatchFlowController>() ?? FindAnyObjectByType<MatchFlowController>();
            _lights.Clear();
            _effectSites.Clear();
            _doorSites.Clear();
            if (_dock == null || _exit == null) return;

            var dockPosition = _dock.position;
            var exitPosition = _exit.position;
            foreach (var light in FindObjectsByType<Light>(FindObjectsInactive.Include))
            {
                if (light.gameObject.scene != gameObject.scene || !light.gameObject.activeInHierarchy) continue;
                bool nearDock = Vector3.Distance(light.transform.position, dockPosition) <= dockLightRadius;
                bool nearExit = Vector3.Distance(light.transform.position, exitPosition) <= exitLightRadius;
                if (!nearDock && !nearExit && !IsCorridorLight(light.transform)) continue;

                _lights.Add(new LightState
                {
                    Light = light,
                    Color = light.color,
                    Intensity = light.intensity,
                    Enabled = light.enabled,
                    Delay = Vector3.Distance(light.transform.position, dockPosition) / lightWaveSpeed,
                    IsExit = nearExit
                });
                if (IsCorridorLight(light.transform))
                    _effectSites.Add(light.transform);
            }
            foreach (var door in FindObjectsByType<NetworkSlidingDoor>(FindObjectsInactive.Exclude))
                if (door.gameObject.scene == gameObject.scene) _doorSites.Add(door.transform);
            foreach (var door in FindObjectsByType<DoorInteractable>(FindObjectsInactive.Exclude))
                if (door.gameObject.scene == gameObject.scene) _doorSites.Add(door.transform);

            GameAudioRuntime.EnsureInitialized();
            _alarm ??= GameAudioRuntime.CreateSource(gameObject, false);
            if (_dockAudio == null)
            {
                var dockAudio = new GameObject("Zone3 Dock Audio");
                dockAudio.transform.SetParent(transform, false);
                _dockAudio = GameAudioRuntime.CreateSource(dockAudio, true);
            }
            _dockAudio.transform.position = dockPosition;
            _dockAudio.minDistance = 8f;
            _dockAudio.maxDistance = 70f;
            _wasBound = true;
        }

        private static bool IsCorridorLight(Transform light)
        {
            for (var parent = light; parent != null; parent = parent.parent)
                if (parent.name.Contains("Corridor")) return true;
            return false;
        }

        private void Update()
        {
            if (!_wasBound || _dock == null) return;
            bool initialSnapshot = !_hasPhaseSample;
            _hasPhaseSample = true;
            var match = NetworkMatchState.Instance;
            bool online = match != null && match.Object != null && match.Object.IsValid;
            if (_offlineFlow == null) _offlineFlow = FindAnyObjectByType<MatchFlowController>();
            bool chargingPhase = online ? match.CurrentPhase == NetworkMatchPhase.Zone3PushFrigate
                : _offlineFlow != null && _offlineFlow.Phase == MatchPhase.Zone3PushFrigate;
            bool hunt = online ? match.CurrentPhase == NetworkMatchPhase.FinalHunt
                    || match.CurrentPhase == NetworkMatchPhase.Escape
                : _offlineFlow != null && (_offlineFlow.Phase == MatchPhase.FinalHunt
                    || _offlineFlow.Phase == MatchPhase.ExitCountdown);
            float progress = chargingPhase ? Zone3MissionDirector.Instance?.ChargeStation?.Progress01 ?? 0f : 0f;

            UpdateChargeMilestones(chargingPhase, progress, initialSnapshot);
            if (hunt && !_wasHunting)
            {
                _huntStartedAt = initialSnapshot ? Time.time - 20f : Time.time;
                _nextEffectAt = Time.time + 2f;
                if (!initialSnapshot)
                {
                    GameAudioRuntime.Play(_dockAudio, "objectives/emergency_grid_restored", 1f);
                    GameAudioRuntime.Play(_dockAudio, "escape_endgame/escape_unlocked", 0.95f);
                }
            }
            _wasHunting = hunt;
            GameAudioRuntime.Loop(_alarm, hunt ? "map_ambience/alarm_ambience_loop" : null, 0.48f);
            UpdateLights(chargingPhase, progress, hunt);
            if (hunt && Time.time >= _nextEffectAt)
            {
                EmitNearbyEffects();
                _nextEffectAt = Time.time + 5.5f;
            }
        }

        private void UpdateChargeMilestones(bool chargingPhase, float progress, bool initialSnapshot)
        {
            if (!chargingPhase) return;
            int milestone = Mathf.FloorToInt(Mathf.Clamp01(progress) * 4f);
            if (milestone <= _highestChargeMilestone) return;
            _highestChargeMilestone = milestone;
            if (initialSnapshot) return;
            if (milestone >= 1 && milestone <= 3)
            {
                GameAudioRuntime.Play(_dockAudio,
                    milestone == 1 ? "power_puzzle/breaker_toggle" : "power_puzzle/electrical_sparks",
                    0.55f + milestone * 0.1f);
                if (milestone >= 2) EmitParticles(_dock.position + Vector3.up * 2f, false);
            }
        }

        private void UpdateLights(bool chargingPhase, float progress, bool hunt)
        {
            if (!hunt && (!chargingPhase || progress <= 0f))
            {
                if (_lightsModified) RestoreLights();
                _lightsModified = false;
                return;
            }
            _lightsModified = true;
            float huntAge = Time.time - _huntStartedAt;
            for (int i = 0; i < _lights.Count; i++)
            {
                var state = _lights[i];
                if (state.Light == null) continue;
                if (hunt)
                {
                    float wave = Mathf.Clamp01((huntAge - state.Delay) * 2f);
                    float pulse = 0.72f + 0.28f * Mathf.Sin(Time.time * 7.5f + i * 1.37f);
                    // A few fixtures fail briefly; the route stays lit and the exit stays readable.
                    if (!state.IsExit && i % 11 == 0 && Mathf.Sin(Time.time * 3f + i) > 0.86f)
                        pulse *= 0.28f;
                    state.Light.enabled = true;
                    state.Light.color = Color.Lerp(state.Color,
                        state.IsExit ? exitColor : emergencyColor, wave);
                    state.Light.intensity = Mathf.Lerp(state.Intensity,
                        Mathf.Max(0.8f, state.Intensity * pulse), wave);
                }
                else if (chargingPhase && state.Delay * lightWaveSpeed <= dockLightRadius)
                {
                    state.Light.enabled = state.Enabled;
                    state.Light.color = Color.Lerp(state.Color, chargingColor, progress * 0.7f);
                    state.Light.intensity = state.Intensity * (1f + progress * 0.3f);
                }
                else
                {
                    RestoreLight(state);
                }
            }
        }

        private void EmitNearbyEffects()
        {
            if (Camera.main == null) return;
            if (_doorSites.Count > 0 && _effectIndex % 3 == 0)
            {
                foreach (var door in _doorSites)
                {
                    if (door == null || Vector3.Distance(door.position, Camera.main.transform.position) > 22f)
                        continue;
                    var sparkPosition = door.position + Vector3.up * 1.8f;
                    EmitParticles(sparkPosition, false);
                    GameAudioRuntime.AtPoint("map_ambience/electrical_flicker", sparkPosition);
                    _effectIndex++;
                    return;
                }
            }
            if (_effectSites.Count == 0) return;
            for (int step = 0; step < _effectSites.Count; step++)
            {
                _effectIndex = (_effectIndex + 7) % _effectSites.Count;
                var site = _effectSites[_effectIndex];
                if (site == null || Vector3.Distance(site.position, Camera.main.transform.position) > 32f)
                    continue;
                bool steam = _effectIndex % 2 == 0;
                var position = steam
                    ? site.position + Vector3.down * 3f
                    : site.position;
                EmitParticles(position, steam);
                if (!steam) GameAudioRuntime.AtPoint("map_ambience/electrical_flicker", position);
                break;
            }
        }

        private static void EmitParticles(Vector3 position, bool steam)
        {
            var owner = new GameObject(steam ? "Zone3 Steam Burst" : "Zone3 Electrical Sparks");
            owner.transform.position = position;
            var particles = owner.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.playOnAwake = false;
            main.duration = 1.5f;
            main.loop = false;
            main.startLifetime = steam ? 1.4f : 0.42f;
            main.startSpeed = steam ? 1.1f : 3.2f;
            main.startSize = steam ? 0.55f : 0.1f;
            main.startColor = steam ? new Color(0.72f, 0.82f, 0.85f, 0.32f)
                : new Color(1f, 0.53f, 0.08f, 1f);
            main.gravityModifier = steam ? -0.1f : 0.6f;
            var emission = particles.emission;
            emission.rateOverTime = 0f;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = steam ? 18f : 55f;
            particles.Play();
            particles.Emit(steam ? 25 : 16);
            Destroy(owner, 2.5f);
        }

        private void RestoreLights()
        {
            foreach (var light in _lights) RestoreLight(light);
        }

        private static void RestoreLight(LightState state)
        {
            if (state.Light == null) return;
            state.Light.enabled = state.Enabled;
            state.Light.color = state.Color;
            state.Light.intensity = state.Intensity;
        }

        private void OnDestroy()
        {
            RestoreLights();
            if (_alarm != null) _alarm.Stop();
        }
    }
}
