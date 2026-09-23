using System.Collections.Generic;
using EchoProtocol.AI.Common.Spatial;

namespace EchoProtocol.AI.Stalker.Spatial
{
    public interface IRoomSweepTargetStrategy
    {
        bool TrySelectTarget(
            RegionId currentRegionId,
            ISet<RegionId> rejectedRoomRegionIds,
            out RegionId targetRoomRegionId);

        void CommitSelectedTarget(
            RegionId targetRoomRegionId);

        bool IsLegacyFallbackTargetAllowed(
            RegionId candidateRoomRegionId);

        bool IsCommittedStrategicTarget(
            RegionId candidateRoomRegionId);

        void PrepareReachedTarget(
            RegionId reachedRoomRegionId);
    }
}
