import { ChatWordsForm } from "@/components/admin/chat-words-form";
import { PageHeader, Panel } from "@/components/ui/panel";
import { adminApi } from "@/lib/api/server-api";

export default async function ChatWordsPage() {
  const data = await adminApi.chatWords();
  return <>
    <PageHeader title="Từ cấm chat" />
    <Panel><ChatWordsForm initialWords={data.words} updatedAtUtc={data.updatedAtUtc} /></Panel>
  </>;
}
