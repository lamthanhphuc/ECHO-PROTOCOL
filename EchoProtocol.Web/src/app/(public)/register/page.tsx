import { redirect } from "next/navigation";
import { getSession } from "@/lib/auth/session";
import { RegisterForm } from "@/components/auth/register-form";

export const metadata = { title: "Đăng ký" };

export default async function RegisterPage() {
  const session = await getSession();
  if (session) redirect(session.role === "ADMIN" ? "/admin" : "/dashboard");

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
          Tạo tài khoản để bắt đầu.
        </p>
      </div>

      <section className="panel login-card w-full rounded-sm p-7 shadow-[0_24px_90px_rgba(0,0,0,.55)] md:p-10">
        <h2 className="page-title text-3xl text-[#f5eee6]">Đăng ký</h2>
        <p className="muted mt-3 text-sm leading-relaxed">
          Điền thông tin để tạo tài khoản mới.
        </p>
        <RegisterForm />
      </section>
    </main>
  );
}