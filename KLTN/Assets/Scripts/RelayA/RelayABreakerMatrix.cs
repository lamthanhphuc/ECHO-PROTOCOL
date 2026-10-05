using System;

namespace EchoProtocol.RelayA
{
    public enum RelayABreakerPhase : byte { Editing, Pulsing, Balancing, Complete }

    public readonly struct RelayABreakerPattern
    {
        public RelayABreakerPattern(int size, uint locked, int[] presses)
        {
            Size = size;
            Locked = locked;
            uint red = 0;
            foreach (int cell in presses)
            {
                if (cell < 0 || cell >= size * size || (locked & (1u << cell)) != 0)
                    throw new ArgumentException("Authored presses must be valid, unlocked breakers.");
                red ^= RelayABreakerMatrix.ToggleMask(size, cell);
            }
            InitialRed = red;
        }
        public int Size { get; }
        public uint Locked { get; }
        public uint InitialRed { get; }
    }

    public readonly struct RelayABreakerSnapshot
    {
        public RelayABreakerSnapshot(RelayABreakerPattern pattern, int patternIndex, uint red,
            int moves, RelayABreakerPhase phase, uint pulse, int sequence)
        {
            Pattern = pattern;
            PatternIndex = patternIndex;
            Red = red;
            Moves = moves;
            Phase = phase;
            Pulse = pulse;
            Sequence = sequence;
        }
        public RelayABreakerPattern Pattern { get; }
        public int PatternIndex { get; }
        public uint Red { get; }
        public int Moves { get; }
        public RelayABreakerPhase Phase { get; }
        public uint Pulse { get; }
        public int Sequence { get; }
        public int StableCount => Pattern.Size * Pattern.Size - RelayABreakerMatrix.CountBits(Red);
    }

    public sealed class RelayABreakerMatrix
    {
        public const float PulseSeconds = 0.16f;
        public const float BalanceSeconds = 0.8f;
        private RelayABreakerPattern _pattern;
        private int _patternIndex;
        private bool _hard;
        private uint _red;
        private uint _pulse;
        private int _moves;
        private int _sequence;
        private float _elapsed;
        private RelayABreakerPhase _phase;
        public event Action Changed;
        public event Action Completed;
        public RelayABreakerSnapshot Snapshot => new RelayABreakerSnapshot(_pattern, _patternIndex,
            _red, _moves, _phase, _pulse, _sequence);

        public static RelayABreakerPattern GetPattern(bool hard, int index)
        {
            if (index < 0 || index > 2) throw new ArgumentOutOfRangeException(nameof(index));
            int[][] easy = { new[] { 0, 2, 5, 7, 9, 14 }, new[] { 1, 3, 4, 6, 10, 12, 15 }, new[] { 0, 3, 5, 6, 8, 11, 13, 14 } };
            int[][] difficult = { new[] { 0, 2, 4, 6, 8, 10, 14, 17, 20, 24 },
                new[] { 1, 3, 5, 7, 9, 11, 13, 16, 18, 21, 23 },
                new[] { 0, 3, 5, 8, 10, 11, 14, 16, 17, 19, 22, 24 } };
            return new RelayABreakerPattern(hard ? 5 : 4, hard ? 1u << 12 : 0u,
                hard ? difficult[index] : easy[index]);
        }

        public void Initialize(bool hard, int seed)
        {
            _hard = hard;
            _patternIndex = new Random(seed).Next(0, 3);
            _pattern = GetPattern(hard, _patternIndex);
            _sequence = 0;
            RestoreInitial();
        }

        public bool Press(int cell)
        {
            if (_phase != RelayABreakerPhase.Editing || cell < 0 || cell >= _pattern.Size * _pattern.Size
                || (_pattern.Locked & (1u << cell)) != 0) return false;
            _pulse = ToggleMask(_pattern.Size, cell);
            _phase = RelayABreakerPhase.Pulsing;
            _elapsed = 0f;
            _sequence++;
            Changed?.Invoke();
            return true;
        }

        public bool Reset()
        {
            if (_phase != RelayABreakerPhase.Editing) return false;
            _sequence++;
            RestoreInitial();
            return true;
        }

        private void RestoreInitial()
        {
            _red = _pattern.InitialRed;
            _moves = 0;
            _pulse = 0;
            _elapsed = 0f;
            _phase = RelayABreakerPhase.Editing;
            Changed?.Invoke();
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f || !float.IsFinite(deltaTime)) return;
            if (_phase != RelayABreakerPhase.Pulsing && _phase != RelayABreakerPhase.Balancing) return;
            _elapsed += deltaTime;
            if (_phase == RelayABreakerPhase.Pulsing && _elapsed >= PulseSeconds)
            {
                _red ^= _pulse;
                _moves++;
                _elapsed = 0f;
                _phase = _red == 0 ? RelayABreakerPhase.Balancing : RelayABreakerPhase.Editing;
                _pulse = _red == 0 ? (1u << (_pattern.Size * _pattern.Size)) - 1u : 0;
                Changed?.Invoke();
            }
            else if (_phase == RelayABreakerPhase.Balancing && _elapsed >= BalanceSeconds)
            {
                _phase = RelayABreakerPhase.Complete;
                _pulse = 0;
                Changed?.Invoke();
                Completed?.Invoke();
            }
        }

        public void ApplyAuthoritative(bool hard, int patternIndex, uint red, int moves,
            RelayABreakerPhase phase, uint pulse, int sequence)
        {
            if (_hard == hard && _patternIndex == patternIndex && _red == red && _moves == moves
                && _phase == phase && _pulse == pulse && _sequence == sequence) return;
            _hard = hard;
            _patternIndex = patternIndex;
            _pattern = GetPattern(hard, patternIndex);
            _red = red & ((1u << (_pattern.Size * _pattern.Size)) - 1u);
            _moves = moves;
            _phase = phase;
            _pulse = pulse;
            _sequence = sequence;
            Changed?.Invoke();
        }

        public static uint ToggleMask(int size, int cell)
        {
            if (size < 1 || size > 5 || cell < 0 || cell >= size * size)
                throw new ArgumentOutOfRangeException(nameof(cell));
            uint mask = 1u << cell;
            int x = cell % size;
            int y = cell / size;
            if (x > 0) mask |= 1u << (cell - 1);
            if (x + 1 < size) mask |= 1u << (cell + 1);
            if (y > 0) mask |= 1u << (cell - size);
            if (y + 1 < size) mask |= 1u << (cell + size);
            return mask;
        }

        public static int CountBits(uint bits)
        {
            int count = 0;
            while (bits != 0) { bits &= bits - 1; count++; }
            return count;
        }
    }
}
