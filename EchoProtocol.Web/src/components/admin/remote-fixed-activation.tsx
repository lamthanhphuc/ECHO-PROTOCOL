"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";

export function RemoteFixedActivation({ version, activeVersion, isActive, activationBlocked, scope, useDefault = false }: {
  version: string; activeVersion: string | null; isActive: boolean;
  activationBlocked: string | null; scope: string;
  useDefault?: boolean;
}) {
  const router = useRouter();
  const [reason, setReason] = useState("");
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState("");
  const replacing = activeVersion !== null && activeVersion !== version;

  async function change(action: "activate" | "deactivate") {
    if (action === "activate" && activationBlocked) return;
    const trimmed = reason.trim();
    if (!trimmed) return setMessage("Vui lòng nhập lý do.");
    if (!window.confirm(action === "deactivate"
      ? `Dùng mặc định trong game thay cho ${version} trên ${scope}?`
      : replacing ? `Dùng ${version} thay cho ${activeVersion} trên ${scope}?` : `Kích hoạt ${version} cho ${scope}?`)) return;
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
    <p className="muted mb-3 text-sm">{scope}{!isActive && replacing ? ` · Sẽ thay thế ${activeVersion}` : ""}</p>
    {!isActive && activationBlocked && <p className="mb-3 text-sm text-rose-300">{activationBlocked}</p>}
    <label className="admin-field">Lý do thay đổi<textarea className="input min-h-24" required maxLength={500} value={reason} onChange={(e) => setReason(e.target.value)} /></label>
    <div className="mt-3 flex gap-3">
      {isActive
        ? <button className="button" type="button" disabled={busy} onClick={() => change("deactivate")}>{useDefault ? "Dùng mặc định trong game" : "Ngừng kích hoạt"}</button>
        : <button className="button" type="button" disabled={busy || !!activationBlocked} onClick={() => change("activate")}>{replacing ? "Thay cấu hình đang áp dụng" : "Kích hoạt cấu hình"}</button>}
    </div>
    {message && <p role="status" className="mt-3 text-sm">{message}</p>}
  </div>;
}
