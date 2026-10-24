using UnityEngine;
using Fusion;

namespace EchoProtocol.Networking
{
    /// <summary>Stops characters entering open water while allowing solid crossings above it.</summary>
    public sealed class FloodedWaterBoundary : MonoBehaviour
    {
        [SerializeField] private Renderer waterSurface;
        [SerializeField] private Terrain terrain;
        [SerializeField, Range(0f, 1f)] private float maximumWadingDepth = 0.45f;
        private static FloodedWaterBoundary _active;
        private readonly RaycastHit[] _hits = new RaycastHit[64];

        private void OnEnable() => _active = this;
        private void OnDisable()
        {
            if (_active == this) _active = null;
        }

        public static bool TryGetSwimSurface(Vector3 point, out float surfaceY, float minDepth = 1.4f)
        {
            surfaceY = 0f;
            if (!_active || !_active.waterSurface || !_active.terrain || !_active.terrain.terrainData) return false;
            var water = _active.waterSurface.bounds;
            if (point.x < water.min.x || point.x > water.max.x
                || point.z < water.min.z || point.z > water.max.z) return false;
            var local = point - _active.terrain.transform.position;
            var terrainSize = _active.terrain.terrainData.size;
            if (local.x < 0f || local.z < 0f || local.x > terrainSize.x || local.z > terrainSize.z) return false;
            surfaceY = water.center.y;
            return surfaceY - (_active.terrain.SampleHeight(point) + _active.terrain.transform.position.y) >= minDepth;
        }

        public static bool TryGetWaterBounds(out Bounds bounds)
        {
            bounds = default;
            if (!_active || !_active.waterSurface || !_active.terrain || !_active.terrain.terrainData) return false;
            bounds = _active.waterSurface.bounds;
            return true;
        }

        public static Vector3 ConstrainMotion(CharacterController character, Vector3 motion)
        {
            if (!_active || !_active.waterSurface || !character
                || motion.x * motion.x + motion.z * motion.z < 0.0000001f)
                return motion;
            return _active.Constrain(character, motion);
        }

        public static bool TryMove(NetworkCharacterController network, CharacterController character, Vector3 direction)
        {
            if (!_active || !_active.waterSurface || !character) return false;
            float delta = network.Runner.DeltaTime;
            if (delta <= 0f) return false;
            var velocity = network.Velocity;
            var horizontal = new Vector3(velocity.x, 0f, velocity.z);
            direction.Normalize();
            horizontal = direction == Vector3.zero
                ? Vector3.Lerp(horizontal, Vector3.zero, network.braking * delta)
                : Vector3.ClampMagnitude(horizontal + direction * network.acceleration * delta, network.maxSpeed);
            var motion = horizontal * delta;
            var allowed = _active.Constrain(character, motion);
            if ((allowed - motion).sqrMagnitude < 0.000000001f) return false;

            // Move() applies braking even with zero input. Compensate so it moves exactly
            // along the safe axes, including momentum from the previous tick.
            float remaining = 1f - Mathf.Clamp01(network.braking * delta);
            var safeVelocity = remaining > 0.0001f ? allowed / (delta * remaining) : Vector3.zero;
            network.Velocity = new Vector3(safeVelocity.x, velocity.y, safeVelocity.z);
            network.Move(Vector3.zero);
            return true;
        }

        private Vector3 Constrain(CharacterController character, Vector3 motion)
        {
            var position = character.bounds.center;
            float feet = character.bounds.min.y;
            float radius = character.radius * Mathf.Max(
                Mathf.Abs(character.transform.lossyScale.x), Mathf.Abs(character.transform.lossyScale.z));
            var horizontal = new Vector3(motion.x, 0f, motion.z);
            if (CanTraverse(position, horizontal, feet, radius, character.stepOffset)) return motion;

            // Keep motion along the bank instead of stopping both axes at a diagonal edge.
            var first = Mathf.Abs(horizontal.x) >= Mathf.Abs(horizontal.z)
                ? new Vector3(horizontal.x, 0f, 0f) : new Vector3(0f, 0f, horizontal.z);
            var second = horizontal - first;
            var allowed = Vector3.zero;
            if (CanTraverse(position, first, feet, radius, character.stepOffset)) allowed = first;
            if (CanTraverse(position + allowed, second, feet, radius, character.stepOffset)) allowed += second;
            return new Vector3(allowed.x, motion.y, allowed.z);
        }

        private bool CanTraverse(Vector3 start, Vector3 motion, float feet, float radius, float step)
        {
            if (motion.sqrMagnitude < 0.0000001f) return true;
            // Sweep samples also protect fast movement and unusually long simulation frames.
            int samples = Mathf.Max(1, Mathf.CeilToInt(motion.magnitude / Mathf.Max(0.1f, radius * 0.5f)));
            var forward = motion.normalized * radius * 0.35f;
            for (int i = 1; i <= samples; i++)
            {
                var point = start + motion * (i / (float)samples);
                // A narrow bridge supports the capsule at its center; its sides can overhang water.
                if (!HasDrySupport(point, feet, step)
                    || !HasDrySupport(point + forward, feet, step)) return false;
            }
            return true;
        }

        private bool HasDrySupport(Vector3 point, float feet, float step)
        {
            var bounds = waterSurface.bounds;
            if (point.x < bounds.min.x || point.x > bounds.max.x
                || point.z < bounds.min.z || point.z > bounds.max.z) return true;
            float minimumFloor = bounds.center.y - maximumWadingDepth;
            float top = feet + Mathf.Max(step, 0.2f) + 0.1f;
            if (terrain && terrain.terrainData)
            {
                var local = point - terrain.transform.position;
                var size = terrain.terrainData.size;
                if (local.x >= 0f && local.z >= 0f && local.x <= size.x && local.z <= size.z
                    && terrain.SampleHeight(point) + terrain.transform.position.y >= minimumFloor
                    && terrain.SampleHeight(point) + terrain.transform.position.y <= top)
                    return true;
            }

            // Search only below reachable foot height: roofs above the player are not support.
            if (top < minimumFloor) return false;
            int count = Physics.RaycastNonAlloc(new Vector3(point.x, top, point.z), Vector3.down,
                _hits, top - minimumFloor, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var hit = _hits[i];
                if (hit.collider.gameObject.scene != gameObject.scene || hit.normal.y < 0.5f
                    || !GameplayEnvironmentPhysics.IsEnvironment(hit.collider)
                    || hit.collider.GetComponentInParent<AlienHuntActor>() != null
                    || hit.collider.GetComponentInParent<EchoProtocol.AI.Minions.CreepMinionRuntime>() != null) continue;
                return true;
            }
            return false;
        }
    }
}
