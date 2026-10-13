using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace EchoProtocol.UI.MainMenu
{
    // Shared visual assets and persistent catalog selection. Layout remains authored in the prefab.
    public sealed class StoreReferencePresentation : MonoBehaviour
    {
        public bool AwaitingSelection { get; private set; }
        public static bool IsAwaitingSelection(Transform root) => root.GetComponent<StoreReferencePresentation>()?.AwaitingSelection == true;
        public static void BeginSelection(Transform root)
        {
            var presentation=root.GetComponent<StoreReferencePresentation>();
            if(presentation==null)presentation=root.gameObject.AddComponent<StoreReferencePresentation>();
            presentation.AwaitingSelection=true;
            SetPreviewVisibility(root,false);
            root.GetComponent<StoreLivePreviewRenderer>()?.Clear();
        }
        public static void ChooseModel(Transform root)
        {
            var presentation=root.GetComponent<StoreReferencePresentation>();
            if(presentation!=null)presentation.AwaitingSelection=false;
            SetPreviewVisibility(root,true);
        }
        private static void SetPreviewVisibility(Transform root,bool visible)
        {
            foreach(var path in new[]{"Window/Content/CharacterContent/CharacterPreviewBox/CharacterPreview","Window/Content/PetContent/PetPreviewBox/PetPreview","Window/Content/TeamTools/SelectedPreviewBox/SelectedPreview"}) {
                var preview=root.Find(path)?.GetComponent<RawImage>();
                if(preview==null)continue;
                preview.enabled=visible;
                if(!visible){preview.texture=null;preview.color=Color.clear;}
            }
        }
        private static Sprite _background, _card, _selected, _buy, _navigation;
        private static Font _serif;
        private static readonly Color Gold = new Color(0.76f, 0.72f, 0.53f);
        public static void Apply(Transform root)
        {
            EnsureAssets();
            if(root.GetComponent<StoreReferencePresentation>()==null)root.gameObject.AddComponent<StoreReferencePresentation>();
            foreach(var text in root.GetComponentsInChildren<Text>(true))
            {
                text.font=_serif; text.fontStyle=FontStyle.Normal; text.raycastTarget=false;
                if(text.name=="SectionTitle"){text.gameObject.SetActive(true);text.fontSize=56;text.resizeTextForBestFit=false;}
                if(text.name=="SelectedName" || text.name=="SelectedCharacterName"){text.fontSize=48;text.resizeTextForBestFit=false;}
                if(text.name=="Caption" && text.transform.parent.name.Contains("BuyButton")){text.fontSize=30;text.resizeTextForBestFit=false;}
                if(text.name=="SelectedName" || text.name=="SelectedCharacterName")text.color=Gold;
                else if(text.name.Contains("Price")){text.fontSize=24;text.color=Gold;text.alignment=TextAnchor.MiddleLeft;}
                else if(text.name=="SectionTitle")text.color=new Color(0.9f,0.89f,0.86f);
                if(text.name=="Title" && text.transform.parent.name.EndsWith("VariantPanel")) {
                    text.text=EchoProtocol.Settings.GameLanguage.Choose("Màu sắc","Skins");text.fontSize=18;text.color=Gold;
                }
            }
            var window=root.Find("Window");
            if(window==null)return;
            EnsureUtilities(root,window);
            var tools=window.Find("Content/TeamTools");
            if(tools!=null && tools.Find("ToolPreviewGlow")==null) {
                var glow=new GameObject("ToolPreviewGlow",typeof(RectTransform),typeof(Image)).GetComponent<RectTransform>();
                glow.SetParent(tools,false);Place(glow,120,320,1000,650);glow.SetAsFirstSibling();
                var graphic=glow.GetComponent<Image>();graphic.raycastTarget=false;
                graphic.sprite=TextureSprite(256,256,(x,y)=>{
                    float distance=Vector2.Distance(new Vector2(x/255f,y/255f),new Vector2(0.5f,0.5f))*2f;
                    return new Color(0.22f,0.11f,0.6f,Mathf.Pow(Mathf.Clamp01(1f-distance),2f)*0.28f);
                });
            }
            var image=window.GetComponent<Image>();
            image.sprite=_background;image.color=Color.white;image.type=Image.Type.Simple;
            foreach(var node in window.GetComponentsInChildren<Transform>(true))
            {
                var graphic=node.GetComponent<Image>();
                if(graphic!=null && (node.name=="Content" || node.name=="Categories" || node.name.EndsWith("PreviewBox") || node.name.EndsWith("VariantPanel") || node.name=="CharacterContent" || node.name=="TeamTools" || node.name=="PetContent"))
                    graphic.color=Color.clear;
                if(node.name.EndsWith("BuyButton") || node.name=="BuyButton")StyleButton(node.GetComponent<Button>(),_buy);
                if(node.name=="CloseButton"){
                    StyleButton(node.GetComponent<Button>(),_navigation);
                    var caption=node.GetComponentInChildren<Text>();
                    if(caption!=null)caption.text=EchoProtocol.Settings.GameLanguage.Choose("Quay lại","Back");
                }
                if(graphic!=null && node.name=="CreditsBox")graphic.color=Color.clear;
            }
            string[] names={"CharacterButton","TeamToolButton","PetButton"};
            for(int i=0;i<names.Length;i++)
            {
                var node=window.Find("Categories/"+names[i]);
                if(node==null)continue;
                StyleButton(node.GetComponent<Button>(),_navigation);
                var icon=node.Find("StoreCategoryIcon")?.GetComponent<Image>();
                if(icon!=null){icon.sprite=MakeIcon(i);icon.color=Gold;icon.preserveAspect=true;}
            }
        }
        private void OnEnable() => EchoProtocol.Settings.GameLanguage.Changed += RefreshLanguage;
        private void OnDisable() => EchoProtocol.Settings.GameLanguage.Changed -= RefreshLanguage;
        private void RefreshLanguage()
        {
            foreach(var controller in new MonoBehaviour[]{GetComponent<CharacterShopController>(),GetComponent<PetShopController>(),GetComponent<TeamToolShopPanel>()})
                if(controller!=null)controller.SendMessage("SetBuyState",SendMessageOptions.DontRequireReceiver);
            var close=transform.Find("Window/CloseButton")?.GetComponentInChildren<Text>();
            if(close!=null)close.text=EchoProtocol.Settings.GameLanguage.Choose("Quay lại","Back");
        }
        private void OnDestroy()
        {
            foreach(var label in GetComponentsInChildren<Text>(true))Names.Remove(label);
        }
        private Transform _headerContent;
        private Text _headerName, _headerTitle;
        private string _headerSource, _headerOutput;
        private static readonly System.Collections.Generic.Dictionary<Text,string> Names = new System.Collections.Generic.Dictionary<Text,string>();
        public static void SetName(Text label,string value) { if(label==null)return; Names[label]=value; label.text=value; }
        private RectTransform _buyRect, _priceRect;


        private Vector2 _lastLayoutSize;
        private void FitCatalogs(Transform content)
        {
            var window=transform.Find("Window") as RectTransform;
            if(window==null || window.rect.size==_lastLayoutSize)return;
            _lastLayoutSize=window.rect.size;
            foreach(var name in new[]{"TeamTools/ItemGrid","PetContent/PetGrid","CharacterContent/CharacterGrid"})
            {
                var rect=content.Find(name) as RectTransform;
                var grid=rect!=null ? rect.GetComponent<GridLayoutGroup>() : null;
                if(grid==null)continue;
                float width=(rect.rect.width-grid.padding.left-grid.padding.right-grid.spacing.x*2f)/3f;
                grid.cellSize=Vector2.one*Mathf.Clamp(width,60f,170f);
                var contentGrid=rect.GetComponent<ScrollRect>()?.content?.GetComponent<GridLayoutGroup>();
                if(contentGrid!=null)contentGrid.cellSize=grid.cellSize;
            }
        }
        private void LateUpdate()
        {
            var content=transform.Find("Window/Content");
            if(content==null)return;
            FitCatalogs(content);
            var credits=transform.Find("Window/CreditsBox");
            if(credits!=null)foreach(var label in credits.GetComponentsInChildren<Text>(true))if(label.name!="Amount")label.text=EchoProtocol.Settings.GameLanguage.Choose("Tín dụng","Credits");
            Transform visible=null;
            foreach(Transform child in content)
                if(child.gameObject.activeInHierarchy && (child.name=="PetContent" || child.name=="CharacterContent" || child.name=="TeamTools"))visible=child;
            if(visible==null)return;
            if(_headerContent!=visible) {
                _headerContent=visible;_headerSource=null;_headerOutput=null;
                bool character=visible.name=="CharacterContent";
                string button=character ? "CharacterBuyButton" : visible.name=="PetContent" ? "PetBuyButton" : "BuyButton";
                _buyRect=visible.Find(button) as RectTransform;
                _priceRect=visible.Find(character ? "SelectedCharacterPrice" : "SelectedPrice") as RectTransform;
                _headerName=visible.Find(visible.name=="CharacterContent" ? "SelectedCharacterName" : "SelectedName")?.GetComponent<Text>();
                _headerTitle=content.Find("SectionTitle")?.GetComponent<Text>();
            }
            if(_headerName==null || _headerTitle==null)return;
            _headerName.gameObject.SetActive(!AwaitingSelection);
            if(_buyRect!=null)_buyRect.gameObject.SetActive(!AwaitingSelection);
            if(_priceRect!=null)_priceRect.gameObject.SetActive(!AwaitingSelection);
            if(AwaitingSelection) {
                _headerTitle.text=EchoProtocol.Settings.GameLanguage.Choose(visible.name=="CharacterContent" ? "Nhân vật" : visible.name=="PetContent" ? "Thú cưng" : "Công cụ",visible.name=="CharacterContent" ? "Character" : visible.name=="PetContent" ? "Pet" : "Team Tool");
                foreach(string name in new[]{"CharacterVariantPanel","PetVariantPanel"})transform.Find("Window/"+name)?.gameObject.SetActive(false);
                return;
            }

            if(!Names.TryGetValue(_headerName,out _headerSource))_headerSource=_headerName.text;
            string[] parts=_headerSource.Split(new[]{" // "},System.StringSplitOptions.None);
            bool pet=visible.name=="PetContent";
            string Display(string value) {
                switch(value.Trim().ToLowerInvariant()) {
                    case "default":return EchoProtocol.Settings.GameLanguage.Choose("Mặc định","Default");
                    case "red":return EchoProtocol.Settings.GameLanguage.Choose("Đỏ","Red");
                    case "green":return EchoProtocol.Settings.GameLanguage.Choose("Xanh lá","Green");
                    case "orange":return EchoProtocol.Settings.GameLanguage.Choose("Cam","Orange");
                    case "purple":return EchoProtocol.Settings.GameLanguage.Choose("Tím","Purple");
                    case "black":return EchoProtocol.Settings.GameLanguage.Choose("Đen","Black");
                    case "blue":return EchoProtocol.Settings.GameLanguage.Choose("Xanh dương","Blue");
                }
                return System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(value.ToLowerInvariant());
            }
            _headerTitle.text=pet ? EchoProtocol.Settings.GameLanguage.Choose("Thú cưng","Pet") : Display(parts[0]);
            _headerOutput=pet ? Display(parts[0]) : parts.Length>1 ? Display(parts[1]) : EchoProtocol.Settings.GameLanguage.Choose("Mặc định","Default");
            _headerName.text=_headerOutput;
            Place(_buyRect,74,206,226,68);
            Place(_priceRect,320,223,400,34);
            foreach(string panelName in new[]{"CharacterVariantPanel","PetVariantPanel"}) {
                var panel=transform.Find("Window/"+panelName) as RectTransform;
                if(panel==null)continue;
                Place(panel,panelName=="CharacterVariantPanel" ? 245 : 165,925,870,150);
                var title=panel.Find("Title");if(title!=null)title.gameObject.SetActive(false);
                var grid=panel.Find("Grid")?.GetComponent<GridLayoutGroup>();
                if(grid!=null){
                    var gridRect=grid.GetComponent<RectTransform>();
                    gridRect.anchorMin=Vector2.zero;gridRect.anchorMax=Vector2.one;
                    gridRect.offsetMin=Vector2.zero;gridRect.offsetMax=Vector2.zero;
                    grid.cellSize=Vector2.one*130f;grid.spacing=new Vector2(18f,0f);
                    grid.childAlignment=TextAnchor.UpperCenter;grid.constraint=GridLayoutGroup.Constraint.FixedRowCount;grid.constraintCount=1;
                }
            }

        }
        private static void Place(RectTransform rect,float x,float y,float width,float height)
        {
            if(rect==null)return;
            rect.anchorMin=new Vector2(x/1920f,1f-(y+height)/1080f);
            rect.anchorMax=new Vector2((x+width)/1920f,1f-y/1080f);
            rect.anchoredPosition=Vector2.zero;rect.sizeDelta=Vector2.zero;
        }

        public static void SelectCategory(Button button,bool selected)
        {
            if(button==null)return;
            var accent=button.transform.Find("Accent");if(accent!=null)accent.gameObject.SetActive(selected);
            var image=button.targetGraphic as Image;if(image!=null)image.color=Color.white;
            var icon=button.transform.Find("StoreCategoryIcon")?.GetComponent<Image>();
            if(icon!=null)icon.color=selected ? Gold : new Color(0.43f,0.43f,0.38f);
            var outline=button.GetComponent<Outline>();
            if(outline!=null){outline.effectColor=selected ? Gold : Color.clear;outline.effectDistance=new Vector2(1,1);}
        }

        private static void EnsureUtilities(Transform root,Transform window)
        {
            if(window.Find("StoreUtilities")!=null)return;
            var row=new GameObject("StoreUtilities",typeof(RectTransform)).GetComponent<RectTransform>();row.SetParent(window,false);
            Place(row,1668,26,202,44);
            for(int i=0;i<4;i++)
            {
                int action=i;
                var rect=new GameObject("Utility"+i,typeof(RectTransform),typeof(Image),typeof(Button)).GetComponent<RectTransform>();rect.SetParent(row,false);
                rect.anchorMin=new Vector2(i*54f/202f,0);rect.anchorMax=new Vector2((i*54f+40f)/202f,1);rect.offsetMin=rect.offsetMax=Vector2.zero;
                var button=rect.GetComponent<Button>();button.targetGraphic=rect.GetComponent<Image>();StyleButton(button,_navigation);
                var icon=new GameObject("Icon",typeof(RectTransform),typeof(Image)).GetComponent<RectTransform>();icon.SetParent(rect,false);
                icon.anchorMin=new Vector2(0.12f,0.12f);icon.anchorMax=new Vector2(0.88f,0.88f);icon.offsetMin=icon.offsetMax=Vector2.zero;
                icon.GetComponent<Image>().sprite=UtilityIcon(i);icon.GetComponent<Image>().color=Gold;icon.GetComponent<Image>().raycastTarget=false;
                EchoProtocol.Audio.GameAudioRuntime.RegisterButton(button);
                if(i==1){button.interactable=false;continue;}
                button.onClick.AddListener(()=>{
                    if(action==0) {
                        EchoProtocol.Voice.VoiceManager.EnsureExists();
                        EchoProtocol.Voice.VoiceManager.Instance.GetComponent<EchoProtocol.Voice.VoiceSettingsPanel>().OpenFromMainMenu();
                    } else if(action==2)EchoProtocol.Settings.GameLanguage.Set(EchoProtocol.Settings.GameLanguage.Current==EchoProtocol.Settings.GameLocale.English
                        ? EchoProtocol.Settings.GameLocale.Vietnamese : EchoProtocol.Settings.GameLocale.English);
                    else root.GetComponent<TeamToolShopPanel>()?.Close();
                });
            }
        }
        private static readonly Sprite[] UtilityIcons=new Sprite[4];
        private static Sprite UtilityIcon(int kind)
        {
            if(UtilityIcons[kind]!=null)return UtilityIcons[kind];
            UtilityIcons[kind]=TextureSprite(64,64,(x,y)=>{
                float px=(x-32)/28f,py=(y-32)/28f,r=Mathf.Sqrt(px*px+py*py);bool fill=false;
                if(kind==0)fill=r>0.3f && r<(0.68f+0.13f*Mathf.Cos(Mathf.Atan2(py,px)*8f));
                else if(kind==1)fill=Mathf.Abs(px)<0.65f && py<0.36f && py> -0.7f && (Mathf.Abs(px)>0.5f || py< -0.52f || py>0.22f)
                    || r>0.34f && r<0.46f && py>0.2f;
                else if(kind==2)fill=Mathf.Abs(r-0.73f)<0.07f || r<0.73f && (Mathf.Abs(py)<0.045f || Mathf.Abs(px)<0.045f || Mathf.Abs(px*px*2f+py*py-0.53f)<0.07f);
                else fill=Mathf.Abs(px)<0.55f && Mathf.Abs(py)<0.78f && (Mathf.Abs(px)>0.44f || Mathf.Abs(py)>0.66f)
                    || Mathf.Abs(py)<0.07f && px<0.38f && px> -0.9f || Mathf.Abs(py-Mathf.Abs(px+0.9f))<0.07f && px< -0.55f;
                return fill ? Color.white : Color.clear;
            });return UtilityIcons[kind];
        }

        private static void StyleButton(Button button,Sprite sprite)
        {
            if(button==null)return;
            var image=button.targetGraphic as Image;
            if(image!=null){image.sprite=sprite;image.color=Color.white;image.type=Image.Type.Simple;}
            var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1.15f,1.12f,1.03f);
            colors.pressedColor=new Color(0.75f,0.72f,0.66f);colors.selectedColor=Color.white;colors.disabledColor=new Color(0.4f,0.4f,0.4f);button.colors=colors;
        }
        public static bool ClearCatalog(Transform parent)
        {
            var cells=parent!=null ? parent.Find("CatalogCells") : null;
            if(cells==null)return false;
            // Keep the ScrollRect content alive. Deferred destruction must never
            // remove a container that receives the replacement cards this frame.
            for(int i=cells.childCount-1;i>=0;i--) {
                var card=cells.GetChild(i).gameObject;
                card.SetActive(false);
                if(Application.isPlaying)Object.Destroy(card);else Object.DestroyImmediate(card);
            }
            var scroll=parent.GetComponent<ScrollRect>();
            if(scroll!=null){scroll.StopMovement();scroll.content.anchoredPosition=Vector2.zero;}
            return true;
        }
        public static Transform CatalogParent(Transform parent)
        {
            foreach(Transform child in parent)
                if(child.name=="CatalogCells" && child.gameObject.activeSelf)return child;
            var rect=parent as RectTransform;
            var authored=parent.GetComponent<GridLayoutGroup>();
            if(rect==null || authored==null)return parent;
            authored.enabled=false;
            if(parent.GetComponent<RectMask2D>()==null)parent.gameObject.AddComponent<RectMask2D>();
            var cells=new GameObject("CatalogCells",typeof(RectTransform),typeof(GridLayoutGroup),typeof(ContentSizeFitter)).GetComponent<RectTransform>();
            cells.SetParent(parent,false);cells.anchorMin=new Vector2(0,1);cells.anchorMax=Vector2.one;cells.pivot=new Vector2(0.5f,1);
            cells.anchoredPosition=Vector2.zero;cells.sizeDelta=new Vector2(0,0);
            var layout=cells.GetComponent<GridLayoutGroup>();layout.cellSize=authored.cellSize;layout.spacing=authored.spacing;
            layout.padding=authored.padding;layout.constraint=GridLayoutGroup.Constraint.FixedColumnCount;layout.constraintCount=3;
            cells.GetComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            var scroll=parent.GetComponent<ScrollRect>() ?? parent.gameObject.AddComponent<ScrollRect>();
            scroll.content=cells;scroll.viewport=rect;scroll.horizontal=false;scroll.vertical=true;scroll.scrollSensitivity=35;scroll.movementType=ScrollRect.MovementType.Clamped;
            string name=parent.name+"Scrollbar";
            var barRect=parent.parent.Find(name) as RectTransform;
            if(barRect==null)
            {
                barRect=new GameObject(name,typeof(RectTransform),typeof(Image),typeof(Scrollbar)).GetComponent<RectTransform>();
                barRect.SetParent(parent.parent,false);
                var fill=new GameObject("Handle",typeof(RectTransform),typeof(Image)).GetComponent<RectTransform>();fill.SetParent(barRect,false);
                fill.anchorMin=Vector2.zero;fill.anchorMax=Vector2.one;fill.offsetMin=fill.offsetMax=Vector2.zero;
                fill.GetComponent<Image>().color=new Color(0.3f,0.29f,0.27f);
                var bar=barRect.GetComponent<Scrollbar>();bar.handleRect=fill;bar.targetGraphic=fill.GetComponent<Image>();bar.direction=Scrollbar.Direction.BottomToTop;
            }
            barRect.anchorMin=new Vector2(rect.anchorMax.x+0.006f,rect.anchorMin.y);
            barRect.anchorMax=new Vector2(rect.anchorMax.x+0.014f,rect.anchorMax.y);barRect.anchoredPosition=Vector2.zero;barRect.sizeDelta=Vector2.zero;
            barRect.GetComponent<Image>().color=new Color(0.08f,0.08f,0.075f);
            scroll.verticalScrollbar=barRect.GetComponent<Scrollbar>();scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
            return cells;
        }

        public static void BindCard(GameObject card)
        {
            EnsureAssets();
            var image=card.GetComponent<Image>();
            if(image!=null){image.sprite=_card;image.color=Color.white;image.type=Image.Type.Simple;}
            var outline=card.GetComponent<Outline>();if(outline!=null)outline.enabled=false;
            var button=card.GetComponent<Button>();button.transition=Selectable.Transition.None;
            var selection=card.AddComponent<StoreReferenceCard>();
            selection.Initialize(_card,_selected);
            button.onClick.AddListener(selection.SelectCard);
            var thumbnail=card.GetComponentInChildren<RawImage>(true);
            if(thumbnail!=null)
            {
                var oldRect=thumbnail.rectTransform;
                var wrapper=new GameObject("ThumbnailInset",typeof(RectTransform)).GetComponent<RectTransform>();
                wrapper.SetParent(card.transform,false);wrapper.anchorMin=oldRect.anchorMin;wrapper.anchorMax=oldRect.anchorMax;
                wrapper.offsetMin=oldRect.offsetMin;wrapper.offsetMax=oldRect.offsetMax;
                oldRect.SetParent(wrapper,false);
                var fitter=thumbnail.gameObject.GetComponent<AspectRatioFitter>() ?? thumbnail.gameObject.AddComponent<AspectRatioFitter>();
                fitter.aspectMode=AspectRatioFitter.AspectMode.FitInParent;
                fitter.aspectRatio=thumbnail.texture!=null ? (float)thumbnail.texture.width/thumbnail.texture.height : 1f;
                thumbnail.raycastTarget=false;
                if(card.name.StartsWith("CharacterOutfit_") || card.name.StartsWith("PetVariant_")) {
                    wrapper.anchorMin=Vector2.zero;wrapper.anchorMax=Vector2.one;
                    wrapper.offsetMin=Vector2.one*4f;wrapper.offsetMax=Vector2.one*-4f;
                }
                // Crop only empty capture margins; keep image pixels and aspect unchanged.
                if(thumbnail.texture!=null && thumbnail.texture.name.StartsWith("character-"))
                    thumbnail.uvRect=new Rect(0.04f,0.04f,0.92f,0.92f);
                switch(thumbnail.texture!=null ? thumbnail.texture.name : "") {
                    case "nightmare":thumbnail.uvRect=new Rect(0.17f,0.15f,0.72f,0.72f);break;
                    case "soul-eater":thumbnail.uvRect=new Rect(0.18f,0.14f,0.68f,0.68f);break;
                    case "usurper":thumbnail.uvRect=new Rect(0.04f,0.10f,0.75f,0.75f);break;
                }
            }

        }
        private static void EnsureAssets()
        {
            if(_background!=null)return;
            _serif=Font.CreateDynamicFontFromOSFont(new[]{"Georgia","Times New Roman","Liberation Serif","DejaVu Serif"},48);
            _background=TextureSprite(960,540,(x,y)=>{
                float nx=x/959f,ny=y/539f;
                float rough=Mathf.PerlinNoise(x*0.035f,y*0.035f)*0.65f+Mathf.PerlinNoise(x*0.14f,y*0.14f)*0.35f;
                float glow=Mathf.Clamp01(1f-Vector2.Distance(new Vector2(nx,ny),new Vector2(0.45f,0.68f))*1.5f);
                float stain=0.35f+rough*0.65f;
                return new Color((0.025f+0.16f*glow)*stain,0.006f+rough*0.014f,0.005f+rough*0.008f,1f);
            });
            _card=Frame(false,false);_selected=Frame(true,false);_buy=Frame(false,true);_navigation=Frame(false,false);
        }
        private static Sprite Frame(bool selected,bool red)
        {
            return TextureSprite(170,170,(x,y)=>{
                int edge=Mathf.Min(Mathf.Min(x,y),Mathf.Min(169-x,169-y));
                float corner=Mathf.Min(Mathf.Min(x+y,169-x+y),Mathf.Min(x+169-y,338-x-y));
                if(corner<5)return Color.clear;
                edge=Mathf.Min(edge,Mathf.FloorToInt((corner-5f)/1.414f));
                float grain=Mathf.PerlinNoise(x*0.33f,y*0.33f);
                grain=Mathf.Clamp01(grain*0.7f+Mathf.PerlinNoise(x*0.07f,y*0.07f)*0.3f);
                if(selected && edge>=2 && edge<=5)return Color.Lerp(new Color(0.58f,0.3f,0.16f),new Color(0.98f,0.94f,0.84f),edge==3 || edge==4 ? 1f : grain);
                if(edge<2)return new Color(0.03f,0.02f,0.016f,1f);
                if(edge<4)return Color.Lerp(new Color(0.12f,0.09f,0.07f),new Color(0.38f,0.35f,0.29f),grain);
                float vignette=Mathf.Clamp01(1f-Vector2.Distance(new Vector2(x,y),new Vector2(85,85))/120f);
                return red ? new Color(0.13f+grain*0.1f,0.009f+grain*0.007f,0.006f,1f)
                    : new Color(0.012f+grain*0.085f*vignette,0.011f+grain*0.075f*vignette,0.01f+grain*0.065f*vignette,1f);
            });
        }
        private static Sprite TextureSprite(int width,int height,System.Func<int,int,Color> pixel)
        {
            var texture=new Texture2D(width,height,TextureFormat.RGBA32,false){name="Store reference visual",hideFlags=HideFlags.DontSave,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            var colors=new Color[width*height];for(int y=0;y<height;y++)for(int x=0;x<width;x++)colors[y*width+x]=pixel(x,y);
            texture.SetPixels(colors);texture.Apply(false,true);
            return Sprite.Create(texture,new Rect(0,0,width,height),new Vector2(0.5f,0.5f),100f);
        }
        private static readonly Sprite[] Icons=new Sprite[3];
        private static Sprite MakeIcon(int kind)
        {
            if(Icons[kind]!=null)return Icons[kind];
            Icons[kind]=TextureSprite(96,96,(x,y)=>{
                float px=(x-48)/40f,py=(y-48)/40f;bool fill=false,cut=false;
                if(kind==0)
                {
                    fill=px*px/0.45f+py*py/0.7f<1f || Mathf.Abs(px)<0.22f && py<0 && py> -0.9f;
                    cut=(Mathf.Pow(px-0.24f,2)+Mathf.Pow(py-0.05f,2)<0.03f)||(Mathf.Pow(px+0.24f,2)+Mathf.Pow(py-0.05f,2)<0.03f)||Mathf.Abs(px)<0.09f && py<0 && py> -0.3f;
                }
                else if(kind==1)
                {
                    float u=px*0.87f-py*0.5f,v=px*0.5f+py*0.87f;
                    fill=Mathf.Abs(u)<0.62f && Mathf.Abs(v)<0.17f || u< -0.3f && u> -0.78f && Mathf.Abs(v)<0.3f;
                    cut=u< -0.59f && Mathf.Abs(v)<0.22f;
                }
                else
                {
                    // Side profile: neck, muzzle, two swept horns and beard.
                    fill=px> -0.65f && px< -0.12f && py> -0.72f && py<0.12f;
                    fill|=Mathf.Pow((px-0.04f)/0.48f,2f)+Mathf.Pow((py-0.16f)/0.36f,2f)<1f;
                    fill|=px>0.12f && px<0.75f && py> -0.1f && py<0.18f;
                    fill|=px> -0.05f && px<0.18f && py> -0.5f && py< -0.1f;
                    fill|=py>0.35f && py<0.86f && Mathf.Abs(px-(-0.25f-(py-0.35f)*0.8f))<0.065f;
                    fill|=py>0.35f && py<0.74f && Mathf.Abs(px-(0.05f-(py-0.35f)*0.8f))<0.055f;
                    cut=Mathf.Pow(px-0.3f,2f)+Mathf.Pow(py-0.18f,2f)<0.007f;

                }
                return fill && !cut ? Color.white : Color.clear;
            });return Icons[kind];
        }
    }
    public sealed class StoreReferenceCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler
    {
        private Sprite _normal,_selected;
        private bool _chosen;
        public void Initialize(Sprite normal,Sprite selected){_normal=normal;_selected=selected;}
        public void SelectCard()
        {
            foreach(var sibling in transform.parent.GetComponentsInChildren<StoreReferenceCard>(true))
            {
                if(sibling.transform.parent!=transform.parent)continue;
                sibling._chosen=sibling==this;
                sibling.GetComponent<Image>().sprite=sibling._chosen ? sibling._selected : sibling._normal;
                sibling.GetComponent<Image>().color=Color.white;
            }
        }
        public void OnSelect(BaseEventData data)=>SelectCard();
        public void OnPointerEnter(PointerEventData data){if(!_chosen)GetComponent<Image>().color=new Color(1.2f,1.17f,1.08f);}
        public void OnPointerExit(PointerEventData data){GetComponent<Image>().color=Color.white;}
    }
}
