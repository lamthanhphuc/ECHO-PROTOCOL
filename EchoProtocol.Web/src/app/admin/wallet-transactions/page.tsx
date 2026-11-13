import Link from "next/link";
import { CopyReference } from "@/components/admin/copy-reference";
import { EmptyState } from "@/components/ui/empty-state";
import { Pagination } from "@/components/ui/pagination";
import { PageHeader, Panel } from "@/components/ui/panel";
import { adminApi } from "@/lib/api/server-api";
import { statusLabel } from "@/components/ui/status-badge";
import type { SearchParams } from "@/lib/types/common";
import { formatAdminDate as formatDate, formatNumber, param, queryString } from "@/lib/utils/format";

const types = ["MATCH_REWARD", "PURCHASE", "PAYMENT_FULFILLMENT", "ADMIN_GRANT"];

export default async function WalletTransactionsPage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const raw = await searchParams;
  const values = { userId: param(raw.userId), type: param(raw.type), reference: param(raw.reference), fromUtc: param(raw.fromUtc), toUtc: param(raw.toUtc) };
  const page = Math.max(1, Number(param(raw.page)) || 1);
  const result = await adminApi.walletTransactions(queryString({ page, pageSize: 20, ...values }));
  const pageParams = Object.fromEntries(Object.entries(values).filter(([, value]) => value));
  return <>
    <PageHeader title="Lịch sử số dư" />
    <Panel className="mb-5"><form action="/admin/wallet-transactions" className="grid gap-4 md:grid-cols-2 2xl:grid-cols-3">
      <label className="admin-field">Mã người dùng<input className="input" name="userId" defaultValue={values.userId} /></label>
      <label className="admin-field">Loại giao dịch<select className="input" name="type" defaultValue={values.type}><option value="">Tất cả</option>{types.map((type) => <option key={type} value={type}>{statusLabel(type)}</option>)}</select></label>
      <label className="admin-field">Mã tham chiếu<input className="input" name="reference" defaultValue={values.reference} /></label>
      <label className="admin-field">Từ ngày (UTC)<input className="input" type="datetime-local" name="fromUtc" defaultValue={values.fromUtc} /></label>
      <label className="admin-field">Đến ngày (UTC)<input className="input" type="datetime-local" name="toUtc" defaultValue={values.toUtc} /></label>
      <button className="button justify-self-start md:col-span-2 2xl:col-span-3" type="submit">Áp dụng bộ lọc</button>
    </form></Panel>
    {result.items.length === 0 ? <EmptyState title="Không có giao dịch phù hợp" /> : <div className="table-wrap rounded-lg"><table><thead><tr><th>Giao dịch</th><th>Người dùng</th><th>Loại</th><th>Thay đổi</th><th>Số dư</th><th>Ngày tạo</th></tr></thead><tbody>
      {result.items.map((item) => <tr key={item.transactionId}><td><CopyReference label={item.transactionId.slice(0, 8) + "…"} value={item.transactionId} /><p className="muted max-w-xs text-xs">{item.description}</p></td><td><Link className="admin-link" href={`/admin/users/${item.userId}`}>{item.displayName ?? item.username}</Link></td><td>{statusLabel(item.type)}<p className="code muted text-xs">Mã {item.reference.slice(0, 8)}…</p></td><td className={item.amount >= 0 ? "text-emerald-300" : "text-rose-300"}>{item.amount >= 0 ? "+" : ""}{formatNumber(item.amount)}</td><td>{formatNumber(item.balanceBefore)} → {formatNumber(item.balanceAfter)}</td><td>{formatDate(item.createdAtUtc)}</td></tr>)}
    </tbody></table></div>}
    <Pagination page={result.page} totalPages={result.totalPages} pathname="/admin/wallet-transactions" params={pageParams} />
  </>;
}
