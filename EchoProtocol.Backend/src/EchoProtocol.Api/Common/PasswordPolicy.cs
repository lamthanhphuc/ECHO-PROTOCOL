using System.Text;

namespace EchoProtocol.Api.Common;

public static class PasswordPolicy
{
    public const int MinLength = 6;
    public const int MaxUtf8Bytes = 72;

    public static bool IsTooShort(string password) => password.Length < MinLength;

    public static bool ExceedsMaxUtf8ByteLength(string password) =>
        Encoding.UTF8.GetByteCount(password) > MaxUtf8Bytes;
}
