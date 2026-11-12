import { ProfileTuningForm } from "@/components/admin/profile-tuning-form";
import { PageHeader } from "@/components/ui/panel";
import { adminApi } from "@/lib/api/server-api";

export default async function ProfileTuningPage() {
  const data = await adminApi.profileTuning();
  return <>
    <PageHeader title="Cách tính hồ sơ AI" description="Kiểm tra cách một trận mẫu cập nhật điểm người chơi và lịch sử của đội." />
    <ProfileTuningForm key={data.version ?? "default"} data={data} />
  </>;
}
