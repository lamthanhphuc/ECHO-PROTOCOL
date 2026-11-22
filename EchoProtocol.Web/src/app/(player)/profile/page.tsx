import { playerApi } from "@/lib/api/server-api";
import { formatNumber } from "@/lib/utils/format";
import { PageHeader, Panel } from "@/components/ui/panel";
import { ProgressionCosmetics } from "@/components/player/progression-cosmetics";

export const metadata = { title: "Hồ sơ" };

export default async function ProfilePage() {
  const profile = await playerApi.profile();
  const rows = [
    ["Mã người dùng", profile.userId], ["Tên hiển thị", profile.displayName],
    ["Cấp độ", String(profile.level)], ["Tổng XP đã kiếm", formatNumber(profile.lifetimeExperiencePoints ?? profile.experiencePoints)],
    ["Tổng số trận", formatNumber(profile.totalMatches)], ["Số trận thắng", formatNumber(profile.totalWins)],
  ];
  return <>
    <PageHeader eyebrow="Khu người chơi" title="Hồ sơ" />
    <Panel className="max-w-3xl">
      <p className="mb-2">{profile.levelCap > 0 && profile.level >= profile.levelCap
        ? "Đã đạt cấp tối đa"
        : `${formatNumber(profile.experienceInLevel ?? profile.experiencePoints % 500)} / ${formatNumber(profile.experienceRequiredForNextLevel ?? 500)} XP lên cấp tiếp theo`}</p>
      <progress className="mb-4 h-3 w-full accent-red-700" aria-label="Tiến độ lên cấp"
        max={profile.experienceRequiredForNextLevel || 1}
        value={profile.levelCap > 0 && profile.level >= profile.levelCap ? 1 : profile.experienceInLevel ?? profile.experiencePoints % 500} />
      <dl className="divide-y divide-[var(--line)]">
        {rows.map(([label, value]) => <div key={label} className="grid gap-1 py-4 sm:grid-cols-[180px_1fr]">
          <dt className="muted text-sm">{label}</dt><dd className={label === "Mã người dùng" ? "code break-all" : "text-white"}>{value}</dd>
        </div>)}
      </dl>
      {!!profile.levelUnlocks?.length && <section className="mt-5 border-t border-[var(--line)] pt-4">
        <h2 className="font-semibold">Mốc mở quyền mua</h2>
        <p className="muted mt-2 text-sm">Đạt cấp để mở quyền mua; vẫn cần Credits. Món đã sở hữu tiếp tục sử dụng được.</p>
        <ul className="mt-3 grid gap-2 sm:grid-cols-2">{profile.levelUnlocks.map(item => <li key={item.itemId} className="rounded border border-[var(--line)] p-3">
          <p>Cấp {item.requiredLevel} · {item.itemName}</p>
          <p className="muted text-sm">{item.owned ? "Đã sở hữu" : profile.level >= item.requiredLevel ? "Đã mở quyền mua" : `Còn ${formatNumber(item.experienceRemaining)} XP`}</p>
        </li>)}</ul>
      </section>}
      <ProgressionCosmetics initial={profile.cosmeticRewards ?? []} level={profile.level} />
    </Panel>
  </>;
}
