export interface AdaptiveConfigRef { id: string; version: string }

export interface AdaptiveParameter { baseline: number; tiers: number[] }

export interface AdaptiveConfiguration {
  enabled: boolean;
  survivalLow: number;
  survivalHigh: number;
  noiseLow: number;
  noiseHigh: number;
  minimumSurvivalSamples: number;
  minimumNoiseSamples: number;
  requireFullRoster: boolean;
  fixed: AdaptiveConfigRef | null;
  relief: AdaptiveConfigRef | null;
  increase: AdaptiveConfigRef | null;
  parameters: Record<string, AdaptiveParameter>;
  pilot: {
    observationSeconds: number;
    windowSeconds: number;
    evidenceMaxAgeSeconds: number;
    pressureWeights: number[];
    reliefThreshold: number;
    increasePressureCeiling: number;
    downRecoverySeconds: number;
    ordinarySupportQuota: number;
    emergencySupportQuota: number;
    supportCooldownSeconds: number;
    reliefCooldownSeconds: number;
    increaseCooldownSeconds: number;
    tierDwellSeconds: number;
    protectionSeconds: number;
    protectionCooldownSeconds: number;
    protectionQuota: number;
  };
}

export interface AdaptiveAdminData {
  compatibility: string | null;
  compatibilities: string[];
  version: string | null;
  publishedAtUtc: string | null;
  configuration: AdaptiveConfiguration;
  catalog: Record<string, AdaptiveParameter & { baselineLocked: boolean }>;
  options: Array<AdaptiveConfigRef & {
    isFixedFallback: boolean;
    source: string;
    fallbackConfigId: string;
    fallbackConfigVersion: string;
    supportItemBudget: number;
    chaseSpeed: number;
  }>;
  chaseSpeedAdminVerified: boolean;
  pilotEnabled: boolean;
  safety: Record<string, number>;
}

export interface AdaptivePreview {
  rule: string | null;
  selected: AdaptiveConfigRef;
  usedFixedFallback: boolean;
  reason: string | null;
}

export interface AdaptiveMatchAudit {
  preMatch: Array<{
    decisionId: string;
    scenarioConfigId: string;
    scenarioConfigVersion: string;
    policyConfigVersion: string | null;
    profileTuningVersion: string | null;
    policyVersion: string;
    resolutionMode: string;
    resolutionResult: string;
    usedFixedFallback: boolean;
    fallbackReasonCode: string | null;
    preMatchRule: string | null;
    fallbackConfigId: string;
    fallbackConfigVersion: string;
    committedAtUtc: string;
    appliedAtUtc: string | null;
    isCurrent: boolean;
    validity: string;
    teamSize: number;
    supportItemBudget: number;
    chaseSpeed: number;
  }>;
  decisions: Array<{
    decisionId: string;
    phaseOrdinal: number;
    decisionPoint: string;
    targetPhaseName: string;
    changedKey: string;
    policyVersion: string;
    catalogVersion: string;
    approvalStatus: string;
    applyStatus: string;
    reasonCode: string;
    createdAtUtc: string;
    expectedPlanRevision: number;
    planRevision: number;
    hostAppliedRevision: number | null;
    changes: Array<{ key: string; before: number; after: number }>;
    clientObservations: Array<{ userId: string; observedAtUtc: string }>;
    applyReceipt: { receiptId: string; effectEventId: string; appliedTick: number; appliedAtUtc: string } | null;
  }>;
  profile: { status: "PENDING" | "PROCESSING" | "COMPLETED" | "FAILED"; attempts: number; lastError: string; nextAttemptAtUtc: string; profileTuningVersion: string | null } | null;
  mayReprocess: boolean;
  hostEvidenceUnavailable: boolean;
}

export const adaptiveParameterLabels: Record<string, string> = {
  DetectionAcquireSeconds: "Thời gian phát hiện (giây)",
  PostChaseCooldownSeconds: "Hồi sau truy đuổi (giây)",
  HearingMultiplier: "Hệ số nghe",
  SearchSeconds: "Thời gian tìm kiếm (giây)",
  ChaseSpeed: "Tốc độ đuổi (đơn vị Unity/giây)",
  SpecialCooldownSeconds: "Hồi chiêu đặc biệt (giây)",
  Zone1MinionCap: "Giới hạn Minion Zone 1",
  Zone2MinionCap: "Giới hạn Minion Zone 2",
  Zone3AdaptiveMinionCap: "Giới hạn Minion Adaptive Zone 3",
  OptionalToolBudget: "Ngân sách công cụ tùy chọn",
  ResourceGrade: "Cấp tài nguyên",
  CabinetAmbushEnabled: "Phục kích tủ (0 tắt, 1 bật)",
};

export const adaptiveReasons: Record<string, string> = {
  POLICY_DISABLED: "Policy Adaptive đang tắt.",
  POLICY_CONFIG_INVALID: "Policy trước trận chưa được bật hoặc cấu hình không hợp lệ.",
  INPUT_INVALID: "Hồ sơ chưa đạt yêu cầu số mẫu hoặc độ phủ.",
  HOLD_RULE: "Điểm đội nằm trong vùng giữ nguyên.",
  HOLD_INCREASE_REQUIRES_FULL_ROSTER: "Tăng áp lực yêu cầu hồ sơ đủ điều kiện của toàn đội.",
  NO_APPROVED_NEXT_VALUE: "Chưa chọn ứng viên hợp lệ cho nấc kế tiếp.",
  ADAPTIVE_CONFIG_UNAVAILABLE: "Ứng viên đã chọn hiện không còn hợp lệ.",
  HOLD_PREMATCH_FIXED_FALLBACK: "Trận đã dùng cấu hình Fixed dự phòng.",
  HOLD_PREMATCH_V02_UNAVAILABLE: "Host không nhận được kế hoạch v0.2 đã duyệt trước khi bắt đầu.",
  HOLD_PREMATCH_CHASE_SPEED_MISMATCH: "Tốc độ đề xuất khác nấc v0.2 đã duyệt.",
  HOLD_ADMIN_TIER_DISABLED: "Giá trị đề xuất nằm ngoài các nấc đã xuất bản.",
  HOLD_PHASE4_INCREASE: "Final Phase khóa các thay đổi tăng áp lực.",
  HOLD_GLOBAL_PRESSURE_BUDGET: "Kế hoạch vượt giới hạn một thay đổi tăng áp lực.",
  HOLD_SINGLE_DELTA_REQUIRED: "Một quyết định giữa trận chỉ được đổi một thông số.",
  HOLD_SINGLE_TIER_REQUIRED: "Một quyết định giữa trận chỉ được đổi một nấc.",
  HOLD_ZONE3_SAFE_ENTRY_REQUIRED: "Tăng Minion Zone 3 cần cửa sổ an toàn khi vào Zone 3.",
  HOLD_STALKER_COMPOSITE_BUDGET: "Trước trận chỉ đổi một thông số Stalker.",
  HOLD_PRESSURE_HANDOFF_WORLD_NOT_REVERTED: "Hiệu ứng áp lực trước đó chưa được gỡ khỏi trận.",
  HOLD_PREVIOUS_RECEIPT_PENDING: "Quyết định trước đang chờ biên nhận.",
  HOLD_INSUFFICIENT_EVIDENCE: "Bằng chứng trong trận chưa đủ.",
  HOLD_HISTORICAL_OPPORTUNITIES_REQUIRED: "Lịch sử của đội chưa đủ điều kiện tăng áp lực.",
  HOLD_SAFE_WINDOW_CLOSED: "Trận chưa ở cửa sổ an toàn.",
  HOLD_TEAM_OVERLOADED: "Đội đang chịu quá nhiều áp lực hoặc vừa có người gục.",
  HOLD_PACING_COOLDOWN: "Chưa hết thời gian giữ mức hiện tại.",
  HOLD_SUPPORT_QUOTA: "Đã dùng hết quota hỗ trợ.",
  HOLD_SUPPORT_COOLDOWN: "Chưa hết hồi hỗ trợ.",
  COMMITTED_EFFECT_FAILED: "Host đã áp dụng revision nhưng backend chưa xác nhận hiệu ứng.",
};
