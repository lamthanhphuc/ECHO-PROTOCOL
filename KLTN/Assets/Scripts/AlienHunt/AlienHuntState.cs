using Fusion;
using UnityEngine;

namespace EchoProtocol.Networking
{
    public enum HuntKind { Alien, Jerrycan, Meat }
    public enum AlienStage { Wandering, Running, Eating, Stunned, Carried, OnPan, Burning, Burned }

    public struct HuntEntityState : INetworkStruct
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public PlayerRef Carrier;
        public int Stage;
        public int Target;
        public float Remaining;
        public int NestIndex;
        public NetworkBool Returning;
        public NetworkBool Fleeing;
        public Vector3 LastGroundPosition;
        public Vector3 ProgressPosition;
        public float StuckSeconds;
        public float TravelSeconds;
        public float BaitCooldown;
    }

    public struct HuntFurnaceState : INetworkStruct
    {
        public int Fuel;
        public int BurnedCount;
        public int AlienSlot; // Index + 1; zero means empty.
        public float BurnRemaining;
    }
}
