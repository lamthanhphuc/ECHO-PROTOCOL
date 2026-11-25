using System;
using System.Collections.Generic;

namespace EchoProtocol.Networking.Loading
{
    public enum OpeningCutsceneState { NotStarted, Playing, Completed }

    /// <summary>Deterministic rules shared by authority and presentation; no local clock or Unity state.</summary>
    public static class OpeningCutsceneRules
    {
        public const float StartLeadSeconds = 1.5f;
        public const float FadeSeconds = 0.65f;
        public const float MaximumDuration = 60f;

        public static bool ValidDuration(double duration) => !double.IsNaN(duration)
            && !double.IsInfinity(duration) && duration >= 1d && duration <= MaximumDuration;

        public static bool GameplayReleased(bool hasOpening, OpeningCutsceneState state) =>
            !hasOpening || state == OpeningCutsceneState.Completed;

        public static double Elapsed(int now, int start, int tickRate) => tickRate <= 0
            ? 0d : Math.Max(0d, ((long)now - start) / (double)tickRate);

        public static int CountVotes(IEnumerable<int> connected, IEnumerable<int> votes)
        {
            var eligible = new HashSet<int>(connected);
            eligible.IntersectWith(votes);
            return eligible.Count;
        }

        public static bool Unanimous(IEnumerable<int> connected, IEnumerable<int> votes)
        {
            var eligible = new HashSet<int>(connected);
            return eligible.Count > 0 && CountVotes(eligible, votes) == eligible.Count;
        }

        public static bool CanVote(OpeningCutsceneState state, int source,
            IEnumerable<int> connected, IEnumerable<int> votes) => state == OpeningCutsceneState.Playing
            && new HashSet<int>(connected).Contains(source) && !new HashSet<int>(votes).Contains(source);
    }
}
