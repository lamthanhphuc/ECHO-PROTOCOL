import { PageHeader } from "@/components/ui/panel";
import { PaymentStatusMonitor } from "@/components/payments/payment-status-monitor";

export const metadata = { title: "Trạng thái thanh toán" };
export default function PaymentReturnPage() {
  return <><PageHeader eyebrow="Ví của tôi" title="Trạng thái thanh toán" description="Đang kiểm tra kết quả thanh toán của bạn." /><PaymentStatusMonitor /></>;
}
