namespace EchoProtocol.Api.Services;

public sealed class PaymentProviderException(string message, Exception? innerException = null)
    : Exception(message, innerException);
