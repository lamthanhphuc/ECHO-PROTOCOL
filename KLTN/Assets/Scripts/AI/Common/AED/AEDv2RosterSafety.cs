using System.Collections.Generic;
using System.Linq;

namespace EchoProtocol.AI.Common.AED
{
    public sealed class AEDv2RosterSafety
    {
        public const double MinimumActiveObservationSeconds = 30d;

        public static AEDv2RosterSafety FromEvidence(
            IReadOnlyCollection<string> roster,
            IReadOnlyDictionary<string, AEDv2PlayerPhaseEvidence> players)
        {
            bool valid = roster != null && roster.Count > 0 && players != null;

            bool allObserved = valid && roster.All(userId =>
                players.TryGetValue(userId, out var player)
                && player.ActiveObservedSeconds >= MinimumActiveObservationSeconds);

            bool anyStruggling = valid && roster.Any(userId =>
                players.TryGetValue(userId, out var player)
                && (player.DownCount > 0 || player.EliminatedCount > 0));

            return new AEDv2RosterSafety(allObserved, anyStruggling);
        }

        public bool AllPlayersObserved { get; }
        public bool AnyPlayerStruggling { get; }
        public bool AllowPressure => AllPlayersObserved && !AnyPlayerStruggling;

        public AEDv2RosterSafety(bool allPlayersObserved, bool anyPlayerStruggling)
        {
            AllPlayersObserved = allPlayersObserved;
            AnyPlayerStruggling = anyPlayerStruggling;
        }
    }
}
