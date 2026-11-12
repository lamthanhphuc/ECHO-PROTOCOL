import Link from "next/link";
import { AdaptiveConfigurationForm } from "@/components/admin/adaptive-configuration-form";
import { PageHeader, Panel } from "@/components/ui/panel";
import { adminApi } from "@/lib/api/server-api";

export default async function AdaptivePage({ searchParams }: { searchParams: Promise<{ compatibility?: string }> }) {
  const { compatibility } = await searchParams;
  const data = await adminApi.adaptive(compatibility);
  return <>
    <PageHeader title="Điều chỉnh độ khó" description="Xem cách AI thay đổi áp lực lên đội và thử quy tắc với dữ liệu mẫu." />
    <nav aria-label="Phiên bản game" className="mb-5 flex flex-wrap gap-2">
      {data.compatibilities.map((version) => <Link
        key={version} href={`?compatibility=${encodeURIComponent(version)}`}
        aria-current={version === data.compatibility ? "page" : undefined}
        className={`button ${version === data.compatibility ? "" : "button-secondary"}`}>Bản game {version}</Link>)}
    </nav>
    {data.compatibility && data.compatibilities.includes(data.compatibility)
      ? <AdaptiveConfigurationForm key={`${data.compatibility}/${data.version}`} data={data} compatibility={data.compatibility} />
      : <Panel><p className="muted">Chưa có cấu hình đã duyệt cho phiên bản Unity này.</p></Panel>}
  </>;
}
