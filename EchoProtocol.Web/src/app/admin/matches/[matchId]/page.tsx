import Link from "next/link";
import { PageHeader, Panel, StatCard } from "@/components/ui/panel";
import { StatusBadge } from "@/components/ui/status-badge";
import { difficultyLabel, mapLabel } from "@/lib/utils/game-labels";
import { adminApi } from "@/lib/api/server-api";
import { ModerationPanel } from "@/components/admin/moderation-panel";
import { AdaptiveMatchPanel } from "@/components/admin/adaptive-match-panel";
import { formatDate } from "@/lib/utils/format";

const outcomes: Record<string, string> = { WIN: "Thắng", LOSE: "Thua" };

export default async function MatchDetailPage({ params }: { params: Promise<{ matchId: string }> }) {
  const { matchId } = await params;
  const [{ match, players }, history, adaptive] = await Promise.all([
    adminApi.match(matchId), adminApi.moderationHistory(matchId), adminApi.matchAdaptive(matchId),
  ]);
  const stoppedByAdmin = history.some(
    (item) => item.action === "STOP_MATCH",
  );

  const status = stoppedByAdmin ? "Stopped" : match.status;
  const flagged = history.find((item) => item.action === "FLAG_MATCH" || item.action === "CLEAR_MATCH")?.action === "FLAG_MATCH";
  const actions = [flagged ? "CLEAR_MATCH" : "FLAG_MATCH", ...(match.status === "Ended" ? [] : ["STOP_MATCH"])] as Array<"FLAG_MATCH" | "CLEAR_MATCH" | "STOP_MATCH">;

  return <>
    <Link className="mb-5 inline-block text-sm text-red-300 hover:underline" href="/admin/matches">← Phòng và trận đấu</Link>
    <PageHeader title="Chi tiết phòng" description={match.matchId} />
    <div className="grid gap-4 sm:grid-cols-3">
      <StatCard label="Trạng thái phòng" value={<StatusBadge status={status} />} />
      <StatCard label="Chưa báo rời" value={`${match.connectedCount} / ${players.length}`} />
      <StatCard label="Xác nhận kết quả" value={match.outcome ? `Đã lưu kết quả trận · ${outcomes[match.outcome] ?? match.outcome}` : "Chưa có kết quả từ máy chủ"} />
    </div>
    <Panel className="mt-5"><dl className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
      <div><dt className="eyebrow">Chủ phòng</dt><dd className="mt-1"><Link className="text-red-300 hover:underline" href={`/admin/users/${match.hostUserId}`}>{match.hostUsername || match.hostUserId}</Link></dd></div>
      <div><dt className="eyebrow">Bản đồ</dt><dd className="mt-1">{mapLabel(match.mapId)}
        {match.mapId && <details className="muted mt-1 text-xs"><summary className="cursor-pointer">Mã bản đồ</summary><code>{match.mapId}</code></details>}
      </dd></div>
      <div><dt className="eyebrow">Độ khó</dt><dd className="mt-1">{difficultyLabel(match.difficulty)}</dd></div>
      <div><dt className="eyebrow">Bắt đầu</dt><dd className="mt-1">{formatDate(match.startedAtUtc)}</dd></div>
      <div><dt className="eyebrow">Kết thúc</dt><dd className="mt-1">{formatDate(match.endedAtUtc)}</dd></div>
      <div><dt className="eyebrow">Quyền host hết hạn</dt><dd className="mt-1">{formatDate(match.leaseExpiresAtUtc)}</dd></div>
    </dl></Panel>
    <Panel className="mt-5"><h2 className="mb-4 text-lg font-semibold text-white">Người chơi trong phòng</h2>
      <p className="muted mb-4 text-sm">Trạng thái dựa trên cập nhật từ host, không phải tín hiệu trực tuyến tức thời.</p>
      {players.length === 0 ? <p className="muted">Chưa có người chơi.</p> : <div className="table-wrap rounded-lg"><table><thead><tr><th>Người chơi</th><th>Vai trò</th><th>Trạng thái</th><th>Tham gia trận</th><th>Gián đoạn trong trận</th><th>Vào phòng</th><th>Cập nhật cuối</th><th>Rời phòng</th></tr></thead><tbody>
        {players.map((player) => <tr key={player.userId}>
          <td><Link className="text-red-300 hover:underline" href={`/admin/users/${player.userId}`}>{player.displayName || player.username}</Link><p className="code muted text-xs">{player.userId}</p></td>
          <td>{player.userId === match.hostUserId ? "Chủ phòng" : "Người chơi"}</td>
          <td>{player.disconnectedAtUtc ? "Đã rời" : "Chưa báo rời"}</td>
          <td>{player.participated ? "Có" : "Chưa"}</td>
          <td>{player.hadGameplayDisconnect ? "Có" : "Không"}</td>
          <td>{formatDate(player.boundAtUtc)}</td><td>{formatDate(player.lastSeenAtUtc)}</td><td>{formatDate(player.disconnectedAtUtc)}</td>
        </tr>)}
      </tbody></table></div>}
    </Panel>
    <AdaptiveMatchPanel audit={adaptive} matchId={matchId} />
    <ModerationPanel key={actions.join()} targetId={matchId} actions={actions} history={history} />
  </>;
}
