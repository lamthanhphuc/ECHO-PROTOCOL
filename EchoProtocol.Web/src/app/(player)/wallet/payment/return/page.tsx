import { PageHeader } from "@/components/ui/panel";
import { PaymentStatusMonitor } from "@/components/payments/payment-status-monitor";

export const metadata = { title: "Payment Status" };
export default function PaymentReturnPage() {
  return <><PageHeader eyebrow="Payment verification" title="Trạng thái thanh toán" description="Trang này không tin query từ payOS; trạng thái chỉ đến từ Backend." /><PaymentStatusMonitor /></>;
}
