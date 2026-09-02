using EchoProtocol.Networking.Authority;

namespace EchoProtocol.AI.Stalker.Telemetry
{
    public sealed class StalkerProductionTelemetryProducer : IStalkerTelemetryProducer
    {
        public StalkerTelemetryPublishResult TryPublishMonsterAttackResolved(
            StalkerAttackTelemetryOccurrence occurrence)
        {
            if (!occurrence.IsValid)
            {
                return StalkerTelemetryPublishResult.InvalidOccurrence;
            }

            var authority = MatchAuthorityRuntime.Instance;
            if (authority == null)
            {
                return StalkerTelemetryPublishResult.RetryableFailure;
            }

            return Map(authority.TryRecordStalkerAttackResolved(
                occurrence.MonsterIdentity.Value,
                occurrence.Fact.EpisodeId.Value.ToString(),
                occurrence.Fact.Outcome.ToString().ToUpperInvariant()));
        }

        public StalkerTelemetryPublishResult TryPublishMonsterSearchEnded(
            StalkerSearchTelemetryOccurrence occurrence)
        {
            if (!occurrence.IsValid)
            {
                return StalkerTelemetryPublishResult.InvalidOccurrence;
            }

            var authority = MatchAuthorityRuntime.Instance;
            if (authority == null)
            {
                return StalkerTelemetryPublishResult.RetryableFailure;
            }

            return Map(authority.TryRecordStalkerSearchEnded(
                occurrence.MonsterIdentity.Value,
                occurrence.Fact.EpisodeId.Value.ToString(),
                occurrence.Fact.Outcome.ToString()));
        }

        private static StalkerTelemetryPublishResult Map(ProductionTelemetryPublishResult result)
        {
            switch (result)
            {
                case ProductionTelemetryPublishResult.Accepted:
                    return StalkerTelemetryPublishResult.Accepted;
                case ProductionTelemetryPublishResult.Suppressed:
                    return StalkerTelemetryPublishResult.Suppressed;
                case ProductionTelemetryPublishResult.InvalidOccurrence:
                    return StalkerTelemetryPublishResult.InvalidOccurrence;
                default:
                    return StalkerTelemetryPublishResult.RetryableFailure;
            }
        }
    }
}
