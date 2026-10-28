using UnityEngine;

namespace EchoProtocol.UI.MainMenu
{
    /// <summary>
    /// Existing Main Menu art and audio used by the runtime leaderboard overlay.
    /// The asset lives in Resources while the referenced media remains in its
    /// original project location.
    /// </summary>
    [CreateAssetMenu(menuName = "Echo Protocol/UI/Leaderboard Archive Visuals")]
    public sealed class LeaderboardArchiveVisuals : ScriptableObject
    {
        public Sprite dossierMetal;
        public Sprite surveillanceStill;
        public Sprite tornNote;
        public Sprite agedTape;
        public Sprite fingerprint;
        public Sprite operatorPortrait;
        public Font spectralFont;
        public AudioClip hoverClip;
    }
}
