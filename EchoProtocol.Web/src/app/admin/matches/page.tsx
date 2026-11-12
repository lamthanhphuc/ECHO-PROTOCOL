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
  const result = invalid ? null : await adminApi.matches(queryString({ page, pageSize: 20, matchId, userId, status, flaggedOnly: flaggedOnly ? "true" : "" }));

  return <>
    <PageHeader title="Phòng và trận đấu" description="Theo dõi phòng, người chơi và lịch sử trận đấu." />
    <Panel className="mb-5"><form action="/admin/matches" className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
      <label className="admin-field">Trạng thái<select className="input" name="status" defaultValue={status}><option value="">Tất cả</option><option value="Lobby">Phòng chờ</option><option value="InMatch">Đang diễn ra</option><option value="Ended">Đã kết thúc hoặc dừng</option></select></label>
      <label className="admin-field">Mã trận đấu<input className="input" name="matchId" defaultValue={matchId} placeholder="Nhập mã trận đấu" /></label>
      <label className="admin-field">Mã người chơi<input className="input" name="userId" defaultValue={userId} placeholder="Nhập mã người chơi" /></label>
      <label className="flex items-center gap-2 text-sm"><input type="checkbox" name="flaggedOnly" value="true" defaultChecked={flaggedOnly} />Chỉ phòng cần kiểm tra</label>
      <button className="button justify-self-start md:col-span-2 xl:col-span-3" type="submit">Tra cứu</button>
    </form></Panel>
    {invalid ? <EmptyState title="Mã tra cứu không hợp lệ" /> : result?.items.length === 0 ? <EmptyState title="Không có trận đấu phù hợp" /> :
      <div className="table-wrap rounded-lg"><table><thead><tr><th>Phòng</th><th>Chủ phòng</th><th>Bản đồ</th><th>Độ khó</th><th>Người chơi</th><th>Trạng thái phòng</th><th>Xác nhận kết quả</th><th>Bắt đầu</th><th>Kết thúc</th></tr></thead><tbody>
        {result?.items.map((item) => <tr key={item.matchId}>
          <td><Link className="code text-xs text-red-300 hover:underline" href={`/admin/matches/${item.matchId}`} title={item.matchId}>{item.matchId.slice(0, 8)}…</Link>{item.isFlagged && <p className="mt-1 text-xs text-amber-300">Cần kiểm tra</p>}</td>
          <td><Link className="hover:underline" href={`/admin/users/${item.hostUserId}`}>{item.hostUsername || item.hostUserId.slice(0, 8)}</Link></td>
          <td>{mapLabel(item.mapId)}</td><td>{difficultyLabel(item.difficulty)}</td>
          <td>{item.playerCount || item.connectedCount}</td><td><StatusBadge status={item.status} /></td>
          <td>{item.outcome ? <>{outcomeNames[item.outcome] ?? item.outcome}<p className="muted text-xs">Đã lưu kết quả trận</p></> : "Chưa có kết quả từ máy chủ"}</td>
          <td>{formatDate(item.startedAtUtc)}</td><td>{formatDate(item.endedAtUtc)}</td>
        </tr>)}
      </tbody></table></div>}
    {result && <Pagination page={result.page} totalPages={result.totalPages} pathname="/admin/matches" params={{ matchId, userId, status, flaggedOnly: flaggedOnly ? "true" : "" }} />}
  </>;
}
