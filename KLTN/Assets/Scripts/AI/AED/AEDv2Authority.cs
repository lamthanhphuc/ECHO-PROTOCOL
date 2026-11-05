using System;
using EchoProtocol.AI.Common.AED;
using EchoProtocol.AI.Common.Profile;

namespace EchoProtocol.AI.AED
{
    // Authority-only sidecar. Never writes to gameplay unless an explicit opt-in
    // has been configured AND valid observed profile evidence passes AEDInputGate.
    public static class AEDv2Authority
    {
        private static Guid _matchId;
        private static Guid _pendingMatchId;
        private static Guid _pendingDecisionId;
        private static Guid _pendingSnapshotId;
        private static string _pendingSnapshotFingerprint;
        private static ScenarioResolutionRequest _pendingRequest;
        private static AEDv2Decision _pendingDecision;
        private static bool _pendingApply;
        private static AEDv2Plan _applied;
        private static uint _revision;

        public static AEDv2Decision LastProposal { get; private set; }
        public static uint Revision => _revision;
        public static bool HasAppliedPlan => _applied != null && _matchId != Guid.Empty;

        public static void Stage(
            ScenarioResolutionRequest request,
            AdaptiveInputSnapshot snapshot,
            AEDPolicyConfig policy,
            AEDEvidencePolicy evidence,
            AdaptiveInputCurrencyValidation currency,
            bool shadowEnabled,
            bool gameplayEnabled)
        {
            if (request == null || (!shadowEnabled && !gameplayEnabled)) return;
            if (request.DecisionPoint != ScenarioDecisionPoint.PreMatch ||
                request.ResolutionMode != ScenarioResolutionMode.Adaptive)
                return;
            var gate = AEDInputGate.Evaluate(snapshot, request, policy, evidence, currency);
            var decision = AEDv2Policy.Evaluate(request, snapshot, gate, policy);
            LastProposal = decision;
            _pendingRequest = request;
            _pendingMatchId = request.TargetMatchId;
            _pendingDecisionId = request.ResolutionId;
            _pendingSnapshotId = gate.SnapshotId;
            _pendingSnapshotFingerprint = gate.SnapshotContentFingerprint;
            _pendingDecision = decision;
            _pendingApply = gameplayEnabled && decision.Changed;
        }

        public static bool Commit(
            Guid matchId, Guid decisionId,
            bool hasStateAuthority, bool precommitRejected)
        {
            if (!hasStateAuthority || precommitRejected || !_pendingApply ||
                (_applied != null && _matchId == matchId) ||
                !AEDv2GameplayBridge.IsNormalCompatible() ||
                _pendingRequest == null || _pendingDecision == null ||
                _pendingMatchId != matchId || _pendingDecisionId != decisionId ||
                !AdaptiveInputSnapshotRuntime.IsStillCurrent(
                    _pendingRequest, _pendingSnapshotId,
                    _pendingSnapshotFingerprint, out _))
            {
                ClearPending();
                return false;
            }
            // Only accept a complete, independently validated one-axis candidate.
            if (!_pendingDecision.Plan.TrySingleBoundedChange(out var changed) ||
                !_pendingDecision.Key.HasValue || changed != _pendingDecision.Key.Value)
            {
                ClearPending();
                return false;
            }
            _matchId = matchId;
            _applied = _pendingDecision.Plan;
            _revision++;
            if (_revision == 0) _revision = 1;
            ClearPending();
            return true;
        }

        public static bool TryGetApplied(Guid matchId, out AEDv2Plan plan, out uint revision)
        {
            plan = null;
            revision = 0;
            if (matchId == Guid.Empty || _matchId != matchId || _applied == null)
                return false;
            plan = _applied;
            revision = _revision;
            return true;
        }

        public static void Reset(Guid matchId)
        {
            if (matchId == Guid.Empty || _matchId == matchId)
            {
                _matchId = Guid.Empty;
                _applied = null;
                _revision = 0;
                LastProposal = null;
            }
            ClearPending();
        }

        private static void ClearPending()
        {
            _pendingMatchId = Guid.Empty;
            _pendingDecisionId = Guid.Empty;
            _pendingSnapshotId = Guid.Empty;
            _pendingSnapshotFingerprint = null;
            _pendingRequest = null;
            _pendingDecision = null;
            _pendingApply = false;
        }
    }
}
