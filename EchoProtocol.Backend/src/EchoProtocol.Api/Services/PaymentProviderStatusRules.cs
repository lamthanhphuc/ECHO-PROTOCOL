using EchoProtocol.Api.Enums;

namespace EchoProtocol.Api.Services;

public static class PaymentProviderStatusRules
{
    public static PaymentOrderStatus? TerminalOrderStatus(string? status) =>
        status?.Trim().ToUpperInvariant() switch
        {
            "FAILED" => PaymentOrderStatus.FAILED,
            "CANCELLED" or "CANCELED" => PaymentOrderStatus.CANCELLED,
            "EXPIRED" => PaymentOrderStatus.EXPIRED,
            _ => null
        };

    public static PaymentOrderStatus? TerminalOrderStatus(PaymentProviderEventStatus status) =>
        status switch
        {
            PaymentProviderEventStatus.FAILED => PaymentOrderStatus.FAILED,
            PaymentProviderEventStatus.CANCELLED => PaymentOrderStatus.CANCELLED,
            PaymentProviderEventStatus.EXPIRED => PaymentOrderStatus.EXPIRED,
            _ => null
        };
}
