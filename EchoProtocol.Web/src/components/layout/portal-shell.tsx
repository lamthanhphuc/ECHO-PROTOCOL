"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { statusLabel } from "@/components/ui/status-badge";
import type { ReactNode } from "react";
import type { SessionUser } from "@/lib/types/auth";
import { LogoutButton } from "@/components/auth/logout-button";

interface NavItem { href: string; label: string; }

export function PortalShell({ session, items, children, mode }: {
  session: SessionUser; items: NavItem[]; children: ReactNode; mode: "PLAYER" | "ADMIN";
}) {
  const pathname = usePathname();
  const activeHref = items.filter((item) => pathname === item.href || pathname.startsWith(`${item.href}/`)).at(-1)?.href;
  return <div className={`${mode === "ADMIN" ? "admin-shell" : ""} min-h-screen md:grid md:grid-cols-[250px_1fr]`}>
    <aside className="panel desktop-nav sticky top-0 h-screen border-y-0 border-l-0 p-5 pt-9">
      <Brand mode={mode} />
      <nav className="mt-8 grid gap-1" aria-label={mode === "ADMIN" ? "Điều hướng quản trị" : "Điều hướng người chơi"}>
        {items.map((item) => <Link key={item.href} href={item.href}
          aria-current={activeHref === item.href ? "page" : undefined}
          className="portal-nav-link">
          {item.label}
        </Link>)}
      </nav>
      <div className="absolute bottom-5 left-5 right-5"><LogoutButton /></div>
    </aside>
    <div>
      <header className="panel sticky top-0 z-20 flex min-h-20 items-center justify-between rounded-none border-x-0 border-t-0 px-4 pt-4 md:px-7">
        <div className="md:hidden"><Brand mode={mode} compact /></div>
        <div className="ml-auto text-right">
          <p className="text-sm text-white">{session.username}</p>
          <p className="text-xs text-[var(--muted)]">{statusLabel(session.role)}</p>
        </div>
      </header>
      <div className="border-b border-[#4a2b2b] p-3 md:hidden">
        <nav className="flex gap-2 overflow-x-auto" aria-label={mode === "ADMIN" ? "Điều hướng quản trị" : "Điều hướng người chơi"}>
          {items.map((item) => <Link className="button button-secondary shrink-0 text-xs" aria-current={activeHref === item.href ? "page" : undefined} key={item.href} href={item.href}>{item.label}</Link>)}
          <LogoutButton />
        </nav>
      </div>
      <main className="mx-auto w-full max-w-[1500px] p-4 pt-8 md:p-7 md:pt-10">{children}</main>
    </div>
  </div>;
}

function Brand({ mode, compact = false }: { mode: string; compact?: boolean }) {
  return <div>
    <p className="brand-mark text-sm">ECHO PROTOCOL</p>
    {!compact && <p className="muted mt-1 text-xs">{mode === "ADMIN" ? "Khu quản trị" : "Khu người chơi"}</p>}
  </div>;
}
