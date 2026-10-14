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

        [SerializeField] private TeamToolShopPanel storePanel;

        [SerializeField]
        private string topUpWebUrl =
            "https://web.3.1.238.6.sslip.io/wallet";

        [SerializeField]
        private string webUrl =
            "https://web.3.1.238.6.sslip.io/";

        private float _nextRefreshAt;


        public void Configure(
            TMP_Text amountText,
            Button credits,
            Button shop,
            Button web,
            TeamToolShopPanel store)
        {
            creditsText = amountText;

            creditsButton = credits;
            shopButton = shop;
            webButton = web;

            storePanel = store;
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
            if (!string.IsNullOrWhiteSpace(
                    topUpWebUrl))
            {
                Application.OpenURL(
                    topUpWebUrl);
            }
        }


        private void OpenWebsite()
        {
            if (!string.IsNullOrWhiteSpace(
                    webUrl))
            {
                Application.OpenURL(
                    webUrl);
            }
        }


        private void OpenShop()
        {
            if (storePanel == null)
            {
                Debug.LogWarning(
                    "[Lobby] StoreV2Popup reference is missing.");

                return;
            }

            storePanel.Open();
        }
    }
}
