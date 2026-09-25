"use client";

import Link from "next/link";
import { useState, type FormEvent } from "react";
import type { ApiResponse } from "@/lib/types/common";

export function RegisterForm() {
  const [loading, setLoading] = useState(false);
  const [success, setSuccess] = useState(false);
  const [error, setError] = useState("");

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (loading) return;

    setLoading(true);
    setError("");

    const values = new FormData(event.currentTarget);
    const email = String(values.get("email") ?? "").trim();
    const username = String(values.get("username") ?? "").trim();
    const password = String(values.get("password") ?? "");
    const confirmPassword = String(values.get("confirmPassword") ?? "");

    if (!email || !username || password.length < 6) {
      setError("Vui lòng nhập đầy đủ thông tin. Mật khẩu cần ít nhất 6 ký tự.");
      setLoading(false);
      return;
    }

    if (password !== confirmPassword) {
      setError("Mật khẩu xác nhận không khớp.");
      setLoading(false);
      return;
    }

    try {
      const response = await fetch("/api/auth/register", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          email,
          username,
          password,
          confirmPassword,
        }),
      });

      const payload = (await response.json()) as ApiResponse<unknown>;

      if (!response.ok || !payload.success) {
        throw new Error(payload.message || "Không thể đăng ký tài khoản.");
      }

      setSuccess(true);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "Đăng ký thất bại.");
    } finally {
      setLoading(false);
    }
  }

  if (success) {
    return (
      <div className="mt-7 grid gap-4">
        <p role="status" className="text-emerald-300">
          Đăng ký thành công. Bạn có thể đăng nhập bằng tài khoản vừa tạo.
        </p>
        <Link className="button text-center" href="/login">
          Đăng nhập
        </Link>
      </div>
    );
  }

  return (
    <form onSubmit={submit} className="mt-7 grid gap-4">
      <label className="grid gap-2 text-sm">
        <span>Email</span>
        <input
          className="input"
          name="email"
          type="email"
          autoComplete="email"
          maxLength={255}
          required
          placeholder="Nhập email"
        />
      </label>

      <label className="grid gap-2 text-sm">
        <span>Tài khoản</span>
        <input
          className="input"
          name="username"
          autoComplete="username"
          maxLength={100}
          required
          placeholder="Nhập tài khoản"
        />
      </label>

      <label className="grid gap-2 text-sm">
        <span>Mật khẩu</span>
        <input
          className="input"
          name="password"
          type="password"
          autoComplete="new-password"
          minLength={6}
          required
          placeholder="Ít nhất 6 ký tự"
        />
      </label>

      <label className="grid gap-2 text-sm">
        <span>Xác nhận mật khẩu</span>
        <input
          className="input"
          name="confirmPassword"
          type="password"
          autoComplete="new-password"
          minLength={6}
          required
          placeholder="Nhập lại mật khẩu"
        />
      </label>

      {error && (
        <p role="alert" className="rounded border border-rose-800 bg-rose-950/30 p-3 text-sm text-rose-300">
          {error}
        </p>
      )}

      <button className="button mt-2" type="submit" disabled={loading}>
        {loading ? "Đang đăng ký..." : "Đăng ký"}
      </button>

      <Link className="button button-secondary text-center" href="/login">
        Quay lại đăng nhập
      </Link>
    </form>
  );
}