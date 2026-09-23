import Link from "next/link";
import type { ReactNode } from "react";
import type { SessionUser } from "@/lib/types/auth";
import { LogoutButton } from "@/components/auth/logout-button";

interface NavItem { href: string; label: string; }

export function PortalShell({ session, items, children, mode }: {
  session: SessionUser; items: NavItem[]; children: ReactNode; mode: "PLAYER" | "ADMIN";
}) {
  return <div className="min-h-screen md:grid md:grid-cols-[250px_1fr]">
    <aside className="panel desktop-nav sticky top-0 h-screen border-y-0 border-l-0 p-5">
      <Brand mode={mode} />
      <nav className="mt-8 grid gap-2" aria-label={`${mode} navigation`}>
        {items.map((item) => <Link key={item.href} href={item.href}
          className="rounded border border-transparent px-3 py-2 text-sm text-slate-300 hover:border-emerald-900 hover:bg-emerald-950/20 hover:text-white">
          {item.label}
        </Link>)}
      </nav>
      <div className="absolute bottom-5 left-5 right-5"><LogoutButton /></div>
    </aside>
    <div>
      <header className="panel sticky top-0 z-20 flex min-h-16 items-center justify-between rounded-none border-x-0 border-t-0 px-4 md:px-7">
        <div className="md:hidden"><Brand mode={mode} compact /></div>
        <div className="ml-auto text-right">
          <p className="text-sm text-white">{session.username}</p>
          <p className="eyebrow">{session.role}</p>
        </div>
      </header>
      <div className="border-b border-[#1d3942] p-3 md:hidden">
        <nav className="flex gap-2 overflow-x-auto">
          {items.map((item) => <Link className="button button-secondary shrink-0 text-xs" key={item.href} href={item.href}>{item.label}</Link>)}
          <LogoutButton />
        </nav>
      </div>
      <main className="mx-auto w-full max-w-[1500px] p-4 md:p-7">{children}</main>
    </div>
  </div>;
}

function Brand({ mode, compact = false }: { mode: string; compact?: boolean }) {
  return <div>
    <p className="text-sm font-bold tracking-[.22em] text-white">ECHO PROTOCOL</p>
    {!compact && <p className="eyebrow mt-1">{mode === "ADMIN" ? "Control Authority" : "Player Uplink"}</p>}
  </div>;
}
