using System.Collections.Generic;
using EchoProtocol.AI.Common;

namespace EchoProtocol.AI.Stalker
{
    public static class StalkerTargetStatusLookup
    {
        public static bool TryGetUnique(
            IReadOnlyList<StalkerTargetStatus> statuses,
            PlayerId playerId,
            out StalkerTargetEligibilityResult eligibility)
        {
            eligibility = default;

            if (!playerId.IsValid || statuses == null)
            {
                return false;
            }

            var found = false;
            for (var i = 0; i < statuses.Count; i++)
            {
                var status = statuses[i];
                if (status.PlayerId != playerId)
                {
                    continue;
                }

                if (found)
                {
                    eligibility = default;
                    return false;
                }

                eligibility = status.Eligibility;
                found = true;
            }

            return found;
        }
    }
}
