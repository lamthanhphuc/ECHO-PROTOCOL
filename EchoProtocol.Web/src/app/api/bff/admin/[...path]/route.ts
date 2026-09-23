import { NextResponse } from "next/server";
import { backendRequest } from "@/lib/api/backend-client";
import { routeErrorResponse } from "@/lib/api/route-response";
import { getAccessToken, getSession } from "@/lib/auth/session";

export async function GET(request: Request, context: { params: Promise<{ path: string[] }> }): Promise<NextResponse> {
  try {
    const session = await getSession();
    const token = await getAccessToken();
    if (!session || session.role !== "ADMIN" || !token) {
      return NextResponse.json({ success: false, message: "Không có quyền quản trị.", data: null, errorCode: "FORBIDDEN" }, { status: 403 });
    }
    const { path } = await context.params;
    const query = new URL(request.url).search;
    const data = await backendRequest<unknown>(`/api/admin/${path.map(encodeURIComponent).join("/")}${query}`, { token });
    return NextResponse.json({ success: true, message: "OK", data, errorCode: null });
  } catch (error) {
    return routeErrorResponse(error);
  }
}
