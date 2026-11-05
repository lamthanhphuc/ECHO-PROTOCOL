#if AED_V02_PILOT_HARNESS
using System.Collections.Generic;
using UnityEngine;

namespace EchoProtocol.Auth
{
    // Harness processes must never replace the owner's stored access/refresh tokens.
    internal static class AEDPilotTokenPrefs
    {
        private static readonly Dictionary<string, string> Values = new Dictionary<string, string>();
        internal static bool UseMemory { get; set; }
        public static string GetString(string key, string fallback) => UseMemory
            ? Values.TryGetValue(key, out var value) ? value : fallback : PlayerPrefs.GetString(key, fallback);
        public static void SetString(string key, string value) { if (UseMemory) Values[key] = value; else PlayerPrefs.SetString(key, value); }
        public static void DeleteKey(string key) { if (UseMemory) Values.Remove(key); else PlayerPrefs.DeleteKey(key); }
        public static void Save() { if (!UseMemory) PlayerPrefs.Save(); }
    }
}
#endif
