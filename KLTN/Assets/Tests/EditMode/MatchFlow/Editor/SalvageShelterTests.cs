using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using EchoProtocol.Salvage;

public sealed class SalvageShelterTests
{
    [Test]
    public void RoomPropsStayAgainstWallsAndOutsideDoorApproaches()
    {
        var sourceDoor=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Environment/Door/PF_SciFiSlidingDoor.prefab");
        var oldFrame=sourceDoor.transform.Find("Frame").GetComponent<MeshFilter>().sharedMesh;
        foreach(var room in Resources.LoadAll<SalvageMapModule>("Salvage/MapModules").Where(m=>m.name!="Salvage_Corridor"))
        {
            Assert.That(room.GetComponentsInChildren<MeshFilter>(true).Any(m=>m.sharedMesh==oldFrame),Is.False,"Orphan door in "+room.name);
            var props=room.transform.Find("SalvageWallProps");Assert.That(props,Is.Not.Null,room.name);
            foreach(Transform prop in props)
            {
                var rs=prop.GetComponentsInChildren<Renderer>(true);var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);
                float wallGap=Mathf.Min(Mathf.Abs(b.min.x-room.Footprint.min.x),Mathf.Abs(b.max.x-room.Footprint.max.x),Mathf.Abs(b.min.z-room.Footprint.min.z),Mathf.Abs(b.max.z-room.Footprint.max.z));
                Assert.That(wallGap,Is.LessThan(.65f),room.name+" / "+prop.name+" is not at a wall");
                foreach(var socket in room.Sockets)
                {
                    // The native wide base entrance keeps a metre of approach clearance on each side.
                    // Retain the original larger buffer for the narrower collection-room exits.
                    var normal=SalvageMapLayout.Direction(socket.Direction);
                    float width=Mathf.Max(3,socket.Width)+(room.name=="Salvage_Base"?2:3);
                    var lane=new Bounds(socket.Position-normal*3+Vector3.up*3,new Vector3(socket.Direction%2==0?width:6,20,socket.Direction%2==0?6:width));
                    Assert.That(lane.Intersects(b),Is.False,room.name+" / "+prop.name+" blocks a doorway");
                }
            }
        }
        var door=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Salvage/Resources/Salvage/SalvageCorridorDoor.prefab");
        var blocker=door.GetComponentsInChildren<BoxCollider>(true).Single(c=>c.name=="PassageBlocker");
        var center=blocker.transform.TransformPoint(blocker.center);
        Assert.That(new Vector2(center.x,center.z).magnitude,Is.LessThan(.01f));
        Assert.That(Mathf.Abs(center.y-blocker.size.y*blocker.transform.lossyScale.y*.5f),Is.LessThan(.01f));
    }
    [Test]
    public void DecoratedRoomsProvideSpacedFunctionalHidingLockers()
    {
        foreach(var room in Resources.LoadAll<SalvageMapModule>("Salvage/MapModules").Where(m=>m.name!="Salvage_Base"&&m.name!="Salvage_Corridor"))
        {
            var lockers=room.GetComponentsInChildren<HidingSpot>(true);
            int expected=room.name=="Salvage_Server"?1:room.name=="Salvage_Warehouse"?3:2;
            Assert.That(lockers.Length,Is.EqualTo(expected),room.name);
            for(int i=0;i<lockers.Length;i++)
            {
                Assert.That(lockers[i].HidePoint,Is.Not.Null);Assert.That(lockers[i].ExitPoint,Is.Not.Null);
                for(int j=0;j<i;j++)Assert.That(Vector3.Distance(lockers[i].transform.position,lockers[j].transform.position),Is.GreaterThanOrEqualTo(7.99f),room.name);
            }
        }
    }

    [Test]
    public void WorldModelsPreserveOriginalMeshScales()
    {
        foreach(string name in new[]{"FuelCell","AlienSpecimen","OrganicSample","FuelCanister","SurveyScanner","Resonator"})
        {
            string sourcePath=name=="SurveyScanner"?"Assets/Prefabs/Tools/PF_FieldScanner_Pickup.prefab"
                :name=="Resonator"?"Assets/Prefabs/Tools/PF_CoreStabilizer_NetworkPickup.prefab"
                :"Assets/Salvage/Resources/Salvage/Held/"+name+".prefab";
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            var world=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Salvage/Art/Scrap/"+name+".prefab");
            var original=source.GetComponentsInChildren<MeshFilter>(true);
            var actual=world.GetComponentsInChildren<MeshFilter>(true);
            Assert.That(actual.Length,Is.EqualTo(original.Length),name);
            for(int i=0;i<original.Length;i++)
            {
                Assert.That(actual[i].sharedMesh,Is.EqualTo(original[i].sharedMesh),name);
                Assert.That(Vector3.Distance(actual[i].transform.lossyScale,original[i].transform.lossyScale),Is.LessThan(.0001f),name);
            }
        }
    }
}
