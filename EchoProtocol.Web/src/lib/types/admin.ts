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
  type: "MATCH_REWARD" | "PURCHASE" | "PAYMENT_FULFILLMENT";
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
