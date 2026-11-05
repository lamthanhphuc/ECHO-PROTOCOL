using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Enums;

namespace EchoProtocol.Api.Services;

public static class AEDPreMatchPolicyV1
{
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

        if (survival < survivalLow)
            return "AED-PRE-010-SURVIVAL-LOW-RELIEVE";

        if (noise < noiseLow)
            return "AED-PRE-020-NOISE-LOW-RELIEVE";

        if (survival >= survivalHigh && noise >= noiseHigh)
            return "AED-PRE-030-BOTH-HIGH-INCREASE";

        return "AED-PRE-040-MIXED-HOLD";
    }
}
