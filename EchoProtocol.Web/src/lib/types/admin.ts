import type { PagedResponse, UserRole } from "./common";
import type { PaymentStatus } from "./payment";

export interface AdminUser {
  userId: string;
  username: string;
  displayName: string | null;
  email: string;
  role: UserRole;
  status: "ACTIVE" | "LOCKED";
  walletBalance: number | null;
  totalMatches: number | null;
  totalWins: number | null;
  createdAtUtc: string;
}

export interface AdminPayment {
  paymentOrderId: string;
  userId: string;
  username: string;
  displayName: string | null;
  provider: string;
  productReference: string;
  amount: number;
  currency: string;
  status: PaymentStatus;
  createdAtUtc: string;
  paidAtUtc: string | null;
  fulfilledAtUtc: string | null;
}

export interface AdminWalletTransaction {
  transactionId: string;
  userId: string;
  username: string;
  displayName: string | null;
  type: "MATCH_REWARD" | "PURCHASE" | "PAYMENT_FULFILLMENT" | "ADMIN_GRANT";
  amount: number;
  balanceBefore: number;
  balanceAfter: number;
  reference: string;
  description: string;
  createdAtUtc: string;
}

export interface AdminPurchase {
  purchaseId: string;
  userId: string;
  username: string;
  displayName: string | null;
  shopItemId: string;
  shopItemName: string;
  category: string;
  priceAtPurchase: number;
  walletTransactionId: string;
  status: "COMPLETED";
  createdAtUtc: string;
}

export interface AdminPaymentDetail {
  order: AdminPayment & {
    email: string;
    providerOrderId: string | null;
    providerTransactionId: string | null;
    purpose: string;
    updatedAtUtc: string;
    expiresAtUtc: string | null;
    fulfillmentReference: string | null;
  };
  checkout: {
    checkoutSequenceId: number;
    provider: string;
    providerOrderId: string;
    providerPaymentLinkId: string | null;
    checkoutUrl: string | null;
    status: "RESERVED" | "READY";
    reservedAtUtc: string;
    readyAtUtc: string | null;
  } | null;
  providerEvents: Array<{
    paymentProviderEventId: string;
    provider: string;
    providerEventId: string;
    providerOrderId: string;
    amount: number;
    currency: string | null;
    normalizedStatus: "PAID" | "FAILED" | "UNKNOWN";
    processingOutcome: string;
    verificationStatus: string;
    receivedAtUtc: string;
    processedAtUtc: string | null;
  }>;
  fulfillment: {
    fulfillmentReference: string;
    kind: "WALLET_CREDIT" | "INVENTORY_ITEM";
    walletTransactionId: string | null;
    inventoryItemId: string | null;
    completedAtUtc: string;
  } | null;
  walletTransaction: AdminWalletTransaction | null;
}

export type AdminUsersPage = PagedResponse<AdminUser>;
export type AdminPaymentsPage = PagedResponse<AdminPayment>;
export type AdminWalletTransactionsPage = PagedResponse<AdminWalletTransaction>;
export type AdminPurchasesPage = PagedResponse<AdminPurchase>;

export interface AdminMatch {
  matchId: string;
  hostUserId: string;
  hostUsername: string;
  mapId: string | null;
  difficulty: string | null;
  status: "Lobby" | "InMatch" | "Ended";
  outcome: "WIN" | "LOSE" | null;
  playerCount: number;
  connectedCount: number;
  isFlagged: boolean;
  leaseExpiresAtUtc: string;
  startedAtUtc: string | null;
  endedAtUtc: string | null;
}
export type AdminMatchesPage = PagedResponse<AdminMatch>;
export interface AdminMatchDetail {
  match: AdminMatch;
  players: Array<{
    userId: string;
    username: string;
    displayName: string | null;
    fusionActorNumber: number;
    participated: boolean;
    hadGameplayDisconnect: boolean;
    boundAtUtc: string;
    lastSeenAtUtc: string;
    disconnectedAtUtc: string | null;
  }>;
}
export interface AdminModerationAction {
  id: string;
  targetId: string;
  action: "FLAG_MATCH" | "CLEAR_MATCH" | "STOP_MATCH" | "FLAG_USER" | "CLEAR_USER" | "LOCK_USER" | "UNLOCK_USER";
  adminUserId: string;
  reason: string;
  createdAtUtc: string;
}

export interface RemoteFixedVersion {
  version: string;
  mapId: "SciFi" | "Scene_A";
  difficulty: "EASY" | "NORMAL" | "HARD";
  protocolVersion: "RFB-1";
  contentHash: string;
  isActive: boolean;
  createdAtUtc: string;
}

export interface RemoteFixedVersionDetail extends RemoteFixedVersion {
  snapshotJson: string;
  activeVersion: string | null;
}
export interface CreateRemoteFixedVersionRequest {
  version: string;
  mapId: "SciFi" | "Scene_A";
  difficulty: "EASY" | "NORMAL" | "HARD";
  snapshotJson: string;
}
export interface RemoteFixedActivationRequest { expectedActiveVersion: string; reason: string; }
export interface RemoteFixedActivationResponse {
  previousActiveVersion: string | null;
  activeVersion: string | null;
  changed: boolean;
}
export interface RemoteFixedAudit {
  id: string; action: "CREATE" | "ACTIVATE" | "DEACTIVATE"; mapId: string; difficulty: string;
  previousVersion: string | null; targetVersion: string | null; adminUserId: string; reason: string; createdAtUtc: string;
}
export interface RemoteFixedStatus {
  remoteFixedEnabled: boolean;
  matchDataPolicy: "STANDARD" | "TEST_ONLY";
  protocolVersion: "RFB-1";
  supportedMaps: Array<"SciFi" | "Scene_A">;
}
export type RemoteFixedVersionsPage = PagedResponse<RemoteFixedVersion>;
export type RemoteFixedHistoryPage = PagedResponse<RemoteFixedAudit>;
