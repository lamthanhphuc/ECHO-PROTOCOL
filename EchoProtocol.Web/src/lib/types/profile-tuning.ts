export interface ProfileTuning {
  survivalAlpha: number;
  noiseAlpha: number;
  objectiveAlpha: number;
  toolUsageAlpha: number;
  noiseMin: number;
  noiseMax: number;
  toolUsageMax: number;
  noiseTypes: string[];
  objectiveTimeMin: number;
  objectiveTimeMax: number;
  historyMatches: number;
  historyOpportunities: number;
  historyMaxAgeDays: number;
  historyHalfLifeDays: number;
}

export interface ProfileTuningData {
  version: string | null;
  publishedAtUtc: string | null;
  settings: ProfileTuning;
  allowedNoiseTypes: string[];
}

export interface ProfileTuningProjection {
  scores: { dimension: string; matchScore: number | null; updatedScore: number | null }[];
  countedNoise: number;
  objectiveSpeed: number | null;
  historyMatches: number;
  historyOpportunities: number;
  historyMaxAgeDays: number;
  historyHalfLifeDays: number;
  thirtyDayHistoryWeight: number;
}
