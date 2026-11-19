using EchoProtocol.Networking;
using NUnit.Framework;

namespace EchoProtocol.Networking.Tests
{
    public class GameplayChatFilterTests
    {
        // The test asmdef cannot reference the predefined Assembly-CSharp directly.
        private static class GameplayChatRpc
        {
            private static readonly System.Type Type = System.Type.GetType(
                "EchoProtocol.Networking.GameplayChatRpc, Assembly-CSharp", true);

            public static void SetAdminBlockedWords(string[] words, bool replacesDefaults) =>
                Type.GetMethod("SetAdminBlockedWords")!.Invoke(null, new object[] { words, replacesDefaults });

            public static bool ContainsBlockedLanguage(string text) =>
                (bool)Type.GetMethod("ContainsBlockedLanguage")!.Invoke(null, new object[] { text });
        }

        [Test]
        public void ConfiguredWordsMatchCommonVariationsWithoutMatchingInsideWords()
        {
            try
            {
                GameplayChatRpc.SetAdminBlockedWords(new[] { "đụ mẹ" }, true);
                Assert.IsTrue(GameplayChatRpc.ContainsBlockedLanguage("đụ mẹ"));
                Assert.IsTrue(GameplayChatRpc.ContainsBlockedLanguage("du me"));
                Assert.IsTrue(GameplayChatRpc.ContainsBlockedLanguage("dm"));
                Assert.IsTrue(GameplayChatRpc.ContainsBlockedLanguage("du.me Phuc"));
                Assert.IsTrue(GameplayChatRpc.ContainsBlockedLanguage("dume Phuc"));
                Assert.IsTrue(GameplayChatRpc.ContainsBlockedLanguage("du---me Phuc"));
                Assert.IsFalse(GameplayChatRpc.ContainsBlockedLanguage("admin"));

                GameplayChatRpc.SetAdminBlockedWords(new[] { "cặc" }, true);
                Assert.IsTrue(GameplayChatRpc.ContainsBlockedLanguage("c.a.c"));
                Assert.IsTrue(GameplayChatRpc.ContainsBlockedLanguage("caaac"));
                Assert.IsFalse(GameplayChatRpc.ContainsBlockedLanguage("cacao"));

                GameplayChatRpc.SetAdminBlockedWords(new[] { "chó" }, true);
                Assert.IsTrue(GameplayChatRpc.ContainsBlockedLanguage("chó"));
                Assert.IsTrue(GameplayChatRpc.ContainsBlockedLanguage("cho"));
                Assert.IsTrue(GameplayChatRpc.ContainsBlockedLanguage("con cho"));
                Assert.IsTrue(GameplayChatRpc.ContainsBlockedLanguage("cho phuc"));
                Assert.IsFalse(GameplayChatRpc.ContainsBlockedLanguage("chocolate"));
            }
            finally
            {
                GameplayChatRpc.SetAdminBlockedWords(new string[0], false);
            }
        }
    }
}
