"use client";

import { useState } from "react";

type SaveResponse = {
  success: boolean;
  message?: string;
  data?: { words: string[]; updatedAtUtc: string };
};

export function ChatWordsForm({ initialWords, updatedAtUtc }: { initialWords: string[]; updatedAtUtc: string | null }) {
  const [savedWords, setSavedWords] = useState(initialWords);
  const [words, setWords] = useState(initialWords);
  const [newWord, setNewWord] = useState("");
  const [query, setQuery] = useState("");
  const [savedAt, setSavedAt] = useState(updatedAtUtc);
  const [status, setStatus] = useState("");
  const [error, setError] = useState(false);
  const [saving, setSaving] = useState(false);

  const changed = JSON.stringify(words) !== JSON.stringify(savedWords);
  const visibleWords = [...words].reverse().filter((word) => word.toLocaleLowerCase("vi").includes(query.trim().toLocaleLowerCase("vi")));

  function addWord(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const word = newWord.trim();
    if (!word || word.length > 50 || !/[\p{L}\p{N}]/u.test(word) || /[\p{C}]/u.test(word)) {
      setStatus("Nhập từ hoặc cụm từ hợp lệ, tối đa 50 ký tự.");
      setError(true);
      return;
    }
    if (words.length >= 256) {
      setStatus("Danh sách đã đạt giới hạn 256 mục.");
      setError(true);
      return;
    }
    if (words.some((item) => item.toLocaleLowerCase("vi") === word.toLocaleLowerCase("vi"))) {
      setStatus("Mục này đã có trong danh sách.");
      setError(true);
      return;
    }
    setWords([...words, word]);
    setNewWord("");
    setQuery(word);
    setStatus("");
    setError(false);
  }

  async function save() {
    setSaving(true);
    setStatus("");
    try {
      const response = await fetch("/api/bff/admin/chat-words", {
        method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ words }),
      });
      const result = await response.json() as SaveResponse;
      if (!response.ok || !result.success || !result.data) throw new Error(result.message || "Không thể lưu danh sách.");
      setWords(result.data.words);
      setSavedWords(result.data.words);
      setSavedAt(result.data.updatedAtUtc);
      setStatus("Đã lưu. Game đang chạy sẽ tải lại trong khoảng 60 giây.");
      setError(false);
    } catch (cause) {
      setStatus(cause instanceof Error ? cause.message : "Không thể lưu danh sách.");
      setError(true);
    } finally {
      setSaving(false);
    }
  }

  return <div className="max-w-4xl space-y-6">
    <div className="flex flex-wrap items-center justify-between gap-3 border-b border-zinc-700 pb-4">
      <div>
        <h2 className="text-lg font-semibold text-white">Danh sách đang chặn</h2>
        <p className="mt-1 text-sm text-zinc-300">{words.length}/256 mục · áp dụng cho lobby và trận đấu</p>
      </div>
      <span className={`rounded px-3 py-1 text-sm font-medium ${changed ? "bg-amber-900/50 text-amber-200" : "bg-emerald-900/40 text-emerald-200"}`}>
        {changed ? "Có thay đổi chưa lưu" : savedAt ? "Đã lưu" : "Danh sách mặc định"}
      </span>
    </div>

    <form onSubmit={addWord} className="space-y-2">
      <label htmlFor="new-chat-word" className="block text-sm font-medium text-white">Thêm từ hoặc cụm từ</label>
      <div className="flex flex-col gap-2 sm:flex-row">
        <input id="new-chat-word" value={newWord} onChange={(event) => setNewWord(event.target.value)}
          maxLength={50} disabled={saving} placeholder="Ví dụ: cụm từ cần chặn"
          className="min-w-0 flex-1 rounded border border-zinc-600 bg-zinc-950 px-3 py-2 text-zinc-100 placeholder:text-zinc-400 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-red-500" />
        <button type="submit" disabled={saving || !newWord.trim()} className="button button-secondary">Thêm vào danh sách</button>
      </div>
      <p className="text-xs text-zinc-300">So khớp nguyên từ hoặc cụm từ; không phân biệt chữ hoa và thường. Tối đa 50 ký tự mỗi mục.</p>
    </form>

    <div className="space-y-3">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div className="min-w-0 flex-1 sm:max-w-sm">
          <label htmlFor="search-chat-word" className="mb-1 block text-sm font-medium text-white">Tìm trong danh sách</label>
          <input id="search-chat-word" value={query} onChange={(event) => setQuery(event.target.value)}
            placeholder="Nhập từ cần tìm" className="w-full rounded border border-zinc-600 bg-zinc-950 px-3 py-2 text-zinc-100 placeholder:text-zinc-400 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-red-500" />
        </div>
        <p className="text-sm text-zinc-300">Hiển thị {visibleWords.length}/{words.length} mục</p>
      </div>
      {visibleWords.length ? <ul className="max-h-[32rem] overflow-y-auto rounded border border-zinc-700 divide-y divide-zinc-800">
        {visibleWords.map((word, index) => <li key={`${word}-${index}`} className="flex items-center justify-between gap-4 px-3 py-2 hover:bg-white/5">
          <span className="min-w-0 break-words text-sm text-zinc-100">{word}</span>
          <button type="button" disabled={saving} onClick={() => { setWords(words.filter((item) => item !== word)); setStatus(""); }}
            aria-label={`Bỏ chặn ${word}`} className="shrink-0 rounded px-2 py-1 text-sm text-red-300 hover:bg-red-950/50 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-red-500 disabled:opacity-50">
            Xóa
          </button>
        </li>)}
      </ul> : <p className="rounded border border-dashed border-zinc-700 px-4 py-6 text-sm text-zinc-300">
        {query ? "Không tìm thấy mục nào. Hãy thử từ khóa khác." : "Danh sách trống. Thêm từ hoặc cụm từ ở phía trên."}
      </p>}
    </div>

    {!words.length && <p className="text-sm text-amber-300">Lưu danh sách trống sẽ tắt bộ lọc từ cấm sau khi game tải lại cấu hình.</p>}
    <div className="flex flex-wrap items-center gap-3 border-t border-zinc-700 pt-4">
      <button type="button" onClick={save} disabled={!changed || saving} className="button button-primary">
        {saving ? "Đang lưu..." : "Lưu thay đổi"}
      </button>
      <button type="button" onClick={() => { setWords(savedWords); setStatus(""); setError(false); }} disabled={!changed || saving}
        className="button button-secondary">Hủy thay đổi</button>
      {savedAt && <span className="text-xs text-zinc-300">Lưu lần cuối (UTC): {savedAt.replace("T", " ").slice(0, 19)}</span>}
    </div>
    <p role="status" aria-live="polite" className={`text-sm ${error ? "text-red-300" : "text-emerald-200"}`}>{status}</p>
  </div>;
}
