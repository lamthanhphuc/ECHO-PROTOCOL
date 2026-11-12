import { AdaptiveConfigurationForm } from "@/components/admin/adaptive-configuration-form";
import { PageHeader, Panel } from "@/components/ui/panel";
import { adminApi } from "@/lib/api/server-api";

export default async function AdaptivePage() {
  const data = await adminApi.adaptive();
  return <>
    <PageHeader title="Điều chỉnh độ khó" description="Xem cách AI thay đổi áp lực lên đội và thử quy tắc với dữ liệu mẫu." />
    {data.compatibility && data.compatibilities.includes(data.compatibility)
      ? <AdaptiveConfigurationForm key={`${data.compatibility}/${data.version}`} data={data} compatibility={data.compatibility} />
      : <Panel><p className="muted">Chưa có cấu hình đã duyệt cho phiên bản Unity này.</p></Panel>}
  </>;
}
