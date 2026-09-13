using System;
using System.Collections.Generic;

namespace EchoProtocol.AI.Listener.Noise
{
    public sealed class RuntimeNoiseCatalog
    {
        // Implementation defaults only. Canonical Listener v1.0 marks final tuning TBD.
        private readonly Dictionary<RuntimeNoiseType, RuntimeNoiseDefinition> _definitions =
            new Dictionary<RuntimeNoiseType, RuntimeNoiseDefinition>
            {
                {
                    RuntimeNoiseType.SPRINT,
                    new RuntimeNoiseDefinition(RuntimeNoiseType.SPRINT, 0.8d, 16d, TimeSpan.FromSeconds(2d),
                        RuntimeNoiseEmissionMode.RecurringMovement)
                },
                {
                    RuntimeNoiseType.INTERACTION,
                    new RuntimeNoiseDefinition(RuntimeNoiseType.INTERACTION, 0.35d, 6d, TimeSpan.FromSeconds(2d),
                        RuntimeNoiseEmissionMode.DiscreteAction)
                },
                {
                    RuntimeNoiseType.CORE_CARRY,
                    new RuntimeNoiseDefinition(RuntimeNoiseType.CORE_CARRY, 0.95d, 20d, TimeSpan.FromSeconds(2d),
                        RuntimeNoiseEmissionMode.RecurringMovement)
                },
                {
                    RuntimeNoiseType.CORE_DROP,
                    new RuntimeNoiseDefinition(RuntimeNoiseType.CORE_DROP, 0.9d, 15d, TimeSpan.FromSeconds(3d),
                        RuntimeNoiseEmissionMode.DiscreteAction)
                },
                {
                    RuntimeNoiseType.NOISE_MAKER,
                    new RuntimeNoiseDefinition(RuntimeNoiseType.NOISE_MAKER, 1d, 22d, TimeSpan.FromSeconds(6d),
                        RuntimeNoiseEmissionMode.DiscreteAction)
                },
                {
                    RuntimeNoiseType.FIELD_SCANNER,
                    new RuntimeNoiseDefinition(RuntimeNoiseType.FIELD_SCANNER, 0.45d, 8d, TimeSpan.FromSeconds(2.5d),
                        RuntimeNoiseEmissionMode.DiscreteAction)
                },
                {
                    RuntimeNoiseType.CROUCH,
                    new RuntimeNoiseDefinition(RuntimeNoiseType.CROUCH, 0.3d, 5d, TimeSpan.FromSeconds(2d),
                        RuntimeNoiseEmissionMode.RecurringMovement)
                },
                {
                    RuntimeNoiseType.WALK,
                    new RuntimeNoiseDefinition(RuntimeNoiseType.WALK, 0.45d, 8d, TimeSpan.FromSeconds(2d),
                        RuntimeNoiseEmissionMode.RecurringMovement)
                },
                {
                    RuntimeNoiseType.DOOR,
                    new RuntimeNoiseDefinition(RuntimeNoiseType.DOOR, 0.55d, 10d, TimeSpan.FromSeconds(2d),
                        RuntimeNoiseEmissionMode.DiscreteAction)
                },
                {
                    RuntimeNoiseType.CORE_INSERT,
                    new RuntimeNoiseDefinition(RuntimeNoiseType.CORE_INSERT, 0.8d, 14d, TimeSpan.FromSeconds(3d),
                        RuntimeNoiseEmissionMode.DiscreteAction)
                }
            };

        public static RuntimeNoiseCatalog CreateDefault()
        {
            return new RuntimeNoiseCatalog();
        }

        public bool TryGetDefinition(RuntimeNoiseType noiseType, out RuntimeNoiseDefinition definition)
        {
            return _definitions.TryGetValue(noiseType, out definition);
        }
    }
}
