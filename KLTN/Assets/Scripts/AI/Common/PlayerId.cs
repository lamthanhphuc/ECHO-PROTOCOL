using System;

namespace EchoProtocol.AI.Common
{
    public readonly struct PlayerId : IEquatable<PlayerId>, IComparable<PlayerId>
    {
        private readonly int _value;

        public PlayerId(int value)
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "PlayerId value must be greater than zero.");
            }

            _value = value;
        }

        public static PlayerId Invalid => default;

        public bool IsValid => _value > 0;

        public int Value => _value;

        public int CompareTo(PlayerId other)
        {
            return _value.CompareTo(other._value);
        }

        public bool Equals(PlayerId other)
        {
            return _value == other._value;
        }

        public override bool Equals(object obj)
        {
            return obj is PlayerId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return _value;
        }

        public override string ToString()
        {
            return IsValid ? $"PlayerId({_value})" : "PlayerId.Invalid";
        }

        public static bool operator ==(PlayerId left, PlayerId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(PlayerId left, PlayerId right)
        {
            return !left.Equals(right);
        }
    }
}
