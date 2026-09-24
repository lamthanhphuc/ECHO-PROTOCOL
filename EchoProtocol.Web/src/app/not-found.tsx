import Link from "next/link";

export default function NotFound() {
  return <main className="mx-auto flex min-h-screen max-w-2xl items-center p-6"><section className="panel w-full rounded-lg p-8 text-center">
    <p className="eyebrow">404 / Signal lost</p><h1 className="mt-3 text-3xl text-white">Không tìm thấy trang</h1>
    <p className="muted mt-3">Đường dẫn không tồn tại hoặc tài nguyên đã bị gỡ.</p><Link className="button mt-6" href="/">Về cổng chính</Link>
  </section></main>;
}
