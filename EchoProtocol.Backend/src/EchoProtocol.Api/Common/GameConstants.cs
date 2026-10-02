namespace EchoProtocol.Api.Common;

public static class GameConstants
{
    public const int DefaultPlayerWalletBalance = 500;

    public const int MinimumMatchDurationSeconds = 60;
    public const int MatchDurationSeconds = 45 * 60;
    public const int MatchResultSubmissionGraceSeconds = 60;
    public const int MaximumAcceptedMatchDurationSeconds =
        MatchDurationSeconds + MatchResultSubmissionGraceSeconds;
}
