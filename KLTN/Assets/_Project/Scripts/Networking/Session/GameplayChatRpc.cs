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
        // shortcut: ambiguous REVIEW terms (dm, cc, vl, lon) stay allowed until context-aware moderation exists.
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
                @"(?<![\p{L}\p{N}])(?:" + string.Join("|", Array.ConvertAll(words,
                    word => Regex.Escape(NormalizeFilterText(word.Trim())).Replace(@"\ ", @"\s+")))
                + @")(?![\p{L}\p{N}])",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            _adminReplacesDefaults = replacesDefaults;
        }

        private static string NormalizeFilterText(string text)
        {
            var normalized = new StringBuilder(text.Length);
            foreach (char c in text.Normalize(NormalizationForm.FormKC))
                if (char.GetUnicodeCategory(c) != UnicodeCategory.Format) normalized.Append(c);
            return normalized.ToString();
        }

        public static bool ContainsBlockedLanguage(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            string value = NormalizeFilterText(text);
            return (_adminWords?.IsMatch(value) ?? false)
                || (!_adminReplacesDefaults && BlockedWords.IsMatch(value));
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
