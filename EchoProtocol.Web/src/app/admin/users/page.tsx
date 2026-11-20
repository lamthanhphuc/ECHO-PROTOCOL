import Link from "next/link";
import { EmptyState } from "@/components/ui/empty-state";
import { Pagination } from "@/components/ui/pagination";
import { PageHeader, Panel } from "@/components/ui/panel";
import { StatusBadge, statusLabel } from "@/components/ui/status-badge";
import { adminApi } from "@/lib/api/server-api";
import type { SearchParams } from "@/lib/types/common";
import { formatAdminDate as formatDate, formatNumber, param, queryString } from "@/lib/utils/format";

export default async function UsersPage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const raw = await searchParams;
  const page = Math.max(1, Number(param(raw.page)) || 1);
  const search = param(raw.search);
  const result = await adminApi.users(queryString({ page, pageSize: 20, search }));
  const params: Record<string, string> = search ? { search } : {};

  return <>
    <PageHeader title="Người dùng" />
    <Panel className="mb-5">
      <h2 className="admin-section-title mb-3">Tìm tài khoản</h2>
      <form className="flex flex-col gap-3 sm:flex-row" action="/admin/users">
        <label className="sr-only" htmlFor="admin-user-search">Tên tài khoản, tên hiển thị hoặc email</label>
        <input id="admin-user-search" className="input" name="search" defaultValue={search} placeholder="Tên tài khoản, tên hiển thị hoặc email" />
        <button className="button" type="submit">Tìm kiếm</button>
      </form>
    </Panel>
    {result.items.length === 0 ? <EmptyState title="Không tìm thấy người dùng" /> : <div className="table-wrap rounded-lg"><table><thead><tr><th>Người dùng</th><th>Vai trò</th><th>Tài khoản</th><th>Kết nối phiên game</th><th>Hoạt động gần nhất</th><th>Số dư</th><th>Trận / Thắng</th><th>Ngày tạo</th></tr></thead><tbody>
      {result.items.map((user) => <tr key={user.userId}><td><Link className="admin-link" href={`/admin/users/${user.userId}`}>{user.displayName ?? user.username}</Link></td><td>{statusLabel(user.role)}</td><td><StatusBadge status={user.status} /></td><td>{user.isConnectedToSession || user.hasRecentGameHeartbeat ? "Online" : user.lastGameHeartbeatAtUtc ? "Offline" : "Chưa ghi nhận"}</td><td>{formatDate([user.lastGameHeartbeatAtUtc, user.lastSessionActivityAtUtc].filter(Boolean).sort().at(-1))}</td><td>{formatNumber(user.walletBalance)}</td><td>{formatNumber(user.totalMatches)} / {formatNumber(user.totalWins)}</td><td>{formatDate(user.createdAtUtc)}</td></tr>)}
    </tbody></table></div>}
    <Pagination page={result.page} totalPages={result.totalPages} pathname="/admin/users" params={params} />
  </>;
}
