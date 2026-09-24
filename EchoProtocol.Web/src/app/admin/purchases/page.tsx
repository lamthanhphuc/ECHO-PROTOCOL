import Link from "next/link";
import { EmptyState } from "@/components/ui/empty-state";
import { Pagination } from "@/components/ui/pagination";
import { PageHeader, Panel } from "@/components/ui/panel";
import { StatusBadge } from "@/components/ui/status-badge";
import { adminApi } from "@/lib/api/server-api";
import type { SearchParams } from "@/lib/types/common";
import { formatDate, formatNumber, param, queryString } from "@/lib/utils/format";

export default async function PurchasesPage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const raw = await searchParams;
  const values = { userId: param(raw.userId), shopItemId: param(raw.shopItemId), fromUtc: param(raw.fromUtc), toUtc: param(raw.toUtc) };
  const page = Math.max(1, Number(param(raw.page)) || 1);
  const result = await adminApi.purchases(queryString({ page, pageSize: 20, ...values }));
  const pageParams = Object.fromEntries(Object.entries(values).filter(([, value]) => value));
  return <>
    <PageHeader eyebrow="Acquisition records" title="Purchases" description="Lịch sử mua item bằng soft currency từ Backend." />
    <Panel className="mb-5"><form action="/admin/purchases" className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
      <input className="input" name="userId" defaultValue={values.userId} placeholder="User ID" />
      <input className="input" name="shopItemId" defaultValue={values.shopItemId} placeholder="Shop item ID" />
      <input className="input" type="datetime-local" name="fromUtc" defaultValue={values.fromUtc} aria-label="From UTC" />
      <input className="input" type="datetime-local" name="toUtc" defaultValue={values.toUtc} aria-label="To UTC" />
      <button className="button md:col-span-2 xl:col-span-4" type="submit">Áp dụng bộ lọc</button>
    </form></Panel>
    {result.items.length === 0 ? <EmptyState title="Không có purchase phù hợp" /> : <div className="table-wrap rounded-lg"><table><thead><tr><th>Purchase</th><th>User</th><th>Item</th><th>Price</th><th>Status</th><th>Created</th></tr></thead><tbody>
      {result.items.map((item) => <tr key={item.purchaseId}><td className="code">{item.purchaseId.slice(0, 8)}…</td><td><Link className="hover:underline" href={`/admin/users/${item.userId}`}>{item.displayName ?? item.username}</Link></td><td>{item.shopItemName}<p className="muted text-xs">{item.category} · {item.shopItemId.slice(0, 8)}…</p></td><td>{formatNumber(item.priceAtPurchase)} credits<p className="code muted text-xs">TX {item.walletTransactionId.slice(0, 8)}…</p></td><td><StatusBadge status={item.status} /></td><td>{formatDate(item.createdAtUtc)}</td></tr>)}
    </tbody></table></div>}
    <Pagination page={result.page} totalPages={result.totalPages} pathname="/admin/purchases" params={pageParams} />
  </>;
}
