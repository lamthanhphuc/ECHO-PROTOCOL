using System;
using System.Collections.Generic;

namespace EchoProtocol.AI.Common.AED
{
    public enum AdaptiveDecisionLedgerLookup
    {
        Missing,
        ExactReplay,
        IdentityConflict
    }

    public sealed class AdaptiveDecisionLedger
    {
        private sealed class LedgerEntry
        {
            public LedgerEntry(
                string semanticFingerprint,
                AdaptiveDecision decision)
            {
                SemanticFingerprint = semanticFingerprint;
                Decision = decision;
            }

            public string SemanticFingerprint { get; }
            public AdaptiveDecision Decision { get; }
        }

        private readonly Dictionary<Guid, LedgerEntry> _entries =
            new Dictionary<Guid, LedgerEntry>();

        public AdaptiveDecisionLedgerLookup Lookup(
            Guid decisionId,
            string semanticFingerprint,
            out AdaptiveDecision decision)
        {
            if (decisionId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Decision id is required.",
                    nameof(decisionId));
            }

            if (string.IsNullOrWhiteSpace(semanticFingerprint))
            {
                throw new ArgumentException(
                    "Semantic fingerprint is required.",
                    nameof(semanticFingerprint));
            }

            if (!_entries.TryGetValue(decisionId, out var entry))
            {
                decision = null;
                return AdaptiveDecisionLedgerLookup.Missing;
            }

            decision = entry.Decision;

            return string.Equals(
                entry.SemanticFingerprint,
                semanticFingerprint,
                StringComparison.Ordinal)
                ? AdaptiveDecisionLedgerLookup.ExactReplay
                : AdaptiveDecisionLedgerLookup.IdentityConflict;
        }

        public bool TryRecord(
            AdaptiveDecision decision,
            string semanticFingerprint)
        {
            if (decision == null)
            {
                throw new ArgumentNullException(nameof(decision));
            }

            if (string.IsNullOrWhiteSpace(semanticFingerprint))
            {
                throw new ArgumentException(
                    "Semantic fingerprint is required.",
                    nameof(semanticFingerprint));
            }

            if (_entries.ContainsKey(decision.DecisionId))
            {
                return false;
            }

            _entries.Add(
                decision.DecisionId,
                new LedgerEntry(
                    semanticFingerprint,
                    decision));

            return true;
        }

        public void Clear()
        {
            _entries.Clear();
        }
    }
}
