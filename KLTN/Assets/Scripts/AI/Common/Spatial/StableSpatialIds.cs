using System;

namespace EchoProtocol.AI.Common.Spatial
{
    public readonly struct GameplayZoneId : IEquatable<GameplayZoneId>, IComparable<GameplayZoneId>
    {
        private readonly int _value;

        public GameplayZoneId(int value)
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "GameplayZoneId value must be greater than zero.");
            }

            _value = value;
        }

        public static GameplayZoneId Invalid => default;
        public bool IsValid => _value > 0;
        public int Value => _value;
        public int CompareTo(GameplayZoneId other) => _value.CompareTo(other._value);
        public bool Equals(GameplayZoneId other) => _value == other._value;
        public override bool Equals(object obj) => obj is GameplayZoneId other && Equals(other);
        public override int GetHashCode() => _value;
        public override string ToString() => IsValid ? $"GameplayZoneId({_value})" : "GameplayZoneId.Invalid";
        public static bool operator ==(GameplayZoneId left, GameplayZoneId right) => left.Equals(right);
        public static bool operator !=(GameplayZoneId left, GameplayZoneId right) => !left.Equals(right);
    }

    public readonly struct RegionId : IEquatable<RegionId>, IComparable<RegionId>
    {
        private readonly int _value;

        public RegionId(int value)
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "RegionId value must be greater than zero.");
            }

            _value = value;
        }

        public static RegionId Invalid => default;
        public bool IsValid => _value > 0;
        public int Value => _value;
        public int CompareTo(RegionId other) => _value.CompareTo(other._value);
        public bool Equals(RegionId other) => _value == other._value;
        public override bool Equals(object obj) => obj is RegionId other && Equals(other);
        public override int GetHashCode() => _value;
        public override string ToString() => IsValid ? $"RegionId({_value})" : "RegionId.Invalid";
        public static bool operator ==(RegionId left, RegionId right) => left.Equals(right);
        public static bool operator !=(RegionId left, RegionId right) => !left.Equals(right);
    }

    public readonly struct DoorId : IEquatable<DoorId>, IComparable<DoorId>
    {
        private readonly int _value;

        public DoorId(int value)
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "DoorId value must be greater than zero.");
            }

            _value = value;
        }

        public static DoorId Invalid => default;
        public bool IsValid => _value > 0;
        public int Value => _value;
        public int CompareTo(DoorId other) => _value.CompareTo(other._value);
        public bool Equals(DoorId other) => _value == other._value;
        public override bool Equals(object obj) => obj is DoorId other && Equals(other);
        public override int GetHashCode() => _value;
        public override string ToString() => IsValid ? $"DoorId({_value})" : "DoorId.Invalid";
        public static bool operator ==(DoorId left, DoorId right) => left.Equals(right);
        public static bool operator !=(DoorId left, DoorId right) => !left.Equals(right);
    }
}
