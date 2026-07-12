namespace EchoProtocol.Api
{
    /// <summary>
    /// Development API configuration fallback. Prefer Resources/ApiConfiguration asset.
    /// </summary>
    public static class ApiConfig
    {
        /// <summary>
        /// Local ASP.NET Core host (no /api suffix). Endpoints include /api/... via ApiConfiguration.BuildApiUrl.
        /// </summary>
        public const string DevBaseUrl = "http://localhost:5042";
    }
}
