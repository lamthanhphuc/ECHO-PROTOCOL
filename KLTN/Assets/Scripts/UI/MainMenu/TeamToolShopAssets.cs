using EchoProtocol.Networking;

namespace EchoProtocol.UI.MainMenu
{
    public static class TeamToolShopAssets
    {
        public readonly struct Item
        {
            public readonly int ToolId;
            public readonly string ItemId;
            public readonly string Name;

            // Editor source prefab.
            public readonly string PrefabPath;

            // Existing static thumbnail fallback/card image.
            public readonly string ThumbnailResource;

            // Generated runtime-safe visual prefab.
            public readonly string LivePreviewResource;

            public Item(
                int toolId,
                string itemId,
                string name,
                string prefabPath,
                string thumbnail,
                string livePreviewResource)
            {
                ToolId = toolId;
                ItemId = itemId;
                Name = name;
                PrefabPath = prefabPath;

                ThumbnailResource =
                    "Shop/Thumbnails/" + thumbnail;

                LivePreviewResource =
                    livePreviewResource;
            }
        }

        public static readonly Item[] Items =
        {
            new Item(
                LobbyPlayerState.FieldScannerToolId,
                "12000000-0000-0000-0000-000000000001",
                "FIELD SCANNER",
                "Assets/Prefabs/Tools/PF_FieldScanner_Pickup.prefab",
                "scanner",
                "Shop/LivePreview/Tool_FieldScanner"),

            new Item(
                LobbyPlayerState.NoiseMakerToolId,
                "12000000-0000-0000-0000-000000000002",
                "NOISE MAKER",
                "Assets/Prefabs/Gameplay/Imported/PF_TeamToolPickup_NoiseMaker.prefab",
                "noise-maker",
                "Shop/LivePreview/Tool_NoiseMaker"),

            new Item(
                LobbyPlayerState.DoorJammerToolId,
                "12000000-0000-0000-0000-000000000004",
                "DOOR JAMMER",
                "Assets/Prefabs/Gameplay/Imported/PF_Plank_Imported.prefab",
                "door-jammer",
                "Shop/LivePreview/Tool_DoorJammer"),

            new Item(
                LobbyPlayerState.CoreStabilizerToolId,
                "12000000-0000-0000-0000-000000000006",
                "CORE STABILIZER",
                "Assets/Prefabs/Environment/Teamtoools/Gameplay/PF_CoreStabilizer_Pickup.prefab",
                "core-stabilizer",
                "Shop/LivePreview/Tool_CoreStabilizer")
        };

        public static bool TryGetByToolId(
            int toolId,
            out Item item)
        {
            for (int i = 0; i < Items.Length; i++)
            {
                if (Items[i].ToolId == toolId)
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
                            System.StringComparison.OrdinalIgnoreCase))
                    {
                        item = Items[i];
                        return true;
                    }
                }
            }

            item = default;
            return false;
        }
    }
}
