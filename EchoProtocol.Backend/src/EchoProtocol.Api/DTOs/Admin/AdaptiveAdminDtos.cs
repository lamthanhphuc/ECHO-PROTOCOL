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
    [Required, StringLength(80)] string Compatibility,
    string? ExpectedVersion,
    [Required] AdaptiveConfiguration Configuration,
    [Required, StringLength(64)] string PreviewFingerprint);

public sealed record AdaptiveSample(
    [Range(0, 100)] decimal? Survival,
    [Range(0, 100)] decimal? Noise,
    [Range(0, int.MaxValue)] int SurvivalSamples,
    [Range(0, int.MaxValue)] int NoiseSamples);

public sealed record PreviewAdaptiveConfiguration(
    [Required, StringLength(80)] string Compatibility,
    [Required] AdaptiveConfiguration Configuration,
    [Required, MinLength(1), MaxLength(4)] AdaptiveSample[] Players);
