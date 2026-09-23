using EchoProtocol.Api.Common;
using EchoProtocol.Api.Configurations;
using EchoProtocol.Api.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace EchoProtocol.Api.Tests;

public sealed class PaymentCatalogServiceTests
{
    [Fact]
    public void ReturnsActiveWalletProductsOrderedByAmount()
    {
        var service = CreateService(
            Product("LARGE", "Large", 50_000, 1_250),
            Product("SMALL", "Small", 20_000, 500));

        var result = service.GetActiveWalletProducts();

        Assert.True(result.IsSuccess);
        Assert.Collection(result.Data!.Items,
            item =>
            {
                Assert.Equal("SMALL", item.ProductReference);
                Assert.Equal(20_000, item.Amount);
                Assert.Equal(500, item.WalletCredit);
                Assert.Equal("PAYOS", item.Provider);
            },
            item => Assert.Equal("LARGE", item.ProductReference));
    }

    [Fact]
    public void DisabledProductsAreNotReturned()
    {
        var disabled = Product("DISABLED", "Disabled", 20_000, 500, false);
        var service = CreateService(disabled, Product("ACTIVE", "Active", 30_000, 750));

        var item = Assert.Single(service.GetActiveWalletProducts().Data!.Items);

        Assert.Equal("ACTIVE", item.ProductReference);
    }

    [Fact]
    public void DuplicateProductReferenceIsRejected()
    {
        var service = CreateService(
            Product("DUPLICATE", "First", 20_000, 500),
            Product("DUPLICATE", "Second", 30_000, 750));

        var result = service.GetActiveWalletProducts();

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.PaymentConfigurationInvalid, result.ErrorCode);
    }

    [Fact]
    public void InvalidWalletCreditIsRejected()
    {
        var service = CreateService(Product("INVALID", "Invalid", 20_000, 0));

        var result = service.GetActiveWalletProducts();

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.PaymentConfigurationInvalid, result.ErrorCode);
    }

    private static PaymentCatalogService CreateService(params PaymentProductSettings[] products) =>
        new(Options.Create(new PaymentCatalogSettings
        {
            AllowedProviders = ["PAYOS"],
            Products = products
        }));

    private static PaymentProductSettings Product(
        string reference,
        string displayName,
        decimal amount,
        int walletCredit,
        bool isActive = true) => new()
        {
            ProductReference = reference,
            DisplayName = displayName,
            Purpose = "WALLET_CREDIT",
            Amount = amount,
            Currency = "VND",
            IsActive = isActive,
            ExpiresAfterMinutes = 30,
            FulfillmentKind = "WALLET_CREDIT",
            WalletCreditAmount = walletCredit
        };
}
