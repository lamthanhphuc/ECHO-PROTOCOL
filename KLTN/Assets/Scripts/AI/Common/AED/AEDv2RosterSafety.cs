namespace EchoProtocol.AI.Common.AED
{
    public sealed class AEDv2RosterSafety
    {
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
