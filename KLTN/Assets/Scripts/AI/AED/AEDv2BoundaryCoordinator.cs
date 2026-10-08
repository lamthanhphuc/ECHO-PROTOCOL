using System;
using System.Threading;
using System.Threading.Tasks;
using EchoProtocol.AI.Common.AED;

namespace EchoProtocol.AI.AED
{
    public enum AEDv2BoundaryState
    {
        Idle,
        PendingBackend,
        Approved,
        Applied,
        Hold,
        AwaitingReceipt
    }

    public sealed class AEDv2BoundaryTransaction
    {
        public Guid MatchId { get; }
        public Guid DecisionId { get; }
        public string ExpectedPhase { get; }
        public string NextPhase { get; }
        public uint PhaseOrdinal { get; }
        public string RosterIdentity { get; }
        public string EvidenceFingerprint { get; }
        public string PreviousPlanFingerprint { get; }
        public AEDv2Plan Plan { get; }
        public AEDv2Key ChangedKey { get; }
        public AEDPlanV2Request Request { get; }

        public AEDv2BoundaryTransaction(Guid matchId, Guid decisionId,
            string expectedPhase, string nextPhase, uint phaseOrdinal,
            string rosterIdentity, string evidenceFingerprint,
            string previousPlanFingerprint, AEDv2Plan plan,
            AEDv2Key changedKey, AEDPlanV2Request request)
        {
            MatchId = matchId;
            DecisionId = decisionId;
            ExpectedPhase = expectedPhase;
            NextPhase = nextPhase;
            PhaseOrdinal = phaseOrdinal;
            RosterIdentity = rosterIdentity;
            EvidenceFingerprint = evidenceFingerprint;
            PreviousPlanFingerprint = previousPlanFingerprint;
            Plan = plan;
            ChangedKey = changedKey;
            Request = request;
        }
    }

    public sealed class AEDv2BoundaryCoordinator : IDisposable
    {
        private CancellationTokenSource _lifetime = new CancellationTokenSource();
        private Task _operation;

        public AEDv2BoundaryState State { get; private set; }
        public AEDv2BoundaryTransaction Transaction { get; private set; }
        public string HoldReason { get; private set; } = string.Empty;

        public bool TryBegin(AEDv2BoundaryTransaction transaction,
            Func<CancellationToken, Task<AEDPlanV2Data>> submit,
            Func<AEDPlanV2Data, bool> revalidate,
            Func<AEDPlanV2Data, bool> apply,
            Action completeTransition,
            Func<CancellationToken, Task<bool>> confirmReceipt,
            Func<bool> submitRetryable = null,
            Action completeHold = null)
        {
            if (transaction == null || submit == null || revalidate == null
                || apply == null || completeTransition == null || confirmReceipt == null)
                return false;
            if (_operation != null && !_operation.IsCompleted) return false;
            if (State == AEDv2BoundaryState.AwaitingReceipt) return false;
            if (Transaction != null && Transaction.DecisionId != transaction.DecisionId)
                return false;

            Transaction = transaction;
            HoldReason = string.Empty;
            AEDv2E2ELog.State("BOUNDARY_BEGIN", decisionId: transaction.DecisionId);
            _operation = RunAsync(submit, revalidate, apply,
                completeTransition, confirmReceipt, submitRetryable,
                completeHold, _lifetime.Token);
            return true;
        }

        public bool IsTransaction(Guid matchId, string expectedPhase,
            string nextPhase, uint phaseOrdinal) =>
            Transaction != null && Transaction.MatchId == matchId
            && Transaction.ExpectedPhase == expectedPhase
            && Transaction.NextPhase == nextPhase
            && Transaction.PhaseOrdinal == phaseOrdinal;

        public void Hold(string reason)
        {
            HoldReason = string.IsNullOrWhiteSpace(reason)
                ? "AED_V2_BOUNDARY_HOLD" : reason;
            State = AEDv2BoundaryState.Hold;
            AEDv2E2ELog.State("BOUNDARY_HOLD", fields: $"reason={HoldReason}",
                decisionId: Transaction != null ? Transaction.DecisionId : Guid.Empty);
        }

        public void Reset()
        {
            _lifetime.Cancel();
            _lifetime.Dispose();
            _lifetime = new CancellationTokenSource();
            _operation = null;
            Transaction = null;
            HoldReason = string.Empty;
            State = AEDv2BoundaryState.Idle;
        }

        public void Dispose()
        {
            _lifetime.Cancel();
            _lifetime.Dispose();
        }

        private async Task RunAsync(Func<CancellationToken, Task<AEDPlanV2Data>> submit,
            Func<AEDPlanV2Data, bool> revalidate,
            Func<AEDPlanV2Data, bool> apply,
            Action completeTransition,
            Func<CancellationToken, Task<bool>> confirmReceipt,
            Func<bool> submitRetryable,
            Action completeHold,
            CancellationToken cancellationToken)
        {
            try
            {
                await RunCoreAsync(submit, revalidate, apply,
                    completeTransition, confirmReceipt, submitRetryable,
                    completeHold, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
        }

        private async Task RunCoreAsync(Func<CancellationToken, Task<AEDPlanV2Data>> submit,
            Func<AEDPlanV2Data, bool> revalidate,
            Func<AEDPlanV2Data, bool> apply,
            Action completeTransition,
            Func<CancellationToken, Task<bool>> confirmReceipt,
            Func<bool> submitRetryable,
            Action completeHold,
            CancellationToken cancellationToken)
        {
            State = AEDv2BoundaryState.PendingBackend;
            AEDPlanV2Data approval = null;
            while (!cancellationToken.IsCancellationRequested && approval == null)
            {
                approval = await submit(cancellationToken);
                if (approval == null)
                {
                    if (submitRetryable != null && !submitRetryable())
                    {
                        Hold("AED_V2_BOUNDARY_BACKEND_REJECTED");
                        completeHold?.Invoke();
                        return;
                    }
                    AEDv2E2ELog.State("POST_RETRY", decisionId: Transaction.DecisionId);
                    await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                }
            }
            if (approval == null || cancellationToken.IsCancellationRequested) return;
            AEDv2E2ELog.State("POST_APPROVED", decisionId: Transaction.DecisionId);
            if (!revalidate(approval))
            {
                AEDv2E2ELog.State("REVALIDATE_FAIL", decisionId: Transaction.DecisionId);
                Hold("AED_V2_BOUNDARY_STALE");
                completeHold?.Invoke();
                return;
            }
            AEDv2E2ELog.State("REVALIDATE_PASS", decisionId: Transaction.DecisionId);
            State = AEDv2BoundaryState.Approved;
            if (!apply(approval))
            {
                Hold("AED_V2_BOUNDARY_COMMIT_REJECTED");
                completeHold?.Invoke();
                return;
            }
            State = AEDv2BoundaryState.Applied;
            completeTransition();
            State = AEDv2BoundaryState.AwaitingReceipt;
            AEDv2E2ELog.State("RECEIPT_WAIT", decisionId: Transaction.DecisionId);
            while (!cancellationToken.IsCancellationRequested)
            {
                if (await confirmReceipt(cancellationToken))
                {
                    State = AEDv2BoundaryState.Idle;
                    Transaction = null;
                    return;
                }
                AEDv2E2ELog.State("RECEIPT_RETRY", decisionId: Transaction.DecisionId);
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
        }
    }
}
