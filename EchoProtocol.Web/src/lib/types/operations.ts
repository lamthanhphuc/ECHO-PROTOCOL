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
  canReview: boolean;
  adminId: string;
  rewardPolicy: string;
  maximumCurrencyPerMatch: number;
}
