using UnityEngine;

namespace EchoProtocol.Api
{
    [CreateAssetMenu(fileName = "ApiConfiguration", menuName = "Echo Protocol/Api Configuration")]
    public class ApiConfiguration : ScriptableObject
    {
        [SerializeField] private string baseUrl = ApiConfig.ProductionBaseUrl;
        [SerializeField] private int requestTimeoutSeconds = 15;

        public string BaseUrl => baseUrl;
        public int RequestTimeoutSeconds => requestTimeoutSeconds;
#if AED_V02_PILOT_HARNESS
        public void ConfigurePilotLoopback(string url)
        {
            if (!Debug.isDebugBuild || !System.Uri.TryCreate(url, System.UriKind.Absolute, out var uri) ||
                !uri.IsLoopback || uri.Scheme != "http" || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0)
                throw new System.InvalidOperationException("Pilot harness requires a loopback HTTP Development backend.");
            baseUrl = url.TrimEnd('/');
        }
#endif

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
