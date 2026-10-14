using System.IO;
using System.Linq;
using EchoProtocol.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class LobbyReferenceStyleSetup
{
    private const string Art="Assets/Resources/UI/LobbyReference/";
    private static readonly Color Gold=new Color(0.79f,0.75f,0.55f);
    private static TMP_FontAsset _font;
    [MenuItem("ECHO Protocol/Lobby/Apply Reference Lobby Style")]
    public static void Apply()
    {
        var scene=SceneManager.GetActiveScene();
        if(scene.name!="Lobby" || Application.isPlaying)throw new System.InvalidOperationException("Open Lobby outside Play Mode.");
        var canvas=scene.GetRootGameObjects().First(x=>x.name=="NetworkLobbyCanvas");
        var panel=(RectTransform)canvas.transform.Find("PreMatchContent");
        var bindings=canvas.GetComponent<LobbyRuntimeBindingsController>();
        var data=new SerializedObject(bindings);
        Directory.CreateDirectory(Art);AssetDatabase.Refresh();
        var face=AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Lobby/Spectral-Regular.ttf");
        _font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Art+"LobbySerif.asset");
        if(_font==null) {
            _font=TMP_FontAsset.CreateFontAsset(face);_font.name="Lobby Serif";
            AssetDatabase.CreateAsset(_font,Art+"LobbySerif.asset");
            AssetDatabase.AddObjectToAsset(_font.material,_font);
            foreach(var atlas in _font.atlasTextures)AssetDatabase.AddObjectToAsset(atlas,_font);
        }
        var frame=ArtSprite("Frame",0);var button=ArtSprite("Button",1);var red=ArtSprite("Start",2);
        var portraitFrame=ArtSprite("Portrait",3);var fade=ArtSprite("Fade",4);var arrow=ArtSprite("Arrow",5);
        panel.anchorMin=panel.anchorMax=Vector2.one;panel.pivot=Vector2.one;panel.anchoredPosition=new Vector2(-48,-76);panel.sizeDelta=new Vector2(600,1004);
        foreach(var image in panel.GetComponentsInChildren<Image>(true))image.color=Color.clear;
        foreach(var outline in panel.GetComponentsInChildren<Outline>(true))outline.enabled=false;
        foreach(var label in panel.GetComponentsInChildren<TMP_Text>(true))label.gameObject.SetActive(false);
        var shade=Rect(panel,"ReferenceShade",-55,-76,703,1080);shade.SetAsFirstSibling();Image(shade,fade,false);
        Text(panel,"ReferenceCharacterLabel","Nhân vật:",0,0,600,30,26);
        var portrait=(RawImage)data.FindProperty("characterPortrait").objectReferenceValue;
        var card=(RectTransform)(portrait.transform.parent.name=="ReferencePortraitFrame" ? portrait.transform.parent.parent : portrait.transform.parent);card.gameObject.SetActive(true);Place(card,0,36,600,220);Image(card,frame);
        var cardButton=card.GetComponent<Button>();
        if(cardButton!=null){cardButton.transition=Selectable.Transition.None;cardButton.interactable=false;}
        card.GetComponent<Image>().canvasRenderer.SetColor(Color.white);
        var portraitBorder=Rect(card,"ReferencePortraitFrame",43,19,184,182);Image(portraitBorder,portraitFrame);
        portrait.transform.SetParent(portraitBorder,false);Place(portrait.rectTransform,5,5,174,172);portrait.gameObject.SetActive(true);portrait.enabled=true;
        var name=Text(card,"ReferenceCharacterName","ASTRONAUT",246,22,310,44,32);
        var description=Text(card,"ReferenceCharacterDescription","Phi hành gia trong bộ đồ thám hiểm.",246,72,300,120,20);
        data.FindProperty("characterNameText").objectReferenceValue=name;
        var previous=(Button)data.FindProperty("characterPreviousButton").objectReferenceValue;
        var next=(Button)data.FindProperty("characterNextButton").objectReferenceValue;
        previous.transform.SetParent(card,false);next.transform.SetParent(card,false);
        Arrow(previous,arrow,7,84,true);Arrow(next,arrow,559,84,false);
        var outfit=panel.Find("OutfitSelectionButton").GetComponent<Button>();Place(outfit.GetComponent<RectTransform>(),0,270,600,56);Style(outfit,button,"Trang phục",27);
        var dropdown=(TMP_Dropdown)data.FindProperty("difficultyDropdown").objectReferenceValue;dropdown.gameObject.SetActive(false);
        Text(panel,"ReferenceDifficultyLabel","Độ khó:",0,350,250,56,25);
        var picker=Rect(panel,"ReferenceDifficultyPicker",270,350,330,56);Image(picker,frame);
        var difficultyValue=Text(picker,"Value","Thường",48,0,234,56,24);difficultyValue.alignment=TextAlignmentOptions.Center;
        var previousDifficulty=NewButton(picker,"Previous");var nextDifficulty=NewButton(picker,"Next");
        Arrow(previousDifficulty,arrow,4,8,true);Arrow(nextDifficulty,arrow,292,8,false);
        Text(panel,"ReferenceToolLabel","Công cụ đội:",0,420,250,56,25);
        var toolSelector=canvas.GetComponentInChildren<LobbyTeamToolSelector>(true);var toolData=new SerializedObject(toolSelector);
        var toolFrame=Rect(panel,"ReferenceToolPicker",270,420,330,56);Image(toolFrame,frame);
        var toolPrevious=(Button)toolData.FindProperty("previousButton").objectReferenceValue;
        var toolNext=(Button)toolData.FindProperty("nextButton").objectReferenceValue;
        var toolValue=(TMP_Text)toolData.FindProperty("valueText").objectReferenceValue;
        toolPrevious.transform.SetParent(toolFrame,false);toolNext.transform.SetParent(toolFrame,false);toolValue.transform.SetParent(toolFrame,false);
        Arrow(toolPrevious,arrow,4,8,true);Arrow(toolNext,arrow,292,8,false);
        Place(toolValue.rectTransform,43,0,244,56);Font(toolValue,21);toolValue.alignment=TextAlignmentOptions.Center;toolValue.gameObject.SetActive(true);
        toolValue.text=EchoProtocol.Settings.GameLanguage.Choose("Không có","None");
        var thumbnail=(RawImage)toolData.FindProperty("thumbnail").objectReferenceValue;if(thumbnail!=null)thumbnail.gameObject.SetActive(false);
        var ready=(Button)data.FindProperty("readyButton").objectReferenceValue;
        var start=(Button)data.FindProperty("startButton").objectReferenceValue;
        Place(ready.GetComponent<RectTransform>(),0,824,600,54);Style(ready,button,"Sẵn sàng",27);
        Place(start.GetComponent<RectTransform>(),0,896,600,68);Style(start,red,"Bắt đầu nhiệm vụ",31);
        var exit=(Button)data.FindProperty("exitButton").objectReferenceValue;
        if(exit!=null){Place(exit.GetComponent<RectTransform>(),24,24,140,48);Style(exit,button,"Rời phòng",22);}
        var utility=canvas.transform.Find("LobbyUtilityBar");
        foreach(var image in utility.GetComponentsInChildren<Image>(true))image.color=Color.clear;
        foreach(var utilityButton in utility.GetComponentsInChildren<Button>(true)) {
            var bg=utilityButton.GetComponent<Image>();if(bg!=null){bg.sprite=button;bg.type=UnityEngine.UI.Image.Type.Sliced;bg.color=Color.white;}
            foreach(var icon in utilityButton.GetComponentsInChildren<Image>(true))if(icon!=bg)icon.color=Gold;
        }
        foreach(var label in utility.GetComponentsInChildren<TMP_Text>(true)){Font(label,label.fontSize>16 ? 23 : 13);if(label.text=="CREDITS")label.text="Tín dụng";}
        var presentation=canvas.GetComponent<LobbyReferencePresentation>() ?? canvas.AddComponent<LobbyReferencePresentation>();
        presentation.bindings=bindings;presentation.difficulty=dropdown;presentation.previousDifficulty=previousDifficulty;presentation.nextDifficulty=nextDifficulty;presentation.difficultyValue=difficultyValue;presentation.description=description;
        var settings=canvas.GetComponent<LobbyGameplaySettings>() ?? canvas.AddComponent<LobbyGameplaySettings>();
        settings.Previous=new Button[3];settings.Next=new Button[3];settings.Values=new TMP_Text[3];
        string[] settingsLabels={"Va chạm người chơi:","Thoại gần:","Túi cứu thương:"};
        for(int i=0;i<3;i++) {
            float y=490+i*70;
            Text(panel,"ReferenceSettingLabel"+i,settingsLabels[i],0,y,260,56,24);
            var settingPicker=Rect(panel,"ReferenceSettingPicker"+i,270,y,330,56);Image(settingPicker,frame);
            settings.Values[i]=Text(settingPicker,"Value","Bật",48,0,234,56,24);settings.Values[i].alignment=TextAlignmentOptions.Center;
            settings.Previous[i]=NewButton(settingPicker,"Previous");settings.Next[i]=NewButton(settingPicker,"Next");
            Arrow(settings.Previous[i],arrow,4,8,true);Arrow(settings.Next[i],arrow,292,8,false);
        }
        EditorUtility.SetDirty(settings);
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(presentation);EditorUtility.SetDirty(bindings);EditorUtility.SetDirty(_font);
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        Debug.Log("LOBBY_REFERENCE_STYLE_VALIDATED: character information bound, label backgrounds clear, arrow difficulty linked.");
    }
    private static RectTransform Rect(Transform parent,string name,float x,float y,float w,float h)
    {
        var existing=parent.Find(name) as RectTransform;
        if(existing==null){existing=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();existing.SetParent(parent,false);}
        Place(existing,x,y,w,h);return existing;
    }
    private static void Place(RectTransform rect,float x,float y,float w,float h)
    {
        rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(x,-y);rect.sizeDelta=new Vector2(w,h);rect.localScale=Vector3.one;
    }
    private static void Image(RectTransform rect,Sprite sprite,bool sliced=true)
    {
        var image=rect.GetComponent<Image>() ?? rect.gameObject.AddComponent<Image>();image.enabled=true;image.sprite=sprite;image.color=Color.white;image.type=sliced ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;image.raycastTarget=false;
    }
    private static TMP_Text Text(Transform parent,string name,string value,float x,float y,float w,float h,int size)
    {
        var rect=Rect(parent,name,x,y,w,h);var label=rect.GetComponent<TextMeshProUGUI>() ?? rect.gameObject.AddComponent<TextMeshProUGUI>();label.text=value;Font(label,size);return label;
    }
    private static void Font(TMP_Text label,float size)
    {
        label.font=_font;label.fontSize=size;label.fontStyle=FontStyles.Normal;label.color=Gold;label.enableAutoSizing=true;label.fontSizeMax=size;label.fontSizeMin=size*0.75f;label.raycastTarget=false;label.gameObject.SetActive(true);label.alignment=TextAlignmentOptions.Left;
    }
    private static void Style(Button control,Sprite sprite,string value,int size)
    {
        control.gameObject.SetActive(true);Image(control.GetComponent<RectTransform>(),sprite);
        var body=control.transform.Find("ReferenceBody");if(body!=null)Object.DestroyImmediate(body.gameObject);
        foreach(var child in control.GetComponentsInChildren<Image>(true))if(child.gameObject!=control.gameObject)child.color=Color.clear;
        var label=control.GetComponentInChildren<TMP_Text>(true);if(label==null)label=Text(control.transform,"Caption",value,0,0,control.GetComponent<RectTransform>().rect.width,control.GetComponent<RectTransform>().rect.height,size);
        Font(label,size);label.text=value;Place(label.rectTransform,8,2,control.GetComponent<RectTransform>().rect.width-16,control.GetComponent<RectTransform>().rect.height-4);label.alignment=TextAlignmentOptions.Center;
        control.targetGraphic=control.GetComponent<Image>();control.targetGraphic.raycastTarget=true;
        var colors=control.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1.2f,1.15f,1f);colors.selectedColor=colors.highlightedColor;colors.pressedColor=new Color(0.7f,0.65f,0.55f);colors.disabledColor=new Color(0.35f,0.35f,0.35f);control.colors=colors;
    }
    private static Button NewButton(Transform parent,string name)
    {
        var rect=Rect(parent,name,0,0,34,40);return rect.GetComponent<Button>() ?? rect.gameObject.AddComponent<Button>();
    }
    private static void Arrow(Button control,Sprite sprite,float x,float y,bool left)
    {
        Place(control.GetComponent<RectTransform>(),x,y,34,40);control.gameObject.SetActive(true);
        foreach(var label in control.GetComponentsInChildren<TMP_Text>(true))label.gameObject.SetActive(false);
        foreach(var image in control.GetComponentsInChildren<Image>(true))image.color=Color.clear;
        var icon=Rect(control.transform,"ReferenceArrow",7,8,20,24);Image(icon,sprite,false);icon.GetComponent<Image>().color=Gold;
        icon.localScale=new Vector3(left ? -1 : 1,1,1);icon.pivot=new Vector2(0.5f,0.5f);icon.anchoredPosition=new Vector2(17,-20);
        var target=control.GetComponent<Image>() ?? control.gameObject.AddComponent<Image>();target.color=Color.clear;target.raycastTarget=true;control.targetGraphic=target;
    }
    private static Sprite ArtSprite(string name,int kind)
    {
        string path=Art+name+".png";
        int width=kind==1 || kind==2 ? 1024 : 256,height=kind==1 || kind==2 ? 128 : 256;
        var texture=new Texture2D(width,height,TextureFormat.RGBA32,false);
        var pixels=new Color[width*height];
        for(int y=0;y<height;y++)for(int x=0;x<width;x++) {
            float u=x/(width-1f),v=y/(height-1f),n=Mathf.PerlinNoise(x*0.19f,y*0.19f),m=Mathf.PerlinNoise(x*0.045f,y*0.045f);
            int edge=Mathf.Min(Mathf.Min(x,y),Mathf.Min(width-1-x,height-1-y));
            Color color;
            if(kind==5)color=u>0.15f && u<0.85f && Mathf.Abs(v-0.5f)<(0.85f-u)*0.65f ? Color.white : Color.clear;
            else if(kind==4) {
                float alpha=Mathf.SmoothStep(0,0.9f,u)*(0.76f+m*0.08f);
                float dx=u-0.52f,dy=v-0.48f,r=Mathf.Sqrt(dx*dx+dy*dy),angle=Mathf.Atan2(dy,dx);
                bool emblem=(Mathf.Abs(r-0.27f)<0.004f || Mathf.Abs(r-0.20f)<0.003f || r<0.27f && Mathf.Abs(Mathf.Sin(angle*3f))<0.025f);
                color=emblem ? new Color(0.22f,0.14f,0.13f,alpha) : new Color(0.008f+m*0.015f,0.008f,0.009f,alpha);
            } else if(edge<2)color=new Color(0.04f,0.03f,0.025f,0.9f);
            else if(edge<5)color=Color.Lerp(new Color(0.12f,0.09f,0.07f),new Color(0.52f,0.46f,0.34f),n);
            else if(kind==1)color=new Color(0.08f+n*0.13f+m*0.06f,0.078f+n*0.12f+m*0.05f,0.07f+n*0.11f+m*0.04f);
            else if(kind==2 || kind==3)color=new Color(0.13f+n*0.11f+m*0.1f,0.008f+n*0.018f,0.006f+n*0.016f);
            else color=new Color(0.009f+n*0.012f,0.008f+n*0.01f,0.007f+n*0.01f,0.94f);
            pixels[y*width+x]=color;
        }
        texture.SetPixels(pixels);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.spriteBorder=kind<4 ? new Vector4(7,7,7,7) : Vector4.zero;importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
