using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace EchoProtocol.AI.Common.AED
{
    public static class AEDResourceToolIdsV1
    {
        public const int Scanner = 1;
        public const int NoiseMaker = 2;
        public const int FirstAid = 3;
        public const int DoorJammer = 4;
        public const int CoreStabilizer = 6;

        public static readonly int[] All =
        {
            Scanner, NoiseMaker, FirstAid, DoorJammer, CoreStabilizer
        };
    }

    public enum AEDResourceIntentV1 { Hold, Relieve, ConstrainSupply }

    public sealed class AEDResourceRequestV1
    {
        public Guid MatchId { get; set; }
        public string Difficulty { get; set; }
        public string ResolutionMode { get; set; }
        public bool FullRosterVerified { get; set; }
        public bool FullRosterObserved { get; set; }
        public bool TeamConfidenceComplete { get; set; }
        public bool BackendMetricVerifierSupported { get; set; }
        public bool PressureSourceVerified { get; set; }
        public string PressureLevel { get; set; }
        public decimal? WeakestSkill { get; set; }
        public bool AnyPlayerStruggling { get; set; }
        public IReadOnlyList<int> LobbyToolIds { get; set; }
        public string ComparisonContextKey { get; set; }
        public string EvidenceFingerprint { get; set; }
    }

    public sealed class AEDResourceProposalV1
    {
        public const string Version = "AED_RESOURCE_PROPOSAL_V1_RESEARCH";
        public Guid MatchId { get; }
        public AEDResourceIntentV1 Intent { get; }
        public string ReasonCode { get; }
        public string EvidenceFingerprint { get; }
        public string ComparisonContextKey { get; }
        public bool ResearchOnly => true;
        public bool CanApplyGameplay => false;
        public IReadOnlyList<int> Zone1 { get; }
        public IReadOnlyList<int> Zone2 { get; }
        public IReadOnlyList<int> Zone3 { get; }
        public bool PreferDistantScanner { get; }

        internal AEDResourceProposalV1(Guid matchId, AEDResourceIntentV1 intent,
            string reason, AEDResourceRequestV1 request, IEnumerable<int> zone1,
            IEnumerable<int> zone2, IEnumerable<int> zone3, bool distantScanner)
        {
            MatchId = matchId; Intent = intent; ReasonCode = reason;
            EvidenceFingerprint = request?.EvidenceFingerprint;
            ComparisonContextKey = request?.ComparisonContextKey;
            Zone1 = new ReadOnlyCollection<int>(zone1.ToArray());
            Zone2 = new ReadOnlyCollection<int>(zone2.ToArray());
            Zone3 = new ReadOnlyCollection<int>(zone3.ToArray());
            PreferDistantScanner = distantScanner;
        }
    }

    public static class AEDResourceDirectorV1
    {
        public const string RuleVersion = "AED_RESOURCE_DIRECTOR_V1_RESEARCH";
        private static int[] Baseline() => (int[])AEDResourceToolIdsV1.All.Clone();

        public static AEDResourceProposalV1 Evaluate(AEDResourceRequestV1 input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));

            AEDResourceProposalV1 Hold(string reason) => new(
                input.MatchId, AEDResourceIntentV1.Hold, reason, input,
                Baseline(), Baseline(), Baseline(), false);

            if (input.MatchId == Guid.Empty)
                return Hold("HOLD_INVALID_MATCH");
            if (input.Difficulty != "Normal" || input.ResolutionMode != "Adaptive")
                return Hold("HOLD_FIXED_OR_NON_NORMAL");
            if (!input.FullRosterVerified || !input.FullRosterObserved)
                return Hold("HOLD_ROSTER_UNVERIFIED");
            if (!input.PressureSourceVerified || string.IsNullOrWhiteSpace(input.PressureLevel))
                return Hold("HOLD_PRESSURE_UNVERIFIED");

            if (input.PressureLevel == "Critical" || input.AnyPlayerStruggling)
            {
                return new AEDResourceProposalV1(
                    input.MatchId, AEDResourceIntentV1.Relieve,
                    "RELIEVE_RESOURCE_PRESSURE_CANDIDATE", input,
                    Baseline().Concat(new[] { AEDResourceToolIdsV1.FirstAid }),
                    Baseline().Concat(new[] { AEDResourceToolIdsV1.FirstAid }),
                    Baseline(), false);
            }

            if (input.PressureLevel != "Quiet")
                return Hold("HOLD_PRESSURE_NOT_LOW");
            if (input.LobbyToolIds == null ||
                input.LobbyToolIds.Any(id =>
                    !AEDResourceToolIdsV1.All.Contains(id)))
            {
                return Hold("HOLD_LOBBY_SUPPLY_UNKNOWN");
            }
            if (!input.BackendMetricVerifierSupported || !input.TeamConfidenceComplete ||
                !input.WeakestSkill.HasValue || input.WeakestSkill.Value < 0.70m ||
                input.WeakestSkill.Value > 1m || string.IsNullOrWhiteSpace(input.ComparisonContextKey) ||
                string.IsNullOrWhiteSpace(input.EvidenceFingerprint))
                return Hold("HOLD_SKILL_CONFIDENCE_INSUFFICIENT");

            var zone1 = new List<int>
            {
                AEDResourceToolIdsV1.Scanner, AEDResourceToolIdsV1.NoiseMaker,
                AEDResourceToolIdsV1.FirstAid, AEDResourceToolIdsV1.DoorJammer
            };
            if (input.LobbyToolIds?.Contains(AEDResourceToolIdsV1.CoreStabilizer) != true)
                zone1.Add(AEDResourceToolIdsV1.CoreStabilizer);

            int[] laterZone =
            {
                AEDResourceToolIdsV1.Scanner, AEDResourceToolIdsV1.NoiseMaker,
                AEDResourceToolIdsV1.FirstAid, AEDResourceToolIdsV1.DoorJammer
            };
            return new AEDResourceProposalV1(
                input.MatchId, AEDResourceIntentV1.ConstrainSupply,
                "VERIFIED_STRONG_TEAM_RESOURCE_CANDIDATE", input,
                zone1, laterZone, laterZone, true);
        }

        public static bool ValidateFairness(AEDResourceProposalV1 plan)
        {
            if (plan == null || plan.MatchId == Guid.Empty) return false;
            var zones = new[] { plan.Zone1, plan.Zone2, plan.Zone3 };
            if (zones.Any(zone => zone == null || zone.Count == 0 ||
                zone.Any(id => !AEDResourceToolIdsV1.All.Contains(id)) ||
                !zone.Contains(AEDResourceToolIdsV1.FirstAid))) return false;
            if (plan.Intent == AEDResourceIntentV1.ConstrainSupply &&
                zones.Sum(z => z.Count(id => id == AEDResourceToolIdsV1.CoreStabilizer)) > 1)
                return false;
            return true;
        }
    }
}
