using EchoProtocol.Api.Common;
using EchoProtocol.Api.DTOs.Payments;

namespace EchoProtocol.Api.Services.Interfaces;

public interface IPaymentCheckoutService
{
    Task<ServiceResult<PaymentCheckoutResponse>> CreateCheckoutAsync(
        Guid userId,
        Guid paymentOrderId,
        CancellationToken cancellationToken = default);
}
