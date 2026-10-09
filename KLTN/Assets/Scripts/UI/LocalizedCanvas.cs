using System.Collections.Generic;
using EchoProtocol.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EchoProtocol.UI
{
    // Adapts existing authored canvases without replacing their controllers or layout.
    public sealed class LocalizedCanvas : MonoBehaviour
    {
        private sealed class Entry
        {
            public Graphic Label;
            public string Source, Output;
            public GameLocale Locale;
        }
        private readonly Dictionary<Graphic, Entry> _labels = new();
        private float _nextDiscovery;
        private void OnTransformChildrenChanged() => _nextDiscovery = 0;

        private void LateUpdate()
        {
            if (Time.unscaledTime >= _nextDiscovery)
            {
                _nextDiscovery = Time.unscaledTime + 1f;
                foreach (var text in GetComponentsInChildren<TMP_Text>(true)) Register(text);
                foreach (var text in GetComponentsInChildren<Text>(true)) Register(text);
                var removed = new List<Graphic>();
                foreach (var entry in _labels) if (entry.Value.Label == null) removed.Add(entry.Key);
                foreach (var label in removed) _labels.Remove(label);
            }
            foreach (var entry in _labels.Values)
            {
                if (entry.Label == null || !entry.Label.gameObject.activeInHierarchy) continue;
                string current = entry.Label is TMP_Text tmp ? tmp.text : ((Text)entry.Label).text;
                if (current == entry.Output && entry.Locale == GameLanguage.Current) continue;
                if (current != entry.Output) entry.Source = current;
                entry.Output = GameLanguage.Translate(entry.Source);
                entry.Locale = GameLanguage.Current;
                if (entry.Label is TMP_Text target) target.text = entry.Output;
                else ((Text)entry.Label).text = entry.Output;
            }
        }

        private void Register(Graphic label)
        {
            if (_labels.ContainsKey(label)) return;
            // Input values and identity labels are player data, not interface copy.
            if ((label.GetComponentInParent<TMP_InputField>() != null || label.GetComponentInParent<InputField>() != null)
                && !label.name.ToLowerInvariant().Contains("placeholder")) return;
            string name = label.name.ToLowerInvariant();
            if (name.Contains("playername") || name.Contains("memberlist") || name.Contains("roomname")
                || name.Contains("operatorname") || name.Contains("username") && !name.Contains("title")
                || label.GetComponentInParent<LobbyTeamToolSelector>() != null && name.Contains("value")
                || label.GetComponentInParent<LobbyPlayerNameplate>() != null
                || label.GetComponentInParent<EchoProtocol.UI.MainMenu.TeamToolShopPanel>() != null) return;
            _labels.Add(label, new Entry { Label = label });
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartService()
        {
            if (FindAnyObjectByType<LocalizationCanvasDiscovery>() == null)
                new GameObject("InterfaceLocalization").AddComponent<LocalizationCanvasDiscovery>();
        }
    }

    internal sealed class LocalizationCanvasDiscovery : MonoBehaviour
    {
        private float _nextScan;
        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += SceneLoaded;
        }
        private void SceneLoaded(Scene scene, LoadSceneMode mode) => _nextScan = 0;
        private void OnDestroy() => SceneManager.sceneLoaded -= SceneLoaded;
        private void Update()
        {
            if (Time.unscaledTime < _nextScan) return;
            _nextScan = Time.unscaledTime + 0.5f;
            foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include))
                if (canvas.isRootCanvas && canvas.GetComponent<LocalizedCanvas>() == null)
                    canvas.gameObject.AddComponent<LocalizedCanvas>();
        }
    }
}
