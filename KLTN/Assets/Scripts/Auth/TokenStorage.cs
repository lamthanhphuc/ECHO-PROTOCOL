namespace EchoProtocol.Auth
{
    /// <summary>
    /// MVP token storage via PlayerPrefs.
    /// Replace with secure storage (OS keychain / encrypted file) before production.
    /// </summary>
    public static class TokenStorage
    {
        private const string AccessTokenKey = "echo_protocol_access_token";
        private const string UsernameKey = "echo_protocol_username";

        public static void Save(string accessToken, string username)
        {
            UnityEngine.PlayerPrefs.SetString(AccessTokenKey, accessToken ?? string.Empty);
            UnityEngine.PlayerPrefs.SetString(UsernameKey, username ?? string.Empty);
            UnityEngine.PlayerPrefs.Save();
        }

        public static string GetAccessToken() =>
            UnityEngine.PlayerPrefs.GetString(AccessTokenKey, string.Empty);

        public static string GetUsername() =>
            UnityEngine.PlayerPrefs.GetString(UsernameKey, string.Empty);

        public static bool HasToken() =>
            !string.IsNullOrEmpty(GetAccessToken());

        public static void Clear()
        {
            UnityEngine.PlayerPrefs.DeleteKey(AccessTokenKey);
            UnityEngine.PlayerPrefs.DeleteKey(UsernameKey);
            UnityEngine.PlayerPrefs.Save();
        }
    }
}
