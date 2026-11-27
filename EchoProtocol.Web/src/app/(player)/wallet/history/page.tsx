import Link from "next/link";
import { redirect } from "next/navigation";
import { EmptyState } from "@/components/ui/empty-state";
import { Pagination } from "@/components/ui/pagination";
import { PageHeader } from "@/components/ui/panel";
import { StatusBadge } from "@/components/ui/status-badge";
import { playerApi } from "@/lib/api/server-api";
import type { SearchParams } from "@/lib/types/common";
import { formatAdminDate, formatMoney, paymentProductLabel, param } from "@/lib/utils/format";

export const metadata = { title: "Lịch sử thanh toán" };

const pageSize = 20;

export default async function PaymentHistoryPage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  const raw = await searchParams;
  const requestedPage = Number(param(raw.page));
  const page = Number.isSafeInteger(requestedPage) && requestedPage > 0
    ? Math.min(requestedPage, Math.floor(2_147_483_647 / pageSize))
    : 1;
  const history = await playerApi.paymentHistory(page, pageSize);

  if (history.totalPages === 0 && page > 1) redirect("/wallet/history");
  if (history.totalPages > 0 && page > history.totalPages)
    redirect(history.totalPages === 1 ? "/wallet/history" : `/wallet/history?page=${history.totalPages}`);

  return <>
    <PageHeader
      eyebrow="Ví của tôi"
      title="Lịch sử thanh toán"
      description="Các đơn nạp xu và thanh toán của tài khoản này, cùng trạng thái đã được máy chủ xác nhận."
    />
    {history.items.length === 0 ? (
      <EmptyState title="Chưa có giao dịch thanh toán">
        <p>Đơn thanh toán và nạp xu sẽ xuất hiện tại đây.</p>
        <Link className="button button-secondary mt-4 inline-flex" href="/wallet">Xem các gói nạp xu</Link>
      </EmptyState>
    ) : (
      <div className="table-wrap rounded-lg">
        <table>
          <caption className="sr-only">Lịch sử thanh toán của tài khoản</caption>
          <thead>
            <tr>
              <th scope="col">Đơn hàng</th>
              <th scope="col">Số tiền</th>
              <th scope="col">Trạng thái</th>
              <th scope="col">Ngày tạo</th>
              <th scope="col">Cập nhật</th>
            </tr>
          </thead>
          <tbody>
            {history.items.map((order) => {
              const label = paymentProductLabel(order.productReference);
              return (
                <tr key={order.paymentOrderId}>
                  <td>
                    <p className="font-semibold text-white">
                      {label === "Sản phẩm khác" ? order.productReference : label}
                    </p>
                    <p className="muted mt-1 font-mono text-xs">{order.paymentOrderId}</p>
                  </td>
                  <td>{formatMoney(order.amount, order.currency)}</td>
                  <td><StatusBadge status={order.status} /></td>
                  <td>{formatAdminDate(order.createdAtUtc)}</td>
                  <td>{formatAdminDate(order.paidAtUtc ?? order.updatedAtUtc)}</td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    )}
    <Pagination page={history.page} totalPages={history.totalPages} pathname="/wallet/history" />
  </>;
}
