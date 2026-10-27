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

        public Task<ApiResult<ApiResponse<MatchAuthorityDto>>> EndAsync(Guid matchId, string reason) =>
            PostAsync<EndMatchAuthorityRequest, MatchAuthorityDto>(
                $"/api/matches/{matchId:D}/end",
                new EndMatchAuthorityRequest { reason = reason });

        public Task<ApiResult<ApiResponse<MatchResultResponseDto>>> SubmitResultAsync(
            Guid matchId,
            SubmitMatchResultRequestDto request) =>
            PutAsync<SubmitMatchResultRequestDto, MatchResultResponseDto>(
                $"/api/matches/{matchId:D}/result",
                request);

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
