import { NextResponse, type NextRequest } from "next/server";
import { readJwtSession } from "@/lib/auth/jwt";

export function proxy(request: NextRequest) {
  const session = readJwtSession(request.cookies.get("echo_session")?.value ?? "");
  const path = request.nextUrl.pathname;
  if (!session) return NextResponse.redirect(new URL("/login", request.url));
  if (path.startsWith("/admin") && session.role !== "ADMIN") {
    return NextResponse.redirect(new URL("/dashboard", request.url));
  }
  return NextResponse.next();
}

export const config = {
  matcher: ["/dashboard/:path*", "/wallet/:path*", "/profile/:path*", "/admin/:path*"],
};
