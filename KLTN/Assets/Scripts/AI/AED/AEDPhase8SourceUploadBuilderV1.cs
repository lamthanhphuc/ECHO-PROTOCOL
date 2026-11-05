using System;
using System.Collections.Generic;
using System.Linq;
using EchoProtocol.Telemetry;

namespace EchoProtocol.AI.AED
{
    public static class AEDPhase8SourceUploadBuilderV1
    {
        public const string SourceVersion = "AED_PHASE8_LIFECYCLE_SIDECARS_V1";

        public static bool TryBuild(Guid matchId, uint phaseOrdinal, string evidenceFingerprint, string rosterIdentity,
            TelemetrySourceIdentity[] references, AEDPhase8AuthoritySourceSnapshotV1 snapshot,
            out AEDPhase8SourceUploadDtoV1 request, out string reason)
        {
            request = null;
            reason = "P8_SOURCE_BUILD_INVALID";
            if (matchId == Guid.Empty || phaseOrdinal == 0 || phaseOrdinal > int.MaxValue || snapshot == null ||
                snapshot.MatchId != matchId || snapshot.PhaseOrdinal != phaseOrdinal || !ValidHash(evidenceFingerprint) ||
                !ValidHash(rosterIdentity) || references == null || snapshot.Tools == null || snapshot.Minions == null)
                return false;

            if (references.Any(x => x == null || x.EventId == Guid.Empty || x.EventSequence <= 0 || string.IsNullOrWhiteSpace(x.EventType) || string.IsNullOrWhiteSpace(x.OccurrenceKey)) ||
                references.Select(x => x.EventId).Distinct().Count() != references.Length ||
                references.Select(x => x.EventSequence).Distinct().Count() != references.Length ||
                references.GroupBy(x => (x.EventType, x.OccurrenceKey)).Any(x => x.Count() != 1))
                return false;

            var toolKeys = references.Where(x => x.EventType == TelemetryEventTypes.TeamToolLifecycle).Select(x => x.OccurrenceKey).ToArray();
            var minionKeys = references.Where(x => x.EventType == TelemetryEventTypes.MinionLifecycle).Select(x => x.OccurrenceKey).ToArray();
            var tools = snapshot.Tools;
            var minions = snapshot.Minions;
            if (toolKeys.Length != tools.Count || minionKeys.Length != minions.Count ||
                toolKeys.Distinct(StringComparer.Ordinal).Count() != toolKeys.Length ||
                minionKeys.Distinct(StringComparer.Ordinal).Count() != minionKeys.Length ||
                !new HashSet<string>(toolKeys, StringComparer.Ordinal).SetEquals(tools.Select(x => x.SourceOccurrenceKey)) ||
                !new HashSet<string>(minionKeys, StringComparer.Ordinal).SetEquals(minions.Select(x => x.SourceOccurrenceKey)))
            {
                reason = "P8_SOURCE_LOCAL_COVERAGE_MISMATCH";
                return false;
            }

            if (tools.Any(x => x == null || string.IsNullOrWhiteSpace(x.InstanceId) || string.IsNullOrWhiteSpace(x.Origin) || string.IsNullOrWhiteSpace(x.Location) || x.SourceTick < 0 || x.Zone is < 0 or > 3 || (!string.IsNullOrEmpty(x.OwnerUserId) && (!Guid.TryParse(x.OwnerUserId, out var owner) || owner == Guid.Empty))) ||
                minions.Any(x => x == null || string.IsNullOrWhiteSpace(x.NetworkInstanceId) || x.SourceTick < 0 || x.Zone is < 0 or > 3 || x.State is not ("SPAWNED" or "DESPAWNED")))
                return false;

            request = new AEDPhase8SourceUploadDtoV1
            {
                completedPhaseOrdinal = (int)phaseOrdinal,
                evidenceFingerprint = evidenceFingerprint,
                rosterIdentity = rosterIdentity,
                comparisonContextKey = "AED_PHASE8_CANONICAL_V1",
                sourceVersion = SourceVersion,
                sourceEvents = references.OrderBy(x => x.EventSequence).Select(x => new AEDPhase8SourceReferenceDtoV1
                {
                    eventId = x.EventId.ToString("D"), eventSequence = x.EventSequence,
                    eventType = x.EventType, occurrenceKey = x.OccurrenceKey
                }).ToArray(),
                metrics = Array.Empty<AEDPhase8MetricDtoV1>(),
                supply = tools.OrderBy(x => x.SourceTick).ThenBy(x => x.SourceOccurrenceKey, StringComparer.Ordinal).Select(x => new AEDPhase8SupplyDtoV1
                {
                    instanceId = x.InstanceId, toolId = x.ToolId, zone = x.Zone, origin = x.Origin,
                    location = x.Location, ownerUserId = x.OwnerUserId, remainingCharges = x.RemainingCharges,
                    reachable = false, sourceTick = x.SourceTick, sourceOccurrenceKey = x.SourceOccurrenceKey
                }).ToArray(),
                minions = minions.OrderBy(x => x.SourceTick).ThenBy(x => x.SourceOccurrenceKey, StringComparer.Ordinal).Select(x => new AEDPhase8MinionDtoV1
                {
                    networkInstanceId = x.NetworkInstanceId, zone = x.Zone, state = x.State,
                    sourceTick = x.SourceTick, sourceOccurrenceKey = x.SourceOccurrenceKey
                }).ToArray()
            };
            reason = string.Empty;
            return true;
        }

        private static bool ValidHash(string value) => value != null && value.Length == 64 && value.All(Uri.IsHexDigit);
    }
}
