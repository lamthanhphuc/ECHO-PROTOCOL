import type { ReactNode } from "react";

export function Panel({ children, className = "" }: { children: ReactNode; className?: string }) {
  return <section className={`panel rounded-lg p-5 ${className}`}>{children}</section>;
}

export function PageHeader({ eyebrow, title, description }: {
  eyebrow: string; title: string; description?: string;
}) {
  return <header className="mb-6">
    <p className="eyebrow mb-2">{eyebrow}</p>
    <h1 className="text-2xl font-semibold tracking-wide text-white md:text-3xl">{title}</h1>
    {description && <p className="muted mt-2 max-w-3xl text-sm">{description}</p>}
  </header>;
}

export function StatCard({ label, value, detail }: { label: string; value: ReactNode; detail?: string }) {
  return <Panel>
    <p className="eyebrow">{label}</p>
    <p className="mt-3 text-2xl font-semibold text-white">{value}</p>
    {detail && <p className="muted mt-2 text-xs">{detail}</p>}
  </Panel>;
}
