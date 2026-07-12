using UnityEngine;

namespace EchoProtocol.Api
{
    [CreateAssetMenu(fileName = "ApiConfiguration", menuName = "Echo Protocol/Api Configuration")]
    public class ApiConfiguration : ScriptableObject
    {
        [SerializeField] private string baseUrl = ApiConfig.DevBaseUrl;
        [SerializeField] private int requestTimeoutSeconds = 15;

        public string BaseUrl => baseUrl;
        public int RequestTimeoutSeconds => requestTimeoutSeconds;

        public string BuildApiUrl(string endpoint)
        {
            var normalizedBase = (baseUrl ?? string.Empty).TrimEnd('/');
            var normalizedEndpoint = (endpoint ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(normalizedEndpoint))
            {
                return normalizedBase;
            }

            if (!normalizedEndpoint.StartsWith("/"))
            {
                normalizedEndpoint = "/" + normalizedEndpoint;
            }

            return normalizedBase + normalizedEndpoint;
        }
    }
}
