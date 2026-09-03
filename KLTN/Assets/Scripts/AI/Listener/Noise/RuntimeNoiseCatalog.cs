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
                    new RuntimeNoiseDefinition(RuntimeNoiseType.SPRINT, 0.7d, 12d, TimeSpan.FromSeconds(2d),
                        RuntimeNoiseEmissionMode.RecurringMovement)
                },
                {
                    RuntimeNoiseType.INTERACTION,
                    new RuntimeNoiseDefinition(RuntimeNoiseType.INTERACTION, 0.35d, 6d, TimeSpan.FromSeconds(2d),
                        RuntimeNoiseEmissionMode.DiscreteAction)
                },
                {
                    RuntimeNoiseType.CORE_CARRY,
                    new RuntimeNoiseDefinition(RuntimeNoiseType.CORE_CARRY, 0.7d, 12d, TimeSpan.FromSeconds(2d),
                        RuntimeNoiseEmissionMode.RecurringMovement)
                },
                {
                    RuntimeNoiseType.CORE_DROP,
                    new RuntimeNoiseDefinition(RuntimeNoiseType.CORE_DROP, 0.9d, 15d, TimeSpan.FromSeconds(3d),
                        RuntimeNoiseEmissionMode.DiscreteAction)
                },
                {
                    RuntimeNoiseType.NOISE_MAKER,
                    new RuntimeNoiseDefinition(RuntimeNoiseType.NOISE_MAKER, 1d, 20d, TimeSpan.FromSeconds(4d),
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
