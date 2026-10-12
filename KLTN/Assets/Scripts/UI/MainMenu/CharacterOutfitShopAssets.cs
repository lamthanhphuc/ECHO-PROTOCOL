namespace EchoProtocol.UI.MainMenu
{
    /// <summary>
    /// Stable mapping between Character visual variants and shop inventory
    /// item ids. Outfit 0 is bundled with the base Character; outfits 1..3
    /// are purchasable cosmetic inventory items.
    /// </summary>
    public static class CharacterOutfitShopAssets
    {
        public readonly struct Item
        {
            public readonly int CharacterId;
            public readonly int OutfitId;
            public readonly string ItemId;
            public readonly string Name;
            public readonly int PreviewPrice;
            public readonly bool DefaultOwned;

            public Item(
                int characterId,
                int outfitId,
                string itemId,
                string name,
                int previewPrice,
                bool defaultOwned)
            {
                CharacterId = characterId;
                OutfitId = outfitId;
                ItemId = itemId;
                Name = name;
                PreviewPrice = previewPrice;
                DefaultOwned = defaultOwned;
            }
        }

        public static readonly Item[] Items =
        {
            // Astronaut
            new(
                CharacterShopAssets.AstronautCharacterId,
                0,
                null,
                "DEFAULT",
                0,
                true),

            new(
                CharacterShopAssets.AstronautCharacterId,
                1,
                "13100000-0000-0000-0000-000000000001",
                "ORANGE",
                150,
                false),

            new(
                CharacterShopAssets.AstronautCharacterId,
                2,
                "13100000-0000-0000-0000-000000000002",
                "GREEN",
                150,
                false),

            new(
                CharacterShopAssets.AstronautCharacterId,
                3,
                "13100000-0000-0000-0000-000000000003",
                "PURPLE",
                150,
                false),

            // Jammo
            new(
                CharacterShopAssets.JammoCharacterId,
                0,
                null,
                "DEFAULT",
                0,
                true),

            new(
                CharacterShopAssets.JammoCharacterId,
                1,
                "13100000-0000-0000-0000-000000000101",
                "BLACK",
                150,
                false),

            new(
                CharacterShopAssets.JammoCharacterId,
                2,
                "13100000-0000-0000-0000-000000000102",
                "BLUE",
                150,
                false),

            new(
                CharacterShopAssets.JammoCharacterId,
                3,
                "13100000-0000-0000-0000-000000000103",
                "YELLOW",
                150,
                false)
        };

        public static bool TryGet(
            int characterId,
            int outfitId,
            out Item item)
        {
            for (int i = 0; i < Items.Length; i++)
            {
                if (Items[i].CharacterId == characterId
                    && Items[i].OutfitId == outfitId)
                {
                    item = Items[i];
                    return true;
                }
            }

            item = default;
            return false;
        }

        public static bool IsOwned(
            int characterId,
            int outfitId)
        {
            // Character ownership is always the parent entitlement.
            if (!CharacterShopAssets.IsOwned(characterId))
                return false;

            if (!TryGet(
                    characterId,
                    outfitId,
                    out var outfit))
            {
                return false;
            }

            if (outfit.DefaultOwned)
                return true;

            return TeamToolOwnershipSession
                .OwnsItem(outfit.ItemId);
        }
    }
}