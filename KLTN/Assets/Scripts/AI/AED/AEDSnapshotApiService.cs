using System;
using System.Threading;
using System.Threading.Tasks;
using EchoProtocol.AI.Common.AED;
using EchoProtocol.AI.Common.Profile;
using EchoProtocol.Api;
using EchoProtocol.Auth;
using UnityEngine;

namespace EchoProtocol.AI.AED
{
    public sealed class AEDSnapshotFetchResult
    {
        public AdaptiveInputSnapshot Snapshot { get; }
        public AdaptiveInputCurrencyValidation Currency { get; }
        public string Reason { get; }
        public bool Succeeded => Snapshot != null && Currency?.IsCurrent == true;

        public AEDSnapshotFetchResult(AdaptiveInputSnapshot snapshot,
            AdaptiveInputCurrencyValidation currency, string reason)
        {
            Snapshot = snapshot;
            Currency = currency;
            Reason = reason ?? string.Empty;
        }
    }

    public sealed class AEDSnapshotApiService
    {
        public bool LastPlanSubmitRetryable { get; private set; }

        public async Task<AEDPlanV2Data> SubmitPlanAsync(Guid matchId,
            AEDPlanV2Request request, CancellationToken cancellationToken)
        {
            Guid.TryParse(request.decisionId, out var traceDecisionId);
            AEDv2E2ELog.Write(
                "POST_BEGIN",
                matchId,
                traceDecisionId,
                fields: $"point={request.decisionPoint} " +
                        $"ordinal={request.phaseOrdinal} " +
                        $"commit={request.commitStatus}");
            var completion = new TaskCompletionSource<ApiResult<AEDPlanV2Response>>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            using (cancellationToken.Register(() => completion.TrySetCanceled()))
            {
                AuthRuntime.EnsureExists().Client.PostJson<AEDPlanV2Request, AEDPlanV2Response>(
                    $"/api/matches/{matchId:D}/scenario/plans-v2", request, true,
                    result => completion.TrySetResult(result));
                try
                {
                    var result = await completion.Task;
                    var data = result.Data?.data;
                    AEDv2E2ELog.Write(
                        "POST_RESULT",
                        matchId,
                        traceDecisionId,
                        fields: $"success={result.IsSuccess} " +
                                $"http={result.StatusCode} " +
                                $"failure={result.FailureKind} " +
                                $"commit={data?.commitStatus ?? "none"} " +
                                $"apply={data?.applyStatus ?? "none"}");
                    var valid = result.IsSuccess && result.Data?.success == true && data != null
                        && data.matchId == matchId.ToString("D")
                        && data.decisionId == request.decisionId
                        && data.phaseOrdinal == request.phaseOrdinal
                        && data.decisionPoint == request.decisionPoint
                        && data.previousPlanFingerprint == request.previousPlanFingerprint
                        && data.resultingPlanFingerprint == request.resultingPlanFingerprint
                        && data.changedKey == request.changedKey
                        && data.previousValue == request.previousValue
                        && data.appliedValue == request.appliedValue
                        && data.snapshotId == request.snapshotId
                        && data.snapshotFingerprint == request.snapshotFingerprint
                        && data.evidenceFingerprint == request.evidenceFingerprint
                        && data.rosterIdentity == request.rosterIdentity
                        && data.commitStatus == request.commitStatus
                        && (request.commitStatus != "COMMITTED"
                            || data.applyStatus == "PENDING"
                            || data.applyStatus == "APPLIED");
                    LastPlanSubmitRetryable = !valid &&
                        (result.FailureKind == ApiFailureKind.Network
                         || result.FailureKind == ApiFailureKind.Timeout
                         || result.StatusCode == 0 || result.StatusCode >= 500);
                    return valid ? data : null;
                }
                catch (TaskCanceledException)
                {
                    AEDv2E2ELog.Write("POST_CANCELED", matchId, traceDecisionId);
                    LastPlanSubmitRetryable = false;
                    return null;
                }
            }
        }

        public async Task<bool> ConfirmPlanAppliedAsync(Guid matchId, Guid decisionId,
            string fingerprint, CancellationToken cancellationToken)
        {
            AEDv2E2ELog.Write(
                "RECEIPT_BEGIN",
                matchId,
                decisionId,
                fields: $"fingerprint={fingerprint}");
            var completion = new TaskCompletionSource<ApiResult<AEDPlanV2Response>>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            using (cancellationToken.Register(() => completion.TrySetCanceled()))
            {
                AuthRuntime.EnsureExists().Client.PutJson<AEDPlanV2AppliedRequest,
                    AEDPlanV2Response>($"/api/matches/{matchId:D}/scenario/plans-v2/{decisionId:D}/applied",
                    new AEDPlanV2AppliedRequest { planFingerprint = fingerprint }, true,
                    result => completion.TrySetResult(result));
                try
                {
                    var result = await completion.Task;
                    AEDv2E2ELog.Write(
                        "RECEIPT_RESULT",
                        matchId,
                        decisionId,
                        fields: $"success={result.IsSuccess} " +
                                $"http={result.StatusCode} " +
                                $"apply={result.Data?.data?.applyStatus ?? "none"}");
                    return result.IsSuccess && result.Data?.success == true
                        && result.Data.data?.decisionId == decisionId.ToString("D")
                        && result.Data.data.resultingPlanFingerprint == fingerprint
                        && result.Data.data.applyStatus == "APPLIED";
                }
                catch (TaskCanceledException)
                {
                    AEDv2E2ELog.Write("RECEIPT_CANCELED", matchId, decisionId);
                    return false;
                }
            }
        }

        public async Task<bool> AbortPlanAsync(Guid matchId, Guid decisionId,
            CancellationToken cancellationToken)
        {
            var completion = new TaskCompletionSource<ApiResult<AEDPlanV2Response>>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            using (cancellationToken.Register(() => completion.TrySetCanceled()))
            {
                AuthRuntime.EnsureExists().Client.PutJson<AEDPlanV2AppliedRequest,
                    AEDPlanV2Response>($"/api/matches/{matchId:D}/scenario/plans-v2/{decisionId:D}/aborted",
                    new AEDPlanV2AppliedRequest(), true,
                    result => completion.TrySetResult(result));
                try
                {
                    var result = await completion.Task;
                    var confirmed = result.IsSuccess && result.Data?.success == true
                        && result.Data.data?.decisionId == decisionId.ToString("D")
                        && result.Data.data.applyStatus == "ABORTED";
                    if (confirmed)
                        AEDv2E2ELog.State("ABORT_CONFIRMED", decisionId: decisionId);
                    return confirmed;
                }
                catch (TaskCanceledException) { return false; }
            }
        }

        public async Task<AEDResolvePreMatchData> ResolvePreMatchAsync(
            Guid matchId, Guid decisionId, string experimentCondition,
            CancellationToken cancellationToken)
        {
            var completion = new TaskCompletionSource<ApiResult<AEDResolvePreMatchResponse>>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            using (cancellationToken.Register(() => completion.TrySetCanceled()))
            {
                AuthRuntime.EnsureExists().Client.PostJson<AEDResolvePreMatchRequest,
                    AEDResolvePreMatchResponse>($"/api/matches/{matchId:D}/scenario/resolve",
                    new AEDResolvePreMatchRequest
                    {
                        decisionId = decisionId.ToString("D"),
                        resolutionMode = "Adaptive",
                        unityCompatibilityVersion = Application.version,
                        experimentCondition = experimentCondition ?? string.Empty
                    }, true, result => completion.TrySetResult(result));
                try
                {
                    var result = await completion.Task;
                    return result.IsSuccess && result.Data?.success == true
                        && Guid.TryParse(result.Data.data?.decisionId, out var returnedId)
                        && returnedId == decisionId ? result.Data.data : null;
                }
                catch (TaskCanceledException) { return null; }
            }
        }

        public async Task<AEDSnapshotFetchResult> GetAsync(Guid matchId, Guid decisionId,
            CancellationToken cancellationToken)
        {
            if (matchId == Guid.Empty || decisionId == Guid.Empty)
                return new AEDSnapshotFetchResult(null, null, "AED_SNAPSHOT_ID_MISSING");
            var completion = new TaskCompletionSource<ApiResult<AEDSnapshotApiResponse>>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            using (cancellationToken.Register(() => completion.TrySetCanceled()))
            {
                AuthRuntime.EnsureExists().Client.GetJson<AEDSnapshotApiResponse>(
                    $"/api/matches/{matchId:D}/scenario/decisions/{decisionId:D}/input-snapshot",
                    true, result => completion.TrySetResult(result));
                ApiResult<AEDSnapshotApiResponse> response;
                try { response = await completion.Task; }
                catch (TaskCanceledException)
                {
                    return new AEDSnapshotFetchResult(null, null, "AED_SNAPSHOT_CANCELLED");
                }
                Debug.LogWarning(
                    $"[AED_V2][SNAPSHOT_HTTP] " +
                    $"http={response.StatusCode} " +
                    $"success={response.IsSuccess} " +
                    $"apiSuccess={response.Data?.success} " +
                    $"errorCode={response.Data?.errorCode} " +
                    $"message={response.Data?.message}");
                if (!response.IsSuccess || response.Data?.success != true)
                    return new AEDSnapshotFetchResult(null, null, response.StatusCode switch
                    {
                        401 => "AED_SNAPSHOT_UNAUTHORIZED",
                        403 => "AED_SNAPSHOT_FORBIDDEN",
                        404 => "AED_SNAPSHOT_NOT_FOUND",
                        409 => "AED_SNAPSHOT_STALE",
                        410 => "AED_SNAPSHOT_LEASE_EXPIRED",
                        _ => response.FailureKind == ApiFailureKind.Timeout
                            ? "AED_SNAPSHOT_TIMEOUT" : "AED_SNAPSHOT_UNAVAILABLE"
                    });
                if (!AEDSnapshotMapper.TryMap(response.Data.data, out var snapshot,
                        out var currency, out var reason))
                    return new AEDSnapshotFetchResult(null, null, reason);
                var dto = response.Data.data;

                Debug.LogWarning(
                    $"[AED_V2][SNAPSHOT_CURRENCY] " +
                    $"matchOk={snapshot.TargetMatchId == matchId} " +
                    $"current={currency.IsCurrent} " +
                    $"roster={dto.rosterCurrent} " +
                    $"revisions={dto.profileRevisionsCurrent} " +
                    $"fingerprint={dto.snapshotFingerprintValid} " +
                    $"semantics={dto.profileSemanticsSupported} " +
                    $"targetMatch={dto.targetMatchCurrent} " +
                    $"decisionPoint={dto.decisionPointCurrent} " +
                    $"phaseContext={dto.phaseContextCurrent}");
                if (snapshot.TargetMatchId != matchId || !currency.IsCurrent)
                    return new AEDSnapshotFetchResult(null, null, "AED_SNAPSHOT_STALE");
                return new AEDSnapshotFetchResult(snapshot, currency, string.Empty);
            }
        }
    }
}
