"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";
import { Panel } from "@/components/ui/panel";
import { adaptiveParameterLabels, adaptiveReasons } from "@/lib/types/adaptive";
import type { AdaptiveAdminData, AdaptiveConfigRef, AdaptiveConfiguration, AdaptivePreview } from "@/lib/types/adaptive";

const preMatchFields = [
  ["survivalLow", "Ngưỡng sinh tồn thấp", 0.01, 99.99, .01],
  ["survivalHigh", "Ngưỡng sinh tồn cao", 0.01, 99.99, .01],
  ["noiseLow", "Ngưỡng tiếng ồn thấp", 0.01, 99.99, .01],
  ["noiseHigh", "Ngưỡng tiếng ồn cao", 0.01, 99.99, .01],
  ["minimumSurvivalSamples", "Mẫu sinh tồn tối thiểu / người", 1, 1000, 1],
  ["minimumNoiseSamples", "Mẫu tiếng ồn tối thiểu / người", 1, 1000, 1],
] as const;

const pilotFields = [
  ["observationSeconds", "Quan sát tối thiểu (giây)", 30, 115, 1],
  ["windowSeconds", "Cửa sổ áp lực (giây)", 35, 120, 1],
  ["evidenceMaxAgeSeconds", "Tuổi dữ liệu tối đa (giây)", 5, 10, 1],
  ["reliefThreshold", "Ngưỡng giảm áp lực", .45, .65, .01],
  ["increasePressureCeiling", "Trần áp lực để được tăng", .10, .25, .01],
  ["downRecoverySeconds", "Bảo vệ sau khi gục hoặc được cứu (giây)", 20, 60, 1],
  ["reliefCooldownSeconds", "Giữ mức trước khi giảm tiếp (giây)", 90, 600, 1],
  ["increaseCooldownSeconds", "Giữ mức trước khi tăng tiếp (giây)", 180, 900, 1],
  ["ordinarySupportQuota", "Quota hỗ trợ thường", 0, 2, 1],
  ["emergencySupportQuota", "Quota hỗ trợ khẩn cấp", 0, 1, 1],
  ["supportCooldownSeconds", "Hồi hỗ trợ thường (giây)", 90, 600, 1],
  ["tierDwellSeconds", "Thời gian chờ hạ tier cảm nhận (giây)", 12, 30, 1],
  ["protectionSeconds", "Bảo vệ trong tủ (giây)", 10, 15, 1],
  ["protectionCooldownSeconds", "Hồi bảo vệ tủ (giây)", 120, 300, 1],
  ["protectionQuota", "Quota bảo vệ tủ / người", 0, 2, 1],
] as const;

const safetyLabels: Record<string, string> = {
  tierHysteresis: "Biên chuyển tier cảm nhận",
  protectionGraceSeconds: "Bảo vệ khi ra tủ (giây)",
  seekRecoverySeconds: "Phục hồi trước khi Stalker tìm lại (giây)",
  ambushCooldownSeconds: "Hồi phục kích tủ (giây)",
  sharedSpecialSeparationSeconds: "Khoảng cách giữa các chiêu đặc biệt (giây)",
};

const sample = { survival: 25 as number | null, noise: 50 as number | null, survivalSamples: 3, noiseSamples: 3 };
const sampleFields = [["survival", "Sinh tồn"], ["noise", "Tiếng ồn"],
  ["survivalSamples", "Mẫu sinh tồn"], ["noiseSamples", "Mẫu tiếng ồn"]] as const;

const relativeFields: Record<string, { min: number; max: number; baseline: number; neutral: number; adverse: number; unit: string }> = {
  DetectionAcquireSeconds: { min: .25, max: 8, baseline: 1.25, neutral: 1.25, adverse: -1, unit: "giây" },
  PostChaseCooldownSeconds: { min: 12, max: 28, baseline: 18, neutral: 18, adverse: -1, unit: "giây" },
  HearingMultiplier: { min: .25, max: 2, baseline: 1, neutral: 1, adverse: 1, unit: "" },
  SearchSeconds: { min: .25, max: 30, baseline: 2.25, neutral: 2.25, adverse: 1, unit: "giây" },
  ChaseSpeed: { min: .5, max: 25, baseline: 10, neutral: 7.5, adverse: 1, unit: "m/s" },
  SpecialCooldownSeconds: { min: 30, max: 900, baseline: 420, neutral: 420, adverse: -1, unit: "giây" },
  Zone1MinionCap: { min: 0, max: 3, baseline: 3, neutral: 1, adverse: 1, unit: "Minion" },
  Zone2MinionCap: { min: 0, max: 5, baseline: 5, neutral: 2, adverse: 1, unit: "Minion" },
  Zone3AdaptiveMinionCap: { min: 0, max: 4, baseline: 0, neutral: 0, adverse: 1, unit: "Minion" },
};
const contextualRules: Record<string, string> = {
  OptionalToolBudget: "Điều chỉnh một suất tùy chọn quanh phân bổ đã khóa. Hệ thống xét từ Khu vực 3 về Khu vực 1 và giữ nguyên nếu khu vực được chọn đã phát vật phẩm hoặc thiếu điểm đặt.",
  ResourceGrade: "Thay đổi cấp bố trí cho vật phẩm chưa phát. Mỗi lần đổi một nấc và cần dữ liệu nhu cầu hoặc lịch sử sử dụng phù hợp.",
  CabinetAmbushEnabled: "Cho phép hoặc khóa phục kích tủ trong Khu vực 1 và 2. Game vẫn kiểm tra dữ liệu, khoảng cách, cooldown và chốt dồn áp lực trước khi thực hiện.",
};

function optionKey(reference: AdaptiveConfigRef | null) {
  return reference ? JSON.stringify([reference.id, reference.version]) : "";
}

export function AdaptiveConfigurationForm({ data, compatibility }: { data: AdaptiveAdminData; compatibility: string }) {
  const router = useRouter();
  const [configuration, setConfiguration] = useState(data.configuration);
  const [players, setPlayers] = useState([{ ...sample }]);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState("");
  const [preview, setPreview] = useState<AdaptivePreview | null>(null);
  const [relativeKey, setRelativeKey] = useState("ChaseSpeed");
  const [relativeBasis, setRelativeBasis] = useState(10);
  const [patrolBasis, setPatrolBasis] = useState(6.5);
  const [relativeCurrent, setRelativeCurrent] = useState(9);
  const [optionalZones, setOptionalZones] = useState([false, false, false]);
  const [spawnedZones, setSpawnedZones] = useState([true, false, false]);
  const field = relativeFields[relativeKey];
  const speed = relativeKey === "ChaseSpeed";
  const min = speed ? patrolBasis : field?.min ?? 0;
  const validPreview = field && [relativeBasis, relativeCurrent, min].every(Number.isFinite) &&
    relativeBasis >= min && relativeBasis <= field.max && relativeCurrent >= min && relativeCurrent <= field.max &&
    (!speed || patrolBasis >= .5 && patrolBasis <= 20) &&
    (!relativeKey.startsWith("Zone") || Number.isInteger(relativeBasis) && Number.isInteger(relativeCurrent));
  const fixed = data.options.find((option) => optionKey(option) === optionKey(configuration.fixed));

  function update(next: AdaptiveConfiguration) {
    setConfiguration(next);
    setPreview(null);
  }

  function loadSample(kind: "complete" | "missing" | "hold") {
    const counts = {
      survivalSamples: configuration.minimumSurvivalSamples,
      noiseSamples: configuration.minimumNoiseSamples,
    };
    setPlayers([kind === "missing"
      ? { survival: null, noise: null, survivalSamples: 0, noiseSamples: 0 }
      : { ...counts, survival: kind === "hold" ? configuration.survivalLow : 100, noise: 100 }]);
    setPreview(null);
    setMessage("");
  }

  async function submit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const submitter = (event.nativeEvent as SubmitEvent).submitter;
    const publish = submitter?.getAttribute("value") === "publish";
    setBusy(true);
    setMessage("");
    setPreview(null);
    try {
      const response = await fetch(publish ? "/api/bff/admin/adaptive" : "/api/bff/admin/adaptive/preview", {
        method: "POST", headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ compatibility, configuration, ...(publish ? { expectedVersion: data.version } : { players }) }),
      });
      const payload = await response.json();
      if (!response.ok || !payload.success) throw new Error(payload.message || "Không lưu được kết quả.");
      if (publish) router.refresh();
      else setPreview(payload.data);
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "Không kết nối được máy chủ.");
    } finally {
      setBusy(false);
    }
  }

  function options(field: "fixed" | "relief" | "increase") {
    if (field === "fixed") return data.options.filter((option) => option.source === "Fixed");
    if (!fixed) return [];
    return data.options.filter((option) => option.source === "Adaptive" && option.fallbackConfigId === fixed.id &&
      option.fallbackConfigVersion === fixed.version && (field === "relief"
        ? option.supportItemBudget === fixed.supportItemBudget + 1 && option.chaseSpeed === fixed.chaseSpeed
        : option.supportItemBudget === fixed.supportItemBudget &&
          (fixed.chaseSpeed === 9 && option.chaseSpeed === 10 || fixed.chaseSpeed === 7.5 && option.chaseSpeed === 8)));
  }

  return <form className="grid gap-5" onSubmit={submit}>
    <Panel>
      <h2 className="text-lg font-semibold">Adaptive trước trận</h2>
      <p className="muted my-3 break-all text-sm">{data.version ? `Bản đang dùng · ${data.version}` : "Đang dùng cấu hình mặc định."}</p>
      <fieldset disabled={busy} className="grid gap-4">
        <label className="flex items-center gap-2"><input type="checkbox" checked={configuration.enabled} onChange={(e) => update({ ...configuration, enabled: e.target.checked })} /> Bật policy trước trận</label>
        <label className="flex items-center gap-2"><input type="checkbox" checked={configuration.requireFullRoster} onChange={(e) => update({ ...configuration, requireFullRoster: e.target.checked })} /> Yêu cầu hồ sơ đủ điều kiện của toàn đội</label>
        <p className="muted text-sm">Khi thiếu hồ sơ, chỉ được xét giảm áp lực nếu tắt yêu cầu đủ đội. Tăng áp lực luôn cần đủ đội.</p>
        <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
          {preMatchFields.map(([key, label, min, max, step]) => <label className="admin-field" key={key}>{label}
            <input className="input" type="number" required min={min} max={max} step={step} value={configuration[key]}
              onChange={(e) => update({ ...configuration, [key]: e.target.valueAsNumber })} />
          </label>)}
        </div>
        <div className="grid gap-3 sm:grid-cols-3">
          {([["fixed", "Cấu hình dự phòng"], ["relief", "Ứng viên giảm áp lực"], ["increase", "Ứng viên tăng áp lực"]] as const).map(([field, label]) =>
            <label className="admin-field" key={field}>{label}
              <select className="input" required={field === "fixed"} value={optionKey(configuration[field])} onChange={(e) => {
                const selected = data.options.find((option) => optionKey(option) === e.target.value);
                const reference = selected ? { id: selected.id, version: selected.version } : null;
                update(field === "fixed" ? { ...configuration, fixed: reference, relief: null, increase: null }
                  : { ...configuration, [field]: reference });
              }}>
                <option value="">{field === "fixed" ? "Chọn cấu hình đã duyệt" : "Dùng Fixed"}</option>
                {options(field).map((option) => <option key={optionKey(option)} value={optionKey(option)}>{option.id} · {option.version}</option>)}
              </select>
            </label>)}
        </div>
      </fieldset>
    </Panel>
    <Panel>
      <h2 className="mb-3 text-lg font-semibold">Adaptive v0.2 giữa trận</h2>
      <p className="muted mb-2 text-sm">Pilot trên backend · {data.pilotEnabled ? "Đang bật" : "Đang tắt"}</p>
      <div className="mb-4 space-y-3 rounded border border-[var(--line)] p-3 text-sm">
        <h3 className="font-medium">Xem thử điều chỉnh · Cơ sở nghiên cứu / Thường</h3>
        <p className="muted">Nhập giá trị nền đã khóa và giá trị đang áp dụng. Nấc mới luôn tính từ nền. Khi policy giữ nguyên, trận tiếp tục dùng giá trị đang áp dụng.</p>
        <label className="admin-field">Thông số
          <select className="input" value={relativeKey} onChange={(event) => {
            const key = event.target.value;
            const baseline = relativeFields[key]?.baseline ?? 0;
            setRelativeKey(key);
            setRelativeBasis(baseline);
            setRelativeCurrent(baseline);
          }}>
            {[...Object.keys(relativeFields), ...Object.keys(contextualRules)].map((key) =>
              <option key={key} value={key}>{adaptiveParameterLabels[key] ?? key}</option>)}
          </select>
        </label>
        {field ? <>
          <div className="grid gap-3 sm:grid-cols-3">
            {speed && <label className="admin-field">Tốc độ tuần tra nền (m/s)<input className="input" type="number" step="any" value={Number.isNaN(patrolBasis) ? "" : patrolBasis} onChange={(e) => setPatrolBasis(e.target.valueAsNumber)} /></label>}
            <label className="admin-field">Giá trị nền {field.unit && `(${field.unit})`}<input className="input" type="number" step="any" value={Number.isNaN(relativeBasis) ? "" : relativeBasis} onChange={(e) => setRelativeBasis(e.target.valueAsNumber)} /></label>
            <label className="admin-field">Đang áp dụng {field.unit && `(${field.unit})`}<input className="input" type="number" step="any" value={Number.isNaN(relativeCurrent) ? "" : relativeCurrent} onChange={(e) => setRelativeCurrent(e.target.valueAsNumber)} /></label>
          </div>
          {validPreview ? <ul className="list-disc pl-5">{(data.catalog[relativeKey]?.tiers ?? []).map((tier) => {
            const direction = Math.sign(tier - field.neutral);
            const projected = direction === 0 ? relativeBasis : relativeKey.startsWith("Zone")
              ? relativeBasis + direction : Math.round(Math.fround(relativeBasis) * (1 + direction / 10) * 100) / 100;
            const reason = speed && direction !== 0 && !data.chaseSpeedAdminVerified ? "build chưa có bằng chứng" :
              !configuration.parameters[relativeKey]?.tiers.includes(tier) ? "nấc đang tắt" :
              projected < min || projected > field.max || direction !== 0 && projected === relativeBasis ? "chạm giới hạn an toàn" : "";
            const action = direction === 0 ? "Về nền" : direction === field.adverse ? "Tăng áp lực" : "Giảm áp lực";
            return <li key={tier}>{action} · {relativeBasis} → {projected} → <strong>{reason ? relativeCurrent : projected} {field.unit}</strong>{reason && ` · Giữ nguyên, ${reason}`}</li>;
          })}<li>Giữ nguyên → <strong>{relativeCurrent} {field.unit}</strong></li></ul>
            : <p className="muted">Nhập giá trị hợp lệ trong khoảng {min}–{field.max} {field.unit}.</p>}
        </> : <>
          <p className="muted">{contextualRules[relativeKey]}</p>
          {relativeKey === "OptionalToolBudget" && <>
            <div className="grid gap-2 sm:grid-cols-3">
              {optionalZones.map((optional, index) => <fieldset key={index} className="rounded border border-[var(--line)] p-2">
                <legend>Khu vực {index + 1}</legend>
                <label className="flex gap-2"><input type="checkbox" checked={optional} onChange={(e) =>
                  setOptionalZones((zones) => zones.map((value, zone) => zone === index ? e.target.checked : value))
                } />Có suất tùy chọn ở nền</label>
                <label className="flex gap-2"><input type="checkbox" checked={spawnedZones[index]} onChange={(e) =>
                  setSpawnedZones((zones) => zones.map((value, zone) => zone === index ? e.target.checked : value))
                } />Đã phát công cụ</label>
              </fieldset>)}
            </div>
            <ul className="list-disc pl-5">{([-1, 1] as const).map((step) => {
              const zone = optionalZones.lastIndexOf(step === -1);
              const action = step < 0 ? "Bớt một suất" : "Thêm một suất";
              return <li key={step}>{action} · {zone < 0 ? "Giữ nguyên, chạm giới hạn" :
                spawnedZones[zone] ? `Giữ nguyên, Khu vực ${zone + 1} đã phát công cụ` :
                `Khu vực ${zone + 1} có ${step < 0 ? 5 : 6} công cụ nếu vị trí và bằng chứng hợp lệ`}</li>;
            })}</ul>
          </>}
          {relativeKey === "ResourceGrade" && <p>Các cấp từ thấp đến cao: phòng phụ ưu tiên → bố trí nền → ưu tiên tuyến chính → bố trí hỗ trợ → hỗ trợ khẩn cấp.</p>}
          {relativeKey === "CabinetAmbushEnabled" && <p>Tắt → không tạo phục kích mới. Bật → đủ điều kiện để game xét phục kích tại cửa sổ an toàn.</p>}
        </>}
      </div>
      <p className="muted mb-4 text-sm">Các nấc dưới đây là giá trị policy. Trong luồng tự động, chúng chọn hướng điều chỉnh quanh nền của trận. Relay và xe hộ tống dùng giá trị nền.</p>
      <fieldset disabled={busy} className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
        {Object.entries(data.catalog).map(([key, approved]) => {
          const value = configuration.parameters[key];
          const speedLocked = key === "ChaseSpeed" && !data.chaseSpeedAdminVerified;
          const setParameter = (next: typeof value) => update({ ...configuration, parameters: { ...configuration.parameters, [key]: next } });
          return <fieldset key={key} className="rounded border border-[var(--line)] px-3 pb-3">
            <legend className="px-1 text-sm font-medium">{adaptiveParameterLabels[key] ?? key}</legend>
            <label className="admin-field">Giá trị gốc
              <select className="input" disabled={approved.baselineLocked} value={value.baseline}
                onChange={(e) => setParameter({ ...value, baseline: Number(e.target.value) })}>
                {value.tiers.filter((tier) =>
                  (key !== "Zone1MinionCap" && key !== "Zone2MinionCap") || tier <= approved.baseline
                ).map((tier) => <option key={tier} value={tier}>{tier}</option>)}
              </select>
            </label>
            <div className="mt-2 flex flex-wrap gap-3">
              {approved.tiers.map((tier) => <label className="flex items-center gap-1 text-sm" key={tier}>
                <input type="checkbox" checked={value.tiers.includes(tier)} disabled={speedLocked || tier === approved.baseline || tier === value.baseline}
                  onChange={(e) => setParameter({ ...value, tiers: approved.tiers.filter((item) =>
                    item === tier ? e.target.checked : value.tiers.includes(item)) })} /> {tier}
              </label>)}
            </div>
            {approved.baselineLocked && <p className="muted mt-2 text-xs">{speedLocked
              ? "Chờ bản build xác nhận Stalker dùng tốc độ đã duyệt."
              : "Giá trị gốc do quy tắc khởi tạo hoặc bằng chứng trong trận quyết định."}</p>}
          </fieldset>;
        })}
      </fieldset>
    </Panel>
    <Panel>
      <h2 className="mb-3 text-lg font-semibold">Policy và chốt an toàn</h2>
      <fieldset disabled={busy} className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
        {pilotFields.map(([key, label, min, max, step]) => <label className="admin-field" key={key}>{label}
          <input className="input" type="number" required min={min} max={max} step={step} value={configuration.pilot[key]}
            onChange={(e) => update({ ...configuration, pilot: { ...configuration.pilot, [key]: e.target.valueAsNumber } })} />
        </label>)}
      </fieldset>
      <fieldset disabled={busy} className="mt-4 grid gap-3 sm:grid-cols-3 xl:grid-cols-5">
        <legend className="mb-2 text-sm">Trọng số áp lực · tổng bằng 1</legend>
        {["Stalker", "Minion", "Gục", "Tài nguyên", "Mục tiêu"].map((label, index) => <label className="admin-field" key={label}>{label}
          <input className="input" type="number" required min={.05} max={.5} step={.01} value={configuration.pilot.pressureWeights[index]}
            onChange={(e) => update({ ...configuration, pilot: { ...configuration.pilot,
              pressureWeights: configuration.pilot.pressureWeights.map((value, i) => i === index ? e.target.valueAsNumber : value) } })} />
        </label>)}
      </fieldset>
      <details className="mt-5 text-sm">
        <summary className="cursor-pointer font-semibold">Chốt chỉ xem và lý do Hold</summary>
        <dl className="my-3 grid gap-2 sm:grid-cols-2">
          {Object.entries(data.safety).map(([key, value]) => <div key={key}><dt className="muted">{safetyLabels[key] ?? key}</dt><dd>{value}</dd></div>)}
        </dl>
        <ul className="list-disc space-y-1 pl-5">
          <li>Final Phase khóa mọi thay đổi tăng áp lực. Một lần đổi giữa trận chỉ được đổi một thông số và đi một nấc.</li>
          <li>Mỗi kế hoạch có tối đa một thay đổi tăng áp lực. Khi chuyển nguồn gây áp lực, hệ thống kiểm tra hiệu ứng cũ đã được gỡ.</li>
          <li>Quyết định chờ áp dụng có thể hủy. Nếu Host đã commit nhưng hiệu ứng lỗi, hệ thống giữ revision để đối soát và chỉ hoàn tác qua cửa sổ an toàn.</li>
          {Object.entries(adaptiveReasons).filter(([code]) => code.startsWith("HOLD_")).map(([code, label]) =>
            <li key={code}><code>{code}</code> · {label}</li>)}
        </ul>
      </details>
    </Panel>
    <Panel>
      <h2 className="mb-3 text-lg font-semibold">Chạy thử với hồ sơ mẫu</h2>
      <p className="muted mb-3 text-sm">Bỏ trống điểm để mô phỏng thiếu hồ sơ. Chạy thử dùng nội dung đang nhập và không lưu trận. Các chốt v0.2 được kiểm tra khi Host bắt đầu trận.</p>
      <fieldset disabled={busy} className="grid gap-3">
        <div className="flex flex-wrap gap-2">
          <button className="button button-secondary" type="button" onClick={() => loadSample("complete")}>Đủ dữ liệu</button>
          <button className="button button-secondary" type="button" onClick={() => loadSample("missing")}>Thiếu dữ liệu</button>
          <button className="button button-secondary" type="button" onClick={() => loadSample("hold")}>Giữ nguyên</button>
        </div>
        {players.map((player, index) => <div key={index} className="grid gap-2 rounded border border-[var(--line)] p-3 sm:grid-cols-4">
          <p className="sm:col-span-4">Người chơi {index + 1}</p>
          {sampleFields.map(([key, label]) => <label className="admin-field" key={key}>{label}
            <input className="input" type="number" min={0} max={key.endsWith("Samples") ? 2147483647 : 100}
              required={key.endsWith("Samples")} step={key.endsWith("Samples") ? 1 : .01} value={player[key] ?? ""}
              onChange={(e) => {
                const value = e.target.value === "" ? null : e.target.valueAsNumber;
                setPlayers(players.map((item, i) => i === index ? { ...item, [key]: value } : item));
                setPreview(null);
              }} />
          </label>)}
        </div>)}
        <div className="flex flex-wrap gap-3">
          <button className="button button-secondary" type="button" disabled={players.length === 4}
            onClick={() => { setPlayers([...players, { ...sample }]); setPreview(null); }}>Thêm người</button>
          <button className="button button-secondary" type="button" disabled={players.length === 1}
            onClick={() => { setPlayers(players.slice(0, -1)); setPreview(null); }}>Bớt người</button>
          <button className="button button-secondary" type="submit" value="preview">Chạy thử</button>
        </div>
      {preview && <div role="status" className="space-y-1 rounded border border-[var(--line)] p-3 text-sm">
        <p>{preview.usedFixedFallback ? "Cấu hình dự phòng" : "Ứng viên được chọn"} · {preview.selected.id} · {preview.selected.version}</p>
        {preview.rule && <details className="muted"><summary className="cursor-pointer">Chi tiết kỹ thuật</summary><code>{preview.rule}</code></details>}
        {preview.reason && <p>{adaptiveReasons[preview.reason] ?? preview.reason}</p>}
      </div>}
      </fieldset>
    </Panel>
    <div>
      <button className="button" type="submit" value="publish" disabled={busy}>{busy ? "Đang xử lý…" : "Xuất bản cho trận mới"}</button>
      {message && <p role="alert" className="mt-3 text-sm text-red-300">{message}</p>}
    </div>
  </form>;
}
