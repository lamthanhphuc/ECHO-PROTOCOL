import { redirect } from "next/navigation";
import { getSession } from "@/lib/auth/session";
import { LoginForm } from "@/components/auth/login-form";

export const metadata = { title: "Đăng nhập" };

export default async function LoginPage() {
  const session = await getSession();
  if (session) redirect(session.role === "ADMIN" ? "/admin" : "/dashboard");

  return <main className="login-stage mx-auto grid min-h-screen w-full max-w-[1480px] items-center gap-10 px-6 pb-10 pt-28 lg:grid-cols-[1fr_460px] lg:gap-20 lg:px-16 lg:pt-16">
    <div className="max-w-2xl">
      <div className="login-emblem" aria-hidden="true">E</div>
      <p className="eyebrow mt-10">Transmission 001 / Access terminal</p>
      <h1 className="login-title mt-4 text-5xl leading-[.95] text-[#f5eee6] sm:text-7xl xl:text-8xl">ECHO<br /><span className="text-[#bd4542]">PROTOCOL</span></h1>
      <div className="mt-8 h-px w-32 bg-[#9f2b30]" />
      <p className="mt-7 max-w-lg text-lg leading-relaxed text-[#c1aea5]">Tín hiệu đã được thiết lập. Xác thực danh tính trước khi bước vào vùng cách ly.</p>
      <p className="mt-10 font-mono text-xs tracking-[.3em] text-[#8e6d69]">THE SIGNAL IS STILL ALIVE</p>
    </div>

    <section className="panel login-card w-full rounded-sm p-7 shadow-[0_24px_90px_rgba(0,0,0,.55)] md:p-10">
      <div className="flex items-center justify-between gap-4">
        <p className="eyebrow">Secure access node</p>
        <span className="font-mono text-xs text-[#a77570]">EP-001</span>
      </div>
      <h2 className="page-title mt-5 text-3xl text-[#f5eee6]">Đăng nhập hệ thống</h2>
      <p className="muted mt-3 text-sm leading-relaxed">Truy cập Player Uplink hoặc Control Authority bằng tài khoản của bạn.</p>
      <LoginForm />
      <div className="mt-8 flex items-center gap-3 border-t border-[#4a2b2b] pt-5">
        <span className="h-2 w-2 rounded-full bg-[#b93a38] shadow-[0_0_12px_#b93a38]" />
        <span className="font-mono text-[10px] uppercase tracking-[.2em] text-[#a78c83]">Awaiting authorized operator</span>
      </div>
    </section>
  </main>;
}
