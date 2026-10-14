using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EchoProtocol.AI.AED;
using EchoProtocol.AI.Common.AED;
using EchoProtocol.AI.Stalker.Networking;
using EchoProtocol.Auth;
using EchoProtocol.Gameplay;
using UnityEngine;

namespace EchoProtocol.Networking.Authority
{
    public sealed partial class MatchAuthorityRuntime
    {
        private AEDV02DecisionRequestDto _v02StagedRequest;
        private AEDV02DecisionResultDto _v02StagedApproval;
        private AEDV02DecisionRequestDto _v02PendingRequest;
        private AEDV02DecisionResultDto _v02PendingApproval;
        private AEDV02ReceiptDto _v02Receipt;
        private bool _v02DecisionBusy;
        private bool _v02ReceiptBusy;
        private float _v02NextReceiptAt;
        private string _v02EvaluatedSource;
        private int _v02WindowSequence;
        private int _v02CoordinatorGeneration;
        private readonly HashSet<Guid> _v02Aborts = new HashSet<Guid>();

        private bool V02GameplayActive => IsHostBinding && RequestedScenarioResolutionMode == ScenarioResolutionMode.Adaptive &&
            Difficulty == MatchDifficulty.Normal && ScenarioConfigAuthorityRuntime.Instance?.RuntimeSettings?.PacingV02GameplayEnabled == true;

        public async Task StageV02PreMatchAsync(CancellationToken cancellation)
        {
            if (!V02GameplayActive || !AEDV02SceneSource.TryLoad(out _, out var scene)) return;
            var snapshot = BackendAdaptiveInputSnapshotProvider.Current;
            if (snapshot == null || snapshot.SnapshotValidity != "Valid") { V02Log("PREMATCH_HOLD", "HOLD_INVALID_SNAPSHOT"); return; }
            var match = MatchId;
            var request = BuildV02Request(AEDV02Plan.Normal(), AEDV02Plan.Normal(), 0, AEDV02DecisionPoint.PRE_MATCH, 0, "CORE_COLLECTION", null, scene);
            var approval = await RequestV02ApprovalAsync(match, request);
            if (cancellation.IsCancellationRequested || !V02GameplayActive || MatchId != match || request.rosterIdentity != AEDv2RosterIdentity)
            { QueueV02Abort(match, request); return; }
            if (approval?.approvalStatus != "APPROVED") return;
            _v02StagedRequest = request; _v02StagedApproval = approval;
            V02Log("PREMATCH_STAGED", approval.reasonCode, approval);
        }

        public void CommitStagedV02PreMatch(NetworkMatchState state)
        {
            var request = _v02StagedRequest; var approval = _v02StagedApproval;
            _v02StagedRequest = null; _v02StagedApproval = null;
            if (request == null) return;
            // A baseline envelope must not overwrite a separately issued v1 chase candidate.
            if (!V02GameplayActive || Math.Abs(state.ScenarioChaseSpeed - AEDV02Plan.Normal().Get(AEDV02Key.ChaseSpeed)) > .000001 ||
                !state.TryCommitV02Authoritative(request, approval, out var reason))
            { V02Log("PREMATCH_HOLD", "HOLD_PREMATCH_REVALIDATION"); QueueV02Abort(MatchId, request); return; }
            AwaitV02Consumer(request, approval, AEDV02Plan.Normal());
        }

        public void ClearStagedV02PreMatch()
        {
            if (_v02StagedRequest != null) QueueV02Abort(MatchId, _v02StagedRequest);
            _v02StagedRequest = null; _v02StagedApproval = null;
        }

        private AEDV02DecisionRequestDto BuildV02Request(AEDV02Plan previous, AEDV02Plan next, int revision,
            AEDV02DecisionPoint point, int sequence, string targetPhase, AEDV02SourceResultDto source, string scene)
        {
            var snapshot = BackendAdaptiveInputSnapshotProvider.Current;
            return new AEDV02DecisionRequestDto
            {
                decisionId = Guid.NewGuid().ToString("D"), phaseOrdinal = source?.phaseOrdinal ?? 0,
                decisionPoint = point.ToString(), windowSequence = sequence,
                windowId = AEDV02Plan.WindowId(MatchId, source?.phaseOrdinal ?? 0, point, sequence, source?.startTick ?? 0, source?.endTick ?? 0, source?.fingerprint ?? string.Empty),
                expectedPlanRevision = revision, previousPlanFingerprint = previous.Fingerprint(), planValues = next.ToArray(),
                snapshotId = snapshot.SnapshotId.ToString("D"), snapshotFingerprint = snapshot.SnapshotFingerprint,
                rosterIdentity = AEDv2RosterIdentity, sourceFingerprint = source?.fingerprint ?? string.Empty,
                startTick = source?.startTick ?? 0, endTick = source?.endTick ?? 0, sceneManifestFingerprint = scene,
                policyVersion = AEDV02PilotPolicy.Version, catalogVersion = AEDV02Plan.CatalogVersion,
                resolutionMode = "Adaptive", difficulty = "Normal", targetPhaseName = targetPhase
            };
        }

        private async Task<AEDV02DecisionResultDto> RequestV02ApprovalAsync(Guid match, AEDV02DecisionRequestDto request)
        {
            try
            {
                var pending = new AEDV02ApiService(AuthRuntime.EnsureExists().Client).Submit(match, request);
                if (await Task.WhenAny(pending, Task.Delay(TimeSpan.FromSeconds(AEDV02PilotPolicy.EvidenceMaxAgeSeconds))) != pending)
                { V02Log("DECISION_HOLD", "HOLD_BACKEND_TIMEOUT"); QueueV02Abort(match, request); return null; }
                var response = await pending;
                if (response == null || !response.IsSuccess || response.Data?.success != true || response.Data.data == null)
                { V02Log("DECISION_HOLD", response?.Data?.message ?? response?.Message ?? "HOLD_BACKEND_UNAVAILABLE"); QueueV02Abort(match, request); return null; }
                var approval = response.Data.data;
                V02Log("DECISION", approval.reasonCode, approval);
                return approval;
            }
            catch (Exception ex) { V02Log("DECISION_HOLD", "HOLD_BACKEND_REQUEST_FAILED:" + ex.GetType().Name); QueueV02Abort(match, request); return null; }
        }

        private bool TryProposeV02(out AEDV02Plan previous, out AEDV02Plan next, out int revision, out AEDV02SourceResultDto source)
        {
            source = LastVerifiedV02Source; next = null;
            if (!AEDv2Authority.TryGetV02(MatchId, out previous, out revision) || source?.players == null ||
                source.planRevision != revision || source.planFingerprint != previous.Fingerprint() || source.rosterIdentity != AEDv2RosterIdentity ||
                !DateTime.TryParse(source.sourceEndedAtUtc, out var ended) || DateTime.UtcNow - ended.ToUniversalTime() > TimeSpan.FromSeconds(AEDV02PilotPolicy.EvidenceMaxAgeSeconds)) return false;
            return previous.TryProposeRelief(source.players.Select(x => x.components).ToArray(), source.players.Select(x => x.score).ToArray(),
                source.recentDownUsers?.Length > 0, source.zone, out next);
        }

        private void TickV02Coordinator(NetworkMatchState state)
        {
            if (!HasStateAuthority || state == null) return;
            RetryV02Aborts();
            if (state.IsEnded) { if (_v02Receipt != null) TickV02Receipt(state); return; }
            if (_v02PendingApproval != null) { TickV02Receipt(state); return; }
            if (!V02GameplayActive || _v02DecisionBusy || BackendAdaptiveInputSnapshotProvider.Current == null ||
                !state.IsV02SafeWindow() || !TryProposeV02(out var previous, out var next, out var revision, out var source) ||
                source.fingerprint == _v02EvaluatedSource) return;
            _v02EvaluatedSource = source.fingerprint;
            var request = BuildV02Request(previous, next, revision, AEDV02DecisionPoint.SAFE_WINDOW, ++_v02WindowSequence,
                source.phaseName, source, source.sceneManifestFingerprint);
            ApplyV02DecisionAsync(state, request, null);
        }

        public bool BeginV02Boundary(NetworkMatchState state, string targetPhase, Action continueTransition)
        {
            if (!V02GameplayActive || BackendAdaptiveInputSnapshotProvider.Current == null) return false;
            if (_v02DecisionBusy || _v02PendingApproval != null || !state.IsV02SafeWindow() ||
                !TryProposeV02(out var previous, out var next, out var revision, out var source)) return false;
            var request = BuildV02Request(previous, next, revision, AEDV02DecisionPoint.PHASE_BOUNDARY, 0,
                targetPhase, source, source.sceneManifestFingerprint);
            ApplyV02DecisionAsync(state, request, continueTransition); return true;
        }

        private async void ApplyV02DecisionAsync(NetworkMatchState state, AEDV02DecisionRequestDto request, Action continueTransition)
        {
            _v02DecisionBusy = true; var match = MatchId; int generation = _v02CoordinatorGeneration;
            try
            {
                var approval = await RequestV02ApprovalAsync(match, request);
                AEDv2Authority.TryGetV02(match, out var previous, out _);
                if (generation != _v02CoordinatorGeneration || MatchId != match || !V02GameplayActive || state == null ||
                    !state.TryCommitV02Authoritative(request, approval, out var reason))
                { QueueV02Abort(match, request); return; }
                AwaitV02Consumer(request, approval, previous ?? AEDV02Plan.Normal());
            }
            finally
            {
                if (generation == _v02CoordinatorGeneration)
                { _v02DecisionBusy = false; continueTransition?.Invoke(); }
            }
        }

        private void AwaitV02Consumer(AEDV02DecisionRequestDto request, AEDV02DecisionResultDto approval, AEDV02Plan previous)
        {
            _v02PendingRequest = request; _v02PendingApproval = approval; _v02Receipt = null; _v02NextReceiptAt = 0;
            var plan = new AEDV02Plan(approval.planValues);
            _v02PendingChangedKeys = Enum.GetValues(typeof(AEDV02Key)).Cast<AEDV02Key>().Where(x => plan.Get(x) != previous.Get(x)).ToArray();
        }

        private void TickV02Receipt(NetworkMatchState state)
        {
            if (_v02ReceiptBusy || Time.unscaledTime < _v02NextReceiptAt || !HasStateAuthority || _v02Receipt == null && !_telemetryMatchActive ||
                _bootstrap?.Runner == null || state.AEDV02PlanRevision != _v02PendingApproval.planRevision) return;
            if (_v02Receipt == null)
            {
                var plan = new AEDV02Plan(_v02PendingApproval.planValues);
                var changed = _v02PendingChangedKeys;
                string consumer = "AEDPolicyEnvelope"; var key = AEDV02Key.CabinetAmbushEnabled; double value = plan.Get(key);
                var ids = Array.Empty<string>();
                if (changed.Length == 1 && (int)changed[0] < 6)
                {
                    key = changed[0]; consumer = "StalkerFusionRuntime";
                    var stalkers = FindObjectsByType<StalkerFusionRuntime>(FindObjectsSortMode.None).Where(x => x.Runner == _bootstrap.Runner).ToArray();
                    if (stalkers.Length == 0 || stalkers.Any(x => x.V02AppliedRevision != _v02PendingApproval.planRevision ||
                        !x.TryReadV02ConsumerValue(key, out var observed) || Math.Abs(observed - plan.Get(key)) > .000001)) return;
                    value = plan.Get(key); ids = stalkers.Select(x => x.Object.Id.ToString()).ToArray();
                }
                else if (changed.Length == 1 && changed[0] is AEDV02Key.Zone1MinionCap or AEDV02Key.Zone2MinionCap or AEDV02Key.Zone3AdaptiveMinionCap)
                {
                    key = changed[0]; consumer = "PlayerSpawner";
                    var spawner = FindAnyObjectByType<PlayerSpawner>();
                    if (spawner == null || !spawner.TryReadV02MinionCap(MatchId, _v02PendingApproval.planRevision, key, out value, out ids) ||
                        Math.Abs(value - plan.Get(key)) > .000001) return;
                }
                else if (changed.Length != 0) return;
                var tick = _bootstrap.Runner.Tick.Raw;
                var effect = new AEDV02EffectSource { DecisionId = Guid.Parse(_v02PendingApproval.decisionId), PlanRevision = _v02PendingApproval.planRevision,
                    PlanFingerprint = plan.Fingerprint(), Consumer = consumer, Key = key.ToString(), ObservedValue = value, ObservedTick = tick, NetworkObjectIds = ids };
                if (!_telemetry.Phase8LifecycleAdapter.EmitAEDV02Effect("v02:effect:" + effect.DecisionId, DateTime.UtcNow, _currentTelemetryPhase,
                    AEDV02SourceJson.Effect(effect), out var emitted, out _)) return;
                _v02Receipt = new AEDV02ReceiptDto { receiptId = Guid.NewGuid().ToString("D"), effectEventId = emitted.Id.ToString("D"),
                    planRevision = effect.PlanRevision, planFingerprint = effect.PlanFingerprint, appliedTick = tick };
                _v02NextWorldTick = 0; _telemetry.TryFlushNow(); _v02NextReceiptAt = Time.unscaledTime + 2; return;
            }
            _v02NextReceiptAt = Time.unscaledTime + 2;
            ConfirmV02ReceiptAsync(MatchId, _v02PendingApproval, _v02Receipt, _v02CoordinatorGeneration);
        }
        private AEDV02Key[] _v02PendingChangedKeys = Array.Empty<AEDV02Key>();

        private async void ConfirmV02ReceiptAsync(Guid match, AEDV02DecisionResultDto approval, AEDV02ReceiptDto receipt, int generation)
        {
            _v02ReceiptBusy = true;
            try
            {
                var response = await new AEDV02ApiService(AuthRuntime.EnsureExists().Client).Applied(match, Guid.Parse(approval.decisionId), receipt);
                if (generation != _v02CoordinatorGeneration || MatchId != match) return;
                if (response?.IsSuccess == true && response.Data?.success == true && response.Data.data?.applyStatus == "APPLIED")
                { V02Log("APPLY_RECEIPT", response.Data.data.reasonCode, response.Data.data); _v02PendingRequest = null; _v02PendingApproval = null; _v02Receipt = null; }
                else V02Log("RECEIPT_PENDING", response?.Data?.message ?? response?.Message ?? "HOLD_BACKEND_UNAVAILABLE", approval);
            }
            catch (Exception ex) { if (generation == _v02CoordinatorGeneration) V02Log("RECEIPT_PENDING", ex.GetType().Name, approval); }
            finally { if (generation == _v02CoordinatorGeneration) _v02ReceiptBusy = false; }
        }

        private void QueueV02Abort(Guid match, AEDV02DecisionRequestDto request)
        { if (match == MatchId && request != null && Guid.TryParse(request.decisionId, out var id)) _v02Aborts.Add(id); }
        private bool _v02AbortBusy;
        private float _v02NextAbortAt;
        private async void RetryV02Aborts()
        {
            if (_v02AbortBusy || _v02Aborts.Count == 0 || Time.unscaledTime < _v02NextAbortAt) return;
            _v02AbortBusy = true; _v02NextAbortAt = Time.unscaledTime + 2;
            var match = MatchId; int generation = _v02CoordinatorGeneration; var id = _v02Aborts.First();
            try
            {
                var response = await new AEDV02ApiService(AuthRuntime.EnsureExists().Client).Abort(match, id);
                if (generation == _v02CoordinatorGeneration && response?.IsSuccess == true && response.Data?.success == true) _v02Aborts.Remove(id);
            }
            catch (Exception ex) { if (generation == _v02CoordinatorGeneration) V02Log("ABORT_PENDING", ex.GetType().Name); }
            finally { if (generation == _v02CoordinatorGeneration) _v02AbortBusy = false; }
        }
        private void ResetV02Coordinator()
        {
            _v02CoordinatorGeneration++; _v02StagedRequest = null; _v02StagedApproval = null; _v02PendingRequest = null;
            _v02PendingApproval = null; _v02Receipt = null; _v02DecisionBusy = false; _v02ReceiptBusy = false;
            _v02EvaluatedSource = null; _v02WindowSequence = 0; _v02Aborts.Clear(); _v02AbortBusy = false;
            _v02NextAbortAt = 0; _v02NextReceiptAt = 0; _v02PendingChangedKeys = Array.Empty<AEDV02Key>();
        }
        private void V02Log(string stage, string reason, AEDV02DecisionResultDto decision = null) =>
            Debug.Log($"[AED_V02][{stage}] match={MatchId:D} decision={decision?.decisionId ?? "none"} revision={decision?.planRevision ?? 0} fingerprint={decision?.planFingerprint ?? "none"} reason={reason}");
    }
}
