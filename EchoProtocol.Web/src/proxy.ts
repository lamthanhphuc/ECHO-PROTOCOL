import { NextResponse, type NextRequest } from "next/server";
import { readJwtSession } from "@/lib/auth/jwt";

export function proxy(request: NextRequest) {
  const session = readJwtSession(request.cookies.get("echo_session")?.value ?? "");
  const path = request.nextUrl.pathname;
  const adminPath = path === "/admin" || path.startsWith("/admin/");
  if (!session) {
    const params = new URLSearchParams();
    if (request.cookies.has("echo_session")) params.set("expired", "1");
    if (adminPath) params.set("next", path + request.nextUrl.search);
    else if (path === "/wallet/payment/return" || path === "/wallet/payment/cancel") params.set("next", path);
    return NextResponse.redirect(new URL(params.size ? `/login?${params}` : "/login", request.url));
  }
  if (path.startsWith("/admin") && session.role !== "ADMIN") {
    return NextResponse.redirect(new URL("/dashboard", request.url));
  }
  const headers = new Headers(request.headers);
  headers.delete("x-echo-admin-return");
  if (adminPath) headers.set("x-echo-admin-return", encodeURIComponent(path + request.nextUrl.search));
  return NextResponse.next({ request: { headers } });
}

export const config = {
  matcher: ["/dashboard/:path*", "/wallet/:path*", "/profile/:path*", "/admin/:path*"],
};
