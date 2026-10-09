using EchoProtocol.MatchFlow;
using EchoProtocol.Networking;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace EchoProtocol.RelayA
{
    [DisallowMultipleComponent]
    public sealed partial class RelayAUIController : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Vector2 preferredPanelSize = new Vector2(1240f, 800f);

        private readonly RelayAPlayerControlLock _controlLock = new RelayAPlayerControlLock();
        private RelayAController _controller;
        [SerializeField] private RectTransform _surface;
        [SerializeField] private RectTransform _board;
        private RectTransform _grid;
        [SerializeField] private TMP_Text _status;
        [SerializeField] private TMP_Text _rules;
        [SerializeField] private Button _testButton;
        [SerializeField] private Button _closeButton;
        [SerializeField] private int routingLayoutRevision;
        [SerializeField] private TMP_Text _routingStage;
        private const int RoutingLayoutRevision = 2;
        private bool _buttonsBound;
        private Button[] _tiles;
        private Image[][] _arms;
        private TMP_Text[] _symbols;
        private GameObject[] _locks;
        private TMP_FontAsset _font;
        private int _boardCount;
        private RelayACircuitScenario _renderedScenario;

        private static readonly Color Back = new Color(0.045f, 0.055f, 0.055f, 0.98f);
        private static readonly Color TileBack = new Color(0.12f, 0.15f, 0.15f, 1f);
        private static readonly Color IdleLine = new Color(0.55f, 0.69f, 0.72f, 1f);
        private static readonly Color LiveLine = new Color(0.49f, 0.65f, 0.64f, 1f);
        private static readonly Color SourceLine = new Color32(239, 185, 84, 255);
        private static readonly Color Green = new Color(0.52f, 0.68f, 0.57f, 1f);
        private static readonly Color Red = new Color(0.82f, 0.41f, 0.37f, 1f);

        public bool IsOpen => panelRoot != null && panelRoot.activeSelf;

        private void Awake() => SetVisible(false);
        private void OnDestroy() => Close();
        private void OnDisable() => Close();

        private void Update()
        {
            if (!IsOpen) return;
            FitPanelToCanvas();
            if (_breakerSurface != null && _breakerSurface.gameObject.activeSelf) RefreshBreakers();
            if (_controlLock.ShouldAutoRelease() || _controlLock.ConsumeEscape()
                || (TryGetNetworkDirector(out var director) && !director.CanLocalPlayerOperateRelay(_controller))) Close();
        }

        public void Bind(RelayAController controller) => _controller = controller;

        public void Open(GameObject interactor)
        {
            if (IsOpen || panelRoot == null) return;
            EnsureEventSystem();
            Zone2MinigameUIFocus.CloseOthers(this);
            if (TryGetNetworkDirector(out var director)
                && (!director.CanLocalPlayerOperateRelay(_controller)
                    || !director.RequestRelayAcquire(_controller))) return;
            _controlLock.Acquire(interactor, Close);
            if (!_controlLock.IsLocked) return;
            EnsureSurface();
            SetVisible(true);
            if (_controller != null) RefreshCircuit(_controller.CircuitSnapshot);
        }

        public void Close()
        {
            if (!_controlLock.IsLocked && !IsOpen) return;
            if (TryGetNetworkDirector(out var director)) director.RequestRelayRelease(_controller);
            SetVisible(false);
            _controlLock.Release();
        }

        public void RefreshCircuit(RelayACircuitSnapshot snapshot)
        {
            if (!IsOpen || snapshot.Scenario == null) return;
            EnsureSurface();
            EnsureStabilizationPanel();
            EnsureBreakerPanel();
            FitPanelToCanvas();
            bool finalStage = snapshot.Phase == RelayACircuitPhase.StabilizeOutput || snapshot.IsOnline;
            bool breakerStage = snapshot.Phase == RelayACircuitPhase.BreakerMatrix;
            _surface.gameObject.SetActive(!finalStage && !breakerStage);
            _stabilizationSurface.gameObject.SetActive(finalStage);
            _breakerSurface.gameObject.SetActive(breakerStage);
            if (breakerStage)
            {
                RefreshBreakers();
                return;
            }
            if (finalStage)
            {
                RefreshStabilization();
                return;
            }
            if (_boardCount != snapshot.Scenario.Count || _renderedScenario != snapshot.Scenario) BuildBoard(snapshot.Scenario);
            bool busy = snapshot.Phase == RelayACircuitPhase.Testing || snapshot.Phase == RelayACircuitPhase.Stable;
            _routingStage.text = snapshot.FaultActive ? "01 · Nối mạch — Đi vòng ô hỏng" : "01 · Nối mạch — Cấp điện cho các đầu nối";
            for (int i = 0; i < _boardCount; i++)
            {
                var cell = snapshot.Scenario.Cells[i];
                bool broken = snapshot.FaultActive && i == snapshot.Scenario.FailedCell;
                bool powered = (snapshot.Powered & (1UL << i)) != 0;
                _tiles[i].interactable = !busy && !snapshot.IsOnline && cell.Type != RelayACircuitTile.Empty;
                _tiles[i].GetComponent<Image>().color = broken ? new Color(0.32f, 0.08f, 0.09f, 1f)
                    : cell.Type == RelayACircuitTile.Source ? new Color(0.28f, 0.19f, 0.07f, 1f)
                    : powered ? new Color(0.05f, 0.3f, 0.34f, 1f) : TileBack;
                int mask = broken ? 0 : RelayACircuitBoard.Connections(cell.Type, snapshot.Rotations[i]);
                Color lineColor = cell.Type == RelayACircuitTile.Fault ? Red
                    : cell.Type == RelayACircuitTile.Target ? Green
                    : cell.Type == RelayACircuitTile.Source ? SourceLine : powered ? LiveLine : IdleLine;
                for (int direction = 0; direction < 4; direction++)
                {
                    _arms[i][direction].gameObject.SetActive((mask & (1 << direction)) != 0);
                    _arms[i][direction].color = lineColor;
                }
                _symbols[i].text = broken ? "X" : cell.Type == RelayACircuitTile.Source ? "P"
                    : cell.Type == RelayACircuitTile.Target ? "●" : cell.Type == RelayACircuitTile.Fault ? "X"
                    : "";
                _locks[i].SetActive(cell.Locked && cell.Type != RelayACircuitTile.Empty
                    && cell.Type != RelayACircuitTile.Source && cell.Type != RelayACircuitTile.Target
                    && cell.Type != RelayACircuitTile.Fault);
                _symbols[i].color = cell.Type == RelayACircuitTile.Target ? Green
                    : cell.Type == RelayACircuitTile.Source ? SourceLine
                    : cell.Type == RelayACircuitTile.Fault || broken ? Red : powered ? LiveLine : IdleLine;
            }

            _status.text = snapshot.Phase == RelayACircuitPhase.Testing ? "Đang kiểm tra mạch"
                : snapshot.Phase == RelayACircuitPhase.Stable ? "Mạch ổn định"
                : snapshot.IsOnline ? "Relay hoạt động"
                : snapshot.FaultActive && snapshot.Phase != RelayACircuitPhase.Failed ? "Đường điện thay đổi · Đi vòng ô đỏ"
                : snapshot.Phase == RelayACircuitPhase.Failed ? snapshot.FaultPowered
                    ? "Bảo vệ ngắt điện · Bảng đã đặt lại"
                    : "Nối sai · Bảng đã đặt lại"
                : "Sẵn sàng kiểm tra";
            _status.color = snapshot.IsOnline ? Green
                : snapshot.Phase == RelayACircuitPhase.Failed || snapshot.Phase == RelayACircuitPhase.Faulted ? Red : LiveLine;
            _testButton.interactable = !busy && !snapshot.IsOnline;
        }

        public void BuildLayout()
        {
            EnsureSurface();
            EnsureStabilizationPanel();
            EnsureBreakerPanel();
            FitPanelToCanvas();
        }

        private void FitPanelToCanvas()
        {
            if (panelRoot == null || !(panelRoot.transform.parent is RectTransform parent)) return;
            var canvas = panelRoot.GetComponentInParent<Canvas>();
            if (canvas == null || canvas.renderMode == RenderMode.WorldSpace || parent.rect.width <= 0f || parent.rect.height <= 0f) return;
            float scale = Mathf.Min(1f, Mathf.Min(parent.rect.width * 0.90f / Mathf.Max(1f, preferredPanelSize.x),
                parent.rect.height * 0.84f / Mathf.Max(1f, preferredPanelSize.y)));
            var panel = panelRoot.GetComponent<RectTransform>();
            Vector2 size = preferredPanelSize * scale;
            if ((panel.sizeDelta - size).sqrMagnitude < 0.1f) return;
            panel.sizeDelta = size;
            Canvas.ForceUpdateCanvases();
            if (_grid != null && _renderedScenario != null)
            {
                float cell = Mathf.Min(_board.rect.width / _renderedScenario.Width, _board.rect.height / _renderedScenario.Height);
                _grid.sizeDelta = new Vector2(cell * _renderedScenario.Width, cell * _renderedScenario.Height);
            }
            if (_breakerGrid != null)
            {
                float side = Mathf.Min(_breakerBoard.rect.width, _breakerBoard.rect.height);
                _breakerGrid.sizeDelta = new Vector2(side, side);
            }
        }

        public void RebuildRoutingLayout()
        {
            if (_surface != null)
            {
                _surface.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(_surface.gameObject);
                else DestroyImmediate(_surface.gameObject);
            }
            _surface = null;
            _boardCount = 0;
            _renderedScenario = null;
            _buttonsBound = false;
            EnsureSurface();
        }

        private void EnsureSurface()
        {
            if (panelRoot == null) return;
            _font = panelRoot.GetComponentInChildren<TMP_Text>(true)?.font ?? TMP_Settings.defaultFontAsset;
            if (_surface != null && routingLayoutRevision != RoutingLayoutRevision)
            {
                RebuildRoutingLayout();
                return;
            }
            if (_surface != null)
            {
                BindButtons();
                return;
            }
            LoadStabilizationArtInEditor();
            _surface = AddIndustrialPanel(panelRoot.transform, "PowerRoutingMatrix", Vector2.zero, Vector2.one, stabilizationPanelSprite);
            AddText(_surface, "Title", "Relay A · Nguồn điện", new Vector2(0.045f, 0.865f),
                new Vector2(0.60f, 0.06f), 24, TextAlignmentOptions.Left, Color.white);
            _routingStage = AddText(_surface, "Stage", "01 · Nối mạch", new Vector2(0.045f, 0.815f),
                new Vector2(0.91f, 0.04f), 17, TextAlignmentOptions.Left, LiveLine);
            _rules = AddText(_surface, "Rules", "P  Nguồn điện\n●  Đầu nối\nX  Ô hỏng\n\nCấp điện cho mọi đầu nối.\nCách ly các ô hỏng.\n\nKiểm tra thất bại sẽ\nđặt lại hướng các ô.",
                new Vector2(0.045f, 0.28f), new Vector2(0.265f, 0.48f), 17,
                TextAlignmentOptions.TopLeft, IdleLine);
            _board = new GameObject("CircuitBoard", typeof(RectTransform)).GetComponent<RectTransform>();
            _board.SetParent(_surface, false);
            SetRect(_board, new Vector2(0.32f, 0.21f), new Vector2(0.955f, 0.79f));
            _status = AddText(_surface, "Status", "Sẵn sàng kiểm tra",
                new Vector2(0.045f, 0.135f), new Vector2(0.91f, 0.05f), 16,
                TextAlignmentOptions.Left, LiveLine);
            _testButton = AddIndustrialButton(_surface, "TestCircuit", "Kiểm tra mạch",
                new Vector2(0.045f, 0.045f), new Vector2(0.32f, 0.075f), stabilizationButtonSprite);
            _closeButton = AddIndustrialButton(_surface, "Close", "CLOSE",
                new Vector2(0.805f, 0.045f), new Vector2(0.15f, 0.075f), stabilizationButtonSprite);
            routingLayoutRevision = RoutingLayoutRevision;
            BindButtons();
        }

        private void BindButtons()
        {
            if (_buttonsBound || !Application.isPlaying) return;
            _buttonsBound = true;
            _testButton.onClick.AddListener(() =>
            {
                if (_controller == null) return;
                if (TryGetNetworkDirector(out var director)) director.RequestRelayATest(_controller);
                else _controller.TestCircuit();
            });
            _closeButton.onClick.AddListener(Close);
        }

        private void BuildBoard(RelayACircuitScenario scenario)
        {
            _renderedScenario = scenario;
            for (int i = _board.childCount - 1; i >= 0; i--)
            {
                var child = _board.GetChild(i).gameObject;
                child.SetActive(false);
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }
            Canvas.ForceUpdateCanvases();
            float availableWidth = _board.rect.width > 0f ? _board.rect.width : 660f;
            float availableHeight = _board.rect.height > 0f ? _board.rect.height : 520f;
            float cellPixels = Mathf.Min(availableWidth / scenario.Width, availableHeight / scenario.Height);
            _grid = new GameObject("Tiles", typeof(RectTransform)).GetComponent<RectTransform>();
            _grid.SetParent(_board, false);
            _grid.anchorMin = _grid.anchorMax = new Vector2(0.5f, 0.5f);
            _grid.sizeDelta = new Vector2(cellPixels * scenario.Width, cellPixels * scenario.Height);
            _grid.anchoredPosition = Vector2.zero;
            _boardCount = scenario.Count;
            _tiles = new Button[_boardCount];
            _arms = new Image[_boardCount][];
            _symbols = new TMP_Text[_boardCount];
            _locks = new GameObject[_boardCount];
            float stepX = 1f / scenario.Width;
            float stepY = 1f / scenario.Height;
            float gapX = 2f / _grid.sizeDelta.x;
            float gapY = 2f / _grid.sizeDelta.y;
            for (int i = 0; i < _boardCount; i++)
            {
                int index = i;
                int x = i % scenario.Width;
                int y = i / scenario.Width;
                var button = AddButton(_grid, $"Tile_{x}_{y}", "",
                    new Vector2(x * stepX + gapX, 1f - (y + 1) * stepY + gapY),
                    new Vector2(stepX - gapX * 2f, stepY - gapY * 2f));
                button.onClick.AddListener(() => HandleTileClicked(index));
                _tiles[i] = button;
                _arms[i] = new Image[4];
                for (int direction = 0; direction < 4; direction++)
                {
                    var arm = new GameObject($"Port_{direction}", typeof(RectTransform), typeof(Image));
                    arm.transform.SetParent(button.transform, false);
                    var rt = arm.GetComponent<RectTransform>();
                    rt.anchorMin = direction == 0 ? new Vector2(0.44f, 0.5f)
                        : direction == 1 ? new Vector2(0.5f, 0.44f)
                        : direction == 2 ? new Vector2(0.44f, 0f) : new Vector2(0f, 0.44f);
                    rt.anchorMax = direction == 0 ? new Vector2(0.56f, 1f)
                        : direction == 1 ? new Vector2(1f, 0.56f)
                        : direction == 2 ? new Vector2(0.56f, 0.5f) : new Vector2(0.5f, 0.56f);
                    rt.offsetMin = rt.offsetMax = Vector2.zero;
                    _arms[i][direction] = arm.GetComponent<Image>();
                    _arms[i][direction].raycastTarget = false;
                }
                _symbols[i] = AddText(button.transform, "Symbol", "", new Vector2(0f, 0f),
                    Vector2.one, 21, TextAlignmentOptions.Center, Color.white);
                var lockRoot = new GameObject("Lock", typeof(RectTransform));
                lockRoot.transform.SetParent(button.transform, false);
                SetRect(lockRoot.GetComponent<RectTransform>(), new Vector2(0.70f, 0.70f), new Vector2(0.93f, 0.94f));
                AddLockPart(lockRoot.transform, new Vector2(0f, 0f), new Vector2(1f, 0.58f));
                AddLockPart(lockRoot.transform, new Vector2(0.17f, 0.5f), new Vector2(0.33f, 0.9f));
                AddLockPart(lockRoot.transform, new Vector2(0.67f, 0.5f), new Vector2(0.83f, 0.9f));
                AddLockPart(lockRoot.transform, new Vector2(0.17f, 0.83f), new Vector2(0.83f, 1f));
                _locks[i] = lockRoot;
                if (scenario.Cells[i].Type == RelayACircuitTile.Target)
                    AddText(button.transform, "TerminalName", scenario.Cells[i].Label,
                        new Vector2(0.03f, 0.02f), new Vector2(0.94f, 0.23f), 11, TextAlignmentOptions.Center, Green);
            }
        }

        private static void AddLockPart(Transform parent, Vector2 min, Vector2 max)
        {
            var part = new GameObject("LockPart", typeof(RectTransform), typeof(Image));
            part.transform.SetParent(parent, false);
            SetRect(part.GetComponent<RectTransform>(), min, max);
            part.GetComponent<Image>().color = IdleLine;
            part.GetComponent<Image>().raycastTarget = false;
        }

        private void HandleTileClicked(int index)
        {
            if (_controller == null) return;
            var state = _controller.CircuitSnapshot;
            if (state.Scenario == null || index >= state.Scenario.Count) return;
            if (state.FaultActive && index == state.Scenario.FailedCell)
            {
                _status.text = "Mạch đứt · Chọn đường cấp điện khác";
                _status.color = Red;
                return;
            }
            if (state.Scenario.Cells[index].Locked)
            {
                _status.text = "Ô bị khóa · Không thể xoay";
                _status.color = IdleLine;
                return;
            }
            if (TryGetNetworkDirector(out var director)) director.RequestRelayARotate(_controller, index);
            else _controller.RotateCircuitTile(index);
        }

        private TMP_Text AddText(Transform parent, string name, string value, Vector2 min, Vector2 size,
            float fontSize, TextAlignmentOptions alignment, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            SetRect(go.GetComponent<RectTransform>(), min, min + size);
            var text = go.GetComponent<TMP_Text>();
            text.text = value;
            text.font = _font;
            fontSize = Mathf.Max(12f, fontSize);
            text.fontSize = fontSize;
            text.enableAutoSizing = true;
            text.fontSizeMin = Mathf.Max(12f, fontSize * 0.8f);
            text.fontSizeMax = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        private Button AddButton(Transform parent, string name, string label, Vector2 min, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            SetRect(go.GetComponent<RectTransform>(), min, min + size);
            var image = go.GetComponent<Image>();
            image.color = TileBack;
            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            if (!string.IsNullOrEmpty(label))
            {
                var textGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                textGo.transform.SetParent(go.transform, false);
                SetRect(textGo.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);
                var text = textGo.GetComponent<TMP_Text>();
                text.text = label;
                text.font = _font;
                text.fontSize = 15;
                text.enableAutoSizing = true;
                text.fontSizeMin = 12f;
                text.fontSizeMax = 15f;
                text.alignment = TextAlignmentOptions.Center;
                text.color = Color.white;
                text.raycastTarget = false;
            }
            return button;
        }

        private static void SetRect(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void SetVisible(bool visible)
        {
            if (panelRoot != null) panelRoot.SetActive(visible);
            if (canvasGroup == null) return;
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }

        private static bool TryGetNetworkDirector(out Zone2MissionDirector director)
        {
            director = Zone2MissionDirector.Instance;
            var match = NetworkMatchState.Instance ?? FindAnyObjectByType<NetworkMatchState>();
            return director != null && match != null && match.Object != null && match.Object.IsValid;
        }

        private static void EnsureEventSystem()
        {
            var system = EventSystem.current ?? FindAnyObjectByType<EventSystem>();
            if (system == null) system = new GameObject("RuntimeEventSystem").AddComponent<EventSystem>();
            var old = system.GetComponent<StandaloneInputModule>();
            if (old != null) Destroy(old);
            if (system.GetComponent<InputSystemUIInputModule>() == null)
            {
                var module = system.gameObject.AddComponent<InputSystemUIInputModule>();
                module.AssignDefaultActions();
            }
        }
    }
}
