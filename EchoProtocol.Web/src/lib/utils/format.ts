export function formatNumber(value: number | null | undefined): string {
  return value == null ? "—" : new Intl.NumberFormat("vi-VN").format(value);
}

export function formatMoney(value: number, currency: string): string {
  return new Intl.NumberFormat("vi-VN", { style: "currency", currency }).format(value);
}

export function formatDate(value: string | null | undefined): string {
  if (!value) return "—";
  return new Intl.DateTimeFormat("vi-VN", {
    dateStyle: "medium",
    timeStyle: "short",
  }).format(new Date(value));
}

export function queryString(params: Record<string, string | number | undefined>): string {
  const search = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && String(value).trim()) search.set(key, String(value));
  }
  const value = search.toString();
  return value ? `?${value}` : "";
}

export function param(value: string | string[] | undefined): string {
  return Array.isArray(value) ? value[0] ?? "" : value ?? "";
}
