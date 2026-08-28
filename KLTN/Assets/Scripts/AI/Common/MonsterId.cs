using System;

namespace EchoProtocol.AI.Common
{
    public readonly struct MonsterId : IEquatable<MonsterId>, IComparable<MonsterId>
    {
        private readonly int _value;

        public MonsterId(int value)
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "MonsterId value must be greater than zero.");
            }

            _value = value;
        }

        public static MonsterId Invalid => default;

        public bool IsValid => _value > 0;

        public int Value => _value;

        public int CompareTo(MonsterId other)
        {
            return _value.CompareTo(other._value);
        }

        public bool Equals(MonsterId other)
        {
            return _value == other._value;
        }

        public override bool Equals(object obj)
        {
            return obj is MonsterId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return _value;
        }

        public override string ToString()
        {
            return IsValid ? $"MonsterId({_value})" : "MonsterId.Invalid";
        }

        public static bool operator ==(MonsterId left, MonsterId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(MonsterId left, MonsterId right)
        {
            return !left.Equals(right);
        }
    }
}
