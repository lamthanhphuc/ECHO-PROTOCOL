import Link from "next/link";
import { EmptyState } from "@/components/ui/empty-state";
import { Pagination } from "@/components/ui/pagination";
import { PageHeader, Panel } from "@/components/ui/panel";
import { StatusBadge } from "@/components/ui/status-badge";
import { adminApi } from "@/lib/api/server-api";
import type { SearchParams } from "@/lib/types/common";
import { formatAdminDate as formatDate, param, queryString } from "@/lib/utils/format";
import { difficultyLabel, mapLabel } from "@/lib/utils/game-labels";

const outcomeNames: Record<string, string> = { WIN: "Thắng", LOSE: "Thua" };

export default async function MatchesPage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const raw = await searchParams;
  const matchId = param(raw.matchId).trim();
  const userId = param(raw.userId).trim();
  const requestedStatus = param(raw.status);
  const status = ["Lobby", "InMatch", "Ended"].includes(requestedStatus) ? requestedStatus : "";
  const flaggedOnly = param(raw.flaggedOnly) === "true";
  const page = Math.max(1, Number(param(raw.page)) || 1);
  const invalid = [matchId, userId].some((value) => value && !/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value));
  const [result, rooms] = await Promise.all([
    invalid ? Promise.resolve(null) : adminApi.matches(queryString({ page, pageSize: 20, matchId, userId, status, flaggedOnly: flaggedOnly ? "true" : "" })),
    adminApi.rooms(),
  ]);
  const showRooms = !matchId && !userId && !status && !flaggedOnly && page === 1;
  const roomMatchIds = new Set(showRooms ? rooms.map((room) => room.currentMatchId) : []);
  const roomByMatchId = new Map(rooms.filter((room) => room.currentMatchId).map((room) => [room.currentMatchId, room.roomInstanceId]));
  const matches = result?.items.filter((item) => !roomMatchIds.has(item.matchId)) ?? [];

  return <>
    <PageHeader title="Phòng và trận đấu" description="Xem phiên phòng, người chơi và các trận trong cùng một nơi." />
    <Panel className="mb-5"><h2 className="admin-section-title mb-3">Tra cứu phòng</h2><form action="/admin/matches" className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
      <label className="admin-field">Trạng thái<select className="input" name="status" defaultValue={status}><option value="">Tất cả</option><option value="Lobby">Phòng chờ</option><option value="InMatch">Đang diễn ra</option><option value="Ended">Đã kết thúc hoặc dừng</option></select></label>
      <label className="admin-field">Mã trận đấu<input className="input" name="matchId" defaultValue={matchId} placeholder="Nhập mã trận đấu" /></label>
      <label className="admin-field">Mã người chơi<input className="input" name="userId" defaultValue={userId} placeholder="Nhập mã người chơi" /></label>
      <label className="flex items-center gap-2 text-sm"><input type="checkbox" name="flaggedOnly" value="true" defaultChecked={flaggedOnly} />Chỉ phòng cần kiểm tra</label>
      <button className="button justify-self-start md:col-span-2 xl:col-span-3" type="submit">Tra cứu</button>
    </form></Panel>
    {invalid ? <EmptyState title="Mã tra cứu không hợp lệ" /> : (showRooms ? rooms.length === 0 && matches.length === 0 : matches.length === 0) ? <EmptyState title="Không có phòng hoặc trận đấu phù hợp" /> :
      <div className="table-wrap rounded-lg"><table className="min-w-[1500px] whitespace-nowrap"><thead><tr><th>Phòng / trận đấu</th><th>Chủ phòng</th><th>Bản đồ</th><th>Độ khó</th><th>Người chơi</th><th>Trạng thái</th><th>Kết quả trận</th><th>Bắt đầu / tạo lúc</th><th>Kết thúc / đóng lúc</th></tr></thead><tbody>
        {showRooms && rooms.map((room) => {
          const current = result?.items.find((item) => item.matchId === room.currentMatchId);
          return <tr key={room.roomInstanceId}>
            <td><Link className="admin-link font-semibold" href={`/admin/rooms/${room.roomInstanceId}`}>{room.fusionSessionName}</Link>
              {room.currentMatchId && <p><Link className="admin-link text-xs" href={`/admin/matches/${room.currentMatchId}`}>Xem trận</Link></p>}</td>
            <td><Link className="admin-link" href={`/admin/users/${room.hostUserId}`}>{current?.hostUsername || room.hostUserId.slice(0, 8)}</Link></td>
            <td>{current ? mapLabel(current.mapId) : "—"}</td><td>{current ? difficultyLabel(current.difficulty) : "—"}</td>
            <td>{current ? current.playerCount || current.connectedCount : "—"}</td>
            <td>{room.closedAtUtc ? <span title="Đã đóng trên backend; chưa xác nhận Photon">Đã đóng</span> : current ? <StatusBadge status={current.status} /> : room.activatedAtUtc ? <span title="Backend chưa đóng; kết nối Photon chưa xác minh">Chưa đóng</span> : "Chờ kích hoạt"}</td>
            <td>{current?.outcome ? outcomeNames[current.outcome] ?? current.outcome : "—"}</td>
            <td>{formatDate(room.createdAtUtc)}</td><td>{formatDate(room.closedAtUtc)}</td>
          </tr>;
        })}
        {matches.map((item) => <tr key={item.matchId}>
          <td><Link className="admin-link font-semibold" href={`/admin/matches/${item.matchId}`}>{item.fusionSessionName || `Trận tại ${mapLabel(item.mapId)}`}</Link>
            {(item.roomSessionId || roomByMatchId.has(item.matchId)) && <p><Link className="admin-link text-xs" href={`/admin/rooms/${item.roomSessionId || roomByMatchId.get(item.matchId)}`}>Xem phòng</Link></p>}
            {item.isFlagged && <p className="mt-1 text-xs text-amber-300">Cần kiểm tra</p>}</td>
          <td><Link className="admin-link" href={`/admin/users/${item.hostUserId}`}>{item.hostUsername || item.hostUserId.slice(0, 8)}</Link></td>
          <td>{mapLabel(item.mapId)}</td><td>{difficultyLabel(item.difficulty)}</td>
          <td>{item.playerCount || item.connectedCount}</td><td><StatusBadge status={item.status} /></td>
          <td>{item.outcome ? (outcomeNames[item.outcome] ?? item.outcome) : item.status === "Ended" ? "Chưa có kết quả" : "—"}</td>
          <td>{formatDate(item.startedAtUtc)}</td><td>{formatDate(item.endedAtUtc)}</td>
        </tr>)}
      </tbody></table></div>}
    {result && <Pagination page={result.page} totalPages={result.totalPages} pathname="/admin/matches" params={{ matchId, userId, status, flaggedOnly: flaggedOnly ? "true" : "" }} />}
  </>;
}
