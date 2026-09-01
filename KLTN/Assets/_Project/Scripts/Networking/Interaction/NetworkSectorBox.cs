using EchoProtocol.Networking.Authority;
using Fusion;
using UnityEngine;

namespace EchoProtocol.Networking
{
    public enum NetworkMatchPhase
    {
        CoreCollection = 0,
        PowerPuzzle = 1,
        SecurityHold = 2,
        FinalHunt = 3,
        Completed = 4,
    }

    /// <summary>Host-owned M2 objective console for Core, phase, puzzle, hold and exit mutations.</summary>
    [DisallowMultipleComponent]
    public sealed class NetworkSectorBox : NetworkInteractable
    {
        [SerializeField, Min(1)] private int _requiredCoreCount = 1;

        [Networked] public int PlacedCoreCount { get; private set; }
        [Networked] public NetworkMatchPhase Phase { get; private set; }
        [Networked] public NetworkBool SecurityHoldWasInterrupted { get; private set; }
        [Networked] public uint ObjectiveOrdinal { get; private set; }

        public override void Spawned()
        {
            NetworkPlayerLifeState.StateChanged += HandlePlayerLifeStateChanged;
            if (!Object.HasStateAuthority) return;
            PlacedCoreCount = 0;
            Phase = NetworkMatchPhase.CoreCollection;
            SecurityHoldWasInterrupted = false;
            ObjectiveOrdinal = 0;
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            NetworkPlayerLifeState.StateChanged -= HandlePlayerLifeStateChanged;
        }

        protected override InteractionValidationResult ValidateCurrentState(in InteractionContext context)
        {
            if (context.PlayerState == null || !context.PlayerState.IsGameplayPlayer)
            {
                return InteractionValidationResult.InvalidRequester;
            }

            if (Phase == NetworkMatchPhase.CoreCollection)
            {
                return context.PlayerState.CarriedCoreId.IsValid
                    ? InteractionValidationResult.Accepted
                    : InteractionValidationResult.InvalidTargetState;
            }

            return Phase == NetworkMatchPhase.Completed
                ? InteractionValidationResult.InvalidTargetState
                : InteractionValidationResult.Accepted;
        }

        protected override void ExecuteInteraction(in InteractionContext context)
        {
            switch (Phase)
            {
                case NetworkMatchPhase.CoreCollection:
                    PlaceCarriedCore(context);
                    break;
                case NetworkMatchPhase.PowerPuzzle:
                    CompletePowerPuzzle();
                    break;
                case NetworkMatchPhase.SecurityHold:
                    AdvanceSecurityHold();
                    break;
                case NetworkMatchPhase.FinalHunt:
                    EscapePlayer(context.Player);
                    break;
            }
        }

        private void PlaceCarriedCore(in InteractionContext context)
        {
            var coreId = context.PlayerState.CarriedCoreId;
            if (!Runner.TryFindObject(coreId, out var coreObject)
                || !coreObject.TryGetComponent<NetworkPickupItem>(out var core)
                || !core.TryPlace(context.Player, transform.position + Vector3.up * 0.75f))
            {
                return;
            }

            PlacedCoreCount++;
            AdvanceOrdinal();
            if (PlacedCoreCount >= _requiredCoreCount)
            {
                CompletePhaseAndStartNext("CORE_COLLECTION", NetworkMatchPhase.PowerPuzzle, "POWER_PUZZLE");
            }
        }

        private void CompletePowerPuzzle()
        {
            AdvanceOrdinal();
            MatchAuthorityRuntime.Instance?.RecordPuzzleCompleted(BuildKey("puzzle-completed"));
            CompletePhaseAndStartNext("POWER_PUZZLE", NetworkMatchPhase.SecurityHold, "SECURITY_HOLD");
        }

        private void AdvanceSecurityHold()
        {
            AdvanceOrdinal();
            if (!SecurityHoldWasInterrupted)
            {
                SecurityHoldWasInterrupted = true;
                MatchAuthorityRuntime.Instance?.RecordSecurityHoldInterrupted(
                    BuildKey("security-hold-interrupted"));
                return;
            }

            CompletePhaseAndStartNext("SECURITY_HOLD", NetworkMatchPhase.FinalHunt, "FINAL_HUNT");
        }

        private void EscapePlayer(PlayerRef player)
        {
            if (!Runner.TryGetPlayerObject(player, out var playerObject)
                || !playerObject.TryGetComponent<NetworkPlayerLifeState>(out var lifeState)
                || !lifeState.TryEscape())
            {
                return;
            }
        }

        private void HandlePlayerLifeStateChanged(NetworkPlayerLifeState _)
        {
            if (Object == null || !Object.HasStateAuthority || Phase != NetworkMatchPhase.FinalHunt)
            {
                return;
            }

            var trackedPlayers = 0;
            var survivorCount = 0;
            foreach (var player in Runner.ActivePlayers)
            {
                if (!Runner.TryGetPlayerObject(player, out var playerObject)
                    || !playerObject.TryGetComponent<NetworkPlayerLifeState>(out var lifeState))
                {
                    continue;
                }

                trackedPlayers++;
                if (lifeState.Status == NetworkPlayerLifeStatus.Alive
                    || lifeState.Status == NetworkPlayerLifeStatus.Downed)
                {
                    return;
                }

                if (lifeState.Status == NetworkPlayerLifeStatus.Escaped) survivorCount++;
            }

            if (trackedPlayers == 0) return;

            AdvanceOrdinal();
            Phase = NetworkMatchPhase.Completed;
            var runtime = MatchAuthorityRuntime.Instance;
            runtime?.RecordPhaseCompleted(
                BuildKey("phase-completed-final-hunt"),
                "FINAL_HUNT",
                "OBJECTIVE_COMPLETED");
            runtime?.RecordMatchEnded(
                BuildKey("match-ended"),
                survivorCount > 0 ? "SUCCESS" : "FAILURE",
                survivorCount,
                survivorCount > 0 ? "TEAM_ESCAPED" : "TEAM_ELIMINATED");
        }

        private void CompletePhaseAndStartNext(
            string completedPhase,
            NetworkMatchPhase nextPhase,
            string nextPhaseName)
        {
            var runtime = MatchAuthorityRuntime.Instance;
            runtime?.RecordPhaseCompleted(
                BuildKey("phase-completed-" + completedPhase.ToLowerInvariant()),
                completedPhase,
                "OBJECTIVE_COMPLETED");
            Phase = nextPhase;
            runtime?.RecordPhaseStarted(
                BuildKey("phase-started-" + nextPhaseName.ToLowerInvariant()),
                nextPhaseName,
                "PREVIOUS_PHASE_COMPLETED");
        }

        private void AdvanceOrdinal()
        {
            ObjectiveOrdinal++;
            if (ObjectiveOrdinal == 0) ObjectiveOrdinal = 1;
        }

        private string BuildKey(string occurrence)
        {
            return $"objective:{Object.Id}:{occurrence}:{ObjectiveOrdinal}";
        }
    }
}
