export interface PlayerProfile {
  cosmeticRewards?: ProgressionCosmetic[];
  userId: string;
  displayName: string;
  totalMatches: number;
  totalWins: number;
  experiencePoints: number;
  lifetimeExperiencePoints: number;
  levelCurveVersion: string;
  experienceInLevel: number;
  experienceRequiredForNextLevel: number;
  levelCap: number;
  levelUnlocks?: { itemId: string; itemName: string; category: string; requiredLevel: number; experienceRemaining: number; owned: boolean }[];
  level: number;
  walletBalance: number;
}

export interface ProgressionCosmetic {
  rewardKey: string; category: string; requiredLevel: number; displayName: string;
  owned: boolean; equipped: boolean; grantedAtUtc: string | null;
}
