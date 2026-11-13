import Link from "next/link";
import { PageHeader, Panel, StatCard } from "@/components/ui/panel";
import { StatusBadge, statusLabel } from "@/components/ui/status-badge";
import { AdminCreditGrant } from "@/components/admin/admin-credit-grant";
import { ModerationPanel } from "@/components/admin/moderation-panel";
import { adminApi } from "@/lib/api/server-api";
import { formatAdminDate as formatDate, formatNumber, param } from "@/lib/utils/format";
import type { SearchParams } from "@/lib/types/common";

export default async function UserDetailPage({ params, searchParams }: { params: Promise<{ userId: string }>; searchParams: Promise<SearchParams> }) {
  const { userId } = await params;
  const requestedPage = Number(param((await searchParams).profilePage));
  const page = Number.isInteger(requestedPage) && requestedPage >= 1 && requestedPage <= 10000 ? requestedPage : 1;
  const [user, history, ai] = await Promise.all([adminApi.user(userId), adminApi.moderationHistory(userId), adminApi.playerAIHistory(userId, page)]);
  const profile = ai.profile;
  const dimensions: Record<string, string> = { Survival: "Sinh tồn", Noise: "Tiếng ồn", Objective: "Mục tiêu", ToolUsage: "Dùng công cụ" };
  const flagged = history.find((item) => item.action === "FLAG_USER" || item.action === "CLEAR_USER")?.action === "FLAG_USER";
  return <>
    <PageHeader title={user.displayName ?? user.username} description={"Tài khoản " + user.username} />
    <nav aria-label="Mục tài khoản" className="mb-4 flex flex-wrap gap-4 text-sm text-red-300"><a href="#ai-history">Hồ sơ AI</a><a href="#transactions">Giao dịch</a><a href="#intervention">Can thiệp</a></nav>
    <div className="grid gap-4 sm:grid-cols-3">
      <StatCard label="Số dư" value={formatNumber(user.walletBalance)} />
      <StatCard label="Tổng số trận" value={formatNumber(user.totalMatches)} />
      <StatCard label="Số trận thắng" value={formatNumber(user.totalWins)} />
    </div>

    <Panel className="mt-5" >
      <h2 id="ai-history" className="admin-section-title mb-2">Hồ sơ AI và lịch sử điểm</h2>
      {profile ? <>
        <p className="muted mb-2 text-sm">Công thức {profile.profileFormulaVersion} · Lần cập nhật {profile.profileRevision}</p>
        <ul className="mb-3 grid gap-2 text-sm sm:grid-cols-2">
          {([
            ["Sinh tồn", profile.survivalScore, profile.survivalStatus, profile.survivalSampleCount],
            ["Tiếng ồn", profile.noiseScore, profile.noiseStatus, profile.noiseSampleCount],
            ["Mục tiêu", profile.objectiveScore, profile.objectiveStatus, profile.objectiveSampleCount],
            ["Dùng công cụ", profile.toolUsageScore, profile.toolUsageStatus, profile.toolUsageSampleCount],
          ] as const).map(([label, score, status, count]) => <li key={label}>{label} · {count > 0 ? score ?? "—" : "—"} · {count} mẫu · {status}</li>)}
        </ul>
      </> : <p className="muted mb-2 text-sm">Chưa có hồ sơ AI hiện hành.</p>}
      <p className="muted mb-2 text-sm">Điểm quan sát từng trận và trạng thái đóng góp vào hồ sơ.</p>
      {ai.rows.length === 0 ? <p className="muted text-sm">Trang này chưa có điểm trận.</p> : <div className="table-wrap"><table>
        <thead><tr><th>Trận</th><th>Chỉ số</th><th>Điểm</th><th>Công thức và cấu hình</th><th>Đóng góp</th></tr></thead>
        <tbody>{ai.rows.map((row) => <tr key={row.id}>
          <td><Link className="admin-link" href={`/admin/matches/${row.matchId}`}>{formatDate(row.matchEndTs)}</Link></td>
          <td>{dimensions[row.dimension] ?? row.dimension}</td><td>{row.score}</td>
          <td>{row.matchScoreFormulaVersion} · {row.normalizationConfigVersion} · {row.alphaConfigVersion}</td>
          <td>{row.status === "Contributing" ? "Đang tính" : "Đã rút"}{row.retractionReason && ` · ${row.retractionReason}`}
            {profile && row.profileLineageId !== profile.profileLineageId && " · Hồ sơ phiên bản trước"}</td>
        </tr>)}</tbody>
      </table></div>}
      <div className="mt-3 flex gap-3 text-sm">
        {page > 1 && <Link href={`?profilePage=${page - 1}#ai-history`}>Trang trước</Link>}
        <span>Trang {page}</span>
        {ai.hasNext && page < 10000 && <Link href={`?profilePage=${page + 1}#ai-history`}>Trang sau</Link>}
      </div>
    </Panel>
    <Panel className="mt-5">
      <h2 id="transactions" className="admin-section-title mb-3">Tài khoản và giao dịch</h2>
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
    <section id="intervention"><h2 className="admin-section-title mt-5">Can thiệp tài khoản</h2>
    <AdminCreditGrant userId={user.userId} recipient={user.displayName ?? user.username} currentBalance={user.walletBalance ?? 0} />
    {user.role === "PLAYER" && <ModerationPanel key={`${user.status}-${flagged}`} targetId={userId}
      actions={[flagged ? "CLEAR_USER" : "FLAG_USER", user.status === "LOCKED" ? "UNLOCK_USER" : "LOCK_USER"]} history={history} />}
    </section>
  </>;
}
