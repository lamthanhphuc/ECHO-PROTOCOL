using System.Security.Claims;
using EchoProtocol.Api.Common;
using EchoProtocol.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace EchoProtocol.Api.Legal;

public sealed class AgreementGateMiddleware(RequestDelegate next)
{
    public static bool RequiresAcceptance(string path, string method)
    {
        path = path.TrimEnd('/').ToLowerInvariant();
        if (path.StartsWith("/api/match-authority") || path.StartsWith("/api/matches")
            || path.StartsWith("/api/telemetry") || path.StartsWith("/api/scenarios")) return true;
        return method != "GET" && method != "HEAD" && (path.StartsWith("/api/payments/orders")
            || path.StartsWith("/api/inventory") || path == "/api/shop/purchase");
    }

    public async Task InvokeAsync(HttpContext context, AppDbContext db)
    {
        if (context.User.Identity?.IsAuthenticated == true
            && RequiresAcceptance(context.Request.Path.Value ?? "", context.Request.Method))
        {
            if (!Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
                || !await db.UserAgreementAcceptances.AsNoTracking().AnyAsync(a => a.UserId == userId
                    && a.BundleVersion == AgreementCatalog.Version && a.ContentSha256 == AgreementCatalog.Hash,
                    context.RequestAborted))
            {
                context.Response.StatusCode = 403;
                await context.Response.WriteAsJsonAsync(ApiResponse<object>.Fail(
                    "Review and accept the current policies before playing or purchasing.", "AGREEMENT_REQUIRED"), context.RequestAborted);
                return;
            }
        }
        await next(context);
    }
}
