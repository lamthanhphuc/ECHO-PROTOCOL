using EchoProtocol.AI.Common;

namespace EchoProtocol.AI.Stalker
{
    public readonly struct StalkerTargetStatus
    {
        public StalkerTargetStatus(PlayerId playerId, StalkerTargetEligibilityResult eligibility)
        {
            PlayerId = playerId;
            Eligibility = eligibility;
        }

        public PlayerId PlayerId { get; }

        public StalkerTargetEligibilityResult Eligibility { get; }
    }
}
