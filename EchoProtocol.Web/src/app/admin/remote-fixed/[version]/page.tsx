import { RemoteFixedActivation } from "@/components/admin/remote-fixed-activation";
import Link from "next/link";
import { MatchContentSummary } from "@/components/admin/match-content-summary";
import { difficultyLabel, gameplayLabel, mapLabel, minionAttackDamage } from "@/lib/utils/game-labels";
import { PageHeader, Panel, StatCard } from "@/components/ui/panel";
import { StatusBadge } from "@/components/ui/status-badge";
import { adminApi } from "@/lib/api/server-api";
import { formatDate, queryString } from "@/lib/utils/format";

const retiredLabels: Record<string, string> = {
  stalkerHearing: "Hệ số nghe của Stalker",
  listenerHearing: "Hệ số tầm nghe của Listener",
  doorTravelSeconds: "Thời gian đóng mở cửa (giây)",
  toolsPerZone: "Ngân sách công cụ mỗi Zone",
  zone1ToolBudget: "Phân bổ công cụ Zone 1",
  zone2ToolBudget: "Phân bổ công cụ Zone 2",
  zone3ToolBudget: "Phân bổ công cụ Zone 3",
};

export default async function RemoteFixedDetailPage({ params }: { params: Promise<{ version: string }> }) {
  const { version } = await params;
  const [detail, gameplayFields, status, catalog] = await Promise.all([
    adminApi.remoteFixedVersion(version), adminApi.remoteGameplayFields(), adminApi.remoteFixedStatus(), adminApi.matchContent(),
  ]);
  let snapshot: (Record<string, unknown> & { schemaVersion?: string }) | null = null;
  try { snapshot = JSON.parse(detail.snapshotJson); } catch { snapshot = null; }
  const overrides: { key: string; value: number }[] = Array.isArray(snapshot?.gameplay) ? snapshot.gameplay : [];
  const content = snapshot?.content as unknown as { mode: string; monster: string; route: string; gameVersion: string } | undefined;
  const groups = [
    { title: "Người chơi", key: "player", fields: [["maxHealth", "Máu tối đa"], ["walkSpeed", "Tốc độ đi"], ["sprintSpeed", "Tốc độ chạy"], ["maxStamina", "Thể lực tối đa"]] },
    { title: "Kẻ săn đuổi", key: "stalker", fields: [["patrolSpeed", "Tốc độ tuần tra"], ["chaseSpeed", "Tốc độ truy đuổi"]] },
    { title: "Quái phụ", key: "minion", fields: [["roamSpeed", "Tốc độ lang thang"], ["trackSpeed", "Tốc độ theo dấu"]] },
  ];
  const scope = queryString({ mapId: detail.mapId, difficulty: detail.difficulty, page: 1, pageSize: 100 });
  const history = await adminApi.remoteFixedHistory(scope);
  const activeVersion = detail.activeVersion;
  const scopeLabel = `${mapLabel(detail.mapId)} · ${difficultyLabel(detail.difficulty)}`;
  const activationBlocked = snapshot?.schemaVersion === "RFB-2" && !status.gameplayTestEnabled
    ? "Hãy bật Thử nghiệm RFB-2 trên Backend để kích hoạt cấu hình này."
    : content && !catalog.gameVersions.includes(content.gameVersion)
      ? `Build Unity ${content.gameVersion} chưa có trong danh sách tương thích.`
      : null;
  return <>
    <Link className="mb-4 inline-block text-sm text-red-300 hover:underline" href="/admin/remote-fixed">← Quay lại danh sách</Link>
    <PageHeader title={detail.version} description={scopeLabel} />
    <div className="grid gap-4 sm:grid-cols-3">
      <StatCard label="Trạng thái" value={<StatusBadge status={detail.isActive ? "ACTIVE" : "INACTIVE"} />} />
      <StatCard label="Ngày tạo" value={formatDate(detail.createdAtUtc)} />
      <StatCard label="Bản được kích hoạt trên Web" value={activeVersion ?? "Chưa có"} />
    </div>
    {!status.remoteFixedEnabled && <p className="muted mt-3 text-sm">Nguồn cấu hình từ Web đang tắt. Trận Fixed mới dùng mặc định trong game.</p>}
    <div className="mt-5 grid gap-5 xl:grid-cols-2">
      <Panel><h2 className="admin-section-title mb-3">Thông số cân bằng</h2>
        {content && <div className="mb-4"><MatchContentSummary map={detail.mapId} {...content} difficulty={detail.difficulty} /></div>}
        {snapshot ? groups.map((group) => <div key={group.key} className="mb-4"><h3 className="mb-2 font-semibold">{group.title}</h3><dl className="grid gap-2 sm:grid-cols-2">{group.fields.map(([key, label]) => <div key={key} className="flex justify-between gap-3 border-b border-[var(--line)] py-1 text-sm"><dt className="muted">{label}</dt><dd>{(snapshot?.[group.key] as Record<string, number> | undefined)?.[key] ?? "—"}</dd></div>)}</dl></div>) : <p role="alert" className="text-rose-300">Không đọc được thông số cấu hình.</p>}
        <p className="muted mb-3 text-sm">Sát thương Minion trong bản game mới cố định {minionAttackDamage} HP mỗi đòn.</p>
        {overrides.length > 0 && <div>
          <h3 className="mb-2 font-semibold">Thông số RFB-2</h3>
          <dl className="grid gap-2 sm:grid-cols-2">{overrides.map((entry) => <div className="flex justify-between gap-2 text-sm" key={entry.key}>
            <dt className="muted">{gameplayLabel(retiredLabels[entry.key] ?? gameplayFields.find((field) => field.key === entry.key)?.label ?? entry.key)}
              {retiredLabels[entry.key] && <span className="ml-2 text-xs">· Tùy chỉnh cũ</span>}</dt><dd>{entry.value}</dd>
          </div>)}</dl>
        </div>}
        <details className="mt-4 text-sm"><summary className="cursor-pointer text-red-300">Xem dữ liệu gốc và mã kiểm tra</summary><pre className="mt-3 overflow-auto whitespace-pre-wrap break-all rounded bg-black/20 p-4 text-xs">{detail.snapshotJson}</pre><p className="code mt-2 break-all">{detail.contentHash}</p></details>
      </Panel>
      <Panel><h2 className="admin-section-title mb-2">Áp dụng cấu hình</h2><p className="admin-hint mb-4">Thay đổi chỉ ảnh hưởng tới các trận bắt đầu sau khi kích hoạt.</p><RemoteFixedActivation version={detail.version} activeVersion={activeVersion} isActive={detail.isActive} activationBlocked={activationBlocked} scope={scopeLabel} /></Panel>
    </div>
    <Panel className="mt-5"><h2 className="admin-section-title mb-4">Lịch sử thay đổi</h2>
      {history.items.length === 0 ? <p className="muted text-sm">Chưa có thao tác.</p> : <div className="table-wrap"><table><thead><tr><th>Thao tác</th><th>Trước đó</th><th>Áp dụng</th><th>Người thực hiện</th><th>Lý do</th><th>Thời gian</th></tr></thead><tbody>
        {history.items.map((item) => <tr key={item.id}><td>{item.action === "CREATE" ? "Tạo" : item.action === "ACTIVATE" ? "Kích hoạt" : "Ngừng kích hoạt"}</td><td className="code">{item.previousVersion ?? "—"}</td><td className="code">{item.targetVersion ?? "—"}</td><td className="code">{item.adminUserId}</td><td>{item.reason}</td><td>{formatDate(item.createdAtUtc)}</td></tr>)}
      </tbody></table></div>}
    </Panel>
  </>;
}
