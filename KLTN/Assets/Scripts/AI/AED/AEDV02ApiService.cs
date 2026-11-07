using System;
using System.Threading.Tasks;
using EchoProtocol.Api;

namespace EchoProtocol.AI.AED
{
    [Serializable] public sealed class AEDV02ConfigurationDto
    {
        [Serializable] public sealed class Parameter { public double[] tiers; }
        public string version;
        public double[] baseline;
        public Parameter[] parameters;
        public double observationSeconds, windowSeconds, evidenceMaxAgeSeconds, reliefThreshold, increasePressureCeiling;
        public double[] pressureWeights;
        public double tierDwellSeconds, protectionSeconds, protectionCooldownSeconds;
        public int protectionQuota;
    }
    [Serializable] public sealed class AEDV02SourceRequestDto
    { public int phaseOrdinal; public long startTick; public long endTick; public string sceneManifestFingerprint; public bool allowEmergencyWindow; }
    [Serializable] public sealed class AEDV02PlayerPressureDto
    { public string userId; public double score; public double[] components; public int tier; public string episodeId; }
    [Serializable] public sealed class AEDV02SourceResultDto
    {
        public string fingerprint; public int phaseOrdinal; public long startTick; public long endTick;
        public string rosterIdentity; public string sceneManifestFingerprint; public AEDV02PlayerPressureDto[] players;
        public string[] recentDownUsers; public bool safeWindow; public int logicalPhase; public int zone;
        public string phaseName; public int planRevision; public string planFingerprint; public string sourceEndedAtUtc;
        public bool pressureComplete; public string[] missingChannels;
        public string[] cabinetClueEventIds;
        public string teamPressureStatus;
        public string opportunityContractVersion; public int teamPressureTier = -1; public double teamPressureSeverity;
        public string[] increaseAxes;
        public AEDV02ResourceNeedDto[] resourceNeeds; public bool supportSafeWindow;
        public int ordinarySupportRemaining, emergencySupportRemaining; public string nextOrdinarySupportAtUtc;
    }
    [Serializable] public sealed class AEDV02ResourceNeedDto { public string userId, causeEventId; public int toolId; public bool downed; }
    [Serializable] public sealed class AEDV02ProposedReservationDto
    {
        public int zone, toolId; public string markerId;
        public EchoProtocol.AI.Common.AED.AEDV02ProposedReservation ToModel() => new EchoProtocol.AI.Common.AED.AEDV02ProposedReservation { Zone = zone, ToolId = toolId, MarkerId = markerId };
    }
    [Serializable] public sealed class AEDV02SupportActionDto
    {
        public string kind, userId, causeEventId, markerId; public int toolId, zone;
        public EchoProtocol.AI.Common.AED.AEDV02SupportAction ToModel() => new EchoProtocol.AI.Common.AED.AEDV02SupportAction { Kind = kind,
            UserId = Guid.TryParse(userId, out var user) ? user : Guid.Empty, CauseEventId = Guid.TryParse(causeEventId, out var cause) ? cause : Guid.Empty,
            ToolId = toolId, Zone = zone, MarkerId = markerId };
    }
    [Serializable] public sealed class AEDV02HistoricalProfileDto
    { public string contractVersion; public string comparisonContext; public string[] increaseAxes; }
    [Serializable] public sealed class AEDV02DecisionRequestDto
    {
        public string decisionId; public int phaseOrdinal; public string decisionPoint; public int windowSequence;
        public string windowId; public int expectedPlanRevision; public string previousPlanFingerprint; public double[] planValues;
        public string snapshotId; public string snapshotFingerprint; public string rosterIdentity; public string sourceFingerprint;
        public long startTick; public long endTick; public string sceneManifestFingerprint; public string policyVersion;
        public string catalogVersion; public string resolutionMode; public string difficulty; public string targetPhaseName;
        public AEDV02ProposedReservationDto[] proposedReservations; public AEDV02SupportActionDto support;
    }
    [Serializable] public sealed class AEDV02DecisionResultDto
    {
        public string decisionId; public string matchId; public int phaseOrdinal; public string decisionPoint;
        public int windowSequence; public string windowId; public int planRevision; public int expectedPlanRevision;
        public string previousPlanFingerprint; public string planFingerprint; public double[] planValues;
        public string rosterIdentity; public string sourceFingerprint; public string sceneManifestFingerprint;
        public string policyVersion; public string catalogVersion; public string approvalStatus; public string applyStatus;
        public string reasonCode; public string expiresAtUtc; public string targetPhaseName; public string snapshotId; public string snapshotFingerprint; public bool isReplay;
        public string actionFingerprint;
    }
    [Serializable] public sealed class AEDV02ReceiptDto
    { public string receiptId; public string effectEventId; public int planRevision; public string planFingerprint; public long appliedTick; }
    [Serializable] public sealed class AEDV02ClientObservationDto
    { public int planRevision; public string planFingerprint; }

    public sealed class AEDV02ApiService
    {
        private readonly ApiClient _client;
        public AEDV02ApiService(ApiClient client) { _client = client ?? throw new ArgumentNullException(nameof(client)); }
        public Task<ApiResult<ApiResponse<AEDV02ConfigurationDto>>> Configuration(Guid match)
        {
            var completion = new TaskCompletionSource<ApiResult<ApiResponse<AEDV02ConfigurationDto>>>();
            _client.GetJson<ApiResponse<AEDV02ConfigurationDto>>($"/api/matches/{match:D}/aed-v02/configuration",
                true, result => completion.TrySetResult(result));
            return completion.Task;
        }
        public Task<ApiResult<ApiResponse<AEDV02HistoricalProfileDto>>> Profile(Guid match, string sceneFingerprint)
        {
            var completion = new TaskCompletionSource<ApiResult<ApiResponse<AEDV02HistoricalProfileDto>>>();
            _client.GetJson<ApiResponse<AEDV02HistoricalProfileDto>>($"/api/matches/{match:D}/aed-v02/profile?sceneFingerprint={sceneFingerprint}",
                true, result => completion.TrySetResult(result));
            return completion.Task;
        }
        public Task<ApiResult<ApiResponse<AEDV02SourceResultDto>>> Sources(Guid matchId, AEDV02SourceRequestDto request) =>
            Post<AEDV02SourceRequestDto, AEDV02SourceResultDto>($"/api/matches/{matchId:D}/aed-v02/sources", request);
        public Task<ApiResult<ApiResponse<AEDV02DecisionResultDto>>> Submit(Guid matchId, AEDV02DecisionRequestDto request)
        {
            var completion = new TaskCompletionSource<ApiResult<ApiResponse<AEDV02DecisionResultDto>>>();
            _client.PostRawJson<ApiResponse<AEDV02DecisionResultDto>>($"/api/matches/{matchId:D}/aed-v02/decisions",
                AEDV02SourceJson.Decision(request), true, result => completion.TrySetResult(result));
            return completion.Task;
        }
        public Task<ApiResult<ApiResponse<AEDV02DecisionResultDto>>> Applied(Guid matchId, Guid decision, AEDV02ReceiptDto request) =>
            Put<AEDV02ReceiptDto, AEDV02DecisionResultDto>($"/api/matches/{matchId:D}/aed-v02/decisions/{decision:D}/applied", request);
        public Task<ApiResult<ApiResponse<bool>>> ClientObserved(Guid matchId, Guid decision, int revision, string fingerprint) =>
            Put<AEDV02ClientObservationDto, bool>($"/api/matches/{matchId:D}/aed-v02/decisions/{decision:D}/client-observed",
                new AEDV02ClientObservationDto { planRevision = revision, planFingerprint = fingerprint });
        public Task<ApiResult<ApiResponse<AEDV02DecisionResultDto>>> Abort(Guid matchId, Guid decision, bool mayHaveCommitted = false) =>
            Put<AEDV02ReceiptDto, AEDV02DecisionResultDto>($"/api/matches/{matchId:D}/aed-v02/decisions/{decision:D}/aborted?mayHaveCommitted={(mayHaveCommitted ? "true" : "false")}", new AEDV02ReceiptDto());
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
