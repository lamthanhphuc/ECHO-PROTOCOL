using UnityEngine;

namespace EchoProtocol.Audio
{
    // Presentation preferences never change hearing, scanner results or AED inputs.
    public static class HorrorPresentationSettings
    {
        public static bool AmbienceEnabled => PlayerPrefs.GetInt("Echo.Audio.RareAmbience", 1) != 0;
        public static int EchoIntensity => Mathf.Clamp(PlayerPrefs.GetInt("Echo.Audio.EchoIntensity", 2), 0, 2);
        public static void SetAmbience(bool enabled)
        { PlayerPrefs.SetInt("Echo.Audio.RareAmbience", enabled ? 1 : 0); PlayerPrefs.Save(); }
        public static void SetEchoIntensity(int intensity)
        { PlayerPrefs.SetInt("Echo.Audio.EchoIntensity", Mathf.Clamp(intensity, 0, 2)); PlayerPrefs.Save(); }
    }

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
