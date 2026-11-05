using System.Collections.Generic;
using EchoProtocol.AI.Common;

namespace EchoProtocol.AI.Stalker
{
    public sealed class StalkerHideSpotMemory
    {
        private readonly Dictionary<ulong, StalkerHideSpotMemorySnapshot> _records =
            new Dictionary<ulong, StalkerHideSpotMemorySnapshot>();

        public void Reset()
        {
            _records.Clear();
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
