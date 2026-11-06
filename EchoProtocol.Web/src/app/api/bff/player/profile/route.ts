import { routeErrorResponse, routeSuccessResponse } from "@/lib/api/route-response";
import { playerApi } from "@/lib/api/server-api";

export async function GET() {
  try { return routeSuccessResponse(await playerApi.profile(), "Đã tải hồ sơ"); }
  catch (error) { return routeErrorResponse(error); }
}
