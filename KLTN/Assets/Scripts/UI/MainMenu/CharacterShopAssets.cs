using System;

namespace EchoProtocol.UI.MainMenu
{
    /// <summary>
    /// Stable Character shop/runtime mapping.
    /// </summary>
    public static class CharacterShopAssets
    {
        public const int AstronautCharacterId = 0;
        public const int JammoCharacterId = 1;

        public const string BuiltinAstronautItemId =
            "builtin:astronaut";

        public const string JammoItemId =
            "13000000-0000-0000-0000-000000000001";

        public readonly struct Item
        {
            public readonly int CharacterId;
            public readonly string ItemId;
            public readonly string Name;
            public readonly string Description;

            // Editor source prefab used to generate preview assets.
            public readonly string PrefabPath;

            // Static fallback/card thumbnail.
            public readonly string ThumbnailResource;

            // Runtime safe visual-only prefab.
            public readonly string LivePreviewResource;

            public readonly int PreviewPrice;
            public readonly int OutfitCount;
            public readonly bool DefaultOwned;

            public Item(
                int characterId,
                string itemId,
                string name,
                string description,
                string prefabPath,
                string thumbnailResource,
                string livePreviewResource,
                int previewPrice,
                int outfitCount,
                bool defaultOwned)
            {
                CharacterId = characterId;
                ItemId = itemId;
                Name = name;
                Description = description;
                PrefabPath = prefabPath;
                ThumbnailResource = thumbnailResource;
                LivePreviewResource = livePreviewResource;
                PreviewPrice = previewPrice;
                OutfitCount = outfitCount;
                DefaultOwned = defaultOwned;
            }
        }

        public static readonly Item[] Items =
        {
            new Item(
                AstronautCharacterId,
                BuiltinAstronautItemId,
                "ASTRONAUT",
                "Standard expedition operator suit.",
                "Assets/Prefabs/Player/Variants/PF_PlayerCharacter_P1_Default.prefab",
                "Shop/Characters/astronaut",
                "Shop/LivePreview/Character_Astronaut",
                0,
                1,
                true),

            new Item(
                JammoCharacterId,
                JammoItemId,
                "JAMMO",
                "Compact expedition unit configured for hazardous operations.",
                "Assets/Resources/Characters/PF_JammoVisual.prefab",
                "Shop/Characters/jammo",
                "Shop/LivePreview/Character_Jammo",
                600,
                4,
                false)
        };

        public static bool TryGetByCharacterId(
            int characterId,
            out Item item)
        {
            for (int i = 0; i < Items.Length; i++)
            {
                if (Items[i].CharacterId == characterId)
                {
                    item = Items[i];
                    return true;
                }
            }

            item = default;
            return false;
        }

        public static bool TryGetByItemId(
            string itemId,
            out Item item)
        {
            if (!string.IsNullOrWhiteSpace(itemId))
            {
                for (int i = 0; i < Items.Length; i++)
                {
                    if (string.Equals(
                            Items[i].ItemId,
                            itemId,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        item = Items[i];
                        return true;
                    }
                }
            }

            item = default;
            return false;
        }

        public static bool IsOwned(
            int characterId)
        {
            if (!TryGetByCharacterId(
                    characterId,
                    out var item))
            {
                return false;
            }

            return item.DefaultOwned
                || TeamToolOwnershipSession
                    .OwnsItem(item.ItemId);
        }
    }
}
