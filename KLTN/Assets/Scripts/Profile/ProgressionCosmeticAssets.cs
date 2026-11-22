using System;
using UnityEngine;

namespace EchoProtocol.Profile
{
    [CreateAssetMenu(menuName = "Echo Protocol/Progression Cosmetic Assets")]
    public sealed class ProgressionCosmeticAssets : ScriptableObject
    {
        public const string ResourcePath = "UI/ProgressionCosmetics";

        [Serializable]
        public sealed class Entry
        {
            public string rewardKey;
            public Sprite preview;
            [Tooltip("Badge icon or transparent frame, rendered over the profile photograph.")]
            public Sprite profileOverlay;
            [Tooltip("Material for the cosmetic tool/outfit. Never changes stats or colliders.")]
            public Material material;
            [Tooltip("Optional visual-only prefab. Do not include gameplay/network components.")]
            public GameObject visualPrefab;
            public bool Ready => rewardKey == "level15_badge" || rewardKey == "level20_frame"
                ? profileOverlay != null : material != null || visualPrefab != null;
        }

        public Entry[] entries = {
            new Entry { rewardKey = "level15_badge" }, new Entry { rewardKey = "level20_frame" },
            new Entry { rewardKey = "level25_tool" }, new Entry { rewardKey = "level30_outfit" }
        };

        public Entry Find(string key) => Array.Find(entries ?? Array.Empty<Entry>(), e => e != null && e.rewardKey == key);
        public static ProgressionCosmeticAssets Load() => Resources.Load<ProgressionCosmeticAssets>(ResourcePath);

        // Integration point for the final tool/outfit assets. Empty slots keep the original visuals.
        public static Entry Equipped(string key)
        {
            foreach (var reward in PlayerProfileSession.CosmeticRewards)
                if (reward != null && reward.rewardKey == key && reward.owned && reward.equipped)
                    return Load()?.Find(key);
            return null;
        }
    }
}
