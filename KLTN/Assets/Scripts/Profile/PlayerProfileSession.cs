namespace EchoProtocol.Profile
{
  public static class PlayerProfileSession
  {
    public static event System.Action ProfileChanged;
    public static ProgressionCosmeticDto[] CosmeticRewards { get; private set; } = System.Array.Empty<ProgressionCosmeticDto>();
    public static void ApplyCosmetics(ProgressionCosmeticDto[] rewards)
    {
      CosmeticRewards = rewards ?? System.Array.Empty<ProgressionCosmeticDto>();
      ProfileChanged?.Invoke();
    }
    public static string UserId { get; private set; } = string.Empty;
    public static string DisplayName { get; private set; } = string.Empty;
    public static int TotalMatches { get; private set; }
    public static int TotalWins { get; private set; }
    public static long ExperiencePoints { get; private set; }
    public static long LifetimeExperiencePoints { get; private set; }
    public static int Level { get; private set; }
    public static long ExperienceInLevel { get; private set; }
    public static long ExperienceRequiredForNextLevel { get; private set; }
    public static int LevelCap { get; private set; }
    public static LevelUnlockDto[] LevelUnlocks { get; private set; } = System.Array.Empty<LevelUnlockDto>();
    public static bool AtLevelCap => HasProfile && LevelCap > 0 && Level >= LevelCap;
    public static bool HasLevelProgress => HasProfile && (AtLevelCap || ExperienceRequiredForNextLevel > 0);
    public static float LevelProgress => AtLevelCap ? 1f : ExperienceRequiredForNextLevel > 0
      ? UnityEngine.Mathf.Clamp01((float)ExperienceInLevel / ExperienceRequiredForNextLevel) : 0f;
    public static string ExperienceLabel => AtLevelCap ? EchoProtocol.Settings.GameLanguage.Choose("CẤP TỐI ĐA", "MAX LEVEL")
      : HasLevelProgress ? $"{ExperienceInLevel:N0} / {ExperienceRequiredForNextLevel:N0} XP" : $"{ExperiencePoints:N0} XP";
    public static int WalletBalance { get; private set; }
    public static PlayerActivityDto RecentMatch { get; private set; }
    public static PlayerActivityDto LatestWin { get; private set; }
    public static PlayerActivityDto LatestNonSurvival { get; private set; }

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
      LifetimeExperiencePoints = string.IsNullOrEmpty(profile.levelCurveVersion)
        ? profile.experiencePoints : profile.lifetimeExperiencePoints;
      Level = profile.level;
      ExperienceInLevel = profile.experienceInLevel;
      ExperienceRequiredForNextLevel = profile.experienceRequiredForNextLevel;
      LevelCap = profile.levelCap;
      LevelUnlocks = profile.levelUnlocks ?? System.Array.Empty<LevelUnlockDto>();
      // Older servers omit the explicit level progress fields.
      if (string.IsNullOrEmpty(profile.levelCurveVersion) && profile.level == profile.experiencePoints / 500 + 1)
      {
        ExperienceInLevel = profile.experiencePoints % 500;
        ExperienceRequiredForNextLevel = 500;
      }
      WalletBalance = profile.walletBalance;
      RecentMatch = profile.recentMatch;
      LatestWin = profile.latestWin;
      LatestNonSurvival = profile.latestNonSurvival;
      ApplyCosmetics(profile.cosmeticRewards);
    }

    public static void Clear()
    {
      UserId = string.Empty;
      DisplayName = string.Empty;
      TotalMatches = 0;
      TotalWins = 0;
      ExperiencePoints = 0;
      LifetimeExperiencePoints = 0;
      Level = 0;
      ExperienceInLevel = 0;
      ExperienceRequiredForNextLevel = 0;
      LevelCap = 0;
      LevelUnlocks = System.Array.Empty<LevelUnlockDto>();
      WalletBalance = 0;
      RecentMatch = null;
      LatestWin = null;
      LatestNonSurvival = null;
      ApplyCosmetics(null);
    }
  }
}
