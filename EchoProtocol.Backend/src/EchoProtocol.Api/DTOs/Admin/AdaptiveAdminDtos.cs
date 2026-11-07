using System.ComponentModel.DataAnnotations;

namespace EchoProtocol.Api.DTOs.Admin;

public sealed record AdaptiveConfigRef(string Id, string Version);

public sealed record AdaptiveParameter(double Baseline, double[] Tiers);

public sealed record AdaptivePilotPolicy(
    double ObservationSeconds, double WindowSeconds, double EvidenceMaxAgeSeconds,
    double[] PressureWeights, double ReliefThreshold, double IncreasePressureCeiling,
    double DownRecoverySeconds, int OrdinarySupportQuota, int EmergencySupportQuota, double SupportCooldownSeconds,
    double ReliefCooldownSeconds = 90, double IncreaseCooldownSeconds = 180,
    double TierDwellSeconds = 12, double ProtectionSeconds = 10, double ProtectionCooldownSeconds = 120,
    int ProtectionQuota = 2);

public sealed record AdaptiveConfiguration(
    bool Enabled, decimal SurvivalLow, decimal SurvivalHigh, decimal NoiseLow, decimal NoiseHigh,
    int MinimumSurvivalSamples, int MinimumNoiseSamples, bool RequireFullRoster,
    AdaptiveConfigRef? Fixed, AdaptiveConfigRef? Relief, AdaptiveConfigRef? Increase,
    Dictionary<string, AdaptiveParameter> Parameters, AdaptivePilotPolicy Pilot);

public sealed record PublishAdaptiveConfiguration(
    [property: Required, StringLength(80)] string Compatibility,
    string? ExpectedVersion,
    [property: Required] AdaptiveConfiguration Configuration);

public sealed record AdaptiveSample(
    [property: Range(0, 100)] decimal? Survival,
    [property: Range(0, 100)] decimal? Noise,
    [property: Range(0, int.MaxValue)] int SurvivalSamples,
    [property: Range(0, int.MaxValue)] int NoiseSamples);

public sealed record PreviewAdaptiveConfiguration(
    [property: Required, StringLength(80)] string Compatibility,
    [property: Required] AdaptiveConfiguration Configuration,
    [property: Required, MinLength(1), MaxLength(4)] AdaptiveSample[] Players);
