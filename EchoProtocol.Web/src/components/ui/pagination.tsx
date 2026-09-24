import Link from "next/link";

export function Pagination({ page, totalPages, pathname, params = {} }: {
  page: number; totalPages: number; pathname: string; params?: Record<string, string>;
}) {
  const href = (target: number) => {
    const query = new URLSearchParams(params);
    query.set("page", String(target));
    return `${pathname}?${query.toString()}`;
  };
  if (totalPages <= 1) return null;
  return <nav aria-label="Pagination" className="mt-5 flex items-center justify-between gap-3">
    {page > 1 ? <Link className="button button-secondary" href={href(page - 1)}>Trang trước</Link> : <span />}
    <span className="muted text-sm">Trang {page} / {totalPages}</span>
    {page < totalPages ? <Link className="button button-secondary" href={href(page + 1)}>Trang sau</Link> : <span />}
  </nav>;
}
