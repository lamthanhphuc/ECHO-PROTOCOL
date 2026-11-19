import { redirect } from "next/navigation";
import Image from "next/image";
import { getSession } from "@/lib/auth/session";
import { LoginForm } from "@/components/auth/login-form";

export const metadata = { title: "Đăng nhập" };

export default async function LoginPage({ searchParams }: { searchParams: Promise<{ expired?: string }> }) {
  const { expired } = await searchParams;
  const session = await getSession();
  if (session && expired !== "1") redirect(session.role === "ADMIN" ? "/admin" : "/dashboard");

  return (
    <main className="login-stage mx-auto grid min-h-screen w-full max-w-[1480px] items-center gap-10 px-6 pb-10 pt-20 lg:grid-cols-[1fr_460px] lg:gap-20 lg:px-16">
      <div className="w-full max-w-2xl justify-self-center text-center">
        <div className="flex justify-center">
          <h1 className="w-full max-w-[580px]">
            <Image
              src="/echo-title.png?v=2"
              alt="ECHO PROTOCOL"
              width={2048}
              height={754}
              className="block h-auto w-full"
              priority
            />
          </h1>
        </div>
        <div className="mx-auto mt-8 h-px w-32 bg-[#9f2b30]" />
      </div>

      <section className="panel login-card w-full rounded-sm p-7 shadow-[0_24px_90px_rgba(0,0,0,.55)] md:p-10">
        <h2 className="page-title text-3xl text-[#f5eee6]">Đăng nhập</h2>
        {expired === "1" && (
          <p className="muted mt-3 text-sm leading-relaxed">
            Phiên đăng nhập đã hết hạn. Đăng nhập lại để tiếp tục.
          </p>
        )}
        <LoginForm />
      </section>
    </main>
  );
}
