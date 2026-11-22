using System;
using System.IO;
using System.Runtime.InteropServices;
using EchoProtocol.Auth;
using EchoProtocol.Settings;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace EchoProtocol.UI.MainMenu
{
    // Uses the shared profile portrait; no scene-specific avatar copies or account cache.
    public sealed class ProfileAvatarEditor : MonoBehaviour, IDragHandler
    {
        const string Endpoint = "/api/player/me/avatar";
        const int Width = 256, Height = 332;
        static readonly Vector2 PhotoSize = new Vector2(266, 266f * Height / Width);
        [Serializable] public sealed class AvatarData { public string pixels; }
        [Serializable] public sealed class AvatarReply { public bool success; public AvatarData data; }
        RawImage _portrait, _preview;
        Texture2D _saved, _source;
        Sprite _defaultSprite;
        Sprite _frameSprite;
        RectTransform _dialog;
        Text _message;
        Button _save, _reset, _choose, _close;
        Slider _zoom;
        Vector2 _pan = new(.5f, .5f);
        bool _busy;
        int _version;

        public void Initialize(Image image)
        {
            _defaultSprite = image.sprite;
            _portrait = Node("CustomAvatar", transform, Vector2.zero, PhotoSize).gameObject.AddComponent<RawImage>();
            _portrait.raycastTarget = false;
            _portrait.gameObject.SetActive(false);
            if (_defaultSprite != null)
            {
                // Reuse only the worn outer edges of the original portrait above the photograph.
                _frameSprite = Sprite.Create(_defaultSprite.texture, _defaultSprite.rect,
                    new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, new Vector4(28, 28, 28, 28));
                var frame = Node("PortraitFrame", transform, Vector2.zero,
                    ((RectTransform)transform).sizeDelta).gameObject.AddComponent<Image>();
                frame.sprite = _frameSprite;
                frame.type = Image.Type.Sliced;
                frame.fillCenter = false;
                frame.pixelsPerUnitMultiplier = _defaultSprite.rect.width / ((RectTransform)transform).sizeDelta.x;
                frame.raycastTarget = false;
            }
            image.raycastTarget = true;
            var button = gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(Open);
            var hint = Node("ChangePhotoHint", transform, new Vector2(0, -151), new Vector2(244, 26)).gameObject.AddComponent<Text>();
            Style(hint, 14);
            hint.text = GameLanguage.Choose("BẤM ĐỂ ĐỔI ẢNH", "CLICK TO CHANGE PHOTO");
            hint.gameObject.AddComponent<Shadow>().effectColor = Color.black;
        }

        public void Refresh()
        {
            int version = ++_version;
            Apply(null); // Never retain a previous account's photograph.
            AuthRuntime.EnsureExists().Client.GetJson<AvatarReply>(Endpoint, true, result =>
            {
                if (this == null || version != _version) return;
                if (result.IsSuccess && result.Data?.data != null) Apply(result.Data.data.pixels);
            });
        }

        void Apply(string pixels)
        {
            if (_saved != null) Destroy(_saved);
            _saved = null;
            if (!string.IsNullOrEmpty(pixels))
            {
                try
                {
                    byte[] bytes = Convert.FromBase64String(pixels);
                    if (bytes.Length != Width * Height * 3) throw new FormatException();
                    _saved = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                    _saved.LoadRawTextureData(bytes);
                    _saved.Apply(false, true);
                }
                catch (FormatException) { }
            }
            _portrait.texture = _saved;
            _portrait.gameObject.SetActive(_saved != null);
        }

        void Open()
        {
            if (_dialog == null) BuildDialog();
            _dialog.gameObject.SetActive(true);
            _dialog.SetAsLastSibling();
            _message.text = GameLanguage.Choose("Chọn ảnh PNG/JPG tối đa 2 MB. Kéo ảnh và chỉnh độ phóng.", "Choose PNG/JPG up to 2 MB. Drag and zoom to crop.");
        }

        void BuildDialog()
        {
            _dialog = Node("AvatarEditor", transform.parent, Vector2.zero, Vector2.zero);
            _dialog.anchorMin = Vector2.zero; _dialog.anchorMax = Vector2.one;
            _dialog.offsetMin = _dialog.offsetMax = Vector2.zero;
            _dialog.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, .96f);
            var title = Label("Title", new Vector2(0, 280), new Vector2(800, 48), 30);
            title.text = GameLanguage.Choose("ẢNH HỒ SƠ", "PROFILE PHOTO");
            _preview = Node("CropPreview", _dialog, new Vector2(0, 55), PhotoSize).gameObject.AddComponent<RawImage>();
            _preview.texture = _saved;
            if (_saved == null && _defaultSprite != null)
            {
                _preview.texture = _defaultSprite.texture;
                var rect = _defaultSprite.textureRect;
                _preview.uvRect = new Rect(rect.x / _defaultSprite.texture.width, rect.y / _defaultSprite.texture.height,
                    rect.width / _defaultSprite.texture.width, rect.height / _defaultSprite.texture.height);
            }
            _preview.color = _preview.texture != null ? Color.white : new Color(.15f, .15f, .15f);
            var trigger = _preview.gameObject.AddComponent<EventTrigger>();
            var drag = new EventTrigger.Entry { eventID = EventTriggerType.Drag };
            drag.callback.AddListener(data => OnDrag((PointerEventData)data));
            trigger.triggers.Add(drag);
            var sliderRect = Node("Zoom", _dialog, new Vector2(0, -150), new Vector2(278, 22));
            sliderRect.gameObject.AddComponent<Image>().color = new Color(.24f, .23f, .2f);
            _zoom = sliderRect.gameObject.AddComponent<Slider>();
            var handle = Node("Handle", sliderRect, Vector2.zero, new Vector2(18, 22));
            handle.gameObject.AddComponent<Image>().color = new Color(.77f, .73f, .51f);
            _zoom.handleRect = handle;
            _zoom.targetGraphic = handle.GetComponent<Image>();
            _zoom.minValue = 1; _zoom.maxValue = 3; _zoom.value = 1;
            _zoom.onValueChanged.AddListener(_ => UpdateCrop());
            _message = Label("Message", new Vector2(0, -198), new Vector2(900, 54), 18);
            _choose = ActionButton(GameLanguage.Choose("CHỌN ẢNH", "CHOOSE PHOTO"), -310, -260, Choose);
            _save = ActionButton(GameLanguage.Choose("LƯU", "SAVE"), -100, -260, Save);
            _reset = ActionButton(GameLanguage.Choose("ẢNH MẶC ĐỊNH", "RESET PHOTO"), 110, -260, ResetPhoto);
            _close = ActionButton(GameLanguage.Choose("ĐÓNG", "CLOSE"), 320, -260, () => { if (!_busy) _dialog.gameObject.SetActive(false); });
            SetBusy(false);
        }

        void Choose()
        {
            try
            {
                string path = PickFile();
                if (string.IsNullOrEmpty(path)) return;
                if (new FileInfo(path).Length > 2 * 1024 * 1024) throw new InvalidDataException();
                byte[] bytes = File.ReadAllBytes(path);
                if (!ValidImageSize(bytes)) throw new InvalidDataException();
                var texture = new Texture2D(2, 2);
                if (!texture.LoadImage(bytes)) { Destroy(texture); throw new InvalidDataException(); }
                if (_source != null) Destroy(_source);
                _source = texture;
                _preview.texture = _source;
                _preview.color = Color.white;
                _pan = new Vector2(.5f, .5f); _zoom.value = 1;
                UpdateCrop(); SetBusy(false);
                _message.text = GameLanguage.Choose("Kéo ảnh để căn khung, sau đó bấm Lưu.", "Drag to frame your photo, then select Save.");
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is NotSupportedException)
            {
                _message.text = GameLanguage.Choose("Không đọc được ảnh. Dùng PNG/JPG ≤ 2 MB, tối đa 4096 × 4096.", "Cannot read image. Use PNG/JPG ≤ 2 MB, up to 4096 × 4096.");
            }
        }

        public void OnDrag(PointerEventData data)
        {
            if (_source == null || _busy) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_preview.rectTransform, data.position, data.pressEventCamera, out var now);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_preview.rectTransform, data.position - data.delta, data.pressEventCamera, out var before);
            var uv = _preview.uvRect;
            _pan -= Vector2.Scale(now - before, new Vector2(uv.width / PhotoSize.x, uv.height / PhotoSize.y));
            UpdateCrop();
        }

        void UpdateCrop()
        {
            if (_source == null) return;
            float ratio = (float)Width / Height;
            float w = Mathf.Min(1, ratio * _source.height / _source.width) / _zoom.value;
            float h = Mathf.Min(1, _source.width / (ratio * _source.height)) / _zoom.value;
            _pan.x = Mathf.Clamp(_pan.x, w / 2, 1 - w / 2);
            _pan.y = Mathf.Clamp(_pan.y, h / 2, 1 - h / 2);
            _preview.uvRect = new Rect(_pan.x - w / 2, _pan.y - h / 2, w, h);
        }

        void Save()
        {
            if (_source == null || _busy) return;
            string encoded = Convert.ToBase64String(CropPixels(_source, _preview.uvRect));
            SetBusy(true);
            int version = ++_version;
            AuthRuntime.EnsureExists().Client.PutJson<AvatarData, AvatarReply>(Endpoint, new AvatarData { pixels = encoded }, true,
                result => Complete(version, result.IsSuccess && result.Data?.success == true, encoded));
        }

        static byte[] CropPixels(Texture2D source, Rect uv)
        {
            var crop = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            var colors = new Color[Width * Height];
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    var pixel = source.GetPixelBilinear(uv.x + (x + .5f) / Width * uv.width, uv.y + (y + .5f) / Height * uv.height);
                    colors[y * Width + x] = Color.Lerp(new Color(.07f, .07f, .07f), pixel, pixel.a);
                }
            crop.SetPixels(colors); crop.Apply();
            byte[] pixels = crop.GetRawTextureData();
            if (Application.isPlaying) Destroy(crop); else DestroyImmediate(crop);
            return pixels;
        }

        void ResetPhoto()
        {
            if (_busy) return;
            SetBusy(true);
            int version = ++_version;
            AuthRuntime.EnsureExists().Client.DeleteJson<AvatarReply>(Endpoint,
                result => Complete(version, result.IsSuccess && result.Data?.success == true, null));
        }

        void Complete(int version, bool success, string pixels)
        {
            if (this == null || version != _version) return;
            SetBusy(false);
            if (success) { Apply(pixels); _dialog.gameObject.SetActive(false); }
            else _message.text = GameLanguage.Choose("Không lưu được ảnh. Vui lòng thử lại.", "Could not save photo. Please try again.");
        }

        void SetBusy(bool busy)
        {
            _busy = busy;
            _choose.interactable = _reset.interactable = !busy;
            _close.interactable = !busy;
            _save.interactable = !busy && _source != null;
            _save.GetComponentInChildren<Text>().color = _save.interactable ? IndustrialUiStyle.Accent : IndustrialUiStyle.Muted;
            _zoom.interactable = !busy;
            if (busy) _message.text = GameLanguage.Choose("ĐANG LƯU...", "SAVING...");
        }

        Button ActionButton(string text, float x, float y, UnityEngine.Events.UnityAction action)
        {
            var rect = Node(text, _dialog, new Vector2(x, y), new Vector2(196, 44));
            rect.gameObject.AddComponent<Image>().color = new Color(.22f, .06f, .07f);
            var outline = rect.gameObject.AddComponent<Outline>(); outline.effectColor = new Color(.65f, .6f, .4f);
            var label = Node("Label", rect, Vector2.zero, rect.sizeDelta).gameObject.AddComponent<Text>();
            Style(label, 18); label.text = text;
            var button = rect.gameObject.AddComponent<Button>(); button.onClick.AddListener(action);
            IndustrialUiStyle.ActionButton(button, text == GameLanguage.Choose("LƯU", "SAVE"));
            UiTypographyTheme.Apply(label, true);
            return button;
        }
        Text Label(string name, Vector2 position, Vector2 size, int fontSize)
        {
            var label = Node(name, _dialog, position, size).gameObject.AddComponent<Text>();
            Style(label, fontSize); return label;
        }
        void Style(Text text, int fontSize)
        {
            text.font = transform.parent.GetComponentInChildren<Text>(true).font;
            text.fontSize = fontSize; text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(.83f, .80f, .67f); text.raycastTarget = false;
        }
        static RectTransform Node(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchoredPosition = position; rect.sizeDelta = size;
            return rect;
        }

        // Check dimensions before Unity allocates decoded pixels (including small compressed bombs).
        public static bool ValidImageSize(byte[] bytes)
        {
            int w = 0, h = 0;
            if (bytes.Length >= 24 && bytes[0] == 137 && bytes[1] == 80 && bytes[2] == 78 && bytes[3] == 71)
            {
                w = (bytes[16] << 24) | (bytes[17] << 16) | (bytes[18] << 8) | bytes[19];
                h = (bytes[20] << 24) | (bytes[21] << 16) | (bytes[22] << 8) | bytes[23];
            }
            else if (bytes.Length > 4 && bytes[0] == 255 && bytes[1] == 216)
            {
                for (int i = 2; i + 8 < bytes.Length;)
                {
                    if (bytes[i++] != 255) break;
                    while (i < bytes.Length && bytes[i] == 255) i++;
                    if (i + 2 >= bytes.Length) break;
                    int marker = bytes[i++];
                    if (marker == 217 || marker == 218) break;
                    int length = bytes[i] * 256 + bytes[i + 1];
                    if (length < 2 || i + length > bytes.Length) break;
                    if (marker >= 192 && marker <= 207 && marker != 196 && marker != 200 && marker != 204)
                    { h = bytes[i + 3] * 256 + bytes[i + 4]; w = bytes[i + 5] * 256 + bytes[i + 6]; break; }
                    i += length;
                }
            }
            return w > 0 && h > 0 && w <= 4096 && h <= 4096;
        }

        static string PickFile()
        {
#if UNITY_EDITOR
            return UnityEditor.EditorUtility.OpenFilePanelWithFilters("PNG / JPG", "", new[] { "Images", "png,jpg,jpeg" });
#elif UNITY_STANDALONE_WIN
            IntPtr buffer = Marshal.AllocHGlobal(32768 * 2);
            try
            {
                Marshal.WriteInt16(buffer, 0);
                var file = new OpenFile { size = Marshal.SizeOf<OpenFile>(), owner = GetActiveWindow(), file = buffer, maxFile = 32768,
                    filter = "Images (PNG/JPG)\0*.png;*.jpg;*.jpeg\0\0", title = "Choose profile photo", flags = 0x00081008 };
                return GetOpenFileName(ref file) ? Marshal.PtrToStringUni(buffer) : null;
            }
            finally { Marshal.FreeHGlobal(buffer); }
#else
            throw new NotSupportedException();
#endif
        }
#if UNITY_STANDALONE_WIN
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct OpenFile
        {
            public int size; public IntPtr owner, instance;
            public string filter, customFilter; public int maxCustomFilter, filterIndex;
            public IntPtr file; public int maxFile; public string fileTitle; public int maxFileTitle;
            public string initialDirectory, title; public int flags; public short fileOffset, fileExtension;
            public string defaultExtension; public IntPtr customData, hook; public string template;
            public IntPtr reserved; public int reserved2, flagsEx;
        }
        [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetOpenFileName(ref OpenFile file);
        [DllImport("user32.dll")] static extern IntPtr GetActiveWindow();
#endif
        void OnDisable()
        {
            ++_version;
            if (_dialog != null) { SetBusy(false); _dialog.gameObject.SetActive(false); }
        }
        void OnDestroy()
        {
            if (_source != null) Destroy(_source);
            if (_saved != null) Destroy(_saved);
            if (_frameSprite != null)
            {
                if (Application.isPlaying) Destroy(_frameSprite);
                else DestroyImmediate(_frameSprite);
            }
        }
    }
}
