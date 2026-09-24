using System.Text.Json;
using EchoProtocol.Api.Enums;

namespace EchoProtocol.Api.Services.Models;

public sealed record PaymentProviderCheckoutRequest(
    long OrderCode,
    long Amount,
    string Description,
    DateTime? ExpiresAtUtc);

public sealed record PaymentProviderCheckoutResult(
    string ProviderOrderId,
    string ProviderPaymentLinkId,
    Uri CheckoutUrl,
    string Status,
    long Amount,
    string Currency);

public sealed record NormalizedPaymentProviderEvent(
    string Provider,
    string ProviderEventId,
    string ProviderOrderId,
    decimal Amount,
    string? Currency,
    PaymentProviderEventStatus Status,
    DateTime? PaidAtUtc,
    string SemanticFingerprint);

public sealed record PaymentWebhookVerificationResult(
    bool IsValid,
    NormalizedPaymentProviderEvent? Event,
    string? ErrorCode,
    string Message)
{
    public static PaymentWebhookVerificationResult Invalid(string message, string errorCode) =>
        new(false, null, errorCode, message);

    public static PaymentWebhookVerificationResult Valid(NormalizedPaymentProviderEvent paymentEvent) =>
        new(true, paymentEvent, null, "Webhook signature verified");
}

public sealed record PaymentWebhookEnvelope(JsonElement Payload);
