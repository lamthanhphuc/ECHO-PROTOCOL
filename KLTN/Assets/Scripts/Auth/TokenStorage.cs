using System;
using System.Globalization;

namespace EchoProtocol.Auth
{
  /// <summary>
  /// MVP token storage via PlayerPrefs. Not secure production storage.
  /// Stores access token and expiresAt only.
  /// </summary>
  public static class TokenStorage
  {
    private const string AccessTokenKey = "echo_protocol.auth.access_token";
    private const string ExpiresAtKey = "echo_protocol.auth.expires_at";
    private const string LegacyAccessTokenKey = "echo_protocol_access_token";
    private const string LegacyUsernameKey = "echo_protocol_username";
    private const int ExpirySkewSeconds = 30;

    public static bool TrySave(string accessToken, string expiresAtIsoUtc)
    {
      if (string.IsNullOrWhiteSpace(accessToken))
      {
        Clear();
        return false;
      }

      if (!TryParseExpiry(expiresAtIsoUtc, out var expiry))
      {
        Clear();
        return false;
      }

      if (IsExpiredRelativeToNow(expiry))
      {
        Clear();
        return false;
      }

      UnityEngine.PlayerPrefs.SetString(AccessTokenKey, accessToken);
      UnityEngine.PlayerPrefs.SetString(ExpiresAtKey, expiresAtIsoUtc);
      UnityEngine.PlayerPrefs.Save();
      return true;
    }

    public static string GetAccessToken() =>
      UnityEngine.PlayerPrefs.GetString(AccessTokenKey, string.Empty);

    public static string GetExpiresAt() =>
      UnityEngine.PlayerPrefs.GetString(ExpiresAtKey, string.Empty);

    public static bool HasToken() =>
      !string.IsNullOrEmpty(GetAccessToken());

    public static bool HasStoredExpiry() =>
      !string.IsNullOrWhiteSpace(GetExpiresAt());

    public static bool IsExpired()
    {
      if (!TryParseExpiry(GetExpiresAt(), out var expiry))
      {
        return true;
      }

      return IsExpiredRelativeToNow(expiry);
    }

    public static void Clear()
    {
      UnityEngine.PlayerPrefs.DeleteKey(AccessTokenKey);
      UnityEngine.PlayerPrefs.DeleteKey(ExpiresAtKey);
      UnityEngine.PlayerPrefs.DeleteKey(LegacyAccessTokenKey);
      UnityEngine.PlayerPrefs.DeleteKey(LegacyUsernameKey);
      UnityEngine.PlayerPrefs.Save();
    }

    private static bool TryParseExpiry(string expiresAtIsoUtc, out DateTimeOffset expiry)
    {
      expiry = default;
      if (string.IsNullOrWhiteSpace(expiresAtIsoUtc))
      {
        return false;
      }

      return DateTimeOffset.TryParse(
        expiresAtIsoUtc,
        CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind,
        out expiry);
    }

    private static bool IsExpiredRelativeToNow(DateTimeOffset expiry) =>
      DateTimeOffset.UtcNow >= expiry.UtcDateTime.AddSeconds(-ExpirySkewSeconds);
  }
}
