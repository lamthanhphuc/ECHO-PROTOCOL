using System;
using System.Collections.Generic;
using UnityEngine;

namespace EchoProtocol.Audio
{
    public sealed class GameAudioCatalog : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string key;
            public AudioClip clip;
        }

        [Range(0f, 1f)] public float effectsVolume = 0.65f;
        [Range(0f, 1f)] public float ambienceVolume = 0.12f;
        public Entry[] clips;
        private Dictionary<string, AudioClip> _lookup;

        public AudioClip Find(string key)
        {
            if (_lookup == null)
            {
                _lookup = new Dictionary<string, AudioClip>(StringComparer.Ordinal);
                foreach (var entry in clips)
                    if (entry.clip != null) _lookup[entry.key] = entry.clip;
            }
            return _lookup.TryGetValue(key, out var clip) ? clip : null;
        }
    }
}
