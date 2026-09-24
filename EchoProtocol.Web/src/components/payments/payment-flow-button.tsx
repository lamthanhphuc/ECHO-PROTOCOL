"use client";

import { useRef, useState } from "react";
import type { ApiResponse } from "@/lib/types/common";
import type { PaymentCheckout, PaymentOrder } from "@/lib/types/payment";

export function PaymentFlowButton({ productReference, provider = "PAYOS" }: {
  productReference: string; provider?: "PAYOS";
}) {
  const idempotencyKey = useRef<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  async function start() {
    setLoading(true); setError("");
    idempotencyKey.current ??= crypto.randomUUID();
    try {
      const orderResponse = await fetch("/api/bff/payments/orders", {
        method: "POST", headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ productReference, provider, idempotencyKey: idempotencyKey.current }),
      });
      const orderPayload = await orderResponse.json() as ApiResponse<PaymentOrder>;
      if (!orderResponse.ok || !orderPayload.data) throw new Error(orderPayload.message);
      const orderId = orderPayload.data.paymentOrderId;
      const checkoutResponse = await fetch(`/api/bff/payments/orders/${orderId}/checkout`, { method: "POST" });
      const checkoutPayload = await checkoutResponse.json() as ApiResponse<PaymentCheckout>;
      if (!checkoutResponse.ok || !checkoutPayload.data) throw new Error(checkoutPayload.message);
      const checkout = new URL(checkoutPayload.data.checkoutUrl);
      if (checkout.protocol !== "https:") throw new Error("Backend trả checkout URL không an toàn.");
      sessionStorage.setItem("echo.pendingPaymentOrderId", orderId);
      window.location.assign(checkout.toString());
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "Không thể tạo thanh toán.");
      setLoading(false);
    }
  }
  return <div>
    <button type="button" className="button" onClick={start} disabled={loading}>{loading ? "Đang tạo checkout…" : "Nạp tiền"}</button>
    {error && <p className="mt-2 text-sm text-rose-300" role="alert">{error}</p>}
  </div>;
}
