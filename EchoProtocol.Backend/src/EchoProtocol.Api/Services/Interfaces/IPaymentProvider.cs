using System.Text.Json;
using EchoProtocol.Api.Services.Models;

namespace EchoProtocol.Api.Services.Interfaces;

public interface IPaymentProvider
{
    string ProviderKey { get; }
    bool IsConfigured { get; }
    Task<PaymentProviderCheckoutResult> CreateCheckoutAsync(
        PaymentProviderCheckoutRequest request,
        CancellationToken cancellationToken = default);
    Task<PaymentProviderCheckoutResult?> QueryPaymentAsync(
        string providerOrderId,
        CancellationToken cancellationToken = default);
    PaymentWebhookVerificationResult VerifyAndParseWebhook(JsonElement payload);
}

public interface IPaymentProviderRegistry
{
    IPaymentProvider? Resolve(string providerKey);
}
