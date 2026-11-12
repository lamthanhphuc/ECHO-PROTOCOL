import { Panel } from "@/components/ui/panel";
import { StatusBadge } from "@/components/ui/status-badge";
import { adaptiveParameterLabels, adaptiveReasons } from "@/lib/types/adaptive";
import type { AdaptiveMatchAudit } from "@/lib/types/adaptive";
import { formatAdminDate as formatDate } from "@/lib/utils/format";
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
    <h2 className="text-lg font-semibold">Cấu hình và bằng chứng theo trận</h2>
    {baseline && <section>
      <h3 className="mb-2 font-semibold">Cấu hình nền đã khóa</h3>
      <p className="text-sm">{baseline.source === "LOCAL" ? "Mặc định trong game" : baseline.source === "REMOTE_FIXED" ? `Cấu hình từ Web${baseline.version ? ` · ${baseline.version}` : ""}` : "Chưa xác định nguồn cấu hình"}
        {" · "}{baseline.matchDataPolicy === "TEST_ONLY" ? "Trận thử nghiệm" : baseline.matchDataPolicy === "STANDARD" ? "Trận thường" : "Chưa xác định chính sách dữ liệu"}</p>
      {baseline.buildVersion && <p className="text-sm">Bản game · {baseline.buildVersion}</p>}
      <details className="muted mt-1 text-xs">
        <summary className="cursor-pointer">Chi tiết kỹ thuật</summary>
        <code className="break-all">{baseline.source} · {baseline.matchDataPolicy}{baseline.hash && ` · ${baseline.hash}`}</code>
      </details>
    </section>}
    {!baseline && <p className="muted text-sm">Chưa có dữ liệu cấu hình nền cho trận này.</p>}
    <section>
      <h3 className="mb-2 font-semibold">Quyết định trước trận</h3>
      {preMatch.length === 0 && <p className="muted text-sm">Chưa có quyết định trước trận.</p>}
      {preMatch.map((item) => <div key={item.decisionId} className="mb-2 space-y-1 break-words rounded border border-[var(--line)] p-3 text-sm">
        <p><strong>{item.usedFixedFallback ? "Dùng cấu hình dự phòng" : item.resolutionResult === "ADAPTIVE_CONFIG_ISSUED" ? "Đã chọn cấu hình do AI đề xuất" : "Đã ghi quyết định"}</strong> · {item.isCurrent ? "Quyết định hiện hành" : "Đã được thay thế"} · {formatDate(item.committedAtUtc)}</p>
        <p>Cấu hình {item.scenarioConfigId} · {item.scenarioConfigVersion}</p>
        <p>Quy mô đội · {item.teamSize} người</p>
        {item.preMatchRule === "AED-PRE-010-SURVIVAL-LOW-RELIEVE" && <p>AI chọn giảm áp lực vì điểm sinh tồn thấp.</p>}
        {item.preMatchRule === "AED-PRE-030-BOTH-HIGH-INCREASE" && <p>AI chọn tăng áp lực dựa trên hồ sơ đội.</p>}
        {item.usedFixedFallback && <p>Cấu hình dự phòng {item.fallbackConfigId} · {item.fallbackConfigVersion}
          {item.fallbackReasonCode && adaptiveReasons[item.fallbackReasonCode] && ` · ${adaptiveReasons[item.fallbackReasonCode]}`}
        </p>}
        <p>Chủ phòng {item.appliedAtUtc ? `xác nhận lúc ${formatDate(item.appliedAtUtc)}` : "chưa xác nhận cấu hình trước trận"}</p>
        <details className="muted text-xs"><summary className="cursor-pointer">Chi tiết kỹ thuật</summary>
          <p>Quy tắc {item.policyVersion} · {item.policyConfigVersion ?? "Mặc định"} · {item.profileTuningVersion ?? "—"} · {item.validity}</p>
          <p>Thông số từ cấu hình · Tốc độ {item.chaseSpeed} · Ngân sách công cụ {item.supportItemBudget}</p>
          <code>{[item.resolutionResult, item.preMatchRule, item.fallbackReasonCode].filter(Boolean).join(" · ")}</code>
        </details>
      </div>)}
    </section>
    <section>
      <h3 className="mb-2 font-semibold">Điều chỉnh trong trận</h3>
      {audit.sourceStatus && <p className="muted mb-2 text-sm">
        Lần đánh giá gần nhất · {formatDate(audit.sourceStatus.checkedAtUtc)} ·{" "}
        {adaptiveReasons[audit.sourceStatus.reasonCode] ?? "Chưa có diễn giải"}
      </p>}
      {decisions.length === 0 && <p className="muted text-sm">Chưa ghi nhận quyết định điều chỉnh.</p>}
      {audit.hostEvidenceUnavailable && <p className="muted mb-2 text-sm">Tạm thời chưa đọc được dữ liệu xác nhận từ chủ phòng.</p>}
      {decisions.map((item) => <details key={item.decisionId} className="mb-2 rounded border border-[var(--line)] p-3 text-sm">
        <summary className="cursor-pointer">
          <strong>Lần điều chỉnh {item.planRevision}</strong> · {decisionLabel(item.approvalStatus, item.applyStatus)}
        </summary>
        <div className="mt-2 space-y-2 break-words">
          <p className="muted">Giai đoạn {item.phaseOrdinal} · {formatDate(item.createdAtUtc)}</p>
          {adaptiveReasons[item.reasonCode] && <p>{adaptiveReasons[item.reasonCode]}</p>}
          {item.changes.length > 0 && <p>{item.applyReceipt ? "Thay đổi đã có biên nhận áp dụng" : item.approvalStatus === "HOLD" ? "Đề xuất bị giữ lại" : "Đề xuất chưa có biên nhận áp dụng"}</p>}
          {item.changes.length > 0
            ? <ul className="list-disc pl-5">{item.changes.map((change) => <li key={change.key}>
              {adaptiveParameterLabels[change.key] ?? "Thông số khác"} · {change.before} → {change.after}
            </li>)}</ul>
            : <p>{item.changedKey === "SupportTool" ? "Hành động hỗ trợ công cụ." : "Giữ nguyên thông số."}</p>}
          <p>{item.clientObservations.length > 0 ? `Đã có báo cáo quan sát từ máy người chơi · ${item.clientObservations.length} báo cáo` : "Chưa có báo cáo quan sát từ máy người chơi"}</p>
          <p>Xác nhận từ chủ phòng · {item.applyReceipt ? formatDate(item.applyReceipt.appliedAtUtc) : "Chưa có"}</p>
          <details className="muted text-xs">
            <summary className="cursor-pointer">Chi tiết kỹ thuật</summary>
            <p>Quy tắc {item.policyVersion} · {item.catalogVersion} · Lần trước {item.expectedPlanRevision} · Host {item.hostAppliedRevision ?? "—"}</p>
            <p><code>{[item.decisionPoint, item.targetPhaseName, item.approvalStatus, item.applyStatus, item.reasonCode].filter(Boolean).join(" · ")}</code></p>
            {item.clientObservations.map((client) => <p key={client.userId}>{client.userId} · {formatDate(client.observedAtUtc)}</p>)}
            {item.applyReceipt && <p><code>APPLY_RECEIPT</code> · {item.applyReceipt.receiptId} · tick {item.applyReceipt.appliedTick}</p>}
            {item.applyReceipt?.effectEventId && <p>Sự kiện hiệu ứng {item.applyReceipt.effectEventId}</p>}
          </details>
        </div>
      </details>)}
    </section>
    <details>
      <summary className="cursor-pointer font-semibold">Hồ sơ đội</summary>
      {teamProfile ? <>
        <p className="muted mb-2 text-sm">Công thức {teamProfile.profileFormulaVersion}{teamProfile.teamPerformanceFormulaVersion && ` / ${teamProfile.teamPerformanceFormulaVersion}`} · Lần cập nhật {teamProfile.processingRevision} · {formatDate(teamProfile.updatedAtUtc)}</p>
        <ul className="grid gap-2 text-sm sm:grid-cols-2">
          {([
            ["Thời gian mục tiêu (giây)", teamProfile.objectiveTimeSeconds, teamProfile.objectiveTimeStatus],
            ["Điểm sinh tồn", teamProfile.survivalScore, teamProfile.survivalStatus],
            ["Điểm phối hợp", teamProfile.teamworkScore, teamProfile.teamworkStatus],
            ["Điểm sử dụng tài nguyên", teamProfile.resourceEfficiencyScore, teamProfile.resourceStatus],
            ["Điểm hiệu quả đội", teamProfile.teamPerformanceScore, teamProfile.performanceStatus],
          ] as const).map(([label, value, status]) => <li key={label}>{label} · {value ?? "—"} <span className="muted" title={status}>{value == null ? "· Chưa có điểm" : ""}</span></li>)}
        </ul>
      </> : <p className="muted text-sm">Chưa có hồ sơ đội cho trận này.</p>}
    </details>
    <section>
      <h3 className="mb-2 font-semibold">Xử lý hồ sơ sau trận</h3>
      {profile ? <div className="space-y-1 text-sm">
        <p><StatusBadge status={profile.status} /> · {profile.attempts} lượt xử lý</p>
        {profile.profileTuningVersion && <p>Phiên bản cấu hình hồ sơ · {profile.profileTuningVersion}</p>}
        {profile.lastError && <details className="muted"><summary>Chi tiết lỗi xử lý</summary><code>{profile.lastError}</code></details>}
        {profile.status === "PENDING" && <p>Dự kiến thử lại {formatDate(profile.nextAttemptAtUtc)}</p>}
        {audit.mayReprocess && <ProfileReprocessButton matchId={matchId} />}
      </div> : <p className="muted text-sm">Chưa có tác vụ xử lý hồ sơ.</p>}
    </section>
  </Panel>;
}
