using UnityEngine;

namespace EchoProtocol.Cinematics
{
    /// <summary>Editable scene poses baked to the camera Animation Track by the Editor tool.</summary>
    public sealed class OpeningCutsceneShotAnchor : MonoBehaviour
    {
        public int shotIndex;
        public Transform source;
        public Transform end;

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.85f, 0.8f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawFrustum(Vector3.zero, 58, 3, 0.1f, 16f / 9f);
            Gizmos.matrix = Matrix4x4.identity;
            if (end != null) Gizmos.DrawLine(transform.position, end.position);
        }
    }
}
