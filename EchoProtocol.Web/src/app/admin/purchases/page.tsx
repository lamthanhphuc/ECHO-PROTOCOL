import Link from "next/link";
import { EmptyState } from "@/components/ui/empty-state";
import { Pagination } from "@/components/ui/pagination";
import { PageHeader, Panel } from "@/components/ui/panel";
import { StatusBadge, statusLabel } from "@/components/ui/status-badge";
import { adminApi } from "@/lib/api/server-api";
import type { SearchParams } from "@/lib/types/common";
import { formatAdminDate as formatDate, formatNumber, param, queryString } from "@/lib/utils/format";

export default async function PurchasesPage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const raw = await searchParams;
  const values = { userId: param(raw.userId), shopItemId: param(raw.shopItemId), fromUtc: param(raw.fromUtc), toUtc: param(raw.toUtc) };
  const page = Math.max(1, Number(param(raw.page)) || 1);
  const result = await adminApi.purchases(queryString({ page, pageSize: 20, ...values }));
  const pageParams = Object.fromEntries(Object.entries(values).filter(([, value]) => value));
  return <>
    <PageHeader title="Mua vật phẩm" />
    <Panel className="mb-5"><form action="/admin/purchases" className="grid gap-4 md:grid-cols-2">
      <label className="admin-field">Mã người dùng<input className="input" name="userId" defaultValue={values.userId} /></label>
      <label className="admin-field">Mã vật phẩm<input className="input" name="shopItemId" defaultValue={values.shopItemId} /></label>
      <label className="admin-field">Từ ngày (UTC)<input className="input" type="datetime-local" name="fromUtc" defaultValue={values.fromUtc} /></label>
      <label className="admin-field">Đến ngày (UTC)<input className="input" type="datetime-local" name="toUtc" defaultValue={values.toUtc} /></label>
      <button className="button justify-self-start md:col-span-2" type="submit">Áp dụng bộ lọc</button>
    </form></Panel>
    {result.items.length === 0 ? <EmptyState title="Không có giao dịch mua phù hợp" /> : <div className="table-wrap rounded-lg"><table><thead><tr><th>Giao dịch</th><th>Người dùng</th><th>Vật phẩm</th><th>Giá</th><th>Trạng thái</th><th>Ngày tạo</th></tr></thead><tbody>
      {result.items.map((item) => <tr key={item.purchaseId}><td className="code">{item.purchaseId.slice(0, 8)}…</td><td><Link className="admin-link" href={`/admin/users/${item.userId}`}>{item.displayName ?? item.username}</Link></td><td>{item.shopItemName}<p className="muted text-xs">{statusLabel(item.category)} · {item.shopItemId.slice(0, 8)}…</p></td><td>{formatNumber(item.priceAtPurchase)} xu<p><Link className="admin-link" href={"/admin/wallet-transactions?reference=" + item.purchaseId}>Xem giao dịch trừ xu</Link></p></td><td><StatusBadge status={item.status} /></td><td>{formatDate(item.createdAtUtc)}</td></tr>)}
    </tbody></table></div>}
    <Pagination page={result.page} totalPages={result.totalPages} pathname="/admin/purchases" params={pageParams} />
  </>;
}
