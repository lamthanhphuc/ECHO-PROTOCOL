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
    public int level;
    public int walletBalance;
  }
}
