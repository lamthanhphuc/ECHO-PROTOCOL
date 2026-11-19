import Link from "next/link";
import { EmptyState } from "@/components/ui/empty-state";
import { Pagination } from "@/components/ui/pagination";
import { PageHeader, Panel } from "@/components/ui/panel";
import { StatusBadge } from "@/components/ui/status-badge";
import { TableFilterPopover } from "@/components/admin/table-filter-popover";
import { adminApi } from "@/lib/api/server-api";
import type { SearchParams } from "@/lib/types/common";
import { formatAdminDate as formatDate, param, queryString } from "@/lib/utils/format";
import { difficultyLabel, mapLabel } from "@/lib/utils/game-labels";

const outcomeNames: Record<string, string> = { WIN: "Thắng", LOSE: "Thua" };

const options = {
  playerCounts: [1, 2, 3, 4].map((value) => ({ value: String(value), label: String(value) })),
  statuses: [{ value: "Lobby", label: "Phòng chờ" }, { value: "InMatch", label: "Đang diễn ra" }, { value: "Ended", label: "Đã kết thúc" }],
  outcomes: Object.entries(outcomeNames).map(([value, label]) => ({ value, label })),
};

function ChoiceFilter({ name, label, selected, choices }: { name: string; label: string; selected: string[]; choices: { value: string; label: string }[] }) {
  return <TableFilterPopover id={`filter-${name}`} label={label} active={selected.length > 0}>
    <div className="max-h-52 space-y-2 overflow-y-auto normal-case tracking-normal">
      {choices.map(({ value, label: choice }) => <label key={value} className="flex cursor-pointer items-center gap-2 text-sm font-normal">
        <input form="match-table-filters" type="checkbox" name={name} value={value} defaultChecked={selected.includes(value)} />{choice}
      </label>)}
    </div>
  </TableFilterPopover>;
}

function DateFilter({ label, from, to, dates }: { label: string; from: string; to: string; dates: Record<string, string> }) {
  return <TableFilterPopover id={`filter-${from}`} label={label} active={Boolean(dates[from] || dates[to])}>
    <div className="space-y-2 normal-case tracking-normal">
      <label className="block text-sm font-normal">Từ ngày<input form="match-table-filters" className="input mt-1" type="date" name={from} defaultValue={dates[from]} /></label>
      <label className="block text-sm font-normal">Đến ngày<input form="match-table-filters" className="input mt-1" type="date" name={to} defaultValue={dates[to]} /></label>
    </div>
  </TableFilterPopover>;
}

export default async function MatchesPage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const raw = await searchParams;
  const matchId = param(raw.matchId).trim();
  const userId = param(raw.userId).trim();
  const requestedStatus = param(raw.status);
  const status = ["Lobby", "InMatch", "Ended"].includes(requestedStatus) ? requestedStatus : "";
  const flaggedOnly = param(raw.flaggedOnly) === "true";
  const filterNames = ["maps", "difficulties", "playerCounts", "statuses", "outcomes"] as const;
  const selected = Object.fromEntries(filterNames.map((name) => [name, (Array.isArray(raw[name]) ? raw[name] : param(raw[name]).split(",")).filter(Boolean)])) as Record<typeof filterNames[number], string[]>;
  const dates = Object.fromEntries(["startedFrom", "startedTo", "endedFrom", "endedTo"].map((name) => [name, param(raw[name])])) as Record<string, string>;
  const filters = Object.fromEntries(filterNames.map((name) => [name, selected[name].join(",")]));
  const hasFilters = Object.values(filters).some(Boolean) || Object.values(dates).some(Boolean);
  const page = Math.max(1, Number(param(raw.page)) || 1);
  const invalid = [matchId, userId].some((value) => value && !/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value));
  const [result, rooms, content] = await Promise.all([
    invalid ? Promise.resolve(null) : adminApi.matches(queryString({ page, pageSize: 20, matchId, userId, status, flaggedOnly: flaggedOnly ? "true" : "", ...filters, ...dates })),
    adminApi.rooms(),
    adminApi.matchContent(),
  ]);
  const showRooms = !matchId && !userId && !status && !flaggedOnly && !hasFilters && page === 1;
  const roomMatchIds = new Set(showRooms ? rooms.map((room) => room.currentMatchId) : []);
  const matches = result?.items.filter((item) => !roomMatchIds.has(item.matchId)) ?? [];

  return <>
    <PageHeader title="Phòng và trận đấu" />
    <Panel className="mb-5"><h2 className="admin-section-title mb-3">Tra cứu phòng</h2><form action="/admin/matches" className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
      <label className="admin-field">Mã trận đấu<input className="input" name="matchId" defaultValue={matchId} placeholder="Nhập mã trận đấu" /></label>
      <label className="admin-field">Mã người chơi<input className="input" name="userId" defaultValue={userId} placeholder="Nhập mã người chơi" /></label>
      <label className="flex items-center gap-2 text-sm"><input type="checkbox" name="flaggedOnly" value="true" defaultChecked={flaggedOnly} />Chỉ phòng cần kiểm tra</label>
      {Object.entries({ ...filters, ...dates }).filter(([, value]) => value).map(([name, value]) => <input key={name} type="hidden" name={name} value={value} />)}
      <button className="button justify-self-start md:col-span-2 xl:col-span-3" type="submit">Tra cứu</button>
    </form></Panel>
    <form id="match-table-filters" action="/admin/matches">
      {matchId && <input type="hidden" name="matchId" value={matchId} />}
      {userId && <input type="hidden" name="userId" value={userId} />}
      {flaggedOnly && <input type="hidden" name="flaggedOnly" value="true" />}
    </form>
    {invalid ? <EmptyState title="Mã tra cứu không hợp lệ" /> :
      <div className="table-wrap rounded-lg"><table className="min-w-[1500px] whitespace-nowrap"><thead><tr>
        <th>Mã phòng</th><th>Chủ phòng</th>
        <th>Bản đồ <ChoiceFilter name="maps" label="Bản đồ" selected={selected.maps} choices={[...new Set(content.entries.map((entry) => entry.map))].map((value) => ({ value, label: mapLabel(value) }))} /></th>
        <th>Độ khó <ChoiceFilter name="difficulties" label="Độ khó" selected={selected.difficulties} choices={content.difficulties.map((value) => ({ value, label: difficultyLabel(value) }))} /></th>
        <th>Người chơi <ChoiceFilter name="playerCounts" label="Người chơi" selected={selected.playerCounts} choices={options.playerCounts} /></th>
        <th>Trạng thái <ChoiceFilter name="statuses" label="Trạng thái" selected={selected.statuses} choices={options.statuses} /></th>
        <th>Kết quả trận <ChoiceFilter name="outcomes" label="Kết quả trận" selected={selected.outcomes} choices={options.outcomes} /></th>
        <th>Bắt đầu <DateFilter label="Bắt đầu" from="startedFrom" to="startedTo" dates={dates} /></th>
        <th>Kết thúc <DateFilter label="Kết thúc" from="endedFrom" to="endedTo" dates={dates} /></th>
      </tr></thead><tbody>
        {(showRooms ? rooms.length === 0 && matches.length === 0 : matches.length === 0) && <tr><td colSpan={9} className="py-10 text-center muted">Không có phòng hoặc trận đấu phù hợp</td></tr>}
        {showRooms && rooms.map((room) => {
          const current = result?.items.find((item) => item.matchId === room.currentMatchId);
          return <tr key={room.roomInstanceId}>
            <td>
              <Link
                className="admin-link font-semibold"
                href={current ? `/admin/rooms/${room.roomInstanceId}?matchId=${current.matchId}` : `/admin/rooms/${room.roomInstanceId}`}
              >
                {room.fusionSessionName}
              </Link>
            </td>
            <td><Link className="admin-link" href={`/admin/users/${room.hostUserId}`}>{current?.hostUsername || room.hostUserId.slice(0, 8)}</Link></td>
            <td>{current ? mapLabel(current.mapId) : "—"}</td><td>{current ? difficultyLabel(current.difficulty) : "—"}</td>
            <td>{current ? current.playerCount || current.connectedCount : "—"}</td>
            <td>{room.closedAtUtc ? <span title="Đã đóng trên backend; chưa xác nhận Photon"><StatusBadge status="Closed" /></span> : current ? <StatusBadge status={current.status} /> : room.activatedAtUtc ? <span title="Backend chưa đóng; kết nối Photon chưa xác minh"><StatusBadge status="Lobby" /></span> : <StatusBadge status="INACTIVE" />}</td>
            <td>{current?.outcome ? outcomeNames[current.outcome] ?? current.outcome : "—"}</td>
            <td>{formatDate(room.createdAtUtc)}</td><td>{formatDate(room.closedAtUtc)}</td>
          </tr>;
        })}
        {matches.map((item) => <tr key={item.matchId}>
          <td>
            {item.roomSessionId || item.fusionSessionName ? (
              <Link
                className="admin-link font-semibold"
                href={
                  item.roomSessionId
                    ? `/admin/rooms/${item.roomSessionId}?matchId=${item.matchId}`
                    : `/admin/matches/${item.matchId}`
                }
              >
                {item.fusionSessionName || item.roomSessionId}
              </Link>
            ) : (
              "—"
            )}
          </td>
          <td><Link className="admin-link" href={`/admin/users/${item.hostUserId}`}>{item.hostUsername || item.hostUserId.slice(0, 8)}</Link></td>
          <td>{mapLabel(item.mapId)}</td><td>{difficultyLabel(item.difficulty)}</td>
          <td>{item.playerCount || item.connectedCount}</td>
          <td>
            <StatusBadge status={item.status} />
            {item.isFlagged && (
              <p className="mt-1 text-xs text-amber-300">
                Cần kiểm tra
              </p>
            )}
          </td>
          <td>{item.outcome ? (outcomeNames[item.outcome] ?? item.outcome) : item.status === "Ended" ? "Chưa có kết quả" : "—"}</td>
          <td>{formatDate(item.startedAtUtc)}</td><td>{formatDate(item.endedAtUtc)}</td>
        </tr>)}
      </tbody></table></div>}
    {result && <Pagination page={result.page} totalPages={result.totalPages} pathname="/admin/matches" params={{ matchId, userId, status, flaggedOnly: flaggedOnly ? "true" : "", ...filters, ...dates }} />}
  </>;
}
