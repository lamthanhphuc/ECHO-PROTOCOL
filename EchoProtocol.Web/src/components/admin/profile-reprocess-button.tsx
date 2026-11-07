"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";

export function ProfileReprocessButton({ matchId }: { matchId: string }) {
  const router = useRouter();
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState("");
  async function reprocess() {
    setBusy(true);
    setMessage("");
    try {
      const response = await fetch(`/api/bff/admin/matches/${encodeURIComponent(matchId)}/reprocess-profiles`, {
        method: "POST", headers: { "Content-Type": "application/json" }, body: "{}",
      });
      const payload = await response.json();
      if (!response.ok || !payload.success) throw new Error(payload.message || "Không đưa được trận vào hàng đợi.");
      setMessage("Đã đưa trận vào hàng đợi.");
      router.refresh();
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "Không kết nối được máy chủ.");
    } finally {
      setBusy(false);
    }
  }
  return <div className="mt-2">
    <button className="button button-secondary" type="button" onClick={reprocess} disabled={busy}>Xử lý lại hồ sơ trận</button>
    {message && <p className="mt-2 text-sm" role="status">{message}</p>}
  </div>;
}
