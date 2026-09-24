using EchoProtocol.Api.Common;
using EchoProtocol.Api.Configurations;
using EchoProtocol.Api.DTOs.Payments;
using EchoProtocol.Api.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace EchoProtocol.Api.Services;

public sealed class PaymentCatalogService(IOptions<PaymentCatalogSettings> options)
    : IPaymentCatalogService
{
    private readonly PaymentCatalogSettings _catalog = options.Value;

    public ServiceResult<PaymentCatalogResponse> GetActiveWalletProducts()
    {
        var provider = _catalog.AllowedProviders
            .Select(item => item.Trim().ToUpperInvariant())
            .FirstOrDefault(item => item == "PAYOS");
        if (provider is null)
            return Fail("PAYOS is not configured as an allowed payment provider");

        var products = _catalog.Products
            .Where(item => item.IsActive &&
                string.Equals(item.Purpose.Trim(), "WALLET_CREDIT", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (products.Length == 0)
            return Fail("No active wallet payment products are configured");

        if (products.GroupBy(item => item.ProductReference.Trim(), StringComparer.Ordinal)
            .Any(group => string.IsNullOrWhiteSpace(group.Key) || group.Count() > 1) ||
            products.Any(item =>
                string.IsNullOrWhiteSpace(item.DisplayName) ||
                item.DisplayName.Trim().Length > 100 ||
                item.Amount <= 0 ||
                !string.Equals(item.Currency.Trim(), "VND", StringComparison.OrdinalIgnoreCase) ||
                item.WalletCreditAmount is not > 0 ||
                !string.Equals(item.FulfillmentKind?.Trim(), "WALLET_CREDIT", StringComparison.OrdinalIgnoreCase)))
        {
            return Fail("Payment wallet product catalog is invalid");
        }

        return ServiceResult<PaymentCatalogResponse>.Success(new PaymentCatalogResponse
        {
            Items = products
                .OrderBy(item => item.Amount)
                .ThenBy(item => item.ProductReference, StringComparer.Ordinal)
                .Select(item => new PaymentCatalogItemResponse
                {
                    ProductReference = item.ProductReference.Trim(),
                    DisplayName = item.DisplayName.Trim(),
                    Amount = item.Amount,
                    Currency = item.Currency.Trim().ToUpperInvariant(),
                    WalletCredit = item.WalletCreditAmount!.Value,
                    Provider = provider
                })
                .ToArray()
        }, "Payment catalog retrieved");
    }

    private static ServiceResult<PaymentCatalogResponse> Fail(string message) =>
        ServiceResult<PaymentCatalogResponse>.Failure(
            message, ErrorCodes.PaymentConfigurationInvalid);
}
