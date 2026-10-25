using System.Collections.Generic;
using UnityEngine;

namespace EchoProtocol.Audio
{
    public enum PurgeVoiceKind { FishcatIdle, FishcatAlert, AlienIdle, FishcatAttack }

    // Per-listener arbitration after all creatures have submitted this frame's calls.
    [DefaultExecutionOrder(500)]
    public sealed class PurgeVoiceBudget : MonoBehaviour
    {
        private struct Call
        {
            public AudioSource Source;
            public AudioClip Clip;
            public PurgeVoiceKind Kind;
            public float Volume;
            public bool LocalThreat;
        }
        private static PurgeVoiceBudget _instance;
        private readonly List<Call> _pending = new List<Call>(16);
        private readonly AudioSource[] _active = new AudioSource[4];
        private readonly float[] _busyUntil = new float[4];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic() => _instance = null;

        private static bool Audible(AudioSource source, PurgeVoiceKind kind, Camera camera)
        {
            if (source == null || !source.isActiveAndEnabled || camera == null) return false;
            float range = Mathf.Min(source.maxDistance, kind == PurgeVoiceKind.FishcatIdle ? 18f : 34f);
            return (source.transform.position - camera.transform.position).sqrMagnitude < range * range;
        }

        public static bool Request(AudioSource source, AudioClip clip, PurgeVoiceKind kind, float volume, bool localThreat = false)
        {
            if (clip == null || !Audible(source, kind, Camera.main)) return false;
            if (_instance != null && _instance._active[(int)kind] == source && source.clip == clip
                && Time.unscaledTime < _instance._busyUntil[(int)kind]) return true;
            if (_instance == null)
            {
                var owner = new GameObject("Purge Voice Budget");
                if (Application.isPlaying) DontDestroyOnLoad(owner);
                _instance = owner.AddComponent<PurgeVoiceBudget>();
            }
            for (int i = 0; i < _instance._pending.Count; i++)
                if (_instance._pending[i].Source == source && _instance._pending[i].Kind == kind) return false;
            _instance._pending.Add(new Call { Source = source, Clip = clip, Kind = kind, Volume = volume, LocalThreat = localThreat });
            return false;
        }

        public static void Cancel(AudioSource source)
        {
            if (_instance == null || source == null) return;
            _instance._pending.RemoveAll(call => call.Source == source);
            for (int i = 0; i < _instance._active.Length; i++)
                if (_instance._active[i] == source) { _instance._active[i] = null; _instance._busyUntil[i] = 0f; }
        }

        private void LateUpdate() => Flush(Time.unscaledTime);

        private void Flush(float now)
        {
            var camera = Camera.main;
            for (int category = 0; category < _active.Length; category++)
            {
                var active = _active[category];
                if (active != null && !Audible(active, (PurgeVoiceKind)category, camera))
                { active.Stop(); _active[category] = null; _busyUntil[category] = 0f; }
                if (_active[category] != null && now < _busyUntil[category]) continue;
                int best = -1; float bestScore = float.NegativeInfinity;
                for (int i = 0; i < _pending.Count; i++)
                {
                    var call = _pending[i];
                    if ((int)call.Kind != category || !Audible(call.Source, call.Kind, camera)) continue;
                    float score = (call.LocalThreat ? 100000f : 0f)
                        - (call.Source.transform.position - camera.transform.position).sqrMagnitude;
                    if (score > bestScore) { best = i; bestScore = score; }
                }
                if (best < 0) continue;
                var winner = _pending[best];
                // A voice cannot simultaneously occupy an idle and alert slot.
                for (int i = 0; i < _active.Length; i++)
                    if (_active[i] == winner.Source) { _active[i] = null; _busyUntil[i] = 0f; }
                winner.Source.Stop(); winner.Source.clip = winner.Clip;
                winner.Source.volume = winner.Volume;
                if (Application.isPlaying) winner.Source.Play();
                _active[category] = winner.Source;
                _busyUntil[category] = now + Mathf.Max(winner.Clip.length,
                    winner.Kind == PurgeVoiceKind.FishcatAlert ? 1.8f : .5f);
            }
            _pending.Clear();
        }

        private void OnDestroy() { if (_instance == this) _instance = null; }
    }
}
