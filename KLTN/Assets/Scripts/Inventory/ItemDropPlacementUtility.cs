using UnityEngine;

/// <summary>
/// Utility dùng chung cho tất cả drop/throw: raycast tìm sàn, giữ rotation thẳng đứng.
/// </summary>
public static class ItemDropPlacementUtility
{
    private const float RaycastHeightAboveGround = 1.5f;
    private const float MaxRaycastDistance = 4f;

    /// <summary>
    /// Tính vị trí đặt vật phẩm xuống sàn phía trước player.
    /// </summary>
    /// <param name="playerTransform">Transform của player (hoặc camera).</param>
    /// <param name="forwardDistance">Khoảng cách phía trước (mét).</param>
    /// <param name="colliderHalfHeight">Một nửa chiều cao collider của vật phẩm – offset nhỏ để không xuyên sàn.</param>
    /// <param name="position">Output: vị trí sát sàn.</param>
    /// <param name="rotation">Output: rotation giữ trục Y của player, pitch=0, roll=0.</param>
    public static void GetFloorSnappedPose(
        Transform playerTransform,
        float forwardDistance,
        float colliderHalfHeight,
        out Vector3 position,
        out Quaternion rotation)
    {
        // Flat forward – bỏ thành phần Y để không bay lên/xuống dốc
        Vector3 flatForward = Vector3.ProjectOnPlane(playerTransform.forward, Vector3.up).normalized;
        if (flatForward == Vector3.zero) flatForward = playerTransform.forward;

        Vector3 candidate = playerTransform.position + flatForward * forwardDistance;
        Vector3 rayOrigin = candidate + Vector3.up * RaycastHeightAboveGround;

        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, MaxRaycastDistance,
                ~0, QueryTriggerInteraction.Ignore))
        {
            position = hit.point + Vector3.up * colliderHalfHeight;
        }
        else
        {
            // Fallback: giữ nguyên tọa độ Y player
            position = new Vector3(candidate.x, playerTransform.position.y, candidate.z);
        }

        // Chỉ giữ trục Y của player – không nghiêng theo camera
        rotation = Quaternion.Euler(0f, playerTransform.eulerAngles.y, 0f);
        // Chỉ giữ trục Y của player, xoay 180 độ để mặt trước vật phẩm hướng về phía player
        rotation = Quaternion.Euler(0f, playerTransform.eulerAngles.y + 180f, 0f);
    }

    /// <summary>
    /// Overload đơn giản với colliderHalfHeight mặc định = 0.05f (sát sàn).
    /// </summary>
    public static void GetFloorSnappedPose(
        Transform playerTransform,
        float forwardDistance,
        out Vector3 position,
        out Quaternion rotation)
    {
        GetFloorSnappedPose(playerTransform, forwardDistance, 0.05f, out position, out rotation);
    }
}
