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

        public Task<ApiResult<ApiResponse<MatchPlayerBindingDto>>> DisconnectPlayerAsync(
            Guid matchId, int actorNumber) =>
            PostAsync<EmptyMatchAuthorityRequest, MatchPlayerBindingDto>(
                $"/api/matches/{matchId:D}/players/{actorNumber}/disconnect",
                new EmptyMatchAuthorityRequest());

        public Task<ApiResult<ApiResponse<MatchAuthorityDto>>> RenewLeaseAsync(Guid matchId) =>
            PostAsync<EmptyMatchAuthorityRequest, MatchAuthorityDto>(
                $"/api/matches/{matchId:D}/lease", new EmptyMatchAuthorityRequest());

        public Task<ApiResult<ApiResponse<MatchAuthorityDto>>> StartAsync(Guid matchId) =>
            PostAsync<EmptyMatchAuthorityRequest, MatchAuthorityDto>(
                $"/api/matches/{matchId:D}/start", new EmptyMatchAuthorityRequest());

        public Task<ApiResult<ApiResponse<MatchAuthorityDto>>> EndAsync(Guid matchId, string reason) =>
            PostAsync<EndMatchAuthorityRequest, MatchAuthorityDto>(
                $"/api/matches/{matchId:D}/end",
                new EndMatchAuthorityRequest { reason = reason });

        private Task<ApiResult<ApiResponse<TResponse>>> PostAsync<TRequest, TResponse>(
            string endpoint, TRequest body)
        {
            var completion = new TaskCompletionSource<ApiResult<ApiResponse<TResponse>>>();
            _client.PostJson<TRequest, ApiResponse<TResponse>>(
                endpoint, body, true, result => completion.TrySetResult(result));
            return completion.Task;
        }
    }
}
