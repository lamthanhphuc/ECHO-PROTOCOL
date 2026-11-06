import { playerApi } from "@/lib/api/server-api";
import { formatNumber } from "@/lib/utils/format";
import { PageHeader, Panel } from "@/components/ui/panel";

export const metadata = { title: "Hồ sơ" };

export default async function ProfilePage() {
  const profile = await playerApi.profile();
  const rows = [
    ["Mã người dùng", profile.userId], ["Tên hiển thị", profile.displayName],
    ["Cấp độ", String(profile.level)], ["Điểm kinh nghiệm", formatNumber(profile.experiencePoints)],
    ["Tổng số trận", formatNumber(profile.totalMatches)], ["Số trận thắng", formatNumber(profile.totalWins)],
  ];
  return <>
    <PageHeader eyebrow="Khu người chơi" title="Hồ sơ" />
    <Panel className="max-w-3xl">
      <dl className="divide-y divide-[var(--line)]">
        {rows.map(([label, value]) => <div key={label} className="grid gap-1 py-4 sm:grid-cols-[180px_1fr]">
          <dt className="muted text-sm">{label}</dt><dd className={label === "Mã người dùng" ? "code break-all" : "text-white"}>{value}</dd>
        </div>)}
      </dl>
    </Panel>
  </>;
}
