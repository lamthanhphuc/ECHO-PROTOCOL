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
        {
            var sourceVertex = vertices[i];
            vertices[i] += normals[i] * 0.012f;

            // The source FBX uses Z for height and positive Y for the coat back.
            // The Happy Idle leg motion needs extra clearance behind the lower skirt.
            float back = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.10f, 0.20f, sourceVertex.y));
            float lowerEdge = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 0.50f, sourceVertex.z));
            float upperEdge = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.95f, 1.10f, sourceVertex.z));
            vertices[i].y += 0.012f * back * lowerEdge * upperEdge;
        }
        mesh.vertices = vertices;
        mesh.RecalculateBounds();
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if (existing == null) AssetDatabase.CreateAsset(mesh, meshPath);
        else
        {
            EditorUtility.CopySerialized(mesh, existing);
            existing.vertices = mesh.vertices;
            existing.UploadMeshData(false);
            Object.DestroyImmediate(mesh);
        }
        renderer.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        EditorUtility.SetDirty(renderer);
        PrefabUtility.SavePrefabAsset(prefab);
        AssetDatabase.SaveAssetIfDirty(renderer.sharedMesh);
    }
}
