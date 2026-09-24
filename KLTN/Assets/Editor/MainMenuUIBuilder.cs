using EchoProtocol.UI.MainMenu;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class MainMenuUIBuilder
{
    private static readonly Color Panel = Hex("071012", 0.84f);
    private static readonly Color Secondary = Hex("10191C", 0.94f);
    private static readonly Color Border = Hex("334247", 1f);
    private static readonly Color Red = Hex("B5322B", 1f);
    private static readonly Color RedHover = Hex("D7473E", 1f);
    private static readonly Color TextPrimary = Hex("F0F2F2", 1f);
    private static readonly Color TextSecondary = Hex("8F9A9D", 1f);
    private static readonly Color Credits = Hex("D3B56D", 1f);

    [MenuItem("ECHO PROTOCOL/Main Menu/Build Store Interface")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        var scene = SceneManager.GetActiveScene();
        if (scene.name != "MainMenu")
        {
            Debug.LogError("[MainMenuUIBuilder] Open the MainMenu scene in Edit mode first.");
            return;
        }

        var canvas = GameObject.Find("MainMenuCanvas");
        var controller = Object.FindAnyObjectByType<MainMenuProfileController>(FindObjectsInactive.Include);
        if (canvas == null || controller == null)
        {
            Debug.LogError("[MainMenuUIBuilder] MainMenuCanvas or MainMenuProfileController is missing.");
            return;
        }

        Undo.IncrementCurrentGroup();
        var group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Build Main Menu Store Interface");
        var canvasTransform = canvas.transform as RectTransform;
        var root = controller.transform as RectTransform;
        Undo.RegisterFullObjectHierarchyUndo(canvas, "Style Main Menu");

        var font = root.GetComponentInChildren<Text>(true)?.font ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        root.name = "MainMenuPanel";
        PlaceCenter(root, 100f, 0f, 600f, 480f);
        DisableLayout(root);
        StylePanel(root.gameObject, Panel);
        AddLine(root, "TopAccent", 0f, 0f, 600f, 3f, Red);

        var header = EnsureText(root, "HeaderText", font, 32, TextPrimary);
        PlaceTopLeft(header.rectTransform, 32f, 24f, 536f, 38f);
        header.text = "ECHO PROTOCOL";
        header.fontSize = 28;
        header.fontStyle = FontStyle.Bold;

        var system = EnsureText(root, "SystemLabel", font, 12, TextSecondary);
        PlaceTopLeft(system.rectTransform, 32f, 64f, 536f, 20f);
        system.text = "SYSTEM ACCESS";
        system.fontStyle = FontStyle.Bold;

        var divider = AddLine(root, "Divider", 32f, 96f, 536f, 1f, Border);
        divider.transform.SetAsLastSibling();

        var welcome = root.Find("WelcomeText")?.GetComponent<Text>();
        var player = EnsureText(root, "PlayerNameText", font, 24, TextPrimary);
        var role = root.Find("RoleText")?.GetComponent<Text>();
        var wallet = root.Find("WalletText")?.GetComponent<Text>();
        PlaceTopLeft(welcome?.rectTransform, 32f, 132f, 536f, 54f);
        PlaceTopLeft(player.rectTransform, 32f, 190f, 536f, 32f);
        PlaceTopLeft(role?.rectTransform, 32f, 224f, 536f, 20f);
        PlaceTopLeft(wallet?.rectTransform, 32f, 250f, 536f, 36f);
        if (welcome != null) { welcome.text = "WELCOME"; welcome.fontSize = 34; welcome.color = TextPrimary; welcome.alignment = TextAnchor.MiddleCenter; }
        player.text = "PLAYER_01"; player.fontSize = 18; player.fontStyle = FontStyle.Bold; player.alignment = TextAnchor.MiddleCenter; player.color = Hex("71C7E8", 1f);
        if (role != null) { role.text = "OPERATOR"; role.fontSize = 12; role.color = TextSecondary; role.alignment = TextAnchor.MiddleCenter; }
        if (wallet != null) { wallet.text = string.Empty; wallet.fontSize = 1; wallet.color = Color.clear; }

        var play = root.Find("PlayButton")?.GetComponent<Button>();
        var logout = root.Find("LogoutButton")?.GetComponent<Button>();
        StyleButton(play, "PLAY", Red, RedHover, root.Find("PlayButton")?.GetComponentInChildren<Text>());
        StyleButton(logout, "LOGOUT", Secondary, Border, root.Find("LogoutButton")?.GetComponentInChildren<Text>());
        PlaceTopLeft(play?.transform as RectTransform, 50f, 300f, 500f, 64f);
        PlaceTopLeft(logout?.transform as RectTransform, 50f, 380f, 500f, 52f);

        var store = EnsurePanel(canvasTransform, "StorePanel", 670f, 0f, 460f, 480f, Panel);
        var storeHeader = EnsureText(store, "HeaderText", font, 24, TextPrimary);
        PlaceTopLeft(storeHeader.rectTransform, 28f, 24f, 404f, 34f); storeHeader.text = "ACCOUNT"; storeHeader.fontStyle = FontStyle.Bold;
        AddLine(store, "Divider", 28f, 66f, 404f, 1f, Border);
        var coin = EnsurePanel(store, "CoinContainer", 28f, 94f, 404f, 124f, Secondary);
        PlaceTopLeft(coin, 28f, 94f, 404f, 124f);
        var icon = EnsureImage(coin, "CoinIcon", Credits); PlaceTopLeft(icon.rectTransform, 24f, 32f, 58f, 58f);
        var amount = EnsureText(coin, "CoinAmountText", font, 34, Credits); PlaceTopLeft(amount.rectTransform, 106f, 22f, 250f, 46f); amount.text = "1,250"; amount.fontStyle = FontStyle.Bold;
        var coinLabel = EnsureText(coin, "CoinLabelText", font, 14, TextSecondary); PlaceTopLeft(coinLabel.rectTransform, 108f, 72f, 250f, 24f); coinLabel.text = "ECHO CREDITS";
        var shop = CreateActionButton(store, "ShopButton", 28f, 250f, 404f, 70f, "SHOP", "Equipment & Cosmetics", font);
        var topUp = CreateActionButton(store, "TopUpButton", 28f, 338f, 404f, 70f, "+ TOP UP", "Acquire Credits", font);

        var shopPopup = CreateShopPopup(canvasTransform, font);
        var topUpPopup = CreateTopUpPopup(canvasTransform, font, out var package500, out var package1200, out var package2500, out var package5500, out var topUpClose);
        var shopClose = shopPopup.transform.Find("Window/CloseButton")?.GetComponent<Button>();

        // Keep the reference layout visible in edit mode; gameplay scripts can toggle these panels later.
        var shopWindow = shopPopup.transform.Find("Window") as RectTransform;
        var topUpWindow = topUpPopup.transform.Find("Window") as RectTransform;
        PlaceCenter(shopWindow, -350f, -315f, 920f, 390f);
        PlaceCenter(topUpWindow, 560f, -300f, 760f, 420f);
        shopPopup.transform.Find("Overlay")?.gameObject.SetActive(false);
        topUpPopup.transform.Find("Overlay")?.gameObject.SetActive(false);

        var serialized = new SerializedObject(controller);
        Assign(serialized, "welcomeText", welcome);
        Assign(serialized, "roleText", role);
        Assign(serialized, "walletText", wallet);
        Assign(serialized, "playerNameText", player);
        Assign(serialized, "playButton", play);
        Assign(serialized, "logoutButton", logout);
        Assign(serialized, "shopButton", shop);
        Assign(serialized, "topUpButton", topUp);
        Assign(serialized, "shopPopup", shopPopup);
        Assign(serialized, "topUpPopup", topUpPopup);
        Assign(serialized, "shopCloseButton", shopClose);
        Assign(serialized, "topUpCloseButton", topUpClose);
        Assign(serialized, "package500Button", package500);
        Assign(serialized, "package1200Button", package1200);
        Assign(serialized, "package2500Button", package2500);
        Assign(serialized, "package5500Button", package5500);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        shopPopup.SetActive(false);
        topUpPopup.SetActive(false);
        EditorSceneManager.MarkSceneDirty(scene);
        Undo.CollapseUndoOperations(group);
        Selection.activeGameObject = root.gameObject;
        Debug.Log("[MainMenuUIBuilder] Main menu store interface built. Save MainMenu with Ctrl+S.");
    }

    private static GameObject CreateShopPopup(RectTransform canvas, Font font)
    {
        var popup = EnsureObject(canvas, "ShopPopup");
        Stretch(popup.GetComponent<RectTransform>());
        var overlay = EnsureImage(popup.transform, "Overlay", new Color(0f, 0f, 0f, 0.72f));
        Stretch(overlay.rectTransform);
        var window = EnsurePanel(popup.transform, "Window", 0f, 0f, 920f, 390f, Panel);
        Center(window, 0f, -20f, 920f, 390f);
        var header = EnsureText(window, "HeaderText", font, 26, TextPrimary); PlaceTopLeft(header.rectTransform, 32f, 24f, 820f, 38f); header.text = "STORE TERMINAL"; header.fontStyle = FontStyle.Bold;
        var status = EnsureText(window, "PlaceholderText", font, 13, TextSecondary); PlaceTopLeft(status.rectTransform, 32f, 62f, 820f, 22f); status.text = "EQUIPMENT AND COSMETICS"; status.alignment = TextAnchor.MiddleLeft;
        var categories = new[] { "ALL", "CHARACTER", "EQUIPMENT", "COSMETICS" };
        for (var i = 0; i < categories.Length; i++)
            CreateSimpleButton(window, "Category" + categories[i], 32f, 104f + i * 48f, 148f, 38f, categories[i], font, i == 0 ? Red : Secondary, Border);
        CreateProductCard(window, "HazmatCard", 204f, 104f, "HAZMAT SUIT", "500", font);
        CreateProductCard(window, "FlashlightCard", 374f, 104f, "TACTICAL LIGHT", "800", font);
        CreateProductCard(window, "BackpackCard", 544f, 104f, "SURVIVAL PACK", "1,200", font);
        CreateProductCard(window, "GasMaskCard", 714f, 104f, "GAS MASK", "1,500", font);
        var close = CreateSimpleButton(window, "CloseButton", 32f, 334f, 856f, 36f, "CLOSE", font, Secondary, Border);
        return popup;
    }

    private static void CreateProductCard(Transform parent, string name, float x, float y, string title, string price, Font font)
    {
        var card = EnsurePanel(parent, name, x, y, 154f, 212f, Secondary);
        PlaceTopLeft(card, x, y, 154f, 212f);
        var art = EnsureImage(card, "ItemPreview", new Color(0.05f, 0.08f, 0.09f, 1f)); PlaceTopLeft(art.rectTransform, 10f, 10f, 134f, 108f);
        var label = EnsureText(card, "TitleText", font, 11, TextPrimary); PlaceTopLeft(label.rectTransform, 10f, 126f, 134f, 22f); label.text = title; label.alignment = TextAnchor.MiddleCenter;
        var cost = EnsureText(card, "PriceText", font, 14, Credits); PlaceTopLeft(cost.rectTransform, 10f, 151f, 134f, 22f); cost.text = price + " CREDITS"; cost.alignment = TextAnchor.MiddleCenter;
        CreateSimpleButton(card, "BuyButton", 10f, 180f, 134f, 28f, "BUY", font, Red, RedHover);
    }

    private static GameObject CreateTopUpPopup(RectTransform canvas, Font font, out Button package500, out Button package1200, out Button package2500, out Button package5500, out Button close)
    {
        var popup = EnsureObject(canvas, "TopUpPopup"); Stretch(popup.GetComponent<RectTransform>());
        var overlay = EnsureImage(popup.transform, "Overlay", new Color(0f, 0f, 0f, 0.72f)); Stretch(overlay.rectTransform);
        var window = EnsurePanel(popup.transform, "Window", 0f, 0f, 760f, 420f, Panel); Center(window, 0f, 0f, 760f, 420f);
        var header = EnsureText(window, "HeaderText", font, 26, TextPrimary); PlaceTopLeft(header.rectTransform, 32f, 28f, 580f, 38f); header.text = "CREDIT ACQUISITION"; header.fontStyle = FontStyle.Bold;
        var current = EnsureText(window, "CurrentCreditsText", font, 15, Credits); PlaceTopLeft(current.rectTransform, 32f, 78f, 580f, 26f); current.text = "CURRENT BALANCE IS SHOWN IN ACCOUNT";
        package500 = CreateSimpleButton(window, "Package500Button", 32f, 126f, 168f, 156f, "500\nCREDITS", font, Secondary, Border);
        package1200 = CreateSimpleButton(window, "Package1200Button", 216f, 126f, 168f, 156f, "1,200\nCREDITS", font, Secondary, Border);
        package2500 = CreateSimpleButton(window, "Package2500Button", 400f, 126f, 168f, 156f, "2,500\nCREDITS", font, Secondary, Border);
        package5500 = CreateSimpleButton(window, "Package5500Button", 584f, 126f, 144f, 156f, "5,500\nCREDITS", font, Secondary, Border);
        close = CreateSimpleButton(window, "CloseButton", 32f, 346f, 696f, 44f, "CLOSE", font, Secondary, Border);
        return popup;
    }

    private static Button CreateActionButton(Transform parent, string name, float x, float y, float w, float h, string title, string subtitle, Font font)
    {
        var button = CreateSimpleButton(parent, name, x, y, w, h, title, font, Secondary, Border);
        var titleText = button.transform.Find("TitleText")?.GetComponent<Text>();
        var defaultText = button.transform.Find("Text")?.GetComponent<Text>();
        if (titleText == null && defaultText != null)
        {
            defaultText.name = "TitleText";
            titleText = defaultText;
        }
        else if (titleText != null && defaultText != null && defaultText != titleText)
        {
            Undo.DestroyObjectImmediate(defaultText.gameObject);
        }
        titleText ??= CreateText(button.transform, "TitleText", font, 16, TextPrimary);
        PlaceTopLeft(titleText.rectTransform, 18f, 10f, w - 36f, 22f); titleText.text = title; titleText.fontStyle = FontStyle.Bold;
        var subtitleText = button.transform.Find("SubtitleText")?.GetComponent<Text>() ?? CreateText(button.transform, "SubtitleText", font, 11, TextSecondary);
        PlaceTopLeft(subtitleText.rectTransform, 18f, 34f, w - 36f, 18f); subtitleText.text = subtitle;
        return button;
    }

    private static Button CreateSimpleButton(Transform parent, string name, float x, float y, float w, float h, string label, Font font, Color normal, Color hover)
    {
        var go = EnsureObject(parent, name);
        var rect = go.GetComponent<RectTransform>(); PlaceTopLeft(rect, x, y, w, h);
        var image = EnsureImage(go.transform, "Background", normal);
        Stretch(image.rectTransform);
        image.transform.SetAsFirstSibling();
        image.raycastTarget = true;
        var button = go.GetComponent<Button>() ?? Undo.AddComponent<Button>(go);
        button.targetGraphic = image;
        var colors = button.colors; colors.normalColor = normal; colors.highlightedColor = hover; colors.selectedColor = hover; colors.pressedColor = Red; colors.disabledColor = normal * 0.45f; button.colors = colors;
        var text = EnsureText(go.transform, "Text", font, 15, TextPrimary); PlaceTopLeft(text.rectTransform, 12f, 0f, w - 24f, h); text.text = label; text.alignment = TextAnchor.MiddleCenter; text.fontStyle = FontStyle.Bold;
        if (go.GetComponent<UIHoverEffect>() == null) Undo.AddComponent<UIHoverEffect>(go);
        return button;
    }

    private static RectTransform EnsurePanel(Transform parent, string name, float x, float y, float w, float h, Color color)
    {
        var go = EnsureObject(parent, name); var rect = go.GetComponent<RectTransform>(); PlaceCenter(rect, x, y, w, h); StylePanel(go, color); return rect;
    }

    private static void StyleButton(Button button, string label, Color normal, Color hover, Text text)
    {
        if (button == null) return;
        var image = button.targetGraphic as Image ?? button.GetComponent<Image>();
        if (image != null) image.color = normal;
        var colors = button.colors; colors.normalColor = normal; colors.highlightedColor = hover; colors.selectedColor = hover; colors.pressedColor = Red; button.colors = colors;
        if (text != null) { text.text = label; text.color = TextPrimary; text.fontStyle = FontStyle.Bold; text.alignment = TextAnchor.MiddleCenter; }
        if (button.GetComponent<UIHoverEffect>() == null) Undo.AddComponent<UIHoverEffect>(button.gameObject);
    }

    private static void StylePanel(GameObject go, Color color)
    {
        var image = EnsureImage(go.transform, "PanelBackground", color);
        Stretch(image.rectTransform);
        image.transform.SetAsFirstSibling();
        image.raycastTarget = false;
        var outline = go.GetComponent<Outline>() ?? Undo.AddComponent<Outline>(go); outline.effectColor = Border; outline.effectDistance = new Vector2(1f, -1f);
    }

    private static Image AddLine(Transform parent, string name, float x, float y, float w, float h, Color color)
    {
        var image = EnsureImage(parent, name, color); PlaceTopLeft(image.rectTransform, x, y, w, h); image.raycastTarget = false; return image;
    }

    private static Text EnsureText(Transform parent, string name, Font font, int size, Color color)
    {
        var existing = parent.Find(name)?.GetComponent<Text>();
        if (existing != null) return existing;
        return CreateText(parent, name, font, size, color);
    }

    private static Text CreateText(Transform parent, string name, Font font, int size, Color color)
    {
        var go = EnsureObject(parent, name); var text = go.GetComponent<Text>() ?? Undo.AddComponent<Text>(go);
        text.font = font; text.fontSize = size; text.color = color; text.raycastTarget = false; text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Overflow; return text;
    }

    private static Image EnsureImage(Transform parent, string name, Color color)
    {
        var go = EnsureObject(parent, name); var image = go.GetComponent<Image>() ?? Undo.AddComponent<Image>(go); image.color = color; return image;
    }

    private static GameObject EnsureObject(Transform parent, string name)
    {
        var existing = parent.Find(name);
        if (existing != null) return existing.gameObject;
        var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false); Undo.RegisterCreatedObjectUndo(go, "Create Main Menu UI"); return go;
    }

    private static void DisableLayout(RectTransform rect)
    {
        var fitter = rect.GetComponent<ContentSizeFitter>(); if (fitter != null) fitter.enabled = false;
        var layout = rect.GetComponent<VerticalLayoutGroup>(); if (layout != null) layout.enabled = false;
    }

    private static void Assign(SerializedObject serialized, string name, Object value)
    {
        var property = serialized.FindProperty(name); if (property != null) property.objectReferenceValue = value;
    }

    private static void PlaceCenter(RectTransform rect, float x, float y, float w, float h)
    {
        if (rect == null) return; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f); rect.anchoredPosition = new Vector2(x, y); rect.sizeDelta = new Vector2(w, h);
    }

    private static void Center(RectTransform rect, float x, float y, float w, float h) => PlaceCenter(rect, x, y, w, h);

    private static void PlaceTopLeft(RectTransform rect, float x, float y, float w, float h)
    {
        if (rect == null) return; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f); rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(w, h);
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero; rect.pivot = new Vector2(0.5f, 0.5f);
    }

    private static Color Hex(string value, float alpha)
    {
        ColorUtility.TryParseHtmlString("#" + value, out var color); color.a = alpha; return color;
    }
}
