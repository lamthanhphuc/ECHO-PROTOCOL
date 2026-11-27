using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace EchoProtocol.Salvage.Editor
{
    public static class SalvageLayoutAuthoring
    {
        private static Bounds BoundsOf(GameObject root)
        {var rs=root.GetComponentsInChildren<Renderer>(true);var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);return b;}
        private static bool Shelf(Transform t)=>t.name.IndexOf("shelf",StringComparison.OrdinalIgnoreCase)>=0||t.name.IndexOf("shelve",StringComparison.OrdinalIgnoreCase)>=0;
        private static bool ShelfAncestor(Transform t,Transform limit)
        {for(var p=t.parent;p!=null&&p!=limit;p=p.parent)if(Shelf(p))return true;return false;}
        public static string RestoreShelves()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Edit mode required");
            var site=UnityEngine.Object.FindAnyObjectByType<SalvageSite>();
            if(site==null||site.gameObject.scene.path!="Assets/Salvage/Scenes/Salvage_SciFi.unity")throw new InvalidOperationException("Salvage scene required");
            var preview=EditorSceneManager.OpenPreviewScene("Assets/Scenes/SciFi.unity");string report="";
            try
            {
                var sourceRoot=preview.GetRootGameObjects().First(g=>g.name=="GameObject").transform;
                foreach(var asset in site.RandomMap.Rooms)
                {
                    var original=sourceRoot.Find(asset.SourcePath.Contains("/")?asset.SourcePath:"Zone01_ResearchStorage/"+asset.SourcePath);
                    if(original==null)throw new InvalidOperationException("Missing source room "+asset.SourcePath);
                    var shelves=original.GetComponentsInChildren<Transform>(true).Where(t=>Shelf(t)&&!ShelfAncestor(t,original)).ToArray();
                    if(shelves.Length==0){report+=asset.name+": no source shelves\n";continue;}
                    var floors=original.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.name.IndexOf("floor",StringComparison.OrdinalIgnoreCase)>=0&&!r.name.Contains("Wall")&&r.bounds.size.y<.6f).ToArray();
                    var floor=floors[0].bounds;foreach(var r in floors)floor.Encapsulate(r.bounds);
                    var pivot=new Vector3(floor.center.x,floor.max.y,floor.center.z);
                    string path=AssetDatabase.GetAssetPath(asset);var room=PrefabUtility.LoadPrefabContents(path);
                    try
                    {
                        foreach(var old in room.GetComponentsInChildren<Transform>(true).Where(t=>Shelf(t)&&!ShelfAncestor(t,room.transform)).ToArray())UnityEngine.Object.DestroyImmediate(old.gameObject);
                        var parent=new GameObject("SalvageAuthoredShelves").transform;parent.SetParent(room.transform,false);
                        foreach(var shelf in shelves)
                        {
                            var clone=UnityEngine.Object.Instantiate(shelf.gameObject,parent);clone.name=shelf.name;
                            clone.transform.localPosition=shelf.position-pivot;clone.transform.localRotation=shelf.rotation;clone.transform.localScale=shelf.lossyScale;
                            foreach(var component in clone.GetComponentsInChildren<Component>(true).Reverse())
                                if(component!=null&&component is not Transform&&component is not Renderer&&component is not MeshFilter&&component is not Collider&&component is not Light&&component is not LODGroup)
                                    UnityEngine.Object.DestroyImmediate(component);
                            foreach(var c in clone.GetComponentsInChildren<MeshCollider>(true))
                            {
                                if(c.sharedMesh==null||c.sharedMesh.isReadable)continue;
                                var readable=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Salvage/Art/NavigationMeshes/SalvageNav_"+c.sharedMesh.name+".asset");
                                if(readable==null)throw new InvalidOperationException("Missing readable shelf mesh "+c.sharedMesh.name);
                                c.sharedMesh=readable;
                            }
                            foreach(var t in clone.GetComponentsInChildren<Transform>(true))GameObjectUtility.SetStaticEditorFlags(t.gameObject,0);
                            foreach(var r in clone.GetComponentsInChildren<Renderer>(true)){r.lightmapIndex=-1;r.realtimeLightmapIndex=-1;}
                        }
                        PrefabUtility.SaveAsPrefabAsset(room,path);report+=asset.name+": restored "+shelves.Length+" shelf groups from SciFi\n";
                    }
                    finally{PrefabUtility.UnloadPrefabContents(room);}
                }
                AssetDatabase.SaveAssets();return report;
            }
            finally{EditorSceneManager.ClosePreviewScene(preview);}
        }
        public static string Apply()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Edit mode required");
            var site=UnityEngine.Object.FindAnyObjectByType<SalvageSite>();
            if(site==null||site.gameObject.scene.path!="Assets/Salvage/Scenes/Salvage_SciFi.unity")throw new InvalidOperationException("Open Salvage scene first");
            string report="";
            for(int i=16;i<SalvageLedger.CartDefinition;i++)
            {
                if(SalvageLedger.Types[i].Pocketable)continue;
                string path=AssetDatabase.GetAssetPath(site.ScrapVisuals[i]);var root=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var model=root.transform.GetChild(0);
                    string folder=i<18?"Assets/SciFi Warehouse Kit/Prefabs/Props/Misc Props/":"Assets/Flooded_Grounds/Prefabs/Props/";
                    var source=AssetDatabase.LoadAssetAtPath<GameObject>(folder+model.name.Replace("(Clone)","")+".prefab");
                    if(source==null)throw new InvalidOperationException("Missing native source for "+model.name);
                    model.localScale=source.transform.localScale;
                    var b=BoundsOf(root);model.position-=new Vector3(b.center.x,b.min.y,b.center.z);
                    site.ScrapSizes[i]=BoundsOf(root).size;
                    PrefabUtility.SaveAsPrefabAsset(root,path);report+=SalvageLedger.Types[i].Name+": "+site.ScrapSizes[i]+"\n";
                }
                finally{PrefabUtility.UnloadPrefabContents(root);}
            }
            var locker=Resources.Load<GameObject>("Salvage/SalvageHidingLocker");
            foreach(var asset in site.RandomMap.Rooms)
            {
                var room=UnityEngine.Object.Instantiate(asset,new Vector3(10000,0,10000),Quaternion.identity);
                try
                {
                    int wanted=room.GetComponentsInChildren<HidingSpot>(true).Length;
                    foreach(var old in room.GetComponentsInChildren<HidingSpot>(true))UnityEngine.Object.DestroyImmediate(old.gameObject);
                    Physics.SyncTransforms();var b=room.Footprint;
                    var candidates=new List<(Vector3 p,Quaternion q)>();
                    for(int side=0;side<4;side++)
                    {
                        var inward=SalvageMapLayout.Direction((side+2)%4);var rotation=Quaternion.LookRotation(inward);
                        bool horizontal=side%2==0;float extent=horizontal?b.extents.x:b.extents.z;
                        foreach(float inset in new[]{.85f,2f,3.5f})for(float t=-extent+2;t<=extent-2;t+=2)
                        {
                            var local=b.center;local.y=0;
                            if(horizontal){local.x+=t;local.z+=side==0?b.extents.z-inset:-b.extents.z+inset;}
                            else{local.z+=t;local.x+=side==1?b.extents.x-inset:-b.extents.x+inset;}
                            if(room.Sockets.Any(s=>Vector3.Distance(s.Position,local)<5))continue;
                            var point=room.transform.TransformPoint(local);
                            if(!Physics.Raycast(point+Vector3.up*2,Vector3.down,out var floor,3,~0,QueryTriggerInteraction.Ignore))continue;
                            point.y=floor.point.y;
                            if(Physics.CheckBox(point+Vector3.up*1.05f,new Vector3(.65f,.95f,.55f),rotation,~0,QueryTriggerInteraction.Ignore)
                                ||Physics.CheckBox(point+inward*1.1f+Vector3.up,new Vector3(.4f,.85f,.4f),rotation,~0,QueryTriggerInteraction.Ignore))continue;
                            candidates.Add((point,rotation));
                        }
                    }
                    var selected=new List<Vector3>();
                    for(int n=0;n<wanted;n++)
                    {
                        var options=candidates.Where(c=>selected.All(p=>Vector3.Distance(p,c.p)>=16)).ToArray();if(options.Length==0)break;
                        var choice=options.OrderByDescending(c=>selected.Count==0?Vector3.Distance(c.p,room.transform.position):selected.Min(p=>Vector3.Distance(p,c.p))).First();
                        var instance=UnityEngine.Object.Instantiate(locker,choice.p,choice.q,room.transform);instance.name="SalvageHidingLocker_"+n;selected.Add(choice.p);
                    }
                    if(selected.Count==0)throw new InvalidOperationException("No safe hiding position: "+asset.name);
                    room.transform.position=Vector3.zero;room.name=asset.name;
                    PrefabUtility.SaveAsPrefabAsset(room.gameObject,AssetDatabase.GetAssetPath(asset));report+=asset.name+": "+selected.Count+" lockers, 16m minimum\n";
                }
                finally{UnityEngine.Object.DestroyImmediate(room.gameObject);}
            }
            EditorUtility.SetDirty(site);EditorSceneManager.MarkSceneDirty(site.gameObject.scene);EditorSceneManager.SaveScene(site.gameObject.scene);
            AssetDatabase.SaveAssets();return report;
        }
    }
}
