using System;
using System.Threading.Tasks;
using EchoProtocol.Api;

namespace EchoProtocol.AI.AED
{
    [Serializable] public sealed class AEDV02SourceRequestDto
    { public int phaseOrdinal; public long startTick; public long endTick; public string sceneManifestFingerprint; }
    [Serializable] public sealed class AEDV02PlayerPressureDto
    { public string userId; public double score; public double[] components; public int tier; public string episodeId; }
    [Serializable] public sealed class AEDV02SourceResultDto
    {
        public string fingerprint; public int phaseOrdinal; public long startTick; public long endTick;
        public string rosterIdentity; public string sceneManifestFingerprint; public AEDV02PlayerPressureDto[] players;
        public string[] recentDownUsers; public bool safeWindow; public int logicalPhase; public int zone;
        public string phaseName; public int planRevision; public string planFingerprint; public string sourceEndedAtUtc;
    }
    [Serializable] public sealed class AEDV02DecisionRequestDto
    {
        public string decisionId; public int phaseOrdinal; public string decisionPoint; public int windowSequence;
        public string windowId; public int expectedPlanRevision; public string previousPlanFingerprint; public double[] planValues;
        public string snapshotId; public string snapshotFingerprint; public string rosterIdentity; public string sourceFingerprint;
        public long startTick; public long endTick; public string sceneManifestFingerprint; public string policyVersion;
        public string catalogVersion; public string resolutionMode; public string difficulty; public string targetPhaseName;
    }
    [Serializable] public sealed class AEDV02DecisionResultDto
    {
        public string decisionId; public string matchId; public int phaseOrdinal; public string decisionPoint;
        public int windowSequence; public string windowId; public int planRevision; public int expectedPlanRevision;
        public string previousPlanFingerprint; public string planFingerprint; public double[] planValues;
        public string rosterIdentity; public string sourceFingerprint; public string sceneManifestFingerprint;
        public string policyVersion; public string catalogVersion; public string approvalStatus; public string applyStatus;
        public string reasonCode; public string expiresAtUtc; public string targetPhaseName; public string snapshotId; public string snapshotFingerprint; public bool isReplay;
    }
    [Serializable] public sealed class AEDV02ReceiptDto
    { public string receiptId; public string effectEventId; public int planRevision; public string planFingerprint; public long appliedTick; }

    public sealed class AEDV02ApiService
    {
        private readonly ApiClient _client;
        public AEDV02ApiService(ApiClient client) { _client = client ?? throw new ArgumentNullException(nameof(client)); }
        public Task<ApiResult<ApiResponse<AEDV02SourceResultDto>>> Sources(Guid matchId, AEDV02SourceRequestDto request) =>
            Post<AEDV02SourceRequestDto, AEDV02SourceResultDto>($"/api/matches/{matchId:D}/aed-v02/sources", request);
        public Task<ApiResult<ApiResponse<AEDV02DecisionResultDto>>> Submit(Guid matchId, AEDV02DecisionRequestDto request) =>
            Post<AEDV02DecisionRequestDto, AEDV02DecisionResultDto>($"/api/matches/{matchId:D}/aed-v02/decisions", request);
        public Task<ApiResult<ApiResponse<AEDV02DecisionResultDto>>> Applied(Guid matchId, Guid decision, AEDV02ReceiptDto request) =>
            Put<AEDV02ReceiptDto, AEDV02DecisionResultDto>($"/api/matches/{matchId:D}/aed-v02/decisions/{decision:D}/applied", request);
        public Task<ApiResult<ApiResponse<AEDV02DecisionResultDto>>> Abort(Guid matchId, Guid decision) =>
            Put<AEDV02ReceiptDto, AEDV02DecisionResultDto>($"/api/matches/{matchId:D}/aed-v02/decisions/{decision:D}/aborted", new AEDV02ReceiptDto());
        private Task<ApiResult<ApiResponse<T>>> Post<R, T>(string endpoint, R request)
        {
            var completion = new TaskCompletionSource<ApiResult<ApiResponse<T>>>();
            _client.PostJson<R, ApiResponse<T>>(endpoint, request, true, result => completion.TrySetResult(result)); return completion.Task;
        }
        private Task<ApiResult<ApiResponse<T>>> Put<R, T>(string endpoint, R request)
        {
            var completion = new TaskCompletionSource<ApiResult<ApiResponse<T>>>();
            _client.PutJson<R, ApiResponse<T>>(endpoint, request, true, result => completion.TrySetResult(result)); return completion.Task;
        }
    }
}
