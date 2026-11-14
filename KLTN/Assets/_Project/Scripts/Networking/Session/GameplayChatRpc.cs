using System;
using System.Collections.Generic;
using System.Text;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EchoProtocol.Networking
{
    public sealed class GameplayChatRpc : SimulationBehaviour
    {
        public static event Action<PlayerRef, string> MessageReceived;

        private static readonly Dictionary<PlayerRef, float> LastSent = new();
        private static NetworkRunner _rateRunner;

        public static string Normalize(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            var result = new StringBuilder(120);

            foreach (char c in text.Trim())
            {
                if (char.IsControl(c) || char.IsSurrogate(c))
                    continue;

                result.Append(c);

                if (result.Length >= 120)
                    break;
            }

            return result.ToString().Trim();
        }

        [Rpc(RpcSources.All, RpcTargets.All, Channel = RpcChannel.Reliable,
            HostMode = RpcHostMode.SourceIsHostPlayer,
            TickAligned = false)]
        public static void RPC_Submit(
            NetworkRunner runner,
            string text,
            RpcInfo info = default)
        {
            if (runner == null || !runner.IsRunning || !runner.IsServer)
                return;

            if (NetworkBootstrap.Instance?.Runner != runner
                || NetworkBootstrap.IsHostMigrationReconciliationPending
                || !LobbyMapCatalog.IsGameplayScene(SceneManager.GetActiveScene().name)
                || !info.Source.IsRealPlayer)
            {
                Debug.LogWarning($"[Chat] Host rejected submit source={info.Source} scene={SceneManager.GetActiveScene().name}.");
                return;
            }

            if (!runner.TryGetPlayerObject(info.Source, out var player)
                || player == null || !player.IsValid)
            {
                Debug.LogWarning($"[Chat] Host has no player object for {info.Source}.");
                return;
            }

            string message = Normalize(text);
            if (message.Length == 0)
                return;

            if (_rateRunner != runner)
            {
                LastSent.Clear();
                _rateRunner = runner;
            }

            float now = Time.realtimeSinceStartup;
            if (LastSent.TryGetValue(info.Source, out float previous)
                && now - previous < 0.75f)
                return;

            LastSent[info.Source] = now;
            Debug.Log($"[Chat] Host accepted source={info.Source} length={message.Length}.");
            RPC_Broadcast(runner, info.Source, message);
        }

        [Rpc(RpcSources.All, RpcTargets.All, Channel = RpcChannel.Reliable,
            HostMode = RpcHostMode.SourceIsServer,
            TickAligned = false)]
        public static void RPC_Broadcast(
            NetworkRunner runner,
            PlayerRef sender,
            string message,
            RpcInfo info = default)
        {
            if (runner == null || !runner.IsRunning
                || NetworkBootstrap.Instance?.Runner != runner
                || info.Source != PlayerRef.None)
            {
                Debug.LogWarning($"[Chat] Broadcast rejected source={info.Source}.");
                return;
            }

            Debug.Log($"[Chat] Broadcast received sender={sender} length={message.Length}.");
            MessageReceived?.Invoke(sender, message);
        }
    }
}
