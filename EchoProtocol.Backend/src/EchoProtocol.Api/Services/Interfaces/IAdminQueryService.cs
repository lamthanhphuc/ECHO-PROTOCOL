using EchoProtocol.Api.Common;
using EchoProtocol.Api.DTOs.Admin;

namespace EchoProtocol.Api.Services.Interfaces;

public interface IAdminQueryService
{
    Task<ServiceResult<AdminPagedResponse<AdminUserResponse>>> GetUsersAsync(
        AdminUsersQuery query, CancellationToken cancellationToken = default);
    Task<ServiceResult<AdminUserResponse>> GetUserAsync(
        Guid userId, CancellationToken cancellationToken = default);
    Task<ServiceResult<AdminPagedResponse<AdminPaymentSummary>>> GetPaymentsAsync(
        AdminPaymentsQuery query, CancellationToken cancellationToken = default);
    Task<ServiceResult<AdminPaymentDetailResponse>> GetPaymentAsync(
        Guid paymentOrderId, CancellationToken cancellationToken = default);
    Task<ServiceResult<AdminPagedResponse<AdminWalletTransactionResponse>>> GetWalletTransactionsAsync(
        AdminWalletTransactionsQuery query, CancellationToken cancellationToken = default);
    Task<ServiceResult<AdminPagedResponse<AdminPurchaseResponse>>> GetPurchasesAsync(
        AdminPurchasesQuery query, CancellationToken cancellationToken = default);
}
