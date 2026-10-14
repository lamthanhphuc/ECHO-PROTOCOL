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
                    return "Shop/LobbyVariants/Pets/pet-1-skin-0";

                case 2:
                    return "Shop/LobbyVariants/Pets/pet-2-skin-0";

                case 3:
                    return "Shop/LobbyVariants/Pets/pet-3-skin-0";

                case 4:
                    return "Shop/LobbyVariants/Pets/pet-4-skin-0";

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