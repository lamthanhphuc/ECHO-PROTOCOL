using UnityEngine;

/// <summary>
/// Shared safe placement resolver for dropped items.
/// Prevents drops through walls and backs the point toward the player when the forward candidate has no valid floor.
/// </summary>
public static class ItemDropPlacementUtility
{
    private const float FloorProbeHeight = 1.5f;
    private const float MaxFloorProbeDistance = 4f;
    private const float ObstacleProbeHeight = 0.75f;
    private const float ObstacleProbeRadius = 0.25f;
    private const float ObstaclePadding = 0.10f;
    private const float MinimumFloorNormalY = 0.55f;
    private const int FloorBackoffSteps = 4;

    public static void GetFloorSnappedPose(
        Transform playerRoot,
        Transform directionSource,
        float forwardDistance,
        float colliderHalfHeight,
        out Vector3 position,
        out Quaternion rotation)
    {
        if (playerRoot == null && directionSource == null)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            return;
        }

        if (playerRoot == null) playerRoot = directionSource;
        if (directionSource == null) directionSource = playerRoot;

        Vector3 flatForward = Vector3.ProjectOnPlane(directionSource.forward, Vector3.up);
        if (flatForward.sqrMagnitude <= 0.0001f)
        {
            flatForward = Vector3.ProjectOnPlane(playerRoot.forward, Vector3.up);
        }
        if (flatForward.sqrMagnitude <= 0.0001f)
        {
            flatForward = Vector3.forward;
        }
        flatForward.Normalize();

        int layerMask = ResolveDropLayerMask();
        float safeDistance = ResolveSafeForwardDistance(
            playerRoot.position,
            flatForward,
            Mathf.Max(0f, forwardDistance),
            playerRoot,
            layerMask);

        if (!TryResolveFloorWithBackoff(
                playerRoot.position,
                flatForward,
                safeDistance,
                Mathf.Max(0f, colliderHalfHeight),
                playerRoot,
                layerMask,
                out position))
        {
            position = playerRoot.position + Vector3.up * Mathf.Max(0.05f, colliderHalfHeight);
        }

        rotation = Quaternion.Euler(0f, directionSource.eulerAngles.y + 180f, 0f);
    }

    public static void GetFloorSnappedPose(
        Transform playerTransform,
        float forwardDistance,
        float colliderHalfHeight,
        out Vector3 position,
        out Quaternion rotation)
    {
        GetFloorSnappedPose(playerTransform, playerTransform, forwardDistance, colliderHalfHeight, out position, out rotation);
    }

    public static void GetFloorSnappedPose(
        Transform playerTransform,
        float forwardDistance,
        out Vector3 position,
        out Quaternion rotation)
    {
        GetFloorSnappedPose(playerTransform, playerTransform, forwardDistance, 0.05f, out position, out rotation);
    }

    private static float ResolveSafeForwardDistance(
        Vector3 playerPosition,
        Vector3 flatForward,
        float requestedDistance,
        Transform ignoreRoot,
        int layerMask)
    {
        if (requestedDistance <= 0f)
        {
            return 0f;
        }

        RaycastHit[] hits = Physics.SphereCastAll(
            playerPosition + Vector3.up * ObstacleProbeHeight,
            ObstacleProbeRadius,
            flatForward,
            requestedDistance,
            layerMask,
            QueryTriggerInteraction.Ignore);

        System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            Collider collider = hits[i].collider;
            if (collider == null || IsIgnoredCollider(collider, ignoreRoot))
            {
                continue;
            }

            return Mathf.Clamp(hits[i].distance - ObstaclePadding, 0f, requestedDistance);
        }

        return requestedDistance;
    }

    private static bool TryResolveFloorWithBackoff(
        Vector3 playerPosition,
        Vector3 flatForward,
        float safeDistance,
        float colliderHalfHeight,
        Transform ignoreRoot,
        int layerMask,
        out Vector3 position)
    {
        for (int step = 0; step <= FloorBackoffSteps; step++)
        {
            float distance = safeDistance * Mathf.Clamp01(1f - step / (float)FloorBackoffSteps);
            Vector3 candidate = playerPosition + flatForward * distance;
            if (TryFindFloor(candidate, colliderHalfHeight, ignoreRoot, layerMask, out position))
            {
                return true;
            }
        }

        position = default;
        return false;
    }

    private static bool TryFindFloor(
        Vector3 candidate,
        float colliderHalfHeight,
        Transform ignoreRoot,
        int layerMask,
        out Vector3 position)
    {
        RaycastHit[] hits = Physics.RaycastAll(
            candidate + Vector3.up * FloorProbeHeight,
            Vector3.down,
            MaxFloorProbeDistance,
            layerMask,
            QueryTriggerInteraction.Ignore);

        System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            if (hit.collider == null || IsIgnoredCollider(hit.collider, ignoreRoot))
            {
                continue;
            }

            if (Vector3.Dot(hit.normal, Vector3.up) < MinimumFloorNormalY)
            {
                continue;
            }

            position = hit.point + Vector3.up * colliderHalfHeight;
            return true;
        }

        position = default;
        return false;
    }

    private static bool IsIgnoredCollider(Collider collider, Transform ignoreRoot)
    {
        if (collider == null || ignoreRoot == null)
        {
            return false;
        }

        Transform hitTransform = collider.transform;
        return hitTransform == ignoreRoot || hitTransform.IsChildOf(ignoreRoot);
    }

    private static int ResolveDropLayerMask()
    {
        int ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");
        return ignoreRaycastLayer >= 0
            ? ~(1 << ignoreRaycastLayer)
            : Physics.DefaultRaycastLayers;
    }
}
