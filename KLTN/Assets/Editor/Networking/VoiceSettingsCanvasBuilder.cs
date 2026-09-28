using System.IO;
using EchoProtocol.Voice;
using UnityEditor;
using UnityEngine;

namespace EchoProtocol.Editor.Networking
{
    public static class VoiceSettingsCanvasBuilder
    {
        private const string PrefabPath = "Assets/Resources/Voice/VoiceSettingsCanvas.prefab";
        private const string RebuildRequest = "Temp/VoiceSettingsCanvas.rebuild.request";

        [InitializeOnLoadMethod]
        private static void CreateIfMissingAfterImport()
        {
            EditorApplication.update -= RebuildIfRequested;
            EditorApplication.update += RebuildIfRequested;
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling
                    || AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null) return;
                Build();
            };
        }

        private static void RebuildIfRequested()
        {
            if (!File.Exists(RebuildRequest) || EditorApplication.isPlayingOrWillChangePlaymode
                || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            File.Delete(RebuildRequest);
            Rebuild();
        }

        [MenuItem("ECHO Protocol/Setup/Create Voice Settings Canvas")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            {
                Debug.LogWarning("Create the voice settings canvas in Edit mode after scripts finish compiling.");
                return;
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
            {
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                return;
            }

            SavePrefab();
        }

        [MenuItem("ECHO Protocol/Setup/Redesign Voice Settings Canvas")]
        public static void Rebuild()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            {
                Debug.LogWarning("Redesign the voice settings canvas in Edit mode after scripts finish compiling.");
                return;
            }
            SavePrefab();
        }

        private static void SavePrefab()
        {
            var canvas = VoiceSettingsCanvasFactory.Create();
            try
            {
                var prefab = PrefabUtility.SaveAsPrefabAsset(canvas, PrefabPath);
                Selection.activeObject = prefab;
                Debug.Log("Voice settings Canvas saved: " + PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }
    }
}
