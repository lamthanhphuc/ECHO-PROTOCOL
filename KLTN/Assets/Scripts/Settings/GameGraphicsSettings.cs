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
        private static Vector2Int _previousResolution;
        private static bool _previousFullscreen;
        private static float _previewDeadline;
        public static bool HasResolutionPreview { get; private set; }
        public static float ResolutionPreviewSecondsRemaining => Mathf.Max(0, _previewDeadline - Time.realtimeSinceStartup);

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
            HasResolutionPreview = false;
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
            if (!CanChangeResolution || HasResolutionPreview) return;
            var choices = AvailableResolutions();
            if (choices.Count == 0) return;
            int current = choices.IndexOf(CurrentResolution);
            var next = choices[(current + 1) % choices.Count];
            BeginResolutionPreview();
            RequestResolution(next, Fullscreen);
        }

        public static void SetFullscreen(bool enabled)
        {
            if (!CanChangeFullscreen || HasResolutionPreview || enabled == Fullscreen) return;
            BeginResolutionPreview();
            RequestFullscreen(enabled);
        }

        private static void BeginResolutionPreview()
        {
            _previousResolution = CurrentResolution;
            _previousFullscreen = Fullscreen;
            _previewDeadline = Time.realtimeSinceStartup + 15f;
            HasResolutionPreview = true;
            // Ensure the deadline is monitored even outside the settings panel.
            _ = EchoProtocol.UI.GameUIFeedback.Instance;
        }

        public static void ConfirmResolution()
        {
            if (!HasResolutionPreview) return;
            HasResolutionPreview = false;
            PlayerPrefs.SetInt(WidthKey, CurrentResolution.x);
            PlayerPrefs.SetInt(HeightKey, CurrentResolution.y);
            PlayerPrefs.SetInt(FullscreenKey, Fullscreen ? 1 : 0);
            PlayerPrefs.Save();
        }

        public static void RevertResolution()
        {
            if (!HasResolutionPreview) return;
            HasResolutionPreview = false;
            RequestResolution(_previousResolution, _previousFullscreen);
        }

        public static void TickResolutionPreview()
        {
            if (HasResolutionPreview && Time.realtimeSinceStartup >= _previewDeadline) RevertResolution();
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
