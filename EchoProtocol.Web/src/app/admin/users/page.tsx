import Link from "next/link";
import { EmptyState } from "@/components/ui/empty-state";
import { Pagination } from "@/components/ui/pagination";
import { PageHeader, Panel } from "@/components/ui/panel";
import { StatusBadge } from "@/components/ui/status-badge";
import { adminApi } from "@/lib/api/server-api";
import type { SearchParams } from "@/lib/types/common";
import { formatDate, formatNumber, param, queryString } from "@/lib/utils/format";

export default async function UsersPage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const raw = await searchParams;
  const page = Math.max(1, Number(param(raw.page)) || 1);
  const search = param(raw.search);
  const result = await adminApi.users(queryString({ page, pageSize: 20, search }));
  const params: Record<string, string> = search ? { search } : {};

  return <>
    <PageHeader eyebrow="Identity registry" title="Người dùng" description="Tra cứu theo username, display name hoặc email." />
    <Panel className="mb-5">
      <form className="flex flex-col gap-3 sm:flex-row" action="/admin/users">
        <input className="input" name="search" defaultValue={search} placeholder="Username, display name hoặc email" />
        <button className="button" type="submit">Tìm kiếm</button>
      </form>
    </Panel>
    {result.items.length === 0 ? <EmptyState title="Không tìm thấy người dùng" /> : <div className="table-wrap rounded-lg"><table><thead><tr><th>User</th><th>Role</th><th>Status</th><th>Wallet</th><th>Matches / Wins</th><th>Created</th></tr></thead><tbody>
      {result.items.map((user) => <tr key={user.userId}><td><Link className="text-emerald-300 hover:underline" href={`/admin/users/${user.userId}`}>{user.displayName ?? user.username}</Link><p className="muted text-xs">{user.email}</p></td><td>{user.role}</td><td><StatusBadge status={user.status} /></td><td>{formatNumber(user.walletBalance)}</td><td>{formatNumber(user.totalMatches)} / {formatNumber(user.totalWins)}</td><td>{formatDate(user.createdAtUtc)}</td></tr>)}
    </tbody></table></div>}
    <Pagination page={result.page} totalPages={result.totalPages} pathname="/admin/users" params={params} />
  </>;
}
