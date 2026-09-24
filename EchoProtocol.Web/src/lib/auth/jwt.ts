import type { SessionUser } from "@/lib/types/auth";
import type { UserRole } from "@/lib/types/common";

interface JwtPayload {
  sub?: string;
  nameid?: string;
  unique_name?: string;
  role?: UserRole;
  exp?: number;
  [key: string]: unknown;
}

const nameIdClaim = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier";
const nameClaim = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name";
const roleClaim = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role";

export function readJwtSession(token: string): SessionUser | null {
  try {
    const part = token.split(".")[1];
    if (!part) return null;
    const base64 = part.replace(/-/g, "+").replace(/_/g, "/");
    const json = decodeURIComponent(
      Array.from(atob(base64.padEnd(Math.ceil(base64.length / 4) * 4, "=")))
        .map((char) => `%${char.charCodeAt(0).toString(16).padStart(2, "0")}`)
        .join(""),
    );
    const payload = JSON.parse(json) as JwtPayload;
    const userId = String(payload.sub ?? payload[nameIdClaim] ?? "");
    const username = String(payload.unique_name ?? payload[nameClaim] ?? "");
    const role = (payload.role ?? payload[roleClaim]) as UserRole | undefined;
    const expiresAt = Number(payload.exp ?? 0);
    if (!userId || !username || !role || !expiresAt || expiresAt * 1000 <= Date.now()) return null;
    if (role !== "PLAYER" && role !== "ADMIN") return null;
    return { userId, username, role, expiresAt };
  } catch {
    return null;
  }
}
