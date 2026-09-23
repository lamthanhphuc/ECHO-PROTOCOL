namespace EchoProtocol.Api.Enums;

public enum PaymentProviderEventStatus
{
    PAID,
    FAILED,
    UNKNOWN
}

public enum PaymentProviderEventOutcome
{
    RECEIVED,
    PAID,
    FULFILLED,
    FULFILLMENT_PENDING,
    UNKNOWN_ORDER,
    EVIDENCE_MISMATCH,
    LATE_TERMINAL_IGNORED,
    STATUS_IGNORED
}

public enum PaymentCheckoutStatus
{
    RESERVED,
    READY
}

public enum PaymentFulfillmentKind
{
    WALLET_CREDIT,
    INVENTORY_ITEM
}
