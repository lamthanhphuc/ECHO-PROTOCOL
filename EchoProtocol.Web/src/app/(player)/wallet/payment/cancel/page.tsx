import Link from "next/link";
import { PageHeader, Panel } from "@/components/ui/panel";

export const metadata = { title: "Payment Cancel" };
export default function PaymentCancelPage() {
  return <><PageHeader eyebrow="Checkout interrupted" title="Bạn đã rời trang thanh toán" />
    <Panel><p className="muted">Website không tự đánh dấu order là CANCELLED. Webhook và Backend vẫn là nguồn sự thật.</p>
      <div className="mt-5 flex flex-wrap gap-3"><Link className="button" href="/wallet/payment/return">Kiểm tra lại trạng thái</Link><Link className="button button-secondary" href="/wallet">Quay lại Wallet</Link></div>
    </Panel></>;
}
