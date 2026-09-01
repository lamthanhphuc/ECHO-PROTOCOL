using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EchoProtocol.Api.Configurations;
using EchoProtocol.Api.Services.Interfaces;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace EchoProtocol.Api.Services;

public sealed class MatchJoinProofService : IMatchJoinProofService
{
    private readonly byte[] _signingKey;
    private readonly TimeProvider _timeProvider;

    public MatchJoinProofService(
        IOptions<MatchAuthoritySettings> settings,
        TimeProvider timeProvider)
    {
        _signingKey = Encoding.UTF8.GetBytes(settings.Value.ProofSigningKey);
        _timeProvider = timeProvider;
    }

    public string Issue(MatchJoinProofPayload payload)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(payload);
        var encodedPayload = WebEncoders.Base64UrlEncode(json);
        var signature = HMACSHA256.HashData(_signingKey, Encoding.ASCII.GetBytes(encodedPayload));
        return $"{encodedPayload}.{WebEncoders.Base64UrlEncode(signature)}";
    }

    public bool TryValidate(string proof, out MatchJoinProofPayload? payload)
    {
        payload = null;
        if (string.IsNullOrWhiteSpace(proof))
        {
            return false;
        }

        var parts = proof.Split('.');
        if (parts.Length != 2)
        {
            return false;
        }

        try
        {
            var expected = HMACSHA256.HashData(_signingKey, Encoding.ASCII.GetBytes(parts[0]));
            var supplied = WebEncoders.Base64UrlDecode(parts[1]);
            if (!CryptographicOperations.FixedTimeEquals(expected, supplied))
            {
                return false;
            }

            payload = JsonSerializer.Deserialize<MatchJoinProofPayload>(
                WebEncoders.Base64UrlDecode(parts[0]));
            return payload is not null
                && payload.ProofId != Guid.Empty
                && payload.MatchId != Guid.Empty
                && payload.UserId != Guid.Empty
                && payload.FusionActorNumber is >= 1 and <= 4
                && payload.ExpiresAtUtc > _timeProvider.GetUtcNow().UtcDateTime;
        }
        catch (Exception)
        {
            payload = null;
            return false;
        }
    }
}
