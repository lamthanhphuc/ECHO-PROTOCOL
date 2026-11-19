"use client";

import { useRef } from "react";

export function TableFilterPopover({ id, label, active, children }: {
  id: string; label: string; active: boolean; children: React.ReactNode;
}) {
  const popover = useRef<HTMLDivElement>(null);
  return <>
    <button type="button" popoverTarget={id} aria-label={`Lọc ${label}`} aria-haspopup="dialog"
      className={`admin-table-filter-trigger${active ? " is-active" : ""}`}
      onClick={(event) => {
        const rect = event.currentTarget.getBoundingClientRect();
        const menu = popover.current;
        if (menu) {
          menu.style.top = `${Math.max(8, Math.min(rect.bottom + 6, window.innerHeight - 340))}px`;
          menu.style.left = `${Math.max(8, Math.min(rect.left, window.innerWidth - 258))}px`;
        }
      }}>▾</button>
    <div id={id} ref={popover} popover="auto" className="admin-table-filter-popover">
      <strong className="block mb-3 text-sm normal-case tracking-normal">Lọc {label}</strong>
      {children}
      <button className="button mt-3 w-full" form="match-table-filters" type="submit">Áp dụng</button>
    </div>
  </>;
}
