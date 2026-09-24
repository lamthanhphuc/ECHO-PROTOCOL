import "server-only";
import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { readJwtSession } from "./jwt";
import type { SessionUser } from "@/lib/types/auth";
import type { UserRole } from "@/lib/types/common";

export const sessionCookieName = "echo_session";

export async function getSession(): Promise<SessionUser | null> {
  const token = (await cookies()).get(sessionCookieName)?.value;
  return token ? readJwtSession(token) : null;
}

export async function getAccessToken(): Promise<string | null> {
  return (await cookies()).get(sessionCookieName)?.value ?? null;
}

export async function requireSession(role?: UserRole): Promise<SessionUser> {
  const session = await getSession();
  if (!session) redirect("/login");
  if (role && session.role !== role) redirect(session.role === "ADMIN" ? "/admin" : "/dashboard");
  return session;
}
