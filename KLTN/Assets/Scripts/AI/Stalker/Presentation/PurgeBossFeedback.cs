using EchoProtocol.AI.Stalker.Networking;
using EchoProtocol.Audio;
using EchoProtocol.Networking;
using UnityEngine;

namespace EchoProtocol.AI.Stalker.Presentation
{
    /// <summary>Local audiovisual feedback for the networked PURGE boss.</summary>
    [DisallowMultipleComponent]
    public sealed class PurgeBossFeedback : MonoBehaviour
    {
        [SerializeField] private StalkerFusionRuntime boss;
        [SerializeField] private AudioSource phaseSource;
        [SerializeField] private AudioSource voiceSource;
        [SerializeField] private AudioSource uvSource;
        [SerializeField] private AudioSource impactSource;
        [SerializeField] private AudioClip warningImpact;
        [SerializeField] private AudioClip huntingSting;
        [SerializeField] private AudioClip uvHiss;
        [SerializeField] private AudioClip staggerCry;
        [SerializeField] private AudioClip recoveryGrowl;
        [SerializeField] private AudioClip attackImpact;
        [SerializeField] private AudioClip[] heavyFootsteps;
        [SerializeField] private ParticleSystem uvContactParticles;
        [SerializeField] private ParticleSystem uvStaggerParticles;
        [SerializeField] private ParticleSystem attackContactParticles;
        [SerializeField] private Light uvFlash;

        private PurgeBossMood _lastMood;
        private long _lastAttackResolvedTick;
        private bool _bound;
        private bool _uvActive;
        private float _flashUntil;
        private Collider _bodyCollider;
        private Animator _visualAnimator;
        private Vector3 _lastStepPosition;
        private float _lastStepPhase;
        private int _lastStepState;
        private int _stepIndex;
        private static readonly int Walk1Hash = Animator.StringToHash("Walk1");
        private static readonly int Walk2Hash = Animator.StringToHash("Walk2");

        public bool IsActive => _bound;

        private void Awake()
        {
            if (boss == null) boss = GetComponent<StalkerFusionRuntime>();
            _bodyCollider = GetComponent<Collider>();
            foreach (var candidate in GetComponentsInChildren<Animator>(true))
                if (candidate.enabled && candidate.runtimeAnimatorController != null)
                {
                    _visualAnimator = candidate;
                    break;
                }
            GameAudioSettings.RouteEffects(phaseSource);
            GameAudioSettings.RouteEffects(voiceSource);
            GameAudioSettings.RouteEffects(uvSource);
            GameAudioSettings.RouteEffects(impactSource);
            if (phaseSource != null)
            {
                phaseSource.spatialBlend = 1f;
                phaseSource.rolloffMode = AudioRolloffMode.Linear;
                phaseSource.minDistance = 4f; phaseSource.maxDistance = 60f;
                phaseSource.dopplerLevel = 0f;
            }
            PurgeSpatialAudio.Attach(phaseSource, transform);
            PurgeSpatialAudio.Attach(voiceSource, transform);
            PurgeSpatialAudio.Attach(uvSource, transform);
            PurgeSpatialAudio.Attach(impactSource, transform);
            if (uvFlash != null) uvFlash.enabled = false;
        }

        private void OnDisable() => ResetFeedback();

        private void Update()
        {
            var match = NetworkMatchState.Instance;
            if (match == null || match.IsEnded || boss == null || boss.Object == null
                || !boss.Object.IsValid || match.PurgeBossId != boss.Object.Id)
            {
                ResetFeedback();
                return;
            }

            var mood = match.PurgeMood;
            var presentation = boss.GetReplicatedPresentationState();
            if (!_bound)
            {
                _lastMood = mood;
                _lastAttackResolvedTick = presentation.AttackResolvedTick;
                _lastStepPosition = transform.position;
                _lastStepState = 0;
                _bound = true;
            }
            else
            {
                if (mood != _lastMood) OnMoodChanged(_lastMood, mood);
                if (presentation.AttackResolvedTick != _lastAttackResolvedTick)
                {
                    if (presentation.AttackHitMomentResolved
                        && presentation.AttackOutcome == StalkerAttackOutcome.Hit)
                        PlayAttackImpact();
                    _lastAttackResolvedTick = presentation.AttackResolvedTick;
                }
                _lastMood = mood;
            }

            SetUvContact(match.PurgeUvContact
                && (mood == PurgeBossMood.Calm || mood == PurgeBossMood.Hunting));
            if (_uvActive) UpdateUvContactPosition();
            PurgeVfx.SetLoop(uvContactParticles, _uvActive, 32f, -1f);
            PurgeVfx.Cull(uvStaggerParticles, 32f);
            PurgeVfx.Cull(attackContactParticles, 25f);
            UpdateFootsteps(mood);
            if (uvSource != null)
            {
                uvSource.volume = Mathf.MoveTowards(uvSource.volume, _uvActive ? 0.5f : 0f,
                    Time.deltaTime * 3f);
                if (!_uvActive && uvSource.volume <= 0f && uvSource.isPlaying) uvSource.Stop();
            }
            if (uvFlash != null)
            {
                var staggerRemaining = mood == PurgeBossMood.Staggered && match.Runner != null
                    ? match.PurgeMoodTimer.RemainingTime(match.Runner) ?? 0f : 0f;
                var intensity = Mathf.Clamp01((_flashUntil - Time.time) / 0.3f) * 1.6f
                    + Mathf.Min(0.7f, staggerRemaining * 0.2f);
                uvFlash.intensity = intensity;
                uvFlash.enabled = intensity > 0f && PurgeVfx.InRange(uvFlash.transform.position, 32f);
            }
        }

        private void OnMoodChanged(PurgeBossMood previous, PurgeBossMood current)
        {
            if (current == PurgeBossMood.Warning)
                Play(phaseSource, warningImpact, 0.8f);
            else if (current == PurgeBossMood.Hunting)
            {
                if (previous == PurgeBossMood.Warning) Play(phaseSource, huntingSting, 0.85f);
                else if (previous == PurgeBossMood.Staggered) Play(voiceSource, recoveryGrowl, 0.8f);
            }
            else if (current == PurgeBossMood.Staggered)
            {
                Play(voiceSource, staggerCry, 0.9f);
                PurgeVfx.Burst(uvStaggerParticles, 32f);
                _flashUntil = Time.time + 0.3f;
            }
        }

        private void SetUvContact(bool active)
        {
            if (_uvActive == active) return;
            _uvActive = active;
            if (active && uvSource != null && uvHiss != null)
            {
                uvSource.clip = uvHiss;
                uvSource.loop = true;
                uvSource.volume = 0f;
                uvSource.Play();
            }
        }

        private void PlayAttackImpact()
        {
            var position = transform.position + transform.forward * 1.1f + Vector3.up;
            if (boss.Runner != null && boss.TargetPlayer.IsRealPlayer
                && boss.Runner.TryGetPlayerObject(boss.TargetPlayer, out var target) && target != null)
                position = target.transform.position + Vector3.up;
            if (attackContactParticles != null)
            {
                attackContactParticles.transform.position = position;
                PurgeVfx.Burst(attackContactParticles, 25f);
            }
            if (impactSource != null) impactSource.transform.position = position;
            Play(impactSource, attackImpact, 0.75f);
        }

        private void UpdateUvContactPosition()
        {
            if (uvContactParticles == null || boss.Runner == null) return;
            var closestBeamDistance = float.PositiveInfinity;
            var contactPoint = transform.position + Vector3.up * 1.2f;
            foreach (var player in boss.Runner.ActivePlayers)
            {
                if (!boss.Runner.TryGetPlayerObject(player, out var obj) || obj == null
                    || !obj.TryGetComponent<NetworkPlayerFlashlight>(out var flashlight)
                    || !flashlight.ResonanceActive || !flashlight.ResonanceHasValidHit) continue;
                var origin = obj.transform.position + Vector3.up * 1.65f;
                var direction = (flashlight.ResonanceEndpoint - origin).normalized;
                var towardBoss = (_bodyCollider != null ? _bodyCollider.bounds.center : contactPoint) - origin;
                var alongBeam = Vector3.Dot(towardBoss, direction);
                if (alongBeam < 0f || alongBeam > NetworkPlayerFlashlight.ResonanceRange) continue;
                var beamDistance = Vector3.Cross(direction, towardBoss).magnitude;
                if (beamDistance >= closestBeamDistance || beamDistance > 1.5f) continue;
                closestBeamDistance = beamDistance;
                contactPoint = _bodyCollider != null
                    ? _bodyCollider.ClosestPoint(origin) : contactPoint;
                if (_bodyCollider != null && _bodyCollider.Raycast(
                        new Ray(origin, direction), out var hit, NetworkPlayerFlashlight.ResonanceRange))
                    contactPoint = hit.point;
            }
            uvContactParticles.transform.position = contactPoint;
        }

        private void UpdateFootsteps(PurgeBossMood mood)
        {
            var travel = Vector3.ProjectOnPlane(transform.position - _lastStepPosition,
                Vector3.up).magnitude;
            _lastStepPosition = transform.position;
            if (_visualAnimator == null || mood == PurgeBossMood.Warning
                || mood == PurgeBossMood.Staggered || travel < 0.001f || travel > 3f
                || heavyFootsteps == null || heavyFootsteps.Length == 0)
            {
                _lastStepState = 0;
                return;
            }
            var state = _visualAnimator.GetCurrentAnimatorStateInfo(0);
            if (_visualAnimator.IsInTransition(0)
                || (state.shortNameHash != Walk1Hash && state.shortNameHash != Walk2Hash))
            {
                _lastStepState = 0;
                return;
            }
            var phase = state.normalizedTime * 2f;
            if (_lastStepState != state.shortNameHash)
            {
                _lastStepState = state.shortNameHash;
                _lastStepPhase = phase;
                return;
            }
            // Two alternating foot plants per animation cycle. Ignore jumps caused by
            // animation resync so a skipped frame cannot produce a burst of steps.
            if (phase < _lastStepPhase || phase - _lastStepPhase > 0.5f)
            {
                _lastStepPhase = phase;
                return;
            }
            var crossedPlant = Mathf.FloorToInt(phase + 0.5f)
                > Mathf.FloorToInt(_lastStepPhase + 0.5f);
            _lastStepPhase = phase;
            if (!crossedPlant) return;
            var clip = heavyFootsteps[_stepIndex++ % heavyFootsteps.Length];
            if (impactSource != null) impactSource.transform.position = transform.position;
            Play(impactSource, clip, state.shortNameHash == Walk2Hash ? 0.7f : 0.5f);
        }

        private void ResetFeedback()
        {
            _bound = false;
            _uvActive = false;
            _lastStepState = 0;
            if (phaseSource != null) phaseSource.Stop();
            if (voiceSource != null) voiceSource.Stop();
            if (uvSource != null) uvSource.Stop();
            if (impactSource != null) impactSource.Stop();
            if (uvContactParticles != null)
                uvContactParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (uvStaggerParticles != null)
                uvStaggerParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (attackContactParticles != null)
                attackContactParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (uvFlash != null) uvFlash.enabled = false;
        }

        private static void Play(AudioSource source, AudioClip clip, float volume)
        {
            if (source != null && clip != null && PurgeVfx.InRange(source.transform.position, source.maxDistance))
                source.PlayOneShot(clip, volume);
        }
    }
}
