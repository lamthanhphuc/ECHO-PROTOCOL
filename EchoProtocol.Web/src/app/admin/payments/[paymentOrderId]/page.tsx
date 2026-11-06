import { PageHeader, Panel, StatCard } from "@/components/ui/panel";
import { StatusBadge, statusLabel } from "@/components/ui/status-badge";
import { adminApi } from "@/lib/api/server-api";
import { formatDate, formatMoney, formatNumber } from "@/lib/utils/format";
import { PaymentRecoveryButton } from "@/components/admin/payment-recovery-button";

export default async function PaymentDetailPage({ params }: { params: Promise<{ paymentOrderId: string }> }) {
  const { paymentOrderId } = await params;
  const detail = await adminApi.payment(paymentOrderId);
  const order = detail.order;
  return <>
    <PageHeader title="Chi tiết đơn thanh toán" description={order.paymentOrderId} />
    <div className="grid gap-4 sm:grid-cols-3">
      <StatCard label="Số tiền" value={formatMoney(order.amount, order.currency)} />
      <StatCard label="Trạng thái" value={<StatusBadge status={order.status} />} />
      <StatCard label="Đơn vị thanh toán" value={order.provider} />
    </div>
    <div className="mt-5 grid gap-5 xl:grid-cols-2">
      <Panel><h2 className="mb-4 text-lg text-white">Thông tin đơn hàng</h2><dl className="grid gap-3 text-sm sm:grid-cols-2">
        <Field label="Người dùng" value={`${order.displayName ?? order.username} (${order.email})`} />
        <Field label="Mã sản phẩm" value={order.productReference} code />
        <Field label="Mục đích" value={statusLabel(order.purpose)} />
        <Field label="Mã đơn tại cổng thanh toán" value={order.providerOrderId} code />
        <Field label="Mã giao dịch tại cổng thanh toán" value={order.providerTransactionId} code />
        <Field label="Ngày tạo" value={formatDate(order.createdAtUtc)} />
        <Field label="Ngày cập nhật" value={formatDate(order.updatedAtUtc)} />
        <Field label="Hết hạn" value={formatDate(order.expiresAtUtc)} />
        <Field label="Đã thanh toán lúc" value={formatDate(order.paidAtUtc)} />
        <Field label="Đã cấp xu lúc" value={formatDate(order.fulfilledAtUtc)} />
      </dl></Panel>
      <Panel><h2 className="mb-4 text-lg text-white">Thanh toán và cấp xu</h2>
        {detail.checkout ? <dl className="grid gap-3 text-sm sm:grid-cols-2"><Field label="Trạng thái thanh toán" value={statusLabel(detail.checkout.status)} /><Field label="Lần tạo liên kết" value={String(detail.checkout.checkoutSequenceId)} /><Field label="Mã liên kết thanh toán" value={detail.checkout.providerPaymentLinkId} code /><Field label="Liên kết sẵn sàng lúc" value={formatDate(detail.checkout.readyAtUtc)} /></dl> : <p className="muted text-sm">Chưa tạo liên kết thanh toán.</p>}
        <hr className="my-5 border-[#1d3942]" />
        {detail.fulfillment ? <dl className="grid gap-3 text-sm sm:grid-cols-2"><Field label="Loại cấp phát" value={statusLabel(detail.fulfillment.kind)} /><Field label="Mã tham chiếu" value={detail.fulfillment.fulfillmentReference} code /><Field label="Mã giao dịch số dư" value={detail.fulfillment.walletTransactionId} code /><Field label="Hoàn tất lúc" value={formatDate(detail.fulfillment.completedAtUtc)} /></dl> : <p className="muted text-sm">Chưa cấp xu hoặc vật phẩm.</p>}
        {order.status === "PAID" && !detail.fulfillment && <PaymentRecoveryButton orderId={order.paymentOrderId} />}
      </Panel>
    </div>
    <Panel className="mt-5"><h2 className="mb-4 text-lg text-white">Lịch sử xác nhận</h2>
      {detail.providerEvents.length === 0 ? <p className="muted text-sm">Chưa nhận sự kiện thanh toán.</p> : <ol className="space-y-4 pl-5">
        {detail.providerEvents.map((event) => <li key={event.paymentProviderEventId} className="relative"><div className="flex flex-wrap items-center gap-3"><StatusBadge status={event.normalizedStatus} /><span className="text-sm">{statusLabel(event.processingOutcome)}</span><span className="muted text-xs">{statusLabel(event.verificationStatus)}</span></div><p className="muted mt-1 text-xs">{formatDate(event.receivedAtUtc)} · {formatMoney(event.amount, event.currency ?? order.currency)}</p></li>)}
      </ol>}
      {detail.walletTransaction && <div className="mt-6 rounded border border-emerald-900 bg-emerald-950/10 p-3 text-sm"><p className="text-sm font-semibold">Thay đổi số dư</p><p className="mt-2">{formatNumber(detail.walletTransaction.balanceBefore)} → {formatNumber(detail.walletTransaction.balanceAfter)} ({detail.walletTransaction.amount >= 0 ? "+" : ""}{formatNumber(detail.walletTransaction.amount)})</p></div>}
    </Panel>
  </>;
}

function Field({ label, value, code = false }: { label: string; value: string | null | undefined; code?: boolean }) {
  return <div><dt className="eyebrow">{label}</dt><dd className={`${code ? "code break-all" : ""} mt-1`}>{value || "—"}</dd></div>;
}
