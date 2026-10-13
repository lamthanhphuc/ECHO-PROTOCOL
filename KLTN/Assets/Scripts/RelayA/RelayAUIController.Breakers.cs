using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace EchoProtocol.RelayA
{
    public sealed partial class RelayAUIController
    {
        [SerializeField] private RectTransform _breakerSurface;
        [SerializeField] private RectTransform _breakerBoard;
        [SerializeField] private TMP_Text _breakerStatus;
        [SerializeField] private TMP_Text _breakerCounts;
        [SerializeField] private TMP_Text _breakerRule;
        [SerializeField] private Button _breakerReset;
        [SerializeField] private Button _breakerClose;
        [SerializeField] private int breakerLayoutRevision;
        private const int BreakerLayoutRevision = 2;
        private Button[] _breakerTiles;
        private TMP_Text[] _breakerSymbols;
        private Image[][] _breakerEdges;
        private RectTransform _breakerGrid;
        private bool _breakerBound;
        private int _breakerSize;
        private int _hoverBreaker = -1;
        private int _deniedBreaker = -1;
        private float _deniedUntil;

        public void RebuildBreakerLayout()
        {
            if (_breakerSurface != null)
            {
                _breakerSurface.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(_breakerSurface.gameObject);
                else DestroyImmediate(_breakerSurface.gameObject);
            }
            _breakerSurface = null;
            _breakerSize = 0;
            _breakerTiles = null;
            _breakerBound = false;
            EnsureBreakerPanel();
        }

        private void EnsureBreakerPanel()
        {
            if (panelRoot == null) return;
            if (_breakerSurface != null && breakerLayoutRevision != BreakerLayoutRevision)
            {
                RebuildBreakerLayout();
                return;
            }
            LoadStabilizationArtInEditor();
            if (_breakerSurface == null)
            {
                _breakerSurface = AddIndustrialPanel(panelRoot.transform, "BreakerMatrix", Vector2.zero,
                    Vector2.one, stabilizationPanelSprite);
                AddText(_breakerSurface, "Title", "Relay A · Nguồn điện", new Vector2(0.045f, 0.865f),
                    new Vector2(0.60f, 0.06f), 24, TextAlignmentOptions.Left, Color.white);
                AddText(_breakerSurface, "Stage", "02 · Ma trận cầu dao", new Vector2(0.045f, 0.815f),
                    new Vector2(0.70f, 0.04f), 17, TextAlignmentOptions.Left, LiveLine);
                _breakerStatus = AddText(_breakerSurface, "Status", "ACTIVE", new Vector2(0.72f, 0.845f),
                    new Vector2(0.23f, 0.065f), 22, TextAlignmentOptions.Right, LiveLine);
                _breakerBoard = new GameObject("BreakerBoard", typeof(RectTransform)).GetComponent<RectTransform>();
                _breakerBoard.SetParent(_breakerSurface, false);
                SetRect(_breakerBoard, new Vector2(0.15f, 0.245f), new Vector2(0.85f, 0.795f));
                _breakerCounts = AddText(_breakerSurface, "Counts", "", new Vector2(0.15f, 0.19f),
                    new Vector2(0.70f, 0.05f), 19, TextAlignmentOptions.Center, Color.white);
                _breakerRule = AddText(_breakerSurface, "Rule", "Đưa mọi ô về màu xanh.\nNhấn đổi ô hiện tại và 4 ô cạnh nó.",
                    new Vector2(0.045f, 0.13f), new Vector2(0.91f, 0.055f), 16, TextAlignmentOptions.Left, IdleLine);
                _breakerReset = AddIndustrialButton(_breakerSurface, "ResetMatrix", "Đặt lại ma trận",
                    new Vector2(0.045f, 0.045f), new Vector2(0.20f, 0.075f), stabilizationButtonSprite);
                _breakerClose = AddIndustrialButton(_breakerSurface, "Close", "CLOSE",
                    new Vector2(0.805f, 0.045f), new Vector2(0.15f, 0.075f), stabilizationButtonSprite);
                breakerLayoutRevision = BreakerLayoutRevision;
                _breakerSurface.gameObject.SetActive(false);
            }
            EchoProtocol.UI.HUD.HUDModalPresentation.StyleRelayControl(_breakerReset);
            EchoProtocol.UI.HUD.HUDModalPresentation.StyleRelayControl(_breakerClose);
            if (_breakerBound || !Application.isPlaying) return;
            _breakerBound = true;
            _breakerReset.onClick.AddListener(() => SendBreaker(-1, true));
            _breakerClose.onClick.AddListener(Close);
        }

        private void BuildBreakerBoard(int size)
        {
            if (_breakerGrid != null)
            {
                _breakerGrid.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(_breakerGrid.gameObject);
                else DestroyImmediate(_breakerGrid.gameObject);
            }
            Canvas.ForceUpdateCanvases();
            float width = _breakerBoard.rect.width > 0f ? _breakerBoard.rect.width : 800f;
            float height = _breakerBoard.rect.height > 0f ? _breakerBoard.rect.height : 370f;
            float pixels = Mathf.Min(width, height);
            _breakerGrid = new GameObject("Breakers", typeof(RectTransform)).GetComponent<RectTransform>();
            _breakerGrid.SetParent(_breakerBoard, false);
            _breakerGrid.anchorMin = _breakerGrid.anchorMax = new Vector2(0.5f, 0.5f);
            _breakerGrid.sizeDelta = new Vector2(pixels, pixels);
            _breakerGrid.anchoredPosition = Vector2.zero;
            _breakerSize = size;
            _hoverBreaker = -1;
            _deniedBreaker = -1;
            _breakerTiles = new Button[size * size];
            _breakerSymbols = new TMP_Text[size * size];
            _breakerEdges = new Image[size * size][];
            for (int cell = 0; cell < size * size; cell++)
            {
                int index = cell;
                float step = 1f / size;
                float gap = 3f / pixels;
                var tile = AddButton(_breakerGrid, "Breaker_" + cell, "",
                    new Vector2(cell % size * step + gap, 1f - (cell / size + 1) * step + gap),
                    new Vector2(step - gap * 2f, step - gap * 2f));
                _breakerTiles[cell] = tile;
                AddText(tile.transform, "ID", $"B{cell + 1:00}", new Vector2(0.09f, 0.74f),
                    new Vector2(0.46f, 0.19f), 12, TextAlignmentOptions.Left, IdleLine);
                _breakerSymbols[cell] = AddText(tile.transform, "State", "", new Vector2(0.1f, 0.18f),
                    new Vector2(0.8f, 0.55f), 30, TextAlignmentOptions.Center, Green);
                var locked = new GameObject("Lock", typeof(RectTransform)).GetComponent<RectTransform>();
                locked.SetParent(tile.transform, false);
                SetRect(locked, new Vector2(0.74f, 0.74f), new Vector2(0.93f, 0.94f));
                AddLockPart(locked, new Vector2(0f, 0f), new Vector2(1f, 0.58f));
                AddLockPart(locked, new Vector2(0.17f, 0.5f), new Vector2(0.33f, 0.9f));
                AddLockPart(locked, new Vector2(0.67f, 0.5f), new Vector2(0.83f, 0.9f));
                AddLockPart(locked, new Vector2(0.17f, 0.83f), new Vector2(0.83f, 1f));
                locked.gameObject.SetActive((_controller.Circuit.Breakers.Snapshot.Pattern.Locked & (1u << cell)) != 0);
                _breakerEdges[cell] = new[]
                {
                    AddFlatImage(tile.transform, "Top", new Vector2(0f, 0.97f), Vector2.one, TileBack),
                    AddFlatImage(tile.transform, "Bottom", Vector2.zero, new Vector2(1f, 0.03f), TileBack),
                    AddFlatImage(tile.transform, "Left", Vector2.zero, new Vector2(0.03f, 1f), TileBack),
                    AddFlatImage(tile.transform, "Right", new Vector2(0.97f, 0f), Vector2.one, TileBack),
                };
                tile.onClick.AddListener(() => SendBreaker(index, false));
                var trigger = tile.gameObject.AddComponent<EventTrigger>();
                var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                enter.callback.AddListener(_ => _hoverBreaker = index);
                var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
                exit.callback.AddListener(_ => { if (_hoverBreaker == index) _hoverBreaker = -1; });
                trigger.triggers.Add(enter);
                trigger.triggers.Add(exit);
            }
        }

        private void RefreshBreakers()
        {
            if (_controller == null) return;
            var state = _controller.Circuit.Breakers.Snapshot;
            if (_breakerSize != state.Pattern.Size || _breakerTiles == null) BuildBreakerBoard(state.Pattern.Size);
            bool editing = state.Phase == RelayABreakerPhase.Editing;
            _breakerReset.interactable = editing;

            _breakerRule.text = "Đưa mọi ô về màu xanh.\nNhấn đổi ô hiện tại và 4 ô cạnh nó."
                + (state.Pattern.Locked != 0 ? " Không thể nhấn cầu dao bị khóa." : "");
            _breakerCounts.text = $"Ô ổn định {state.StableCount}/{state.Pattern.Size * state.Pattern.Size} · {state.Moves} lượt";
            _breakerStatus.text = Time.unscaledTime < _deniedUntil ? EchoProtocol.Settings.GameLanguage.Choose("Đã khóa", "Locked")
                : state.Phase == RelayABreakerPhase.Balancing || state.Phase == RelayABreakerPhase.Complete ? "Ma trận ổn định" : "ACTIVE";
            _breakerStatus.color = state.Red == 0 ? Green : LiveLine;
            uint preview = editing && _hoverBreaker >= 0 && (state.Pattern.Locked & (1u << _hoverBreaker)) == 0
                ? RelayABreakerMatrix.ToggleMask(state.Pattern.Size, _hoverBreaker) : 0u;
            for (int cell = 0; cell < _breakerTiles.Length; cell++)
            {
                bool red = (state.Red & (1u << cell)) != 0;
                bool pulse = (state.Pulse & (1u << cell)) != 0;
                bool locked = (state.Pattern.Locked & (1u << cell)) != 0;
                Color color = red ? Red : Green;
                _breakerTiles[cell].interactable = editing;
                _breakerTiles[cell].GetComponent<Image>().color = red
                    ? new Color(0.22f, 0.065f, 0.065f, 1f) : new Color(0.045f, 0.19f, 0.12f, 1f);
                _breakerSymbols[cell].text = red ? "\u00D7" : "\u25CF";
                _breakerSymbols[cell].color = color;
                Color edge = pulse ? Color.Lerp(color, Color.white, 0.5f + Mathf.Sin(Time.unscaledTime * 24f) * 0.4f)
                    : (preview & (1u << cell)) != 0 ? LiveLine : locked ? WarningYellow : Color.Lerp(TileBack, color, 0.3f);
                foreach (var image in _breakerEdges[cell]) image.color = edge;
                _breakerTiles[cell].GetComponent<RectTransform>().anchoredPosition = cell == _deniedBreaker && Time.unscaledTime < _deniedUntil
                    ? new Vector2(Mathf.Sin(Time.unscaledTime * 70f) * 3f, 0f) : Vector2.zero;
            }
        }

        private void SendBreaker(int cell, bool reset)
        {
            if (_controller == null || _controller.CircuitSnapshot.Phase != RelayACircuitPhase.BreakerMatrix) return;
            var state = _controller.Circuit.Breakers.Snapshot;
            if (!reset && (state.Pattern.Locked & (1u << cell)) != 0)
            {
                _deniedBreaker = cell;
                _deniedUntil = Time.unscaledTime + 0.3f;
                _controller.PlayBreakerDenied();
                RefreshBreakers();
                return;
            }
            if (TryGetNetworkDirector(out var director)) director.RequestRelayABreaker(_controller, cell, reset);
            else if (reset) _controller.ResetBreakers();
            else _controller.PressBreaker(cell);
        }
    }
}
