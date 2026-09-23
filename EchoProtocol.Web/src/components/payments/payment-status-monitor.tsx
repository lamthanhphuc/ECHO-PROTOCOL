"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import type { ApiResponse } from "@/lib/types/common";
import type { PaymentOrder, PaymentStatus } from "@/lib/types/payment";
import type { PlayerProfile } from "@/lib/types/player";
import { StatusBadge } from "@/components/ui/status-badge";

const messages: Record<PaymentStatus, string> = {
  CREATED: "Đơn thanh toán đã được tạo.", PENDING_PAYMENT: "Đang xác nhận thanh toán.",
  PAID: "Thanh toán đã được xác nhận. Đang cập nhật Wallet.", FULFILLED: "Wallet đã được cập nhật thành công.",
  FAILED: "Thanh toán thất bại.", CANCELLED: "Thanh toán đã bị hủy.", EXPIRED: "Đơn thanh toán đã hết hạn.",
};

export function PaymentStatusMonitor() {
  const [order, setOrder] = useState<PaymentOrder | null>(null);
  const [balance, setBalance] = useState<number | null>(null);
  const [message, setMessage] = useState("Đang tìm payment order…");
  const [error, setError] = useState("");
  useEffect(() => {
    const orderId = sessionStorage.getItem("echo.pendingPaymentOrderId");
    if (!orderId) {
      const missingOrderTimer = setTimeout(
        () => setError("Không tìm thấy paymentOrderId trong phiên trình duyệt."),
        0,
      );
      return () => clearTimeout(missingOrderTimer);
    }
    let stopped = false; let attempts = 0; let timer: ReturnType<typeof setTimeout> | undefined;
    const poll = async () => {
      attempts += 1;
      try {
        const response = await fetch(`/api/bff/payments/orders/${orderId}`, { cache: "no-store" });
        const payload = await response.json() as ApiResponse<PaymentOrder>;
        if (response.status === 501) { setError(payload.message); return; }
        if (!response.ok || !payload.data) throw new Error(payload.message);
        if (stopped) return;
        setOrder(payload.data); setMessage(messages[payload.data.status]);
        if (payload.data.status === "FULFILLED") {
          const profileResponse = await fetch("/api/bff/player/profile", { cache: "no-store" });
          const profilePayload = await profileResponse.json() as ApiResponse<PlayerProfile>;
          if (profilePayload.data) setBalance(profilePayload.data.walletBalance);
          sessionStorage.removeItem("echo.pendingPaymentOrderId"); return;
        }
        if (["FAILED", "CANCELLED", "EXPIRED"].includes(payload.data.status)) return;
        if (attempts >= 40) { setMessage("Hết thời gian chờ xác nhận. Bạn có thể kiểm tra lại sau."); return; }
        timer = setTimeout(poll, 2500);
      } catch (reason) { setError(reason instanceof Error ? reason.message : "Không thể kiểm tra trạng thái."); }
    };
    void poll(); return () => { stopped = true; if (timer) clearTimeout(timer); };
  }, []);
  return <div className="panel rounded-lg p-6">
    {order && <StatusBadge status={order.status} />}
    <p className="mt-4 text-lg text-white">{message}</p>
    {balance !== null && <p className="mt-3 text-emerald-300">Wallet balance mới: {balance} Coins</p>}
    {error && <p role="alert" className="mt-4 rounded border border-amber-800 bg-amber-950/20 p-3 text-amber-200">{error}</p>}
    <div className="mt-6 flex flex-wrap gap-3"><Link className="button" href="/wallet">Quay lại Wallet</Link><Link className="button button-secondary" href="/wallet/history">Lịch sử giao dịch</Link></div>
  </div>;
}
