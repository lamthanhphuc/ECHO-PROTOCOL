using UnityEngine;
using UnityEngine.UI;

namespace EchoProtocol.UI.MainMenu
{
    /// <summary>
    /// Instantiates Store V2 cards from prefab-authored visual templates.
    /// Runtime code only binds data and listeners; visual styling stays in
    /// StoreV2Popup.prefab.
    /// </summary>
    internal static class StoreV2PrefabCardFactory
    {
        private const string CatalogTemplatePath =
            "Window/StoreVisualTemplates/CatalogGridTemplate/CardTemplate";

        private const string VariantTemplatePath =
            "Window/StoreVisualTemplates/VariantGridTemplate/CardTemplate";

        public static GameObject CreateCatalogCard(
            Transform storeRoot,
            Transform parent,
            string cardName,
            string thumbnailResource,
            Object context)
        {
            Texture texture =
                string.IsNullOrWhiteSpace(thumbnailResource)
                    ? null
                    : Resources.Load<Texture2D>(
                        thumbnailResource);

            return CreateCard(
                storeRoot,
                parent,
                CatalogTemplatePath,
                cardName,
                texture,
                context);
        }

        public static GameObject CreateVariantCard(
            Transform storeRoot,
            Transform parent,
            string cardName,
            Texture texture,
            Object context)
        {
            return CreateCard(
                storeRoot,
                parent,
                VariantTemplatePath,
                cardName,
                texture,
                context);
        }

        private static GameObject CreateCard(
            Transform storeRoot,
            Transform parent,
            string templatePath,
            string cardName,
            Texture texture,
            Object context)
        {
            if (storeRoot == null || parent == null)
                return null;

            Transform template =
                storeRoot.Find(templatePath);

            if (template == null)
            {
                Debug.LogError(
                    "[StoreV2] Missing prefab-authored card template: "
                    + templatePath,
                    context);

                return null;
            }

            if(templatePath==CatalogTemplatePath)parent=StoreReferencePresentation.CatalogParent(parent);
            GameObject card =
                Object.Instantiate(
                    template.gameObject,
                    parent,
                    false);

            card.name = cardName;
            card.SetActive(true);

            RawImage thumbnail =
                card.GetComponentInChildren<RawImage>(
                    true);

            if (thumbnail == null)
            {
                Debug.LogError(
                    "[StoreV2] Card template is missing RawImage Thumbnail: "
                    + templatePath,
                    context);

                Object.Destroy(card);
                return null;
            }

            Button button =
                card.GetComponent<Button>();

            if (button == null)
            {
                Debug.LogError(
                    "[StoreV2] Card template is missing Button: "
                    + templatePath,
                    context);

                Object.Destroy(card);
                return null;
            }

            // Data binding only. Color, raycast, outline, rect and Button
            // transition values are authored in StoreV2Popup.prefab.
            thumbnail.texture = texture;

            button.onClick.RemoveAllListeners();
            if(templatePath==CatalogTemplatePath)button.onClick.AddListener(()=>StoreReferencePresentation.ChooseModel(storeRoot));
            EchoProtocol.Audio.GameAudioRuntime.RegisterButton(button);
            StoreReferencePresentation.BindCard(card);

            return card;
        }
    }
}
