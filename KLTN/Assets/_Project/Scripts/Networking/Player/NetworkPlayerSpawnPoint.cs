using UnityEngine;

namespace EchoProtocol.Networking
{
    /// <summary>Ordered gameplay spawn marker discovered after Fusion finishes loading the Game scene.</summary>
    public sealed class NetworkPlayerSpawnPoint : MonoBehaviour
    {
        [SerializeField, Min(0)] private int _order;

        public int Order => _order;
    }
}
