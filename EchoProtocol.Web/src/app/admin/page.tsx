import Link from "next/link";
import { EmptyState } from "@/components/ui/empty-state";
import { PageHeader, Panel, StatCard } from "@/components/ui/panel";
import { StatusBadge } from "@/components/ui/status-badge";
import { adminApi } from "@/lib/api/server-api";
import { formatAdminDate as formatDate, formatMoney, paymentProductLabel } from "@/lib/utils/format";

export default async function AdminDashboard() {
  const [users, payments, recentPurchases, analytics, balanceStatus, balanceVersions] = await Promise.all([
    adminApi.users("?page=1&pageSize=1"),
    adminApi.payments("?page=1&pageSize=5"),
    adminApi.purchases("?page=1&pageSize=5"),
    adminApi.balanceAnalytics("?days=30&testOnly=false"),
    adminApi.remoteFixedStatus(),
    adminApi.remoteFixedVersions("?page=1&pageSize=20&activeOnly=true"),
  ]);

  const matchIds = analytics.cohorts
    .filter((group) => group.group === "ADAPTIVE_APPLIED")
    .flatMap((group) => group.recentMatchIds).slice(0, 3);
  const balanceLabel = balanceStatus.remoteFixedEnabled && balanceVersions.totalItems > 0
    ? String(balanceVersions.totalItems)
    : "Mặc định";
  return <>
    <PageHeader title="Tổng quan"
      description="Theo dõi cấu hình, cách AI điều chỉnh trận và kết quả được ghi nhận." />
    <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
      <StatCard label="Trận đã ghi kết quả" value={analytics.total} detail="Trận thường trong 30 ngày" />
      <StatCard label="Điều chỉnh có xác nhận từ chủ phòng" value={analytics.applied} detail="Số quyết định đã có biên nhận áp dụng" />
      <StatCard label="Cấu hình nền trên Web" value={balanceLabel} detail={balanceLabel === "Mặc định" ? "Dùng cấu hình trong game khi chọn nguồn tự động" : "Số bản đã kích hoạt. Xem phạm vi có hiệu lực ở Cấu hình trận."} />
    </div>
    <Panel className="mt-5">
      <div className="flex flex-wrap gap-3">
        <Link className="button button-secondary" href="/admin/remote-fixed">Xem cấu hình trận</Link>
        <Link className="button button-secondary" href="/admin/adaptive#adaptive-sample">Chạy thử Adaptive AI</Link>
        <Link className="button button-secondary" href="/admin/balance-analytics">Xem báo cáo trận đấu</Link>
      </div>
      <h2 className="admin-section-title mb-2 mt-5">Trận có ghi nhận điều chỉnh</h2>
      {matchIds.length ? <ul className="space-y-2 text-sm">{matchIds.map((id) =>
        <li key={id}><Link className="admin-link" href={"/admin/matches/" + id}>Xem chi tiết trận {id.slice(0, 8)}</Link></li>
      )}</ul> : <p className="muted text-sm">Chưa có trận thường có xác nhận điều chỉnh trong 30 ngày.</p>}
    </Panel>
    <p className="muted mt-7 text-sm">Vận hành · {users.totalItems} người dùng · {payments.totalItems} đơn thanh toán</p>
    <div className="mt-3 grid gap-5 xl:grid-cols-2">
      <Panel>
        <div className="mb-4 flex items-center justify-between"><h2 className="text-lg text-white">Thanh toán gần đây</h2><Link className="admin-link" href="/admin/payments">Xem tất cả</Link></div>
        {payments.items.length === 0 ? <EmptyState title="Chưa có thanh toán" /> : <div className="space-y-3">
          {payments.items.map((item) => <Link key={item.paymentOrderId} href={`/admin/payments/${item.paymentOrderId}`} className="block rounded border border-[#4a2b2b] p-3 hover:border-red-700">
            <div className="flex items-start justify-between gap-3"><div><p className="text-sm text-white">{item.username}</p><p className="muted mt-1 text-xs" title={item.productReference}>{paymentProductLabel(item.productReference)}</p></div><StatusBadge status={item.status} /></div>
            <p className="muted mt-2 text-xs">{formatMoney(item.amount, item.currency)} · {formatDate(item.createdAtUtc)}</p>
          </Link>)}
        </div>}
      </Panel>
      <Panel>
        <div className="mb-4 flex items-center justify-between"><h2 className="text-lg text-white">Giao dịch mua gần đây</h2><Link className="admin-link" href="/admin/purchases">Xem tất cả</Link></div>
        {recentPurchases.items.length === 0 ? <EmptyState title="Chưa có giao dịch mua" /> : <div className="space-y-3">
          {recentPurchases.items.map((item) => <div key={item.purchaseId} className="rounded border border-[#4a2b2b] p-3">
            <div className="flex justify-between gap-3"><p className="text-sm text-white">{item.shopItemName}</p><StatusBadge status={item.status} /></div>
            <p className="muted mt-2 text-xs">{item.username} · {item.priceAtPurchase} xu · {formatDate(item.createdAtUtc)}</p>
          </div>)}
        </div>}
      </Panel>
    </div>
  </>;
}
