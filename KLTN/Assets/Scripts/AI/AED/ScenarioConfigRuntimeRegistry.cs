using System;
using System.Collections.Generic;
using EchoProtocol.AI.Common.AED;

namespace EchoProtocol.AI.AED
{
    public static class ScenarioConfigRuntimeRegistry
    {
        private static readonly Dictionary<Guid, ScenarioConfig> AppliedConfigs =
            new Dictionary<Guid, ScenarioConfig>();

        public static event Action<Guid, ScenarioConfig> AppliedConfigChanged;

        public static bool TryGetAppliedConfig(Guid matchId, out ScenarioConfig config)
        {
            return AppliedConfigs.TryGetValue(matchId, out config);
        }

        public static void Apply(Guid matchId, ScenarioConfig config)
        {
            if (matchId == Guid.Empty) throw new ArgumentException("Match id is required.", nameof(matchId));
            if (config == null) throw new ArgumentNullException(nameof(config));

            if (AppliedConfigs.TryGetValue(matchId, out var existing)
                && string.Equals(
                    ScenarioConfigFingerprint.Compute(existing),
                    ScenarioConfigFingerprint.Compute(config),
                    StringComparison.Ordinal))
            {
                return;
            }

            AppliedConfigs[matchId] = config;
            AppliedConfigChanged?.Invoke(matchId, config);
        }

        public static void Clear(Guid matchId)
        {
            if (matchId == Guid.Empty) return;
            if (AppliedConfigs.Remove(matchId))
            {
                AppliedConfigChanged?.Invoke(matchId, null);
            }
        }

        public static void ClearAll()
        {
            AppliedConfigs.Clear();
        }
    }
}
