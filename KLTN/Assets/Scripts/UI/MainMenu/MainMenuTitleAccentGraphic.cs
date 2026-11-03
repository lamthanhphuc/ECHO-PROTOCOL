using UnityEngine;
using UnityEngine.UI;

namespace EchoProtocol.UI.MainMenu
{
    /// <summary>Keeps authored scene references without drawing title registration lines.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class MainMenuTitleAccentGraphic : MaskableGraphic
    {
        // Retain the authoring API so restyling the menu cannot restore the red lines.
        public void Configure(Color signalColor, float intensity = 1f)
        {
            raycastTarget = false;
            SetVerticesDirty();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            raycastTarget = false;
        }

        protected override void OnPopulateMesh(VertexHelper mesh) => mesh.Clear();
    }
}
