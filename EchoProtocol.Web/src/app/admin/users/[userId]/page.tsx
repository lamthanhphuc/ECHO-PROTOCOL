import Link from "next/link";
import { PageHeader, Panel, StatCard } from "@/components/ui/panel";
import { StatusBadge, statusLabel } from "@/components/ui/status-badge";
import { AdminCreditGrant } from "@/components/admin/admin-credit-grant";
import { ModerationPanel } from "@/components/admin/moderation-panel";
import { adminApi } from "@/lib/api/server-api";
import { formatDate, formatNumber } from "@/lib/utils/format";

export default async function UserDetailPage({ params }: { params: Promise<{ userId: string }> }) {
  const { userId } = await params;
  const [user, history] = await Promise.all([adminApi.user(userId), adminApi.moderationHistory(userId)]);
  const flagged = history.find((item) => item.action === "FLAG_USER" || item.action === "CLEAR_USER")?.action === "FLAG_USER";
  return <>
    <PageHeader title={user.displayName ?? user.username} description={user.email} />
    <div className="grid gap-4 sm:grid-cols-3">
      <StatCard label="Số dư" value={formatNumber(user.walletBalance)} />
      <StatCard label="Tổng số trận" value={formatNumber(user.totalMatches)} />
      <StatCard label="Số trận thắng" value={formatNumber(user.totalWins)} />
    </div>
    <AdminCreditGrant
      userId={user.userId}
      currentBalance={user.walletBalance ?? 0}
    />
    <Panel className="mt-5">
      <dl className="grid gap-4 sm:grid-cols-2">
        <div><dt className="eyebrow">Mã người dùng</dt><dd className="code mt-1 break-all">{user.userId}</dd></div>
        <div><dt className="eyebrow">Tên tài khoản</dt><dd className="mt-1">{user.username}</dd></div>
        <div><dt className="eyebrow">Vai trò</dt><dd className="mt-1">{statusLabel(user.role)}</dd></div>
        <div><dt className="eyebrow">Trạng thái</dt><dd className="mt-1"><StatusBadge status={user.status} /></dd></div>
        <div><dt className="eyebrow">Ngày tạo</dt><dd className="mt-1">{formatDate(user.createdAtUtc)}</dd></div>
      </dl>
      <div className="mt-6 flex flex-wrap gap-3">
        <Link className="button button-secondary" href={`/admin/matches?userId=${user.userId}`}>Phòng và trận đấu</Link>
        <Link className="button button-secondary" href={`/admin/payments?userId=${user.userId}`}>Thanh toán</Link>
        <Link className="button button-secondary" href={`/admin/wallet-transactions?userId=${user.userId}`}>Lịch sử số dư</Link>
        <Link className="button button-secondary" href={`/admin/purchases?userId=${user.userId}`}>Giao dịch mua</Link>
      </div>
    </Panel>
    {user.role === "PLAYER" && <ModerationPanel key={`${user.status}-${flagged}`} targetId={userId}
      actions={[flagged ? "CLEAR_USER" : "FLAG_USER", user.status === "LOCKED" ? "UNLOCK_USER" : "LOCK_USER"]} history={history} />}
  </>;
}
