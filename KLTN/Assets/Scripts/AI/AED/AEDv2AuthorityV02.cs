using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using EchoProtocol.AI.Common.AED;

namespace EchoProtocol.AI.AED
{
    public static partial class AEDv2Authority
    {
        private static Guid _v02Match;
        private static Guid _v02Decision;
        private static AEDV02Plan _v02Plan;
        private static int _v02Revision;
        private static int _v02InitialOptional;
        private static string _v02Roster;
        private static string _v02Scene;
        private static DateTime _v02SourceEndedUtc;
        private static readonly Dictionary<Guid, AEDV02PressureSnapshot> V02Pressure = new Dictionary<Guid, AEDV02PressureSnapshot>();
        private static readonly Dictionary<Guid, AEDV02PressureTierMemory> V02Tiers = new Dictionary<Guid, AEDV02PressureTierMemory>();
        private static readonly Dictionary<Guid, string> V02Episodes = new Dictionary<Guid, string>();
        private static readonly AEDV02HideProtectionLedger V02Hide = new AEDV02HideProtectionLedger();

        public static bool CommitV02(Guid matchId, AEDV02DecisionRequestDto request, AEDV02DecisionResultDto approval,
            string currentRoster, bool hasStateAuthority, bool adaptiveNormal, bool gameplayEnabled, bool safeWindow,
            DateTime nowUtc, out string reason)
        {
            reason = "ABORT_AMBUSH_REVALIDATION";
            if (!hasStateAuthority || !adaptiveNormal || !gameplayEnabled || request == null || approval == null || matchId == Guid.Empty ||
                request.resolutionMode != "Adaptive" || request.difficulty != "Normal" ||
                request.policyVersion != AEDV02PilotPolicy.Version || request.catalogVersion != AEDV02Plan.CatalogVersion ||
                !AEDV02WorldSample.Hash(request.sceneManifestFingerprint) ||
                approval.matchId != matchId.ToString("D") || approval.decisionId != request.decisionId || approval.approvalStatus != "APPROVED" ||
                approval.applyStatus != "PENDING" && approval.applyStatus != "APPLIED" || approval.policyVersion != AEDV02PilotPolicy.Version ||
                approval.catalogVersion != AEDV02Plan.CatalogVersion || approval.rosterIdentity != currentRoster || request.rosterIdentity != currentRoster ||
                approval.snapshotId != request.snapshotId || approval.snapshotFingerprint != request.snapshotFingerprint ||
                !Guid.TryParse(request.snapshotId, out var snapshotId) || snapshotId == Guid.Empty || !AEDV02WorldSample.Hash(request.snapshotFingerprint) ||
                approval.sourceFingerprint != request.sourceFingerprint || approval.sceneManifestFingerprint != request.sceneManifestFingerprint ||
                approval.phaseOrdinal != request.phaseOrdinal || approval.decisionPoint != request.decisionPoint || approval.windowSequence != request.windowSequence ||
                approval.windowId != request.windowId || approval.targetPhaseName != request.targetPhaseName || !Guid.TryParse(approval.decisionId, out var decisionId) || decisionId == Guid.Empty ||
                !DateTime.TryParse(approval.expiresAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var expiry) || expiry.ToUniversalTime() < nowUtc ||
                !Enum.TryParse<AEDV02DecisionPoint>(request.decisionPoint, out var point) || request.decisionPoint != point.ToString() ||
                (point == AEDV02DecisionPoint.PRE_MATCH ? request.phaseOrdinal != 0 || request.windowSequence != 0 : request.phaseOrdinal < 1 ||
                    (point == AEDV02DecisionPoint.SAFE_WINDOW ? request.windowSequence < 1 : request.windowSequence != 0)) ||
                point != AEDV02DecisionPoint.PRE_MATCH && (!safeWindow || !AEDV02WorldSample.Hash(request.sourceFingerprint))) return false;
            AEDV02Plan next;
            try { next = new AEDV02Plan(approval.planValues); } catch (ArgumentException) { return false; }
            if (request.proposedReservations?.Any(x => x == null || !x.ToModel().IsValid()) == true ||
                request.support != null && (request.proposedReservations != null || !request.support.ToModel().IsValid() || point != AEDV02DecisionPoint.SAFE_WINDOW)) return false;
            var actionFingerprint = request.support?.ToModel().Fingerprint(decisionId) ??
                AEDV02ProposedReservation.Fingerprint(request.proposedReservations?.Select(x => x.ToModel()));
            if ((approval.actionFingerprint ?? string.Empty) != actionFingerprint) return false;
            if (request.planValues == null || !next.ToArray().SequenceEqual(request.planValues) || next.Fingerprint() != approval.planFingerprint ||
                approval.windowId != AEDV02Plan.WindowId(matchId, request.phaseOrdinal, point, request.windowSequence, request.startTick, request.endTick, request.sourceFingerprint)) return false;
            if (_v02Match == matchId && _v02Decision == decisionId) { reason = "REPLAY"; return _v02Plan.Fingerprint() == next.Fingerprint(); }
            var previous = _v02Plan ?? AEDV02Plan.Normal();
            var optional = point == AEDV02DecisionPoint.PRE_MATCH ? (int)next.Get(AEDV02Key.OptionalToolBudget) : _v02InitialOptional;
            if (_v02Match != Guid.Empty && _v02Match != matchId || request.expectedPlanRevision != _v02Revision ||
                approval.expectedPlanRevision != _v02Revision || approval.planRevision <= _v02Revision ||
                approval.previousPlanFingerprint != previous.Fingerprint() || request.previousPlanFingerprint != previous.Fingerprint() ||
                (!next.TryValidateChange(previous, point, AEDV02PhaseMap.LogicalPhase(request.targetPhaseName) == 4,
                    optional, out _, out reason) && !(request.support != null && next.ToArray().SequenceEqual(previous.ToArray()) && reason == "HOLD_SINGLE_DELTA_REQUIRED"))) return false;
            if (request.proposedReservations != null && next.Get(AEDV02Key.ResourceGrade) == previous.Get(AEDV02Key.ResourceGrade)) return false;
            if (_v02Match == Guid.Empty) V02Hide.Reset(matchId);
            _v02Match = matchId; _v02Decision = decisionId; _v02Plan = next; _v02Revision = approval.planRevision;
            _v02InitialOptional = optional; _v02Roster = currentRoster; _v02Scene = approval.sceneManifestFingerprint;
            InvalidateV02Source(); reason = string.Empty; return true;
        }

        public static bool TryGetV02(Guid matchId, out AEDV02Plan plan, out int revision)
        {
            plan = null; revision = 0;
            if (_v02Match != matchId || matchId == Guid.Empty || _v02Plan == null) return false;
            plan = _v02Plan; revision = _v02Revision; return true;
        }
        public static int V02PositiveTokens(Guid matchId) => TryGetV02(matchId, out var plan, out _) ? plan.PositiveTokens(_v02InitialOptional) : int.MaxValue;

        public static bool AcceptV02Source(Guid matchId, AEDV02SourceResultDto source, string currentRoster,
            IEnumerable<Guid> users, DateTime nowUtc)
        {
            if (TryAcceptV02Source(matchId, source, currentRoster, users, nowUtc)) return true;
            InvalidateV02Source(); return false;
        }
        private static bool TryAcceptV02Source(Guid matchId, AEDV02SourceResultDto source, string currentRoster,
            IEnumerable<Guid> users, DateTime nowUtc)
        {
            if (_v02Match != matchId || _v02Plan == null || source == null || !source.pressureComplete || source.players == null || users == null ||
                source.rosterIdentity != currentRoster || currentRoster != _v02Roster || source.planRevision != _v02Revision ||
                source.planFingerprint != _v02Plan.Fingerprint() || source.sceneManifestFingerprint != _v02Scene ||
                !AEDV02WorldSample.Hash(source.fingerprint) || source.startTick < 0 || source.endTick <= source.startTick ||
                !DateTime.TryParse(source.sourceEndedAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var ended) ||
                nowUtc - ended.ToUniversalTime() > TimeSpan.FromSeconds(AEDV02PilotPolicy.EvidenceMaxAgeSeconds) || ended.ToUniversalTime() > nowUtc.AddSeconds(2)) return false;
            var roster = users.ToHashSet();
            var resolved = new Dictionary<Guid, AEDV02PressureSnapshot>();
            foreach (var player in source.players)
            {
                if (player == null || !Guid.TryParse(player.userId, out var user) || !roster.Contains(user) || resolved.ContainsKey(user) ||
                    player.components == null || player.components.Length != 5 || player.components.Any(x => !AEDV02PilotPolicy.Finite(x) || x < 0 || x > 1) ||
                    !AEDV02PilotPolicy.Finite(player.score) || player.score < 0 || player.score > 1 || player.tier != AEDV02PilotPolicy.Tier(player.score)) return false;
                var weights = new[] { .30, .20, .25, .15, .10 };
                if (Math.Abs(player.components.Select((x, i) => x * weights[i]).Sum() - player.score) > .000001) return false;
                resolved.Add(user, new AEDV02PressureSnapshot(player.components, player.score, true, "BACKEND_SOURCE_VERIFIED"));
            }
            if (!resolved.Keys.ToHashSet().SetEquals(roster)) return false;
            V02Pressure.Clear(); V02Episodes.Clear();
            foreach (var item in resolved) V02Pressure.Add(item.Key, item.Value);
            foreach (var player in source.players) V02Episodes.Add(Guid.Parse(player.userId), player.episodeId ?? string.Empty);
            _v02SourceEndedUtc = ended.ToUniversalTime(); return true;
        }
        public static void InvalidateV02Source()
        {
            V02Pressure.Clear(); V02Episodes.Clear(); _v02SourceEndedUtc = default;
            foreach (var memory in V02Tiers.Values) memory.InvalidateObservation();
        }
        public static bool TryGetV02Pressure(Guid matchId, Guid user, DateTime nowUtc, out AEDV02PressureSnapshot pressure)
        {
            pressure = default;
            return _v02Match == matchId && nowUtc - _v02SourceEndedUtc <= TimeSpan.FromSeconds(AEDV02PilotPolicy.EvidenceMaxAgeSeconds) && V02Pressure.TryGetValue(user, out pressure);
        }
        public static int V02StableTier(Guid matchId, Guid user, DateTime nowUtc, double simulationSeconds)
        {
            if (!TryGetV02Pressure(matchId, user, nowUtc, out var pressure))
            { if (V02Tiers.TryGetValue(user, out var unknown)) unknown.InvalidateObservation(); return -1; }
            if (!V02Tiers.TryGetValue(user, out var memory)) V02Tiers.Add(user, memory = new AEDV02PressureTierMemory());
            return memory.Update(pressure, simulationSeconds);
        }
        public static bool TryGrantV02Hide(Guid matchId, Guid user, ulong spot, double simulationSeconds,
            DateTime nowUtc, out AEDV02HideProtectionToken token)
        {
            token = null;
            if (V02Hide.TryGetCurrentToken(matchId, user, spot, simulationSeconds, out token)) return true;
            if (!TryGetV02Pressure(matchId, user, nowUtc, out var pressure)) return false;
            if (!V02Episodes.TryGetValue(user, out var episode) || string.IsNullOrWhiteSpace(episode)) return false;
            return V02Hide.TryGrant(matchId, user, spot, episode, _v02Revision, simulationSeconds, _v02Plan != null, pressure, out token);
        }
        public static bool BlocksV02Hide(Guid matchId, Guid user, ulong spot, double seconds, out string reason) =>
            V02Hide.BlocksForceExit(matchId, user, spot, seconds, out reason);
        public static void ExitV02Hide(Guid user, ulong spot, double seconds) => V02Hide.Exit(user, spot, seconds);
        public static void ExitV02HideToken(Guid token, ulong spot, double seconds) => V02Hide.ExitToken(token, spot, seconds);
        public static bool TryGetExistingV02Hide(Guid matchId, Guid user, ulong spot, double seconds, out AEDV02HideProtectionToken token) =>
            V02Hide.TryGetCurrentToken(matchId, user, spot, seconds, out token);
        private static void ResetV02(Guid matchId)
        {
            if (matchId != Guid.Empty && matchId != _v02Match) return;
            _v02Match = Guid.Empty; _v02Decision = Guid.Empty; _v02Plan = null; _v02Revision = 0; _v02InitialOptional = 0;
            _v02Roster = null; _v02Scene = null; V02Pressure.Clear(); V02Episodes.Clear(); V02Tiers.Clear(); V02Hide.Reset(Guid.Empty);
        }
    }
}
