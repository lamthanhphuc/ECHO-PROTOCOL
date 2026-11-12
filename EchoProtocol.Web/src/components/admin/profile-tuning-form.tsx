"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";
import { Panel } from "@/components/ui/panel";
import type { ProfileTuningData, ProfileTuningProjection } from "@/lib/types/profile-tuning";
import { formatAdminDate } from "@/lib/utils/format";

const fields = [
  ["survivalAlpha", "Trọng số trận mới · Sinh tồn", .001, 1, .001], ["noiseAlpha", "Trọng số trận mới · Tiếng ồn", .001, 1, .001],
  ["objectiveAlpha", "Trọng số trận mới · Mục tiêu", .001, 1, .001], ["toolUsageAlpha", "Trọng số trận mới · Công cụ", .001, 1, .001],
  ["noiseMin", "Tiếng ồn tối thiểu", 0, 99999, 1], ["noiseMax", "Tiếng ồn tối đa", 1, 100000, 1],
  ["toolUsageMax", "Số lượt công cụ tại điểm tối đa", 1, 100000, 1],
  ["objectiveTimeMin", "Thời gian mục tiêu thấp (giây)", 0, 7199, 1],
  ["objectiveTimeMax", "Thời gian mục tiêu cao (giây)", 1, 7200, 1],
  ["historyMatches", "Số trận tối thiểu", 3, 30, 1], ["historyOpportunities", "Số tình huống đã hoàn tất tối thiểu", 8, 200, 1],
  ["historyMaxAgeDays", "Số ngày xem lại lịch sử", 7, 30, 1], ["historyHalfLifeDays", "Số ngày để ảnh hưởng giảm còn một nửa", 1, 30, 1],
] as const;
const noiseLabels: Record<string, string> = {
  SPRINT: "Chạy nhanh", INTERACTION: "Tương tác", CORE_CARRY: "Mang lõi",
  CORE_DROP: "Làm rơi lõi", NOISE_MAKER: "Máy tạo tiếng ồn", FIELD_SCANNER: "Máy quét hiện trường",
  CROUCH: "Đi khom", WALK: "Đi bộ", DOOR: "Đóng hoặc mở cửa", CORE_INSERT: "Lắp lõi",
  MACHINE_REPAIR: "Sửa máy", MINION_ALERT: "Quái phụ báo động", MACHINE_OVERLOAD: "Máy quá tải",
  TERMINAL_DOWNLOAD: "Tải dữ liệu", VEHICLE_PUSH: "Đẩy xe", CHARGE_TRANSFER: "Chuyển điện", POWER_SURGE: "Xung điện",
};
const dimensionLabels: Record<string, string> = { Survival: "Sinh tồn", Noise: "Tiếng ồn", Objective: "Mục tiêu", ToolUsage: "Công cụ" };

export function ProfileTuningForm({ data }: { data: ProfileTuningData }) {
  const router = useRouter();
  const [settings, setSettings] = useState(data.settings);
  const [sample, setSample] = useState({ survived: true, completedObjective: true, toolsUsed: 3, objectiveSeconds: 300, previousScore: 60 as number | null });
  const [noiseCounts, setNoiseCounts] = useState<Record<string, number>>({ SPRINT: 20 });
  const [preview, setPreview] = useState<{
    before: ProfileTuningProjection;
    after: ProfileTuningProjection;
    previewFingerprint: string;
  } | null>(null);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState("");

  async function submit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const publish = (event.nativeEvent as SubmitEvent).submitter?.getAttribute("value") === "publish";
    if (publish && !preview) return;
    if (publish && !window.confirm("Xuất bản công thức cho hồ sơ mới? Hồ sơ đã có tiếp tục dùng công thức đã gắn.")) return;
    setBusy(true);
    setMessage("");
    try {
      const response = await fetch(publish ? "/api/bff/admin/ai-profile" : "/api/bff/admin/ai-profile/preview", {
        method: "POST", headers: { "Content-Type": "application/json" },
        body: JSON.stringify(publish ? { settings, expectedVersion: data.version, previewFingerprint: preview?.previewFingerprint }
          : { settings, ...sample, noiseCounts }),
      });
      const payload = await response.json();
      if (!response.ok || !payload.success) throw new Error(payload.message || "Không lưu được cấu hình.");
      if (publish) router.refresh();
      else setPreview(payload.data);
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "Không kết nối được máy chủ.");
    } finally {
      setBusy(false);
    }
  }

  return <form className="grid gap-5" onSubmit={submit} onInvalidCapture={(event) => {
    const section = event.target instanceof HTMLElement ? event.target.closest("details") : null;
    if (section) section.open = true;
  }}>
    <Panel>
      <h2 className="text-lg font-semibold">Công thức đang dùng</h2>
      <p className="muted my-3 text-sm">{data.publishedAtUtc ? "Xuất bản lúc " + formatAdminDate(data.publishedAtUtc) : "Đang dùng công thức mặc định."}</p>
      {data.version && <details className="muted mb-3 text-sm"><summary>Mã phiên bản</summary><code className="code">{data.version}</code></details>}
      <p className="muted mb-3 text-sm">Công thức mới áp dụng khi tạo hồ sơ. Hồ sơ đã có tiếp tục dùng công thức của mình. Nếu cần xử lý lại một trận, mở chi tiết trận để xem thao tác và trạng thái xử lý.</p>
      <p className="muted mb-3 text-sm">Trọng số càng lớn thì kết quả trận mới càng ảnh hưởng đến điểm hồ sơ.</p>
      <details><summary className="mb-3 cursor-pointer text-sm font-medium">Chỉnh cách tính điểm và thời gian lưu lịch sử</summary>
      <fieldset disabled={busy} className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
        {fields.map(([key, label, min, max, step]) => <label className="admin-field" key={key}>{label}
          <input className="input" type="number" required min={min} max={max} step={step} value={settings[key]}
            onChange={(e) => { setSettings({ ...settings, [key]: e.target.valueAsNumber }); setPreview(null); }} />
        </label>)}
      </fieldset></details>
    </Panel>
    <Panel>
      <h2 className="mb-3 text-lg font-semibold">Thử tính điểm với một trận mẫu</h2>
      <p className="muted mb-3 text-sm">Dữ liệu giả lập để so sánh điểm theo công thức đang dùng và công thức mới.</p>
      <details className="mb-4"><summary className="cursor-pointer text-sm font-medium">Chọn hành động gây tiếng ồn và nhập số lần</summary>
      <fieldset disabled={busy} className="mt-3 grid gap-2 sm:grid-cols-2 xl:grid-cols-3">
        <legend className="mb-2 text-sm">Chọn tiếng ồn được tính và nhập số lần xuất hiện trong trận mẫu</legend>
        {data.allowedNoiseTypes.map((type) => <div key={type} className="grid grid-cols-[minmax(0,1fr)_5rem] items-center gap-2">
          <label className="flex flex-1 items-center gap-2 text-sm"><input type="checkbox" checked={settings.noiseTypes.includes(type)}
            onChange={(e) => {
              setSettings({ ...settings, noiseTypes: e.target.checked ? [...settings.noiseTypes, type] : settings.noiseTypes.filter((key) => key !== type) });
              setPreview(null);
            }} /> {noiseLabels[type] ?? "Tiếng ồn khác"}</label>
          <input aria-label={`Số lần ${noiseLabels[type] ?? "phát tiếng ồn"}`} className="input min-w-0 w-full" type="number" min={0} max={100000} value={noiseCounts[type] ?? ""}
            placeholder="0" onChange={(e) => { setNoiseCounts({ ...noiseCounts, [type]: Number(e.target.value) }); setPreview(null); }} />
        </div>)}
      </fieldset></details>
      <fieldset disabled={busy} className="mt-4 grid gap-3 sm:grid-cols-3">
        {([["toolsUsed", "Lượt dùng công cụ", 100000], ["objectiveSeconds", "Thời gian hoàn thành mục tiêu (giây)", 7200], ["previousScore", "Điểm hồ sơ trước trận", 100]] as const)
          .map(([key, label, max]) => <label className="admin-field" key={key}>{label}
            <input className="input" type="number" min={0} max={max} required={key !== "previousScore"} value={sample[key] ?? ""}
              onChange={(e) => { setSample({ ...sample, [key]: e.target.value === "" ? null : e.target.valueAsNumber }); setPreview(null); }} />
          </label>)}
        {([["survived", "Sống sót"], ["completedObjective", "Hoàn thành mục tiêu"]] as const).map(([key, label]) =>
          <label className="flex gap-2 text-sm" key={key}><input type="checkbox" checked={sample[key]}
            onChange={(e) => { setSample({ ...sample, [key]: e.target.checked }); setPreview(null); }} />{label}</label>)}
      </fieldset>
      <button className="button button-secondary mt-4" type="submit" value="preview" disabled={busy}>Xem trước ảnh hưởng</button>
      {preview && <div className="mt-4 grid gap-4 sm:grid-cols-2" role="status">
        {([["Trước thay đổi", preview.before], ["Sau thay đổi", preview.after]] as const).map(([label, result]) =>
          <div className="rounded border border-[var(--line)] p-3" key={label}>
            <h3 className="mb-2 font-medium">{label}</h3>
            <ul className="space-y-1 text-sm">
              {result.scores.map((score) => <li key={score.dimension}>{dimensionLabels[score.dimension] ?? score.dimension} · Điểm trận {score.matchScore?.toFixed(2) ?? "—"} · Điểm hồ sơ {score.updatedScore?.toFixed(2) ?? "—"}</li>)}
              <li>Sự kiện tiếng ồn được tính · {result.countedNoise}</li>
              <li>Điểm tốc độ mục tiêu của đội · {result.objectiveSpeed?.toFixed(2) ?? "—"}</li>
              <li>Lịch sử · {result.historyMatches} trận, {result.historyOpportunities} tình huống, tối đa {result.historyMaxAgeDays} ngày</li>
              <li>Trọng số trận 30 ngày trước · {result.thirtyDayHistoryWeight.toFixed(3)}</li>
            </ul>
          </div>)}
      </div>}
    </Panel>
    <div>
      <button className="button" type="submit" value="publish" disabled={busy || !preview}>Xuất bản phiên bản mới</button>
      {message && <p role="alert" className="mt-3 text-sm text-red-300">{message}</p>}
    </div>
  </form>;
}
