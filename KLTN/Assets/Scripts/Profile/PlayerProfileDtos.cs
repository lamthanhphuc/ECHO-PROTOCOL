using System;

namespace EchoProtocol.Profile
{
  [Serializable]
  public sealed class PlayerProfileApiResponse
  {
    public bool success;
    public string message;
    public PlayerProfileDataDto data;
    public string errorCode;
  }

  [Serializable]
  public sealed class PlayerProfileDataDto
  {
    public string userId;
    public string displayName;
    public int totalMatches;
    public int totalWins;
    public long experiencePoints;
    public long lifetimeExperiencePoints;
    public int level;
    public string levelCurveVersion;
    public long experienceInLevel;
    public long experienceRequiredForNextLevel;
    public int levelCap;
    public LevelUnlockDto[] levelUnlocks;
    public ProgressionCosmeticDto[] cosmeticRewards;
    public int walletBalance;
    public PlayerActivityDto recentMatch;
    public PlayerActivityDto latestWin;
    public PlayerActivityDto latestNonSurvival;
  }

  [Serializable]
  public sealed class ProgressionCosmeticDto
  {
    public string rewardKey;
    public string category;
    public int requiredLevel;
    public string displayName;
    public bool owned;
    public bool equipped;
    public string grantedAtUtc;
  }

  [Serializable]
  public sealed class ProgressionCosmeticReply
  {
    public bool success;
    public string message;
    public ProgressionCosmeticDto[] data;
  }

  [Serializable]
  public sealed class EquipProgressionCosmeticRequest
  {
    public string category;
    public string rewardKey;
  }

  [Serializable]
  public sealed class ReconcileProgressionCosmeticsRequest { }

  [Serializable]
  public sealed class LevelUnlockDto
  {
    public string itemId;
    public string itemName;
    public string category;
    public int requiredLevel;
    public long experienceRemaining;
    public bool owned;
  }

  [Serializable]
  public sealed class PlayerActivityDto
  {
    public string matchId;
    public string endedAtUtc;
    public string mapId;
    public string outcome;
    public int durationSeconds;
  }
}
