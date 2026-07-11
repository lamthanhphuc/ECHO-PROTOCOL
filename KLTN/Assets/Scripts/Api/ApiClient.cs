using System;
using System.Text;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace EchoProtocol.Api
{
    /// <summary>
    /// HTTP client foundation using UnityWebRequest.
    /// </summary>
    public class ApiClient : MonoBehaviour
    {
        [SerializeField] private string baseUrl = ApiConfig.DevApiBaseUrl;

        public void GetHealth(Action<ApiResponse<HealthData>> onSuccess, Action<string> onError)
        {
            StartCoroutine(GetCoroutine($"{baseUrl}/health", onSuccess, onError));
        }

        private IEnumerator GetCoroutine<T>(string url, Action<ApiResponse<T>> onSuccess, Action<string> onError)
        {
            using var request = UnityWebRequest.Get(url);
            request.SetRequestHeader("Accept", "application/json");
            ApplyAuthHeader(request);

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke(request.error);
                yield break;
            }

            try
            {
                var response = JsonUtility.FromJson<ApiResponse<T>>(request.downloadHandler.text);
                onSuccess?.Invoke(response);
            }
            catch (Exception ex)
            {
                onError?.Invoke(ex.Message);
            }
        }

        private static void ApplyAuthHeader(UnityWebRequest request)
        {
            var token = Auth.TokenStorage.GetAccessToken();
            if (!string.IsNullOrEmpty(token))
            {
                request.SetRequestHeader("Authorization", $"Bearer {token}");
            }
        }
    }

    [Serializable]
    public class HealthData
    {
        public string service;
    }
}
