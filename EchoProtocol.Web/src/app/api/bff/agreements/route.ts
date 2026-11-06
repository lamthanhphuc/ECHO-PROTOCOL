import { backendRequest } from "@/lib/api/backend-client";
import { getAccessToken, requireSession } from "@/lib/auth/session";
import { routeErrorResponse, routeSuccessResponse } from "@/lib/api/route-response";

export async function GET() {
  try { await requireSession(); return routeSuccessResponse(await backendRequest("/api/agreements/status", { token: await getAccessToken() })); }
  catch (error) { return routeErrorResponse(error); }
}
export async function POST(request: Request) {
  if (request.headers.get("origin") !== new URL(request.url).origin)
    return Response.json({ success: false, message: "Yêu cầu không hợp lệ." }, { status: 403 });
  try {
    await requireSession();
    return routeSuccessResponse(await backendRequest("/api/agreements/accept", { method: "POST", token: await getAccessToken(), body: await request.json() }));
  } catch (error) { return routeErrorResponse(error); }
}
