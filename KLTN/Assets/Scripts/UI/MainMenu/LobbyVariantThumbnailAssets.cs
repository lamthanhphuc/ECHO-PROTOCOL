using UnityEngine;

namespace EchoProtocol.UI.MainMenu
{
    public static class LobbyVariantThumbnailAssets
    {
        public static string GetCharacterOutfitResource(
            int characterId,
            int outfitId)
        {
            return "Shop/LobbyVariants/Characters/character-"
                + Mathf.Max(0, characterId)
                + "-outfit-"
                + Mathf.Max(0, outfitId);
        }

        public static string GetPetSkinResource(
            int petId,
            int skinId)
        {
            return "Shop/LobbyVariants/Pets/pet-"
                + Mathf.Max(0, petId)
                + "-skin-"
                + Mathf.Max(0, skinId);
        }
    }
}