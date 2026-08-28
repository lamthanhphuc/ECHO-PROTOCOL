using System;

namespace EchoProtocol.AI.Common
{
    public readonly struct AiSimulationTime : IEquatable<AiSimulationTime>, IComparable<AiSimulationTime>
    {
        private readonly bool _isValid;
        private readonly long _tick;
        private readonly double _seconds;

        public AiSimulationTime(long tick, double seconds)
        {
            if (tick < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tick), tick, "Simulation tick must be non-negative.");
            }

            if (seconds < 0d || double.IsNaN(seconds) || double.IsInfinity(seconds))
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "Simulation seconds must be finite and non-negative.");
            }

            _isValid = true;
            _tick = tick;
            _seconds = seconds;
        }

        public static AiSimulationTime Invalid => default;

        public bool IsValid => _isValid;

        public long Tick => _tick;

        public double Seconds => _seconds;

        public int CompareTo(AiSimulationTime other)
        {
            if (_isValid != other._isValid)
            {
                return _isValid ? 1 : -1;
            }

            var tickComparison = _tick.CompareTo(other._tick);
            return tickComparison != 0
                ? tickComparison
                : _seconds.CompareTo(other._seconds);
        }

        public bool Equals(AiSimulationTime other)
        {
            return _isValid == other._isValid
                && _tick == other._tick
                && _seconds.Equals(other._seconds);
        }

        public override bool Equals(object obj)
        {
            return obj is AiSimulationTime other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = (hash * 31) + _isValid.GetHashCode();
                hash = (hash * 31) + _tick.GetHashCode();
                hash = (hash * 31) + _seconds.GetHashCode();
                return hash;
            }
        }

        public override string ToString()
        {
            return IsValid ? $"AiSimulationTime(Tick={_tick}, Seconds={_seconds})" : "AiSimulationTime.Invalid";
        }

        public static bool operator ==(AiSimulationTime left, AiSimulationTime right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(AiSimulationTime left, AiSimulationTime right)
        {
            return !left.Equals(right);
        }
    }
}
