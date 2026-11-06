import { redirect } from "next/navigation";
import { getSession } from "@/lib/auth/session";
import { LoginForm } from "@/components/auth/login-form";

export const metadata = { title: "Đăng nhập" };

export default async function LoginPage({ searchParams }: { searchParams: Promise<{ expired?: string }> }) {
  const { expired } = await searchParams;
  const session = await getSession();
  if (session && expired !== "1") redirect(session.role === "ADMIN" ? "/admin" : "/dashboard");

  return (
    <main className="login-stage mx-auto grid min-h-screen w-full max-w-[1480px] items-center gap-10 px-6 pb-10 pt-20 lg:grid-cols-[1fr_460px] lg:gap-20 lg:px-16">
      <div className="max-w-2xl">
        <div className="login-emblem" aria-hidden="true">E</div>
        <h1 className="login-title mt-10 text-5xl leading-[.95] text-[#f5eee6] sm:text-7xl xl:text-8xl">
          ECHO<br />
          <span className="text-[#bd4542]">PROTOCOL</span>
        </h1>
        <div className="mt-8 h-px w-32 bg-[#9f2b30]" />
        <p className="mt-7 max-w-lg text-lg leading-relaxed text-[#c1aea5]">
          Đăng nhập để vào hệ thống.
        </p>
      </div>

      <section className="panel login-card w-full rounded-sm p-7 shadow-[0_24px_90px_rgba(0,0,0,.55)] md:p-10">
        <h2 className="page-title text-3xl text-[#f5eee6]">Đăng nhập</h2>
        <p className="muted mt-3 text-sm leading-relaxed">
          Nhập tài khoản và mật khẩu để tiếp tục.
        </p>
        <LoginForm />
      </section>
    </main>
  );
}
