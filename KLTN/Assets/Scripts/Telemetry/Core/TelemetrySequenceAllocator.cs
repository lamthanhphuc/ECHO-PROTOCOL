using System;

namespace EchoProtocol.Telemetry
{
    public sealed class TelemetrySequenceAllocator
    {
        private Guid _matchId;
        private long _lastSequence;
        private bool _active;
        private bool _terminal;

        public Guid MatchId => _matchId;
        public long LastAllocatedSequence => _lastSequence;
        public bool IsActive => _active && !_terminal;
        public bool IsTerminal => _terminal;

        public void BeginMatch(Guid matchId)
        {
            if (matchId == Guid.Empty)
            {
                throw new ArgumentException("Match ID is required.", nameof(matchId));
            }

            if (_active && !_terminal)
            {
                throw new InvalidOperationException("The current telemetry match is still active.");
            }

            _matchId = matchId;
            _lastSequence = 0;
            _active = true;
            _terminal = false;
        }

        public long Allocate()
        {
            if (!_active || _terminal)
            {
                throw new InvalidOperationException("Telemetry sequence allocation requires an active match.");
            }

            if (_lastSequence == long.MaxValue)
            {
                throw new OverflowException("Telemetry event sequence is exhausted.");
            }

            _lastSequence++;
            return _lastSequence;
        }

        public void MarkTerminal()
        {
            if (!_active || _terminal)
            {
                throw new InvalidOperationException("Telemetry match is not active.");
            }

            _terminal = true;
        }
    }
}
