using System;

namespace EchoProtocol.RelayB
{
    public enum RelayBDecodePhase : byte { Editing, Transmitting, Revealing, Holding, Failed, Solved, Complete }
    public enum RelayBCodeFeedback : byte { Unused, WrongPlace, Right }

    public readonly struct RelayBDecodeSnapshot
    {
        public RelayBDecodeSnapshot(int round, int attempts, int current, int feedback, RelayBDecodePhase phase,
            float elapsed, int[] codes, int[] results)
        {
            Round = round; Attempts = attempts; Current = current; Feedback = feedback; Phase = phase; Elapsed = elapsed;
            Codes = codes == null ? new int[5] : (int[])codes.Clone();
            Results = results == null ? new int[5] : (int[])results.Clone();
        }
        public int Round { get; }
        public int Attempts { get; }
        public int Current { get; }
        public int Feedback { get; }
        public RelayBDecodePhase Phase { get; }
        public float Elapsed { get; }
        public int[] Codes { get; }
        public int[] Results { get; }
        public bool IsComplete => Phase == RelayBDecodePhase.Complete;
        public bool CanEdit => Phase == RelayBDecodePhase.Editing;
        public int RevealedCount => Phase == RelayBDecodePhase.Transmitting || Phase == RelayBDecodePhase.Editing ? 0
            : Phase == RelayBDecodePhase.Revealing ? Math.Min(6, (int)(Elapsed / 0.12f) + 1) : 6;
        public int RightCount
        {
            get { int count = 0; for (int i = 0; i < 6; i++) if (RelayBDecoder.FeedbackAt(Feedback, i) == RelayBCodeFeedback.Right) count++; return count; }
        }
    }

    // Mastermind with distinct symbols: only the authority retains the secret.
    public sealed class RelayBDecoder
    {
        public const int CodeLength = 6;
        public const int MaxAttempts = 5;
        private Random _random;
        private int _secret;
        private int _round;
        private int _attempts;
        private int _current;
        private int _feedback;
        private readonly int[] _codes = new int[5];
        private readonly int[] _results = new int[5];
        private RelayBDecodePhase _phase;
        private float _elapsed;
        public event Action Failed;
        public event Action Completed;
        public bool IsComplete => _phase == RelayBDecodePhase.Complete;
        public bool CanEdit => _phase == RelayBDecodePhase.Editing;
        public RelayBDecodeSnapshot Snapshot => new RelayBDecodeSnapshot(_round, _attempts, _current, _feedback, _phase, _elapsed, _codes, _results);

        public void Initialize(int seed)
        {
            _random = new Random(seed);
            _round = 0;
            NewRound();
        }

        private void NewRound()
        {
            int previous = _secret;
            do
            {
                var bank = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
                for (int i = bank.Length - 1; i > 0; i--)
                {
                    int j = _random.Next(i + 1);
                    int value = bank[i]; bank[i] = bank[j]; bank[j] = value;
                }
                _secret = 0;
                for (int i = 0; i < 6; i++) _secret |= bank[i] << (i * 4);
            } while (_round > 0 && _secret == previous);
            _round++;
            _attempts = _current = _feedback = 0;
            Array.Clear(_codes, 0, 5); Array.Clear(_results, 0, 5);
            _phase = RelayBDecodePhase.Editing; _elapsed = 0f;
        }

        public bool Transmit(int packed)
        {
            if (_random == null || _phase != RelayBDecodePhase.Editing || !IsValidCode(packed) || _attempts >= MaxAttempts) return false;
            _current = packed;
            _feedback = Evaluate(_secret, packed);
            _attempts++;
            _phase = RelayBDecodePhase.Transmitting; _elapsed = 0f;
            return true;
        }

        public void Tick(float seconds)
        {
            if (_random == null || !float.IsFinite(seconds) || seconds <= 0f) return;
            // Carry excess time across presentation states so low frame rates do not change outcomes.
            while (seconds > 0f && _phase != RelayBDecodePhase.Editing && _phase != RelayBDecodePhase.Complete)
            {
                float duration = _phase == RelayBDecodePhase.Transmitting ? 0.9f
                    : _phase == RelayBDecodePhase.Revealing ? 0.72f
                    : _phase == RelayBDecodePhase.Holding ? 0.9f
                    : _phase == RelayBDecodePhase.Failed ? 1.8f : 0.8f;
                float step = Math.Min(seconds, Math.Max(0f, duration - _elapsed));
                _elapsed += step; seconds -= step;
                if (_elapsed + 0.00001f < duration) break;
                _elapsed = 0f;
                switch (_phase)
                {
                    case RelayBDecodePhase.Transmitting: _phase = RelayBDecodePhase.Revealing; break;
                    case RelayBDecodePhase.Revealing: _phase = RelayBDecodePhase.Holding; break;
                    case RelayBDecodePhase.Holding:
                        _codes[_attempts - 1] = _current; _results[_attempts - 1] = _feedback;
                        if (_current == _secret) _phase = RelayBDecodePhase.Solved;
                        else if (_attempts == MaxAttempts) { _phase = RelayBDecodePhase.Failed; Failed?.Invoke(); }
                        else { _phase = RelayBDecodePhase.Editing; _current = _feedback = 0; }
                        break;
                    case RelayBDecodePhase.Failed: NewRound(); break;
                    case RelayBDecodePhase.Solved: _phase = RelayBDecodePhase.Complete; Completed?.Invoke(); break;
                }
            }
        }

        public void ApplyAuthoritative(RelayBDecodeSnapshot state)
        {
            // A new authoritative repair attempt legitimately resets its round counter.
            _round = state.Round; _attempts = state.Attempts; _current = state.Current; _feedback = state.Feedback;
            _phase = state.Phase; _elapsed = state.Elapsed;
            for (int i = 0; i < 5; i++) { _codes[i] = state.Codes?[i] ?? 0; _results[i] = state.Results?[i] ?? 0; }
            // Replica application never emits completion or failure events.
        }

        public static int DigitAt(int code, int slot) => (code >> (slot * 4)) & 15;
        public static RelayBCodeFeedback FeedbackAt(int feedback, int slot) => (RelayBCodeFeedback)((feedback >> (slot * 2)) & 3);
        public static bool IsValidCode(int packed)
        {
            if ((packed & ~0xffffff) != 0) return false;
            int seen = 0;
            for (int i = 0; i < 6; i++)
            {
                int digit = DigitAt(packed, i);
                if (digit < 1 || digit > 9 || (seen & (1 << digit)) != 0) return false;
                seen |= 1 << digit;
            }
            return true;
        }
        public static int Evaluate(int secret, int guess)
        {
            if (!IsValidCode(secret) || !IsValidCode(guess)) throw new ArgumentException("Code must contain six distinct digits from 1 to 9.");
            int used = 0, result = 0;
            for (int i = 0; i < 6; i++) used |= 1 << DigitAt(secret, i);
            for (int i = 0; i < 6; i++)
            {
                int digit = DigitAt(guess, i);
                var value = digit == DigitAt(secret, i) ? RelayBCodeFeedback.Right
                    : (used & (1 << digit)) != 0 ? RelayBCodeFeedback.WrongPlace : RelayBCodeFeedback.Unused;
                result |= (int)value << (i * 2);
            }
            return result;
        }
    }
}
