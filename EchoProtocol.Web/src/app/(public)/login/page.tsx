import { redirect } from "next/navigation";
import { getSession } from "@/lib/auth/session";
import { LoginForm } from "@/components/auth/login-form";

export const metadata = { title: "Đăng nhập" };

export default async function LoginPage() {
  const session = await getSession();
  if (session) redirect(session.role === "ADMIN" ? "/admin" : "/dashboard");
  return <main className="grid min-h-screen place-items-center p-5">
    <section className="panel w-full max-w-md rounded-lg p-7 md:p-9">
      <p className="eyebrow">Secure access node</p>
      <h1 className="mt-3 text-3xl font-bold tracking-[.16em] text-white">ECHO<br />PROTOCOL</h1>
      <p className="muted mt-4 text-sm">Xác thực danh tính để truy cập Player Uplink hoặc Control Authority.</p>
      <LoginForm />
    </section>
  </main>;
}
