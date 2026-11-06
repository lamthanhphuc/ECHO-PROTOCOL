import Link from "next/link";
import { playerApi } from "@/lib/api/server-api";
import { formatNumber } from "@/lib/utils/format";
import { PageHeader, Panel, StatCard } from "@/components/ui/panel";

export const metadata = { title: "Tổng quan" };

export default async function DashboardPage() {
  const profile = await playerApi.profile();
  return <>
    <PageHeader eyebrow="Khu người chơi" title={`Chào, ${profile.displayName}`}
      description="Thông tin tài khoản và thành tích của bạn." />
    <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
      <StatCard label="Số dư" value={`${formatNumber(profile.walletBalance)} xu`} />
      <StatCard label="Cấp độ" value={profile.level} detail={`${formatNumber(profile.experiencePoints)} điểm kinh nghiệm`} />
      <StatCard label="Số trận" value={formatNumber(profile.totalMatches)} />
      <StatCard label="Trận thắng" value={formatNumber(profile.totalWins)} />
    </div>
    <Panel className="mt-5">
      <h2 className="text-lg font-semibold text-white">Truy cập nhanh</h2>
      <div className="mt-4 flex flex-wrap gap-3">
        <Link className="button" href="/wallet">Xem ví</Link>
        <Link className="button button-secondary" href="/wallet/history">Lịch sử thanh toán</Link>
        <Link className="button button-secondary" href="/profile">Xem hồ sơ</Link>
      </div>
    </Panel>
  </>;
}
