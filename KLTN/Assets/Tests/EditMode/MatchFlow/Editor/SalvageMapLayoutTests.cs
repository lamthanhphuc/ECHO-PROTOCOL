using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using EchoProtocol.Salvage;
public sealed class SalvageMapLayoutTests
{
    [Test]
    public void ModuleColliderMeshesCanBeReadByRuntimeNavMeshBuilderInPlayer()
    {
        var modules=Resources.LoadAll<GameObject>("Salvage/MapModules");
        Assert.That(modules.Length,Is.GreaterThanOrEqualTo(9));
        foreach(var module in modules)
            foreach(var collider in module.GetComponentsInChildren<MeshCollider>(true))
                if(collider.sharedMesh!=null)
                    Assert.That(collider.sharedMesh.isReadable,Is.True,module.name+" / "+collider.sharedMesh.name);
    }

    [Test]
    public void ThousandSeedsPreserveBaseAndConnectEveryRoomWithoutOverlappingRooms()
    {
        var rooms=Resources.LoadAll<SalvageMapModule>("Salvage/MapModules").Where(m=>m.name!="Salvage_Base"&&m.name!="Salvage_Corridor").OrderBy(m=>m.name).ToArray();
        var baseModule=Resources.Load<GameObject>("Salvage/MapModules/Salvage_Base").GetComponent<SalvageMapModule>();
        Assert.That(rooms.Length,Is.GreaterThanOrEqualTo(6));
        var original=new SalvageMapPlacement {Module=-1,Parent=-1,Position=new Vector3(35.96f,1.125f,-63.3f)};
        int success=0;
        for(int seed=0;seed<1000;seed++)
        {
            System.Collections.Generic.List<SalvageMapPlacement> layout=null;
            for(int retry=0;retry<24&&layout==null;retry++)
                try{var candidate=SalvageMapLayout.Generate(unchecked(seed+retry*7919),rooms,original,baseModule,8);
                    if(SalvageMapLayout.PassagesClear(candidate,rooms,baseModule))layout=candidate;}catch(InvalidOperationException){}
            Assert.That(layout,Is.Not.Null,"seed "+seed);
            Assert.That(layout[0].Position,Is.EqualTo(original.Position));
            Assert.That(SalvageMapLayout.PassagesClear(layout,rooms,baseModule),Is.True);
            var used=new System.Collections.Generic.HashSet<(int,int)>();
            for(int i=1;i<layout.Count;i++)
            {
                var p=layout[i];Assert.That(p.Parent,Is.InRange(0,i-1));
                Assert.That(used.Add((p.Parent,p.ParentSocket)),Is.True,"Exit reused");
                used.Add((i,p.EntrySocket));
                var parent=layout[p.Parent];var socket=(p.Parent==0?baseModule:rooms[parent.Module]).Sockets[p.ParentSocket];
                var entry=rooms[p.Module].Sockets[p.EntrySocket];
                var a=SalvageMapLayout.SocketPoint(parent,socket);var b=SalvageMapLayout.SocketPoint(p,entry);
                int outward=SalvageMapLayout.SocketDirection(parent,socket);
                Assert.That(Vector3.Distance(a+SalvageMapLayout.Direction(outward)*Vector3.Distance(a,b),b),Is.LessThan(.01f));
                Assert.That(SalvageMapLayout.SocketDirection(p,entry),Is.EqualTo((outward+2)%4));
                var bounds=SalvageMapLayout.WorldBounds(p,rooms[p.Module].Footprint);bounds.Expand(-.1f);
                for(int j=0;j<i;j++)Assert.That(bounds.Intersects(SalvageMapLayout.WorldBounds(layout[j],j==0?baseModule.Footprint:rooms[layout[j].Module].Footprint)),Is.False,"Room overlap");
            }
            success++;
        }
        Assert.That(success,Is.EqualTo(1000));
    }
    [Test]
    public void SeedAndLibraryOrderProduceIdenticalManifest()
    {
        var rooms=Resources.LoadAll<SalvageMapModule>("Salvage/MapModules").Where(m=>m.Sockets!=null&&m.Sockets.Length>1&&m.name!="Salvage_Corridor").OrderBy(m=>m.name).ToArray();
        var b=Resources.Load<GameObject>("Salvage/MapModules/Salvage_Base").GetComponent<SalvageMapModule>();
        var root=new SalvageMapPlacement{Parent=-1,Module=-1};
        var a=SalvageMapLayout.Generate(12345,rooms,root,b,8);var c=SalvageMapLayout.Generate(12345,rooms,root,b,8);
        Assert.That(c.Count,Is.EqualTo(a.Count));
        for(int i=0;i<a.Count;i++){Assert.That(c[i].Position,Is.EqualTo(a[i].Position));Assert.That(c[i].Module,Is.EqualTo(a[i].Module));Assert.That(c[i].QuarterTurns,Is.EqualTo(a[i].QuarterTurns));}
    }
}
