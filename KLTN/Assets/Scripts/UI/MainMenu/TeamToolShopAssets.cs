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
            public readonly string PrefabPath;
            public readonly string ThumbnailResource;

            public Item(
                int toolId,
                string itemId,
                string name,
                string prefabPath,
                string thumbnail)
            {
                ToolId = toolId;
                ItemId = itemId;
                Name = name;
                PrefabPath = prefabPath;
                ThumbnailResource =
                    "Shop/Thumbnails/" + thumbnail;
            }
        }

        public static readonly Item[] Items =
        {
            new Item(
                LobbyPlayerState.FieldScannerToolId,
                "12000000-0000-0000-0000-000000000001",
                "FIELD SCANNER",
                "Assets/Prefabs/Tools/PF_FieldScanner_Pickup.prefab",
                "scanner"),

            new Item(
                LobbyPlayerState.NoiseMakerToolId,
                "12000000-0000-0000-0000-000000000002",
                "NOISE MAKER",
                "Assets/Prefabs/Gameplay/Imported/PF_TeamToolPickup_NoiseMaker.prefab",
                "noise-maker"),

            new Item(
                LobbyPlayerState.DoorJammerToolId,
                "12000000-0000-0000-0000-000000000004",
                "DOOR JAMMER",
                "Assets/Prefabs/Gameplay/Imported/PF_Plank_Imported.prefab",
                "door-jammer"),

            new Item(
                LobbyPlayerState.CoreStabilizerToolId,
                "12000000-0000-0000-0000-000000000006",
                "CORE STABILIZER",
                "Assets/Prefabs/Environment/Teamtoools/Gameplay/PF_CoreStabilizer_Pickup.prefab",
                "core-stabilizer")
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
