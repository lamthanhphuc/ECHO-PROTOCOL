using System;

namespace EchoProtocol.AI.Common.AED
{
    public sealed class AEDv2PlayerPhaseEvidence
    {
        public string UserId { get; }
        public int DownCount { get; private set; }
        public int ReviveCount { get; private set; }
        public int NoiseCount { get; private set; }
        public int ToolUseCount { get; private set; }
        public int EliminatedCount { get; private set; }
        public int ObservationCount => DownCount + ReviveCount + NoiseCount + ToolUseCount + EliminatedCount;
        public double ActiveObservedSeconds { get; private set; }

        public AEDv2PlayerPhaseEvidence(string userId)
        {
            UserId = !string.IsNullOrWhiteSpace(userId) ? userId : throw new ArgumentException(nameof(userId));
        }

        public void RecordDown() => DownCount++;
        public void RecordRevive() => ReviveCount++;
        public void RecordNoise() => NoiseCount++;
        public void RecordToolUse() => ToolUseCount++;
        public void RecordElimination() => EliminatedCount++;

        public void RecordActiveObservation(double seconds)
        {
            if (seconds > 0 && !double.IsNaN(seconds) && !double.IsInfinity(seconds))
                ActiveObservedSeconds += seconds;
        }
    }
}
