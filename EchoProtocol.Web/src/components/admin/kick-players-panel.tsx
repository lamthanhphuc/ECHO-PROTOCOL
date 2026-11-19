"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useRef, useState } from "react";
import type { AdminMatchDetail, AdminModerationAction } from "@/lib/types/admin";
import { formatAdminDate as formatDate } from "@/lib/utils/format";

type Player = AdminMatchDetail["players"][number];

export function KickPlayersPanel({ matchId, status, hostUserId, closed, players, history }: {
  matchId: string | null;
  status?: "Lobby" | "InMatch" | "Ended";
  hostUserId: string;
  closed: boolean;
  players: Player[];
  history: AdminModerationAction[];
}) {
  const router = useRouter();
  const [selected, setSelected] = useState<string[]>([]);
  const [reason, setReason] = useState("");
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState("");
  const actionIds = useRef<Record<string, string>>({});
  const active = !closed && (status === "Lobby" || status === "InMatch") && Boolean(matchId);
  const eligible = players.filter((player) => !player.kickedAtUtc && (status === "Lobby" ? !player.disconnectedAtUtc : player.participated));
  const remaining = eligible.filter((player) => !selected.includes(player.userId) && !player.disconnectedAtUtc);

  async function kickSelected() {
    const note = reason.trim();
    if (!matchId || !active || selected.length === 0 || note.length < 10 || note.length > 500) {
      setMessage("Chọn người chơi và nhập lý do dài 10–500 ký tự.");
      return;
    }
    if (selected.includes(hostUserId) && remaining.length === 0) {
      setMessage("Cần giữ lại ít nhất một người chơi để nhận quyền Host.");
      return;
    }
    if (!window.confirm(`Đuổi ${selected.length} người khỏi phòng ${status === "Lobby" ? "đang chờ" : "đang chơi"}?${selected.includes(hostUserId) ? " Host sẽ được bàn giao cho người còn lại nếu Fusion chuyển quyền thành công." : ""}`)) return;
    setBusy(true);
    setMessage("");
    const targets = [...selected].sort((a, b) => Number(a === hostUserId) - Number(b === hostUserId));
    let done = 0;
    try {
      for (const userId of targets) {
        actionIds.current[userId] ??= crypto.randomUUID();
        const response = await fetch(`/api/bff/admin/matches/${encodeURIComponent(matchId)}/players/${encodeURIComponent(userId)}/kick`, {
          method: "POST", headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ actionId: actionIds.current[userId], reason: note }),
        });
        const payload = await response.json();
        if (!response.ok || !payload.success) throw new Error(payload.message || "Không gửi được quyết định Kick.");
        delete actionIds.current[userId];
        done++;
        setSelected((current) => current.filter((id) => id !== userId));
      }
      setMessage(`Đã ghi ${done} quyết định Kick. Đang chờ game thực thi; tải lại để xem trạng thái.`);
    } catch (error) {
      setMessage(`Đã ghi ${done}/${targets.length} quyết định. ${error instanceof Error ? error.message : "Lỗi kết nối."} Có thể thử lại các người còn được chọn.`);
    } finally {
      setBusy(false);
      router.refresh();
    }
  }

  return <section aria-labelledby="room-players-heading" className="panel mt-5 p-5">
    <h2 id="room-players-heading" className="admin-section-title mb-2">Thành viên trong phòng</h2>
    <p className="muted mb-4 text-sm">Trạng thái kết nối dựa trên lần Host báo gần nhất; không chứng minh người chơi đang online. Kick được ghi trên backend trước; Host xử lý sau.</p>
    {players.length === 0 ? <p className="muted text-sm">Chưa có thành viên được ghi nhận trong trận hiện tại.</p> :
      <div className="table-wrap rounded-lg"><table><thead><tr><th>Chọn</th><th>Người chơi</th><th>Vai trò</th><th>Trạng thái</th><th>Tham gia trận</th><th>Gián đoạn trong trận</th><th>Vào phòng</th><th>Cập nhật cuối</th><th>Rời phòng</th></tr></thead><tbody>
        {players.map((player) => {
          const decision = history.find((entry) => entry.action === "KICK_PLAYER" && entry.targetUserId === player.userId);
          const selectable = active && eligible.some((item) => item.userId === player.userId);
          return <tr key={player.userId}>
            <td><input type="checkbox" aria-label={`Chọn ${player.displayName || player.username || player.userId}`} checked={selected.includes(player.userId)} disabled={!selectable || busy}
              onChange={(event) => setSelected((current) => event.target.checked ? [...current, player.userId] : current.filter((id) => id !== player.userId))} /></td>
            <td><Link className="admin-link" href={`/admin/users/${player.userId}`}>{player.displayName || player.username || player.userId}</Link></td>
            <td>{player.userId === hostUserId ? "Chủ phòng" : "Người chơi"}</td>
            <td>{player.kickedAtUtc ? decision?.hostExecutedAtUtc ? "Đã Kick · Host xác nhận" : "Đã Kick · chờ Host xác nhận" : player.disconnectedAtUtc ? "Đã báo rời" : "Chưa báo rời; kết nối chưa xác minh"}</td>
            <td>{player.participated ? "Có" : "Chưa"}</td>
            <td>{player.hadGameplayDisconnect ? "Có" : "Không"}</td>
            <td>{formatDate(player.boundAtUtc)}</td><td>{formatDate(player.lastSeenAtUtc)}</td><td>{formatDate(player.disconnectedAtUtc)}</td>
          </tr>;
        })}
      </tbody></table></div>}
    {active && eligible.length > 0 && <div className="mt-5 grid max-w-xl gap-3">
      <label className="admin-field">Lý do Kick (10–500 ký tự)<textarea className="input min-h-24" value={reason} maxLength={500} onChange={(event) => setReason(event.target.value)} disabled={busy} /></label>
      <button className="button button-secondary justify-self-start" type="button" onClick={kickSelected} disabled={busy || selected.length === 0}>{busy ? "Đang xử lý…" : `Kick ${selected.length} người đã chọn`}</button>
      {selected.includes(hostUserId) && <p className="text-sm text-amber-200">Host chỉ rời sau khi game lưu snapshot để bàn giao. Nếu bàn giao thất bại, trạng thái sẽ tiếp tục chờ xử lý.</p>}
    </div>}
    {!active && <p className="muted mt-4 text-sm">Chỉ có thể Kick khi phòng chưa đóng và trận hiện tại đang ở lobby hoặc đang chơi.</p>}
    {message && <p role="status" className="mt-3 text-sm text-amber-200">{message}</p>}
  </section>;
}
