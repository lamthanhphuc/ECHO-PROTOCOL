import Link from "next/link";
import { PageHeader, Panel, StatCard } from "@/components/ui/panel";
import { StatusBadge } from "@/components/ui/status-badge";
import { adminApi } from "@/lib/api/server-api";
import { formatDate, formatNumber } from "@/lib/utils/format";

export default async function UserDetailPage({ params }: { params: Promise<{ userId: string }> }) {
  const { userId } = await params;
  const user = await adminApi.user(userId);
  return <>
    <PageHeader eyebrow="User record" title={user.displayName ?? user.username} description={user.email} />
    <div className="grid gap-4 sm:grid-cols-3">
      <StatCard label="Wallet balance" value={formatNumber(user.walletBalance)} />
      <StatCard label="Total matches" value={formatNumber(user.totalMatches)} />
      <StatCard label="Total wins" value={formatNumber(user.totalWins)} />
    </div>
    <Panel className="mt-5">
      <dl className="grid gap-4 sm:grid-cols-2">
        <div><dt className="eyebrow">User ID</dt><dd className="code mt-1 break-all">{user.userId}</dd></div>
        <div><dt className="eyebrow">Username</dt><dd className="mt-1">{user.username}</dd></div>
        <div><dt className="eyebrow">Role</dt><dd className="mt-1">{user.role}</dd></div>
        <div><dt className="eyebrow">Status</dt><dd className="mt-1"><StatusBadge status={user.status} /></dd></div>
        <div><dt className="eyebrow">Created</dt><dd className="mt-1">{formatDate(user.createdAtUtc)}</dd></div>
      </dl>
      <div className="mt-6 flex flex-wrap gap-3">
        <Link className="button button-secondary" href={`/admin/payments?userId=${user.userId}`}>Thanh toán</Link>
        <Link className="button button-secondary" href={`/admin/wallet-transactions?userId=${user.userId}`}>Wallet ledger</Link>
        <Link className="button button-secondary" href={`/admin/purchases?userId=${user.userId}`}>Purchases</Link>
      </div>
    </Panel>
  </>;
}
