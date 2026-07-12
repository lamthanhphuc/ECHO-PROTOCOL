namespace EchoProtocol.Auth
{
  public static class AuthSession
  {
    public static string CurrentUserId { get; private set; } = string.Empty;
    public static string Username { get; private set; } = string.Empty;
    public static string Role { get; private set; } = string.Empty;
    public static string DisplayName { get; private set; } = string.Empty;
    public static int WalletBalance { get; private set; }
    public static bool IsAuthenticated { get; private set; }

    public static void ApplyFromMe(MeDto me)
    {
      CurrentUserId = me.id ?? string.Empty;
      Username = me.username ?? string.Empty;
      Role = me.role ?? string.Empty;
      DisplayName = me.displayName ?? string.Empty;
      WalletBalance = me.walletBalance;
      IsAuthenticated = !string.IsNullOrEmpty(CurrentUserId);
    }

    public static void Clear()
    {
      CurrentUserId = string.Empty;
      Username = string.Empty;
      Role = string.Empty;
      DisplayName = string.Empty;
      WalletBalance = 0;
      IsAuthenticated = false;
    }
  }
}
