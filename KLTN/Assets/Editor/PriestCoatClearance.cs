using UnityEditor;
using UnityEngine;
using System.Linq;

internal static class PriestCoatClearance
{
    [MenuItem("ECHO Protocol/Characters/Bake Priest Coat Clearance")]
    private static void Bake()
    {
        const string source = "Assets/Cursed Priest/Mesh/Priest.fbx";
        const string prefabPath = "Assets/Resources/Characters/PF_CursedPriestVisual.prefab";
        const string meshPath = "Assets/Resources/Characters/PriestCoatClearance.asset";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        var renderer = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(item => item.name == "Upper");
        var meshSource = AssetDatabase.LoadAllAssetsAtPath(source).OfType<Mesh>().Single(mesh => mesh.name == "Upper");
        var mesh = Object.Instantiate(meshSource);
        mesh.name = "Priest Coat Clearance";
        var vertices = mesh.vertices;
        var normals = mesh.normals;
        for (int i = 0; i < vertices.Length; i++)
            vertices[i] += normals[i] * 0.012f;
        mesh.vertices = vertices;
        mesh.RecalculateBounds();
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if (existing == null) AssetDatabase.CreateAsset(mesh, meshPath);
        else
        {
            EditorUtility.CopySerialized(mesh, existing);
            Object.DestroyImmediate(mesh);
        }
        renderer.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        EditorUtility.SetDirty(renderer);
        PrefabUtility.SavePrefabAsset(prefab);
        AssetDatabase.SaveAssets();
    }
}
