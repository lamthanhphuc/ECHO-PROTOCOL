using EchoProtocol.Api.Common;
using EchoProtocol.Api.DTOs.Payments;
using EchoProtocol.Api.Services.Models;

namespace EchoProtocol.Api.Services.Interfaces;

public interface IPaymentWebhookService
{
    Task<ServiceResult<PaymentWebhookResponse>> ProcessVerifiedAsync(
        NormalizedPaymentProviderEvent paymentEvent,
        CancellationToken cancellationToken = default);
}
