using System;
using Fusion;

namespace EchoProtocol.Networking
{
    public enum InteractionValidationResult
    {
        Accepted = 0,
        NotInputAuthority,
        InvalidRequester,
        InvalidTarget,
        OutOfRange,
        InvalidTargetState,
        MissingRequiredTool,
        OnCooldown,
        DuplicateRequest,
    }

    public readonly struct InteractionCommand
    {
        public InteractionCommand(NetworkId targetId, uint sequence)
        {
            TargetId = targetId;
            Sequence = sequence;
        }

        public NetworkId TargetId { get; }
        public uint Sequence { get; }
    }

    public readonly struct InteractionRequestResult
    {
        public InteractionRequestResult(NetworkId targetId, uint sequence, InteractionValidationResult result)
        {
            TargetId = targetId;
            Sequence = sequence;
            Result = result;
        }

        public NetworkId TargetId { get; }
        public uint Sequence { get; }
        public InteractionValidationResult Result { get; }
        public bool Accepted => Result == InteractionValidationResult.Accepted;
    }

    public readonly struct InteractionContext
    {
        public InteractionContext(NetworkPlayerInteractor requester, NetworkInteractable target, PlayerRef player)
        {
            Requester = requester;
            Target = target;
            Player = player;
        }

        public NetworkPlayerInteractor Requester { get; }
        public NetworkInteractable Target { get; }
        public PlayerRef Player { get; }
        public LobbyPlayerState PlayerState => Requester.GetComponent<LobbyPlayerState>();
    }

    public interface IAuthoritativeNetworkInteractable
    {
        InteractionValidationResult ValidateInteraction(in InteractionContext context);
        void ExecuteAuthoritative(in InteractionContext context);
    }
}
