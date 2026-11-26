using UnityEngine;

namespace EchoProtocol.Salvage
{
    [System.Serializable]
    public struct SalvageMapSocket
    {
        public Vector3 Position;
        public int Direction; // 0 north, 1 east, 2 south, 3 west; outward.
        public float Width;
    }

    public sealed class SalvageMapModule : MonoBehaviour
    {
        public string SourcePath;
        public Bounds Footprint;
        public SalvageMapSocket[] Sockets;
        public Vector3[] LootPoints;
    }
}
