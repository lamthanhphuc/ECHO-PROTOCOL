using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace EchoProtocol.UI.MainMenu
{
    public sealed class UIHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField, Range(1f, 1.05f)] private float hoverScale = 1.02f;
        [SerializeField, Range(0.05f, 0.2f)] private float duration = 0.1f;

        private Vector3 _normalScale;
        private Coroutine _transition;

        private void Awake() => _normalScale = transform.localScale;

        public void OnPointerEnter(PointerEventData eventData) => Animate(_normalScale * hoverScale);
        public void OnPointerExit(PointerEventData eventData) => Animate(_normalScale);

        private void Animate(Vector3 target)
        {
            if (_transition != null) StopCoroutine(_transition);
            _transition = StartCoroutine(ScaleTo(target));
        }

        private IEnumerator ScaleTo(Vector3 target)
        {
            var start = transform.localScale;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                transform.localScale = Vector3.Lerp(start, target, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            transform.localScale = target;
        }
    }
}
