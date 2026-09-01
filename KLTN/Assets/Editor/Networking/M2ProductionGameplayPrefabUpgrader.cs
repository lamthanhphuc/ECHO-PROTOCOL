using EchoProtocol.Networking;
using UnityEditor;
using UnityEngine;

namespace EchoProtocol.Editor.Networking
{
    [InitializeOnLoad]
    internal static class M2ProductionGameplayPrefabUpgrader
    {
        private const string PlayerPrefabPath =
            "Assets/_Project/Prefabs/Network/TestNetworkPlayer.prefab";
        private const string RuntimePlayerPrefabPath =
            "Assets/Prefabs/PlayerNetwork.prefab";
        private const string SectorBoxPrefabPath =
            "Assets/Resources/Network/NetworkSectorBox.prefab";

        static M2ProductionGameplayPrefabUpgrader()
        {
            EditorApplication.delayCall += UpgradePlayerPrefabIfNeeded;
            EditorApplication.delayCall += CreateSectorBoxPrefabIfNeeded;
        }

        [MenuItem("ECHO Protocol/M2/Upgrade Production Gameplay Prefabs")]
        private static void UpgradeAll()
        {
            UpgradePlayerPrefabIfNeeded();
            CreateSectorBoxPrefabIfNeeded();
        }

        private static void UpgradePlayerPrefabIfNeeded()
        {
            UpgradePlayerPrefabAtPath(PlayerPrefabPath);
            UpgradePlayerPrefabAtPath(RuntimePlayerPrefabPath);
        }

        private static void UpgradePlayerPrefabAtPath(string prefabPath)
        {
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            if (root == null)
            {
                Debug.LogError($"[M2 Prefab Upgrade] Player prefab not found: {prefabPath}");
                return;
            }

            try
            {
                if (root.GetComponent<NetworkPlayerLifeState>() != null)
                {
                    return;
                }

                var component = root.AddComponent<NetworkPlayerLifeState>();
                if (component == null)
                {
                    Debug.LogError("[M2 Prefab Upgrade] Unity could not create NetworkPlayerLifeState.");
                    return;
                }

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out var saved);
                if (saved)
                {
                    Debug.Log($"[M2 Prefab Upgrade] Added NetworkPlayerLifeState to {prefabPath}.");
                }
                else
                {
                    Debug.LogError($"[M2 Prefab Upgrade] Failed to save {prefabPath}; prefab left unchanged.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void CreateSectorBoxPrefabIfNeeded()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(SectorBoxPrefabPath) != null) return;

            const string directory = "Assets/Resources/Network";
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }
            if (!AssetDatabase.IsValidFolder(directory))
            {
                AssetDatabase.CreateFolder("Assets/Resources", "Network");
            }

            var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = "NetworkSectorBox";
            root.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);
            var collider = root.GetComponent<BoxCollider>();
            collider.isTrigger = true;
            root.AddComponent<Fusion.NetworkObject>();
            root.AddComponent<NetworkSectorBox>();
            PrefabUtility.SaveAsPrefabAsset(root, SectorBoxPrefabPath, out var saved);
            Object.DestroyImmediate(root);
            if (saved) Debug.Log("[M2 Prefab Upgrade] Created NetworkSectorBox prefab.");
            else Debug.LogError("[M2 Prefab Upgrade] Failed to create NetworkSectorBox prefab.");
        }
    }
}
