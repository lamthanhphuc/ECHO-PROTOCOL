using EchoProtocol.Api.Common;
using EchoProtocol.Api.DTOs.Payments;

namespace EchoProtocol.Api.Services.Interfaces;

public interface IPaymentCatalogService
{
    ServiceResult<PaymentCatalogResponse> GetActiveWalletProducts();
}
