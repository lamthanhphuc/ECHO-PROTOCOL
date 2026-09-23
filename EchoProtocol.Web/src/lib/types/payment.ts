export type PaymentStatus =
  | "CREATED"
  | "PENDING_PAYMENT"
  | "PAID"
  | "FULFILLED"
  | "FAILED"
  | "CANCELLED"
  | "EXPIRED";

export interface PaymentCatalogItem {
  productReference: string;
  displayName: string;
  amount: number;
  currency: string;
  walletCredit: number;
  provider: "PAYOS";
}

export interface PaymentCatalog {
  items: PaymentCatalogItem[];
}

export interface CreatePaymentOrderRequest {
  productReference: string;
  provider: "PAYOS";
  idempotencyKey: string;
}

export interface PaymentOrder {
  paymentOrderId: string;
  provider: string;
  providerOrderId: string | null;
  purpose: string;
  productReference: string;
  amount: number;
  currency: string;
  status: PaymentStatus;
  createdAtUtc: string;
  updatedAtUtc: string;
  expiresAtUtc: string | null;
  paidAtUtc: string | null;
  fulfilledAtUtc: string | null;
  isReplay: boolean;
}

export interface PaymentCheckout {
  paymentOrderId: string;
  provider: string;
  providerOrderId: string;
  checkoutUrl: string;
  expiresAtUtc: string | null;
  status: PaymentStatus;
  isReplay: boolean;
}
