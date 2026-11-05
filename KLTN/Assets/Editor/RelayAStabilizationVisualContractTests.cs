using System.IO;
using NUnit.Framework;

namespace EchoProtocol.Tests
{
    public sealed class RelayAStabilizationVisualContractTests
    {
        private string _source;
        private string _prefix;
        [SetUp]
        public void SetUp()
        {
            _prefix = Directory.Exists("Assets/Scripts/RelayA") ? "" : "KLTN/";
            _source = File.ReadAllText(_prefix + "Assets/Scripts/RelayA/RelayAUIController.Stabilization.cs");
        }
        [TearDown] public void TearDown() { }

        [Test]
        public void IndustrialHierarchy_ContainsMonitoringControlsStabilityAndConditionalWarning()
        {
            foreach (string label in new[] { "POWER RELAY A", "STABILIZE OUTPUT", "SYSTEM MONITORING", "MANUAL CONTROL", "STABILITY" })
                StringAssert.Contains(label, _source);
            StringAssert.Contains("_stabilizationWarningPanel.gameObject.SetActive(faultVisible", _source);
            StringAssert.Contains("_stabilizationStop.gameObject.SetActive(state.IsRunning", _source);
            StringAssert.Contains("_stabilizationGauges[i].rectTransform.anchorMax", _source);
            StringAssert.Contains("_stabilizationSafeBands[i].rectTransform.anchorMin", _source);
            StringAssert.Contains("slider.fillRect = fill.rectTransform", _source);
        }

        [Test]
        public void PlayerText_DoesNotExposeAnswersOrIdleDiagnosticClutter()
        {
            foreach (string clutter in new[] { "SolvedControls", "TargetNotch", "NOMINAL RANGE", "OVERLOAD: CLEAR", "NO ACTIVE FAULT", "RECOVER MANUALLY", "ECHO FACILITY", "Reduce Generator" })
                StringAssert.DoesNotContain(clutter, _source);
        }

        [Test]
        public void IndustrialSprites_ExistAndUseOnlyScopedSubtreeRebuild()
        {
            const string art = "Assets/_Project/UI/BunkerSurvivalUI/Sprites/";
            foreach (string path in new[] { "NineSlice/panel_industrial_main_normal_9slice.png", "NineSlice/frame_security_terminal_normal_9slice.png",
                "NineSlice/button_primary_normal_9slice.png", "NineSlice/button_danger_normal_9slice.png", "Icons/icon_hazard_sign.png" })
                Assert.IsTrue(File.Exists(_prefix + art + path), path);
            StringAssert.Contains("DestroyImmediate(_stabilizationSurface.gameObject)", _source);
            StringAssert.DoesNotContain("DestroyImmediate(panelRoot", _source);
            string builder = File.ReadAllText(_prefix + "Assets/Editor/RelayASetupBuilder.cs");
            StringAssert.Contains("Update Relay A Stabilization UI", builder);
            StringAssert.DoesNotContain("DestroyImmediate(existing.gameObject)", builder);
        }
    }
}
