import { NextResponse } from "next/server";
import { routeErrorResponse, routeSuccessResponse } from "@/lib/api/route-response";
import { playerApi } from "@/lib/api/server-api";

export async function POST(request: Request, context: { params: Promise<{ action: string }> }) {
  const origin = request.headers.get("origin");
  const expected = process.env.WEB_PUBLIC_ORIGIN ?? new URL(request.url).origin;
  if (origin !== expected) return NextResponse.json({ success: false, message: "Origin không hợp lệ" }, { status: 403 });
  try {
    const { action } = await context.params;
    if (action === "reconcile") return routeSuccessResponse(await playerApi.reconcileCosmetics());
    if (action === "equip") return routeSuccessResponse(await playerApi.equipCosmetic(await request.json()));
    return NextResponse.json({ success: false }, { status: 404 });
  } catch (error) { return routeErrorResponse(error); }
}
