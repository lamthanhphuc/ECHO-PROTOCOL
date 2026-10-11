using System;
using System.Threading;
using System.Threading.Tasks;
using EchoProtocol.Api;

namespace EchoProtocol.AI.AED
{
    public sealed class AEDPhase8ApprovalApiService
    {
        [Serializable] private sealed class EmptyRequest { }

        public async Task<AEDPhase8ApprovalDataV1> SubmitResearchDecisionAsync(Guid matchId, AEDPhase8ApprovalRequestV1 request, CancellationToken cancellationToken)
        {
            if (matchId == Guid.Empty || !ValidRequest(request)) return null;
            var completion = new TaskCompletionSource<ApiResult<AEDPhase8ApprovalResponseV1>>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (cancellationToken.Register(() => completion.TrySetCanceled()))
            {
                AuthRuntime.EnsureExists().Client.PostJson<AEDPhase8ApprovalRequestV1, AEDPhase8ApprovalResponseV1>($"/api/matches/{matchId:D}/scenario/director-decisions-v1", request, true, result => completion.TrySetResult(result));
                try
                {
                    var result = await completion.Task;
                    return result.IsSuccess && result.Data?.success == true && Matches(result.Data.data, matchId, request) ? result.Data.data : null;
                }
                catch (TaskCanceledException) { return null; }
            }
        }

        public async Task<AEDPhase8ApprovalDataV1> GetDecisionAsync(Guid matchId, AEDPhase8ApprovalRequestV1 expected, CancellationToken cancellationToken)
        {
            if (matchId == Guid.Empty || !ValidRequest(expected)) return null;
            var completion = new TaskCompletionSource<ApiResult<AEDPhase8ApprovalResponseV1>>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (cancellationToken.Register(() => completion.TrySetCanceled()))
            {
                AuthRuntime.EnsureExists().Client.GetJson<AEDPhase8ApprovalResponseV1>($"/api/matches/{matchId:D}/scenario/director-decisions-v1/{expected.decisionId}", true, result => completion.TrySetResult(result));
                try
                {
                    var result = await completion.Task;
                    return result.IsSuccess && result.Data?.success == true && Matches(result.Data.data, matchId, expected) ? result.Data.data : null;
                }
                catch (TaskCanceledException) { return null; }
            }
        }

        public async Task<AEDPhase8ApprovalDataV1> AbortPendingAsync(Guid matchId, AEDPhase8ApprovalRequestV1 expected, CancellationToken cancellationToken)
        {
            if (matchId == Guid.Empty || !ValidRequest(expected)) return null;
            var completion = new TaskCompletionSource<ApiResult<AEDPhase8ApprovalResponseV1>>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (cancellationToken.Register(() => completion.TrySetCanceled()))
            {
                AuthRuntime.EnsureExists().Client.PutJson<EmptyRequest, AEDPhase8ApprovalResponseV1>($"/api/matches/{matchId:D}/scenario/director-decisions-v1/{expected.decisionId}/aborted", new EmptyRequest(), true, result => completion.TrySetResult(result));
                try
                {
                    var result = await completion.Task;
                    var data = result.Data?.data;
                    return result.IsSuccess && result.Data?.success == true && Matches(data, matchId, expected) && data.status == "ABORTED" ? data : null;
                }
                catch (TaskCanceledException) { return null; }
            }
        }

        private static bool ValidRequest(AEDPhase8ApprovalRequestV1 request) => request != null && Guid.TryParse(request.decisionId, out var id) && id != Guid.Empty && request.completedPhaseOrdinal > 0 && request.expectedRevision >= 0 && !string.IsNullOrWhiteSpace(request.directorKind) && !string.IsNullOrWhiteSpace(request.policyVersion) && !string.IsNullOrWhiteSpace(request.payloadJson);

        private static bool Matches(AEDPhase8ApprovalDataV1 data, Guid matchId, AEDPhase8ApprovalRequestV1 request) => data != null && string.Equals(data.matchId, matchId.ToString("D"), StringComparison.OrdinalIgnoreCase) && string.Equals(data.decisionId, request.decisionId, StringComparison.OrdinalIgnoreCase) && data.completedPhaseOrdinal == request.completedPhaseOrdinal && data.directorKind == request.directorKind && data.policyVersion == request.policyVersion && data.rosterIdentity == request.rosterIdentity && data.snapshotFingerprint == request.snapshotFingerprint && data.evidenceFingerprint == request.evidenceFingerprint && data.sourceFingerprint == request.sourceFingerprint && data.payloadFingerprint == request.payloadFingerprint && data.expectedRevision == request.expectedRevision && data.approvalScope == "SHADOW" && data.status is "APPROVED_RESEARCH" or "ABORTED" && !data.gameplayApproved;
    }
}
