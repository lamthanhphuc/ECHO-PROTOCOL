"use client";

export default function GlobalError({ error, reset }: { error: Error & { digest?: string }; reset: () => void }) {
  return <div className="mx-auto flex min-h-[55vh] max-w-2xl items-center p-6"><section className="panel w-full rounded-lg border-rose-950 p-6">
    <p className="eyebrow text-rose-300">System fault</p><h1 className="mt-2 text-2xl text-white">Không thể tải dữ liệu</h1>
    <p className="muted mt-3 text-sm">{error.message || "Đã xảy ra lỗi không xác định."}</p>
    <button className="button mt-5" type="button" onClick={reset}>Thử lại</button>
  </section></div>;
}
