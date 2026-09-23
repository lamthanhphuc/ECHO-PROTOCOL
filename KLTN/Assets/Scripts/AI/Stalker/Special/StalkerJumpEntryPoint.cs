using UnityEngine;

namespace EchoProtocol.AI.Stalker.Special
{
    public sealed class StalkerJumpEntryPoint : MonoBehaviour
    {
        [SerializeField] private string stableId;
        [SerializeField] private bool jumpInAllowed = true;
        [SerializeField] private float minPlayerDistance = 7f;
        [SerializeField] private float maxPlayerDistance = 10f;
        [SerializeField] private float frontFacingDotThreshold = 0.5f;
        [SerializeField] private float reuseCooldownSeconds = 120f;
        [SerializeField] private Transform facingOverride;

        public string StableId => string.IsNullOrWhiteSpace(stableId) ? BuildHierarchyId(transform) : stableId;
        public bool JumpInAllowed => jumpInAllowed;
        public float MinPlayerDistance => Mathf.Max(0f, minPlayerDistance);
        public float MaxPlayerDistance => Mathf.Max(MinPlayerDistance, maxPlayerDistance);
        public float FrontFacingDotThreshold => Mathf.Clamp(frontFacingDotThreshold, -1f, 1f);
        public float ReuseCooldownSeconds => Mathf.Max(0f, reuseCooldownSeconds);
        public Vector3 EntryPosition => transform.position;
        public Vector3 Forward => facingOverride != null ? facingOverride.forward : transform.forward;

        private static string BuildHierarchyId(Transform node)
        {
            var path = node != null ? node.name : "missing";
            while (node != null && node.parent != null)
            {
                node = node.parent;
                path = node.name + "/" + path;
            }

            return path;
        }
    }
}
