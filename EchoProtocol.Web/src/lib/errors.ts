export class BackendApiError extends Error {
  constructor(
    message: string,
    public readonly status: number,
    public readonly errorCode: string,
  ) {
    super(message);
    this.name = "BackendApiError";
  }
}

export function safeErrorMessage(error: unknown): string {
  if (error instanceof BackendApiError) return error.message;
  return "Không thể kết nối hệ thống. Vui lòng thử lại.";
}
