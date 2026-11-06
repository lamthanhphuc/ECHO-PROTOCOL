import { EmptyState } from "@/components/ui/empty-state";
import { PageHeader } from "@/components/ui/panel";

export const metadata = { title: "Lịch sử thanh toán" };

export default function PaymentHistoryPage() {
  return <>
    <PageHeader eyebrow="Ví của tôi" title="Lịch sử thanh toán" />
    <EmptyState title="Chưa thể hiển thị lịch sử thanh toán">
      Tính năng này sẽ được cập nhật sau.
    </EmptyState>
  </>;
}
