using System.Security.Claims;
using EchoProtocol.Api.Common;
using EchoProtocol.Api.Data;
using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Legal;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EchoProtocol.Api.Controllers;

public sealed record AcceptAgreementRequest(string BundleVersion, string ContentSha256, string Locale,
    bool AdultConfirmed, bool WarningAcknowledged, bool TermsAccepted, bool PrivacyNoticeAcknowledged,
    bool CommunityAccepted, bool TransactionsAccepted)
{
    public bool ConfirmsAll => AdultConfirmed && WarningAcknowledged && TermsAccepted
        && PrivacyNoticeAcknowledged && CommunityAccepted && TransactionsAccepted;
}

[ApiController]
[Route("api/agreements")]
public sealed class AgreementsController(AppDbContext db) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("documents")]
    public IActionResult Documents() => Ok(ApiResponse<object>.Ok(new {
        bundle = AgreementCatalog.Bundle, contentSha256 = AgreementCatalog.Hash }));

    [Authorize]
    [HttpGet("status")]
    public async Task<IActionResult> Status(CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        bool accepted = await db.UserAgreementAcceptances.AsNoTracking().AnyAsync(a => a.UserId == userId
            && a.BundleVersion == AgreementCatalog.Version && a.ContentSha256 == AgreementCatalog.Hash, ct);
        return Ok(ApiResponse<object>.Ok(new { accepted, bundleVersion = AgreementCatalog.Version,
            contentSha256 = AgreementCatalog.Hash }));
    }

    [Authorize]
    [HttpPost("accept")]
    public async Task<IActionResult> Accept(AcceptAgreementRequest request, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        if (request.BundleVersion != AgreementCatalog.Version || request.ContentSha256 != AgreementCatalog.Hash)
            return Conflict(ApiResponse<object>.Fail("Policies changed. Read the current documents.", "AGREEMENT_VERSION_CHANGED"));
        if (!request.ConfirmsAll || request.Locale is not ("vi" or "en"))
            return BadRequest(ApiResponse<object>.Fail("All required declarations must be confirmed.", "AGREEMENT_CONFIRMATION_REQUIRED"));
        if (!await db.UserAgreementAcceptances.AnyAsync(a => a.UserId == userId && a.BundleVersion == AgreementCatalog.Version, ct))
        {
            db.UserAgreementAcceptances.Add(new UserAgreementAcceptance {
                UserId = userId, BundleVersion = AgreementCatalog.Version, ContentSha256 = AgreementCatalog.Hash,
                DocumentVersions = AgreementCatalog.DocumentVersions, AcceptedAtUtc = DateTime.UtcNow, Locale = request.Locale,
                AdultConfirmed = true, WarningAcknowledged = true, TermsAccepted = true,
                PrivacyNoticeAcknowledged = true, CommunityAccepted = true, TransactionsAccepted = true });
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException)
            {
                // A simultaneous retry may have saved the same acceptance. Never swallow other DB errors.
                if (!await db.UserAgreementAcceptances.AsNoTracking().AnyAsync(a => a.UserId == userId
                    && a.BundleVersion == AgreementCatalog.Version && a.ContentSha256 == AgreementCatalog.Hash, ct)) throw;
            }
        }
        bool accepted = await db.UserAgreementAcceptances.AsNoTracking().AnyAsync(a => a.UserId == userId
            && a.BundleVersion == AgreementCatalog.Version && a.ContentSha256 == AgreementCatalog.Hash, ct);
        if (!accepted) return Conflict(ApiResponse<object>.Fail("A new policy version is required.", "AGREEMENT_VERSION_CHANGED"));
        return Ok(ApiResponse<object>.Ok(new { accepted = true, bundleVersion = AgreementCatalog.Version, contentSha256 = AgreementCatalog.Hash }));
    }
}
