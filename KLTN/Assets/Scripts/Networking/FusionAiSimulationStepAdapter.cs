using EchoProtocol.AI.Common;
using Fusion;

namespace EchoProtocol.Networking
{
    public static class FusionAiSimulationStepAdapter
    {
        public static bool TryCreate(NetworkRunner runner, out AiSimulationStep step)
        {
            if (runner == null || !runner.IsRunning)
            {
                step = AiSimulationStep.Invalid;
                return false;
            }

            return TryCreateFromValues(
                runner.Tick.Raw,
                runner.SimulationTime,
                runner.DeltaTime,
                out step);
        }

        private static bool TryCreateFromValues(
            long tick,
            double simulationTime,
            float deltaTime,
            out AiSimulationStep step)
        {
            step = AiSimulationStep.Invalid;

            try
            {
                var time = new AiSimulationTime(tick, simulationTime);
                step = new AiSimulationStep(time, deltaTime);
                return true;
            }
            catch (System.ArgumentException)
            {
                step = AiSimulationStep.Invalid;
                return false;
            }
        }
    }
}
