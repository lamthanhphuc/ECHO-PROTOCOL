"use client";

import { useState, type FormEvent } from "react";
import { useRouter } from "next/navigation";
import { Panel } from "@/components/ui/panel";
import type { EconomyData } from "@/lib/types/operations";
import { formatDate } from "@/lib/utils/format";

const itemCategories = [
  { value: "CHARACTER", label: "Nhân vật gốc" },
  { value: "CHARACTER_OUTFIT", label: "Trang phục nhân vật" },
  { value: "TEAM_TOOL", label: "Công cụ đội" },
  { value: "PET", label: "Thú cưng" },
  { value: "PET_SKIN", label: "Trang phục thú cưng" },
];

const proposalStatuses: Record<string, string> = {
  PENDING: "Chờ duyệt",
  APPROVED: "Đã duyệt",
  REJECTED: "Đã từ chối",
};

export function EconomyPanel({ data }: { data: EconomyData }) {
  const router = useRouter();
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState("");
  const [category, setCategory] = useState("");
  const [search, setSearch] = useState("");
  const [itemId, setItemId] = useState("");
  const [price, setPrice] = useState("");
  const [proposalReason, setProposalReason] = useState("");
  const query = search.trim().toLocaleLowerCase("vi");
  const availableCategories = itemCategories.filter(({ value }) =>
    data.items.some((row) => row.category === value));
  const filteredItems = data.items.filter((row) =>
    row.category === category && row.itemName.toLocaleLowerCase("vi").includes(query));
  const item = filteredItems.find((row) => row.itemId === itemId);
  const pending = data.proposals.filter((proposal) => proposal.status === "PENDING");
  const history = data.proposals.filter((proposal) => proposal.status !== "PENDING");

  function selectItem(nextId: string) {
    if (nextId === itemId) return;
    setItemId(nextId);
    setPrice("");
    setProposalReason("");
  }

  async function submit(event: FormEvent<HTMLFormElement>, proposalId?: string) {
    event.preventDefault();
    const form = event.currentTarget;
    const fields = new FormData(form);
    const approve = (event.nativeEvent as SubmitEvent).submitter?.getAttribute("value") === "approve";
    const reason = String(fields.get("reason") ?? "").trim();
    if (!proposalId && !item) return;
    setBusy(true);
    setMessage("");
    try {
      const response = await fetch(proposalId ? `/api/bff/admin/economy/prices/${proposalId}/review` : "/api/bff/admin/economy/prices", {
        method: "POST", headers: { "Content-Type": "application/json" },
        body: JSON.stringify(proposalId ? { approve, reason } : { itemId, expectedPrice: item?.price, price: Number(fields.get("price")), reason }),
      });
      const body = await response.json();
      if (!response.ok || !body.success) throw new Error(body.message || "Không lưu được thay đổi.");
      form.reset();
      if (!proposalId) selectItem("");
      setMessage(proposalId ? "Đã ghi nhận quyết định duyệt." : "Đã gửi đề xuất giá.");
      router.refresh();
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "Không kết nối được máy chủ.");
    } finally {
      setBusy(false);
    }
  }

  return <div className="grid gap-5">
    <Panel>
      <h2 className="mb-2 font-semibold">Luật kết quả và thưởng</h2>
      <p className="text-sm">Chính sách {data.rewardPolicy} · Tối đa {data.maximumCurrencyPerMatch} xu mỗi trận.</p>
      <p className="muted mt-1 text-sm">Thắng/thua, tư cách nhận thưởng, điều kiện mở khóa và trọng số đóng góp được quản lý bởi nghiệp vụ game.</p>
    </Panel>
    <Panel>
      <h2 className="mb-3 font-semibold">Đề xuất giá shop</h2>
      <form onSubmit={(event) => submit(event)} className="grid gap-3 sm:grid-cols-2">
        <label className="admin-field">Loại mặt hàng<select className="input" required disabled={busy} value={category} onChange={(e) => {
          setCategory(e.target.value);
          setSearch("");
          selectItem("");
        }}>
          <option value="">Chọn loại mặt hàng</option>
          {availableCategories.map(({ value, label }) => <option key={value} value={value}>{label}</option>)}
        </select></label>
        <label className="admin-field">Tìm theo tên<input className="input" type="search" value={search} disabled={!category || busy}
          onChange={(e) => setSearch(e.target.value)} /></label>
        <label className="admin-field">Mặt hàng<select className="input" required disabled={busy || filteredItems.length === 0}
          value={item?.itemId ?? ""} onChange={(e) => selectItem(e.target.value)}>
          <option value="">{!category ? "Chọn loại mặt hàng trước" : filteredItems.length === 0 ? "Không có mặt hàng phù hợp" : "Chọn mặt hàng"}</option>
          {filteredItems.map((row) => <option key={row.itemId} value={row.itemId}>{row.itemName} · {row.price} xu{row.isActive ? "" : " · Đang ẩn"}</option>)}
        </select></label>
        <label className="admin-field">Giá mới (xu)<input className="input" name="price" type="number" min={0} max={1000000} step={1} required
          value={price} disabled={!item || busy} onChange={(e) => setPrice(e.target.value)} /></label>
        <label className="admin-field sm:col-span-2">Lý do<textarea className="input" name="reason" required minLength={10} maxLength={500}
          value={proposalReason} disabled={!item || busy} onChange={(e) => setProposalReason(e.target.value)} /></label>
        <div>
          {item && <p className="muted mb-2 text-sm">{item.itemName} · {item.price} xu → {price === "" ? "Chưa nhập giá mới" : `${price} xu`}</p>}
          <button className="button" disabled={busy || !item || price === "" || Number(price) === item.price}>Gửi đề xuất</button>
        </div>
      </form>
    </Panel>
    <Panel>
      <h2 className="mb-2 font-semibold">Duyệt giá và lịch sử</h2>
      <p className="muted mb-4 text-sm">{data.canReview ? "Bạn có quyền duyệt đề xuất của quản trị viên khác." : "Tài khoản này có quyền gửi đề xuất và xem lịch sử."}</p>
      {[{ title: "Chờ duyệt", proposals: pending }, { title: "Lịch sử", proposals: history }].map((group) => <section className="mt-4" key={group.title}>
        <h3 className="mb-2 font-semibold">{group.title} ({group.proposals.length})</h3>
        {group.proposals.length === 0 && <p className="muted text-sm">Chưa có đề xuất.</p>}
        {group.proposals.map((proposal) => <details className="mb-2 rounded border border-[var(--line)] p-3 text-sm" key={proposal.id}>
        <summary className="cursor-pointer">{data.items.find((row) => row.itemId === proposal.itemId)?.itemName ?? proposal.itemId}
          {" · "}{proposal.previousPrice} → {proposal.proposedPrice} xu · {proposalStatuses[proposal.status] ?? proposal.status}</summary>
        <p className="mt-2">Lý do đề xuất · {proposal.reason}</p>
        {proposal.reviewReason && <p>Lý do xử lý · {proposal.reviewReason}</p>}
        <details className="muted mt-2">
          <summary className="cursor-pointer">Người thực hiện và thời gian</summary>
          <p className="break-all">Người gửi · {proposal.proposedByUserId} · {formatDate(proposal.proposedAtUtc)}</p>
          {proposal.reviewedAtUtc && <p className="break-all">Người xử lý · {proposal.reviewedByUserId} · {formatDate(proposal.reviewedAtUtc)}</p>}
        </details>
        {data.canReview && proposal.status === "PENDING" && proposal.proposedByUserId !== data.adminId && <form
          className="mt-3 grid gap-2" onSubmit={(event) => submit(event, proposal.id)}>
          <label className="admin-field">Lý do duyệt hoặc từ chối<textarea className="input" name="reason" required minLength={10} maxLength={500} /></label>
          <div className="flex gap-2"><button className="button" type="submit" value="approve" disabled={busy}>Duyệt giá</button>
            <button className="button button-secondary" type="submit" value="reject" disabled={busy}>Từ chối</button></div>
        </form>}
        </details>)}
      </section>)}
    </Panel>
    {message && <p role="status" className="text-sm">{message}</p>}
  </div>;
}
