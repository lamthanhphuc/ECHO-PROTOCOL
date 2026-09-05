using System;
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
        public static event Action<NetworkSectorBox> ObjectiveStateChanged;

        [SerializeField, Min(1)] private int _requiredCoreCount = 3;
        [SerializeField] private Transform[] _corePlacementPoints = Array.Empty<Transform>();
        [SerializeField, Min(0.1f)] private float _fallbackSlotSpacing = 0.65f;
        [SerializeField] private Renderer _embeddedFallbackRenderer;

        [Networked, OnChangedRender(nameof(HandleObjectiveStateChanged))]
        public int PlacedCoreCount { get; private set; }

        [Networked, OnChangedRender(nameof(HandleObjectiveStateChanged))]
        public NetworkMatchPhase Phase { get; private set; }

        [Networked] public NetworkBool SecurityHoldWasInterrupted { get; private set; }
        [Networked] public uint ObjectiveOrdinal { get; private set; }

        public int RequiredCoreCount => _requiredCoreCount;

        public override void Spawned()
        {
            NetworkPlayerLifeState.StateChanged += HandlePlayerLifeStateChanged;
            ConfigurePresentation();
            if (!Object.HasStateAuthority)
            {
                // OnChangedRender is not guaranteed for the initial late-join snapshot.
                HandleObjectiveStateChanged();
                return;
            }
            PlacedCoreCount = 0;
            Phase = NetworkMatchPhase.CoreCollection;
            SecurityHoldWasInterrupted = false;
            ObjectiveOrdinal = 0;
            HandleObjectiveStateChanged();
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
                if (!EnergyCoreObjectiveRules.CanRegisterPlacement(PlacedCoreCount, _requiredCoreCount)
                    || !context.PlayerState.CarriedCoreId.IsValid
                    || !Runner.TryFindObject(context.PlayerState.CarriedCoreId, out var coreObject)
                    || coreObject == null
                    || !coreObject.TryGetComponent<NetworkPickupItem>(out var core)
                    || !core.CanBePlacedBy(context.Player))
                {
                    return InteractionValidationResult.InvalidTargetState;
                }

                return InteractionValidationResult.Accepted;
            }

            return Phase == NetworkMatchPhase.Completed
                || Phase == NetworkMatchPhase.PowerPuzzle
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
            var slotIndex = PlacedCoreCount;
            GetPlacementPose(slotIndex, out var position, out var rotation);
            if (!Runner.TryFindObject(coreId, out var coreObject)
                || !coreObject.TryGetComponent<NetworkPickupItem>(out var core)
                || !core.TryPlace(context.Player, Object.Id, slotIndex, position, rotation))
            {
                return;
            }

            PlacedCoreCount = Mathf.Min(PlacedCoreCount + 1, _requiredCoreCount);
            AdvanceOrdinal();
            HandleObjectiveStateChanged();
            if (PlacedCoreCount >= _requiredCoreCount)
            {
                CompletePhaseAndStartNext("CORE_COLLECTION", NetworkMatchPhase.PowerPuzzle, "POWER_PUZZLE");
            }
        }

        public bool TryCommitPowerPuzzleCompletion(NetworkId puzzleId)
        {
            if (!Object.HasStateAuthority
                || Phase != NetworkMatchPhase.PowerPuzzle
                || !puzzleId.IsValid
                || !Runner.TryFindObject(puzzleId, out var puzzleObject)
                || puzzleObject == null
                || !puzzleObject.TryGetComponent<NetworkPowerPuzzle>(out var puzzle)
                || puzzle.State != NetworkPowerPuzzleState.Completed)
            {
                return false;
            }

            AdvanceOrdinal();
            MatchAuthorityRuntime.Instance?.RecordPuzzleCompleted(BuildKey("puzzle-completed"));
            CompletePhaseAndStartNext("POWER_PUZZLE", NetworkMatchPhase.SecurityHold, "SECURITY_HOLD");
            HandleObjectiveStateChanged();
            return true;
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

        private void GetPlacementPose(int slotIndex, out Vector3 position, out Quaternion rotation)
        {
            if (slotIndex >= 0
                && slotIndex < _corePlacementPoints.Length
                && _corePlacementPoints[slotIndex] != null)
            {
                position = _corePlacementPoints[slotIndex].position;
                rotation = _corePlacementPoints[slotIndex].rotation;
                return;
            }

            var centeredIndex = slotIndex - (_requiredCoreCount - 1) * 0.5f;
            position = transform.TransformPoint(new Vector3(centeredIndex * _fallbackSlotSpacing, 0.75f, 0f));
            rotation = transform.rotation;
        }

        private void HandleObjectiveStateChanged()
        {
            foreach (var legacyProgress in FindObjectsByType<EnergyCoreObjectiveProgress>(FindObjectsInactive.Include))
            {
                legacyProgress.ApplyAuthoritativeSnapshot(PlacedCoreCount, _requiredCoreCount);
            }

            ObjectiveStateChanged?.Invoke(this);
        }

        private void ConfigurePresentation()
        {
            if (_embeddedFallbackRenderer == null) return;

            var legacyBoxes = FindObjectsByType<SectorBox>(FindObjectsInactive.Include);
            _embeddedFallbackRenderer.enabled = legacyBoxes.Length == 0;
        }
    }

    public static class EnergyCoreObjectiveRules
    {
        public static bool CanRegisterPlacement(int placedCoreCount, int requiredCoreCount)
        {
            return requiredCoreCount > 0
                && placedCoreCount >= 0
                && placedCoreCount < requiredCoreCount;
        }
    }
}
