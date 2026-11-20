import Link from "next/link";
import { CopyReference } from "@/components/admin/copy-reference";
import { PageHeader, Panel, StatCard } from "@/components/ui/panel";
import { StatusBadge } from "@/components/ui/status-badge";
import { difficultyLabel, mapLabel } from "@/lib/utils/game-labels";
import { adminApi } from "@/lib/api/server-api";
import { ModerationPanel } from "@/components/admin/moderation-panel";
import { KickPlayersPanel } from "@/components/admin/kick-players-panel";
import { AdaptiveMatchPanel } from "@/components/admin/adaptive-match-panel";
import { formatAdminDate as formatDate } from "@/lib/utils/format";
import { notFound } from "next/navigation";
import type { AdminRoomSession } from "@/lib/types/admin";

const outcomes: Record<string, string> = { WIN: "Thắng", LOSE: "Thua" };

export async function MatchDetailContent({
  matchId,
  roomId,
  room,
  hostUsername,
  roomClosed = false,
}: {
  matchId: string;
  roomId?: string;
  room?: AdminRoomSession;
  hostUsername?: string;
  roomClosed?: boolean;
}) {
  const [{ match, players }, history, adaptive] = await Promise.all([
    adminApi.match(matchId), adminApi.moderationHistory(matchId), adminApi.matchAdaptive(matchId),
  ]);
  if (roomId && match.roomSessionId !== roomId) notFound();
  const adminNames = await adminApi.moderationAdminNames(history);

  const stoppedByAdmin = history.some(
    (item) => item.action === "STOP_MATCH",
  );

  const status = stoppedByAdmin ? "Stopped" : match.status;
  const flagged = history.find((item) => item.action === "FLAG_MATCH" || item.action === "CLEAR_MATCH")?.action === "FLAG_MATCH";
  const actions = [
    flagged ? "CLEAR_MATCH" : "FLAG_MATCH",
    ...(match.status === "Ended" || roomClosed ? [] : ["STOP_MATCH"]),
  ] as Array<"FLAG_MATCH" | "CLEAR_MATCH" | "STOP_MATCH">;

  const locked = adaptive.baseline;
  const activeDecision = adaptive.preMatch.find((item) => item.isCurrent);
  const configuration = locked?.source === "LOCAL" ? "Mặc định trong game" :
    locked?.source === "REMOTE_FIXED" ? "Cấu hình từ Web" : null;
  const resolution = activeDecision?.usedFixedFallback ? "Cấu hình dự phòng" :
    activeDecision?.resolutionMode === "ADAPTIVE" ? "Adaptive AI" :
    activeDecision?.resolutionMode === "FIXED" ? "Cấu hình cố định" : null;
  const overview = [mapLabel(match.mapId), difficultyLabel(match.difficulty),
    locked?.buildVersion && "Bản game " + locked.buildVersion, configuration, resolution].filter(Boolean).join(" · ");
  return (
    <>
      {roomId ? (
        <h2 className="admin-section-title mb-3 mt-5">
          Chi tiết trận đấu
        </h2>
      ) : (
        <>
          <Link className="mb-5 inline-block admin-link" href="/admin/matches">← Phòng và trận đấu</Link>
          <PageHeader title="Chi tiết trận đấu" description={overview} />
        </>
      )}
      <div className="grid gap-4 sm:grid-cols-3">
        <StatCard label="Trạng thái trận" value={<StatusBadge status={status} />} />
        <StatCard label="Người chơi" value={players.length} />
        <StatCard label="Xác nhận kết quả" value={match.outcome ? `Đã lưu kết quả trận · ${outcomes[match.outcome] ?? match.outcome}` : "Chưa có kết quả từ máy chủ"} />
      </div>
      <AdaptiveMatchPanel audit={adaptive} matchId={matchId} />
      <Panel className="mt-5">
        {room && <>
          <CopyReference label="Mã định danh phòng" value={room.roomInstanceId} alwaysVisible />
          <dl className="mt-5 grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
            <div><dt className="eyebrow">Mã phòng</dt><dd>{room.fusionSessionName}</dd></div>
            <div><dt className="eyebrow">Khu vực</dt><dd>{room.actualRegion?.toUpperCase() || (room.region === "auto" ? "Tự động, chưa xác nhận" : room.region.toUpperCase())}</dd></div>
            <div><dt className="eyebrow">Chủ phòng</dt><dd><Link className="admin-link" href={`/admin/users/${room.hostUserId}`}>{hostUsername || room.hostUserId}</Link></dd></div>
            <div><dt className="eyebrow">Tạo lúc</dt><dd>{formatDate(room.createdAtUtc)}</dd></div>
            <div><dt className="eyebrow">Đóng lúc</dt><dd>{formatDate(room.closedAtUtc)}</dd></div>
            <div><dt className="eyebrow">Lý do đóng</dt><dd>{room.closeReason || "—"}</dd></div>
            {room.closedAtUtc && room.closeReason !== "HOST_LEFT" && room.closeReason !== "ROOM_LEASE_EXPIRED" &&
              <div><dt className="eyebrow">Host thực thi</dt><dd>{room.hostExecutedAtUtc ? formatDate(room.hostExecutedAtUtc) : "Chưa có ACK từ Host"}</dd></div>}
          </dl>
          <hr className="my-5 border-[var(--line)]" />
        </>}
        <CopyReference label="Mã trận" value={match.matchId} alwaysVisible />
        <dl className="mt-3 grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
          {!roomId && <div>
            <dt className="eyebrow">Chủ phòng</dt>
            <dd className="mt-1"><Link className="admin-link" href={`/admin/users/${match.hostUserId}`}>{match.hostUsername || match.hostUserId}</Link></dd>
          </div>}
          <div>
            <dt className="eyebrow">Bản đồ</dt>
            <dd className="mt-1">{mapLabel(match.mapId)}</dd>
          </div>
          <div>
            <dt className="eyebrow">Độ khó</dt>
            <dd className="mt-1">{difficultyLabel(match.difficulty)}</dd>
          </div>
          <div>
            <dt className="eyebrow">Bắt đầu</dt>
            <dd className="mt-1">{formatDate(match.startedAtUtc)}</dd>
          </div>
          <div>
            <dt className="eyebrow">Kết thúc</dt>
            <dd className="mt-1">{formatDate(match.endedAtUtc)}</dd>
          </div>
          <div>
            <dt className="eyebrow">Quyền điều phối của chủ phòng hết hạn</dt>
            <dd className="mt-1">{formatDate(match.leaseExpiresAtUtc)}</dd>
          </div>
        </dl>
      </Panel>
      <KickPlayersPanel matchId={matchId} status={match.status} hostUserId={match.hostUserId}
        closed={roomClosed} players={players} history={history} />
      <ModerationPanel key={actions.join()} targetId={matchId} actions={actions} history={history}
        usernames={Object.fromEntries(players.map((player) => [player.userId, player.username]))} adminNames={adminNames} />
    </>
  );
}
