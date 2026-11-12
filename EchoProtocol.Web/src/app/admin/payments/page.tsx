import Link from "next/link";
import { EmptyState } from "@/components/ui/empty-state";
import { Pagination } from "@/components/ui/pagination";
import { PageHeader, Panel } from "@/components/ui/panel";
import { StatusBadge, statusLabel } from "@/components/ui/status-badge";
import { adminApi } from "@/lib/api/server-api";
import type { SearchParams } from "@/lib/types/common";
import { formatAdminDate as formatDate, formatMoney, paymentProductLabel, param, queryString } from "@/lib/utils/format";

const statuses = ["CREATED", "PENDING_PAYMENT", "PAID", "FULFILLED", "FAILED", "CANCELLED", "EXPIRED"];

export default async function PaymentsPage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const raw = await searchParams;
  const values = {
    status: param(raw.status), provider: param(raw.provider), userId: param(raw.userId),
    productReference: param(raw.productReference), fromUtc: param(raw.fromUtc), toUtc: param(raw.toUtc),
  };
  const page = Math.max(1, Number(param(raw.page)) || 1);
  const result = await adminApi.payments(queryString({ page, pageSize: 20, ...values }));
  const filterQuery = queryString({ ...values, page });
  const pageParams = Object.fromEntries(Object.entries(values).filter(([, value]) => value));

  return <>
    <PageHeader title="Thanh toán" description="Tra cứu đơn thanh toán và tình trạng cấp xu." />
    <Panel className="mb-5"><form action="/admin/payments" className="grid gap-4 md:grid-cols-2 2xl:grid-cols-3">
      <label className="admin-field">Trạng thái<select className="input" name="status" defaultValue={values.status}><option value="">Tất cả</option>{statuses.map((status) => <option key={status} value={status}>{statusLabel(status)}</option>)}</select></label>
      <label className="admin-field">Đơn vị thanh toán<input className="input" name="provider" defaultValue={values.provider} placeholder="Ví dụ: PAYOS" /></label>
      <label className="admin-field">Mã người dùng<input className="input" name="userId" defaultValue={values.userId} /></label>
      <label className="admin-field">Mã sản phẩm<input className="input" name="productReference" defaultValue={values.productReference} /></label>
      <label className="admin-field">Từ ngày (UTC)<input className="input" type="datetime-local" name="fromUtc" defaultValue={values.fromUtc} /></label>
      <label className="admin-field">Đến ngày (UTC)<input className="input" type="datetime-local" name="toUtc" defaultValue={values.toUtc} /></label>
      <button className="button justify-self-start md:col-span-2 2xl:col-span-3" type="submit">Áp dụng bộ lọc</button>
    </form></Panel>
    {result.items.length === 0 ? <EmptyState title="Không có đơn thanh toán phù hợp" /> : <div className="table-wrap rounded-lg"><table><thead><tr><th>Đơn hàng</th><th>Người dùng</th><th>Sản phẩm</th><th>Số tiền</th><th>Trạng thái</th><th>Ngày tạo</th></tr></thead><tbody>
      {result.items.map((item) => <tr key={item.paymentOrderId}><td><Link className="code text-red-300 hover:underline" href={"/admin/payments/" + item.paymentOrderId + queryString({ filters: filterQuery.slice(1) })}>{item.paymentOrderId.slice(0, 8)}…</Link><p className="muted text-xs">{item.provider}</p></td><td><Link className="hover:underline" href={`/admin/users/${item.userId}`}>{item.displayName ?? item.username}</Link></td><td title={item.productReference}>{paymentProductLabel(item.productReference)}</td><td>{formatMoney(item.amount, item.currency)}</td><td><StatusBadge status={item.status} /></td><td>{formatDate(item.createdAtUtc)}</td></tr>)}
    </tbody></table></div>}
    <Pagination page={result.page} totalPages={result.totalPages} pathname="/admin/payments" params={pageParams} />
  </>;
}
