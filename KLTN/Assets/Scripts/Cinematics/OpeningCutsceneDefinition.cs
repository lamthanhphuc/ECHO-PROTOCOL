using System;
using EchoProtocol.Networking.Loading;
using UnityEngine;
using UnityEngine.Timeline;

namespace EchoProtocol.Cinematics
{
    [CreateAssetMenu(menuName = "ECHO Protocol/Opening Cutscene")]
    public sealed class OpeningCutsceneDefinition : ScriptableObject
    {
        [Serializable]
        public sealed class Shot
        {
            public string name;
            public string sourceHierarchy;
            public Vector3 startPosition, endPosition, startEuler, endEuler;
            [Min(1f)] public float duration = 6f;
            [TextArea] public string subtitle;
            [TextArea] public string subtitleEnglish;
            public string audioKey;
            public AudioClip audioOverride;
            [Range(0f, 1f)] public float fillMultiplier = 1f;
        }

        public string sceneName = "SciFi";
        public TimelineAsset timeline;
        public bool hidePlayers;
        public Shot[] shots = Array.Empty<Shot>();
        [Range(30f, 85f)] public float fieldOfView = 58f;
        [Min(0.2f)] public float holdToSkipSeconds = 1f;
        public float Duration => timeline != null ? (float)timeline.duration : 0f;
        public bool IsPlayable => timeline != null && OpeningCutsceneRules.ValidDuration(timeline.duration)
            && shots != null && shots.Length > 0
            && Array.TrueForAll(shots, shot => shot != null && shot.duration > 0 && !float.IsNaN(shot.duration) && !float.IsInfinity(shot.duration));

        public int ShotIndex(double elapsed)
        {
            double end = 0;
            for (int i = 0; i < shots.Length; i++)
            {
                end += shots[i].duration;
                if (elapsed < end) return i;
            }
            return Math.Max(0, shots.Length - 1);
        }
    }
}
