using EchoProtocol.Api;

namespace EchoProtocol.Auth
{
  public static class AuthErrorMapper
  {
    public static string Map(ApiResult<RegisterApiResponse> result) =>
      result == null ? "Something went wrong. Please try again." : Map(result.ErrorCode, result.FailureKind, result.Message);

    public static string Map(ApiResult<LoginApiResponse> result) =>
      result == null ? "Something went wrong. Please try again." : Map(result.ErrorCode, result.FailureKind, result.Message);

    public static string Map(ApiResult<MeApiResponse> result) =>
      result == null ? "Something went wrong. Please try again." : Map(result.ErrorCode, result.FailureKind, result.Message);

    public static string Map(string errorCode, ApiFailureKind failureKind, string backendMessage)
    {
      if (failureKind == ApiFailureKind.Network)
      {
        return "Cannot connect to server. Check backend connection.";
      }

      if (failureKind == ApiFailureKind.Timeout)
      {
        return "Request timed out. Please try again.";
      }

      if (failureKind == ApiFailureKind.Parse)
      {
        return string.IsNullOrWhiteSpace(backendMessage)
          ? "Something went wrong. Please try again."
          : backendMessage;
      }

      return errorCode switch
      {
        "USERNAME_ALREADY_EXISTS" => "Username already exists",
        "INVALID_CREDENTIALS" => "Invalid username or password",
        "ACCOUNT_LOCKED" => "Account is locked",
        "PASSWORD_CONFIRMATION_MISMATCH" => "Password confirmation does not match",
        "PASSWORD_TOO_LONG" => "Password must not exceed 72 UTF-8 bytes",
        "TOKEN_INVALID" => "Session expired. Please log in again.",
        "UNAUTHORIZED" => "Session expired. Please log in again.",
        _ => string.IsNullOrWhiteSpace(backendMessage)
          ? "Something went wrong. Please try again."
          : backendMessage
      };
    }

    public static string MapClientValidation(string message) => message;
  }
}
