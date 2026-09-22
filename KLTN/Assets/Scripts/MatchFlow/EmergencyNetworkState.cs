using System.Collections.Generic;
using EchoProtocol.MatchFlow;
using UnityEngine;

public static class EmergencyNetworkState
{
    public static bool AreRelaysOnline()
    {
        // 1. Zone2MissionDirector if available
        if (Zone2MissionDirector.Instance != null)
        {
            return Zone2MissionDirector.Instance.AreAllRelaysOnline;
        }

        // 2. Fusion Multiplayer check via NetworkMatchState
        var matchState = UnityEngine.Object.FindAnyObjectByType<EchoProtocol.Networking.NetworkMatchState>();
        if (matchState != null && matchState.Object != null && matchState.Object.IsValid)
        {
            return matchState.AreAllRelaysOnline;
        }

        // 3. Check all Relay controllers present in the scene
        var relayAs = UnityEngine.Object.FindObjectsByType<EchoProtocol.RelayA.RelayAController>(FindObjectsInactive.Exclude);
        var relayBs = UnityEngine.Object.FindObjectsByType<EchoProtocol.RelayB.RelayBController>(FindObjectsInactive.Exclude);

        if (relayAs.Length > 0 || relayBs.Length > 0)
        {
            for (int i = 0; i < relayAs.Length; i++)
            {
                if (relayAs[i] != null && !relayAs[i].IsOnline) return false;
            }
            for (int i = 0; i < relayBs.Length; i++)
            {
                if (relayBs[i] != null && !relayBs[i].IsOnline) return false;
            }
            return true;
        }

        // 4. Fallback for test contexts where no relays exist
        return true;
    }

    public static bool IsRelayAOnline()
    {
        var relayAs = UnityEngine.Object.FindObjectsByType<EchoProtocol.RelayA.RelayAController>(FindObjectsInactive.Exclude);
        if (relayAs.Length > 0)
        {
            for (int i = 0; i < relayAs.Length; i++)
            {
                if (relayAs[i] != null && !relayAs[i].IsOnline) return false;
            }
            return true;
        }

        var relayANet = UnityEngine.Object.FindAnyObjectByType<EchoProtocol.RelayA.RelayANetworkState>();
        if (relayANet != null)
        {
            return relayANet.RelayAOnline;
        }

        return true;
    }

    public static bool IsRelayBOnline()
    {
        var relayBs = UnityEngine.Object.FindObjectsByType<EchoProtocol.RelayB.RelayBController>(FindObjectsInactive.Exclude);
        if (relayBs.Length > 0)
        {
            for (int i = 0; i < relayBs.Length; i++)
            {
                if (relayBs[i] != null && !relayBs[i].IsOnline) return false;
            }
            return true;
        }

        var relayBNet = UnityEngine.Object.FindAnyObjectByType<EchoProtocol.RelayB.RelayBNetworkState>();
        if (relayBNet != null)
        {
            return relayBNet.RelayBOnline;
        }

        return true;
    }
}
