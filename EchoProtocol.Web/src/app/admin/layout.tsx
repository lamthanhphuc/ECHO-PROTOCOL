import type { ReactNode } from "react";
import { PortalShell } from "@/components/layout/portal-shell";
import { requireSession } from "@/lib/auth/session";

const items = [
  { href: "/admin", label: "Tổng quan", group: "Vận hành" },
  { href: "/admin/matches", label: "Phòng và trận đấu", group: "Vận hành" },
  { href: "/admin/users", label: "Người dùng", group: "Vận hành" },
  { href: "/admin/remote-fixed", label: "Cấu hình trận", group: "Cấu hình trò chơi" },
  { href: "/admin/adaptive", label: "Điều chỉnh độ khó", group: "Adaptive AI" },
  { href: "/admin/ai-profile", label: "Cách tính hồ sơ", group: "Adaptive AI" },
  { href: "/admin/balance-analytics", label: "Báo cáo trận đấu", group: "Adaptive AI" },
  { href: "/admin/economy", label: "Giá và phần thưởng", group: "Kinh tế" },
  { href: "/admin/payments", label: "Thanh toán", group: "Kinh tế" },
  { href: "/admin/wallet-transactions", label: "Lịch sử số dư", group: "Kinh tế" },
  { href: "/admin/purchases", label: "Mua vật phẩm", group: "Kinh tế" },
];

export default async function AdminLayout({ children }: { children: ReactNode }) {
  const session = await requireSession("ADMIN");
  return <PortalShell session={session} items={items} mode="ADMIN">{children}</PortalShell>;
}
