using EchoProtocol.Audio;
using UnityEngine;

namespace EchoProtocol.Networking
{
    /// <summary>Local presentation of confirmed mission states; never emits AI noise.</summary>
    [DisallowMultipleComponent]
    public sealed class AlienHuntAudio : MonoBehaviour
    {
        // Normalized contacts in AlienYoung_Eating. The middle head-lift has no chewing.
        private static readonly float[] BitePhases = { .08f, .20f, .32f, .41f, .96f };
        private GameAudioCatalog _catalog;
        private Animator _animator;
        private AudioSource _voice, _eating, _contact;
        private AlienStage _stage;
        private bool _hasStage;
        private float _lastEatingTime = -1f;
        private float _idleRemaining, _baitCooldown, _burnElapsed;
        private int _chewIndex, _idleIndex;

        public void Initialize(Animator animator)
        {
            _animator = animator;
            if (_voice != null) return;
            _catalog = Resources.Load<GameAudioCatalog>("GameAudioCatalog");
            _voice = CreateSource("Alien Voice", 12f, .6f);
            _eating = CreateSource("Alien Eating", 10f, .35f);
            _contact = CreateSource("Alien Contact", 15f, .25f);
            _chewIndex = Random.Range(0, 3);
            _idleIndex = Random.Range(0, 2);
            _idleRemaining = Random.Range(5f, 10f);
        }

        private AudioSource CreateSource(string sourceName, float range, float height)
        {
            var child = new GameObject(sourceName);
            child.transform.SetParent(transform, false);
            child.transform.localPosition = Vector3.up * height;
            var source = GameAudioRuntime.CreateSource(child, true);
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 1.2f;
            source.maxDistance = range;
            source.priority = 150;
            PurgeSpatialAudio.Attach(source, transform);
            return source;
        }

        public void Present(HuntEntityState state, float delta)
        {
            if (_voice == null) return;
            var stage = (AlienStage)state.Stage;
            _baitCooldown = Mathf.Max(0f, _baitCooldown - delta);
            if (_hasStage && stage != _stage)
            {
                PurgeVoiceBudget.Cancel(_voice);
                Stop(_eating);
                Stop(_voice);
                _lastEatingTime = -1f;
                _burnElapsed = 0f;
                _idleRemaining = Random.Range(8f, 15f);
                if (stage == AlienStage.Carried && (_stage == AlienStage.Eating || _stage == AlienStage.Stunned))
                    Play(_contact, "pickup", .70f);
                else if (_stage == AlienStage.Carried && stage == AlienStage.Stunned)
                    Play(_contact, "drop", .65f);
                else if (_stage == AlienStage.OnPan && stage == AlienStage.Burning)
                    Play(_voice, "burn_start", .75f);
                else if (_stage == AlienStage.Wandering && stage == AlienStage.Running && _baitCooldown <= 0f)
                {
                    Play(_voice, "bait_detect", .50f);
                    _baitCooldown = 3f;
                }
            }
            // Seed the first snapshot without replaying historical interaction/ignition cues.
            _hasStage = true;
            _stage = stage;

            if (stage == AlienStage.Burning)
            {
                _burnElapsed += delta;
                _voice.volume = .75f * EffectsVolume * Mathf.Exp(-_burnElapsed * .9f);
            }
            if (stage == AlienStage.Eating) UpdateEating();
            if (stage == AlienStage.Wandering && !state.Fleeing && !state.Returning)
            {
                _idleRemaining -= delta;
                if (_idleRemaining <= 0f)
                {
                    PurgeVoiceBudget.Request(_voice, _catalog != null
                        ? _catalog.Find("purge_alien/idle_0" + (++_idleIndex % 2 + 1)) : null,
                        PurgeVoiceKind.AlienIdle, .28f * EffectsVolume);
                    _idleRemaining = Random.Range(10f, 18f);
                }
            }
        }

        private void UpdateEating()
        {
            if (_animator == null || !_animator.isActiveAndEnabled || _animator.runtimeAnimatorController == null) return;
            var animation = _animator.GetCurrentAnimatorStateInfo(0);
            if (!animation.IsName("Eating")) return;
            float now = animation.normalizedTime;
            float previous = _lastEatingTime;
            _lastEatingTime = now;
            // No backlog after culling, a late snapshot, or an animation restart.
            if (previous < 0f || now < previous || now - previous > .25f) return;
            foreach (float phase in BitePhases)
                if (Mathf.FloorToInt(now - phase) > Mathf.FloorToInt(previous - phase))
                {
                    Play(_eating, "chew_0" + (++_chewIndex % 3 + 1), .57f);
                    break;
                }
        }

        private float EffectsVolume => _catalog != null ? _catalog.effectsVolume : 1f;

        private void Play(AudioSource source, string cue, float volume)
        {
            var camera = Camera.main;
            if (camera == null || (camera.transform.position - source.transform.position).sqrMagnitude
                > source.maxDistance * source.maxDistance) return;
            var clip = _catalog != null ? _catalog.Find("purge_alien/" + cue) : null;
            if (clip == null) return;
            source.Stop();
            source.clip = clip;
            source.volume = volume * EffectsVolume;
            if (Application.isPlaying) source.Play();
        }

        private static void Stop(AudioSource source)
        {
            if (source == null) return;
            source.Stop();
            source.clip = null;
        }

        public void Silence()
        {
            PurgeVoiceBudget.Cancel(_voice);
            Stop(_voice); Stop(_eating); Stop(_contact);
            _hasStage = false;
            _lastEatingTime = -1f;
            _idleRemaining = Random.Range(5f, 10f);
        }

        private void OnDisable() => Silence();
    }
}
