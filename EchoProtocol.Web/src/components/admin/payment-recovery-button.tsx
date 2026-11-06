"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";

export function PaymentRecoveryButton({ orderId }: { orderId: string }) {
  const router = useRouter();
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState("");
  async function recover() {
    setBusy(true);
    setMessage("");
    try {
      const response = await fetch(`/api/bff/admin/payments/${encodeURIComponent(orderId)}/retry-fulfillment`, {
        method: "POST", headers: { "Content-Type": "application/json" }, body: "{}",
      });
      const payload = await response.json();
      if (!response.ok || !payload.success) throw new Error(payload.message || "Chưa phục hồi được đơn thanh toán.");
      setMessage("Đã yêu cầu cấp lại xu hoặc vật phẩm. Hệ thống không cấp trùng.");
      router.refresh();
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "Không kết nối được máy chủ.");
    } finally { setBusy(false); }
  }
  return <div className="mt-4">
    <p className="muted mb-3 text-sm">Đã nhận thanh toán nhưng chưa cấp xu hoặc vật phẩm. Hệ thống đang tự thử lại.</p>
    <button type="button" className="button" disabled={busy} onClick={recover}>{busy ? "Đang xử lý…" : "Yêu cầu cấp lại"}</button>
    {message && <p role="status" className="mt-3 text-sm">{message}</p>}
  </div>;
}
