using System.Collections.Generic;
using UnityEngine;

namespace EchoProtocol.Audio
{
    public sealed class EnvironmentAudioAnchor : MonoBehaviour
    {
        public string cue;
        [Range(.01f, .5f)] public float gain = .18f;
        [Range(4, 40)] public float range = 24f;
        [Range(.5f, 8)] public float duration = 4f;
        public static readonly HashSet<EnvironmentAudioAnchor> Active = new HashSet<EnvironmentAudioAnchor>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry() => Active.Clear();
        private void OnEnable() => Active.Add(this);
        private void OnDisable() => Active.Remove(this);
    }
}
