import { Panel } from "@/components/ui/panel";
import { StatusBadge } from "@/components/ui/status-badge";
import { adaptiveParameterLabels, adaptiveReasons } from "@/lib/types/adaptive";
import type { AdaptiveMatchAudit } from "@/lib/types/adaptive";
import { formatDate } from "@/lib/utils/format";
import { ProfileReprocessButton } from "./profile-reprocess-button";

export function AdaptiveMatchPanel({ audit, matchId }: { audit: AdaptiveMatchAudit; matchId: string }) {
  const { preMatch, decisions, profile } = audit;
  return <Panel className="mt-5 space-y-5">
    <h2 className="text-lg font-semibold">Adaptive theo trận</h2>
    <section>
      <h3 className="mb-2 font-semibold">Quyết định trước trận</h3>
      {preMatch.length === 0 && <p className="muted text-sm">Chưa có quyết định trước trận.</p>}
      {preMatch.map((item) => <div key={item.decisionId} className="mb-2 space-y-1 break-words rounded border border-[var(--line)] p-3 text-sm">
        <p><strong>{item.resolutionResult}</strong> · {item.isCurrent ? "Quyết định hiện hành" : "Đã được thay thế"} · {formatDate(item.committedAtUtc)}</p>
        <p>Cấu hình {item.scenarioConfigId} · {item.scenarioConfigVersion}</p>
        <p>Policy {item.policyVersion} · {item.policyConfigVersion ?? "Mặc định"}</p>
        {item.profileTuningVersion && <p>Policy lịch sử đội · {item.profileTuningVersion}</p>}
        <p>Hồ sơ {item.validity} · {item.teamSize} người · Ngân sách {item.supportItemBudget} · Tốc độ trong registry {item.chaseSpeed}</p>
        {item.preMatchRule && <p><code>{item.preMatchRule}</code></p>}
        {item.usedFixedFallback && <p>Fixed {item.fallbackConfigId} · {item.fallbackConfigVersion}
          {item.fallbackReasonCode && <> · <code>{item.fallbackReasonCode}</code> {adaptiveReasons[item.fallbackReasonCode]}</>}
        </p>}
        <p>Host {item.appliedAtUtc ? `xác nhận lúc ${formatDate(item.appliedAtUtc)}` : "chưa xác nhận cấu hình trước trận"}</p>
      </div>)}
    </section>
    <section>
      <h3 className="mb-2 font-semibold">Lịch sử kế hoạch v0.2</h3>
      {decisions.length === 0 && <p className="muted text-sm">Chưa có quyết định v0.2 trong sổ theo dõi.</p>}
      {audit.hostEvidenceUnavailable && <p className="muted mb-2 text-sm">Tạm thời không đọc được telemetry của các revision đang chờ.</p>}
      {decisions.map((item) => <details key={item.decisionId} className="mb-2 rounded border border-[var(--line)] p-3 text-sm">
        <summary className="cursor-pointer">
          <strong>Revision {item.planRevision}</strong> · {item.decisionPoint} · {item.targetPhaseName} · {item.approvalStatus} / {item.applyStatus}
        </summary>
        <div className="mt-2 space-y-2 break-words">
          <p className="muted">Pha {item.phaseOrdinal} · {formatDate(item.createdAtUtc)} · {item.policyVersion} · {item.catalogVersion}</p>
          <p><code>{item.reasonCode}</code> {adaptiveReasons[item.reasonCode]}</p>
          <p>Revision trước đó {item.expectedPlanRevision} · Host đã áp dụng {item.hostAppliedRevision ?? "chưa có bằng chứng"}</p>
          {item.changes.length > 0
            ? <ul className="list-disc pl-5">{item.changes.map((change) => <li key={change.key}>
              {adaptiveParameterLabels[change.key] ?? change.key} · {change.before} → {change.after}
            </li>)}</ul>
            : <p>{item.changedKey === "SupportTool" ? "Hành động hỗ trợ công cụ." : "Giữ nguyên thông số."}</p>}
          <p>Client báo đã nhận revision · {item.clientObservations.length || "chưa có"}</p>
          {item.clientObservations.length > 0 && <ul className="list-disc pl-5">{item.clientObservations.map((client) =>
            <li key={client.userId}>{client.userId} · {formatDate(client.observedAtUtc)}</li>)}</ul>}
          <p><code>APPLY_RECEIPT</code> · {item.applyReceipt
            ? `${item.applyReceipt.receiptId} · tick ${item.applyReceipt.appliedTick} · ${formatDate(item.applyReceipt.appliedAtUtc)}`
            : "Chưa có"}</p>
          {item.applyReceipt?.effectEventId && <p className="muted">Sự kiện hiệu ứng {item.applyReceipt.effectEventId}</p>}
        </div>
      </details>)}
    </section>
    <section>
      <h3 className="mb-2 font-semibold">Xử lý hồ sơ sau trận</h3>
      {profile ? <div className="space-y-1 text-sm">
        <p><StatusBadge status={profile.status} /> <code>{profile.status}</code> · {profile.attempts} lượt xử lý</p>
        {profile.profileTuningVersion && <p>Phiên bản cấu hình hồ sơ · {profile.profileTuningVersion}</p>}
        {profile.lastError && <p>Lỗi gần nhất · <code>{profile.lastError}</code></p>}
        {profile.status === "PENDING" && <p>Dự kiến thử lại {formatDate(profile.nextAttemptAtUtc)}</p>}
        {audit.mayReprocess && <ProfileReprocessButton matchId={matchId} />}
      </div> : <p className="muted text-sm">Chưa có tác vụ xử lý hồ sơ.</p>}
    </section>
  </Panel>;
}
