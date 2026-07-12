namespace EchoProtocol.Api
{
    public enum ApiFailureKind
    {
        None,
        Business,
        Network,
        Timeout,
        Parse
    }

    public class ApiResult<T>
    {
        public bool IsSuccess { get; set; }
        public long StatusCode { get; set; }
        public string Message { get; set; } = string.Empty;
        public string ErrorCode { get; set; } = string.Empty;
        public T Data { get; set; }
        public ApiFailureKind FailureKind { get; set; }

        public override string ToString()
        {
            return $"IsSuccess={IsSuccess}, StatusCode={StatusCode}, FailureKind={FailureKind}, ErrorCode={ErrorCode}, Message={Message}";
        }
    }
}
