using UnityEngine;

namespace EchoProtocol.AI.AED
{
    [DisallowMultipleComponent]
    public sealed class AEDZoneEntryMarker : MonoBehaviour
    {
        [SerializeField, Range(1, 3)] private int zone;
        public int Zone => zone;
    }
}
