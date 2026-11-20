import Link from "next/link";
import { CloseRoomForm } from "@/components/admin/close-room-form";
import { MatchDetailContent } from "@/components/admin/match-detail";
import { CopyReference } from "@/components/admin/copy-reference";
import { PageHeader, Panel } from "@/components/ui/panel";
import { adminApi } from "@/lib/api/server-api";
import { formatAdminDate as formatDate } from "@/lib/utils/format";

export default async function RoomPage({
  params,
  searchParams,
}: {
  params: Promise<{ roomInstanceId: string }>;
  searchParams: Promise<{ matchId?: string | string[] }>;
}) {
  const { roomInstanceId } = await params;
  const room = await adminApi.room(roomInstanceId);
  let hostUsername = room.hostUserId;
  try { hostUsername = (await adminApi.user(room.hostUserId)).username; } catch { /* user may have been removed */ }
  const { matchId: requestedMatchId } = await searchParams;
  const matchId = typeof requestedMatchId === "string" && requestedMatchId;
  return <>
    <Link className="admin-link mb-5 inline-block" href="/admin/matches">← Phòng và trận đấu</Link>
    <PageHeader title="Chi tiết phòng" description={room.closedAtUtc ? undefined : "Trạng thái kết nối Photon chưa được xác minh"} />
    {!matchId && <Panel className="p-5"><CopyReference label="Mã định danh phòng" value={room.roomInstanceId} alwaysVisible /><dl className="mt-5 grid gap-4 sm:grid-cols-2">
      <div><dt className="eyebrow">Khu vực</dt><dd>{room.actualRegion?.toUpperCase() || (room.region === "auto" ? "Tự động, chưa xác nhận" : room.region.toUpperCase())}</dd></div>
      <div><dt className="eyebrow">Chủ phòng</dt><dd><Link className="admin-link" href={`/admin/users/${room.hostUserId}`}>{hostUsername}</Link></dd></div>
      <div><dt className="eyebrow">Tạo lúc</dt><dd>{formatDate(room.createdAtUtc)}</dd></div>
      <div><dt className="eyebrow">Mã phòng</dt><dd>{room.fusionSessionName}</dd></div>
      <div><dt className="eyebrow">Đóng lúc</dt><dd>{formatDate(room.closedAtUtc)}</dd></div>
      <div><dt className="eyebrow">Lý do đóng</dt><dd>{room.closeReason || "—"}</dd></div>
      {room.closedAtUtc && room.closeReason !== "HOST_LEFT" && room.closeReason !== "ROOM_LEASE_EXPIRED" &&
        <div><dt className="eyebrow">Host thực thi</dt><dd>{room.hostExecutedAtUtc ? formatDate(room.hostExecutedAtUtc) : "Chưa có ACK từ Host"}</dd></div>}
    </dl></Panel>}
    {matchId && (
      <MatchDetailContent
        matchId={matchId}
        roomId={room.roomInstanceId}
        room={room}
        hostUsername={hostUsername}
        roomClosed={
          Boolean(room.closedAtUtc) ||
          matchId !== room.currentMatchId
        }
      />
    )}
    {!room.closedAtUtc && <CloseRoomForm roomInstanceId={room.roomInstanceId} roomName={room.fusionSessionName} />}
    {room.closedAtUtc && <Panel className="mt-5 p-5"><p className="muted text-sm">Backend đã ghi nhận đóng phòng. {room.hostExecutedAtUtc ? "Host đã xác nhận xử lý; client và Photon Cloud chưa được xác nhận." : "Chưa có xác nhận Host, client hoặc Photon Cloud đã rời phòng."}</p></Panel>}
  </>;
}
