using System;
using EchoProtocol.Api;
using UnityEngine;

namespace EchoProtocol.Auth
{
  public class AuthApiService : MonoBehaviour
  {
    private ApiClient _apiClient;

    public void Initialize(ApiClient apiClient)
    {
      _apiClient = apiClient;
    }

    public bool IsInitialized => _apiClient != null;

    public void Register(string email, string username, string password, string confirmPassword, Action<ApiResult<RegisterApiResponse>> callback)
    {
      var request = new RegisterRequestDto
      {
        email = email,
        username = username,
        password = password,
        confirmPassword = confirmPassword
      };

      _apiClient.PostJson<RegisterRequestDto, RegisterApiResponse>(
        ApiEndpoints.AuthRegister,
        request,
        attachBearer: false,
        callback);
    }

    public void Login(string username, string password, Action<ApiResult<LoginApiResponse>> callback)
    {
      var request = new LoginRequestDto
      {
        username = username,
        password = password
      };

      _apiClient.PostJson<LoginRequestDto, LoginApiResponse>(
        ApiEndpoints.AuthLogin,
        request,
        attachBearer: false,
        result =>
        {
          if (result.IsSuccess && result.Data != null && result.Data.success && result.Data.data != null)
          {
            var loginData = result.Data.data;
            if (!TokenStorage.TrySave(loginData.accessToken, loginData.expiresAt))
            {
              callback?.Invoke(new ApiResult<LoginApiResponse>
              {
                IsSuccess = false,
                StatusCode = result.StatusCode,
                FailureKind = ApiFailureKind.Parse,
                Message = "Login response contains an invalid or expired token.",
                ErrorCode = AuthErrorCodes.TokenInvalid
              });
              return;
            }
          }

          callback?.Invoke(result);
        });
    }

    public void GetCurrentUser(Action<ApiResult<MeApiResponse>> callback)
    {
      _apiClient.GetJson<MeApiResponse>(
        ApiEndpoints.AuthMe,
        attachBearer: true,
        result =>
        {
          if (result.IsSuccess && result.Data != null && result.Data.success && result.Data.data != null)
          {
            AuthSession.ApplyFromMe(result.Data.data);
            callback?.Invoke(result);
            return;
          }

          if (ShouldClearToken(result))
          {
            ClearLocalAuth();
          }

          callback?.Invoke(result);
        });
    }

    public void LogoutLocal()
    {
      ClearLocalAuth();
    }

    public static bool ShouldClearToken<T>(ApiResult<T> result)
    {
      if (TokenStorage.IsExpired())
      {
        return true;
      }

      if (result == null)
      {
        return false;
      }

      if (result.StatusCode == 401)
      {
        return true;
      }

      return result.ErrorCode == AuthErrorCodes.TokenInvalid
        || result.ErrorCode == AuthErrorCodes.Unauthorized
        || result.ErrorCode == AuthErrorCodes.AccountLocked;
    }

    private static void ClearLocalAuth()
    {
      TokenStorage.Clear();
      AuthSession.Clear();
    }
  }
}
