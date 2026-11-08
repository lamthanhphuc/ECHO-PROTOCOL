export interface BalanceAnalytics {
  total: number;
  truncated: boolean;
  limit: number;
  cohorts: { group: string; matches: number; wins: number; averageSeconds: number; players: number; survivors: number; recentMatchIds: string[] }[];
  reasons: { reason: string; matches: number }[];
  approved: number;
  applied: number;
  clientObserved: number;
  failedEffect: number;
}

export interface PlayerAIHistory {
  profile: {
    profileLineageId: string; profileRevision: number; profileFormulaVersion: string;
    survivalScore: number; survivalStatus: string; survivalSampleCount: number;
    noiseScore: number; noiseStatus: string; noiseSampleCount: number;
    objectiveScore: number | null; objectiveStatus: string; objectiveSampleCount: number;
    toolUsageScore: number | null; toolUsageStatus: string; toolUsageSampleCount: number;
  } | null;
  rows: {
    id: string; matchId: string; matchEndTs: string; score: number; dimension: string; profileLineageId: string;
    matchScoreFormulaVersion: string; normalizationConfigVersion: string; alphaConfigVersion: string;
    status: string; retractionReason: string | null;
  }[];
  hasNext: boolean;
}
