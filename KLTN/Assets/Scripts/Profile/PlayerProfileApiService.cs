using System;
using EchoProtocol.Api;

namespace EchoProtocol.Profile
{
  public sealed class PlayerProfileApiService
  {
    private readonly ApiClient _apiClient;
    private string _cosmeticsSyncedUser;

    public PlayerProfileApiService(ApiClient apiClient)
    {
      _apiClient = apiClient
        ?? throw new ArgumentNullException(nameof(apiClient));
    }

    public void GetCurrentProfile(
      Action<ApiResult<PlayerProfileApiResponse>> callback)
    {
      string user = EchoProtocol.Auth.AuthSession.CurrentUserId;
      if (!string.IsNullOrEmpty(user) && _cosmeticsSyncedUser != user)
      {
        _apiClient.PostJson<ReconcileProgressionCosmeticsRequest, ProgressionCosmeticReply>("/api/player/cosmetics/reconcile", new ReconcileProgressionCosmeticsRequest(), true, reply =>
        {
          if (reply.IsSuccess && reply.Data?.success == true && user == EchoProtocol.Auth.AuthSession.CurrentUserId)
            _cosmeticsSyncedUser = user;
          if (user == EchoProtocol.Auth.AuthSession.CurrentUserId) FetchProfile(callback);
        });
        return;
      }
      FetchProfile(callback);
    }

    public void EquipCosmetic(string category, string rewardKey, Action<bool> callback)
    {
      string user = EchoProtocol.Auth.AuthSession.CurrentUserId;
      _apiClient.PostJson<EquipProgressionCosmeticRequest, ProgressionCosmeticReply>("/api/player/cosmetics/equip",
        new EquipProgressionCosmeticRequest { category = category, rewardKey = rewardKey }, true, reply =>
        {
          if (user != EchoProtocol.Auth.AuthSession.CurrentUserId) return;
          bool success = reply.IsSuccess && reply.Data?.success == true;
          if (success) PlayerProfileSession.ApplyCosmetics(reply.Data.data);
          callback?.Invoke(success);
        });
    }

    private void FetchProfile(Action<ApiResult<PlayerProfileApiResponse>> callback)
    {
      string user = EchoProtocol.Auth.AuthSession.CurrentUserId;
      _apiClient.GetJson<PlayerProfileApiResponse>(
        ApiEndpoints.PlayerMe,
        attachBearer: true,
        result =>
        {
          if (user != EchoProtocol.Auth.AuthSession.CurrentUserId) return;
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
