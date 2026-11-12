import { NextResponse } from "next/server";
import { sessionCookieName } from "@/lib/auth/session";

export async function POST(): Promise<NextResponse> {
  const response = new NextResponse(null, { status: 303, headers: { Location: "/login" } });
  response.cookies.set(sessionCookieName, "", {
    httpOnly: true,
    secure: process.env.NODE_ENV === "production",
    sameSite: "lax",
    path: "/",
    maxAge: 0,
  });
  return response;
}
