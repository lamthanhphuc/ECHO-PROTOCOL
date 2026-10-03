using System;
using System.Collections;
using System.Text;
using EchoProtocol.Auth;
using UnityEngine;
using UnityEngine.Networking;

namespace EchoProtocol.Api
{
  public static class ApiEndpoints
  {
    public const string Health = "/api/health";
    public const string AuthRegister = "/api/auth/register";
    public const string AuthLogin = "/api/auth/login";
    public const string AuthRefresh = "/api/auth/refresh";
    public const string AuthLogout = "/api/auth/logout";
    public const string AuthMe = "/api/auth/me";
    public const string PlayerMe = "/api/player/me";
    public const string PaymentsCatalog = "/api/payments/catalog";
    public const string ShopTeamTools = "/api/shop/items?category=TEAM_TOOL&pageSize=100";
    public const string ShopPurchase = "/api/shop/purchase";
    public const string InventoryMe = "/api/inventory/me";
    public const string TelemetryBatch = "/api/telemetry/batch";
  }

  /// <summary>
  /// HTTP client using UnityWebRequest. All URLs built via ApiConfiguration.BuildApiUrl.
  /// </summary>
  public class ApiClient : MonoBehaviour
  {
    private ApiConfiguration _configuration;

    // Only one refresh request may run at a time.
    private bool _refreshInProgress;
    private bool _lastRefreshSucceeded;

    public void Initialize(ApiConfiguration configuration)
    {
      _configuration = configuration;
    }

    public void GetJson<TResponse>(
      string endpoint,
      bool attachBearer,
      Action<ApiResult<TResponse>> callback)
    {
      StartCoroutine(SendJsonCoroutine(
        UnityWebRequest.kHttpVerbGET,
        endpoint,
        null,
        attachBearer,
        callback));
    }

    public void PostJson<TRequest, TResponse>(
      string endpoint,
      TRequest body,
      bool attachBearer,
      Action<ApiResult<TResponse>> callback)
    {
      var json = JsonUtility.ToJson(body);
      StartCoroutine(SendJsonCoroutine(
        UnityWebRequest.kHttpVerbPOST,
        endpoint,
        json,
        attachBearer,
        callback));
    }

    public void PutJson<TRequest, TResponse>(
      string endpoint,
      TRequest body,
      bool attachBearer,
      Action<ApiResult<TResponse>> callback)
    {
      var json = JsonUtility.ToJson(body);
      StartCoroutine(SendJsonCoroutine(
        UnityWebRequest.kHttpVerbPUT,
        endpoint,
        json,
        attachBearer,
        callback));
    }

    public void PostRawJson<TResponse>(
      string endpoint,
      string jsonBody,
      bool attachBearer,
      Action<ApiResult<TResponse>> callback)
    {
      StartCoroutine(SendJsonCoroutine(
        UnityWebRequest.kHttpVerbPOST,
        endpoint,
        jsonBody,
        attachBearer,
        callback));
    }

    private static bool IsTimeout(UnityWebRequest request)
    {
      if (request.result != UnityWebRequest.Result.ConnectionError)
      {
        return false;
      }

      var error = request.error ?? string.Empty;
      return error.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0
        || error.IndexOf("timed out", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private IEnumerator EnsureFreshAccessTokenCoroutine(
      Action<bool> completed)
    {
      if (!TokenStorage.CanRefreshSession())
      {
        completed?.Invoke(false);
        yield break;
      }

      // Another authenticated request is already rotating the token.
      // Wait for that request instead of sending another refresh.
      if (_refreshInProgress)
      {
        while (_refreshInProgress)
        {
          yield return null;
        }

        completed?.Invoke(_lastRefreshSucceeded);
        yield break;
      }

      _refreshInProgress = true;
      _lastRefreshSucceeded = false;

      try
      {
        var requestDto = new RefreshTokenRequestDto
        {
          refreshToken = TokenStorage.GetRefreshToken()
        };

        var json = JsonUtility.ToJson(requestDto);

        var url = _configuration.BuildApiUrl(
          ApiEndpoints.AuthRefresh);

        using var request = new UnityWebRequest(
          url,
          UnityWebRequest.kHttpVerbPOST);

        request.downloadHandler =
          new DownloadHandlerBuffer();

        request.uploadHandler =
          new UploadHandlerRaw(
            Encoding.UTF8.GetBytes(json));

        request.timeout =
          _configuration.RequestTimeoutSeconds;

        request.SetRequestHeader(
          "Accept",
          "application/json");

        request.SetRequestHeader(
          "Content-Type",
          "application/json; charset=utf-8");

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success
            || request.responseCode < 200
            || request.responseCode >= 300)
        {
          // 401 from /refresh means this refresh session can no
          // longer be used: expired, invalid, revoked or replayed.
          if (request.responseCode == 401)
          {
            ClearExpiredSession();
          }

          yield break;
        }

        LoginApiResponse response;

        try
        {
          response =
            JsonUtility.FromJson<LoginApiResponse>(
              request.downloadHandler?.text
              ?? string.Empty);
        }
        catch (Exception)
        {
          yield break;
        }

        if (response == null
            || !response.success
            || response.data == null)
        {
          yield break;
        }

        var data = response.data;

        if (!TokenStorage.TrySave(
              data.accessToken,
              data.expiresAt,
              data.refreshToken,
              data.refreshExpiresAt))
        {
          ClearExpiredSession();
          yield break;
        }

        _lastRefreshSucceeded = true;

        Debug.Log(
          "[Auth] Access token refreshed successfully.");
      }
      finally
      {
        _refreshInProgress = false;
        completed?.Invoke(
          _lastRefreshSucceeded);
      }
    }

    private static ApiResult<TResponse>
      CreateRefreshUnavailableResult<TResponse>()
    {
      return new ApiResult<TResponse>
      {
        IsSuccess = false,
        StatusCode = 0,
        FailureKind = ApiFailureKind.Network,
        ErrorCode = string.Empty,
        Message =
          "Unable to refresh session. Check backend connection."
      };
    }
    private static ApiResult<TResponse>
      CreateUnauthorizedResult<TResponse>()
    {
      return new ApiResult<TResponse>
      {
        IsSuccess = false,
        StatusCode = 401,
        FailureKind = ApiFailureKind.Business,
        ErrorCode = AuthErrorCodes.Unauthorized,
        Message =
          "Session expired. Please log in again."
      };
    }

    private static void ClearExpiredSession()
    {
      TokenStorage.Clear();
      AuthSession.Clear();
    }
    private IEnumerator SendJsonCoroutine<TResponse>(
      string method,
      string endpoint,
      string jsonBody,
      bool attachBearer,
      Action<ApiResult<TResponse>> callback,
      bool allowRefreshRetry = true)
    {
      if (_configuration == null)
      {
        callback?.Invoke(new ApiResult<TResponse>
        {
          IsSuccess = false,
          FailureKind = ApiFailureKind.Parse,
          Message = "ApiClient is not initialized",
          ErrorCode = "INTERNAL_SERVER_ERROR"
        });
        yield break;
      }

      if (attachBearer
          && TokenStorage.ShouldRefreshAccessToken())
      {
        var refreshCompleted = false;
        var refreshSucceeded = false;

        yield return EnsureFreshAccessTokenCoroutine(
          success =>
          {
            refreshSucceeded = success;
            refreshCompleted = true;
          });

        while (!refreshCompleted)
        {
          yield return null;
        }

        // If proactive refresh temporarily fails but the current
        // access token is still valid, use it for this request.
        if (!refreshSucceeded
            && TokenStorage.IsExpired())
        {
          callback?.Invoke(
            TokenStorage.CanRefreshSession()
              ? CreateRefreshUnavailableResult<TResponse>()
              : CreateUnauthorizedResult<TResponse>());

          yield break;
        }
      }

      var url = _configuration.BuildApiUrl(endpoint);
      using var request = new UnityWebRequest(url, method);
      request.downloadHandler = new DownloadHandlerBuffer();
      request.timeout = _configuration.RequestTimeoutSeconds;
      request.SetRequestHeader("Accept", "application/json");

      if (!string.IsNullOrEmpty(jsonBody))
      {
        var bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.SetRequestHeader("Content-Type", "application/json; charset=utf-8");
      }

      if (attachBearer)
      {
        var token = TokenStorage.GetAccessToken();
        if (!string.IsNullOrEmpty(token))
        {
          request.SetRequestHeader("Authorization", $"Bearer {token}");
        }
      }

      yield return request.SendWebRequest();

      var rawBody = request.downloadHandler?.text ?? string.Empty;
      var statusCode = request.responseCode;
      var result = new ApiResult<TResponse>
      {
        StatusCode = statusCode
      };

      if (request.result == UnityWebRequest.Result.ConnectionError)
      {
        result.IsSuccess = false;
        if (IsTimeout(request))
        {
          result.FailureKind = ApiFailureKind.Timeout;
          result.Message = "Request timed out. Please try again.";
        }
        else
        {
          result.FailureKind = ApiFailureKind.Network;
          result.Message = "Cannot connect to server. Check backend connection.";
        }

        callback?.Invoke(result);
        yield break;
      }

      if (request.result == UnityWebRequest.Result.DataProcessingError)
      {
        result.IsSuccess = false;
        result.FailureKind = ApiFailureKind.Parse;
        result.Message = request.error ?? "Failed to process server response.";
        callback?.Invoke(result);
        yield break;
      }

      // 401 refresh/retry must stay outside JSON parsing try/catch.
      // C# iterators cannot yield inside a try block that has catch.
      if (statusCode == 401
          && attachBearer
          && allowRefreshRetry
          && TokenStorage.CanRefreshSession())
      {
        var refreshCompleted = false;
        var refreshSucceeded = false;

        yield return EnsureFreshAccessTokenCoroutine(
          success =>
          {
            refreshSucceeded = success;
            refreshCompleted = true;
          });

        while (!refreshCompleted)
        {
          yield return null;
        }

        if (refreshSucceeded)
        {
          yield return SendJsonCoroutine(
            method,
            endpoint,
            jsonBody,
            attachBearer,
            callback,
            allowRefreshRetry: false);

          yield break;
        }

        // Temporary refresh/network failure must not destroy
        // an otherwise usable refresh session.
        if (TokenStorage.CanRefreshSession())
        {
          callback?.Invoke(
            CreateRefreshUnavailableResult<TResponse>());

          yield break;
        }

        // Otherwise the refresh endpoint rejected/cleared the
        // refresh session. Continue into the normal 401 parser.
      }
      try
      {
        if (statusCode >= 200 && statusCode < 300)
        {
          var parsed = JsonUtility.FromJson<TResponse>(rawBody);
          result.IsSuccess = true;
          result.Data = parsed;
          result.FailureKind = ApiFailureKind.None;
          callback?.Invoke(result);
          yield break;
        }

        if (statusCode == 401)
        {          result.IsSuccess = false;
          result.FailureKind = ApiFailureKind.Business;
          result.ErrorCode = AuthErrorCodes.Unauthorized;
          result.Message = "Session expired. Please log in again.";

          if (!string.IsNullOrWhiteSpace(rawBody))
          {
            try
            {
              var errorEnvelope = JsonUtility.FromJson<ErrorApiResponse>(rawBody);
              if (!string.IsNullOrEmpty(errorEnvelope.errorCode))
              {
                result.ErrorCode = errorEnvelope.errorCode;
              }

              if (!string.IsNullOrEmpty(errorEnvelope.message))
              {
                result.Message = errorEnvelope.message;
              }
            }
            catch (Exception)
            {
              result.FailureKind = ApiFailureKind.Parse;
            }
          }
          else
          {
            result.FailureKind = ApiFailureKind.Parse;
          }

          callback?.Invoke(result);
          yield break;
        }

        var businessError = JsonUtility.FromJson<ErrorApiResponse>(rawBody);
        result.IsSuccess = false;
        result.FailureKind = ApiFailureKind.Business;
        result.Message = string.IsNullOrEmpty(businessError.message)
          ? "Request failed"
          : businessError.message;
        result.ErrorCode = businessError.errorCode ?? string.Empty;
        callback?.Invoke(result);
      }
      catch (Exception)
      {
        result.IsSuccess = false;
        result.FailureKind = ApiFailureKind.Parse;
        result.Message = "Failed to parse server response.";
        callback?.Invoke(result);
      }
    }
  }

  [Serializable]
  public class HealthApiResponse
  {
    public bool success;
    public string message;
    public HealthData data;
    public string errorCode;
  }

  [Serializable]
  public class HealthData
  {
    public string service;
  }
}
