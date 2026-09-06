namespace EchoProtocol.AI.Stalker.Spatial
{
    public enum RegionSemanticZone
    {
        Unknown = 0,
        Zone01 = 1,
        Zone02 = 2,
        Zone03 = 3,
        Isolated = 4
    }

    public enum RegionSemanticKind
    {
        Unknown = 0,
        Room = 1,
        Route = 2,
        IsolatedIsland = 3
    }

    public readonly struct RegionSemanticMetadata
    {
        public static readonly RegionSemanticMetadata None = new RegionSemanticMetadata(
            false,
            -1,
            string.Empty,
            RegionSemanticZone.Unknown,
            RegionSemanticKind.Unknown);

        public RegionSemanticMetadata(
            int sourceIndex,
            string sourcePath,
            RegionSemanticZone zone,
            RegionSemanticKind kind)
            : this(true, sourceIndex, sourcePath, zone, kind)
        {
        }

        private RegionSemanticMetadata(
            bool hasMetadata,
            int sourceIndex,
            string sourcePath,
            RegionSemanticZone zone,
            RegionSemanticKind kind)
        {
            HasMetadata = hasMetadata;
            SourceIndex = sourceIndex;
            SourcePath = sourcePath ?? string.Empty;
            Zone = hasMetadata ? zone : RegionSemanticZone.Unknown;
            Kind = hasMetadata ? kind : RegionSemanticKind.Unknown;
        }

        public bool HasMetadata { get; }
        public bool IsValid => HasMetadata && Zone != RegionSemanticZone.Unknown && Kind != RegionSemanticKind.Unknown;
        public int SourceIndex { get; }
        public string SourcePath { get; }
        public RegionSemanticZone Zone { get; }
        public RegionSemanticKind Kind { get; }
    }
}
