using EchoProtocol.AI.Common.Spatial;

namespace EchoProtocol.AI.Stalker.Spatial
{
    public enum GlobalPatrolObjectiveInvalidationReason
    {
        None,
        TargetRegionVisited,
        TargetRegionDisabled,
        TargetRegionUnreachable,
        TopologyChanged,
        FsmInterrupted,
        NavigationRecoveryFailed
    }

    public readonly struct GlobalPatrolObjective
    {
        public GlobalPatrolObjective(RegionId targetRegionId, RegionId nextRegionId)
        {
            TargetRegionId = targetRegionId;
            NextRegionId = nextRegionId;
        }

        public RegionId TargetRegionId { get; }
        public RegionId NextRegionId { get; }
        public bool IsValid => TargetRegionId.IsValid && NextRegionId.IsValid;
        public static GlobalPatrolObjective Invalid => default;
    }

    public sealed class GlobalPatrolPlanner
    {
        private readonly RegionGraph _regionGraph;
        private readonly CoverageMemory _coverageMemory;

        public GlobalPatrolPlanner(RegionGraph regionGraph, CoverageMemory coverageMemory)
        {
            _regionGraph = regionGraph;
            _coverageMemory = coverageMemory;
        }

        public GlobalPatrolObjective CurrentObjective { get; private set; }
        public GlobalPatrolObjectiveInvalidationReason LastInvalidationReason { get; private set; }

        public bool TryGetOrCreateObjective(
            RegionId currentRegionId,
            RegionId previousRegionId,
            out GlobalPatrolObjective objective)
        {
            objective = GlobalPatrolObjective.Invalid;
            if (_regionGraph == null || _coverageMemory == null || !currentRegionId.IsValid)
            {
                LastInvalidationReason = GlobalPatrolObjectiveInvalidationReason.TopologyChanged;
                CurrentObjective = GlobalPatrolObjective.Invalid;
                return false;
            }

            if (CurrentObjective.IsValid && IsObjectiveStillValid(currentRegionId, out var nextRegionId))
            {
                objective = new GlobalPatrolObjective(CurrentObjective.TargetRegionId, nextRegionId);
                CurrentObjective = objective;
                LastInvalidationReason = GlobalPatrolObjectiveInvalidationReason.None;
                return true;
            }

            if (CurrentObjective.IsValid)
            {
                LastInvalidationReason = GetInvalidationReason(currentRegionId);
            }

            if (!TrySelectTargetRegion(currentRegionId, previousRegionId, out var targetRegionId))
            {
                CurrentObjective = GlobalPatrolObjective.Invalid;
                return false;
            }

            if (!_regionGraph.TryGetNextRegionOnRoute(currentRegionId, targetRegionId, out nextRegionId))
            {
                CurrentObjective = GlobalPatrolObjective.Invalid;
                LastInvalidationReason = GlobalPatrolObjectiveInvalidationReason.TargetRegionUnreachable;
                return false;
            }

            objective = new GlobalPatrolObjective(targetRegionId, nextRegionId);
            CurrentObjective = objective;
            return true;
        }

        public void Invalidate(GlobalPatrolObjectiveInvalidationReason reason)
        {
            LastInvalidationReason = reason;
            CurrentObjective = GlobalPatrolObjective.Invalid;
        }

        private bool IsObjectiveStillValid(RegionId currentRegionId, out RegionId nextRegionId)
        {
            nextRegionId = RegionId.Invalid;
            var target = CurrentObjective.TargetRegionId;
            return target != currentRegionId
                && _regionGraph.IsRegionEnabled(target)
                && _regionGraph.TryGetNextRegionOnRoute(currentRegionId, target, out nextRegionId);
        }

        private GlobalPatrolObjectiveInvalidationReason GetInvalidationReason(RegionId currentRegionId)
        {
            var target = CurrentObjective.TargetRegionId;
            if (target == currentRegionId)
            {
                return GlobalPatrolObjectiveInvalidationReason.TargetRegionVisited;
            }

            if (!_regionGraph.IsRegionEnabled(target))
            {
                return GlobalPatrolObjectiveInvalidationReason.TargetRegionDisabled;
            }

            return GlobalPatrolObjectiveInvalidationReason.TargetRegionUnreachable;
        }

        private bool TrySelectTargetRegion(RegionId currentRegionId, RegionId previousRegionId, out RegionId targetRegionId)
        {
            targetRegionId = RegionId.Invalid;
            var bestVisitCount = int.MaxValue;
            var bestLastVisited = float.MaxValue;
            var bestBacktrack = 1;
            var bestRecentFrequency = int.MaxValue;
            var bestHopCost = int.MaxValue;

            var regions = _regionGraph.Regions;
            for (var i = 0; i < regions.Count; i++)
            {
                var candidate = regions[i].Id;
                if (candidate == currentRegionId
                    || !_regionGraph.IsRegionEnabled(candidate)
                    || !_regionGraph.TryGetRouteHopCost(currentRegionId, candidate, out var hopCost))
                {
                    continue;
                }

                var visitCount = _coverageMemory.GetRegionVisitCount(candidate);
                var lastVisited = _coverageMemory.WasRegionVisited(candidate)
                    ? _coverageMemory.GetRegionLastVisitedTime(candidate)
                    : -1f;
                var backtrack = candidate == previousRegionId ? 1 : 0;
                var recentFrequency = _coverageMemory.GetRecentRegionFrequency(candidate);

                if (IsBetter(candidate, visitCount, lastVisited, backtrack, recentFrequency, hopCost,
                        targetRegionId, bestVisitCount, bestLastVisited, bestBacktrack, bestRecentFrequency, bestHopCost))
                {
                    targetRegionId = candidate;
                    bestVisitCount = visitCount;
                    bestLastVisited = lastVisited;
                    bestBacktrack = backtrack;
                    bestRecentFrequency = recentFrequency;
                    bestHopCost = hopCost;
                }
            }

            return targetRegionId.IsValid;
        }

        private static bool IsBetter(
            RegionId candidate,
            int visitCount,
            float lastVisited,
            int backtrack,
            int recentFrequency,
            int hopCost,
            RegionId best,
            int bestVisitCount,
            float bestLastVisited,
            int bestBacktrack,
            int bestRecentFrequency,
            int bestHopCost)
        {
            if (!best.IsValid)
            {
                return true;
            }

            if (visitCount != bestVisitCount)
            {
                return visitCount < bestVisitCount;
            }

            if (!lastVisited.Equals(bestLastVisited))
            {
                return lastVisited < bestLastVisited;
            }

            if (backtrack != bestBacktrack)
            {
                return backtrack < bestBacktrack;
            }

            if (recentFrequency != bestRecentFrequency)
            {
                return recentFrequency < bestRecentFrequency;
            }

            if (hopCost != bestHopCost)
            {
                return hopCost < bestHopCost;
            }

            return candidate.CompareTo(best) < 0;
        }
    }
}
