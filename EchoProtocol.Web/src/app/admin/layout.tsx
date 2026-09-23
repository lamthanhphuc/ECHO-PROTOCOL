import type { ReactNode } from "react";
import { PortalShell } from "@/components/layout/portal-shell";
import { requireSession } from "@/lib/auth/session";

const items = [
  { href: "/admin", label: "Tổng quan" },
  { href: "/admin/users", label: "Người dùng" },
  { href: "/admin/payments", label: "Thanh toán" },
  { href: "/admin/wallet-transactions", label: "Wallet ledger" },
  { href: "/admin/purchases", label: "Giao dịch mua" },
];

export default async function AdminLayout({ children }: { children: ReactNode }) {
  const session = await requireSession("ADMIN");
  return <PortalShell session={session} items={items} mode="ADMIN">{children}</PortalShell>;
}
