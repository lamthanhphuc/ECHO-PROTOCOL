import { EconomyPanel } from "@/components/admin/economy-panel";
import { PageHeader } from "@/components/ui/panel";
import { adminApi } from "@/lib/api/server-api";

export default async function EconomyPage() {
  return <>
    <PageHeader title="Giá và phần thưởng" />
    <EconomyPanel data={await adminApi.economy()} />
  </>;
}
