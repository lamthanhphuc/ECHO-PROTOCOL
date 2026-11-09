import { Panel } from "@/components/ui/panel";
import { StatusBadge } from "@/components/ui/status-badge";
import { adaptiveParameterLabels, adaptiveReasons } from "@/lib/types/adaptive";
import type { AdaptiveMatchAudit } from "@/lib/types/adaptive";
import { formatDate } from "@/lib/utils/format";
import { ProfileReprocessButton } from "./profile-reprocess-button";

function decisionLabel(approval: string, application: string) {
  if (approval === "HOLD") return "Giữ nguyên";
  if (application === "APPLIED") return "Đã áp dụng";
  if (application === "FAILED_EFFECT") return "Áp dụng lỗi hiệu ứng";
  if (application === "ABORTED") return "Đã hủy";
  if (application === "PENDING") return "Chờ xác nhận áp dụng";
  return "Chưa có bằng chứng áp dụng";
}

export function AdaptiveMatchPanel({ audit, matchId }: { audit: AdaptiveMatchAudit; matchId: string }) {
  const { baseline, preMatch, decisions, profile, teamProfile } = audit;
  return <Panel className="mt-5 space-y-5">
    <h2 className="text-lg font-semibold">Cân bằng theo trận</h2>
    <section>
      <h3 className="mb-2 font-semibold">Hồ sơ đội</h3>
      {teamProfile ? <>
        <p className="text-sm">{teamProfile.status} · {teamProfile.processingReason} · Telemetry {teamProfile.telemetryCompleteness}</p>
        <p className="muted mb-2 text-sm">Công thức {teamProfile.profileFormulaVersion}{teamProfile.teamPerformanceFormulaVersion && ` / ${teamProfile.teamPerformanceFormulaVersion}`} · Revision {teamProfile.processingRevision} · {formatDate(teamProfile.updatedAtUtc)}</p>
        <ul className="grid gap-2 text-sm sm:grid-cols-2">
          {([
            ["Thời gian mục tiêu (giây)", teamProfile.objectiveTimeSeconds, teamProfile.objectiveTimeStatus],
            ["Điểm sinh tồn", teamProfile.survivalScore, teamProfile.survivalStatus],
            ["Điểm phối hợp", teamProfile.teamworkScore, teamProfile.teamworkStatus],
            ["Điểm sử dụng tài nguyên", teamProfile.resourceEfficiencyScore, teamProfile.resourceStatus],
            ["Điểm hiệu quả đội", teamProfile.teamPerformanceScore, teamProfile.performanceStatus],
          ] as const).map(([label, value, status]) => <li key={label}>{label} · {value ?? "—"} · {status}</li>)}
        </ul>
      </> : <p className="muted text-sm">Chưa có hồ sơ đội cho trận này.{profile?.lastError && ` ${profile.lastError}`}</p>}
    </section>
    {baseline && <section>
      <h3 className="mb-2 font-semibold">Cấu hình nền đã khóa</h3>
      <p className="text-sm">{baseline.source === "LOCAL" ? "Mặc định trong game" : `Cấu hình từ Web${baseline.version ? ` · ${baseline.version}` : ""}`}
        {" · "}{baseline.matchDataPolicy === "TEST_ONLY" ? "Trận thử nghiệm" : baseline.matchDataPolicy === "STANDARD" ? "Trận thường" : "Chưa xác định chính sách dữ liệu"}</p>
      {baseline.buildVersion && <p className="text-sm">Bản game · {baseline.buildVersion}</p>}
      <details className="muted mt-1 text-xs">
        <summary className="cursor-pointer">Chi tiết kỹ thuật</summary>
        <code className="break-all">{baseline.source} · {baseline.matchDataPolicy}{baseline.hash && ` · ${baseline.hash}`}</code>
      </details>
    </section>}
    <section>
      <h3 className="mb-2 font-semibold">Quyết định trước trận</h3>
      {preMatch.length === 0 && <p className="muted text-sm">Chưa có quyết định trước trận.</p>}
      {preMatch.map((item) => <div key={item.decisionId} className="mb-2 space-y-1 break-words rounded border border-[var(--line)] p-3 text-sm">
        <p><strong>{item.usedFixedFallback ? "Dùng cấu hình dự phòng" : item.resolutionResult === "ADAPTIVE_CONFIG_ISSUED" ? "Đã chọn cấu hình Adaptive" : "Đã ghi quyết định"}</strong> · {item.isCurrent ? "Quyết định hiện hành" : "Đã được thay thế"} · {formatDate(item.committedAtUtc)}</p>
        <p>Cấu hình {item.scenarioConfigId} · {item.scenarioConfigVersion}</p>
        <p>Policy {item.policyVersion} · {item.policyConfigVersion ?? "Mặc định"}</p>
        {item.profileTuningVersion && <p>Policy lịch sử đội · {item.profileTuningVersion}</p>}
        <p>Hồ sơ {item.validity} · {item.teamSize} người · Ngân sách {item.supportItemBudget} · Tốc độ trong registry {item.chaseSpeed}</p>
        {item.usedFixedFallback && <p>Cấu hình dự phòng {item.fallbackConfigId} · {item.fallbackConfigVersion}
          {item.fallbackReasonCode && adaptiveReasons[item.fallbackReasonCode] && ` · ${adaptiveReasons[item.fallbackReasonCode]}`}
        </p>}
        <p>Host {item.appliedAtUtc ? `xác nhận lúc ${formatDate(item.appliedAtUtc)}` : "chưa xác nhận cấu hình trước trận"}</p>
        <details className="muted text-xs"><summary className="cursor-pointer">Chi tiết kỹ thuật</summary>
          <code>{[item.resolutionResult, item.preMatchRule, item.fallbackReasonCode].filter(Boolean).join(" · ")}</code>
        </details>
      </div>)}
    </section>
    <section>
      <h3 className="mb-2 font-semibold">Điều chỉnh Adaptive</h3>
      {decisions.length === 0 && <p className="muted text-sm">Chưa có quyết định v0.2 trong sổ theo dõi.</p>}
      {audit.hostEvidenceUnavailable && <p className="muted mb-2 text-sm">Tạm thời không đọc được telemetry của các revision đang chờ.</p>}
      {decisions.map((item) => <details key={item.decisionId} className="mb-2 rounded border border-[var(--line)] p-3 text-sm">
        <summary className="cursor-pointer">
          <strong>Revision {item.planRevision}</strong> · {decisionLabel(item.approvalStatus, item.applyStatus)}
        </summary>
        <div className="mt-2 space-y-2 break-words">
          <p className="muted">Pha {item.phaseOrdinal} · {formatDate(item.createdAtUtc)} · {item.policyVersion} · {item.catalogVersion}</p>
          {adaptiveReasons[item.reasonCode] && <p>{adaptiveReasons[item.reasonCode]}</p>}
          <p>Revision trước đó {item.expectedPlanRevision} · Host đã áp dụng {item.hostAppliedRevision ?? "chưa có bằng chứng"}</p>
          {item.changes.length > 0
            ? <ul className="list-disc pl-5">{item.changes.map((change) => <li key={change.key}>
              {adaptiveParameterLabels[change.key] ?? change.key} · {change.before} → {change.after}
            </li>)}</ul>
            : <p>{item.changedKey === "SupportTool" ? "Hành động hỗ trợ công cụ." : "Giữ nguyên thông số."}</p>}
          <p>{item.clientObservations.length > 0 ? `Đã có báo cáo từ ít nhất một Client · ${item.clientObservations.length} báo cáo` : "Chưa có báo cáo Client"}</p>
          <p>Biên nhận áp dụng · {item.applyReceipt ? formatDate(item.applyReceipt.appliedAtUtc) : "Chưa có"}</p>
          <details className="muted text-xs">
            <summary className="cursor-pointer">Chi tiết kỹ thuật</summary>
            <p><code>{[item.decisionPoint, item.targetPhaseName, item.approvalStatus, item.applyStatus, item.reasonCode].filter(Boolean).join(" · ")}</code></p>
            {item.clientObservations.map((client) => <p key={client.userId}>{client.userId} · {formatDate(client.observedAtUtc)}</p>)}
            {item.applyReceipt && <p><code>APPLY_RECEIPT</code> · {item.applyReceipt.receiptId} · tick {item.applyReceipt.appliedTick}</p>}
            {item.applyReceipt?.effectEventId && <p>Sự kiện hiệu ứng {item.applyReceipt.effectEventId}</p>}
          </details>
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
