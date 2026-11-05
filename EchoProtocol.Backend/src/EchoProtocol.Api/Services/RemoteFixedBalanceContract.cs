using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EchoProtocol.Api.Services;

public static class RemoteFixedBalanceContract
{
    public const string ProtocolVersion = "RFB-1";
    public const string Standard = "STANDARD";
    public const string TestOnly = "TEST_ONLY";

    public static byte[] GetBytes(string json) =>
        new UTF8Encoding(false, true).GetBytes(json);

    public static string ComputeHash(string json) =>
        Convert.ToHexString(SHA256.HashData(GetBytes(json))).ToLowerInvariant();

    public static bool IsValidSnapshot(string? json)
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
                root.EnumerateObject().Count() != 4 ||
                !root.TryGetProperty("schemaVersion", out var schema) ||
                schema.ValueKind != JsonValueKind.String ||
                schema.GetString() != ProtocolVersion)
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
