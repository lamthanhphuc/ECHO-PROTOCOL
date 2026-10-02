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
            if (!TokenStorage.TrySave(
                  loginData.accessToken,
                  loginData.expiresAt,
                  loginData.refreshToken,
                  loginData.refreshExpiresAt))
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

    public void Logout(
      Action<ApiResult<LogoutApiResponse>> callback = null)
    {
      var refreshToken = TokenStorage.GetRefreshToken();

      if (string.IsNullOrWhiteSpace(refreshToken))
      {
        ClearLocalAuth();

        callback?.Invoke(
          new ApiResult<LogoutApiResponse>
          {
            IsSuccess = true,
            FailureKind = ApiFailureKind.None
          });

        return;
      }

      var request = new RefreshTokenRequestDto
      {
        refreshToken = refreshToken
      };

      _apiClient.PostJson<
        RefreshTokenRequestDto,
        LogoutApiResponse>(
        ApiEndpoints.AuthLogout,
        request,
        attachBearer: false,
        result =>
        {
          // Explicit logout always clears local auth.
          // Server revocation is best-effort if connectivity fails.
          ClearLocalAuth();
          callback?.Invoke(result);
        });
    }
    public void LogoutLocal()
    {
      ClearLocalAuth();
    }

    public static bool ShouldClearToken<T>(ApiResult<T> result)
    {
      if (result == null)
      {
        return false;
      }

      // Temporary connectivity problems must not destroy a
      // still-valid refresh session.
      if (result.FailureKind == ApiFailureKind.Network
          || result.FailureKind == ApiFailureKind.Timeout)
      {
        return false;
      }

      if (result.StatusCode == 401)
      {
        return true;
      }

      return result.ErrorCode == AuthErrorCodes.TokenInvalid
        || result.ErrorCode == AuthErrorCodes.Unauthorized
        || result.ErrorCode == AuthErrorCodes.AccountLocked
        || result.ErrorCode == AuthErrorCodes.RefreshTokenInvalid
        || result.ErrorCode == AuthErrorCodes.RefreshTokenExpired
        || result.ErrorCode == AuthErrorCodes.RefreshTokenReused;
    }

    private static void ClearLocalAuth()
    {
      TokenStorage.Clear();
      AuthSession.Clear();
    }
  }
}
