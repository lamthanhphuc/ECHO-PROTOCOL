using EchoProtocol.AI.Common.Spatial;
using UnityEngine;

namespace EchoProtocol.AI.Stalker.Spatial
{
    public sealed class RegionDefinition : MonoBehaviour
    {
        [SerializeField] private int regionId;
        [SerializeField] private Bounds localBounds = new Bounds(Vector3.zero, Vector3.one);

        public RegionId RegionId => regionId > 0 ? new RegionId(regionId) : RegionId.Invalid;

        public RegionDefinitionBakeData ToBakeData()
        {
            var worldCenter = transform.TransformPoint(localBounds.center);
            var scale = transform.lossyScale;
            var worldSize = Vector3.Scale(
                localBounds.size,
                new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            return new RegionDefinitionBakeData(RegionId, new Bounds(worldCenter, worldSize));
        }

        public bool ContainsWorldPoint(Vector3 worldPoint)
        {
            var localPoint = transform.InverseTransformPoint(worldPoint);
            return localBounds.Contains(localPoint);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(localBounds.center, localBounds.size);
        }
    }
}
