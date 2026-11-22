using System;
using System.Threading.Tasks;
using EchoProtocol.Api;

namespace EchoProtocol.Networking.Authority
{
    public sealed class MatchAuthorityApiService
    {
        private readonly ApiClient _client;

        public MatchAuthorityApiService(ApiClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public Task<ApiResult<ApiResponse<MatchAuthorityDto>>> CreateAsync(string sessionName, int maxPlayers) =>
            PostAsync<CreateMatchAuthorityRequest, MatchAuthorityDto>(
                "/api/matches/authority",
                new CreateMatchAuthorityRequest
                {
                    fusionSessionName = sessionName,
                    maxPlayers = maxPlayers
                });

        public Task<ApiResult<ApiResponse<JoinProofDto>>> IssueJoinProofAsync(
            Guid matchId, string sessionName, int actorNumber) =>
            PostAsync<IssueJoinProofRequest, JoinProofDto>(
                $"/api/matches/{matchId:D}/join-proofs",
                new IssueJoinProofRequest
                {
                    fusionActorNumber = actorNumber,
                    fusionSessionName = sessionName
                });

        public Task<ApiResult<ApiResponse<MatchPlayerBindingDto>>> BindPlayerAsync(
            Guid matchId, int actorNumber, string proof) =>
            PostAsync<BindMatchPlayerRequest, MatchPlayerBindingDto>(
                $"/api/matches/{matchId:D}/players/bind",
                new BindMatchPlayerRequest
                {
                    fusionActorNumber = actorNumber,
                    joinProof = proof
                });

        public Task<ApiResult<ApiResponse<KickDecisionDto[]>>> GetKickDecisionsAsync(Guid matchId) =>
            GetAsync<KickDecisionDto[]>($"/api/matches/{matchId:D}/kick-decisions");

        public Task<ApiResult<ApiResponse<KickDecisionDto>>> AckKickAsync(Guid matchId, Guid actionId) =>
            PostAsync<EmptyMatchAuthorityRequest, KickDecisionDto>(
                $"/api/matches/{matchId:D}/kick-decisions/{actionId:D}/host-ack",
                new EmptyMatchAuthorityRequest());

        public Task<ApiResult<ApiResponse<RoomSessionDto>>> CreateRoomAsync(
            Guid requestId, string sessionName, string region) =>
            PostAsync<CreateRoomSessionRequest, RoomSessionDto>("/api/rooms",
                new CreateRoomSessionRequest
                {
                    requestId = requestId.ToString("D"),
                    fusionSessionName = sessionName,
                    region = region
                });

        public Task<ApiResult<ApiResponse<RoomSessionDto>>> ActivateRoomAsync(Guid roomId, string region) =>
            PostAsync<ActivateRoomRequest, RoomSessionDto>(
                $"/api/rooms/{roomId:D}/activate", new ActivateRoomRequest { actualRegion = region });

        public Task<ApiResult<ApiResponse<MatchAuthorityDto>>> PrepareRoomMatchAsync(
            Guid roomId, Guid requestId, int maxPlayers) =>
            PostAsync<PrepareRoomMatchRequest, MatchAuthorityDto>(
                $"/api/rooms/{roomId:D}/matches",
                new PrepareRoomMatchRequest { requestId = requestId.ToString("D"), maxPlayers = maxPlayers });

        public Task<ApiResult<ApiResponse<RoomSessionDto>>> ConfirmRoomPublicationAsync(
            Guid roomId, Guid matchId) =>
            PostAsync<EmptyMatchAuthorityRequest, RoomSessionDto>(
                $"/api/rooms/{roomId:D}/matches/{matchId:D}/publication-ack",
                new EmptyMatchAuthorityRequest());

        public Task<ApiResult<ApiResponse<RoomSessionDto>>> SyncRoomHostAsync(Guid roomId) =>
            PostAsync<EmptyMatchAuthorityRequest, RoomSessionDto>(
                $"/api/rooms/{roomId:D}/sync-host", new EmptyMatchAuthorityRequest());

        public Task<ApiResult<ApiResponse<RoomSessionDto>>> RenewRoomLeaseAsync(Guid roomId) =>
            PostAsync<EmptyMatchAuthorityRequest, RoomSessionDto>(
                $"/api/rooms/{roomId:D}/lease", new EmptyMatchAuthorityRequest());

        public Task<ApiResult<ApiResponse<RoomSessionDto>>> ReleaseRoomAsync(Guid roomId) =>
            PostAsync<EmptyMatchAuthorityRequest, RoomSessionDto>(
                $"/api/rooms/{roomId:D}/release", new EmptyMatchAuthorityRequest());

        public Task<ApiResult<ApiResponse<MatchPlayerEntitlementsDto>>> GetPlayerEntitlementsAsync(
            Guid matchId, int actorNumber) =>
            GetAsync<MatchPlayerEntitlementsDto>(
                $"/api/matches/{matchId:D}/players/{actorNumber}/entitlements");

        public Task<ApiResult<ApiResponse<MatchPlayerBindingDto>>> DisconnectPlayerAsync(
            Guid matchId, int actorNumber, Guid expectedUserId) =>
            PostAsync<DisconnectMatchPlayerRequest, MatchPlayerBindingDto>(
                $"/api/matches/{matchId:D}/players/{actorNumber}/disconnect",
                new DisconnectMatchPlayerRequest
                {
                    expectedUserId = expectedUserId.ToString("D")
                });

        public Task<ApiResult<ApiResponse<MatchAuthorityDto>>> TakeoverAsync(
            Guid matchId,
            string sessionName,
            int actorNumber) =>
            PostAsync<TakeoverMatchAuthorityRequest, MatchAuthorityDto>(
                $"/api/matches/{matchId:D}/authority/takeover",
                new TakeoverMatchAuthorityRequest
                {
                    fusionSessionName = sessionName,
                    fusionActorNumber = actorNumber
                });

        public Task<ApiResult<ApiResponse<MatchAuthorityDto>>> RelinquishAsync(Guid matchId) =>
            PostAsync<EmptyMatchAuthorityRequest, MatchAuthorityDto>(
                $"/api/matches/{matchId:D}/authority/relinquish",
                new EmptyMatchAuthorityRequest());

        public Task<ApiResult<ApiResponse<MatchAuthorityDto>>> RenewLeaseAsync(Guid matchId) =>
            PostAsync<EmptyMatchAuthorityRequest, MatchAuthorityDto>(
                $"/api/matches/{matchId:D}/lease", new EmptyMatchAuthorityRequest());

        public Task<ApiResult<ApiResponse<MatchAuthorityDto>>> StartAsync(
            Guid matchId, string mapId, string difficulty) =>
            PostAsync<StartMatchAuthorityRequest, MatchAuthorityDto>(
                $"/api/matches/{matchId:D}/start",
                new StartMatchAuthorityRequest
                {
                    mapId = mapId,
                    difficulty = difficulty
                });

        public Task<ApiResult<ApiResponse<MatchAuthorityDto>>> StartV2Async(
            Guid matchId, string mapId, string difficulty, bool adaptive = false, bool prepare = false) =>
            PostAsync<StartMatchV2Request, MatchAuthorityDto>(
                $"/api/matches/{matchId:D}/start-v2",
                new StartMatchV2Request
                {
                    mapId = mapId,
                    difficulty = difficulty,
                    resolutionMode = adaptive ? "ADAPTIVE" : "FIXED",
                    balanceSource = "AUTO",
                    prepareBalance = prepare,
                    supportsRelativeAdaptiveV02 = true,
                    balanceProtocolVersion = "RFB-1",
                    supportsGameplayTuning = true,
                    gameVersion = UnityEngine.Application.version
                });

        [Serializable]
        public sealed class AutomaticAdaptiveDto
        {
            public bool enabled;
        }

        public Task<ApiResult<ApiResponse<AutomaticAdaptiveDto>>> GetAutomaticAdaptiveAsync(Guid matchId) =>
            GetAsync<AutomaticAdaptiveDto>(
                $"/api/matches/{matchId:D}/scenario/automatic-adaptive?compatibility={Uri.EscapeDataString(UnityEngine.Application.version)}");

        public Task<ApiResult<ApiResponse<LockedBalanceDto>>> GetLockedBalanceAsync(
            Guid matchId) =>
            GetAsync<LockedBalanceDto>($"/api/matches/{matchId:D}/balance");

        public Task<ApiResult<ApiResponse<MatchAuthorityDto>>> EndAsync(Guid matchId, string reason) =>
            PostAsync<EndMatchAuthorityRequest, MatchAuthorityDto>(
                $"/api/matches/{matchId:D}/end",
                new EndMatchAuthorityRequest { reason = reason });

        public Task<ApiResult<ApiResponse<MatchResultResponseDto>>> SubmitResultAsync(
            Guid matchId,
            SubmitMatchResultRequestDto request)
        {
            if (request.evidenceVersion == 0 && !request.usedDebugCheats)
                return PutAsync<LegacySubmitMatchResultRequestDto, MatchResultResponseDto>(
                    $"/api/matches/{matchId:D}/result",
                    new LegacySubmitMatchResultRequestDto
                    {
                        outcome = request.outcome,
                        objectiveCompletion = request.objectiveCompletion,
                        players = Array.ConvertAll(request.players, player => new LegacySubmitMatchResultPlayerDto
                        {
                            userId = player.userId, survived = player.survived,
                            disconnected = player.disconnected, detectionCount = player.detectionCount,
                            downedCount = player.downedCount, reviveCount = player.reviveCount,
                            objectiveContribution = player.objectiveContribution
                        })
                    });
            if (!request.usedDebugCheats)
                return PutAsync<StandardSubmitMatchResultRequestDto, MatchResultResponseDto>(
                    $"/api/matches/{matchId:D}/result", new StandardSubmitMatchResultRequestDto
                    {
                        evidenceVersion = request.evidenceVersion, outcome = request.outcome,
                        objectiveCompletion = request.objectiveCompletion, players = request.players
                    });
            return PutAsync<SubmitMatchResultRequestDto, MatchResultResponseDto>(
                $"/api/matches/{matchId:D}/result", request);
        }

        public Task<ApiResult<ApiResponse<RewardMeResponseDto>>> GetMyRewardAsync(Guid matchId) =>
            GetAsync<RewardMeResponseDto>($"/api/matches/{matchId:D}/reward/me");

        private Task<ApiResult<ApiResponse<TResponse>>> GetAsync<TResponse>(string endpoint)
        {
            var completion = new TaskCompletionSource<ApiResult<ApiResponse<TResponse>>>();
            _client.GetJson<ApiResponse<TResponse>>(
                endpoint, true, result => completion.TrySetResult(result));
            return completion.Task;
        }

        private Task<ApiResult<ApiResponse<TResponse>>> PostAsync<TRequest, TResponse>(
            string endpoint, TRequest body)
        {
            var completion = new TaskCompletionSource<ApiResult<ApiResponse<TResponse>>>();
            _client.PostJson<TRequest, ApiResponse<TResponse>>(
                endpoint, body, true, result => completion.TrySetResult(result));
            return completion.Task;
        }

        private Task<ApiResult<ApiResponse<TResponse>>> PutAsync<TRequest, TResponse>(
            string endpoint, TRequest body)
        {
            var completion = new TaskCompletionSource<ApiResult<ApiResponse<TResponse>>>();
            _client.PutJson<TRequest, ApiResponse<TResponse>>(
                endpoint, body, true, result => completion.TrySetResult(result));
            return completion.Task;
        }
    }
}
