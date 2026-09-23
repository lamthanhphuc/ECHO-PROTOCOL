using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EchoProtocol.Api.Common;
using EchoProtocol.Api.Configurations;
using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services.Interfaces;
using EchoProtocol.Api.Services.Models;
using Microsoft.Extensions.Options;

namespace EchoProtocol.Api.Services;

public sealed class PayOSPaymentProvider : IPaymentProvider
{
    private readonly HttpClient _httpClient;
    private readonly PayOSSettings _settings;

    public PayOSPaymentProvider(HttpClient httpClient, IOptions<PayOSSettings> settings)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
    }

    public string ProviderKey => PayOSSettings.ProviderKey;
    public bool IsConfigured => ValidateConfiguration() is null;

    public async Task<PaymentProviderCheckoutResult> CreateCheckoutAsync(
        PaymentProviderCheckoutRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        var returnUrl = new Uri(_settings.ReturnUrl, UriKind.Absolute);
        var cancelUrl = new Uri(_settings.CancelUrl, UriKind.Absolute);
        var signatureData =
            $"amount={request.Amount}&cancelUrl={cancelUrl.AbsoluteUri}&description={request.Description}" +
            $"&orderCode={request.OrderCode}&returnUrl={returnUrl.AbsoluteUri}";
        var payload = new Dictionary<string, object?>
        {
            ["orderCode"] = request.OrderCode,
            ["amount"] = request.Amount,
            ["description"] = request.Description,
            ["cancelUrl"] = cancelUrl.AbsoluteUri,
            ["returnUrl"] = returnUrl.AbsoluteUri,
            ["signature"] = Sign(signatureData)
        };
        if (request.ExpiresAtUtc is not null)
        {
            var expiresAt = new DateTimeOffset(
                DateTime.SpecifyKind(request.ExpiresAtUtc.Value, DateTimeKind.Utc)).ToUnixTimeSeconds();
            if (expiresAt is <= 0 or > int.MaxValue)
                throw new PaymentProviderException("payOS expiration is outside the supported Unix Int32 range");
            payload["expiredAt"] = expiresAt;
        }

        using var message = CreateRequest(HttpMethod.Post, "/v2/payment-requests");
        message.Content = JsonContent.Create(payload);
        return await SendAndParseAsync(message, cancellationToken);
    }

    public async Task<PaymentProviderCheckoutResult?> QueryPaymentAsync(
        string providerOrderId,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        try
        {
            using var message = CreateRequest(
                HttpMethod.Get,
                $"/v2/payment-requests/{Uri.EscapeDataString(providerOrderId)}");
            using var response = await _httpClient.SendAsync(message, cancellationToken);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return null;
            if (!response.IsSuccessStatusCode)
                throw new PaymentProviderException($"payOS query failed with HTTP {(int)response.StatusCode}");
            return await ParseResponseAsync(response, cancellationToken);
        }
        catch (PaymentProviderException)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            throw new PaymentProviderException("payOS query transport or response failure", exception);
        }
    }

    public PaymentWebhookVerificationResult VerifyAndParseWebhook(JsonElement payload)
    {
        if (!IsConfigured)
            return PaymentWebhookVerificationResult.Invalid(
                "payOS configuration is missing or invalid",
                ErrorCodes.PaymentProviderConfigurationInvalid);
        if (payload.ValueKind != JsonValueKind.Object ||
            !payload.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object ||
            !payload.TryGetProperty("signature", out var signatureElement) ||
            signatureElement.ValueKind != JsonValueKind.String)
        {
            return PaymentWebhookVerificationResult.Invalid(
                "payOS webhook envelope is invalid",
                ErrorCodes.PaymentWebhookInvalid);
        }

        var signature = signatureElement.GetString();
        var canonicalData = CanonicalizeObject(data);
        if (!VerifySignature(canonicalData, signature))
        {
            return PaymentWebhookVerificationResult.Invalid(
                "payOS webhook signature is invalid",
                ErrorCodes.PaymentWebhookSignatureInvalid);
        }

        if (!TryGetInt64(data, "orderCode", out var orderCode) ||
            !TryGetDecimal(data, "amount", out var amount) ||
            amount <= 0 ||
            !TryGetString(data, "reference", out var reference) ||
            string.IsNullOrWhiteSpace(reference))
        {
            return PaymentWebhookVerificationResult.Invalid(
                "Verified payOS webhook is missing required normalized fields",
                ErrorCodes.PaymentWebhookInvalid);
        }

        var currency = data.TryGetProperty("currency", out var currencyElement) &&
                       currencyElement.ValueKind == JsonValueKind.String
            ? currencyElement.GetString()
            : null;
        var dataCode = data.TryGetProperty("code", out var dataCodeElement)
            ? ElementValue(dataCodeElement)
            : string.Empty;
        var status = dataCode == "00"
            ? PaymentProviderEventStatus.PAID
            : PaymentProviderEventStatus.FAILED;
        var paidAtUtc = TryParseProviderTimestamp(data);
        var fingerprint = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(canonicalData)));

        return PaymentWebhookVerificationResult.Valid(new NormalizedPaymentProviderEvent(
            ProviderKey,
            reference.Trim(),
            orderCode.ToString(CultureInfo.InvariantCulture),
            amount,
            currency?.Trim().ToUpperInvariant(),
            status,
            paidAtUtc,
            fingerprint));
    }

    private async Task<PaymentProviderCheckoutResult> SendAndParseAsync(
        HttpRequestMessage message,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.SendAsync(message, cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new PaymentProviderException($"payOS checkout failed with HTTP {(int)response.StatusCode}");
            return await ParseResponseAsync(response, cancellationToken);
        }
        catch (PaymentProviderException)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            throw new PaymentProviderException("payOS checkout transport or response failure", exception);
        }
    }

    private async Task<PaymentProviderCheckoutResult> ParseResponseAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        using var document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);
        var root = document.RootElement;
        if (!root.TryGetProperty("code", out var code) || ElementValue(code) != "00" ||
            !root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("signature", out var signature) || signature.ValueKind != JsonValueKind.String ||
            !VerifySignature(CanonicalizeObject(data), signature.GetString()))
        {
            throw new PaymentProviderException("payOS returned an unsuccessful or unverifiable response");
        }

        if (!TryGetInt64(data, "orderCode", out var orderCode) ||
            !TryGetInt64(data, "amount", out var amount) ||
            !(TryGetString(data, "paymentLinkId", out var paymentLinkId) ||
              TryGetString(data, "id", out paymentLinkId)))
        {
            throw new PaymentProviderException("payOS response is missing required checkout fields");
        }

        Uri? checkoutUri = null;
        if (TryGetString(data, "checkoutUrl", out var checkoutUrl))
            Uri.TryCreate(checkoutUrl, UriKind.Absolute, out checkoutUri);
        checkoutUri ??= new Uri($"https://pay.payos.vn/web/{Uri.EscapeDataString(paymentLinkId)}");
        if (checkoutUri.Scheme != Uri.UriSchemeHttps ||
            !string.Equals(checkoutUri.Host, "pay.payos.vn", StringComparison.OrdinalIgnoreCase))
            throw new PaymentProviderException("payOS checkout URL is not on the approved HTTPS host");

        var status = TryGetString(data, "status", out var statusValue) ? statusValue : "UNKNOWN";
        var currency = TryGetString(data, "currency", out var currencyValue) ? currencyValue : "VND";
        return new PaymentProviderCheckoutResult(
            orderCode.ToString(CultureInfo.InvariantCulture),
            paymentLinkId,
            checkoutUri,
            status,
            amount,
            currency);
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path)
    {
        var baseUri = new Uri(_settings.BaseUrl.TrimEnd('/') + "/", UriKind.Absolute);
        var message = new HttpRequestMessage(method, new Uri(baseUri, path.TrimStart('/')));
        message.Headers.TryAddWithoutValidation("x-client-id", _settings.ClientId);
        message.Headers.TryAddWithoutValidation("x-api-key", _settings.ApiKey);
        return message;
    }

    private string? ValidateConfiguration()
    {
        if (string.IsNullOrWhiteSpace(_settings.ClientId) ||
            string.IsNullOrWhiteSpace(_settings.ApiKey) ||
            string.IsNullOrWhiteSpace(_settings.ChecksumKey))
            return "payOS credentials are not configured";
        if (!ValidHttpsUri(_settings.BaseUrl) ||
            !ValidHttpsUri(_settings.ReturnUrl) ||
            !ValidHttpsUri(_settings.CancelUrl))
            return "payOS URLs must be absolute HTTPS URLs";
        return null;
    }

    private void EnsureConfigured()
    {
        var error = ValidateConfiguration();
        if (error is not null)
            throw new PaymentProviderException(error);
    }

    private string Sign(string canonicalData)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_settings.ChecksumKey));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(canonicalData))).ToLowerInvariant();
    }

    private bool VerifySignature(string canonicalData, string? providedSignature)
    {
        if (string.IsNullOrWhiteSpace(providedSignature) || providedSignature.Length != 64)
            return false;
        byte[] provided;
        try { provided = Convert.FromHexString(providedSignature); }
        catch (FormatException) { return false; }
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_settings.ChecksumKey));
        var expected = hmac.ComputeHash(Encoding.UTF8.GetBytes(canonicalData));
        return CryptographicOperations.FixedTimeEquals(expected, provided);
    }

    internal static string CanonicalizeObject(JsonElement data) => string.Join("&",
        data.EnumerateObject()
            .OrderBy(property => property.Name, StringComparer.Ordinal)
            .Select(property => $"{property.Name}={ElementValue(property.Value)}"));

    private static string ElementValue(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString() ?? string.Empty,
        JsonValueKind.Number => value.GetRawText(),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
        _ => value.GetRawText()
    };

    private static bool TryGetString(JsonElement data, string name, out string value)
    {
        value = string.Empty;
        if (!data.TryGetProperty(name, out var element)) return false;
        value = ElementValue(element);
        return value.Length > 0;
    }

    private static bool TryGetInt64(JsonElement data, string name, out long value)
    {
        value = default;
        if (!data.TryGetProperty(name, out var element)) return false;
        return element.ValueKind == JsonValueKind.Number
            ? element.TryGetInt64(out value)
            : element.ValueKind == JsonValueKind.String && long.TryParse(
                element.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryGetDecimal(JsonElement data, string name, out decimal value)
    {
        value = default;
        if (!data.TryGetProperty(name, out var element)) return false;
        return element.ValueKind == JsonValueKind.Number
            ? element.TryGetDecimal(out value)
            : element.ValueKind == JsonValueKind.String && decimal.TryParse(
                element.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    private static DateTime? TryParseProviderTimestamp(JsonElement data)
    {
        if (!TryGetString(data, "transactionDateTime", out var value) ||
            !DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces, out var parsed))
            return null;
        var hasExplicitOffset = value.EndsWith('Z') ||
                                (value.Length >= 6 &&
                                 value[^6] is '+' or '-' && value[^3] == ':');
        return hasExplicitOffset
            ? parsed.UtcDateTime
            : null;
    }

    private static bool ValidHttpsUri(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;
}
