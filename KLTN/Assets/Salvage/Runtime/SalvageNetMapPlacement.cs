using Fusion;
using UnityEngine;
namespace EchoProtocol.Salvage
{
    public struct SalvageNetMapPlacement : INetworkStruct
    {
        public int Module, QuarterTurns, Parent, ParentSocket, EntrySocket;
        public Vector3 Position;
        public SalvageMapPlacement Decode() => new SalvageMapPlacement { Module=Module, QuarterTurns=QuarterTurns,
            Parent=Parent, ParentSocket=ParentSocket, EntrySocket=EntrySocket, Position=Position };
        public static SalvageNetMapPlacement Encode(SalvageMapPlacement p) => new SalvageNetMapPlacement {
            Module=p.Module, QuarterTurns=p.QuarterTurns, Parent=p.Parent, ParentSocket=p.ParentSocket, EntrySocket=p.EntrySocket, Position=p.Position };
    }
}
