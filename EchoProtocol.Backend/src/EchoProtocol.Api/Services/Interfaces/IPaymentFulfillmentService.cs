using EchoProtocol.Api.Common;
using EchoProtocol.Api.DTOs.Payments;

namespace EchoProtocol.Api.Services.Interfaces;

public interface IPaymentFulfillmentService
{
    Task<ServiceResult<PaymentFulfillmentResponse>> FulfillAsync(
        Guid paymentOrderId,
        CancellationToken cancellationToken = default);
}
