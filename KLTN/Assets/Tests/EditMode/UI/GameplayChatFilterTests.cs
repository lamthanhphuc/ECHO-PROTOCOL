using EchoProtocol.Networking;
using NUnit.Framework;

namespace EchoProtocol.UI.Tests
{
    public class GameplayChatFilterTests
    {
        [TearDown]
        public void ClearAdminWords() => GameplayChatRpc.SetAdminBlockedWords(new string[0], false);

        [TestCase("fuck", true)]
        [TestCase("SHIT!", true)]
        [TestCase("địt mẹ", true)]
        [TestCase("dit me", true)]
        [TestCase("vãi lồn", true)]
        [TestCase("f.u.c.k", true)]
        [TestCase("f*ck", true)]
        [TestCase("sh1t", true)]
        [TestCase("sh!tty", true)]
        [TestCase("b*tch", true)]
        [TestCase("c*c", true)]
        [TestCase("l*n", true)]
        [TestCase("d!t", true)]
        [TestCase("fu\u200Bck", true)]
        [TestCase("kill yourself", true)]
        [TestCase("class", false)]
        [TestCase("người lớn", false)]
        [TestCase("lon", false)]
        [TestCase("dm", false)]
        [TestCase("cc", false)]
        [TestCase("vl", false)]
        [TestCase("xin chào", false)]
        public void BlocksOnlyWholeWordsAndPhrases(string message, bool expected)
        {
            Assert.AreEqual(expected, GameplayChatRpc.ContainsBlockedLanguage(message));
        }

        [Test]
        public void AdminWordsReplaceDefaultRulesAfterPublication()
        {
            GameplayChatRpc.SetAdminBlockedWords(new[] { "custom phrase", "x.y" }, true);
            Assert.IsTrue(GameplayChatRpc.ContainsBlockedLanguage("a custom phrase!"));
            Assert.IsFalse(GameplayChatRpc.ContainsBlockedLanguage("custom phrases"));
            Assert.IsTrue(GameplayChatRpc.ContainsBlockedLanguage("x.y"));
            Assert.IsFalse(GameplayChatRpc.ContainsBlockedLanguage("xay"));
            Assert.IsFalse(GameplayChatRpc.ContainsBlockedLanguage("fuck"));
            GameplayChatRpc.SetAdminBlockedWords(new string[0], true);
            Assert.IsFalse(GameplayChatRpc.ContainsBlockedLanguage("fuck"));
            GameplayChatRpc.SetAdminBlockedWords(new string[0], false);
            Assert.IsTrue(GameplayChatRpc.ContainsBlockedLanguage("fuck"));
        }
    }
}
