using System.Collections.Generic;
using EchoProtocol.Pets;
using UnityEngine;
using UnityEngine.UI;

namespace EchoProtocol.UI.MainMenu
{
    public static class LobbyModelThumbnailUtility
    {
        private static readonly Dictionary<string, Sprite>
            SpriteCache =
                new Dictionary<string, Sprite>();

        public static Sprite LoadSpriteResource(
            string resourcePath)
        {
            if (string.IsNullOrWhiteSpace(resourcePath))
                return null;

            if (SpriteCache.TryGetValue(
                    resourcePath,
                    out var cached)
                && cached != null)
            {
                return cached;
            }

            var sprite =
                Resources.Load<Sprite>(
                    resourcePath);

            if (sprite == null)
            {
                var texture =
                    Resources.Load<Texture2D>(
                        resourcePath);

                if (texture != null)
                {
                    sprite =
                        Sprite.Create(
                            texture,
                            new Rect(
                                0f,
                                0f,
                                texture.width,
                                texture.height),
                            new Vector2(
                                0.5f,
                                0.5f),
                            100f);
                }
            }

            if (sprite != null)
            {
                SpriteCache[resourcePath] =
                    sprite;
            }

            return sprite;
        }

        public static void SetResource(
            Image image,
            string resourcePath,
            Color tint)
        {
            if (image == null)
                return;

            image.sprite =
                LoadSpriteResource(
                    resourcePath);

            image.preserveAspect =
                true;

            image.color =
                image.sprite != null
                    ? tint
                    : Color.clear;
        }

        public static string GetCharacterBaseResource(
            int characterId)
        {
            foreach (var item
                     in CharacterShopAssets.Items)
            {
                if (item.CharacterId
                    == characterId)
                {
                    return item.ThumbnailResource;
                }
            }

            return null;
        }

        public static string GetCharacterPortraitResource(
            int characterId)
        {
            if (characterId == 0)
                return "Shop/LobbyPortraits/astronaut-hero";

            if (characterId == 1)
                return "Shop/LobbyPortraits/jammo-hero";

            if (characterId == 2) return "Shop/LobbyPortraits/Cursed Priest";

            return GetCharacterBaseResource(
                characterId);
        }

        public static string GetPetBaseResource(
            int petId)
        {
            if (petId <= 0)
                return null;

            return PetShopThumbnailAssets
                .GetThumbnailResource(
                    petId);
        }

        public static Color GetCharacterOutfitTint(
            int characterId,
            int outfitId)
        {
            var swatch =
                CharacterOutfitLibrary.GetSwatch(
                    characterId,
                    outfitId);

            return Color.Lerp(
                Color.white,
                swatch,
                0.42f);
        }

        public static Color GetPetSkinTint(
            int petId,
            int skinId)
        {
            var material =
                PetSkinLibrary.Load(
                    petId,
                    skinId);

            if (material == null)
                return Color.white;

            Color baseColor =
                Color.white;

            if (material.HasProperty(
                    "_BaseColor"))
            {
                baseColor =
                    material.GetColor(
                        "_BaseColor");
            }
            else if (material.HasProperty(
                         "_Color"))
            {
                baseColor =
                    material.GetColor(
                        "_Color");
            }
            else
            {
                baseColor =
                    material.color;
            }

            return Color.Lerp(
                Color.white,
                baseColor,
                0.48f);
        }
    }
}
