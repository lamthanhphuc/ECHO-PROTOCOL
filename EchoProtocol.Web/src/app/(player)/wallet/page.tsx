import Link from "next/link";
import { PaymentFlowButton } from "@/components/payments/payment-flow-button";
import { playerApi } from "@/lib/api/server-api";
import { formatMoney, formatNumber } from "@/lib/utils/format";
import { PageHeader, Panel } from "@/components/ui/panel";

export const metadata = { title: "Wallet" };

export default async function WalletPage() {
  const [profile, catalog] = await Promise.all([
    playerApi.profile(),
    playerApi.paymentCatalog(),
  ]);
  return <>
    <PageHeader eyebrow="Secure wallet" title="Wallet" description="Số dư chỉ được cập nhật sau verified webhook và fulfillment thành công." />
    <Panel className="overflow-hidden">
      <p className="eyebrow">Current balance</p>
      <p className="page-title mt-3 text-4xl font-bold text-white">{formatNumber(profile.walletBalance)} <span className="text-lg text-amber-200">Coins</span></p>
    </Panel>
    <Panel className="mt-5">
      <h2 className="text-xl font-semibold text-white">Top up Wallet</h2>
      <p className="muted mt-2 text-sm">Giá và số Coins được lấy trực tiếp từ Payment Catalog của Backend.</p>
      <div className="mt-5 grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        {catalog.items.map((item) => <article key={item.productReference} className="rounded-sm border border-[#593432] bg-[#130e0f] p-5">
          <p className="eyebrow">{item.provider}</p>
          <h3 className="mt-3 text-xl font-semibold text-white">{item.displayName}</h3>
          <p className="page-title mt-3 text-3xl font-bold text-amber-200">{formatNumber(item.walletCredit)} <span className="text-sm">Coins</span></p>
          <p className="muted mb-5 mt-2 text-sm">{formatMoney(item.amount, item.currency)}</p>
          <PaymentFlowButton productReference={item.productReference} provider={item.provider} />
        </article>)}
      </div>
      <Link className="button button-secondary mt-4" href="/wallet/history">Xem lịch sử giao dịch</Link>
    </Panel>
  </>;
}
