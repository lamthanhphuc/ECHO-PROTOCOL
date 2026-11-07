import { EconomyPanel } from "@/components/admin/economy-panel";
import { PageHeader } from "@/components/ui/panel";
import { adminApi } from "@/lib/api/server-api";

export default async function EconomyPage() {
  return <>
    <PageHeader title="Kết quả và kinh tế" description="Đề xuất giá shop và theo dõi lịch sử duyệt." />
    <EconomyPanel data={await adminApi.economy()} />
  </>;
}
