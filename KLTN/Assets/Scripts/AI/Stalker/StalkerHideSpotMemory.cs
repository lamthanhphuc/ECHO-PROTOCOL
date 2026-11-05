using System.Collections.Generic;
using EchoProtocol.AI.Common;

namespace EchoProtocol.AI.Stalker
{
    public sealed class StalkerHideSpotMemory
    {
        private readonly Dictionary<ulong, StalkerHideSpotMemorySnapshot> _records =
            new Dictionary<ulong, StalkerHideSpotMemorySnapshot>();
        private readonly Dictionary<ulong, double> _protectedUntil = new Dictionary<ulong, double>();

        public void Reset()
        {
            _records.Clear();
            _protectedUntil.Clear();
        }
        public bool CanInspect(ulong stableId, double now) => !_protectedUntil.TryGetValue(stableId, out var until) || now >= until;
        public void RecordProtectedInspection(ulong stableId, double now, double retrySeconds)
        {
            if (stableId == 0 || double.IsNaN(now) || double.IsInfinity(now) || double.IsNaN(retrySeconds) || double.IsInfinity(retrySeconds) || retrySeconds <= 0)
                throw new System.ArgumentException("Protection requires a stable spot and a finite remaining window.");
            _protectedUntil[stableId] = now + retrySeconds;
        }

        public StalkerHideSpotMemorySnapshot GetSnapshot(ulong stableId)
        {
            return _records.TryGetValue(stableId, out var snapshot)
                ? snapshot
                : default;
        }

        public void RecordEmptyInspection(ulong stableId, AiSimulationTime inspectedAt)
        {
            var previous = GetSnapshot(stableId);
            _records[stableId] = new StalkerHideSpotMemorySnapshot(
                previous.ConfirmedUseCount,
                previous.LastConfirmedUseTime,
                inspectedAt,
                previous.ConsecutiveEmptyInspections + 1);
        }

        public void RecordConfirmedUse(ulong stableId, AiSimulationTime confirmedAt)
        {
            var previous = GetSnapshot(stableId);
            _records[stableId] = new StalkerHideSpotMemorySnapshot(
                previous.ConfirmedUseCount + 1,
                confirmedAt,
                confirmedAt,
                0);
        }
    }
}
