using UnityEditor;

namespace EchoProtocol.Editor.Networking
{
    internal static class GhostJumpscareSetupMenu
    {
        [MenuItem(
            "ECHO Protocol/Setup/Rebuild Ghost Jumpscare",
            priority = 200)]
        private static void RebuildGhostJumpscare()
        {
            GhostJumpscarePrefabSetup.Setup();
        }
    }
}