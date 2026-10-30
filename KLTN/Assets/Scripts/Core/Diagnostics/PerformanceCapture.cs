using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace EchoProtocol.Diagnostics
{
    // Opt-in capture: no frame samples or on-screen UI until F10 is pressed.
    public sealed class PerformanceCapture : MonoBehaviour
    {
        private const double WarmupSeconds = 5d;
        private const double CaptureSeconds = 15d * 60d;
        private readonly List<float> _frames = new List<float>(120000);
        private double _started;
        private string _scene;
        private bool _recording;
        private string _status;
        private float _nextDisplay;
        private string _display;
        private GUIStyle _style;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var root = new GameObject("PerformanceCapture");
            DontDestroyOnLoad(root);
            root.AddComponent<PerformanceCapture>();
        }

        private void Update()
        {
            if (Application.isFocused && Keyboard.current != null
                && Keyboard.current.f10Key.wasPressedThisFrame)
            {
                _recording = !_recording;
                _frames.Clear();
                _started = Time.realtimeSinceStartupAsDouble;
                _scene = SceneManager.GetActiveScene().name;
                _status = _recording ? "Warmup 5s, capture 15 minutes" : null;
            }
            if (!_recording) return;
            if (!Application.isFocused || SceneManager.GetActiveScene().name != _scene)
            {
                _recording = false;
                _frames.Clear();
                _status = "Capture cancelled: focus/scene changed. F10 to retry.";
                return;
            }
            double elapsed = Time.realtimeSinceStartupAsDouble - _started;
            if (elapsed < WarmupSeconds) return;
            float delta = Time.unscaledDeltaTime;
            if (delta > 0f) _frames.Add(delta);
            if (Time.unscaledTime >= _nextDisplay)
            {
                _nextDisplay = Time.unscaledTime + 0.5f;
                var progress = TimeSpan.FromSeconds(Math.Min(CaptureSeconds, elapsed - WarmupSeconds));
                _display = $"{1f / delta:0} FPS | Capture {progress.Minutes:00}:{progress.Seconds:00}/15:00 | F10 cancel";
            }
            // List capacity can grow: a high frame rate must not end the capture early.
            if (elapsed >= WarmupSeconds + CaptureSeconds) Save();
        }

        private void Save()
        {
            _recording = false;
            if (_frames.Count == 0) return;
            double total = 0d;
            foreach (float frame in _frames) total += frame;
            _frames.Sort();
            var result = new CaptureResult
            {
                utc = DateTime.UtcNow.ToString("O"), scene = _scene,
                unity = Application.unityVersion, buildVersion = Application.version,
                editor = Application.isEditor, developmentBuild = Debug.isDebugBuild,
                cpu = SystemInfo.processorType, gpu = SystemInfo.graphicsDeviceName,
                ramMb = SystemInfo.systemMemorySize, vramMb = SystemInfo.graphicsMemorySize,
                operatingSystem = SystemInfo.operatingSystem,
                width = Screen.width, height = Screen.height,
                quality = QualitySettings.names[QualitySettings.GetQualityLevel()],
                vSync = QualitySettings.vSyncCount, targetFps = Application.targetFrameRate,
                samples = _frames.Count, seconds = total,
                averageFps = _frames.Count / total,
                p99FrameMs = _frames[Math.Min(_frames.Count - 1,
                    (int)Math.Ceiling(_frames.Count * 0.99d) - 1)] * 1000d,
                worstFrameMs = _frames[_frames.Count - 1] * 1000d
            };
            try
            {
                string folder = Path.Combine(Application.persistentDataPath, "PerformanceCaptures");
                Directory.CreateDirectory(folder);
                string path = Path.Combine(folder, $"capture-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}.json");
                File.WriteAllText(path, JsonUtility.ToJson(result, true));
                _status = $"Average {result.averageFps:0.0} FPS | Saved (see log) | F10 retry";
                Debug.Log($"[PerformanceCapture] {path}");
            }
            catch (Exception error)
            {
                _status = "Capture save failed (see log).";
                Debug.LogException(error);
            }
            _frames.Clear();
        }

        private void OnGUI()
        {
            if (_status == null) return;
            _style ??= new GUIStyle(GUI.skin.box) { fontSize = 16, alignment = TextAnchor.MiddleLeft };
            GUI.Box(new Rect(12f, Screen.height - 44f, Math.Min(Screen.width - 24f, 650f), 32f),
                _recording && Time.realtimeSinceStartupAsDouble - _started >= WarmupSeconds ? _display : _status, _style);
        }

        [Serializable]
        private sealed class CaptureResult
        {
            public string utc, scene, unity, buildVersion, cpu, gpu, operatingSystem, quality;
            public bool editor, developmentBuild;
            public int ramMb, vramMb, width, height, vSync, targetFps, samples;
            public double seconds, averageFps, p99FrameMs, worstFrameMs;
        }
    }
}
