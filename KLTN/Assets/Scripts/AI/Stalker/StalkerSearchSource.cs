namespace EchoProtocol.AI.Stalker
{
    /// <summary>
    /// Describes why a Stalker search episode exists.
    ///
    /// VisualTargetLoss retains player-target semantics.
    /// HeardNoise is only a positional hypothesis and must not
    /// manufacture or require a PlayerId.
    /// </summary>
    public enum StalkerSearchSource
    {
        VisualTargetLoss = 0,
        HeardNoise = 1
    }
}
