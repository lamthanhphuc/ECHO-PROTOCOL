import { NextResponse } from "next/server";
import { backendRequest } from "@/lib/api/backend-client";
import { routeErrorResponse } from "@/lib/api/route-response";
import { sessionCookieName } from "@/lib/auth/session";
import type { AuthResponse, LoginRequest } from "@/lib/types/auth";

export async function POST(request: Request): Promise<NextResponse> {
  try {
    const body = (await request.json()) as Partial<LoginRequest>;
    if (!body.username?.trim() || !body.password) {
      return NextResponse.json(
        { success: false, message: "Vui lòng nhập tài khoản và mật khẩu.", data: null, errorCode: "VALIDATION_ERROR" },
        { status: 400 },
      );
    }
    const auth = await backendRequest<AuthResponse>("/api/auth/login", {
      method: "POST",
      body: { username: body.username.trim(), password: body.password },
    });
    const response = NextResponse.json({
      success: true,
      message: "Đăng nhập thành công",
      data: { user: auth.user, expiresAt: auth.expiresAt },
      errorCode: null,
    });
    response.cookies.set(sessionCookieName, auth.accessToken, {
      httpOnly: true,
      secure: process.env.NODE_ENV === "production",
      sameSite: "lax",
      path: "/",
      expires: new Date(auth.expiresAt),
    });
    return response;
  } catch (error) {
    return routeErrorResponse(error);
  }
}
