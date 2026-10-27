using System.Collections.Generic;
using EchoProtocol.Networking;
using EchoProtocol.Settings;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EchoProtocol.UI
{
    public sealed class LobbyMapSelector : MonoBehaviour
    {
        public RectTransform Panel;
        public Texture2D[] Previews;
        public Sprite Frame, Arrow;
        public TMP_FontAsset Font;
        private RawImage _photo;
        private TMP_Text _title;
        private Button _previous, _next;
        private NetworkBootstrap _bootstrap;
        private LobbyManager _manager;
        private float _refreshAt;
        private bool _confirmedPurgeMode;
        private NetworkRunner _confirmedRunner;
        private string _confirmedRoom;

        private void Start()
        {
            var frame = Rect("MapSelection", Panel, 0, 334, 600, 166);
            var border = frame.gameObject.AddComponent<Image>(); border.sprite = Frame; border.type = Image.Type.Sliced;
            var photo = Rect("Preview", frame, 6, 6, 588, 154);
            _photo = photo.gameObject.AddComponent<RawImage>(); _photo.raycastTarget = false;
            var shade = Rect("TitleShade", frame, 6, 121, 588, 39).gameObject.AddComponent<Image>();
            shade.color = new Color(0, 0, 0, .75f); shade.raycastTarget = false;
            _title = Rect("MapName", frame, 52, 121, 496, 39).gameObject.AddComponent<TextMeshProUGUI>();
            _title.font = Font; _title.fontSize = 25; _title.alignment = TextAlignmentOptions.Center;
            _title.color = new Color(.8f, .76f, .5f); _title.raycastTarget = false;
            _previous = Button(frame, "Previous", 12, true); _next = Button(frame, "Next", 548, false);
            _previous.onClick.AddListener(() => Step(-1)); _next.onClick.AddListener(() => Step(1));
            Move("ReferenceDifficultyLabel", 508); Move("ReferenceDifficultyPicker", 508);
            Move("ReferenceToolLabel", 568); Move("ReferenceToolPicker", 568);
            for (int i = 0; i < 3; i++) { Move("ReferenceSettingLabel" + i, 628 + 60 * i); Move("ReferenceSettingPicker" + i, 628 + 60 * i); }
        }
        private void Move(string name, float y)
        {
            var rect = Panel.Find(name) as RectTransform;
            if (rect != null) rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -y);
        }
        private static RectTransform Rect(string name, Transform parent, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.layer = parent.gameObject.layer;
            var rect = (RectTransform)go.transform; rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(w, h); return rect;
        }
        private Button Button(Transform parent, string name, float x, bool left)
        {
            var rect = Rect(name, parent, x, 60, 40, 46);
            var hitArea = rect.gameObject.AddComponent<Image>();
            hitArea.color = Color.clear; hitArea.raycastTarget = true;
            var icon = Rect("Arrow", rect, 9, 10, 22, 26);
            var image = icon.gameObject.AddComponent<Image>(); image.sprite = Arrow;
            image.raycastTarget = false;
            image.color = new Color(.8f, .76f, .5f); image.preserveAspect = true;
            icon.pivot = new Vector2(.5f, .5f); icon.anchoredPosition = new Vector2(20, -23);
            if (left) icon.localScale = new Vector3(-1, 1, 1);
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            return button;
        }
        private bool CanEdit => _bootstrap != null && _bootstrap.Runner != null && _bootstrap.Runner.IsServer
            && _bootstrap.State == NetworkSessionState.InLobby && _manager != null
            && !_manager.IsStartingMatch && !_manager.CurrentState.IsReady
            && _bootstrap.Runner.SessionInfo.IsValid
            && !_confirmedPurgeMode && !LobbyMapCatalog.MapLocked(_bootstrap.Runner);
        private void Step(int direction)
        {
            if (!CanEdit) return;
            int next = (LobbyMapCatalog.Selected(_bootstrap.Runner) + direction + 2) % 2;
            if (LobbyMapCatalog.TrySelect(_bootstrap.Runner, next))
            {
                LobbySelectionMemory.Write("map", next);
                _refreshAt = 0;
                Update();
            }
        }
        private void Update()
        {
            if (_title == null || Time.unscaledTime < _refreshAt) return;
            _refreshAt = Time.unscaledTime + .15f;
            if (_bootstrap == null) _bootstrap = FindAnyObjectByType<NetworkBootstrap>();
            if (_manager == null) _manager = FindAnyObjectByType<LobbyManager>();
            var runner = _bootstrap != null ? _bootstrap.Runner : null;
            if (runner != _confirmedRunner || runner != null && runner.SessionInfo.IsValid
                && _confirmedRoom != runner.SessionInfo.Name)
            {
                _confirmedRunner = runner;
                _confirmedRoom = runner != null && runner.SessionInfo.IsValid ? runner.SessionInfo.Name : null;
                _confirmedPurgeMode = false;
            }
            if (runner != null && runner.SessionInfo.IsValid
                && runner.SessionInfo.Properties.TryGetValue(LobbyMapCatalog.GameModeKey, out var mode))
                _confirmedPurgeMode = (int)mode == 1;
            if (CanEdit && runner.SessionInfo.IsValid && !runner.SessionInfo.Properties.ContainsKey(LobbyMapCatalog.PropertyKey))
                LobbyMapCatalog.TrySelect(runner, Mathf.Clamp(LobbySelectionMemory.Read("map", 0), 0, LobbyMapCatalog.MapCount - 1));
            int map = _confirmedPurgeMode ? 2 : LobbyMapCatalog.Selected(runner);
            int preview = map == 2 ? 1 : map;
            _photo.texture = Previews != null && preview < Previews.Length ? Previews[preview] : null;
            if (_photo.texture != null)
            {
                float aspect = (float)_photo.texture.width / _photo.texture.height;
                float height = Mathf.Min(1, aspect / (588f / 154f));
                _photo.uvRect = new Rect(0, (1-height)*.5f, 1, height);
            }
            _title.text = GameLanguage.Choose("Bản đồ: ", "Map: ") + (map == 2
                ? GameLanguage.Choose("Vùng ngập · Săn alien", "Flooded Grounds · Alien Hunt") : map == 1
                ? GameLanguage.Choose("Vùng ngập", "Flooded Grounds") : GameLanguage.Choose("Cơ sở nghiên cứu", "Research Facility"));
            _previous.interactable = _next.interactable = CanEdit;
        }
    }
}
