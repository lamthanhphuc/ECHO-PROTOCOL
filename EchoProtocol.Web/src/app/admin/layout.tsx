import type { ReactNode } from "react";
import { PortalShell } from "@/components/layout/portal-shell";
import { requireSession } from "@/lib/auth/session";

const items = [
  { href: "/admin", label: "Tổng quan" },
  { href: "/admin/users", label: "Người dùng" },
  { href: "/admin/matches", label: "Phòng và trận đấu" },
  { href: "/admin/payments", label: "Thanh toán" },
  { href: "/admin/wallet-transactions", label: "Lịch sử số dư" },
  { href: "/admin/purchases", label: "Giao dịch mua" },
  { href: "/admin/remote-fixed", label: "Cân bằng cố định" },
];

export default async function AdminLayout({ children }: { children: ReactNode }) {
  const session = await requireSession("ADMIN");
  return <PortalShell session={session} items={items} mode="ADMIN">{children}</PortalShell>;
}
