import Link from "next/link";
import { EmptyState } from "@/components/ui/empty-state";
import { Pagination } from "@/components/ui/pagination";
import { PageHeader, Panel } from "@/components/ui/panel";
import { StatusBadge } from "@/components/ui/status-badge";
import { adminApi } from "@/lib/api/server-api";
import type { SearchParams } from "@/lib/types/common";
import { formatDate, formatMoney, param, queryString } from "@/lib/utils/format";

const statuses = ["CREATED", "PENDING_PAYMENT", "PAID", "FULFILLED", "FAILED", "CANCELLED", "EXPIRED"];

export default async function PaymentsPage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const raw = await searchParams;
  const values = {
    status: param(raw.status), provider: param(raw.provider), userId: param(raw.userId),
    productReference: param(raw.productReference), fromUtc: param(raw.fromUtc), toUtc: param(raw.toUtc),
  };
  const page = Math.max(1, Number(param(raw.page)) || 1);
  const result = await adminApi.payments(queryString({ page, pageSize: 20, ...values }));
  const pageParams = Object.fromEntries(Object.entries(values).filter(([, value]) => value));

  return <>
    <PageHeader eyebrow="Payment operations" title="Thanh toán" description="Theo dõi vòng đời PaymentOrder và fulfillment từ Backend." />
    <Panel className="mb-5"><form action="/admin/payments" className="grid gap-3 md:grid-cols-3 xl:grid-cols-6">
      <select className="input" name="status" defaultValue={values.status}><option value="">Mọi status</option>{statuses.map((status) => <option key={status}>{status}</option>)}</select>
      <input className="input" name="provider" defaultValue={values.provider} placeholder="Provider" />
      <input className="input" name="userId" defaultValue={values.userId} placeholder="User ID" />
      <input className="input" name="productReference" defaultValue={values.productReference} placeholder="Product reference" />
      <input className="input" type="datetime-local" name="fromUtc" defaultValue={values.fromUtc} aria-label="From UTC" />
      <input className="input" type="datetime-local" name="toUtc" defaultValue={values.toUtc} aria-label="To UTC" />
      <button className="button md:col-span-3 xl:col-span-6" type="submit">Áp dụng bộ lọc</button>
    </form></Panel>
    {result.items.length === 0 ? <EmptyState title="Không có PaymentOrder phù hợp" /> : <div className="table-wrap rounded-lg"><table><thead><tr><th>Order</th><th>User</th><th>Product</th><th>Amount</th><th>Status</th><th>Created</th></tr></thead><tbody>
      {result.items.map((item) => <tr key={item.paymentOrderId}><td><Link className="code text-emerald-300 hover:underline" href={`/admin/payments/${item.paymentOrderId}`}>{item.paymentOrderId.slice(0, 8)}…</Link><p className="muted text-xs">{item.provider}</p></td><td><Link className="hover:underline" href={`/admin/users/${item.userId}`}>{item.displayName ?? item.username}</Link></td><td className="code">{item.productReference}</td><td>{formatMoney(item.amount, item.currency)}</td><td><StatusBadge status={item.status} /></td><td>{formatDate(item.createdAtUtc)}</td></tr>)}
    </tbody></table></div>}
    <Pagination page={result.page} totalPages={result.totalPages} pathname="/admin/payments" params={pageParams} />
  </>;
}
