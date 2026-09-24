import { EmptyState } from "@/components/ui/empty-state";
import { PageHeader } from "@/components/ui/panel";

export const metadata = { title: "Payment History" };

export default function PaymentHistoryPage() {
  return <>
    <PageHeader eyebrow="Transaction archive" title="Payment History" />
    <EmptyState title="Owner payment history contract chưa có">
      Backend hiện chỉ có service đọc một order và Admin payment list. Player Portal không sử dụng Admin API để tránh vượt quyền.
    </EmptyState>
  </>;
}
