import { routeErrorResponse, routeSuccessResponse } from "@/lib/api/route-response";
import { playerApi } from "@/lib/api/server-api";

export async function GET() {
  try {
    return routeSuccessResponse(await playerApi.paymentCatalog(), "Đã tải danh sách gói nạp");
  } catch (error) {
    return routeErrorResponse(error);
  }
}
