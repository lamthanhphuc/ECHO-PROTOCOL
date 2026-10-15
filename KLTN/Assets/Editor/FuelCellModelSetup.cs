using System;
using System.IO;
using System.Linq;
using EchoProtocol.MatchFlow;
using UnityEditor;
using UnityEngine;

public static class FuelCellModelSetup
{
    private const string PrefabPath="Assets/Resources/Network/PF_Zone3FuelCell.prefab";
    private const string Source="Assets/import/fuel cell/source/PowerCell.fbx";
    private const string Textures="Assets/import/fuel cell/textures/";
    private const string Output="Assets/Resources/Network/FuelCell/";
    private const string Revision="Echo.FuelCell.PowerCell.v1";

    [InitializeOnLoadMethod]
    private static void QueueImportedModel()
    {
        EditorApplication.delayCall+=()=>{
            if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling
                || SessionState.GetBool(Revision,false) || AssetDatabase.LoadAssetAtPath<GameObject>(Source)==null)return;
            var existing=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if(existing!=null && existing.transform.Find("FuelCellVisual")!=null){SessionState.SetBool(Revision,true);return;}
            try{Apply();SessionState.SetBool(Revision,true);}
            catch(Exception error){Debug.LogException(error);}
        };
    }

    [MenuItem("ECHO Protocol/Items/Apply Imported Fuel Cell Model")]
    public static void Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode before rebuilding Fuel Cell.");
        var model=AssetDatabase.LoadAssetAtPath<GameObject>(Source);
        if(model==null)throw new InvalidOperationException("PowerCell FBX is not imported yet.");
        Directory.CreateDirectory(Output);AssetDatabase.Refresh();
        var material=BuildMaterial();
        var root=PrefabUtility.LoadPrefabContents(PrefabPath);
        try {
            root.transform.localScale=Vector3.one;root.transform.rotation=Quaternion.identity;root.transform.position=Vector3.zero;
            var previous=root.transform.Find("FuelCellVisual");if(previous!=null)UnityEngine.Object.DestroyImmediate(previous.gameObject);
            // Keep inherited components and network prefab identity; hide only the old model.
            foreach(Transform child in root.transform)child.gameObject.SetActive(false);
            var visual=new GameObject("FuelCellVisual");visual.transform.SetParent(root.transform,false);
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(model,root.scene);instance.transform.SetParent(visual.transform,false);
            foreach(var light in instance.GetComponentsInChildren<Light>(true))UnityEngine.Object.DestroyImmediate(light);
            foreach(var camera in instance.GetComponentsInChildren<Camera>(true))UnityEngine.Object.DestroyImmediate(camera);
            foreach(var collider in instance.GetComponentsInChildren<Collider>(true))UnityEngine.Object.DestroyImmediate(collider);
            var bounds=BoundsOf(instance);
            if(bounds.size.z>bounds.size.y*1.3f)instance.transform.localRotation=Quaternion.Euler(-90,0,0);
            else if(bounds.size.x>bounds.size.y*1.3f)instance.transform.localRotation=Quaternion.Euler(0,0,90);
            bounds=BoundsOf(instance);
            if(bounds.size.z>bounds.size.x*1.15f){instance.transform.localRotation=Quaternion.Euler(0,90,0)*instance.transform.localRotation;bounds=BoundsOf(instance);}
            float scale=.62f/Mathf.Max(.0001f,bounds.size.y);instance.transform.localScale*=scale;
            bounds=BoundsOf(instance);instance.transform.position-=bounds.center;
            foreach(var renderer in instance.GetComponentsInChildren<Renderer>(true)) {
                renderer.sharedMaterials=Enumerable.Repeat(material,renderer.sharedMaterials.Length).ToArray();
            }
            bounds=BoundsOf(instance);
            var box=root.GetComponent<BoxCollider>();if(box==null)throw new InvalidOperationException("Fuel cell pickup collider missing.");
            box.center=bounds.center;box.size=bounds.size;box.isTrigger=true;
            var fuel=root.GetComponent<Zone3FuelCell>();var data=new SerializedObject(fuel);
            // Measured straight handle centreline of PowerCell, at the normalized .62m height.
            float gripX=.19132f*(bounds.size.y/.62f);
            data.FindProperty("leftGripLocalPosition").vector3Value=new Vector3(-gripX,0,0);
            data.FindProperty("rightGripLocalPosition").vector3Value=new Vector3(gripX,0,0);
            data.ApplyModifiedPropertiesWithoutUndo();
            foreach(var pair in new[]{("LeftGrip",-gripX),("RightGrip",gripX)}) {
                var grip=root.transform.Find(pair.Item1);
                if(grip==null){grip=new GameObject(pair.Item1).transform;grip.SetParent(root.transform,false);}
                grip.localPosition=new Vector3(pair.Item2,0,0);
            }
            var glow=visual.AddComponent<EchoProtocol.Visuals.ObjectiveGlowHighlight>();
            glow.CurrentObjectiveType=EchoProtocol.Visuals.ObjectiveGlowHighlight.ObjectiveType.FuelCell;
            PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);AssetDatabase.SaveAssets();
            string report=$"FuelCell model applied. Height={bounds.size.y:F3}m, width={bounds.size.x:F3}m, handles=+/-{gripX:F3}m. Prefab network identity preserved.";
            Debug.Log(report);
            File.WriteAllText(Path.GetFullPath("../.git/fuel-cell-model-validation.txt"),report);
        } finally {PrefabUtility.UnloadPrefabContents(root);}
    }
    private static Bounds BoundsOf(GameObject root)
    {
        var renderers=root.GetComponentsInChildren<Renderer>(true);
        if(renderers.Length==0)throw new InvalidOperationException("PowerCell FBX has no renderable mesh.");
        var bounds=renderers[0].bounds;foreach(var renderer in renderers.Skip(1))bounds.Encapsulate(renderer.bounds);return bounds;
    }
    private static Texture2D Texture(string file,bool normal=false)
    {
        string path=Textures+file;var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        if(importer==null)throw new InvalidOperationException("Missing Fuel Cell texture: "+file);
        var type=normal ? TextureImporterType.NormalMap:TextureImporterType.Default;
        bool srgb=file.Contains("BaseColor");
        if(importer.textureType!=type || importer.sRGBTexture!=srgb){importer.textureType=type;importer.sRGBTexture=srgb;importer.SaveAndReimport();}
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
    private static Material BuildMaterial()
    {
        var material=AssetDatabase.LoadAssetAtPath<Material>(Output+"M_FuelCell.mat");
        if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,Output+"M_FuelCell.mat");}
        material.SetTexture("_BaseMap",Texture("PowerCell_PowerCell_BaseColor.png"));material.SetColor("_BaseColor",Color.white);
        material.SetTexture("_BumpMap",Texture("PowerCell_PowerCell_Normal.png",true));material.EnableKeyword("_NORMALMAP");
        material.SetTexture("_OcclusionMap",Texture("PowerCell_ao.png"));
        // Pack metallic R and inverse roughness A for URP's metallic workflow.
        string metalPath=Textures+"PowerCell_PowerCell_Metallic.png",roughPath=Textures+"PowerCell_PowerCell_Roughness.png";
        Texture("PowerCell_PowerCell_Metallic.png");Texture("PowerCell_PowerCell_Roughness.png");
        var metalImporter=(TextureImporter)AssetImporter.GetAtPath(metalPath);var roughImporter=(TextureImporter)AssetImporter.GetAtPath(roughPath);
        bool oldMetal=metalImporter.isReadable,oldRough=roughImporter.isReadable;
        try {
            if(!oldMetal){metalImporter.isReadable=true;metalImporter.SaveAndReimport();}
            if(!oldRough){roughImporter.isReadable=true;roughImporter.SaveAndReimport();}
            var metal=AssetDatabase.LoadAssetAtPath<Texture2D>(metalPath);var rough=AssetDatabase.LoadAssetAtPath<Texture2D>(roughPath);
            var pixels=metal.GetPixels32();
            for(int y=0;y<metal.height;y++)for(int x=0;x<metal.width;x++){
                int i=y*metal.width+x;pixels[i].a=(byte)Mathf.RoundToInt((1f-rough.GetPixelBilinear(x/(float)metal.width,y/(float)metal.height).r)*255f);
            }
            var packed=new Texture2D(metal.width,metal.height,TextureFormat.RGBA32,false,true);packed.SetPixels32(pixels);packed.Apply();
            File.WriteAllBytes(Output+"FuelCell_MetallicSmoothness.png",packed.EncodeToPNG());UnityEngine.Object.DestroyImmediate(packed);
            AssetDatabase.ImportAsset(Output+"FuelCell_MetallicSmoothness.png",ImportAssetOptions.ForceUpdate);
            var packedImporter=(TextureImporter)AssetImporter.GetAtPath(Output+"FuelCell_MetallicSmoothness.png");packedImporter.sRGBTexture=false;packedImporter.SaveAndReimport();
            material.SetTexture("_MetallicGlossMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Output+"FuelCell_MetallicSmoothness.png"));
            material.SetFloat("_Metallic",1f);material.SetFloat("_Smoothness",1f);material.EnableKeyword("_METALLICSPECGLOSSMAP");
            EditorUtility.SetDirty(material);return material;
        } finally {
            if(metalImporter.isReadable!=oldMetal){metalImporter.isReadable=oldMetal;metalImporter.SaveAndReimport();}
            if(roughImporter.isReadable!=oldRough){roughImporter.isReadable=oldRough;roughImporter.SaveAndReimport();}
        }
    }
}


