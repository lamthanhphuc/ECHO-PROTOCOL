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
        private Vector2Int _lastScreenSize;
        private Rect _lastSafeArea;
        private Button _profileButton;
        private Button _settingsButton;


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
            if (shopButton != null) shopButton.gameObject.SetActive(false);
            UtilityBarStyle.Lobby(transform as RectTransform);
            _profileButton = transform.Find("ProfileButton")?.GetComponent<Button>();
            _settingsButton = transform.Find("SettingsButton")?.GetComponent<Button>();
            var canvas = GetComponentInParent<Canvas>();
            MainMenuLeaderboardController.Install(canvas,
                transform.Find("LeaderboardButton")?.GetComponent<Button>());
            if (creditsButton != null)
                creditsButton.onClick.AddListener(OpenTopUp);
            if (_profileButton != null)
                _profileButton.onClick.AddListener(OpenProfile);
            if (_settingsButton != null)
                _settingsButton.onClick.AddListener(OpenSettings);

            if (webButton != null)
                webButton.onClick.AddListener(OpenWebsite);

            RefreshCredits();
        }


        private void OnDisable()
        {
            if (creditsButton != null)
                creditsButton.onClick.RemoveListener(OpenTopUp);
            if (_profileButton != null)
                _profileButton.onClick.RemoveListener(OpenProfile);
            if (_settingsButton != null)
                _settingsButton.onClick.RemoveListener(OpenSettings);

            if (webButton != null)
                webButton.onClick.RemoveListener(OpenWebsite);
        }


        private void Update()
        {
            if (_lastScreenSize.x != Screen.width || _lastScreenSize.y != Screen.height
                || !_lastSafeArea.Equals(Screen.safeArea))
            {
                UtilityBarStyle.PositionAtSafeTopRight(transform as RectTransform,
                    UtilityBarStyle.ToolbarWidth);
                _lastScreenSize = new Vector2Int(Screen.width, Screen.height);
                _lastSafeArea = Screen.safeArea;
            }
            if (Time.unscaledTime < _nextRefreshAt)
                return;

            _nextRefreshAt =
                Time.unscaledTime + 1f;

            RefreshCredits();
        }


        public void RefreshCredits()
        {
            UtilityBarStyle.RefreshCreditsLabel(creditsButton != null
                ? creditsButton.transform as RectTransform : null);
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


        public void OpenWebsite()
        {
            if (!string.IsNullOrWhiteSpace(
                    webUrl))
            {
                Application.OpenURL(
                    webUrl);
            }
        }

        private void OpenProfile()
        {
            UtilityProfileOverlay.Open(GetComponentInParent<Canvas>());
        }

        private void OpenSettings()
        {
            EchoProtocol.Voice.VoiceManager.EnsureExists();
            EchoProtocol.Voice.VoiceManager.Instance
                .GetComponent<EchoProtocol.Voice.VoiceSettingsPanel>()?.OpenFromMainMenu();
        }


    }
}
