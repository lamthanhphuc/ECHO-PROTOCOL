using System;
using System.Globalization;

namespace EchoProtocol.Auth
{
  /// <summary>
  /// MVP auth token storage via PlayerPrefs.
  /// Access and refresh tokens are persisted for session restore.
  /// Replace PlayerPrefs with platform-secure storage before production release.
  /// </summary>
  public static class TokenStorage
  {
    private const string AccessTokenKey =
      "echo_protocol.auth.access_token";

    private const string AccessExpiresAtKey =
      "echo_protocol.auth.expires_at";

    private const string RefreshTokenKey =
      "echo_protocol.auth.refresh_token";

    private const string RefreshExpiresAtKey =
      "echo_protocol.auth.refresh_expires_at";

    private const string LegacyAccessTokenKey =
      "echo_protocol_access_token";

    private const string LegacyUsernameKey =
      "echo_protocol_username";

    // Refresh before the access token is actually expired.
    // Match authority sends authenticated requests frequently,
    // so this protects the lease from expiring mid-match.
    private const int AccessRefreshSkewSeconds = 120;

    private const int RefreshExpirySkewSeconds = 30;

    public static bool TrySave(
      string accessToken,
      string accessExpiresAtIsoUtc,
      string refreshToken,
      string refreshExpiresAtIsoUtc)
    {
      if (string.IsNullOrWhiteSpace(accessToken)
          || string.IsNullOrWhiteSpace(refreshToken))
      {
        Clear();
        return false;
      }

      if (!TryParseExpiry(
            accessExpiresAtIsoUtc,
            out var accessExpiry)
          || !TryParseExpiry(
            refreshExpiresAtIsoUtc,
            out var refreshExpiry))
      {
        Clear();
        return false;
      }

      if (IsExpiredRelativeToNow(
            refreshExpiry,
            RefreshExpirySkewSeconds))
      {
        Clear();
        return false;
      }

      UnityEngine.PlayerPrefs.SetString(
        AccessTokenKey,
        accessToken);

      UnityEngine.PlayerPrefs.SetString(
        AccessExpiresAtKey,
        accessExpiresAtIsoUtc);

      UnityEngine.PlayerPrefs.SetString(
        RefreshTokenKey,
        refreshToken);

      UnityEngine.PlayerPrefs.SetString(
        RefreshExpiresAtKey,
        refreshExpiresAtIsoUtc);

      UnityEngine.PlayerPrefs.Save();
      return true;
    }

    public static string GetAccessToken() =>
      UnityEngine.PlayerPrefs.GetString(
        AccessTokenKey,
        string.Empty);

    public static string GetExpiresAt() =>
      UnityEngine.PlayerPrefs.GetString(
        AccessExpiresAtKey,
        string.Empty);

    public static string GetRefreshToken() =>
      UnityEngine.PlayerPrefs.GetString(
        RefreshTokenKey,
        string.Empty);

    public static string GetRefreshExpiresAt() =>
      UnityEngine.PlayerPrefs.GetString(
        RefreshExpiresAtKey,
        string.Empty);

    public static bool HasToken() =>
      !string.IsNullOrWhiteSpace(GetAccessToken());

    public static bool HasRefreshToken() =>
      !string.IsNullOrWhiteSpace(GetRefreshToken());

    public static bool HasStoredExpiry() =>
      !string.IsNullOrWhiteSpace(GetExpiresAt());

    public static bool CanRefreshSession() =>
      HasRefreshToken()
      && !IsRefreshExpired();

    public static bool HasUsableSession() =>
      (HasToken() && !IsExpired())
      || CanRefreshSession();

    public static bool IsExpired()
    {
      if (!TryParseExpiry(
            GetExpiresAt(),
            out var expiry))
      {
        return true;
      }

      return IsExpiredRelativeToNow(
        expiry,
        0);
    }

    public static bool ShouldRefreshAccessToken()
    {
      if (!TryParseExpiry(
            GetExpiresAt(),
            out var expiry))
      {
        return true;
      }

      return IsExpiredRelativeToNow(
        expiry,
        AccessRefreshSkewSeconds);
    }

    public static bool IsRefreshExpired()
    {
      if (!TryParseExpiry(
            GetRefreshExpiresAt(),
            out var expiry))
      {
        return true;
      }

      return IsExpiredRelativeToNow(
        expiry,
        RefreshExpirySkewSeconds);
    }

    public static void Clear()
    {
      UnityEngine.PlayerPrefs.DeleteKey(AccessTokenKey);
      UnityEngine.PlayerPrefs.DeleteKey(AccessExpiresAtKey);
      UnityEngine.PlayerPrefs.DeleteKey(RefreshTokenKey);
      UnityEngine.PlayerPrefs.DeleteKey(RefreshExpiresAtKey);

      UnityEngine.PlayerPrefs.DeleteKey(LegacyAccessTokenKey);
      UnityEngine.PlayerPrefs.DeleteKey(LegacyUsernameKey);

      UnityEngine.PlayerPrefs.Save();
    }

    private static bool TryParseExpiry(
      string expiresAtIsoUtc,
      out DateTimeOffset expiry)
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

    private static bool IsExpiredRelativeToNow(
      DateTimeOffset expiry,
      int skewSeconds) =>
      DateTimeOffset.UtcNow >=
      expiry.UtcDateTime.AddSeconds(-skewSeconds);
  }
}
