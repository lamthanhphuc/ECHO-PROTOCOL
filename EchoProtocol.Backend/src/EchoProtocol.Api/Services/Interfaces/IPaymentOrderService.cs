using EchoProtocol.Api.Common;
using EchoProtocol.Api.DTOs.Payments;
using EchoProtocol.Api.Services.Models;

namespace EchoProtocol.Api.Services.Interfaces;

public interface IPaymentOrderService
{
    Task<ServiceResult<PaymentOrderResponse>> CreateAsync(
        Guid userId,
        CreatePaymentOrderRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PaymentOrderResponse>> GetOwnedAsync(
        Guid userId,
        Guid paymentOrderId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PaymentOrderResponse>> TransitionAsync(
        Guid paymentOrderId,
        PaymentOrderTransition transition,
        CancellationToken cancellationToken = default);
}
