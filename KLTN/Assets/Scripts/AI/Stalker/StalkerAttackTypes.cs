using System;
using EchoProtocol.AI.Common;
using UnityEngine;

namespace EchoProtocol.AI.Stalker
{
    public readonly struct StalkerAttackEpisodeId : IEquatable<StalkerAttackEpisodeId>, IComparable<StalkerAttackEpisodeId>
    {
        private readonly long _value;

        public StalkerAttackEpisodeId(long value)
        {
            if (value <= 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Attack episode id must be greater than zero.");
            }

            _value = value;
        }

        public static StalkerAttackEpisodeId Invalid => default;

        public bool IsValid => _value > 0L;

        public long Value => _value;

        public int CompareTo(StalkerAttackEpisodeId other)
        {
            return _value.CompareTo(other._value);
        }

        public bool Equals(StalkerAttackEpisodeId other)
        {
            return _value == other._value;
        }

        public override bool Equals(object obj)
        {
            return obj is StalkerAttackEpisodeId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return _value.GetHashCode();
        }

        public override string ToString()
        {
            return IsValid ? $"StalkerAttackEpisodeId({_value})" : "StalkerAttackEpisodeId.Invalid";
        }

        public static bool operator ==(StalkerAttackEpisodeId left, StalkerAttackEpisodeId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(StalkerAttackEpisodeId left, StalkerAttackEpisodeId right)
        {
            return !left.Equals(right);
        }
    }

    public enum StalkerAttackOutcome
    {
        None,
        Hit,
        Miss
    }

    public enum StalkerAttackResolutionResult
    {
        ResolvedHit,
        ResolvedMiss,
        AlreadyResolved,
        NoActiveEpisode,
        NotStateAuthority,
        EpisodeMismatch
    }

    public readonly struct StalkerAttackTargetSnapshot
    {
        public StalkerAttackTargetSnapshot(
            PlayerId playerId,
            bool gameplayValid,
            Vector3 authoritativePosition,
            bool hasConsequenceReceiver)
        {
            PlayerId = playerId;
            GameplayValid = gameplayValid;
            AuthoritativePosition = authoritativePosition;
            HasConsequenceReceiver = hasConsequenceReceiver;
        }

        public static StalkerAttackTargetSnapshot Missing(PlayerId playerId)
        {
            return new StalkerAttackTargetSnapshot(playerId, false, default, false);
        }

        public PlayerId PlayerId { get; }

        public bool GameplayValid { get; }

        public Vector3 AuthoritativePosition { get; }

        public bool HasConsequenceReceiver { get; }

        public bool IsUsableForHit(PlayerId expectedPlayerId)
        {
            return expectedPlayerId.IsValid
                && PlayerId == expectedPlayerId
                && GameplayValid
                && HasConsequenceReceiver
                && IsFinite(AuthoritativePosition);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    public readonly struct StalkerAttackEpisode
    {
        public StalkerAttackEpisode(
            StalkerAttackEpisodeId episodeId,
            PlayerId targetIdAtEntry,
            AiSimulationTime startedAt,
            float windupElapsedSeconds,
            bool hitMomentResolved,
            StalkerAttackOutcome outcome,
            AiSimulationTime resolutionTime)
        {
            EpisodeId = episodeId;
            TargetIdAtEntry = targetIdAtEntry;
            StartedAt = startedAt;
            WindupElapsedSeconds = windupElapsedSeconds;
            HitMomentResolved = hitMomentResolved;
            Outcome = outcome;
            ResolutionTime = resolutionTime;
        }

        public StalkerAttackEpisodeId EpisodeId { get; }

        public PlayerId TargetIdAtEntry { get; }

        public AiSimulationTime StartedAt { get; }

        public long StartedTick => StartedAt.IsValid ? StartedAt.Tick : -1L;

        public double StartedTime => StartedAt.IsValid ? StartedAt.Seconds : 0d;

        public float WindupElapsedSeconds { get; }

        public bool HitMomentResolved { get; }

        public StalkerAttackOutcome Outcome { get; }

        public AiSimulationTime ResolutionTime { get; }
    }

    public interface IPlayerAttackConsequenceSink
    {
        bool TryApplyStalkerHit(
            StalkerAttackEpisodeId episodeId,
            PlayerId playerId,
            Vector3 authoritativeHitPosition,
            AiSimulationTime resolvedAt);
    }
}
