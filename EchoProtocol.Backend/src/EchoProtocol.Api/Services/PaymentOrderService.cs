using System.Security.Cryptography;
using System.Text;
using EchoProtocol.Api.Common;
using EchoProtocol.Api.Configurations;
using EchoProtocol.Api.Data;
using EchoProtocol.Api.DTOs.Payments;
using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services.Interfaces;
using EchoProtocol.Api.Services.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace EchoProtocol.Api.Services;

public sealed class PaymentOrderService : IPaymentOrderService
{
    private const int MaximumIdempotencyKeyLength = 100;
    private readonly AppDbContext _db;
    private readonly PaymentCatalogSettings _catalog;
    private readonly TimeProvider _timeProvider;

    public PaymentOrderService(
        AppDbContext db,
        IOptions<PaymentCatalogSettings> catalog,
        TimeProvider timeProvider)
    {
        _db = db;
        _catalog = catalog.Value;
        _timeProvider = timeProvider;
    }

    public async Task<ServiceResult<PaymentOrderResponse>> CreateAsync(
        Guid userId,
        CreatePaymentOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateRequest(request);
        if (validation is not null)
            return Fail(validation, ErrorCodes.ValidationError);

        var idempotencyKey = request.IdempotencyKey.Trim();
        var productReference = request.ProductReference.Trim();
        var requestedProvider = request.Provider.Trim().ToUpperInvariant();
        var requestFingerprint = Fingerprint(productReference, requestedProvider);

        var existing = await LoadByIdempotencyKeyAsync(userId, idempotencyKey, cancellationToken);
        if (existing is not null)
            return ResolveReplay(existing, requestFingerprint);

        var configuredProvider = ResolveProvider(requestedProvider);
        if (configuredProvider is null)
        {
            return _catalog.AllowedProviders.Length == 0
                ? Fail("Payment provider whitelist is not configured", ErrorCodes.PaymentConfigurationMissing)
                : Fail("Payment provider is not allowed", ErrorCodes.PaymentProviderInvalid);
        }

        var productResult = ResolveProduct(productReference);
        if (!productResult.IsSuccess)
            return Fail(productResult.Message, productResult.ErrorCode!);

        var product = productResult.Data!;
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var order = new PaymentOrder
        {
            PaymentOrderId = Guid.NewGuid(),
            UserId = userId,
            Provider = configuredProvider,
            Purpose = product.Purpose.Trim(),
            ProductReference = product.ProductReference.Trim(),
            Amount = product.Amount,
            Currency = product.Currency.Trim().ToUpperInvariant(),
            Status = PaymentOrderStatus.CREATED,
            IdempotencyKey = idempotencyKey,
            RequestFingerprint = requestFingerprint,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            ExpiresAtUtc = product.ExpiresAfterMinutes is > 0
                ? now.AddMinutes(product.ExpiresAfterMinutes.Value)
                : null
        };

        _db.PaymentOrders.Add(order);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            return ServiceResult<PaymentOrderResponse>.Success(
                PaymentOrderResponse.From(order, false),
                "Payment order created");
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            _db.ChangeTracker.Clear();
            existing = await LoadByIdempotencyKeyAsync(userId, idempotencyKey, cancellationToken);
            return existing is not null
                ? ResolveReplay(existing, requestFingerprint)
                : Fail("Payment order conflicted with another request", ErrorCodes.PaymentOrderConflict);
        }
    }

    public async Task<ServiceResult<PaymentOrderResponse>> GetOwnedAsync(
        Guid userId,
        Guid paymentOrderId,
        CancellationToken cancellationToken = default)
    {
        var order = await _db.PaymentOrders.AsNoTracking()
            .SingleOrDefaultAsync(item => item.PaymentOrderId == paymentOrderId, cancellationToken);
        if (order is null)
            return Fail("Payment order not found", ErrorCodes.PaymentOrderNotFound);

        return order.UserId == userId
            ? ServiceResult<PaymentOrderResponse>.Success(PaymentOrderResponse.From(order, false))
            : Fail("Payment order does not belong to the authenticated user", ErrorCodes.PaymentOrderForbidden);
    }

    public async Task<ServiceResult<PaymentOrderResponse>> TransitionAsync(
        Guid paymentOrderId,
        PaymentOrderTransition transition,
        CancellationToken cancellationToken = default)
    {
        if (transition.TargetStatus is PaymentOrderStatus.PAID or PaymentOrderStatus.FULFILLED)
            return Fail(
                "PAID requires verified provider evidence and FULFILLED requires PaymentFulfillmentService",
                ErrorCodes.PaymentInvalidStateTransition);

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var order = await LoadForUpdateAsync(paymentOrderId, cancellationToken);
        if (order is null)
            return Fail("Payment order not found", ErrorCodes.PaymentOrderNotFound);

        if (!PaymentOrderStateMachine.CanTransition(order.Status, transition.TargetStatus))
        {
            return Fail(
                $"Payment order cannot transition from {order.Status} to {transition.TargetStatus}",
                ErrorCodes.PaymentInvalidStateTransition);
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var transitionError = ValidateTransition(order, transition, now);
        if (transitionError is not null)
            return Fail(transitionError, ErrorCodes.PaymentInvalidTransitionPayload);

        ApplyTransition(order, transition, now);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ServiceResult<PaymentOrderResponse>.Success(
                PaymentOrderResponse.From(order, false),
                "Payment order state updated");
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            await transaction.RollbackAsync(cancellationToken);
            _db.ChangeTracker.Clear();
            return Fail("Provider or fulfillment identity is already in use", ErrorCodes.PaymentProviderIdentityConflict);
        }
    }

    private async Task<PaymentOrder?> LoadForUpdateAsync(Guid paymentOrderId, CancellationToken cancellationToken)
    {
        if (_db.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL")
        {
            return await _db.PaymentOrders
                .FromSqlInterpolated($"SELECT * FROM \"PaymentOrders\" WHERE \"PaymentOrderId\" = {paymentOrderId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);
        }

        return await _db.PaymentOrders.SingleOrDefaultAsync(
            item => item.PaymentOrderId == paymentOrderId,
            cancellationToken);
    }

    private Task<PaymentOrder?> LoadByIdempotencyKeyAsync(
        Guid userId,
        string idempotencyKey,
        CancellationToken cancellationToken) =>
        _db.PaymentOrders.AsNoTracking().SingleOrDefaultAsync(
            item => item.UserId == userId && item.IdempotencyKey == idempotencyKey,
            cancellationToken);

    private string? ResolveProvider(string requestedProvider) =>
        _catalog.AllowedProviders
            .Select(item => item.Trim())
            .FirstOrDefault(item =>
                item.Length > 0 && string.Equals(item, requestedProvider, StringComparison.OrdinalIgnoreCase));

    private ServiceResult<PaymentProductSettings> ResolveProduct(string productReference)
    {
        if (_catalog.Products.Length == 0)
            return ServiceResult<PaymentProductSettings>.Failure(
                "Payment product catalog is not configured",
                ErrorCodes.PaymentConfigurationMissing);

        var matches = _catalog.Products.Where(item =>
            string.Equals(item.ProductReference.Trim(), productReference, StringComparison.Ordinal)).ToArray();
        if (matches.Length > 1)
            return ServiceResult<PaymentProductSettings>.Failure(
                "Payment product catalog contains duplicate references",
                ErrorCodes.PaymentConfigurationInvalid);

        var product = matches.SingleOrDefault();
        if (product is null || !product.IsActive)
            return ServiceResult<PaymentProductSettings>.Failure(
                "Payment product is unknown or disabled",
                ErrorCodes.PaymentProductInvalid);

        if (product.Amount <= 0 ||
            string.IsNullOrWhiteSpace(product.Purpose) ||
            !IsCurrency(product.Currency) ||
            product.ExpiresAfterMinutes is <= 0)
        {
            return ServiceResult<PaymentProductSettings>.Failure(
                "Payment product configuration is invalid",
                ErrorCodes.PaymentConfigurationInvalid);
        }

        return ServiceResult<PaymentProductSettings>.Success(product);
    }

    private static string? ValidateRequest(CreatePaymentOrderRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ProductReference) || request.ProductReference.Trim().Length > 128)
            return "ProductReference must contain between 1 and 128 characters";
        if (string.IsNullOrWhiteSpace(request.Provider) || request.Provider.Trim().Length > 50)
            return "Provider must contain between 1 and 50 characters";
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey) ||
            request.IdempotencyKey.Trim().Length > MaximumIdempotencyKeyLength)
            return $"IdempotencyKey must contain between 1 and {MaximumIdempotencyKeyLength} characters";
        return request.ExtensionData is { Count: > 0 }
            ? "Payment order request contains unsupported fields"
            : null;
    }

    private static string? ValidateTransition(
        PaymentOrder order,
        PaymentOrderTransition transition,
        DateTime now)
    {
        if (transition.TargetStatus == PaymentOrderStatus.PENDING_PAYMENT &&
            !ValidIdentifier(transition.ProviderOrderId, 200))
            return "ProviderOrderId is required when payment becomes pending";
        if (transition.TargetStatus == PaymentOrderStatus.PAID)
        {
            if (!ValidIdentifier(transition.ProviderTransactionId, 200))
                return "ProviderTransactionId is required when payment becomes paid";
            if (order.ExpiresAtUtc is not null && order.ExpiresAtUtc <= now)
                return "Expired payment order cannot become paid";
        }
        if (transition.TargetStatus == PaymentOrderStatus.FULFILLED &&
            !ValidIdentifier(transition.FulfillmentReference, 200))
            return "FulfillmentReference is required when payment becomes fulfilled";
        return null;
    }

    private static void ApplyTransition(
        PaymentOrder order,
        PaymentOrderTransition transition,
        DateTime now)
    {
        order.Status = transition.TargetStatus;
        order.UpdatedAtUtc = now;
        if (transition.TargetStatus == PaymentOrderStatus.PENDING_PAYMENT)
            order.ProviderOrderId = transition.ProviderOrderId!.Trim();
        else if (transition.TargetStatus == PaymentOrderStatus.PAID)
        {
            order.ProviderTransactionId = transition.ProviderTransactionId!.Trim();
            order.PaidAtUtc = now;
        }
        else if (transition.TargetStatus == PaymentOrderStatus.FULFILLED)
        {
            order.FulfillmentReference = transition.FulfillmentReference!.Trim();
            order.FulfilledAtUtc = now;
        }
    }

    private static ServiceResult<PaymentOrderResponse> ResolveReplay(
        PaymentOrder order,
        string requestFingerprint) =>
        string.Equals(order.RequestFingerprint, requestFingerprint, StringComparison.Ordinal)
            ? ServiceResult<PaymentOrderResponse>.Success(
                PaymentOrderResponse.From(order, true),
                "Payment order already exists")
            : Fail(
                "Idempotency key was already used with a different payment request",
                ErrorCodes.PaymentIdempotencyConflict);

    private static string Fingerprint(string productReference, string provider)
    {
        var value = $"{productReference.Length}:{productReference}|{provider.Length}:{provider}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }

    private static bool IsCurrency(string value) =>
        value.Trim().Length == 3 && value.Trim().All(character => character is >= 'A' and <= 'Z');

    private static bool ValidIdentifier(string? value, int maximumLength) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= maximumLength;

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    private static ServiceResult<PaymentOrderResponse> Fail(string message, string code) =>
        ServiceResult<PaymentOrderResponse>.Failure(message, code);
}
