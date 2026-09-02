namespace EchoProtocol.AI.Stalker.Networking
{
    public readonly struct StalkerPresentationConsumeResult
    {
        public StalkerPresentationConsumeResult(
            bool changed,
            bool semanticStateChanged,
            bool newAttackEpisode,
            bool attackPhaseChanged,
            bool attackProgressUpdated,
            bool attackResolutionChanged)
        {
            Changed = changed;
            SemanticStateChanged = semanticStateChanged;
            NewAttackEpisode = newAttackEpisode;
            AttackPhaseChanged = attackPhaseChanged;
            AttackProgressUpdated = attackProgressUpdated;
            AttackResolutionChanged = attackResolutionChanged;
        }

        public bool Changed { get; }
        public bool SemanticStateChanged { get; }
        public bool NewAttackEpisode { get; }
        public bool AttackPhaseChanged { get; }
        public bool AttackProgressUpdated { get; }
        public bool AttackResolutionChanged { get; }

        public static StalkerPresentationConsumeResult NoChange =>
            new StalkerPresentationConsumeResult(false, false, false, false, false, false);
    }

    public sealed class StalkerPresentationDriver
    {
        private StalkerNetworkPresentationState _lastState;
        private bool _hasState;

        public bool HasState => _hasState;
        public StalkerNetworkPresentationState LastState => _lastState;
        public StalkerAttackEpisodeId LastConsumedAttackEpisodeId => _lastState.AttackEpisodeId;
        public StalkerNetworkAttackPhase LastConsumedAttackPhase => _lastState.AttackPhase;
        public StalkerAttackOutcome LastConsumedAttackOutcome => _lastState.AttackOutcome;
        public int ChangeCount { get; private set; }

        public StalkerPresentationConsumeResult Consume(StalkerNetworkPresentationState state)
        {
            if (_hasState && Equivalent(_lastState, state))
            {
                return StalkerPresentationConsumeResult.NoChange;
            }

            var result = _hasState
                ? new StalkerPresentationConsumeResult(
                    true,
                    _lastState.SemanticState != state.SemanticState,
                    _lastState.AttackEpisodeId != state.AttackEpisodeId && state.AttackEpisodeId.IsValid,
                    _lastState.AttackPhase != state.AttackPhase,
                    !NearlyEqual(_lastState.AttackProgressSeconds, state.AttackProgressSeconds),
                    _lastState.AttackHitMomentResolved != state.AttackHitMomentResolved
                        || _lastState.AttackOutcome != state.AttackOutcome
                        || _lastState.AttackResolvedTick != state.AttackResolvedTick)
                : new StalkerPresentationConsumeResult(
                    true,
                    true,
                    state.AttackEpisodeId.IsValid,
                    state.AttackPhase != StalkerNetworkAttackPhase.None,
                    state.AttackProgressSeconds > 0f,
                    state.AttackHitMomentResolved || state.AttackOutcome != StalkerAttackOutcome.None);

            _lastState = state;
            _hasState = true;
            ChangeCount++;
            return result;
        }

        private static bool Equivalent(
            StalkerNetworkPresentationState left,
            StalkerNetworkPresentationState right)
        {
            return left.SemanticState == right.SemanticState
                && left.AttackEpisodeId == right.AttackEpisodeId
                && left.AttackPhase == right.AttackPhase
                && NearlyEqual(left.AttackProgressSeconds, right.AttackProgressSeconds)
                && left.AttackHitMomentResolved == right.AttackHitMomentResolved
                && left.AttackOutcome == right.AttackOutcome
                && left.AttackStartedTick == right.AttackStartedTick
                && left.AttackResolvedTick == right.AttackResolvedTick;
        }

        private static bool NearlyEqual(float left, float right)
        {
            return System.Math.Abs(left - right) <= 0.0001f;
        }
    }
}
