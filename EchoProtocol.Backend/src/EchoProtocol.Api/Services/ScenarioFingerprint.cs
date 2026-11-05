using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using EchoProtocol.Api.Entities;

namespace EchoProtocol.Api.Services;

public static class ScenarioFingerprint
{
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
}
