namespace EchoProtocol.Api.Common;

public sealed class ServiceResult<T>
{
    public bool IsSuccess { get; init; }
    public T? Data { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? ErrorCode { get; init; }

    public static ServiceResult<T> Success(T data, string message = "Success") =>
        new() { IsSuccess = true, Data = data, Message = message };

    public static ServiceResult<T> Failure(string message, string errorCode) =>
        new() { IsSuccess = false, Message = message, ErrorCode = errorCode };
}
