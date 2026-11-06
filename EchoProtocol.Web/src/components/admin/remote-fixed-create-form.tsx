"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";

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

export function RemoteFixedCreateForm() {
  const router = useRouter();
  const [version, setVersion] = useState("");
  const [mapId, setMapId] = useState<"SciFi" | "Scene_A">("SciFi");
  const [difficulty, setDifficulty] = useState<"EASY" | "NORMAL" | "HARD">("NORMAL");
  const [values, setValues] = useState(initial);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState("");

  async function submit(event: React.FormEvent) {
    event.preventDefault(); setMessage("");
    if (Object.values(values).some((value) => !Number.isFinite(value))) return setMessage("Mọi thông số phải là số hợp lệ.");
    if (values.sprintSpeed < values.walkSpeed) return setMessage("Tốc độ chạy phải lớn hơn hoặc bằng tốc độ đi.");
    if (values.chaseSpeed < values.patrolSpeed) return setMessage("Tốc độ truy đuổi phải lớn hơn hoặc bằng tốc độ tuần tra.");
    const snapshotJson = JSON.stringify({
      schemaVersion: "RFB-1",
      player: { maxHealth: values.maxHealth, walkSpeed: values.walkSpeed, sprintSpeed: values.sprintSpeed, maxStamina: values.maxStamina },
      stalker: { patrolSpeed: values.patrolSpeed, chaseSpeed: values.chaseSpeed },
      minion: { roamSpeed: values.roamSpeed, trackSpeed: values.trackSpeed, attackDamage: values.attackDamage },
    });
    setBusy(true);
    try {
      const response = await fetch("/api/bff/admin/remote-fixed/versions", {
        method: "POST", headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ version, mapId, difficulty, snapshotJson }),
      });
      const payload = await response.json();
      if (!response.ok || !payload.success) throw new Error(payload.message || "Không tạo được cấu hình.");
      setVersion(""); setMessage(`Đã tạo ${payload.data.version}. Cấu hình chưa được kích hoạt.`); router.refresh();
    } catch (error) { setMessage(error instanceof Error ? error.message : "Không kết nối được máy chủ."); }
    finally { setBusy(false); }
  }

  return <form onSubmit={submit} className="grid gap-5">
    <div className="grid gap-3 md:grid-cols-3">
      <label className="admin-field">Mã cấu hình<input className="input" required maxLength={80} value={version} onChange={(e) => setVersion(e.target.value)} /></label>
      <label className="admin-field">Bản đồ<select className="input" value={mapId} onChange={(e) => setMapId(e.target.value as typeof mapId)}><option>SciFi</option><option>Scene_A</option></select></label>
      <label className="admin-field">Độ khó<select className="input" value={difficulty} onChange={(e) => setDifficulty(e.target.value as typeof difficulty)}><option value="EASY">Dễ</option><option value="NORMAL">Bình thường</option><option value="HARD">Khó</option></select></label>
    </div>
    {[{ title: "Người chơi", start: 0, end: 4 }, { title: "Kẻ săn đuổi", start: 4, end: 6 }, { title: "Quái phụ", start: 6, end: 9 }].map((group) =>
      <fieldset key={group.title} className="rounded border border-[var(--line)] p-4"><legend className="px-2 font-semibold">{group.title}</legend><div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
        {fields.slice(group.start, group.end).map(([key, label, min, max]) => <label className="admin-field" key={key}>{label}<input className="input" type="number" required step="any" min={min} max={max} value={values[key]} onChange={(e) => setValues({ ...values, [key]: e.target.valueAsNumber })} /></label>)}
      </div></fieldset>)}
    <div><button className="button" disabled={busy}>{busy ? "Đang tạo…" : "Tạo cấu hình"}</button>{message && <p role="status" className="mt-3 text-sm">{message}</p>}</div>
  </form>;
}
