namespace EchoProtocol.Api.Common;

public static class AuthHttpStatusMapper
{
    public static int ToStatusCode(string errorCode) => errorCode switch
    {
        ErrorCodes.ValidationError => StatusCodes.Status400BadRequest,
        ErrorCodes.PasswordConfirmationMismatch => StatusCodes.Status400BadRequest,
        ErrorCodes.PasswordTooLong => StatusCodes.Status400BadRequest,
        ErrorCodes.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorCodes.TokenInvalid => StatusCodes.Status401Unauthorized,
        ErrorCodes.InvalidCredentials => StatusCodes.Status401Unauthorized,
        ErrorCodes.AccountLocked => StatusCodes.Status403Forbidden,
        ErrorCodes.Forbidden => StatusCodes.Status403Forbidden,
        ErrorCodes.NotFound => StatusCodes.Status404NotFound,
        ErrorCodes.Conflict => StatusCodes.Status409Conflict,
        ErrorCodes.UsernameAlreadyExists => StatusCodes.Status409Conflict,
        ErrorCodes.InternalServerError => StatusCodes.Status500InternalServerError,
        _ => StatusCodes.Status500InternalServerError
    };
}
