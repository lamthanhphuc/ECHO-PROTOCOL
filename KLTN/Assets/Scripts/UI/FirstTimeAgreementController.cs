using System;
using System.Security.Cryptography;
using System.Text;
using EchoProtocol.Api;
using EchoProtocol.Auth;
using EchoProtocol.Core;
using EchoProtocol.Settings;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EchoProtocol.UI
{
    [Serializable] public sealed class PolicyDocument { public string id, version, titleVi, titleEn, bodyVi, bodyEn; }
    [Serializable] public sealed class PolicyBundle { public string bundleVersion, effectiveDate, operatorName, supportEmail; public PolicyDocument[] documents; }
    [Serializable] public sealed class PolicyStatus { public bool accepted; public string bundleVersion, contentSha256; }
    [Serializable] public sealed class PolicyStatusResponse { public bool success; public PolicyStatus data; }
    [Serializable] public sealed class PolicyDocumentsData { public PolicyBundle bundle; public string contentSha256; }
    [Serializable] public sealed class PolicyDocumentsResponse { public bool success; public PolicyDocumentsData data; }
    [Serializable] public sealed class PolicyAcceptance
    {
        public string bundleVersion, contentSha256, locale;
        public bool adultConfirmed, warningAcknowledged, termsAccepted, privacyNoticeAcknowledged, communityAccepted, transactionsAccepted;
    }

    // One persistent modal, with a public offline reader and an authenticated server gate.
    public sealed class FirstTimeAgreementController : MonoBehaviour
    {
        private static FirstTimeAgreementController _instance;
        private GameObject _overlay, _reader;
        private RectTransform _window;
        private Text _heading, _warning, _warningHeading, _agreementLabel, _status, _readText, _readTitle;
        private Text _acceptLabel, _declineLabel, _languageLabel, _readCloseLabel;
        private Button _accept;
        private Toggle _agreement;
        private Button[] _documents;
        private PolicyBundle _bundle;
        private string _hash, _owner, _destination, _acceptedOwner;
        private bool _required, _busy, _serverKnown, _failed;
        private int _generation, _reading = -1;
        private readonly PlayerInteractionControlLock _lock = new();
        private string L(string vi, string en) => GameLanguage.Choose(vi, en);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _instance = null;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= SceneLoaded;
            SceneManager.sceneLoaded += SceneLoaded;
            SceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }
        private static void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == GameConstants.SceneLogin) Ensure();
            if (scene.name != GameConstants.SceneMainMenu && scene.name != GameConstants.SceneLobby
                && !EchoProtocol.Networking.LobbyMapCatalog.IsGameplayScene(scene.name)) return;
            if (AuthSession.IsAuthenticated && (_instance == null || _instance._acceptedOwner != AuthSession.CurrentUserId))
                Require(null);
        }
        public static void Require(string destination)
        {
            var screen = Ensure();
            GameUIFeedback.Instance.EndStartupForAgreement();
            screen._required = true;
            screen._destination = destination;
            screen._owner = AuthSession.CurrentUserId;
            screen._agreement.isOn = false;
            screen._overlay.SetActive(true);
            screen._reader.SetActive(false);
            screen._lock.Acquire(screen.gameObject, allowWhileDowned: true);
            screen.Check();
        }
        public static void OpenPolicies()
        {
            var screen = Ensure();
            if (screen._required && screen._overlay.activeSelf) return;
            screen._required = false;
            screen._overlay.SetActive(true);
            screen._reader.SetActive(false);
            screen._lock.Acquire(screen.gameObject, allowWhileDowned: true);
            screen.Render();
        }
        public static void ClearSession()
        {
            if (_instance == null) return;
            _instance._generation++;
            _instance._acceptedOwner = null;
            _instance._required = false;
            _instance._busy = false;
            _instance.Close();
        }
        private static FirstTimeAgreementController Ensure()
        {
            if (_instance == null) new GameObject("Policies", typeof(RectTransform)).AddComponent<FirstTimeAgreementController>();
            return _instance;
        }
        private void Awake()
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
            var resource = Resources.Load<TextAsset>("Legal/policies");
            _bundle = JsonUtility.FromJson<PolicyBundle>(resource.text);
            using (var sha = SHA256.Create()) _hash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(resource.text.TrimStart('\uFEFF').Replace("\r\n", "\n")))).Replace("-", "").ToLowerInvariant();
            Build();
            _overlay.SetActive(false);
            GameLanguage.Changed += Render;
        }
        private void OnDestroy() { GameLanguage.Changed -= Render; _lock.Release(); if (_instance == this) _instance = null; }
        private void Update()
        {
            string scene = SceneManager.GetActiveScene().name;
            transform.Find("PoliciesButton").gameObject.SetActive(!_overlay.activeSelf &&
                (scene == GameConstants.SceneLogin || scene == GameConstants.SceneMainMenu));
            if (!_overlay.activeSelf) return;
            if (_required && (!AuthSession.IsAuthenticated || AuthSession.CurrentUserId != _owner)) { ClearSession(); return; }
            if (EventSystem.current == null)
                new GameObject("PolicyEventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule))
                    .GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            var bounds = ((RectTransform)transform).rect;
            _window.localScale = Vector3.one * Mathf.Max(.1f, Mathf.Min(1, (bounds.width - 32) / 1040, (bounds.height - 32) / 720));
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                _lock.ConsumeEscape();
                if (_reader.activeSelf) { _reader.SetActive(false); _reading = -1; }
                else if (!_required) Close();
            }
            _accept.interactable = _required && !_busy && (_failed || _serverKnown) && _agreement.isOn;
        }
        private void Check()
        {
            if (_busy) return;
            _busy = true; _failed = false; _serverKnown = false;
            int generation = ++_generation;
            Render();
            AuthRuntime.EnsureExists().Client.GetJson<PolicyStatusResponse>("/api/agreements/status", true, result =>
            {
                if (!Current(generation)) return;
                if (!result.IsSuccess || result.Data?.success != true || result.Data.data == null) { Failure(); return; }
                var state = result.Data.data;
                if (state.accepted) { _busy = false; Finish(); return; }
                if (state.bundleVersion == _bundle.bundleVersion && state.contentSha256 == _hash)
                { _busy = false; _serverKnown = true; Render(); return; }
                AuthRuntime.EnsureExists().Client.GetJson<PolicyDocumentsResponse>("/api/agreements/documents", false, documents =>
                {
                    if (!Current(generation)) return;
                    var data = documents.Data?.data;
                    if (!documents.IsSuccess || documents.Data?.success != true || data?.bundle?.documents == null
                        || data.bundle.documents.Length != 4 || string.IsNullOrEmpty(data.contentSha256)) { Failure(); return; }
                    _bundle = data.bundle; _hash = data.contentSha256;
                    _agreement.isOn = false;
                    _reader.SetActive(false); _reading = -1;
                    _busy = false; _serverKnown = true; Render();
                });
            });
        }
        private bool Current(int generation) => this != null && generation == _generation
            && AuthSession.CurrentUserId == _owner && _overlay.activeSelf && _required;
        private void Failure() { _busy = false; _failed = true; _serverKnown = false; Render(); }
        private void Accept()
        {
            if (_busy || !_accept.interactable) return;
            if (!_serverKnown) { Check(); return; }
            _busy = true; _failed = false; Render();
            int generation = ++_generation;
            AuthRuntime.EnsureExists().Client.PostJson<PolicyAcceptance, PolicyStatusResponse>("/api/agreements/accept",
                new PolicyAcceptance { bundleVersion = _bundle.bundleVersion, contentSha256 = _hash,
                    locale = GameLanguage.Current == GameLocale.Vietnamese ? "vi" : "en", adultConfirmed = true,
                    warningAcknowledged = true, termsAccepted = true, privacyNoticeAcknowledged = true,
                    communityAccepted = true, transactionsAccepted = true }, true, result =>
                {
                    if (!Current(generation)) return;
                    _busy = false;
                    if (result.IsSuccess && result.Data?.success == true && result.Data.data?.accepted == true) Finish();
                    else { _agreement.isOn = false; Failure(); }
                });
        }
        private void Finish()
        {
            _acceptedOwner = _owner;
            string destination = _destination;
            _required = false; Close();
            if (!string.IsNullOrEmpty(destination)) GameUIFeedback.LoadScene(destination);
        }
        private void Close() { _overlay.SetActive(false); _lock.Release(); _reading = -1; }
        private void Decline()
        {
            if (!_required) { Close(); return; }
            AuthRuntime.EnsureExists().AuthService.LogoutLocal();
            GameUIFeedback.LoadScene(GameConstants.SceneLogin);
        }
        private void Read(int index)
        {
            _reading = index;
            _reader.SetActive(true); _reader.transform.SetAsLastSibling();
            Render();
            Canvas.ForceUpdateCanvases();
            _reader.GetComponentInChildren<ScrollRect>().verticalNormalizedPosition = 1;
        }
        private void Render()
        {
            if (_heading == null) return;
            _heading.text = _required ? L("TRƯỚC KHI BẮT ĐẦU", "BEFORE YOU BEGIN") : L("THÔNG TIN & CHÍNH SÁCH", "INFORMATION & POLICIES");
            _warning.text = L("ECHO PROTOCOL · DÀNH CHO NGƯỜI ĐỦ 18 TUỔI\n\nTrò chơi có nội dung kinh dị, tình huống bất ngờ, âm thanh lớn, ánh sáng nhấp nháy và bạo lực giả tưởng. Hãy điều chỉnh âm lượng và dừng chơi nếu cảm thấy không thoải mái.\n\nVui lòng đọc các tài liệu bên dưới trước khi tiếp tục.",
                "ECHO PROTOCOL · ADULTS 18+\n\nContains horror themes, sudden scares, loud audio, flashing effects and fantasy violence. Adjust volume and stop playing if uncomfortable.\n\nPlease review the documents below before continuing.");
            _agreement.gameObject.SetActive(_required); _accept.gameObject.SetActive(_required);
            _warningHeading.text = L("CẢNH BÁO NỘI DUNG", "CONTENT WARNING");
            _agreementLabel.text = L("Tôi xác nhận đã đủ 18 tuổi, đã xem cảnh báo nội dung, đồng ý Điều khoản sử dụng, Quy tắc cộng đồng và Chính sách giao dịch, đồng thời xác nhận đã được cung cấp Chính sách quyền riêng tư.",
                "I confirm I am at least 18, acknowledge the content warning, accept the Terms, Community Rules and Transaction Policy, and acknowledge receipt of the Privacy Notice.");
            _acceptLabel.text = _failed ? L("TIẾP TỤC", "CONTINUE") : L("ĐỒNG Ý & TIẾP TỤC", "AGREE & CONTINUE");
            _declineLabel.text = _required ? L("ĐĂNG XUẤT", "SIGN OUT") : L("QUAY LẠI", "BACK");
            _languageLabel.text = "VI / EN";
            _readCloseLabel.text = L("QUAY LẠI", "BACK");

            _status.text = _busy ? L("Đang kiểm tra/lưu xác nhận...", "Checking/saving acceptance...")
                : _failed ? L("Không thể kết nối. Kiểm tra mạng rồi chọn Tiếp tục.", "Unable to connect. Check your connection and select Continue.")
                : L("Cập nhật: ", "Updated: ") + DisplayDate(_bundle.effectiveDate) + " · hoangdhse184661@fpt.edu.vn";
            _status.color = _failed ? new Color(.9f,.48f,.46f) : new Color(.72f,.74f,.73f);
            for (int i = 0; i < _documents.Length; i++)
                _documents[i].GetComponentInChildren<Text>().text = GameLanguage.Current == GameLocale.Vietnamese ? _bundle.documents[i].titleVi : _bundle.documents[i].titleEn;
            if (_reading >= 0)
            {
                var doc = _bundle.documents[_reading];
                _readTitle.text = (GameLanguage.Current == GameLocale.Vietnamese ? doc.titleVi : doc.titleEn);
                _readText.text = GameLanguage.Current == GameLocale.Vietnamese ? doc.bodyVi : doc.bodyEn;
            }
        }
        private static string DisplayDate(string date)
        {
            return DateTime.TryParseExact(date, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var parsed) ? parsed.ToString("dd/MM/yyyy") : "";
        }
        private void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 31000;
            var scaler = gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600,900); scaler.matchWidthOrHeight = .5f;
            gameObject.AddComponent<GraphicRaycaster>();
            _overlay = Rect("AgreementOverlay", transform).gameObject;
            Stretch((RectTransform)_overlay.transform);
            _overlay.AddComponent<Image>().color = new Color(.006f,.012f,.016f,.95f);
            _window = Rect("Window",_overlay.transform); _window.anchorMin = _window.anchorMax = _window.pivot = new Vector2(.5f,.5f); _window.sizeDelta = new Vector2(1040,720);
            _window.gameObject.AddComponent<Image>().color = new Color(.025f,.035f,.04f,.98f);
            _heading = Label(_window,"Heading",36,24,800,44,28);
            var warningIcon = Rect("WarningIcon", _window);
            Position(warningIcon, 36, 88, 28, 32);
            warningIcon.gameObject.AddComponent<SettingsIconGraphic>().Configure(SettingsIcon.Warning, new Color(.77f,.22f,.25f));
            _warningHeading = Label(_window,"WarningHeading",76,88,928,32,19);
            _warningHeading.color = new Color(.77f,.22f,.25f);
            _warning = Label(_window,"ContentWarning",36,132,968,192,20);
            _documents = new Button[4];
            for (int i=0;i<4;i++) { int index=i; _documents[i]=Button(_window,"Document"+i,36+(i%2)*490,342+(i/2)*58,478,48,()=>Read(index),out _); }
            _agreement = Checkbox("Agreement",474,90,out _agreementLabel);
            _status=Label(_window,"PolicyStatus",36,576,968,54,18);

            var decline = Button(_window,"Decline",36,648,260,48,Decline,out _declineLabel);
            ActionColors(decline, false);
            _accept=Button(_window,"Accept",610,648,394,48,Accept,out _acceptLabel);
            ActionColors(_accept, true);
            Button(_window,"Language",850,25,154,40,()=> { GameLanguage.Set(GameLanguage.Current==GameLocale.Vietnamese?GameLocale.English:GameLocale.Vietnamese); },out _languageLabel);
            _reader=Rect("DocumentReader",_window).gameObject; Stretch((RectTransform)_reader.transform);
            _reader.AddComponent<Image>().color = new Color(.02f,.03f,.035f,1);
            _readTitle=Label(_reader.transform,"DocumentTitle",30,22,980,50,23);
            var viewport=Rect("Viewport",_reader.transform); Position(viewport,30,90,980,530);
            viewport.gameObject.AddComponent<Image>().color = new Color(.025f,.035f,.04f); viewport.gameObject.AddComponent<Mask>().showMaskGraphic=false;
            var content=Rect("Content",viewport); content.anchorMin=new Vector2(0,1); content.anchorMax=Vector2.one; content.pivot=new Vector2(.5f,1); content.sizeDelta=Vector2.zero;
            _readText=content.gameObject.AddComponent<Text>(); Style(_readText,20); _readText.alignment=TextAnchor.UpperLeft;
            var fitter=content.gameObject.AddComponent<ContentSizeFitter>(); fitter.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            var scroll=_reader.AddComponent<ScrollRect>(); scroll.viewport=viewport; scroll.content=content; scroll.horizontal=false; scroll.movementType=ScrollRect.MovementType.Clamped;
            Button(_reader.transform,"Back",744,648,266,48,()=>{_reader.SetActive(false);_reading=-1;},out _readCloseLabel);
            _reader.SetActive(false);
            var publicButton = Button(transform,"PoliciesButton",0,0,240,36,OpenPolicies,out var publicLabel);
            var publicRect=(RectTransform)publicButton.transform;
            publicRect.anchorMin=publicRect.anchorMax=new Vector2(0,0);publicRect.pivot=new Vector2(0,0);publicRect.anchoredPosition=new Vector2(16,12);
            publicLabel.text="Chính sách / Policies";
        }
        private static void ActionColors(Button button, bool primary)
        {
            button.targetGraphic.color = Color.white;
            var colors = button.colors;
            colors.normalColor = primary ? new Color(.43f,.085f,.12f) : new Color(.055f,.065f,.075f);
            colors.highlightedColor = primary ? new Color(.53f,.12f,.16f) : new Color(.105f,.12f,.13f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = primary ? new Color(.32f,.05f,.08f) : new Color(.035f,.04f,.05f);
            colors.disabledColor = new Color(.19f,.21f,.22f);
            colors.fadeDuration = .12f;
            button.colors = colors;
        }
        private Toggle Checkbox(string name,float y,float height,out Text label)
        {
            var rect=Rect(name,_window); Position(rect,36,y,968,height);
            var hitArea=rect.gameObject.AddComponent<Image>(); hitArea.color=Color.clear;
            var toggle=rect.gameObject.AddComponent<Toggle>();
            var box=Rect("Box",rect); Position(box,0,6,28,28); var background=box.gameObject.AddComponent<Image>(); background.color=new Color(.25f,.27f,.27f);
            var check=Rect("Check",box); Stretch(check); check.offsetMin=new Vector2(5,5);check.offsetMax=new Vector2(-5,-5);
            var mark=check.gameObject.AddComponent<Image>();mark.color=new Color(.7f,.13f,.15f); toggle.targetGraphic=background;toggle.graphic=mark;toggle.isOn=false;
            label=Label(rect,"Caption",42,0,926,height,18); label.alignment=TextAnchor.UpperLeft;
            return toggle;
        }
        private static RectTransform Rect(string name,Transform parent) { var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);return r; }
        private static void Stretch(RectTransform r) { r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero; }
        private static void Position(RectTransform r,float x,float y,float w,float h) { r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h); }
        private static void Style(Text t,int size) { t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.fontSize=size;t.color=new Color(.9f,.89f,.85f);t.raycastTarget=false;t.supportRichText=false; }
        private static Text Label(Transform parent,string name,float x,float y,float w,float h,int size) { var r=Rect(name,parent);Position(r,x,y,w,h);var t=r.gameObject.AddComponent<Text>();Style(t,size);t.alignment=TextAnchor.MiddleLeft;return t; }
        private static Button Button(Transform parent,string name,float x,float y,float w,float h,UnityEngine.Events.UnityAction action,out Text label)
        {
            var r=Rect(name,parent);Position(r,x,y,w,h);var image=r.gameObject.AddComponent<Image>();image.color=new Color(.11f,.075f,.075f);
            var button=r.gameObject.AddComponent<Button>();button.targetGraphic=image;button.onClick.AddListener(action);
            label=Label(r,"Label",12,0,w-24,h,18);label.alignment=TextAnchor.MiddleCenter;return button;
        }
    }
}
