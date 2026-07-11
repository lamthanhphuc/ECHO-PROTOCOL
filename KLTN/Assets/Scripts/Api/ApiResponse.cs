using System;

namespace EchoProtocol.Api
{
    [Serializable]
    public class ApiResponse<T>
    {
        public bool success;
        public string message;
        public T data;
        public string errorCode;
    }

    [Serializable]
    public class ApiResponse
    {
        public bool success;
        public string message;
        public string errorCode;
    }
}
