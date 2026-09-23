using EchoProtocol.Api.Services.Interfaces;

namespace EchoProtocol.Api.Services;

public sealed class PaymentProviderRegistry(IEnumerable<IPaymentProvider> providers)
    : IPaymentProviderRegistry
{
    private readonly IReadOnlyDictionary<string, IPaymentProvider> _providers = providers
        .GroupBy(provider => provider.ProviderKey, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(
            group => group.Key,
            group => group.Single(),
            StringComparer.OrdinalIgnoreCase);

    public IPaymentProvider? Resolve(string providerKey) =>
        _providers.GetValueOrDefault(providerKey);
}
