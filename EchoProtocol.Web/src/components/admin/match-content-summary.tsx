import type { MatchContent } from "@/lib/types/operations";
import { difficultyLabel, mapLabel, modeLabel, monsterLabel, routeLabel } from "@/lib/utils/game-labels";

export function MatchContentSummary({ map, mode, monster, route, gameVersion, difficulty }: MatchContent["entries"][number] & {
  gameVersion: string;
  difficulty?: string;
}) {
  const fields = [
    ["Bản đồ", mapLabel(map)],
    ["Chế độ", modeLabel(mode)],
    ["Quái chính", monsterLabel(monster)],
    ["Tuyến", routeLabel(route)],
    ["Bản game tương thích", gameVersion || "—"],
    ...(difficulty ? [["Độ khó", difficultyLabel(difficulty)]] : []),
  ];

  return <div className="rounded border border-[var(--line)] p-3 text-sm">
    <dl className="grid gap-2 sm:grid-cols-2">
      {fields.map(([label, value]) => <div key={label}>
        <dt className="muted">{label}</dt><dd>{value}</dd>
      </div>)}
    </dl>
    <details className="muted mt-2 text-xs">
      <summary className="cursor-pointer">Chi tiết kỹ thuật</summary>
      <code className="break-all">{map} · {mode} · {monster} · {route}</code>
    </details>
  </div>;
}
