import type { ReactNode } from "react";
import { requireSession } from "@/lib/auth/session";
import { PortalShell } from "@/components/layout/portal-shell";

const items = [
  { href: "/dashboard", label: "Tổng quan" },
  { href: "/wallet", label: "Wallet" },
  { href: "/wallet/history", label: "Lịch sử thanh toán" },
  { href: "/profile", label: "Hồ sơ" },
];

export default async function PlayerLayout({ children }: { children: ReactNode }) {
  const session = await requireSession("PLAYER");
  return <PortalShell session={session} items={items} mode="PLAYER">{children}</PortalShell>;
}
