using System.Text.Json;
using EchoProtocol.AI.Common.AED;
using EchoProtocol.Api.Data;
using EchoProtocol.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace EchoProtocol.Api.Services;

public static class AEDV02SceneRegistry
{
    public static async Task RegisterPilotAsync(AppDbContext db, string json, DateTime now, CancellationToken ct = default)
    {
        var manifest = JsonSerializer.Deserialize<AEDV02SceneManifest>(json);
        if (manifest == null || !manifest.IsValid() || manifest.NavCells.Length == 0) throw new InvalidOperationException("AED_V02_SCENE_CONTENT_INVALID");
        var fingerprint = AEDV02Plan.Hash(json);
        if (await db.AEDV02SceneContents.AnyAsync(x => x.Fingerprint == fingerprint, ct)) return;
        db.AEDV02SceneContents.Add(new AEDV02SceneContent { Fingerprint = fingerprint, SceneVersion = manifest.SceneVersion, ManifestJson = json, RegisteredAtUtc = now });
        await db.SaveChangesAsync(ct);
    }
}
