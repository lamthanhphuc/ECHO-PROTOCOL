using EchoProtocol.AI.Common;

namespace EchoProtocol.AI.Stalker
{
    public readonly struct StalkerTargetStatus
    {
        public StalkerTargetStatus(PlayerId playerId, StalkerTargetEligibilityResult eligibility)
            : this(playerId, eligibility, false, 0UL)
        {
        }

        public StalkerTargetStatus(
            PlayerId playerId,
            StalkerTargetEligibilityResult eligibility,
            bool isHidden)
            : this(playerId, eligibility, isHidden, 0UL)
        {
        }

        public StalkerTargetStatus(
            PlayerId playerId,
            StalkerTargetEligibilityResult eligibility,
            bool isHidden,
            ulong hideSpotId)
        {
            PlayerId = playerId;
            Eligibility = eligibility;
            IsHidden = isHidden;
            HideSpotId = isHidden ? hideSpotId : 0UL;
        }

        public PlayerId PlayerId { get; }

        public StalkerTargetEligibilityResult Eligibility { get; }

        public bool IsHidden { get; }

        public ulong HideSpotId { get; }
    }
}
