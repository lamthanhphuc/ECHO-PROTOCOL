export interface MatchContent {
  entries: { map: string; mode: string; monster: string; route: string }[];
  difficulties: string[];
  gameVersions: string[];
}

export interface EconomyData {
  items: { itemId: string; itemName: string; category: string; price: number; isActive: boolean }[];
  proposals: {
    id: string; itemId: string; previousPrice: number; proposedPrice: number;
    reason: string; status: string; proposedByUserId: string; proposedAtUtc: string;
    reviewedByUserId: string | null; reviewedAtUtc: string | null; reviewReason: string | null;
  }[];
  rewards?: Record<"participation" | "objective" | "win" | "survival" | "contribution" | "disconnected" | "maximum", number> & {
    policyKey: "REWARD_V1" | "REWARD_V2";
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
}
