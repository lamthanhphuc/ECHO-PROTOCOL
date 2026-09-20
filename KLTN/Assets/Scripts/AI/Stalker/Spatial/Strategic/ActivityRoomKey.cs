using System;
using EchoProtocol.AI.Stalker.Spatial;

namespace EchoProtocol.AI.Stalker.Spatial.Strategic
{
    public readonly struct ActivityRoomKey :
        IEquatable<ActivityRoomKey>,
        IComparable<ActivityRoomKey>
    {
        public ActivityRoomKey(
            RegionSemanticZone zone,
            RegionSemanticKind kind,
            string sourcePath)
        {
            Zone = zone;
            Kind = kind;
            SourcePath = sourcePath ?? string.Empty;
        }

        public static ActivityRoomKey Invalid => default;
        public RegionSemanticZone Zone { get; }
        public RegionSemanticKind Kind { get; }
        public string SourcePath { get; }
        public bool IsValid =>
            Zone != RegionSemanticZone.Unknown
            && Kind != RegionSemanticKind.Unknown
            && !string.IsNullOrWhiteSpace(SourcePath);
        public bool IsRoom => Kind == RegionSemanticKind.Room;

        public static bool TryCreate(
            RegionSemanticMetadata metadata,
            out ActivityRoomKey key)
        {
            key = metadata.IsValid
                ? new ActivityRoomKey(metadata.Zone, metadata.Kind, metadata.SourcePath)
                : Invalid;
            return key.IsValid;
        }

        public int CompareTo(ActivityRoomKey other)
        {
            var comparison = Zone.CompareTo(other.Zone);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = Kind.CompareTo(other.Kind);
            return comparison != 0
                ? comparison
                : string.Compare(SourcePath, other.SourcePath, StringComparison.Ordinal);
        }

        public bool Equals(ActivityRoomKey other) =>
            Zone == other.Zone
            && Kind == other.Kind
            && string.Equals(SourcePath, other.SourcePath, StringComparison.Ordinal);

        public override bool Equals(object obj) =>
            obj is ActivityRoomKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = (hash * 31) + (int)Zone;
                hash = (hash * 31) + (int)Kind;
                hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(SourcePath ?? string.Empty);
                return hash;
            }
        }

        public override string ToString() =>
            IsValid ? $"{Zone}|{Kind}|{SourcePath}" : "ActivityRoomKey.Invalid";

        public static bool operator ==(ActivityRoomKey left, ActivityRoomKey right) => left.Equals(right);
        public static bool operator !=(ActivityRoomKey left, ActivityRoomKey right) => !left.Equals(right);
    }
}
