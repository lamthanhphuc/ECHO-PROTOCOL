import { NextResponse } from "next/server";
import { BackendApiError } from "@/lib/errors";

export function routeErrorResponse(error: unknown): NextResponse {
  const apiError = error instanceof BackendApiError
    ? error
    : new BackendApiError("Yêu cầu không thể hoàn tất.", 500, "WEB_INTERNAL_ERROR");
  return NextResponse.json(
    { success: false, message: apiError.message, data: null, errorCode: apiError.errorCode },
    { status: apiError.status },
  );
}

export function routeSuccessResponse<T>(data: T, message = "Success"): NextResponse {
  return NextResponse.json({ success: true, message, data, errorCode: null });
}
