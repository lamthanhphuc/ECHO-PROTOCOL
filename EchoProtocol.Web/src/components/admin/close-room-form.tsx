"use client";

import { useRouter } from "next/navigation";
import { useRef, useState, type FormEvent } from "react";

export function CloseRoomForm({ roomInstanceId, roomName }: { roomInstanceId: string; roomName: string }) {
  const router = useRouter();
  const pending = useRef<{ id: string; reason: string } | null>(null);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState("");

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const reason = String(new FormData(event.currentTarget).get("reason") || "").trim();
    if (!window.confirm(`Đóng phòng ${roomName} (${roomInstanceId})? Người chơi có thể chưa rời Photon ngay.`)) return;
    if (!pending.current || pending.current.reason !== reason) pending.current = { id: crypto.randomUUID(), reason };
    setBusy(true);
    setMessage("");
    try {
      const response = await fetch(`/api/bff/admin/rooms/${encodeURIComponent(roomInstanceId)}/close`, {
        method: "POST", headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ actionId: pending.current.id, reason }),
      });
      const payload = await response.json();
      if (!response.ok || !payload.success) throw new Error(payload.message || "Không đóng được phòng.");
      pending.current = null;
      setMessage("Backend đã ghi nhận đóng phòng. Chưa có xác nhận Host/client đã rời Photon.");
      router.refresh();
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "Không kết nối được máy chủ. Thử lại để đối soát quyết định.");
    } finally {
      setBusy(false);
    }
  }

  return <section className="panel mt-5 rounded-lg p-5"><h2 className="text-lg font-semibold text-white">Đóng phòng</h2>
    <p className="muted mt-2 text-sm">Thao tác sẽ chặn vòng đời phòng trên backend. Trạng thái Photon cần xác nhận riêng.</p>
    <form onSubmit={submit} className="mt-4 grid gap-4 md:grid-cols-[minmax(0,1fr)_auto] md:items-end">
      <label className="admin-field">Lý do đóng<input className="input" name="reason" required minLength={10} maxLength={500} placeholder="Ghi rõ lý do để kiểm tra lại" /></label>
      <button className="button" type="submit" disabled={busy}>{busy ? "Đang gửi…" : "Đóng phòng"}</button>
    </form>
    {message && <p role="status" className="mt-3 text-sm text-amber-200">{message}</p>}
  </section>;
}
