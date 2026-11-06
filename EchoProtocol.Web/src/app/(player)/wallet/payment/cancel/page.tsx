import Link from "next/link";
import { PageHeader, Panel } from "@/components/ui/panel";

export const metadata = { title: "Thanh toán chưa hoàn tất" };
export default function PaymentCancelPage() {
  return <><PageHeader eyebrow="Ví của tôi" title="Bạn đã rời trang thanh toán" />
    <Panel><p className="muted">Nếu đã thanh toán, hãy kiểm tra lại trạng thái trước khi thử nạp lần nữa.</p>
      <div className="mt-5 flex flex-wrap gap-3"><Link className="button" href="/wallet/payment/return">Kiểm tra trạng thái</Link><Link className="button button-secondary" href="/wallet">Quay lại ví</Link></div>
    </Panel></>;
}
