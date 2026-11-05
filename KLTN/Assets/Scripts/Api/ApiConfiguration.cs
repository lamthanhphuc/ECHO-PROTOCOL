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
        public static string ValidatePilotEndpoint(string url, string backendEnvironment, bool allowProduction, bool developmentBuild)
        {
            if (!developmentBuild || !System.Uri.TryCreate(url, System.UriKind.Absolute, out var uri) ||
                uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0 || uri.AbsolutePath != "/")
                throw new System.InvalidOperationException("Harness requires a Development build and a root API URL without credentials, query or fragment.");
            if (string.IsNullOrEmpty(backendEnvironment)) backendEnvironment = "Local";
            if (backendEnvironment == "Local" && uri.IsLoopback && uri.Scheme == "http")
                return uri.GetLeftPart(System.UriPartial.Authority);
            var expected = new System.Uri(ApiConfig.ProductionBaseUrl);
            if (backendEnvironment == "Production" && allowProduction && uri.Scheme == "https" &&
                System.Uri.Compare(uri, expected, System.UriComponents.SchemeAndServer, System.UriFormat.UriEscaped,
                    System.StringComparison.OrdinalIgnoreCase) == 0)
                return uri.GetLeftPart(System.UriPartial.Authority);
            throw new System.InvalidOperationException("Backend target rejected. Production requires the pinned HTTPS API and explicit launcher opt-in.");
        }
#if AED_V02_PILOT_HARNESS
        public void ConfigurePilotLoopback(string url)
        {
            ConfigurePilotBackend(url, "Local", false);
        }

        public void ConfigurePilotBackend(string url, string backendEnvironment, bool allowProduction)
        {
            baseUrl = ValidatePilotEndpoint(url, backendEnvironment, allowProduction, Debug.isDebugBuild);
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
