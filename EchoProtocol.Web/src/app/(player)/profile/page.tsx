import { playerApi } from "@/lib/api/server-api";
import { formatNumber } from "@/lib/utils/format";
import { PageHeader, Panel } from "@/components/ui/panel";

export const metadata = { title: "Hồ sơ" };

export default async function ProfilePage() {
  const profile = await playerApi.profile();
  const rows = [
    ["User ID", profile.userId], ["Display name", profile.displayName],
    ["Level", String(profile.level)], ["Experience", formatNumber(profile.experiencePoints)],
    ["Total matches", formatNumber(profile.totalMatches)], ["Total wins", formatNumber(profile.totalWins)],
  ];
  return <>
    <PageHeader eyebrow="Identity record" title="Player Profile" />
    <Panel className="max-w-3xl">
      <dl className="divide-y divide-[#142a31]">
        {rows.map(([label, value]) => <div key={label} className="grid gap-1 py-4 sm:grid-cols-[180px_1fr]">
          <dt className="muted text-sm">{label}</dt><dd className={label === "User ID" ? "code break-all" : "text-white"}>{value}</dd>
        </div>)}
      </dl>
    </Panel>
  </>;
}
