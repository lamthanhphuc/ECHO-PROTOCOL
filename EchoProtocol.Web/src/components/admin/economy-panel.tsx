"use client";

import { useState, type FormEvent } from "react";
import { useRouter } from "next/navigation";
import { Panel } from "@/components/ui/panel";
import { EconomyWalletTelemetry } from "@/components/admin/economy-wallet-telemetry";
import type { EconomyData } from "@/lib/types/operations";
import { formatAdminDate as formatDate } from "@/lib/utils/format";

const rewardFields = [
  ["participation", "Tham gia"],
  ["objective", "Hoàn thành mục tiêu (tối đa)"],
  ["win", "Thắng trận"],
  ["survival", "Sống sót"],
  ["contribution", "Đóng góp mục tiêu"],
  ["disconnected", "Mất kết nối"],
  ["maximum", "Tổng thưởng tối đa"],
] as const;

const rewardPilotV2 = {
  participation: 0, objective: 70, win: 150, survival: 10,
  contribution: 70, disconnected: 0, maximum: 320,
};

const actionFields = [
  ["objectiveActionCap", "Số hành động mục tiêu tối đa", 4, 1, 100],
  ["rescueActionCap", "Số lần cứu tối đa", 2, 1, 100],
  ["oneActionTeamPercent", "Hệ số đội khi có 1 hành động (%)", 50, 0, 100],
  ["twoActionTeamPercent", "Hệ số đội khi có 2 hành động (%)", 75, 0, 100],
  ["fullTeamPercent", "Hệ số đội khi có từ 3 hành động (%)", 100, 0, 100],
] as const;

const experienceFields = [
  ["winExperience", "XP thắng trận", 75, 0, 10000],
  ["objectiveExperience", "XP hoàn thành mục tiêu", 50, 0, 10000],
  ["contributionExperience", "XP đóng góp", 25, 0, 10000],
  ["survivalExperience", "XP sống sót", 25, 0, 10000],
  ["lossExperiencePerMinute", "Trần XP/phút khi thua", 1, 0, 100],
] as const;

const itemCategories = [
  { value: "CHARACTER", label: "Nhân vật gốc" },
  { value: "CHARACTER_OUTFIT", label: "Trang phục nhân vật" },
  { value: "TEAM_TOOL", label: "Công cụ đội" },
  { value: "PET", label: "Thú cưng" },
  { value: "PET_SKIN", label: "Trang phục thú cưng" },
];

const v3Fields = [
  ["baseMinutes", "Thời lượng chuẩn chung (phút)", 20, 1, 120],
  ["sciFiReferenceMinutes", "Tref · Cơ sở nghiên cứu (phút)", 20, 1, 120],
  ["floodedReferenceMinutes", "Tref · Vùng ngập (phút)", 20, 1, 120],
  ["purgeReferenceMinutes", "Tref · Thanh trừng (phút)", 20, 1, 120],
  ["easyMultiplier", "Hệ số độ khó Dễ", 0.9, 0.5, 1],
  ["hardMultiplier", "Hệ số độ khó Khó", 1.15, 1, 1.15],
  ["v3LossCreditsPerMinute", "Trần xu/phút khi thua · V3", 4, 0, 10],
  ["v3LossExperiencePerMinute", "Trần XP/phút khi thua · V3", 2, 0, 10],
] as const;

const proposalStatuses: Record<string, string> = {
  PENDING: "Chờ duyệt",
  APPROVED: "Đã duyệt",
  REJECTED: "Đã từ chối",
};

export function EconomyPanel({ data }: { data: EconomyData }) {
  const router = useRouter();
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState("");
  const [policyKey, setPolicyKey] = useState(data.rewards?.policyKey ?? "REWARD_V1");
  const [category, setCategory] = useState("");
  const [search, setSearch] = useState("");
  const [itemId, setItemId] = useState("");
  const [price, setPrice] = useState("");
  const [requiredLevel, setRequiredLevel] = useState("");
  const [proposalReason, setProposalReason] = useState("");
  const query = search.trim().toLocaleLowerCase("vi");
  const availableCategories = itemCategories.filter(({ value }) =>
    data.items.some((row) => row.category === value));
  const filteredItems = data.items.filter((row) =>
    row.category === category && row.itemName.toLocaleLowerCase("vi").includes(query));
  const item = filteredItems.find((row) => row.itemId === itemId);
  const itemsById = new Map(data.items.map((row) => [row.itemId, row]));
  const pending = data.proposals.filter((proposal) => proposal.status === "PENDING");
  const history = data.proposals.filter((proposal) => proposal.status !== "PENDING");
  const switchingToV2 = policyKey !== "REWARD_V1" && data.rewards?.policyKey === "REWARD_V1";
  const isV3 = policyKey === "REWARD_V3";

  function selectItem(nextId: string) {
    if (nextId === itemId) return;
    setItemId(nextId);
    setPrice("");
    setRequiredLevel("");
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
      const response = await fetch(proposalId ? `/api/bff/admin/economy/prices/${proposalId}/review` : "/api/bff/admin/economy/prices/apply", {
        method: "POST", headers: { "Content-Type": "application/json" },
        body: JSON.stringify(proposalId ? { approve, reason } : { itemId, expectedPrice: item?.price,
          price: price === "" ? item?.price : Number(price),
          requiredLevel: requiredLevel === "" ? item?.requiredLevel : Number(requiredLevel),
          expectedRequiredLevel: item?.requiredLevel, reason }),
      });
      const body = await response.json();
      if (!response.ok || !body.success) throw new Error(body.message || "Không lưu được thay đổi.");
      form.reset();
      if (!proposalId) selectItem("");
      setMessage(proposalId ? "Đã ghi nhận quyết định duyệt." : "Đã lưu giá và cấp mua.");
      router.refresh();
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "Không kết nối được máy chủ.");
    } finally {
      setBusy(false);
    }
  }

  async function saveRewards(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const fields = new FormData(event.currentTarget);
    setBusy(true);
    setMessage("");
    try {
      const response = await fetch("/api/bff/admin/economy/rewards", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          ...Object.fromEntries(rewardFields.map(([key]) => [key, isV3 ? rewardPilotV2[key] : Number(fields.get(key))])),
          policyKey: String(fields.get("policyKey")),
          lossCapCreditsPerMinute: Number(fields.get("lossCapCreditsPerMinute") ?? data.rewards?.lossCapCreditsPerMinute ?? 2),
          rescueBonusCredits: isV3 ? 10 : Number(fields.get("rescueBonusCredits") ?? data.rewards?.rescueBonusCredits ?? 10),
          ...Object.fromEntries([...actionFields, ...experienceFields].map(([key, , initial]) =>
            [key, isV3 ? initial : Number(fields.get(key) ?? data.rewards?.[key] ?? initial)])),
          ...Object.fromEntries(v3Fields.map(([key, , initial]) =>
            [key, Number(fields.get(key) ?? data.rewards?.[key] ?? initial)])),
          effectiveAtUtc: data.rewards?.effectiveAtUtc,
        }),
      });
      const body = await response.json();
      if (!response.ok || !body.success) throw new Error(body.message || "Không lưu được mức thưởng.");
      setMessage("Đã lưu cấu hình thưởng chung.");
      router.refresh();
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "Không kết nối được máy chủ.");
    } finally {
      setBusy(false);
    }
  }

  return <div className="grid gap-5">
    <EconomyWalletTelemetry data={data.walletTelemetry} />
    {data.rewardTelemetry && <Panel>
      <h2 className="mb-2 font-semibold">Dữ liệu cân bằng Reward · {data.rewardTelemetry.days} ngày</h2>
      <p className="muted mb-3 text-sm">Loại trận TEST_ONLY. Median thời lượng thắng loại trận mất kết nối; chỉ gợi ý Tref sau 50 trận thắng hợp lệ trong cùng nhóm. Số liệu này không tự đổi cấu hình.{data.rewardTelemetry.truncated && ` Đang giới hạn ${data.rewardTelemetry.limit} trận gần nhất.`}</p>
      {data.rewardTelemetry.cohorts.length === 0 ? <p className="muted">Chưa có trận hợp lệ đã trả thưởng.</p> :
        <div className="overflow-x-auto"><table className="w-full text-left text-sm"><thead><tr>
          <th className="p-2">Map / độ khó / đội / policy</th><th className="p-2">Trận / thắng hợp lệ</th><th className="p-2">Median thắng</th><th className="p-2">Xu / XP mỗi người·giờ</th><th className="p-2">Tref gợi ý</th>
        </tr></thead><tbody>{data.rewardTelemetry.cohorts.map((row, index) => <tr key={index} className="border-t border-[var(--line)]">
          <td className="p-2">{row.mapId ?? "Chưa rõ"} / {row.difficulty ?? "—"} / {row.playerCount} / {row.rewardPolicyVersion ?? "V1 cũ"}</td>
          <td className="p-2">{row.matches} / {row.eligibleWins}</td><td className="p-2">{row.medianWinMinutes?.toFixed(1) ?? "—"} phút</td>
          <td className="p-2">{row.creditsPerPlayerHour.toFixed(1)} / {row.xpPerPlayerHour.toFixed(1)}</td><td className="p-2">{row.suggestedReferenceMinutes?.toFixed(1) ?? "Chờ thêm dữ liệu"}</td>
        </tr>)}</tbody></table></div>}
    </Panel>}
    <Panel>
      <h2 className="mb-2 font-semibold">Luật kết quả và thưởng</h2>
      {data.rewards && <form key={data.rewardPolicy} className="mt-4 grid gap-3 sm:grid-cols-2" onSubmit={saveRewards}>
        <label className="admin-field">Reward Policy<select className="input" name="policyKey" value={policyKey} onChange={(event) => setPolicyKey(event.target.value as typeof policyKey)} disabled={busy}>
          <option value="REWARD_V1">Reward V1</option><option value="REWARD_V2">Reward V2</option><option value="REWARD_V3">Reward V3</option>
        </select></label>
        <h3 className="font-semibold sm:col-span-2">Xu thưởng</h3>
        {rewardFields.filter(([key]) => policyKey === "REWARD_V1" || (key !== "participation" && key !== "disconnected")).map(([key, label]) => <label className="admin-field" key={key}>{label} (xu)
          <input key={`${policyKey}-${key}`} className="input" name={key} type="number" min={0} max={10000} step={1} required defaultValue={isV3 || switchingToV2 ? rewardPilotV2[key] : data.rewards?.[key]} readOnly={isV3} disabled={busy} />
        </label>)}
        {policyKey !== "REWARD_V1" && <>
          {!isV3 && <label className="admin-field">Trần xu/phút khi thua<input className="input" name="lossCapCreditsPerMinute" type="number" min={0} max={100} step="0.1" required defaultValue={data.rewards.lossCapCreditsPerMinute ?? 2} disabled={busy} /></label>}
          <label className="admin-field">Xu mỗi lần cứu đồng đội<input key={`${policyKey}-rescue`} className="input" name="rescueBonusCredits" type="number" min={0} max={10000} step={1} required defaultValue={isV3 ? 10 : data.rewards.rescueBonusCredits ?? 10} readOnly={isV3} disabled={busy} /></label>
          <p className="muted text-sm sm:col-span-2">Xu cứu đồng đội chịu trần thưởng và trần khi thua.</p>
          <h3 className="font-semibold sm:col-span-2">Đóng góp và hệ số đội</h3>
          {actionFields.map(([key, label, initial, min, max]) => <label className="admin-field" key={key}>{label}
            <input key={`${policyKey}-${key}`} className="input" name={key} type="number" min={min} max={max} step={1} required defaultValue={isV3 ? initial : data.rewards?.[key] ?? initial} readOnly={isV3} disabled={busy} />
          </label>)}
          <h3 className="font-semibold sm:col-span-2">Kinh nghiệm (XP)</h3>
          {experienceFields.filter(([key]) => !isV3 || key !== "lossExperiencePerMinute").map(([key, label, initial, min, max]) => <label className="admin-field" key={key}>{label}
            <input key={`${policyKey}-${key}`} className="input" name={key} type="number" min={min} max={max} step={1} required defaultValue={isV3 ? initial : data.rewards?.[key] ?? initial} readOnly={isV3} disabled={busy} />
          </label>)}
        </>}
        {isV3 && <>
          <h3 className="font-semibold sm:col-span-2">Map, độ khó và thưởng thua V3</h3>
          <p className="muted text-sm sm:col-span-2">M = Tref / thời lượng chuẩn, giới hạn 0,75–1,50. Tref khởi tạo 20 phút để M = 1; chỉ điều chỉnh khi có dữ liệu trận thực. Thưởng thua dùng tối đa min(thời gian tham gia, Tref, 30 phút). Contribution và F giữ nguyên V2.</p>
          {v3Fields.map(([key, label, initial, min, max]) => <label className="admin-field" key={key}>{label}
            <input className="input" name={key} type="number" min={min} max={max} step="0.01" required defaultValue={data.rewards?.[key] ?? initial} disabled={busy} />
          </label>)}
        </>}
        <button className="button self-end justify-self-start" type="submit" disabled={busy}>Lưu mức thưởng</button>
      </form>}
    </Panel>
    <Panel>
      <h2 className="mb-3 font-semibold">Giá và cấp mua trong Shop</h2>
      <p className="muted mb-4 text-sm">Thay đổi giá hoặc cấp mua để áp dụng ngay. Để trống ô nào thì giữ giá trị hiện tại.</p>
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
        <label className="admin-field">Giá mới (xu)<input className="input" name="price" type="number" min={0} max={1000000} step={1} placeholder={item ? String(item.price) : ""}
          value={price} disabled={!item || busy} onChange={(e) => setPrice(e.target.value)} /></label>
        <label className="admin-field">Cấp cần đạt để mua<input className="input" type="number" min={1} max={item?.category === "TEAM_TOOL" && !["Core Stabilizer", "Field Scanner"].includes(item.itemName) ? 1 : 30} step={1}
          placeholder={item ? String(item.requiredLevel) : ""} value={requiredLevel} disabled={!item || busy} onChange={(e) => setRequiredLevel(e.target.value)} /></label>
        {item && <p className="muted self-end text-sm">Hiện tại: cấp {item.requiredLevel}. Món đã sở hữu vẫn được trang bị. Core Stabilizer và Field Scanner có thể yêu cầu cấp cao hơn; các tool khác mở từ cấp 1.</p>}
        <label className="admin-field sm:col-span-2">Lý do<textarea className="input" name="reason" required minLength={10} maxLength={500}
          value={proposalReason} disabled={!item || busy} onChange={(e) => setProposalReason(e.target.value)} /></label>
        <div>
          {item && <p className="muted mb-2 text-sm">{item.itemName} · {item.price} xu → {price === "" ? "Chưa nhập giá mới" : `${price} xu`}</p>}
          <button className="button" disabled={busy || !item || ((price === "" || Number(price) === item.price) && (requiredLevel === "" || Number(requiredLevel) === item.requiredLevel))}>Lưu và áp dụng</button>
        </div>
      </form>
    </Panel>
    <Panel>
      <h2 className="mb-2 font-semibold">Lịch sử giá và cấp mua</h2>
      {[{ title: "Đề xuất cũ chờ xử lý", proposals: pending }, { title: "Đã áp dụng và xử lý", proposals: history }]
        .filter((group) => group.proposals.length > 0)
        .map((group) => <section className="mt-4" key={group.title}>
        <h3 className="mb-2 font-semibold">{group.title} ({group.proposals.length})</h3>
        {group.proposals.map((proposal) => {
          const current = itemsById.get(proposal.itemId);
          const priceChanged = proposal.status === "PENDING" && current && (current.price !== proposal.previousPrice
            || (proposal.previousRequiredLevel != null && current.requiredLevel !== proposal.previousRequiredLevel));
          return <details className="mb-2 rounded border border-[var(--line)] p-3 text-sm" key={proposal.id}>
        <summary className="cursor-pointer">{current?.itemName ?? "Mặt hàng không còn trong danh sách"}
          {" · "}{proposal.previousPrice} → {proposal.proposedPrice} xu · {priceChanged ? "Cần đề xuất lại vì mặt hàng đã đổi" : proposalStatuses[proposal.status] ?? "Chưa rõ trạng thái"}</summary>
        {priceChanged && <p className="muted mt-2">Hiện tại {current.price} xu, cấp {current.requiredLevel}. Hãy gửi đề xuất mới.</p>}
        <p className="mt-2">Lý do đề xuất · {proposal.reason}</p>
        {proposal.proposedRequiredLevel != null && <p>Cấp mua · {proposal.previousRequiredLevel} → {proposal.proposedRequiredLevel}</p>}
        {proposal.reviewReason && <p>Lý do xử lý · {proposal.reviewReason}</p>}
        <details className="muted mt-2">
          <summary className="cursor-pointer">Người thực hiện và thời gian</summary>
          <p className="break-all">Người gửi · {proposal.proposedByUserId} · {formatDate(proposal.proposedAtUtc)}</p>
          {proposal.reviewedAtUtc && <p className="break-all">Người xử lý · {proposal.reviewedByUserId} · {formatDate(proposal.reviewedAtUtc)}</p>}
        </details>
        {proposal.status === "PENDING" && <form
          className="mt-3 grid gap-2" onSubmit={(event) => submit(event, proposal.id)}>
          <label className="admin-field">Lý do duyệt hoặc từ chối<textarea className="input" name="reason" required minLength={10} maxLength={500} /></label>
          <div className="flex gap-2"><button className="button" type="submit" value="approve" disabled={busy || !!priceChanged}>Duyệt giá</button>
            <button className="button button-secondary" type="submit" value="reject" disabled={busy}>Từ chối</button></div>
        </form>}
        </details>;
        })}
      </section>)}
    </Panel>
    {message && <p role="status" className="text-sm">{message}</p>}
  </div>;
}
