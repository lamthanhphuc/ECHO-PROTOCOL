import { ChatWordsForm } from "@/components/admin/chat-words-form";
import { PageHeader, Panel } from "@/components/ui/panel";
import { adminApi } from "@/lib/api/server-api";

export default async function ChatWordsPage() {
  const data = await adminApi.chatWords();
  return <>
    <PageHeader title="Từ cấm chat" description="Chỉnh toàn bộ danh sách từ và cụm từ bị chặn trong lobby và trận đấu." />
    <Panel><ChatWordsForm initialWords={data.words} updatedAtUtc={data.updatedAtUtc} /></Panel>
  </>;
}
