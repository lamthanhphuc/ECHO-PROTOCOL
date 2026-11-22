import { Panel } from "@/components/ui/panel";
import type { EconomyData } from "@/lib/types/operations";

const flowNames: Record<string, string> = { MATCH_REWARD: "Thưởng trận", PAYMENT_FULFILLMENT: "Nạp tiền", ADMIN_GRANT: "Admin cấp", PURCHASE: "Mua Shop" };
const amount = (value: number | null) => value == null ? "Chưa có mẫu" : value.toLocaleString("vi-VN", { maximumFractionDigits: 2 });

export function EconomyWalletTelemetry({ data }: { data: EconomyData["walletTelemetry"] }) {
  if (!data) return null;
  const truncated = data.playersTruncated || data.ledgerTruncated || data.grantsTruncated || data.unlockEventsTruncated || data.experienceSources.adminTruncated;
  return <Panel>
    <h2 className="font-semibold">Ví, chi tiêu và mốc mở khóa</h2>
    <p className="muted mt-2 text-sm">{data.players} ví được lấy mẫu · Giao dịch trong {data.days} ngày · Ví là số dư hiện tại, không phải tổng tiền đã kiếm.</p>
    <p className="mt-3">Ví P50: {amount(data.walletP50)} Credits · P90: {amount(data.walletP90)} Credits</p>
    <p className="muted mt-2 text-sm">XP từ trận: {amount(data.experienceSources.gameplay)} · XP admin cấp: {amount(data.experienceSources.admin)} ({data.experienceSources.adminGrants} lượt). Mốc có admin cấp XP không dùng để ước tính thời gian cày tự nhiên.</p>
    {truncated && <p role="status" className="mt-2 text-sm text-amber-200">Mẫu đã chạm giới hạn. Không dùng số liệu này như tổng toàn hệ thống.</p>}
    {data.flows.length > 0 && <div className="mt-4 overflow-x-auto"><table className="w-full text-left text-sm [&_td]:p-2 [&_th]:p-2 [&_tbody_tr]:border-t [&_tbody_tr]:border-[var(--line)]"><thead><tr><th>Nguồn</th><th>Credits ròng</th><th>Giao dịch</th></tr></thead><tbody>
      {data.flows.map(row => <tr key={row.source}><td>{flowNames[row.source] ?? row.source}</td><td>{amount(row.credits)}</td><td>{row.transactions}</td></tr>)}
    </tbody></table></div>}
    {data.purchasesByCategory.length > 0 && <div className="mt-4 overflow-x-auto"><table className="w-full text-left text-sm [&_td]:p-2 [&_th]:p-2 [&_tbody_tr]:border-t [&_tbody_tr]:border-[var(--line)]"><thead><tr><th>Loại món đã mua</th><th>Credits đã tiêu</th><th>Lượt mua</th></tr></thead><tbody>
      {data.purchasesByCategory.map(row => <tr key={row.category}><td>{row.category}</td><td>{amount(row.creditsSpent)}</td><td>{row.purchases}</td></tr>)}
    </tbody></table></div>}
    {data.cohorts.length > 0 && <div className="mt-4 overflow-x-auto"><table className="w-full text-left text-sm [&_td]:p-2 [&_th]:p-2 [&_tbody_tr]:border-t [&_tbody_tr]:border-[var(--line)]"><thead><tr><th>Số trận trong mẫu</th><th>Người chơi</th><th>Ví P50 / P90</th><th>Credits từ trận</th><th>Lượt mua</th></tr></thead><tbody>
      {data.cohorts.map(row => <tr key={row.matchesInWindow}><td>{row.matchesInWindow}</td><td>{row.players}</td><td>{amount(row.walletP50)} / {amount(row.walletP90)}</td><td>{amount(row.gameplayCredits)}</td><td>{row.purchases}</td></tr>)}
    </tbody></table></div>}
    {data.unlocks.length > 0 && <>
      <h3 className="mt-5 font-medium">Ví tại thời điểm mở quyền mua</h3>
      <p className="muted mt-1 text-sm">Giá * là giá hiện tại dùng thay thế vì bản thưởng cũ chưa lưu giá. Mốc có nạp/admin cấp tiền được đánh dấu riêng.</p>
      <div className="mt-3 overflow-x-auto"><table className="w-full text-left text-sm [&_td]:p-2 [&_th]:p-2 [&_tbody_tr]:border-t [&_tbody_tr]:border-[var(--line)]"><thead><tr><th>Món / cấp</th><th>Giá</th><th>Ví khi mở</th><th>Ví / giá</th><th>Phút trận đến mốc</th><th>Chờ mở (giờ thực)</th><th>Nguồn ngoài trận</th></tr></thead><tbody>
        {data.unlocks.slice(0, 50).map(row => <tr key={`${row.userId}:${row.itemId}:${row.unlockedAtUtc}`}><td>{row.itemName} · {row.requiredLevel}</td><td>{amount(row.price)}{row.priceWasSnapshotted ? "" : "*"}</td><td>{amount(row.walletAtUnlock)}</td><td>{amount(row.affordabilityRatio)}×</td><td>{amount(row.minutesToUnlock)}</td><td>{amount(row.unlockGapWallMinutes == null ? null : row.unlockGapWallMinutes / 60)}</td><td>{row.adminExperienceBeforeUnlock ? "Admin XP" : row.externalFundsBeforeUnlock ? "Có nạp/admin" : "Không ghi nhận trong mẫu"}</td></tr>)}
      </tbody></table></div>
    </>}
    <p className="muted mt-3 text-sm">Phút trận gồm thời gian gục, không gồm lobby. Thời gian chờ mở dùng giờ thực, gồm cả offline/lobby. Chỉ tính tiến trình đầy đủ cho tài khoản mới trong cửa sổ mẫu; thiếu lịch sử thì không suy đoán.</p>
  </Panel>;
}
