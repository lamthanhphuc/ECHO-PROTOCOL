using System.Text;

namespace EchoProtocol.Auth
{
  public static class PasswordPolicy
  {
    public const int MinLength = 6;
    public const int MaxUsernameLength = 100;
    public const int MaxUtf8Bytes = 72;

    public static bool IsTooShort(string password) => password.Length < MinLength;

    public static bool ExceedsMaxUtf8ByteLength(string password) =>
      Encoding.UTF8.GetByteCount(password) > MaxUtf8Bytes;

    public static bool IsUsernameTooLong(string username) =>
      username.Length > MaxUsernameLength;
  }
}
