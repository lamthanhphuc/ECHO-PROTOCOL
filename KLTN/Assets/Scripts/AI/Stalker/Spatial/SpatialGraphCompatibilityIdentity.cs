using System;

namespace EchoProtocol.AI.Stalker.Spatial
{
    [Serializable]
    public readonly struct SpatialGraphCompatibilityIdentity : IEquatable<SpatialGraphCompatibilityIdentity>, IComparable<SpatialGraphCompatibilityIdentity>
    {
        private readonly ulong _value;

        public SpatialGraphCompatibilityIdentity(ulong value)
        {
            _value = value;
        }

        public static SpatialGraphCompatibilityIdentity Invalid => default;
        public bool IsValid => _value != 0UL;
        public ulong Value => _value;
        public int CompareTo(SpatialGraphCompatibilityIdentity other) => _value.CompareTo(other._value);
        public bool Equals(SpatialGraphCompatibilityIdentity other) => _value == other._value;
        public override bool Equals(object obj) => obj is SpatialGraphCompatibilityIdentity other && Equals(other);
        public override int GetHashCode() => _value.GetHashCode();
        public override string ToString() => IsValid ? $"SpatialGraphCompatibilityIdentity(0x{_value:X16})" : "SpatialGraphCompatibilityIdentity.Invalid";
        public static bool operator ==(SpatialGraphCompatibilityIdentity left, SpatialGraphCompatibilityIdentity right) => left.Equals(right);
        public static bool operator !=(SpatialGraphCompatibilityIdentity left, SpatialGraphCompatibilityIdentity right) => !left.Equals(right);
    }
}
