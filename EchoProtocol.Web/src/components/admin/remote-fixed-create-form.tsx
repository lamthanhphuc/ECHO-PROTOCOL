"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import type { RemoteGameplayField } from "@/lib/types/profile-tuning";
import type { MatchContent } from "@/lib/types/operations";

const initial = {
  maxHealth: 100, walkSpeed: 4, sprintSpeed: 7, maxStamina: 100,
  patrolSpeed: 3, chaseSpeed: 6, roamSpeed: 3, trackSpeed: 5, attackDamage: 10,
};
const fields = [
  ["maxHealth", "Máu tối đa", 1, 500], ["walkSpeed", "Tốc độ đi", .5, 15],
  ["sprintSpeed", "Tốc độ chạy", .5, 25], ["maxStamina", "Thể lực tối đa", 1, 500],
  ["patrolSpeed", "Tốc độ tuần tra", .5, 20], ["chaseSpeed", "Tốc độ truy đuổi", .5, 25],
  ["roamSpeed", "Tốc độ lang thang", .5, 20], ["trackSpeed", "Tốc độ theo dấu", .5, 25],
  ["attackDamage", "Sát thương tấn công", 1, 100],
] as const;

export function RemoteFixedCreateForm({ gameplayFields, content }: { gameplayFields: RemoteGameplayField[]; content: MatchContent }) {
  const router = useRouter();
  const [version, setVersion] = useState("");
  const [variantIndex, setVariantIndex] = useState(0);
  const variant = content.entries[variantIndex];
  const mapId = variant?.map;
  const [gameVersion, setGameVersion] = useState(content.gameVersions[0] ?? "");
  const [difficulty, setDifficulty] = useState("NORMAL");
  const [values, setValues] = useState(initial);
  const [overrides, setOverrides] = useState<Record<string, string>>({});
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState("");
  const visibleFields = gameplayFields.filter((field) => !field.map || field.map === mapId);

  async function submit(event: React.FormEvent) {
    event.preventDefault(); setMessage("");
    if (!variant || !content.gameVersions.includes(gameVersion) || !content.difficulties.includes(difficulty))
      return setMessage("Hãy chọn nội dung và phiên bản Unity tương thích.");
    if (Object.values(values).some((value) => !Number.isFinite(value))) return setMessage("Mọi thông số phải là số hợp lệ.");
    if (values.sprintSpeed < values.walkSpeed) return setMessage("Tốc độ chạy phải lớn hơn hoặc bằng tốc độ đi.");
    if (values.chaseSpeed < values.patrolSpeed) return setMessage("Tốc độ truy đuổi phải lớn hơn hoặc bằng tốc độ tuần tra.");
    const gameplay = visibleFields.filter((field) => overrides[field.key]?.trim()).map((field) =>
      ({ key: field.key, value: Number(overrides[field.key]) }));
    const snapshotJson = JSON.stringify({
      schemaVersion: "RFB-2",
      player: { maxHealth: values.maxHealth, walkSpeed: values.walkSpeed, sprintSpeed: values.sprintSpeed, maxStamina: values.maxStamina },
      stalker: { patrolSpeed: values.patrolSpeed, chaseSpeed: values.chaseSpeed },
      minion: { roamSpeed: values.roamSpeed, trackSpeed: values.trackSpeed, attackDamage: values.attackDamage },
      gameplay,
      content: { gameVersion, mode: variant.mode, monster: variant.monster, route: variant.route },
    });
    setBusy(true);
    try {
      const response = await fetch("/api/bff/admin/remote-fixed/versions", {
        method: "POST", headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ version, mapId, difficulty, snapshotJson }),
      });
      const payload = await response.json();
      if (!response.ok || !payload.success) throw new Error(payload.message || "Không tạo được cấu hình.");
      setVersion(""); setOverrides({}); setMessage(`Đã tạo ${payload.data.version}. Cấu hình chưa được kích hoạt.`); router.refresh();
    } catch (error) { setMessage(error instanceof Error ? error.message : "Không kết nối được máy chủ."); }
    finally { setBusy(false); }
  }

  return <form onSubmit={submit} className="grid gap-5">
    <div className="grid gap-3 md:grid-cols-3">
      <label className="admin-field">Mã cấu hình<input className="input" required maxLength={80} value={version} onChange={(e) => setVersion(e.target.value)} /></label>
      <label className="admin-field">Map · chế độ · quái · tuyến<select className="input" value={variantIndex}
        onChange={(e) => setVariantIndex(Number(e.target.value))}>
        {content.entries.map((entry, index) => <option key={index} value={index}>{entry.map} · {entry.mode} · {entry.monster} · {entry.route}</option>)}
      </select></label>
      <label className="admin-field">Độ khó<select className="input" value={difficulty} onChange={(e) => setDifficulty(e.target.value)}>
        {content.difficulties.map((level) => <option key={level} value={level}>{({ EASY: "Dễ", NORMAL: "Bình thường", HARD: "Khó" } as Record<string, string>)[level] ?? level}</option>)}
      </select></label>
      <label className="admin-field">Phiên bản Unity tương thích<select required className="input" value={gameVersion} onChange={(e) => setGameVersion(e.target.value)}>
        {content.gameVersions.map((build) => <option key={build}>{build}</option>)}
      </select></label>
    </div>
    {[{ title: "Người chơi", start: 0, end: 4 }, { title: "Kẻ săn đuổi", start: 4, end: 6 }, { title: "Quái phụ", start: 6, end: 9 }].map((group) =>
      <fieldset key={group.title} className="rounded border border-[var(--line)] p-4"><legend className="px-2 font-semibold">{group.title}</legend><div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
        {fields.slice(group.start, group.end).map(([key, label, min, max]) => <label className="admin-field" key={key}>{label}<input className="input" type="number" required step="any" min={min} max={max} value={values[key]} onChange={(e) => setValues({ ...values, [key]: e.target.valueAsNumber })} /></label>)}
      </div></fieldset>)}
    <details className="rounded border border-[var(--line)] p-4">
      <summary className="cursor-pointer font-semibold">Cân bằng gameplay mở rộng</summary>
      <p className="muted my-3 text-sm">Để trống để dùng mặc định của bản đồ và độ khó. Trận dùng tùy chỉnh cần build hỗ trợ RFB-2.</p>
      {[...new Set(visibleFields.map((field) => field.group))].map((group) => <fieldset key={group} className="mt-3 rounded border border-[var(--line)] p-3">
        <legend className="px-2">{group}</legend>
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
          {visibleFields.filter((field) => field.group === group).map((field) => <label className="admin-field" key={field.key}>{field.label}
            <input className="input" type="number" min={field.min} max={field.max} step={field.integral ? 1 : "any"}
              placeholder={`Mặc định · ${field.min}–${field.max}`} value={overrides[field.key] ?? ""}
              onChange={(e) => setOverrides({ ...overrides, [field.key]: e.target.value })} />
          </label>)}
        </div>
      </fieldset>)}
    </details>
    <div><button className="button" disabled={busy}>{busy ? "Đang tạo…" : "Tạo cấu hình"}</button>{message && <p role="status" className="mt-3 text-sm">{message}</p>}</div>
  </form>;
}
