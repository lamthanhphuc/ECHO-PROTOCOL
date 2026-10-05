namespace EchoProtocol.Auth
{
  public static class AuthErrorCodes
  {
    public const string EmailAlreadyExists = "EMAIL_ALREADY_EXISTS";
    public const string TokenInvalid = "TOKEN_INVALID";
    public const string Unauthorized = "UNAUTHORIZED";
    public const string AccountLocked = "ACCOUNT_LOCKED";
    public const string RefreshTokenInvalid = "REFRESH_TOKEN_INVALID";
    public const string RefreshTokenExpired = "REFRESH_TOKEN_EXPIRED";
    public const string RefreshTokenReused = "REFRESH_TOKEN_REUSED";
  }
}
