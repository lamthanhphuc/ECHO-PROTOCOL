using EchoProtocol.RelayA;
using EchoProtocol.RelayB;
using UnityEngine;

namespace EchoProtocol.MatchFlow
{
    public static class Zone2MinigameUIFocus
    {
        public static void CloseOthers(Object keepOpen)
        {
            foreach (var ui in Object.FindObjectsByType<RelayAUIController>(FindObjectsInactive.Include))
            {
                if (ui != null && ui != keepOpen && ui.IsOpen) ui.Close();
            }

            foreach (var ui in Object.FindObjectsByType<RelayBUIController>(FindObjectsInactive.Include))
            {
                if (ui != null && ui != keepOpen && ui.IsOpen) ui.Close();
            }

            foreach (var ui in Object.FindObjectsByType<SecurityTerminalUIController>(FindObjectsInactive.Include))
            {
                if (ui != null && ui != keepOpen && ui.IsOpen) ui.Close();
            }
        }
    }
}
