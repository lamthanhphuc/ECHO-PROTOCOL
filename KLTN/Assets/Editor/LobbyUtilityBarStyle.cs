using EchoProtocol.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class LobbyUtilityBarStyle
{
    private const string LobbyScene =
        "Assets/Scenes/Lobby.unity";

    private static readonly Color Background =
        Hex("090C0D", 0.88f);

    private static readonly Color Border =
        Hex("373D3D", 0.95f);

    private static readonly Color Foreground =
        Hex("D5D6D1", 1f);

    private static readonly Color Muted =
        Hex("777D7C", 1f);

    private static readonly Color Credits =
        Hex("B9AA82", 1f);

    public static void ApplyLobbyBatch()
    {
        var scene =
            EditorSceneManager.OpenScene(
                LobbyScene,
                OpenSceneMode.Single);

        Apply();

        EditorSceneManager.MarkSceneDirty(scene);

        if (!EditorSceneManager.SaveScene(scene))
            throw new System.InvalidOperationException(
                "Could not save Lobby.");

        Debug.Log(
            "[ECHO] Lobby utility bar completed.");
    }

    [MenuItem(
        "ECHO PROTOCOL/Lobby/Apply Utility Bar")]
    private static void ApplyMenu()
    {
        Apply();
        EditorSceneManager.MarkSceneDirty(
            SceneManager.GetActiveScene());
    }

    private static void Apply()
    {
        var lobby =
            Object.FindAnyObjectByType<NetworkLobbyUI>(
                FindObjectsInactive.Include);

        if (lobby == null)
            throw new System.InvalidOperationException(
                "NetworkLobbyUI missing.");

        var canvas =
            lobby.GetComponentInParent<Canvas>();

        if (canvas == null)
            throw new System.InvalidOperationException(
                "Lobby Canvas missing.");

        var root =
            EnsureRect(
                canvas.transform,
                "LobbyUtilityBar");

        PlaceTopRight(
            root,
            28f,
            24f,
            314f,
            60f);

        root.SetAsLastSibling();

        var credit =
            EnsureButton(
                root,
                "CreditsButton");

        PlaceTopLeft(
            credit.transform as RectTransform,
            0f,
            0f,
            178f,
            52f);

        StyleBox(credit);

        var amount =
            EnsureTMP(
                credit.transform,
                "CreditsAmount");

        PlaceTopLeft(
            amount.rectTransform,
            18f,
            5f,
            145f,
            24f);

        amount.text = "0";
        amount.fontSize = 17;
        amount.fontStyle = FontStyles.Bold;
        amount.color = Credits;
        amount.alignment =
            TextAlignmentOptions.Left;

        var caption =
            EnsureTMP(
                credit.transform,
                "CreditsLabel");

        PlaceTopLeft(
            caption.rectTransform,
            18f,
            28f,
            145f,
            16f);

        caption.text = "CREDITS";
        caption.fontSize = 9;
        caption.color = Muted;
        caption.alignment =
            TextAlignmentOptions.Left;

        var shop =
            EnsureButton(
                root,
                "ShopButton");

        PlaceTopLeft(
            shop.transform as RectTransform,
            190f,
            0f,
            52f,
            52f);

        StyleBox(shop);

        EnsureIcon(
            shop.transform,
            "Icon",
            SettingsIcon.Shop);

        var web =
            EnsureButton(
                root,
                "WebButton");

        PlaceTopLeft(
            web.transform as RectTransform,
            252f,
            0f,
            52f,
            52f);

        StyleBox(web);

        EnsureIcon(
            web.transform,
            "Icon",
            SettingsIcon.Web);

        var controller =
            root.GetComponent<LobbyUtilityBarController>();

        if (controller == null)
            controller =
                Undo.AddComponent<LobbyUtilityBarController>(
                    root.gameObject);

        var so =
            new SerializedObject(controller);

        Assign(so, "creditsText", amount);
        Assign(so, "creditsButton", credit);
        Assign(so, "shopButton", shop);
        Assign(so, "webButton", web);

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void StyleBox(Button button)
    {
        var image =
            button.GetComponent<Image>();

        image.color = Background;

        var outline =
            button.GetComponent<Outline>();

        if (outline == null)
            outline =
                Undo.AddComponent<Outline>(
                    button.gameObject);

        outline.effectColor = Border;
        outline.effectDistance =
            new Vector2(1f, -1f);

        var colors =
            button.colors;

        colors.normalColor = Background;
        colors.highlightedColor =
            Hex("24292A", 1f);
        colors.selectedColor =
            colors.highlightedColor;
        colors.pressedColor =
            Hex("402421", 1f);
        colors.fadeDuration = 0.08f;

        button.colors = colors;
    }

    private static void EnsureIcon(
        Transform parent,
        string name,
        SettingsIcon type)
    {
        var child =
            parent.Find(name);

        GameObject go;

        if (child != null)
            go = child.gameObject;
        else
        {
            go = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(SettingsIconGraphic));

            go.transform.SetParent(parent, false);
        }

        var rect =
            go.transform as RectTransform;

        PlaceTopLeft(
            rect,
            12f,
            12f,
            28f,
            28f);

        var icon =
            go.GetComponent<SettingsIconGraphic>();

        icon.Configure(type, Foreground);
        icon.raycastTarget = false;
    }

    private static Button EnsureButton(
        Transform parent,
        string name)
    {
        var go =
            EnsureRect(parent, name).gameObject;

        var image =
            go.GetComponent<Image>()
            ?? go.AddComponent<Image>();

        var button =
            go.GetComponent<Button>()
            ?? go.AddComponent<Button>();

        button.targetGraphic = image;

        return button;
    }

    private static TMP_Text EnsureTMP(
        Transform parent,
        string name)
    {
        var go =
            EnsureRect(parent, name).gameObject;

        var text =
            go.GetComponent<TextMeshProUGUI>()
            ?? go.AddComponent<TextMeshProUGUI>();

        text.font = TMP_Settings.defaultFontAsset;
        text.raycastTarget = false;

        return text;
    }

    private static RectTransform EnsureRect(
        Transform parent,
        string name)
    {
        var found =
            parent.Find(name) as RectTransform;

        if (found != null)
            return found;

        var go =
            new GameObject(
                name,
                typeof(RectTransform));

        go.transform.SetParent(parent, false);

        return go.GetComponent<RectTransform>();
    }

    private static void Assign(
        SerializedObject so,
        string name,
        Object value)
    {
        var p = so.FindProperty(name);
        if (p != null)
            p.objectReferenceValue = value;
    }

    private static void PlaceTopLeft(
        RectTransform rect,
        float x,
        float y,
        float w,
        float h)
    {
        rect.anchorMin =
            rect.anchorMax =
            rect.pivot =
                new Vector2(0f,1f);

        rect.anchoredPosition =
            new Vector2(x,-y);

        rect.sizeDelta =
            new Vector2(w,h);
    }

    private static void PlaceTopRight(
        RectTransform rect,
        float x,
        float y,
        float w,
        float h)
    {
        rect.anchorMin =
            rect.anchorMax =
            rect.pivot =
                new Vector2(1f,1f);

        rect.anchoredPosition =
            new Vector2(-x,-y);

        rect.sizeDelta =
            new Vector2(w,h);
    }

    private static Color Hex(
        string value,
        float alpha)
    {
        ColorUtility.TryParseHtmlString(
            "#" + value,
            out var color);

        color.a = alpha;
        return color;
    }
}
