"use client";

import { useEffect, useState } from "react";
import Image from "next/image";
import type { ProgressionCosmetic } from "@/lib/types/player";
import { progressionCosmeticAssets } from "@/lib/progression-cosmetic-assets";

export function ProgressionCosmetics({ initial, level }: { initial: ProgressionCosmetic[]; level: number }) {
  const [rewards, setRewards] = useState(initial);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState("");
  useEffect(() => {
    const controller = new AbortController();
    fetch("/api/bff/player/cosmetics/reconcile", { method: "POST", signal: controller.signal })
      .then(async response => { const result = await response.json(); if (response.ok && result.success) setRewards(result.data); })
      .catch(error => { if (error.name !== "AbortError") setMessage("Chưa đồng bộ được phần thưởng. Hãy tải lại hồ sơ."); });
    return () => controller.abort();
  }, []);
  async function equip(item: ProgressionCosmetic) {
    if (busy) return;
    setBusy(true); setMessage("");
    try {
      const response = await fetch("/api/bff/player/cosmetics/equip", { method: "POST", headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ category: item.category, rewardKey: item.equipped ? null : item.rewardKey }) });
      const result = await response.json();
      if (!response.ok || !result.success) throw new Error(result.message || "Không lưu được lựa chọn.");
      setRewards(result.data);
    } catch (error) { setMessage(error instanceof Error ? error.message : "Không lưu được lựa chọn."); }
    finally { setBusy(false); }
  }
  if (!rewards.length) return null;
  return <section className="mt-5 border-t border-[var(--line)] pt-4">
    <h2 className="font-semibold">Ngoại hình miễn phí theo cấp</h2>
    <p className="muted mt-2 text-sm">Nhận một lần, không tốn Credits. Các mốc đã đạt được tự nhận bù. Hình ảnh và model sẽ được bổ sung sau.</p>
    <ul className="mt-3 grid gap-3 sm:grid-cols-2">{rewards.map(item => {
      const asset = progressionCosmeticAssets[item.rewardKey];
      const state = !item.owned ? `Chưa đạt cấp ${item.requiredLevel}` : !asset ? "Đã nhận · Ngoại hình sẽ được bổ sung" : item.equipped ? "Đang dùng" : "Đã nhận";
      return <li key={item.rewardKey} className="rounded border border-[var(--line)] p-3">
        {asset && <Image src={asset} alt={item.displayName} width={160} height={160} className="mb-2 h-24 w-24 object-contain" />}
        <p>Cấp {item.requiredLevel} · {item.displayName}</p>
        <p className="muted mt-1 text-sm">{state}{!item.owned && level >= item.requiredLevel ? " · Đang đồng bộ" : ""}</p>
        <button className="button mt-3" disabled={busy || !item.owned || (!asset && !item.equipped)} onClick={() => equip(item)}>
          {item.equipped ? "Bỏ chọn" : asset ? "Sử dụng" : "Chờ bổ sung ngoại hình"}
        </button>
      </li>;
    })}</ul>
    {message && <p role="status" className="mt-3 text-sm">{message}</p>}
  </section>;
}
