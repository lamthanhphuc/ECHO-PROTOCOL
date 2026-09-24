using System;
using System.Collections.Generic;
using EchoProtocol.AI.Common.Spatial;
using EchoProtocol.AI.Stalker.Spatial;
using EchoProtocol.Diagnostics;
using UnityEngine;

namespace EchoProtocol.AI.Stalker.Spatial.Strategic
{
    public sealed class SmartRoomSweepTargetStrategy : IRoomSweepTargetStrategy
    {
        private readonly RegionGraph _regionGraph;
        private readonly RoomSweepCoverageMemory _roomSweepCoverageMemory;
        private readonly CoverageMemory _coverageMemory;
        private readonly ActivityRoomIndex _roomIndex;
        private readonly RoomHeatSystem _heatSystem;
        private readonly StalkerPatrolDirector _director;
        private readonly StalkerSmartPatrolSettings _settings;
        private readonly List<Candidate> _candidates = new List<Candidate>();
        private Candidate _pendingCandidate;
        private RegionId _pendingCurrentRegion;
        private ActivityRoomKey _pendingCurrentRoom;
        private RegionId _committedStrategicRegion;
        private bool _hasPendingCandidate;
        private ActivityRoomKey _coreCarrierRoom = ActivityRoomKey.Invalid;
        private int _variationSeed;
        private int _decisionSequence;

        public SmartRoomSweepTargetStrategy(
            RegionGraph regionGraph,
            RoomSweepCoverageMemory roomSweepCoverageMemory,
            CoverageMemory coverageMemory,
            ActivityRoomIndex roomIndex,
            RoomHeatSystem heatSystem,
            StalkerPatrolDirector director,
            StalkerSmartPatrolSettings settings,
            int variationSeed)
        {
            _regionGraph = regionGraph ?? throw new ArgumentNullException(nameof(regionGraph));
            _roomSweepCoverageMemory = roomSweepCoverageMemory
                ?? throw new ArgumentNullException(nameof(roomSweepCoverageMemory));
            _coverageMemory = coverageMemory ?? throw new ArgumentNullException(nameof(coverageMemory));
            _roomIndex = roomIndex ?? throw new ArgumentNullException(nameof(roomIndex));
            _heatSystem = heatSystem ?? throw new ArgumentNullException(nameof(heatSystem));
            _director = director ?? throw new ArgumentNullException(nameof(director));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _variationSeed = variationSeed;
        }

        public void ConfigureVariationSeed(int variationSeed)
        {
            if (_variationSeed == variationSeed)
            {
                return;
            }

            _variationSeed = variationSeed;
            _decisionSequence = 0;
        }

        public void ResetForMatch()
        {
            _decisionSequence = 0;
            _candidates.Clear();

            _pendingCandidate = default;
            _pendingCurrentRegion = RegionId.Invalid;
            _pendingCurrentRoom = ActivityRoomKey.Invalid;
            _committedStrategicRegion = RegionId.Invalid;
            _hasPendingCandidate = false;
            _coreCarrierRoom = ActivityRoomKey.Invalid;
        }

        public void SetCoreCarrierRoom(ActivityRoomKey room) =>
            _coreCarrierRoom = room;

        public bool HasCoreCarrierRoom => _coreCarrierRoom.IsValid;

        public bool TrySelectTarget(
            RegionId currentRegionId,
            ISet<RegionId> rejectedRoomRegionIds,
            out RegionId targetRoomRegionId)
        {
            targetRoomRegionId = RegionId.Invalid;
            _candidates.Clear();
            _committedStrategicRegion = RegionId.Invalid;

            _director.CancelPendingStrategicSweep();

            _pendingCandidate = default;
            _pendingCurrentRegion = RegionId.Invalid;
            _pendingCurrentRoom = ActivityRoomKey.Invalid;
            _hasPendingCandidate = false;

            if (TryGetCrowdedTarget(
                    currentRegionId,
                    rejectedRoomRegionIds,
                    out var crowdedRoom,
                    out var crowdedRegion))
            {
                _pendingCandidate = new Candidate(
                    crowdedRoom, crowdedRegion, 0, 0,
                    0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f);
                _pendingCurrentRegion = currentRegionId;
                _roomIndex.TryGetAreaForRegion(currentRegionId, out _pendingCurrentRoom);
                _hasPendingCandidate = true;
                targetRoomRegionId = crowdedRegion;
                return true;
            }

            if (!_director.HasHotspot
                || _heatSystem.EffectivePlayerPresence <= 0f
                || !_roomIndex.TryGetAreaForRegion(currentRegionId, out var currentRoom))
            {
                return false;
            }

            var nowSeconds = _director.CurrentTimeSeconds;
            for (var i = 0; i < _roomIndex.RoomAreas.Count; i++)
            {
                var room = _roomIndex.RoomAreas[i];
                if (room == currentRoom
                    || !_roomIndex.TryGetMinimumHopDistance(room, _director.Hotspot, out var hotspotRing)
                    || (hotspotRing == 0 && !_director.IsDirectHotspotRoomEligible())
                    || !TryChooseRepresentativeRegion(
                        currentRegionId,
                        room,
                        rejectedRoomRegionIds,
                        out var representative,
                        out var routeHopCost))
                {
                    continue;
                }

                GetVisitSignals(
                    room,
                    nowSeconds,
                    out var exploration,
                    out var novelty,
                    out var visitPenalty,
                    out var repeatPenalty);

                var strategicHeat = _heatSystem.GetStrategicHeat(room, _roomIndex, nowSeconds);
                var ringAffinity = _director.GetRingAffinity(hotspotRing);
                var routeEfficiency = 1f - Mathf.Clamp01(
                    routeHopCost / _settings.RouteEfficiencyFullHopCount);
                var pressurePenalty = _settings.RecentPressurePenalty
                    * _director.GetRecentRoomPressure01(room);
                var distancePenalty = _settings.DistancePenaltyPerHop
                    * Math.Min(routeHopCost, 5);
                var finalScore = Mathf.Clamp01(
                    (0.45f * strategicHeat)
                    + (0.15f * ringAffinity)
                    + (0.15f * exploration)
                    + (0.10f * routeEfficiency)
                    + (0.15f * novelty)
                    - visitPenalty
                    - repeatPenalty
                    - pressurePenalty
                    - distancePenalty);

                _candidates.Add(new Candidate(
                    room,
                    representative,
                    routeHopCost,
                    hotspotRing,
                    strategicHeat,
                    ringAffinity,
                    exploration,
                    routeEfficiency,
                    novelty,
                    visitPenalty,
                    repeatPenalty,
                    pressurePenalty,
                    distancePenalty,
                    finalScore));
            }

            if (_candidates.Count == 0)
            {
                return false;
            }

            _candidates.Sort(CompareCandidates);
            LogCandidates();

            var topCount = Math.Min(_settings.TopK, _candidates.Count);
            var random = CreateDecisionRandom(currentRegionId, _decisionSequence);
            var useTail = _candidates.Count > topCount
                && NextUnit(ref random) < _settings.ExplorationProbability;
            var poolStart = useTail ? topCount : 0;
            var poolCount = useTail ? _candidates.Count - topCount : topCount;
            var selected = SelectSoftmax(poolStart, poolCount, ref random);

            _pendingCandidate = selected;
            _pendingCurrentRegion = currentRegionId;
            _pendingCurrentRoom = currentRoom;
            _hasPendingCandidate = true;

            targetRoomRegionId =
                selected.RepresentativeRegion;

            return true;
        }

        public bool TryGetCrowdedTarget(
            RegionId currentRegionId,
            ISet<RegionId> rejectedRoomRegionIds,
            out ActivityRoomKey targetRoom,
            out RegionId targetRegion)
        {
            targetRoom = ActivityRoomKey.Invalid;
            targetRegion = RegionId.Invalid;
            if ((!_director.ShouldSeekPlayers && !_coreCarrierRoom.IsValid)
                || !_roomIndex.TryGetAreaForRegion(currentRegionId, out var currentRoom)
                || !_regionGraph.TryGetRegionSemanticMetadata(
                    currentRegionId, out var currentMetadata))
            {
                return false;
            }

            if (_coreCarrierRoom.IsValid)
            {
                if (_coreCarrierRoom == currentRoom)
                {
                    return false;
                }

                var nearestCarrierRoomHops = int.MaxValue;
                var nearestStalkerHops = int.MaxValue;
                if (currentRoom.IsRoom
                    && _roomIndex.TryGetMinimumHopDistance(
                        currentRoom, _coreCarrierRoom, out var currentHops))
                {
                    nearestCarrierRoomHops = currentHops;
                    nearestStalkerHops = 0;
                }
                for (var i = 0; i < _roomIndex.RoomAreas.Count; i++)
                {
                    var room = _roomIndex.RoomAreas[i];
                    if (room == currentRoom
                        || !_roomIndex.TryGetMinimumHopDistance(
                            room, _coreCarrierRoom, out var carrierHops)
                        || !TryChooseRepresentativeRegion(currentRegionId,
                            room, rejectedRoomRegionIds,
                            out var region, out var stalkerHops)
                        || !_regionGraph.TryGetRegionSemanticMetadata(
                            region, out var carrierMetadata)
                        || (currentMetadata.Zone != RegionSemanticZone.Unknown
                            && carrierMetadata.Zone != currentMetadata.Zone))
                    {
                        continue;
                    }

                    if (carrierHops < nearestCarrierRoomHops
                        || (carrierHops == nearestCarrierRoomHops
                            && stalkerHops < nearestStalkerHops))
                    {
                        nearestCarrierRoomHops = carrierHops;
                        nearestStalkerHops = stalkerHops;
                        targetRoom = room;
                        targetRegion = region;
                    }
                }

                if (targetRegion.IsValid)
                {
                    return true;
                }
            }

            if (!_director.ShouldSeekPlayers)
            {
                return false;
            }

            var bestCount = _heatSystem.GetVisiblePlayerCount(currentRoom);
            var bestHops = bestCount > 0 ? 0 : int.MaxValue;
            for (var i = 0; i < _roomIndex.RoomAreas.Count; i++)
            {
                var room = _roomIndex.RoomAreas[i];
                var count = _heatSystem.GetVisiblePlayerCount(room);
                if (room == currentRoom || count == 0
                    || !TryChooseRepresentativeRegion(currentRegionId, room,
                        rejectedRoomRegionIds, out var region, out var hops)
                    || !_regionGraph.TryGetRegionSemanticMetadata(
                        region, out var candidateMetadata)
                    || (currentMetadata.Zone != RegionSemanticZone.Unknown
                        && candidateMetadata.Zone != currentMetadata.Zone))
                {
                    continue;
                }

                if (count > bestCount
                    || (count == bestCount && hops < bestHops))
                {
                    bestCount = count;
                    bestHops = hops;
                    targetRoom = room;
                    targetRegion = region;
                }
            }

            return targetRegion.IsValid;
        }

        public void CommitSelectedTarget(
            RegionId targetRoomRegionId)
        {
            if (!_hasPendingCandidate
                || !targetRoomRegionId.IsValid
                || targetRoomRegionId
                    != _pendingCandidate.RepresentativeRegion)
            {
                return;
            }

            var selected = _pendingCandidate;
            _committedStrategicRegion =
                selected.RepresentativeRegion;

            _director.RecordStrategicSelection(
                selected.Room,
                selected.HotspotRing);

            RuntimeLog.Log(
                RuntimeLogCategory.StalkerDirector,
                $"[STK_DIRECTOR] Seq={_decisionSequence} State=PATROL " +
                $"Mode={_director.Mode} " +
                $"CurrentRegion={_pendingCurrentRegion} " +
                $"CurrentRoom={_pendingCurrentRoom} " +
                $"Hotspot={_director.Hotspot} " +
                $"SelectedRoom={selected.Room} " +
                $"SelectedRegion={selected.RepresentativeRegion} " +
                $"Ring={selected.HotspotRing} " +
                $"Score={selected.FinalScore:F3} " +
                $"Pressure={_director.Pressure01:F3}");

            _decisionSequence++;

            _pendingCandidate = default;
            _pendingCurrentRegion = RegionId.Invalid;
            _pendingCurrentRoom = ActivityRoomKey.Invalid;
            _hasPendingCandidate = false;
        }

        public bool IsLegacyFallbackTargetAllowed(
            RegionId candidateRoomRegionId)
        {
            if (!candidateRoomRegionId.IsValid
                || !_director.HasHotspot
                || _heatSystem.EffectivePlayerPresence <= 0f)
            {
                return true;
            }

            if (!_roomIndex.TryGetAreaForRegion(
                    candidateRoomRegionId,
                    out var candidateRoom))
            {
                return true;
            }

            if (candidateRoom != _director.Hotspot)
            {
                return true;
            }

            return _director.IsDirectHotspotRoomEligible();
        }

        public bool IsCommittedStrategicTarget(
            RegionId candidateRoomRegionId)
        {
            return candidateRoomRegionId.IsValid
                && _committedStrategicRegion.IsValid
                && candidateRoomRegionId
                    == _committedStrategicRegion;
        }

        public void PrepareReachedTarget(
            RegionId reachedRoomRegionId)
        {
            if (!IsCommittedStrategicTarget(
                    reachedRoomRegionId))
            {
                return;
            }

            if (_roomSweepCoverageMemory.IsRegionCleared(
                    reachedRoomRegionId))
            {
                _roomSweepCoverageMemory.ResetRegion(
                    reachedRoomRegionId);
            }

            _committedStrategicRegion =
                RegionId.Invalid;
        }

        private bool TryChooseRepresentativeRegion(
            RegionId currentRegionId,
            ActivityRoomKey room,
            ISet<RegionId> rejectedRoomRegionIds,
            out RegionId representative,
            out int routeHopCost)
        {
            if (TryChooseRepresentativeRegionPass(
                    currentRegionId,
                    room,
                    rejectedRoomRegionIds,
                    allowCleared: false,
                    out representative,
                    out routeHopCost))
            {
                return true;
            }

            return TryChooseRepresentativeRegionPass(
                currentRegionId,
                room,
                rejectedRoomRegionIds,
                allowCleared: true,
                out representative,
                out routeHopCost);
        }

        private bool TryChooseRepresentativeRegionPass(
            RegionId currentRegionId,
            ActivityRoomKey room,
            ISet<RegionId> rejectedRoomRegionIds,
            bool allowCleared,
            out RegionId representative,
            out int routeHopCost)
        {
            representative = RegionId.Invalid;
            routeHopCost = int.MaxValue;

            if (!_roomIndex.TryGetMemberRegions(room, out var members))
            {
                return false;
            }

            for (var i = 0; i < members.Count; i++)
            {
                var member = members[i];
                if (!member.IsValid
                    || member == currentRegionId
                    || (rejectedRoomRegionIds != null
                        && rejectedRoomRegionIds.Contains(member))
                    || !_regionGraph.ContainsRegion(member)
                    || !_regionGraph.IsRegionEnabled(member)
                    || (!allowCleared
                        && _roomSweepCoverageMemory.IsRegionCleared(member))
                    || !_regionGraph.TryGetRegionSemanticMetadata(
                        member,
                        out var metadata)
                    || metadata.Kind != RegionSemanticKind.Room
                    || !_regionGraph.TryGetRouteHopCost(
                        currentRegionId,
                        member,
                        out var hop)
                    || !_regionGraph.TryGetNextRegionOnRoute(
                        currentRegionId,
                        member,
                        out _))
                {
                    continue;
                }

                if (hop < routeHopCost
                    || (hop == routeHopCost
                        && member.CompareTo(representative) < 0))
                {
                    representative = member;
                    routeHopCost = hop;
                }
            }

            return representative.IsValid;
        }

        private void GetVisitSignals(
            ActivityRoomKey room,
            double nowSeconds,
            out float exploration,
            out float novelty,
            out float visitPenalty,
            out float repeatPenalty)
        {
            var lastVisited = -1f;
            if (_roomIndex.TryGetMemberRegions(room, out var members))
            {
                for (var i = 0; i < members.Count; i++)
                {
                    lastVisited = Mathf.Max(lastVisited, _coverageMemory.GetRegionLastVisitedTime(members[i]));
                }
            }

            if (lastVisited < 0f)
            {
                exploration = 1f;
                visitPenalty = 0f;
            }
            else
            {
                var secondsSinceVisit = Math.Max(0d, nowSeconds - lastVisited);
                exploration = Mathf.Clamp01((float)secondsSinceVisit / _settings.ExplorationFullTimeSeconds);
                visitPenalty = _settings.RecentVisitPenalty
                    * Mathf.Exp(-(float)secondsSinceVisit / _settings.RecentVisitDecaySeconds);
            }

            var frequency = 0;
            var previousRecentRoom = ActivityRoomKey.Invalid;
            var hasPreviousRecentRoom = false;

            for (var i = 0; i < _coverageMemory.RecentRegionHistoryCount; i++)
            {
                if (!_coverageMemory.TryGetRecentRegion(
                        i,
                        out var recentRegion)
                    || !_roomIndex.TryGetAreaForRegion(
                        recentRegion,
                        out var recentRoom))
                {
                    previousRecentRoom = ActivityRoomKey.Invalid;
                    hasPreviousRecentRoom = false;
                    continue;
                }

                if (!hasPreviousRecentRoom
                    || recentRoom != previousRecentRoom)
                {
                    if (recentRoom == room)
                    {
                        frequency++;
                    }

                    previousRecentRoom = recentRoom;
                    hasPreviousRecentRoom = true;
                }
            }

            var repeat01 = Mathf.Clamp01(
                frequency / _settings.RecentRepeatNormalizationCount);
            repeatPenalty = _settings.RepeatVisitPenalty * repeat01;
            novelty = 1f - repeat01;
        }

        private Candidate SelectSoftmax(int start, int count, ref uint random)
        {
            var maxScore = float.NegativeInfinity;
            for (var i = start; i < start + count; i++)
            {
                maxScore = Mathf.Max(maxScore, _candidates[i].FinalScore);
            }

            var totalWeight = 0d;
            for (var i = start; i < start + count; i++)
            {
                totalWeight += Math.Exp(
                    (_candidates[i].FinalScore - maxScore)
                    / _settings.SoftmaxTemperature);
            }

            var threshold = NextUnit(ref random) * totalWeight;
            var accumulated = 0d;
            for (var i = start; i < start + count; i++)
            {
                accumulated += Math.Exp(
                    (_candidates[i].FinalScore - maxScore)
                    / _settings.SoftmaxTemperature);
                if (threshold < accumulated)
                {
                    return _candidates[i];
                }
            }

            return _candidates[start + count - 1];
        }

        private void LogCandidates()
        {
            if (!RuntimeLog.IsEnabled(RuntimeLogCategory.StalkerDirector))
            {
                return;
            }

            for (var i = 0; i < _candidates.Count; i++)
            {
                var candidate = _candidates[i];
                RuntimeLog.Log(
                    RuntimeLogCategory.StalkerDirector,
                    $"[STK_CANDIDATE] Room={candidate.Room} Region={candidate.RepresentativeRegion} " +
                    $"Heat={candidate.StrategicHeat:F3} Ring={candidate.HotspotRing} " +
                    $"Explore={candidate.Exploration:F3} Route={candidate.RouteEfficiency:F3} " +
                    $"Novelty={candidate.Novelty:F3} VisitPenalty={candidate.VisitPenalty:F3} " +
                    $"RepeatPenalty={candidate.RepeatPenalty:F3} " +
                    $"PressurePenalty={candidate.PressurePenalty:F3} " +
                    $"DistancePenalty={candidate.DistancePenalty:F3} Final={candidate.FinalScore:F3}");
            }
        }

        private static int CompareCandidates(Candidate left, Candidate right)
        {
            var scoreComparison = right.FinalScore.CompareTo(left.FinalScore);
            if (scoreComparison != 0)
            {
                return scoreComparison;
            }

            var roomComparison = left.Room.CompareTo(right.Room);
            return roomComparison != 0
                ? roomComparison
                : left.RepresentativeRegion.CompareTo(right.RepresentativeRegion);
        }

        private uint CreateDecisionRandom(RegionId currentRegionId, int sequence) =>
            Mix(
                unchecked((uint)_variationSeed)
                ^ Mix(unchecked((uint)currentRegionId.Value))
                ^ Mix(unchecked((uint)sequence)));

        private static double NextUnit(ref uint state)
        {
            state = Mix(state + 0x9e3779b9U);
            return state / ((double)uint.MaxValue + 1d);
        }

        private static uint Mix(uint value)
        {
            value ^= value >> 16;
            value *= 0x7feb352dU;
            value ^= value >> 15;
            value *= 0x846ca68bU;
            value ^= value >> 16;
            return value;
        }

        private readonly struct Candidate
        {
            public Candidate(
                ActivityRoomKey room,
                RegionId representativeRegion,
                int routeHopCost,
                int hotspotRing,
                float strategicHeat,
                float ringAffinity,
                float exploration,
                float routeEfficiency,
                float novelty,
                float visitPenalty,
                float repeatPenalty,
                float pressurePenalty,
                float distancePenalty,
                float finalScore)
            {
                Room = room;
                RepresentativeRegion = representativeRegion;
                RouteHopCost = routeHopCost;
                HotspotRing = hotspotRing;
                StrategicHeat = strategicHeat;
                RingAffinity = ringAffinity;
                Exploration = exploration;
                RouteEfficiency = routeEfficiency;
                Novelty = novelty;
                VisitPenalty = visitPenalty;
                RepeatPenalty = repeatPenalty;
                PressurePenalty = pressurePenalty;
                DistancePenalty = distancePenalty;
                FinalScore = finalScore;
            }

            public ActivityRoomKey Room { get; }
            public RegionId RepresentativeRegion { get; }
            public int RouteHopCost { get; }
            public int HotspotRing { get; }
            public float StrategicHeat { get; }
            public float RingAffinity { get; }
            public float Exploration { get; }
            public float RouteEfficiency { get; }
            public float Novelty { get; }
            public float VisitPenalty { get; }
            public float RepeatPenalty { get; }
            public float PressurePenalty { get; }
            public float DistancePenalty { get; }
            public float FinalScore { get; }
        }
    }
}
