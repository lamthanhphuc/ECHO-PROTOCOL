namespace EchoProtocol.Api.Common;

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }
    public string? ErrorCode { get; set; }

    public static ApiResponse<T> Ok(T data, string message = "Success") =>
        new() { Success = true, Message = message, Data = data };

    public static ApiResponse<T> Fail(string message, string errorCode) =>
        new() { Success = false, Message = message, ErrorCode = errorCode };
}

public class ApiResponse : ApiResponse<object>
{
    public static ApiResponse OkMessage(string message = "Success") =>
        new() { Success = true, Message = message };
}
