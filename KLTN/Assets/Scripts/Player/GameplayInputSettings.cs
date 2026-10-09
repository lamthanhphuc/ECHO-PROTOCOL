using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EchoProtocol.Settings
{
    public enum GameplayAction
    {
        MoveForward, MoveBackward, MoveLeft, MoveRight, Interact, Sprint, Crouch, Flashlight, Inventory
    }

    /// <summary>Local preferences shared by keyboard bindings and the player camera.</summary>
    public static class GameplayInputSettings
    {
        private const string Prefix = "EchoProtocol.Controls.";
        private static readonly Key[] DefaultKeys =
        {
            Key.W, Key.S, Key.A, Key.D, Key.E, Key.LeftShift, Key.C, Key.F, Key.Tab
        };
        private static readonly string[] ActionLabels =
        {
            "Di chuyển tiến", "Lùi", "Sang trái", "Sang phải", "Tương tác", "Chạy", "Cúi", "Bật đèn pin", "Túi đồ"
        };
        private static readonly Dictionary<InputAction, int> RegisteredActions = new Dictionary<InputAction, int>();
        private static readonly Key[] Keys = new Key[DefaultKeys.Length];
        private static bool _loaded;
        private static float _mouseSensitivity;
        private static bool _invertY;
        private static bool _mouseAcceleration;

        public static event Action Changed;

        public static float MouseSensitivity
        {
            get { EnsureLoaded(); return _mouseSensitivity; }
            set
            {
                EnsureLoaded();
                float next = float.IsNaN(value) || float.IsInfinity(value) ? 1f : Mathf.Clamp(value, 0.1f, 3f);
                if (Mathf.Approximately(_mouseSensitivity, next)) return;
                _mouseSensitivity = next;
                PlayerPrefs.SetFloat(Prefix + "Sensitivity", next);
                Changed?.Invoke();
            }
        }

        public static bool InvertY
        {
            get { EnsureLoaded(); return _invertY; }
            set
            {
                EnsureLoaded();
                if (_invertY == value) return;
                _invertY = value;
                PlayerPrefs.SetInt(Prefix + "InvertY", value ? 1 : 0);
                Changed?.Invoke();
            }
        }

        public static bool MouseAcceleration
        {
            get { EnsureLoaded(); return _mouseAcceleration; }
            set
            {
                EnsureLoaded();
                if (_mouseAcceleration == value) return;
                _mouseAcceleration = value;
                PlayerPrefs.SetInt(Prefix + "Acceleration", value ? 1 : 0);
                Changed?.Invoke();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeState()
        {
            _loaded = false;
            RegisteredActions.Clear();
            Changed = null;
        }

        private static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            _mouseSensitivity = PlayerPrefs.GetFloat(Prefix + "Sensitivity", 1f);
            if (float.IsNaN(_mouseSensitivity) || float.IsInfinity(_mouseSensitivity)) _mouseSensitivity = 1f;
            _mouseSensitivity = Mathf.Clamp(_mouseSensitivity, 0.1f, 3f);
            _invertY = PlayerPrefs.GetInt(Prefix + "InvertY", 0) != 0;
            _mouseAcceleration = PlayerPrefs.GetInt(Prefix + "Acceleration", 0) != 0;
            for (int i = 0; i < Keys.Length; i++)
            {
                Key saved = (Key)PlayerPrefs.GetInt(Prefix + (GameplayAction)i, (int)DefaultKeys[i]);
                Keys[i] = IsAssignableKey(saved) ? saved : DefaultKeys[i];
            }
        }

        public static Key GetKey(GameplayAction action)
        {
            EnsureLoaded();
            return Keys[(int)action];
        }

        public static string GetKeyLabel(GameplayAction action)
        {
            Key key = GetKey(action);
            if (action == GameplayAction.Crouch && key == Key.C) return "Ctrl / C";
            return GetKeyLabel(key);
        }

        public static string GetKeyLabel(Key key)
        {
            switch (key)
            {
                case Key.LeftShift: return "Shift";
                case Key.RightShift: return "R Shift";
                case Key.LeftCtrl: return "Ctrl";
                case Key.RightCtrl: return "R Ctrl";
                case Key.LeftAlt: return "Alt";
                case Key.RightAlt: return "R Alt";
                case Key.Tab: return "Tab";
                case Key.Space: return "Space";
                case Key.Enter: return "Enter";
                case Key.Backspace: return "Backspace";
            }
            var keyboard = Keyboard.current;
            return keyboard != null ? keyboard[key].displayName : key.ToString();
        }

        public static bool IsKeyBound(Key key)
        {
            EnsureLoaded();
            if (IsFixedGameplayKey(key)) return true;
            for (int i = 0; i < Keys.Length; i++)
                if ((GameplayAction)i != GameplayAction.Inventory)
                if (Keys[i] == key || IsDefaultAlternate((GameplayAction)i, key)) return true;
            return false;
        }

        public static bool TrySetKey(GameplayAction action, Key key, out string error)
        {
            EnsureLoaded();
            if (!IsAssignableKey(key))
            {
                error = "ESC được dùng để đóng menu. Hãy chọn phím khác.";
                return false;
            }
            Key microphoneKey = Enum.TryParse(PlayerPrefs.GetString("Echo.Voice.ToggleKey", "V"), out Key savedMicrophoneKey)
                && IsAssignableKey(savedMicrophoneKey) ? savedMicrophoneKey : Key.V;
            if (EchoProtocol.Voice.VoiceManager.Instance != null)
                microphoneKey = EchoProtocol.Voice.VoiceManager.Instance.ToggleMicrophoneKey;
            if (key == microphoneKey || IsDefaultAlternate(action, key, microphoneKey))
            {
                error = "Phím này đang dùng cho microphone.";
                return false;
            }
            for (int i = 0; i < Keys.Length; i++)
            {
                if ((GameplayAction)i != GameplayAction.Inventory && i != (int)action && (Keys[i] == key || IsDefaultAlternate((GameplayAction)i, key)
                    || IsDefaultAlternate(action, key, Keys[i])))
                {
                    error = "Phím này đang dùng cho: " + ActionLabels[i] + ".";
                    return false;
                }
            }
            // These commands currently have fixed shortcuts outside this settings page.
            if (IsFixedGameplayKey(key))
            {
                error = "Phím này đang dùng cho thao tác khác trong game.";
                return false;
            }
            Keys[(int)action] = key;
            PlayerPrefs.SetInt(Prefix + action, (int)key);
            foreach (InputAction registered in RegisteredActions.Keys) ApplyBindings(registered);
            Changed?.Invoke();
            error = string.Empty;
            return true;
        }

        private static bool IsAssignableKey(Key key)
        {
            return key != Key.None && key != Key.Escape && Enum.IsDefined(typeof(Key), key);
        }

        private static bool IsFixedGameplayKey(Key key)
        {
            return key == Key.G || key == Key.H || key == Key.Q || key == Key.Digit1 || key == Key.Digit2
                || key == Key.F1 || key == Key.Backquote;
        }

        private static bool IsDefaultAlternate(GameplayAction action, Key key)
        {
            return IsDefaultAlternate(action, Keys[(int)action], key);
        }

        private static bool IsDefaultAlternate(GameplayAction action, Key primaryKey, Key key)
        {
            if (primaryKey != DefaultKeys[(int)action]) return false;
            switch (action)
            {
                case GameplayAction.MoveForward: return key == Key.UpArrow;
                case GameplayAction.MoveBackward: return key == Key.DownArrow;
                case GameplayAction.MoveLeft: return key == Key.LeftArrow;
                case GameplayAction.MoveRight: return key == Key.RightArrow;
                case GameplayAction.Crouch: return key == Key.LeftCtrl || key == Key.RightCtrl;
                default: return false;
            }
        }

        public static bool IsPressed(GameplayAction action)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return false;
            Key key = GetKey(action);
            if (keyboard[key].isPressed) return true;
            if (action == GameplayAction.Crouch && key == Key.C)
                return keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed;
            return false;
        }

        public static bool WasPressedThisFrame(GameplayAction action)
        {
            return Keyboard.current != null && Keyboard.current[GetKey(action)].wasPressedThisFrame;
        }

        public static Vector2 ApplyLookSettings(Vector2 input, float deltaTime)
        {
            float multiplier = MouseSensitivity;
            if (MouseAcceleration)
            {
                // Measure speed rather than per-frame distance so the curve is stable across frame rates.
                float speed = input.magnitude / Mathf.Max(deltaTime, 0.001f);
                multiplier *= 1f + Mathf.Clamp01((speed - 150f) / 1500f);
            }
            input *= multiplier;
            if (InvertY) input.y = -input.y;
            return input;
        }

        public static void RegisterAction(InputAction action)
        {
            if (action == null) return;
            EnsureLoaded();
            if (RegisteredActions.TryGetValue(action, out int count))
            {
                RegisteredActions[action] = count + 1;
                return;
            }
            RegisteredActions.Add(action, 1);
            ApplyBindings(action);
        }

        public static void UnregisterAction(InputAction action)
        {
            if (action == null || !RegisteredActions.TryGetValue(action, out int count)) return;
            if (count > 1) RegisteredActions[action] = count - 1;
            else RegisteredActions.Remove(action);
        }

        private static void ApplyBindings(InputAction action)
        {
            for (int i = 0; i < action.bindings.Count; i++)
            {
                InputBinding binding = action.bindings[i];
                if (binding.isComposite || string.IsNullOrEmpty(binding.path)
                    || !binding.path.StartsWith("<Keyboard>/", StringComparison.OrdinalIgnoreCase)) continue;
                if (!TryGetAction(action.name, binding.name, out GameplayAction setting)) continue;
                Key key = Keys[(int)setting];
                if (key == DefaultKeys[(int)setting]) action.RemoveBindingOverride(i);
                else action.ApplyBindingOverride(i, KeyboardPath(key));
            }
        }

        private static bool TryGetAction(string actionName, string part, out GameplayAction action)
        {
            action = GameplayAction.Interact;
            switch (actionName)
            {
                case "Move":
                    switch ((part ?? string.Empty).ToLowerInvariant())
                    {
                        case "up": action = GameplayAction.MoveForward; return true;
                        case "down": action = GameplayAction.MoveBackward; return true;
                        case "left": action = GameplayAction.MoveLeft; return true;
                        case "right": action = GameplayAction.MoveRight; return true;
                        default: return false;
                    }
                case "Interact": return true;
                case "Sprint": action = GameplayAction.Sprint; return true;
                case "Crouch": action = GameplayAction.Crouch; return true;
                case "Flashlight": action = GameplayAction.Flashlight; return true;
                default: return false;
            }
        }

        private static string KeyboardPath(Key key)
        {
            if (Keyboard.current != null) return "<Keyboard>/" + Keyboard.current[key].name;
            string name = key.ToString();
            if (name.StartsWith("Digit", StringComparison.Ordinal)) name = name.Substring(5);
            return "<Keyboard>/" + char.ToLowerInvariant(name[0]) + name.Substring(1);
        }

        public static void Save() => PlayerPrefs.Save();
    }
}
