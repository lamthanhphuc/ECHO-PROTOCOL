export default function Loading() {
  return <div className="flex min-h-[45vh] items-center justify-center" role="status">
    <div className="text-center"><div className="mx-auto h-8 w-8 animate-spin rounded-full border-2 border-[#1d3942] border-t-emerald-300" /><p className="eyebrow mt-4">Đang đồng bộ dữ liệu</p></div>
  </div>;
}
