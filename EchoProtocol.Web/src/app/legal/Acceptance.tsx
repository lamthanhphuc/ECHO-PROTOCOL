"use client";
import { useState } from "react";

export default function Acceptance({ version, hash }: { version: string; hash: string }) {
  const [agreement, setAgreement] = useState(false);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState("");
  async function accept() {
    if (!agreement || busy) return;
    setBusy(true); setMessage("");
    try {
      const response = await fetch("/api/bff/agreements", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({
        bundleVersion: version, contentSha256: hash, locale: "vi", adultConfirmed: true,
        warningAcknowledged: true, termsAccepted: true, privacyNoticeAcknowledged: true, communityAccepted: true, transactionsAccepted: true,
      }) });
      const body = await response.json();
      if (!response.ok || !body.success || !body.data?.accepted) throw new Error(body.message || "Chưa lưu được xác nhận. Hãy tải lại tài liệu và thử lại.");
      setMessage("Đã lưu xác nhận cho tài khoản. Bạn có thể tiếp tục trong game hoặc quay lại trang giao dịch.");
    } catch (error) { setMessage(error instanceof Error ? error.message : "Không thể kết nối. Thử lại."); }
    finally { setBusy(false); }
  }
  return <section aria-label="Xác nhận chính sách" style={{ marginTop: 32, padding: 20, border: "1px solid #78353b" }}>
    <h2>Xác nhận</h2>
    <p>Trò chơi có nội dung kinh dị, âm thanh lớn và hiệu ứng ánh sáng nhấp nháy.</p>

    <label style={{ display: "block", marginBottom: 16 }}><input type="checkbox" checked={agreement} onChange={e => setAgreement(e.target.checked)} /> Tôi xác nhận đủ 18 tuổi, đã xem cảnh báo, đồng ý Điều khoản, Quy tắc cộng đồng và Chính sách giao dịch, xác nhận được cung cấp Chính sách quyền riêng tư.</label>
    <button onClick={accept} disabled={!agreement || busy} style={{ border: "1px solid #a34b53", padding: 12, background: agreement && !busy ? "#6e161f" : "#33383b", color: "#eee9df", cursor: agreement && !busy ? "pointer" : "not-allowed" }}>{busy ? "Đang lưu…" : "Đồng ý và tiếp tục"}</button>
    <p role="status">{message}</p>
  </section>;
}
