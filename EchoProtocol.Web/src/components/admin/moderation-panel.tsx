"use client";

import { useRouter } from "next/navigation";
import { useState, type FormEvent } from "react";
import type { AdminModerationAction } from "@/lib/types/admin";
import { formatAdminDate as formatDate } from "@/lib/utils/format";

const labels: Record<AdminModerationAction["action"], string> = {
  FLAG_MATCH: "Đánh dấu cần kiểm tra",
  CLEAR_MATCH: "Bỏ đánh dấu",
  STOP_MATCH: "Dừng phòng trên máy chủ",
  FLAG_USER: "Đánh dấu tài khoản cần kiểm tra",
  CLEAR_USER: "Bỏ đánh dấu tài khoản",
  LOCK_USER: "Khóa tài khoản",
  UNLOCK_USER: "Mở khóa tài khoản",
};

export function ModerationPanel({ targetId, actions, history }: {
  targetId: string;
  actions: AdminModerationAction["action"][];
  history: AdminModerationAction[];
}) {
  const router = useRouter();
  const [action, setAction] = useState<AdminModerationAction["action"]>(actions[0]);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState("");

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if ((action === "LOCK_USER" || action === "STOP_MATCH") && !window.confirm(`${labels[action]}?`)) return;
    const form = event.currentTarget;
    const reason = String(new FormData(form).get("reason") || "").trim();
    setBusy(true);
    setMessage("");
    try {
      const response = await fetch(`/api/bff/admin/moderation/${encodeURIComponent(targetId)}`, {
        method: "POST", headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ action, reason }),
      });
      const payload = await response.json();
      if (!response.ok || !payload.success) throw new Error(payload.message || "Không thực hiện được thao tác.");
      form.reset();
      setMessage("Đã cập nhật.");
      router.refresh();
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "Không kết nối được máy chủ.");
    } finally { setBusy(false); }
  }

  return <section className="panel mt-5 rounded-lg p-5">
    <h2 className="text-lg font-semibold text-white">Kiểm tra và can thiệp</h2>
    {actions.length > 0 && <form onSubmit={submit} className="mt-4 grid gap-4 md:grid-cols-[minmax(0,1fr)_minmax(0,2fr)_auto] md:items-end">
      <label className="admin-field">Thao tác<select className="input" value={action} onChange={(event) => setAction(event.target.value as AdminModerationAction["action"])}>{actions.map((item) => <option key={item} value={item}>{labels[item]}</option>)}</select></label>
      <label className="admin-field">Lý do<input className="input" name="reason" required minLength={10} maxLength={500} placeholder="Ghi rõ căn cứ để kiểm tra lại" /></label>
      <button className="button" type="submit" disabled={busy}>{busy ? "Đang lưu…" : "Xác nhận"}</button>
    </form>}
    {action === "STOP_MATCH" && <p className="muted mt-3 text-sm">Thao tác này chặn quyền phòng trên máy chủ; phiên Fusion có thể tiếp tục cho đến khi client ngắt kết nối.</p>}
    {message && <p role="status" className="mt-3 text-sm text-red-300">{message}</p>}
    {history.length > 0 && <div className="table-wrap mt-5 rounded-lg"><table><thead><tr><th>Thời gian</th><th>Thao tác</th><th>Lý do</th><th>Quản trị viên</th></tr></thead><tbody>{history.map((entry) => <tr key={entry.id}><td>{formatDate(entry.createdAtUtc)}</td><td>{labels[entry.action]}</td><td>{entry.reason}</td><td className="code text-xs">{entry.adminUserId}</td></tr>)}</tbody></table></div>}
  </section>;
}
