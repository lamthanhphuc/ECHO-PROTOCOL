import Link from "next/link";
import { RemoteFixedCreateForm } from "@/components/admin/remote-fixed-create-form";
import { EmptyState } from "@/components/ui/empty-state";
import { Pagination } from "@/components/ui/pagination";
import { PageHeader, Panel, StatCard } from "@/components/ui/panel";
import { StatusBadge } from "@/components/ui/status-badge";
import { adminApi } from "@/lib/api/server-api";
import type { SearchParams } from "@/lib/types/common";
import { formatDate, param, queryString } from "@/lib/utils/format";

export default async function RemoteFixedPage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const raw = await searchParams;
  const mapId = param(raw.mapId), difficulty = param(raw.difficulty);
  const page = Math.max(1, Number(param(raw.page)) || 1);
  const [status, result] = await Promise.all([
    adminApi.remoteFixedStatus(),
    adminApi.remoteFixedVersions(queryString({ page, pageSize: 20, mapId, difficulty })),
  ]);
  return <>
    <PageHeader title="Cân bằng cố định" description="Tạo cấu hình, kiểm tra rồi kích hoạt cho các trận mới." />
    <div className="grid gap-4 sm:grid-cols-2">
      <StatCard label="Remote Fixed production" value={status.remoteFixedEnabled ? "Đang bật" : "Đang tắt"} />
      <StatCard label="Phiên bản quy tắc" value={status.protocolVersion} />
    </div>
    <Panel className="mt-5">
      <div className="mb-4"><h2 className="admin-section-title">1. Chọn phạm vi</h2><p className="admin-hint mt-1">Lọc cấu hình theo bản đồ và độ khó.</p></div>
      <form action="/admin/remote-fixed" className="grid gap-3 sm:grid-cols-[1fr_1fr_auto] sm:items-end">
        <label className="admin-field">Bản đồ<select className="input" name="mapId" defaultValue={mapId}><option value="">Tất cả bản đồ</option><option value="SciFi">SciFi</option><option value="Scene_A">Scene_A</option></select></label>
        <label className="admin-field">Độ khó<select className="input" name="difficulty" defaultValue={difficulty}><option value="">Tất cả độ khó</option><option value="EASY">Dễ</option><option value="NORMAL">Bình thường</option><option value="HARD">Khó</option></select></label>
        <button className="button">Lọc cấu hình</button>
      </form>
    </Panel>
    <Panel className="mt-5"><div className="mb-4"><h2 className="admin-section-title">2. Xem cấu hình</h2><p className="admin-hint mt-1">Chọn mã cấu hình để xem thông số và lịch sử trước khi kích hoạt.</p></div>
      {result.items.length === 0 ? <EmptyState title="Chưa có cấu hình phù hợp" /> : <div className="table-wrap"><table><thead><tr><th>Mã cấu hình</th><th>Bản đồ</th><th>Độ khó</th><th>Trạng thái</th><th>Ngày tạo</th></tr></thead><tbody>
        {result.items.map((item) => <tr key={item.version}><td><Link className="code text-red-300 hover:underline" href={`/admin/remote-fixed/${encodeURIComponent(item.version)}`}>{item.version}</Link></td><td>{item.mapId}</td><td>{item.difficulty === "EASY" ? "Dễ" : item.difficulty === "HARD" ? "Khó" : "Bình thường"}</td><td><StatusBadge status={item.isActive ? "ACTIVE" : "INACTIVE"} /></td><td>{formatDate(item.createdAtUtc)}</td></tr>)}
      </tbody></table></div>}
      <Pagination page={result.page} totalPages={result.totalPages} pathname="/admin/remote-fixed" params={{ ...(mapId && { mapId }), ...(difficulty && { difficulty }) }} />
    </Panel>
    <Panel className="mt-5"><h2 className="admin-section-title mb-1">3. Tạo cấu hình mới</h2><p className="admin-hint mb-5">Cấu hình mới chưa được áp dụng cho trận đấu cho đến khi bạn kích hoạt.</p><RemoteFixedCreateForm /></Panel>
  </>;
}
