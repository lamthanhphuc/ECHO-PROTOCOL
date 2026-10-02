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
                    new RuntimeNoiseDefinition(RuntimeNoiseType.SPRINT, 0.8d, 30d, TimeSpan.FromSeconds(2d),
                        RuntimeNoiseEmissionMode.RecurringMovement)
                },
                {
                    RuntimeNoiseType.INTERACTION,
                    new RuntimeNoiseDefinition(RuntimeNoiseType.INTERACTION, 0.35d, 200d, TimeSpan.FromSeconds(2d),
                        RuntimeNoiseEmissionMode.DiscreteAction)
                },
                {
                    RuntimeNoiseType.CORE_CARRY,
                    new RuntimeNoiseDefinition(RuntimeNoiseType.CORE_CARRY, 0.95d, 36d, TimeSpan.FromSeconds(2d),
                        RuntimeNoiseEmissionMode.RecurringMovement)
                },
                {
                    RuntimeNoiseType.CORE_DROP,
                    new RuntimeNoiseDefinition(RuntimeNoiseType.CORE_DROP, 0.9d, 28d, TimeSpan.FromSeconds(3d),
                        RuntimeNoiseEmissionMode.DiscreteAction)
                },
                {
                    RuntimeNoiseType.NOISE_MAKER,
                    new RuntimeNoiseDefinition(RuntimeNoiseType.NOISE_MAKER, 1d, 40d, TimeSpan.FromSeconds(6d),
                        RuntimeNoiseEmissionMode.DiscreteAction)
                },
                {
                    RuntimeNoiseType.FIELD_SCANNER,
                    new RuntimeNoiseDefinition(RuntimeNoiseType.FIELD_SCANNER, 0.45d, 8d, TimeSpan.FromSeconds(2.5d),
                        RuntimeNoiseEmissionMode.DiscreteAction)
                },
                {
                    RuntimeNoiseType.CROUCH,
                    new RuntimeNoiseDefinition(RuntimeNoiseType.CROUCH, 0.3d, 7d, TimeSpan.FromSeconds(2d),
                        RuntimeNoiseEmissionMode.RecurringMovement)
                },
                {
                    RuntimeNoiseType.WALK,
                    new RuntimeNoiseDefinition(RuntimeNoiseType.WALK, 0.45d, 12d, TimeSpan.FromSeconds(2d),
                        RuntimeNoiseEmissionMode.RecurringMovement)
                },
                {
                    RuntimeNoiseType.DOOR,
                    new RuntimeNoiseDefinition(RuntimeNoiseType.DOOR, 0.55d, 16d, TimeSpan.FromSeconds(2d),
                        RuntimeNoiseEmissionMode.DiscreteAction)
                },
                {
                    RuntimeNoiseType.CORE_INSERT,
                    new RuntimeNoiseDefinition(RuntimeNoiseType.CORE_INSERT, 0.8d, 24d, TimeSpan.FromSeconds(3d),
                        RuntimeNoiseEmissionMode.DiscreteAction)
                },
                {
                    RuntimeNoiseType.MACHINE_REPAIR,
                    new RuntimeNoiseDefinition(RuntimeNoiseType.MACHINE_REPAIR, 1.5d, 200d, TimeSpan.FromSeconds(3d),
                        RuntimeNoiseEmissionMode.RecurringMovement, 2.5d)
                },
                {
                    RuntimeNoiseType.MINION_ALERT,
                    new RuntimeNoiseDefinition(RuntimeNoiseType.MINION_ALERT, 1d, 55d, TimeSpan.FromSeconds(4d),
                        RuntimeNoiseEmissionMode.DiscreteAction)
                },
                {
                    RuntimeNoiseType.MACHINE_OVERLOAD,
                    new RuntimeNoiseDefinition(RuntimeNoiseType.MACHINE_OVERLOAD, 1.8d, 70d, TimeSpan.FromSeconds(3d),
                        RuntimeNoiseEmissionMode.RecurringMovement, 1.5d)
                },
                {
                    RuntimeNoiseType.TERMINAL_DOWNLOAD,
                    new RuntimeNoiseDefinition(RuntimeNoiseType.TERMINAL_DOWNLOAD, 0.95d, 40d, TimeSpan.FromSeconds(3d),
                        RuntimeNoiseEmissionMode.RecurringMovement, 2.5d)
                },
                {
                    RuntimeNoiseType.VEHICLE_PUSH,
                    new RuntimeNoiseDefinition(RuntimeNoiseType.VEHICLE_PUSH, 1.25d, 55d, TimeSpan.FromSeconds(3d),
                        RuntimeNoiseEmissionMode.RecurringMovement, 2d)
                },
                {
                    RuntimeNoiseType.CHARGE_TRANSFER,
                    new RuntimeNoiseDefinition(RuntimeNoiseType.CHARGE_TRANSFER, 1.8d, 90d, TimeSpan.FromSeconds(2d),
                        RuntimeNoiseEmissionMode.RecurringMovement, 1.5d)
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
