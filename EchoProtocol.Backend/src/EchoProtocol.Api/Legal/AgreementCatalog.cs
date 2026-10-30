using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EchoProtocol.Api.Legal;

public static class AgreementCatalog
{
    private static readonly byte[] Bytes = Read();
    public static readonly string Hash = Convert.ToHexString(SHA256.HashData(Bytes)).ToLowerInvariant();
    public static readonly JsonElement Bundle = JsonSerializer.Deserialize<JsonElement>(Bytes);
    public static string Version => Bundle.GetProperty("bundleVersion").GetString()!;
    public static string DocumentVersions => string.Join(";", Bundle.GetProperty("documents").EnumerateArray()
        .Select(doc => $"{doc.GetProperty("id").GetString()}={doc.GetProperty("version").GetString()}"));
    private static byte[] Read()
    {
        using var stream = typeof(AgreementCatalog).Assembly.GetManifestResourceStream("EchoProtocol.Api.Legal.policies.json")
            ?? throw new InvalidOperationException("Policy bundle missing.");
        using var output = new MemoryStream();
        stream.CopyTo(output);
        return Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(output.ToArray()).TrimStart('\uFEFF').Replace("\r\n", "\n"));
    }
}
