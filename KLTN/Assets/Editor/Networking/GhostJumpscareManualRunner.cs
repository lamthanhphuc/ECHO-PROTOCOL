using UnityEditor;

namespace EchoProtocol.Editor.Networking
{
    internal static class GhostJumpscareManualRunner
    {
        [MenuItem("Tools/ECHO/Rebuild Stalker Jumpscare", false, 500)]
        private static void Rebuild()
        {
            GhostJumpscarePrefabSetup.Setup();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}
