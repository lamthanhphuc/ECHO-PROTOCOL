import "server-only";
import { redirect } from "next/navigation";
import { getAccessToken } from "@/lib/auth/session";
import { backendRequest } from "./backend-client";
import { BackendApiError } from "@/lib/errors";
import type { PlayerProfile } from "@/lib/types/player";
import type { AdaptiveAdminData, AdaptiveMatchAudit } from "@/lib/types/adaptive";
import type { ProfileTuningData } from "@/lib/types/profile-tuning";
import type { PaymentCatalog, PaymentCheckout, PaymentOrder, CreatePaymentOrderRequest } from "@/lib/types/payment";
import type {
  AdminPaymentDetail,
  AdminMatchesPage,
  AdminMatchDetail,
  AdminModerationAction,
  AdminPaymentsPage,
  AdminPurchasesPage,
  AdminUser,
  AdminUsersPage,
  AdminWalletTransactionsPage,
  RemoteFixedHistoryPage,
  RemoteFixedStatus,
  RemoteFixedVersionDetail,
  RemoteFixedVersionsPage,
} from "@/lib/types/admin";

async function authorized<T>(path: string, init?: { method?: string; body?: unknown }): Promise<T> {
  const token = await getAccessToken();
  if (!token) redirect("/login");
  try {
    return await backendRequest<T>(path, { ...init, token });
  } catch (error) {
    if (error instanceof BackendApiError && error.status === 401) redirect("/login?expired=1");
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
  profileTuning: () => authorized<ProfileTuningData>("/api/admin/ai-profile"),
  adaptive: (compatibility?: string) => authorized<AdaptiveAdminData>(
    `/api/admin/adaptive${compatibility ? `?compatibility=${encodeURIComponent(compatibility)}` : ""}`),
  matchAdaptive: (id: string) => authorized<AdaptiveMatchAudit>(`/api/admin/matches/${encodeURIComponent(id)}/adaptive`),
  matches: (query: string) => authorized<AdminMatchesPage>(`/api/admin/matches/logs${query}`),
  match: (id: string) => authorized<AdminMatchDetail>(`/api/admin/matches/${encodeURIComponent(id)}`),
  moderationHistory: (id: string) => authorized<AdminModerationAction[]>(`/api/admin/moderation/${encodeURIComponent(id)}`),
  users: (query: string) => authorized<AdminUsersPage>(`/api/admin/users${query}`),
  user: (id: string) => authorized<AdminUser>(`/api/admin/users/${encodeURIComponent(id)}`),
  payments: (query: string) => authorized<AdminPaymentsPage>(`/api/admin/payments${query}`),
  payment: (id: string) =>
    authorized<AdminPaymentDetail>(`/api/admin/payments/${encodeURIComponent(id)}`),
  walletTransactions: (query: string) =>
    authorized<AdminWalletTransactionsPage>(`/api/admin/wallet-transactions${query}`),
  purchases: (query: string) => authorized<AdminPurchasesPage>(`/api/admin/purchases${query}`),
  remoteFixedStatus: () => authorized<RemoteFixedStatus>("/api/admin/remote-fixed/status"),
  remoteFixedVersions: (query: string) =>
    authorized<RemoteFixedVersionsPage>(`/api/admin/remote-fixed/versions${query}`),
  remoteFixedVersion: (version: string) =>
    authorized<RemoteFixedVersionDetail>(`/api/admin/remote-fixed/versions/${encodeURIComponent(version)}`),
  remoteFixedHistory: (query: string) =>
    authorized<RemoteFixedHistoryPage>(`/api/admin/remote-fixed/history${query}`),
};
