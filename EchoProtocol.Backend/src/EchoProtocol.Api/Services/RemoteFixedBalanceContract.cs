using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EchoProtocol.AI.Common.AED;

namespace EchoProtocol.Api.Services;

public static class RemoteFixedBalanceContract
{
    public const string ProtocolVersion = "RFB-1";
    public const int MinionAttackDamage = 10;
    public const string GameplaySchema = "RFB-2";
    public const string Standard = "STANDARD";
    public const string TestOnly = "TEST_ONLY";

    public static byte[] GetBytes(string json) =>
        new UTF8Encoding(false, true).GetBytes(json);

    public static string ComputeHash(string json) =>
        Convert.ToHexString(SHA256.HashData(GetBytes(json))).ToLowerInvariant();

    public static bool UsesGameplayTuning(string validatedJson)
    {
        using var document = JsonDocument.Parse(validatedJson);
        return document.RootElement.GetProperty("schemaVersion").GetString() == GameplaySchema;
    }

    public static string? GameVersion(string validatedJson)
    {
        using var document = JsonDocument.Parse(validatedJson);
        return document.RootElement.TryGetProperty("content", out var content) ? content.GetProperty("gameVersion").GetString() : null;
    }

    public static (double Patrol, double[] Values) AdaptiveBasis(string? validatedJson, string? gameVersion)
    {
        var values = AEDV02Plan.Normal().ToArray().Concat(new double[1]).ToArray();
        values[(int)AEDV02Key.Zone1MinionCap] = 3;
        values[(int)AEDV02Key.Zone2MinionCap] = 5;
        values[(int)AEDV02Key.Zone3AdaptiveMinionCap] = gameVersion == "0.1.1" ? 1 : 0;
        if (validatedJson is null) return (6.5, values);
        using var document = JsonDocument.Parse(validatedJson);
        var root = document.RootElement;
        var stalker = root.GetProperty("stalker");
        values[(int)AEDV02Key.ChaseSpeed] = stalker.GetProperty("chaseSpeed").GetSingle();
        if (root.TryGetProperty("gameplay", out var gameplay))
        {
            var fields = gameplay.EnumerateArray().ToDictionary(x => x.GetProperty("key").GetString()!, x => x.GetProperty("value").GetDouble());
            var toolsPerZone = fields.GetValueOrDefault("toolsPerZone", 5);
            for (int zone = 1; zone <= 3; zone++)
                if (fields.GetValueOrDefault($"zone{zone}ToolBudget", toolsPerZone) == 6)
                    values[12] += 1 << (zone - 1);
            values[(int)AEDV02Key.OptionalToolBudget] = AEDV02Plan.OptionalCount((int)values[12]);
            foreach (var entry in gameplay.EnumerateArray())
            {
                int index = entry.GetProperty("key").GetString() switch
                {
                    "stalkerDetectSeconds" => (int)AEDV02Key.DetectionAcquireSeconds,
                    "stalkerHearing" => (int)AEDV02Key.HearingMultiplier,
                    "stalkerSearchSeconds" => (int)AEDV02Key.SearchSeconds,
                    "stalkerSpecialCooldown" => (int)AEDV02Key.SpecialCooldownSeconds,
                    "minionCapZone1" => (int)AEDV02Key.Zone1MinionCap,
                    "minionCapZone2" => (int)AEDV02Key.Zone2MinionCap,
                    "minionCapZone3" => (int)AEDV02Key.Zone3AdaptiveMinionCap,
                    _ => -1
                };
                if (index >= 0) values[index] = entry.GetProperty("value").GetSingle();
            }
        }
        return (stalker.GetProperty("patrolSpeed").GetSingle(), values);
    }

    public static bool IsValidSnapshot(string? json, string? map = null, bool allowRetiredFields = true)
    {
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            if (GetBytes(json).Length > 8192)
                return false;

            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("schemaVersion", out var schema) ||
                schema.ValueKind != JsonValueKind.String ||
                schema.GetString() is not (ProtocolVersion or GameplaySchema))
                return false;

            bool extended = schema.GetString() == GameplaySchema;
            bool hasContent = root.TryGetProperty("content", out var content);
            int count = extended ? (hasContent ? 6 : 5) : 4;
            if (root.EnumerateObject().Count() != count ||
                root.EnumerateObject().Select(x => x.Name).Distinct(StringComparer.Ordinal).Count() != count ||
                (map == "Scene_B" && !hasContent) ||
                (hasContent && (!extended || !ValidContent(content, map))) ||
                (extended && !ValidGameplay(root, map, hasContent, allowRetiredFields)))
                return false;

            if (!ValidSection(root, "player",
                    ("maxHealth", 1, 500),
                    ("walkSpeed", 0.5m, 15),
                    ("sprintSpeed", 0.5m, 25),
                    ("maxStamina", 1, 500)) ||
                !ValidSection(root, "stalker",
                    ("patrolSpeed", 0.5m, 20),
                    ("chaseSpeed", 0.5m, 25)) ||
                !ValidSection(root, "minion",
                    ("roamSpeed", 0.5m, 20),
                    ("trackSpeed", 0.5m, 25),
                    ("attackDamage", 1, 100)))
                return false;

            if (!allowRetiredFields && root.GetProperty("minion").GetProperty("attackDamage").GetDecimal() != MinionAttackDamage)
                return false;

            var player = root.GetProperty("player");
            var stalker = root.GetProperty("stalker");
            return player.GetProperty("sprintSpeed").GetDecimal() >=
                       player.GetProperty("walkSpeed").GetDecimal() &&
                   stalker.GetProperty("chaseSpeed").GetDecimal() >=
                       stalker.GetProperty("patrolSpeed").GetDecimal();
        }
        catch (JsonException) { return false; }
        catch (FormatException) { return false; }
        catch (EncoderFallbackException) { return false; }
    }

    private static bool ValidContent(JsonElement content, string? map)
    {
        if (content.ValueKind != JsonValueKind.Object || content.EnumerateObject().Count() != 4) return false;
        string? Read(string key) => content.TryGetProperty(key, out var field) && field.ValueKind == JsonValueKind.String ? field.GetString() : null;
        return Read("gameVersion") is { Length: > 0 and <= 80 } version && version.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_') &&
            MatchContentCatalog.Entries.Any(x => (map == null || x.Map == map) && x.Mode == Read("mode") &&
                x.Monster == Read("monster") && x.Route == Read("route"));
    }

    private static bool ValidGameplay(JsonElement root, string? map, bool hasContent, bool allowRetiredFields)
    {
        if (!root.TryGetProperty("gameplay", out var gameplay) || gameplay.ValueKind != JsonValueKind.Array ||
            (!hasContent && gameplay.GetArrayLength() == 0) || gameplay.GetArrayLength() > RemoteGameplayTuning.Fields.Length) return false;
        var keys = new HashSet<string>(StringComparer.Ordinal);
        decimal terminalHold = 60, relayWindow = 300;
        foreach (var entry in gameplay.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object || entry.EnumerateObject().Count() != 2 ||
                !entry.TryGetProperty("key", out var key) || key.ValueKind != JsonValueKind.String || key.GetString() is not { } name ||
                (!allowRetiredFields && RemoteGameplayTuning.IsRetired(name)) ||
                !RemoteGameplayTuning.ByKey.TryGetValue(name, out var field) || !keys.Add(name) ||
                (map != null && field.Map != null && field.Map != map &&
                    !(!hasContent && map == "Scene_A" && field.Map == "Scene_B")) ||
                !entry.TryGetProperty("value", out var value) || value.ValueKind != JsonValueKind.Number ||
                !value.TryGetDecimal(out var number) || number < field.Min || number > field.Max ||
                (field.Integral && number != decimal.Truncate(number))) return false;
            if (name == "terminalHoldSeconds") terminalHold = number;
            if (name == "relayRepairWindowSeconds") relayWindow = number;
        }
        return relayWindow >= terminalHold + 15;
    }

    private static bool ValidSection(
        JsonElement root,
        string name,
        params (string Name, decimal Min, decimal Max)[] fields)
    {
        if (!root.TryGetProperty(name, out var section) ||
            section.ValueKind != JsonValueKind.Object ||
            section.EnumerateObject().Count() != fields.Length)
            return false;

        foreach (var field in fields)
        {
            if (!section.TryGetProperty(field.Name, out var value) ||
                value.ValueKind != JsonValueKind.Number ||
                !value.TryGetDecimal(out var number) ||
                number < field.Min || number > field.Max)
                return false;
        }

        return true;
    }
}
