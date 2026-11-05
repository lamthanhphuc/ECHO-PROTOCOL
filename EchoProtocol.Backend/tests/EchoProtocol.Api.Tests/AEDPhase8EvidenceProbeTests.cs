using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using EchoProtocol.Api.DTOs.Scenarios;
using Xunit;

namespace EchoProtocol.Api.Tests;

public sealed class AEDPhase8EvidenceProbeTests
{
    private static bool Validate(AEDPhase8EvidenceProbeRequestV1 request)
    {
        var errors = new List<ValidationResult>();
        return Validator.TryValidateObject(request, new ValidationContext(request), errors, true);
    }

    [Fact, Trait("Category", "AEDv2")]
    public void ZeroOrdinalIsRejected() => Assert.False(Validate(new AEDPhase8EvidenceProbeRequestV1 { CompletedEvidencePhaseOrdinal = 0, EvidenceFingerprint = new string('a', 64) }));

    [Fact, Trait("Category", "AEDv2")]
    public void EmptyFingerprintIsRejected() => Assert.False(Validate(new AEDPhase8EvidenceProbeRequestV1 { CompletedEvidencePhaseOrdinal = 1, EvidenceFingerprint = "" }));

    [Fact, Trait("Category", "AEDv2")]
    public void NonHexFingerprintIsRejected() => Assert.False(Validate(new AEDPhase8EvidenceProbeRequestV1 { CompletedEvidencePhaseOrdinal = 1, EvidenceFingerprint = new string('z', 64) }));

    [Fact, Trait("Category", "AEDv2")]
    public void CanonicalEvidenceIsNotGameplayApproval()
    {
        var request = new AEDPhase8EvidenceProbeRequestV1 { CompletedEvidencePhaseOrdinal = 1, EvidenceFingerprint = new string('a', 64) };
        Assert.True(Validate(request));
        var receipt = new AEDPhase8EvidenceProbeResultV1(Guid.NewGuid(), 1, request.EvidenceFingerprint, true, false, false, false, "P8_CANONICAL_VERIFIED_EXTENDED_UNSUPPORTED");
        Assert.True(receipt.CanonicalEvidenceVerified);
        Assert.False(receipt.ExtendedMetricsVerified);
        Assert.False(receipt.ResourceLedgerVerified);
        Assert.False(receipt.GameplayApproved);
    }
}
