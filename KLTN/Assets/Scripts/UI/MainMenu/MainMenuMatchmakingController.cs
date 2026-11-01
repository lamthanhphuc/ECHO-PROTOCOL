using EchoProtocol.Auth;

using EchoProtocol.Gameplay;

using EchoProtocol.Networking;

using EchoProtocol.Profile;

using EchoProtocol.Settings;

using UnityEngine;

using UnityEngine.UI;



namespace EchoProtocol.UI.MainMenu

{

    [DisallowMultipleComponent]

    public sealed class MainMenuMatchmakingController : MonoBehaviour

    {

        [Header("Entry")]

        [SerializeField] private Button hostButton;

        [SerializeField] private Button joinButton;



        [Header("Dialog")]

        [SerializeField] private GameObject dialog;

        [SerializeField] private Text titleText;

        [SerializeField] private Text statusText;

        [SerializeField] private InputField roomCodeInput;

        [SerializeField] private GameObject difficultyRow;

        [SerializeField] private Button difficultyButton;

        [SerializeField] private Text difficultyValueText;

        [SerializeField] private Button confirmButton;

        [SerializeField] private Button backButton;



        [Header("Networking")]

        [SerializeField] private NetworkBootstrap bootstrap;

        [SerializeField] private LobbyManager lobbyManager;

        [SerializeField, Range(2, 4)] private int maxPlayers = 4;



        private MatchDifficulty _difficulty =

            MatchDifficulty.Normal;



        private GameObject _gameModeRow;

        private Button _gameModeButton;

        private Text _gameModeValue;

        private int _gameMode;

        private bool _hostMode;

        private bool _busy;



        private void Awake()

        {

            ResolveServices();


            RefreshDifficultyText();



            if (dialog != null)

            {

                dialog.SetActive(false);

            }

        }



        private readonly System.Collections.Generic.List<CanvasGroup> _menuEntryGroups = new();
        private bool _dialogStyled;
        private Vector2 _dialogSize, _confirmPosition, _backPosition, _statusPosition;

        private void LateUpdate()
        {
            if (!_dialogStyled && dialog != null)
            {
                var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                foreach (var text in dialog.GetComponentsInChildren<Text>(true))
                {
                    text.font = font;
                    text.fontSize = text == titleText ? 26 : 15;
                    text.color = new Color32(229, 227, 218, 255);
                    text.resizeTextForBestFit = false;
                }
                if (titleText != null) titleText.fontStyle = FontStyle.Bold;
                foreach (var button in dialog.GetComponentsInChildren<Button>(true))
                {
                    var label = button.GetComponentInChildren<Text>(true);
                    if (label != null) { label.fontSize = 17; label.fontStyle = FontStyle.Bold; }
                }
                if (roomCodeInput != null)
                {
                    roomCodeInput.textComponent.fontSize = 18;
                    if (roomCodeInput.placeholder is Text placeholder)
                    { placeholder.fontSize = 17; placeholder.fontStyle = FontStyle.Normal; }
                }
                var window = dialog.GetComponent<RectTransform>();
                if (window != null) window.sizeDelta += new Vector2(64f, 0f);
                if (roomCodeInput != null) roomCodeInput.GetComponent<RectTransform>().sizeDelta += new Vector2(64f, 4f);
                if (confirmButton != null) confirmButton.GetComponent<RectTransform>().sizeDelta += new Vector2(40f, 0f);
                if (backButton != null) backButton.GetComponent<RectTransform>().anchoredPosition += new Vector2(40f, 0f);
                if (window != null) _dialogSize = window.sizeDelta;
                if (confirmButton != null) _confirmPosition = confirmButton.GetComponent<RectTransform>().anchoredPosition;
                if (backButton != null) _backPosition = backButton.GetComponent<RectTransform>().anchoredPosition;
                if (statusText != null) _statusPosition = statusText.rectTransform.anchoredPosition;
                ApplyDialogFrame(titleText.transform.parent.gameObject, new Color(.008f,.014f,.018f,.68f), false);
                if (roomCodeInput != null) ApplyDialogFrame(roomCodeInput.gameObject, new Color(.025f,.035f,.036f,.72f), false);
                if (confirmButton != null)
                {
                    var graphic = ApplyDialogFrame(confirmButton.gameObject, Color.white, true);
                    confirmButton.targetGraphic = graphic;
                    var colors = confirmButton.colors;
                    colors.normalColor = new Color(.035f,.045f,.05f,.65f);
                    colors.highlightedColor = colors.selectedColor = new Color(.13f,.17f,.18f,.82f);
                    colors.pressedColor = new Color(.08f,.10f,.11f,.9f);
                    confirmButton.colors = colors;
                }
                if (backButton != null)
                {
                    backButton.targetGraphic = ApplyDialogFrame(backButton.gameObject, Color.white, false);
                    var colors = backButton.colors;
                    colors.normalColor = new Color(.035f,.045f,.05f,.65f);
                    colors.highlightedColor = colors.selectedColor = new Color(.13f,.17f,.18f,.82f);
                    colors.pressedColor = new Color(.025f,.03f,.03f);
                    backButton.colors = colors;
                }
                _dialogStyled = true;
            }
            if (dialog != null)
            {
                var overlay = dialog.GetComponent<RectTransform>();
                overlay.anchorMin = Vector2.zero;
                overlay.anchorMax = Vector2.one;
                overlay.offsetMin = overlay.offsetMax = Vector2.zero;
                overlay.localScale = Vector3.one;
                var window = (RectTransform)titleText.transform.parent;
                window.anchorMin = window.anchorMax = window.pivot = new Vector2(.5f,.5f);
                window.sizeDelta = new Vector2(720, 330);
                Place(titleText != null ? titleText.rectTransform : null, 0, 28, 636, 48);
                if (titleText != null)
                {
                    titleText.fontSize = 30;
                    titleText.alignment = TextAnchor.MiddleCenter;
                }
                if (roomCodeInput != null)
                {
                    Place((RectTransform)roomCodeInput.transform, 0, 120, 636, 56);
                    roomCodeInput.textComponent.gameObject.SetActive(true);
                    roomCodeInput.textComponent.alignment = TextAnchor.MiddleCenter;
                    ClearRoomCodePlaceholder(roomCodeInput);
                }
                foreach (var label in dialog.GetComponentsInChildren<Text>(true))
                    if (label.text == "MÃ PHÒNG" || label.text == "ROOM CODE")
                    {
                        Place(label.rectTransform, 0, 88, 600, 26);
                        label.alignment = TextAnchor.MiddleCenter;
                    }
                if (difficultyRow != null) Place((RectTransform)difficultyRow.transform,0,218,636,44);
                if (_gameModeRow != null) Place((RectTransform)_gameModeRow.transform,0,274,636,44);
                float buttonY = 240;
                if (confirmButton != null) Place((RectTransform)confirmButton.transform,-162,buttonY,306,54);
                if (backButton != null) Place((RectTransform)backButton.transform,162,buttonY,306,54);
                if (statusText != null)
                {
                    Place(statusText.rectTransform,0,184,636,28);
                    statusText.alignment = TextAnchor.MiddleCenter;
                }
                var canvas = dialog.GetComponentInParent<Canvas>();
                var bounds = ((RectTransform)canvas.transform).rect;
                float scale = Mathf.Min(1f,(bounds.width-48)/720,(bounds.height*.60f-32)/window.sizeDelta.y);
                window.localScale = Vector3.one * Mathf.Max(.1f,scale);
                window.anchoredPosition = new Vector2(0,-bounds.height*.12f);
            }

            if (_menuEntryGroups.Count == 0 && hostButton != null)
            {
                var canvas = hostButton.GetComponentInParent<Canvas>();
                if (canvas != null)
                    foreach (var button in canvas.GetComponentsInChildren<Button>(true))
                        if (button == hostButton || button == joinButton || button.name == "OptionsButton" || button.name == "LogoutButton")
                        {
                            var group = button.GetComponent<CanvasGroup>();
                            if (group == null) group = button.gameObject.AddComponent<CanvasGroup>();
                            _menuEntryGroups.Add(group);
                        }
            }
            bool hidden = dialog != null && dialog.activeInHierarchy || EchoProtocol.Voice.VoiceSettingsPanel.IsOpen;
            foreach (var group in _menuEntryGroups)
                if (group != null) { group.alpha = hidden ? 0f : 1f; group.interactable = !hidden; group.blocksRaycasts = !hidden; }
        }

        private static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            if (rect == null) return;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f,1);
            rect.anchoredPosition = new Vector2(x,-y);
            rect.sizeDelta = new Vector2(width,height);
        }

        private static void ClearRoomCodePlaceholder(InputField input)
        {
            if (input.placeholder is not Text placeholder) return;
            placeholder.text = string.Empty;
            placeholder.alignment = TextAnchor.MiddleCenter;
        }

        private static Graphic ApplyDialogFrame(GameObject owner, Color fill, bool accent)
        {
            // Keep the authored Image: replacing a Selectable's Graphic breaks its canvas geometry.
            var image = owner.GetComponent<Image>() ?? owner.AddComponent<Image>();
            image.enabled = true;
            image.sprite = null;
            image.type = Image.Type.Simple;
            image.material = null;
            image.color = fill;
            var outline = owner.GetComponent<Outline>();
            if (outline != null) outline.enabled = false;
            if (owner.transform.Find("FrameTop") == null)
            {
                for (int i = 0; i < 4; i++)
                {
                    var edge = new GameObject(i == 0 ? "FrameTop" : "FrameEdge" + i, typeof(RectTransform), typeof(Image));
                    edge.transform.SetParent(owner.transform, false);
                    var rect = (RectTransform)edge.transform;
                    bool horizontal = i < 2;
                    rect.anchorMin = horizontal ? new Vector2(0,i == 0 ? 1 : 0) : new Vector2(i == 2 ? 0 : 1,0);
                    rect.anchorMax = horizontal ? new Vector2(1,i == 0 ? 1 : 0) : new Vector2(i == 2 ? 0 : 1,1);
                    rect.sizeDelta = horizontal ? new Vector2(0,1) : new Vector2(1,0);
                    rect.anchoredPosition = Vector2.zero;
                    edge.GetComponent<Image>().color = new Color(.42f,.44f,.42f,.45f);
                    edge.GetComponent<Image>().raycastTarget = false;
                }
            }
            if (owner.GetComponent<Button>() != null && owner.transform.Find("MenuAccent") == null)
            {
                var stripe = new GameObject("MenuAccent", typeof(RectTransform), typeof(Image));
                stripe.transform.SetParent(owner.transform, false);
                var rect = (RectTransform)stripe.transform;
                rect.anchorMin = new Vector2(0,0);
                rect.anchorMax = new Vector2(0,1);
                rect.pivot = new Vector2(0,.5f);
                rect.offsetMin = new Vector2(0,9);
                rect.offsetMax = new Vector2(3,-9);
                stripe.GetComponent<Image>().color = new Color(.72f,.025f,.05f);
                stripe.GetComponent<Image>().raycastTarget = false;
            }
            return image;
        }

        private void BuildGameModePicker()

        {

            if (!difficultyRow || _gameModeRow) return;

            _gameModeRow = Instantiate(difficultyRow,difficultyRow.transform.parent,false);

            _gameModeRow.name = "GameModeRow";

            var row = (RectTransform)_gameModeRow.transform;

            row.anchoredPosition = ((RectTransform)difficultyRow.transform).anchoredPosition + Vector2.down*54;

            var label = row.Find("Label")?.GetComponent<Text>();

            if (label) label.text = GameLanguage.Choose("CHẾ ĐỘ CHƠI", "GAME MODE");

            _gameModeButton = row.GetComponentInChildren<Button>(true);

            _gameModeButton.onClick = new Button.ButtonClickedEvent();

            _gameModeValue = _gameModeButton.GetComponentInChildren<Text>(true);

            _gameModeButton.onClick.AddListener(() => { if (!_busy && _hostMode) { _gameMode = 1-_gameMode; RefreshGameMode(); } });

            foreach (var component in new Component[] {confirmButton,backButton,statusText})

                if(component) ((RectTransform)component.transform).anchoredPosition += Vector2.down*54;

            var window = difficultyRow.transform.parent as RectTransform;

            if(window) window.sizeDelta += Vector2.up*54;

            RefreshGameMode();

        }

        private void RefreshGameMode()

        {

            if (_gameModeValue) _gameModeValue.text = _gameMode == 1

                ? GameLanguage.Choose("<  THANH TRỪNG  >", "<  PURGE  >")

                : GameLanguage.Choose("<  KHÁM PHÁ  >", "<  EXPEDITION  >");

        }



        private void OnEnable()

        {

            if (hostButton != null)

                hostButton.onClick.AddListener(OpenHost);



            if (joinButton != null)

                joinButton.onClick.AddListener(OpenJoin);



            if (difficultyButton != null)

                difficultyButton.onClick.AddListener(CycleDifficulty);



            if (confirmButton != null)

                confirmButton.onClick.AddListener(Submit);



            if (backButton != null)

                backButton.onClick.AddListener(CloseDialog);



            ResolveServices();



            if (bootstrap != null)

                bootstrap.SessionStateChanged += HandleSessionStateChanged;

        }



        private void OnDisable()

        {

            if (hostButton != null)

                hostButton.onClick.RemoveListener(OpenHost);



            if (joinButton != null)

                joinButton.onClick.RemoveListener(OpenJoin);



            if (difficultyButton != null)

                difficultyButton.onClick.RemoveListener(CycleDifficulty);



            if (confirmButton != null)

                confirmButton.onClick.RemoveListener(Submit);



            if (backButton != null)

                backButton.onClick.RemoveListener(CloseDialog);



            if (bootstrap != null)

                bootstrap.SessionStateChanged -= HandleSessionStateChanged;

        }



        private void ResolveServices()

        {

            if (bootstrap == null)

                bootstrap = NetworkBootstrap.Instance

                    ?? FindAnyObjectByType<NetworkBootstrap>();



            if (lobbyManager == null)

                lobbyManager =

                    FindAnyObjectByType<LobbyManager>();

        }



        private void OpenHost()

        {

            if (_busy)

                return;



            _hostMode = true;

            if (_gameModeRow) _gameModeRow.SetActive(false);

            RefreshGameMode();



            if (titleText != null)

                titleText.text = "HOST GAME";



            if (difficultyRow != null)

                difficultyRow.SetActive(false);



            SetConfirmLabel("CREATE ROOM");

            SetStatus(string.Empty);

            OpenDialog();

        }



        private void OpenJoin()

        {

            if (_busy)

                return;



            _hostMode = false;

            if (_gameModeRow) _gameModeRow.SetActive(false);



            if (titleText != null)

                titleText.text = "JOIN GAME";



            if (difficultyRow != null)

                difficultyRow.SetActive(false);



            SetConfirmLabel("JOIN ROOM");

            SetStatus(string.Empty);

            OpenDialog();

        }



        private void OpenDialog()

        {

            if (dialog != null)

                dialog.SetActive(true);



            if (roomCodeInput != null)

            {

                roomCodeInput.interactable = true;

                roomCodeInput.Select();

                roomCodeInput.ActivateInputField();

            }

        }



        private void CloseDialog()

        {

            if (_busy)

                return;



            if (dialog != null)

                dialog.SetActive(false);

        }



        private void CycleDifficulty()

        {

            if (_busy || !_hostMode)

                return;



            _difficulty = _difficulty switch

            {

                MatchDifficulty.Easy =>

                    MatchDifficulty.Normal,



                MatchDifficulty.Normal =>

                    MatchDifficulty.Hard,



                _ =>

                    MatchDifficulty.Easy

            };



            RefreshDifficultyText();

        }



        private void RefreshDifficultyText()

        {

            if (difficultyValueText != null)

            {

                difficultyValueText.text =

                    $"<  {_difficulty.ToString().ToUpperInvariant()}  >";

            }

        }



        private async void Submit()

        {

            if (_busy)

                return;



            ResolveServices();



            if (bootstrap == null || lobbyManager == null)

            {

                ReportConnectionError("NETWORK SERVICES UNAVAILABLE");

                return;

            }



            if (bootstrap.HasRunningRunner || bootstrap.IsBusy)

            {

                ReportConnectionError("NETWORK SESSION IS BUSY");

                return;

            }



            string roomCode =

                roomCodeInput != null

                    ? roomCodeInput.text.Trim()

                    : string.Empty;



            if (roomCode.Length == 0)

            {

                ReportConnectionError("ROOM CODE REQUIRED");

                return;

            }



            string operatorName =

                ResolveOperatorName();



            if (!lobbyManager.SetLocalOperatorName(operatorName))

            {

                ReportConnectionError("PLAYER NAME UNAVAILABLE");

                return;

            }



            _busy = true;

            RefreshInteractableState();



            SetStatus(

                _hostMode

                    ? "CREATING ROOM..."

                    : "JOINING ROOM...");



            bool success;

            string failureDetail = null;



            try

            {

                success =

                    _hostMode

                        ? await bootstrap.CreateRoomAsync(

                            roomCode,

                            maxPlayers,

                            MatchDifficulty.Normal, 0)

                        : await bootstrap.JoinRoomAsync(

                            roomCode);

            }

            catch (System.Exception exception)

            {

                Debug.LogException(exception);

                success = false;

                failureDetail = exception.Message;



                if (this != null)

                    SetStatus(NetworkLobbyUI.ConnectionFeedback(exception.Message));

            }



            if (this == null)

                return;



            _busy = false;

            RefreshInteractableState();



            if (!success)

            {

                ReportConnectionError(

                    !string.IsNullOrWhiteSpace(failureDetail) ? failureDetail : bootstrap == null || string.IsNullOrWhiteSpace(bootstrap.LastError)

                        ? "CONNECTION FAILED"

                        : bootstrap.LastError);



                return;

            }



            SetStatus("CONNECTED. ENTERING LOBBY...");

        }



        private static string ResolveOperatorName()

        {

            if (PlayerProfileSession.HasProfile

                && !string.IsNullOrWhiteSpace(

                    PlayerProfileSession.DisplayName))

            {

                return PlayerProfileSession.DisplayName.Trim();

            }



            if (!string.IsNullOrWhiteSpace(

                    AuthSession.DisplayName))

            {

                return AuthSession.DisplayName.Trim();

            }



            if (!string.IsNullOrWhiteSpace(

                    AuthSession.Username))

            {

                return AuthSession.Username.Trim();

            }



            return "Operator";

        }



        private void HandleSessionStateChanged(

            NetworkSessionState state,

            string message)

        {

            if (state == NetworkSessionState.Connecting)

            {

                SetStatus(

                    _hostMode

                        ? "CREATING ROOM..."

                        : "JOINING ROOM...");

            }

            else if (state == NetworkSessionState.Failed)

            {

                string feedback = NetworkLobbyUI.ConnectionFeedback(message);

                SetStatus(feedback.Split('\n')[0]);

            }

        }



        private void SetConfirmLabel(string value)

        {

            if (confirmButton == null)

                return;



            var label =

                confirmButton.GetComponentInChildren<Text>(

                    true);



            if (label != null)

                label.text = value;

        }



        private void SetStatus(string value)

        {

            if (statusText != null)

                statusText.text = GameLanguage.Translate(value ?? string.Empty);

        }



        private void ReportConnectionError(string detail)

        {

            string message = NetworkLobbyUI.ConnectionFeedback(detail);

            int split = message.IndexOf('\n');

            SetStatus(split >= 0 ? message.Substring(0, split) : message);

            GameUIFeedback.Instance.Confirm(split >= 0 ? message.Substring(0, split) : message,

                () => { string current = NetworkLobbyUI.ConnectionFeedback(detail); int line = current.IndexOf('\n'); return line >= 0 ? current.Substring(line + 1) : current; },

                Submit, GameLanguage.Choose("Thử lại", "Retry"));

        }



        private void RefreshInteractableState()

        {

            if (hostButton != null)

                hostButton.interactable = !_busy;



            if (joinButton != null)

                joinButton.interactable = !_busy;



            if (roomCodeInput != null)

                roomCodeInput.interactable = !_busy;



            if (difficultyButton != null)

                difficultyButton.interactable =

                    !_busy && _hostMode;



            if (_gameModeButton) _gameModeButton.interactable = !_busy && _hostMode;

            if (confirmButton != null)

                confirmButton.interactable = !_busy;



            if (backButton != null)

                backButton.interactable = !_busy;

        }

    }

}
