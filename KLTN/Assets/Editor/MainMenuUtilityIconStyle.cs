using EchoProtocol.UI;
using EchoProtocol.UI.MainMenu;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class MainMenuUtilityIconStyle
{
    private const string MainMenuScenePath =
        "Assets/Scenes/MainMenu.unity";

    private static readonly Color Background =
        Hex("090C0D", 0.86f);

    private static readonly Color Hover =
        Hex("24292A", 0.98f);

    private static readonly Color Pressed =
        Hex("402421", 1f);

    private static readonly Color Border =
        Hex("373D3D", 0.92f);

    private static readonly Color Icon =
        Hex("D5D6D1", 1f);

    private static readonly Color Credits =
        Hex("B9AA82", 1f);

    [MenuItem(
        "ECHO PROTOCOL/Main Menu/Apply Utility Icons")]
    public static void ApplyFromMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode
            || EditorApplication.isCompiling)
        {
            return;
        }

        var scene =
            SceneManager.GetActiveScene();

        if (scene.name != "MainMenu")
        {
            Debug.LogError(
                "[MainMenuUtilityIconStyle] Open MainMenu first.");

            return;
        }

        Apply(scene);

        EditorSceneManager.MarkSceneDirty(scene);

        Debug.Log(
            "[MainMenuUtilityIconStyle] Utility icons applied.");
    }

    public static void ApplyMainMenuBatch()
    {
        var scene =
            EditorSceneManager.OpenScene(
                MainMenuScenePath,
                OpenSceneMode.Single);

        Apply(scene);

        EditorSceneManager.MarkSceneDirty(scene);

        if (!EditorSceneManager.SaveScene(scene))
        {
            throw new System.InvalidOperationException(
                "Could not save MainMenu.");
        }

        Debug.Log(
            "[ECHO] Main menu utility icons completed.");
    }

    private static void Apply(Scene scene)
    {
        var controller =
            Object.FindAnyObjectByType<MainMenuProfileController>(
                FindObjectsInactive.Include);

        var canvas =
            GameObject.Find("MainMenuCanvas");

        if (controller == null || canvas == null)
        {
            throw new System.InvalidOperationException(
                "MainMenu controller/canvas missing.");
        }

        var store =
            canvas.transform.Find("StorePanel")
            as RectTransform;

        if (store == null)
        {
            throw new System.InvalidOperationException(
                "StorePanel missing.");
        }

        var coin =
            store.Find("CoinContainer")
            as RectTransform;

        var shop =
            store.Find("ShopButton")
                ?.GetComponent<Button>();

        var web =
            store.Find("WebButton")
                ?.GetComponent<Button>();

        if (coin == null || shop == null || web == null)
        {
            throw new System.InvalidOperationException(
                "CoinContainer / ShopButton / WebButton missing. " +
                "Main Menu phases A/B must be applied first.");
        }

        Undo.RegisterFullObjectHierarchyUndo(
            store.gameObject,
            "Apply Main Menu Utility Icons");

        // =================================================
        // TOP RIGHT COMPOSITION
        // =================================================

        PlaceTopRight(
            store,
            28f,
            24f,
            314f,
            60f);

        // Credit remains the largest element.
        PlaceTopLeft(
            coin,
            0f,
            0f,
            178f,
            52f);

        // Small square utilities, closer to DEVOUR.
        StyleIconButton(
            shop,
            SettingsIcon.Shop,
            190f,
            0f,
            52f,
            52f);

        StyleIconButton(
            web,
            SettingsIcon.Web,
            252f,
            0f,
            52f,
            52f);

        // =================================================
        // CREDIT VISUAL REFINEMENT
        // =================================================

        var coinBackground =
            coin.GetComponent<Image>()
            ?? coin.Find("PanelBackground")
                ?.GetComponent<Image>();

        if (coinBackground != null)
        {
            coinBackground.color = Background;
        }

        var coinOutline =
            coin.GetComponent<Outline>();

        if (coinOutline == null)
        {
            coinOutline =
                Undo.AddComponent<Outline>(
                    coin.gameObject);
        }

        coinOutline.effectColor = Border;

        coinOutline.effectDistance =
            new Vector2(1f, -1f);

        var amount =
            coin.Find("CoinAmountText")
                ?.GetComponent<Text>();

        if (amount != null)
        {
            amount.color = Credits;
            amount.fontSize = 17;
            amount.fontStyle = FontStyle.Bold;
        }

        var label =
            coin.Find("CoinLabelText")
                ?.GetComponent<Text>();

        if (label != null)
        {
            label.text = "CREDITS";
            label.fontSize = 9;
        }
    }

    private static void StyleIconButton(
        Button button,
        SettingsIcon iconType,
        float x,
        float y,
        float width,
        float height)
    {
        if (button == null)
        {
            return;
        }

        button.gameObject.SetActive(true);

        PlaceTopLeft(
            button.transform as RectTransform,
            x,
            y,
            width,
            height);

        // Hide all old SHOP / WEB text labels.
        foreach (var text in
                 button.GetComponentsInChildren<Text>(
                     true))
        {
            text.gameObject.SetActive(false);
        }

        var target =
            button.targetGraphic as Image;

        if (target == null)
        {
            target =
                button.GetComponent<Image>();
        }

        if (target == null)
        {
            target =
                Undo.AddComponent<Image>(
                    button.gameObject);
        }

        target.color = Background;
        target.raycastTarget = true;

        button.targetGraphic = target;

        button.transition =
            Selectable.Transition.ColorTint;

        var colors =
            button.colors;

        colors.normalColor =
            Background;

        colors.highlightedColor =
            Hover;

        colors.selectedColor =
            Hover;

        colors.pressedColor =
            Pressed;

        colors.disabledColor =
            Hex("080A0A", 0.38f);

        colors.fadeDuration =
            0.08f;

        button.colors = colors;

        var outline =
            button.GetComponent<Outline>();

        if (outline == null)
        {
            outline =
                Undo.AddComponent<Outline>(
                    button.gameObject);
        }

        outline.effectColor =
            Border;

        outline.effectDistance =
            new Vector2(1f, -1f);

        // ---------------------------------------------
        // VECTOR ICON
        // ---------------------------------------------

        var iconTransform =
            button.transform.Find("UtilityIcon");

        GameObject iconObject;

        if (iconTransform != null)
        {
            iconObject =
                iconTransform.gameObject;
        }
        else
        {
            iconObject =
                new GameObject(
                    "UtilityIcon",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(SettingsIconGraphic));

            iconObject.transform.SetParent(
                button.transform,
                false);

            Undo.RegisterCreatedObjectUndo(
                iconObject,
                "Create Utility Icon");
        }

        var iconRect =
            iconObject.transform
                as RectTransform;

        PlaceTopLeft(
            iconRect,
            12f,
            12f,
            28f,
            28f);

        var graphic =
            iconObject.GetComponent<SettingsIconGraphic>();

        graphic.Configure(
            iconType,
            Icon);

        graphic.raycastTarget = false;

        iconObject.transform.SetAsLastSibling();
    }

    private static void PlaceTopLeft(
        RectTransform rect,
        float x,
        float y,
        float width,
        float height)
    {
        if (rect == null)
        {
            return;
        }

        rect.anchorMin =
            new Vector2(0f, 1f);

        rect.anchorMax =
            new Vector2(0f, 1f);

        rect.pivot =
            new Vector2(0f, 1f);

        rect.anchoredPosition =
            new Vector2(x, -y);

        rect.sizeDelta =
            new Vector2(width, height);
    }

    private static void PlaceTopRight(
        RectTransform rect,
        float x,
        float y,
        float width,
        float height)
    {
        rect.anchorMin =
            new Vector2(1f, 1f);

        rect.anchorMax =
            new Vector2(1f, 1f);

        rect.pivot =
            new Vector2(1f, 1f);

        rect.anchoredPosition =
            new Vector2(-x, -y);

        rect.sizeDelta =
            new Vector2(width, height);
    }

    private static Color Hex(
        string hex,
        float alpha)
    {
        ColorUtility.TryParseHtmlString(
            "#" + hex,
            out var color);

        color.a = alpha;

        return color;
    }
}
