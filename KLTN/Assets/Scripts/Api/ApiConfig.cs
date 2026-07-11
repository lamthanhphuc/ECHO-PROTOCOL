namespace EchoProtocol.Api
{
    /// <summary>
    /// Development API configuration. Change BaseUrl when deploying backend.
    /// </summary>
    public static class ApiConfig
    {
        /// <summary>
        /// Local ASP.NET Core API (see EchoProtocol.Backend launchSettings.json http profile).
        /// </summary>
        public const string DevApiBaseUrl = "http://localhost:5042/api";

        // Production: set via build scripting or remote config — never hard-code secrets here.
    }
}
