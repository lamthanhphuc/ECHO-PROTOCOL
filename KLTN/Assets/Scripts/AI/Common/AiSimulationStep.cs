using System;

namespace EchoProtocol.AI.Common
{
    public readonly struct AiSimulationStep
    {
        private readonly bool _hasExplicitDelta;
        private readonly float _deltaSeconds;

        public AiSimulationStep(AiSimulationTime time, float deltaSeconds)
        {
            if (!time.IsValid)
            {
                throw new ArgumentException("Simulation step time must be valid.", nameof(time));
            }

            if (deltaSeconds < 0f || float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "Simulation delta seconds must be finite and non-negative.");
            }

            Time = time;
            _deltaSeconds = deltaSeconds;
            _hasExplicitDelta = true;
        }

        public static AiSimulationStep Invalid => default;

        public bool IsValid => Time.IsValid && _hasExplicitDelta;

        public AiSimulationTime Time { get; }

        public float DeltaSeconds => _deltaSeconds;
    }
}
