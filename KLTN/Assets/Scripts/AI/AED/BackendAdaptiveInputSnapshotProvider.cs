using System;
using System.Threading;
using System.Threading.Tasks;
using EchoProtocol.AI.Common.AED;
using EchoProtocol.AI.Common.Profile;

namespace EchoProtocol.AI.AED
{
    public sealed class BackendAdaptiveInputSnapshotProvider : IAdaptiveInputSnapshotProvider, IDisposable
    {
        public static BackendAdaptiveInputSnapshotProvider Current { get; private set; }
        private readonly AEDSnapshotApiService _api = new AEDSnapshotApiService();
        private AdaptiveInputSnapshot _snapshot;
        private AdaptiveInputCurrencyValidation _currency;
        private Guid _decisionId;
        private string _reason = "ADAPTIVE_INPUT_PROVIDER_UNAVAILABLE";
        public Guid SnapshotId => _snapshot?.SnapshotId ?? Guid.Empty;
        public string SnapshotFingerprint => _snapshot?.SnapshotContentFingerprint ?? string.Empty;
        public string SnapshotValidity => _snapshot?.SnapshotValidity.ToString() ?? "Unavailable";
        public int TeamSize => _snapshot?.TeamSize ?? 0;
        public double? SurvivalMean => _snapshot?.RosterProfileSummary.Survival.MeanObservedScore;
        public double? NoiseMean => _snapshot?.RosterProfileSummary.Noise.MeanObservedScore;
        public string LastReason => _reason;

        public async Task<bool> PrepareAsync(Guid matchId, Guid decisionId,
            CancellationToken cancellationToken)
        {
            ClearForMatch(Guid.Empty);
            var result = await _api.GetAsync(matchId, decisionId, cancellationToken);
            if (!result.Succeeded || result.Snapshot.TargetMatchId != matchId
                || result.Snapshot.DecisionPoint != ScenarioDecisionPoint.PreMatch
                || result.Snapshot.PhaseContext != "PRE_MATCH")
            {
                _reason = result.Reason.Length > 0 ? result.Reason : "AED_SNAPSHOT_CONTEXT_MISMATCH";
                return false;
            }
            if (cancellationToken.IsCancellationRequested)
            {
                _reason = "AED_SNAPSHOT_CANCELLED";
                return false;
            }
            _snapshot = result.Snapshot;
            _currency = result.Currency;
            _decisionId = decisionId;
            _reason = string.Empty;
            Current = this;
            AdaptiveInputSnapshotRuntime.BindProvider(this);
            return true;
        }

        public bool TryGetSnapshot(ScenarioResolutionRequest request,
            out AdaptiveInputSnapshot snapshot,
            out AdaptiveInputCurrencyValidation currency,
            out string reason)
        {
            snapshot = null;
            currency = null;
            reason = _reason;
            if (_snapshot == null || _currency?.IsCurrent != true || request == null
                || request.TargetMatchId != _snapshot.TargetMatchId
                || request.ResolutionId != _decisionId
                || request.DecisionPoint != _snapshot.DecisionPoint
                || request.PhaseContext != _snapshot.PhaseContext)
            {
                if (string.IsNullOrEmpty(reason)) reason = "AED_SNAPSHOT_CONTEXT_MISMATCH";
                return false;
            }
            snapshot = _snapshot;
            currency = _currency;
            reason = string.Empty;
            return true;
        }

        public void ClearForMatch(Guid matchId)
        {
            if (matchId != Guid.Empty && _snapshot?.TargetMatchId != matchId) return;
            AdaptiveInputSnapshotRuntime.ClearProvider(this);
            if (ReferenceEquals(Current, this)) Current = null;
            _snapshot = null;
            _currency = null;
            _decisionId = Guid.Empty;
            _reason = "ADAPTIVE_INPUT_PROVIDER_UNAVAILABLE";
        }

        public void Dispose() => ClearForMatch(Guid.Empty);
    }
}
