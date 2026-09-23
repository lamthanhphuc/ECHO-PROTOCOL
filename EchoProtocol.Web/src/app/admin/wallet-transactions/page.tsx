import Link from "next/link";
import { EmptyState } from "@/components/ui/empty-state";
import { Pagination } from "@/components/ui/pagination";
import { PageHeader, Panel } from "@/components/ui/panel";
import { adminApi } from "@/lib/api/server-api";
import type { SearchParams } from "@/lib/types/common";
import { formatDate, formatNumber, param, queryString } from "@/lib/utils/format";

const types = ["MATCH_REWARD", "PURCHASE", "PAYMENT_FULFILLMENT"];

export default async function WalletTransactionsPage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const raw = await searchParams;
  const values = { userId: param(raw.userId), type: param(raw.type), reference: param(raw.reference), fromUtc: param(raw.fromUtc), toUtc: param(raw.toUtc) };
  const page = Math.max(1, Number(param(raw.page)) || 1);
  const result = await adminApi.walletTransactions(queryString({ page, pageSize: 20, ...values }));
  const pageParams = Object.fromEntries(Object.entries(values).filter(([, value]) => value));
  return <>
    <PageHeader eyebrow="Immutable ledger" title="Wallet transactions" description="Đọc lịch sử thay đổi balance; website không tự tính hoặc chỉnh số dư." />
    <Panel className="mb-5"><form action="/admin/wallet-transactions" className="grid gap-3 md:grid-cols-3 xl:grid-cols-5">
      <input className="input" name="userId" defaultValue={values.userId} placeholder="User ID" />
      <select className="input" name="type" defaultValue={values.type}><option value="">Mọi type</option>{types.map((type) => <option key={type}>{type}</option>)}</select>
      <input className="input" name="reference" defaultValue={values.reference} placeholder="Reference GUID" />
      <input className="input" type="datetime-local" name="fromUtc" defaultValue={values.fromUtc} aria-label="From UTC" />
      <input className="input" type="datetime-local" name="toUtc" defaultValue={values.toUtc} aria-label="To UTC" />
      <button className="button md:col-span-3 xl:col-span-5" type="submit">Áp dụng bộ lọc</button>
    </form></Panel>
    {result.items.length === 0 ? <EmptyState title="Không có wallet transaction phù hợp" /> : <div className="table-wrap rounded-lg"><table><thead><tr><th>Transaction</th><th>User</th><th>Type</th><th>Change</th><th>Balance</th><th>Created</th></tr></thead><tbody>
      {result.items.map((item) => <tr key={item.transactionId}><td><p className="code">{item.transactionId.slice(0, 8)}…</p><p className="muted max-w-xs text-xs">{item.description}</p></td><td><Link className="hover:underline" href={`/admin/users/${item.userId}`}>{item.displayName ?? item.username}</Link></td><td>{item.type}<p className="code muted text-xs">Ref {item.reference.slice(0, 8)}…</p></td><td className={item.amount >= 0 ? "text-emerald-300" : "text-rose-300"}>{item.amount >= 0 ? "+" : ""}{formatNumber(item.amount)}</td><td>{formatNumber(item.balanceBefore)} → {formatNumber(item.balanceAfter)}</td><td>{formatDate(item.createdAtUtc)}</td></tr>)}
    </tbody></table></div>}
    <Pagination page={result.page} totalPages={result.totalPages} pathname="/admin/wallet-transactions" params={pageParams} />
  </>;
}
