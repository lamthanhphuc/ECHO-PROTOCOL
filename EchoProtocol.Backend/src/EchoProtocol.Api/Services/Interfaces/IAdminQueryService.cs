using EchoProtocol.Api.Common;
using EchoProtocol.Api.DTOs.Admin;

namespace EchoProtocol.Api.Services.Interfaces;

public interface IAdminQueryService
{
    Task<ServiceResult<object>> GetProgressionAsync(Guid userId, CancellationToken ct = default);
    Task<ServiceResult<object>> GrantExperienceAsync(Guid adminId, Guid userId, AdminGrantExperienceRequest request, CancellationToken ct = default);
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

    Task<ServiceResult<AdminGrantCreditsResponse>> GrantCreditsAsync(
        Guid userId,
        AdminGrantCreditsRequest request,
        CancellationToken cancellationToken = default);}
