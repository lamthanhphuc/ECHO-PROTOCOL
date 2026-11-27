using UnityEngine;

namespace EchoProtocol.Audio
{
    public sealed class HorrorPresentationRules
    {
        public static bool ConsumeOrdinal(uint incoming, ref uint consumed)
        {
            if (incoming == 0 || incoming <= consumed) return false;
            consumed = incoming; return true;
        }
        public float NextEventAt { get; private set; }
        public float RecoveryUntil { get; private set; }
        public float NextEchoAt { get; private set; }
        public int LastCue { get; private set; } = -1;
        public int PreviousCue { get; private set; } = -1;

        public void Reset(float now)
        { NextEventAt = now + 60f; RecoveryUntil = now; NextEchoAt = now + 180f; LastCue = PreviousCue = -1; }

        public bool Ready(float now, bool blocked)
        {
            if (blocked) RecoveryUntil = now + 20f;
            return !blocked && now >= RecoveryUntil && now >= NextEventAt;
        }

        public bool CanChoose(int cue) => cue >= 0 && cue != LastCue && cue != PreviousCue;
        public void Presented(int cue, float now, float delay, bool echo)
        {
            PreviousCue = LastCue; LastCue = cue;
            NextEventAt = now + Mathf.Max(60f, delay);
            if (echo) NextEchoAt = now + 300f;
        }

        public static string StepPrefix(bool metal, bool softGround, bool crouching, bool sprinting)
            => metal ? "player/metal_footstep_" : softGround || crouching ? "player/crouch_footstep_"
                : sprinting ? "player/sprint_footstep_" : "player/concrete_footstep_";
    }
}
