using EchoProtocol.AI.Common;

namespace EchoProtocol.AI.Stalker
{
    public readonly struct StalkerTargetStatus
    {
        public StalkerTargetStatus(PlayerId playerId, StalkerTargetEligibilityResult eligibility)
            : this(playerId, eligibility, false)
        {
        }

        public StalkerTargetStatus(
            PlayerId playerId,
            StalkerTargetEligibilityResult eligibility,
            bool isHidden)
        {
            PlayerId = playerId;
            Eligibility = eligibility;
            IsHidden = isHidden;
        }

        public PlayerId PlayerId { get; }

        public StalkerTargetEligibilityResult Eligibility { get; }

        public bool IsHidden { get; }
    }
}
