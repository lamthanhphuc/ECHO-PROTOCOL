import { routeErrorResponse, routeSuccessResponse } from "@/lib/api/route-response";
import { playerApi } from "@/lib/api/server-api";
import type { CreatePaymentOrderRequest } from "@/lib/types/payment";

export async function POST(request: Request) {
  try {
    const body = (await request.json()) as CreatePaymentOrderRequest;
    return routeSuccessResponse(await playerApi.createPayment(body), "Đã tạo đơn thanh toán");
  } catch (error) { return routeErrorResponse(error); }
}
