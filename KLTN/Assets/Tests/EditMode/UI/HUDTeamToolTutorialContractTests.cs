using System.IO;
using NUnit.Framework;

namespace EchoProtocol.UI.HUD.Tests
{
    public sealed class HUDTeamToolTutorialContractTests
    {
        private const string SourcePath =
            "Assets/Scripts/UI/HUD/" +
            "HUDScannerTutorial.cs";

        [Test]
        public void TutorialProgress_IsNotGlobalStatic()
        {
            string source = File.ReadAllText(SourcePath);

            StringAssert.DoesNotContain(
                "static int s_shownToolMask",
                source);

            StringAssert.Contains(
                "private int _shownToolMask",
                source);
        }

        [Test]
        public void TutorialProgress_IsScopedByMatchAndPlayer()
        {
            string source = File.ReadAllText(SourcePath);

            StringAssert.Contains(
                "ResolveTutorialScopeKey",
                source);

            StringAssert.Contains(
                "TryGetMatchId",
                source);

            StringAssert.Contains(
                "InputAuthority.PlayerId",
                source);

            StringAssert.Contains(
                "_shownToolMask = 0",
                source);
        }

        [Test]
        public void Unbind_DoesNotResetTutorialProgress()
        {
            string source = File.ReadAllText(SourcePath);

            int start = source.IndexOf(
                "public void Unbind()");

            int end = source.IndexOf(
                "private void HandleInventoryChanged",
                start);

            Assert.That(start, Is.GreaterThanOrEqualTo(0));
            Assert.That(end, Is.GreaterThan(start));

            string method = source.Substring(
                start,
                end - start);

            StringAssert.DoesNotContain(
                "_shownToolMask = 0",
                method);

            StringAssert.DoesNotContain(
                "_tutorialScopeKey = string.Empty",
                method);
        }
    }
}
