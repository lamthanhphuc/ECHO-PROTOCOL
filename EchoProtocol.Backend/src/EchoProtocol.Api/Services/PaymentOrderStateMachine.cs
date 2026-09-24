using EchoProtocol.Api.Enums;

namespace EchoProtocol.Api.Services;

public static class PaymentOrderStateMachine
{
    public static bool CanTransition(PaymentOrderStatus current, PaymentOrderStatus target) =>
        (current, target) switch
        {
            (PaymentOrderStatus.CREATED, PaymentOrderStatus.PENDING_PAYMENT) => true,
            (PaymentOrderStatus.PENDING_PAYMENT, PaymentOrderStatus.PAID) => true,
            (PaymentOrderStatus.PENDING_PAYMENT, PaymentOrderStatus.FAILED) => true,
            (PaymentOrderStatus.PENDING_PAYMENT, PaymentOrderStatus.CANCELLED) => true,
            (PaymentOrderStatus.PENDING_PAYMENT, PaymentOrderStatus.EXPIRED) => true,
            (PaymentOrderStatus.PAID, PaymentOrderStatus.FULFILLED) => true,
            _ => false
        };
}
