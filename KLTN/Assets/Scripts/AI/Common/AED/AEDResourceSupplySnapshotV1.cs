using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace EchoProtocol.AI.Common.AED
{
    public enum AEDResourceSupplyOriginV1 { WorldSpawn, LobbyLoadout, Drop, Recovery, SupportSpawn }
    public enum AEDResourceLocationV1 { World, Carried, Consumed, Unknown }

    public sealed class AEDResourceSupplyItemV1
    {
        public string InstanceId { get; }
        public int ToolId { get; }
        public int Zone { get; }
        public AEDResourceSupplyOriginV1 Origin { get; }
        public AEDResourceLocationV1 Location { get; }
        public string OwnerUserId { get; }
        public int? RemainingCharges { get; }
        public bool Discovered { get; }
        public bool PickedUp { get; }
        public bool Reachable { get; }
        public long SourceTick { get; }
        public string SourceOccurrenceKey { get; }
        public bool HostVerified { get; }

        public AEDResourceSupplyItemV1(string instanceId, int toolId, int zone,
            AEDResourceSupplyOriginV1 origin, AEDResourceLocationV1 location,
            string ownerUserId, int? remainingCharges, bool discovered, bool pickedUp,
            bool reachable, long sourceTick, string sourceOccurrenceKey, bool hostVerified)
        {
            InstanceId = instanceId; ToolId = toolId; Zone = zone; Origin = origin;
            Location = location; OwnerUserId = ownerUserId; RemainingCharges = remainingCharges;
            Discovered = discovered; PickedUp = pickedUp; Reachable = reachable;
            SourceTick = sourceTick; SourceOccurrenceKey = sourceOccurrenceKey; HostVerified = hostVerified;
        }
    }

    public sealed class AEDResourceSupplySnapshotV1
    {
        public const string Version = "AED_RESOURCE_SUPPLY_V1_RESEARCH";
        public Guid MatchId { get; }
        public uint PhaseOrdinal { get; }
        public bool CoverageComplete { get; }
        public bool SourcesHostVerified { get; }
        public bool ResearchOnly => true;
        public bool CanApplyGameplay => false;
        public IReadOnlyList<AEDResourceSupplyItemV1> Items { get; }

        private AEDResourceSupplySnapshotV1(Guid matchId, uint phaseOrdinal,
            bool coverageComplete, AEDResourceSupplyItemV1[] items)
        {
            MatchId = matchId; PhaseOrdinal = phaseOrdinal; CoverageComplete = coverageComplete;
            SourcesHostVerified = coverageComplete && items.Length > 0 && items.All(x => x.HostVerified);
            Items = new ReadOnlyCollection<AEDResourceSupplyItemV1>(items);
        }

        public static bool TryCreate(Guid matchId, uint phaseOrdinal,
            IEnumerable<AEDResourceSupplyItemV1> items, bool coverageComplete,
            out AEDResourceSupplySnapshotV1 snapshot)
        {
            snapshot = null;
            if (matchId == Guid.Empty || phaseOrdinal == 0 || items == null) return false;
            var source = items.ToArray();
            if (source.Any(item => item == null || string.IsNullOrWhiteSpace(item.InstanceId) ||
                string.IsNullOrWhiteSpace(item.SourceOccurrenceKey) ||
                !AEDResourceToolIdsV1.All.Contains(item.ToolId) || item.Zone < 0 || item.Zone > 3 ||
                item.SourceTick < 0 || (item.RemainingCharges.HasValue && item.RemainingCharges.Value < 0) ||
                !Enum.IsDefined(typeof(AEDResourceSupplyOriginV1), item.Origin) ||
                !Enum.IsDefined(typeof(AEDResourceLocationV1), item.Location) ||
                (!string.IsNullOrWhiteSpace(item.OwnerUserId) &&
                    (!Guid.TryParse(item.OwnerUserId, out var owner) || owner == Guid.Empty)) ||
                (item.Location == AEDResourceLocationV1.Carried && string.IsNullOrWhiteSpace(item.OwnerUserId)) ||
                (item.Origin == AEDResourceSupplyOriginV1.LobbyLoadout && string.IsNullOrWhiteSpace(item.OwnerUserId))))
                return false;
            if (source.GroupBy(x => x.InstanceId, StringComparer.Ordinal).Any(g => g.Count() > 1)) return false;
            snapshot = new AEDResourceSupplySnapshotV1(matchId, phaseOrdinal, coverageComplete,
                source.OrderBy(x => x.InstanceId, StringComparer.Ordinal).ToArray());
            return true;
        }

        public int CountReachableWorld(int zone, int toolId) => Items.Count(x =>
            x.Zone == zone && x.ToolId == toolId && x.Location == AEDResourceLocationV1.World &&
            x.Reachable && x.HostVerified);
    }
}
