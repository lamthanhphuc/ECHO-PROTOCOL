using EchoProtocol.Networking;
using EchoProtocol.Settings;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace EchoProtocol.UI.MainMenu
{
    [DefaultExecutionOrder(200)]
    public sealed class TeamToolShopGuide : MonoBehaviour
    {
        private RectTransform _root;
        private GameObject _help, _overlay;
        private Text _title, _body;
        private Text _selectedName;
        private Text _heading;
        private Font _font;
        private int _toolId;
        private GameLocale _locale;
        private static readonly Color Gold = IndustrialUiStyle.Accent;
        public void Configure(Text selectedName, int toolId)
        {
            _toolId = toolId;
            _selectedName = selectedName;
            _heading = selectedName != null ? selectedName.transform.parent.parent.Find("SectionTitle")?.GetComponent<Text>() : null;
            if (_root == null && selectedName != null) Build((RectTransform)selectedName.transform.parent, selectedName.font);
            if (_help != null) _help.SetActive(toolId != 0 && !StoreReferencePresentation.IsAwaitingSelection(transform));
            if (_overlay != null) _overlay.SetActive(false);
        }
        private RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
        {
            var rect = new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent,false);rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);
            rect.sizeDelta=size;rect.anchoredPosition=position;return rect;
        }
        private Text Label(string name, Transform parent, string value, Vector2 size, Vector2 position, int fontSize)
        {
            var rect=Rect(name,parent,size,position);var text=rect.gameObject.AddComponent<Text>();
            text.font=UiTypographyTheme.BodyLegacy;text.fontSize=fontSize;
            text.color=name.ToLowerInvariant().Contains("title") || name.ToLowerInvariant().Contains("heading")
                ? Gold : IndustrialUiStyle.Text;
            text.text=value;text.raycastTarget=false;
            text.alignment=TextAnchor.UpperLeft;return text;
        }
        private Button Button(string name, Transform parent, string value, Vector2 size, Vector2 position)
        {
            var rect=Rect(name,parent,size,position);var image=rect.gameObject.AddComponent<Image>();
            image.color=new Color(.11f,.10f,.08f,.98f);image.sprite=Resources.Load<Sprite>("UI/LobbyReference/Frame");image.type=Image.Type.Sliced;
            var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=image;
            var text=Label("Caption",rect,value,size-Vector2.one*8,Vector2.zero,26);text.alignment=TextAnchor.MiddleCenter;
            EchoProtocol.Audio.GameAudioRuntime.RegisterButton(button);return button;
        }
        private void Build(RectTransform parent, Font font)
        {
            _root=parent;_font=font;
            var help=Button("ToolHelpButton",parent,"?",new Vector2(48,48),Vector2.zero);
            help.GetComponent<Image>().color=Color.white;
            var helpCaption=help.GetComponentInChildren<Text>();helpCaption.fontSize=34;helpCaption.fontStyle=FontStyle.Normal;
            helpCaption.rectTransform.sizeDelta=new Vector2(42,42);
            helpCaption.rectTransform.anchoredPosition=new Vector2(0,2);
            var rect=(RectTransform)help.transform;rect.anchorMin=rect.anchorMax=new Vector2(0,1);
            _help=help.gameObject;help.onClick.AddListener(()=>{RefreshContent();_overlay.SetActive(true);_overlay.transform.SetAsLastSibling();});
            var overlay=Rect("ToolGuideOverlay",parent,Vector2.zero,Vector2.zero);
            overlay.anchorMin=Vector2.zero;overlay.anchorMax=Vector2.one;overlay.offsetMin=overlay.offsetMax=Vector2.zero;
            overlay.gameObject.AddComponent<Image>().color=new Color(0,0,0,.72f);_overlay=overlay.gameObject;
            var panel=Rect("ToolGuideCard",overlay,new Vector2(760,620),Vector2.zero);
            var bg=panel.gameObject.AddComponent<Image>();bg.sprite=Resources.Load<Sprite>("UI/LobbyReference/Frame");bg.type=Image.Type.Sliced;bg.color=IndustrialUiStyle.Panel;
            _title=Label("GuideTitle",panel,"",new Vector2(610,52),new Vector2(-30,254),32);
            _body=Label("GuideBody",panel,"",new Vector2(672,444),new Vector2(0,-6),21);
            var close=Button("CloseGuide",panel,"×",new Vector2(44,44),new Vector2(331,262));close.onClick.AddListener(Hide);
            var done=Button("GuideDone",panel,GameLanguage.Choose("Đã hiểu","Got it"),new Vector2(220,48),new Vector2(0,-260));done.onClick.AddListener(Hide);
            _overlay.SetActive(false);
        }
        private void Hide()=>_overlay.SetActive(false);
        private readonly Vector3[] _nameCorners=new Vector3[4];
        private void LateUpdate()
        {
            if(_help==null || _selectedName==null || _root==null)return;
            bool show=_toolId!=0 && !StoreReferencePresentation.IsAwaitingSelection(transform)
                && _selectedName.gameObject.activeInHierarchy;
            if(_help.activeSelf!=show)_help.SetActive(show);
            if(!show){if(_overlay.activeSelf)Hide();return;}
            var title = _heading != null ? _heading : _selectedName;
            float size = Mathf.Clamp(title.fontSize,48f,64f);
            var helpRect=(RectTransform)_help.transform;
            helpRect.sizeDelta=Vector2.one*size;
            var caption=_help.GetComponentInChildren<Text>();
            caption.fontSize=Mathf.RoundToInt(size*.8f);
            caption.rectTransform.sizeDelta=Vector2.one*(size-6f);
            title.rectTransform.GetWorldCorners(_nameCorners);
            Vector3 topLeft=_root.InverseTransformPoint(_nameCorners[1]);
            Vector3 bottomLeft=_root.InverseTransformPoint(_nameCorners[0]);
            float width=Mathf.Min(title.preferredWidth,title.rectTransform.rect.width)
                * title.rectTransform.localScale.x;
            // Follow the actual rendered title width instead of a fixed screen position.
            _help.transform.localPosition=new Vector3(topLeft.x+width+16f+size*.5f,(topLeft.y+bottomLeft.y)*.5f,topLeft.z);
        }
        private void OnDisable(){if(_overlay!=null)_overlay.SetActive(false);}
        private void Update()
        {
            if(_overlay==null || !_overlay.activeInHierarchy)return;
            if(Keyboard.current!=null && Keyboard.current.escapeKey.wasPressedThisFrame){Hide();return;}
            if(_locale!=GameLanguage.Current)RefreshContent();
        }
        private void RefreshContent()
        {
            _locale=GameLanguage.Current;
            string title,effect,use,note;
            string interact=GameplayInputSettings.GetKeyLabel(GameplayAction.Interact);
            switch(_toolId) {
                case 1:
                    title=GameLanguage.TeamToolName(_toolId);
                    effect=GameLanguage.Choose("Tìm nguồn năng lượng gần bạn hoặc theo dõi Stalker trên radar.","Locate nearby energy sources or track the Stalker on radar.");
                    use=GameLanguage.Choose("Chuột trái: quét. Chuột phải: đổi chế độ Lõi / Stalker. Bạn nằm ở giữa radar.","Left click: scan. Right click: switch Core / Stalker modes. You are at the radar centre.");
                    note=GameLanguage.Choose("Chế độ Stalker chỉ phát hiện Stalker đang di chuyển. Đừng xem radar trống là dấu hiệu an toàn.","Stalker mode only detects a moving Stalker. An empty radar does not guarantee safety.");break;
                case 2:
                    title=GameLanguage.TeamToolName(_toolId);
                    effect=GameLanguage.Choose("Thu hút Stalker đến vị trí đặt thiết bị, tạo cơ hội cho đội di chuyển.","Draw the Stalker to the device location, giving your team a chance to move.");
                    use=GameLanguage.Choose("Chuột trái: đặt tại vị trí xem trước hợp lệ. Rời khu vực sau khi đặt.","Left click: place at the valid preview location. Leave the area after deployment.");
                    note=GameLanguage.Choose("Đèn đỏ nhấp nháy báo thiết bị đang thu hút Stalker. Không đứng chờ cạnh thiết bị.","A flashing red light indicates that the device is attracting the Stalker. Do not wait beside it.");break;
                case 4:
                    title=GameLanguage.TeamToolName(_toolId);
                    effect=GameLanguage.Choose("Chặn lối qua cửa đã bị phá để trì hoãn Stalker.","Block a broken doorway to delay the Stalker.");
                    use=GameLanguage.Choose($"Nhắm vào cửa đã bị phá, nhấn {interact} hoặc chuột trái để gắn ván.",$"Aim at a broken door and press {interact} or left click to attach the plank.");
                    note=GameLanguage.Choose("Chỉ dùng với cửa đã bị phá. Stalker có thể phá ván; đây là cách câu giờ, không phải nơi trú ẩn lâu dài.","Only works on broken doors. The Stalker can break the plank; it buys time rather than providing permanent shelter.");break;
                case 6:
                    title=GameLanguage.TeamToolName(_toolId);
                    effect=GameLanguage.Choose($"Hỗ trợ người mang lõi trong bán kính {CoreStabilizerRules.SupportRadius:0.#} m, tăng tốc độ mang lõi.",$"Support core carriers within {CoreStabilizerRules.SupportRadius:0.#} m, increasing their carrying speed.");
                    use=GameLanguage.Choose($"Chuột trái: kích hoạt trong {CoreStabilizerRules.DurationSeconds:0.#} giây. Đi cùng người mang lõi để giữ họ trong vùng hỗ trợ.",$"Left click: activate for {CoreStabilizerRules.DurationSeconds:0.#} seconds. Stay close to the core carrier to keep them in range.");
                    note=GameLanguage.Choose($"Hồi chiêu {CoreStabilizerRules.CooldownSeconds:0.#} giây. Không có tác dụng khi người mang lõi ở ngoài vùng hỗ trợ.",$"Cooldown: {CoreStabilizerRules.CooldownSeconds:0.#} seconds. Carriers outside the support area receive no benefit.");break;
                default:return;
            }
            _title.text=title;
            _body.text=GameLanguage.Choose("TÁC DỤNG","EFFECT")+"\n"+effect+"\n\n"+GameLanguage.Choose("CÁCH DÙNG","HOW TO USE")+"\n"+use+"\n\n"+GameLanguage.Choose("LƯU Ý","NOTE")+"\n"+note;
        }
    }
}
