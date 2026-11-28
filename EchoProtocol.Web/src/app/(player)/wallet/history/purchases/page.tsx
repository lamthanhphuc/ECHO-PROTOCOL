import Link from "next/link";
import { redirect } from "next/navigation";
import { EmptyState } from "@/components/ui/empty-state";
import { Pagination } from "@/components/ui/pagination";
import { PageHeader } from "@/components/ui/panel";
import { StatusBadge } from "@/components/ui/status-badge";
import { playerApi } from "@/lib/api/server-api";
import type { SearchParams } from "@/lib/types/common";
import { formatAdminDate, formatNumber, param } from "@/lib/utils/format";

export const metadata = { title: "Lịch sử mua vật phẩm" };
const pageSize = 20;

export default async function PurchaseHistoryPage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  const raw = await searchParams;
  const requestedPage = Number(param(raw.page));
  const page = Number.isSafeInteger(requestedPage) && requestedPage > 0 ? requestedPage : 1;
  const history = await playerApi.purchaseHistory(page, pageSize);
  if (history.totalPages === 0 && page > 1) redirect("/wallet/history/purchases");
  if (history.totalPages > 0 && page > history.totalPages)
    redirect(history.totalPages === 1 ? "/wallet/history/purchases" : `/wallet/history/purchases?page=${history.totalPages}`);

  return <>
    <PageHeader eyebrow="Ví của tôi" title="Lịch sử mua vật phẩm"
      description="Các vật phẩm ảo đã mua bằng xu trong trò chơi, tách riêng khỏi đơn nạp và thanh toán." />
    {history.items.length === 0 ? (
      <EmptyState title="Chưa có giao dịch mua vật phẩm">
        <p>Các nhân vật, thú cưng và vật phẩm đã mua sẽ xuất hiện tại đây.</p>
        <Link className="button button-secondary mt-4 inline-flex" href="/wallet/history">Lịch sử thanh toán</Link>
      </EmptyState>
    ) : (
      <div className="table-wrap rounded-lg">
        <table>
          <caption className="sr-only">Lịch sử mua vật phẩm của tài khoản</caption>
          <thead><tr><th scope="col">Vật phẩm</th><th scope="col">Giá</th><th scope="col">Trạng thái</th><th scope="col">Ngày mua</th></tr></thead>
          <tbody>{history.items.map((item) => <tr key={item.purchaseId}>
            <td><p className="font-semibold text-white">{item.itemName}</p><p className="muted mt-1 text-xs">{item.category}</p></td>
            <td>{formatNumber(item.pricePaid)} xu</td><td><StatusBadge status={item.status} /></td><td>{formatAdminDate(item.purchasedAtUtc)}</td>
          </tr>)}</tbody>
        </table>
      </div>
    )}
    <Pagination page={history.page} totalPages={history.totalPages} pathname="/wallet/history/purchases" />
  </>;
}
