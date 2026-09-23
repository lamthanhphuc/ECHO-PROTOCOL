import Link from "next/link";
import { playerApi } from "@/lib/api/server-api";
import { formatNumber } from "@/lib/utils/format";
import { PageHeader, Panel, StatCard } from "@/components/ui/panel";

export const metadata = { title: "Player Dashboard" };

export default async function DashboardPage() {
  const profile = await playerApi.profile();
  return <>
    <PageHeader eyebrow="Player uplink" title={`Chào, ${profile.displayName}`}
      description="Trạng thái hồ sơ và tài nguyên đồng bộ từ ECHO PROTOCOL Backend." />
    <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
      <StatCard label="Wallet" value={`${formatNumber(profile.walletBalance)} Coins`} detail="Server-authoritative balance" />
      <StatCard label="Level" value={profile.level} detail={`${formatNumber(profile.experiencePoints)} XP`} />
      <StatCard label="Matches" value={formatNumber(profile.totalMatches)} />
      <StatCard label="Wins" value={formatNumber(profile.totalWins)} />
    </div>
    <Panel className="mt-5">
      <h2 className="text-lg font-semibold text-white">Quick actions</h2>
      <div className="mt-4 flex flex-wrap gap-3">
        <Link className="button" href="/wallet">Mở Wallet</Link>
        <Link className="button button-secondary" href="/wallet/history">Payment History</Link>
        <Link className="button button-secondary" href="/profile">Xem Profile</Link>
      </div>
    </Panel>
  </>;
}
