using UnityEngine;
using UnityEngine.UI;

namespace EchoProtocol.UI.Credits
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CreditsEdgeFade : MaskableGraphic
    {
        [SerializeField, Min(1f)] private float _fadeHeight = 110f;

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var rect = rectTransform.rect;
            float height = Mathf.Min(_fadeHeight, rect.height * 0.3f);
            Strip(mesh, rect.xMin, rect.xMax, rect.yMin, rect.yMin + height, 1, 0);
            Strip(mesh, rect.xMin, rect.xMax, rect.yMax - height, rect.yMax, 0, 1);
        }

        private static void Strip(VertexHelper mesh, float left, float right, float bottom, float top, float bottomAlpha, float topAlpha)
        {
            int index = mesh.currentVertCount;
            mesh.AddVert(new Vector3(left, bottom), new Color(0, 0, 0, bottomAlpha), Vector2.zero);
            mesh.AddVert(new Vector3(left, top), new Color(0, 0, 0, topAlpha), Vector2.zero);
            mesh.AddVert(new Vector3(right, top), new Color(0, 0, 0, topAlpha), Vector2.zero);
            mesh.AddVert(new Vector3(right, bottom), new Color(0, 0, 0, bottomAlpha), Vector2.zero);
            mesh.AddTriangle(index, index + 1, index + 2);
            mesh.AddTriangle(index + 2, index + 3, index);
        }
    }
}
