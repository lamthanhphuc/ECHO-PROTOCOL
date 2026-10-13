namespace EchoProtocol.UI.MainMenu
{
    public static class PetShopThumbnailAssets
    {
        public static string GetThumbnailResource(
            int petId)
        {
            switch (petId)
            {
                case 1:
                    return "Shop/Pets/nightmare";

                case 2:
                    return "Shop/Pets/soul-eater";

                case 3:
                    return "Shop/Pets/terror-bringer";

                case 4:
                    return "Shop/Pets/usurper";

                default:
                    return null;
            }
        }

        public static string GetPrefabAssetPath(
            int petId)
        {
            if (!PetShopAssets.TryGetPet(
                    petId,
                    out var pet))
            {
                return null;
            }

            return "Assets/Resources/" +
                pet.PrefabResource +
                ".prefab";
        }
    }
}