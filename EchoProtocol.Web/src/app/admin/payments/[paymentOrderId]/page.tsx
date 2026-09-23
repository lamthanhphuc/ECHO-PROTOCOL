import { PageHeader, Panel, StatCard } from "@/components/ui/panel";
import { StatusBadge } from "@/components/ui/status-badge";
import { adminApi } from "@/lib/api/server-api";
import { formatDate, formatMoney, formatNumber } from "@/lib/utils/format";

export default async function PaymentDetailPage({ params }: { params: Promise<{ paymentOrderId: string }> }) {
  const { paymentOrderId } = await params;
  const detail = await adminApi.payment(paymentOrderId);
  const order = detail.order;
  return <>
    <PageHeader eyebrow="Payment trace" title="Chi tiết thanh toán" description={order.paymentOrderId} />
    <div className="grid gap-4 sm:grid-cols-3">
      <StatCard label="Amount" value={formatMoney(order.amount, order.currency)} />
      <StatCard label="Status" value={<StatusBadge status={order.status} />} />
      <StatCard label="Provider" value={order.provider} />
    </div>
    <div className="mt-5 grid gap-5 xl:grid-cols-2">
      <Panel><h2 className="mb-4 text-lg text-white">PaymentOrder</h2><dl className="grid gap-3 text-sm sm:grid-cols-2">
        <Field label="User" value={`${order.displayName ?? order.username} (${order.email})`} />
        <Field label="Product" value={order.productReference} code />
        <Field label="Purpose" value={order.purpose} />
        <Field label="Provider order" value={order.providerOrderId} code />
        <Field label="Provider transaction" value={order.providerTransactionId} code />
        <Field label="Created" value={formatDate(order.createdAtUtc)} />
        <Field label="Updated" value={formatDate(order.updatedAtUtc)} />
        <Field label="Expires" value={formatDate(order.expiresAtUtc)} />
        <Field label="Paid" value={formatDate(order.paidAtUtc)} />
        <Field label="Fulfilled" value={formatDate(order.fulfilledAtUtc)} />
      </dl></Panel>
      <Panel><h2 className="mb-4 text-lg text-white">Checkout & fulfillment</h2>
        {detail.checkout ? <dl className="grid gap-3 text-sm sm:grid-cols-2"><Field label="Checkout status" value={detail.checkout.status} /><Field label="Sequence" value={String(detail.checkout.checkoutSequenceId)} /><Field label="Payment link ID" value={detail.checkout.providerPaymentLinkId} code /><Field label="Ready at" value={formatDate(detail.checkout.readyAtUtc)} /></dl> : <p className="muted text-sm">Chưa có checkout.</p>}
        <hr className="my-5 border-[#1d3942]" />
        {detail.fulfillment ? <dl className="grid gap-3 text-sm sm:grid-cols-2"><Field label="Kind" value={detail.fulfillment.kind} /><Field label="Reference" value={detail.fulfillment.fulfillmentReference} code /><Field label="Wallet transaction" value={detail.fulfillment.walletTransactionId} code /><Field label="Completed" value={formatDate(detail.fulfillment.completedAtUtc)} /></dl> : <p className="muted text-sm">Chưa có fulfillment.</p>}
      </Panel>
    </div>
    <Panel className="mt-5"><h2 className="mb-4 text-lg text-white">Provider event timeline</h2>
      {detail.providerEvents.length === 0 ? <p className="muted text-sm">Chưa nhận provider event.</p> : <ol className="space-y-4 border-l border-[#28735f] pl-5">
        {detail.providerEvents.map((event) => <li key={event.paymentProviderEventId} className="relative"><span className="absolute -left-[1.45rem] top-1 h-2 w-2 rounded-full bg-emerald-300" /><div className="flex flex-wrap items-center gap-3"><StatusBadge status={event.normalizedStatus} /><span className="text-sm">{event.processingOutcome}</span><span className="muted text-xs">{event.verificationStatus}</span></div><p className="muted mt-1 text-xs">{formatDate(event.receivedAtUtc)} · {formatMoney(event.amount, event.currency ?? order.currency)}</p></li>)}
      </ol>}
      {detail.walletTransaction && <div className="mt-6 rounded border border-emerald-900 bg-emerald-950/10 p-3 text-sm"><p className="eyebrow">Wallet mutation</p><p className="mt-2">{formatNumber(detail.walletTransaction.balanceBefore)} → {formatNumber(detail.walletTransaction.balanceAfter)} ({detail.walletTransaction.amount >= 0 ? "+" : ""}{formatNumber(detail.walletTransaction.amount)})</p></div>}
    </Panel>
  </>;
}

function Field({ label, value, code = false }: { label: string; value: string | null | undefined; code?: boolean }) {
  return <div><dt className="eyebrow">{label}</dt><dd className={`${code ? "code break-all" : ""} mt-1`}>{value || "—"}</dd></div>;
}
