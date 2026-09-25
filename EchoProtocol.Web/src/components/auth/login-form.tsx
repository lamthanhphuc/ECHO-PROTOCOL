"use client";

import Link from "next/link";
import { useState, type FormEvent } from "react";
import { useRouter } from "next/navigation";
import type { ApiResponse, UserRole } from "@/lib/types/common";

interface LoginResult { user: { role: UserRole }; expiresAt: string; }

export function LoginForm() {
  const router = useRouter();
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); setLoading(true); setError("");
    const values = new FormData(event.currentTarget);
    try {
      const response = await fetch("/api/auth/login", {
        method: "POST", headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ username: values.get("username"), password: values.get("password") }),
      });
      const payload = await response.json() as ApiResponse<LoginResult>;
      if (!response.ok || !payload.success || !payload.data) throw new Error(payload.message);
      const next = new URLSearchParams(window.location.search).get("next");
      const paymentReturnPath =
        next === "/wallet/payment/return" || next === "/wallet/payment/cancel"
          ? next
          : null;
      router.replace(payload.data.user.role === "ADMIN" ? "/admin" : paymentReturnPath ?? "/dashboard");
      router.refresh();
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "Đăng nhập thất bại.");
    } finally { setLoading(false); }
  }
  return <form onSubmit={submit} className="mt-7 grid gap-4">
    <label className="grid gap-2 text-sm"><span>Tài khoản</span>
      <input className="input" name="username" autoComplete="username" required placeholder="Nhập tài khoản" /></label>
    <label className="grid gap-2 text-sm"><span>Mật khẩu</span>
      <input className="input" name="password" type="password" autoComplete="current-password" required /></label>
    {error && <p role="alert" className="rounded border border-rose-800 bg-rose-950/30 p-3 text-sm text-rose-300">{error}</p>}
    <button className="button mt-2" type="submit" disabled={loading}>{loading ? "Đang đăng nhập..." : "Đăng nhập"}</button>
    <Link className="button button-secondary text-center" href="/register">Đăng ký tài khoản</Link>
  </form>;
}
