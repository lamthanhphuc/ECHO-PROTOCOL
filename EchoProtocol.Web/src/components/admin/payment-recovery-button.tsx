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
      setMessage("Đã xác nhận cấp thưởng. Gửi lại yêu cầu sẽ không cấp thêm lần nữa.");
      router.refresh();
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "Không kết nối được máy chủ.");
    } finally { setBusy(false); }
  }
  return <div className="mt-4">
    <p className="muted mb-3 text-sm">Đã nhận thanh toán, đang chờ cấp thưởng. Hệ thống tự thử lại mỗi phút; bạn cũng có thể yêu cầu thử ngay.</p>
    <button type="button" className="button" disabled={busy} onClick={recover}>{busy ? "Đang phục hồi…" : "Thử cấp thưởng lại"}</button>
    {message && <p role="status" className="mt-3 text-sm">{message}</p>}
  </div>;
}
