using EchoProtocol.Diagnostics;
using UnityEditor;
using UnityEngine;

namespace EchoProtocol.Editor.Diagnostics
{
    internal static class RuntimeLogSettingsMenu
    {
        private const string ResourcesFolder =
            "Assets/Resources";

        private const string SettingsAssetPath =
            "Assets/Resources/RuntimeLogSettings.asset";

        [MenuItem(
            "Echo Protocol/Diagnostics/Open Runtime Log Settings")]
        private static void OpenRuntimeLogSettings()
        {
            if (!AssetDatabase.IsValidFolder(
                    ResourcesFolder))
            {
                AssetDatabase.CreateFolder(
                    "Assets",
                    "Resources");
            }

            var settings =
                AssetDatabase.LoadAssetAtPath<
                    RuntimeLogSettings>(
                    SettingsAssetPath);

            if (settings == null)
            {
                settings =
                    ScriptableObject.CreateInstance<
                        RuntimeLogSettings>();

                settings.name =
                    "RuntimeLogSettings";

                AssetDatabase.CreateAsset(
                    settings,
                    SettingsAssetPath);

                AssetDatabase.SaveAssets();
            }

            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }
    }
}
