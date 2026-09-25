using UnityEngine;

namespace EchoProtocol.AI.Stalker.Presentation
{
    [DisallowMultipleComponent]
    public sealed class StalkerAnimationAudioEvents : MonoBehaviour
    {
        private StalkerAudioController _audio;

        public void Bind(StalkerAudioController audio) => _audio = audio;

        public void PlayFootstep() => _audio?.PlayFootstep();
        public void PlaySniff() => _audio?.PlaySniff();
        public void PlayBite() => _audio?.PlayBiteFromAnimation();
        public void PlayPunch() => _audio?.PlayPunch();
        public void PlayDetect() => _audio?.PlayDetectFromAnimation();
        public void PlayJumpOut() => _audio?.PlayJumpOut();
        public void PlayJumpIn() => _audio?.PlayJumpIn();
    }
}
