import { redirect } from "next/navigation";
import { adminApi } from "@/lib/api/server-api";
import { MatchDetailContent } from "@/components/admin/match-detail";

export default async function MatchDetailPage({
  params,
}: {
  params: Promise<{ matchId: string }>;
}) {
  const { matchId } = await params;
  const { match } = await adminApi.match(matchId);

  if (match.roomSessionId) {
    redirect(
      `/admin/rooms/${encodeURIComponent(match.roomSessionId)}?matchId=${encodeURIComponent(matchId)}`
    );
  }

  return <MatchDetailContent matchId={matchId} />;
}
