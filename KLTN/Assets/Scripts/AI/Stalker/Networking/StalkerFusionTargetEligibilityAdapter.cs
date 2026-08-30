using EchoProtocol.AI.Common;

namespace EchoProtocol.AI.Stalker.Networking
{
    public static class StalkerFusionTargetEligibilityAdapter
    {
        public static StalkerTargetEligibilitySnapshot CreateActive(PlayerId playerId)
        {
            if (!playerId.IsValid)
            {
                return default;
            }

            return new StalkerTargetEligibilitySnapshot(
                true,
                true,
                false,
                false,
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
