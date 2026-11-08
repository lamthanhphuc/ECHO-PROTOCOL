const labels: Record<string, string> = {
  ACTIVE: "Đang hoạt động", INACTIVE: "Chưa kích hoạt", LOCKED: "Đã khóa",
  CREATED: "Đã tạo", PENDING_PAYMENT: "Chờ thanh toán", PAID: "Đã thanh toán",
  FULFILLED: "Đã hoàn tất", COMPLETED: "Hoàn tất", FAILED: "Thất bại",
  CANCELLED: "Đã hủy", EXPIRED: "Hết hạn", READY: "Sẵn sàng",
  RESERVED: "Đã giữ chỗ", UNKNOWN: "Chưa xác định", ADMIN: "Quản trị viên",
  WALLET_CREDIT: "Cộng xu", INVENTORY_ITEM: "Cấp vật phẩm", WALLET_TOPUP: "Nạp xu",
  CHARACTER: "Nhân vật gốc", TEAM_TOOL: "Công cụ đội", PET: "Thú cưng",
  PET_SKIN: "Trang phục thú cưng", CHARACTER_OUTFIT: "Trang phục nhân vật",
  RECEIVED: "Đã nhận", VERIFIED: "Đã xác minh", FULFILLMENT_PENDING: "Chờ cấp phát",
  UNKNOWN_ORDER: "Không tìm thấy đơn", EVIDENCE_MISMATCH: "Thông tin không khớp",
  LATE_TERMINAL_IGNORED: "Sự kiện đến muộn", STATUS_IGNORED: "Không đổi trạng thái",
  TERMINAL_APPLIED: "Đã cập nhật trạng thái",
  PLAYER: "Người chơi", MATCH_REWARD: "Thưởng trận đấu", PURCHASE: "Mua vật phẩm",
  PAYMENT_FULFILLMENT: "Nạp tiền", ADMIN_GRANT: "Cộng xu quản trị",
  Lobby: "Phòng chờ", InMatch: "Đang diễn ra", Ended: "Đã kết thúc", Stopped: "Đã dừng",
};

export function statusLabel(status: string): string { return labels[status] ?? status; }

export function StatusBadge({ status }: { status: string }) {
  const tone = status === "FULFILLED" || status === "ACTIVE" || status === "COMPLETED" || status === "Ended"
    ? "border-emerald-700/70 bg-emerald-900/25 text-emerald-300"
    : status === "FAILED" || status === "CANCELLED" || status === "LOCKED"
      ? "border-rose-800/70 bg-rose-950/35 text-rose-300"
      : "border-amber-700/60 bg-amber-950/25 text-amber-200";
  return <span className={`inline-flex rounded border px-2 py-1 text-sm font-semibold ${tone}`}>{statusLabel(status)}</span>;
}
