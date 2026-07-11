using System.Threading.Tasks;
using Fusion;
using UnityEngine;

namespace EchoProtocol.Networking
{
    /// <summary>
    /// Owns the Photon Fusion <see cref="NetworkRunner"/> lifecycle for ECHO PROTOCOL.
    /// App ID is read from PhotonAppSettings (Resources) by Fusion — never hard-coded here.
    /// </summary>
    public class NetworkBootstrap : MonoBehaviour
    {
        public const string RunnerObjectName = "EchoNetworkRunner";

        [SerializeField] private NetworkRunner _runnerPrefab;

        public NetworkRunner Runner { get; private set; }

        public bool HasRunningRunner => Runner != null && Runner.IsRunning;

        /// <summary>
        /// Returns an active runner, creating one from prefab or a runtime object when needed.
        /// </summary>
        public NetworkRunner EnsureRunner()
        {
            if (Runner != null && !Runner.IsShutdown)
            {
                return Runner;
            }

            if (_runnerPrefab != null)
            {
                Runner = Instantiate(_runnerPrefab);
            }
            else
            {
                var existing = FindAnyObjectByType<NetworkRunner>();
                if (existing != null && !existing.IsShutdown)
                {
                    Runner = existing;
                    EnsureRunnerComponents(Runner);
                    return Runner;
                }

                var runnerObject = new GameObject(RunnerObjectName);
                Runner = runnerObject.AddComponent<NetworkRunner>();
            }

            Runner.name = RunnerObjectName;
            DontDestroyOnLoad(Runner.gameObject);
            EnsureRunnerComponents(Runner);
            return Runner;
        }

        public async Task ShutdownRunnerAsync()
        {
            if (Runner == null || !Runner.IsRunning)
            {
                return;
            }

            await Runner.Shutdown();
        }

        private static void EnsureRunnerComponents(NetworkRunner runner)
        {
            if (runner.GetComponent<INetworkSceneManager>() == null)
            {
                runner.gameObject.AddComponent<NetworkSceneManagerDefault>();
            }

            if (runner.GetComponent<INetworkObjectProvider>() == null)
            {
                runner.gameObject.AddComponent<NetworkObjectProviderDefault>();
            }
        }
    }
}
