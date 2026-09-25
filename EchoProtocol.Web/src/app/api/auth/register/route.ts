import { NextResponse } from "next/server";
import { backendRequest } from "@/lib/api/backend-client";
import { routeErrorResponse } from "@/lib/api/route-response";

interface RegisterRequest {
  email: string;
  username: string;
  password: string;
  confirmPassword: string;
}

export async function POST(request: Request): Promise<NextResponse> {
  try {
    const body = (await request.json()) as Partial<RegisterRequest> | null;

    const email = body?.email?.trim() ?? "";
    const username = body?.username?.trim() ?? "";
    const password = body?.password ?? "";
    const confirmPassword = body?.confirmPassword ?? "";

    if (
      !email ||
      !username ||
      email.length > 255 ||
      username.length > 100 ||
      password.length < 6 ||
      password !== confirmPassword
    ) {
      return NextResponse.json(
        {
          success: false,
          message: "Thông tin đăng ký không hợp lệ.",
          data: null,
          errorCode: "VALIDATION_ERROR",
        },
        { status: 400 },
      );
    }

    const user = await backendRequest<unknown>("/api/auth/register", {
      method: "POST",
      body: { email, username, password, confirmPassword },
    });

    return NextResponse.json(
      {
        success: true,
        message: "Đăng ký thành công",
        data: user,
        errorCode: null,
      },
      { status: 201 },
    );
  } catch (error) {
    return routeErrorResponse(error);
  }
}