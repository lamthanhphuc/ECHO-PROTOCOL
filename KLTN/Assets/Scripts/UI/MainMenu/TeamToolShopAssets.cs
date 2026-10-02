namespace EchoProtocol.UI.MainMenu
{
    /// <summary>Stable shop identities and the imported prefabs used to bake their thumbnails.</summary>
    public static class TeamToolShopAssets
    {
        public readonly struct Item
        {
            public readonly string ItemId;
            public readonly string Name;
            public readonly string PrefabPath;
            public readonly string ThumbnailResource;

            public Item(string itemId, string name, string prefab, string thumbnail)
            {
                ItemId = itemId;
                Name = name;
                PrefabPath = "Assets/Prefabs/Gameplay/Imported/" + prefab + ".prefab";
                ThumbnailResource = "Shop/Thumbnails/" + thumbnail;
            }
        }

        public static readonly Item[] Items =
        {
            new Item("12000000-0000-0000-0000-000000000003", "FIRST KIT", "PF_TeamToolPickup_FirstAid", "first-kit"),
            new Item("12000000-0000-0000-0000-000000000002", "NOISE MAKER", "PF_TeamToolPickup_NoiseMaker", "noise-maker"),
            new Item("12000000-0000-0000-0000-000000000001", "SCANNER", "PF_Scanner_Imported", "scanner"),
            new Item("12000000-0000-0000-0000-000000000005", "DISTRESS BEACON", "DistressBeaconDeployed", "distress-beacon")
        };
    }
}
