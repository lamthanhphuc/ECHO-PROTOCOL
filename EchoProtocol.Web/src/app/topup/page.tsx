import Link from "next/link";
import { backendRequest } from "@/lib/api/backend-client";
import type { PaymentCatalog } from "@/lib/types/payment";
import { formatMoney, formatNumber } from "@/lib/utils/format";
import { PageHeader } from "@/components/ui/panel";

export const metadata = { title: "Các gói nạp Credits" };
export const dynamic = "force-dynamic";

export default async function TopUpPage() {
  const catalog = await backendRequest<PaymentCatalog>("/api/payments/catalog");

  return <main className="mx-auto max-w-6xl px-6 py-16">
    <PageHeader title="Các gói nạp Credits" description="Chọn gói phù hợp. Bạn sẽ đăng nhập khi tiếp tục thanh toán." />
    <p className="muted mt-4 text-sm">Credits nạp và Credits kiếm từ trận dùng chung một số dư. Gói nạp không cấp XP hoặc bỏ qua cấp mở khóa vật phẩm.</p>
    {catalog.items.length === 0 ? <p className="muted">Hiện chưa có gói nạp khả dụng.</p> :
      <div className="mt-8 grid gap-5 sm:grid-cols-2 xl:grid-cols-4">
        {catalog.items.map((item) => <article key={item.productReference} className="panel flex flex-col rounded-lg p-6">
          <h2 className="text-xl font-semibold text-white">{item.displayName}</h2>
          <p className="page-title mt-5 text-3xl font-bold text-amber-200">{formatNumber(item.walletCredit)} <span className="text-sm">Credits</span></p>
          <p className="muted mt-3 text-sm">{formatMoney(item.amount, item.currency)}</p>
          <Link className="button mt-8 text-center" href={`/wallet?product=${encodeURIComponent(item.productReference)}`}>Chọn gói</Link>
        </article>)}
      </div>}
  </main>;
}
