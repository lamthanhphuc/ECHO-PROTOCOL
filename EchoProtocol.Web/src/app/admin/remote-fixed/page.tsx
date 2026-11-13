import Link from "next/link";
import { difficultyLabel, mapLabel } from "@/lib/utils/game-labels";
import { RemoteFixedCreateForm } from "@/components/admin/remote-fixed-create-form";
import { RemoteFixedActivation } from "@/components/admin/remote-fixed-activation";
import { EmptyState } from "@/components/ui/empty-state";
import { Pagination } from "@/components/ui/pagination";
import { PageHeader, Panel, StatCard } from "@/components/ui/panel";
import { StatusBadge } from "@/components/ui/status-badge";
import { adminApi } from "@/lib/api/server-api";
import type { SearchParams } from "@/lib/types/common";
import { formatAdminDate as formatDate, param, queryString } from "@/lib/utils/format";

export default async function RemoteFixedPage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const raw = await searchParams;
  const mapId = param(raw.mapId), difficulty = param(raw.difficulty), gameVersion = param(raw.gameVersion);
  const page = Math.max(1, Number(param(raw.page)) || 1);
  const [status, result, gameplayFields, content, active, defaults] = await Promise.all([
    adminApi.remoteFixedStatus(),
    adminApi.remoteFixedVersions(queryString({ page, pageSize: 20, mapId, difficulty })),
    adminApi.remoteGameplayFields(),
    adminApi.matchContent(),
    adminApi.remoteFixedVersions(queryString({ page: 1, pageSize: 100, mapId, difficulty, activeOnly: "true" })),
    adminApi.unityBalanceDefaults(),
  ]);
  const defaultProfiles = defaults.available && (!gameVersion || gameVersion === defaults.gameVersion)
    ? (defaults.profiles ?? []).filter((profile) => profile && (!mapId || profile.mapId === mapId) && (!difficulty || profile.difficulty === difficulty))
    : [];
  const scopeQuery = queryString({ mapId, difficulty, gameVersion });
  if (param(raw.create) === "1") {
    const copyVersion = param(raw.copy);
    const copy = copyVersion ? await adminApi.remoteFixedVersion(copyVersion) : null;
    return <>
    <Link href={`/admin/remote-fixed${scopeQuery}`} className="mb-4 inline-block text-sm text-red-300">← Danh sách cấu hình</Link>
    <PageHeader title="Tạo cấu hình trận" description="Chọn phạm vi và xem lại thông số. Bản mới được lưu ở trạng thái chưa kích hoạt." />
    <Panel><RemoteFixedCreateForm gameplayFields={gameplayFields} content={content} initialScope={{ mapId: copy?.mapId ?? mapId, difficulty: copy?.difficulty ?? difficulty, gameVersion }} copySnapshot={copy?.snapshotJson} /></Panel>
    </>;
  }
  const inspectActive = status.remoteFixedEnabled && (!!gameVersion || !status.gameplayTestEnabled);
  const activeDetails = inspectActive
    ? await Promise.all(active.items.map((item) => adminApi.remoteFixedVersion(item.version)))
    : [];
  const compatibleActive = inspectActive ? activeDetails.filter((item) => {
    try {
      const snapshot = JSON.parse(item.snapshotJson);
      return item.isActive && (status.gameplayTestEnabled || snapshot.schemaVersion !== "RFB-2") &&
        (!gameVersion || !snapshot.content?.gameVersion || snapshot.content.gameVersion === gameVersion);
    } catch {
      return false;
    }
  }) : active.items;
  return <>
    <PageHeader title="Cấu hình trận" description="Xem thông số dùng cho trận mới theo bản đồ, độ khó và phiên bản game." />
    <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
      <div className="text-sm">
        <p>{status.remoteFixedEnabled && compatibleActive.length > 0
          ? compatibleActive.length + " bản Web có thể áp dụng cho phạm vi đã chọn."
          : "Bản game đã đăng ký chọn nguồn tự động sẽ dùng mặc định trong game ở phạm vi này."}</p>
        {status.remoteFixedEnabled && !status.gameplayTestEnabled &&
          <p className="muted mt-1">Thử nghiệm RFB-2 đang tắt. Cấu hình thử nghiệm đã kích hoạt được giữ lại để dùng khi bật lại.</p>}
      </div>
      <Link className="button" href={`/admin/remote-fixed${queryString({ mapId, difficulty, gameVersion, create: 1 })}`}>Tạo cấu hình</Link>
    </div>
    <details className="mb-4 text-sm"><summary className="muted">Trạng thái kỹ thuật</summary>
    <div className="mt-3 grid gap-4 sm:grid-cols-3">
      <StatCard label="Cấu hình từ Web" value={status.remoteFixedEnabled ? "Đang bật" : "Đang tắt"} />
      <StatCard label="Thử nghiệm RFB-2" value={status.gameplayTestEnabled ? "Đang bật" : "Đang tắt"} />
      <StatCard label="Phiên bản quy tắc" value={status.protocolVersion} />
    </div>
    </details>
    <Panel className="mt-5">
      <div className="mb-4"><h2 className="admin-section-title">Lọc theo phạm vi</h2><p className="admin-hint mt-1">Chọn bản game để xem nguồn có hiệu lực. Danh sách phiên bản bên dưới vẫn giữ lịch sử của mọi bản game trong bản đồ và độ khó đã chọn.</p></div>
      <form action="/admin/remote-fixed" className="grid gap-3 sm:grid-cols-2 xl:grid-cols-[1fr_1fr_1fr_auto] sm:items-end">
        <label className="admin-field">Bản đồ<select className="input" name="mapId" defaultValue={mapId}><option value="">Tất cả bản đồ</option>{status.supportedMaps.map((map) => <option key={map} value={map}>{mapLabel(map)}</option>)}</select></label>
        <label className="admin-field">Độ khó<select className="input" name="difficulty" defaultValue={difficulty}><option value="">Tất cả độ khó</option><option value="EASY">Dễ</option><option value="NORMAL">Thường</option><option value="HARD">Khó</option></select></label>
        <label className="admin-field">Bản game<select className="input" name="gameVersion" defaultValue={gameVersion}><option value="">Tất cả bản game</option>{content.gameVersions.map((build) => <option key={build}>{build}</option>)}</select></label>
        <button className="button">Lọc cấu hình</button>
      </form>
    </Panel>
    <Panel className="mt-5"><div className="mb-4"><h2 className="admin-section-title">Các phiên bản cấu hình</h2><p className="admin-hint mt-1">Chọn mã cấu hình để xem thông số và lịch sử trước khi kích hoạt.</p></div>
      <div className="mb-4 rounded border border-[var(--line)] p-4">
        <h3 className="font-semibold">Mặc định trong game</h3>
        <p className="muted mt-1 text-sm">Chỉ xem. Trận dùng cấu hình cố định mới dùng cấu hình có sẵn trong game khi phạm vi chưa có bản Web được kích hoạt hoặc nguồn Web đang tắt.</p>
        <details className="mt-3">
          <summary className="cursor-pointer">Xem thông số mặc định{defaults.gameVersion && ` · ${defaults.gameVersion}`}</summary>
          {defaultProfiles.length === 0 ? <p className="muted mt-2 text-sm">Chưa có bảng thông số mặc định cho bản game đã chọn.</p> :
            <div className="table-wrap mt-2"><table><thead><tr><th>Bản đồ / độ khó</th><th>Tuần tra / truy đuổi (m/s)</th><th>Phát hiện / tìm kiếm (giây)</th><th>Hệ số nghe / hồi chiêu (giây)</th><th>Minion khu vực 1/2/3</th><th>Công cụ / lượt cứu mỗi khu</th></tr></thead><tbody>
              {defaultProfiles.map((profile) => <tr key={`${profile.mapId}/${profile.difficulty}`}><td>{mapLabel(profile.mapId)} · {difficultyLabel(profile.difficulty)}</td><td>{profile.patrolSpeed} / {profile.chaseSpeed}</td><td>{profile.detectionSeconds} / {profile.searchSeconds}</td><td>{profile.hearingMultiplier} / {profile.specialCooldownSeconds}</td><td>{profile.zone1Minions} / {profile.zone2Minions} / {profile.zone3Minions}</td><td>{profile.toolsPerZone} / {profile.revivesPerZone}</td></tr>)}
            </tbody></table></div>}
        </details>
        {active.items.map((item) => <details key={item.version} className="mt-3">
          <summary className="cursor-pointer">Ngừng kích hoạt {item.version} · {mapLabel(item.mapId)} · {difficultyLabel(item.difficulty)}</summary>
          <div className="mt-2"><RemoteFixedActivation version={item.version} activeVersion={item.version}
            scope={`${mapLabel(item.mapId)} · ${difficultyLabel(item.difficulty)}`} isActive activationBlocked={null} useDefault /></div>
        </details>)}
      </div>
      {result.items.length === 0 ? <EmptyState title="Chưa có cấu hình phù hợp" /> : <div className="table-wrap"><table><thead><tr><th>Mã cấu hình</th><th>Bản đồ</th><th>Độ khó</th><th>Kích hoạt trên Web</th><th>Ngày tạo</th></tr></thead><tbody>
        {result.items.map((item) => <tr key={item.version}><td><Link className="code admin-link" href={`/admin/remote-fixed/${encodeURIComponent(item.version)}`}>{item.version}</Link></td><td>{mapLabel(item.mapId)}</td><td>{difficultyLabel(item.difficulty)}</td><td><StatusBadge status={item.isActive ? "ACTIVE" : "INACTIVE"} /></td><td>{formatDate(item.createdAtUtc)}</td></tr>)}
      </tbody></table></div>}
      <Pagination page={result.page} totalPages={result.totalPages} pathname="/admin/remote-fixed" params={{ ...(mapId && { mapId }), ...(difficulty && { difficulty }), ...(gameVersion && { gameVersion }) }} />
    </Panel>
  </>;
}
