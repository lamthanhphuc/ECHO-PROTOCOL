import "server-only";
import type { ApiResponse } from "@/lib/types/common";
import { BackendApiError } from "@/lib/errors";

interface BackendRequestOptions extends Omit<RequestInit, "body"> {
  token?: string | null;
  body?: unknown;
  timeoutMs?: number;
}

function backendBaseUrl(): string {
  const value = process.env.BACKEND_API_URL?.trim().replace(/\/$/, "");
  if (!value) throw new BackendApiError("BACKEND_API_URL chưa được cấu hình.", 503, "WEB_CONFIGURATION_MISSING");
  return value;
}

export async function backendRequest<T>(
  path: string,
  options: BackendRequestOptions = {},
): Promise<T> {
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), options.timeoutMs ?? 10_000);
  try {
    const headers = new Headers(options.headers);
    headers.set("Accept", "application/json");
    if (options.body !== undefined) headers.set("Content-Type", "application/json");
    if (options.token) headers.set("Authorization", `Bearer ${options.token}`);
    const response = await fetch(`${backendBaseUrl()}${path}`, {
      ...options,
      body: options.body === undefined ? undefined : JSON.stringify(options.body),
      headers,
      signal: controller.signal,
      cache: "no-store",
    });
    const payload = (await response.json().catch(() => null)) as ApiResponse<T> | null;
    if (!response.ok || !payload?.success || payload.data === null) {
      throw new BackendApiError(
        payload?.message || `Backend request failed (${response.status})`,
        response.status,
        payload?.errorCode || "BACKEND_REQUEST_FAILED",
      );
    }
    return payload.data;
  } catch (error) {
    if (error instanceof BackendApiError) throw error;
    if (error instanceof Error && error.name === "AbortError") {
      throw new BackendApiError("Backend không phản hồi đúng thời hạn.", 504, "BACKEND_TIMEOUT");
    }
    throw new BackendApiError("Không thể kết nối Backend.", 503, "BACKEND_UNAVAILABLE");
  } finally {
    clearTimeout(timeout);
  }
}
