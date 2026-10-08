using EchoProtocol.Auth;
using EchoProtocol.Profile;
using EchoProtocol.UI.MainMenu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EchoProtocol.UI
{
    [DisallowMultipleComponent]
    public sealed class LobbyUtilityBarController : MonoBehaviour
    {
        [SerializeField] private TMP_Text creditsText;
        [SerializeField] private Button creditsButton;
        [SerializeField] private Button shopButton;
        [SerializeField] private Button webButton;

        [SerializeField]
        private string topUpWebUrl =
            "https://web.3.1.238.6.sslip.io/wallet";

        [SerializeField]
        private string webUrl =
            "https://web.3.1.238.6.sslip.io/";

        private Canvas _canvas;
        private float _nextRefreshAt;

        private void Awake()
        {
            _canvas =
                GetComponentInParent<Canvas>();
        }

        private void OnEnable()
        {
            if (creditsButton != null)
                creditsButton.onClick.AddListener(OpenTopUp);

            if (shopButton != null)
                shopButton.onClick.AddListener(OpenShop);

            if (webButton != null)
                webButton.onClick.AddListener(OpenWebsite);

            RefreshCredits();
        }

        private void OnDisable()
        {
            if (creditsButton != null)
                creditsButton.onClick.RemoveListener(OpenTopUp);

            if (shopButton != null)
                shopButton.onClick.RemoveListener(OpenShop);

            if (webButton != null)
                webButton.onClick.RemoveListener(OpenWebsite);
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextRefreshAt)
                return;

            _nextRefreshAt =
                Time.unscaledTime + 1f;

            RefreshCredits();
        }

        public void RefreshCredits()
        {
            if (creditsText == null)
                return;

            int balance =
                PlayerProfileSession.HasProfile
                    ? PlayerProfileSession.WalletBalance
                    : AuthSession.WalletBalance;

            creditsText.text =
                $"{balance:N0}";
        }

        private void OpenTopUp()
        {
            if (!string.IsNullOrWhiteSpace(topUpWebUrl))
                Application.OpenURL(topUpWebUrl);
        }

        private void OpenWebsite()
        {
            if (!string.IsNullOrWhiteSpace(webUrl))
                Application.OpenURL(webUrl);
        }

        private void OpenShop()
        {
            if (_canvas == null)
                _canvas = GetComponentInParent<Canvas>();

            if (_canvas == null)
            {
                Debug.LogWarning(
                    "[Lobby] Lobby Canvas is missing.");

                return;
            }

            TeamToolShopPanel store = null;

            foreach (var root
                     in gameObject.scene.GetRootGameObjects())
            {
                var panels =
                    root.GetComponentsInChildren<
                        TeamToolShopPanel>(true);

                if (panels.Length == 0)
                    continue;

                store = panels[0];
                break;
            }

            if (store == null)
            {
                Debug.LogWarning(
                    "[Lobby] StoreV2Popup is missing.");

                return;
            }

            var popup = store.transform;

            if (popup.parent != _canvas.transform)
            {
                popup.SetParent(
                    _canvas.transform,
                    false);

                if (popup is RectTransform rect)
                {
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = Vector2.one;
                    rect.offsetMin = Vector2.zero;
                    rect.offsetMax = Vector2.zero;
                }
            }

            popup.SetAsLastSibling();
            store.Open();
        }
    }
}
