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
            if (!TryGetUniqueStatus(statuses, playerId, out var status))
            {
                return false;
            }

            eligibility = status.Eligibility;
            return true;
        }

        public static bool TryGetUniqueStatus(
            IReadOnlyList<StalkerTargetStatus> statuses,
            PlayerId playerId,
            out StalkerTargetStatus status)
        {
            status = default;

            if (!playerId.IsValid || statuses == null)
            {
                return false;
            }

            var found = false;
            for (var i = 0; i < statuses.Count; i++)
            {
                var candidate = statuses[i];
                if (candidate.PlayerId != playerId)
                {
                    continue;
                }

                if (found)
                {
                    status = default;
                    return false;
                }

                status = candidate;
                found = true;
            }

            return found;
        }
    }
}
