import { ProfileTuningForm } from "@/components/admin/profile-tuning-form";
import { PageHeader } from "@/components/ui/panel";
import { adminApi } from "@/lib/api/server-api";

export default async function ProfileTuningPage() {
  const data = await adminApi.profileTuning();
  return <>
    <PageHeader title="Hồ sơ AI và lịch sử đội" description="Xem trước ảnh hưởng của hệ số, chuẩn hóa và tiêu chí lịch sử trước khi xuất bản." />
    <ProfileTuningForm key={data.version ?? "default"} data={data} />
  </>;
}
