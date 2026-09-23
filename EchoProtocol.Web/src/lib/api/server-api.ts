import "server-only";
import { redirect } from "next/navigation";
import { getAccessToken } from "@/lib/auth/session";
import { backendRequest } from "./backend-client";
import { BackendApiError } from "@/lib/errors";
import type { PlayerProfile } from "@/lib/types/player";
import type { PaymentCatalog, PaymentCheckout, PaymentOrder, CreatePaymentOrderRequest } from "@/lib/types/payment";
import type {
  AdminPaymentDetail,
  AdminPaymentsPage,
  AdminPurchasesPage,
  AdminUser,
  AdminUsersPage,
  AdminWalletTransactionsPage,
} from "@/lib/types/admin";

async function authorized<T>(path: string, init?: { method?: string; body?: unknown }): Promise<T> {
  const token = await getAccessToken();
  if (!token) redirect("/login");
  try {
    return await backendRequest<T>(path, { ...init, token });
  } catch (error) {
    if (error instanceof BackendApiError && error.status === 401) redirect("/login");
    throw error;
  }
}

export const playerApi = {
  profile: () => authorized<PlayerProfile>("/api/player/me"),
  paymentCatalog: () => authorized<PaymentCatalog>("/api/payments/catalog"),
  paymentOrder: (paymentOrderId: string) =>
    authorized<PaymentOrder>(`/api/payments/orders/${encodeURIComponent(paymentOrderId)}`),
  createPayment: (request: CreatePaymentOrderRequest) =>
    authorized<PaymentOrder>("/api/payments/orders", { method: "POST", body: request }),
  checkout: (paymentOrderId: string) =>
    authorized<PaymentCheckout>(`/api/payments/orders/${paymentOrderId}/checkout`, { method: "POST" }),
};

export const adminApi = {
  users: (query: string) => authorized<AdminUsersPage>(`/api/admin/users${query}`),
  user: (id: string) => authorized<AdminUser>(`/api/admin/users/${encodeURIComponent(id)}`),
  payments: (query: string) => authorized<AdminPaymentsPage>(`/api/admin/payments${query}`),
  payment: (id: string) =>
    authorized<AdminPaymentDetail>(`/api/admin/payments/${encodeURIComponent(id)}`),
  walletTransactions: (query: string) =>
    authorized<AdminWalletTransactionsPage>(`/api/admin/wallet-transactions${query}`),
  purchases: (query: string) => authorized<AdminPurchasesPage>(`/api/admin/purchases${query}`),
};
