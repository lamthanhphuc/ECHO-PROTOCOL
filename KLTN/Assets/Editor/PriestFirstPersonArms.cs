using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

internal static class PriestFirstPersonArms
{
    [MenuItem("ECHO Protocol/Characters/Bake Priest First Person Arms")]
    private static void Bake()
    {
        const string path = "Assets/Resources/Characters/PF_CursedPriestVisual.prefab";
        using var scope = new PrefabUtility.EditPrefabContentsScope(path);
        var root = scope.prefabContentsRoot;
        var animator = root.GetComponent<Animator>();
        var armBones = new HashSet<Transform>();
        foreach (var arm in new[] { HumanBodyBones.LeftLowerArm, HumanBodyBones.RightLowerArm })
        {
            var bone = animator.GetBoneTransform(arm);
            if (bone == null) throw new System.InvalidOperationException("Priest forearm bone missing.");
            armBones.UnionWith(bone.GetComponentsInChildren<Transform>(true));
        }

        foreach (var source in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (source.name.StartsWith("FirstPersonArms_")) continue;
            var mesh = source.sharedMesh;
            if (mesh == null) continue;
            var bones = source.bones;
            var weights = mesh.boneWeights;
            if (weights.Length != mesh.vertexCount) continue;
            var selected = new bool[mesh.vertexCount];
            for (int i = 0; i < weights.Length; i++)
            {
                var w = weights[i];
                float Weight(int index, float amount) => amount > 0 && index < bones.Length && armBones.Contains(bones[index]) ? amount : 0f;
                selected[i] = Weight(w.boneIndex0, w.weight0) + Weight(w.boneIndex1, w.weight1)
                    + Weight(w.boneIndex2, w.weight2) + Weight(w.boneIndex3, w.weight3) >= 0.55f;
            }

            var arms = Object.Instantiate(mesh);
            int triangleCount = 0;
            for (int sub = 0; sub < mesh.subMeshCount; sub++)
            {
                var original = mesh.GetTriangles(sub);
                var triangles = new List<int>();
                for (int i = 0; i < original.Length; i += 3)
                {
                    if (!selected[original[i]] || !selected[original[i + 1]] || !selected[original[i + 2]]) continue;
                    triangles.Add(original[i]);
                    triangles.Add(original[i + 1]);
                    triangles.Add(original[i + 2]);
                }
                arms.SetTriangles(triangles, sub, false);
                triangleCount += triangles.Count / 3;
            }
            if (triangleCount == 0)
            {
                Object.DestroyImmediate(arms);
                continue;
            }

            string name = "FirstPersonArms_" + source.name;
            string meshPath = "Assets/Resources/Characters/Priest" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (existing == null) AssetDatabase.CreateAsset(arms, meshPath);
            else
            {
                EditorUtility.CopySerialized(arms, existing);
                Object.DestroyImmediate(arms);
            }

            var child = source.transform.Find(name);
            if (child == null)
            {
                child = new GameObject(name).transform;
                child.SetParent(source.transform, false);
            }
            if (!child.TryGetComponent<SkinnedMeshRenderer>(out var renderer)) renderer = child.gameObject.AddComponent<SkinnedMeshRenderer>();
            EditorUtility.CopySerialized(source, renderer);
            renderer.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            Debug.Log($"[Priest Arms] {source.name}: {triangleCount} triangles.");
        }
        AssetDatabase.SaveAssets();
    }
}
