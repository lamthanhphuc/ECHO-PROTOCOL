"use client";

import Link from "next/link";
import { Panel } from "@/components/ui/panel";

export default function AdminError({ reset }: { reset: () => void }) {
  return <Panel>
    <h1 className="admin-section-title">Không tải được trang</h1>
    <p className="muted my-3 text-sm">Dữ liệu quản trị tạm thời chưa tải được.</p>
    <div className="flex flex-wrap gap-2">
      <button className="button" type="button" onClick={reset}>Thử lại</button>
      <Link className="button button-secondary" href="/admin">Về Tổng quan</Link>
    </div>
  </Panel>;
}
