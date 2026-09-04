using EchoProtocol.AI.Common;

namespace EchoProtocol.AI.Stalker.Networking
{
    public static class StalkerFusionTargetEligibilityAdapter
    {
        public static StalkerTargetEligibilitySnapshot CreateActive(PlayerId playerId)
        {
            return CreateActive(playerId, false, false);
        }

        public static StalkerTargetEligibilitySnapshot CreateActive(
            PlayerId playerId,
            bool isDowned,
            bool isEliminated)
        {
            if (!playerId.IsValid)
            {
                return default;
            }

            return new StalkerTargetEligibilitySnapshot(
                true,
                true,
                isDowned,
                isEliminated,
                false);
        }

        public static StalkerTargetEligibilitySnapshot CreateDisconnected(PlayerId playerId)
        {
            if (!playerId.IsValid)
            {
                return default;
            }

            return new StalkerTargetEligibilitySnapshot(
                false,
                false,
                false,
                false,
                false);
        }
    }
}
