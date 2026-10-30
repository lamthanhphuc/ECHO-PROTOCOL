using Fusion;

namespace EchoProtocol.Networking
{
    /// <summary>Object-independent host announcements survive Lobby PlayerObject replacement.</summary>
    public sealed class MatchLoadingRpc : SimulationBehaviour
    {
        [Rpc(RpcSources.All, RpcTargets.All, Channel = RpcChannel.Reliable,
            HostMode = RpcHostMode.SourceIsServer, InvokeLocal = false, TickAligned = false)]
        public static void RPC_LoadingStatus(NetworkRunner runner, string envelope, RpcInfo info = default)
        {
            // Static RPCs have no source filter. Fusion supplies the authenticated source:
            // only the server/host uses None; a relayed client's source remains its PlayerRef.
            if (runner == null || runner.IsServer || info.Source != PlayerRef.None) return;
            MatchLoadingCoordinator.Instance?.ReceiveHostStatus(runner, envelope);
        }
    }
}
