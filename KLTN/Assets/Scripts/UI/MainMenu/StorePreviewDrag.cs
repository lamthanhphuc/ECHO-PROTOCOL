using UnityEngine;
using UnityEngine.EventSystems;

namespace EchoProtocol.UI.MainMenu
{
    public sealed class StorePreviewDrag : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public StoreLivePreviewRenderer Renderer { get; set; }
        public void OnPointerDown(PointerEventData eventData) => OnBeginDrag(eventData);
        public void OnPointerUp(PointerEventData eventData) => Renderer?.EndPreviewDrag();
        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
                Renderer?.DragPreview(Vector2.zero);
        }
        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
                Renderer?.DragPreview(eventData.delta);
        }
        public void OnEndDrag(PointerEventData eventData) => Renderer?.EndPreviewDrag();
        private void OnDisable() => Renderer?.EndPreviewDrag();
    }
}
