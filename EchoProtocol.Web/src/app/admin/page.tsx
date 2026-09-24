import Link from "next/link";
import { EmptyState } from "@/components/ui/empty-state";
import { PageHeader, Panel, StatCard } from "@/components/ui/panel";
import { StatusBadge } from "@/components/ui/status-badge";
import { adminApi } from "@/lib/api/server-api";
import { formatDate, formatMoney } from "@/lib/utils/format";

export default async function AdminDashboard() {
  const [users, payments, pending, paid, fulfilled, failed, recentPurchases] = await Promise.all([
    adminApi.users("?page=1&pageSize=1"),
    adminApi.payments("?page=1&pageSize=5"),
    adminApi.payments("?page=1&pageSize=1&status=PENDING_PAYMENT"),
    adminApi.payments("?page=1&pageSize=1&status=PAID"),
    adminApi.payments("?page=1&pageSize=1&status=FULFILLED"),
    adminApi.payments("?page=1&pageSize=1&status=FAILED"),
    adminApi.purchases("?page=1&pageSize=5"),
  ]);

  return <>
    <PageHeader eyebrow="Control authority" title="Admin Dashboard"
      description="Ảnh chụp vận hành từ các API quản trị được Backend phân quyền ADMIN." />
    <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
      <StatCard label="Người dùng" value={users.totalItems} />
      <StatCard label="Payment orders" value={payments.totalItems} />
      <StatCard label="Pending" value={pending.totalItems} />
      <StatCard label="Paid" value={paid.totalItems} />
      <StatCard label="Đã hoàn tất" value={fulfilled.totalItems} />
      <StatCard label="Thất bại" value={failed.totalItems} />
    </div>
    <div className="mt-5 grid gap-5 xl:grid-cols-2">
      <Panel>
        <div className="mb-4 flex items-center justify-between"><h2 className="text-lg text-white">Thanh toán gần đây</h2><Link className="eyebrow" href="/admin/payments">Xem tất cả</Link></div>
        {payments.items.length === 0 ? <EmptyState title="Chưa có thanh toán" /> : <div className="space-y-3">
          {payments.items.map((item) => <Link key={item.paymentOrderId} href={`/admin/payments/${item.paymentOrderId}`} className="block rounded border border-[#1d3942] p-3 hover:border-emerald-700">
            <div className="flex items-start justify-between gap-3"><div><p className="text-sm text-white">{item.username}</p><p className="code muted mt-1">{item.productReference}</p></div><StatusBadge status={item.status} /></div>
            <p className="muted mt-2 text-xs">{formatMoney(item.amount, item.currency)} · {formatDate(item.createdAtUtc)}</p>
          </Link>)}
        </div>}
      </Panel>
      <Panel>
        <div className="mb-4 flex items-center justify-between"><h2 className="text-lg text-white">Purchase gần đây</h2><Link className="eyebrow" href="/admin/purchases">Xem tất cả</Link></div>
        {recentPurchases.items.length === 0 ? <EmptyState title="Chưa có purchase" /> : <div className="space-y-3">
          {recentPurchases.items.map((item) => <div key={item.purchaseId} className="rounded border border-[#1d3942] p-3">
            <div className="flex justify-between gap-3"><p className="text-sm text-white">{item.shopItemName}</p><StatusBadge status={item.status} /></div>
            <p className="muted mt-2 text-xs">{item.username} · {item.priceAtPurchase} credits · {formatDate(item.createdAtUtc)}</p>
          </div>)}
        </div>}
      </Panel>
    </div>
  </>;
}
