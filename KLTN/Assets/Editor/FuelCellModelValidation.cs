using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using EchoProtocol.MatchFlow;

public static class FuelCellModelValidation
{
    [InitializeOnLoadMethod]
    private static void Queue()
    {
        EditorApplication.delayCall+=()=>{
            if(EditorApplication.isPlayingOrWillChangePlaymode || SessionState.GetBool("Echo.FuelCell.Validation.v2",false))return;
            if(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Network/PF_Zone3FuelCell.prefab")?.transform.Find("FuelCellVisual")==null)return;
            try{Validate();SessionState.SetBool("Echo.FuelCell.Validation.v2",true);}catch(Exception error){Debug.LogException(error);}
        };
    }
    [MenuItem("ECHO Protocol/Items/Validate Fuel Cell Model")]
    public static void Validate()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode before validation.");
        const string path="Assets/Resources/Network/PF_Zone3FuelCell.prefab";
        var root=PrefabUtility.LoadPrefabContents(path);var carrier=new GameObject("FuelCellGripValidation");
        try {
            var cell=root.GetComponent<Zone3FuelCell>();
            foreach(var rotation in new[]{Quaternion.identity,Quaternion.Euler(15,90,0),Quaternion.Euler(-20,180,0)}) {
                Vector3 handle = new Vector3(2, 1.4f, 3);
                cell.SetSingleHandGripPose(handle, rotation);
                if (Vector3.Distance(handle, root.transform.Find("RightGrip").position) > .001f)
                    throw new Exception("Single-hand grip does not match the right bar.");
                if (Vector3.Distance(root.transform.localScale, Vector3.one) > .001f)
                    throw new Exception("Fuel Cell carry scale differs from its ground scale.");
            }
            string report="Fuel Cell single-hand target matches the right bar through yaw/pitch changes; uncarried size retained.";
            File.WriteAllText(Path.GetFullPath("../.git/fuel-cell-grip-validation.txt"),report);Debug.Log(report);
        } finally {UnityEngine.Object.DestroyImmediate(carrier);PrefabUtility.UnloadPrefabContents(root);}

        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var preview=new PreviewRenderUtility();Texture2D screenshot=null;
        try {
            var model=UnityEngine.Object.Instantiate(prefab.transform.Find("FuelCellVisual").gameObject);
            preview.AddSingleGO(model);
            preview.camera.transform.position=new Vector3(.9f,.45f,1.4f);preview.camera.transform.LookAt(Vector3.zero);
            preview.camera.fieldOfView=30;preview.camera.nearClipPlane=.01f;preview.camera.farClipPlane=10f;
            preview.camera.backgroundColor=new Color(.04f,.05f,.065f);preview.camera.clearFlags=CameraClearFlags.Color;
            preview.lights[0].intensity=1.4f;preview.lights[0].transform.rotation=Quaternion.Euler(35,180,0);
            preview.lights[1].intensity=1f;preview.ambientColor=new Color(.22f,.22f,.22f);
            preview.BeginStaticPreview(new Rect(0,0,640,640));preview.Render(true);screenshot=preview.EndStaticPreview();
            File.WriteAllBytes(Path.GetFullPath("../.git/fuel-cell-preview.png"),screenshot.EncodeToPNG());
        } finally {if(screenshot!=null)UnityEngine.Object.DestroyImmediate(screenshot);preview.Cleanup();}
    }
}
