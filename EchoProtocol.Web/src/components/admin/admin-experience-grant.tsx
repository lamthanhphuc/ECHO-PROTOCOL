"use client";

import { useEffect, useRef, useState, type FormEvent } from "react";
import { useRouter } from "next/navigation";

type Grant = { id: string; amount: number; levelBefore: number; levelAfter: number; experienceAfter: number;
  adminUserId: string; reason: string; createdAtUtc: string };
type Progression = { profile: { level: number; experiencePoints: number; lifetimeExperiencePoints: number }; grants: Grant[] };

export function AdminExperienceGrant({ userId, recipient }: { userId: string; recipient: string }) {
  const router = useRouter();
  const [data, setData] = useState<Progression | null>(null);
  const [amount, setAmount] = useState("250");
  const [reason, setReason] = useState("");
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState("");
  const requestId = useRef<string | null>(null);
  const endpoint = `/api/bff/admin/users/${encodeURIComponent(userId)}/experience`;
  useEffect(() => {
    const controller = new AbortController();
    fetch(endpoint, { signal: controller.signal }).then(r => r.json()).then(body => {
      if (body.success) setData(body.data); else setMessage(body.message || "Không tải được tiến độ XP.");
    }).catch(error => { if (error.name !== "AbortError") setMessage("Không tải được tiến độ XP."); });
    return () => controller.abort();
  }, [endpoint]);

  async function submit(event: FormEvent) {
    event.preventDefault();
    if (!data || busy) return;
    const value = Number(amount);
    if (!Number.isInteger(value) || value < 1 || value > 100000 || !reason.trim()) return;
    if (!window.confirm(`Cấp ${value.toLocaleString("vi-VN")} XP cho ${recipient}?\nLý do: ${reason.trim()}\nKhông thay đổi Credits, số trận hoặc số trận thắng.`)) return;
    requestId.current ??= crypto.randomUUID();
    setBusy(true); setMessage("");
    try {
      const response = await fetch(endpoint, { method: "POST", headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ requestId: requestId.current, amount: value, reason: reason.trim() }) });
      const body = await response.json();
      if (!response.ok || !body.success) throw new Error(body.message || "Không cấp được XP.");
      setMessage(`Đã cộng ${body.data.amount.toLocaleString("vi-VN")} XP · Cấp ${body.data.levelBefore} → ${body.data.levelAfter}.`);
      requestId.current = null;
      setAmount(""); setReason("");
      const refreshed = await fetch(endpoint).then(r => r.json()).catch(() => null);
      if (refreshed?.success) setData(refreshed.data);
      router.refresh();
    } catch (error) { setMessage(error instanceof Error ? error.message : "Không kết nối được máy chủ. Có thể thử lại an toàn."); }
    finally { setBusy(false); }
  }

  return <section className="mt-5 rounded-lg border border-white/10 bg-black/20 p-5">
    <h2 className="text-lg font-semibold">Cấp XP cho người dùng</h2>
    <p className="muted mt-2 text-sm">{data ? `Cấp ${data.profile.level} · ${data.profile.experiencePoints.toLocaleString("vi-VN")} XP tiến trình · ${data.profile.lifetimeExperiencePoints.toLocaleString("vi-VN")} XP đã kiếm` : "Đang tải tiến độ…"}</p>
    <form onSubmit={submit} className="mt-4 grid gap-4 sm:grid-cols-2">
      <div className="flex flex-wrap gap-2 sm:col-span-2">{[250, 500, 1000, 5000].map(value =>
        <button key={value} className="button button-secondary" type="button" disabled={busy}
          onClick={() => { setAmount(String(value)); requestId.current = null; }}>+{value.toLocaleString("vi-VN")} XP</button>)}</div>
      <label className="admin-field">Số XP<input className="input" type="number" min={1} max={100000} step={1} required value={amount} disabled={busy}
        onChange={event => { setAmount(event.target.value); requestId.current = null; }} /></label>
      <label className="admin-field">Lý do<input className="input" required maxLength={200} value={reason} disabled={busy}
        onChange={event => { setReason(event.target.value); requestId.current = null; }} /></label>
      <button className="button justify-self-start" disabled={busy || !data}>{busy ? "Đang cấp…" : "Cấp XP"}</button>
      {message && <p role="status" className="text-sm sm:col-span-2">{message}</p>}
    </form>
    {!!data?.grants.length && <details className="mt-4 text-sm"><summary className="cursor-pointer">20 lần cấp XP gần nhất</summary>
      {data.grants.map(grant => <div key={grant.id} className="mt-2 border-t border-white/10 py-2">
        <p>+{grant.amount.toLocaleString("vi-VN")} XP · Cấp {grant.levelBefore} → {grant.levelAfter} · {new Date(grant.createdAtUtc).toLocaleString("vi-VN")}</p>
        <p className="muted">{grant.reason} · Người cấp: {grant.adminUserId}</p>
      </div>)}
    </details>}
  </section>;
}
