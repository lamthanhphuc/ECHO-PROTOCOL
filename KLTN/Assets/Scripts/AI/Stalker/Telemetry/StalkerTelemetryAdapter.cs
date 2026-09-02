using System.Collections.Generic;

namespace EchoProtocol.AI.Stalker.Telemetry
{
    public enum StalkerTelemetryPublishResult
    {
        RetryableFailure,
        InvalidOccurrence,
        Accepted,
        Suppressed,
        AlreadyHandled
    }

    public readonly struct StalkerTelemetryMonsterIdentity
    {
        public StalkerTelemetryMonsterIdentity(string value)
        {
            Value = value;
        }

        public string Value { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Value);
    }

    public readonly struct StalkerAttackTelemetryOccurrence
    {
        public StalkerAttackTelemetryOccurrence(
            StalkerTelemetryMonsterIdentity monsterIdentity,
            StalkerAttackResolvedFact fact)
        {
            MonsterIdentity = monsterIdentity;
            Fact = fact;
        }

        public StalkerTelemetryMonsterIdentity MonsterIdentity { get; }
        public StalkerAttackResolvedFact Fact { get; }
        public bool IsValid => MonsterIdentity.IsValid && Fact.IsValid;
    }

    public readonly struct StalkerSearchTelemetryOccurrence
    {
        public StalkerSearchTelemetryOccurrence(
            StalkerTelemetryMonsterIdentity monsterIdentity,
            StalkerSearchEndedFact fact)
        {
            MonsterIdentity = monsterIdentity;
            Fact = fact;
        }

        public StalkerTelemetryMonsterIdentity MonsterIdentity { get; }
        public StalkerSearchEndedFact Fact { get; }
        public bool IsValid => MonsterIdentity.IsValid && Fact.IsValid;
    }

    public interface IStalkerTelemetryProducer
    {
        StalkerTelemetryPublishResult TryPublishMonsterAttackResolved(
            StalkerAttackTelemetryOccurrence occurrence);

        StalkerTelemetryPublishResult TryPublishMonsterSearchEnded(
            StalkerSearchTelemetryOccurrence occurrence);
    }

    public sealed class StalkerTelemetryAdapter
    {
        private const int MaxTerminalOccurrences = 128;

        private readonly HashSet<StalkerTelemetryOccurrenceKey> _terminalOccurrences =
            new HashSet<StalkerTelemetryOccurrenceKey>();
        private readonly Queue<StalkerTelemetryOccurrenceKey> _terminalOccurrenceOrder =
            new Queue<StalkerTelemetryOccurrenceKey>();

        public int TerminalOccurrenceCount => _terminalOccurrences.Count;

        public StalkerTelemetryPublishResult TryPublishAttackResolved(
            StalkerTelemetryMonsterIdentity monsterIdentity,
            StalkerAttackResolvedFact fact,
            IStalkerTelemetryProducer producer)
        {
            if (!monsterIdentity.IsValid || !fact.IsValid)
            {
                return HandleInvalidAttackOccurrence(monsterIdentity, fact);
            }

            if (producer == null)
            {
                return StalkerTelemetryPublishResult.RetryableFailure;
            }

            var key = StalkerTelemetryOccurrenceKey.AttackResolved(monsterIdentity.Value, fact.EpisodeId.Value);
            if (_terminalOccurrences.Contains(key))
            {
                return StalkerTelemetryPublishResult.AlreadyHandled;
            }

            var result = producer.TryPublishMonsterAttackResolved(
                new StalkerAttackTelemetryOccurrence(monsterIdentity, fact));
            MarkTerminalIfOwned(key, result);
            return result;
        }

        public StalkerTelemetryPublishResult TryPublishSearchEnded(
            StalkerTelemetryMonsterIdentity monsterIdentity,
            StalkerSearchEndedFact fact,
            IStalkerTelemetryProducer producer)
        {
            if (!monsterIdentity.IsValid || !fact.IsValid)
            {
                return HandleInvalidSearchOccurrence(monsterIdentity, fact);
            }

            if (producer == null)
            {
                return StalkerTelemetryPublishResult.RetryableFailure;
            }

            var key = StalkerTelemetryOccurrenceKey.SearchEnded(monsterIdentity.Value, fact.EpisodeId.Value);
            if (_terminalOccurrences.Contains(key))
            {
                return StalkerTelemetryPublishResult.AlreadyHandled;
            }

            var result = producer.TryPublishMonsterSearchEnded(
                new StalkerSearchTelemetryOccurrence(monsterIdentity, fact));
            MarkTerminalIfOwned(key, result);
            return result;
        }

        public void ResetForOwnerLifecycle()
        {
            _terminalOccurrences.Clear();
            _terminalOccurrenceOrder.Clear();
        }

        private StalkerTelemetryPublishResult HandleInvalidAttackOccurrence(
            StalkerTelemetryMonsterIdentity monsterIdentity,
            StalkerAttackResolvedFact fact)
        {
            if (monsterIdentity.IsValid && fact.EpisodeId.IsValid)
            {
                var key = StalkerTelemetryOccurrenceKey.AttackResolved(monsterIdentity.Value, fact.EpisodeId.Value);
                if (_terminalOccurrences.Contains(key))
                {
                    return StalkerTelemetryPublishResult.AlreadyHandled;
                }

                MarkTerminal(key);
            }

            return StalkerTelemetryPublishResult.InvalidOccurrence;
        }

        private StalkerTelemetryPublishResult HandleInvalidSearchOccurrence(
            StalkerTelemetryMonsterIdentity monsterIdentity,
            StalkerSearchEndedFact fact)
        {
            if (monsterIdentity.IsValid && fact.EpisodeId.IsValid)
            {
                var key = StalkerTelemetryOccurrenceKey.SearchEnded(monsterIdentity.Value, fact.EpisodeId.Value);
                if (_terminalOccurrences.Contains(key))
                {
                    return StalkerTelemetryPublishResult.AlreadyHandled;
                }

                MarkTerminal(key);
            }

            return StalkerTelemetryPublishResult.InvalidOccurrence;
        }

        private void MarkTerminalIfOwned(
            StalkerTelemetryOccurrenceKey key,
            StalkerTelemetryPublishResult result)
        {
            if (result != StalkerTelemetryPublishResult.Accepted
                && result != StalkerTelemetryPublishResult.Suppressed)
            {
                return;
            }

            MarkTerminal(key);
        }

        private void MarkTerminal(StalkerTelemetryOccurrenceKey key)
        {
            if (!_terminalOccurrences.Add(key))
            {
                return;
            }

            _terminalOccurrenceOrder.Enqueue(key);
            while (_terminalOccurrences.Count > MaxTerminalOccurrences && _terminalOccurrenceOrder.Count > 0)
            {
                _terminalOccurrences.Remove(_terminalOccurrenceOrder.Dequeue());
            }
        }

        private readonly struct StalkerTelemetryOccurrenceKey
        {
            private readonly string _monsterIdentity;
            private readonly long _episodeId;
            private readonly StalkerTelemetryOccurrenceKind _kind;

            private StalkerTelemetryOccurrenceKey(
                string monsterIdentity,
                long episodeId,
                StalkerTelemetryOccurrenceKind kind)
            {
                _monsterIdentity = monsterIdentity;
                _episodeId = episodeId;
                _kind = kind;
            }

            public static StalkerTelemetryOccurrenceKey AttackResolved(string monsterIdentity, long episodeId)
            {
                return new StalkerTelemetryOccurrenceKey(monsterIdentity, episodeId, StalkerTelemetryOccurrenceKind.AttackResolved);
            }

            public static StalkerTelemetryOccurrenceKey SearchEnded(string monsterIdentity, long episodeId)
            {
                return new StalkerTelemetryOccurrenceKey(monsterIdentity, episodeId, StalkerTelemetryOccurrenceKind.SearchEnded);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = 17;
                    hash = (hash * 31) + (_monsterIdentity != null ? _monsterIdentity.GetHashCode() : 0);
                    hash = (hash * 31) + _episodeId.GetHashCode();
                    hash = (hash * 31) + _kind.GetHashCode();
                    return hash;
                }
            }

            public override bool Equals(object obj)
            {
                return obj is StalkerTelemetryOccurrenceKey other
                    && _monsterIdentity == other._monsterIdentity
                    && _episodeId == other._episodeId
                    && _kind == other._kind;
            }
        }

        private enum StalkerTelemetryOccurrenceKind
        {
            AttackResolved,
            SearchEnded
        }
    }
}
