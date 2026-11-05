using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EchoProtocol.Api.Entities;

namespace EchoProtocol.Api.Services;

public static class ScenarioFingerprint
{
    private static string CanonicalJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        using var stream = new MemoryStream();

        using (var writer = new Utf8JsonWriter(stream))
        {
            WriteCanonicalJson(writer, document.RootElement);
            writer.Flush();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteCanonicalJson(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject()
                    .OrderBy(x => x.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonicalJson(writer, property.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                    WriteCanonicalJson(writer, item);
                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                writer.WriteStringValue(element.GetString());
                break;
            case JsonValueKind.Number:
                writer.WriteRawValue(element.GetDecimal().ToString(
                    "G29", CultureInfo.InvariantCulture));
                break;
            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;
            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;
            case JsonValueKind.Null:
                writer.WriteNullValue();
                break;
            default:
                throw new JsonException("Unsupported JSON value");
        }
    }

    private static string? Decimal6(decimal? value) => value.HasValue
        ? Math.Round(value.Value, 6, MidpointRounding.AwayFromZero)
            .ToString("F6", CultureInfo.InvariantCulture)
        : null;

    public static string Hash(params string?[] parts) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            string.Join("|", parts.Select(item => item ?? "<null>"))))).ToLowerInvariant();

    public static string Config(ScenarioConfigDefinition item) => Hash(
        item.ScenarioConfigId, item.ScenarioConfigVersion, item.SchemaVersion, item.PolicyVersion,
        item.ConfigSource.ToString().ToUpperInvariant(), item.MapId, item.MonsterType,
        item.ObjectiveSpawnSetId, item.SupportItemBudget.ToString(CultureInfo.InvariantCulture),
        item.DetectionFillRate.ToString("R", CultureInfo.InvariantCulture),
        item.DetectionDecayRate.ToString("R", CultureInfo.InvariantCulture),
        item.ChaseSpeed.ToString("R", CultureInfo.InvariantCulture),
        item.SearchDuration.ToString("R", CultureInfo.InvariantCulture), item.RouteModifier,
        item.EscapeDoorTimerSeconds.ToString("R", CultureInfo.InvariantCulture),
        item.FallbackConfigId, item.FallbackConfigVersion, item.ContentWhitelistVersion,
        item.UnityCompatibilityVersion);

    public static string Snapshot(AdaptiveInputSnapshot s) => s.FingerprintVersion == "V2"
        ? Hash(
            s.FingerprintVersion, s.MatchId.ToString("D"), s.DecisionPoint, s.RosterIdentity,
            s.TeamSize.ToString(CultureInfo.InvariantCulture),
            s.Validity.ToString().ToUpperInvariant(), CanonicalJson(s.ReasonCodesJson),
            s.ProfileFormulaSemanticId, s.SurvivalComparisonKey, s.NoiseComparisonKey,
            s.ObjectiveAggregationStatus, s.ObjectiveComparisonKey,
            Decimal6(s.ObjectiveMeanObservedScore),
            s.ObjectiveObservedActiveCount.ToString(CultureInfo.InvariantCulture),
            s.ToolUsageAggregationStatus, s.ToolUsageComparisonKey,
            Decimal6(s.ToolUsageMeanObservedScore),
            s.ToolUsageObservedActiveCount.ToString(CultureInfo.InvariantCulture),
            string.Join(";", s.Players.OrderBy(x => x.UserId).Select(x => string.Join(",",
                x.UserId.ToString("D"), x.ProfileRevision?.ToString(CultureInfo.InvariantCulture),
                x.SurvivalStatus, Decimal6(x.SurvivalScore), x.SurvivalSampleCount,
                x.NoiseStatus, Decimal6(x.NoiseScore), x.NoiseSampleCount,
                CanonicalJson(x.DeferredDimensionsJson)))))
        : Hash(
            s.MatchId.ToString("D"), s.DecisionPoint, s.RosterIdentity,
            s.TeamSize.ToString(CultureInfo.InvariantCulture),
            s.Validity.ToString().ToUpperInvariant(), s.ReasonCodesJson,
            s.ProfileFormulaSemanticId, s.SurvivalComparisonKey, s.NoiseComparisonKey,
            string.Join(";", s.Players.OrderBy(x => x.UserId).Select(x => string.Join(",",
                x.UserId.ToString("D"), x.ProfileRevision?.ToString(CultureInfo.InvariantCulture),
                x.SurvivalStatus, x.SurvivalScore?.ToString(CultureInfo.InvariantCulture), x.SurvivalSampleCount,
                x.NoiseStatus, x.NoiseScore?.ToString(CultureInfo.InvariantCulture), x.NoiseSampleCount))));
}
