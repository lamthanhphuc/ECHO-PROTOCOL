import Link from "next/link";
import { CloseRoomForm } from "@/components/admin/close-room-form";
import { KickPlayersPanel } from "@/components/admin/kick-players-panel";
import { CopyReference } from "@/components/admin/copy-reference";
import { PageHeader, Panel } from "@/components/ui/panel";
import { adminApi } from "@/lib/api/server-api";
import { formatAdminDate as formatDate } from "@/lib/utils/format";

export default async function RoomPage({ params }: { params: Promise<{ roomInstanceId: string }> }) {
  const { roomInstanceId } = await params;
  const room = await adminApi.room(roomInstanceId);
  const current = room.currentMatchId ? await adminApi.match(room.currentMatchId) : null;
  const history = room.currentMatchId ? await adminApi.moderationHistory(room.currentMatchId) : [];
  return <>
    <Link className="admin-link mb-5 inline-block" href="/admin/matches">← Phòng và trận đấu</Link>
    <PageHeader title={room.fusionSessionName} description={room.closedAtUtc ? "Phòng đã đóng trên backend" : "Trạng thái kết nối Photon chưa được xác minh"} />
    <Panel className="p-5"><CopyReference label="RoomInstanceId" value={room.roomInstanceId} /><dl className="mt-5 grid gap-4 sm:grid-cols-2">
      <div><dt className="eyebrow">Khu vực</dt><dd>{room.actualRegion || (room.region === "auto" ? "Tự động, chưa xác nhận" : room.region)}</dd></div>
      <div><dt className="eyebrow">Chủ phòng</dt><dd><Link className="admin-link code" href={`/admin/users/${room.hostUserId}`}>{room.hostUserId}</Link></dd></div>
      <div><dt className="eyebrow">Tạo lúc</dt><dd>{formatDate(room.createdAtUtc)}</dd></div>
      <div><dt className="eyebrow">Kích hoạt lúc</dt><dd>{formatDate(room.activatedAtUtc)}</dd></div>
      <div><dt className="eyebrow">Trận hiện tại</dt><dd className="code">{room.currentMatchId || "—"}</dd></div>
      <div><dt className="eyebrow">Trận đã công bố</dt><dd>{room.matchPublished ? "Có" : "Chưa"}</dd></div>
      <div><dt className="eyebrow">Đóng lúc</dt><dd>{formatDate(room.closedAtUtc)}</dd></div>
      <div><dt className="eyebrow">Lý do đóng</dt><dd>{room.closeReason || "—"}</dd></div>
      {room.closedAtUtc && room.closeReason !== "HOST_LEFT" && room.closeReason !== "ROOM_LEASE_EXPIRED" &&
        <div><dt className="eyebrow">Host thực thi</dt><dd>{room.hostExecutedAtUtc ? formatDate(room.hostExecutedAtUtc) : "Chưa có ACK từ Host"}</dd></div>}
    </dl></Panel>
    <KickPlayersPanel matchId={room.currentMatchId} status={current?.match.status} hostUserId={room.hostUserId}
      closed={Boolean(room.closedAtUtc)} players={current?.players || []} history={history} />
    {!room.closedAtUtc && <CloseRoomForm roomInstanceId={room.roomInstanceId} roomName={room.fusionSessionName} />}
    {room.closedAtUtc && <Panel className="mt-5 p-5"><p className="muted text-sm">Backend đã ghi nhận đóng phòng. {room.hostExecutedAtUtc ? "Host đã xác nhận xử lý; client và Photon Cloud chưa được xác nhận." : "Chưa có xác nhận Host, client hoặc Photon Cloud đã rời phòng."}</p></Panel>}
  </>;
}
