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
    public const string AuthMe = "/api/auth/me";
  }

  /// <summary>
  /// HTTP client using UnityWebRequest. All URLs built via ApiConfiguration.BuildApiUrl.
  /// </summary>
  public class ApiClient : MonoBehaviour
  {
    private ApiConfiguration _configuration;

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

    private IEnumerator SendJsonCoroutine<TResponse>(
      string method,
      string endpoint,
      string jsonBody,
      bool attachBearer,
      Action<ApiResult<TResponse>> callback)
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
        {
          result.IsSuccess = false;
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
