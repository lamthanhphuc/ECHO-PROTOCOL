using UnityEngine;
using EchoProtocol.Networking;

// Environment queries ignore characters and portable props, but still respect doors/walls.
public static class GameplayEnvironmentPhysics
{
    private static readonly RaycastHit[] Hits = new RaycastHit[64];
    private static readonly Collider[] Overlaps = new Collider[64];
    public static bool IsEnvironment(Collider collider) => collider != null
        && !collider.isTrigger
        && collider.GetComponentInParent<NetworkPlayerMovement>() == null
        && collider.GetComponentInParent<PlayerMovement>() == null
        && collider.GetComponentInParent<NetworkPickupItem>() == null
        && collider.GetComponentInParent<EnergyCorePickup>() == null
        && collider.GetComponentInParent<NetworkTeamToolPickup>() == null
        && collider.GetComponentInParent<EchoProtocol.Tools.Scanner.NetworkToolPickup>() == null
        && collider.GetComponentInParent<EchoProtocol.MatchFlow.Zone3FuelCell>() == null;
    public static bool Raycast(Vector3 origin, Vector3 direction, out RaycastHit hit, float distance,
        int mask, QueryTriggerInteraction query)
    {
        int count = Physics.RaycastNonAlloc(origin, direction, Hits, distance, mask, query);
        hit = default; float closest = float.PositiveInfinity;
        for (int i = 0; i < count; i++) if (IsEnvironment(Hits[i].collider) && Hits[i].distance < closest)
        { hit = Hits[i]; closest = hit.distance; }
        return closest < float.PositiveInfinity;
    }
    public static bool Raycast(Vector3 origin, Vector3 direction, float distance, int mask, QueryTriggerInteraction query)
        => Raycast(origin, direction, out _, distance, mask, query);
    public static bool Linecast(Vector3 start, Vector3 end, int mask, QueryTriggerInteraction query)
        => Raycast(start, (end-start).normalized, out _, Vector3.Distance(start,end), mask, query);
    public static bool SphereCast(Vector3 origin, float radius, Vector3 direction, out RaycastHit hit,
        float distance, int mask, QueryTriggerInteraction query)
    {
        int count = Physics.SphereCastNonAlloc(origin, radius, direction, Hits, distance, mask, query);
        hit = default; float closest = float.PositiveInfinity;
        for (int i = 0; i < count; i++) if (IsEnvironment(Hits[i].collider) && Hits[i].distance < closest)
        { hit = Hits[i]; closest = hit.distance; }
        return closest < float.PositiveInfinity;
    }
    public static bool CheckCapsule(Vector3 start, Vector3 end, float radius, int mask, QueryTriggerInteraction query)
    {
        int count = Physics.OverlapCapsuleNonAlloc(start,end,radius,Overlaps,mask,query);
        for(int i=0;i<count;i++) if(IsEnvironment(Overlaps[i])) return true;
        return false;
    }
}
