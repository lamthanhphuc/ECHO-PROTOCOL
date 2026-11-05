#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace EchoProtocol.AI.Common.AED
{
    public sealed class AEDV02ProposedReservation
    {
        public int Zone { get; set; }
        public int ToolId { get; set; }
        public string MarkerId { get; set; } = string.Empty;
        public bool IsValid() => Zone is >= 1 and <= 3 && ToolId is 1 or 2 or 3 or 4 or 6 && AEDV02WorldSample.Hash(MarkerId);
        public static string Fingerprint(IEnumerable<AEDV02ProposedReservation>? items) => items == null ? string.Empty :
            AEDV02Plan.Hash(string.Join("|", items.OrderBy(x => x.Zone).ThenBy(x => x.ToolId).ThenBy(x => x.MarkerId, StringComparer.Ordinal)
                .Select(x => x.Zone + ":" + x.ToolId + ":" + x.MarkerId)));
    }
    public sealed class AEDV02SupportAction
    {
        public string Kind { get; set; } = string.Empty;
        public Guid UserId { get; set; }
        public Guid CauseEventId { get; set; }
        public int ToolId { get; set; }
        public int Zone { get; set; }
        public string MarkerId { get; set; } = string.Empty;
        public bool IsValid() => Kind is "ORDINARY" or "EMERGENCY" && UserId != Guid.Empty && CauseEventId != Guid.Empty &&
            ToolId is 1 or 2 or 3 or 4 or 6 && (Kind != "EMERGENCY" || ToolId == 3) && Zone is >= 1 and <= 3 && AEDV02WorldSample.Hash(MarkerId);
        public string Fingerprint(Guid decision) => AEDV02Plan.Hash(AEDV02PilotOpportunities.Version + "|" + decision.ToString("D") + "|" +
            Kind + "|" + UserId.ToString("D") + "|" + CauseEventId.ToString("D") + "|" + ToolId + "|" + Zone + "|" + MarkerId);
    }
    public sealed class AEDV02ResourceNeed
    {
        public Guid UserId { get; set; }
        public Guid CauseEventId { get; set; }
        public int ToolId { get; set; }
        public bool Downed { get; set; }
    }
    public sealed class AEDV02PlanCommitSource
    {
        public Guid DecisionId { get; set; }
        public int PlanRevision { get; set; }
        public string PlanFingerprint { get; set; } = string.Empty;
        public string ActionFingerprint { get; set; } = string.Empty;
        public long AppliedTick { get; set; }
        public bool IsValid() => DecisionId != Guid.Empty && PlanRevision > 0 && AEDV02WorldSample.Hash(PlanFingerprint) &&
            (ActionFingerprint == string.Empty || AEDV02WorldSample.Hash(ActionFingerprint)) && AppliedTick >= 0;
    }
}
