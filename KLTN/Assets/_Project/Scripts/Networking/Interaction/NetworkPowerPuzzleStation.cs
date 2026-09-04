using Fusion;
using UnityEngine;

namespace EchoProtocol.Networking
{
    /// <summary>A configurable puzzle input endpoint; all mutation is delegated to puzzle State Authority.</summary>
    [DisallowMultipleComponent]
    public sealed class NetworkPowerPuzzleStation : NetworkInteractable
    {
        [SerializeField] private Renderer _fallbackRenderer;

        [Networked] public NetworkId PuzzleId { get; private set; }
        [Networked] public int InputId { get; private set; }
        [Networked, OnChangedRender(nameof(ApplyPresentation))]
        private NetworkBool ShowFallbackVisual { get; set; }

        public override void Spawned()
        {
            ApplyPresentation();
        }

        public void InitializeAuthoritative(NetworkId puzzleId, int inputId, bool showFallbackVisual)
        {
            if (!Object.HasStateAuthority || !puzzleId.IsValid) return;

            PuzzleId = puzzleId;
            InputId = inputId;
            ShowFallbackVisual = showFallbackVisual;
            ApplyPresentation();
            Debug.Log($"[PowerPuzzleStation] Station {Object.Id} input={inputId}, puzzle={puzzleId}.");
        }

        protected override InteractionValidationResult ValidateCurrentState(in InteractionContext context)
        {
            if (context.PlayerState == null || !context.PlayerState.IsGameplayPlayer)
            {
                return InteractionValidationResult.InvalidRequester;
            }

            return TryGetPuzzle(out var puzzle) && puzzle.CanAcceptInput(InputId)
                ? InteractionValidationResult.Accepted
                : InteractionValidationResult.InvalidTargetState;
        }

        protected override void ExecuteInteraction(in InteractionContext context)
        {
            if (!TryGetPuzzle(out var puzzle)) return;

            var result = puzzle.TryApplyInput(context.Player, InputId);
            Debug.Log(
                $"[PowerPuzzleStation] player={context.Player}, station={Object.Id}, " +
                $"input={InputId}, result={result}.");
        }

        private bool TryGetPuzzle(out NetworkPowerPuzzle puzzle)
        {
            puzzle = null;
            return PuzzleId.IsValid
                && Runner.TryFindObject(PuzzleId, out var puzzleObject)
                && puzzleObject != null
                && puzzleObject.TryGetComponent(out puzzle);
        }

        private void ApplyPresentation()
        {
            if (_fallbackRenderer != null) _fallbackRenderer.enabled = ShowFallbackVisual;
        }
    }
}
