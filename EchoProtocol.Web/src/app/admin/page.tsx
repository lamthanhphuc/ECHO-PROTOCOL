import Link from "next/link";
import { DashboardRefresh } from "@/components/admin/dashboard-refresh";
import { EmptyState } from "@/components/ui/empty-state";
import { PageHeader, Panel } from "@/components/ui/panel";
import { StatusBadge } from "@/components/ui/status-badge";
import { adminApi } from "@/lib/api/server-api";
import { formatAdminDate as formatDate, formatMoney, paymentProductLabel } from "@/lib/utils/format";

export default async function AdminDashboard() {
  const [dashboard, payments, purchases] = await Promise.all([
    adminApi.dashboard(),
    adminApi.payments("?page=1&pageSize=5"),
    adminApi.purchases("?page=1&pageSize=5"),
  ]);
  const metrics = [
    {
      label: "Người chơi online",
      value: dashboard.onlinePlayersEstimate,
      href: "/admin/matches",
    },
    {
      label: "Phòng hoạt động",
      value: dashboard.activeRooms,
      href: "/admin/matches",
    },
    {
      label: "Trận đang diễn ra",
      value: dashboard.activeMatches,
      href: "/admin/matches?status=InMatch",
    },
    {
      label: "Tài khoản người chơi",
      value: dashboard.registeredPlayers,
      href: "/admin/users",
    },
  ];

  return <>
    <div className="flex flex-wrap items-start justify-between gap-3">
      <PageHeader title="Tổng quan vận hành" />
      <DashboardRefresh />
    </div>
    <p className="muted -mt-3 mb-5 text-sm">
      Dữ liệu lúc {formatDate(dashboard.observedAtUtc)}
    </p>

    <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
      {metrics.map((metric) => <Link key={metric.label} href={metric.href}
        className="panel block rounded-lg p-5 transition-colors hover:border-[#cf7774] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#ffb5ab]">
        <p className="eyebrow">{metric.label}</p>
        <p className="mt-3 text-3xl font-semibold tabular-nums text-white">{metric.value.toLocaleString("vi-VN")}</p>
      </Link>)}
    </div>

    <Panel className="mt-5">
      <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
        <div><h2 className="admin-section-title">Phòng gần đây</h2></div>
        <Link className="admin-link text-sm" href="/admin/matches">Xem phòng và trận đấu →</Link>
      </div>
      {dashboard.recentRooms.length === 0 ? <EmptyState title="Chưa có phòng được ghi nhận" /> :
        <div className="table-wrap rounded-lg"><table><thead><tr><th>Phòng</th><th>Chủ phòng</th><th>Trạng thái</th><th>Tạo lúc</th></tr></thead><tbody>
          {dashboard.recentRooms.map((room) => <tr key={room.roomInstanceId}>
            <td><Link className="admin-link font-semibold" href={`/admin/rooms/${room.roomInstanceId}`}>{room.fusionSessionName}</Link></td>
            <td>{room.hostUsername || "—"}</td>
            <td>{room.closedAtUtc ? "Đã đóng" : !room.activatedAtUtc ? "Chờ kích hoạt" :
              Date.parse(room.leaseExpiresAtUtc) > Date.parse(dashboard.observedAtUtc) ? "Lease còn hiệu lực" : "Lease đã hết"}</td>
            <td>{formatDate(room.createdAtUtc)}</td>
          </tr>)}
        </tbody></table></div>}
    </Panel>

    <Panel className="mt-5">
      <h2 className="admin-section-title mb-3">Đi đến công việc</h2>
      <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
        <Link className="admin-action-card" href="/admin/matches">
          <strong>Phòng và trận đấu</strong>
        </Link>
        <Link className="admin-action-card" href="/admin/users">
          <strong>Người dùng</strong>
        </Link>
        <Link className="admin-action-card" href="/admin/chat-words">
          <strong>Từ cấm chat</strong>
        </Link>
        <Link className="admin-action-card" href="/admin/remote-fixed">
          <strong>Cấu hình trận</strong>
        </Link>
      </div>
    </Panel>

    <div className="mt-5 grid gap-5 xl:grid-cols-2">
      <Panel>
        <div className="mb-4 flex items-center justify-between"><h2 className="admin-section-title">Thanh toán gần đây</h2><Link className="admin-link text-sm" href="/admin/payments">Xem tất cả</Link></div>
        {payments.items.length === 0 ? <EmptyState title="Chưa có thanh toán" /> : <div className="space-y-3">
          {payments.items.map((item) => <Link key={item.paymentOrderId} href={`/admin/payments/${item.paymentOrderId}`} className="block rounded border border-[#4a2b2b] p-3 hover:border-red-700">
            <div className="flex items-start justify-between gap-3"><div><p className="text-sm text-white">{item.username}</p><p className="muted mt-1 text-xs" title={item.productReference}>{paymentProductLabel(item.productReference)}</p></div><StatusBadge status={item.status} /></div>
            <p className="muted mt-2 text-xs">{formatMoney(item.amount, item.currency)} · {formatDate(item.createdAtUtc)}</p>
          </Link>)}
        </div>}
      </Panel>
      <Panel>
        <div className="mb-4 flex items-center justify-between"><h2 className="admin-section-title">Giao dịch mua gần đây</h2><Link className="admin-link text-sm" href="/admin/purchases">Xem tất cả</Link></div>
        {purchases.items.length === 0 ? <EmptyState title="Chưa có giao dịch mua" /> : <div className="space-y-3">
          {purchases.items.map((item) => <div key={item.purchaseId} className="rounded border border-[#4a2b2b] p-3">
            <div className="flex justify-between gap-3"><p className="text-sm text-white">{item.shopItemName}</p><StatusBadge status={item.status} /></div>
            <p className="muted mt-2 text-xs">{item.username} · {item.priceAtPurchase} xu · {formatDate(item.createdAtUtc)}</p>
          </div>)}
        </div>}
      </Panel>
    </div>
  </>;
}
