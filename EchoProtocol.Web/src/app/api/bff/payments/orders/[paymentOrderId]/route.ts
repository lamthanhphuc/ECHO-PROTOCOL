import { routeErrorResponse, routeSuccessResponse } from "@/lib/api/route-response";
import { playerApi } from "@/lib/api/server-api";

export async function GET(
  _request: Request,
  context: { params: Promise<{ paymentOrderId: string }> },
) {
  try {
    const { paymentOrderId } = await context.params;
    return routeSuccessResponse(
      await playerApi.paymentOrder(paymentOrderId),
      "Payment order retrieved",
    );
  } catch (error) {
    return routeErrorResponse(error);
  }
}
