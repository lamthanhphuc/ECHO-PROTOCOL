using Fusion;

namespace EchoProtocol.RelayB
{
    public struct RelayBSurgeTelemetry : INetworkStruct
    {
        public int Seed, Attempt, Difficulty, Variant, PulseSequence;
        public byte Phase;
        public ulong InfectedLow, InfectedHigh, InsulatedLow, InsulatedHigh;
        public float Elapsed, PulseRemaining, Cooldown;
        public static RelayBSurgeTelemetry From(RelayBSurgeSimulation sim) => sim.Board == null ? default : new RelayBSurgeTelemetry
        {
            Seed=sim.Board.Seed, Attempt=sim.Attempt, Difficulty=sim.Board.Difficulty, Variant=sim.Board.Variant,
            Phase=(byte)sim.Phase, PulseSequence=sim.PulseSequence, Elapsed=sim.Elapsed,
            PulseRemaining=sim.PulseRemaining, Cooldown=sim.Cooldown,
            InfectedLow=sim.Infected.Low, InfectedHigh=sim.Infected.High,
            InsulatedLow=sim.Insulated.Low, InsulatedHigh=sim.Insulated.High
        };
    }
}
