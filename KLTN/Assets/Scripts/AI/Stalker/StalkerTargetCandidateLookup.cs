using System.Collections.Generic;
using EchoProtocol.AI.Common;

namespace EchoProtocol.AI.Stalker
{
    public static class StalkerTargetCandidateLookup
    {
        public static bool TryGetUnique(
            IReadOnlyList<StalkerTargetCandidate> candidates,
            PlayerId playerId,
            out StalkerTargetCandidate candidate,
            out bool hasDuplicate)
        {
            candidate = default;
            hasDuplicate = false;

            if (!playerId.IsValid || candidates == null)
            {
                return false;
            }

            var found = false;
            for (var i = 0; i < candidates.Count; i++)
            {
                var current = candidates[i];
                if (current.Observation.PlayerId != playerId)
                {
                    continue;
                }

                if (found)
                {
                    candidate = default;
                    hasDuplicate = true;
                    return false;
                }

                candidate = current;
                found = true;
            }

            return found;
        }
    }
}
