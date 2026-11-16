"use client";

import { useRouter } from "next/navigation";
import { useTransition } from "react";

export function DashboardRefresh() {
  const router = useRouter();
  const [pending, startTransition] = useTransition();
  return <button type="button" className="button button-secondary" disabled={pending}
    onClick={() => startTransition(() => router.refresh())}>
    {pending ? "Đang cập nhật…" : "Cập nhật số liệu"}
  </button>;
}
