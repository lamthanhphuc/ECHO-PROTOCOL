using Photon.Voice.Unity;
using UnityEngine;

namespace EchoProtocol.Voice
{
    /// <summary>Standalone Photon Voice client. Fusion only supplies player/session identity.</summary>
    public sealed class EchoVoiceClient : UnityVoiceClient
    {
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
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 2;
            source.maxDistance = 15;
            var speaker = go.AddComponent<Speaker>();
            speaker.OnRemoteVoiceRemoveAction += removed => { if (removed != null) Destroy(removed.gameObject); };
            go.AddComponent<VoicePlayerBinding>().Initialize(Owner, key, source, speaker);
            return speaker;
        }
    }
}
