using EchoProtocol.Audio;
using Fusion;
using UnityEngine;

namespace EchoProtocol.AI.Minions
{
    /// <summary>Local, spatial feedback for the PURGE Fishcat visual on every peer.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CreepMinionRuntime))]
    public sealed class PurgeFishcatFeedback : MonoBehaviour
    {
        [SerializeField] private AudioClip alertScreech;
        [SerializeField] private AudioClip idleBreath;
        [SerializeField] private AudioClip[] alertVariations;
        [SerializeField] private AudioClip[] idleVariations;
        [SerializeField] private AudioClip attackScream;
        [SerializeField] private AudioClip uvHiss;
        [SerializeField] private AudioClip uvDeath;
        [SerializeField] private AudioClip[] landSteps;
        [SerializeField] private Material particleMaterial;

        private ParticleSystem uvExposureParticles;
        private ParticleSystem spearImpactParticles;
        private ParticleSystem uvDeathParticles;

        private static readonly int WalkHash = Animator.StringToHash("Walk");
        private static readonly int RunHash = Animator.StringToHash("Run");
        private static readonly int AttackHash = Animator.StringToHash("Attack01");
        private float _alertPendingUntil;
        private int _lastAlertVariant = -1, _lastIdleVariant = -1;
        private AudioClip _pendingAlertClip;

        private CreepMinionRuntime _minion;
        private Animator _animator;
        private AudioSource _voice;
        private AudioSource _foley;
        private AudioSource _uv;
        private uint _attackSequence;
        private uint _hitSequence;
        private bool _initialized;
        private bool _hadTarget;
        private bool _wasUv;
        private bool _wasDying;
        private bool _awaitingThrust;
        private float _attackStartedAt;
        private float _nextIdleTime;
        private float _lastStepPhase;
        private int _lastStepState;
        private int _stepIndex;
        private Vector3 _lastPosition;

        private void Awake()
        {
            _minion = GetComponent<CreepMinionRuntime>();
        }

        private AudioSource CreateSource(string sourceName, float maxDistance)
        {
            var child = transform.Find("Fishcat " + sourceName);
            if (child == null)
            {
                var objectForAudio = new GameObject("Fishcat " + sourceName);
                objectForAudio.transform.SetParent(transform, false);
                child = objectForAudio.transform;
            }
            var source = child.GetComponent<AudioSource>();
            if (source == null) source = child.gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 2f;
            source.maxDistance = maxDistance;
            source.dopplerLevel = 0f;
            child.localPosition = Vector3.up * .65f;
            GameAudioSettings.RouteEffects(source);
            PurgeSpatialAudio.Attach(source, transform);
            return source;
        }

        private void Update()
        {
            if (_minion == null || _minion.Object == null || !_minion.Object.IsValid
                || _minion.Role != CreepMinionRole.Catfish) return;

            if (!_initialized)
            {
                _voice = CreateSource("Voice", 34f);
                _foley = CreateSource("Foley", 22f);
                _uv = CreateSource("UV", 25f);
                _uv.loop = true;
                _uv.clip = uvHiss;
                _uv.volume = 0.35f;
                uvExposureParticles = CreateParticles("Fishcat UV Exposure", true, 8f,
                    0.45f, 0.07f, new Color(0.73f, 0.56f, 1f, 0.6f));
                spearImpactParticles = CreateParticles("Fishcat Spear Contact", false, 0f,
                    0.2f, 0.09f, new Color(0.95f, 0.78f, 0.65f, 0.7f));
                var impactEmission = spearImpactParticles.emission;
                impactEmission.SetBursts(new[] { new ParticleSystem.Burst(0f, 5) });
                uvDeathParticles = CreateParticles("Fishcat UV Death", true, 8f,
                    0.9f, 0.10f, new Color(0.65f, 0.48f, 1f, 0.48f));
                if (uvExposureParticles != null) uvExposureParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                if (spearImpactParticles != null) spearImpactParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                if (uvDeathParticles != null) uvDeathParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                _initialized = true;
                _attackSequence = _minion.AttackSequence;
                _hitSequence = _minion.ConfirmedHitSequence;
                _hadTarget = _minion.TargetPlayer.IsRealPlayer;
                _wasUv = false;
                _wasDying = _minion.IsDying;
                PurgeVfx.SetLoop(uvDeathParticles, _wasDying, 28f, 8f * _minion.DeathVisualAlpha);
                _lastPosition = transform.position;
                _nextIdleTime = Time.time + Random.Range(3f, 7f);
                FindVisualAnimator();
                return;
            }

            if (_animator == null || !_animator.gameObject.activeInHierarchy) FindVisualAnimator();
            bool dying = _minion.IsDying;
            bool uv = _minion.IsUvStunned && !dying;
            bool hasTarget = _minion.TargetPlayer.IsRealPlayer;
            PurgeVfx.Cull(spearImpactParticles, 25f);
            PurgeVfx.SetLoop(uvExposureParticles, uv, 28f, 8f);
            PurgeVfx.SetLoop(uvDeathParticles, dying, 28f, 8f * _minion.DeathVisualAlpha);

            if (hasTarget && !_hadTarget || uv && !_wasUv || dying && !_wasDying)
            { PurgeVoiceBudget.Cancel(_voice); _voice.Stop(); }

            if (dying && !_wasDying)
            {
                StopUv();
                PlayVoice(uvDeath, .85f);
            }
            if (dying)
            {
                if (uvDeathParticles != null)
                {
                    var emission = uvDeathParticles.emission;
                    emission.rateOverTime = 8f * _minion.DeathVisualAlpha;
                }
                _wasDying = true;
                _lastPosition = transform.position;
                return;
            }

            if (uv != _wasUv)
            {
                if (uv)
                {
                    if (uvHiss != null) { _uv.clip = uvHiss; _uv.Play(); }
                }
                else StopUv();
                _wasUv = uv;
            }

            if (hasTarget && !_hadTarget)
            {
                _alertPendingUntil = Time.time + 1.2f;
                _pendingAlertClip = Pick(alertVariations, alertScreech, ref _lastAlertVariant);
            }
            if (hasTarget && !uv && Time.time <= _alertPendingUntil)
                if (PurgeVoiceBudget.Request(_voice, _pendingAlertClip,
                    PurgeVoiceKind.FishcatAlert, .8f,
                    _minion.Runner != null && _minion.TargetPlayer == _minion.Runner.LocalPlayer))
                    _alertPendingUntil = 0f;
            _hadTarget = hasTarget;

            if (_minion.AttackSequence != _attackSequence)
            {
                _attackSequence = _minion.AttackSequence;
                _awaitingThrust = _minion.AttackKind == CreepMinionAttackKind.MeleeSlow;
                _attackStartedAt = Time.time;
            }
            if (_awaitingThrust)
            {
                var state = _animator != null ? _animator.GetCurrentAnimatorStateInfo(0) : default;
                bool mouthOpen = _animator != null && state.shortNameHash == AttackHash
                    && state.normalizedTime >= 0.09f;
                if (mouthOpen || Time.time - _attackStartedAt >= 0.35f)
                {
                    // Prefer the mouth-opening frame; delayed network presentation still gets its attack cue.
                    PurgeVoiceBudget.Request(_voice, attackScream,
                        PurgeVoiceKind.FishcatAttack, .45f,
                        _minion.Runner != null && _minion.TargetPlayer == _minion.Runner.LocalPlayer);
                    _awaitingThrust = false;
                }
            }
            if (_minion.ConfirmedHitSequence != _hitSequence)
            {
                _hitSequence = _minion.ConfirmedHitSequence;
                if (spearImpactParticles != null)
                {
                    spearImpactParticles.transform.position = _minion.ConfirmedHitPosition;
                    PurgeVfx.Burst(spearImpactParticles, 25f);
                }
            }

            if (!uv && !hasTarget && _minion.State == CreepMinionState.Roam
                && Time.time >= _nextIdleTime)
            {
                PurgeVoiceBudget.Request(_voice, Pick(idleVariations, idleBreath, ref _lastIdleVariant),
                    PurgeVoiceKind.FishcatIdle, .48f);
                _nextIdleTime = Time.time + Random.Range(9f, 15f);
            }
            UpdateSteps(uv);
        }

        private void FindVisualAnimator()
        {
            foreach (var animator in GetComponentsInChildren<Animator>(true))
                if (animator.gameObject.activeInHierarchy && animator.runtimeAnimatorController != null)
                { _animator = animator; return; }
        }

        private ParticleSystem CreateParticles(string name, bool loop, float rate,
            float lifetime, float size, Color color)
        {
            var child = transform.Find(name);
            if (child == null)
            {
                var objectForParticles = new GameObject(name);
                objectForParticles.transform.SetParent(transform, false);
                child = objectForParticles.transform;
            }
            child.localPosition = new Vector3(0f, 0.65f, 0f);
            var system = child.GetComponent<ParticleSystem>();
            if (system == null) system = child.gameObject.AddComponent<ParticleSystem>();
            var main = system.main;
            main.playOnAwake = false;
            main.loop = loop;
            main.startLifetime = lifetime;
            main.startSpeed = 0.28f;
            main.startSize = size;
            main.startColor = color;
            main.maxParticles = 40;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = system.emission;
            emission.rateOverTime = rate;
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.32f;
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            if (particleMaterial != null) renderer.sharedMaterial = particleMaterial;
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return system;
        }

        private void UpdateSteps(bool uv)
        {
            float travel = Vector3.ProjectOnPlane(transform.position - _lastPosition, Vector3.up).magnitude;
            _lastPosition = transform.position;
            if (uv || !_minion.IsOnLand || !_minion.IsMoving || travel < 0.001f || travel > 3f
                || _animator == null || landSteps == null || landSteps.Length == 0)
            { _lastStepState = 0; return; }
            var state = _animator.GetCurrentAnimatorStateInfo(0);
            if (_animator.IsInTransition(0)
                || state.shortNameHash != WalkHash && state.shortNameHash != RunHash)
            { _lastStepState = 0; return; }
            float phase = state.normalizedTime * 2f;
            if (_lastStepState != state.shortNameHash)
            { _lastStepState = state.shortNameHash; _lastStepPhase = phase; return; }
            if (phase < _lastStepPhase || phase - _lastStepPhase > 0.5f)
            { _lastStepPhase = phase; return; }
            bool planted = Mathf.FloorToInt(phase + 0.5f) > Mathf.FloorToInt(_lastStepPhase + 0.5f);
            _lastStepPhase = phase;
            if (planted) Play(_foley, landSteps[_stepIndex++ % landSteps.Length],
                state.shortNameHash == RunHash ? 0.45f : 0.32f);
        }

        private void StopUv()
        {
            if (_uv != null) _uv.Stop();
            if (uvExposureParticles != null)
                uvExposureParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            _wasUv = false;
        }

        private void OnDisable()
        {
            _initialized = false;
            _wasDying = false;
            _awaitingThrust = false;
            _alertPendingUntil = 0f;
            PurgeVoiceBudget.Cancel(_voice);
            StopUv();
            if (_voice != null) _voice.Stop();
            if (_foley != null) _foley.Stop();
            if (uvDeathParticles != null) uvDeathParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (uvExposureParticles != null) uvExposureParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (spearImpactParticles != null) spearImpactParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private static AudioClip Pick(AudioClip[] variants, AudioClip fallback, ref int last)
        {
            if (variants == null || variants.Length == 0) return fallback;
            int next = last < 0 ? Random.Range(0, variants.Length)
                : variants.Length > 1 ? (last + Random.Range(1, variants.Length)) % variants.Length : 0;
            last = next;
            return variants[next] != null ? variants[next] : fallback;
        }

        private void PlayVoice(AudioClip clip, float volume)
        {
            if (_voice == null || clip == null || !PurgeVfx.InRange(_voice.transform.position, _voice.maxDistance)) return;
            _voice.Stop(); _voice.clip = clip; _voice.volume = volume; _voice.Play();
        }

        private static void Play(AudioSource source, AudioClip clip, float volume)
        {
            if (source != null && clip != null && PurgeVfx.InRange(source.transform.position, source.maxDistance))
                source.PlayOneShot(clip, volume);
        }
    }
}
