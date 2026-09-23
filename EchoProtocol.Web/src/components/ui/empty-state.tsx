import type { ReactNode } from "react";
import { Panel } from "./panel";

export function EmptyState({ title, children }: { title: string; children?: ReactNode }) {
  return <Panel className="text-center">
    <p className="text-base font-semibold text-white">{title}</p>
    {children && <div className="muted mt-2 text-sm">{children}</div>}
  </Panel>;
}
