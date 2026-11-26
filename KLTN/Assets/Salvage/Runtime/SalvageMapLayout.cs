using System;
using System.Collections.Generic;
using UnityEngine;

namespace EchoProtocol.Salvage
{
    public struct SalvageMapPlacement
    {
        public int Module, QuarterTurns, Parent, ParentSocket, EntrySocket;
        public Vector3 Position;
    }

    // Only host runs the generator. Clients receive the resulting placements, not a random stream.
    public static class SalvageMapLayout
    {
        public const int Version = 6, Capacity = 32;
        public static Vector3 Direction(int direction) => Rotate(Vector3.forward, direction);
        public static Vector3 Rotate(Vector3 point, int turns)
        {
            switch ((turns % 4 + 4) % 4)
            { case 1: return new Vector3(point.z, point.y, -point.x);
              case 2: return new Vector3(-point.x, point.y, -point.z);
              case 3: return new Vector3(-point.z, point.y, point.x); default: return point; }
        }
        public static Vector3 SocketPoint(SalvageMapPlacement placement, SalvageMapSocket socket)
            => placement.Position + Rotate(socket.Position, placement.QuarterTurns);
        public static int SocketDirection(SalvageMapPlacement placement, SalvageMapSocket socket)
            => (placement.QuarterTurns + socket.Direction) % 4;
        public static Bounds WorldBounds(SalvageMapPlacement placement, Bounds bounds)
        {
            Vector3 size = bounds.size;
            if (placement.QuarterTurns % 2 != 0) size = new Vector3(size.z, size.y, size.x);
            return new Bounds(placement.Position + Rotate(bounds.center, placement.QuarterTurns), size);
        }
        public static List<SalvageMapPlacement> Generate(int seed, SalvageMapModule[] rooms,
            SalvageMapPlacement basePlacement, SalvageMapModule baseModule, int targetRooms)
        {
            if (rooms == null || rooms.Length == 0 || baseModule.Sockets == null || baseModule.Sockets.Length != 1)
                throw new InvalidOperationException("Map requires a one-exit fixed base and room modules.");
            var random = new System.Random(seed);
            var result = new List<SalvageMapPlacement> { basePlacement };
            var open = new List<(int room, int socket)> { (0, 0) };
            for (int attempt = 0; attempt < 1200 && result.Count <= targetRooms && open.Count > 0; attempt++)
            {
                var exit = open[random.Next(open.Count)]; var parent = result[exit.room];
                var parentModule = exit.room == 0 ? baseModule : rooms[parent.Module];
                var socket = parentModule.Sockets[exit.socket];
                int outward = SocketDirection(parent, socket);
                int id = random.Next(rooms.Length); var module = rooms[id];
                // Prefer junctions early so a one-exit room does not terminate the whole layout.
                if (result.Count < 3 && module.Sockets.Length < 2) continue;
                int entry = random.Next(module.Sockets.Length);
                int turns = (outward + 2 - module.Sockets[entry].Direction + 4) % 4;
                float length = 12 + random.Next(3) * 6;
                var placement = new SalvageMapPlacement { Module = id, QuarterTurns = turns,
                    Parent = exit.room, ParentSocket = exit.socket, EntrySocket = entry,
                    Position = SocketPoint(parent, socket) + Direction(outward) * length - Rotate(module.Sockets[entry].Position, turns) };
                var candidate = WorldBounds(placement, module.Footprint); candidate.Expand(-.1f);
                bool overlap = false;
                foreach (var previous in result)
                {
                    var bounds = WorldBounds(previous, previous.Parent < 0 ? baseModule.Footprint : rooms[previous.Module].Footprint);
                    if (candidate.Intersects(bounds)) { overlap = true; break; }
                }
                // Reserve straight connecting passages, including the two-metre doorway approach.
                var from = SocketPoint(parent, socket); var to = SocketPoint(placement, module.Sockets[entry]);
                var passage = new Bounds((from + to) * .5f + Vector3.up * 2,
                    new Vector3(Mathf.Abs(from.x-to.x)+4.5f, 4, Mathf.Abs(from.z-to.z)+4.5f));
                for (int i=1; !overlap && i<result.Count; i++)
                {
                    if (i == exit.room) continue;
                    if (passage.Intersects(WorldBounds(result[i], rooms[result[i].Module].Footprint))) overlap=true;
                }
                for (int i=1; !overlap && i<result.Count; i++)
                {
                    var p = result[i]; var source = result[p.Parent];
                    var a = SocketPoint(source,(p.Parent==0?baseModule:rooms[source.Module]).Sockets[p.ParentSocket]);
                    var b = SocketPoint(p, rooms[p.Module].Sockets[p.EntrySocket]);
                    var occupied = new Bounds((a+b)*.5f+Vector3.up*2,
                        new Vector3(Mathf.Abs(a.x-b.x)+4.5f,4,Mathf.Abs(a.z-b.z)+4.5f));
                    if (candidate.Intersects(occupied) || passage.Intersects(occupied)) overlap=true;
                }
                if (overlap) continue;
                int index = result.Count; result.Add(placement); open.Remove(exit);
                for(int i=0;i<module.Sockets.Length;i++) if(i!=entry) open.Add((index,i));
            }
            if (result.Count < 5) throw new InvalidOperationException("Seed cannot produce a connected collection area.");
            return result;
        }
        public static bool PassagesClear(IReadOnlyList<SalvageMapPlacement> plan,SalvageMapModule[] rooms,SalvageMapModule baseModule)
        {
            var used=new HashSet<(int,int)>();var passages=new List<(Vector3 a,Vector3 b,int from,int to)>();
            for(int i=1;i<plan.Count;i++)
            {
                var p=plan[i];var parent=plan[p.Parent];
                passages.Add((SocketPoint(parent,(p.Parent==0?baseModule:rooms[parent.Module]).Sockets[p.ParentSocket]),
                    SocketPoint(p,rooms[p.Module].Sockets[p.EntrySocket]),p.Parent,i));
                used.Add((p.Parent,p.ParentSocket));used.Add((i,p.EntrySocket));
            }
            for(int i=0;i<plan.Count;i++)
            {
                var module=i==0?baseModule:rooms[plan[i].Module];
                for(int s=0;s<module.Sockets.Length;s++)if(!used.Contains((i,s)))
                {var a=SocketPoint(plan[i],module.Sockets[s]);passages.Add((a,a+Direction(SocketDirection(plan[i],module.Sockets[s]))*6,i,-1));}
            }
            for(int i=0;i<passages.Count;i++)
            {
                var p=passages[i];var inner=PassageBounds(p.a,p.b);
                for(int j=0;j<plan.Count;j++) if(j!=p.from&&j!=p.to
                    &&inner.Intersects(WorldBounds(plan[j],j==0?baseModule.Footprint:rooms[plan[j].Module].Footprint)))return false;
                for(int j=0;j<i;j++)if(inner.Intersects(PassageBounds(passages[j].a,passages[j].b)))return false;
            }
            return true;
        }
        private static Bounds PassageBounds(Vector3 a,Vector3 b)
        {
            var size=new Vector3(Mathf.Abs(a.x-b.x),4,Mathf.Abs(a.z-b.z));
            if(size.x<.1f){size.x=5.6f;size.z-=.2f;}else{size.z=5.6f;size.x-=.2f;}
            return new Bounds((a+b)*.5f+Vector3.up*2,size);
        }
    }
}
