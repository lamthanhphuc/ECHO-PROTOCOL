using System.Collections.Generic;
using UnityEngine;

namespace EchoProtocol.Settings
{
    /// <summary>Explicit local graphics preferences; fresh installs keep the platform defaults.</summary>
    public static class GameGraphicsSettings
    {
        private const string QualityKey = "Echo.Graphics.Quality";
        private const string WidthKey = "Echo.Graphics.Width";
        private const string HeightKey = "Echo.Graphics.Height";
        private const string FullscreenKey = "Echo.Graphics.Fullscreen";
        private const string VSyncKey = "Echo.Graphics.VSync";
        private static Vector2Int _requestedResolution;
        private static int _resolutionRequestFrame = -10;
        private static bool _requestedFullscreen;
        private static int _fullscreenRequestFrame = -10;

        public static string QualityLabel
        {
            get
            {
                var names = QualitySettings.names;
                return names.Length > 0 ? names[Mathf.Clamp(QualitySettings.GetQualityLevel(), 0, names.Length - 1)] : "Default";
            }
        }

        public static string ResolutionLabel
        {
            get
            {
                var resolution = CurrentResolution;
                return $"{resolution.x} × {resolution.y}";
            }
        }

        public static bool Fullscreen => Time.frameCount - _fullscreenRequestFrame <= 1
            ? _requestedFullscreen : Screen.fullScreen;
        public static bool VSync => QualitySettings.vSyncCount > 0;
        public static bool CanChangeResolution => !Application.isEditor && AvailableResolutions().Count > 1;
        public static bool CanChangeFullscreen => !Application.isEditor;

        private static Vector2Int CurrentResolution => Time.frameCount - _resolutionRequestFrame <= 1
            ? _requestedResolution : new Vector2Int(Screen.width, Screen.height);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic()
        {
            _resolutionRequestFrame = -10;
            _fullscreenRequestFrame = -10;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ApplySaved()
        {
            if (PlayerPrefs.HasKey(QualityKey))
            {
                var names = QualitySettings.names;
                var saved = PlayerPrefs.GetString(QualityKey);
                for (int i = 0; i < names.Length; i++)
                    if (names[i] == saved)
                    {
                        QualitySettings.SetQualityLevel(i, true);
                        break;
                    }
            }
            if (PlayerPrefs.HasKey(VSyncKey))
                QualitySettings.vSyncCount = PlayerPrefs.GetInt(VSyncKey) != 0 ? 1 : 0;

            bool fullscreen = PlayerPrefs.HasKey(FullscreenKey)
                ? PlayerPrefs.GetInt(FullscreenKey) != 0 : Screen.fullScreen;
            if (CanChangeResolution && PlayerPrefs.HasKey(WidthKey) && PlayerPrefs.HasKey(HeightKey))
            {
                var saved = new Vector2Int(PlayerPrefs.GetInt(WidthKey), PlayerPrefs.GetInt(HeightKey));
                if (AvailableResolutions().Contains(saved))
                {
                    RequestResolution(saved, fullscreen);
                    return;
                }
            }
            if (CanChangeFullscreen && PlayerPrefs.HasKey(FullscreenKey)) RequestFullscreen(fullscreen);
        }

        public static void CycleQuality()
        {
            var names = QualitySettings.names;
            if (names.Length == 0) return;
            int next = (QualitySettings.GetQualityLevel() + 1) % names.Length;
            QualitySettings.SetQualityLevel(next, true);
            PlayerPrefs.SetString(QualityKey, names[next]);
            if (PlayerPrefs.HasKey(VSyncKey))
                QualitySettings.vSyncCount = PlayerPrefs.GetInt(VSyncKey) != 0 ? 1 : 0;
        }

        public static void CycleResolution()
        {
            if (!CanChangeResolution) return;
            var choices = AvailableResolutions();
            if (choices.Count == 0) return;
            int current = choices.IndexOf(CurrentResolution);
            var next = choices[(current + 1) % choices.Count];
            PlayerPrefs.SetInt(WidthKey, next.x);
            PlayerPrefs.SetInt(HeightKey, next.y);
            RequestResolution(next, Fullscreen);
        }

        public static void SetFullscreen(bool enabled)
        {
            if (!CanChangeFullscreen) return;
            PlayerPrefs.SetInt(FullscreenKey, enabled ? 1 : 0);
            RequestFullscreen(enabled);
        }

        public static void SetVSync(bool enabled)
        {
            QualitySettings.vSyncCount = enabled ? 1 : 0;
            PlayerPrefs.SetInt(VSyncKey, enabled ? 1 : 0);
        }

        public static void Save() => PlayerPrefs.Save();

        private static List<Vector2Int> AvailableResolutions()
        {
            var result = new List<Vector2Int>();
            foreach (var resolution in Screen.resolutions)
            {
                var size = new Vector2Int(resolution.width, resolution.height);
                if (!result.Contains(size)) result.Add(size);
            }
            return result;
        }

        private static void RequestResolution(Vector2Int size, bool fullscreen)
        {
            _requestedResolution = size;
            _resolutionRequestFrame = Time.frameCount;
            _requestedFullscreen = fullscreen;
            _fullscreenRequestFrame = Time.frameCount;
            Screen.SetResolution(size.x, size.y, fullscreen);
        }

        private static void RequestFullscreen(bool enabled)
        {
            _requestedFullscreen = enabled;
            _fullscreenRequestFrame = Time.frameCount;
            if (Screen.fullScreen != enabled) Screen.fullScreen = enabled;
        }
    }
}
