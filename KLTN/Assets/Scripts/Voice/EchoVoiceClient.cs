using Photon.Voice.Unity;
using UnityEngine;

namespace EchoProtocol.Voice
{
    /// <summary>Standalone Photon Voice client. Fusion only supplies player/session identity.</summary>
    public sealed class EchoVoiceClient : UnityVoiceClient
    {
        private const float GameplayVoiceMinDistance = 10f;
        private const float GameplayVoiceMaxDistance = 30f;

        public VoiceManager Owner { get; set; }

        protected override Speaker InstantiateSpeakerForRemoteVoice(int playerId, byte voiceId, object userData)
        {
            if (Owner == null || !(userData is string key) || !Owner.AcceptStream(key)) return null;
            Owner.RemovePreviousStream(key);
            var go = new GameObject("RemoteVoice");
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.mute = true; // Never play at the origin before the owning avatar is found.
            source.dopplerLevel = 0;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = GameplayVoiceMinDistance;
            source.maxDistance = GameplayVoiceMaxDistance;
            var speaker = go.AddComponent<Speaker>();
            speaker.OnRemoteVoiceRemoveAction += removed => { if (removed != null) Destroy(removed.gameObject); };
            go.AddComponent<VoicePlayerBinding>().Initialize(Owner, key, source, speaker);
            return speaker;
        }
    }
}
