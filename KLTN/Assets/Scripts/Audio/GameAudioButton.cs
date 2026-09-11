using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace EchoProtocol.Audio
{
    public sealed class GameAudioButton : MonoBehaviour, IPointerEnterHandler
    {
        private Button _button;
        private void Awake() => _button = GetComponent<Button>();
        private void OnEnable() => _button.onClick.AddListener(Click);
        private void OnDisable() => _button.onClick.RemoveListener(Click);
        private void Click() => GameAudioRuntime.UI("ui/click");
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_button.IsInteractable()) GameAudioRuntime.UI("ui/hover");
        }
    }
}
