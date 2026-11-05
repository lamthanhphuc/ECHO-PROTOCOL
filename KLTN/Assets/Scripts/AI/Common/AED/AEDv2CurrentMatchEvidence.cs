using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace EchoProtocol.AI.Common.AED
{
    public sealed class AEDv2CurrentMatchEvidence
    {
        public Guid MatchId { get; }
        public string CompletedPhase { get; }
        public uint PhaseOrdinal { get; }
        public string RosterIdentity { get; }
        public DateTime StartedAtUtc { get; }
        public DateTime EndedAtUtc { get; }
        public int AlivePlayers { get; }
        public int DownCount { get; }
        public int ReviveCount { get; }
        public int EliminatedCount { get; }
        public int AcceptedNoiseCount { get; }
        public int ObjectiveProgress { get; }
        public int TeamToolUseCount { get; }
        public double ElapsedSeconds => (EndedAtUtc - StartedAtUtc).TotalSeconds;
        public string EvidenceFingerprint { get; }
        public bool TelemetryCompleteness { get; }
        public IReadOnlyList<string> ReasonCodes { get; }

        public AEDv2CurrentMatchEvidence(Guid matchId, string completedPhase,
            uint phaseOrdinal, string rosterIdentity, DateTime startedAtUtc,
            DateTime endedAtUtc, int alivePlayers, int downCount, int reviveCount,
            int eliminatedCount, int acceptedNoiseCount, int objectiveProgress,
            bool telemetryCompleteness, IReadOnlyList<string> reasonCodes,
            int teamToolUseCount = 0)
        {
            if (matchId == Guid.Empty || string.IsNullOrWhiteSpace(completedPhase)
                || phaseOrdinal == 0 || string.IsNullOrWhiteSpace(rosterIdentity)
                || startedAtUtc.Kind != DateTimeKind.Utc || endedAtUtc.Kind != DateTimeKind.Utc
                || endedAtUtc < startedAtUtc)
                throw new ArgumentException("AED_V2_EVIDENCE_INVALID");
            MatchId = matchId;
            CompletedPhase = completedPhase;
            PhaseOrdinal = phaseOrdinal;
            RosterIdentity = rosterIdentity;
            StartedAtUtc = startedAtUtc;
            EndedAtUtc = endedAtUtc;
            AlivePlayers = alivePlayers;
            DownCount = downCount;
            ReviveCount = reviveCount;
            EliminatedCount = eliminatedCount;
            AcceptedNoiseCount = acceptedNoiseCount;
            ObjectiveProgress = objectiveProgress;
            TeamToolUseCount = teamToolUseCount;
            TelemetryCompleteness = telemetryCompleteness;
            ReasonCodes = Array.AsReadOnly(reasonCodes == null
                ? Array.Empty<string>() : new List<string>(reasonCodes).ToArray());
            EvidenceFingerprint = ComputeFingerprint(this);
        }

        public bool HasValidFingerprint() =>
            string.Equals(EvidenceFingerprint, ComputeFingerprint(this), StringComparison.Ordinal);

        private static string ComputeFingerprint(AEDv2CurrentMatchEvidence e)
        {
            var content = string.Join("|", "AED_V2_PHASE_EVIDENCE_V2", e.MatchId.ToString("D"), e.CompletedPhase,
                e.PhaseOrdinal.ToString(CultureInfo.InvariantCulture), e.RosterIdentity,
                e.StartedAtUtc.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture),
                e.EndedAtUtc.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture),
                e.AlivePlayers.ToString(CultureInfo.InvariantCulture), e.DownCount.ToString(CultureInfo.InvariantCulture),
                e.ReviveCount.ToString(CultureInfo.InvariantCulture), e.EliminatedCount.ToString(CultureInfo.InvariantCulture),
                e.AcceptedNoiseCount.ToString(CultureInfo.InvariantCulture), e.ObjectiveProgress.ToString(CultureInfo.InvariantCulture),
                e.TeamToolUseCount.ToString(CultureInfo.InvariantCulture), e.TelemetryCompleteness ? "1" : "0",
                string.Join(",", e.ReasonCodes));
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(content)))
                    .Replace("-", string.Empty).ToLowerInvariant();
        }
    }
}
