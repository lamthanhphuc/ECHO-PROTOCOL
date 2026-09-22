using UnityEngine;

namespace EchoProtocol.Diagnostics
{
    public static class RuntimeLog
    {
        private const string SettingsResourcePath =
            "RuntimeLogSettings";

        private static RuntimeLogSettings _settings;
        private static bool _settingsLoadAttempted;

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _settings = null;
            _settingsLoadAttempted = false;
        }

        public static bool IsEnabled(
            RuntimeLogCategory category)
        {
            var settings = GetSettings();

            return settings != null
                && settings.IsEnabled(category);
        }

        public static void Log(
            RuntimeLogCategory category,
            string message,
            Object context = null)
        {
            if (!IsEnabled(category))
            {
                return;
            }

            if (context != null)
            {
                Debug.Log(message, context);
                return;
            }

            Debug.Log(message);
        }

        private static RuntimeLogSettings GetSettings()
        {
            if (_settingsLoadAttempted)
            {
                return _settings;
            }

            _settingsLoadAttempted = true;

            _settings =
                Resources.Load<RuntimeLogSettings>(
                    SettingsResourcePath);

            return _settings;
        }
    }
}
