export function StatusBadge({ status }: { status: string }) {
  const tone = status === "FULFILLED" || status === "ACTIVE" || status === "COMPLETED"
    ? "border-emerald-700/70 bg-emerald-900/25 text-emerald-300"
    : status === "FAILED" || status === "CANCELLED" || status === "LOCKED"
      ? "border-rose-800/70 bg-rose-950/35 text-rose-300"
      : "border-amber-700/60 bg-amber-950/25 text-amber-200";
  return <span className={`inline-flex rounded border px-2 py-1 text-xs font-semibold ${tone}`}>{status}</span>;
}
