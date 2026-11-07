using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Enums;

namespace EchoProtocol.Api.Services;

public static class AEDPreMatchPolicyV1
{
    public const string SurvivalRelief = "AED-PRE-010-SURVIVAL-LOW-RELIEVE";
    public const string NoiseRelief = "AED-PRE-020-NOISE-LOW-RELIEVE";
    public const string Increase = "AED-PRE-030-BOTH-HIGH-INCREASE";
    public const string Hold = "AED-PRE-040-MIXED-HOLD";

    public static string? SelectRule(
        AdaptiveInputSnapshot snapshot,
        decimal survivalLow,
        decimal survivalHigh,
        decimal noiseLow,
        decimal noiseHigh)
    {
        if (snapshot == null ||
            snapshot.Validity != AdaptiveSnapshotValidity.Valid ||
            snapshot.TeamSize < 1 ||
            snapshot.SurvivalAggregationStatus != "AVAILABLE" ||
            snapshot.NoiseAggregationStatus != "AVAILABLE" ||
            snapshot.SurvivalObservedActiveCount != snapshot.TeamSize ||
            snapshot.NoiseObservedActiveCount != snapshot.TeamSize ||
            snapshot.SurvivalMeanObservedScore is not decimal survival ||
            snapshot.NoiseMeanObservedScore is not decimal noise ||
            survival is < 0 or > 100 ||
            noise is < 0 or > 100 ||
            survivalLow <= 0 ||
            survivalLow >= survivalHigh ||
            survivalHigh >= 100 ||
            noiseLow <= 0 ||
            noiseLow >= noiseHigh ||
            noiseHigh >= 100)
        {
            return null;
        }

        return SelectRule(survival, noise, survivalLow, survivalHigh, noiseLow, noiseHigh);
    }

    public static string SelectRule(decimal survival, decimal noise, decimal survivalLow, decimal survivalHigh,
        decimal noiseLow, decimal noiseHigh) => survival < survivalLow ? SurvivalRelief
        : noise < noiseLow ? NoiseRelief : survival >= survivalHigh && noise >= noiseHigh ? Increase : Hold;
}
