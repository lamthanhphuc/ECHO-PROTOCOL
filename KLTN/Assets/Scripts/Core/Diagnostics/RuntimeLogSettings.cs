using UnityEngine;

namespace EchoProtocol.Diagnostics
{
    public sealed class RuntimeLogSettings : ScriptableObject
    {
        [SerializeField]
        [Tooltip("Master switch for category-controlled informational logs.")]
        private bool enableInfoLogs;

        [SerializeField]
        [Tooltip("Informational log categories allowed when the master switch is enabled.")]
        private RuntimeLogCategory enabledCategories =
            RuntimeLogCategory.None;

        public bool EnableInfoLogs => enableInfoLogs;

        public RuntimeLogCategory EnabledCategories =>
            enabledCategories;

        public bool IsEnabled(
            RuntimeLogCategory category)
        {
            return enableInfoLogs
                && category != RuntimeLogCategory.None
                && (enabledCategories & category) != 0;
        }
    }
}
