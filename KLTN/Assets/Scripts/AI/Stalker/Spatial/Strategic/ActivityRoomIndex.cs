using System;
using System.Collections.Generic;
using EchoProtocol.AI.Common.Spatial;
using EchoProtocol.AI.Stalker.Spatial;

namespace EchoProtocol.AI.Stalker.Spatial.Strategic
{
    public sealed class ActivityRoomIndex
    {
        private readonly RegionGraph _regionGraph;
        private readonly Dictionary<RegionId, ActivityRoomKey> _areaByRegion;
        private readonly Dictionary<ActivityRoomKey, IReadOnlyList<RegionId>> _regionsByArea;
        private readonly ActivityRoomKey[] _allAreas;
        private readonly ActivityRoomKey[] _roomAreas;
        private readonly Dictionary<ActivityRoomPair, int> _distanceCache;

        public ActivityRoomIndex(RegionGraph regionGraph)
        {
            _regionGraph = regionGraph ?? throw new ArgumentNullException(nameof(regionGraph));
            _areaByRegion = new Dictionary<RegionId, ActivityRoomKey>();
            _regionsByArea = new Dictionary<ActivityRoomKey, IReadOnlyList<RegionId>>();
            _distanceCache = new Dictionary<ActivityRoomPair, int>();

            var grouped = new Dictionary<ActivityRoomKey, List<RegionId>>();
            for (var i = 0; i < regionGraph.Regions.Count; i++)
            {
                var region = regionGraph.Regions[i];
                if (!ActivityRoomKey.TryCreate(region.SemanticMetadata, out var area))
                {
                    continue;
                }

                _areaByRegion[region.Id] = area;
                if (!grouped.TryGetValue(area, out var members))
                {
                    members = new List<RegionId>();
                    grouped.Add(area, members);
                }

                members.Add(region.Id);
            }

            var allAreas = new List<ActivityRoomKey>(grouped.Count);
            var roomAreas = new List<ActivityRoomKey>();
            foreach (var pair in grouped)
            {
                pair.Value.Sort((left, right) => left.CompareTo(right));
                _regionsByArea.Add(pair.Key, Array.AsReadOnly(pair.Value.ToArray()));
                allAreas.Add(pair.Key);
                if (pair.Key.IsRoom)
                {
                    roomAreas.Add(pair.Key);
                }
            }

            allAreas.Sort((left, right) => left.CompareTo(right));
            roomAreas.Sort((left, right) => left.CompareTo(right));
            _allAreas = allAreas.ToArray();
            _roomAreas = roomAreas.ToArray();
        }

        public IReadOnlyList<ActivityRoomKey> AllAreas => _allAreas;
        public IReadOnlyList<ActivityRoomKey> RoomAreas => _roomAreas;

        public bool TryGetAreaForRegion(RegionId regionId, out ActivityRoomKey area) =>
            _areaByRegion.TryGetValue(regionId, out area);

        public bool TryGetMemberRegions(
            ActivityRoomKey area,
            out IReadOnlyList<RegionId> regionIds)
        {
            if (_regionsByArea.TryGetValue(area, out regionIds))
            {
                return true;
            }

            regionIds = Array.Empty<RegionId>();
            return false;
        }

        public bool TryGetMinimumHopDistance(
            ActivityRoomKey from,
            ActivityRoomKey to,
            out int hopDistance)
        {
            hopDistance = -1;
            if (!_regionsByArea.TryGetValue(from, out var fromRegions)
                || !_regionsByArea.TryGetValue(to, out var toRegions))
            {
                return false;
            }

            if (from == to)
            {
                hopDistance = 0;
                return true;
            }

            var pair = new ActivityRoomPair(from, to);
            if (_distanceCache.TryGetValue(pair, out hopDistance))
            {
                return hopDistance >= 0;
            }

            var minimum = int.MaxValue;
            for (var i = 0; i < fromRegions.Count; i++)
            {
                for (var j = 0; j < toRegions.Count; j++)
                {
                    if (_regionGraph.TryGetRouteHopCost(fromRegions[i], toRegions[j], out var cost)
                        && cost < minimum)
                    {
                        minimum = cost;
                    }
                }
            }

            hopDistance = minimum == int.MaxValue ? -1 : minimum;
            _distanceCache[pair] = hopDistance;
            return hopDistance >= 0;
        }

        public void InvalidateDistanceCache() => _distanceCache.Clear();

        private readonly struct ActivityRoomPair : IEquatable<ActivityRoomPair>
        {
            public ActivityRoomPair(ActivityRoomKey from, ActivityRoomKey to)
            {
                From = from;
                To = to;
            }

            private ActivityRoomKey From { get; }
            private ActivityRoomKey To { get; }

            public bool Equals(ActivityRoomPair other) => From == other.From && To == other.To;
            public override bool Equals(object obj) => obj is ActivityRoomPair other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    return (From.GetHashCode() * 397) ^ To.GetHashCode();
                }
            }
        }
    }
}
