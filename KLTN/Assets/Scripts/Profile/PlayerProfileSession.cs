namespace EchoProtocol.Profile
{
  public static class PlayerProfileSession
  {
    public static string UserId { get; private set; } = string.Empty;
    public static string DisplayName { get; private set; } = string.Empty;
    public static int TotalMatches { get; private set; }
    public static int TotalWins { get; private set; }
    public static long ExperiencePoints { get; private set; }
    public static int Level { get; private set; }
    public static int WalletBalance { get; private set; }

    public static bool HasProfile =>
      !string.IsNullOrWhiteSpace(UserId);

    public static void Apply(PlayerProfileDataDto profile)
    {
      if (profile == null)
      {
        Clear();
        return;
      }

      UserId = profile.userId ?? string.Empty;
      DisplayName = profile.displayName ?? string.Empty;
      TotalMatches = profile.totalMatches;
      TotalWins = profile.totalWins;
      ExperiencePoints = profile.experiencePoints;
      Level = profile.level;
      WalletBalance = profile.walletBalance;
    }

    public static void Clear()
    {
      UserId = string.Empty;
      DisplayName = string.Empty;
      TotalMatches = 0;
      TotalWins = 0;
      ExperiencePoints = 0;
      Level = 0;
      WalletBalance = 0;
    }
  }
}
