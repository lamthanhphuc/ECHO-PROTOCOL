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
  if (!value) throw new BackendApiError("Chưa cấu hình địa chỉ máy chủ.", 503, "WEB_CONFIGURATION_MISSING");
  return value;
}

const errorMessages: Record<string, string> = {
  INVALID_CREDENTIALS: "Tài khoản hoặc mật khẩu không đúng.",
  ACCOUNT_LOCKED: "Tài khoản đã bị khóa.",
  EMAIL_ALREADY_EXISTS: "Email đã được sử dụng.",
  USERNAME_ALREADY_EXISTS: "Tên tài khoản đã được sử dụng.",
  INSUFFICIENT_WALLET_BALANCE: "Số dư không đủ.",
  BALANCE_VERSION_UNAVAILABLE: "Chưa có cấu hình cân bằng đang hoạt động.",
  BALANCE_SNAPSHOT_INVALID: "Dữ liệu cấu hình cân bằng không hợp lệ.",
};

function errorMessage(code: string | null | undefined, status: number): string {
  if (code && errorMessages[code]) return errorMessages[code];
  if (status === 400) return "Thông tin gửi lên không hợp lệ. Vui lòng kiểm tra lại.";
  if (status === 401) return "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.";
  if (status === 403) return "Bạn không có quyền thực hiện thao tác này.";
  if (status === 404) return "Không tìm thấy dữ liệu yêu cầu. Vui lòng kiểm tra máy chủ đã được cập nhật.";
  if (status === 409) return "Dữ liệu đã thay đổi. Hãy tải lại trang rồi thử lại.";
  return "Máy chủ không thể xử lý yêu cầu. Vui lòng thử lại sau.";
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
      const code = payload?.errorCode;

      throw new BackendApiError(
        (code && errorMessages[code]) ||
          (response.status < 500 ? payload?.message : null) ||
          errorMessage(code, response.status),
        response.status,
        code || "BACKEND_REQUEST_FAILED",
      );
    }
    return payload.data;
  } catch (error) {
    if (error instanceof BackendApiError) throw error;
    if (error instanceof Error && error.name === "AbortError") {
      throw new BackendApiError("Máy chủ không phản hồi đúng thời hạn.", 504, "BACKEND_TIMEOUT");
    }
    throw new BackendApiError("Không thể kết nối máy chủ.", 503, "BACKEND_UNAVAILABLE");
  } finally {
    clearTimeout(timeout);
  }
}
