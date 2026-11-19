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
  return <nav aria-label="Phân trang" className="mt-5 flex flex-wrap items-center justify-center gap-2">
    {page > 1 && <Link className="button button-secondary whitespace-nowrap" href={href(page - 1)}>Trang trước</Link>}
    <form action={pathname} className="flex flex-nowrap items-center gap-2 text-sm whitespace-nowrap">
      {Object.entries(params).filter(([key, value]) => key !== "page" && value).map(([key, value]) =>
        <input key={key} type="hidden" name={key} value={value} />)}
      <label htmlFor="pagination-page">Trang</label>
      <input id="pagination-page" className="input w-14 px-2 text-center tabular-nums" type="number" name="page" min={1} max={totalPages} step={1} required defaultValue={page} aria-label={`Chọn trang từ 1 đến ${totalPages}`} />
      <span className="muted tabular-nums">/ {totalPages}</span>
      <button className="button button-secondary whitespace-nowrap" type="submit">Đi tới</button>
    </form>
    {page < totalPages && <Link className="button button-secondary whitespace-nowrap" href={href(page + 1)}>Trang sau</Link>}
  </nav>;
}
