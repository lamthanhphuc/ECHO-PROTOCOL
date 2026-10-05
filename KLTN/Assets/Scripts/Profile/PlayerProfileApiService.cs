using System;
using EchoProtocol.Api;

namespace EchoProtocol.Profile
{
  public sealed class PlayerProfileApiService
  {
    private readonly ApiClient _apiClient;

    public PlayerProfileApiService(ApiClient apiClient)
    {
      _apiClient = apiClient
        ?? throw new ArgumentNullException(nameof(apiClient));
    }

    public void GetCurrentProfile(
      Action<ApiResult<PlayerProfileApiResponse>> callback)
    {
      _apiClient.GetJson<PlayerProfileApiResponse>(
        ApiEndpoints.PlayerMe,
        attachBearer: true,
        result =>
        {
          if (result.IsSuccess
              && result.Data != null
              && result.Data.success
              && result.Data.data != null)
          {
            PlayerProfileSession.Apply(result.Data.data);
          }

          callback?.Invoke(result);
        });
    }
  }
}
