using System;
namespace EchoProtocol.Voice
{
    // Fast initial attempts, then a bounded background retry while Fusion remains valid.
    public static class VoiceRetryPolicy
    {
        public static float DelaySeconds(int failures) => failures < 3
            ? (float)Math.Pow(2, Math.Max(1, failures)) : failures < 5 ? 30f : 60f;
    }
}
