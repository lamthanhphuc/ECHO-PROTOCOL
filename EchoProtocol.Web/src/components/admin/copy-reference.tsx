"use client";

import { useState } from "react";

export function CopyReference({ value, label }: { value: string; label: string }) {
  const [message, setMessage] = useState("");

  async function copy() {
    try {
      await navigator.clipboard.writeText(value);
      setMessage("Đã sao chép");
    } catch {
      setMessage("Hãy chọn và sao chép mã.");
    }
  }

  return <details className="text-sm">
    <summary>{label}</summary>
    <div className="mt-2 flex flex-wrap items-center gap-2">
      <code className="code">{value}</code>
      <button type="button" className="button button-secondary" onClick={copy}>Sao chép</button>
      <span className="muted" role="status">{message}</span>
    </div>
  </details>;
}
