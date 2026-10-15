using System.Text.Json;
using System.Text.Json.Serialization;
using EchoProtocol.AI.Common.AED;
using EchoProtocol.Api.DTOs.Telemetry;

namespace EchoProtocol.Api.Services;

internal static class TelemetryV13AEDValidator
{
    private static readonly JsonSerializerOptions Json = new() { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    private static readonly string[] ContextFields = ["eventSequence", "authorityTick", "scenarioConfigVersion", "policyVersion", "configSource", "occurrenceKey", "phase", "researchCaptureEnabled"];
    public static bool IsSourceEvent(string type) => type is "AED_V02_WORLD_SAMPLE" or "AED_V02_ACTION_EFFECT" or "AED_V02_TOOL_CARRIER" or "AED_V02_CABINET_CLUE";
    public static bool TryValidate(TelemetryEventRequest item, bool? captureAllowed,
        out TelemetryValidationResult result, out string reason)
    {
        result = new(0, false, null, false); reason = "AED_V02_SOURCE_INVALID";
        if (!IsSourceEvent(item.EventType) || captureAllowed != true || item.UserId.HasValue || item.ReasonCode != null ||
            item.ValueJson.ValueKind != JsonValueKind.Object || !item.ValueJson.TryGetProperty("context", out var context) ||
            !item.ValueJson.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object || context.ValueKind != JsonValueKind.Object ||
            !Fields(item.ValueJson, ["context", "data"]) || !Fields(context, ContextFields) ||
            !Fields(data, [item.EventType == "AED_V02_WORLD_SAMPLE" ? "world" : item.EventType == "AED_V02_ACTION_EFFECT" ? "effect" : item.EventType == "AED_V02_TOOL_CARRIER" ? "carrier" : "clue"]) ||
            !context.TryGetProperty("eventSequence", out var sequence) || !sequence.TryGetInt64(out var seq) || seq <= 0 ||
            !context.TryGetProperty("authorityTick", out var tick) || !tick.TryGetInt64(out var authorityTick) || authorityTick < 0 ||
            !Text(context, "occurrenceKey", out _) || !Text(context, "phase", out var phase) ||
            !Text(context, "scenarioConfigVersion", out _) || !Text(context, "policyVersion", out _) ||
            !context.TryGetProperty("researchCaptureEnabled", out var research) || research.ValueKind != JsonValueKind.True ||
            !Text(context, "configSource", out var source) || source is not ("ADAPTIVE" or "FIXED")) return false;
        try
        {
            if (item.EventType == "AED_V02_WORLD_SAMPLE")
            {
                if (!data.TryGetProperty("world", out var world)) return false;
                if (!Complete<AEDV02WorldSample>(world) ||
                    !CompleteArray<AEDV02PlayerSource>(world, "Players") || !CompleteArray<AEDV02ThreatSource>(world, "Stalkers") ||
                    !CompleteArray<AEDV02ThreatSource>(world, "Minions") || !CompleteArray<AEDV02ToolSource>(world, "Tools") ||
                    !CompleteArray<AEDV02DoorSource>(world, "Doors")) return false;
                if (world.GetProperty("Supply").ValueKind != JsonValueKind.Null &&
                    (!Complete<AEDV02SupplySource>(world.GetProperty("Supply")) ||
                     !CompleteArray<AEDV02SupplyReservation>(world.GetProperty("Supply"), "Reservations"))) return false;
                var sample = JsonSerializer.Deserialize<AEDV02WorldSample>(world.GetRawText(), Json);
                if (sample == null || !sample.IsValid() || sample.Tick != authorityTick || sample.PhaseName != phase) return false;
            }
            else if (item.EventType == "AED_V02_ACTION_EFFECT")
            {
                if (!data.TryGetProperty("effect", out var effect)) return false;
                if (!Complete<AEDV02EffectSource>(effect)) return false;
                var receipt = JsonSerializer.Deserialize<AEDV02EffectSource>(effect.GetRawText(), Json);
                if (receipt == null || !receipt.IsValid() || receipt.ObservedTick > authorityTick) return false;
            }
            else if (item.EventType == "AED_V02_TOOL_CARRIER")
            {
                if (!data.TryGetProperty("carrier", out var carrier) || !Complete<AEDV02ToolCarrierSource>(carrier)) return false;
                var transition = JsonSerializer.Deserialize<AEDV02ToolCarrierSource>(carrier.GetRawText(), Json);
                if (transition == null || !transition.IsValid() || transition.Tick != authorityTick) return false;
            }
            else
            {
                if (!data.TryGetProperty("clue", out var clue) || !Complete<AEDV02CabinetClueSource>(clue)) return false;
                var observed = JsonSerializer.Deserialize<AEDV02CabinetClueSource>(clue.GetRawText(), Json);
                if (observed == null || !observed.IsValid() || observed.Tick != authorityTick) return false;
            }
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException) { return false; }
        result = new(seq, false, null, false); reason = string.Empty; return true;
    }
    private static bool Fields(JsonElement value, string[] allowed) => value.EnumerateObject().All(x => allowed.Contains(x.Name)) &&
        value.EnumerateObject().Select(x => x.Name).Distinct(StringComparer.Ordinal).Count() == value.EnumerateObject().Count();
    private static bool Complete<T>(JsonElement value) => value.ValueKind == JsonValueKind.Object &&
        value.EnumerateObject().Count() == typeof(T).GetProperties().Length &&
        typeof(T).GetProperties().Select(x => x.Name).ToHashSet(StringComparer.Ordinal).SetEquals(value.EnumerateObject().Select(x => x.Name));
    private static bool CompleteArray<T>(JsonElement value, string key) => value.TryGetProperty(key, out var array) &&
        array.ValueKind == JsonValueKind.Array && array.EnumerateArray().All(Complete<T>);
    private static bool Text(JsonElement parent, string key, out string value)
    {
        value = string.Empty;
        return parent.TryGetProperty(key, out var element) && element.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(value = element.GetString() ?? string.Empty) && value.Length <= 128;
    }
}
