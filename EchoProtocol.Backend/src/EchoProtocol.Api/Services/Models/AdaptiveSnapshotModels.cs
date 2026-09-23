using EchoProtocol.Api.Entities;

namespace EchoProtocol.Api.Services.Models;

public sealed record AdaptiveSnapshotBuildResult(
    AdaptiveInputSnapshot Snapshot,
    IReadOnlyList<string> ReasonCodes);
