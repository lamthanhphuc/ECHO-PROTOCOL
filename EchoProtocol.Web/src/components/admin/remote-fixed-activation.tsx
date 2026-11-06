"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";

export function RemoteFixedActivation({ version, activeVersion, isActive }: {
  version: string; activeVersion: string | null; isActive: boolean;
}) {
  const router = useRouter();
  const [reason, setReason] = useState("");
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState("");

  async function change(action: "activate" | "deactivate") {
    const trimmed = reason.trim();
    if (!trimmed) return setMessage("Vui lòng nhập lý do.");
    if (!window.confirm(`${action === "activate" ? "Kích hoạt" : "Ngừng kích hoạt"} ${version}?`)) return;
    setBusy(true); setMessage("");
    try {
      const response = await fetch(`/api/bff/admin/remote-fixed/versions/${encodeURIComponent(version)}/${action}`, {
        method: "POST", headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ expectedActiveVersion: activeVersion ?? "", reason: trimmed }),
      });
      const payload = await response.json();
      if (response.status === 409) throw new Error("Cấu hình đang áp dụng đã thay đổi. Hãy tải lại trang trước khi thử lại.");
      if (!response.ok || !payload.success) throw new Error(payload.message || "Không đổi được trạng thái.");
      setReason(""); setMessage("Đã cập nhật trạng thái."); router.refresh();
    } catch (error) { setMessage(error instanceof Error ? error.message : "Không kết nối được máy chủ."); }
    finally { setBusy(false); }
  }

  return <div>
    <label className="admin-field">Lý do thay đổi<textarea className="input min-h-24" required maxLength={500} value={reason} onChange={(e) => setReason(e.target.value)} /></label>
    <div className="mt-3 flex gap-3">
      {isActive
        ? <button className="button" type="button" disabled={busy} onClick={() => change("deactivate")}>Ngừng kích hoạt</button>
        : <button className="button" type="button" disabled={busy} onClick={() => change("activate")}>{activeVersion ? "Kích hoạt lại cấu hình này" : "Kích hoạt cấu hình"}</button>}
    </div>
    {message && <p role="status" className="mt-3 text-sm">{message}</p>}
  </div>;
}
