"use client";

import Link from "next/link";
import { useState, type FormEvent } from "react";
import type { ApiResponse, UserRole } from "@/lib/types/common";

interface LoginResult { user: { role: UserRole }; expiresAt: string; }

export function LoginForm() {
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [showPassword, setShowPassword] = useState(false);
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
      const walletPath = next && /^\/wallet(?:\?product=[A-Za-z0-9_-]+)?$/.test(next) ? next : null;
      const paymentReturnPath =
        next === "/wallet/payment/return" || next === "/wallet/payment/cancel"
          ? next
          : null;
      const adminPath = next && /^\/admin(?:\/[^?#\\]*)?(?:\?[^#\\]*)?$/.test(next) ? next : "/admin";
      const destination = payload.data.user.role === "ADMIN" ? adminPath : walletPath ?? paymentReturnPath ?? "/dashboard";
      window.history.replaceState(window.history.state, "", "/");
      window.location.assign(destination);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "Đăng nhập thất bại.");
    } finally { setLoading(false); }
  }
  return <form onSubmit={submit} className="mt-7 grid gap-4">
    <label className="grid gap-2 text-sm">
      <span>Tên đăng nhập</span>
      <input
        className="input"
        name="username"
        autoComplete="username"
        required
        placeholder="Nhập tên đăng nhập"
      />
    </label>

    <div className="grid gap-2 text-sm">
      <label htmlFor="login-password">Mật khẩu</label>
      <div className="relative">
        <input
          id="login-password"
          className="input"
          name="password"
          type={showPassword ? "text" : "password"}
          autoComplete="current-password"
          placeholder="Nhập mật khẩu"
          style={{ paddingRight: "3rem" }}
          required
        />
        <button
          type="button"
          onClick={() => setShowPassword((value) => !value)}
          aria-label={showPassword ? "Ẩn mật khẩu" : "Hiện mật khẩu"}
          aria-pressed={showPassword}
          className="absolute right-3 top-1/2 -translate-y-1/2 cursor-pointer text-[#b4a29d] hover:text-white"
        >
          <svg
            xmlns="http://www.w3.org/2000/svg"
            width="20"
            height="20"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="1.8"
            strokeLinecap="round"
            strokeLinejoin="round"
            aria-hidden="true"
          >
            <path d="M2 12s3.6-7 10-7 10 7 10 7-3.6 7-10 7S2 12 2 12Z" />
            <circle cx="12" cy="12" r="3" />
            {showPassword && <path d="M4 4 20 20" />}
          </svg>
        </button>
      </div>
    </div>
    {error && <p role="alert" className="rounded border border-rose-800 bg-rose-950/30 p-3 text-sm text-rose-300">{error}</p>}
    <button className="button mt-2" type="submit" disabled={loading}>{loading ? "Đang đăng nhập..." : "Đăng nhập"}</button>
    <Link className="button button-secondary text-center" href="/register">Đăng ký tài khoản</Link>
  </form>;
}
