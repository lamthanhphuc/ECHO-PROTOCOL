namespace EchoProtocol.Api.Common;

public static class UsernameNormalizer
{
    public static string Normalize(string username) =>
        username.Trim().ToLowerInvariant();
}
