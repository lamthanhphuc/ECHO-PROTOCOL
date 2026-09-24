using System;
using System.Collections.Generic;
using EchoProtocol.AI.Common.Spatial;

namespace EchoProtocol.AI.Stalker.Spatial
{
    public enum RoomSweepGlobalObjectiveInvalidationReason
    {
        None = 0,
        TargetReached = 1,
        TargetCleared = 2,
        TargetDisabled = 3,
        TargetUnreachable = 4,
        TopologyChanged = 5,
        NavigationRecoveryFailed = 6,
        StrategicPolicyChanged = 7
    }

    public readonly struct RoomSweepGlobalObjective
    {
        public RoomSweepGlobalObjective(RegionId targetRoomRegionId, RegionId nextRegionId)
        {
            TargetRoomRegionId = targetRoomRegionId;
            NextRegionId = nextRegionId;
        }

        public static RoomSweepGlobalObjective Invalid => default;
        public RegionId TargetRoomRegionId { get; }
        public RegionId NextRegionId { get; }
        public bool IsValid => TargetRoomRegionId.IsValid && NextRegionId.IsValid;
    }

    public sealed class RoomSweepGlobalPlanner
    {
        private readonly RegionGraph _regionGraph;
        private readonly RoomSweepCoverageMemory _coverageMemory;
        private readonly int _variationSeed;
        private readonly int _nearOptimalHopSlack;
        private readonly bool _useSeededVariation;
        private IRoomSweepTargetStrategy _targetStrategy;
        private RegionSemanticZone _zone = RegionSemanticZone.Unknown;

        private readonly List<SeededCandidate> _seededCandidates =
            new List<SeededCandidate>();

        private readonly struct SeededCandidate
        {
            public SeededCandidate(
                RegionId target,
                RegionId nextRegion,
                int hopCost)
            {
                Target = target;
                NextRegion = nextRegion;
                HopCost = hopCost;
            }

            public RegionId Target { get; }
            public RegionId NextRegion { get; }
            public int HopCost { get; }
        }

        public RoomSweepGlobalPlanner(RegionGraph regionGraph, RoomSweepCoverageMemory coverageMemory)
        {
            _regionGraph = regionGraph;
            _coverageMemory = coverageMemory;
            _variationSeed = 0;
            _nearOptimalHopSlack = 0;
            _useSeededVariation = false;
            CurrentObjective = RoomSweepGlobalObjective.Invalid;
            LastInvalidationReason = RoomSweepGlobalObjectiveInvalidationReason.None;
        }

        public RoomSweepGlobalPlanner(
            RegionGraph regionGraph,
            RoomSweepCoverageMemory coverageMemory,
            int variationSeed,
            int nearOptimalHopSlack)
        {
            _regionGraph = regionGraph;
            _coverageMemory = coverageMemory;
            _variationSeed = variationSeed;
            _nearOptimalHopSlack = Math.Max(0, nearOptimalHopSlack);
            _useSeededVariation = true;
            CurrentObjective = RoomSweepGlobalObjective.Invalid;
            LastInvalidationReason = RoomSweepGlobalObjectiveInvalidationReason.None;
        }

        public RoomSweepGlobalObjective CurrentObjective { get; private set; }
        public RoomSweepGlobalObjectiveInvalidationReason LastInvalidationReason { get; private set; }

        public void ConfigureTargetStrategy(
            IRoomSweepTargetStrategy targetStrategy)
        {
            _targetStrategy = targetStrategy;
        }

        public void ConfigureZone(RegionSemanticZone zone)
        {
            if (_zone == zone) return;
            _zone = zone;
            ClearObjective();
        }

        public bool TryGetOrCreateObjective(
            RegionId currentRegionId,
            out RoomSweepGlobalObjective objective)
        {
            return TryGetOrCreateObjective(currentRegionId, null, out objective);
        }

        public bool TryGetOrCreateObjective(
            RegionId currentRegionId,
            ISet<RegionId> rejectedRoomRegionIds,
            out RoomSweepGlobalObjective objective)
        {
            objective = RoomSweepGlobalObjective.Invalid;
            if (_regionGraph == null || _coverageMemory == null || !IsValidCurrentRegion(currentRegionId))
            {
                CurrentObjective = RoomSweepGlobalObjective.Invalid;
                return false;
            }

            if (CurrentObjective.IsValid)
            {
                var target =
                    CurrentObjective.TargetRoomRegionId;

                if (target == currentRegionId)
                {
                    _targetStrategy?.PrepareReachedTarget(
                        target);

                    Invalidate(
                        RoomSweepGlobalObjectiveInvalidationReason
                            .TargetReached);

                    return false;
                }
                else if (_targetStrategy != null
                    && !_targetStrategy.IsLegacyFallbackTargetAllowed(
                        target))
                {
                    Invalidate(
                        RoomSweepGlobalObjectiveInvalidationReason
                            .StrategicPolicyChanged);
                }
                else if (IsRejected(
                    target,
                    rejectedRoomRegionIds))
                {
                    Invalidate(
                        RoomSweepGlobalObjectiveInvalidationReason
                            .NavigationRecoveryFailed);
                }
                else if (_coverageMemory.IsRegionCleared(target)
                    && (_targetStrategy == null
                        || !_targetStrategy.IsCommittedStrategicTarget(
                            target)))
                {
                    Invalidate(
                        RoomSweepGlobalObjectiveInvalidationReason
                            .TargetCleared);
                }
                else if (!_regionGraph.ContainsRegion(target)
                    || !_regionGraph.TryGetRegionSemanticMetadata(
                        target,
                        out var metadata)
                    || metadata.Kind != RegionSemanticKind.Room
                    || !IsInZone(target))
                {
                    Invalidate(
                        RoomSweepGlobalObjectiveInvalidationReason
                            .TopologyChanged);
                }
                else if (!_regionGraph.IsRegionEnabled(target))
                {
                    Invalidate(
                        RoomSweepGlobalObjectiveInvalidationReason
                            .TargetDisabled);
                }
                else if (_regionGraph.TryGetNextRegionOnRoute(
                    currentRegionId,
                    target,
                    out var nextRegionId)
                    && IsInZone(nextRegionId))
                {
                    CurrentObjective =
                        new RoomSweepGlobalObjective(
                            target,
                            nextRegionId);

                    objective = CurrentObjective;
                    return true;
                }
                else
                {
                    Invalidate(
                        RoomSweepGlobalObjectiveInvalidationReason
                            .TargetUnreachable);
                }
            }

            if (TrySelectTarget(currentRegionId, rejectedRoomRegionIds, out var selectedObjective))
            {
                CurrentObjective = selectedObjective;
                objective = selectedObjective;
                return true;
            }

            CurrentObjective = RoomSweepGlobalObjective.Invalid;
            return false;
        }

        public void ClearObjective()
        {
            CurrentObjective = RoomSweepGlobalObjective.Invalid;
            LastInvalidationReason = RoomSweepGlobalObjectiveInvalidationReason.None;
        }

        public void Invalidate(RoomSweepGlobalObjectiveInvalidationReason reason)
        {
            CurrentObjective = RoomSweepGlobalObjective.Invalid;
            LastInvalidationReason = reason;
        }

        private bool TrySelectTarget(
            RegionId currentRegionId,
            ISet<RegionId> rejectedRoomRegionIds,
            out RoomSweepGlobalObjective objective)
        {
            objective = RoomSweepGlobalObjective.Invalid;

            if (_targetStrategy != null
                && _targetStrategy.TrySelectTarget(
                    currentRegionId,
                    rejectedRoomRegionIds,
                    out var strategicTarget)
                && IsEligibleStrategicTarget(
                    currentRegionId,
                    strategicTarget,
                    rejectedRoomRegionIds)
                && _regionGraph.TryGetNextRegionOnRoute(
                    currentRegionId,
                    strategicTarget,
                    out var strategicNextRegion)
                && IsInZone(strategicNextRegion))
            {
                _targetStrategy.CommitSelectedTarget(
                    strategicTarget);

                objective = new RoomSweepGlobalObjective(
                    strategicTarget,
                    strategicNextRegion);
                return true;
            }

            RecycleLegacyCoverageIfExhausted(
                currentRegionId,
                rejectedRoomRegionIds);

            if (_useSeededVariation)
            {
                return TrySelectSeededTarget(
                    currentRegionId,
                    rejectedRoomRegionIds,
                    out objective);
            }

            var bestTarget = RegionId.Invalid;
            var bestNextRegion = RegionId.Invalid;
            var bestCost = int.MaxValue;

            var regions = _regionGraph.Regions;
            for (var i = 0; i < regions.Count; i++)
            {
                var candidate = regions[i].Id;
                if (!IsEligibleLegacyFallbackTarget(
                        currentRegionId,
                        candidate,
                        rejectedRoomRegionIds))
                {
                    continue;
                }

                if (!_regionGraph.TryGetRouteHopCost(currentRegionId, candidate, out var cost)
                    || !_regionGraph.TryGetNextRegionOnRoute(currentRegionId, candidate, out var nextRegionId)
                    || !IsInZone(nextRegionId))
                {
                    continue;
                }

                if (cost < bestCost || (cost == bestCost && candidate.CompareTo(bestTarget) < 0))
                {
                    bestCost = cost;
                    bestTarget = candidate;
                    bestNextRegion = nextRegionId;
                }
            }

            if (!bestTarget.IsValid)
            {
                return false;
            }

            objective = new RoomSweepGlobalObjective(bestTarget, bestNextRegion);
            return true;
        }

        private void RecycleLegacyCoverageIfExhausted(
            RegionId currentRegionId,
            ISet<RegionId> rejectedRoomRegionIds)
        {
            var regions = _regionGraph.Regions;
            var hasReachableClearedCandidate = false;

            for (var i = 0; i < regions.Count; i++)
            {
                var candidate = regions[i].Id;

                if (!IsLegacyFallbackCandidate(
                        currentRegionId,
                        candidate,
                        rejectedRoomRegionIds))
                {
                    continue;
                }

                if (!_regionGraph.TryGetRouteHopCost(
                        currentRegionId,
                        candidate,
                        out _)
                    || !_regionGraph.TryGetNextRegionOnRoute(
                        currentRegionId,
                        candidate,
                        out var nextRegionId)
                    || !IsInZone(nextRegionId))
                {
                    continue;
                }

                // At least one normal reachable room still exists.
                // Keep the current sweep cycle unchanged.
                if (!_coverageMemory.IsRegionCleared(candidate))
                {
                    return;
                }

                hasReachableClearedCandidate = true;
            }

            if (!hasReachableClearedCandidate)
            {
                return;
            }

            // Every reachable legacy room has already been swept.
            // Start a new patrol cycle using the existing coverage system.
            for (var i = 0; i < regions.Count; i++)
            {
                var candidate = regions[i].Id;

                if (!IsLegacyFallbackCandidate(
                        currentRegionId,
                        candidate,
                        rejectedRoomRegionIds)
                    || !_coverageMemory.IsRegionCleared(candidate))
                {
                    continue;
                }

                if (!_regionGraph.TryGetRouteHopCost(
                        currentRegionId,
                        candidate,
                        out _)
                    || !_regionGraph.TryGetNextRegionOnRoute(
                        currentRegionId,
                        candidate,
                        out var nextRegionId)
                    || !IsInZone(nextRegionId))
                {
                    continue;
                }

                _coverageMemory.ResetRegion(candidate);
            }
        }

        private bool TrySelectSeededTarget(
            RegionId currentRegionId,
            ISet<RegionId> rejectedRoomRegionIds,
            out RoomSweepGlobalObjective objective)
        {
            objective = RoomSweepGlobalObjective.Invalid;
            _seededCandidates.Clear();

            var regions = _regionGraph.Regions;
            var bestCost = int.MaxValue;
            for (var i = 0; i < regions.Count; i++)
            {
                var candidate = regions[i].Id;
                if (!IsEligibleLegacyFallbackTarget(
                        currentRegionId,
                        candidate,
                        rejectedRoomRegionIds))
                {
                    continue;
                }

                if (!_regionGraph.TryGetRouteHopCost(currentRegionId, candidate, out var cost)
                    || !_regionGraph.TryGetNextRegionOnRoute(currentRegionId, candidate, out var nextRegionId)
                    || !IsInZone(nextRegionId))
                {
                    continue;
                }

                if (cost < bestCost)
                {
                    bestCost = cost;
                }

                _seededCandidates.Add(new SeededCandidate(candidate, nextRegionId, cost));
            }

            if (bestCost == int.MaxValue)
            {
                return false;
            }

            var maximumCost = bestCost + _nearOptimalHopSlack;
            for (var i = _seededCandidates.Count - 1; i >= 0; i--)
            {
                if (_seededCandidates[i].HopCost > maximumCost)
                {
                    _seededCandidates.RemoveAt(i);
                }
            }

            _seededCandidates.Sort(
                (left, right) => left.Target.CompareTo(right.Target));

            var mixed = Mix(
                unchecked((uint)_variationSeed)
                ^ Mix(unchecked((uint)currentRegionId.Value)));
            var selected = _seededCandidates[(int)(mixed % (uint)_seededCandidates.Count)];
            objective = new RoomSweepGlobalObjective(selected.Target, selected.NextRegion);
            return true;
        }

        private bool IsEligibleStrategicTarget(
            RegionId currentRegionId,
            RegionId candidate,
            ISet<RegionId> rejectedRoomRegionIds)
        {
            return candidate.IsValid
                && candidate != currentRegionId
                && !IsRejected(
                    candidate,
                    rejectedRoomRegionIds)
                && _regionGraph.ContainsRegion(candidate)
                && _regionGraph.IsRegionEnabled(candidate)
                && _regionGraph.TryGetRegionSemanticMetadata(
                    candidate,
                    out var metadata)
                && metadata.Kind == RegionSemanticKind.Room
                && IsInZone(candidate);
        }

        private bool IsEligibleLegacyFallbackTarget(
            RegionId currentRegionId,
            RegionId candidate,
            ISet<RegionId> rejectedRoomRegionIds)
        {
            return IsLegacyFallbackCandidate(
                    currentRegionId,
                    candidate,
                    rejectedRoomRegionIds)
                && !_coverageMemory.IsRegionCleared(candidate);
        }

        private bool IsLegacyFallbackCandidate(
            RegionId currentRegionId,
            RegionId candidate,
            ISet<RegionId> rejectedRoomRegionIds)
        {
            return candidate.IsValid
                && candidate != currentRegionId
                && !IsRejected(
                    candidate,
                    rejectedRoomRegionIds)
                && _regionGraph.ContainsRegion(candidate)
                && _regionGraph.IsRegionEnabled(candidate)
                && _regionGraph.TryGetRegionSemanticMetadata(
                    candidate,
                    out var metadata)
                && metadata.Kind == RegionSemanticKind.Room
                && IsInZone(candidate)
                && (_targetStrategy == null
                    || _targetStrategy.IsLegacyFallbackTargetAllowed(
                        candidate));
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

        private bool IsValidCurrentRegion(RegionId currentRegionId)
        {
            return currentRegionId.IsValid
                && _regionGraph.ContainsRegion(currentRegionId)
                && _regionGraph.IsRegionEnabled(currentRegionId)
                && IsInZone(currentRegionId);
        }

        private bool IsInZone(RegionId regionId)
        {
            return _zone == RegionSemanticZone.Unknown
                || (_regionGraph.TryGetRegionSemanticMetadata(regionId, out var metadata)
                    && metadata.Zone == _zone);
        }

        private static bool IsRejected(RegionId regionId, ISet<RegionId> rejectedRoomRegionIds)
        {
            return rejectedRoomRegionIds != null && rejectedRoomRegionIds.Contains(regionId);
        }
    }
}
