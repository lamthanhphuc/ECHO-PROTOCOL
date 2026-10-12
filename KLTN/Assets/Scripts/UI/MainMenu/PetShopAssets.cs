using System;
using EchoProtocol.Pets;

namespace EchoProtocol.UI.MainMenu
{
    public static class PetShopAssets
    {
        public readonly struct PetItem
        {
            public readonly int PetId;
            public readonly string ItemId;
            public readonly string Name;
            public readonly string Description;
            public readonly string PrefabResource;
            public readonly int PreviewPrice;

            public PetItem(
                int petId,
                string itemId,
                string name,
                string description,
                string prefabResource,
                int previewPrice)
            {
                PetId = petId;
                ItemId = itemId;
                Name = name;
                Description = description;
                PrefabResource = prefabResource;
                PreviewPrice = previewPrice;
            }
        }

        public readonly struct SkinItem
        {
            public readonly int PetId;
            public readonly int SkinId;
            public readonly string ItemId;
            public readonly string Name;
            public readonly string MaterialResource;
            public readonly int PreviewPrice;
            public readonly bool DefaultOwned;

            public SkinItem(
                int petId,
                int skinId,
                string itemId,
                string name,
                int previewPrice,
                bool defaultOwned)
            {
                PetId = petId;
                SkinId = skinId;
                ItemId = itemId;
                Name = name;
                PreviewPrice = previewPrice;
                DefaultOwned = defaultOwned;

                MaterialResource =
                    PetSkinLibrary.GetResourcePath(
                        petId,
                        skinId);
            }
        }

        public static readonly PetItem[] Pets =
        {
            new(
                1,
                "14000000-0000-0000-0000-000000000001",
                "NIGHTMARE",
                "Ground companion with agile movement and idle animations.",
                "Pets/PF_Pet_DragonNightmare",
                700),

            new(
                2,
                "14000000-0000-0000-0000-000000000002",
                "SOUL EATER",
                "Flying cosmetic companion configured for expedition support.",
                "Pets/PF_Pet_DragonSoulEater",
                800),

            new(
                3,
                "14000000-0000-0000-0000-000000000003",
                "TERROR BRINGER",
                "Large flying cosmetic companion with synchronized movement.",
                "Pets/PF_Pet_DragonTerrorBringer",
                900),

            new(
                4,
                "14000000-0000-0000-0000-000000000004",
                "USURPER",
                "Premium flying cosmetic companion.",
                "Pets/PF_Pet_DragonUsurper",
                1000)
        };

        public static readonly SkinItem[] Skins =
        {
            // Nightmare
            new(1, 0, null, "GREEN", 0, true),
            new(1, 1, "14100000-0000-0000-0000-000000000101", "ALBINO", 150, false),
            new(1, 2, "14100000-0000-0000-0000-000000000102", "BLUE", 150, false),
            new(1, 3, "14100000-0000-0000-0000-000000000103", "RED", 150, false),

            // Soul Eater
            new(2, 0, null, "PURPLE", 0, true),
            new(2, 1, "14100000-0000-0000-0000-000000000201", "BLUE", 150, false),
            new(2, 2, "14100000-0000-0000-0000-000000000202", "GREEN", 150, false),
            new(2, 3, "14100000-0000-0000-0000-000000000203", "RED", 150, false),

            // Terror Bringer
            new(3, 0, null, "GREY", 0, true),
            new(3, 1, "14100000-0000-0000-0000-000000000301", "ALBINO", 150, false),
            new(3, 2, "14100000-0000-0000-0000-000000000302", "GREEN", 150, false),
            new(3, 3, "14100000-0000-0000-0000-000000000303", "RED", 150, false),

            // Usurper
            new(4, 0, null, "RED", 0, true),
            new(4, 1, "14100000-0000-0000-0000-000000000401", "ALBINO", 150, false),
            new(4, 2, "14100000-0000-0000-0000-000000000402", "BLACK", 150, false),
            new(4, 3, "14100000-0000-0000-0000-000000000403", "BLUE", 150, false)
        };

        public static bool TryGetPet(
            int petId,
            out PetItem item)
        {
            for (int i = 0; i < Pets.Length; i++)
            {
                if (Pets[i].PetId == petId)
                {
                    item = Pets[i];
                    return true;
                }
            }

            item = default;
            return false;
        }

        public static bool TryGetPetByItemId(
            string itemId,
            out PetItem item)
        {
            if (!string.IsNullOrWhiteSpace(itemId))
            {
                for (int i = 0; i < Pets.Length; i++)
                {
                    if (string.Equals(
                            Pets[i].ItemId,
                            itemId,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        item = Pets[i];
                        return true;
                    }
                }
            }

            item = default;
            return false;
        }

        public static bool TryGetSkin(
            int petId,
            int skinId,
            out SkinItem item)
        {
            for (int i = 0; i < Skins.Length; i++)
            {
                if (Skins[i].PetId == petId
                    && Skins[i].SkinId == skinId)
                {
                    item = Skins[i];
                    return true;
                }
            }

            item = default;
            return false;
        }

        public static bool TryGetSkinByItemId(
            string itemId,
            out SkinItem item)
        {
            if (!string.IsNullOrWhiteSpace(itemId))
            {
                for (int i = 0; i < Skins.Length; i++)
                {
                    if (string.Equals(
                            Skins[i].ItemId,
                            itemId,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        item = Skins[i];
                        return true;
                    }
                }
            }

            item = default;
            return false;
        }

        public static bool IsPetOwned(
            int petId)
        {
            return TryGetPet(
                       petId,
                       out var item)
                   && TeamToolOwnershipSession
                       .OwnsItem(item.ItemId);
        }

        public static bool IsSkinOwned(
            int petId,
            int skinId)
        {
            if (!IsPetOwned(petId))
                return false;

            if (!TryGetSkin(
                    petId,
                    skinId,
                    out var skin))
            {
                return false;
            }

            return skin.DefaultOwned
                || TeamToolOwnershipSession
                    .OwnsItem(skin.ItemId);
        }
    }
}
