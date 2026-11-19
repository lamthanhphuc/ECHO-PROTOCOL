using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EchoProtocol.Networking
{
    public sealed class GameplayChatRpc : SimulationBehaviour
    {
        public static event Action<PlayerRef, string> MessageReceived;

        private static readonly Dictionary<PlayerRef, float> LastSent = new();
        private static NetworkRunner _rateRunner;
        // shortcut: "dm" is ambiguous but blocked when the configured list includes "đụ mẹ"; review if false positives appear.
        private static readonly Regex BlockedWords = new(
            @"(?<![\p{L}\p{N}])(?:" +
            @"fuck\p{L}*|motherfuck\p{L}*|clusterfuck|fck|fuk|fuq|phuck|phuk|f0ck|f\W{1,2}u\W{1,2}c\W{1,2}k|f\W{1,2}ck|" +
            @"shit\p{L}*|bullshit|horseshit|dipshit|sh1t\p{L}*|sh!t\p{L}*|shyt|s\W{1,2}h\W{1,2}i\W{1,2}t|" +
            @"bitch\p{L}*|b1tch|b!tch|b\*tch|biatch|bastard|asshole|asswipe|dumbass|jackass|a55hole|a\$\$hole|" +
            @"dick\p{L}*|d1ck|d!ck|d\*ck|cock\p{L}*|c0ck|c\*ck|cunt|cun7|c\*nt|pussy|pu55y|p\*ssy|twat|prick|wanker|tosser|bollocks|whore|slut|skank|douche\p{L}*|scumbag|" +
            @"nigger|nigga|faggot|stfu|gtfo|kys|kill\s+yourself|mothafucka|m0therfucker|mthrfckr|mofo|mfer|" +
            @"địt|đụ|đéo|lồn|cặc|buồi|dái|đĩ|phò|cứt|dit|djt|dis|d1t|d!t|đ!t|đjt|d3o|đ3o|loz+|l0n|l0z+|l\*+n|c4c|c4k|cak|c\*+c|bu0i|" +
            @"dit\s*me(?:may)?|djt\s*me(?:may)?|du\s*ma|du\s*me|vai\s+lon|cai\s+lon|cho\s+de|oc\s+cho|suc\s+vat|khon\s+nan|mat\s+day|ngu\s+nhu\s+cho|" +
            @"chó\s+đẻ|óc\s+chó|súc\s+vật|súc\s+sinh|khốn\s+nạn|mất\s+dạy|ngu\s+như\s+chó" +
            @")(?![\p{L}\p{N}])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static Regex _adminWords;
        private static bool _adminReplacesDefaults;

        public static void SetAdminBlockedWords(string[] words, bool replacesDefaults)
        {
            if (words == null || words.Length > 256) return;
            if (Array.Exists(words, word => string.IsNullOrWhiteSpace(word) || word.Length > 50)) return;
            _adminWords = words.Length == 0 ? null : new Regex(
                @"(?<![\p{L}\p{N}])(?:" + string.Join("|", Array.ConvertAll(words, BuildWordPattern))
                + (Array.Exists(words, word => NormalizeFilterText(word.Trim()) == "du me") ? "|dm" : "")
                + @")(?![\p{L}\p{N}])",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));
            _adminReplacesDefaults = replacesDefaults;
        }

        private static string BuildWordPattern(string word)
        {
            string value = NormalizeFilterText(word.Trim());
            string[] parts = Regex.Split(value, @"\s+");
            var pattern = new StringBuilder();
            for (int i = 0; i < parts.Length; i++)
            {
                if (i > 0) pattern.Append(@"[\s\p{P}]*");
                if (parts[i].Length < 3) pattern.Append(Regex.Escape(parts[i]));
                else
                {
                    for (int j = 0; j < parts[i].Length; j++)
                    {
                        if (j > 0) pattern.Append(@"[\s\p{P}]*");
                        pattern.Append(Regex.Escape(parts[i][j].ToString()));
                        if (parts[i].Length >= 3) pattern.Append("{1,3}");
                    }
                }
            }
            return pattern.ToString();
        }

        private static string NormalizeFilterText(string text)
        {
            var normalized = new StringBuilder(text.Length);
            foreach (char c in text.Normalize(NormalizationForm.FormKD))
                if (char.GetUnicodeCategory(c) != UnicodeCategory.Format
                    && char.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    normalized.Append(c == 'đ' ? 'd' : c == 'Đ' ? 'D' : c);
            return normalized.ToString();
        }

        public static bool ContainsBlockedLanguage(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            string value = NormalizeFilterText(text);
            try
            {
                return (_adminWords?.IsMatch(value) ?? false)
                    || (!_adminReplacesDefaults && (BlockedWords.IsMatch(text) || BlockedWords.IsMatch(value)));
            }
            catch (RegexMatchTimeoutException)
            {
                return true;
            }
        }

        public static string Normalize(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            var result = new StringBuilder(120);

            foreach (char c in text.Trim())
            {
                if (char.IsControl(c) || char.IsSurrogate(c))
                    continue;

                result.Append(c);

                if (result.Length >= 120)
                    break;
            }

            return result.ToString().Trim();
        }

        [Rpc(RpcSources.All, RpcTargets.All, Channel = RpcChannel.Reliable,
            HostMode = RpcHostMode.SourceIsHostPlayer,
            TickAligned = false)]
        public static void RPC_Submit(
            NetworkRunner runner,
            string text,
            RpcInfo info = default)
        {
            if (runner == null || !runner.IsRunning || !runner.IsServer)
                return;

            if (NetworkBootstrap.Instance?.Runner != runner
                || NetworkBootstrap.IsHostMigrationReconciliationPending
                || !LobbyMapCatalog.IsChatScene(SceneManager.GetActiveScene().name)
                || !info.Source.IsRealPlayer)
            {
                Debug.LogWarning($"[Chat] Host rejected submit source={info.Source} scene={SceneManager.GetActiveScene().name}.");
                return;
            }

            if (!runner.TryGetPlayerObject(info.Source, out var player)
                || player == null || !player.IsValid)
            {
                Debug.LogWarning($"[Chat] Host has no player object for {info.Source}.");
                return;
            }

            string message = Normalize(text);
            if (message.Length == 0 || ContainsBlockedLanguage(message))
                return;

            if (_rateRunner != runner)
            {
                LastSent.Clear();
                _rateRunner = runner;
            }

            float now = Time.realtimeSinceStartup;
            if (LastSent.TryGetValue(info.Source, out float previous)
                && now - previous < 0.75f)
                return;

            LastSent[info.Source] = now;
            Debug.Log($"[Chat] Host accepted source={info.Source} length={message.Length}.");
            RPC_Broadcast(runner, info.Source, message);
        }

        [Rpc(RpcSources.All, RpcTargets.All, Channel = RpcChannel.Reliable,
            HostMode = RpcHostMode.SourceIsServer,
            TickAligned = false)]
        public static void RPC_Broadcast(
            NetworkRunner runner,
            PlayerRef sender,
            string message,
            RpcInfo info = default)
        {
            if (runner == null || !runner.IsRunning
                || NetworkBootstrap.Instance?.Runner != runner
                || info.Source != PlayerRef.None)
            {
                Debug.LogWarning($"[Chat] Broadcast rejected source={info.Source}.");
                return;
            }

            Debug.Log($"[Chat] Broadcast received sender={sender} length={message.Length}.");
            MessageReceived?.Invoke(sender, message);
        }
    }
}
