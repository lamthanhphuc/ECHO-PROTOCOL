import Link from "next/link";
import { difficultyLabel, mapLabel } from "@/lib/utils/game-labels";
import { PageHeader, Panel, StatCard } from "@/components/ui/panel";
import { adminApi } from "@/lib/api/server-api";
import { adaptiveReasons } from "@/lib/types/adaptive";
import type { SearchParams } from "@/lib/types/common";
import { param, queryString } from "@/lib/utils/format";

const labels: Record<string, string> = {
  FIXED: "Cấu hình cố định", ADAPTIVE_APPLIED: "AI đã áp dụng điều chỉnh",
  ADAPTIVE_FALLBACK: "AI giữ nguyên hoặc dùng cấu hình dự phòng",
  ADAPTIVE_UNVERIFIED: "AI chưa có xác nhận áp dụng",
  UNKNOWN: "Chưa xác định",
};

function ratio(count: number, total: number) {
  return total ? `${count}/${total} · ${(100 * count / total).toFixed(1)}%` : `Chưa có mẫu`;
}

export default async function BalanceAnalyticsPage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const raw = await searchParams;
  const days = param(raw.days) || "30";
  const map = param(raw.map), difficulty = param(raw.difficulty), teamSize = param(raw.teamSize);
  const baseline = param(raw.baseline), testOnly = param(raw.testOnly) === "true";
  const [data, catalog] = await Promise.all([
    adminApi.balanceAnalytics(queryString({ days, map, difficulty, teamSize, baseline, testOnly: String(testOnly) })),
    adminApi.matchContent(),
  ]);

  return <>
    <PageHeader title="Báo cáo trận đấu" description="So sánh kết quả và bằng chứng áp dụng Adaptive trên các trận có kết quả đã ghi nhận." />
    <Panel>
      <form className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3" action="/admin/balance-analytics">
        <label className="admin-field">Số ngày gần nhất<input className="input" type="number" name="days" min={1} max={90} required defaultValue={days} /></label>
        <label className="admin-field">Bản đồ<select className="input" name="map" defaultValue={map}>
          <option value="">Tất cả</option>{[...new Set(catalog.entries.map((entry) => entry.map))].map((value) => <option key={value} value={value}>{mapLabel(value)}</option>)}
        </select></label>
        <label className="admin-field">Độ khó<select className="input" name="difficulty" defaultValue={difficulty}>
          <option value="">Tất cả</option>{catalog.difficulties.map((value) => <option key={value} value={value}>{difficultyLabel(value)}</option>)}
        </select></label>
        <label className="admin-field">Quy mô đội<select className="input" name="teamSize" defaultValue={teamSize}>
          <option value="">Tất cả</option>{[1, 2, 3, 4].map((size) => <option key={size} value={size}>{size} người</option>)}
        </select></label>
        <label className="admin-field">Phiên bản cấu hình nền<input className="input" name="baseline" defaultValue={baseline} placeholder="Mã cấu hình Web hoặc LOCAL" /></label>
        <label className="admin-field">Dữ liệu<select className="input" name="testOnly" defaultValue={String(testOnly)}>
          <option value="false">Trận thường</option><option value="true">Trận thử nghiệm</option>
        </select></label>
        <div><button className="button">Áp dụng bộ lọc</button></div>
      </form>
    </Panel>
    <Panel className="mt-5">
      <h2 className="admin-section-title">Kết quả theo cách điều chỉnh độ khó</h2>
      <p className="muted my-2 text-sm">{data.total} trận trong mẫu. Nên so sánh các trận cùng bản đồ, độ khó, quy mô đội và cấu hình nền.</p>
      {data.truncated && <p className="my-2 text-sm">Chỉ tính {data.limit} trận mới nhất. Thu hẹp bộ lọc để xem toàn bộ một phạm vi nhỏ hơn.</p>}
      {data.cohorts.length === 0 ? <p className="muted">Chưa có kết quả trận phù hợp.</p> : <div className="table-wrap"><table>
        <thead><tr><th>Nhóm</th><th>Số trận</th><th>Thắng</th><th>Thời lượng TB</th><th>Sống sót</th><th>Trận gần đây</th></tr></thead>
        <tbody>{data.cohorts.map((row) => <tr key={row.group}>
          <td>{labels[row.group] ?? row.group}</td><td>{row.matches}</td><td>{ratio(row.wins, row.matches)}</td>
          <td>{(row.averageSeconds / 60).toFixed(1)} phút</td><td>{ratio(row.survivors, row.players)}</td>
          <td>{row.recentMatchIds.map((id) => <Link key={id} className="mr-2 text-red-300 hover:underline" href={`/admin/matches/${id}`}>{id.slice(0, 8)}</Link>)}</td>
        </tr>)}</tbody>
      </table></div>}
    </Panel>
    <Panel className="mt-5">
      <h2 className="admin-section-title mb-3">Quyết định Adaptive trước và trong trận</h2>
      <div className="grid gap-3 sm:grid-cols-3">
        <StatCard label="Đã áp dụng / Đã duyệt" value={ratio(data.applied, data.approved)} />
        <StatCard label="Máy người chơi ghi nhận / Đã áp dụng" value={ratio(data.clientObserved, data.applied)} />
        <StatCard label="Áp dụng thất bại / Đã duyệt" value={ratio(data.failedEffect, data.approved)} />
        <StatCard label="Hoàn tác có biên nhận" value={data.confirmedRollbacks ?? "Chưa đo"} />
      </div>
      <p className="muted mt-3 text-sm">Khối này đếm quyết định, bảng phía trên đếm trận. Số lần hoàn tác chỉ gồm các lần có xác nhận áp dụng.</p>
      <h3 className="mt-4 font-semibold">Vì sao AI giữ nguyên hoặc dùng cấu hình dự phòng</h3>
      <p className="muted text-sm">Một trận có thể xuất hiện ở nhiều lý do. Chỉ tính lý do đã được hệ thống lưu.</p>
      {data.reasons.length === 0 ? <p className="muted mt-2">Chưa có lý do được lưu trong phạm vi này.</p> :
        <ul className="mt-2 space-y-1 text-sm">{data.reasons.map((row) => <li key={row.reason}>
          {adaptiveReasons[row.reason] ?? "Lý do khác"} · {ratio(row.matches, data.total)}
        </li>)}</ul>}
    </Panel>
  </>;
}
