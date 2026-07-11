using System;

namespace EchoProtocol.Auth
{
    [Serializable]
    public class RegisterRequest
    {
        public string username;
        public string email;
        public string password;
    }

    [Serializable]
    public class LoginRequest
    {
        public string usernameOrEmail;
        public string password;
    }

    [Serializable]
    public class LoginResponseData
    {
        public string accessToken;
        public string expiresAt;
        public string userId;
        public string username;
        public string role;
    }
}
