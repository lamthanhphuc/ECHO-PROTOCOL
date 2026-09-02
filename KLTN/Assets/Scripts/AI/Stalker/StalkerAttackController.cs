using System;
using EchoProtocol.AI.Common;
using EchoProtocol.AI.Stalker.Telemetry;
using UnityEngine;

namespace EchoProtocol.AI.Stalker
{
    public sealed class StalkerAttackController
    {
        private long _nextEpisodeSequence = 1L;
        private StalkerAttackEpisode _activeEpisode;
        private int _resolutionCount;
        private StalkerAttackResolutionResult _lastResolutionResult = StalkerAttackResolutionResult.NoActiveEpisode;
        private StalkerAttackResolvedFact _lastCommittedResolutionFact;

        public StalkerAttackEpisode ActiveEpisode => _activeEpisode;

        public StalkerAttackEpisodeId ActiveEpisodeId => _activeEpisode.EpisodeId;

        public PlayerId AttackTargetId => _activeEpisode.TargetIdAtEntry;

        public bool HasActiveEpisode => _activeEpisode.EpisodeId.IsValid;

        public bool HitMomentResolved => _activeEpisode.HitMomentResolved;

        public StalkerAttackOutcome Outcome => _activeEpisode.Outcome;

        public StalkerAttackResolutionResult LastResolutionResult => _lastResolutionResult;

        public int ResolutionCount => _resolutionCount;

        public bool HasCommittedResolutionFact => _lastCommittedResolutionFact.IsValid;

        public StalkerAttackResolvedFact LastCommittedResolutionFact => _lastCommittedResolutionFact;

        public StalkerAttackEpisode BeginAttack(bool hasStateAuthority, PlayerId targetId, AiSimulationStep step)
        {
            if (!hasStateAuthority || !targetId.IsValid || !step.IsValid)
            {
                return _activeEpisode;
            }

            if (HasActiveEpisode && !_activeEpisode.HitMomentResolved)
            {
                return _activeEpisode;
            }

            var id = new StalkerAttackEpisodeId(_nextEpisodeSequence++);
            _activeEpisode = new StalkerAttackEpisode(
                id,
                targetId,
                step.Time,
                0f,
                false,
                StalkerAttackOutcome.None,
                AiSimulationTime.Invalid);
            _lastResolutionResult = StalkerAttackResolutionResult.NoActiveEpisode;
            return _activeEpisode;
        }

        public void AdvanceWindup(float deltaSeconds)
        {
            if (!HasActiveEpisode || _activeEpisode.HitMomentResolved || deltaSeconds <= 0f)
            {
                return;
            }

            _activeEpisode = new StalkerAttackEpisode(
                _activeEpisode.EpisodeId,
                _activeEpisode.TargetIdAtEntry,
                _activeEpisode.StartedAt,
                _activeEpisode.WindupElapsedSeconds + deltaSeconds,
                _activeEpisode.HitMomentResolved,
                _activeEpisode.Outcome,
                _activeEpisode.ResolutionTime);
        }

        public StalkerAttackResolutionResult ResolveHitMoment(
            bool hasStateAuthority,
            StalkerAttackEpisodeId episodeId,
            Vector3 stalkerPosition,
            float attackRange,
            StalkerAttackTargetSnapshot targetSnapshot,
            IPlayerAttackConsequenceSink consequenceSink,
            AiSimulationStep step)
        {
            if (!hasStateAuthority)
            {
                _lastResolutionResult = StalkerAttackResolutionResult.NotStateAuthority;
                return _lastResolutionResult;
            }

            if (!HasActiveEpisode)
            {
                _lastResolutionResult = StalkerAttackResolutionResult.NoActiveEpisode;
                return _lastResolutionResult;
            }

            if (episodeId != _activeEpisode.EpisodeId)
            {
                _lastResolutionResult = StalkerAttackResolutionResult.EpisodeMismatch;
                return _lastResolutionResult;
            }

            if (_activeEpisode.HitMomentResolved)
            {
                _lastResolutionResult = StalkerAttackResolutionResult.AlreadyResolved;
                return _lastResolutionResult;
            }

            var isHit = targetSnapshot.IsUsableForHit(_activeEpisode.TargetIdAtEntry)
                && IsWithinAttackRange(stalkerPosition, targetSnapshot.AuthoritativePosition, attackRange);
            var outcome = isHit ? StalkerAttackOutcome.Hit : StalkerAttackOutcome.Miss;

            // Commit the guard and immutable outcome before invoking external gameplay code.
            _activeEpisode = new StalkerAttackEpisode(
                _activeEpisode.EpisodeId,
                _activeEpisode.TargetIdAtEntry,
                _activeEpisode.StartedAt,
                _activeEpisode.WindupElapsedSeconds,
                true,
                outcome,
                step.IsValid ? step.Time : AiSimulationTime.Invalid);
            _resolutionCount++;
            _lastCommittedResolutionFact = new StalkerAttackResolvedFact(
                _activeEpisode.EpisodeId,
                outcome,
                _activeEpisode.ResolutionTime);

            if (isHit && consequenceSink != null)
            {
                consequenceSink.TryApplyStalkerHit(
                    _activeEpisode.EpisodeId,
                    _activeEpisode.TargetIdAtEntry,
                    targetSnapshot.AuthoritativePosition,
                    _activeEpisode.ResolutionTime);
            }

            _lastResolutionResult = isHit
                ? StalkerAttackResolutionResult.ResolvedHit
                : StalkerAttackResolutionResult.ResolvedMiss;
            return _lastResolutionResult;
        }

        public void ClearActiveEpisode()
        {
            _activeEpisode = default;
            _lastResolutionResult = StalkerAttackResolutionResult.NoActiveEpisode;
        }

        private static bool IsWithinAttackRange(Vector3 stalkerPosition, Vector3 targetPosition, float attackRange)
        {
            var clampedRange = Mathf.Max(0f, attackRange);
            var delta = targetPosition - stalkerPosition;
            return delta.sqrMagnitude <= clampedRange * clampedRange;
        }
    }
}
