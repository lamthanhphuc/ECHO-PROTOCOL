using UnityEngine;

namespace EchoProtocol.UI.MainMenu
{
    /// <summary>
    /// Shared cosmetic outfit mapping for lobby/store presentation.
    /// OutfitId is independent from TeamId.
    /// </summary>
    public static class CharacterOutfitLibrary
    {
        public const int OutfitCount = 4;

        private static readonly string[] AstronautNames =
        {
            "SILVER",
            "ORANGE",
            "GREEN",
            "PURPLE",
        };

        private static readonly string[] JammoNames =
        {
            "DEFAULT",
            "BLACK",
            "BLUE",
            "YELLOW",
        };


        public static string GetName(
            int characterId,
            int outfitId)
        {
            int index =
                Mathf.Clamp(
                    outfitId,
                    0,
                    OutfitCount - 1);

            return characterId == 1
                ? JammoNames[index]
                : AstronautNames[index];
        }


        public static Color32 GetSwatch(
            int characterId,
            int outfitId)
        {
            int index =
                Mathf.Clamp(
                    outfitId,
                    0,
                    OutfitCount - 1);

            if (characterId == 1)
            {
                switch (index)
                {
                    case 1:
                        return new Color32(
                            35, 37, 39, 255);

                    case 2:
                        return new Color32(
                            52, 87, 139, 255);

                    case 3:
                        return new Color32(
                            187, 151, 53, 255);

                    default:
                        return new Color32(
                            177, 66, 105, 255);
                }
            }

            switch (index)
            {
                case 1:
                    return new Color32(
                        205, 121, 63, 255);

                case 2:
                    return new Color32(
                        71, 125, 82, 255);

                case 3:
                    return new Color32(
                        113, 77, 139, 255);

                default:
                    return new Color32(
                        190, 195, 198, 255);
            }
        }
    }
}