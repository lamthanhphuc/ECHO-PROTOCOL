export interface MatchContent {
  entries: { map: string; mode: string; monster: string; route: string }[];
  difficulties: string[];
  gameVersions: string[];
}

export interface EconomyData {
  walletTelemetry?: {
    days: number; playerLimit: number; eventLimit: number; players: number;
    playersTruncated: boolean; ledgerTruncated: boolean; grantsTruncated: boolean; unlockEventsTruncated: boolean;
    walletP50: number | null; walletP90: number | null; timeBasis: string;
    experienceSources: { gameplay: number; admin: number; adminGrants: number; adminTruncated: boolean };
    flows: { source: string; credits: number; transactions: number }[];
    purchasesByCategory: { category: string; creditsSpent: number; purchases: number }[];
    cohorts: { matchesInWindow: string; players: number; walletP50: number | null; walletP90: number | null; gameplayCredits: number; purchases: number }[];
    unlocks: { userId: string; itemId: string; itemName: string; requiredLevel: number; unlockedAtUtc: string;
      price: number; priceWasSnapshotted: boolean; walletAtUnlock: number; affordabilityRatio: number | null;
      externalFundsBeforeUnlock: boolean; adminExperienceBeforeUnlock: boolean; historyComplete: boolean; minutesToUnlock: number | null; unlockGapWallMinutes: number | null }[];
  };
  items: { itemId: string; itemName: string; category: string; price: number; requiredLevel: number; isActive: boolean }[];
  proposals: {
    id: string; itemId: string; previousPrice: number; proposedPrice: number;
    previousRequiredLevel?: number | null; proposedRequiredLevel?: number | null;
    reason: string; status: string; proposedByUserId: string; proposedAtUtc: string;
    reviewedByUserId: string | null; reviewedAtUtc: string | null; reviewReason: string | null;
  }[];
  rewards?: Record<"participation" | "objective" | "win" | "survival" | "contribution" | "disconnected" | "maximum", number> & {
    policyKey: "REWARD_V1" | "REWARD_V2" | "REWARD_V3";
    baseMinutes: number;
    sciFiReferenceMinutes: number;
    floodedReferenceMinutes: number;
    purgeReferenceMinutes: number;
    easyMultiplier: number;
    hardMultiplier: number;
    v3LossCreditsPerMinute: number;
    v3LossExperiencePerMinute: number;
    lossCapCreditsPerMinute: number;
    rescueBonusCredits: number;
    objectiveActionCap: number;
    rescueActionCap: number;
    oneActionTeamPercent: number;
    twoActionTeamPercent: number;
    fullTeamPercent: number;
    winExperience: number;
    objectiveExperience: number;
    contributionExperience: number;
    survivalExperience: number;
    lossExperiencePerMinute: number;
    effectiveAtUtc: string;
  };
  rewardPolicy: string;
  maximumCurrencyPerMatch: number;
  rewardTelemetry?: { days: number; limit: number; truncated: boolean; cohorts: {
    mapId: string | null; difficulty: string | null; playerCount: number; rewardPolicyVersion: string | null;
    matches: number; eligibleWins: number; medianWinMinutes: number | null; suggestedReferenceMinutes: number | null;
    creditsPerPlayerHour: number; xpPerPlayerHour: number;
  }[] };
}
