namespace EchoProtocol.Api.Common;

public static class ErrorCodes
{
    public const string ValidationError = "VALIDATION_ERROR";
    public const string Unauthorized = "UNAUTHORIZED";
    public const string Forbidden = "FORBIDDEN";
    public const string NotFound = "NOT_FOUND";
    public const string Conflict = "CONFLICT";
    public const string InternalServerError = "INTERNAL_SERVER_ERROR";
    public const string EmailAlreadyExists = "EMAIL_ALREADY_EXISTS";
    public const string UsernameAlreadyExists = "USERNAME_ALREADY_EXISTS";
    public const string InvalidCredentials = "INVALID_CREDENTIALS";
    public const string AccountLocked = "ACCOUNT_LOCKED";
    public const string PasswordConfirmationMismatch = "PASSWORD_CONFIRMATION_MISMATCH";
    public const string PasswordTooLong = "PASSWORD_TOO_LONG";
    public const string TokenInvalid = "TOKEN_INVALID";
}
