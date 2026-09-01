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
    public const string TelemetrySchemaUnsupported = "TELEMETRY_SCHEMA_UNSUPPORTED";
    public const string TelemetryUserMismatch = "TELEMETRY_USER_MISMATCH";
    public const string TelemetryUnavailable = "TELEMETRY_UNAVAILABLE";
    public const string MatchNotFound = "MATCH_NOT_FOUND";
    public const string MatchAuthorityForbidden = "MATCH_AUTHORITY_FORBIDDEN";
    public const string MatchLeaseExpired = "MATCH_LEASE_EXPIRED";
    public const string MatchAlreadyEnded = "MATCH_ALREADY_ENDED";
    public const string MatchSessionConflict = "MATCH_SESSION_CONFLICT";
    public const string MatchCapacityReached = "MATCH_CAPACITY_REACHED";
    public const string JoinProofInvalid = "JOIN_PROOF_INVALID";
    public const string MatchPlayerBindingConflict = "MATCH_PLAYER_BINDING_CONFLICT";
}
