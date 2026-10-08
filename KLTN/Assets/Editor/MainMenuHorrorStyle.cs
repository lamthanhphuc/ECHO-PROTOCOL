using EchoProtocol.UI.MainMenu;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class MainMenuHorrorStyle
{
    private const string MainMenuScenePath =
        "Assets/Scenes/MainMenu.unity";

    private static readonly Color TextPrimary =
        Hex("E1E2DE", 1f);

    private static readonly Color TextSecondary =
        Hex("777D7C", 1f);

    private static readonly Color Hover =
        Hex("181D1D", 0.88f);

    private static readonly Color Accent =
        Hex("883B37", 1f);

    private static readonly Color Credits =
        Hex("B9AA82", 1f);

    private static readonly Color UtilityBackground =
        Hex("0B0E0E", 0.78f);

    [MenuItem(
        "ECHO PROTOCOL/Main Menu/Apply Horror Menu Restyle")]
    public static void ApplyFromMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode
            || EditorApplication.isCompiling)
        {
            return;
        }

        var scene = SceneManager.GetActiveScene();

        if (scene.name != "MainMenu")
        {
            Debug.LogError(
                "[MainMenuHorrorStyle] Open MainMenu first.");
            return;
        }

        Apply(scene);
        EditorSceneManager.MarkSceneDirty(scene);

        Debug.Log(
            "[MainMenuHorrorStyle] Restyle applied. Ctrl+S to save.");
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
                "Could not save MainMenu scene.");
        }

        Debug.Log(
            "[ECHO] Main menu horror restyle batch completed.");
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
                "MainMenuProfileController or MainMenuCanvas is missing.");
        }

        var root =
            controller.transform as RectTransform;

        var canvasRect =
            canvas.transform as RectTransform;

        if (root == null || canvasRect == null)
        {
            throw new System.InvalidOperationException(
                "MainMenu RectTransform is missing.");
        }

        Undo.RegisterFullObjectHierarchyUndo(
            canvas,
            "Restyle Main Menu");

        DisableAutomaticLayout(root);

        var font =
            root.GetComponentInChildren<Text>(true)?.font
            ?? Resources.GetBuiltinResource<Font>(
                "LegacyRuntime.ttf");

        // =================================================
        // LEFT-SIDE MAIN MENU
        // =================================================

        PlaceTopLeft(
            root,
            64f,
            72f,
            440f,
            820f);

        // Remove old dashboard/card appearance.
        var rootImage =
            root.GetComponent<Image>();

        if (rootImage != null)
        {
            rootImage.color = Color.clear;
            rootImage.raycastTarget = false;
        }

        SetActive(
            root,
            "PanelBackground",
            false);

        SetActive(
            root,
            "TopAccent",
            false);

        var rootOutline =
            root.GetComponent<Outline>();

        if (rootOutline != null)
        {
            rootOutline.enabled = false;
        }

        var header =
            EnsureText(
                root,
                "HeaderText",
                font);

        PlaceTopLeft(
            header.rectTransform,
            0f,
            0f,
            430f,
            62f);

        header.text =
            "ECHO PROTOCOL";

        header.fontSize = 38;
        header.fontStyle =
            FontStyle.Bold;

        header.color =
            TextPrimary;

        header.alignment =
            TextAnchor.MiddleLeft;

        var system =
            EnsureText(
                root,
                "SystemLabel",
                font);

        PlaceTopLeft(
            system.rectTransform,
            2f,
            58f,
            420f,
            24f);

        system.text =
            "COOPERATIVE SURVIVAL HORROR";

        system.fontSize = 11;
        system.fontStyle =
            FontStyle.Bold;

        system.color =
            TextSecondary;

        system.alignment =
            TextAnchor.MiddleLeft;

        var divider =
            EnsureImage(
                root,
                "Divider");

        PlaceTopLeft(
            divider.rectTransform,
            2f,
            98f,
            64f,
            2f);

        divider.color =
            Accent;

        divider.raycastTarget =
            false;

        // Old account presentation is no longer part
        // of the left main menu.
        SetActive(root, "WelcomeText", false);
        SetActive(root, "RoleText", false);
        SetActive(root, "WalletText", false);
        SetActive(root, "PlayerNameText", false);

        var play =
            root.Find("PlayButton")
                ?.GetComponent<Button>();

        var logout =
            root.Find("LogoutButton")
                ?.GetComponent<Button>();
        var options =
            EnsureMenuButton(
                root,
                "OptionsButton",
                font);

        // PLAY is intentionally preserved in Phase A.
        // Phase B will replace it with Host / Join.
        StyleMenuButton(
            play,
            "PLAY",
            2f,
            220f,
            310f,
            58f);

        StyleMenuButton(
            options,
            "OPTIONS",
            2f,
            292f,
            310f,
            52f);

        StyleMenuButton(
            logout,
            "LOGOUT",
            2f,
            364f,
            310f,
            48f);

        // =================================================
        // TOP-RIGHT UTILITIES
        // =================================================

        var store =
            canvas.transform.Find("StorePanel")
            as RectTransform;

        if (store == null)
        {
            throw new System.InvalidOperationException(
                "StorePanel is missing.");
        }

        PlaceTopRight(
            store,
            28f,
            24f,
            370f,
            72f);

        var storeImage =
            store.GetComponent<Image>();

        if (storeImage != null)
        {
            storeImage.color =
                Color.clear;

            storeImage.raycastTarget =
                false;
        }

        SetActive(
            store,
            "PanelBackground",
            false);

        SetActive(
            store,
            "HeaderText",
            false);

        SetActive(
            store,
            "Divider",
            false);

        var storeOutline =
            store.GetComponent<Outline>();

        if (storeOutline != null)
        {
            storeOutline.enabled = false;
        }

        // -------------------------------------------------
        // CREDITS
        // -------------------------------------------------

        var coin =
            store.Find("CoinContainer")
            as RectTransform;

        if (coin == null)
        {
            throw new System.InvalidOperationException(
                "CoinContainer is missing.");
        }

        PlaceTopLeft(
            coin,
            0f,
            0f,
            176f,
            52f);

        var coinBackground =
            coin.Find("PanelBackground")
                ?.GetComponent<Image>();

        if (coinBackground == null)
        {
            coinBackground =
                coin.GetComponent<Image>();

            if (coinBackground == null)
            {
                coinBackground =
                    Undo.AddComponent<Image>(
                        coin.gameObject);
            }
        }

        coinBackground.color =
            UtilityBackground;

        coinBackground.raycastTarget =
            true;

        var coinButton =
            coin.GetComponent<Button>();

        if (coinButton == null)
        {
            coinButton =
                Undo.AddComponent<Button>(
                    coin.gameObject);
        }

        coinButton.targetGraphic =
            coinBackground;

        ConfigureCompactColors(
            coinButton);

        var coinOutline =
            coin.GetComponent<Outline>();

        if (coinOutline != null)
        {
            coinOutline.effectColor =
                Hex("393E3D", 0.7f);

            coinOutline.effectDistance =
                new Vector2(1f, -1f);
        }

        var coinIcon =
            coin.Find("CoinIcon")
                ?.GetComponent<Image>();

        if (coinIcon != null)
        {
            PlaceTopLeft(
                coinIcon.rectTransform,
                12f,
                12f,
                28f,
                28f);

            coinIcon.color =
                Credits;

            coinIcon.raycastTarget =
                false;
        }

        var amount =
            coin.Find("CoinAmountText")
                ?.GetComponent<Text>();

        if (amount != null)
        {
            PlaceTopLeft(
                amount.rectTransform,
                50f,
                5f,
                112f,
                24f);

            amount.fontSize = 17;
            amount.fontStyle =
                FontStyle.Bold;

            amount.color =
                Credits;

            amount.alignment =
                TextAnchor.MiddleLeft;

            amount.raycastTarget =
                false;
        }

        var coinLabel =
            coin.Find("CoinLabelText")
                ?.GetComponent<Text>();

        if (coinLabel != null)
        {
            PlaceTopLeft(
                coinLabel.rectTransform,
                50f,
                27f,
                112f,
                17f);

            coinLabel.text =
                "CREDITS";

            coinLabel.fontSize = 9;
            coinLabel.color =
                TextSecondary;

            coinLabel.alignment =
                TextAnchor.MiddleLeft;

            coinLabel.raycastTarget =
                false;
        }

        // -------------------------------------------------
        // SHOP
        // -------------------------------------------------

        var shop =
            store.Find("ShopButton")
                ?.GetComponent<Button>();

        StyleCompactButton(
            shop,
            "SHOP",
            188f,
            0f,
            72f,
            52f);

        // -------------------------------------------------
        // OLD TOP UP
        // -------------------------------------------------

        // Credit container now owns this action.
        var oldTopUp =
            store.Find("TopUpButton");

        if (oldTopUp != null)
        {
            oldTopUp.gameObject.SetActive(false);
        }

        // -------------------------------------------------
        // WEB
        // -------------------------------------------------

        var web =
            EnsureCompactButton(
                store,
                "WebButton",
                font);

        StyleCompactButton(
            web,
            "WEB",
            272f,
            0f,
            72f,
            52f);

        // -------------------------------------------------
        // LEGACY TOP-UP POPUP
        // -------------------------------------------------

        // Kept for compatibility/rollback, but normal
        // MainMenu navigation no longer opens it.
        var topUpPopup =
            canvas.transform.Find("TopUpPopup");

        if (topUpPopup != null)
        {
            topUpPopup.gameObject.SetActive(false);
        }

        // ShopPopup stays untouched.

        // =================================================
        // SERIALIZED REFERENCES
        // =================================================

        var serialized =
            new SerializedObject(controller);

        Assign(
            serialized,
            "topUpButton",
            coinButton);

        Assign(
            serialized,
            "shopButton",
            shop);

        Assign(
            serialized,
            "webButton",
            web);

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void StyleMenuButton(
        Button button,
        string label,
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

        var background =
            button.transform.Find("Background")
                ?.GetComponent<Image>()
            ?? button.targetGraphic as Image
            ?? button.GetComponent<Image>();

        if (background != null)
        {
            background.color =
                Color.clear;

            background.raycastTarget =
                true;

            button.targetGraphic =
                background;
        }

        var colors =
            button.colors;

        colors.normalColor =
            Color.clear;

        colors.highlightedColor =
            Hover;

        colors.selectedColor =
            Hover;

        colors.pressedColor =
            new Color(
                Accent.r,
                Accent.g,
                Accent.b,
                0.55f);

        colors.disabledColor =
            Color.clear;

        colors.fadeDuration =
            0.08f;

        button.colors =
            colors;

        var text =
            button.GetComponentInChildren<Text>(
                true);

        if (text != null)
        {
            text.text =
                label;

            text.fontSize =
                label == "PLAY"
                    ? 27
                    : 21;

            text.fontStyle =
                FontStyle.Normal;

            text.color =
                TextPrimary;

            text.alignment =
                TextAnchor.MiddleLeft;

            text.raycastTarget =
                false;

            PlaceTopLeft(
                text.rectTransform,
                8f,
                0f,
                width - 16f,
                height);
        }
    }

    private static void StyleCompactButton(
        Button button,
        string label,
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

        var background =
            button.transform.Find("Background")
                ?.GetComponent<Image>()
            ?? button.targetGraphic as Image
            ?? button.GetComponent<Image>();

        if (background == null)
        {
            background =
                Undo.AddComponent<Image>(
                    button.gameObject);
        }

        background.color =
            UtilityBackground;

        background.raycastTarget =
            true;

        button.targetGraphic =
            background;

        ConfigureCompactColors(
            button);

        var text =
            button.GetComponentInChildren<Text>(
                true);

        if (text != null)
        {
            text.text =
                label;

            text.fontSize = 10;

            text.fontStyle =
                FontStyle.Bold;

            text.color =
                TextPrimary;

            text.alignment =
                TextAnchor.MiddleCenter;

            text.raycastTarget =
                false;

            PlaceTopLeft(
                text.rectTransform,
                4f,
                0f,
                width - 8f,
                height);
        }

        var title =
            button.transform.Find("TitleText");

        if (title != null
            && title.GetComponent<Text>() != text)
        {
            title.gameObject.SetActive(false);
        }

        var subtitle =
            button.transform.Find("SubtitleText");

        if (subtitle != null)
        {
            subtitle.gameObject.SetActive(false);
        }
    }

    private static void ConfigureCompactColors(
        Button button)
    {
        button.transition =
            Selectable.Transition.ColorTint;

        var colors =
            button.colors;

        colors.normalColor =
            UtilityBackground;

        colors.highlightedColor =
            Hex("252A29", 0.96f);

        colors.selectedColor =
            colors.highlightedColor;

        colors.pressedColor =
            Hex("3A2422", 1f);

        colors.disabledColor =
            Hex("090B0B", 0.4f);

        colors.fadeDuration =
            0.08f;

        button.colors =
            colors;
    }

    private static Button EnsureMenuButton(
        Transform parent,
        string name,
        Font font)
    {
        var existing =
            parent.Find(name);

        GameObject go;

        if (existing != null)
        {
            go = existing.gameObject;
        }
        else
        {
            go =
                new GameObject(
                    name,
                    typeof(RectTransform),
                    typeof(Image),
                    typeof(Button));

            go.transform.SetParent(
                parent,
                false);

            Undo.RegisterCreatedObjectUndo(
                go,
                "Create Main Menu Button");
        }

        var button =
            go.GetComponent<Button>()
            ?? Undo.AddComponent<Button>(go);

        var image =
            go.GetComponent<Image>()
            ?? Undo.AddComponent<Image>(go);

        image.color = Color.clear;
        image.raycastTarget = true;
        button.targetGraphic = image;

        var text =
            go.transform.Find("Text")
                ?.GetComponent<Text>();

        if (text == null)
        {
            var textObject =
                new GameObject(
                    "Text",
                    typeof(RectTransform),
                    typeof(Text));

            textObject.transform.SetParent(
                go.transform,
                false);

            text =
                textObject.GetComponent<Text>();

            text.font = font;
            text.raycastTarget = false;
        }

        Stretch(text.rectTransform);

        return button;
    }
    private static Button EnsureCompactButton(
        Transform parent,
        string name,
        Font font)
    {
        var existing =
            parent.Find(name);

        GameObject go;

        if (existing != null)
        {
            go =
                existing.gameObject;
        }
        else
        {
            go =
                new GameObject(
                    name,
                    typeof(RectTransform),
                    typeof(Image),
                    typeof(Button));

            go.transform.SetParent(
                parent,
                false);

            Undo.RegisterCreatedObjectUndo(
                go,
                "Create Main Menu Web Button");
        }

        var button =
            go.GetComponent<Button>()
            ?? Undo.AddComponent<Button>(go);

        var image =
            go.GetComponent<Image>()
            ?? Undo.AddComponent<Image>(go);

        image.color =
            UtilityBackground;

        image.raycastTarget =
            true;

        button.targetGraphic =
            image;

        var text =
            go.transform.Find("Text")
                ?.GetComponent<Text>();

        if (text == null)
        {
            var textObject =
                new GameObject(
                    "Text",
                    typeof(RectTransform),
                    typeof(Text));

            textObject.transform.SetParent(
                go.transform,
                false);

            text =
                textObject.GetComponent<Text>();

            text.font =
                font;

            text.raycastTarget =
                false;
        }

        Stretch(
            text.rectTransform);

        return button;
    }

    private static Text EnsureText(
        Transform parent,
        string name,
        Font font)
    {
        var existing =
            parent.Find(name)
                ?.GetComponent<Text>();

        if (existing != null)
        {
            return existing;
        }

        var go =
            new GameObject(
                name,
                typeof(RectTransform),
                typeof(Text));

        go.transform.SetParent(
            parent,
            false);

        Undo.RegisterCreatedObjectUndo(
            go,
            "Create Main Menu Text");

        var text =
            go.GetComponent<Text>();

        text.font =
            font;

        text.raycastTarget =
            false;

        return text;
    }

    private static Image EnsureImage(
        Transform parent,
        string name)
    {
        var existing =
            parent.Find(name)
                ?.GetComponent<Image>();

        if (existing != null)
        {
            return existing;
        }

        var go =
            new GameObject(
                name,
                typeof(RectTransform),
                typeof(Image));

        go.transform.SetParent(
            parent,
            false);

        Undo.RegisterCreatedObjectUndo(
            go,
            "Create Main Menu Image");

        return go.GetComponent<Image>();
    }

    private static void DisableAutomaticLayout(
        RectTransform root)
    {
        var layout =
            root.GetComponent<VerticalLayoutGroup>();

        if (layout != null)
        {
            layout.enabled =
                false;
        }

        var fitter =
            root.GetComponent<ContentSizeFitter>();

        if (fitter != null)
        {
            fitter.enabled =
                false;
        }
    }

    private static void SetActive(
        Transform parent,
        string name,
        bool active)
    {
        var child =
            parent.Find(name);

        if (child != null)
        {
            child.gameObject.SetActive(
                active);
        }
    }

    private static void Assign(
        SerializedObject serialized,
        string propertyName,
        Object value)
    {
        var property =
            serialized.FindProperty(
                propertyName);

        if (property != null)
        {
            property.objectReferenceValue =
                value;
        }
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
        if (rect == null)
        {
            return;
        }

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

    private static void Stretch(
        RectTransform rect)
    {
        rect.anchorMin =
            Vector2.zero;

        rect.anchorMax =
            Vector2.one;

        rect.offsetMin =
            Vector2.zero;

        rect.offsetMax =
            Vector2.zero;

        rect.pivot =
            new Vector2(0.5f, 0.5f);
    }

    private static Color Hex(
        string value,
        float alpha)
    {
        ColorUtility.TryParseHtmlString(
            "#" + value,
            out var color);

        color.a =
            alpha;

        return color;
    }
}
