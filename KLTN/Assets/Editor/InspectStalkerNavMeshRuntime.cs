using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

public static class InspectStalkerNavMeshRuntime
{
    [MenuItem("Tools/ECHO/Stalker/Inspect Selected Agent Runtime")]
    private static void Inspect()
    {
        var selected = Selection.activeGameObject;
        var agent = selected != null
            ? selected.GetComponent<NavMeshAgent>()
            : null;

        if (agent == null)
        {
            Debug.LogError("[STK_NAV] Select StalkerNetwork(Clone) first.");
            return;
        }

        Debug.Log(
            $"[STK_NAV] position={agent.transform.position} " +
            $"enabled={agent.enabled} " +
            $"isOnNavMesh={agent.isOnNavMesh} " +
            $"isStopped={agent.isStopped} " +
            $"hasPath={agent.hasPath} " +
            $"pathPending={agent.pathPending} " +
            $"pathStatus={agent.pathStatus} " +
            $"velocity={agent.velocity} " +
            $"remainingDistance={agent.remainingDistance}"
        );

        if (NavMesh.SamplePosition(
                agent.transform.position,
                out var hit,
                3f,
                agent.areaMask))
        {
            Debug.Log(
                $"[STK_NAV] nearestNavMesh={hit.position} " +
                $"distance={Vector3.Distance(agent.transform.position, hit.position):F3}");
        }
        else
        {
            Debug.LogWarning("[STK_NAV] No NavMesh found within 3m.");
        }
    }
}
