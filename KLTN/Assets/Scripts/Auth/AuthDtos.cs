using System;

namespace EchoProtocol.Auth
{
  [Serializable]
  public class RegisterRequestDto
  {
    public string email;
    public string username;
    public string password;
    public string confirmPassword;
  }

  [Serializable]
  public class LoginRequestDto
  {
    public string username;
    public string password;
  }

  [Serializable]
  public class ErrorApiResponse
  {
    public bool success;
    public string message;
    public string errorCode;
  }

  [Serializable]
  public class RegisterApiResponse
  {
    public bool success;
    public string message;
    public UserSummaryDto data;
    public string errorCode;
  }

  [Serializable]
  public class LoginApiResponse
  {
    public bool success;
    public string message;
    public LoginDataDto data;
    public string errorCode;
  }

  [Serializable]
  public class MeApiResponse
  {
    public bool success;
    public string message;
    public MeDto data;
    public string errorCode;
  }

  [Serializable]
  public class UserSummaryDto
  {
    public string id;
    public string email;
    public string username;
    public string role;
  }

  [Serializable]
  public class WalletSummaryDto
  {
    public int balance;
  }

  [Serializable]
  public class LoginDataDto
  {
    public string accessToken;
    public string expiresAt;
    public UserSummaryDto user;
    public WalletSummaryDto wallet;
  }

  [Serializable]
  public class MeDto
  {
    public string id;
    public string email;
    public string username;
    public string role;
    public string displayName;
    public int walletBalance;
  }
}
