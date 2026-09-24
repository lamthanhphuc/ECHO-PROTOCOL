namespace EchoProtocol.Api.Enums;

public enum PaymentOrderStatus
{
    CREATED,
    PENDING_PAYMENT,
    PAID,
    FULFILLED,
    FAILED,
    CANCELLED,
    EXPIRED
}
