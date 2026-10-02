using UnityEditor;
using UnityEngine;
using EchoProtocol.Gameplay;

public static class SpaceFrigatePrefabSetup
{
    private static readonly string[] PrefabPaths = new[]
    {
        "Assets/SpaceFrigate/Prefabs/Spacefrigate.prefab",
        "Assets/Prefabs/Gameplay/Spacefrigate.prefab"
    };

    [InitializeOnLoadMethod]
    private static void Init()
    {
        EditorApplication.delayCall += () =>
        {
            SetupPrefabIfNeeded();
        };
    }

    [MenuItem("ECHO PROTOCOL/Tools/Setup SpaceFrigate Pushable Prefab")]
    public static void SetupPrefabMenuItem()
    {
        SetupPrefab(force: true);
    }

    private static void SetupPrefabIfNeeded()
    {
        SetupPrefab(force: false);
    }

    private static void SetupPrefab(bool force)
    {
        foreach (var path in PrefabPaths)
        {
            SetupSinglePrefab(path, force);
        }
    }

    private static void SetupSinglePrefab(string prefabPath, bool force)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null) return;

        bool hasPushable = prefab.GetComponent<PushableObject>() != null;
        bool hasRb = prefab.GetComponent<Rigidbody>() != null;

        if (!force && hasPushable && hasRb)
        {
            return;
        }

        using (var editScope = new PrefabUtility.EditPrefabContentsScope(prefabPath))
        {
            var root = editScope.prefabContentsRoot;

            var rb = root.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = root.AddComponent<Rigidbody>();
            }

            rb.mass = 2000f;
            rb.linearDamping = 5f;
            rb.angularDamping = 5f;
            rb.useGravity = false;
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.constraints = RigidbodyConstraints.FreezeRotationX
                           | RigidbodyConstraints.FreezeRotationZ;

            var col = root.GetComponent<BoxCollider>();

            var pushable = root.GetComponent<PushableObject>();
            if (pushable == null)
            {
                pushable = root.AddComponent<PushableObject>();
            }

            var so = new SerializedObject(pushable);
            so.FindProperty("pushSpeed").floatValue = 1.4f;
            so.FindProperty("acceleration").floatValue = 2.0f;
            so.FindProperty("deceleration").floatValue = 4.0f;
            so.FindProperty("maxTurnSpeed").floatValue = 25.0f;
            so.FindProperty("rotationDeadZone").floatValue = 0.12f;
            so.FindProperty("maxInteractionDistance").floatValue = 4.0f;
            so.FindProperty("groundCheckDistance").floatValue = 4.0f;

            if (col != null)
            {
                so.FindProperty("pushCollider").objectReferenceValue = col;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[ECHO] Successfully configured Spacefrigate.prefab with Rigidbody and PushableObject.");
    }
}
