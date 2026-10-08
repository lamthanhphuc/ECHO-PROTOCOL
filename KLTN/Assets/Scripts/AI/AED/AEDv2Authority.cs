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
        private static uint _lastBoundaryPhaseOrdinal;
        private static Guid _approvedMatchId;
        private static Guid _approvedDecisionId;
        private static Guid _approvedSnapshotId;
        private static string _approvedSnapshotFingerprint;
        private static string _approvedRosterIdentity;
        private static string _approvedPlanFingerprint;

        public static AEDv2Decision LastProposal { get; private set; }
        public static uint Revision => _revision;
        public static bool HasAppliedPlan => _applied != null && _matchId != Guid.Empty;
        public static bool HasBackendPreMatchApproval => _approvedMatchId != Guid.Empty;
        public static Guid LastAppliedDecisionId { get; private set; }
        public static string LastAppliedPlanFingerprint { get; private set; }

        public static void ApproveBackendPreMatch(Guid matchId, AEDPlanV2Data plan)
        {
            ClearApproval();
            if (plan == null || plan.commitStatus != "COMMITTED"
                || plan.matchId != matchId.ToString("D")
                || !Guid.TryParse(plan.decisionId, out _approvedDecisionId)
                || !Guid.TryParse(plan.snapshotId, out _approvedSnapshotId)) return;
            _approvedMatchId = matchId;
            _approvedSnapshotFingerprint = plan.snapshotFingerprint;
            _approvedRosterIdentity = plan.rosterIdentity;
            _approvedPlanFingerprint = plan.resultingPlanFingerprint;
        }

        public static bool TryValidateTransition(AEDv2Plan previous, AEDv2Plan next,
            ScenarioDecisionPoint decisionPoint, out AEDv2Key changedKey, out string reason)
        {
            changedKey = default;
            reason = "AED_V2_PLAN_INVALID";
            if (previous == null || next == null || !previous.IsBounded() || !next.IsBounded())
                return false;
            var changes = 0;
            foreach (var spec in AEDv2Catalog.All)
            {
                var before = previous.Get(spec.Key);
                var after = next.Get(spec.Key);
                if (before == after) continue;
                changedKey = spec.Key;
                if (++changes > 1)
                {
                    reason = "AED_V2_MULTIPLE_KEYS";
                    return false;
                }
                if (spec.PreMatchOnly && decisionPoint != ScenarioDecisionPoint.PreMatch)
                {
                    reason = "AED_V2_PREMATCH_ONLY";
                    return false;
                }
                if ((spec.Key == AEDv2Key.SupportBonus
                    || spec.Key == AEDv2Key.ReviveBonusPerZone) && after < before)
                {
                    reason = "AED_V2_RESOURCE_BONUS_REDUCTION";
                    return false;
                }
                if (spec.Key == AEDv2Key.ReviveBonusPerZone
                    && decisionPoint == ScenarioDecisionPoint.FinalHuntSetup)
                {
                    reason = "AED_V2_REVIVE_TIMING";
                    return false;
                }
            }
            reason = changes == 0 ? "AED_V2_NO_CHANGE" : string.Empty;
            return true;
        }

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
            _pendingApply = gameplayEnabled && decision.Changed
                && _approvedMatchId == request.TargetMatchId
                && _approvedDecisionId == request.ResolutionId
                && _approvedSnapshotId == gate.SnapshotId
                && _approvedSnapshotFingerprint == gate.SnapshotContentFingerprint
                && _approvedRosterIdentity == gate.RosterIdentity
                && _approvedPlanFingerprint == decision.Plan.Fingerprint();
        }

        public static bool Commit(
            Guid matchId, Guid decisionId,
            bool hasStateAuthority, bool precommitRejected)
        {
            if (!hasStateAuthority || precommitRejected || !_pendingApply ||
                (_applied != null && _matchId != matchId) ||
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
            if (!TryValidateTransition(_applied ?? AEDv2Plan.Normal(),
                    _pendingDecision.Plan, _pendingRequest.DecisionPoint,
                    out var changed, out var reason)
                || reason.Length != 0 ||
                !_pendingDecision.Key.HasValue || changed != _pendingDecision.Key.Value)
            {
                ClearPending();
                return false;
            }
            _matchId = matchId;
            _applied = _pendingDecision.Plan;
            LastAppliedDecisionId = decisionId;
            LastAppliedPlanFingerprint = _applied.Fingerprint();
            _revision++;
            if (_revision == 0) _revision = 1;
            ClearPending();
            ClearApproval();
            return true;
        }

        public static bool CommitBoundary(Guid matchId, Guid decisionId,
            uint phaseOrdinal, string rosterIdentity, string evidenceFingerprint,
            AEDv2Plan next, AEDv2Key expectedKey, AEDPlanV2Data approval,
            bool hasStateAuthority, bool matchRunning, bool boundarySafe,
            out string reason)
        {
            reason = "AED_V2_BOUNDARY_COMMIT_REJECTED";
            if (!hasStateAuthority || !matchRunning || !boundarySafe
                || matchId == Guid.Empty || decisionId == Guid.Empty
                || phaseOrdinal == 0 || string.IsNullOrWhiteSpace(rosterIdentity)
                || string.IsNullOrWhiteSpace(evidenceFingerprint)
                || approval == null || approval.commitStatus != "COMMITTED"
                || (approval.applyStatus != "PENDING" && approval.applyStatus != "APPLIED")
                || approval.matchId != matchId.ToString("D")
                || approval.decisionId != decisionId.ToString("D")
                || approval.phaseOrdinal != phaseOrdinal
                || approval.rosterIdentity != rosterIdentity
                || approval.evidenceFingerprint != evidenceFingerprint
                || approval.resultingPlanFingerprint != next?.Fingerprint())
                return false;

            if (LastAppliedDecisionId == decisionId)
            {
                reason = string.Empty;
                return LastAppliedPlanFingerprint == approval.resultingPlanFingerprint;
            }

            var previous = _applied ?? AEDv2Plan.Normal();
            if ((_applied != null && _matchId != matchId)
                || phaseOrdinal <= _lastBoundaryPhaseOrdinal
                || approval.previousPlanFingerprint != previous.Fingerprint()
                || !TryValidateTransition(previous, next,
                    approval.decisionPoint == "FINAL_HUNT_SETUP"
                        ? ScenarioDecisionPoint.FinalHuntSetup
                        : ScenarioDecisionPoint.AllowedPhaseBoundary,
                    out var changedKey, out reason)
                || changedKey != expectedKey)
                return false;

            _matchId = matchId;
            _applied = next;
            LastAppliedDecisionId = decisionId;
            LastAppliedPlanFingerprint = next.Fingerprint();
            _lastBoundaryPhaseOrdinal = phaseOrdinal;
            _revision++;
            if (_revision == 0) _revision = 1;
            reason = string.Empty;
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
                LastAppliedDecisionId = Guid.Empty;
                LastAppliedPlanFingerprint = null;
                _lastBoundaryPhaseOrdinal = 0;
            }
            ClearPending();
            ClearApproval();
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

        private static void ClearApproval()
        {
            _approvedMatchId = Guid.Empty;
            _approvedDecisionId = Guid.Empty;
            _approvedSnapshotId = Guid.Empty;
            _approvedSnapshotFingerprint = null;
            _approvedRosterIdentity = null;
            _approvedPlanFingerprint = null;
        }
    }
}
